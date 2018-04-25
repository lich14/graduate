using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DBDefine.YardMap;
using SSWPF.Define;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;

namespace SSWPF.SimManagers
{
    /// <summary>
    /// 管作业线布置的，即哪台岸桥该摆在哪个位置，因此 QCTP 的位置和 PB 的进出安排均受此变化
    /// </summary>
    public class SimHandleLineManager
    {
        public event EventHandler<ProjectToViewFrameEventArgs> ProjectToViewFrameEvent;
        public event EventHandler<ProjectToInfoFrameEventArgs> ProjectToInfoFrameEvent;

        public SimDataStore oSimDataStore;

        // 自定常量
        private readonly double MinSTSPosIntv;
        private readonly int NumPBIO;
        private readonly double NoPBSideLength;                              
        private readonly double AGVMaxSpeed;
        private readonly double Acceleration;
        private readonly double AbreastQCTPsIntv;
        private readonly double AGVLength;
        private readonly double AntennasDistance;
        private readonly double AGVObliqueSpeed;
        private readonly double ObliqueLockDist;
        private readonly double InvalidVMSIDX;
        private readonly double ObliqueMove;
        private readonly double AdjustDistanceOblique;

        // 因定常量
        private readonly double AGVLockDistForOneside;
        private readonly double SafeDistForObliqueOnce;
        private readonly double SafeDistForObliqueTwice;
        private readonly double DistBeforeOrAfterOblique;
        
        // 变量
        private uint QCExpectTimeInd;
        public bool IsInited;

        public SimHandleLineManager()
        {
            // 只读参数赋值
            // 自定量
            this.MinSTSPosIntv = 51.5;
            this.NumPBIO = 3;
            // 每台岸桥边的无QCPB距离都是确定的，且不分是自己的QCPB还是别人的QCPB。统一取20m。
            this.NoPBSideLength = 20; 
            this.AGVMaxSpeed = 5.8;
            this.Acceleration = 0.3;
            this.AbreastQCTPsIntv = 4;
            this.AGVLength = 15;
            this.AntennasDistance = 12;
            this.AGVObliqueSpeed = 1;
            this.ObliqueLockDist = 8;
            this.InvalidVMSIDX = -1;
            this.ObliqueMove = 10;
            this.AdjustDistanceOblique = 2;

            // 因定量
            this.AGVLockDistForOneside = this.AGVLength * 0.75 + 4;
            this.SafeDistForObliqueOnce = 2 * (this.AGVLockDistForOneside + this.ObliqueLockDist + 2);
            this.SafeDistForObliqueTwice = this.SafeDistForObliqueOnce + this.AntennasDistance;
            this.DistBeforeOrAfterOblique = this.AGVLockDistForOneside + this.ObliqueLockDist + 2 - this.AntennasDistance / 2;
        }

        public SimHandleLineManager(SimDataStore oSDS)
            : this()
        {
            this.oSimDataStore = oSDS;
        }

        /// <summary>
        /// 初始化，返回受改变的 QCTP 和 TP 列表
        /// </summary>
        /// <param name="oPPTSimFrame">投影构件</param>
        /// <returns>成功返回true, 失败返回false</returns>
        public bool Init()
        {
            if (this.oSimDataStore == null) 
                return false;

            // 准备维护对象
            this.oSimDataStore.lQCExpectTimes = new List<SimExpectTime>();
            this.oSimDataStore.dHandleLinePlans = new Dictionary<string, HandleLinePlan>();
            this.oSimDataStore.dQCPosPlans = new Dictionary<uint, QCPosPlan>();

            // 生成岸桥作业点
            this.GenerateAllQCWorkPoints();

            // 生成所有作业线计划
            this.GenerateAllHandleLinePlans();

            // 初始化岸桥位置计划
            this.InitQCPosPlans();

            // 初始化完成标记
            this.IsInited = true;

            return true;
        }

