using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using SSWPF.Define;
using ZECS.Schedule.DB;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.DBDefine.YardMap;
using SharpSim;
using ZECS.Schedule.Define;

namespace SSWPF.SimManagers
{
    /// <summary>
    /// 控制AGV的起止车道
    /// </summary>
    public class SimAGVManager
    {
        public event EventHandler<AlloPlanLocsForDiscContsEventArgs> AlloPlanLocsForDiscContsEvent;
        public event EventHandler<GenerateAGVRouteEventArgs> GenerateAGVRouteEvent;
        public event EventHandler<ProjectToViewFrameEventArgs> ProjectToViewFrameEvent;
        public event EventHandler<ProjectToInfoFrameEventArgs> ProjectToInfoFrameEvent;

        private SimDataStore oSimDataStore;
        public bool IsInited;

        public SimAGVManager()
        {
        }

        public SimAGVManager(SimDataStore oSimDataStore)
            : this()
        {
            this.oSimDataStore = oSimDataStore;
        }

        /// <summary>
        /// 初始化函数
        /// </summary>
        /// <returns></returns>
        public bool Init()
        {
            if (this.oSimDataStore == null)
            {
                Logger.Simulate.Error("SimAGVManager: Null SimDataStore!");
                return false;
            }

            if (this.oSimDataStore.dAGVs == null || this.oSimDataStore.dAGVs.Count == 0)
            {
                Logger.Simulate.Error("SimAGVManager: No AGV Existed!");
                return false;
            }

            this.IsInited = true;
            return true;
        }

