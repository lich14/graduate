using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Define.DBDefine.Schedule;
using ZECS.Schedule.Define;
using SharpSim;
using SSWPF.Define;
using ZECS.Schedule.DB;

namespace SSWPF.SimManagers
{
    /// <summary>
    /// 此类用于存放与TOS有关的逻辑，包括 ResJob和 Task 的生成，TOS 向 Schedule 接口的更新，以及卸箱位置的指定等等
    /// </summary>
    public class SimTosManager
    {
        public event EventHandler<PilesReclaimAndReleaseEventArgs> PileReclaimAndReleaseEvent;
        public event EventHandler<ProjectToInfoFrameEventArgs> ProjectToInfoFrameEvent;

        private SimDataStore oSimDataStore;
        public bool IsInited;
        private int MoveNumForseePerQC = 30;
        private ProjectPackageToInfoFrame oPPTInfoFrame;
        
        public SimTosManager()
        {
        }

        public SimTosManager(SimDataStore oSimDataStore)
            :this()
        {
            this.oSimDataStore = oSimDataStore;
        }

        public bool Init()
        {
            if (this.oSimDataStore == null)
            {
                Logger.Simulate.Error("SimTosManager: Null SimDataStore!");
                return false;
            }

            this.IsInited = true;
            return true;
        }

        /// <summary>
        /// 释放出一小时内的工作量，按照 30 move/h计算。包括 WQ、WI 和 ResJob
        /// 后续应该要更改。实际 ResJob 和 Task 的生成应该由人控制
        /// </summary>
        public void RenewTosOutput()
        {
            string TempWQ;
            int MoveNum;
            double AccuMoveNum;
            DateTime CurrStartDt;
            List<STS_ResJob> lSTSResJobs;
            ResJobStatus eResJobStatus;

            if (!this.IsInited)
                return;

            if (this.oSimDataStore.dViewBerthStatus.Values.Count(u => u.VESSEL_VISIT_PHASE == StatusEnums.VesselVisitPhrase.AtBerthDoing.ToString()) == 0)
                return;

            this.oPPTInfoFrame = new ProjectPackageToInfoFrame();

            // 对于现有的尚未完成的 ResJob，统计作业量
            // 若作业量不满足，按顺序逐渐放出 WQ
            // 对于所有放出的 WQ 更新起止时间，WI 的起止时间与其所属 WQ 相同，具体顺序交给调度决定
            foreach (uint iKey in this.oSimDataStore.dQCs.Keys)
            {
                CurrStartDt = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                lSTSResJobs = this.oSimDataStore.dSTSResJobs.Values.Where(u => Convert.ToUInt32(u.CHE_ID) == iKey).ToList();

                AccuMoveNum = 0;
                TempWQ = "";
                foreach (STS_ResJob oRS in lSTSResJobs)
                {
                    Enum.TryParse<ResJobStatus>(oRS.JOB_STATUS, out eResJobStatus);
                    if (eResJobStatus == ResJobStatus.Cancel)
                        continue;
                    if (oRS.JOB_LINK != null && oRS.JOB_LINK.Length > 0)
                        AccuMoveNum++;
                    else
                        AccuMoveNum = AccuMoveNum + 2;

                    if (TempWQ.Length == 0
                        || this.oSimDataStore.dViewWorkQueues[this.oSimDataStore.dViewWorkInstructions[oRS.CONTAINER_ID].WORK_QUEUE].WQ_SEQ > this.oSimDataStore.dViewWorkQueues[TempWQ].WQ_SEQ)
                        TempWQ = this.oSimDataStore.dViewWorkInstructions[oRS.CONTAINER_ID].WORK_QUEUE;
                }

                if (AccuMoveNum % 2 == 1)
                    AccuMoveNum = (AccuMoveNum + 1) / 2;
                else
                    AccuMoveNum = AccuMoveNum / 2;

                while (AccuMoveNum < this.MoveNumForseePerQC)
                {
                    TempWQ = this.FindNextWQ(iKey, TempWQ);
                    if (TempWQ.Length > 0)
                    {
                        // 暂时先一个 WQ 全部扔下去。注意更新 WI 的 PREDICESSOR
                        MoveNum = this.ReleaseJobsOfWQ(TempWQ);
                        this.RenewStartAndEndTimes(TempWQ, MoveNum, ref CurrStartDt);
                        AccuMoveNum = AccuMoveNum + MoveNum;
                    }
                    else
                        break;
                }
            }

            this.oPPTInfoFrame.lWIs = this.oSimDataStore.dViewWorkInstructions.Values.ToList();
            this.oPPTInfoFrame.lWQs = this.oSimDataStore.dViewWorkQueues.Values.ToList();

            this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTInfoFrame = this.oPPTInfoFrame
                });