        /// <summary>
        /// 生成所有岸桥作业点
        /// </summary>
        /// <param name="VesID"></param>
        /// <returns></returns>
        private bool GenerateAllQCWorkPoints()
        {
            QCWorkPoint oQWP;

            foreach (Vessel oVes in this.oSimDataStore.dVessels.Values)
            {
                for (int i = 0; i < oVes.oType.CabinNum; i++)
                {
                    if (oVes.eBerthWay == StatusEnums.BerthWay.L)
                    {
                        for (int j = 1; j <= 3; j++)
                        {
                            oQWP = new QCWorkPoint();
                            oQWP.VesID = oVes.ID;
                            oQWP.Bay = Convert.ToUInt32(4 * (oVes.oType.CabinNum - i) - 4 + j);
                            oQWP.IndStr = oVes.ID.ToString() + "." + oQWP.Bay.ToString().PadLeft(3, '0');
                            oQWP.BasePoint.X = oVes.BeginMeter + oVes.oType.SternSpaceLen + oVes.oType.SingleCabinLength * (i + (double)j / 4);
                            oQWP.BasePoint.Y = 309.2;
                            this.oSimDataStore.dQCWorkPoints.Add(oQWP.IndStr, oQWP);
                        }
                    }
                    else
                    {
                        for (int j = 1; j <= 3; j++)
                        {
                            oQWP = new QCWorkPoint();
                            oQWP.VesID = oVes.ID;
                            oQWP.Bay = Convert.ToUInt32(4 * i + j);
                            oQWP.IndStr = oVes.ID.ToString() + "." + oQWP.Bay.ToString().PadLeft(3, '0');
                            oQWP.BasePoint.X = oVes.BeginMeter + oVes.oType.BowSpaceLen + oVes.oType.SingleCabinLength * (i + (double)j / 4);
                            oQWP.BasePoint.Y = 309.2;
                            this.oSimDataStore.dQCWorkPoints.Add(oQWP.IndStr, oQWP);
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 生成所有船舶的作业线计划
        /// </summary>
        /// <returns></returns>
        private bool GenerateAllHandleLinePlans()
        {
            HandleLinePlan oHLP;
            Voyage oV;
            QCWorkPoint oQWP;

            foreach (STS_WORK_QUEUE_STATUS oWQ in this.oSimDataStore.dWorkQueues.Values)
            {
                oV = this.oSimDataStore.dVoyages.Values.FirstOrDefault(u => u.ID == Convert.ToUInt32(oWQ.SHIP_NO));
                if (oV == null)
                    continue;

                oQWP = this.oSimDataStore.dQCWorkPoints.Values.FirstOrDefault(u => u.VesID == oV.VesID && u.Bay == Convert.ToUInt32(oWQ.VESSEL_BAY));
                if (oQWP == null)
                    continue;

                oHLP = new HandleLinePlan();
                oHLP.WQInd = oWQ.WORK_QUEUE;
                oHLP.QCID = Convert.ToUInt32(oWQ.QC_ID);
                oHLP.Voyage = oWQ.SHIP_NO;
                oHLP.VesID = oV.ID;
                oHLP.QCWorkPointInd = oQWP.IndStr;
                // 先不管箱门方向
                oHLP.eSTSVisitDir = StatusEnums.STSVisitDir.Clockwise;

                this.oSimDataStore.dHandleLinePlans.Add(oHLP.WQInd, oHLP);
            }

            return true;
        }

        /// <summary>
        /// 初始化岸桥的位置计划
        /// </summary>
        /// <returns></returns>
        private bool InitQCPosPlans()
        {
            QCPosPlan oQCPP;

            foreach (QCDT oQC in this.oSimDataStore.dQCs.Values)
            {
                oQCPP = new QCPosPlan();
                oQCPP.QCID = oQC.ID;
                oQCPP.CurrPos = oQC.BasePoint.X;
                oQCPP.AimPos = oQC.BasePoint.X;
                oQCPP.lQCTPs = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_TP && u.CheNo == oQC.ID).ToList();
                oQCPP.lQCTPs.OrderBy(u => Convert.ToInt32(u.AreaLaneID));
                this.oSimDataStore.dQCPosPlans.Add(oQC.ID, oQCPP);
            }

            return true;
        }


        /// <summary>
        /// 刷新作业线计划和岸桥位置计划
        /// </summary>
        /// <returns></returns>
        public bool RefreshPlans()
        {
            if (!this.IsInited)
                return false;

            // 更新 QC 的 PosPlan
            this.RenewQCPosPlans();

            // 调整 QCTP 的位置和状态。位置总是根据 STSPosPlan 进行调整，到不了的岸桥的所有 QCTP 缩短，并将状态改为 DISABLED
            this.RenewQCTPLocs();

            // 更新 QCPBIN 和 QCPBOUT 的分布
            this.RenewQCPBDirs();

            // 调整对应的 STSPBExpectTime，供 VmsAlgoAdapter 使用
            this.UpdateExpectTimeofQCLanes();

            return true;
        }

        /// <summary>
        /// 制定新的 STSPosPlan，包括 AimPos 和 Reachable，并更新到岸桥
        /// </summary>
        private void RenewQCPosPlans()
        {
            int unPairCurr;
            int PairThis = 0;
            int iStart;
            int iEnd;
            double PosMax = 0;
            double PosMin = 0;
            QCPosPlan oQCPP;
            string QCWorkPointInd;
            List<QCPosPlan> lTempQCPPs;
            List<QCDT> lQCs = new List<QCDT>();

            // 刷新当前状态
            foreach (QCDT qc in this.oSimDataStore.dQCs.Values)
            {
                oQCPP = this.oSimDataStore.dQCPosPlans[qc.ID];

                if (oQCPP.CurrWQ != qc.CurrWQ)  // 找到，且与目前 lQCPosPlans 中该 QC 的 CurrWQ 不同
                {
                    oQCPP.CurrWQ = qc.CurrWQ;
                    QCWorkPointInd = this.oSimDataStore.dHandleLinePlans[oQCPP.CurrWQ].QCWorkPointInd;
                    oQCPP.WQPos = this.oSimDataStore.dQCWorkPoints[QCWorkPointInd].BasePoint.X;
                    oQCPP.Reachable = false;
                }
            }

            // 多岸桥兼顾算的。首先确定有多少 QC 要移动，且能不能到达WQ位置
            lTempQCPPs = this.oSimDataStore.dQCPosPlans.Values.OrderBy(u => u.CurrPos).ToList();
            unPairCurr = lTempQCPPs.Count(u => u.CurrWQ != null && this.oSimDataStore.dHandleLinePlans.ContainsKey(u.CurrWQ) && !u.Reachable);

            // 然后尝试给尽可能多的 QC 分配移动目标，考虑位置约束
            while (unPairCurr > 0)
            {
                PairThis = 0;
                iStart = -1;
                iEnd = -1;

                for (int i = 0; i < lTempQCPPs.Count; i++)
                {
                    if (iStart < 0 && !lTempQCPPs[i].Reachable)
                        iStart = i;
                    if (iEnd < 0 && (lTempQCPPs[i].Reachable || i == lTempQCPPs.Count - 1))
                    {
                        if (lTempQCPPs[i].Reachable)
                            iEnd = i - 1;
                        else
                            iEnd = i;
                    }
                    if (iStart >= 0 && iEnd >= iStart)
                    {
                        // 首先所有STS尽可能移到X较小的目标位置
                        for (int j = iStart; j <= iEnd; j++)
                        {
                            if (j == 0) 
                                lTempQCPPs[j].AimPos = this.oSimDataStore.dQCs[lTempQCPPs[j].QCID].oType.BaseGauge / 2;
                            else 
                                lTempQCPPs[j].AimPos = lTempQCPPs[j - 1].AimPos + MinSTSPosIntv / 2;
                        }

                        // 然后所有STS尝试尽可能向X较大的位置移动；若有WQ且能到达WQ位置，则停在那里
                        for (int j = iEnd; j >= iStart; j--)
                        {
                            if (j == lTempQCPPs.Count - 1) 
                                PosMax = this.oSimDataStore.oTerminalRegion.Width - this.oSimDataStore.dQCs[lTempQCPPs[j].QCID].oType.BaseGauge / 2;
                            else 
                                PosMax = lTempQCPPs[j + 1].AimPos - MinSTSPosIntv;

                            if (lTempQCPPs[j].CurrWQ.Length > 0)
                            {
                                if (PosMax >= this.oSimDataStore.dQCWorkPoints[this.oSimDataStore.dHandleLinePlans[lTempQCPPs[j].CurrWQ].QCWorkPointInd].BasePoint.X)
                                {
                                    lTempQCPPs[j].AimPos = this.oSimDataStore.dQCWorkPoints[this.oSimDataStore.dHandleLinePlans[lTempQCPPs[j].CurrWQ].QCWorkPointInd].BasePoint.X;
                                    lTempQCPPs[j].Reachable = true;
                                    PairThis++;
                                }
                                else
                                {
                                    lTempQCPPs[j].AimPos = PosMax;
                                    lTempQCPPs[j].Reachable = false;
                                }
                            }
                            else
                            {
                                lTempQCPPs[j].AimPos = PosMax;
                                lTempQCPPs[j].Reachable = false;
                            }
                        }

                        // 对于没有 WQ 的 STS，向 X 较小的方向移动以尽可能回到原位
                        for (int j = iStart; j <= iEnd; j++)
                        {
                            if (lTempQCPPs[j].CurrWQ.Length == 0)
                            {
                                if (j == 0) 
                                    PosMin = this.oSimDataStore.dQCs[lTempQCPPs[j].QCID].oType.BaseGauge / 2;
                                else 
                                    PosMin = lTempQCPPs[j - 1].AimPos + MinSTSPosIntv;

                                if (PosMin < lTempQCPPs[j].CurrPos)
                                    lTempQCPPs[j].AimPos = lTempQCPPs[j].CurrPos;
                                else
                                    lTempQCPPs[j].AimPos = PosMin;
                            }
                        }
                        iStart = -1;
                        iEnd = -1;
                    }
                }

                if (PairThis == 0)
                    break;
                else
                    unPairCurr = unPairCurr - PairThis;
            }

            // 决定QC是否需要移动
            foreach (QCPosPlan stspp in lTempQCPPs)
            {
                this.oSimDataStore.dQCs[stspp.QCID].AimPos = stspp.AimPos;
                this.oSimDataStore.dQCs[stspp.QCID].Reachable = stspp.Reachable;

                if (this.oSimDataStore.dQCs[stspp.QCID].eMotionStatus != StatusEnums.MotionStatus.Working
                    && this.oSimDataStore.dQCs[stspp.QCID].BasePoint.X != stspp.AimPos)
                {
                    this.oSimDataStore.dQCs[stspp.QCID].eMotionStatus = StatusEnums.MotionStatus.Moving;
                    lQCs.Add(this.oSimDataStore.dQCs[stspp.QCID]);
                }                    
            }

            if (lQCs.Count > 0)
            {
                // 如果有 QC 开始移动，需要向 ViewFrame 投射
                this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                    {
                        eProjectType = StatusEnums.ProjectType.Renew,
                        oPPTViewFrame = new ProjectPackageToViewFrame()
                        {
                            lQCs = lQCs
                        }
                    });
            }
        }

        
        /// <summary>
        /// 调整 QCTP 的位置和状态（主要是DISABLED状态）
        /// </summary>
        /// <param name="oPPTViewFrame"></param>
        private void RenewQCTPLocs()
        {
            double NewPos;
            double NewStartPos;
            double NewEndPos;
            uint LineID;
            uint NewTPStartID;
            uint NewTPEndID;
            List<Lane> lQCTPs;
            List<SimTransponder> lTPs = new List<SimTransponder>();

            lQCTPs = this.oSimDataStore.dLanes.Values.Where(u => u.eAttr == LaneAttribute.STS_TP_PASS || u.eAttr == LaneAttribute.STS_TP_WORK).ToList();

            foreach (Lane oL in lQCTPs)
            {
                QCPosPlan oSTSPP = this.oSimDataStore.dQCPosPlans[oL.CheNo];
                NewPos = oSTSPP.AimPos;
                this.oSimDataStore.dTransponders[oL.TPIDStart].LaneID = 0;
                if (!lTPs.Contains(this.oSimDataStore.dTransponders[oL.TPIDStart])) 
                    lTPs.Add(this.oSimDataStore.dTransponders[oL.TPIDStart]);
                this.oSimDataStore.dTransponders[oL.TPIDEnd].LaneID = 0;
                if (!lTPs.Contains(this.oSimDataStore.dTransponders[oL.TPIDEnd])) 
                    lTPs.Add(this.oSimDataStore.dTransponders[oL.TPIDEnd]);
                NewTPStartID = 0;
                NewTPEndID = 0;

                // 更新 QCTP 的长度与状态
                if (this.oSimDataStore.dHandleLinePlans.ContainsKey(oSTSPP.CurrWQ) && !oSTSPP.Reachable)
                {
                    oL.eStatus = LaneStatus.DISABLED;
                    NewStartPos = NewPos - oL.InitLen / 2;
                    NewEndPos = NewPos + oL.InitLen / 2;
                }
                else
                {
                    oL.eStatus = LaneStatus.IDLE;
                    NewStartPos = NewPos - this.oSimDataStore.dQCs[oSTSPP.QCID].oType.BaseGauge / 2;
                    NewEndPos = NewPos + this.oSimDataStore.dQCs[oSTSPP.QCID].oType.BaseGauge / 2;
                }

                // 更新 QCTP 的首尾磁钉，以此更新 QCTP 的位置
                LineID = Convert.ToUInt32(oL.LineID);
                foreach (SimTransponder oSTP in this.oSimDataStore.dTransponders.Values)
                {
                    if (oSTP.HorizontalLineID == LineID)
                    {
                        if (NewTPStartID == 0 || Math.Abs(oSTP.LogicPosX - NewStartPos) < Math.Abs(this.oSimDataStore.dTransponders[Convert.ToUInt16(NewTPStartID)].LogicPosX - NewStartPos))
                            NewTPStartID = oSTP.ID;
                        if (NewTPEndID == 0 || Math.Abs(oSTP.LogicPosX - NewEndPos) < Math.Abs(this.oSimDataStore.dTransponders[Convert.ToUInt16(NewTPEndID)].LogicPosX - NewEndPos))
                            NewTPEndID = oSTP.ID;
                    }
                }

                oL.TPIDStart = NewTPStartID;
                oL.TPIDEnd = NewTPEndID;
                this.oSimDataStore.dTransponders[NewTPStartID].LaneID = oL.ID;
                this.oSimDataStore.dTransponders[NewTPEndID].LaneID = oL.ID;
                if (!lTPs.Contains(this.oSimDataStore.dTransponders[NewTPStartID])) 
                    lTPs.Add(this.oSimDataStore.dTransponders[NewTPStartID]);
                if (!lTPs.Contains(this.oSimDataStore.dTransponders[NewTPEndID])) 
                    lTPs.Add(this.oSimDataStore.dTransponders[NewTPEndID]);

                if (Math.Abs(this.oSimDataStore.dTransponders[NewTPStartID].LogicPosX - this.oSimDataStore.dTransponders[NewTPEndID].LogicPosX) < 1)
                {
                    // 纵向
                    oL.pMid.X = this.oSimDataStore.dTransponders[NewTPStartID].LogicPosX;
                    oL.pMid.Y = (this.oSimDataStore.dTransponders[NewTPStartID].LogicPosY + this.oSimDataStore.dTransponders[NewTPEndID].LogicPosY) / 2;
                    oL.pWork = oL.pMid;
                    oL.RotateAngle = 90;
                }
                else
                {
                    // 横向
                    oL.pMid.X = (this.oSimDataStore.dTransponders[NewTPStartID].LogicPosX + this.oSimDataStore.dTransponders[NewTPEndID].LogicPosX) / 2;
                    oL.pMid.Y = this.oSimDataStore.dTransponders[NewTPStartID].LogicPosY;
                    oL.pWork = oL.pMid;
                    oL.RotateAngle = 0;
                }
            }

            // 投射
            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
            {
                eProjectType = StatusEnums.ProjectType.Renew,
                oPPTViewFrame = new ProjectPackageToViewFrame()
                {
                    lLanes = lQCTPs,
                    lTPs = lTPs
                }
            });
        }


        /// <summary>
        /// 调整 QCPB 的进出方向
        /// </summary>
        /// <param name="oPPTViewFrame"></param>
        private void RenewQCPBDirs()
        {
            List<Lane> lTempQCPBs;
            
            lTempQCPBs = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_PB).OrderBy(u => u.pMid.X).ToList();

            // 先初始化 lSTSPBPlans 并区分出被岸桥大车挡住的PB，NONE 相对于 STS_PB_IN_OUT
            foreach (Lane oL in lTempQCPBs)
            {
                oL.CheNo = 0;
                oL.eAttr = LaneAttribute.STS_PB_IN_OUT;
                foreach (QCPosPlan opp in this.oSimDataStore.dQCPosPlans.Values)
                {
                    if ((opp.AimPos - this.NoPBSideLength < oL.pMid.X) && (opp.AimPos + this.NoPBSideLength > oL.pMid.X))
                        oL.CheNo = opp.QCID;
                }
            }

            // 然后分三轮，先给一个进，再给一个出，按照时针方向取最近。
            for (int i = 0; i < this.NumPBIO; i++)
            {
                foreach (QCPosPlan opp in this.oSimDataStore.dQCPosPlans.Values)
                {
                    // 依次一进一出
                    if (opp.Reachable && this.oSimDataStore.dHandleLinePlans.ContainsKey(opp.CurrWQ))
                    {
                        this.FindOneDirPBNearQCAimPos(opp, ref lTempQCPBs, "in");
                        this.FindOneDirPBNearQCAimPos(opp, ref lTempQCPBs, "out");
                    }
                }
            }

            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTViewFrame = new ProjectPackageToViewFrame()
                    {
                        lLanes = lTempQCPBs
                    }
                });
        }