        /// <summary>
        /// 对于所有已发任务但没开始作业的AGV，生成Command，规划路径并开始移动
        /// </summary>
        /// <param name="oPPTViewFrame">与新路径有关的投射</param>
        /// <returns>若有AGV启动返回true，否则返回false</returns>
        public bool PushForwardAGVProcedure(out ProjectPackageToViewFrame oPPTViewFrame)
        {
            bool bRet = false;
            List<AGV_Command> lCurrAGVCommands, lAGVCommandsToMoveOn;
            List<AGV> lTempAGVs;
            List<AGV_Order> lNewAGVOrdersToExecute;
            AGV oAGV;
            AGV_STATUS oAGVStatus;
            AGV_Command oAGVComm;
            AlloPlanLocsForDiscContsEventArgs d;
            GenerateAGVRouteEventArgs e;

            oPPTViewFrame = new ProjectPackageToViewFrame();

            // 按照流程走。装船流程优先

            /*卸船流程*/

            // 卸船新任务，对新发布的 Order 配齐 Command，并将 AGV 转入 ToQCPBIn 的 Ready。岸桥或者堆场不明确则抛异常。
            lCurrAGVCommands = this.oSimDataStore.dAGVCommands.Values.Where(u => u.JOB_TYPE == JobType.DISC.ToString()).ToList();
            lTempAGVs = this.oSimDataStore.dAGVs.Values.Where(u => u.eAGVStage == StatusEnums.AGVWorkStage.Null && u.eMotionStatus == StatusEnums.MotionStatus.Free
                && !lCurrAGVCommands.Exists(v => !string.IsNullOrWhiteSpace(v.CHE_ID) && Convert.ToUInt32(v.CHE_ID) == u.ID)).ToList();
            lNewAGVOrdersToExecute = this.oSimDataStore.dAGVOrders.Values.Where(u => u.JOB_STATUS == ResJobStatus.New.ToString() && string.IsNullOrWhiteSpace(u.COMMAND_ID)
                 && !string.IsNullOrWhiteSpace(u.CHE_ID) && lTempAGVs.Exists(v => Convert.ToUInt32(u.CHE_ID) == v.ID)).ToList();
            if (lNewAGVOrdersToExecute.Count > 0)
            {
                bRet = true;
                lAGVCommandsToMoveOn = new List<AGV_Command>();
                foreach (AGV_Order oOrd in lNewAGVOrdersToExecute)
                {
                    oAGV = this.oSimDataStore.dAGVs[Convert.ToUInt32(oOrd.CHE_ID)];
                    oAGV.eJobType = JobType.DISC;
                    oAGV.eAGVStage = StatusEnums.AGVWorkStage.ToQCPBIn;
                    oAGVStatus = this.oSimDataStore.dAGVStatus.Values.FirstOrDefault<AGV_STATUS>(u => !string.IsNullOrWhiteSpace(u.CHE_ID)
                        && Convert.ToUInt32(u.CHE_ID) == oAGV.ID);
                    oAGVStatus.ORDER_GKEY = Convert.ToInt64(oOrd.ORDER_ID);
                    this.GenerateNewAGVCommandFromOrder(oOrd, out oAGVComm);
                    oAGVStatus.COMMAND_GKEY = Convert.ToInt64(oAGVComm.COMMAND_ID);
                    oAGVStatus.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                    if (!string.IsNullOrWhiteSpace(oAGVComm.FROM_BAY_TYPE) && !string.IsNullOrWhiteSpace(oAGVComm.FROM_BLOCK))
                        oAGV.eAGVStageStatus = StatusEnums.ActionStatus.Ready;
                    else
                        throw new Exception("From Location Of Job : " + oOrd.JOB_ID + " To AGV : " + oAGV.ID.ToString() + " Required");
                }
            }

            // 所有 ToQCPBIn，AGVStageStatus 为 Ready 的 AGV，尝试分配 QCPBIn，成功后转入 Doing 并向目标移动。
            lTempAGVs = this.oSimDataStore.dAGVs.Values.Where(u => u.eAGVStage == StatusEnums.AGVWorkStage.ToQCPBIn && u.eAGVStageStatus == StatusEnums.ActionStatus.Ready).ToList();
            
            if (lTempAGVs.Count > 0)
            {
                bRet = true;
                lAGVCommandsToMoveOn = this.oSimDataStore.dAGVCommands.Values.Where(u => lTempAGVs.Exists(v => v.ID == Convert.ToUInt32(u.CHE_ID))).ToList();
                lAGVCommandsToMoveOn.Sort(this.AGVCommandDisCompareFunc);
                foreach (AGV_Command oComm in lAGVCommandsToMoveOn)
                {
                    oAGV = this.oSimDataStore.dAGVs[Convert.ToUInt32(oComm.CHE_ID)];
                    if (this.SearchForAGVAimLane(oAGV, oComm.FROM_BAY_TYPE, oComm.FROM_BLOCK))
                    {
                        e = new GenerateAGVRouteEventArgs()
                        {
                            oA = oAGV,
                            AimLaneID = oAGV.AimLaneID,
                            IsGenerationSucc = false
                        };

                        this.GenerateAGVRouteEvent.Invoke(this, e);

                        if (e.IsGenerationSucc)
                            oAGV.eAGVStageStatus = StatusEnums.ActionStatus.Doing;
                        else
                            oAGV.AimLaneID = 0;
                    }   
                }
            }

            // AGVWorkStage 为 AtQCPBIn，AGVStageStatus 为 Done 的卸船 AGV， 转入 ToQCTP 的 Ready 状态
            lTempAGVs = this.oSimDataStore.dAGVs.Values.Where(u => u.eAGVStage == StatusEnums.AGVWorkStage.AtQCPBIn && u.eAGVStageStatus == StatusEnums.ActionStatus.Done).ToList();
            lTempAGVs.ForEach(u => u.eAGVStage = StatusEnums.AGVWorkStage.ToQCTP);
            lTempAGVs.ForEach(u => u.eAGVStageStatus = StatusEnums.ActionStatus.Ready);

            // AGVWorkStage 为 ToQCTP，AGVStageStatus 为 Ready 的卸船 AGV， 若可以的话开始向 QCTP 移动
            lTempAGVs = this.oSimDataStore.dAGVs.Values.Where(u => u.eAGVStage == StatusEnums.AGVWorkStage.ToQCTP && u.eAGVStageStatus == StatusEnums.ActionStatus.Ready).ToList();
            if (lTempAGVs.Count > 0)
            {
                bRet = true;
                lAGVCommandsToMoveOn = this.oSimDataStore.dAGVCommands.Values.Where(u => lTempAGVs.Exists(v => v.ID == Convert.ToUInt32(u.CHE_ID))).ToList();
                // 早到的 AGV 先上 QCTP
                lAGVCommandsToMoveOn.Sort(this.AGVCommandTimeCompareFuncByStatus);
                // 卸船不管箱，只要有空的 QCTP 就直接开上去
                foreach (AGV_Command oComm in lAGVCommandsToMoveOn)
                {
                    oAGV = this.oSimDataStore.dAGVs[Convert.ToUInt32(oComm.CHE_ID)];
                    if (this.SearchForAGVAimLane(oAGV, oComm.FROM_BAY_TYPE, oComm.FROM_BLOCK))
                    {
                        e = new GenerateAGVRouteEventArgs()
                        {
                            oA = oAGV,
                            AimLaneID = oAGV.AimLaneID,
                            IsGenerationSucc = false
                        };

                        this.GenerateAGVRouteEvent.Invoke(this, e);

                        if (e.IsGenerationSucc)
                            oAGV.eAGVStageStatus = StatusEnums.ActionStatus.Doing;
                        else
                            oAGV.AimLaneID = 0;
                    }
                }
            }

            // AGVWorkStage 为 AtQCTP，AGVStageStatus 为 Done，进入 ToQCPBOut 的 Ready 状态
            lTempAGVs = this.oSimDataStore.dAGVs.Values.Where(u => u.eAGVStage == StatusEnums.AGVWorkStage.AtQCTP && u.eAGVStageStatus == StatusEnums.ActionStatus.Done).ToList();
            lTempAGVs.ForEach(u => u.eAGVStage = StatusEnums.AGVWorkStage.ToQCPBOut);
            lTempAGVs.ForEach(u => u.eAGVStageStatus = StatusEnums.ActionStatus.Ready);

            // AGVWorkStage 为 ToQCPBOut，AGVStageStatus 为 Ready，尝试预约 QCPBOut 并开始向其移动
            lTempAGVs = this.oSimDataStore.dAGVs.Values.Where(u => u.eAGVStage == StatusEnums.AGVWorkStage.ToQCPBOut && u.eAGVStageStatus == StatusEnums.ActionStatus.Ready).ToList();
            if (lTempAGVs.Count > 0)
            {
                bRet = true;
                lAGVCommandsToMoveOn = this.oSimDataStore.dAGVCommands.Values.Where(u => lTempAGVs.Exists(v => v.ID == Convert.ToUInt32(u.CHE_ID))).ToList();
                // 早装到箱的 AGV 先上 QCPBOut
                lAGVCommandsToMoveOn.Sort(this.AGVCommandTimeCompareFuncByStatus);
                foreach (AGV_Command oComm in lAGVCommandsToMoveOn)
                {
                    oAGV = this.oSimDataStore.dAGVs[Convert.ToUInt32(oComm.CHE_ID)];
                    if (this.SearchForAGVAimLane(oAGV, oComm.FROM_BAY_TYPE, oComm.FROM_BLOCK))
                    {
                        e = new GenerateAGVRouteEventArgs()
                        {
                            oA = oAGV,
                            AimLaneID = oAGV.AimLaneID,
                            IsGenerationSucc = false
                        };

                        this.GenerateAGVRouteEvent.Invoke(this, e);

                        if (e.IsGenerationSucc)
                            oAGV.eAGVStageStatus = StatusEnums.ActionStatus.Doing;
                        else
                            oAGV.AimLaneID = 0;
                    }
                }
            }

            // AGVWorkStage 为 AtQCPBOut，AGVStageStatus 为 Done，进入 ToWSTP 的 Null 状态。
            lTempAGVs = this.oSimDataStore.dAGVs.Values.Where(u => u.eAGVStage == StatusEnums.AGVWorkStage.AtQCPBOut && u.eAGVStageStatus == StatusEnums.ActionStatus.Done).ToList();
            lTempAGVs.ForEach(u => u.eAGVStage = StatusEnums.AGVWorkStage.ToWSTP);
            lTempAGVs.ForEach(u => u.eAGVStageStatus = StatusEnums.ActionStatus.Null);

            // AGVWorkStage 为 ToWSTP，AGVStageStatus 为 Null，尝试为卸船箱分配堆场位置，分配成功后进入状态Ready
            lTempAGVs = this.oSimDataStore.dAGVs.Values.Where(u => u.eAGVStage == StatusEnums.AGVWorkStage.ToWSTP && u.eAGVStageStatus == StatusEnums.ActionStatus.Null).ToList();

            if (lTempAGVs.Count > 0)
            {
                bRet = true;
                lAGVCommandsToMoveOn = this.oSimDataStore.dAGVCommands.Values.Where(u => lTempAGVs.Exists(v => v.ID == Convert.ToUInt32(u.CHE_ID))).ToList();
                // 最早到 QCPBOut 的 AGV 最先分配
                lAGVCommandsToMoveOn.Sort(this.AGVCommandTimeCompareFuncByStatus);
                foreach (AGV_Command oComm in lAGVCommandsToMoveOn)
                {
                    oAGV = this.oSimDataStore.dAGVs[Convert.ToUInt32(oComm.CHE_ID)];
                    d = new AlloPlanLocsForDiscContsEventArgs();
                    d.lContIDs.Add(oAGV.oTwinStoreUnit.ContID1);
                    if (!string.IsNullOrWhiteSpace(oAGV.oTwinStoreUnit.ContID2))
                        d.lContIDs.Add(oAGV.oTwinStoreUnit.ContID2);
                    this.AlloPlanLocsForDiscContsEvent.Invoke(this, d);
                    if (d.IsSucc)
                    {
                        oComm.TO_BAY_TYPE = "WS";
                        oComm.TO_BLOCK = this.oSimDataStore.dSimContainerInfos[oAGV.oTwinStoreUnit.ContID1].PlanBlock;
                        oComm.VERSION++;
                        oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                        oAGV.eAGVStageStatus = StatusEnums.ActionStatus.Ready;
                    }
                }
            }

            

            

            //if (lAGVCommandsToMoveOn.Count > 0)
            //{
            //    bRet = true;
            //    foreach (AGV_Command oComm in lAGVCommandsToMoveOn)
            //    {
            //        oAGV = lTempAGVs.Find(u => u.ID == Convert.ToUInt32(oComm.CHE_ID));
            //        this.DiscContPlanPlacAllo.Invoke(this, new SimDiscAlloEventArgs(Convert.ToUInt32(oComm.CHE_ID), oComm.CONTAINER_ID, this.oSimDataStore.dSimContainerInfos[oComm.CONTAINER_ID].eSize));
            //        if (!string.IsNullOrWhiteSpace(this.oSimDataStore.dSimContainerInfos[oComm.CONTAINER_ID].PlanLoc)
            //            && this.oSimDataStore.dSimContainerInfos[oComm.CONTAINER_ID].PlanLoc.Length > 0
            //            && this.SearchForAGVAimLane(oAGV, oComm.TO_BAY_TYPE, oComm.TO_BLOCK))
            //        {
            //            bRet = true;
            //            this.oSimTrafficCtrl.GenerateAGVRouteToGivenLane(oAGV, ref oPPTViewFrame);
            //        }
            //    }
            //}


            // AGVWorkStage 为 AtQCPBOut，MotionStatus 为 Waiting，尝试申请 WSTP 并向其移动。




            // AGVWorkStage 为 ToWSTP，MotionStatus 为 Waiting，尝试申请 WSTP 并向其移动。




            // AGVWorkStage 为 AtWSTP，AGVStageStatus 为 Done，尝试申请 WSPB 并向其移动。



            return bRet;
        }

  