            this.oPPTInfoFrame = null;
        }


        // 寻找岸桥的下一个WQ
        private string FindNextWQ(uint QCID, string CurrWQ)
        {
            string NextWQ = "";

            foreach (string str in oSimDataStore.dWorkQueues.Keys)
            {
                if (Convert.ToUInt32(oSimDataStore.dWorkQueues[str].QC_ID) == QCID && oSimDataStore.dWorkQueues[str].WQ_STATUS != WQ_Status.EMPTY
                    && (CurrWQ.Length == 0 || oSimDataStore.dWorkQueues[str].WQ_SEQ > oSimDataStore.dWorkQueues[CurrWQ].WQ_SEQ))
                    if (NextWQ.Length == 0 || oSimDataStore.dWorkQueues[str].WQ_SEQ < oSimDataStore.dWorkQueues[NextWQ].WQ_SEQ)
                        NextWQ = str;
            }

            return NextWQ;
        }

        // 统计目前 ResJob 列表中某 WQ 未完成的任务数量
        private int CountResJobsInMoves(string TempWQ)
        {
            int dRet = 0;

            foreach (STS_ResJob obj in oSimDataStore.dSTSResJobs.Values)
            {
                if (this.oSimDataStore.dWorkInstructions[obj.CONTAINER_ID].WORK_QUEUE == TempWQ &&
                        (obj.JOB_STATUS != "Completed" || obj.JOB_STATUS != "Rejected" || obj.JOB_STATUS != "Abort" || obj.JOB_STATUS != "Cancel"))
                {
                    if (obj.JOB_LINK != null && obj.JOB_LINK.Length > 0) 
                        dRet = dRet + 1;
                    else 
                        dRet = dRet + 2;
                }
            }

            if (dRet % 2 == 0)
                dRet = dRet / 2;
            else
                dRet = (dRet + 1) / 2;

            return dRet;
        }

        // 根据指定的 WQ, WI ，生成对应的 ResJob
        private int ReleaseJobsOfWQ(string TempWQ)
        {
            int Amount, LogicalRow, LogicalRow2;
            STS_ResJob oSTSResJob;
            List<string> lContIDs;
            List<WORK_INSTRUCTION_STATUS> lWIs;
            SimContainerInfo oContInfo;
            List<SimContainerInfo> lTempContInfos;
            StatusEnums.BerthWay eBerthWay;
            bool bZeroRow;
            string StrPhysical, StrLogical, StrLogical2;

            lContIDs = this.oSimDataStore.dViewWorkInstructions.Where(u => u.Value.WORK_QUEUE == TempWQ).Select(u => u.Key).ToList();
            Amount = 0;
            foreach (string ContID in lContIDs)
            {
                this.GenerateResJobsAndTasks(ContID);
                if (this.oSimDataStore.dViewWorkInstructions[ContID].LIFT_REFERENCE != null && this.oSimDataStore.dViewWorkInstructions[ContID].LIFT_REFERENCE.Length > 0) 
                    Amount = Amount + 1;
                else 
                    Amount = Amount + 2;

                if (this.oSimDataStore.dViewWorkInstructions[ContID].LIFT_REFERENCE != null && this.oSimDataStore.dViewWorkInstructions[ContID].LIFT_REFERENCE.Length > 0)
                {
                    oSTSResJob = this.oSimDataStore.dSTSResJobs.Values.First(u => u.CONTAINER_ID == ContID);
                    if (oSTSResJob.JOB_LINK == null || oSTSResJob.JOB_LINK.Length == 0)
                        oSTSResJob.JOB_LINK = this.oSimDataStore.dWorkInstructions[ContID].LIFT_REFERENCE;

                    oSTSResJob = this.oSimDataStore.dSTSResJobs.Values.First(u => u.CONTAINER_ID == this.oSimDataStore.dViewWorkInstructions[ContID].LIFT_REFERENCE);
                    if (oSTSResJob.JOB_LINK == null || oSTSResJob.JOB_LINK.Length == 0)
                        oSTSResJob.JOB_LINK = ContID;
                }
            }

            if (Amount % 2 == 0)
                Amount = Amount / 2;
            else
                Amount = (Amount + 1) / 2;

            // PREDECESSORS
            if (this.oSimDataStore.dViewWorkQueues[TempWQ].MOVE_KIND == Move_Kind.LOAD)
            {
                lWIs = this.oSimDataStore.dViewWorkInstructions.Values.Where(u => u.WORK_QUEUE == TempWQ).ToList();
                foreach (WORK_INSTRUCTION_STATUS oWI in lWIs)
                {
                    StrPhysical = "";
                    StrLogical = "";
                    StrLogical2 = "";
                    oContInfo = this.oSimDataStore.dSimContainerInfos[oWI.CONTAINER_ID];
                    lTempContInfos = this.oSimDataStore.dSimContainerInfos.Values.Where(u => u.VoyageNo == oContInfo.VoyageNo 
                        && Math.Abs(u.StowBay - oContInfo.StowBay) <= 1 && u.StowRow == oContInfo.StowRow && u.StowTier == oContInfo.StowTier - 2).ToList();

                    if (lTempContInfos.Count > 0)
                    {
                        if (lTempContInfos.Count > 1)
                            Logger.Simulate.Error("TosManager : Multiple Physical Predecessors Found");
                        StrPhysical = lTempContInfos[0].ContainerID;
                    }

                    if (oContInfo.StowTier >= 80)
                    {
                        lTempContInfos = this.oSimDataStore.dSimContainerInfos.Values.Where(u => u.VoyageNo == oContInfo.VoyageNo && u.StowRow == 0).ToList();
                        if (lTempContInfos.Count > 0)
                            bZeroRow = true;
                        else
                            bZeroRow = false;
                        eBerthWay = this.oSimDataStore.dVessels[oContInfo.VesNo].eBerthWay;
                        LogicalRow = this.CalcLogicalPredRow(oContInfo.StowRow, eBerthWay, bZeroRow);
                        LogicalRow2 = this.CalcLogicalPredRow(LogicalRow, eBerthWay, bZeroRow);
                        lTempContInfos = this.oSimDataStore.dSimContainerInfos.Values.Where(u => u.VoyageNo == oContInfo.VoyageNo
                            && Math.Abs(u.StowBay - oContInfo.StowBay) <= 1 && u.StowRow == LogicalRow && u.StowTier == oContInfo.StowTier + 2).ToList();
                        if (lTempContInfos.Count > 0)
                        {
                            if (lTempContInfos.Count > 1)
                                Logger.Simulate.Error("TosManager : Multiple Logical Predecessors Found");
                            StrLogical = lTempContInfos[0].ContainerID;

                            lTempContInfos = this.oSimDataStore.dSimContainerInfos.Values.Where(u => u.VoyageNo == oContInfo.VoyageNo
                                && Math.Abs(u.StowBay - oContInfo.StowBay) <= 1 && u.StowRow == LogicalRow2 && u.StowTier == oContInfo.StowTier + 4).ToList();
                            if (lTempContInfos.Count > 0)
                            {
                                if (lTempContInfos.Count > 1)
                                    Logger.Simulate.Error("TosManager : Multiple Logical Predecessors 2 Found");
                                StrLogical2 = lTempContInfos[0].ContainerID;
                            }
                        }
                    }

                    oWI.PHYSICAL_PREDECESSOR = StrPhysical;

                    if (StrLogical.Length > 0 && StrLogical2.Length > 0)
                        oWI.LOGICAL_PREDECESSOR = StrLogical + ";" + StrLogical2;
                    else if (StrLogical.Length > 0 && StrPhysical.Length > 0)
                        oWI.LOGICAL_PREDECESSOR = StrLogical + ";" + StrPhysical;
                    else if (StrLogical.Length > 0)
                        oWI.LOGICAL_PREDECESSOR = StrLogical;
                    else if (StrPhysical.Length > 0)
                        oWI.LOGICAL_PREDECESSOR = StrPhysical;
                    else
                        oWI.LOGICAL_PREDECESSOR = "";
                }
            }
            
            return Amount;
        }

        // 计算逻辑上在前的列
        private int CalcLogicalPredRow(int CurrRow, StatusEnums.BerthWay eBerthWay, bool bZeroRow)
        {
            int PredRow = -1;

            switch (eBerthWay)
            {
                case StatusEnums.BerthWay.L:
                    if (CurrRow % 2 == 0)
                    {
                        if (bZeroRow && CurrRow == 0)
                            PredRow = 1;
                        else
                            PredRow = CurrRow - 2;
                    }
                    else
                        CurrRow = CurrRow + 2;
                    break;
                case StatusEnums.BerthWay.R:
                    if (CurrRow % 2 == 1)
                        if (CurrRow > 1)
                            PredRow = CurrRow - 2;
                        else
                        {
                            if (bZeroRow)
                                PredRow = 0;
                            else
                                PredRow = 2;
                        }
                    else
                        PredRow = PredRow + 2;
                    break;
                default:
                    break;
            }

            return PredRow;
        }

        // 更新 WQ 和 所属 WI 的起止时间。整个 WQ 往下扔，不用考虑 MOVE_STAGE
        private void RenewStartAndEndTimes(string TempWQ, double MoveNum, ref DateTime CurrStartDt)
        {
            List<WORK_INSTRUCTION_STATUS> lWIs;

            this.oSimDataStore.dViewWorkQueues[TempWQ].START_TIME = CurrStartDt;
            
            lWIs = this.oSimDataStore.dViewWorkInstructions.Values.Where(u => u.WORK_QUEUE == TempWQ).OrderBy(u => u.ORDER_SEQ).ToList();

            foreach (WORK_INSTRUCTION_STATUS oWI in lWIs)
            {
                oWI.T_StartTime = CurrStartDt;
                CurrStartDt = CurrStartDt.AddSeconds(120);
                oWI.T_EndTime = CurrStartDt;
            }

            this.oSimDataStore.dViewWorkQueues[TempWQ].END_TIME = CurrStartDt;
        }

        // 更新某 WQ 的所有 WI 的起止时间，更新前所有 WI 已经投射。总之我先给出来，用不用以及怎么用我不管便是。
        private void RenewStartAndEndTimesOfWIsInWQ(string sWQ, int MoveNum, DateTime StartDt)
        {
            int Ord = 0;
            int ActOrd = 0;
            int CurrOrd;
            while (Ord < MoveNum)
            {
                CurrOrd = -1;

                foreach (WORK_INSTRUCTION_STATUS obj in oSimDataStore.dViewWorkInstructions.Values)
                {
                    if (obj.WORK_QUEUE == sWQ && obj.MOVE_STAGE != Move_Stage.COMPLETE && obj.ORDER_SEQ > ActOrd)
                    {
                        if (CurrOrd == -1 || obj.ORDER_SEQ < CurrOrd) CurrOrd = obj.ORDER_SEQ;
                    }
                }

                if (CurrOrd < 0) break;

                foreach (WORK_INSTRUCTION_STATUS obj in oSimDataStore.dViewWorkInstructions.Values)
                {
                    if (obj.WORK_QUEUE == sWQ && obj.ORDER_SEQ == CurrOrd)
                    {
                        obj.T_StartTime = StartDt.AddSeconds(120 * Ord);
                        obj.T_EndTime = StartDt.AddSeconds(120 * (Ord + 1));
                    }
                }

                ActOrd = CurrOrd;
                Ord++;
            }
        }

        // 生成 ResJob 和 Task
        private void GenerateResJobsAndTasks(string ContID)
        {
            WORK_INSTRUCTION_STATUS oWI;

            oWI = this.oSimDataStore.dViewWorkInstructions[ContID];
            if (oWI.JOB_ID != null && oWI.JOB_ID.Length > 0)
                return;

            long JobID = DB_ECS.Instance.GetNewIndexNum(StatusEnums.IndexType.JobID);
            long TaskID = DB_ECS.Instance.GetNewIndexNum(StatusEnums.IndexType.TaskID);
            oWI.JOB_ID = JobID.ToString();
            this.GenerateSTSResJob(oWI, JobID);
            this.GenerateSTSTask(oWI, JobID, TaskID);
            this.GenerateAGVResJob(oWI, JobID);
            this.GenerateAGVTask(oWI, JobID, TaskID);
            // ASCResJob 生成，但仅到AGV启动以后才变成Ready
            this.GenerateASCResJob(oWI, JobID);
            this.GenerateASCTask(oWI, JobID, TaskID);
        }

        // 生成岸桥的 ResJob
        private void GenerateSTSResJob(WORK_INSTRUCTION_STATUS oWI, long JobID)
        {
            STS_ResJob oResJob = new STS_ResJob();
            string ContID = oWI.CONTAINER_ID;
            string ContISO = oWI.CONTAINER_ISO;

            oResJob.CHE_ID = oWI.POINT_OF_WORK;
            oResJob.CONTAINER_DOOR_DIRECTION = oWI.DOOR_DIRECTION.ToString();
            oResJob.CONTAINER_HEIGHT = this.oSimDataStore.dISORefs[ContISO].ContainerHeightCM.ToString();
            oResJob.CONTAINER_ID = ContID;
            if (this.oSimDataStore.dSimContainerInfos[ContID].eEF == StatusEnums.EF.E) 
                oResJob.CONTAINER_IS_EMPTY = "1";
            else 
                oResJob.CONTAINER_IS_EMPTY = "0";
            oResJob.CONTAINER_ISO = ContISO;
            oResJob.CONTAINER_LENGTH = oWI.CONTAINER_LENGTH_CM.ToString();
            oResJob.CONTAINER_WEIGHT = oWI.CONTAINER_WEIGHT_KG.ToString();
            oResJob.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            oResJob.ID = JobID;
            oResJob.JOB_ID = JobID.ToString();
            oResJob.JOB_STATUS = ResJobStatus.New.ToString();
            oResJob.MESSAGE_ID = "";
            oResJob.VERSION = 1;
            oResJob.VESSEL_ID = this.oSimDataStore.dSimContainerInfos[ContID].VesNo.ToString();
            oResJob.QUAY_ID = oWI.POINT_OF_WORK;

            if (oWI.MOVE_KIND == Move_Kind.DSCH)
            {
                oResJob.JOB_TYPE = "DISC";
                oResJob.FROM_BAY_TYPE = "QC";
                oResJob.FROM_BAY = this.oSimDataStore.dSimContainerInfos[ContID].VesBay.ToString();
                oResJob.FROM_LANE = this.oSimDataStore.dSimContainerInfos[ContID].VesRow.ToString();
                oResJob.FROM_TIER = this.oSimDataStore.dSimContainerInfos[ContID].VesTier.ToString();
                oResJob.TO_BAY_TYPE = "Yard";
            }
            else if (oWI.MOVE_KIND == Move_Kind.LOAD)
            {
                oResJob.JOB_TYPE = "LOAD";
                oResJob.FROM_BAY_TYPE = "WS";
                oResJob.FROM_BAY = this.oSimDataStore.dSimContainerInfos[ContID].YardBay.ToString();
                oResJob.FROM_LANE = this.oSimDataStore.dSimContainerInfos[ContID].YardRow.ToString();
                oResJob.FROM_TIER = this.oSimDataStore.dSimContainerInfos[ContID].YardTier.ToString();
                oResJob.TO_BAY_TYPE = "QC";
                oResJob.TO_BAY = this.oSimDataStore.dSimContainerInfos[ContID].StowBay.ToString();
                oResJob.TO_LANE = this.oSimDataStore.dSimContainerInfos[ContID].StowRow.ToString();
                oResJob.TO_TIER = this.oSimDataStore.dSimContainerInfos[ContID].StowTier.ToString();
                oResJob.YARD_ID = this.oSimDataStore.dSimContainerInfos[ContID].YardBlock;
            }

            this.oSimDataStore.dSTSResJobs.Add(oResJob.ID, oResJob);
            if (this.oPPTInfoFrame.lSTSResJobs == null)
                this.oPPTInfoFrame.lSTSResJobs = new List<STS_ResJob>();
            this.oPPTInfoFrame.lSTSResJobs.Add(oResJob);
        }

        // 生成岸桥的 Task
        private void GenerateSTSTask(WORK_INSTRUCTION_STATUS oWI, long JobID, long TaskID)
        {
            STS_Task oTask = new STS_Task();
            string ContID = oWI.CONTAINER_ID;

            oTask.ID = TaskID;
            oTask.LastContainerLocation = "";
            oTask.Task = this.oSimDataStore.dSTSResJobs[JobID];
            oTask.TaskState = TaskStatus.None;

            if (oWI.MOVE_KIND == Move_Kind.DSCH)
            {
                oTask.ContainerLocation = this.oSimDataStore.dSimContainerInfos[ContID].VesLoc;
            }
            else if (oWI.MOVE_KIND == Move_Kind.LOAD)
            {
                oTask.ContainerLocation = this.oSimDataStore.dSimContainerInfos[ContID].YardLoc;
            }

            this.oSimDataStore.dSTSTasks.Add(oTask.ID, oTask);
            if (this.oPPTInfoFrame.lSTSTasks == null)
                this.oPPTInfoFrame.lSTSTasks = new List<STS_Task>();
            this.oPPTInfoFrame.lSTSTasks.Add(oTask);
        }

        // 生成 ASC 的 ResJob
        public void GenerateASCResJob(WORK_INSTRUCTION_STATUS oWI, long JobID)
        {
            ASC_ResJob oResJob = new ASC_ResJob();
            string ContID = oWI.CONTAINER_ID;
            string ContISO = oWI.CONTAINER_ISO;

            oResJob.CONTAINER_DOOR_DIRECTION = oWI.DOOR_DIRECTION.ToString();
            oResJob.CONTAINER_HEIGHT = this.oSimDataStore.dISORefs[ContISO].ContainerHeightCM.ToString();
            oResJob.CONTAINER_ID = ContID;
            if (this.oSimDataStore.dSimContainerInfos[ContID].eEF == StatusEnums.EF.E)
                oResJob.CONTAINER_IS_EMPTY = "1";
            else if (this.oSimDataStore.dSimContainerInfos[ContID].eEF == StatusEnums.EF.F) 
                oResJob.CONTAINER_IS_EMPTY = "0";
            oResJob.CONTAINER_ISO = oWI.CONTAINER_ISO;
            oResJob.CONTAINER_LENGTH = oWI.CONTAINER_LENGTH_CM.ToString();
            oResJob.CONTAINER_WEIGHT = oWI.CONTAINER_WEIGHT_KG.ToString();
            oResJob.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            oResJob.ID = JobID;
            oResJob.JOB_ID = JobID.ToString();
            oResJob.JOB_STATUS = ResJobStatus.New.ToString();
            oResJob.VERSION = 1;
            oResJob.QUAY_ID = oWI.POINT_OF_WORK;

            if (oWI.MOVE_KIND == Move_Kind.LOAD)
            {
                oResJob.JOB_TYPE = "LOAD";
                oResJob.YARD_ID = this.oSimDataStore.dSimContainerInfos[ContID].YardBlock.ToString();
                foreach (ASC obj in this.oSimDataStore.dASCs.Values)
                {
                    if (obj.BlockName == oResJob.YARD_ID && obj.eSide == StatusEnums.ASCSide.WS)
                    {
                        oResJob.CHE_ID = obj.ID.ToString();
                        break;
                    }
                }
                oResJob.FROM_BAY = this.oSimDataStore.dSimContainerInfos[ContID].YardBay.ToString();
                oResJob.FROM_BAY_TYPE = "BLOCK";
                oResJob.FROM_BLOCK = this.oSimDataStore.dSimContainerInfos[ContID].YardBlock.Trim();
                oResJob.FROM_LANE = this.oSimDataStore.dSimContainerInfos[ContID].YardRow.ToString();
                oResJob.FROM_TIER = this.oSimDataStore.dSimContainerInfos[ContID].YardTier.ToString();
                oResJob.TO_BAY_TYPE = "WS";
                oResJob.TO_BLOCK = this.oSimDataStore.dSimContainerInfos[ContID].YardBlock.Trim();
                oResJob.YARD_ID = this.oSimDataStore.dSimContainerInfos[ContID].YardBlock;
            }
            else if (oWI.MOVE_KIND == Move_Kind.DSCH)
            {
                oResJob.JOB_TYPE = "DISC";
                oResJob.FROM_BAY_TYPE = "WS";
                oResJob.TO_BAY_TYPE = "BLOCK";
            }

            this.oSimDataStore.dASCResJobs.Add(oResJob.ID, oResJob);
            if (this.oPPTInfoFrame.lASCResJobs == null)
                this.oPPTInfoFrame.lASCResJobs = new List<ASC_ResJob>();
            this.oPPTInfoFrame.lASCResJobs.Add(oResJob);
        }

        // 生成 ASC 的 Task
        public void GenerateASCTask(WORK_INSTRUCTION_STATUS oWI, long JobID, long TaskID)
        {

            ASC_Task oTask = new ASC_Task();

            oTask.ID = TaskID;
            oTask.Task = this.oSimDataStore.dASCResJobs[JobID];
            oTask.TaskState = TaskStatus.None;

            this.oSimDataStore.dASCTasks.Add(oTask.ID, oTask);
            if (this.oPPTInfoFrame.lASCTasks == null)
                this.oPPTInfoFrame.lASCTasks = new List<ASC_Task>();
            this.oPPTInfoFrame.lASCTasks.Add(oTask);
        }

        // 生成 AGV 的 ResJob
        private void GenerateAGVResJob(WORK_INSTRUCTION_STATUS oWI, long JobID)
        {
            AGV_ResJob oResJob = new AGV_ResJob();
            string ContID = oWI.CONTAINER_ID;
            string ContISO = oWI.CONTAINER_ISO;

            oResJob.CONTAINER_DOOR_DIRECTION = oWI.DOOR_DIRECTION.ToString();
            oResJob.CONTAINER_HEIGHT = this.oSimDataStore.dISORefs[ContISO].ContainerHeightCM.ToString();
            oResJob.CONTAINER_ID = ContID;
            if (this.oSimDataStore.dSimContainerInfos[ContID].eEF == StatusEnums.EF.E)
                oResJob.CONTAINER_IS_EMPTY = "1";
            else if (this.oSimDataStore.dSimContainerInfos[ContID].eEF == StatusEnums.EF.F)
                oResJob.CONTAINER_IS_EMPTY = "0";
            oResJob.CONTAINER_ISO = ContISO;
            oResJob.CONTAINER_LENGTH = oWI.CONTAINER_LENGTH_CM.ToString();
            oResJob.CONTAINER_WEIGHT = oWI.CONTAINER_WEIGHT_KG.ToString();
            oResJob.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            oResJob.ID = JobID;
            oResJob.JOB_ID = JobID.ToString();
            oResJob.JOB_STATUS = ResJobStatus.New.ToString();
            oResJob.VERSION = 1;
            oResJob.QUAY_ID = oWI.POINT_OF_WORK;

            if (oWI.MOVE_KIND == Move_Kind.DSCH)
            {
                oResJob.JOB_TYPE = "DISC";
                oResJob.FROM_BAY_TYPE = "QC";
                oResJob.FROM_BLOCK = this.oSimDataStore.dWorkInstructions[ContID].POINT_OF_WORK;
                oResJob.FROM_BAY = this.oSimDataStore.dSimContainerInfos[ContID].VesBay.ToString();
                oResJob.TO_BAY_TYPE = "WS";
            }
            else if (oWI.MOVE_KIND == Move_Kind.LOAD)
            {
                oResJob.JOB_TYPE = "LOAD";
                oResJob.FROM_BAY_TYPE = "WS";
                oResJob.FROM_BLOCK = this.oSimDataStore.dSimContainerInfos[ContID].YardBlock;
                oResJob.TO_BAY_TYPE = "QC";
                oResJob.TO_BLOCK = this.oSimDataStore.dWorkInstructions[ContID].POINT_OF_WORK;
                oResJob.TO_BAY = this.oSimDataStore.dSimContainerInfos[ContID].StowBay.ToString();
                oResJob.YARD_ID = this.oSimDataStore.dSimContainerInfos[ContID].YardBlock;
            }

            this.oSimDataStore.dAGVResJobs.Add(oResJob.ID, oResJob);
            if (this.oPPTInfoFrame.lAGVResJobs == null)
                this.oPPTInfoFrame.lAGVResJobs = new List<AGV_ResJob>();
            this.oPPTInfoFrame.lAGVResJobs.Add(oResJob);
        }

        // 生成 AGV 的 Task
        private void GenerateAGVTask(WORK_INSTRUCTION_STATUS oWI, long JobID, long TaskID)
        {
            AGV_Task oTask = new AGV_Task();
            oTask.ID = TaskID;
            oTask.Task = this.oSimDataStore.dAGVResJobs[JobID];
            oTask.TaskState = TaskStatus.None;

            this.oSimDataStore.dAGVTasks.Add(oTask.ID, oTask);
            if (this.oPPTInfoFrame.lAGVTasks == null)
                this.oPPTInfoFrame.lAGVTasks = new List<AGV_Task>();
            this.oPPTInfoFrame.lAGVTasks.Add(oTask);
        }

        /// <summary>
        /// 为箱号列表内的集装箱分配堆场位置
        /// <param name="sender">一般为SimAGVManager</param>
        /// <param name="e">AlloPlanLocsForDiscContsEventArgs</param>
        /// </summary>
        public void OnAlloPlanPlacsForDiscConts(object sender, AlloPlanLocsForDiscContsEventArgs e)
        {
            e.IsSucc = true;
            foreach (string ContID in e.lContIDs)
            {
                if (this.oSimDataStore.dSimContainerInfos.ContainsKey(ContID))
                {
                    if (string.IsNullOrWhiteSpace(this.oSimDataStore.dSimContainerInfos[ContID].PlanLoc)
                        && !this.GenerateInboundYardLocForCont(ContID))
                        e.IsSucc = false;
                }
            }

            return;
        }

        /// <summary>
        /// 为指定的卸船箱分配堆场位置
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool GenerateInboundYardLocForCont(string ContID)
        {
            StatusEnums.ContType eContType;
            StatusEnums.ContSize eContSize;
            PlanGroup oPG;
            List<PlanRange> lPRs;
            List<PlanPlac> lPPs;
            SimContainerInfo oContInfo;
            WORK_INSTRUCTION_STATUS oWI;
            AGV_Task oAgvTask;
            ASC_Task oAscTask;
            string PileName;
            PilesReclaimAndReleaseEventArgs e;
            Pile oPile;

            if (!this.oSimDataStore.dSimContainerInfos.ContainsKey(ContID))
                return false;

            eContSize = this.oSimDataStore.dSimContainerInfos[ContID].eSize;
            eContType = this.oSimDataStore.dSimContainerInfos[ContID].eType;

            oPG = this.oSimDataStore.dPlanGroups.Values.First(u => u.eContSize == eContSize && u.eContType == eContType && u.UsedNum < u.TotalNum);
            if (oPG == null)
                return false;

            lPRs = this.oSimDataStore.dPlanRanges.Values.Where(u => u.PlanGroupName == oPG.Name && u.UsedNum < u.TotalNum).OrderBy(u => u.SeqNo).ToList();
            if (lPRs.Count == 0)
                return false;

            lPPs = this.oSimDataStore.dPlanPlacs.Values.Where(u => u.PlanRangeName == lPRs[0].Name && !u.IsUsed).OrderBy(u => u.OrderID).ToList();
            if (lPPs.Count == 0)
                return false;

            lPPs[0].IsUsed = true;
            lPRs[0].UsedNum++;
            oPG.UsedNum++;

            // 更新ContainerInfo、WI、AGV_ResJob、AGV_Task
            oContInfo = this.oSimDataStore.dSimContainerInfos.Values.FirstOrDefault<SimContainerInfo>(u => u.ContainerID == ContID);
            oWI = this.oSimDataStore.dViewWorkInstructions.Values.FirstOrDefault<WORK_INSTRUCTION_STATUS>(u => u.CONTAINER_ID == ContID);
            oAgvTask = this.oSimDataStore.dAGVTasks.Values.FirstOrDefault<AGV_Task>(u => u.Task.CONTAINER_ID == ContID);
            oAscTask = this.oSimDataStore.dASCTasks.Values.FirstOrDefault<ASC_Task>(u => u.Task.CONTAINER_ID == ContID);

            if (oContInfo == null || oWI == null || oAgvTask == null || oAgvTask.Task == null || oAscTask == null || oAscTask.Task == null)
                return false;

            // 可能需要增加 Pile 来应对放箱
            PileName = oContInfo.PlanBlock + oContInfo.PlanBay.ToString().PadLeft(2, '0') + oContInfo.PlanRow.ToString().PadLeft(2, '0');

            if (!this.oSimDataStore.dPiles.ContainsKey(PileName))
            {
                e = new PilesReclaimAndReleaseEventArgs();
                e.lReclaimMsgs.Add(new PileReclaimMsg() { PileName = PileName, oPileType = this.oSimDataStore.dPileTypes[eContSize] });
                if (this.PileReclaimAndReleaseEvent != null)
                    this.PileReclaimAndReleaseEvent.Invoke(this, e);
                if (!e.IsSucc)
                {
                    return false;
                }
            }

            // 信息更新
            oContInfo.PlanLoc = lPPs[0].Plac;
            oContInfo.PlanBlock = lPPs[0].Plac.Substring(0, 3);
            oContInfo.PlanBay = Convert.ToInt32(lPPs[0].Plac.Substring(3, 2));
            oContInfo.PlanRow = Convert.ToInt32(lPPs[0].Plac.Substring(5, 2));
            oContInfo.PlanTier = Convert.ToInt32(lPPs[0].Plac.Substring(7, 1));

            oWI.FUTURE_PLAC = lPPs[0].Plac;
            oWI.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

            oAgvTask.Task.TO_BAY_TYPE = "WS";
            oAgvTask.Task.TO_BLOCK = oContInfo.PlanBlock;
            oAgvTask.Task.VERSION++;
            oAgvTask.Task.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

            oAscTask.Task.TO_BAY_TYPE = "BLOCK";
            oAscTask.Task.TO_BLOCK = oContInfo.PlanBlock;
            oAscTask.Task.TO_BAY = oContInfo.PlanBay.ToString();
            oAscTask.Task.TO_LANE = oContInfo.PlanRow.ToString();
            oAscTask.Task.TO_TIER = oContInfo.PlanTier.ToString();
            oAscTask.Task.YARD_ID = oContInfo.PlanBlock;
            oAscTask.Task.VERSION++;
            oAscTask.Task.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

            return true;
        }

    }
}