        // 在 QC 附近找一个进出PB
        private void FindOneDirPBNearQCAimPos(QCPosPlan stspp, ref List<Lane> lTempQCPBs, string PBDir)
        {
            bool found = false;

            // 按方向找
            if (this.oSimDataStore.dHandleLinePlans[stspp.CurrWQ].eSTSVisitDir == StatusEnums.STSVisitDir.Clockwise)
                if (PBDir == "in")
                    found = this.FindOneDirPBAtOneSideOfQC(ref lTempQCPBs, stspp.AimPos, "+", stspp.QCID, PBDir);
                else
                    found = this.FindOneDirPBAtOneSideOfQC(ref lTempQCPBs, stspp.AimPos, "-", stspp.QCID, PBDir);
            else
                if (PBDir == "in")
                    found = this.FindOneDirPBAtOneSideOfQC(ref lTempQCPBs, stspp.AimPos, "-", stspp.QCID, PBDir);
                else
                    found = this.FindOneDirPBAtOneSideOfQC(ref lTempQCPBs, stspp.AimPos, "+", stspp.QCID, PBDir);

            if (!found)
                if (this.oSimDataStore.dHandleLinePlans[stspp.CurrWQ].eSTSVisitDir == StatusEnums.STSVisitDir.Clockwise)
                    if (PBDir == "in")
                        found = this.FindOneDirPBAtOneSideOfQC(ref lTempQCPBs, stspp.AimPos, "-", stspp.QCID, PBDir);
                    else
                        found = this.FindOneDirPBAtOneSideOfQC(ref lTempQCPBs, stspp.AimPos, "+", stspp.QCID, PBDir);
                else
                    if (PBDir == "in")
                        found = this.FindOneDirPBAtOneSideOfQC(ref lTempQCPBs, stspp.AimPos, "+", stspp.QCID, PBDir);
                    else
                        found = this.FindOneDirPBAtOneSideOfQC(ref lTempQCPBs, stspp.AimPos, "-", stspp.QCID, PBDir);
        }