        /// <summary>
        /// 返回AGV的目标类型和目标。
        /// </summary>
        /// <param name="oComm">AGVCommand</param>
        /// <param name="AimBayType">目标类型，岸边为"QC"，堆场为"WS"</param>
        /// <param name="AimBlock">目标编号，岸边为岸桥编号，堆场为箱区编号</param>
        /// <returns>找到目标返回true，找不到返回false</returns>
        private bool SearchForAGVAimBayTypeAndBlock(AGV_Command oComm, out string AimBayType, out string AimBlock)
        {
            bool bRet = false;

            AimBayType = "";
            AimBlock = "";

            if (oComm.JOB_TYPE == JobType.DISC.ToString() || oComm.JOB_TYPE == JobType.LOAD.ToString())
            {
                if (oComm.COMMAND_STATUS == TaskStatus.Enter.ToString())
                {
                    AimBayType = oComm.FROM_BAY_TYPE;
                    AimBlock = oComm.FROM_BLOCK;
                    bRet = true;
                }
                else if (oComm.COMMAND_STATUS == TaskStatus.Complete_From.ToString())
                {
                    AimBayType = oComm.TO_BAY_TYPE;
                    AimBlock = oComm.TO_BLOCK;
                    bRet = true;
                }
            }

            return bRet;
        }


