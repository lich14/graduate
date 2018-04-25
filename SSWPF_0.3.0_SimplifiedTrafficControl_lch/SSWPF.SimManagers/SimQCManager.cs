using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Define;
using SharpSim;
using SSWPF.Define;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.DBDefine;
using ZECS.Schedule.DBDefine.YardMap;
using ZECS.Schedule.DB;

namespace SSWPF.SimManagers
{
    // 管岸桥的，小车该怎么动，大车该怎么动，==
    public class SimQCManager
    {
        public event EventHandler<ProjectToViewFrameEventArgs> ProjectToViewFrameEvent;
        public event EventHandler<ProjectToInfoFrameEventArgs> ProjectToInfoFrameEvent;

        private SimDataStore oSimDataStore;
        
        public SimQCManager()
        {
        }

        public SimQCManager(SimDataStore oSimDataStore)
            : this()
        {
            this.oSimDataStore = oSimDataStore;
        }

        public bool Init()
        {
            if (this.oSimDataStore == null)
            {
                Logger.Simulate.Error("SimQCManager: Null SimDataStore!");
                return false;
            }

            if (this.ProjectToViewFrameEvent == null || this.ProjectToInfoFrameEvent == null)
            {
                Logger.Simulate.Error("SimQCManager: Null Event Listener!");
                return false;
            }

            if (this.oSimDataStore.dQCs == null || this.oSimDataStore.dQCs.Count == 0)
            {
                Logger.Simulate.Error("SimQCManager: No QC Existed!");
                return false;
            }

            // QCContStageRec 清单初始化
            this.oSimDataStore.dQCContStageRecs = new Dictionary<uint, List<QCContStageRec>>();
            foreach (uint iKey in this.oSimDataStore.dQCs.Keys)
            {
                if (!this.oSimDataStore.dQCContStageRecs.ContainsKey(iKey))
                    this.oSimDataStore.dQCContStageRecs.Add(iKey, new List<QCContStageRec>());
            }

            return true;
        }

        /// <summary>
        /// 刷新所有QC的CurrWQ
        /// </summary>
        /// <returns>有QC的CurrWQ发生变化则返回true，否则返回false</returns>
        public bool RefreshWQForQCs()
        {
            bool bRet = false;
            string NextWQ;
            List<QCDT> lTempQCs = new List<QCDT>();

            foreach (uint iKey in this.oSimDataStore.dQCs.Keys)
            {
                if (this.oSimDataStore.dQCs[iKey].eMotionStatus == StatusEnums.MotionStatus.Free)
                {
                    NextWQ = this.FindNextWQWithResJobsGened(iKey);
                    if (!string.IsNullOrWhiteSpace(NextWQ))
                    {
                        bRet = true;
                        lTempQCs.Add(this.oSimDataStore.dQCs[iKey]);
                        this.oSimDataStore.dQCs[iKey].CurrWQ = NextWQ;
                        // 指定作业组才能分配任务
                        this.oSimDataStore.dQCs[iKey].eMotionStatus = StatusEnums.MotionStatus.Ready;
                        if (this.oSimDataStore.dViewWorkQueues.ContainsKey(NextWQ))
                            this.oSimDataStore.dQCs[iKey].eMoveKind = this.oSimDataStore.dViewWorkQueues[NextWQ].MOVE_KIND;
                        else
                            this.oSimDataStore.dQCs[iKey].eMoveKind = Move_Kind.NULL;
                    }
                }
            }

            if (lTempQCs.Count > 0)
            {
                this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                    {
                        eProjectType = StatusEnums.ProjectType.Renew,
                        oPPTViewFrame = new ProjectPackageToViewFrame()
                        {
                            lQCs = lTempQCs
                        }
                    });
            }

            return bRet;
        }

        /// <summary>
        /// 寻找某QC的已经生成ResJob和Task的下一个WQ
        /// </summary>
        /// <param name="QCID">QC编号</param>
        /// <returns>WQ名。若找不到则返回空字符串</returns>
        public string FindNextWQWithResJobsGened(uint QCID)
        {
            string NextWQ = "";
            string CurrWQ = this.oSimDataStore.dQCs[QCID].CurrWQ;  // 可能为空串

            foreach (string sKey in this.oSimDataStore.dViewWorkQueues.Keys)
            {
                if (Convert.ToUInt32(this.oSimDataStore.dViewWorkQueues[sKey].QC_ID) == QCID
                    && this.oSimDataStore.dViewWorkInstructions.Values.Where(u => u.WORK_QUEUE == sKey && !string.IsNullOrWhiteSpace(u.JOB_ID)).Count() > 0
                    && (string.IsNullOrWhiteSpace(CurrWQ) || this.oSimDataStore.dViewWorkQueues[sKey].WQ_SEQ > this.oSimDataStore.dViewWorkQueues[CurrWQ].WQ_SEQ))
                {
                    if (NextWQ.Length == 0 || this.oSimDataStore.dWorkQueues[sKey].WQ_SEQ < this.oSimDataStore.dWorkQueues[NextWQ].WQ_SEQ)
                        NextWQ = sKey;
                }
            }

            return NextWQ;
        }