        // 在 QC 的指定一侧找进出 PB
        private bool FindOneDirPBAtOneSideOfQC(ref List<Lane> lTempLanes, double ori, string SearchDir, uint QCID, string PBDir)
        {
            bool found = false;
            List<Lane> lLanes;

            lLanes = new List<Lane>();

            if (SearchDir == "-")
                lLanes = lTempLanes.Where(u => u.pMid.X < ori - this.NoPBSideLength && u.eAttr == LaneAttribute.STS_PB_IN_OUT && u.CheNo == 0).OrderByDescending(u => u.pMid.X).ToList();
            else if (SearchDir == "+")
                lLanes = lTempLanes.Where(u => u.pMid.X > ori + this.NoPBSideLength && u.eAttr == LaneAttribute.STS_PB_IN_OUT && u.CheNo == 0).OrderBy(u => u.pMid.X).ToList();

            if (lLanes.Count > 0)
            {
                found = true;
                lLanes[0].CheNo = QCID;
                if (PBDir == "in")
                    lTempLanes.First(u => u.ID == lLanes[0].ID).eAttr = LaneAttribute.STS_PB_ONLY_IN;
                else 
                    lTempLanes.First(u => u.ID == lLanes[0].ID).eAttr = LaneAttribute.STS_PB_ONLY_OUT;
            }

            return found;
        }

