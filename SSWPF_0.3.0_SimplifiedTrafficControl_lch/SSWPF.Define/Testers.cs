using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NExcel;
using System.Data;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Shapes;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.YardMap;
using SSWPF.Define;
using SharpSim;
using solutionfordata;

namespace SSWPF.Define
{
    public class RandomStepTester
    {
        public event EventHandler<ProjectToViewFrameEventArgs> ProjectToViewFrameEvent;

        SimDataStore oSimDataStore;

        Random rand;

        public RandomStepTester()
        {
        }

        public RandomStepTester(SimDataStore oSTC)
            : this()
        {
            this.oSimDataStore = oSTC;
        }

        public bool Init()
        {
            return true;
        }

        public bool SingleQCRandomScheduleTest()
        {
            bool bRet = true;
            QCDT oQC;
            ProjectPackageToViewFrame oPPTViewFrame;

            oPPTViewFrame = new ProjectPackageToViewFrame();
            oQC = this.oSimDataStore.dQCs[118];
            if (oQC.BasePoint.X > 20)
                oQC.BasePoint.X = oQC.BasePoint.X - 10;
            else
                oQC.BasePoint.X = 190;
            oPPTViewFrame.lQCs.Add(oQC);

            if (this.ProjectToViewFrameEvent != null)
                this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTViewFrame = oPPTViewFrame
                });