        /// <summary>
        /// 计算 AGV 到目标的大致距离
        /// </summary>
        /// <param name="oA">AGV对象</param>
        /// <param name="AimBayType">目标类型</param>
        /// <param name="AimBlock">目标编号</param>
        /// <returns>距离</returns>
        private double CalcuDisOfAGVToAim(AGV oA, string AimBayType, string AimBlock)
        {
            double dis = -1;
            double AimX, AimY;

            switch (AimBayType)
            {
                case "WS":
                    if (!this.oSimDataStore.dBlocks.ContainsKey(AimBlock))
                        return dis;
                    AimX = this.oSimDataStore.dBlocks[AimBlock].X + this.oSimDataStore.dBlocks[AimBlock].MarginX;
                    AimY = this.oSimDataStore.dBlocks[AimBlock].Y + this.oSimDataStore.dBlocks[AimBlock].MarginY / 2;
                    break;
                case "QC":
                    if (!this.oSimDataStore.dQCs.ContainsKey(Convert.ToUInt32(AimBlock)))
                        return dis;
                    AimX = this.oSimDataStore.dQCs[Convert.ToUInt32(AimBlock)].BasePoint.X;
                    AimY = this.oSimDataStore.dQCs[Convert.ToUInt32(AimBlock)].BasePoint.Y;
                    break;
                default:
                    return dis;
            }

            dis = Math.Abs(oA.MidPoint.X - AimX) + Math.Abs(oA.MidPoint.Y - AimY);

            return dis;
        }

        /// <summary>
        /// 用于 Command 按照距离排序
        /// </summary>
        /// <param name="oComm1">AGVCommand1</param>
        /// <param name="oComm2">AGVCommand2</param>
        /// <returns></returns>
        private int AGVCommandDisCompareFunc(AGV_Command oComm1, AGV_Command oComm2)
        {
            AGV oAGV1, oAGV2;
            string AimBayType1, AimBayType2, AimBlock1, AimBlock2;
            double dis1, dis2;

            int bRet = 0;

            if (oComm1.Equals(oComm2) || oComm1.CHE_ID == oComm2.CHE_ID)
                return bRet;

            oAGV1 = this.oSimDataStore.dAGVs.Values.FirstOrDefault<AGV>(u => u.ID == Convert.ToUInt32(oComm1.CHE_ID));
            oAGV2 = this.oSimDataStore.dAGVs.Values.FirstOrDefault<AGV>(u => u.ID == Convert.ToUInt32(oComm2.CHE_ID));

            if (oAGV1 == null || oAGV2 == null)
                return bRet;

            if (oAGV1.eAGVStage != StatusEnums.AGVWorkStage.ToQCPBIn && oAGV1.eAGVStage != StatusEnums.AGVWorkStage.ToWSPBIn && oAGV1.eAGVStage != StatusEnums.AGVWorkStage.ToWSTP
                && oAGV2.eAGVStage != StatusEnums.AGVWorkStage.ToQCPBIn && oAGV2.eAGVStage != StatusEnums.AGVWorkStage.ToWSPBIn && oAGV2.eAGVStage != StatusEnums.AGVWorkStage.ToWSTP)
                return bRet;

            // 只关注两类移动 1 去岸边的，ToQCPBIn 2 去堆场的 ToWSTP

            dis1 = 0; dis2 = 0;

            if (this.SearchForAGVAimBayTypeAndBlock(oComm1, out AimBayType1, out AimBlock1))
                dis1 = this.CalcuDisOfAGVToAim(oAGV1, AimBayType1, AimBlock1);
            ;
            if (this.SearchForAGVAimBayTypeAndBlock(oComm2, out AimBayType2, out AimBlock2))
                dis2 = this.CalcuDisOfAGVToAim(oAGV2, AimBayType2, AimBlock2);

            if (dis1 <= 0 || dis2 <= 0)
                return bRet;

            if (dis1 > dis2)
                bRet = 1;
            else if (dis1 < dis2)
                bRet = -1;

            return bRet;
        }

        /// <summary>
        /// 用于 AGVCommand 按照最后一次状态更新时间排序
        /// </summary>
        /// <param name="oComm1">AGVCommand1</param>
        /// <param name="oComm2">AGVCommand2</param>
        /// <returns></returns>
        private int AGVCommandTimeCompareFuncByStatus(AGV_Command oComm1, AGV_Command oComm2)
        {
            AGV_STATUS oAgvStatus1, oAgvStatus2;
            int bRet = 0;

            if (oComm1.Equals(oComm2) || oComm1.CHE_ID == oComm2.CHE_ID)
                return bRet;

            oAgvStatus1 = this.oSimDataStore.dAGVStatus.Values.FirstOrDefault<AGV_STATUS>(u => oComm1.CHE_ID.Trim() == u.CHE_ID.Trim());
            oAgvStatus2 = this.oSimDataStore.dAGVStatus.Values.FirstOrDefault<AGV_STATUS>(u => oComm2.CHE_ID.Trim() == u.CHE_ID.Trim());

            if (oAgvStatus1 == null || oAgvStatus2 == null)
                return bRet;

            return oAgvStatus1.UPDATED.CompareTo(oAgvStatus2.UPDATED);
        }

        /// <summary>
        /// 一轮移动结束后，尝试更新所有 eMotionStatus 为 Waiting， 但 ActionStatus 不为 Done 的 AGV 状态
        /// </summary>
        public void RenewAGVStagesAfterArrival()
        {
            Lane oL;

            List<AGV> lAGVs = this.oSimDataStore.dAGVs.Values.Where(u => this.IsNewAimLaneInNeed(u.eJobType, u.eAGVStage, u.eAGVStageStatus)
                && u.eMotionStatus == StatusEnums.MotionStatus.Waiting && u.eAGVStageStatus == StatusEnums.ActionStatus.Doing).ToList();

            foreach (AGV oA in lAGVs)
            {
                oL = this.oSimDataStore.dLanes[oA.AimLaneID];
                switch (oA.eAGVStage)
                {
                    case StatusEnums.AGVWorkStage.ToQCPBIn:
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Done;
                        if (this.oSimDataStore.dLanes[oA.AimLaneID].eAttr == LaneAttribute.STS_PB_ONLY_IN)
                            oA.eAGVStage = StatusEnums.AGVWorkStage.AtQCPBIn;
                        break;
                    case StatusEnums.AGVWorkStage.ToQCPBOut:
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Done;
                        // 注意可能实际上并不在 LaneAttribute 为 STS_QCPB_ONLY_OUT 的车道上。
                        oA.eAGVStage = StatusEnums.AGVWorkStage.AtQCPBOut;
                        break;
                    case StatusEnums.AGVWorkStage.ToQCPB:
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Done;
                        oA.eAGVStage = StatusEnums.AGVWorkStage.AtQCPB;
                        break;
                    case StatusEnums.AGVWorkStage.ToQCTP:
                        oA.eAGVStage = StatusEnums.AGVWorkStage.AtQCTP;
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Ready;
                        break;
                    case StatusEnums.AGVWorkStage.ToWSPB:
                        oA.eAGVStage = StatusEnums.AGVWorkStage.AtWSPB;
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Done;
                        break;
                    case StatusEnums.AGVWorkStage.ToWSPBIn:
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Done;
                        if (this.oSimDataStore.dLanes[oA.AimLaneID].eType == AreaType.WS_PB)
                            oA.eAGVStage = StatusEnums.AGVWorkStage.AtWSPBIn;
                        break;
                    case StatusEnums.AGVWorkStage.ToWSPBOut:
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Done;
                        if (this.oSimDataStore.dLanes[oA.AimLaneID].eType == AreaType.WS_PB)
                            oA.eAGVStage = StatusEnums.AGVWorkStage.AtWSPBOut;
                        break;
                    case StatusEnums.AGVWorkStage.ToWSTP:
                        oA.eAGVStage = StatusEnums.AGVWorkStage.AtWSTP;
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Ready;
                        break;
                    default:
                        break;
                }
            }
        }