        // 更新与所有 QCTP 有关的 EXPECTTIME
        private void UpdateExpectTimeofQCLanes()
        {
            // 直接操作数据源
            this.oSimDataStore.lQCExpectTimes.Clear();
            List<Lane> lQCTPs;
            List<QCPosPlan> lQCPPs;
            QCExpectTimeInd = 0;

            // QC 与 QCPB 之间的 ExpectTime
            lQCTPs = this.oSimDataStore.dLanes.Values.Where(u => u.eAttr == LaneAttribute.STS_TP_PASS || u.eAttr == LaneAttribute.STS_TP_WORK).ToList();

            foreach (QCPosPlan stspp in this.oSimDataStore.dQCPosPlans.Values)
                this.UpdateExpectTimeBetweenQCAndQCPBs(stspp);

            // QCTP 之间的 ExpectTime
            lQCPPs = new List<QCPosPlan>(this.oSimDataStore.dQCPosPlans.Values);

            for (int i = 0; i < lQCPPs.Count - 1; i++)
            {
                for (int j = i + 1; j < lQCPPs.Count; j++)
                {
                    if (this.JudgeArrivabilityBetweenQCsFromDir(lQCPPs, i, j)) 
                        this.UpdateExpectTimeFromQCTPstoQCTPs(lQCPPs, i, j, lQCTPs);
                    else if (this.JudgeArrivabilityBetweenQCsFromDir(lQCPPs, j, i)) 
                        this.UpdateExpectTimeFromQCTPstoQCTPs(lQCPPs, j, i, lQCTPs);
                }
            }
        }