            return bRet;
        }

        public bool RandomScheduleTest()
        {
            rand = new Random();
            ProjectPackageToViewFrame oPPTViewFrame;
            oPPTViewFrame = new ProjectPackageToViewFrame();

            // QC 部分
            foreach (uint i in this.oSimDataStore.dQCs.Keys)
            {
                QCDT oQC = this.oSimDataStore.dQCs[i];
                // 位置
                oQC.BasePoint.X = rand.NextDouble() * 400 + 20;
                // 小车和平台颜色
                oQC.MainTrolley.oTwinStoreUnit.eUnitStoreType = (StatusEnums.StoreType)Enum.ToObject(typeof(StatusEnums.StoreType), rand.Next(0, 5));
                oQC.ViceTrolley.oTwinStoreUnit.eUnitStoreType = (StatusEnums.StoreType)Enum.ToObject(typeof(StatusEnums.StoreType), rand.Next(0, 5));
                foreach (QCPlatformSlot oSlot in oQC.Platform)
                {
                    oSlot.oTwinStoreUnit.eUnitStoreType = (StatusEnums.StoreType)Enum.ToObject(typeof(StatusEnums.StoreType), rand.Next(0, 5));
                }
                // 大车颜色
                oQC.eStepTravelStatus = (StatusEnums.StepTravelStatus)Enum.ToObject(typeof(StatusEnums.StepTravelStatus), rand.Next(0, 5));
                oQC.eMoveKind = (Move_Kind)Enum.ToObject(typeof(Move_Kind), rand.Next(3, 5));
                oPPTViewFrame.lQCs.Add(oQC);
            }

            // ASC 部分
            foreach (uint i in this.oSimDataStore.dASCs.Keys)
            {
                ASC oASC = this.oSimDataStore.dASCs[i];
                // 位置
                oASC.BasePoint.X = rand.NextDouble() * 260 + 50;
                // 颜色
                oASC.oTrolley.oSingleStoreUnit.eUnitStoreType = (StatusEnums.StoreType)Enum.ToObject(typeof(StatusEnums.StoreType), rand.Next(0, 5));
                oASC.eTravelStatus = (StatusEnums.StepTravelStatus)Enum.ToObject(typeof(StatusEnums.StepTravelStatus), rand.Next(0, 3));
                oPPTViewFrame.lASCs.Add(oASC);
            }

            // Lane 部分
            foreach (uint i in this.oSimDataStore.dLanes.Keys)
            {
                Lane oL = this.oSimDataStore.dLanes[i];
                oL.eStatus = (LaneStatus)rand.Next(0, 6);
                if (oL.oDirSign != null)
                    oL.eAttr = (LaneAttribute)rand.Next(0, 4);
                oPPTViewFrame.lLanes.Add(oL);
            }

            // Mate 部分
            foreach (uint i in this.oSimDataStore.dMates.Keys)
            {
                Mate oM = this.oSimDataStore.dMates[i];
                oM.oStorageUnit.eUnitStoreType = (StatusEnums.StoreType)rand.Next(0, 5);
                oPPTViewFrame.lMates.Add(oM);
            }

            // AGV 部分
            foreach (uint i in this.oSimDataStore.dAGVs.Keys)
            {
                AGV oAGV = this.oSimDataStore.dAGVs[i];
                oAGV.MidPoint.X = rand.NextDouble() * 400 + 20;
                oAGV.MidPoint.Y = rand.NextDouble() * 300 + 20;
                oAGV.RotateAngle = rand.NextDouble() * 360;
                oAGV.oTwinStoreUnit.eUnitStoreType = (StatusEnums.StoreType)rand.Next(0, 5);
                oPPTViewFrame.lAGVs.Add(oAGV);
            }

            // Pile 部分
            foreach (string str in this.oSimDataStore.dPiles.Keys)
            {
                Pile oP = this.oSimDataStore.dPiles[str];
                oP.StackedNum = rand.Next(0, 6);
                oPPTViewFrame.lPiles.Add(oP);
            }

            // 投射
            if (this.ProjectToViewFrameEvent != null)
                this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTViewFrame = oPPTViewFrame
                });

            return true;
        }
    }

    public class AGVTrafficTester
    {
        public event EventHandler<GenerateAGVRouteEventArgs> GenerateAGVRouteEvent;
        public event EventHandler<ProjectToViewFrameEventArgs> ProjectToViewFrameEvent;
        public event EventHandler<ProjectToInfoFrameEventArgs> ProjectToInfoFrameEvent;
        public event EventHandler<DeleteAGVOccupyLineSegArgs> DeleteAOLSEvent;
        public event EventHandler<ResetAGVRoutesEventArgs> ResetAGVRoutesEvent;

        private SimDataStore oSimDataStore;

        int row = 0;
        int TrafficTestNum;
        private int iLaneOrd1, iLaneOrd2;
        private List<uint> lLaneIDs;
        private AGV oTestAGV;

        public AGVTrafficTester()
        {
        }

        public AGVTrafficTester(SimDataStore oSimDataStore)
            : this()
        {
            this.oSimDataStore = oSimDataStore;
        }

        /// <summary>
        /// 基本初始化
        /// </summary>
        /// <returns></returns>
        public bool Init()
        {
            if (this.oSimDataStore == null)
            {
                Logger.Simulate.Error("AGVTrafficTester: Null SimDataStore!");
                return false;
            }

            if (this.oSimDataStore.dAGVs.Count == 0)
            {
                Logger.Simulate.Error("AGVRouteTester: No AGV Sample!");
                return false;
            }

            if (this.GenerateAGVRouteEvent == null || this.ProjectToViewFrameEvent == null
                || this.ProjectToInfoFrameEvent == null || this.DeleteAOLSEvent == null || this.ResetAGVRoutesEvent == null)
            {
                Logger.Simulate.Error("AGVTrafficTester: Null Event Listener!");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 路径测试带参初始化。给定默认的起始车道号和终止车道号
        /// </summary>
        /// <param name="iLaneOrd1">默认起始车道号</param>
        /// <param name="iLaneOrd2">默认终止车道号</param>
        /// <returns></returns>
        public bool InitRouteTestWithDefaultOriAndDestLaneIDs(int iLaneOrd1, int iLaneOrd2)
        {
            bool bRet;
            bRet = this.Init();
            this.iLaneOrd1 = iLaneOrd1;
            this.iLaneOrd2 = iLaneOrd2;

            return bRet;
        }

        /// <summary>
        /// 路径规划测试接口
        /// </summary>
        public void AGVRouteStepTest()
        {
            GenerateAGVRouteEventArgs e;
            ProjectPackageToViewFrame oPPTViewFrame;

            // 初始化界面，干掉所有 AGV 路径
            this.ResetTransportForRouteTest();

            // 将 TestAGV 放到 this.uLaneID1, 设置好目标车道
            this.GenerateSingleAGV(this.lLaneIDs[this.iLaneOrd1], this.lLaneIDs[this.iLaneOrd2]);
            e = new GenerateAGVRouteEventArgs()
            {
                oA = this.oTestAGV,
                AimLaneID = this.lLaneIDs[this.iLaneOrd2],
                IsGenerationSucc = false
            };

            this.GenerateAGVRouteEvent.Invoke(this, e);

            // 失败时，终点状态改为 DISABLED
            if (!e.IsGenerationSucc)
            {
                this.oSimDataStore.dLanes[this.lLaneIDs[this.iLaneOrd2]].eStatus = LaneStatus.DISABLED;
                oPPTViewFrame = new ProjectPackageToViewFrame();
                oPPTViewFrame.lLanes.Add(this.oSimDataStore.dLanes[this.lLaneIDs[this.iLaneOrd2]]);
                this.ProjectToViewFrameEvent(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTViewFrame = oPPTViewFrame
                });
            }

            // 迭代
            this.iLaneOrd2++;
            if (this.lLaneIDs.Count == this.iLaneOrd2)
            {
                this.iLaneOrd2 = 0;
                this.iLaneOrd1++;
            }
            if (this.lLaneIDs.Count == this.iLaneOrd1)
                this.iLaneOrd1 = 0;
        }

        /// <summary>
        /// 重置车道函数
        /// </summary>
        private void ResetTransportForRouteTest()
        {
            ProjectPackageToViewFrame oPPTViewFrame;
            List<QCTPRange> lQCTPRanges;
            List<Lane> lTempLanes;
            bool IfAdd;

            // 删掉所有 AGV
            if (this.oSimDataStore.dAGVs.Count > 0)
            {
                oPPTViewFrame = new ProjectPackageToViewFrame();
                oPPTViewFrame.lAGVs = new List<AGV>(this.oSimDataStore.dAGVs.Values);
                this.oSimDataStore.dAGVs.Clear();
                this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Delete,
                    oPPTViewFrame = oPPTViewFrame
                });
            }

            // 还原所有车道的进出状态
            oPPTViewFrame = new ProjectPackageToViewFrame();
            lQCTPRanges = new List<QCTPRange>();
            lTempLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_PB || u.eType == AreaType.STS_TP).ToList();

            foreach (Lane oL in lTempLanes)
            {
                if (!lQCTPRanges.Exists(u => u.QCID == oL.CheNo))
                    lQCTPRanges.Add(new QCTPRange()
                    {
                        QCID = oL.CheNo,
                        StartPos = this.oSimDataStore.dTransponders[oL.TPIDStart].LogicPosX,
                        EndPos = this.oSimDataStore.dTransponders[oL.TPIDEnd].LogicPosX
                    });
            }

            foreach (Lane oL in this.oSimDataStore.dLanes.Values)
            {
                IfAdd = false;
                if (oL.eStatus != LaneStatus.IDLE)
                {
                    IfAdd = true;
                    oL.eStatus = LaneStatus.IDLE;
                }
                if (oL.AGVNo > 0)
                {
                    IfAdd = true;
                    oL.AGVNo = 0;
                }
                if (oL.eType == AreaType.STS_PB)
                {
                    IfAdd = true;
                    oL.eAttr = LaneAttribute.STS_PB_IN_OUT;
                    oL.CheNo = 0;
                    foreach (QCTPRange oQCTPR in lQCTPRanges)
                    {
                        if (oQCTPR.StartPos <= oL.pMid.X && oQCTPR.EndPos >= oL.pMid.X)
                            oL.CheNo = oQCTPR.QCID;
                    }
                }
                if (IfAdd)
                    oPPTViewFrame.lLanes.Add(oL);
            }

            // 还原所有磁钉状态
            foreach (SimTransponder oTP in this.oSimDataStore.dTransponders.Values)
            {
                IfAdd = false;
                if (oTP.dRouteTPDivisions.Count > 0)
                {
                    IfAdd = true;
                    oTP.dRouteTPDivisions.Clear();
                }
                if (IfAdd)
                    oPPTViewFrame.lTPs.Add(oTP);
            }
            if (oPPTViewFrame.lLanes.Count > 0 || oPPTViewFrame.lTPs.Count > 0)
                this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTViewFrame = oPPTViewFrame
                });

            // 清除所有 AGV 路径
            this.ResetAGVRoutesEvent.Invoke(this, new ResetAGVRoutesEventArgs());
        }

        /// <summary>
        /// 生成初始 AGV 的函数。路径规划用
        /// </summary>
        /// <param name="CurrLaneID"></param>
        /// <param name="AimLaneID"></param>
        private void GenerateSingleAGV(uint CurrLaneID, uint AimLaneID)
        {
            ProjectPackageToViewFrame oPPTViewFrame;

            // 注入 AGV
            this.oSimDataStore.dAGVs.Add(this.oTestAGV.ID, this.oTestAGV);

            // 新增 AGV 图像
            this.oTestAGV.CurrLaneID = CurrLaneID;
            this.oTestAGV.AimLaneID = AimLaneID;
            this.oTestAGV.MidPoint.X = this.oSimDataStore.dLanes[CurrLaneID].pWork.X;
            this.oTestAGV.MidPoint.Y = this.oSimDataStore.dLanes[CurrLaneID].pWork.Y;
            switch (this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[CurrLaneID].LineID].eFlowDir)
            {
                case CoordinateDirection.X_NEGATIVE:
                case CoordinateDirection.X_POSITIVE:
                    this.oTestAGV.RotateAngle = 0;
                    break;
                case CoordinateDirection.Y_NEGATIVE:
                case CoordinateDirection.Y_POSITIVE:
                    this.oTestAGV.RotateAngle = 90;
                    break;
            }

            oPPTViewFrame = new ProjectPackageToViewFrame();
            oPPTViewFrame.lAGVs.Add(this.oTestAGV);
            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
            {
                eProjectType = StatusEnums.ProjectType.Create,
                oPPTViewFrame = oPPTViewFrame
            });

            // 改 CurrLane 状态
            this.oSimDataStore.dLanes[CurrLaneID].AGVNo = this.oTestAGV.ID;
            this.oSimDataStore.dLanes[CurrLaneID].eStatus = LaneStatus.OCCUPIED;

            oPPTViewFrame = new ProjectPackageToViewFrame();
            oPPTViewFrame.lLanes.Add(this.oSimDataStore.dLanes[CurrLaneID]);
            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
            {
                eProjectType = StatusEnums.ProjectType.Renew,
                oPPTViewFrame = oPPTViewFrame
            });
        }

        /// <summary>
        /// 交通测试带参初始化。给定 AGV 数量
        /// 注意不要超过案例输入的 AGV 数量
        /// </summary>
        /// <param name="AgvNum"></param>
        /// <returns></returns>
        public bool InitTrafficTestWithAgvNum(int AgvNum)
        {
            bool bRet;
            List<AGV> lTempAGVs;
            List<uint> lTempAGVIDs;
            List<AGV_STATUS> lTempAGVStatus;
            List<Lane> lTempLanes, lSelectLanes;
            List<SimTransponder> lTempTPs, lSelectTPs;
            List<QCTPRange> lQCTPRanges;
            Lane oLane;
            AGV oAGV;
            AGV_STATUS oAgvStatus;

            bRet = this.Init();
            if (!bRet)
                return bRet;

            if (AgvNum > this.oSimDataStore.dAGVs.Count)
                return false;

            if (AgvNum == this.oSimDataStore.dAGVs.Count)
                return true;

            lTempAGVs = new List<AGV>();
            lTempLanes = new List<Lane>();
            lTempAGVStatus = new List<AGV_STATUS>();
            lTempTPs = new List<SimTransponder>();
            lTempAGVIDs = new List<uint>();
            while (this.oSimDataStore.dAGVs.Count > AgvNum)
            {
                oAGV = this.oSimDataStore.dAGVs.Values.Last();
                lTempAGVs.Add(oAGV);
                this.oSimDataStore.dAGVs.Remove(oAGV.ID);
                lTempAGVIDs.Add(oAGV.ID);

                oLane = this.oSimDataStore.dLanes.Values.First(u => u.AGVNo == oAGV.ID);
                oLane.AGVNo = 0;
                oLane.eStatus = LaneStatus.IDLE;
                lTempLanes.Add(oLane);

                lSelectTPs = this.oSimDataStore.dTransponders.Values.Where(u => u.dRouteTPDivisions.ContainsKey(oAGV.ID)).ToList();
                lSelectTPs.ForEach(u => u.dRouteTPDivisions.Remove(oAGV.ID));
                lTempTPs.AddRange(lSelectTPs);

                oAgvStatus = this.oSimDataStore.dAGVStatus[oAGV.ID.ToString()];
                lTempAGVStatus.Add(oAgvStatus);
                this.oSimDataStore.dAGVStatus.Remove(oAgvStatus.CHE_ID);
            }

            // 重置 QCPB 为 InOut
            lSelectLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_PB && u.eAttr != LaneAttribute.STS_PB_IN_OUT).ToList();
            lSelectLanes.ForEach(u => u.eAttr = LaneAttribute.STS_PB_IN_OUT);
            foreach (Lane oL in lSelectLanes)
            {
                if (!lTempLanes.Exists(u => u.ID == oL.ID))
                    lTempLanes.Add(oL);
            }

            // 补上 QCTP 范围内的 QCPB 的 CheNo
            lSelectLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_TP).ToList();
            lQCTPRanges = new List<QCTPRange>();
            foreach (Lane oL in lSelectLanes)
            {
                if (!lQCTPRanges.Exists(u => u.QCID == oL.CheNo))
                    lQCTPRanges.Add(new QCTPRange()
                    {
                        QCID = oL.CheNo,
                        StartPos = this.oSimDataStore.dTransponders[oL.TPIDStart].LogicPosX,
                        EndPos = this.oSimDataStore.dTransponders[oL.TPIDEnd].LogicPosX
                    });
            }

            foreach (QCTPRange oQCTPRange in lQCTPRanges)
            {
                lSelectLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_PB
                    && u.pMid.X >= oQCTPRange.StartPos && u.pMid.X <= oQCTPRange.EndPos).ToList();
                lSelectLanes.ForEach(u => u.CheNo = oQCTPRange.QCID);
                foreach (Lane oL in lSelectLanes)
                {
                    if (!lTempLanes.Exists(u => u.ID == oL.ID))
                        lTempLanes.Add(oL);
                }
            }

            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
            {
                eProjectType = StatusEnums.ProjectType.Renew,
                oPPTViewFrame = new ProjectPackageToViewFrame()
                {
                    lLanes = lTempLanes
                }
            });

            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
            {
                eProjectType = StatusEnums.ProjectType.Delete,
                oPPTViewFrame = new ProjectPackageToViewFrame()
                {
                    lAGVs = lTempAGVs
                }
            });
            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
            {
                eProjectType = StatusEnums.ProjectType.Renew,
                oPPTViewFrame = new ProjectPackageToViewFrame()
                {
                    lLanes = lTempLanes,
                    lTPs = lTempTPs
                }
            });

            this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
            {
                eProjectType = StatusEnums.ProjectType.Delete,
                oPPTInfoFrame = new ProjectPackageToInfoFrame()
                {
                    lAGVStatuses = lTempAGVStatus
                }
            });

            this.DeleteAOLSEvent.Invoke(this, new DeleteAGVOccupyLineSegArgs()
            {
                lAGVIDs = lTempAGVIDs
            });

            return true;
        }


        /// <summary>
        /// 选取指定AGV到指定Lane
        /// </summary>
        public void AGVTrafficChoose(uint AGV_num, uint Lane_num)
        {
            Random rand = new Random();
            string Str;
            List<Lane> lFreeLanes;
            StatusEnums.StoreType eStoreType;
            GenerateAGVRouteEventArgs e;
            int LoopNum, TestAddNum;

            TestAddNum = 0;


            AGV oA = this.oSimDataStore.dAGVs[AGV_num];

            if (oA.eMotionStatus == StatusEnums.MotionStatus.Free || oA.eMotionStatus == StatusEnums.MotionStatus.Waiting)
            {

                TestAddNum = 1;
                Str = rand.Next(0, 5).ToString();
                Enum.TryParse<StatusEnums.StoreType>(Str, out eStoreType);
                oA.oTwinStoreUnit.eUnitStoreType = eStoreType;

                lFreeLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eStatus == LaneStatus.IDLE).ToList();
                LoopNum = 0;
                do
                {
                    // row = Convert.ToInt32(Math.Floor(rand.NextDouble() * (double)lFreeLanes.Count));

                    oA.AimLaneID = Lane_num;
                    

                    e = new GenerateAGVRouteEventArgs()
                    {
                        oA = oA,
                        AimLaneID = oA.AimLaneID,
                        IsGenerationSucc = false
                    };

                    this.GenerateAGVRouteEvent.Invoke(this, e);
                    LoopNum++;
                }
                while (LoopNum > 500 || !e.IsGenerationSucc);
            }
            this.TrafficTestNum = this.TrafficTestNum + TestAddNum;
        }

        /// <summary>
        /// 交通测试接口
        /// </summary>
        public void AGVTrafficStepTest()
        {
            Random rand = new Random();
            Solute SJK = new Solute();
            string Str;
            List<Lane> lFreeLanes;
            StatusEnums.StoreType eStoreType;
            GenerateAGVRouteEventArgs e;
            int LoopNum, TestAddNum;

            TestAddNum = 0;

            foreach (AGV oA in this.oSimDataStore.dAGVs.Values)
            {
                int num_x = (int)oA.MidPoint.X;
                int num_y = (int)oA.MidPoint.Y;
                int nowLine = (int)oA.CurrAGVLineID;

                SJK.Add((int)oA.ID, num_x, num_y, (int)oA.RotateAngle, "agvid", "x", "y", "angle", "agv");
                SJK.DeleteLine((int)oA.ID, "dataintime", "agvid");
                SJK.Add((int)oA.ID, num_x, num_y, (int)oA.RotateAngle, (int)oA.CurrLaneID, (int)oA.NextLaneID, (int)oA.AimLaneID, nowLine, (int)oA.CurrVelo, "agvid", "x", "y", "angle", "CurrLaneID", "NextLaneID", "AimLaneID", "CurrAGVline", "Deviation", "dataintime");

                SJK.DeleteLine((int)oA.ID, "agv_lane", "agvid");
                SJK.DeleteLine((int)oA.ID, "agv_AGVline", "agvid");

                if ((int)oA.CurrLaneID != 0)
                {
                    SJK.Add((int)oA.ID, (int)oA.CurrLaneID, "agvid", "laneid", "agv_lane");
                }
                SJK.Add((int)oA.ID, (int)oA.CurrAGVLineID, "agvid", "AGVlineid", "agv_AGVline");

                if (oA.eMotionStatus == StatusEnums.MotionStatus.Free || oA.eMotionStatus == StatusEnums.MotionStatus.Waiting)
                {
                    TestAddNum = 1;
                    Str = rand.Next(0, 5).ToString();
                    Enum.TryParse<StatusEnums.StoreType>(Str, out eStoreType);
                    oA.oTwinStoreUnit.eUnitStoreType = eStoreType;

                    lFreeLanes = this.oSimDataStore.dLanes.Values.Where(u => u.eStatus == LaneStatus.IDLE).ToList();
                    LoopNum = 0;
                    do
                    {
                        row = Convert.ToInt32(Math.Floor(rand.NextDouble() * (double)lFreeLanes.Count));
                        while (row == 25)
                        {
                            row = Convert.ToInt32(Math.Floor(rand.NextDouble() * (double)lFreeLanes.Count));
                        }

                        oA.AimLaneID = lFreeLanes[row].ID;
                        if (oA.ID == 951)
                        {
                            oA.AimLaneID = 25;
                        }
                        oA.eMotionStatus = StatusEnums.MotionStatus.Moving;

                        e = new GenerateAGVRouteEventArgs()
                        {
                            oA = oA,
                            AimLaneID = oA.AimLaneID,
                            IsGenerationSucc = false
                        };

                        this.GenerateAGVRouteEvent.Invoke(this, e);
                        LoopNum++;
                        if (LoopNum > 500)
                        {

                        }
                    }
                    while ( !e.IsGenerationSucc);
                }
            }

            this.TrafficTestNum = this.TrafficTestNum + TestAddNum;
        }

    }

    public class QCTPRange
    {
        public uint QCID;
        public int StartPos;
        public int EndPos;
    }

    public class QCModuleTester
    {



    }

    public class YCModuleTester
    {

    }
}
       