        /// <summary>
        /// 步进移动所有QC
        /// </summary>
        /// <param name="timeLength">步长</param>
        /// <param name="oPPTViewFrame">ViewFrame投影单元</param>
        /// <returns>若有QC的位置发生变化则返回true，否则返回false</returns>
        public bool MoveQCsInStep(double timeLength)
        {
            bool bRet = false;
            double StepLength, LengthLeft;
            List<QCDT> lTempQCs = new List<QCDT>();
            List<STS_STATUS> lTempQCStatus = new List<STS_STATUS>();

            foreach (uint iKey in this.oSimDataStore.dQCs.Keys)
            {
                if (this.oSimDataStore.dQCs[iKey].eMotionStatus == StatusEnums.MotionStatus.Moving)
                {
                    lTempQCs.Add(this.oSimDataStore.dQCs[iKey]);
                    lTempQCStatus.Add(this.oSimDataStore.dSTSStatus[iKey.ToString()]);
                    bRet = true;
                    StepLength = this.oSimDataStore.dQCs[iKey].oType.TravelSpeed * timeLength;
                    LengthLeft = Math.Abs(this.oSimDataStore.dQCs[iKey].AimPos - this.oSimDataStore.dQCs[iKey].BasePoint.X);
                    if (StepLength >= LengthLeft)
                    {
                        this.oSimDataStore.dQCs[iKey].BasePoint.X = this.oSimDataStore.dQCs[iKey].AimPos;
                        this.oSimDataStore.dSTSStatus[iKey.ToString()].nQCPosition = Convert.ToInt32(this.oSimDataStore.dQCs[iKey].AimPos);
                        if (!string.IsNullOrWhiteSpace(this.oSimDataStore.dQCs[iKey].CurrWQ))
                        {
                            this.oSimDataStore.dQCs[iKey].eMotionStatus = StatusEnums.MotionStatus.Waiting;
                            if (this.oSimDataStore.dQCs[iKey].Reachable)
                                this.StartHandling(iKey);
                        }
                        else
                            this.oSimDataStore.dQCs[iKey].eMotionStatus = StatusEnums.MotionStatus.Free;
                    }
                    else
                    {
                        if (this.oSimDataStore.dQCs[iKey].AimPos > this.oSimDataStore.dQCs[iKey].BasePoint.X)
                            this.oSimDataStore.dQCs[iKey].BasePoint.X = this.oSimDataStore.dQCs[iKey].BasePoint.X + StepLength;
                        else
                            this.oSimDataStore.dQCs[iKey].BasePoint.X = this.oSimDataStore.dQCs[iKey].BasePoint.X - StepLength;

                        this.oSimDataStore.dSTSStatus[iKey.ToString()].nQCPosition = Convert.ToInt32(this.oSimDataStore.dQCs[iKey].AimPos);
                    }
                }
            }

            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTViewFrame = new ProjectPackageToViewFrame()
                    {
                        lQCs = lTempQCs
                    }
                });
            this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTInfoFrame = new ProjectPackageToInfoFrame()
                    {
                        lSTSStatuses = lTempQCStatus
                    }
                });

            return bRet;
        }

        /// <summary>
        /// 开始装卸，列出所有的待做ResJob，并修改小车的 eMotionStatus 使 Action 事件能被激发。
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <returns>有ResJob返回true，否则返回false</returns>
        private bool StartHandling(uint QCID)
        {
            List<WORK_INSTRUCTION_STATUS> lWIs;
            List<STS_ResJob> lReleasedSTSResJobList;
            QCContStageRec oQCContLocTemp;

            if (this.oSimDataStore.dQCs[QCID].eMotionStatus != StatusEnums.MotionStatus.Waiting || !this.oSimDataStore.dQCs[QCID].Reachable
                || string.IsNullOrWhiteSpace(this.oSimDataStore.dQCs[QCID].CurrWQ))
                return false;

            lWIs = this.oSimDataStore.dViewWorkInstructions.Values.Where(u => u.WORK_QUEUE == this.oSimDataStore.dQCs[QCID].CurrWQ).ToList();
            lReleasedSTSResJobList = this.oSimDataStore.dSTSResJobs.Values.Where(u => lWIs.Exists(v => v.CONTAINER_ID == u.CONTAINER_ID)).ToList();

            this.oSimDataStore.dQCContStageRecs[QCID].Clear();

            // 列出所有已发出的 ResJob 以及对应 WI
            foreach (STS_ResJob oResJob in lReleasedSTSResJobList)
            {
                oQCContLocTemp = new QCContStageRec();
                oQCContLocTemp.oResJob = oResJob;
                oQCContLocTemp.oWI = lWIs.Find(u => u.CONTAINER_ID == oResJob.CONTAINER_ID);
                if (this.oSimDataStore.dQCs[QCID].eMoveKind == Move_Kind.DSCH)
                    oQCContLocTemp.eQCContLocType = StatusEnums.QCContStage.Vessel;
                else if (this.oSimDataStore.dQCs[QCID].eMoveKind == Move_Kind.LOAD)
                    oQCContLocTemp.eQCContLocType = StatusEnums.QCContStage.AGV;
                this.oSimDataStore.dQCContStageRecs[QCID].Add(oQCContLocTemp);
            }

            if (this.oSimDataStore.dQCContStageRecs[QCID].Count == 0)
                return false;

            this.oSimDataStore.dQCs[QCID].eMotionStatus = StatusEnums.MotionStatus.Working;
            this.oSimDataStore.dQCs[QCID].SetNextActionDateTime(SimStaticParas.SimDtStart.AddSeconds(Simulation.clock));
            this.oSimDataStore.dQCs[QCID].gSimToStep.condition = true;

            switch (this.oSimDataStore.dQCs[QCID].eMoveKind)
            {
                case Move_Kind.DSCH:
                case Move_Kind.LOAD:
                    this.oSimDataStore.dQCs[QCID].MainTrolley.eTroSubProc = StatusEnums.QCMainTrolleySubProc.Null;
                    this.oSimDataStore.dQCs[QCID].ViceTrolley.eTroSubProc = StatusEnums.QCViceTrolleySubProc.Null;
                    break;
                default:
                    this.oSimDataStore.dQCs[QCID].MainTrolley.eTroSubProc = StatusEnums.QCMainTrolleySubProc.Null;
                    this.oSimDataStore.dQCs[QCID].ViceTrolley.eTroSubProc = StatusEnums.QCViceTrolleySubProc.Null;
                    break;
            }

            return true;
        }

        /// <summary>
        /// 判断岸桥主小车是否还有卸船钩要做
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <returns>若有返回true，没有返回false</returns>
        private bool IfMainTroNextDiscMove(uint QCID)
        {
            bool bRet = false;

            if (this.oSimDataStore.dQCs[QCID].eMoveKind != Move_Kind.DSCH)
                return bRet;

            if (this.oSimDataStore.dQCContStageRecs[QCID].Exists(u => u.eQCContLocType == StatusEnums.QCContStage.Vessel))
                bRet = true;

            return bRet;
        }

        /// <summary>
        /// 判断岸桥主小车是否还有装船钩要做
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <returns>若有返回true，没有返回false</returns>
        private bool IfMainTroNextLoadMove(uint QCID)
        {
            bool bRet = false;

            if (this.oSimDataStore.dQCs[QCID].eMoveKind != Move_Kind.LOAD)
                return bRet;

            if (this.oSimDataStore.dQCContStageRecs[QCID].Exists(u => u.eQCContLocType == StatusEnums.QCContStage.AGV || u.eQCContLocType == StatusEnums.QCContStage.ViceTro
                || u.eQCContLocType == StatusEnums.QCContStage.Platform || u.eQCContLocType == StatusEnums.QCContStage.PlatformConfirm))
                bRet = true;

            return bRet;
        }

        /// <summary>
        /// 判断岸桥副小车是否还有卸船钩要做
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <returns>若有返回true，没有返回false</returns>
        private bool IfViceTroNextDiscMove(uint QCID)
        {
            bool bRet = false;

            if (this.oSimDataStore.dQCs[QCID].eMoveKind != Move_Kind.DSCH)
                return bRet;

            if (this.oSimDataStore.dQCContStageRecs[QCID].Exists(u => u.eQCContLocType == StatusEnums.QCContStage.Vessel || u.eQCContLocType == StatusEnums.QCContStage.MainTro
                || u.eQCContLocType == StatusEnums.QCContStage.Platform || u.eQCContLocType == StatusEnums.QCContStage.PlatformConfirm))
                bRet = true;

            return bRet;
        }

        /// <summary>
        /// 判断岸桥副小车是否还有装船钩要做
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <returns>若有返回true，没有返回false</returns>
        private bool IfViceTroNextLoadMove(uint QCID)
        {
            bool bRet = false;

            if (this.oSimDataStore.dQCs[QCID].eMoveKind != Move_Kind.LOAD)
                return bRet;

            if (this.oSimDataStore.dQCContStageRecs[QCID].Exists(u => u.eQCContLocType == StatusEnums.QCContStage.AGV))
                bRet = true;

            return bRet;
        }

        /// <summary>
        /// 主小车预约下一卸船Move。
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool MainTroReserveNextDiscMove(uint QCID)
        {
            List<QCContStageRec> lRecsOnVessel, lTopRecsOnVessel, lTempRecs, lTempRecsOnVessel;
            List<QCPlatformSlot> lFreeSlots;
            Random rand = new Random();
            int Row;
            QCDT oQC = this.oSimDataStore.dQCs[QCID];
            string ContID1 = "", SizeStr1 = "", ContID2 = "", SizeStr2 = "";
            WORK_INSTRUCTION_STATUS oRefWI;

            if (!this.IfMainTroNextDiscMove(QCID) || oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage != StatusEnums.StoreStage.None
                || !oQC.Platform.Exists(u => u.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None))
                return false;

            // 先甲板上
            lRecsOnVessel = this.oSimDataStore.dQCContStageRecs[QCID].Where(u => u.eQCContLocType == StatusEnums.QCContStage.Vessel
                && this.oSimDataStore.dSimContainerInfos[u.oWI.CONTAINER_ID].VesTier > 80).ToList();

            // 再甲板下
            if (lRecsOnVessel.Count == 0)
                lRecsOnVessel = this.oSimDataStore.dQCContStageRecs[QCID].Where(u => u.eQCContLocType == StatusEnums.QCContStage.Vessel
                    && this.oSimDataStore.dSimContainerInfos[u.oWI.CONTAINER_ID].VesTier < 80).ToList();

            // 若没有可卸箱应该报错，这里返回false。
            if (lRecsOnVessel.Count == 0)
                return false;

            // 若没有可卸的位置也返回false
            lFreeSlots = oQC.Platform.FindAll(u => u.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None);
            if (lFreeSlots.Count == 0)
                return false;

            lTopRecsOnVessel = new List<QCContStageRec>();
            lTempRecsOnVessel = new List<QCContStageRec>(lRecsOnVessel);
            while (lTempRecsOnVessel.Count > 0)
            {
                lTempRecs = lTempRecsOnVessel.Where(u => this.oSimDataStore.dSimContainerInfos[u.oWI.CONTAINER_ID].VesBay == this.oSimDataStore.dSimContainerInfos[lTempRecsOnVessel[0].oWI.CONTAINER_ID].VesBay
                    && this.oSimDataStore.dSimContainerInfos[u.oWI.CONTAINER_ID].VesRow == this.oSimDataStore.dSimContainerInfos[lTempRecsOnVessel[0].oWI.CONTAINER_ID].VesRow).ToList();
                lTempRecs.Sort((u, v) => this.oSimDataStore.dSimContainerInfos[u.oWI.CONTAINER_ID].VesTier < this.oSimDataStore.dSimContainerInfos[v.oWI.CONTAINER_ID].VesTier ? 1 : -1);
                lTempRecsOnVessel.RemoveAll(u => lTempRecs.Contains(u));
                lTopRecsOnVessel.Add(lTempRecs[0]);
            }

            // 注意此处并未区分双箱的相对位置
            Row = rand.Next(lTopRecsOnVessel.Count);
            ContID1 = lTopRecsOnVessel[Row].oWI.CONTAINER_ID;
            SizeStr1 = this.oSimDataStore.dSimContainerInfos[ContID1].eSize.ToString();

            if (StatusEnums.GetContSize(SizeStr1) == StatusEnums.ContSize.TEU)
            {
                oRefWI = this.oSimDataStore.dViewWorkInstructions.Values.FirstOrDefault(u => u.LIFT_REFERENCE == ContID1);
                if (!string.IsNullOrWhiteSpace(oRefWI.CONTAINER_ID)
                    && this.oSimDataStore.dSimContainerInfos[oRefWI.CONTAINER_ID].eSize == StatusEnums.ContSize.TEU
                    && this.oSimDataStore.dQCContStageRecs[QCID].Exists(u => u.oWI.CONTAINER_ID == oRefWI.CONTAINER_ID && u.eQCContLocType == StatusEnums.QCContStage.Vessel))
                {
                    ContID2 = oRefWI.CONTAINER_ID;
                    SizeStr2 = this.oSimDataStore.dSimContainerInfos[ContID2].eSize.ToString();
                }
            }

            oQC.MainTrolley.oTwinStoreUnit.ContReserve(ContID1, SizeStr1);
            if (!string.IsNullOrWhiteSpace(ContID2))
                oQC.MainTrolley.oTwinStoreUnit.ContReserve(ContID2, SizeStr2);

            this.TrolleyReservePlatform(oQC.MainTrolley);

            return true;
        }

        /// <summary>
        /// 副小车预约下一装船Move
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <returns>成功返回true，找不到返回false</returns>
        private bool ViceTroReserveNextLoadMove(uint QCID, bool IsTestMode = false)
        {
            QCDT oQC = this.oSimDataStore.dQCs[QCID];
            List<QCContStageRec> lRecsOnAGV, lRecsCouldLoadAtOnce;
            AGV_Command oAGVComm;
            string[] aContIDs;
            List<string> lPredecessors;
            bool bAvailable;
            Random rand = new Random();
            int Row;
            string ContID1 = "", SizeStr1 = "", ContID2 = "", SizeStr2 = "";
            WORK_INSTRUCTION_STATUS oRefWI;

            if (!this.IfViceTroNextLoadMove(QCID)
                || !this.oSimDataStore.dQCContStageRecs[QCID].Exists(u => u.eQCContLocType == StatusEnums.QCContStage.AGV)
                || oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage != StatusEnums.StoreStage.None
                || !oQC.Platform.Exists(u => u.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None))
                return false;

            // 先甲板下
            lRecsOnAGV = this.oSimDataStore.dQCContStageRecs[QCID].Where(u => u.eQCContLocType == StatusEnums.QCContStage.AGV
                && this.oSimDataStore.dSimContainerInfos[u.oWI.CONTAINER_ID].StowTier < 80).ToList();

            // 若无，再甲板上
            if (lRecsOnAGV.Count == 0)
                lRecsOnAGV = this.oSimDataStore.dQCContStageRecs[QCID].Where(u => u.eQCContLocType == StatusEnums.QCContStage.AGV
                    && this.oSimDataStore.dSimContainerInfos[u.oWI.CONTAINER_ID].StowTier > 80).ToList();

            if (lRecsOnAGV.Count == 0)
                return false;

            // 此处应为仍然有箱子没装完。需要筛掉 PREDECESSOR 没装完的，以及 AGV 还没到的 QCContStageRec
            lRecsCouldLoadAtOnce = new List<QCContStageRec>();
            foreach (QCContStageRec oRec in lRecsOnAGV)
            {
                aContIDs = oRec.oWI.LOGICAL_PREDECESSOR.Split(new char[] { ';' });
                lPredecessors = new List<string>(aContIDs);
                aContIDs = oRec.oWI.PHYSICAL_PREDECESSOR.Split(new char[] { ';' });
                lPredecessors.Union(new List<string>(aContIDs));
                lPredecessors.RemoveAll(u => string.IsNullOrWhiteSpace(u));

                // 只要有还没装的Predecessor，就不让现在装船
                bAvailable = true;
                foreach (string ContID in lPredecessors)
                {
                    if (lRecsOnAGV.Exists(u => u.oWI.CONTAINER_ID == ContID))
                    {
                        bAvailable = false;
                        break;
                    }
                }
                if (!IsTestMode && bAvailable)
                {
                    // 删掉 AGV 没到 STSTP 的
                    oAGVComm = this.oSimDataStore.dAGVCommands.Values.FirstOrDefault<AGV_Command>(u => u.CONTAINER_ID == oRec.oWI.CONTAINER_ID);
                    if (oAGVComm == null || string.IsNullOrWhiteSpace(oAGVComm.CONTAINER_ID))
                        bAvailable = false;
                    else
                    {
                        if (!this.oSimDataStore.dAGVs.ContainsKey(Convert.ToUInt32(oAGVComm.CHE_ID))
                            || this.oSimDataStore.dAGVs[Convert.ToUInt32(oAGVComm.CHE_ID)].eAGVStage != StatusEnums.AGVWorkStage.AtQCTP)
                            bAvailable = false;
                    }
                }
                if (bAvailable)
                    lRecsCouldLoadAtOnce.Add(oRec);
            }

            if (lRecsCouldLoadAtOnce.Count == 0)
                return true;

            // 可能导致 Cont 的相对位置与车载位置不同
            Row = rand.Next(lRecsCouldLoadAtOnce.Count);
            ContID1 = lRecsCouldLoadAtOnce[Row].oWI.CONTAINER_ID;
            SizeStr1 = this.oSimDataStore.dSimContainerInfos[ContID1].eSize.ToString();

            if (StatusEnums.GetContSize(SizeStr1) == StatusEnums.ContSize.TEU)
            {
                oRefWI = this.oSimDataStore.dViewWorkInstructions.Values.FirstOrDefault<WORK_INSTRUCTION_STATUS>(u => u.LIFT_REFERENCE == ContID1);
                if (oRefWI != null && !string.IsNullOrWhiteSpace(oRefWI.LIFT_REFERENCE) && !string.IsNullOrWhiteSpace(oRefWI.CONTAINER_ID)
                    && this.oSimDataStore.dSimContainerInfos[oRefWI.CONTAINER_ID].eSize == StatusEnums.ContSize.TEU
                    && this.oSimDataStore.dQCContStageRecs[QCID].Exists(u => u.oWI.CONTAINER_ID == oRefWI.CONTAINER_ID && u.eQCContLocType == StatusEnums.QCContStage.AGV))
                {
                    ContID2 = oRefWI.CONTAINER_ID;
                    SizeStr2 = this.oSimDataStore.dSimContainerInfos[ContID2].eSize.ToString();
                }
            }

            this.oSimDataStore.dQCs[QCID].ViceTrolley.oTwinStoreUnit.ContReserve(ContID1, SizeStr1);
            if (!string.IsNullOrWhiteSpace(ContID2))
                this.oSimDataStore.dQCs[QCID].ViceTrolley.oTwinStoreUnit.ContReserve(ContID2, SizeStr2);

            return true;
        }

        /// <summary>
        /// 主小车预约下一个装船Move
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool MainTroReserveNextLoadMove(uint QCID)
        {
            // 可能在多个PlatFormSlot里面选择
            QCDT oQC = this.oSimDataStore.dQCs[QCID];
            List<QCPlatformSlot> lSlots;
            TwinRigidStoreUnit oUnit = new TwinRigidStoreUnit();
            List<string> lPredecessors;
            string[] aContIDs;

            if (!this.IfMainTroNextLoadMove(QCID)
                || !this.oSimDataStore.dQCContStageRecs[QCID].Exists(u => u.eQCContLocType == StatusEnums.QCContStage.Platform)
                || oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage != StatusEnums.StoreStage.None)
                return false;

            lSlots = oQC.Platform.FindAll(u => u.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored
                && this.oSimDataStore.dQCContStageRecs[QCID].Find(v => v.oWI.CONTAINER_ID == u.oTwinStoreUnit.ContID1).eQCContLocType == StatusEnums.QCContStage.Platform);

            if (lSlots.Count > 1)
            {
                // 如果多个平台位都放着箱子，那么选哪个平台位的
                lPredecessors = new List<string>();
                foreach (QCPlatformSlot oSlot in lSlots)
                {
                    switch (oSlot.oTwinStoreUnit.eUnitStoreType)
                    {
                        case StatusEnums.StoreType.STEU:
                        case StatusEnums.StoreType.FEU:
                        case StatusEnums.StoreType.FFEU:
                            aContIDs = this.oSimDataStore.dViewWorkInstructions[oSlot.oTwinStoreUnit.ContID1].LOGICAL_PREDECESSOR.Split(new char[] { ';' });
                            lPredecessors = new List<string>(aContIDs);
                            aContIDs = this.oSimDataStore.dViewWorkInstructions[oSlot.oTwinStoreUnit.ContID1].PHYSICAL_PREDECESSOR.Split(new char[] { ';' });
                            lPredecessors.Union(new List<string>(aContIDs));
                            break;
                        case StatusEnums.StoreType.DTEU:
                            aContIDs = this.oSimDataStore.dViewWorkInstructions[oSlot.oTwinStoreUnit.ContID1].LOGICAL_PREDECESSOR.Split(new char[] { ';' });
                            lPredecessors = new List<string>(aContIDs);
                            aContIDs = this.oSimDataStore.dViewWorkInstructions[oSlot.oTwinStoreUnit.ContID1].PHYSICAL_PREDECESSOR.Split(new char[] { ';' });
                            lPredecessors.Union(new List<string>(aContIDs));
                            aContIDs = this.oSimDataStore.dViewWorkInstructions[oSlot.oTwinStoreUnit.ContID2].LOGICAL_PREDECESSOR.Split(new char[] { ';' });
                            lPredecessors.Union(new List<string>(aContIDs));
                            aContIDs = this.oSimDataStore.dViewWorkInstructions[oSlot.oTwinStoreUnit.ContID2].PHYSICAL_PREDECESSOR.Split(new char[] { ';' });
                            lPredecessors.Union(new List<string>(aContIDs));
                            break;
                    }
                    if (!this.oSimDataStore.dQCContStageRecs[QCID].Exists(u => lPredecessors.Contains(u.oWI.CONTAINER_ID) && u.eQCContLocType != StatusEnums.QCContStage.Vessel))
                    {
                        oUnit = oSlot.oTwinStoreUnit;
                        break;
                    }
                }
            }
            else if (lSlots.Count == 1)
                oUnit = lSlots[0].oTwinStoreUnit;
            else
                return false;

            oQC.MainTrolley.oTwinStoreUnit.ContReserve(oUnit.ContID1, oUnit.eContSize1.ToString());
            if (oUnit.eUnitStoreType == StatusEnums.StoreType.DTEU)
                oQC.MainTrolley.oTwinStoreUnit.ContReserve(oUnit.ContID2, oUnit.eContSize2.ToString());

            return true;
        }

        /// <summary>
        /// 副小车预约下一个卸船Move
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool ViceTroReserveNextDiscMove(uint QCID)
        {
            // 和主小车装船类似，但不需要考虑先后关系
            // 然而，这么搞容易与 AGV 的到达脱离。
            // 因此，对于两个槽均有箱的情况，考虑了AGV的状态AGVStatus
            QCDT oQC = this.oSimDataStore.dQCs[QCID];
            List<QCPlatformSlot> lSlots;
            TwinRigidStoreUnit oUnit = new TwinRigidStoreUnit();
            Random rand = new Random();

            if (!this.IfViceTroNextDiscMove(QCID)
                || !this.oSimDataStore.dQCContStageRecs[QCID].Exists(u => u.eQCContLocType == StatusEnums.QCContStage.Platform)
                || oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage != StatusEnums.StoreStage.None)
                return false;

            lSlots = oQC.Platform.FindAll(u => u.oTwinStoreUnit.eContStoreStage1 == StatusEnums.StoreStage.Stored
                && this.oSimDataStore.dQCContStageRecs[QCID].Find(v => v.oWI.CONTAINER_ID == u.oTwinStoreUnit.ContID1).eQCContLocType == StatusEnums.QCContStage.Platform);

            if (lSlots.Count >= 1)
                oUnit = lSlots[rand.Next(0, lSlots.Count)].oTwinStoreUnit;
            else
                return false;

            oQC.ViceTrolley.oTwinStoreUnit.ContReserve(oUnit.ContID1, oUnit.eContSize1.ToString());
            if (oUnit.eUnitStoreType == StatusEnums.StoreType.DTEU)
                oQC.ViceTrolley.oTwinStoreUnit.ContReserve(oUnit.ContID2, oUnit.eContSize2.ToString());

            return true;
        }

        /// <summary>
        /// 生成新的STSCommand，卸船用
        /// </summary>
        /// <param name="oResJob"></param>
        private void GenerateNewSTSDiscCommand(STS_ResJob oResJob)
        {
            STS_Command oComm = new STS_Command();
            SimContainerInfo oInfo = this.oSimDataStore.dSimContainerInfos[oResJob.CONTAINER_ID];
            ISORef oISO = this.oSimDataStore.dISORefs[oInfo.ISO];

            oComm.CONTAINER_ID = oResJob.CONTAINER_ID;
            oComm.CHE_ID = oResJob.CHE_ID;
            oComm.COMMAND_ID = this.CreateNewSTSCommID().ToString();
            oComm.COMMAND_STATUS = TaskStatus.Enter.ToString();
            oComm.COMMAND_VERSION = "1";
            oComm.CONTAINER_DOOR_DIRECTION = "2";
            oComm.CONTAINER_HEIGHT = "";
            if (oInfo.eEF == StatusEnums.EF.E)
                oComm.CONTAINER_IS_EMPTY = "1";
            else
                oComm.CONTAINER_IS_EMPTY = "0";
            oComm.CONTAINER_ISO = oInfo.ISO;
            oComm.CONTAINER_LENGTH = oISO.ContainerLengthCM.ToString();
            oComm.CONTAINER_WEIGHT = oInfo.GrossWeight.ToString();
            oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            oComm.EXCEPTION_CODE = "";
            oComm.FROM_BAY = oResJob.FROM_BAY;
            oComm.FROM_BAY_TYPE = oResJob.FROM_BAY_TYPE;
            oComm.FROM_LANE = oResJob.FROM_LANE;
            oComm.FROM_RFID = oResJob.FROM_RFID;
            oComm.FROM_TIER = oResJob.FROM_TIER;
            oComm.FROM_TRUCK_ID = oComm.FROM_TRUCK_ID;
            oComm.FROM_TRUCK_POS = oComm.FROM_TRUCK_POS;
            oComm.FROM_TRUCK_TYPE = oComm.FROM_TRUCK_TYPE;
            oComm.JOB_ID = oResJob.JOB_ID;
            oComm.JOB_STATUS = "None";
            oComm.JOB_TYPE = oResJob.JOB_TYPE;
            oComm.ORDER_ID = "";
            oComm.ORDER_VERSION = "0";
            oComm.QUAY_ID = oResJob.QUAY_ID;
            oComm.RECORDSEQUENCE = 0;
            oComm.TO_BAY = oResJob.TO_BAY;
            oComm.TO_BAY_TYPE = oResJob.TO_BAY_TYPE;
            oComm.TO_LANE = oResJob.TO_LANE;
            oComm.TO_RFID = oResJob.TO_RFID;
            oComm.TO_TIER = oResJob.TO_TIER;
            oComm.TO_TRUCK_ID = oResJob.TO_TRUCK_ID;
            oComm.TO_TRUCK_POS = oResJob.TO_TRUCK_POS;
            oComm.TO_TRUCK_TYPE = oResJob.TO_TRUCK_TYPE;
            oComm.VERSION = 0;
            oComm.VESSEL_ID = oResJob.VESSEL_ID;

            this.oSimDataStore.dSTSCommands.Add(oComm.COMMAND_ID, oComm);
        }

        /// <summary>
        /// 生成新的CommandID，调用DB_ECS
        /// </summary>
        /// <returns></returns>
        public long CreateNewSTSCommID()
        {
            long CurrMaxID = 0;

            CurrMaxID = DB_ECS.Instance.GetNewIndexNum(StatusEnums.IndexType.STSOrdComm);

            return CurrMaxID;
        }

        /// <summary>
        /// oTro检查平台，希望平台上相关箱的状态与 eExpectedStage 相符
        /// </summary>
        /// <param name="oTro">小车，可主可副</param>
        /// <param name="eExpectedStage">希望的状态</param>
        /// <returns>有相关箱且状态相符返回true，否则返回false</returns>
        private bool TrolleyCheckPlatform(QCTrolley oTro, StatusEnums.StoreStage eExpectedStage)
        {
            List<QCPlatformSlot> lSlots;

            if (oTro.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None || eExpectedStage == StatusEnums.StoreStage.None || oTro.oQC == null)
                return false;

            // 若已经有平台位置能够达成条件，返回true
            if (eExpectedStage == StatusEnums.StoreStage.None)
                lSlots = oTro.oQC.Platform.FindAll(u => u.oTwinStoreUnit.eUnitStoreStage == eExpectedStage);
            else
                lSlots = oTro.oQC.Platform.FindAll(u => u.oTwinStoreUnit.ContID1 == oTro.oTwinStoreUnit.ContID1
                    && u.oTwinStoreUnit.ContID2 == oTro.oTwinStoreUnit.ContID2 && u.oTwinStoreUnit.eUnitStoreStage == eExpectedStage);

            if (lSlots.Count > 0)
                return true;

            return false;
        }

        /// <summary>
        /// 小车预约平台位置
        /// </summary>
        /// <param name="oTro">小车</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool TrolleyReservePlatform(QCTrolley oTro)
        {
            List<QCPlatformSlot> lSlots;
            bool bRet = false;

            if (oTro.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None || oTro.oQC == null)
                return false;

            lSlots = oTro.oQC.Platform.FindAll(u => u.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None);

            if (lSlots.Count == 0)
                return false;

            switch (oTro.oTwinStoreUnit.eUnitStoreType)
            {
                case StatusEnums.StoreType.STEU:
                case StatusEnums.StoreType.FEU:
                case StatusEnums.StoreType.FFEU:
                    bRet = lSlots[0].oTwinStoreUnit.ContReserve(oTro.oTwinStoreUnit.ContID1, oTro.oTwinStoreUnit.eContSize1.ToString());
                    break;
                case StatusEnums.StoreType.DTEU:
                    bRet = lSlots[0].oTwinStoreUnit.ContReserve(oTro.oTwinStoreUnit.ContID1, oTro.oTwinStoreUnit.eContSize1.ToString());
                    bRet = (lSlots[0].oTwinStoreUnit.ContReserve(oTro.oTwinStoreUnit.ContID2, oTro.oTwinStoreUnit.eContSize2.ToString()) && bRet);
                    break;
            }

            return bRet;
        }

        /// <summary>
        /// 将编号为 QCID 的岸桥副小车上的集装箱改卸到编号为 AGVID 的 AGV 上，由此引起 AGV_Command 的变化
        /// </summary>
        /// <param name="QCID">岸桥编号</param>
        /// <param name="AGVID">AGV编号</param>
        private void ChangeDiscAimAGV(uint QCID, uint AGVID)
        {
            // 注意目标小车应该有 AGVCommand 不然不会上到 QCTP
            List<AGV_Command> lTroToAGVComms, lAGVComms;
            List<string> lContIDs;
            string sOriAGVID;
            TwinRigidStoreUnit oUnit = this.oSimDataStore.dQCs[QCID].ViceTrolley.oTwinStoreUnit;

            lContIDs = new List<string>() { oUnit.ContID1 };
            if (oUnit.eUnitStoreType == StatusEnums.StoreType.DTEU)
                lContIDs.Add(oUnit.ContID2);

            lTroToAGVComms = this.oSimDataStore.dAGVCommands.Values.Where(u => lContIDs.Contains(u.CONTAINER_ID)).ToList();
            lAGVComms = this.oSimDataStore.dAGVCommands.Values.Where(u => u.CHE_ID.Trim() == AGVID.ToString()).ToList();
            sOriAGVID = lTroToAGVComms[0].CHE_ID;

            // 换车号，不要换箱号，双箱的问题。
            foreach (AGV_Command oComm in lTroToAGVComms)
            {
                oComm.CHE_ID = AGVID.ToString();
                oComm.COMMAND_VERSION = (Convert.ToInt32(oComm.COMMAND_VERSION) + 1).ToString();
                oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            }

            foreach (AGV_Command oComm in lAGVComms)
            {
                oComm.CHE_ID = sOriAGVID.Trim();
                oComm.COMMAND_VERSION = (Convert.ToInt32(oComm.COMMAND_VERSION) + 1).ToString();
                oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            }
        }


        #region 岸桥事件逻辑

        /// <summary>
        /// 岸桥的Step事件逻辑，从外部的Step事件到组件的Action事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnQCStepEvent(object sender, EventInfoArgs e)
        {
            uint QCID = Convert.ToUInt32(e.evnt.subModelName);
            QCDT oQC = this.oSimDataStore.dQCs[QCID];
            QCContStageRec oRec;

            if (oQC.MainTrolley.gStep2Action.condition)
                oQC.MainTrolley.gStep2Action.condition = false;
            if (oQC.ViceTrolley.gStep2Action.condition)
                oQC.ViceTrolley.gStep2Action.condition = false;
            foreach (QCPlatformSlot oSlot in oQC.Platform)
            {
                if (oSlot.gStep2Action.condition)
                    oSlot.gStep2Action.condition = false;
            }

            // 是否当前倍任务已经做完？
            switch (oQC.eMoveKind)
            {
                case Move_Kind.DSCH:
                    if (this.oSimDataStore.dQCContStageRecs[QCID].All(u => u.eQCContLocType == StatusEnums.QCContStage.AGV)
                        && oQC.MainTrolley.eTroSubProc == StatusEnums.QCMainTrolleySubProc.Done
                        && oQC.ViceTrolley.eTroSubProc == StatusEnums.QCViceTrolleySubProc.Done
                        && oQC.Platform.All(u => u.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None))
                        oQC.eMotionStatus = StatusEnums.MotionStatus.Done;
                    break;
                case Move_Kind.LOAD:
                    if (this.oSimDataStore.dQCContStageRecs[QCID].All(u => u.eQCContLocType == StatusEnums.QCContStage.AGV)
                        && oQC.MainTrolley.eTroSubProc == StatusEnums.QCMainTrolleySubProc.Done
                        && oQC.ViceTrolley.eTroSubProc == StatusEnums.QCViceTrolleySubProc.Done
                        && oQC.Platform.All(u => u.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None))
                        oQC.eMotionStatus = StatusEnums.MotionStatus.Done;
                    break;
                default:
                    break;
            }


            // 没做完的话需要驱动
            if (oQC.eMotionStatus == StatusEnums.MotionStatus.Working)
            {
                if (oQC.eMoveKind == Move_Kind.LOAD || oQC.eMoveKind == Move_Kind.DSCH)
                {
                    if (oQC.MainTrolley.eTroSubProc != StatusEnums.QCMainTrolleySubProc.Done
                        && SimStaticParas.SimDtStart.AddSeconds(Simulation.clock).CompareTo(oQC.MainTrolley.dtNextAction) >= 0)
                    {
                        oQC.MainTrolley.gStep2Action.condition = true;
                        oQC.MainTrolley.gStep2Action.attribute = oQC.MainTrolley.oToken;
                    }
                    if (oQC.ViceTrolley.eTroSubProc != StatusEnums.QCViceTrolleySubProc.Done
                        && SimStaticParas.SimDtStart.AddSeconds(Simulation.clock).CompareTo(oQC.ViceTrolley.dtNextAction) >= 0)
                    {
                        oQC.ViceTrolley.gStep2Action.condition = true;
                        oQC.ViceTrolley.gStep2Action.attribute = oQC.ViceTrolley.oToken;
                    }
                    foreach (QCPlatformSlot oSlot in oQC.Platform)
                    {
                        if (oSlot.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                        {
                            oRec = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault(u => u.oWI.CONTAINER_ID == oSlot.oTwinStoreUnit.ContID1);
                            if (oRec.oWI.CONTAINER_ID == oSlot.oTwinStoreUnit.ContID1 && oRec.eQCContLocType == StatusEnums.QCContStage.PlatformConfirm
                                && oSlot.eConfirmStatus != StatusEnums.ActionStatus.Done && SimStaticParas.SimDtStart.AddSeconds(Simulation.clock).CompareTo(oSlot.dtNextAction) >= 0)
                            {
                                oSlot.gStep2Action.condition = true;
                                oSlot.gStep2Action.attribute = oSlot.oToken;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 主小车的Action事件逻辑
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnQCMainTroActionEvent(object sender, EventInfoArgs e)
        {
            uint QCID = Convert.ToUInt32(e.evnt.subModelName);
            QCDT oQC = this.oSimDataStore.dQCs[QCID];
            QCContStageRec oContStageRec;
            QCPlatformSlot oSlot;
            double SecondsAddition;
            string ContID;
            STS_Command oComm;
            
            // 决定 Trolley 的下一个 QCTrolleyBasicStatus 和 MotionStatus
            switch (this.oSimDataStore.dQCs[QCID].MainTrolley.eTroStage)
            {
                case StatusEnums.QCTrolleyStage.BHigh:
                case StatusEnums.QCTrolleyStage.WSToB:
                case StatusEnums.QCTrolleyStage.LSToB:
                case StatusEnums.QCTrolleyStage.BRise:
                    oQC.MainTrolley.bLockPlatform = false;
                    oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.BHigh;

                    // 任务总是从BHigh开始，因此在BHigh处，若主小车没有预约，则需要判断是否还有下一任务？
                    // 以 QCMainTrolleySubProc 判断是否有任务以及任务的类型
                    // 以 eMotionStatus 判断本 Action 采取的动作
                    if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                    {
                        if (this.IfViceTroNextDiscMove(QCID))
                            oQC.MainTrolley.eTroSubProc = StatusEnums.QCMainTrolleySubProc.DiscContNormal;
                        else if (this.IfMainTroNextDiscMove(QCID))
                            oQC.MainTrolley.eTroSubProc = StatusEnums.QCMainTrolleySubProc.LoadContNormal;
                        else
                            oQC.MainTrolley.eTroSubProc = StatusEnums.QCMainTrolleySubProc.Done;
                    }

                    switch (oQC.MainTrolley.eTroSubProc)
                    {
                        case StatusEnums.QCMainTrolleySubProc.DiscContNormal:
                            if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                            {
                                if (this.MainTroReserveNextDiscMove(QCID))
                                    oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.BToWS;
                            }
                            else if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                            {
                                if (!oQC.ViceTrolley.bLockPlatform)
                                {
                                    oQC.MainTrolley.bLockPlatform = true;
                                    oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.BToLS;
                                }
                            }
                            break;
                        case StatusEnums.QCMainTrolleySubProc.LoadContNormal:
                            if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                            {
                                if (!oQC.ViceTrolley.bLockPlatform && this.MainTroReserveNextLoadMove(QCID))
                                {
                                    oQC.MainTrolley.bLockPlatform = true;
                                    oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.BToLS;
                                }
                            }
                            else if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                                oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.BToWS;
                            break;
                    }
                    break;
                case StatusEnums.QCTrolleyStage.WSHigh:
                case StatusEnums.QCTrolleyStage.BToWS:
                case StatusEnums.QCTrolleyStage.WSRise:
                    oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSHigh;
                    switch (oQC.MainTrolley.eTroSubProc)
                    {
                        case StatusEnums.QCMainTrolleySubProc.DiscContNormal:
                            if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Reserved)
                                oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSFall;
                            else if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                                oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSToB;
                            break;
                        case StatusEnums.QCMainTrolleySubProc.LoadContNormal:
                            if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                                oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSFall;
                            else if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                                oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSToB;
                            break;
                    }
                    break;
                case StatusEnums.QCTrolleyStage.WSFall:
                    // 交割
                    oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSLow;

                    switch (oQC.MainTrolley.eTroSubProc)
                    {
                        case StatusEnums.QCMainTrolleySubProc.DiscContNormal:
                            // 卸船时只有岸桥自己知道发生了什么
                            ContID = oQC.MainTrolley.oTwinStoreUnit.ContID1;
                            oContStageRec = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                            if (oContStageRec != null && !string.IsNullOrWhiteSpace(oContStageRec.oWI.CONTAINER_ID))
                            {
                                oContStageRec.eQCContLocType = StatusEnums.QCContStage.MainTro;
                                oQC.MainTrolley.oTwinStoreUnit.ContOccupy(ContID);
                                if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreType == StatusEnums.StoreType.DTEU)
                                {
                                    ContID = oQC.MainTrolley.oTwinStoreUnit.ContID2;
                                    oContStageRec = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                                    if (oContStageRec != null && !string.IsNullOrWhiteSpace(oContStageRec.oWI.CONTAINER_ID))
                                    {
                                        oContStageRec.eQCContLocType = StatusEnums.QCContStage.MainTro;
                                        oQC.MainTrolley.oTwinStoreUnit.ContOccupy(ContID);
                                    }
                                }
                            }
                            break;
                        case StatusEnums.QCMainTrolleySubProc.LoadContNormal:
                            // 装船则需要终结 Command，否则 MainTrolley 再无记录
                            ContID = oQC.MainTrolley.oTwinStoreUnit.ContID1;
                            oContStageRec = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                            if (oContStageRec != null && !string.IsNullOrWhiteSpace(oContStageRec.oWI.CONTAINER_ID))
                            {
                                oContStageRec.eQCContLocType = StatusEnums.QCContStage.Vessel;
                                oQC.MainTrolley.oTwinStoreUnit.ContRemove(ContID);
                                oComm = this.oSimDataStore.dSTSCommands.Values.FirstOrDefault<STS_Command>(u => u.CONTAINER_ID == ContID);
                                if (oComm != null || oComm.CONTAINER_ID == ContID)
                                {
                                    oComm.JOB_STATUS = "4";
                                    oComm.VERSION++;
                                }
                            }
                            if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreType == StatusEnums.StoreType.DTEU)
                            {
                                ContID = oQC.MainTrolley.oTwinStoreUnit.ContID2;
                                oContStageRec = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                                if (oContStageRec != null && !string.IsNullOrWhiteSpace(oContStageRec.oWI.CONTAINER_ID))
                                {
                                    oContStageRec.eQCContLocType = StatusEnums.QCContStage.Vessel;
                                    oQC.MainTrolley.oTwinStoreUnit.ContRemove(ContID);
                                    oComm = this.oSimDataStore.dSTSCommands.Values.FirstOrDefault<STS_Command>(u => u.CONTAINER_ID == ContID);
                                    if (oComm != null && oComm.CONTAINER_ID == ContID)
                                    {
                                        oComm.JOB_STATUS = "4";
                                        oComm.VERSION++;
                                    }
                                }
                            }
                            break;
                    }

                    // 后续一定是起升
                    oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSRise;
                    break;
                case StatusEnums.QCTrolleyStage.BFall:
                    // 注意目前不会到达这里
                    oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.BLow;
                    switch (oQC.MainTrolley.eTroSubProc)
                    {
                        case StatusEnums.QCMainTrolleySubProc.DiscContToApron:
                        case StatusEnums.QCMainTrolleySubProc.LoadContFromApron:
                        case StatusEnums.QCMainTrolleySubProc.ChangeSpreader:
                        case StatusEnums.QCMainTrolleySubProc.LoadCover:
                        case StatusEnums.QCMainTrolleySubProc.DiscCover:
                            oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.BRise;
                            break;
                    }
                    break;
                case StatusEnums.QCTrolleyStage.LSHigh:
                case StatusEnums.QCTrolleyStage.BToLS:
                case StatusEnums.QCTrolleyStage.LSRise:
                    oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSHigh;
                    switch (oQC.MainTrolley.eTroSubProc)
                    {
                        case StatusEnums.QCMainTrolleySubProc.DiscContNormal:
                            if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                                oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSToB;
                            else if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                                oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSFall;
                            break;
                        case StatusEnums.QCMainTrolleySubProc.LoadContNormal:
                            if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Reserved)
                                oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSFall;
                            else if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                                oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSToB;
                            break;
                    }
                    break;
                case StatusEnums.QCTrolleyStage.LSFall:
                    oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSLow;
                    switch (oQC.MainTrolley.eTroSubProc)
                    {
                        case StatusEnums.QCMainTrolleySubProc.DiscContNormal:
                            // 卸船时箱子交割到平台，并生成Command
                            ContID = oQC.MainTrolley.oTwinStoreUnit.ContID1;
                            oSlot = oQC.Platform.Find(u => u.oTwinStoreUnit.CheckContStoreStatus(ContID) == StatusEnums.StoreStage.Reserved);
                            oQC.MainTrolley.oTwinStoreUnit.ContRemove(ContID);
                            oSlot.oTwinStoreUnit.ContOccupy(ContID);
                            oContStageRec = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                            if (oContStageRec != null && !string.IsNullOrWhiteSpace(oContStageRec.oWI.CONTAINER_ID))
                                oContStageRec.eQCContLocType = StatusEnums.QCContStage.PlatformConfirm;
                            this.GenerateNewSTSDiscCommand(oContStageRec.oResJob);

                            if (oSlot.oTwinStoreUnit.eUnitStoreType == StatusEnums.StoreType.DTEU)
                            {
                                ContID = oQC.MainTrolley.oTwinStoreUnit.ContID2;
                                oQC.MainTrolley.oTwinStoreUnit.ContRemove(ContID);
                                oSlot.oTwinStoreUnit.ContOccupy(ContID);
                                oContStageRec = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                                if (oContStageRec != null && !string.IsNullOrWhiteSpace(oContStageRec.oWI.CONTAINER_ID))
                                    oContStageRec.eQCContLocType = StatusEnums.QCContStage.PlatformConfirm;
                                this.GenerateNewSTSDiscCommand(oContStageRec.oResJob);
                            }

                            break;
                        case StatusEnums.QCMainTrolleySubProc.LoadContNormal:
                            // 装船时箱子交割到主小车
                            ContID = oQC.MainTrolley.oTwinStoreUnit.ContID1;
                            oSlot = oQC.Platform.First(u => u.oTwinStoreUnit.CheckContStoreStatus(ContID) == StatusEnums.StoreStage.Stored);
                            oQC.MainTrolley.oTwinStoreUnit.ContOccupy(ContID);
                            oSlot.oTwinStoreUnit.ContRemove(ContID);
                            oContStageRec = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                            if (oContStageRec != null && !string.IsNullOrWhiteSpace(oContStageRec.oWI.CONTAINER_ID))
                                oContStageRec.eQCContLocType = StatusEnums.QCContStage.MainTro;
                            oSlot.eConfirmStatus = StatusEnums.ActionStatus.Null;

                            if (oQC.MainTrolley.oTwinStoreUnit.eUnitStoreType == StatusEnums.StoreType.DTEU)
                            {
                                ContID = oQC.MainTrolley.oTwinStoreUnit.ContID2;
                                oQC.MainTrolley.oTwinStoreUnit.ContOccupy(ContID);
                                oSlot.oTwinStoreUnit.ContRemove(ContID);
                                oContStageRec = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                                if (oContStageRec != null && !string.IsNullOrWhiteSpace(oContStageRec.oWI.CONTAINER_ID))
                                    oContStageRec.eQCContLocType = StatusEnums.QCContStage.MainTro;
                            }

                            break;
                    }

                    oQC.MainTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSRise;
                    break;
                case StatusEnums.QCTrolleyStage.WSLow:
                case StatusEnums.QCTrolleyStage.LSLow:
                case StatusEnums.QCTrolleyStage.BLow:
                    break;
            }

            // 根据 MotionStatus 和 QCTrolleyStatus 决定下一事件的时间间隔

            switch (oQC.MainTrolley.eTroStage)
            {
                case StatusEnums.QCTrolleyStage.BToWS:
                    SecondsAddition = oQC.MainTrolley.oQCTrolleyTimeStat.oStatBToWS.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.BToLS:
                    SecondsAddition = oQC.MainTrolley.oQCTrolleyTimeStat.oStatBToLS.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.BFall:
                    SecondsAddition = oQC.MainTrolley.oQCTrolleyTimeStat.oStatBFall.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.BRise:
                    SecondsAddition = oQC.MainTrolley.oQCTrolleyTimeStat.oStatBRise.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.WSFall:
                    SecondsAddition = oQC.MainTrolley.oQCTrolleyTimeStat.oStatWSFall.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.WSRise:
                    SecondsAddition = oQC.MainTrolley.oQCTrolleyTimeStat.oStatWSRise.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.WSToB:
                    SecondsAddition = oQC.MainTrolley.oQCTrolleyTimeStat.oStatWSToB.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.LSFall:
                    SecondsAddition = oQC.MainTrolley.oQCTrolleyTimeStat.oStatLSFall.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.LSRise:
                    SecondsAddition = oQC.MainTrolley.oQCTrolleyTimeStat.oStatLSRise.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.LSToB:
                    SecondsAddition = oQC.MainTrolley.oQCTrolleyTimeStat.oStatLSToB.Avg;
                    break;
                default:
                    SecondsAddition = 1;
                    break;
            }

            oQC.MainTrolley.dtNextAction = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock).AddSeconds(SecondsAddition);
        }

        public void OnQCViceTroActionEvent(object sender, EventInfoArgs e)
        {
            uint QCID = Convert.ToUInt32(e.evnt.subModelName);
            QCDT oQC = this.oSimDataStore.dQCs[QCID];
            QCContStageRec oContStageRec1, oContStageRec2;
            string ContID1, ContID2;
            QCPlatformSlot oSlot;
            AGV oAGV;
            AGV_STATUS oAgvStatus;
            List<AGV> lAGVs;
            List<STS_Command> lSTSComms;
            List<uint> lQCTPIDs;
            AGV_Command oAGVComm;
            double SecondsAddition;

            switch (oQC.ViceTrolley.eTroStage)
            {
                case StatusEnums.QCTrolleyStage.BHigh:
                case StatusEnums.QCTrolleyStage.LSToB:
                case StatusEnums.QCTrolleyStage.WSToB:
                case StatusEnums.QCTrolleyStage.BRise:
                    oQC.ViceTrolley.bLockPlatform = false;
                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.BHigh;

                    if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                    {
                        if (this.IfViceTroNextLoadMove(QCID))
                            oQC.ViceTrolley.eTroSubProc = StatusEnums.QCViceTrolleySubProc.LoadContNormal;
                        else if (this.IfViceTroNextDiscMove(QCID))
                            oQC.ViceTrolley.eTroSubProc = StatusEnums.QCViceTrolleySubProc.DiscContNormal;
                        else
                            oQC.ViceTrolley.eTroSubProc = StatusEnums.QCViceTrolleySubProc.Done;
                    }

                    switch (oQC.ViceTrolley.eTroSubProc)
                    {
                        case StatusEnums.QCViceTrolleySubProc.DiscContNormal:
                            if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                            {
                                // 副小车卸箱，箱子到平台以后再进去
                                if (!oQC.MainTrolley.bLockPlatform && this.ViceTroReserveNextDiscMove(QCID))
                                {
                                    oQC.ViceTrolley.bLockPlatform = true;
                                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.BToWS;
                                }
                            }
                            else
                                oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.BToLS;
                            break;
                        case StatusEnums.QCViceTrolleySubProc.LoadContNormal:
                            if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                            {
                                oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.BToLS;
                            }
                            else if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                            {
                                if (!oQC.MainTrolley.bLockPlatform && this.TrolleyReservePlatform(oQC.ViceTrolley))
                                {
                                    oQC.ViceTrolley.bLockPlatform = true;
                                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.BToWS;
                                }
                            }
                            break;
                    }
                    break;
                case StatusEnums.QCTrolleyStage.WSHigh:
                case StatusEnums.QCTrolleyStage.BToWS:
                case StatusEnums.QCTrolleyStage.WSRise:
                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSHigh;
                    switch (oQC.ViceTrolley.eTroSubProc)
                    {
                        case StatusEnums.QCViceTrolleySubProc.DiscContNormal:
                            if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Reserved)
                                oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSFall;
                            else if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                                oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSToB;
                            break;
                        case StatusEnums.QCViceTrolleySubProc.LoadContNormal:
                            if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                                oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSToB;
                            else if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                                oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSFall;
                            break;
                    }
                    break;
                case StatusEnums.QCTrolleyStage.WSFall:
                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSLow;
                    // 在平台上
                    switch (oQC.ViceTrolley.eTroSubProc)
                    {
                        case StatusEnums.QCViceTrolleySubProc.DiscContNormal:
                            // 卸船时箱子从平台到副小车
                            ContID1 = oQC.ViceTrolley.oTwinStoreUnit.ContID1;
                            oSlot = oQC.Platform.FirstOrDefault<QCPlatformSlot>(u => u.oTwinStoreUnit.CheckContStoreStatus(ContID1) == StatusEnums.StoreStage.Stored);
                            if (oSlot == null)
                                return;
                            oSlot.oTwinStoreUnit.ContRemove(ContID1);
                            oQC.ViceTrolley.oTwinStoreUnit.ContOccupy(ContID1);
                            oContStageRec1 = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID1);
                            if (oContStageRec1 != null && !string.IsNullOrWhiteSpace(oContStageRec1.oWI.CONTAINER_ID))
                                oContStageRec1.eQCContLocType = StatusEnums.QCContStage.ViceTro;
                            oSlot.eConfirmStatus = StatusEnums.ActionStatus.Null;

                            if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreType == StatusEnums.StoreType.DTEU)
                            {
                                ContID1 = oQC.ViceTrolley.oTwinStoreUnit.ContID2;
                                oSlot.oTwinStoreUnit.ContRemove(ContID1);
                                oQC.ViceTrolley.oTwinStoreUnit.ContOccupy(ContID1);
                                oContStageRec1 = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID1);
                                if (oContStageRec1 != null && !string.IsNullOrWhiteSpace(oContStageRec1.oWI.CONTAINER_ID))
                                    oContStageRec1.eQCContLocType = StatusEnums.QCContStage.ViceTro;
                            }

                            break;
                        case StatusEnums.QCViceTrolleySubProc.LoadContNormal:
                            // 装船时箱子从副小车到平台
                            ContID1 = oQC.ViceTrolley.oTwinStoreUnit.ContID1;
                            oSlot = oQC.Platform.FirstOrDefault<QCPlatformSlot>(u => u.oTwinStoreUnit.CheckContStoreStatus(ContID1) == StatusEnums.StoreStage.Reserved);
                            if (oSlot == null)
                                return;
                            oQC.ViceTrolley.oTwinStoreUnit.ContRemove(ContID1);
                            oSlot.oTwinStoreUnit.ContOccupy(ContID1);
                            oContStageRec1 = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID1);
                            if (oContStageRec1 != null && !string.IsNullOrWhiteSpace(oContStageRec1.oWI.CONTAINER_ID))
                                oContStageRec1.eQCContLocType = StatusEnums.QCContStage.PlatformConfirm;

                            if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreType == StatusEnums.StoreType.DTEU)
                            {
                                ContID1 = oQC.ViceTrolley.oTwinStoreUnit.ContID2;
                                oQC.ViceTrolley.oTwinStoreUnit.ContRemove(ContID1);
                                oSlot.oTwinStoreUnit.ContOccupy(ContID1);
                                oContStageRec1 = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID1);
                                if (oContStageRec1 != null && !string.IsNullOrWhiteSpace(oContStageRec1.oWI.CONTAINER_ID))
                                    oContStageRec1.eQCContLocType = StatusEnums.QCContStage.PlatformConfirm;
                            }

                            break;
                    }
                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.WSRise;
                    break;
                case StatusEnums.QCTrolleyStage.BFall:
                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.BRise;
                    // 应该不会到这里
                    break;
                case StatusEnums.QCTrolleyStage.LSHigh:
                case StatusEnums.QCTrolleyStage.BToLS:
                case StatusEnums.QCTrolleyStage.LSRise:
                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSHigh;
                    switch (oQC.ViceTrolley.eTroSubProc)
                    {
                        case StatusEnums.QCViceTrolleySubProc.DiscContNormal:
                            if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                                oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSToB;
                            else if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                            {
                                // 检查是否有 AGV 到位，只要有空车到就下落。不然，等待。
                                lAGVs = this.oSimDataStore.dAGVs.Values.Where(u => u.eJobType == JobType.DISC && u.eAGVStage == StatusEnums.AGVWorkStage.AtQCTP
                                    && u.eAGVStageStatus == StatusEnums.ActionStatus.Ready && this.oSimDataStore.dLanes[u.CurrLaneID].CheNo == QCID).ToList();

                                if (lAGVs.Count > 0)
                                {
                                    lAGVs.Sort(this.AGVTimeCompareFunc);
                                    lAGVs[0].eAGVStageStatus = StatusEnums.ActionStatus.Doing;
                                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSFall;
                                }
                            }
                            break;
                        case StatusEnums.QCViceTrolleySubProc.LoadContNormal:
                            if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                            {
                                if (this.ViceTroReserveNextLoadMove(QCID, true))
                                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSFall;
                            }
                            else if (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                                oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSToB;
                            break;
                    }

                    break;
                case StatusEnums.QCTrolleyStage.LSFall:
                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSLow;

                    // 岸边与 AGV 交接
                    switch (oQC.ViceTrolley.eTroSubProc)
                    {
                        case StatusEnums.QCViceTrolleySubProc.DiscContNormal:
                            oAGV = this.oSimDataStore.dAGVs.Values.FirstOrDefault<AGV>(u => u.eJobType == JobType.DISC && u.eAGVStage == StatusEnums.AGVWorkStage.AtQCTP
                                    && u.eAGVStageStatus == StatusEnums.ActionStatus.Doing && this.oSimDataStore.dLanes[u.CurrLaneID].CheNo == QCID);

                            if (oAGV == null)
                                return;

                            oAgvStatus = this.oSimDataStore.dAGVStatus.Values.FirstOrDefault<AGV_STATUS>(u => !string.IsNullOrWhiteSpace(u.CHE_ID) 
                                && Convert.ToUInt32(u.CHE_ID) == oAGV.ID);

                            if (oAgvStatus == null)
                                return;

                            switch (oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreType)
                            {
                                case StatusEnums.StoreType.STEU:
                                case StatusEnums.StoreType.FEU:
                                case StatusEnums.StoreType.FFEU:
                                    ContID1 = oQC.ViceTrolley.oTwinStoreUnit.ContID1;
                                    oContStageRec1 = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID1);
                                    if (oContStageRec1 == null)
                                        return;
                                    oQC.ViceTrolley.oTwinStoreUnit.ContRemove(ContID1);
                                    oContStageRec1.eQCContLocType = StatusEnums.QCContStage.AGV;
                                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSRise;
                                    oAGV.oTwinStoreUnit.ContOccupy(ContID1);
                                    oAGV.eAGVStageStatus = StatusEnums.ActionStatus.Done;
                                    oAgvStatus.CONTAINER_ID_1 = ContID1;
                                    oAgvStatus.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                                    break;
                                case StatusEnums.StoreType.DTEU:
                                    ContID1 = oQC.ViceTrolley.oTwinStoreUnit.ContID1;
                                    oContStageRec1 = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID1);
                                    ContID2 = oQC.ViceTrolley.oTwinStoreUnit.ContID2;
                                    oContStageRec2 = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID2);
                                    if (oContStageRec1 == null || oContStageRec2 == null)
                                        return;
                                    oQC.ViceTrolley.oTwinStoreUnit.ContRemove(ContID1);
                                    oQC.ViceTrolley.oTwinStoreUnit.ContRemove(ContID2);
                                    oContStageRec1.eQCContLocType = StatusEnums.QCContStage.AGV;
                                    oContStageRec2.eQCContLocType = StatusEnums.QCContStage.AGV;
                                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSRise;
                                    oAGV.oTwinStoreUnit.ContOccupy(ContID1);
                                    oAGV.oTwinStoreUnit.ContOccupy(ContID2);
                                    oAGV.eAGVStageStatus = StatusEnums.ActionStatus.Done;
                                    oAgvStatus.CONTAINER_ID_1 = ContID1;
                                    oAgvStatus.CONTAINER_ID_2 = ContID2;
                                    oAgvStatus.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                                    break;
                                default:
                                    return;
                            }

                            // STS_Command 更新
                            lSTSComms = this.oSimDataStore.dSTSCommands.Values.Where(u => u.CONTAINER_ID == oQC.ViceTrolley.oTwinStoreUnit.ContID1 
                                || u.CONTAINER_ID == oQC.ViceTrolley.oTwinStoreUnit.ContID2).ToList();
                            foreach (STS_Command oComm in lSTSComms)
                            {
                                oComm.COMMAND_VERSION = (Convert.ToInt32(oComm.COMMAND_VERSION) + 1).ToString();
                                oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                                oComm.END_TIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                                oComm.JOB_STATUS = Convert.ToByte(TaskStatus.Complete).ToString();
                            }

                            // 岸桥单独调试，箱子抹杀掉
                            //ContID1 = oQC.ViceTrolley.oTwinStoreUnit.ContID1;
                            //oContStageRec1 = this.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID1);
                            //if (oContStageRec1 == null || !string.IsNullOrWhiteSpace(oContStageRec1.oWI.CONTAINER_ID))
                            //    oContStageRec1.eQCContLocType = StatusEnums.QCContStage.AGV;
                            //oQC.ViceTrolley.oTwinStoreUnit.Reset();

                            //oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSRise;

                            break;
                        case StatusEnums.QCViceTrolleySubProc.LoadContNormal:
                            //ContID = oQC.ViceTrolley.oTwinStoreUnit.ContID1;
                            //SizeStr = oQC.ViceTrolley.oTwinStoreUnit.eContSize1.ToString();
                            //oAGVComm = this.oSimDataStore.dAGVCommands.Values.FirstOrDefault<AGV_Command>(u => u.CONTAINER_ID == ContID);
                            //if (oAGVComm == null || oAGVComm.CONTAINER_ID != ContID || string.IsNullOrWhiteSpace(oAGVComm.CHE_ID))
                            //    return;
                            //oAGV = this.oSimDataStore.dAGVs.Values.FirstOrDefault<AGV>(u => u.ID == Convert.ToUInt32(oAGVComm.CHE_ID));
                            //if (oAGV == null || oAGV.ID == 0)
                            //    return;

                            //oQC.ViceTrolley.oTwinStoreUnit.ContOccupy(ContID);
                            //oAGV.oTwinStoreUnit.ContRemove(ContID);
                            //oContStageRec = this.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                            //if (oContStageRec == null || string.IsNullOrWhiteSpace(oContStageRec.oWI.CONTAINER_ID))
                            //    return;
                            //oContStageRec.eQCContLocType = StatusEnums.QCContStage.ViceTro;

                            //if (!string.IsNullOrWhiteSpace(oQC.ViceTrolley.oTwinStoreUnit.ContID2))
                            //{
                            //    ContID = oQC.MainTrolley.oTwinStoreUnit.ContID2;
                            //    SizeStr = oQC.MainTrolley.oTwinStoreUnit.eContSize2.ToString();
                            //    oQC.ViceTrolley.oTwinStoreUnit.ContOccupy(ContID);
                            //    oAGV.oTwinStoreUnit.ContRemove(ContID);
                            //    oContStageRec = this.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                            //    if (oContStageRec == null || string.IsNullOrWhiteSpace(oContStageRec.oWI.CONTAINER_ID))
                            //        return;
                            //    oContStageRec.eQCContLocType = StatusEnums.QCContStage.ViceTro;
                            //}

                            // Command 更新
                            
                            lSTSComms = this.oSimDataStore.dSTSCommands.Values.Where(u => u.CONTAINER_ID == oQC.ViceTrolley.oTwinStoreUnit.ContID1 
                                || u.CONTAINER_ID == oQC.ViceTrolley.oTwinStoreUnit.ContID2).ToList();
                            foreach (STS_Command oComm in lSTSComms)
                            {
                                oComm.COMMAND_VERSION = (Convert.ToInt32(oComm.COMMAND_VERSION) + 1).ToString();
                                oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                                oComm.END_TIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                                oComm.JOB_STATUS = Convert.ToByte(TaskStatus.Enter).ToString();
                            }

                            // 岸桥单独调试，箱子凭空来
                            ContID1 = oQC.ViceTrolley.oTwinStoreUnit.ContID1;
                            oContStageRec1 = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID1);
                            if (oContStageRec1 == null || !string.IsNullOrWhiteSpace(oContStageRec1.oWI.CONTAINER_ID))
                                oContStageRec1.eQCContLocType = StatusEnums.QCContStage.AGV;
                            oQC.ViceTrolley.oTwinStoreUnit.Reset();
                            oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSRise;

                            break;
                    }

                    oQC.ViceTrolley.eTroStage = StatusEnums.QCTrolleyStage.LSRise;
                    break;
                case StatusEnums.QCTrolleyStage.BLow:
                case StatusEnums.QCTrolleyStage.WSLow:
                case StatusEnums.QCTrolleyStage.LSLow:
                    break;
            }

            // 根据 MotionStatus 和 QCTrolleyStatus 决定下一事件的时间间隔

            switch (oQC.ViceTrolley.eTroStage)
            {
                case StatusEnums.QCTrolleyStage.BToWS:
                    SecondsAddition = oQC.ViceTrolley.oQCTrolleyTimeStat.oStatBToWS.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.BToLS:
                    SecondsAddition = oQC.ViceTrolley.oQCTrolleyTimeStat.oStatBToLS.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.BFall:
                    SecondsAddition = oQC.ViceTrolley.oQCTrolleyTimeStat.oStatBFall.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.BRise:
                    SecondsAddition = oQC.ViceTrolley.oQCTrolleyTimeStat.oStatBRise.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.WSFall:
                    SecondsAddition = oQC.ViceTrolley.oQCTrolleyTimeStat.oStatWSFall.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.WSRise:
                    SecondsAddition = oQC.ViceTrolley.oQCTrolleyTimeStat.oStatWSRise.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.WSToB:
                    SecondsAddition = oQC.ViceTrolley.oQCTrolleyTimeStat.oStatWSToB.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.LSFall:
                    SecondsAddition = oQC.ViceTrolley.oQCTrolleyTimeStat.oStatLSFall.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.LSRise:
                    SecondsAddition = oQC.ViceTrolley.oQCTrolleyTimeStat.oStatLSRise.Avg;
                    break;
                case StatusEnums.QCTrolleyStage.LSToB:
                    SecondsAddition = oQC.ViceTrolley.oQCTrolleyTimeStat.oStatLSToB.Avg;
                    break;
                default:
                    SecondsAddition = 1;
                    break;
            }

            oQC.ViceTrolley.dtNextAction = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock).AddSeconds(SecondsAddition);

        }

        public void OnPlatformSlotActionEvent(object sender, EventInfoArgs e)
        {
            string[] sInfo = e.evnt.subModelName.Split(new char[] { ':' });
            uint QCID = Convert.ToUInt32(sInfo[0]);
            QCDT oQC = this.oSimDataStore.dQCs[QCID];
            uint SlotID = Convert.ToUInt32(sInfo[1]);
            QCPlatformSlot oSlot = oQC.Platform.Find(u => u.ID == SlotID);
            QCContStageRec oRec;
            string ContID;

            switch (oSlot.eConfirmStatus)
            {
                case StatusEnums.ActionStatus.Ready:
                case StatusEnums.ActionStatus.Null:
                    oSlot.eConfirmStatus = StatusEnums.ActionStatus.Doing;
                    oSlot.dtNextAction = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock).AddSeconds(oSlot.oConfirmStat.Avg);
                    break;
                case StatusEnums.ActionStatus.Doing:
                    oSlot.eConfirmStatus = StatusEnums.ActionStatus.Done;
                    ContID = oSlot.oTwinStoreUnit.ContID1;
                    oRec = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                    if (oRec == null || !string.IsNullOrWhiteSpace(oRec.oWI.CONTAINER_ID))
                        oRec.eQCContLocType = StatusEnums.QCContStage.Platform;
                    if (oSlot.oTwinStoreUnit.eUnitStoreType == StatusEnums.StoreType.DTEU)
                    {
                        ContID = oSlot.oTwinStoreUnit.ContID2;
                        oRec = this.oSimDataStore.dQCContStageRecs[QCID].FirstOrDefault<QCContStageRec>(u => u.oWI.CONTAINER_ID == ContID);
                        if (oRec == null || !string.IsNullOrWhiteSpace(oRec.oWI.CONTAINER_ID))
                            oRec.eQCContLocType = StatusEnums.QCContStage.Platform;
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 用于 AGV 按照最后一次状态更新时间排序
        /// </summary>
        /// <param name="oComm1">AGVCommand1</param>
        /// <param name="oComm2">AGVCommand2</param>
        /// <returns></returns>
        private int AGVTimeCompareFunc(AGV oA1, AGV oA2)
        {
            AGV_STATUS oAgvStatus1, oAgvStatus2;
            int bRet = 0;

            if (oA1.Equals(oA2) || oA1.ID == oA2.ID)
                return bRet;

            oAgvStatus1 = this.oSimDataStore.dAGVStatus.Values.FirstOrDefault<AGV_STATUS>(u => !string.IsNullOrWhiteSpace(u.CHE_ID) && oA1.ID == Convert.ToUInt32(u.CHE_ID));
            oAgvStatus2 = this.oSimDataStore.dAGVStatus.Values.FirstOrDefault<AGV_STATUS>(u => !string.IsNullOrWhiteSpace(u.CHE_ID) && oA2.ID == Convert.ToUInt32(u.CHE_ID));

            if (oAgvStatus1 == null || oAgvStatus2 == null)
                return bRet;

            return oAgvStatus1.UPDATED.CompareTo(oAgvStatus2.UPDATED);
        }

        #endregion
    }
 
}