        /// <summary>
        /// 指定AGV的路径终点，并更新SubStage
        /// </summary>
        /// <param name="oA">AGV对象</param>
        /// <param name="AimTypeStr">终点类型</param>
        /// <param name="AimStr">终点标记</param>
        private bool SearchForAGVAimLane(AGV oA, string AimTypeStr, string AimStr)
        {
            uint QCID = 0;
            string BlockName = "";
            uint AimLaneID;
            Lane CurrLane;
            AGV_Command oComm;

            if (!this.IsNewAimLaneInNeed(oA.eJobType, oA.eAGVStage, oA.eAGVStageStatus))
                return false;

            switch (AimTypeStr)
            {
                case "QC":
                    QCID = Convert.ToUInt32(AimStr);
                    break;
                case "WS":
                    BlockName = AimStr;
                    break;
                default:
                    return false;
            }

            CurrLane = this.oSimDataStore.dLanes.Values.FirstOrDefault<Lane>(u => u.AGVNo == oA.ID
                && u.eStatus == LaneStatus.OCCUPIED && u.ID == oA.CurrLaneID);

            switch (oA.eAGVStage)
            {
                case StatusEnums.AGVWorkStage.ToQCPBIn:
                    if (CurrLane.eType == AreaType.STS_PB && CurrLane.eAttr == LaneAttribute.STS_PB_ONLY_IN && CurrLane.CheNo == QCID)
                        // 不允许同岸桥QCPBIn之间的移动
                        return false;
                    if (this.SearchForCertainAttrVacantQCPBID(QCID, LaneAttribute.STS_PB_ONLY_IN, out AimLaneID))
                    {
                        oA.AimLaneID = AimLaneID;
                        return true;
                    }
                    if (this.SearchForCertainSideVacantQCPBID(QCID, LaneAttribute.STS_PB_ONLY_IN, out AimLaneID))
                    {
                        oA.AimLaneID = AimLaneID;
                        return true;
                    }
                    if (CurrLane.eType != AreaType.STS_PB && this.SearchForNearestVacantQCPBID(QCID, LaneAttribute.STS_PB_IN_OUT, out AimLaneID))
                    {
                        oA.AimLaneID = AimLaneID;
                        return true;
                    }
                    break;
                case StatusEnums.AGVWorkStage.ToQCTP:
                    if (CurrLane.CheNo != QCID)
                        return false;
                    if (this.SearchForVacantQCTP(oA, QCID, out AimLaneID))
                    {
                        oA.AimLaneID = AimLaneID;
                        oComm = this.oSimDataStore.dAGVCommands.Values.FirstOrDefault<AGV_Command>(u => !string.IsNullOrWhiteSpace(u.CHE_ID) && Convert.ToUInt32(u.CHE_ID) == oA.ID);
                        if (oComm.JOB_TYPE == JobType.DISC.ToString())
                        {
                            oComm.FROM_LANE = this.oSimDataStore.dLanes[AimLaneID].AreaLaneID;
                            oComm.COMMAND_VERSION = (Convert.ToInt32(oComm.COMMAND_VERSION) + 1).ToString();
                            oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                        }
                        else if (oComm.JOB_TYPE == JobType.LOAD.ToString())
                        {
                            oComm.TO_LANE = this.oSimDataStore.dLanes[AimLaneID].AreaLaneID;
                            oComm.COMMAND_VERSION = (Convert.ToInt32(oComm.COMMAND_VERSION) + 1).ToString();
                            oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                        }

                        return true;
                    }
                    break;
                case StatusEnums.AGVWorkStage.ToQCPBOut:
                    if (CurrLane.CheNo != QCID)
                        // 不允许同岸桥QCTP之间的移动
                        return false;
                    if (this.SearchForCertainAttrVacantQCPBID(QCID, LaneAttribute.STS_PB_ONLY_OUT, out AimLaneID))
                    {
                        oA.AimLaneID = AimLaneID;
                        return true;
                    }
                    else if (this.SearchForCertainSideVacantQCPBID(QCID, LaneAttribute.STS_PB_ONLY_OUT, out AimLaneID))
                    {
                        oA.AimLaneID = AimLaneID;
                        return true;
                    }
                    break;
                case StatusEnums.AGVWorkStage.AtQCPBOut:
                    if (this.SearchForVacantWSTP(BlockName, out AimLaneID))
                    {
                        oA.AimLaneID = AimLaneID;
                        oA.eAGVStage = StatusEnums.AGVWorkStage.ToWSTP;
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Doing;
                        return true;
                    }
                    if (this.SearchForVacantDirWSPBID(BlockName, "IN", out AimLaneID))
                    {
                        oA.AimLaneID = AimLaneID;
                        oA.eAGVStage = StatusEnums.AGVWorkStage.ToWSPBIn;
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Doing;
                    }
                    if (this.SearchForVacantNoDirWSPBID(BlockName, out AimLaneID))
                    {
                        oA.AimLaneID = AimLaneID;
                        oA.eAGVStage = StatusEnums.AGVWorkStage.ToWSPBIn;
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Doing;
                    }
                    break;
                case StatusEnums.AGVWorkStage.ToWSPBIn:
                    if (this.SearchForVacantWSTP(BlockName, out AimLaneID))
                    {
                        oA.AimLaneID = AimLaneID;
                        oA.eAGVStage = StatusEnums.AGVWorkStage.ToWSTP;
                        oA.eAGVStageStatus = StatusEnums.ActionStatus.Doing;
                        return true;
                    }
                    break;
                case StatusEnums.AGVWorkStage.ToQCPB:


                    break;
                case StatusEnums.AGVWorkStage.ToWSPB:


                    break;
                case StatusEnums.AGVWorkStage.ToWSPBOut:


                    break;
                case StatusEnums.AGVWorkStage.ToWSTP:


                    break;
            }

            return false;

        }