        // 从方向上判断能否从岸桥 iStart 到 岸桥 iEnd
        private bool JudgeArrivabilityBetweenQCsFromDir(List<QCPosPlan> lstspp, int iStart, int iEnd)
        {
            bool arrivable = true;
            StatusEnums.STSVisitDir eStandardDir;

            if (lstspp[iStart].AimPos > lstspp[iEnd].AimPos)
                eStandardDir = StatusEnums.STSVisitDir.AntiClockwise;
            else
                eStandardDir = StatusEnums.STSVisitDir.Clockwise;

            for (int i = iStart; i <= iEnd; i++)
            {
                if (this.oSimDataStore.dHandleLinePlans.ContainsKey(lstspp[i].CurrWQ) && this.oSimDataStore.dHandleLinePlans[lstspp[i].CurrWQ].eSTSVisitDir != eStandardDir)
                {
                    arrivable = false;
                    break;
                }
            }

            return arrivable;
        }

        // 更新某个 QC 的所有 QCTP 和所有 QCPB 之间的 EXPECTTIME
        private void UpdateExpectTimeBetweenQCAndQCPBs(QCPosPlan stspp)
        {
            double rangeMin = 0;
            double rangeMax = 0;
            int time = 0;
            SimExpectTime oSET;
            List<Lane> lQCPBLanes;

            rangeMin = stspp.AimPos - NoPBSideLength;
            if (rangeMin < 0) rangeMin = 0;

            rangeMax = stspp.AimPos + NoPBSideLength;
            if (rangeMax > this.oSimDataStore.oTerminalRegion.Width)
                rangeMax = this.oSimDataStore.oTerminalRegion.Width;

            lQCPBLanes = new List<Lane>(this.oSimDataStore.dLanes.Values).Where(u => u.eType == AreaType.STS_PB).ToList();

            // QC 到 QCPB 的 ExpectTime。注意并不区分QCTP。
            foreach (Lane ol in lQCPBLanes)
            {
                if ((ol.pMid.X <= rangeMin || ol.pMid.X >= rangeMax) && (ol.CheNo == stspp.QCID) && (ol.eAttr == LaneAttribute.STS_PB_ONLY_IN || ol.eAttr == LaneAttribute.STS_PB_ONLY_OUT))
                {
                    time = CalcExpectTimeBetweenQCAndQCPB(stspp, ol);

                    oSET = new SimExpectTime();
                    QCExpectTimeInd++;
                    oSET.ID = QCExpectTimeInd;
                    if (ol.eAttr == LaneAttribute.STS_PB_ONLY_IN)
                    {
                        oSET.fromID = ol.ID;
                        oSET.toID = stspp.QCID;
                        oSET.l2LType = LaneToLaneType.QCPB_2_QC;
                        oSET.expectTime = time;
                    }
                    if (ol.eAttr == LaneAttribute.STS_PB_ONLY_OUT)
                    {
                        oSET.fromID = stspp.QCID;
                        oSET.toID = ol.ID;
                        oSET.l2LType = LaneToLaneType.QC_2_QCPB;
                        oSET.expectTime = time;
                    }
                    if (oSET.CheckDefinitionCompleteness()) 
                        this.oSimDataStore.lQCExpectTimes.Add(oSET);
                }
            }
        }