        /// <summary>
        /// 根据 AGV_Order 形成新的 AGV_Command
        /// </summary>
        /// <param name="OrderInput">所参考的AGV_Order</param>
        private void GenerateNewAGVCommandFromOrder(AGV_Order OrderInput, out AGV_Command oComm)
        {
            Order_Command_Base oBase;
            PropertyInfo[] lPInfo;

            oBase = new Order_Command_Base();
            oBase.Copy(OrderInput);

            lPInfo = typeof(Order_Command_Base).GetProperties();

            oComm = new AGV_Command();

            foreach (PropertyInfo oInfo in lPInfo)
            {
                oComm.SetPropertyValue(oInfo.Name, oInfo.GetValue(oBase, null));
            }

            oComm.COMMAND_ID = OrderInput.ORDER_ID;
            oComm.ORDER_VERSION = OrderInput.ORDER_VERSION;
            oComm.COMMAND_VERSION = "1";
            oComm.COMMAND_STATUS = TaskStatus.Enter.ToString();
            oComm.EXCEPTION_CODE = "";
            oComm.START_TIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

            oComm.QUAY_ID = OrderInput.QUAY_ID;
            oComm.YARD_ID = OrderInput.YARD_ID;
            oComm.FROM_BLOCK = OrderInput.FROM_BLOCK;
            oComm.TO_BLOCK = OrderInput.TO_BLOCK;

            this.oSimDataStore.dAGVCommands.Add(oComm.COMMAND_ID, oComm);
        }

        /// <summary>
        /// 在指定岸桥附近寻找指定Attr的空闲QCPB
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <param name="eAttr">LaneAttribute</param>
        /// <param name="ResLaneID">找到的QCPB编号</param>
        /// <returns>找到返回true，否则返回false</returns>
        private bool SearchForCertainAttrVacantQCPBID(uint QCID, LaneAttribute eAttr, out uint ResLaneID)
        {
            List<Lane> lTempLanes;
            QCPosPlan oQCPP;

            ResLaneID = 0;

            if (!this.oSimDataStore.dQCs.ContainsKey(QCID) || !this.oSimDataStore.dQCPosPlans.ContainsKey(QCID))
                return false;

            oQCPP = this.oSimDataStore.dQCPosPlans[QCID];

            lTempLanes = this.oSimDataStore.dLanes.Values.Where(u => u.CheNo == QCID && u.eStatus == LaneStatus.IDLE
                && u.eAttr == eAttr).ToList();

            if (lTempLanes.Count > 0)
            {
                lTempLanes.Sort((u1, u2) =>  Math.Abs(u1.pMid.X - oQCPP.WQPos) == Math.Abs(u2.pMid.X - oQCPP.WQPos) 
                    ? 0 : (Math.Abs(u1.pMid.X - oQCPP.WQPos) > Math.Abs(u2.pMid.X - oQCPP.WQPos) ? 1 : -1));
                ResLaneID = lTempLanes[0].ID;
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// 在指定岸桥的某一侧寻找指定Attr的空闲QCPB
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <param name="eAttr">本打算寻找的LaneAttribute</param>
        /// <param name="ResLaneID">找到的QCPB编号</param>
        /// <returns>找到返回true，否则返回false</returns>
        private bool SearchForCertainSideVacantQCPBID(uint QCID, LaneAttribute eAttr, out uint ResLaneID)
        {
            List<Lane> lPotentialLanes;
            QCPosPlan oQCPP;
            HandleLinePlan oHLP;
            string DirStr;

            ResLaneID = 0;

            if (!this.oSimDataStore.dQCs.ContainsKey(QCID) || !this.oSimDataStore.dQCPosPlans.ContainsKey(QCID)
                || (eAttr != LaneAttribute.STS_PB_ONLY_IN && eAttr != LaneAttribute.STS_PB_ONLY_OUT))
                return false;

            oQCPP = this.oSimDataStore.dQCPosPlans[QCID];

            if (!this.oSimDataStore.dHandleLinePlans.ContainsKey(oQCPP.CurrWQ))
                return false;

            oHLP = this.oSimDataStore.dHandleLinePlans[oQCPP.CurrWQ];

            DirStr = "";

            // 限制岸桥某侧的选择
            // 先定侧方向
            switch (eAttr)
            {
                case LaneAttribute.STS_PB_ONLY_IN:
                    // 找不到QCPBIN的条件下，按照进出方向在某一侧寻找空闲的QCPB_IN_OUT
                    switch (oHLP.eSTSVisitDir)
                    {
                        case StatusEnums.STSVisitDir.Clockwise:
                            DirStr = "+";
                            break;
                        case StatusEnums.STSVisitDir.AntiClockwise:
                            DirStr = "-";
                            break;
                        default:
                            return false;
                    }
                    break;
                case LaneAttribute.STS_PB_ONLY_OUT:
                    // 找不到空闲的 QCPBOUT 的条件下，按照进出方向在某一侧寻找空闲的 QCPB_IN_OUT
                    switch (oHLP.eSTSVisitDir)
                    {
                        case StatusEnums.STSVisitDir.Clockwise:
                            DirStr = "-";
                            break;
                        case StatusEnums.STSVisitDir.AntiClockwise:
                            DirStr = "+";
                            break;
                        default:
                            return false;
                    }
                    break;
                default:
                    break;
            }

            // 再找是不是有
            if (DirStr == "-")
            {
                lPotentialLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_PB
                    && u.pMid.X < oQCPP.WQPos && u.eAttr == LaneAttribute.STS_PB_IN_OUT && u.eStatus == LaneStatus.IDLE).ToList();
                if (lPotentialLanes.Count > 0)
                {
                    lPotentialLanes.Sort((u1, u2) =>  u1.pMid.X == u2.pMid.X ? 0 : (u1.pMid.X < u2.pMid.X ? 1 : -1));
                    ResLaneID = lPotentialLanes[0].ID;
                    return true;
                }
                else
                    return false;
            }
            else
            {
                lPotentialLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_PB
                    && u.pMid.X > oQCPP.WQPos && u.eAttr == LaneAttribute.STS_PB_IN_OUT && u.eStatus == LaneStatus.IDLE).ToList();
                if (lPotentialLanes.Count > 0)
                {
                    lPotentialLanes.Sort((u1, u2) => u1.pMid.X == u2.pMid.X ? 0 : (u1.pMid.X > u2.pMid.X ? 1 : -1));
                    ResLaneID = lPotentialLanes[0].ID;
                    return true;
                }
                else
                    return false;
            }
        }

        /// <summary>
        /// 在指定岸桥附近寻找空闲的QCPB
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <returns>找到的车道编号</returns>
        private bool SearchForNearestVacantQCPBID(uint QCID, LaneAttribute eAttr, out uint ResLaneID)
        {
            List<Lane> lPotentialLanes;
            QCPosPlan oQCPP;
            HandleLinePlan oHLP;
            ResLaneID = 0;

            if (!this.oSimDataStore.dQCs.ContainsKey(QCID) || !this.oSimDataStore.dQCPosPlans.ContainsKey(QCID))
                return false;

            oQCPP = this.oSimDataStore.dQCPosPlans[QCID];
            
            lPotentialLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_PB
                && u.eAttr == eAttr && u.eStatus == LaneStatus.IDLE).ToList();

            if (lPotentialLanes.Count > 0)
            {
                lPotentialLanes.Sort((u, v) => Math.Abs(u.pMid.X - oQCPP.WQPos) == Math.Abs(v.pMid.X - oQCPP.WQPos) 
                    ? 0 : (Math.Abs(u.pMid.X - oQCPP.WQPos) > Math.Abs(v.pMid.X - oQCPP.WQPos) ? 1 : -1));
                ResLaneID = lPotentialLanes[0].ID;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 在岸桥附近就近寻找空闲的QCPB，不考虑方向约束，一般能找到
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <returns>找到的Lane编号。异常返回0.</returns>
        private uint GetVacantNoDirQCPBID(uint QCID)
        {
            uint ResLaneID = 0;
            List<Lane> lVacantLanes;
            QCPosPlan oQCPP;

            if (!this.oSimDataStore.dQCs.ContainsKey(QCID) || !this.oSimDataStore.dQCPosPlans.ContainsKey(QCID))
                return ResLaneID;

            oQCPP = this.oSimDataStore.dQCPosPlans[QCID];

            lVacantLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_PB 
                && u.eStatus == LaneStatus.IDLE && u.eAttr == LaneAttribute.STS_PB_IN_OUT).ToList();
            lVacantLanes.Sort((u, v) => Math.Abs(u.pMid.X - oQCPP.WQPos) > Math.Abs(u.pMid.X - oQCPP.WQPos) ? 1 : -1);

            if (lVacantLanes.Count > 0)
                ResLaneID = lVacantLanes[0].ID;

            return ResLaneID;
        }