        // 计算某台 QC 与某个 QCPB 之间的 ExpectTime （所有同 QC 的 QCTP 的 ExpectTime 一样）
        private int CalcExpectTimeBetweenQCAndQCPB(QCPosPlan stspp, Lane ol)
        {
            double StraightDistance = 0;
            double time = 0;

            // 暂不区分 Lane 的 DISABLE 状态

            // 直线时间
            StraightDistance = Math.Abs(stspp.AimPos - ol.pMid.X - this.NoPBSideLength);
            time = CalcTime4Straight(StraightDistance, 1, 0, Acceleration);

            // 转弯时间
            time = time + CalcTime4Turn();

            return (int)time;
        }

        // 更新所有从岸桥 iStart 到岸桥 iEnd 的 QCTP 之间的 ExpectTime
        private void UpdateExpectTimeFromQCTPstoQCTPs(List<QCPosPlan> lstspp, int iStart, int iEnd, List<Lane> lQCTPs)
        {
            SimExpectTime oSET;
            int ActExpectTime;
            int LaneIntv;

            foreach (Lane ol1 in lstspp[iStart].lQCTPs)
            {
                foreach (Lane ol2 in lstspp[iEnd].lQCTPs)
                {
                    oSET = new SimExpectTime();
                    oSET.expectTime = Convert.ToInt32(4 * CalcTime4Turn() + 2 * CalcTime4Straight(this.oSimDataStore.oTerminalRegion.Width, 0, 0, Acceleration));
                    ActExpectTime = 0;
                    LaneIntv = (int)Math.Round(Math.Abs(ol1.pMid.Y - ol2.pMid.Y) / AbreastQCTPsIntv);

                    switch (LaneIntv)
                    {
                        case 0:
                            oSET.expectTime = Convert.ToInt32(CalcTime4Straight(Math.Abs(ol1.pMid.X - ol2.pMid.X), 0, 0, Acceleration));
                            break;
                        case 1:
                            ActExpectTime = Convert.ToInt32(CalcTimeObliqueOnce(ol1, ol2));
                            if (ActExpectTime > InvalidVMSIDX) oSET.expectTime = ActExpectTime;
                            break;
                        case 2:
                            ActExpectTime = Convert.ToInt32(CalcTimeObliqueTwice(lstspp[iStart].lQCTPs, ol1, ol2));
                            if (ActExpectTime > InvalidVMSIDX) oSET.expectTime = ActExpectTime;
                            break;
                        case 3:
                            ActExpectTime = Convert.ToInt32(CalcTimeObliqueThreeTimes(lstspp[iStart].lQCTPs, ol1, ol2));
                            if (ActExpectTime > InvalidVMSIDX) oSET.expectTime = ActExpectTime;
                            break;
                    }

                    oSET.l2LType = LaneToLaneType.QCTP_2_QCTP;
                    oSET.fromID = ol1.ID;
                    oSET.toID = ol2.ID;
                    QCExpectTimeInd++;
                    oSET.ID = QCExpectTimeInd;
                    if (oSET.CheckDefinitionCompleteness()) 
                        this.oSimDataStore.lQCExpectTimes.Add(oSET);
                }
            }
        }

        // 一次斜行时间
        private double CalcTimeObliqueOnce(Lane ol1, Lane ol2)
        {
            double ET = InvalidVMSIDX;
            double dist = Math.Round(ol1.pMid.X - ol2.pMid.X);

            if (ol1.eAttr == LaneAttribute.STS_TP_WORK || ol2.eAttr == LaneAttribute.STS_TP_WORK) 
            {
                if (dist > SafeDistForObliqueOnce)
                    ET = CalcTime4Straight(DistBeforeOrAfterOblique, 0, AGVObliqueSpeed, Acceleration) 
                        + CalcTime4Oblique()
                        + CalcTime4Straight(dist - DistBeforeOrAfterOblique - 2 * (ObliqueMove + AdjustDistanceOblique),AGVObliqueSpeed,0,Acceleration);
            }
            else
            {
                if (dist > DistBeforeOrAfterOblique)
                    ET = CalcTime4Straight((dist - DistBeforeOrAfterOblique), AGVObliqueSpeed, 0, Acceleration);
            }

            return ET;
        }