        /// <summary>
        /// 在某箱区交换区寻找空闲的WSTP，不一定保证找到
        /// </summary>
        /// <param name="BlockName">箱区名</param>
        /// <param name="ResLaneID">out 找到的车道编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool SearchForVacantWSTP(string BlockName, out uint ResLaneID)
        {
            ResLaneID = 0;
            List<Lane> lPotentialLanes;

            // 优先找有支架的
            lPotentialLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_TP && u.eStatus == LaneStatus.IDLE
                && u.CheNo == Convert.ToUInt32(BlockName.Substring(BlockName.Length - 1,1)) && u.MateID > 0).ToList();
            if (lPotentialLanes.Count > 0)
            {
                ResLaneID = lPotentialLanes[0].ID;
                return true;
            }
            lPotentialLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_TP && u.eStatus == LaneStatus.IDLE
                && u.CheNo == Convert.ToUInt32(BlockName.Substring(BlockName.Length - 1, 1))).ToList();
            if (lPotentialLanes.Count > 0)
            {
                ResLaneID = lPotentialLanes[0].ID;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 寻找进入或者离开某箱区的空闲WSPB。不一定保证找到
        /// </summary>
        /// <param name="BlockName">箱区编号</param>
        /// <param name="Dir">方向，进入或者出来</param>
        /// <param name="ResLaneID">out 找到的车道编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool SearchForVacantDirWSPBID(string BlockName, string Dir, out uint ResLaneID)
        {
            ResLaneID = 0;
            List<Lane> lTempWSTPs, lTempWSPBs;
            double PosY;

            if (!this.oSimDataStore.dBlocks.ContainsKey(BlockName))
                return false;

            lTempWSTPs = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_TP 
                && u.CheNo == Convert.ToUInt32(BlockName.Substring(BlockName.Length - 1,1))).ToList();

            // 受厦门单行道布置影响，箱区一定是顺时针进出
            switch (Dir.ToUpper())
            {
                case "IN":
                    PosY = lTempWSTPs.Max(u => u.pMid.Y);
                    lTempWSPBs = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_PB
                        && u.eStatus == LaneStatus.IDLE && u.pMid.Y >= PosY).OrderBy(u => u.pMid.Y).ToList();
                    if (lTempWSPBs.Count > 0)
                    {
                        ResLaneID = lTempWSPBs[0].ID;
                        return true;
                    }
                    else
                        return false;
                    //break;
                case "OUT":
                    PosY = lTempWSTPs.Min(u => u.pMid.Y);
                    lTempWSPBs = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_PB
                        && u.eStatus == LaneStatus.IDLE && u.pMid.Y <= PosY).OrderByDescending(u => u.pMid.Y).ToList();
                    if (lTempWSPBs.Count > 0)
                    {
                        ResLaneID = lTempWSPBs[0].ID;
                        return true;
                    }
                    else
                        return false;
                    //break;
                default:
                    break;
            }

            return false;
        }

        /// <summary>
        /// 在箱区附近寻找空闲的WSPB，不考虑方向，应该能找到
        /// </summary>
        /// <param name="BlockName">箱区编号</param>
        /// <returns>找到的车道编号</returns>
        private bool SearchForVacantNoDirWSPBID(string BlockName, out uint ResLaneID)
        {
            ResLaneID = 0;
            List<Lane> lTempWSTPs, lTempWSPBs;
            double PosY;

            if (!this.oSimDataStore.dBlocks.ContainsKey(BlockName))
                return false;

            lTempWSTPs = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_TP
                && u.CheNo == Convert.ToUInt32(BlockName.Substring(BlockName.Length - 1, 1))).ToList();

            PosY = lTempWSTPs.Average(u => u.pMid.Y);

            lTempWSPBs = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_PB
                && u.eStatus == LaneStatus.IDLE).ToList();
            lTempWSPBs.Sort((u, v) => Math.Abs(u.pMid.Y - PosY) == Math.Abs(v.pMid.Y - PosY) ? 0 : Math.Abs(u.pMid.Y - PosY) > Math.Abs(v.pMid.Y - PosY) ? 1 : -1);

            ResLaneID = lTempWSPBs[0].ID;

            return true;
        }

        /// <summary>
        /// 在某QC下方寻找空闲的QCTP。不一定保证找到
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <param name="ResLaneID">找到的车道编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool SearchForVacantQCTP(AGV oA, uint QCID, out uint ResLaneID)
        {
            ResLaneID = 0;
            List<Lane> lTempQCTPs;
            QCDT oQC;

            if ((oA.eJobType != JobType.DISC && oA.eJobType != JobType.LOAD) || !this.oSimDataStore.dQCs.ContainsKey(QCID))
                return false;


            if (oA.eJobType == JobType.LOAD)
            {
                oQC = this.oSimDataStore.dQCs[QCID];
                switch (oA.oTwinStoreUnit.eUnitStoreType)
                {
                    case StatusEnums.StoreType.STEU:
                    case StatusEnums.StoreType.FEU:
                    case StatusEnums.StoreType.FFEU:
                        if (!oQC.lLoadableContList.Contains(oA.oTwinStoreUnit.ContID1))
                            return false;
                        break;
                    case StatusEnums.StoreType.DTEU:
                        if (!oQC.lLoadableContList.Contains(oA.oTwinStoreUnit.ContID1) || !oQC.lLoadableContList.Contains(oA.oTwinStoreUnit.ContID2))
                            return false;
                        break;
                }
            }

            lTempQCTPs = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_TP
                && u.CheNo == QCID && u.eAttr == LaneAttribute.STS_TP_WORK && u.eStatus == LaneStatus.IDLE).ToList();

            if (lTempQCTPs.Count > 0)
            {
                ResLaneID = lTempQCTPs[0].ID;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断是否需要新的 AimLane
        /// </summary>
        /// <param name="eAGVStage">AGVWorkStage</param>
        /// <returns>在进行状态返回true，否则返回false</returns>
        private bool IsNewAimLaneInNeed(JobType eJobType, StatusEnums.AGVWorkStage eAGVStage, StatusEnums.ActionStatus eAGVStageStatus)
        {
            bool bRet = false;

            if (eJobType != JobType.DISC && eJobType != JobType.LOAD)
                return bRet;

            if (eAGVStage == StatusEnums.AGVWorkStage.ToQCPBIn 
                && (eAGVStageStatus != StatusEnums.ActionStatus.Ready || eAGVStageStatus != StatusEnums.ActionStatus.Doing))
                bRet = true;
            else if (eAGVStage == StatusEnums.AGVWorkStage.AtQCPBIn || eAGVStage == StatusEnums.AGVWorkStage.AtQCPBOut)
                bRet = true;
            else if (eAGVStage == StatusEnums.AGVWorkStage.AtQCTP && eAGVStageStatus == StatusEnums.ActionStatus.Done)
                bRet = true;
            else if (eAGVStage == StatusEnums.AGVWorkStage.ToWSPBIn || eAGVStage == StatusEnums.AGVWorkStage.ToWSTP
                && (eAGVStageStatus != StatusEnums.ActionStatus.Ready || eAGVStageStatus != StatusEnums.ActionStatus.Doing))
                bRet = true;
            else if (eAGVStage == StatusEnums.AGVWorkStage.AtWSPBIn || eAGVStage == StatusEnums.AGVWorkStage.AtWSPBOut)
                bRet = true;
            else if (eJobType != JobType.LOAD && eAGVStage == StatusEnums.AGVWorkStage.AtWSTP && eAGVStageStatus == StatusEnums.ActionStatus.Done)
                bRet = true;

            return bRet;
        }


    }
}