        // 两次斜行时间
        private double CalcTimeObliqueTwice(List<Lane> lQCTPs, Lane ol1, Lane ol2)
        {
            double ET = InvalidVMSIDX;
            double dist = Math.Round(ol1.pMid.X - ol2.pMid.X);

            LaneAttribute LA = LaneAttribute.NONE;
            for (int i = 0; i < lQCTPs.Count; i++)
            {
                if ((lQCTPs[i].pMid.Y < ol1.pMid.Y && lQCTPs[i].pMid.Y > ol2.pMid.Y) || (lQCTPs[i].pMid.Y > ol1.pMid.Y && lQCTPs[i].pMid.Y < ol2.pMid.Y))
                    LA = lQCTPs[i].eAttr;
            }

            if (LA == LaneAttribute.STS_TP_WORK)
            {
                if (dist > SafeDistForObliqueTwice)
                    ET = CalcTime4Straight(DistBeforeOrAfterOblique, 0, AGVObliqueSpeed, Acceleration)
                        + 2 * CalcTime4Oblique()
                        + CalcTime4Straight(dist - DistBeforeOrAfterOblique - 2 * (ObliqueMove + AdjustDistanceOblique), AGVObliqueSpeed, 0, Acceleration);
            }
            else if (LA == LaneAttribute.STS_TP_PASS)
            {
                if (dist > 2 * DistBeforeOrAfterOblique)
                    ET = CalcTime4Straight(dist - 2 * DistBeforeOrAfterOblique, AGVObliqueSpeed, 0, Acceleration)
                        + 2 * CalcTime4Oblique();
            }

            return ET;
        }

        // 三次斜行时间
        private double CalcTimeObliqueThreeTimes(List<Lane> lQCTPs, Lane ol1, Lane ol2)
        {
            double ET = InvalidVMSIDX;
            double dist = Math.Round(ol1.pMid.X - ol2.pMid.X);

            LaneAttribute[] aLA = new LaneAttribute[2];
            int j = 0;
            for (int i = 0; i < lQCTPs.Count; i++)
            {
                if ((lQCTPs[i].pMid.Y < ol1.pMid.Y && lQCTPs[i].pMid.Y > ol2.pMid.Y) || (lQCTPs[i].pMid.Y > ol1.pMid.Y && lQCTPs[i].pMid.Y < ol2.pMid.Y))
                {
                    aLA[j] = lQCTPs[i].eAttr;
                    j++;
                    if (j >= 2) break;
                }
            }

            if ((aLA[0] == LaneAttribute.STS_TP_PASS && aLA[1] == LaneAttribute.STS_TP_WORK) || (aLA[1] == LaneAttribute.STS_TP_PASS && aLA[0] == LaneAttribute.STS_TP_WORK))
            {
                if (dist >= SafeDistForObliqueTwice)
                    ET = CalcTime4Straight(DistBeforeOrAfterOblique, 0, AGVObliqueSpeed, Acceleration)
                        + 3 * CalcTime4Oblique()
                        + CalcTime4Straight(dist - DistBeforeOrAfterOblique - 3 * (ObliqueMove + AdjustDistanceOblique), AGVObliqueSpeed, 0, Acceleration);
            }
            else if (aLA[0] == LaneAttribute.STS_TP_PASS && aLA[1] == LaneAttribute.STS_TP_PASS)
            {
                if (dist > 3 * DistBeforeOrAfterOblique)
                    ET = CalcTime4Straight(dist - 3 * DistBeforeOrAfterOblique, AGVObliqueSpeed, 0, Acceleration) + 3 * CalcTime4Oblique();
            }
            return ET;
        }

        // 计算首尾速度确定时定长距离的运行时间
        private double CalcTime4Straight(double distance, double vStart, double vEnd, double acce)
        {
            double vtemp = 0;
            double time = 0;
            double vSumSquare = vStart * vStart + vEnd * vEnd;

            if (distance < (2 * AGVMaxSpeed * AGVMaxSpeed - vSumSquare) / (2 * Acceleration))
            {
                vtemp = Math.Sqrt(distance * Acceleration + vSumSquare / 2);
                time = ((vtemp - vStart) + (vtemp - vEnd)) / Acceleration;
            }
            else
                time = (2 * AGVMaxSpeed - vStart - vEnd) / Acceleration
                    + (distance - (2 * AGVMaxSpeed * AGVMaxSpeed - vSumSquare) / (2 * Acceleration) / AGVMaxSpeed);
            if (time < 0) 
                time = 0;

            return time;
        }

        // 计算斜行时间
        private double CalcTime4Oblique()
        {
            return 12.65;
        }

        // 计算转弯时间
        private double CalcTime4Turn()
        {
            return 25.1;
        }

    }
}
