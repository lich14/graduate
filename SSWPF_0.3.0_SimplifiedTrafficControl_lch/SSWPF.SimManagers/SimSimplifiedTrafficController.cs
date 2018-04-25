using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using ZECS.Schedule.DBDefine.YardMap;
using SSWPF.Define;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using SharpSim;
using solutionfordata;
using Potential;

namespace SSWPF.SimManagers
{
    public class SimSimplifiedTrafficController
    {
        public event EventHandler<ProjectToViewFrameEventArgs> ProjectToViewFrameEvent;
        public event EventHandler<ProjectToInfoFrameEventArgs> ProjectToInfoFrameEvent;

        public SimDataStore oSimDataStore;
        public Dictionary<uint, AGVLine> dEssentialAGVLines;
        private List<AGV> lMovingAGVs;
        public Dictionary<uint, SimTransponder> dQCAndEssentialTPs;
        public Dictionary<uint, AGVRoute> dAGVRoutes;
        private List<AGVOccuLineSeg> lAGVOccupyLineSegs;
        private Dictionary<uint, double> dAGVAnglesEnRoute;
        public Dictionary<uint, int> dStorecount = new Dictionary<uint,int>();
       
        AGVOccuLineSeg oAOLS;
        AGVLine oAL;

        // 必须距磁钉2米（除非约到），必须距前车3米，AGV 转弯半径（参考），AGV向前最大申请距离（参考）
        private readonly double CompelAGVIntvToTP, CompelAGVIntvToAGV, AGVTurnRadius, MaxReclaimLengthRecommended;
        private readonly int DecimalNum;
        
        // For Debug Use
        private FileStream fs_Routing, fs_AgvResvRelease;
        private StreamWriter sw_Routing, sw_AgvResvRelease;
        private long DeadlockDetectTime, TPReclaimTime;
        private int CurrCSVNum, CSVReserveNumInOneRecord, TPReclaimRecordNum, CSVRecordNum;
        private List<long> lFullCycleDetectRecords = new List<long>();
        private bool IsRouteLog, IsDeadLockDetectOutputLog, IsDeadLockDetectInputLog, IsSelfCheckAndThrowException;
        public List<double> lDeadlockDetectTimes;
        private SimException DeadLockExpection = new SimException("DeadLock Detected");

        public SimSimplifiedTrafficController(int DecimalNum = 2)
        {
            this.lAGVOccupyLineSegs = new List<AGVOccuLineSeg>();
            this.CompelAGVIntvToTP = 2;
            this.CompelAGVIntvToAGV = 3;
            this.DecimalNum = DecimalNum;
            this.AGVTurnRadius = 14;
            this.MaxReclaimLengthRecommended = 15;

            this.CurrCSVNum = -1;
            this.CSVReserveNumInOneRecord = 3000;
            this.TPReclaimRecordNum = 10000;
            this.CSVRecordNum = 3;
        }

        public SimSimplifiedTrafficController(SimDataStore oSDS)
            : this()
        {
            this.oSimDataStore = oSDS;
        }

        /// <summary>
        /// 无调试初始化
        /// </summary>
        /// <returns></returns>
        public bool Init()
        {
            List<SimTransponder> lTempTPs1, lTempTPs2;
            int DivNum;
            double dGap, dMinAGVLength;

            if (this.oSimDataStore == null)
            {
                Logger.Simulate.Error("SimTrafficController: Null SimDataStore!");
                return false;
            }

            List<AGVLine> oagvlineselect = this.oSimDataStore.dAGVLines.Values.Where(u => u.eMoveDir != CHE_Direction.Unknown).ToList();
            for (int i = 0; i < oagvlineselect.Count(); i++)
            {
                if (!dStorecount.Keys.Contains(oagvlineselect[i].ID))
                {
                    dStorecount.Add(oagvlineselect[i].ID, 0);
                }
            }

            if (this.ProjectToViewFrameEvent == null || this.ProjectToInfoFrameEvent == null)
            {
                Logger.Simulate.Error("SimTrafficController: Null Event Listener!");
                return false;
            }

            if (this.oSimDataStore.dAGVs == null || this.oSimDataStore.dAGVs.Count == 0)
            {
                Logger.Simulate.Error("SimTrafficController: No AGV!");
                return false;
            }

            this.dAGVRoutes = new Dictionary<uint, AGVRoute>();
            this.dAGVAnglesEnRoute = new Dictionary<uint, double>();
            this.lDeadlockDetectTimes = new List<double>();

            // Routing Debug Log
            if (File.Exists(System.Environment.CurrentDirectory + "\\PrintOut\\Routings.txt"))
                File.Delete(System.Environment.CurrentDirectory + "\\PrintOut\\Routings.txt");
            this.fs_Routing = new FileStream(System.Environment.CurrentDirectory + "\\PrintOut\\Routings.txt", FileMode.Create);
            this.sw_Routing = new StreamWriter(this.fs_Routing, Encoding.Default);
            this.sw_Routing.WriteLine(DateTime.Now.ToString() + " Collection of Routings Starts");
            this.sw_Routing.Close();
            this.fs_Routing.Close();

            // Strong Component Debug Log
            if (Directory.Exists(System.Environment.CurrentDirectory + "\\PrintOut\\SolverInputs"))
                Directory.Delete(System.Environment.CurrentDirectory + "\\PrintOut\\SolverInputs", true);
            if (Directory.Exists(System.Environment.CurrentDirectory + "\\PrintOut\\SolverOutputs"))
                Directory.Delete(System.Environment.CurrentDirectory + "\\PrintOut\\SolverOutputs", true);
            Directory.CreateDirectory(System.Environment.CurrentDirectory + "\\PrintOut\\SolverInputs");
            Directory.CreateDirectory(System.Environment.CurrentDirectory + "\\PrintOut\\SolverOutputs");

            // DeadLock Detect Debug Log
            if (Directory.Exists(System.Environment.CurrentDirectory + "\\PrintOut\\FullCycleDetectInputs"))
                Directory.Delete(System.Environment.CurrentDirectory + "\\PrintOut\\FullCycleDetectInputs", true);
            if (Directory.Exists(System.Environment.CurrentDirectory + "\\PrintOut\\TPActions"))
                Directory.Delete(System.Environment.CurrentDirectory + "\\PrintOut\\TPActions", true);
            Directory.CreateDirectory(System.Environment.CurrentDirectory + "\\PrintOut\\FullCycleDetectInputs");
            Directory.CreateDirectory(System.Environment.CurrentDirectory + "\\PrintOut\\TPActions");

            // 理出必要 AGVLine，包括单行道和有 Lane 的 AGVLine
            this.dEssentialAGVLines = this.oSimDataStore.dAGVLines.Values.Where(u => u.eMoveDir != CHE_Direction.Unknown
                || this.oSimDataStore.dLanes.Values.FirstOrDefault(v => v.LineID == u.ID) != null).ToDictionary(o => o.ID);
            this.dEssentialAGVLines.All(u => u.Value.bIfEssential = true);

            // 理出必要磁钉并且标记，注意补充 Essential 磁钉到足够密集
            this.dQCAndEssentialTPs = this.oSimDataStore.dTransponders.Values.Where(u => u.LaneID > 0 || u.MateID > 0
                || (this.dEssentialAGVLines.ContainsKey(u.HorizontalLineID) && this.dEssentialAGVLines.ContainsKey(u.VerticalLineID))).ToDictionary(u => u.ID);
            this.dQCAndEssentialTPs.Values.Where(u => this.dEssentialAGVLines.ContainsKey(u.HorizontalLineID)
                && this.dEssentialAGVLines.ContainsKey(u.VerticalLineID)).ToList().ForEach(u => u.bIfEssential = true);

            // 补充磁钉到足够密，不至于出现有车不占磁钉的情况
            dMinAGVLength = this.oSimDataStore.dAGVs.Values.Select(u => u.oType).Select(u => u.Length).Min();
            foreach (AGVLine oAL in this.dEssentialAGVLines.Values)
            {
                if (oAL.eFlowDir == CoordinateDirection.X_NEGATIVE || oAL.eFlowDir == CoordinateDirection.X_POSITIVE)
                {
                    lTempTPs1 = this.dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oAL.ID).OrderBy(u => u.LogicPosX).ToList();
                    for (int i = 0; i < lTempTPs1.Count - 1; i++)
                    {
                        if (lTempTPs1[i + 1].LogicPosX - lTempTPs1[i].LogicPosX > dMinAGVLength && lTempTPs1[i].LaneID == 0 && lTempTPs1[i + 1].LaneID == 0)
                        {
                            lTempTPs2 = this.oSimDataStore.dTransponders.Values.Where(u =>
                                u.HorizontalLineID == oAL.ID && u.LogicPosX >= lTempTPs1[i].LogicPosX && u.LogicPosX <= lTempTPs1[i + 1].LogicPosX).ToList();
                            dGap = lTempTPs1[i + 1].LogicPosX - lTempTPs1[i].LogicPosX;
                            DivNum = Convert.ToInt32(Math.Ceiling(dGap / dMinAGVLength));
                            for (int j = 1; j < DivNum; j++)
                            {
                                lTempTPs2.RemoveAll(u => u.LogicPosX < lTempTPs1[i].LogicPosX + dGap / DivNum * j);
                                if (lTempTPs2.Count > 0)
                                {
                                    lTempTPs2[0].bIfEssential = true;
                                    this.dQCAndEssentialTPs.Add(lTempTPs2[0].ID, lTempTPs2[0]);
                                }
                            }
                        }
                    }
                }
                else
                {
                    lTempTPs1 = this.dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oAL.ID).OrderBy(u => u.LogicPosY).ToList();
                    for (int i = 0; i < lTempTPs1.Count - 1; i++)
                    {
                        if (lTempTPs1[i + 1].LogicPosY - lTempTPs1[i].LogicPosY > dMinAGVLength && lTempTPs1[i].LaneID == 0 && lTempTPs1[i + 1].LaneID == 0)
                        {
                            lTempTPs2 = this.oSimDataStore.dTransponders.Values.Where(u =>
                                u.HorizontalLineID == oAL.ID && u.LogicPosY >= lTempTPs1[i].LogicPosY && u.LogicPosY <= lTempTPs1[i + 1].LogicPosY).ToList();
                            dGap = lTempTPs1[i + 1].LogicPosY - lTempTPs1[i].LogicPosY;
                            DivNum = Convert.ToInt32(Math.Ceiling(dGap / dMinAGVLength));
                            for (int j = 1; j < DivNum; j++)
                            {
                                lTempTPs2.RemoveAll(u => u.LogicPosY < lTempTPs1[i].LogicPosY + dGap / DivNum * j);
                                if (lTempTPs2.Count > 0)
                                {
                                    lTempTPs2[0].bIfEssential = true;
                                    this.dQCAndEssentialTPs.Add(lTempTPs2[0].ID, lTempTPs2[0]);
                                }
                            }
                        }
                    }
                }
            }
            lTempTPs1 = this.oSimDataStore.dTransponders.Values.Where(u => u.HorizontalLineID >= 44 && u.HorizontalLineID <= 47).ToList();
            foreach (SimTransponder oTP in lTempTPs1)
            {
                if (!this.dQCAndEssentialTPs.ContainsKey(oTP.ID))
                    this.dQCAndEssentialTPs.Add(oTP.ID, oTP);
            }

            // AGVLines 相互连接
            foreach (SimTransponder oTP in this.dQCAndEssentialTPs.Values)
            {
                if (this.dEssentialAGVLines.Keys.Contains(oTP.HorizontalLineID) && this.dEssentialAGVLines.Keys.Contains(oTP.VerticalLineID))
                {
                    this.dEssentialAGVLines[oTP.HorizontalLineID].lLinkLineIDs.Add(oTP.VerticalLineID);
                    this.dEssentialAGVLines[oTP.VerticalLineID].lLinkLineIDs.Add(oTP.HorizontalLineID);
                }
            }

            // 地图至少包含四个关键 TP 和一个 AGVLine
            if (this.dQCAndEssentialTPs.Count < 4 || this.dEssentialAGVLines.Count == 0)
            {
                Logger.Simulate.Error("TrafficController: Initialization Failed For Not Enough Essential Transponders Or AGVLines");
                return false;
            }

            // AGV 占据 AGVLine，并且初始化 AGV 的车辆角度。
            // 注意保证各 AgvOccuLineSeg 在 AGVLine 上的特征位置 StartPos 和 EndPos 的严格大小关系
            foreach (uint AgvID in this.oSimDataStore.dAGVs.Keys)
            {
                oAL = this.dEssentialAGVLines.Values.First(u => u.lLaneIDs.Contains(this.oSimDataStore.dAGVs[AgvID].CurrLaneID));

                // 占据 Line，生成 AGVOccuLineSeg
                oAOLS = new AGVOccuLineSeg() { AGVID = AgvID, AGVLineID = oAL.ID };
                if (oAL.eFlowDir == CoordinateDirection.X_POSITIVE || oAL.eFlowDir == CoordinateDirection.X_NEGATIVE)
                {
                    oAOLS.StartPos = this.oSimDataStore.dAGVs[AgvID].MidPoint.X - this.oSimDataStore.dAGVs[AgvID].oType.Length / 2;
                    oAOLS.EndPos = this.oSimDataStore.dAGVs[AgvID].MidPoint.X + this.oSimDataStore.dAGVs[AgvID].oType.Length / 2;
                    this.dAGVAnglesEnRoute.Add(AgvID, 0);
                }
                else if (oAL.eFlowDir == CoordinateDirection.Y_NEGATIVE || oAL.eFlowDir == CoordinateDirection.Y_POSITIVE)
                {
                    oAOLS.StartPos = this.oSimDataStore.dAGVs[AgvID].MidPoint.Y - this.oSimDataStore.dAGVs[AgvID].oType.Length / 2;
                    oAOLS.EndPos = this.oSimDataStore.dAGVs[AgvID].MidPoint.Y + this.oSimDataStore.dAGVs[AgvID].oType.Length / 2;
                    this.dAGVAnglesEnRoute.Add(AgvID, 90);
                }
                oAOLS.bStartPointHinge = false;
                oAOLS.bEndPointHinge = false;
                this.lAGVOccupyLineSegs.Add(oAOLS);

                // 占据磁钉
                List<SimTransponder> lTPs = this.dQCAndEssentialTPs.Values.Where(u => oAL.lTPIDs.Contains(u.ID)).ToList();
                if (oAL.eFlowDir == CoordinateDirection.X_POSITIVE || oAL.eFlowDir == CoordinateDirection.X_NEGATIVE)
                    lTPs = lTPs.Where(u => u.LogicPosX >= oAOLS.StartPos && u.LogicPosX <= oAOLS.EndPos).ToList();
                else
                    lTPs = lTPs.Where(u => u.LogicPosY >= oAOLS.StartPos && u.LogicPosY <= oAOLS.EndPos).ToList();

                foreach (SimTransponder oTP in lTPs)
                {
                    oTP.dRouteTPDivisions.Add(AgvID, StatusEnums.RouteTPDivision.Claim);
                }
            }

            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
            {
                eProjectType = StatusEnums.ProjectType.Renew,
                oPPTViewFrame = new ProjectPackageToViewFrame()
                    {
                        lTPs = this.dQCAndEssentialTPs.Values.ToList(),
                        lAGVLines = this.dEssentialAGVLines.Values.ToList()
                    }
            });

            return true;
        }

        /// <summary>
        /// 有调试输出的初始化
        /// </summary>
        /// <param name="bRouteLog">是否打印路径</param>
        /// <param name="IfDeadLockDetectOutputLog">是否打印强连通分量</param>
        /// <param name="IfDeadLockDetectInputLog">是否打印路径段、搜索过程和路径段更新</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool Init(bool bRouteLog = false, bool IfDeadLockDetectInputLog = false, bool IfDeadLockDetectOutputLog = false, bool IfSelfCheck = true)
        {
            bool bRet = false;

            bRet = this.Init();

            if (bRet)
            {
                this.IsRouteLog = bRouteLog;
                this.IsDeadLockDetectInputLog = IfDeadLockDetectInputLog;
                this.IsDeadLockDetectOutputLog = IfDeadLockDetectOutputLog;
                this.IsSelfCheckAndThrowException = IfSelfCheck;
            }

            return bRet;
        }

        #region AGV路径生成

        /// <summary>
        /// 路径重置接口。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnResetAGVRoutes(object sender, ResetAGVRoutesEventArgs e)
        {
            this.ResetAGVRoutes();
        }

        /// <summary>
        /// 路径生成接口。注意事件消息有返回值
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnGenerateAGVRoute(object sender, GenerateAGVRouteEventArgs e)
        {
            e.IsGenerationSucc = this.GenerateAGVRouteToGivenLane(e.oA);
        }

        /// <summary>
        /// 删除 AOLS 接口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnDeleteAOLS(object sender, DeleteAGVOccupyLineSegArgs e)
        {
            this.lAGVOccupyLineSegs.RemoveAll(u => e.lAGVIDs.Exists(v => v == u.AGVID));
        }

        /// <summary>
        /// 针对AGV生成路径。注意 AGV 的 .AimLaneID 和 .CurrLaneID 必须有合法值
        /// </summary>
        /// <param name="oA">AGV对象</param>
        /// <returns>路径生成成功返回true，失败返回false</returns>
        private bool GenerateAGVRouteToGivenLane(AGV oA)
        {
            bool bRet = true;
            ProjectPackageToViewFrame oPPTViewFrame;
            string Flag = "";
            List<uint> lAGVLineIDs = new List<uint>();
            List<Lane> lLanes = new List<Lane>();
            List<Lane> lTempLanes1 = new List<Lane>();
            List<Lane> lTempLanes2 = new List<Lane>();
            List<SimTransponder> lTempTPs = new List<SimTransponder>();
            AGVRoute oAGVRoute;
            RouteSegment oRS;
            List<TPInfoEnRoute> lTPInfoERs, lTempTPInfos;
            StatusEnums.STSVisitDir eSTSVisitDir;
            uint uTemp;
            double LinePos;

            if (oA.CurrLaneID == 0 || !this.oSimDataStore.dLanes.ContainsKey(oA.CurrLaneID))
            {
                bRet = false;
                Flag = "Invalid Origin LaneID : " + oA.CurrLaneID.ToString() + " when Searching Route for AGV : " + oA.ID.ToString();
            }
            else if (oA.AimLaneID == 0 || !this.oSimDataStore.dLanes.ContainsKey(oA.AimLaneID) || this.oSimDataStore.dLanes[oA.AimLaneID].eStatus != LaneStatus.IDLE)
            {
                bRet = false;
                Flag = "Invalid Destination LaneID : " + oA.AimLaneID.ToString() + " when Searching Route for AGV : " + oA.ID.ToString();
            }
            else if (this.oSimDataStore.dLanes[oA.CurrLaneID].eType != AreaType.STS_PB && this.oSimDataStore.dLanes[oA.CurrLaneID].eType != AreaType.STS_TP
                && this.oSimDataStore.dLanes[oA.CurrLaneID].eType != AreaType.WS_PB && this.oSimDataStore.dLanes[oA.CurrLaneID].eType != AreaType.WS_TP)
            {
                bRet = false;
                Flag = "UnExpected Origin Lane Type : " + this.oSimDataStore.dLanes[oA.CurrLaneID].eType.ToString() + " when searching Route for AGV : " + oA.ID.ToString();
            }
            else if (this.oSimDataStore.dLanes[oA.AimLaneID].eType != AreaType.STS_PB && this.oSimDataStore.dLanes[oA.AimLaneID].eType != AreaType.STS_TP
                && this.oSimDataStore.dLanes[oA.AimLaneID].eType != AreaType.WS_PB && this.oSimDataStore.dLanes[oA.AimLaneID].eType != AreaType.WS_TP)
            {
                bRet = false;
                Flag = "UnExpected Destination Lane Type : " + this.oSimDataStore.dLanes[oA.AimLaneID].eType.ToString() + " when searching Route for AGV : " + oA.ID.ToString();
            }

            // 先收集经过的 AGVLine 和车道集合，再补磁钉。注意转弯半径约束
            if (bRet)
            {
                lLanes.Add(this.oSimDataStore.dLanes[oA.CurrLaneID]);
                lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.CurrLaneID].LineID);
                if (oA.CurrLaneID != oA.AimLaneID)
                {
                    switch (this.oSimDataStore.dLanes[oA.CurrLaneID].eType)
                    {
                        case AreaType.WS_TP:
                            // From WSTP
                            switch (this.oSimDataStore.dLanes[oA.AimLaneID].eType)
                            {
                                case AreaType.WS_TP:
                                    // From WSTP to WSTP
                                    if (this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.AimLaneID].LineID].FeaturePosition
                                        > this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].FeaturePosition)
                                    {
                                        // 如果起止方向与单行道方向相符，可以直接到
                                        uTemp = this.GetOneCHEDirLineIDForUTurnwithpotential(
                                                CHE_Direction.South,
                                                this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y - this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y,
                                                this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.CurrLaneID].TPIDEnd].PhysicalPosX,
                                                this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.AimLaneID].TPIDEnd].PhysicalPosX,
                                                this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y);
                                        if (uTemp > 0)
                                        {
                                            //lAGVLineIDs.Add(uTemp);
                                            lAGVLineIDs.Add(346);
                                            lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                            lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                        }
                                        else
                                        {
                                            bRet = false;
                                            Flag = "Generate Route from WSTP Lane : " + oA.CurrLaneID.ToString() + " to WSTP Lane : " + oA.AimLaneID.ToString()
                                                + " of AGV : " + oA.ID.ToString() + " Failed for No AGVLine Found Between Ori And Dest WSTPs";
                                        }
                                    }
                                    else
                                    {
                                        // 得找两个 WSPB 绕回去。第一个不高于 CurrLane ，第二个不低于 AimLane
                                        lTempLanes1 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_PB && u.eStatus == LaneStatus.IDLE
                                            && u.pWork.Y >= this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y).ToList();
                                        lTempLanes1.Sort((u, v) => u.pWork.Y.CompareTo(v.pWork.Y));
                                        lTempLanes2 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_PB && u.eStatus == LaneStatus.IDLE
                                            && u.pWork.Y <= this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y).ToList();
                                        lTempLanes2.Sort((u, v) => v.pWork.Y.CompareTo(u.pWork.Y));

                                        if (lTempLanes1.Count > 0 && lTempLanes2.Count > 0)
                                        {
                                            bRet = false;
                                            foreach (Lane oL1 in lTempLanes1)
                                            {
                                                foreach (Lane oL2 in lTempLanes2)
                                                {
                                                    uTemp = this.GetOneCHEDirLineIDForUTurnwithpotential(
                                                        CHE_Direction.North, 
                                                        Math.Abs(oL1.pWork.Y - oL2.pWork.Y), 
                                                        this.oSimDataStore.dTransponders[oL1.TPIDStart].LogicPosX,
                                                        this.oSimDataStore.dTransponders[oL1.TPIDEnd].LogicPosX,
                                                        oL1.pWork.Y);
                                                    if (uTemp > 0)
                                                    {
                                                        lLanes.Add(oL1);
                                                        lLanes.Add(oL2);
                                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);

                                                        // 模拟斜行，不管半径约束
                                                        if (this.oSimDataStore.dLanes[oA.CurrLaneID].LineID != oL1.LineID)
                                                        {
                                                            //lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.South, this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y));

                                                            lAGVLineIDs.Add(356);
                                                            lAGVLineIDs.Add(oL1.LineID);
                                                        }
                                                        lAGVLineIDs.Add(uTemp);
                                                        if (this.oSimDataStore.dLanes[oA.AimLaneID].LineID != oL2.LineID)
                                                        {
                                                            lAGVLineIDs.Add(oL2.LineID);
                                                            lAGVLineIDs.Add(346);
                                                            //lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.South, oL2.pWork.Y));
                                                        }
                                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);

                                                        bRet = true;
                                                        break;
                                                    }
                                                }
                                                if (bRet)
                                                    break;
                                            }

                                            if (!bRet)
                                                Flag = "Generate Route from WSTP Lane : " + oA.CurrLaneID.ToString() + " to WSTP Lane : " + oA.AimLaneID.ToString()
                                                + " of AGV : " + oA.ID.ToString() + " Failed for no Two Idle Extra WSPBs Found Considering Turn Radius";
                                        }
                                        else
                                        {
                                            bRet = false;
                                            Flag = "Generate Route from WSTP Lane : " + oA.CurrLaneID.ToString() + " to WSTP Lane : " + oA.AimLaneID.ToString()
                                                + " of AGV : " + oA.ID.ToString() + " Failed for no Two Idle Extra WSPBs Found";
                                        }
                                    }
                                    break;
                                case AreaType.WS_PB:
                                    // From WSTP to WSPB
                                    if (this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.AimLaneID].LineID].FeaturePosition
                                        > this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].FeaturePosition)
                                    {
                                        // 在下方，加一个向下的单行道即可
                                        //lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.South, this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y));
                                        lAGVLineIDs.Add(356);
                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    }
                                    else if (this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.AimLaneID].LineID].FeaturePosition
                                        < this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].FeaturePosition)
                                    {
                                        // 在上方，要垫一个靠下的PB绕过去。注意垫的 WSPB 和目标之间须符合转弯半径约束
                                        lTempLanes1 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_PB && u.eStatus == LaneStatus.IDLE
                                            && u.pWork.Y >= this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y).OrderBy(u => u.pWork.Y).ToList();

                                        if (lTempLanes1.Count > 0)
                                        {
                                            bRet = false;
                                            foreach (Lane oL in lTempLanes1)
                                            {
                                                uTemp = this.GetOneCHEDirLineIDForUTurnwithpotential(
                                                    CHE_Direction.North, 
                                                    Math.Abs(oL.pWork.Y - this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y), 
                                                    this.oSimDataStore.dTransponders[oL.TPIDEnd].LogicPosX,
                                                    this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.AimLaneID].TPIDEnd].LogicPosX,
                                                    oL.pWork.Y);
                                                if (uTemp > 0)
                                                {
                                                    if (this.oSimDataStore.dLanes[oA.CurrLaneID].LineID != oL.LineID)
                                                    {
                                                        lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.South, this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y));
                                                        lAGVLineIDs.Add(oL.LineID);
                                                    }
                                                    lAGVLineIDs.Add(uTemp);
                                                    lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);

                                                    lLanes.Add(oL);
                                                    lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);

                                                    bRet = true;
                                                    break;
                                                }
                                            }

                                            if (!bRet)
                                                Flag = "Generate Route from WSTP Lane : " + oA.CurrLaneID.ToString() + " to WSTP Lane : " + oA.AimLaneID.ToString()
                                                + " of AGV : " + oA.ID.ToString() + " Failed for Two Idle Extra WSPBs Found Considering Turn Radius";
                                        }
                                        else
                                        {
                                            bRet = false;
                                            Flag = "Generate Route from WSTP Lane : " + oA.CurrLaneID.ToString() + " to WSPB Lane : " + oA.AimLaneID.ToString()
                                                + " of AGV : " + oA.ID.ToString() + " Failed for no Idle Extra WSPB Found";
                                        }
                                    }
                                    else
                                    {
                                        // 正好一条线
                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    }
                                    break;
                                case AreaType.STS_PB:
                                    // From WSTP to QCPB
                                    // 如果可以直达(符合单行线约束的前提下)，直接到线避免转弯，不用加AGVLine；否则，偏向于某侧道路
                                    if (this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.AimLaneID].LineID].eMoveDir != CHE_Direction.South)
                                    {
                                        uint oAGVlinestore=this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.South, this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y);
                                        lAGVLineIDs.Add(oAGVlinestore);
                                        // 找个分界线，任意一条都行。
                                        uTemp = this.oSimDataStore.dAGVLines.Values.First(u => u.eMoveDir == CHE_Direction.South).ID;
                                        if (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X < this.oSimDataStore.dAGVLines[uTemp].FeaturePosition)
                                        {
                                            switch (oAGVlinestore)
                                            {
                                                case 346:
                                                    lAGVLineIDs.Add(38);
                                                    break;
                                                case 351:
                                                    lAGVLineIDs.Add(39);
                                                    break;
                                                case 356:
                                                    lAGVLineIDs.Add(40);
                                                    break;
                                                default:
                                                    lAGVLineIDs.Add(38);
                                                    break;
                                            }
                                        }
                                           // lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.West, this.oSimDataStore.dAGVLines[oAGVlinestore].FeaturePosition));
                                        else
                                        {
                                            switch (oAGVlinestore)
                                            {
                                                case 346:
                                                    lAGVLineIDs.Add(43);
                                                    break;
                                                case 351:
                                                    lAGVLineIDs.Add(42);
                                                    break;
                                                case 356:
                                                    lAGVLineIDs.Add(41);
                                                    break;
                                                default:
                                                    lAGVLineIDs.Add(41);
                                                    break;
                                            }
                                        }
                                            //lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.East, this.oSimDataStore.dAGVLines[oAGVlinestore].FeaturePosition));
                                    }
                                    lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                    lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    break;
                                case AreaType.STS_TP:
                                    // From WSTP to QCTP，不能直达
                                    bRet = false;
                                    Flag = "Route from WSTP to QCTP is not Expected to be Generated Directly";
                                    break;
                            }
                            break;
                        case AreaType.WS_PB:
                            // From WSPB
                            switch (this.oSimDataStore.dLanes[oA.AimLaneID].eType)
                            {
                                case AreaType.WS_TP:
                                    // From WSPB to WSTP
                                    if (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y > this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y)
                                    {
                                        lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.South, this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y));
                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    }
                                    else if (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y < this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y)
                                    {
                                        // 找个 WSPB 垫着
                                        lTempLanes1 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_PB && u.eStatus == LaneStatus.IDLE
                                            && u.pWork.Y <= this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y).ToList();
                                        lTempLanes1.Sort((u, v) => v.pWork.Y.CompareTo(u.pWork.Y));

                                        if (lTempLanes1.Count > 0)
                                        {
                                            bRet = false;
                                            foreach (Lane oL in lTempLanes1)
                                            {
                                                uTemp = this.GetOneCHEDirLineIDForUTurnwithpotential(
                                                    CHE_Direction.North,
                                                    Math.Abs(this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y - oL.pWork.Y),
                                                    this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.CurrLaneID].TPIDEnd].LogicPosX,
                                                    this.oSimDataStore.dTransponders[oL.TPIDEnd].LogicPosX,
                                                    this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y);
                                                if (uTemp > 0)
                                                {
                                                    lAGVLineIDs.Add(uTemp);
                                                    if (this.oSimDataStore.dLanes[oA.AimLaneID].LineID != oL.LineID)
                                                    {
                                                        lAGVLineIDs.Add(oL.LineID);
                                                        lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.South, oL.pWork.Y));
                                                    }
                                                    lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);

                                                    lLanes.Add(oL);
                                                    lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);

                                                    bRet = true;
                                                    break;
                                                }
                                            }

                                            if (!bRet)
                                                Flag = "Generate Route from WSPB Lane : " + oA.CurrLaneID.ToString() + " to WSTP Lane : " + oA.AimLaneID.ToString()
                                                + " of AGV : " + oA.ID.ToString() + " Failed for no AGVLine Found Between Ori And Extra WSPB";
                                        }
                                        else
                                        {
                                            bRet = false;
                                            Flag = "Generate Route from WSPB Lane : " + oA.CurrLaneID.ToString() + " to WSTP Lane : " + oA.AimLaneID.ToString()
                                                + " of AGV : " + oA.ID.ToString() + " Failed for no WSPB Found";
                                        }
                                    }
                                    else
                                    {
                                        // 正好一条线
                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    }
                                    break;
                                case AreaType.WS_PB:
                                    // From WSPB to WSPB
                                    if (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y < this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y)
                                        uTemp = this.GetOneCHEDirLineIDForUTurnwithpotential(
                                            CHE_Direction.North,
                                            Math.Abs(this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y - this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y),
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.CurrLaneID].TPIDEnd].LogicPosX,
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.AimLaneID].TPIDEnd].LogicPosX,
                                            this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y);
                                    else
                                        uTemp = this.GetOneCHEDirLineIDForUTurnwithpotential(
                                            CHE_Direction.South,
                                            Math.Abs(this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y - this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y),
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.CurrLaneID].TPIDStart].LogicPosX,
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.AimLaneID].TPIDStart].LogicPosX,
                                            this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y);

                                    if (uTemp > 0)
                                    {
                                        lAGVLineIDs.Add(uTemp);
                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    }
                                    else
                                    {
                                        bRet = false;
                                        Flag = "Generate Route from WSPB Lane : " + oA.CurrLaneID.ToString() + " to WSPB Lane : " + oA.AimLaneID.ToString()
                                            + " of AGV : " + oA.ID.ToString() + " Failed for no AGVLine Found Considering Turn Radius";
                                    }
                                    break;
                                case AreaType.STS_PB:
                                    // From WSPB to QCPB
                                    // 如果可以直达(符合单行线约束的条件下)，直接到线避免转弯，不用加AGVLine；否则，随机
                                    if (this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.AimLaneID].LineID].eMoveDir != CHE_Direction.South)
                                    {
                                        uint oAGVlinestore = this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.South, this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y);
                                        lAGVLineIDs.Add(oAGVlinestore);
                                        uTemp = this.oSimDataStore.dAGVLines.Values.First(u => u.eMoveDir == CHE_Direction.South).ID;
                                        if (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X < this.oSimDataStore.dAGVLines[uTemp].FeaturePosition)
                                            lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.West,this.oSimDataStore.dAGVLines[oAGVlinestore].FeaturePosition));
                                        else
                                            lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.East, this.oSimDataStore.dAGVLines[oAGVlinestore].FeaturePosition));
                                    }
                                    lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                    lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    break;
                                case AreaType.STS_TP:
                                    // From WSPB to QCTP，不能直达
                                    bRet = false;
                                    Flag = "Route from WSPB to QCTP is not Expected to be Generated Directly";
                                    break;
                            }
                            break;
                        case AreaType.STS_PB:
                            // From QCPB
                            switch (this.oSimDataStore.dLanes[oA.AimLaneID].eType)
                            {
                                case AreaType.WS_TP:
                                    // From QCPB to WSTP，必须垫一个WSPB
                                    lTempLanes1 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_PB && u.eStatus == LaneStatus.IDLE
                                        && u.pWork.Y <= this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y).ToList();
                                    lTempLanes1.Sort((u, v) => v.pWork.Y.CompareTo(u.pWork.Y));

                                    if (lTempLanes1.Count > 0)
                                    {
                                        // 如果不能直接到 PB，加线
                                        if (this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].eMoveDir != CHE_Direction.North)
                                        {
                                            LinePos = this.oSimDataStore.dAGVLines.Values.First(u => u.eMoveDir == CHE_Direction.North).FeaturePosition;
                                            uint oAGVlinestore;
                                            if (this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X < LinePos)
                                            {
                                                oAGVlinestore=this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.East, this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X);
                                                lAGVLineIDs.Add(oAGVlinestore);
                                            }
                                            else
                                            {
                                                oAGVlinestore=this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.West, this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X);
                                                lAGVLineIDs.Add(oAGVlinestore);
                                            }
                                          
                                            lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.North,this.oSimDataStore.dAGVLines[oAGVlinestore].FeaturePosition));
                                        }
                                        if (lTempLanes1[0].LineID != this.oSimDataStore.dLanes[oA.AimLaneID].LineID)
                                        {
                                            lAGVLineIDs.Add(lTempLanes1[0].LineID);
                                            lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.South, lTempLanes1[0].pWork.Y));
                                        }
                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);

                                        lLanes.Add(lTempLanes1[0]);
                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    }
                                    else
                                    {
                                        bRet = false;
                                        Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to WSTP Lane : "
                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed For No Idle WSPB Found";
                                    }
                                    break;
                                case AreaType.WS_PB:
                                    // From QCPB to WSPB
                                    // 如果不能直接到 PB，加线
                                    if (this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].eMoveDir != CHE_Direction.North)
                                    {
                                        LinePos = this.oSimDataStore.dAGVLines.Values.First(u => u.eMoveDir == CHE_Direction.North).FeaturePosition;
                                        uint oAGVlinestore;
                                        if (this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X < LinePos)
                                        {
                                            oAGVlinestore = this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.East, this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X);
                                            lAGVLineIDs.Add(oAGVlinestore);
                                        }
                                        else
                                        {
                                            oAGVlinestore = this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.West, this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X);
                                            lAGVLineIDs.Add(oAGVlinestore);
                                        }

                                        lAGVLineIDs.Add(this.GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction.North, this.oSimDataStore.dAGVLines[oAGVlinestore].FeaturePosition));
                                    }
                                    lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                    lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    break;
                                case AreaType.STS_PB:
                                    // From QCPB to QCPB
                                    uTemp = 0;
                                    if (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X < this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X)
                                        /*uTemp = this.GetOneCHEDirLineIDForUTurnwithpotential(
                                            CHE_Direction.West,
                                            this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X - this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X,
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.CurrLaneID].TPIDStart].LogicPosY,
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.AimLaneID].TPIDStart].LogicPosY,
                                            this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X);*/
                                        uTemp = 40;
                                    else
                                        /*uTemp = this.GetOneCHEDirLineIDForUTurnwithpotential(
                                            CHE_Direction.East,
                                            this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X - this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X,
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.CurrLaneID].TPIDStart].LogicPosY,
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.AimLaneID].TPIDStart].LogicPosY,
                                            this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X);*/
                                        uTemp = 41;

                                    if (uTemp > 0)
                                    {
                                        lAGVLineIDs.Add(uTemp);
                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    }
                                    else
                                    {
                                        bRet = false;
                                        Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCPB Lane : "
                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed For No AGVLine Found Considering Turn Radius";
                                    }
                                    break;
                                case AreaType.STS_TP:
                                    // From QCPB to QCTP
                                    // 情况1：从QCPBIn到对应的QCTP，不用检查转弯半径
                                    // 情况2：从QCPBInOut到QCTP，横向转弯半径和总转弯距离足够，且不被其他QCTP阻挡
                                    if ((this.oSimDataStore.dLanes[oA.CurrLaneID].eAttr == LaneAttribute.STS_PB_ONLY_IN
                                            && this.oSimDataStore.dLanes[oA.CurrLaneID].CheNo == this.oSimDataStore.dLanes[oA.AimLaneID].CheNo)
                                        || (this.oSimDataStore.dLanes[oA.CurrLaneID].eAttr == LaneAttribute.STS_PB_IN_OUT && this.oSimDataStore.dLanes[oA.CurrLaneID].CheNo == 0
                                            && Math.Abs(this.oSimDataStore.dLanes[oA.CurrLaneID].pMid.X - this.oSimDataStore.dLanes[oA.AimLaneID].pMid.X) >= this.AGVTurnRadius
                                            && this.GetManhattanDistance(this.oSimDataStore.dLanes[oA.CurrLaneID].pMid, this.oSimDataStore.dLanes[oA.AimLaneID].pMid) >= 2 * this.AGVTurnRadius))
                                    {
                                        // 除非直接到目标 Lane， 否则必须垫 QCTP_PASS，且内侧垫内侧，外侧垫外侧
                                        if (this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X < this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X)
                                        {
                                            lTempLanes1 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_TP && u.pWork.X > this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X
                                                && u.pWork.X < this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X).OrderBy(u => u.pWork.X).ToList();
                                        }
                                        else
                                        {
                                            lTempLanes1 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_TP && u.pWork.X < this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X
                                                && u.pWork.X > this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X).OrderByDescending(u => u.pWork.X).ToList();
                                        }

                                        // 能直接到，不需要穿越作业线
                                        if (lTempLanes1.Count == 0)
                                        {
                                            lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                            lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                        }
                                        else
                                        {
                                            switch (this.oSimDataStore.dLanes[oA.AimLaneID].LineID)
                                            {
                                                case 44:
                                                case 47:
                                                    lTempLanes1 = lTempLanes1.FindAll(u => u.LineID == this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                                    if (lTempLanes1.Count > 0 && lTempLanes1.All(u => u.eStatus == LaneStatus.IDLE))
                                                    {
                                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                                        lLanes.AddRange(lTempLanes1);
                                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                                    }
                                                    else
                                                    {
                                                        // 垫脚 QCTP 不够
                                                        bRet = false;
                                                        Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Idle QCTP";
                                                    }
                                                    break;
                                                case 45:
                                                    lTempLanes1 = lTempLanes1.FindAll(u => u.LineID == 44);
                                                    if (lTempLanes1.Count > 0 && lTempLanes1.All(u => u.eStatus == LaneStatus.IDLE))
                                                    {
                                                        uTemp = this.GetLineIDForSwitchBetweenQCTPsInEvenRate(lTempLanes1.Last(), this.oSimDataStore.dLanes[oA.AimLaneID], oA.oType.Length / 2);
                                                        if (uTemp > 0)
                                                        {
                                                            lAGVLineIDs.Add(44);
                                                            lAGVLineIDs.Add(uTemp);
                                                            lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                                            lLanes.AddRange(lTempLanes1);
                                                            lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                                        }
                                                        else
                                                        {
                                                            bRet = false;
                                                            Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                                + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Switching AGVLine";
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 垫脚 QCTP 不够
                                                        bRet = false;
                                                        Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Idle QCTP";
                                                    }
                                                    break;
                                                case 46:
                                                    lTempLanes1 = lTempLanes1.Where(u => u.LineID == 47).ToList();
                                                    if (lTempLanes1.Count > 0 && lTempLanes1.All(u => u.eStatus == LaneStatus.IDLE))
                                                    {
                                                        uTemp = this.GetLineIDForSwitchBetweenQCTPsInEvenRate(lTempLanes1.Last(), this.oSimDataStore.dLanes[oA.AimLaneID], oA.oType.Length / 2);
                                                        if (uTemp > 0)
                                                        {
                                                            lAGVLineIDs.Add(47);
                                                            lAGVLineIDs.Add(uTemp);
                                                            lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                                            lLanes.AddRange(lTempLanes1);
                                                            lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                                        }
                                                        else
                                                        {
                                                            bRet = false;
                                                            Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                                + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Switching AGVLine";
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 垫脚 QCTP 不够
                                                        bRet = false;
                                                        Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Idle QCTP";
                                                    }
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        bRet = false;
                                        Flag = "Route From QCPBIn To QCTP Not Paired，Or From QCPBInOut To QCTP With Turn Radius Not Enough Or Blocked By QCTPs Of Other QC";
                                    }
                                    break;
                            }
                            break;
                        case AreaType.STS_TP:
                            // From QCTP
                            switch (this.oSimDataStore.dLanes[oA.AimLaneID].eType)
                            {
                                case AreaType.WS_TP:
                                    // From QCTP to WSTP
                                    bRet = false;
                                    Flag = "Route from QCTP to WSTP is not Expected to be Generated Directly";
                                    break;
                                case AreaType.WS_PB:
                                    // From QCTP to WSPB
                                    bRet = false;
                                    Flag = "Route from QCTP to WSPB is not Expected to be Generated Directly";
                                    break;
                                case AreaType.STS_PB:
                                    // From QCTP to QCPB，两种情况：
                                    // 情况1：从对应的 QCTP 到 QCPBOut，不用检查转弯半径
                                    // 情况2：从 QCTP 到 QCPBInOut ，横向转弯半径和总转弯距离足够，且不被其他QCTP阻挡
                                    if ((this.oSimDataStore.dLanes[oA.AimLaneID].eAttr == LaneAttribute.STS_PB_ONLY_OUT
                                            && this.oSimDataStore.dLanes[oA.AimLaneID].CheNo == this.oSimDataStore.dLanes[oA.CurrLaneID].CheNo)
                                        || (this.oSimDataStore.dLanes[oA.AimLaneID].eAttr == LaneAttribute.STS_PB_IN_OUT && this.oSimDataStore.dLanes[oA.AimLaneID].CheNo == 0
                                            && Math.Abs(this.oSimDataStore.dLanes[oA.CurrLaneID].pMid.X - this.oSimDataStore.dLanes[oA.AimLaneID].pMid.X) > this.AGVTurnRadius
                                            && this.GetManhattanDistance(this.oSimDataStore.dLanes[oA.CurrLaneID].pMid, this.oSimDataStore.dLanes[oA.AimLaneID].pMid) >= 2 * this.AGVTurnRadius))
                                    {
                                        // 除非直接到目标 Lane， 否则必须垫 QCTP_PASS，且内侧垫内侧，外侧垫外侧
                                        if (this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X < this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X)
                                        {
                                            lTempLanes1 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_TP && u.pWork.X > this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X
                                                && u.pWork.X < this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X).OrderBy(u => u.pWork.X).ToList();
                                        }
                                        else
                                        {
                                            lTempLanes1 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_TP && u.pWork.X < this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X
                                                && u.pWork.X > this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X).OrderByDescending(u => u.pWork.X).ToList();
                                        }

                                        // 能直接到，不需要穿越作业线
                                        if (lTempLanes1.Count == 0)
                                        {
                                            lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                            lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                        }
                                        else
                                        {
                                            switch (this.oSimDataStore.dLanes[oA.CurrLaneID].LineID)
                                            {
                                                case 44:
                                                case 47:
                                                    lTempLanes1 = lTempLanes1.Where(u => u.LineID == this.oSimDataStore.dLanes[oA.CurrLaneID].LineID).ToList();

                                                    if (lTempLanes1.Count > 0 && lTempLanes1.All(u => u.eStatus == LaneStatus.IDLE)
                                                        && Math.Abs(this.oSimDataStore.dLanes[oA.AimLaneID].pMid.X - lTempLanes1.Last().pMid.X) < this.AGVTurnRadius)
                                                    {
                                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                                        lLanes.AddRange(lTempLanes1);
                                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                                    }
                                                    else
                                                    {
                                                        // 垫脚 QCTP 不够，或者垫脚 QCTP 导致的转弯半径不够
                                                        bRet = false;
                                                        Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of proper QCTP, Idle And Within AGVTurnRadius";
                                                    }
                                                    break;
                                                case 45:
                                                    lTempLanes1 = lTempLanes1.Where(u => u.LineID == 44).ToList();
                                                    if (lTempLanes1.Count > 0 && lTempLanes1.All(u => u.eStatus == LaneStatus.IDLE)
                                                        && Math.Abs(this.oSimDataStore.dLanes[oA.AimLaneID].pMid.X - lTempLanes1.Last().pMid.X) < this.AGVTurnRadius)
                                                    {
                                                        uTemp = this.GetLineIDForSwitchBetweenQCTPsInEvenRate(this.oSimDataStore.dLanes[oA.CurrLaneID], lTempLanes1[0], oA.oType.Length / 2);
                                                        if (uTemp > 0)
                                                        {
                                                            lAGVLineIDs.Add(uTemp);
                                                            lAGVLineIDs.Add(44);
                                                            lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                                            lLanes.AddRange(lTempLanes1);
                                                            lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                                        }
                                                        else
                                                        {
                                                            bRet = false;
                                                            Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                                + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Switching AGVLine";
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 垫脚 QCTP 不够
                                                        bRet = false;
                                                        Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of proper QCTP, Idle And Within AGVTurnRadius";
                                                    }
                                                    break;
                                                case 46:
                                                    lTempLanes1 = lTempLanes1.Where(u => u.LineID == 47).ToList();
                                                    if (lTempLanes1.Count > 0 && lTempLanes1.All(u => u.eStatus == LaneStatus.IDLE)
                                                        && Math.Abs(this.oSimDataStore.dLanes[oA.AimLaneID].pMid.X - lTempLanes1.Last().pMid.X) < this.AGVTurnRadius)
                                                    {
                                                        uTemp = this.GetLineIDForSwitchBetweenQCTPsInEvenRate(this.oSimDataStore.dLanes[oA.CurrLaneID], lTempLanes1[0], oA.oType.Length / 2);
                                                        if (uTemp > 0)
                                                        {
                                                            lAGVLineIDs.Add(uTemp);
                                                            lAGVLineIDs.Add(47);
                                                            lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                                            lLanes.AddRange(lTempLanes1);
                                                            lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                                        }
                                                        else
                                                        {
                                                            bRet = false;
                                                            Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                                + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Switching AGVLine";
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 垫脚 QCTP 不够
                                                        bRet = false;
                                                        Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of proper QCTP, Idle And Within AGVTurnRadius";
                                                    }
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        bRet = false;
                                        Flag = "Generate Route From QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : " + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString()
                                            + " Failed, For Ruote QCTP To QCPBOut Not Paired，Or From QCTP To QCPBInOut With Turn Radius Not Enough Or Blocked By Other QCTP";
                                    }
                                    break;
                                case AreaType.STS_TP:
                                    // From QCTP to QCTP
                                    // 不能跨岸边双黄线
                                    if ((this.oSimDataStore.dLanes[oA.CurrLaneID].LineID <= 45 && this.oSimDataStore.dLanes[oA.AimLaneID].LineID >= 46)
                                        || (this.oSimDataStore.dLanes[oA.CurrLaneID].LineID >= 46 && this.oSimDataStore.dLanes[oA.AimLaneID].LineID <= 45))
                                    {
                                        bRet = false;
                                        Flag = "Generate Route From QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : " + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString()
                                            + " Failed, For Route between QCTPs across working QCTP AGVLine is not Allowed";
                                    }
                                    // 不能去同一岸桥的 QCTP
                                    if (Math.Abs(this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X - this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X) < 0.1)
                                    {
                                        bRet = false;
                                        Flag = "Generate Route From QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : " + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString()
                                            + " Failed, For Route between QCTPs under a same QC is not Allowed";
                                    }
                                    if (bRet)
                                    {
                                        // 看看中间有没有夹着若干 QCTP
                                        lTempLanes1.Clear();
                                        if (this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X < this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X)
                                        {
                                            lTempLanes1 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_TP && u.pWork.X > this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X
                                                && u.pWork.X < this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X).OrderBy(u => u.pWork.X).ToList();
                                        }
                                        else
                                        {
                                            lTempLanes1 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_TP && u.pWork.X > this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X
                                                && u.pWork.X < this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X).OrderByDescending(u => u.pWork.X).ToList();
                                        }

                                        // 先确定 lLanes。如果夹了QCTP，需要依次把 QCTP_PASS 夹在中间。注意只能夹外侧的。
                                        if (lTempLanes1.Count > 0)
                                        {
                                            if (this.oSimDataStore.dLanes[oA.CurrLaneID].LineID <= 45)
                                                lTempLanes1 = lTempLanes1.Where(u => u.LineID == 44).ToList();
                                            else
                                                lTempLanes1 = lTempLanes1.Where(u => u.LineID == 47).ToList();
                                            if (lTempLanes1.All(u => u.eStatus == LaneStatus.IDLE))
                                                lTempLanes1.ForEach(u => lLanes.Add(u));
                                            else
                                            {
                                                bRet = false;
                                                Flag = "Generate Route From QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : " + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString()
                                                     + "Failed For No Enough Idle QCTP To Pass";
                                            }
                                        }
                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);

                                        // 如果有岸桥位置计划，要求所有 Lane 的进出方向一致。
                                        if (bRet && this.oSimDataStore.dQCPosPlans.Count > 0)
                                        {
                                            eSTSVisitDir = StatusEnums.STSVisitDir.Null;
                                            for (int i = 0; i < lLanes.Count; i++)
                                            {
                                                if (this.oSimDataStore.dQCPosPlans.ContainsKey(lLanes[i].CheNo)
                                                    && this.oSimDataStore.dHandleLinePlans.ContainsKey(this.oSimDataStore.dQCPosPlans[lLanes[i].CheNo].CurrWQ))
                                                {
                                                    if (eSTSVisitDir == StatusEnums.STSVisitDir.Null)
                                                        eSTSVisitDir = this.oSimDataStore.dHandleLinePlans[this.oSimDataStore.dQCPosPlans[lLanes[i].CheNo].CurrWQ].eSTSVisitDir;
                                                    else
                                                    {
                                                        if (eSTSVisitDir != this.oSimDataStore.dHandleLinePlans[this.oSimDataStore.dQCPosPlans[lLanes[i].CheNo].CurrWQ].eSTSVisitDir)
                                                        {
                                                            bRet = false;
                                                            Flag = "Generate Route From QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : " + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString()
                                                                + "Failed For Different QC Visit Direction";
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        // 补齐AGVLine
                                        if (bRet)
                                        {
                                            for (int i = 1; i < lLanes.Count; i++)
                                            {
                                                if (lLanes[i - 1].LineID != lLanes[i].LineID)
                                                {
                                                    uTemp = this.GetLineIDForSwitchBetweenQCTPsInEvenRate(lLanes[i - 1], lLanes[i], oA.oType.Length / 2);
                                                    if (uTemp == 0)
                                                    {
                                                        bRet = false;
                                                        Flag = "Generate Route From QCPB Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : " + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString()
                                                                + "Failed For No Switch AGVLine Found Between Lane: " + lLanes[i - 1].ID.ToString() + " And Lane: " + lLanes[i].ID.ToString();
                                                        break;
                                                    }
                                                    else
                                                    {
                                                        lAGVLineIDs.Add(uTemp);
                                                        lAGVLineIDs.Add(lLanes[i].LineID);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    break;
                            }
                            break;
                    }
                }
            }

            if (!bRet)
            {
                if (this.IsRouteLog)
                {
                    this.fs_Routing = new FileStream(System.Environment.CurrentDirectory + "\\PrintOut\\Routings.txt", FileMode.Append);
                    this.sw_Routing = new StreamWriter(this.fs_Routing, Encoding.Default);
                    this.sw_Routing.WriteLine(DateTime.Now.ToString() + " : " + Flag);
                    this.sw_Routing.Close();
                    this.fs_Routing.Close();
                }
            }
            else
            {
                if (this.IsRouteLog)
                {
                    this.fs_Routing = new FileStream(System.Environment.CurrentDirectory + "\\PrintOut\\Routings.txt", FileMode.Append);
                    this.sw_Routing = new StreamWriter(this.fs_Routing, Encoding.Default);
                    this.sw_Routing.WriteLine(DateTime.Now.ToString() + " : Route Generated for AGV: " + oA.ID.ToString() + ": From " + oA.CurrLaneID.ToString() + " To : " + oA.AimLaneID.ToString());
                    this.sw_Routing.Close();
                    this.fs_Routing.Close();
                }

                oPPTViewFrame = new ProjectPackageToViewFrame();
                foreach (Lane oL in lLanes)
                {
                    if (lLanes.IndexOf(oL) > 0)
                    {
                        oL.AGVNo = oA.ID;
                        if (lLanes.IndexOf(oL) < lLanes.Count - 1)
                            oL.eStatus = LaneStatus.PASSTHROUGH;
                        else
                            oL.eStatus = LaneStatus.RESERVED;
                        if (!oPPTViewFrame.lLanes.Contains(oL))
                            oPPTViewFrame.lLanes.Add(oL);
                    }
                }

                oAGVRoute = new AGVRoute();
                oAGVRoute.AGVID = oA.ID;
                oAGVRoute.lRouteLaneIDs = lLanes.Select(u => u.ID).ToList();
                oAGVRoute.CurrLaneSeq = 0;
                oAGVRoute.CurrClaimLength = oA.oType.Length;
                oA.NextLaneID = oAGVRoute.lRouteLaneIDs[1];
                oA.eMotionStatus = StatusEnums.MotionStatus.Moving;

                // 按照 AGVLine 给 AGV 写路径，留在 SimYardManager 的 dAGVRoutes 里面
                for (int i = 0; i < lAGVLineIDs.Count; i++)
                {
                    oRS = new RouteSegment();
                    oRS.AGVID = oA.ID;
                    oRS.ID = (uint)i;
                    oRS.AGVLineID = lAGVLineIDs[i];

                    // StartPoint. 第一 StartPoint 是 MidPoint 需要修正。
                    if (i == 0)
                    {
                        oRS.StartPoint.X = oA.MidPoint.X;
                        oRS.StartPoint.Y = oA.MidPoint.Y;
                    }
                    else
                    {
                        if (this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].eFlowDir == CoordinateDirection.X_POSITIVE
                            || this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].eFlowDir == CoordinateDirection.X_NEGATIVE)
                        {
                            oRS.StartPoint.X = Math.Round(this.oSimDataStore.dAGVLines[lAGVLineIDs[i - 1]].FeaturePosition, this.DecimalNum);
                            oRS.StartPoint.Y = Math.Round(this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].FeaturePosition, this.DecimalNum);
                            oRS.StartLinePos = Math.Round(oRS.StartPoint.X, this.DecimalNum);
                        }
                        else
                        {
                            oRS.StartPoint.X = Math.Round(this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].FeaturePosition, this.DecimalNum);
                            oRS.StartPoint.Y = Math.Round(this.oSimDataStore.dAGVLines[lAGVLineIDs[i - 1]].FeaturePosition, this.DecimalNum);
                            oRS.StartLinePos = Math.Round(oRS.StartPoint.Y, this.DecimalNum);
                        }
                    }

                    // EndPoint。最后 EndPoint 是 WorkPoint 需要修正。
                    if (i == lAGVLineIDs.Count - 1)
                    {
                        oRS.EndPoint.X = this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X;
                        oRS.EndPoint.Y = this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y;
                    }
                    else
                    {
                        if (this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].eFlowDir == CoordinateDirection.X_POSITIVE
                            || this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].eFlowDir == CoordinateDirection.X_NEGATIVE)
                        {
                            oRS.EndPoint.X = Math.Round(this.oSimDataStore.dAGVLines[lAGVLineIDs[i + 1]].FeaturePosition, this.DecimalNum);
                            oRS.EndPoint.Y = Math.Round(this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].FeaturePosition, this.DecimalNum);
                            oRS.EndLinePos = Math.Round(oRS.EndPoint.X);
                        }
                        else
                        {
                            oRS.EndPoint.X = Math.Round(this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].FeaturePosition, this.DecimalNum);
                            oRS.EndPoint.Y = Math.Round(this.oSimDataStore.dAGVLines[lAGVLineIDs[i + 1]].FeaturePosition, this.DecimalNum);
                            oRS.EndLinePos = Math.Round(oRS.EndPoint.Y);
                        }
                    }

                    // RouteSegDir 赋值
                    if (this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].eFlowDir == CoordinateDirection.X_POSITIVE
                        || this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].eFlowDir == CoordinateDirection.X_NEGATIVE)
                    {
                        if (oRS.EndPoint.X > oRS.StartPoint.X) 
                            oRS.eCD = CHE_Direction.East;
                        else 
                            oRS.eCD = CHE_Direction.West;
                    }
                    else
                    {
                        if (oRS.EndPoint.Y > oRS.StartPoint.Y)
                            oRS.eCD = CHE_Direction.South;
                        else
                            oRS.eCD = CHE_Direction.North;
                    }

                    // 修正 StartPoint and EndPoint, 并决定各 RouteSegment 的起止 RoutePos.
                    if (i == 0)
                    {
                        switch (oRS.eCD)
                        {
                            case CHE_Direction.East:
                                oRS.StartPoint.X = Math.Round(oRS.StartPoint.X - oA.oType.Length / 2, this.DecimalNum);
                                oRS.StartLinePos = Math.Round(oRS.StartPoint.X, this.DecimalNum);
                                break;
                            case CHE_Direction.West:
                                oRS.StartPoint.X = Math.Round(oRS.StartPoint.X + oA.oType.Length / 2, this.DecimalNum);
                                oRS.StartLinePos = Math.Round(oRS.StartPoint.X, this.DecimalNum);
                                break;
                            case CHE_Direction.South:
                                oRS.StartPoint.Y = Math.Round(oRS.StartPoint.Y - oA.oType.Length / 2, this.DecimalNum);
                                oRS.StartLinePos = Math.Round(oRS.StartPoint.Y, this.DecimalNum);
                                break;
                            case CHE_Direction.North:
                                oRS.StartPoint.Y = Math.Round(oRS.StartPoint.Y + oA.oType.Length / 2, this.DecimalNum);
                                oRS.StartLinePos = Math.Round(oRS.StartPoint.Y, this.DecimalNum);
                                break;
                        }
                        oRS.StartRoutePos = 0;
                    }
                    else
                        oRS.StartRoutePos = oAGVRoute.lRouteSegments[oAGVRoute.lRouteSegments.Count - 1].EndRoutePos;

                    if (i == lAGVLineIDs.Count - 1)
                    {
                        switch (oRS.eCD)
                        {
                            case CHE_Direction.East:
                                oRS.EndPoint.X = Math.Round(oRS.EndPoint.X + oA.oType.Length / 2, this.DecimalNum);
                                oRS.EndLinePos = Math.Round(oRS.EndPoint.X, this.DecimalNum);
                                break;
                            case CHE_Direction.West:
                                oRS.EndPoint.X = Math.Round(oRS.EndPoint.X - oA.oType.Length / 2, this.DecimalNum);
                                oRS.EndLinePos = Math.Round(oRS.EndPoint.X, this.DecimalNum);
                                break;
                            case CHE_Direction.South:
                                oRS.EndPoint.Y = Math.Round(oRS.EndPoint.Y + oA.oType.Length / 2, this.DecimalNum);
                                oRS.EndLinePos = Math.Round(oRS.EndPoint.Y, this.DecimalNum);
                                break;
                            case CHE_Direction.North:
                                oRS.EndPoint.Y = Math.Round(oRS.EndPoint.Y - oA.oType.Length / 2, this.DecimalNum);
                                oRS.EndLinePos = Math.Round(oRS.EndPoint.Y, this.DecimalNum);
                                break;
                        }

                    }
                    oRS.EndRoutePos = Math.Round(oRS.StartRoutePos + Point.Subtract(oRS.StartPoint, oRS.EndPoint).Length, this.DecimalNum);

                    // 标记有 Route 经过的磁钉
                    // 加入相关的 TPIDInfo 进 AGVRoute.lTPInfoEnRoutes ( LaneID 相关参数之后会补 )
                    switch (oRS.eCD)
                    {
                        case CHE_Direction.East:
                            lTempTPs = this.dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oRS.AGVLineID
                                && u.LogicPosX >= oRS.StartLinePos && u.LogicPosX <= oRS.EndLinePos).OrderBy(u => u.LogicPosX).ToList();
                            break;
                        case CHE_Direction.West:
                            lTempTPs = this.dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oRS.AGVLineID
                                && u.LogicPosX <= oRS.StartLinePos && u.LogicPosX >= oRS.EndLinePos).OrderByDescending(u => u.LogicPosX).ToList();
                            break;
                        case CHE_Direction.South:
                            lTempTPs = this.dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oRS.AGVLineID
                                && u.LogicPosY >= oRS.StartLinePos && u.LogicPosY <= oRS.EndLinePos).OrderBy(u => u.LogicPosY).ToList();
                            break;
                        case CHE_Direction.North:
                            lTempTPs = this.dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oRS.AGVLineID
                                && u.LogicPosY <= oRS.StartLinePos && u.LogicPosY >= oRS.EndLinePos).OrderByDescending(u => u.LogicPosY).ToList();
                            break;
                    }
                    if (lTempTPs.Count > 0)
                    {
                        oRS.StartTPID = lTempTPs[0].ID;
                        oRS.EndTPID = lTempTPs.Last().ID;
                        foreach (SimTransponder oTP in lTempTPs)
                        {
                            if (!oPPTViewFrame.lTPs.Contains(oTP))
                                oPPTViewFrame.lTPs.Add(oTP);
                            if (!oTP.dRouteTPDivisions.ContainsKey(oA.ID))
                                oTP.dRouteTPDivisions.Add(oA.ID, StatusEnums.RouteTPDivision.Detect);
                            if (!oAGVRoute.lRouteTPInfos.Exists(u => u.TPID == oTP.ID))
                                oAGVRoute.lRouteTPInfos.Add(new TPInfoEnRoute(oTP.ID, oRS.eCD, oRS.StartRoutePos, oRS.StartLinePos, oTP.LogicPosX, oTP.LogicPosY));
                        }
                    }
                    oAGVRoute.lRouteSegments.Add(oRS);
                }

                // 整理 oAGVRoute.lTPinfosEnRoute，标记 EnterLaneID 和 ExitLaneID
                lTPInfoERs = oAGVRoute.lRouteTPInfos.Where(u => this.dQCAndEssentialTPs[u.TPID].LaneID > 0).OrderBy(u => u.RoutePos).ToList();
                lTempTPInfos = new List<TPInfoEnRoute>(lTPInfoERs);
                if (lTPInfoERs.Count > 0)
                {
                    if (lTPInfoERs.Count == 1 ||
                        this.dQCAndEssentialTPs[lTPInfoERs[0].TPID].LaneID != this.dQCAndEssentialTPs[lTPInfoERs[1].TPID].LaneID)
                    {
                        lTPInfoERs[0].ExitLaneID = this.dQCAndEssentialTPs[lTPInfoERs[0].TPID].LaneID;
                        lTPInfoERs.RemoveAt(0);
                    }
                }
                if (lTPInfoERs.Count > 0)
                {
                    if (lTPInfoERs.Count == 1 ||
                        this.dQCAndEssentialTPs[lTPInfoERs[lTPInfoERs.Count - 1].TPID].LaneID != this.dQCAndEssentialTPs[lTPInfoERs[lTPInfoERs.Count - 2].TPID].LaneID)
                    {
                        lTPInfoERs[lTPInfoERs.Count - 1].EnterLaneID = this.dQCAndEssentialTPs[lTPInfoERs[lTPInfoERs.Count - 1].TPID].LaneID;
                        lTPInfoERs.RemoveAt(lTPInfoERs.Count - 1);
                    }
                }
                while (lTPInfoERs.Count > 0)
                {
                    if (lTPInfoERs.Count == 1 || this.dQCAndEssentialTPs[lTPInfoERs[0].TPID].LaneID != this.dQCAndEssentialTPs[lTPInfoERs[1].TPID].LaneID)
                    {
                        // 应急处理。不应该发生这种情况
                        Logger.Simulate.Error("SimTrafficController: Sth Wrong In Routing With Two Neighbouring TPInfo To Different Lanes");
                        lTPInfoERs.RemoveAt(0);
                    }
                    else
                    {
                        lTPInfoERs[0].EnterLaneID = this.dQCAndEssentialTPs[lTPInfoERs[0].TPID].LaneID;
                        lTPInfoERs[1].ExitLaneID = this.dQCAndEssentialTPs[lTPInfoERs[1].TPID].LaneID;
                        lTPInfoERs.RemoveRange(0, 2);
                    }
                }

                oAGVRoute.lRouteTPInfos.OrderBy(u => u.RoutePos);
                oAGVRoute.TotalLength = oAGVRoute.lRouteSegments[oAGVRoute.lRouteSegments.Count - 1].EndRoutePos;
                this.dAGVRoutes.Add(oAGVRoute.AGVID, oAGVRoute);
                
                // 生成路径之后 dAGVRouteAngle 要更新一下
                switch (oAGVRoute.lRouteSegments[0].eCD)
                {
                    case CHE_Direction.East:
                        this.dAGVAnglesEnRoute[oA.ID] = 0;
                        break;
                    case CHE_Direction.West:
                        this.dAGVAnglesEnRoute[oA.ID] = 180;
                        break;
                    case CHE_Direction.South:
                        this.dAGVAnglesEnRoute[oA.ID] = 90;
                        break;
                    case CHE_Direction.North:
                        this.dAGVAnglesEnRoute[oA.ID] = 270;
                        break;
                }

                // 起始 TP 占用
                lTempTPs = this.dQCAndEssentialTPs.Values.Where(u =>
                    (u.HorizontalLineID == oAGVRoute.lRouteSegments[0].AGVLineID && u.LogicPosX > oA.MidPoint.X - oA.oType.Length / 2 && u.LogicPosX < oA.MidPoint.X + oA.oType.Length / 2)
                    || (u.VerticalLineID == oAGVRoute.lRouteSegments[0].AGVLineID && u.LogicPosY > oA.MidPoint.Y - oA.oType.Length / 2 && u.LogicPosY < oA.MidPoint.Y + oA.oType.Length / 2)).ToList();

                foreach (SimTransponder oTP in lTempTPs)
                {
                    oTP.dRouteTPDivisions[oA.ID] = StatusEnums.RouteTPDivision.Claim;
                    this.dAGVRoutes[oA.ID].lRouteTPInfos.First(v => v.TPID == oTP.ID).eRouteTPDivision = StatusEnums.RouteTPDivision.Claim;
                    this.dAGVRoutes[oA.ID].lRouteTPInfos.First(v => v.TPID == oTP.ID).IsUnSurpassable = true;
                }

                if (this.ProjectToViewFrameEvent != null)
                    this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                    {
                        eProjectType = StatusEnums.ProjectType.Renew,
                        oPPTViewFrame = oPPTViewFrame
                    });
            }

            return bRet;
        }

        /// <summary>
        /// 以等概率形式返回从 QCTP : Lane1 到 QCTP : Lane2 之间，能用于中途切换的 AGVLineID
        /// </summary>
        /// <param name="Lane1">起始Lane</param>
        /// <param name="Lane2">终止Lane</param>
        /// <param name="HalfAGVLength">AGV半车长，预留斜行距离</param>
        /// <returns>能找到返回车道号，不能找到返回0</returns>
        private uint GetLineIDForSwitchBetweenQCTPsInEvenRate(Lane Lane1, Lane Lane2, double HalfAGVLength)
        {
            uint SwitchLineID = 0;
            double dMin;
            double dMax;
            Random rand = new Random();
            List<AGVLine> lTempAGVLines;
            List<double> lStarts = new List<double>();
            List<double> lEnds = new List<double>();

            // 只有 QCTP 之间需要这样
            if (Lane1.eType != AreaType.STS_TP || Lane2.eType != AreaType.STS_TP)
                return SwitchLineID;

            if (Math.Abs(Lane1.pMid.X - Lane2.pMid.X) < 0.1) 
                return SwitchLineID;

            if (Lane1.pMid.X > Lane2.pMid.X)
            {
                dMin = Math.Max(this.dQCAndEssentialTPs[Lane2.TPIDEnd].LogicPosX, this.dQCAndEssentialTPs[Lane2.TPIDStart].LogicPosX) + HalfAGVLength;
                dMax = Math.Min(this.dQCAndEssentialTPs[Lane1.TPIDEnd].LogicPosX, this.dQCAndEssentialTPs[Lane1.TPIDStart].LogicPosX) - HalfAGVLength;
            }
            else
            {
                dMin = Math.Max(this.dQCAndEssentialTPs[Lane1.TPIDEnd].LogicPosX, this.dQCAndEssentialTPs[Lane1.TPIDStart].LogicPosX) + HalfAGVLength;
                dMax = Math.Min(this.dQCAndEssentialTPs[Lane2.TPIDEnd].LogicPosX, this.dQCAndEssentialTPs[Lane2.TPIDStart].LogicPosX) - HalfAGVLength;
            }

            // 没有区间，返回
            if (dMin >= dMax) 
                return SwitchLineID;

            lTempAGVLines = this.dEssentialAGVLines.Values.Where(u => u.FeaturePosition > dMin && u.FeaturePosition < dMax
                && (u.eFlowDir == CoordinateDirection.Y_NEGATIVE || u.eFlowDir == CoordinateDirection.Y_POSITIVE)).ToList();

            // 没有基本款，返回
            if (lTempAGVLines.Count == 0) 
                return SwitchLineID;

            List<Lane> lQCTPs = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_TP && u.pWork.X > dMin && u.pWork.X < dMax).ToList();

            if (lQCTPs.Count > 0)
                lTempAGVLines.RemoveAll(u => lQCTPs.Exists(v => this.oSimDataStore.dTransponders[v.TPIDStart].LogicPosX <= u.FeaturePosition
                    && this.oSimDataStore.dTransponders[v.TPIDEnd].LogicPosX >= u.FeaturePosition));
            
            // 没有滤后款，返回
            if (lTempAGVLines.Count == 0)
                return SwitchLineID;

            SwitchLineID = lTempAGVLines[rand.Next(0, lTempAGVLines.Count)].ID;

            return SwitchLineID;
        }

        /// <summary>
        /// 返回满足距离和方向要求的U型弯转接用单向 AGVLineID
        /// </summary>
        /// <param name="eCD">AGVLine的方向，CHE_Direction</param>
        /// <param name="OriDestIntv">U弯前后的直行线间距离</param>
        /// <param name="ZeroFeaturePos">最早入弯点到连接用 AGVLine 的距离</param>
        /// <returns>AGVLineID，找不到返回0</returns>
        private uint  GetOneCHEDirLineIDForUTurn(CHE_Direction eCD, double OriDestIntv, double ZeroFeaturePos1, double ZeroFeaturePos2)
        {
            uint iRet = 0;
            Random rand = new Random();
            List<AGVLine> lAGVLines;
            List<uint> lAGVLineIDs;
            List<double> datasave = new List<double>();
            Solute SJK = new Solute();
            Getpotential POT = new Getpotential();

            // 硬条件：前后直线间距必须大于转弯直径
            if (OriDestIntv < 2 * this.AGVTurnRadius)
                return iRet;

            lAGVLines = this.oSimDataStore.dAGVLines.Values.Where(u => u.eMoveDir == eCD).ToList();

            // 软条件：基点到转弯衔接线的距离不小于一半的转弯半径
            lAGVLineIDs = lAGVLines.Where(u => Math.Abs(u.FeaturePosition - ZeroFeaturePos1) + Math.Abs(u.FeaturePosition - ZeroFeaturePos2) >= this.AGVTurnRadius).Select(u => u.ID).ToList();

            lAGVLineIDs.Sort((u, v) => (Math.Abs(this.oSimDataStore.dAGVLines[u].FeaturePosition - ZeroFeaturePos1) + Math.Abs(this.oSimDataStore.dAGVLines[u].FeaturePosition - ZeroFeaturePos2))
                .CompareTo((Math.Abs(this.oSimDataStore.dAGVLines[v].FeaturePosition - ZeroFeaturePos1) + Math.Abs(this.oSimDataStore.dAGVLines[u].FeaturePosition - ZeroFeaturePos2))));

            if (lAGVLineIDs.Count > 0) 
                iRet = lAGVLineIDs[rand.Next(lAGVLineIDs.Count)];

            return iRet;
        }

        //相对于上面的函数加入了一些改动
        private uint GetOneCHEDirLineIDForUTurnwithpotential(CHE_Direction eCD, double OriDestIntv, double ZeroFeaturePos1, double ZeroFeaturePos2,double position)
        {
            uint iRet = 0;
            Random rand = new Random();
            List<AGVLine> lAGVLines;
            List<uint> lAGVLineIDs;
            Dictionary<uint, double> odatasave = new Dictionary<uint, double>();
            Solute SJK = new Solute();
            Getpotential POT = new Getpotential();


            // 硬条件：前后直线间距必须大于转弯直径
            if (OriDestIntv < 2 * this.AGVTurnRadius)
                return iRet;

            lAGVLines = this.oSimDataStore.dAGVLines.Values.Where(u => u.eMoveDir == eCD).ToList();

            // 软条件：基点到转弯衔接线的距离不小于一半的转弯半径
            lAGVLineIDs = lAGVLines.Where(u => Math.Abs(u.FeaturePosition - ZeroFeaturePos1) + Math.Abs(u.FeaturePosition - ZeroFeaturePos2) >= this.AGVTurnRadius).Select(u => u.ID).ToList();

            lAGVLineIDs.Sort((u, v) => (Math.Abs(this.oSimDataStore.dAGVLines[u].FeaturePosition - ZeroFeaturePos1) + Math.Abs(this.oSimDataStore.dAGVLines[u].FeaturePosition - ZeroFeaturePos2))
                .CompareTo((Math.Abs(this.oSimDataStore.dAGVLines[v].FeaturePosition - ZeroFeaturePos1) + Math.Abs(this.oSimDataStore.dAGVLines[u].FeaturePosition - ZeroFeaturePos2))));

            if (lAGVLineIDs.Count() != 0)
            {
                foreach (uint lAGVLineID in lAGVLineIDs)
                {
                    int lAGVLineid = (int)lAGVLineID;
                    List<int> AGVIDs = new List<int>();
                    AGVIDs = SJK.ReadLine(lAGVLineid, "AGVlineid", "agvid", "agv_agvline");
                    /*foreach (int oagvid in this.oSimDataStore.dfindlineforagv.Keys)
                    {
                        if (this.oSimDataStore.dfindlineforagv[oagvid] == lAGVLineid)
                        {
                            AGVIDs.Add(oagvid);
                        }
                    }*/
                    
                    double lpotential = 0;

                    if (lAGVLineIDs.Count() == 3)
                    {
                        lpotential += this.dStorecount[lAGVLineID];
                    }
                    if (AGVIDs.Count() != 0)
                    {
                        if (eCD == CHE_Direction.South)
                        {
                            foreach (int AGVID in AGVIDs)
                            {
                                double distance = oSimDataStore.dAGVs[(uint)AGVID].MidPoint.Y - position;
                                double speed = oSimDataStore.dAGVs[(uint)AGVID].CurrVelo;
                                lpotential += POT.chooselinepotential(distance, speed);
                            }
                        }
                        else if (eCD == CHE_Direction.North)
                        {
                            foreach (int AGVID in AGVIDs)
                            {
                                double distance = position - oSimDataStore.dAGVs[(uint)AGVID].MidPoint.Y;
                                double speed = oSimDataStore.dAGVs[(uint)AGVID].CurrVelo;
                                lpotential += POT.chooselinepotential(distance, speed);
                            }
                        }
                        else if (eCD == CHE_Direction.East)
                        {
                            foreach (int AGVID in AGVIDs)
                            {
                                double distance = oSimDataStore.dAGVs[(uint)AGVID].MidPoint.X - position;
                                double speed = oSimDataStore.dAGVs[(uint)AGVID].CurrVelo;
                                lpotential += POT.chooselinepotential(distance, speed);
                            }
                        }
                        else
                        {
                            foreach (int AGVID in AGVIDs)
                            {
                                double distance = position - oSimDataStore.dAGVs[(uint)AGVID].MidPoint.X;
                                double speed = oSimDataStore.dAGVs[(uint)AGVID].CurrVelo;
                                lpotential += POT.chooselinepotential(distance, speed);
                            }
                        }
                    }

                    odatasave.Add(lAGVLineID, lpotential);
                }

                List<uint> minindic = new List<uint>();
                foreach (uint choice in odatasave.Keys)
                {
                    if (odatasave[choice] == odatasave.Values.Min())
                    {
                        minindic.Add(choice);
                    }
                }
                if (minindic.Count() == 1)
                {
                    iRet = minindic[0];
                }
                else
                {
                    iRet = minindic[rand.Next(minindic.Count())];
                }


                this.dStorecount[iRet]++;
            }

            return iRet;
        }

        /// <summary>
        /// 返回满足方向要求的转接用单行 AGVLineID
        /// </summary>
        /// <param name="eCD">道路方向</param>
        /// <returns>车道线号</returns>
        private uint GetOneCHEDirLineIDInEvenRate(CHE_Direction eCD)
        {
            uint iRet = 0;
            Random rand = new Random();
            List<uint> lAGVLineIDs;

            lAGVLineIDs = this.oSimDataStore.dAGVLines.Values.Where(u => u.eMoveDir == eCD).Select(u => u.ID).ToList();

            if (lAGVLineIDs.Count > 0) 
                iRet = lAGVLineIDs[rand.Next(0, lAGVLineIDs.Count)];

            return iRet;
        }

        //加入了势场
        private uint GetOneCHEDirLineIDInEvenRatewithpotential(CHE_Direction eCD,double position)
        {
            uint iRet = 0;
            Random rand = new Random();
            List<uint> lAGVLineIDs;
            Dictionary<uint, double> odatasave = new Dictionary<uint, double>();
            Solute SJK = new Solute();
            Getpotential POT = new Getpotential();

            lAGVLineIDs = this.oSimDataStore.dAGVLines.Values.Where(u => u.eMoveDir == eCD).Select(u => u.ID).ToList();

            if (lAGVLineIDs.Count() == 0)
            {

            }

            if (lAGVLineIDs.Count() != 0)
            {
                foreach (uint lAGVLineID in lAGVLineIDs)
                {
                    int lAGVLineid = (int)lAGVLineID;
                    List<int> AGVIDs = new List<int>();
                    AGVIDs = SJK.ReadLine(lAGVLineid, "AGVlineid", "agvid", "agv_agvline");
                    /*foreach (int oagvid in this.oSimDataStore.dfindlineforagv.Keys)
                    {
                        if (this.oSimDataStore.dfindlineforagv[oagvid] == lAGVLineid)
                        {
                            AGVIDs.Add(oagvid);
                        }
                    }*/
                    double lpotential = 0;
                    if (lAGVLineIDs.Count() == 3)
                    {
                        lpotential += this.dStorecount[lAGVLineID];
                    }

                    if (AGVIDs.Count() != 0)
                    {
                        if (eCD == CHE_Direction.South)
                        {
                            foreach (int AGVID in AGVIDs)
                            {
                                double distance = oSimDataStore.dAGVs[(uint)AGVID].MidPoint.Y - position;
                                double speed = oSimDataStore.dAGVs[(uint)AGVID].CurrVelo;
                                lpotential += POT.chooselinepotential(distance, speed);
                            }
                        }
                        else if (eCD == CHE_Direction.North)
                        {
                            foreach (int AGVID in AGVIDs)
                            {
                                double distance = position - oSimDataStore.dAGVs[(uint)AGVID].MidPoint.Y;
                                double speed = oSimDataStore.dAGVs[(uint)AGVID].CurrVelo;
                                lpotential += POT.chooselinepotential(distance, speed);
                            }
                        }
                        else if (eCD == CHE_Direction.East)
                        {
                            foreach (int AGVID in AGVIDs)
                            {
                                double distance = oSimDataStore.dAGVs[(uint)AGVID].MidPoint.X - position;
                                double speed = oSimDataStore.dAGVs[(uint)AGVID].CurrVelo;                               
                                lpotential += POT.chooselinepotential(distance, speed);
                            }
                        }
                        else
                        {
                            foreach (int AGVID in AGVIDs)
                            {
                                double distance = position - oSimDataStore.dAGVs[(uint)AGVID].MidPoint.X;
                                double speed = oSimDataStore.dAGVs[(uint)AGVID].CurrVelo;
                                lpotential += POT.chooselinepotential(distance, speed);
                            }
                        }
                    }

                    odatasave.Add(lAGVLineID, lpotential);
                }

                List<uint> minindic = new List<uint>();
                foreach (uint choice in odatasave.Keys)
                {
                    if (odatasave[choice] == odatasave.Values.Min())
                    {
                        minindic.Add(choice);
                    }
                }
                if (minindic.Count() == 1)
                {
                    iRet = minindic[0];
                }
                else
                {
                    iRet = minindic[rand.Next(minindic.Count())];
                }

                this.dStorecount[iRet]++;
            }

            return iRet;
        }
        // 计算曼哈顿距离
        private double GetManhattanDistance(double X1, double Y1, double X2, double Y2)
        {
            double dRet;

            dRet = Math.Round(Math.Abs(X1 - X2) + Math.Abs(Y1 - Y2), this.DecimalNum);

            return dRet;
        }

        // 计算曼哈顿距离
        private double GetManhattanDistance(Point p1, Point p2)
        {
            double dRet;

            dRet = Math.Round(Math.Abs(p1.X - p2.X) + Math.Abs(p1.Y - p2.Y), this.DecimalNum);

            return dRet;
        }

        /// <summary>
        /// 重置所有 AGV 路径。测试用。注意与AGV配合
        /// </summary>
        private void ResetAGVRoutes()
        {
            this.dAGVRoutes.Clear();
        }

        #endregion


        #region AGV运动模拟

        /// <summary>
        /// 按照时间步长移动，返回变动后的磁钉、AGV和车道对象列表
        /// </summary>
        /// <param name="TimeLength">步长，单位秒</param>
        /// <param name="oPPTViewFrame">ViewFrame投射单元</param>
        public bool MoveAGVsInStep(double TimeLength)
        {
            double MaxReclaimLength, ActVeloStepEnd, ActMoveLength;
            List<Lane> lTempLanes = new List<Lane>();
            List<AGV_STATUS> lTempAGVStatus = new List<AGV_STATUS>();
            List<SimTransponder> lTempTPs = new List<SimTransponder>();
            List<AGV> lTempAGVs = new List<AGV>();

            this.RenewMovingAGVsAndSeqs(TimeLength);
            

            if (this.lMovingAGVs.Count == 0)
                return true;

            for (int i = 0; i < this.lMovingAGVs.Count; i++)
            {
                if (this.IsDeadLockDetectInputLog)
                {
                    int ThisCSVNum = (int)(this.TPReclaimTime / this.CSVReserveNumInOneRecord);
                    if (ThisCSVNum > this.CurrCSVNum)
                    {
                        this.CurrCSVNum = ThisCSVNum;
                        this.fs_AgvResvRelease = new FileStream(System.Environment.CurrentDirectory + "\\PrintOut\\TPActions\\TPResvRelease" + (this.CurrCSVNum).ToString().PadLeft(4, '0') + ".csv", FileMode.Create);
                        this.sw_AgvResvRelease = new StreamWriter(fs_AgvResvRelease, Encoding.Default);
                        this.sw_AgvResvRelease.WriteLine("AGV,Action,TPID,SolveSeq,SolveResult");
                        if (this.CurrCSVNum - this.CSVRecordNum >= 0
                            && File.Exists(System.Environment.CurrentDirectory + "\\PrintOut\\TPActions\\TPResvRelease" + (this.CurrCSVNum - this.CSVRecordNum).ToString().PadLeft(4, '0') + ".csv"))
                            File.Delete(System.Environment.CurrentDirectory + "\\PrintOut\\TPActions\\TPResvRelease" + (this.CurrCSVNum - this.CSVRecordNum).ToString().PadLeft(4, '0') + ".csv");
                    }
                    else
                    {
                        this.fs_AgvResvRelease = new FileStream(System.Environment.CurrentDirectory + "\\PrintOut\\TPActions\\TPResvRelease" + (this.CurrCSVNum).ToString().PadLeft(4, '0') + ".csv", FileMode.Append);
                        this.sw_AgvResvRelease = new StreamWriter(fs_AgvResvRelease, Encoding.Default);
                    }
                }

                // 本步需要考虑的最大距离。
                this.GetMaxReclaimLength(this.lMovingAGVs[i], TimeLength, out MaxReclaimLength);

                // 在最大范围内预约磁钉，返回本次实际预约到的最远位置
                // 返回磁钉状态的变化，汇总后投影
                if (!this.ClaimTransponder(this.lMovingAGVs[i], MaxReclaimLength, ref lTempTPs))
                {
                    Logger.Simulate.Error("Traffic Controller: Sth Wrong When AGV: " + this.lMovingAGVs[i].ID.ToString() 
                        + " Reclaiming Transponder With TP Reclaim Time: " + this.TPReclaimTime.ToString());
                    return false;
                }

                // AGV 步末速度和本步移动距离决定，考虑道路总长、加速度、弯道限速和前车安全距离
                this.AmendMaxVeloStepEnd(this.lMovingAGVs[i], TimeLength, out ActVeloStepEnd, out ActMoveLength);

                // 在移动范围内处理相关事件，可能触发：到达事件、进入事件、离开事件
                // 返回 AGV 状态的变化，汇总后投影
                if (!this.RenewAGVStatus(this.lMovingAGVs[i], ActVeloStepEnd, ActMoveLength, ref lTempTPs, ref lTempLanes, ref lTempAGVs, ref lTempAGVStatus))
                {
                    Logger.Simulate.Error("Traffic Controller: Sth Wrong When AGV: " + this.lMovingAGVs[i].ID.ToString()
                        + " Renewing Status With TP Reclaim Time: " + this.TPReclaimTime.ToString());
                    return false;
                }

                if (this.IsDeadLockDetectInputLog)
                {
                    this.sw_AgvResvRelease.Close();
                    this.fs_AgvResvRelease.Close();
                }
            }

            this.SearchForAGVlineChange();

            // 投影
            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTViewFrame = new ProjectPackageToViewFrame()
                    {
                        lTPs = lTempTPs,
                        lLanes = lTempLanes,
                        lAGVs = lTempAGVs
                    }
                });
            this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTInfoFrame = new ProjectPackageToInfoFrame()
                    {
                        lAGVStatuses = lTempAGVStatus
                    }
                });

            if (this.CheckIfAllStopped())
            {
                this.DeadLockExpection.ThrowTime++;
                Logger.Simulate.Info("DeadLocked At Cycle Detect Time " + this.DeadlockDetectTime.ToString());
            }

            return true;
        }

        /// <summary>
        /// 本步需要考虑的最大距离。以车头为基准，不考虑任何安全距离和道路长度限制。
        /// </summary>
        /// <param name="oA">AGV 对象</param>
        /// <param name="TimeLength">时间步长，单位毫秒</param>
        /// <returns>返回本步需要向前考虑的距离</returns>
        private bool GetMaxReclaimLength(AGV oA, double TimeLength, out double MaxReclaimLength)
        {
            double MaxVeloVeh, MaxVeloStepEnd;

            SearchForLineIDAndPosByCoor(oA.MidPoint.X, oA.MidPoint.Y, out oA.CurrAGVLineID, out oA.Deviation);
            if (oA.CurrAGVLineID == 0)
            {
                oA.CurrAGVLineID = this.oSimDataStore.dLanes[oA.CurrLaneID].LineID;
                oA.Deviation = 0;
            }

            // 双 AOLS 一定在转弯，否则以车身终点到 RS 起点/终点的距离来判断是否转弯
            MaxVeloVeh = this.GetMaxVeloVeh(oA);
            MaxVeloStepEnd = Math.Min(oA.CurrVelo + oA.oType.Acceleration * TimeLength, MaxVeloVeh);

            // 本时段全力行使，车头到停止为止经过的距离。
            MaxReclaimLength = (oA.CurrVelo + MaxVeloStepEnd) / 2 * TimeLength + (MaxVeloStepEnd * MaxVeloStepEnd) / 2 * oA.oType.Acceleration;

            MaxReclaimLength = Math.Round(MaxReclaimLength, this.DecimalNum);

            if (MaxReclaimLength <= this.MaxReclaimLengthRecommended)
                MaxReclaimLength = this.MaxReclaimLengthRecommended;

            return true;
        }

        /// <summary>
        /// 返回速度上限
        /// </summary>
        /// <param name="oA">AGV实体</param>
        /// <returns>速度上限</returns>
        private double GetMaxVeloVeh(AGV oA)
        {
            bool IsTurning, IsHead, IsTail;
            double MaxVeloVeh;
            RouteSegment oRS;

            // 双 AOLS 一定在转弯，否则以车身终点到 RS 起点/终点的距离来判断是否转弯
            if (this.lAGVOccupyLineSegs.Where(u => u.AGVID == oA.ID).ToList().Count > 1)
                IsTurning = true;
            else
            {
                IsTurning = false;
                oAOLS = this.lAGVOccupyLineSegs.First(u => u.AGVID == oA.ID);
                oRS = this.dAGVRoutes[oA.ID].lRouteSegments.First(u => u.AGVLineID == oAOLS.AGVLineID 
                    && Math.Min(u.StartLinePos, u.EndLinePos) <= oAOLS.StartPos 
                    && Math.Max(u.StartLinePos, u.EndLinePos) >= oAOLS.EndPos);
                if (this.dAGVRoutes[oA.ID].lRouteSegments.IndexOf(oRS) == 0)
                    IsHead = true;
                else
                    IsHead = false;
                if (this.dAGVRoutes[oA.ID].lRouteSegments.IndexOf(oRS) == this.dAGVRoutes[oA.ID].lRouteSegments.Count - 1)
                    IsTail = true;
                else
                    IsTail = false;
                if (!IsHead && Math.Abs(oRS.StartLinePos - (oAOLS.StartPos + oAOLS.EndPos) / 2) <= this.AGVTurnRadius)
                    IsTurning = true;
                if (!IsTail && Math.Abs(oRS.EndLinePos - (oAOLS.StartPos + oAOLS.EndPos) / 2) <= this.AGVTurnRadius)
                    IsTurning = true;
            }

            if (IsTurning)
            {
                MaxVeloVeh = oA.oType.VeloTurnUpper;
                this.dAGVRoutes[oA.ID].IsChanged = false;
            }
            else
            {
                if (oA.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                    MaxVeloVeh = oA.oType.VeloEmptyUpper;
                else
                    MaxVeloVeh = oA.oType.VeloFullUpper;
            }

            return MaxVeloVeh;
        }

        /// <summary>
        /// 尝试预约路径前方给定距离范围内的磁钉，返回能申请到的最大静态距离。注意不包含到磁钉的安全距离。
        /// </summary>
        /// <param name="oA">AGV对象</param>
        /// <param name="MaxReclaimLength">最大申请距离</param>
        /// <param name="ActReclaimLength">实际申请距离</param>
        /// <returns>正常返回true，否则返回false</returns>
        private bool ClaimTransponder(AGV oA, double MaxReclaimLength, ref List<SimTransponder> lTPs)
        {
            bool IsClaimToTail;
            double RoutePosMid, MaxReclaimRoutePos, CurrClaimRoutePos, StartClaimRoutePos, LastClaimRoutePos;
            List<TPInfoEnRoute> lTempTPInfos;
            SimTransponder oTP;
            List<uint> lTempU;

            RoutePosMid = -1;

            if (!this.SearchForRoutePosByCoor(oA.ID, oA.MidPoint.X, oA.MidPoint.Y, out RoutePosMid))
            {
                if (this.IsSelfCheckAndThrowException)
                    throw new SimException("MidPoint of AGV: " + oA.ID.ToString() + " Not On AGVLine");
                return false;
            }

            // 考虑预约磁钉的开始 RoutePos 和结束 RoutePos。此时考虑车头到磁钉的安全距离。
            StartClaimRoutePos = RoutePosMid;
            CurrClaimRoutePos = RoutePosMid;

            MaxReclaimRoutePos = Math.Round(Math.Min(this.dAGVRoutes[oA.ID].TotalLength, StartClaimRoutePos + oA.oType.Length / 2 + MaxReclaimLength), this.DecimalNum);

            lTempTPInfos = this.dAGVRoutes[oA.ID].lRouteTPInfos.Where(u => u.RoutePos > StartClaimRoutePos && u.RoutePos <= MaxReclaimRoutePos)
                .OrderBy(u => u.RoutePos).ToList();

            IsClaimToTail = true;
            foreach (TPInfoEnRoute oTPInfo in lTempTPInfos)
            {
                oTP = this.dQCAndEssentialTPs[oTPInfo.TPID];
                LastClaimRoutePos = CurrClaimRoutePos;
                CurrClaimRoutePos = oTPInfo.RoutePos;

                // 不能被其他 AGV 预约
                if (oTP.dRouteTPDivisions.Any(u => u.Key != oA.ID && u.Value == StatusEnums.RouteTPDivision.Claim))
                {
                    IsClaimToTail = false;
                    break;
                }

                // 若已经Resv，除非是被自己Resv才继续
                switch (oTP.dRouteTPDivisions[oA.ID])
                {
                    case StatusEnums.RouteTPDivision.Passed:
                        Logger.Simulate.Error("SimTrafficController: Trying To Reserve Passed Transponder!");
                        return false;
                    case StatusEnums.RouteTPDivision.Claim:
                        continue;
                    case StatusEnums.RouteTPDivision.Detect:
                        if (this.IsDeadLockDetectInputLog)
                            this.sw_AgvResvRelease.Write(oA.ID.ToString() + ",Reserve," + oTP.ID.ToString());

                        this.TPReclaimTime++;

                        oTP.dRouteTPDivisions[oA.ID] = StatusEnums.RouteTPDivision.Claim;
                        oTPInfo.eRouteTPDivision = StatusEnums.RouteTPDivision.Claim;
                        this.RenewSurpassableClaimTPs(oA, CurrClaimRoutePos);

                        // 只有占领路径的交点才可能导致死锁
                        if (oTP.dRouteTPDivisions.Count >= 2)
                        {
                            if (this.DeadlockDetect(oA.ID, oTPInfo.TPID))
                            {
                                oTP.dRouteTPDivisions[oA.ID] = StatusEnums.RouteTPDivision.Detect;
                                oTPInfo.eRouteTPDivision = StatusEnums.RouteTPDivision.Detect;
                                this.RenewSurpassableClaimTPs(oA, LastClaimRoutePos);
                                IsClaimToTail = false;
                                if (this.IsDeadLockDetectInputLog)
                                    this.sw_AgvResvRelease.WriteLine("," + this.DeadlockDetectTime.ToString().PadLeft(8, '0') + ",false");
                            }
                            else
                            {
                                lTPs.Add(oTP);
                                if (this.IsDeadLockDetectInputLog)
                                    this.sw_AgvResvRelease.WriteLine("," + this.DeadlockDetectTime.ToString().PadLeft(8, '0') + ",true");
                            }
                        }
                        else
                        {
                            lTPs.Add(oTP);
                            if (this.IsDeadLockDetectInputLog)
                                this.sw_AgvResvRelease.WriteLine(",NotCross,true");
                        }
                        break;
                    default:
                        break;
                }

                if (!this.IsUnSurpassableTPToClaimEndTest(out lTempU))
                    lTempU.ForEach(u => Console.WriteLine("AGV : " + u.ToString() + "Claim End TP Is Not UnSurpassable!"));

                if (!IsClaimToTail)
                    break;
            }

            if (!IsClaimToTail)
                CurrClaimRoutePos = CurrClaimRoutePos - this.CompelAGVIntvToTP;

            if (IsClaimToTail && CurrClaimRoutePos < MaxReclaimRoutePos)
                CurrClaimRoutePos = MaxReclaimRoutePos;

            this.dAGVRoutes[oA.ID].CurrClaimLength = Math.Round(CurrClaimRoutePos, this.DecimalNum);

            return true;
        }

        /// <summary>
        /// 修正 TimeLength 末尾 AGV 的速度上限
        /// </summary>
        /// <param name="oA">AGV 对象</param>
        /// <param name="TimeLength">时长</param>
        /// <param name="ActReclaimLength">实际预约距离</param>
        /// <param name="ActVeloStepEnd">修正后的步末速度</param>
        /// <param name="ActMoveLength">本步实际移动距离</param>
        private void AmendMaxVeloStepEnd(AGV oA, double TimeLength, out double ActVeloStepEnd, out double ActMoveLength)
        {
            double MaxVeloVeh, HeadRoutePos, TempFrontAGVPos, MidRoutePos;
            double CurrMaxVeloStepEnd, CurrMoveLengthStepEnd, FrontAGVPosInThisRoute, FrontAGVPosInSelfRoute, VeloRelatedDir;
            double CurrMaxVeloStepEnd_ori, CurrMoveLengthStepEnd_ori;
            double CurrMaxVeloStepEnd_ActClaimLength, CurrMoveLengthStepEnd_ActClaimLength;
            double CurrMaxVeloStepEnd_Turn, CurrMoveLengthStepEnd_Turn;
            double CurrMaxVeloStepEnd_Safe, CurrMoveLengthStepEnd_Safe;
            double x1_ActRecLength, x2_ActRecLength, x1_Turn, x2_Turn, x1_Safe, x2_Safe, CurrMaxMoveLength;
            int SegID, SegID2;
            AGVOccuLineSeg oAOLS;
            List<AGVOccuLineSeg> lAOLSs;
            bool bCycelSegStart, bEmergentStop;

            CurrMaxVeloStepEnd = -1;
            FrontAGVPosInThisRoute = -1;
            VeloRelatedDir = 0;

            // 按照车辆加速能力和道路限速，本步末端的理论最大速度
            MaxVeloVeh = this.GetMaxVeloVeh(oA);
            CurrMaxVeloStepEnd_ori = Math.Round(Math.Min(oA.CurrVelo + oA.oType.Acceleration * TimeLength, MaxVeloVeh), this.DecimalNum);
            CurrMoveLengthStepEnd_ori = Math.Round((oA.CurrVelo + CurrMaxVeloStepEnd_ori) * TimeLength / 2, this.DecimalNum);

            this.SearchForRoutePosByCoor(oA.ID, oA.MidPoint.X, oA.MidPoint.Y, out MidRoutePos);
            if (MidRoutePos < 0)
                if (this.IsSelfCheckAndThrowException)
                    throw new SimException("AGV: " + oA.ID.ToString() + " Calcu Route Pos Failed");

            HeadRoutePos = MidRoutePos + oA.oType.Length / 2;
            CurrMaxMoveLength = this.dAGVRoutes[oA.ID].CurrClaimLength - HeadRoutePos;

            // 按照实际预约长度修正
            CurrMaxVeloStepEnd_ActClaimLength = -1;
            CurrMoveLengthStepEnd_ActClaimLength = -1;
            if ((oA.CurrVelo + CurrMaxVeloStepEnd_ori) * TimeLength / 2 +
                (CurrMaxVeloStepEnd_ori * CurrMaxVeloStepEnd_ori) / 2 / oA.oType.Acceleration > CurrMaxMoveLength)
            {
                if (this.QuadraticEquationWithOneUnknownSolver(1, oA.oType.Acceleration * TimeLength,
                    oA.oType.Acceleration * (oA.CurrVelo * TimeLength - 2 * CurrMaxMoveLength), out x1_ActRecLength, out x2_ActRecLength))
                {
                    if (x1_ActRecLength >= 0 && x1_ActRecLength <= CurrMaxVeloStepEnd_ori)
                    {
                        CurrMaxVeloStepEnd_ActClaimLength = x1_ActRecLength;
                        CurrMoveLengthStepEnd_ActClaimLength = Math.Round((oA.CurrVelo + CurrMaxVeloStepEnd_ActClaimLength) * TimeLength / 2, this.DecimalNum);
                    }
                    else if (x2_ActRecLength >= 0 && x2_ActRecLength <= CurrMaxVeloStepEnd_ori)
                    {
                        CurrMaxVeloStepEnd_ActClaimLength = x2_ActRecLength;
                        CurrMoveLengthStepEnd_ActClaimLength = Math.Round((oA.CurrVelo + CurrMaxVeloStepEnd_ActClaimLength) * TimeLength / 2, this.DecimalNum);
                    }
                    else
                    {
                        CurrMaxVeloStepEnd_ActClaimLength = 0;
                        CurrMoveLengthStepEnd_ActClaimLength = 0;
                    }
                }
                else
                {
                    CurrMaxVeloStepEnd_ActClaimLength = 0;
                    CurrMoveLengthStepEnd_ActClaimLength = 0;
                }
            }

            // 在转弯范围以外时，按照转弯位置修正
            CurrMaxVeloStepEnd_Turn = -1;
            CurrMoveLengthStepEnd_Turn = -1;
            
            SegID = this.dAGVRoutes[oA.ID].GetRouteSegIDByRoutePos(MidRoutePos);

            if ((SegID > 0 && Math.Abs(this.dAGVRoutes[oA.ID].lRouteSegments[SegID].StartRoutePos - MidRoutePos) <= this.AGVTurnRadius)
                || (SegID < this.dAGVRoutes[oA.ID].lRouteSegments.Count - 1 && Math.Abs(this.dAGVRoutes[oA.ID].lRouteSegments[SegID].EndRoutePos - MidRoutePos) <= this.AGVTurnRadius))
            {
                // 转弯区
                if (oA.CurrVelo > oA.oType.VeloTurnUpper)
                    oA.CurrVelo = oA.oType.VeloTurnUpper;
                    //throw new Exception("AGV: " + oA.ID.ToString() + " Turn Exceeding Regular Turn Speed");
                CurrMaxVeloStepEnd_Turn = oA.oType.VeloTurnUpper;
                CurrMoveLengthStepEnd_Turn = CurrMaxVeloStepEnd_Turn * TimeLength;
            }
            else
            {
                if (SegID >= 0 && SegID < this.dAGVRoutes[oA.ID].lRouteSegments.Count - 1)
                {
                    if ((oA.CurrVelo + CurrMaxVeloStepEnd_ori) * TimeLength / 2 + (CurrMaxVeloStepEnd_ori * CurrMaxVeloStepEnd_ori - oA.oType.VeloTurnUpper * oA.oType.VeloTurnUpper)
                        / 2 / oA.oType.Acceleration > this.dAGVRoutes[oA.ID].lRouteSegments[SegID].EndRoutePos - MidRoutePos - this.AGVTurnRadius)
                    {
                        if (this.QuadraticEquationWithOneUnknownSolver(1, oA.oType.Acceleration * TimeLength,
                            oA.oType.Acceleration * oA.CurrVelo * TimeLength - (oA.oType.VeloTurnUpper * oA.oType.VeloTurnUpper)
                                - 2 * oA.oType.Acceleration * (this.dAGVRoutes[oA.ID].lRouteSegments[SegID].EndRoutePos - MidRoutePos - this.AGVTurnRadius), out x1_Turn, out x2_Turn))
                        {
                            if (x1_Turn >= 0 && x1_Turn <= CurrMaxVeloStepEnd_ori)
                            {
                                CurrMaxVeloStepEnd_Turn = x1_Turn;
                                CurrMoveLengthStepEnd_Turn = Math.Round((oA.CurrVelo + CurrMaxVeloStepEnd_Turn) * TimeLength / 2, this.DecimalNum);
                            }
                            else if (x2_Turn >= 0 && x2_Turn <= CurrMaxVeloStepEnd_ori)
                            {
                                CurrMaxVeloStepEnd_Turn = x2_Turn;
                                CurrMoveLengthStepEnd_Turn = Math.Round((oA.CurrVelo + CurrMaxVeloStepEnd_Turn) * TimeLength / 2, this.DecimalNum);
                            }
                            else
                            {
                                CurrMaxVeloStepEnd_Turn = 0;
                                CurrMoveLengthStepEnd_Turn = 0;
                            }
                        }
                        else
                        {
                            CurrMaxVeloStepEnd_Turn = 0;
                            CurrMoveLengthStepEnd_Turn = 0;
                        }

                    }
                }
                else if (SegID < 0)
                    if (this.IsSelfCheckAndThrowException)
                        throw new SimException("AGV: " + oA.ID.ToString() + " Not EnRoute");
            }

            // 按照前车安全距离修正，根据前面 AGV 的端点位置判断（在本 AGV 道路前方，离本 AGV 的车头最近的车头/车尾）
            CurrMaxVeloStepEnd_Safe = -1;
            CurrMoveLengthStepEnd_Safe = -1;
            FrontAGVPosInThisRoute = -1;
            FrontAGVPosInSelfRoute = -1;
            oAOLS = new AGVOccuLineSeg();
            lAOLSs = this.lAGVOccupyLineSegs.FindAll(u => u.AGVID != oA.ID && !(u.bStartPointHinge && u.bEndPointHinge));
            bCycelSegStart = false;
            bEmergentStop = false;

            foreach (AGVOccuLineSeg obj in lAOLSs)
            {
                // 相对于本线的较近端
                if (!obj.bStartPointHinge)
                {
                    TempFrontAGVPos = this.dAGVRoutes[oA.ID].GetRoutePosByLineAndPos(obj.AGVLineID, obj.StartPos);
                    if (TempFrontAGVPos > HeadRoutePos)
                    {
                        if (TempFrontAGVPos < HeadRoutePos + this.CompelAGVIntvToAGV)
                            bEmergentStop = true;
                        else if (TempFrontAGVPos - HeadRoutePos < CurrMaxMoveLength + this.CompelAGVIntvToAGV)
                        {
                            if (FrontAGVPosInThisRoute < 0 || TempFrontAGVPos < FrontAGVPosInThisRoute)
                            {
                                oAOLS = obj;
                                FrontAGVPosInThisRoute = TempFrontAGVPos;
                                bCycelSegStart = true;
                            }
                        }
                    }
                    else if (this.IsSelfCheckAndThrowException && TempFrontAGVPos > MidRoutePos)
                        Console.WriteLine("Sth Wrong");
                }

                if (!obj.bEndPointHinge)
                {
                    TempFrontAGVPos = this.dAGVRoutes[oA.ID].GetRoutePosByLineAndPos(obj.AGVLineID, obj.EndPos);
                    if (TempFrontAGVPos > HeadRoutePos)
                    {
                        if (TempFrontAGVPos < HeadRoutePos + this.CompelAGVIntvToAGV)
                            bEmergentStop = true;
                        else if (TempFrontAGVPos - HeadRoutePos < CurrMaxMoveLength + this.CompelAGVIntvToAGV)
                        {
                            if (FrontAGVPosInThisRoute < 0 || TempFrontAGVPos < FrontAGVPosInThisRoute)
                            {
                                oAOLS = obj;
                                FrontAGVPosInThisRoute = TempFrontAGVPos;
                                bCycelSegStart = false;
                            }
                        }
                    }
                    else if (this.IsSelfCheckAndThrowException && TempFrontAGVPos > MidRoutePos)
                        Console.WriteLine("Sth Wrong");
                }
            }

            if (bEmergentStop)
            {
                CurrMaxVeloStepEnd_Safe = 0;
                CurrMoveLengthStepEnd_Safe = 0;
            }
            else if (FrontAGVPosInThisRoute >= 0)
            {
                SegID = this.dAGVRoutes[oA.ID].GetRouteSegIDByRoutePos(FrontAGVPosInThisRoute);
                if (bCycelSegStart)
                    FrontAGVPosInSelfRoute = this.dAGVRoutes[oAOLS.AGVID].GetRoutePosByLineAndPos(oAOLS.AGVLineID, oAOLS.StartPos);
                else
                    FrontAGVPosInSelfRoute = this.dAGVRoutes[oAOLS.AGVID].GetRoutePosByLineAndPos(oAOLS.AGVLineID, oAOLS.EndPos);
                SegID2 = this.dAGVRoutes[oAOLS.AGVID].GetRouteSegIDByRoutePos(FrontAGVPosInSelfRoute);
                if (SegID >= 0 && SegID2 >= 0)
                {
                    if (this.dAGVRoutes[oA.ID].lRouteSegments[SegID].eCD == this.dAGVRoutes[oAOLS.AGVID].lRouteSegments[SegID2].eCD)
                        VeloRelatedDir = 1;
                    else
                        VeloRelatedDir = -1;
                }
                else
                    if (this.IsSelfCheckAndThrowException)
                        throw new SimException("Fail To Get SegID Of Some RoutePos");

                // 如果不能在两车同刹停车后维持安全距离
                if (HeadRoutePos + (oA.CurrVelo + CurrMaxVeloStepEnd_ori) * TimeLength / 2 + (CurrMaxVeloStepEnd_ori * CurrMaxVeloStepEnd_ori) / 2 * oA.oType.Acceleration + this.CompelAGVIntvToAGV >
                    FrontAGVPosInThisRoute + VeloRelatedDir * (this.oSimDataStore.dAGVs[oAOLS.AGVID].CurrVelo * this.oSimDataStore.dAGVs[oAOLS.AGVID].CurrVelo) / 2 * oA.oType.Acceleration)
                {
                    if (this.QuadraticEquationWithOneUnknownSolver(1, oA.oType.Acceleration * TimeLength,
                        oA.oType.Acceleration * (2 * HeadRoutePos + 2 * this.CompelAGVIntvToAGV + oA.CurrVelo - 2 * FrontAGVPosInThisRoute + (VeloRelatedDir) * (this.oSimDataStore.dAGVs[oAOLS.AGVID].CurrVelo
                            * this.oSimDataStore.dAGVs[oAOLS.AGVID].CurrVelo) * oA.oType.Acceleration / this.oSimDataStore.dAGVs[oAOLS.AGVID].oType.Acceleration), out x1_Safe, out x2_Safe))
                    {
                        if (x1_Safe >= 0 && x1_Safe <= CurrMaxVeloStepEnd_ori)
                        {
                            CurrMaxVeloStepEnd_Safe = x1_Safe;
                            CurrMoveLengthStepEnd_Safe = Math.Round((oA.CurrVelo + CurrMaxVeloStepEnd_Safe) * TimeLength / 2, this.DecimalNum);
                        }
                        else if (x2_Safe >= 0 && x2_Safe <= CurrMaxVeloStepEnd_ori)
                        {
                            CurrMaxVeloStepEnd_Safe = x2_Safe;
                            CurrMoveLengthStepEnd_Safe = Math.Round((oA.CurrVelo + CurrMaxVeloStepEnd_Safe) * TimeLength / 2, this.DecimalNum);
                        }
                        else
                        {
                            CurrMaxVeloStepEnd_Safe = 0;
                            CurrMoveLengthStepEnd_Safe = 0;
                        }
                    }
                    else
                    {
                        CurrMaxVeloStepEnd_Safe = 0;
                        CurrMoveLengthStepEnd_Safe = 0;
                    }
                }
            }

            CurrMaxVeloStepEnd = CurrMaxVeloStepEnd_ori;
            CurrMoveLengthStepEnd = CurrMoveLengthStepEnd_ori;
            if (CurrMaxVeloStepEnd_ActClaimLength >= 0)
            {
                CurrMaxVeloStepEnd = Math.Min(CurrMaxVeloStepEnd, CurrMaxVeloStepEnd_ActClaimLength);
                CurrMoveLengthStepEnd = Math.Min(CurrMoveLengthStepEnd, CurrMoveLengthStepEnd_ActClaimLength);
            }
            if (CurrMaxVeloStepEnd_Turn >= 0)
            {
                CurrMaxVeloStepEnd = Math.Min(CurrMaxVeloStepEnd, CurrMaxVeloStepEnd_Turn);
                CurrMoveLengthStepEnd = Math.Min(CurrMoveLengthStepEnd, CurrMoveLengthStepEnd_Turn);
            }
            if (CurrMaxVeloStepEnd_Safe >= 0)
            {
                CurrMaxVeloStepEnd = Math.Min(CurrMaxVeloStepEnd, CurrMaxVeloStepEnd_Safe);
                CurrMoveLengthStepEnd = Math.Min(CurrMoveLengthStepEnd, CurrMoveLengthStepEnd_Safe);
            }

            ActVeloStepEnd = CurrMaxVeloStepEnd;
            ActMoveLength = CurrMoveLengthStepEnd;

            if (this.IsSelfCheckAndThrowException && ActMoveLength > Math.Max(oA.CurrVelo, CurrMaxVeloStepEnd) * TimeLength)
                throw new SimException("AGV: Crossed");
        }

        /// <summary>
        /// 更新AGV的单步速度和位置，占线状态，并将释放的磁钉加入列表
        /// </summary>
        /// <param name="oA">AGV 对象</param>
        /// <param name="ActVeloStepEnd">步末实际速度</param>
        /// <param name="ActMoveLength">本步实际移动距离</param>
        /// <param name="lTempTPs">状态改变的磁钉列表</param>
        /// <param name="lTempLanes">状态改变的车道列表</param>
        /// <param name="lTempAGVs">状态改变的AGV列表</param>
        /// <param name="lTempAGVStatus">状态改变的AGV_STATUS列表</param>
        /// <returns>正常返回true，否则返回false</returns>
        private bool RenewAGVStatus(AGV oA, double ActVeloStepEnd, double ActMoveLength, ref List<SimTransponder> lTempTPs, ref List<Lane> lTempLanes, ref List<AGV> lTempAGVs, ref List<AGV_STATUS> lTempAGVStatus)
        {
            double CurrMidRoutePos, NextMidRoutePos, SegLowRoutePos, SegHighRoutePos, Pos1, Pos2, X, Y, NextRouteAngle;
            uint LineID1, LineID2;
            bool bArrive;
            List<AGVOccuLineSeg> lOccuSegs;
            List<TPInfoEnRoute> lTPInfos;
            AGV_STATUS oAgvStatus;
         
       
            if (!this.SearchForRoutePosByCoor(oA.ID, oA.MidPoint.X, oA.MidPoint.Y, out CurrMidRoutePos))
            {
                throw new SimException("AGV: " + oA.ID.ToString() + "Not EnRoute");
                //return false;
            }

            if (!lTempAGVs.Exists(u => u.ID == oA.ID))
                lTempAGVs.Add(oA);

            NextMidRoutePos = CurrMidRoutePos + ActMoveLength;

            // 到达目标点，CurrMidRoutePos 和 NextMidRoutePos 的微小误差修正
            bArrive = false;
            if (Math.Abs(this.dAGVRoutes[oA.ID].TotalLength - CurrMidRoutePos - oA.oType.Length / 2) <= 1 / Math.Pow(10, this.DecimalNum) * 2)
            {
                NextMidRoutePos = this.dAGVRoutes[oA.ID].TotalLength - oA.oType.Length / 2;
                CurrMidRoutePos = NextMidRoutePos;
                bArrive = true;
            }

            // 更新 AGVOccuLineSeg 以及 AGV 倾角，返回本步和本步末的 RoutePos
            this.lAGVOccupyLineSegs.RemoveAll(u => u.AGVID == oA.ID);
            lOccuSegs = new List<AGVOccuLineSeg>();

            foreach (RouteSegment oRS in this.dAGVRoutes[oA.ID].lRouteSegments)
            {
                if (NextMidRoutePos - oA.oType.Length / 2 < oRS.EndRoutePos && NextMidRoutePos + oA.oType.Length / 2 > oRS.StartRoutePos)
                {
                    oAOLS = new AGVOccuLineSeg();
                    oAOLS.AGVID = oA.ID;
                    oAOLS.AGVLineID = oRS.AGVLineID;

                    SegLowRoutePos = Math.Max(oRS.StartRoutePos, NextMidRoutePos - oA.oType.Length / 2);
                    if (NextMidRoutePos - oA.oType.Length / 2 < oRS.StartRoutePos)
                        oAOLS.bStartPointHinge = true;
                    else
                        oAOLS.bStartPointHinge = false;

                    if (!this.dAGVRoutes[oA.ID].SearchForLineAndPosByRoutePos(SegLowRoutePos, out LineID1, out Pos1, out LineID2, out Pos2))
                        return false;

                    if (LineID1 == oRS.AGVLineID)
                        oAOLS.StartPos = Pos1;
                    else if (LineID2 == oRS.AGVLineID)
                        oAOLS.StartPos = Pos2;
                    else
                        return false;

                    SegHighRoutePos = Math.Min(oRS.EndRoutePos, NextMidRoutePos + oA.oType.Length / 2);
                    if (NextMidRoutePos + oA.oType.Length / 2 > oRS.EndRoutePos)
                        oAOLS.bEndPointHinge = true;
                    else
                        oAOLS.bEndPointHinge = false;
                    if (!this.dAGVRoutes[oA.ID].SearchForLineAndPosByRoutePos(SegHighRoutePos, out LineID1, out Pos1, out LineID2, out Pos2))
                        return false;
                    if (LineID1 == oRS.AGVLineID)
                        oAOLS.EndPos = Pos1;
                    else if (LineID2 == oRS.AGVLineID)
                        oAOLS.EndPos = Pos2;
                    else
                        return false;

                    lOccuSegs.Add(oAOLS);
                }
            }

            this.lAGVOccupyLineSegs.AddRange(lOccuSegs);

            // 更新方向
            NextRouteAngle = this.GetAGVRouteAngle(oA);
            oA.RotateAngle = oA.RotateAngle + (NextRouteAngle - this.dAGVAnglesEnRoute[oA.ID]);
            while (oA.RotateAngle < 0)
                oA.RotateAngle = oA.RotateAngle + 360;
            while (oA.RotateAngle >= 360)
                oA.RotateAngle = oA.RotateAngle - 360;
            this.dAGVAnglesEnRoute[oA.ID] = NextRouteAngle;

            // 释放 TP 和 CycleSeg，可能轧过出 Lane TP，触发 Lane 离开事件
            // 注意不影响交点的lRouteAGVIDs属性
            lTPInfos = this.dAGVRoutes[oA.ID].lRouteTPInfos.Where(u => u.RoutePos < NextMidRoutePos - oA.oType.Length / 2 - this.CompelAGVIntvToTP
                && this.dQCAndEssentialTPs[u.TPID].dRouteTPDivisions[oA.ID] == StatusEnums.RouteTPDivision.Claim).ToList();

            foreach (TPInfoEnRoute obj in lTPInfos)
            {
                obj.eRouteTPDivision = StatusEnums.RouteTPDivision.Passed;
                this.dQCAndEssentialTPs[obj.TPID].dRouteTPDivisions[oA.ID] = StatusEnums.RouteTPDivision.Passed;

                if (!lTempTPs.Exists(u => u.ID == obj.TPID))
                    lTempTPs.Add(this.dQCAndEssentialTPs[obj.TPID]);

                if (obj.ExitLaneID > 0)
                {
                    this.oSimDataStore.dLanes[obj.ExitLaneID].eStatus = LaneStatus.IDLE;
                    if (!lTempLanes.Exists(u => u.ID == obj.ExitLaneID))
                        lTempLanes.Add(this.oSimDataStore.dLanes[obj.ExitLaneID]);
                    if (oA.CurrLaneID == obj.ExitLaneID) 
                        oA.CurrLaneID = 0;
                }

                if (this.IsDeadLockDetectInputLog && this.dQCAndEssentialTPs[obj.TPID].dRouteTPDivisions.Count >= 2)
                    this.sw_AgvResvRelease.WriteLine(oA.ID.ToString() + ",Release," + obj.TPID.ToString());
            }

            // 可能轧过进 Lane 的 TP，触发 Lane 进入事件。一进入就往前推 Lane，不管上一 Lane 是否已经退出
            lTPInfos = this.dAGVRoutes[oA.ID].lRouteTPInfos.Where(u => u.RoutePos > CurrMidRoutePos + oA.oType.Length / 2
                && u.RoutePos <= NextMidRoutePos + oA.oType.Length / 2 && u.EnterLaneID > 0).ToList();
            foreach (TPInfoEnRoute obj in lTPInfos)
            {
                this.oSimDataStore.dLanes[obj.EnterLaneID].eStatus = LaneStatus.OCCUPIED;
                lTempLanes.Add(this.oSimDataStore.dLanes[obj.EnterLaneID]);
                this.dAGVRoutes[oA.ID].CurrLaneSeq++;
                oA.CurrLaneID = this.dAGVRoutes[oA.ID].lRouteLaneIDs[this.dAGVRoutes[oA.ID].CurrLaneSeq];
                if (this.dAGVRoutes[oA.ID].CurrLaneSeq + 1 < this.dAGVRoutes[oA.ID].lRouteLaneIDs.Count)
                    oA.NextLaneID = this.dAGVRoutes[oA.ID].lRouteLaneIDs[this.dAGVRoutes[oA.ID].CurrLaneSeq + 1];
            }

            // 更新车辆位置和速度
            if (!this.SearchForCoorByRoutePos(oA.ID, NextMidRoutePos, out X, out Y))
            {
                throw new SimException("AGV Not EnRoute");
                //return false;
            }
            if (ActMoveLength == 0)
                oA.eStepTravelStatus = StatusEnums.StepTravelStatus.Wait;
            else
                oA.eStepTravelStatus = StatusEnums.StepTravelStatus.Move;
            oA.MidPoint = new Point(Math.Round(X, this.DecimalNum), Math.Round(Y, this.DecimalNum));
            oA.CurrVelo = Math.Round(ActVeloStepEnd, this.DecimalNum);

            // AGV_STATUS 更新
            oAgvStatus = this.oSimDataStore.dAGVStatus.Values.FirstOrDefault<AGV_STATUS>(u => !string.IsNullOrWhiteSpace(u.CHE_ID)
                && Convert.ToUInt32(u.CHE_ID) == oA.ID);
            if (oAgvStatus != null)
            {
                oAgvStatus.LOCATION = oA.CurrLaneID.ToString();
                oAgvStatus.NEXT_LOCATION = oA.NextLaneID.ToString();
                oAgvStatus.LOCATION_X = Convert.ToInt32(oA.MidPoint.X);
                oAgvStatus.LOCATION_Y = Convert.ToInt32(oA.MidPoint.Y);
                oAgvStatus.ORIENTATION = Convert.ToInt16(oA.RotateAngle);
                oAgvStatus.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            }
            lTempAGVStatus.Add(oAgvStatus);

            if (bArrive)
            {
                // 到达，去除磁钉点的信息，删除路径
                oA.eMotionStatus = StatusEnums.MotionStatus.Waiting;
                foreach (TPInfoEnRoute oInfo in this.dAGVRoutes[oA.ID].lRouteTPInfos)
                {
                    if (this.dQCAndEssentialTPs.ContainsKey(oInfo.TPID))
                        this.dQCAndEssentialTPs[oInfo.TPID].dRouteTPDivisions.Remove(oA.ID);
                    if (!lTempTPs.Exists(u => u.ID == oInfo.TPID))
                        lTempTPs.Add(this.dQCAndEssentialTPs[oInfo.TPID]);
                }
                if (oA.CurrLaneID == 0)
                {
                    oA.CurrLaneID = oA.AimLaneID;
                }
                if (oA.ID == 951)
                {

                }

                this.dAGVRoutes.Remove(oA.ID);                
            }

            return true;
        }

        /// <summary>
        /// 根据坐标点寻找线上点
        /// </summary>
        /// <param name="X">坐标点的X坐标</param>
        /// <param name="Y">坐标点的Y坐标</param>
        /// <param name="LineID1">线号1</param>
        /// <param name="Pos1">特征坐标1</param>
        /// <param name="LineID2">线号2</param>
        /// <param name="Pos2">特征坐标2</param>
        /// <returns>能找到返回true，否则返回false</returns>
        private bool SearchForLineIDAndPosByCoor(double X, double Y, out uint LineID1, out double Pos1, out uint LineID2, out double Pos2)
        {
            bool bRet = false;

            LineID1 = 0; LineID2 = 0; Pos1 = -1; Pos2 = -1;

            foreach (AGVLine oAL in this.dEssentialAGVLines.Values)
            {
                switch (oAL.eFlowDir)
                {
                    case CoordinateDirection.X_POSITIVE:
                    case CoordinateDirection.X_NEGATIVE:
                        if (Math.Abs(oAL.FeaturePosition - Y) < 0.01)
                        {
                            LineID1 = oAL.ID;
                            Pos1 = Math.Round(X, this.DecimalNum);
                            bRet = true;
                        }
                        break;
                    case CoordinateDirection.Y_POSITIVE:
                    case CoordinateDirection.Y_NEGATIVE:
                        if (Math.Abs(oAL.FeaturePosition - X) < 0.01)
                        {
                            LineID2 = oAL.ID;
                            Pos2 = Math.Round(Y, this.DecimalNum);
                            bRet = true;
                        }
                        break;
                }
            }

            return bRet;
        }

        /// <summary>
        /// 根据坐标点寻找线上点
        /// </summary>
        /// <param name="X">坐标点的X坐标</param>
        /// <param name="Y">坐标点的Y坐标</param>
        /// <param name="LineID1">线号1</param>
        /// <param name="Pos1">特征坐标1</param>
        /// <returns>能找到返回true，否则返回false</returns>
        private bool SearchForLineIDAndPosByCoor(double X, double Y, out uint LineID1, out double Pos1)
        {
            bool bRet = false;

            LineID1 = 0; Pos1 = -1;

            foreach (AGVLine oAL in this.dEssentialAGVLines.Values)
            {
                switch (oAL.eFlowDir)
                {
                    case CoordinateDirection.X_POSITIVE:
                    case CoordinateDirection.X_NEGATIVE:
                        if (Math.Abs(oAL.FeaturePosition - Y) < 0.01)
                        {
                            LineID1 = oAL.ID;
                            Pos1 = Math.Round(X, this.DecimalNum);
                            bRet = true;
                        }
                        break;
                    case CoordinateDirection.Y_POSITIVE:
                    case CoordinateDirection.Y_NEGATIVE:
                        if (Math.Abs(oAL.FeaturePosition - X) < 0.01)
                        {
                            LineID1 = oAL.ID;
                            Pos1 = Math.Round(Y, this.DecimalNum);
                            bRet = true;
                        }
                        break;
                }
            }

            return bRet;
        }

        /// <summary>
        /// 从线上点到坐标点的转换
        /// </summary>
        /// <param name="AGVLineID">线号</param>
        /// <param name="Pos">线上坐标</param>
        /// <param name="X">点X坐标</param>
        /// <param name="Y">点Y坐标</param>
        /// <returns>转换成功返回true，否则返回false</returns>
        private bool SearchForCoorByLineAndPos(uint AGVLineID, double Pos, out double X, out double Y)
        {
            bool bRet = false;
            X = -1;
            Y = -1;
            if (this.dEssentialAGVLines.ContainsKey(AGVLineID))
            {
                switch (this.dEssentialAGVLines[AGVLineID].eFlowDir)
                {
                    case CoordinateDirection.X_POSITIVE:
                    case CoordinateDirection.X_NEGATIVE:
                        X = Math.Round(Pos, this.DecimalNum);
                        Y = Math.Round(this.dEssentialAGVLines[AGVLineID].FeaturePosition, this.DecimalNum);
                        bRet = true;
                        break;
                    case CoordinateDirection.Y_POSITIVE:
                    case CoordinateDirection.Y_NEGATIVE:
                        X = Math.Round(this.dEssentialAGVLines[AGVLineID].FeaturePosition, this.DecimalNum);
                        Y = Math.Round(Pos, this.DecimalNum);
                        bRet = true;
                        break;
                }
            }
            return bRet;
        }

        ///根据当前的状态动态的调整AGV路径
        ///只有AGV在主干道的时候路径才有可能被调整
        ///考虑在行驶方向之前的所有车辆在当前位置产生的势场
        ///路径变化之后改动AGVline,segment和磁钉
        private bool SearchForAGVlineChange()
        {
            Solute SJK = new Solute();
            Getpotential POT = new Getpotential();
            Random rand = new Random();

            bool result = false;
            if (this.lMovingAGVs.Count == 0)
                return result;

            foreach (AGV oA in this.lMovingAGVs)
            {
                AGVLine oAL = this.oSimDataStore.dAGVLines[oA.CurrAGVLineID];

                if (this.dAGVRoutes.ContainsKey(oA.ID))
                {
                    if (this.dAGVRoutes[oA.ID].lRouteSegments[this.dAGVRoutes[oA.ID].lRouteSegments.Count() - 1].AGVLineID != oAL.ID && oA.RotateAngle % 90 == 0 && this.dAGVRoutes[oA.ID].IsChanged == false) 
                    {
                        int lAGVLineid = (int)oAL.ID;
                        List<uint> AGVIDs = new List<uint>();
                        Dictionary<int, double> potentialwithline = new Dictionary<int, double>();

                       
                        if (oAL.eMoveDir == CHE_Direction.South)
                        {
                            List<SimTransponder> listforagvcd = dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oAL.ID).ToList()
                                .Where(u => u.dRouteTPDivisions.Values.Contains(StatusEnums.RouteTPDivision.Claim)).ToList();
                            
                            
                            foreach (SimTransponder lsp in listforagvcd)
                            {
                                foreach (uint oagvid in lsp.dRouteTPDivisions.Keys)
                                {
                                    if (lsp.dRouteTPDivisions[oagvid] == StatusEnums.RouteTPDivision.Claim && !AGVIDs.Contains(oagvid) && oagvid != oA.ID) 
                                    {
                                        AGVIDs.Add(oagvid);
                                    }
                                }
                            }


                            potentialwithline.Add(lAGVLineid, 0);
                            double dist_1 = 1000;
                            foreach (uint AGVidinlist in AGVIDs)
                            {
                                AGV oAGVinlist = this.oSimDataStore.dAGVs[AGVidinlist];
                                double odistance = oAGVinlist.MidPoint.Y - oA.MidPoint.Y;
                                double ospeed = oAGVinlist.CurrVelo;
                                if(oA.CurrAGVLineID!=oAGVinlist.CurrAGVLineID)
                                if(Math.Abs(oA.RotateAngle-oAGVinlist.RotateAngle)>45)
                                {
                                    ospeed=0;
                                }

                                if (odistance > 0 && odistance < dist_1)
                                {
                                    dist_1 = odistance;
                                    potentialwithline[lAGVLineid] = POT.changelinepotential(odistance, ospeed, oA.CurrVelo);
                                }
                            }

                            double dist_record = dist_1;


                            Dictionary<int, List<uint>> ostored = new Dictionary<int, List<uint>>();
                            int onum_1=0;

                            ///查找同方向道路在相同位置的势场
                            if (potentialwithline[lAGVLineid] > 0)
                            {
                                List<AGVLine> nearDXD = new List<AGVLine>();
                                nearDXD = this.oSimDataStore.dAGVLines.Values.Where(u => u.eMoveDir == CHE_Direction.South && u.ID != oAL.ID).ToList();
                                foreach (AGVLine PreAGVline in nearDXD)
                                {
                                    potentialwithline.Add((int)PreAGVline.ID, 0);
                                    int numjudge = (int)PreAGVline.ID-(int)oAL.ID;

                                    List<uint> AGVIDinOtherLine = new List<uint>();
                                    //AGVIDinOtherLine = SJK.ReadLine((int)PreAGVline.ID, "AGVlineid", "agvid", "agv_agvline");
                                    List<SimTransponder> listforotheragv = dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == PreAGVline.ID).ToList()
                                        .Where(u => u.dRouteTPDivisions.Values.Contains(StatusEnums.RouteTPDivision.Claim)).ToList();


                                    foreach (SimTransponder lsp in listforotheragv)
                                    {
                                        foreach (uint oagvid in lsp.dRouteTPDivisions.Keys)
                                        {
                                            if (lsp.dRouteTPDivisions[oagvid] == StatusEnums.RouteTPDivision.Claim && !AGVIDinOtherLine.Contains(oagvid))
                                            {
                                                AGVIDinOtherLine.Add(oagvid);
                                            }
                                        }
                                    }

                                    dist_1 = 1000;
                                    ostored.Add(onum_1, AGVIDinOtherLine);
                                    onum_1++;

                                    foreach (int AGVidinlist in AGVIDinOtherLine)
                                    {
                                        AGV oAGVinlist = this.oSimDataStore.dAGVs[(uint)AGVidinlist];
                                        double odistance = oAGVinlist.MidPoint.Y - oA.MidPoint.Y;
                                        double ospeed = oAGVinlist.CurrVelo;
                                       
                                        if (Math.Abs(oAGVinlist.RotateAngle - oA.RotateAngle) > 45) 
                                        {
                                            ospeed = 0;
                                        }

                                        if (odistance > 0 && odistance < dist_1)
                                        {
                                            dist_1 = odistance;
                                            potentialwithline[(int)PreAGVline.ID] = POT.changelinepotential(odistance, ospeed, oA.CurrVelo);
                                        }
                                    }

                                    if (Math.Abs(numjudge) == 5)
                                    {
                                        potentialwithline[(int)PreAGVline.ID] += 3;
                                    }
                                    else
                                    {
                                        potentialwithline[(int)PreAGVline.ID] += 1000;
                                    }
                                }

                                if (oA.AimLaneID > 89 && oA.AimLaneID < 127) 
                                {
                                    potentialwithline[346] += 20;
                                    potentialwithline[351] += 10;
                                }

                                if (oA.AimLaneID < 90 && oA.AimLaneID > 65)
                                {
                                    potentialwithline[356] += 20;
                                    potentialwithline[351] += 10;
                                }
                                if (potentialwithline.Values.Min() != 0)
                                {

                                }

                                if (potentialwithline[lAGVLineid] != potentialwithline.Values.Min() && oA.CurrVelo < 4 && potentialwithline[lAGVLineid] > 2) 
                                {
                                    List<int> MinGetout = new List<int>();
                                    foreach (int keyinPWL in potentialwithline.Keys)
                                    {
                                        if (potentialwithline[keyinPWL] == potentialwithline.Values.Min())
                                        {
                                            MinGetout.Add(keyinPWL);
                                        }
                                    }
                                    int ChangeLineID = MinGetout[rand.Next(0, MinGetout.Count())];

                                    ///修改
                                    ChangeEverything(oA, this.oSimDataStore.dAGVLines[(uint)ChangeLineID], oAL);

                                    result = true;
                                }

                                if (oA.AimLaneID > 89 && oA.AimLaneID < 127 && potentialwithline[lAGVLineid] != potentialwithline.Values.Min() && oA.CurrVelo >= 2) 
                                {
                                    List<int> MinGetout = new List<int>();
                                    foreach (int keyinPWL in potentialwithline.Keys)
                                    {
                                        if (potentialwithline[keyinPWL] == potentialwithline.Values.Min())
                                        {
                                            MinGetout.Add(keyinPWL);
                                        }
                                    }
                                    int ChangeLineID = MinGetout[rand.Next(0, MinGetout.Count())];

                                    ///修改
                                    ChangeEverything(oA, this.oSimDataStore.dAGVLines[(uint)ChangeLineID], oAL);

                                    result = true;
                                }

                                if (oA.AimLaneID < 90 && oA.AimLaneID > 65 && potentialwithline[lAGVLineid] != potentialwithline.Values.Min() && oA.CurrVelo >= 2)
                                {
                                    List<int> MinGetout = new List<int>();
                                    foreach (int keyinPWL in potentialwithline.Keys)
                                    {
                                        if (potentialwithline[keyinPWL] == potentialwithline.Values.Min())
                                        {
                                            MinGetout.Add(keyinPWL);
                                        }
                                    }
                                    int ChangeLineID = MinGetout[rand.Next(0, MinGetout.Count())];

                                    ///修改
                                    ChangeEverything(oA, this.oSimDataStore.dAGVLines[(uint)ChangeLineID], oAL);

                                    result = true;
                                }
                            }
                        }

                        if (oAL.eMoveDir == CHE_Direction.North)
                        {
                            List<SimTransponder> listforagvcd = dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oAL.ID).ToList()
                               .Where(u => u.dRouteTPDivisions.Values.Contains(StatusEnums.RouteTPDivision.Claim)).ToList();


                            foreach (SimTransponder lsp in listforagvcd)
                            {
                                foreach (uint oagvid in lsp.dRouteTPDivisions.Keys)
                                {
                                    if (lsp.dRouteTPDivisions[oagvid] == StatusEnums.RouteTPDivision.Claim)
                                    {
                                        AGVIDs.Add(oagvid);
                                    }
                                }
                            }

                            potentialwithline.Add(lAGVLineid, 0);
                            foreach (int AGVidinlist in AGVIDs)
                            {
                                AGV oAGVinlist = this.oSimDataStore.dAGVs[(uint)AGVidinlist];
                                double odistance = oA.MidPoint.Y - oAGVinlist.MidPoint.Y;
                                double ospeed = oAGVinlist.CurrVelo;
                                if (oA.CurrAGVLineID != oAGVinlist.CurrAGVLineID)
                                {
                                    ospeed = 0;
                                }

                                if (odistance > 0)
                                {
                                    potentialwithline[lAGVLineid] += POT.changelinepotential(odistance, ospeed, oA.CurrVelo);
                                }
                            }

                            ///查找同方向道路在相同位置的势场
                            if (potentialwithline[lAGVLineid] > 0)
                            {
                                List<AGVLine> nearDXD = new List<AGVLine>();
                                nearDXD = this.oSimDataStore.dAGVLines.Values.Where(u => u.eMoveDir == CHE_Direction.North && u.ID != oAL.ID).ToList();
                                foreach (AGVLine PreAGVline in nearDXD)
                                {
                                    potentialwithline.Add((int)PreAGVline.ID, 0);
                                    List<uint> AGVIDinOtherLine = new List<uint>();
                                    //AGVIDinOtherLine = SJK.ReadLine((int)PreAGVline.ID, "AGVlineid", "agvid", "agv_agvline");
                                    List<SimTransponder> listforotheragv = dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == PreAGVline.ID).ToList()
                                        .Where(u => u.dRouteTPDivisions.Values.Contains(StatusEnums.RouteTPDivision.Claim)).ToList();


                                    foreach (SimTransponder lsp in listforotheragv)
                                    {
                                        foreach (uint oagvid in lsp.dRouteTPDivisions.Keys)
                                        {
                                            if (lsp.dRouteTPDivisions[oagvid] == StatusEnums.RouteTPDivision.Claim)
                                            {
                                                AGVIDinOtherLine.Add(oagvid);
                                            }
                                        }
                                    } 
                                    
                                    foreach (int AGVidinlist in AGVIDinOtherLine)
                                    {
                                        AGV oAGVinlist = this.oSimDataStore.dAGVs[(uint)AGVidinlist];
                                        double odistance = oA.MidPoint.Y - oAGVinlist.MidPoint.Y;
                                        double ospeed = oAGVinlist.CurrVelo;
                                        if (this.oSimDataStore.dAGVLines[oAGVinlist.CurrAGVLineID].eFlowDir == CoordinateDirection.X_NEGATIVE
                                            || this.oSimDataStore.dAGVLines[oAGVinlist.CurrAGVLineID].eFlowDir == CoordinateDirection.X_POSITIVE)
                                        {
                                            ospeed = 0;
                                        }

                                        if (odistance > 0)
                                        {
                                            potentialwithline[(int)PreAGVline.ID] += POT.changelinepotential(odistance, ospeed, oA.CurrVelo);
                                        }
                                    }
                                }

                                if (potentialwithline[lAGVLineid] - potentialwithline.Values.Min() > 5)
                                {
                                    List<int> MinGetout = new List<int>();
                                    foreach (int keyinPWL in potentialwithline.Keys)
                                    {
                                        if (potentialwithline[keyinPWL] == potentialwithline.Values.Min())
                                        {
                                            MinGetout.Add(keyinPWL);
                                        }
                                    }
                                    int ChangeLineID = MinGetout[rand.Next(0, MinGetout.Count())];

                                    ///修改
                                    ChangeEverything(oA, this.oSimDataStore.dAGVLines[(uint)ChangeLineID], oAL);

                                    result = true;
                                }
                            }
                        }

                        if (oAL.eMoveDir == CHE_Direction.East)
                        {
                            List<SimTransponder> listforagvcd = dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oAL.ID).ToList()
                              .Where(u => u.dRouteTPDivisions.Values.Contains(StatusEnums.RouteTPDivision.Claim)).ToList();


                            foreach (SimTransponder lsp in listforagvcd)
                            {
                                foreach (uint oagvid in lsp.dRouteTPDivisions.Keys)
                                {
                                    if (lsp.dRouteTPDivisions[oagvid] == StatusEnums.RouteTPDivision.Claim)
                                    {
                                        AGVIDs.Add(oagvid);
                                    }
                                }
                            }

                            potentialwithline.Add(lAGVLineid, 0);
                            foreach (int AGVidinlist in AGVIDs)
                            {
                                AGV oAGVinlist = this.oSimDataStore.dAGVs[(uint)AGVidinlist];
                                double odistance = oAGVinlist.MidPoint.X - oA.MidPoint.X;
                                double ospeed = oAGVinlist.CurrVelo;
                                if (oA.CurrAGVLineID != oAGVinlist.CurrAGVLineID)
                                {
                                    ospeed = 0;
                                }

                                if (odistance > 0)
                                {
                                    potentialwithline[lAGVLineid] += POT.changelinepotential(odistance, ospeed, oA.CurrVelo);
                                }
                            }

                            ///查找同方向道路在相同位置的势场
                            if (potentialwithline[lAGVLineid] > 0)
                            {
                                List<AGVLine> nearDXD = new List<AGVLine>();
                                nearDXD = this.oSimDataStore.dAGVLines.Values.Where(u => u.eMoveDir == CHE_Direction.East && u.ID != oAL.ID).ToList();
                                foreach (AGVLine PreAGVline in nearDXD)
                                {
                                    potentialwithline.Add((int)PreAGVline.ID, 0);
                                    List<uint> AGVIDinOtherLine = new List<uint>();
                                    //AGVIDinOtherLine = SJK.ReadLine((int)PreAGVline.ID, "AGVlineid", "agvid", "agv_agvline");
                                    List<SimTransponder> listforotheragv = dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == PreAGVline.ID).ToList()
                                        .Where(u => u.dRouteTPDivisions.Values.Contains(StatusEnums.RouteTPDivision.Claim)).ToList();


                                    foreach (SimTransponder lsp in listforotheragv)
                                    {
                                        foreach (uint oagvid in lsp.dRouteTPDivisions.Keys)
                                        {
                                            if (lsp.dRouteTPDivisions[oagvid] == StatusEnums.RouteTPDivision.Claim)
                                            {
                                                AGVIDinOtherLine.Add(oagvid);
                                            }
                                        }
                                    } 
                                    
                                    foreach (int AGVidinlist in AGVIDinOtherLine)
                                    {
                                        AGV oAGVinlist = this.oSimDataStore.dAGVs[(uint)AGVidinlist];
                                        double odistance = oAGVinlist.MidPoint.X - oA.MidPoint.X;
                                        double ospeed = oAGVinlist.CurrVelo;
                                        if (this.oSimDataStore.dAGVLines[oAGVinlist.CurrAGVLineID].eFlowDir == CoordinateDirection.Y_NEGATIVE
                                            || this.oSimDataStore.dAGVLines[oAGVinlist.CurrAGVLineID].eFlowDir == CoordinateDirection.Y_POSITIVE)
                                        {
                                            ospeed = 0;
                                        }

                                        if (odistance > 0)
                                        {
                                            potentialwithline[(int)PreAGVline.ID] += POT.changelinepotential(odistance, ospeed, oA.CurrVelo);
                                        }
                                    }
                                }

                                if (potentialwithline[lAGVLineid] - potentialwithline.Values.Min() > 5)
                                {
                                    List<int> MinGetout = new List<int>();
                                    foreach (int keyinPWL in potentialwithline.Keys)
                                    {
                                        if (potentialwithline[keyinPWL] == potentialwithline.Values.Min())
                                        {
                                            MinGetout.Add(keyinPWL);
                                        }
                                    }
                                    int ChangeLineID = MinGetout[rand.Next(0, MinGetout.Count())];

                                    ///修改
                                    ChangeEverything(oA, this.oSimDataStore.dAGVLines[(uint)ChangeLineID], oAL);

                                    result = true;
                                }
                            }
                        }

                        if (oAL.eMoveDir == CHE_Direction.West)
                        {
                            List<SimTransponder> listforagvcd = dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oAL.ID).ToList()
                             .Where(u => u.dRouteTPDivisions.Values.Contains(StatusEnums.RouteTPDivision.Claim)).ToList();


                            foreach (SimTransponder lsp in listforagvcd)
                            {
                                foreach (uint oagvid in lsp.dRouteTPDivisions.Keys)
                                {
                                    if (lsp.dRouteTPDivisions[oagvid] == StatusEnums.RouteTPDivision.Claim && !AGVIDs.Contains(oagvid) && oagvid != oA.ID) 
                                    {
                                        AGVIDs.Add(oagvid);
                                    }
                                }
                            }

                            potentialwithline.Add(lAGVLineid, 0);
                            double dist_1 = 1000;
                            foreach (uint AGVidinlist in AGVIDs)
                            {
                                AGV oAGVinlist = this.oSimDataStore.dAGVs[AGVidinlist];
                                double odistance = oAGVinlist.MidPoint.Y - oA.MidPoint.Y;
                                double ospeed = oAGVinlist.CurrVelo;
                                if(oA.CurrAGVLineID!=oAGVinlist.CurrAGVLineID)
                                if(Math.Abs(oA.RotateAngle-oAGVinlist.RotateAngle)>45)
                                {
                                    ospeed=0;
                                    break;
                                }

                                if (odistance > 0 && odistance < dist_1)
                                {
                                    dist_1 = odistance;
                                    potentialwithline[lAGVLineid] = POT.changelinepotential(odistance, ospeed, oA.CurrVelo);
                                }
                            }

                            Dictionary<int, List<uint>> ostored = new Dictionary<int, List<uint>>();
                            int onum_1=0;

                            ///查找同方向道路在相同位置的势场
                            if (potentialwithline[lAGVLineid] > 0)
                            {
                                List<AGVLine> nearDXD = new List<AGVLine>();
                                nearDXD = this.oSimDataStore.dAGVLines.Values.Where(u => u.eMoveDir == CHE_Direction.West && u.ID != oAL.ID).ToList();
                                foreach (AGVLine PreAGVline in nearDXD)
                                {
                                    potentialwithline.Add((int)PreAGVline.ID, 0);
                                    int numjudge = (int)PreAGVline.ID-(int)oAL.ID;

                                    List<uint> AGVIDinOtherLine = new List<uint>();
                                    //AGVIDinOtherLine = SJK.ReadLine((int)PreAGVline.ID, "AGVlineid", "agvid", "agv_agvline");
                                    List<SimTransponder> listforotheragv = dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == PreAGVline.ID).ToList()
                                        .Where(u => u.dRouteTPDivisions.Values.Contains(StatusEnums.RouteTPDivision.Claim)).ToList();


                                    foreach (SimTransponder lsp in listforotheragv)
                                    {
                                        foreach (uint oagvid in lsp.dRouteTPDivisions.Keys)
                                        {
                                            if (lsp.dRouteTPDivisions[oagvid] == StatusEnums.RouteTPDivision.Claim && !AGVIDinOtherLine.Contains(oagvid))
                                            {
                                                AGVIDinOtherLine.Add(oagvid);
                                            }
                                        }
                                    }

                                    dist_1 = 1000;
                                    ostored.Add(onum_1, AGVIDinOtherLine);
                                    onum_1++;

                                    foreach (int AGVidinlist in AGVIDinOtherLine)
                                    {
                                        AGV oAGVinlist = this.oSimDataStore.dAGVs[(uint)AGVidinlist];
                                        double odistance = oAGVinlist.MidPoint.Y - oA.MidPoint.Y;
                                        double ospeed = oAGVinlist.CurrVelo;
                                       
                                        if (Math.Abs(oAGVinlist.RotateAngle - oA.RotateAngle) > 60) 
                                        {
                                            ospeed = 0;
                                            continue;
                                        }

                                        if (odistance > 0 && odistance < dist_1)
                                        {
                                            dist_1 = odistance;
                                            potentialwithline[(int)PreAGVline.ID] = POT.changelinepotential(odistance, ospeed, oA.CurrVelo);
                                        }
                                    }

                                    if (Math.Abs(numjudge) == 1)
                                    {
                                        potentialwithline[(int)PreAGVline.ID] += 5;
                                    }
                                    else
                                    {
                                        potentialwithline[(int)PreAGVline.ID] += 1000;
                                    }
                                }

                                if (oA.AimLaneID > 26 && oA.AimLaneID < 66) 
                                {
                                    potentialwithline[39] += 5;
                                    potentialwithline[38] += 10;
                                }

                                if (potentialwithline[lAGVLineid] != potentialwithline.Values.Min() && oA.CurrVelo < 2 && potentialwithline[lAGVLineid] > 2) 
                                {
                                    List<int> MinGetout = new List<int>();
                                    foreach (int keyinPWL in potentialwithline.Keys)
                                    {
                                        if (potentialwithline[keyinPWL] == potentialwithline.Values.Min())
                                        {
                                            MinGetout.Add(keyinPWL);
                                        }
                                    }
                                    int ChangeLineID = MinGetout[rand.Next(0, MinGetout.Count())];

                                    ///修改
                                    ChangeEverything(oA, this.oSimDataStore.dAGVLines[(uint)ChangeLineID], oAL);

                                    result = true;
                                }
                            }                         
                        }
                    }
                }
            }

            return result;
        }

        ///在路径中插入其他的路径
        ///改变车道函数
        private bool ChangeEverything(AGV oAGV, AGVLine ChangetoLine, AGVLine PreAGVline)
        {
            AGVLine NDAL;
            CHE_Direction eCD = PreAGVline.eMoveDir;
            RouteSegment oRStore = new RouteSegment();
            AGVRoute oAGVRoute = this.dAGVRoutes[oAGV.ID];
            List<AGVLine> ChangeLine = new List<AGVLine>();
            Dictionary<int, List<SimTransponder>> lrecord = new Dictionary<int, List<SimTransponder>>();
            List<SimTransponder> lTPUnUsed = new List<SimTransponder>();
            List<SimTransponder> oTPUnUsed = new List<SimTransponder>();


            if (eCD == CHE_Direction.North)
            {
                List<AGVLine> ZZLine = this.oSimDataStore.dAGVLines.Values.Where(u => u.ID < 48 && u.FeaturePosition < (oAGV.MidPoint.Y - 12) && u.eMoveDir == CHE_Direction.Unknown && u.lLinkLineIDs.Count() != 0).ToList();

                if (ZZLine.Count() == 0)
                {
                    return false;
                }
                ZZLine.Sort((v1, v2) => v1.FeaturePosition.CompareTo(v2.FeaturePosition));

                NDAL = ZZLine.Last();
            }

            else if (eCD == CHE_Direction.South)
            {
                List<AGVLine> ZZLine = this.oSimDataStore.dAGVLines.Values.Where(u => u.ID < 48 && u.FeaturePosition > (oAGV.MidPoint.Y + 12) && u.eMoveDir == CHE_Direction.Unknown && u.lLinkLineIDs.Count() != 0).ToList();
                if (ZZLine.Count() == 0)
                {
                    return false;
                }
                ZZLine.Sort((v1, v2) => v1.FeaturePosition.CompareTo(v2.FeaturePosition));

                NDAL = ZZLine.First();
            }

            else if (eCD == CHE_Direction.West)
            {
                List<AGVLine> ZZLine = this.oSimDataStore.dAGVLines.Values.Where(u => u.ID > 47 && u.FeaturePosition < (oAGV.MidPoint.X - 12) && u.eMoveDir == CHE_Direction.Unknown && u.lLinkLineIDs.Count() != 0).ToList();

                if (ZZLine.Count() == 0)
                {
                    return false;
                }
                ZZLine.Sort((v1, v2) => v1.FeaturePosition.CompareTo(v2.FeaturePosition));

                NDAL = ZZLine.Last();
            }

            else
            {
                List<AGVLine> ZZLine = this.oSimDataStore.dAGVLines.Values.Where(u => u.ID > 47 && u.FeaturePosition > (oAGV.MidPoint.X + 12) && u.eMoveDir == CHE_Direction.Unknown && u.lLinkLineIDs.Count() != 0).ToList();

                if (ZZLine.Count() == 0)
                {
                    return false;
                }
                ZZLine.Sort((v1, v2) => v1.FeaturePosition.CompareTo(v2.FeaturePosition));

                NDAL = ZZLine.First();
            }

            ChangeLine.Add(NDAL);
            ChangeLine.Add(ChangetoLine);

            List<RouteSegment> oRST = new List<RouteSegment>();
            int pos = oAGVRoute.lRouteSegments.FindIndex(u => u.AGVLineID == PreAGVline.ID);
            if (pos < 0)
            {
                return false;
            }
            double PreLinedir = oAGVRoute.lRouteSegments[pos].EndLinePos;
            uint Changebackline = oAGVRoute.lRouteSegments[pos + 1].AGVLineID;


            if (oAGVRoute.lRouteSegments.Count() > pos + 2)
            {
                AGVLine FTLine = this.oSimDataStore.dAGVLines[oAGVRoute.lRouteSegments[pos + 2].AGVLineID];
                AGVLine NTLine = this.oSimDataStore.dAGVLines[oAGVRoute.lRouteSegments[pos + 1].AGVLineID];
                double numnow;
                if (NTLine.eFlowDir == CoordinateDirection.X_POSITIVE ||
                    NTLine.eFlowDir == CoordinateDirection.X_NEGATIVE)
                {
                    numnow = oAGV.MidPoint.Y;
                }
                else
                {
                    numnow = oAGV.MidPoint.X;
                }
                if (Math.Abs(NTLine.FeaturePosition - numnow) < 30)
                {
                    return false;
                }

                if (eCD == CHE_Direction.South)
                {
                    switch (ChangetoLine.ID)
                    {
                        case 346:
                            if (NTLine.ID < 41)
                            {
                                Changebackline = 38;
                            }
                            else
                            {
                                Changebackline = 43;
                            }
                            break;
                        case 351:
                            if (NTLine.ID < 41)
                            {
                                Changebackline = 39;
                            }
                            else
                            {
                                Changebackline = 42;
                            }
                            break;
                        case 356:
                            if (NTLine.ID < 41)
                            {
                                Changebackline = 40;
                            }
                            else
                            {
                                Changebackline = 41;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }


            oRStore.EndLinePos = oAGVRoute.lRouteSegments[pos].EndLinePos;
            oRStore.EndPoint = oAGVRoute.lRouteSegments[pos].EndPoint;
            oRStore.EndRoutePos = oAGVRoute.lRouteSegments[pos].EndRoutePos;

            if (PreAGVline.eFlowDir == CoordinateDirection.X_POSITIVE ||
                PreAGVline.eFlowDir == CoordinateDirection.X_NEGATIVE)
            {
                Point oPint1 = new Point();
                oPint1.X = ChangeLine[0].FeaturePosition;
                oPint1.Y = oAGVRoute.lRouteSegments[pos].EndPoint.Y;
                oAGVRoute.lRouteSegments[pos].ChangeEndPoint(oPint1);
                oAGVRoute.lRouteSegments[pos].ChangeEndlinePos(Math.Round(oAGVRoute.lRouteSegments[pos].EndPoint.X, this.DecimalNum));
            }
            else
            {
                Point oPint1 = new Point();
                oPint1.X = oAGVRoute.lRouteSegments[pos].EndPoint.X;
                oPint1.Y = ChangeLine[0].FeaturePosition;

                oAGVRoute.lRouteSegments[pos].ChangeEndPoint(oPint1);
                oAGVRoute.lRouteSegments[pos].ChangeEndlinePos(Math.Round(oAGVRoute.lRouteSegments[pos].EndPoint.Y, this.DecimalNum));
            }
            oAGVRoute.lRouteSegments[pos].ChangeEndRoutePos(oAGVRoute.lRouteSegments[pos].EndRoutePos - Math.Abs(PreLinedir - oAGVRoute.lRouteSegments[pos].EndLinePos));

            for (int i = 0; i < 2; i++)
            {
                RouteSegment oRS = new RouteSegment();
                List<SimTransponder> lTempTPs = new List<SimTransponder>();

                oRS.AGVID = oAGV.ID;
                oRS.AGVLineID = ChangeLine[i].ID;

                if (ChangeLine[i].eMoveDir == CHE_Direction.Unknown)
                {
                    oRS.ID = (uint)pos + 1;
                }
                else
                {
                    oRS.ID = (uint)pos + 2;
                }


                if (this.oSimDataStore.dAGVLines[oRS.AGVLineID].eFlowDir == CoordinateDirection.X_POSITIVE
                        || this.oSimDataStore.dAGVLines[oRS.AGVLineID].eFlowDir == CoordinateDirection.X_NEGATIVE)
                {
                    if (i == 0)
                    {
                        oRS.StartPoint.X = Math.Round(PreAGVline.FeaturePosition, this.DecimalNum);
                        oRS.StartPoint.Y = Math.Round(ChangeLine[0].FeaturePosition, this.DecimalNum);
                        oRS.StartLinePos = Math.Round(oRS.StartPoint.X, this.DecimalNum);
                    }
                    else
                    {
                        oRS.StartPoint.X = Math.Round(ChangeLine[0].FeaturePosition, this.DecimalNum);
                        oRS.StartPoint.Y = Math.Round(ChangeLine[1].FeaturePosition, this.DecimalNum);

                        oRS.StartLinePos = Math.Round(oRS.StartPoint.X, this.DecimalNum);
                    }
                }
                else
                {
                    if (i == 0)
                    {
                        oRS.StartPoint.X = Math.Round(ChangeLine[0].FeaturePosition, this.DecimalNum);
                        oRS.StartPoint.Y = Math.Round(PreAGVline.FeaturePosition, this.DecimalNum);
                        oRS.StartLinePos = Math.Round(oRS.StartPoint.Y, this.DecimalNum);
                    }
                    else
                    {
                        oRS.StartPoint.X = Math.Round(ChangeLine[1].FeaturePosition, this.DecimalNum);
                        oRS.StartPoint.Y = Math.Round(ChangeLine[0].FeaturePosition, this.DecimalNum);
                        oRS.StartLinePos = Math.Round(oRS.StartPoint.Y, this.DecimalNum);
                    }

                }



                if (this.oSimDataStore.dAGVLines[oRS.AGVLineID].eFlowDir == CoordinateDirection.X_POSITIVE
                    || this.oSimDataStore.dAGVLines[oRS.AGVLineID].eFlowDir == CoordinateDirection.X_NEGATIVE)
                {
                    if (i == 0)
                    {
                        oRS.EndPoint.X = Math.Round(ChangeLine[1].FeaturePosition, this.DecimalNum);
                        oRS.EndPoint.Y = Math.Round(ChangeLine[0].FeaturePosition, this.DecimalNum);
                        oRS.EndLinePos = Math.Round(oRS.EndPoint.X);
                    }
                    else
                    {
                        oRS.EndPoint.X = Math.Round(this.oSimDataStore.dAGVLines[oAGVRoute.lRouteSegments[pos + 1].AGVLineID].FeaturePosition, this.DecimalNum);
                        oRS.EndPoint.Y = Math.Round(ChangeLine[1].FeaturePosition, this.DecimalNum);
                        oRS.EndLinePos = Math.Round(oRS.EndPoint.X);
                    }

                }
                else
                {
                    if (i == 0)
                    {
                        oRS.EndPoint.X = Math.Round(ChangeLine[0].FeaturePosition, this.DecimalNum);
                        oRS.EndPoint.Y = Math.Round(ChangeLine[1].FeaturePosition, this.DecimalNum);
                        oRS.EndLinePos = Math.Round(oRS.EndPoint.Y);
                    }
                    else
                    {
                        oRS.EndPoint.X = Math.Round(ChangeLine[1].FeaturePosition, this.DecimalNum);
                        oRS.EndPoint.Y = Math.Round(this.oSimDataStore.dAGVLines[Changebackline].FeaturePosition, this.DecimalNum);
                        oRS.EndLinePos = Math.Round(oRS.EndPoint.Y);
                    }
                }



                // RouteSegDir 赋值
                if (this.oSimDataStore.dAGVLines[oRS.AGVLineID].eFlowDir == CoordinateDirection.X_POSITIVE
                    || this.oSimDataStore.dAGVLines[oRS.AGVLineID].eFlowDir == CoordinateDirection.X_NEGATIVE)
                {
                    if (oRS.EndPoint.X > oRS.StartPoint.X)
                        oRS.eCD = CHE_Direction.East;
                    else
                        oRS.eCD = CHE_Direction.West;
                }
                else
                {
                    if (oRS.EndPoint.Y > oRS.StartPoint.Y)
                        oRS.eCD = CHE_Direction.South;
                    else
                        oRS.eCD = CHE_Direction.North;
                }

                // 修正 StartPoint and EndPoint, 并决定各 RouteSegment 的起止 RoutePos.
                if (i == 0)
                {
                    oRS.StartRoutePos = oAGVRoute.lRouteSegments[pos].EndRoutePos;
                }
                else
                {
                    oRS.StartRoutePos = oRST[0].EndRoutePos;
                }

                oRS.EndRoutePos = Math.Round(oRS.StartRoutePos + Point.Subtract(oRS.StartPoint, oRS.EndPoint).Length, this.DecimalNum);

                // 标记有 Route 经过的磁钉
                // 加入相关的 TPIDInfo 进 AGVRoute.lTPInfoEnRoutes ( LaneID 相关参数之后会补 )
                switch (oRS.eCD)
                {
                    case CHE_Direction.East:
                        lTempTPs = this.dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oRS.AGVLineID
                            && u.LogicPosX >= oRS.StartLinePos && u.LogicPosX <= oRS.EndLinePos).OrderBy(u => u.LogicPosX).ToList();
                        break;
                    case CHE_Direction.West:
                        lTempTPs = this.dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oRS.AGVLineID
                            && u.LogicPosX <= oRS.StartLinePos && u.LogicPosX >= oRS.EndLinePos).OrderByDescending(u => u.LogicPosX).ToList();
                        break;
                    case CHE_Direction.South:
                        lTempTPs = this.dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oRS.AGVLineID
                            && u.LogicPosY >= oRS.StartLinePos && u.LogicPosY <= oRS.EndLinePos).OrderBy(u => u.LogicPosY).ToList();
                        break;
                    case CHE_Direction.North:
                        lTempTPs = this.dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oRS.AGVLineID
                            && u.LogicPosY <= oRS.StartLinePos && u.LogicPosY >= oRS.EndLinePos).OrderByDescending(u => u.LogicPosY).ToList();
                        break;
                }
                if (lTempTPs.Count > 0)
                {
                    oRS.StartTPID = lTempTPs[0].ID;
                    oRS.EndTPID = lTempTPs.Last().ID;
                }
                else
                {
                    oAGVRoute.lRouteSegments[pos].EndLinePos = oRStore.EndLinePos;
                    oAGVRoute.lRouteSegments[pos].EndPoint = oRStore.EndPoint;
                    oAGVRoute.lRouteSegments[pos].EndRoutePos = oRStore.EndRoutePos;
                    return false;
                }

                lrecord.Add(i, lTempTPs);

                oRST.Add(oRS);
            }

            if (Math.Abs(oRST[0].StartLinePos - oRST[0].EndLinePos) < 20 && Math.Abs(oRST[1].StartLinePos - oRST[1].EndLinePos) < 20)
            {
                oAGVRoute.lRouteSegments[pos].EndLinePos = oRStore.EndLinePos;
                oAGVRoute.lRouteSegments[pos].EndPoint = oRStore.EndPoint;
                oAGVRoute.lRouteSegments[pos].EndRoutePos = oRStore.EndRoutePos;
                return false;
            }

            if (oRST[1].eCD != PreAGVline.eMoveDir)
            {
                oAGVRoute.lRouteSegments[pos].EndLinePos = oRStore.EndLinePos;
                oAGVRoute.lRouteSegments[pos].EndPoint = oRStore.EndPoint;
                oAGVRoute.lRouteSegments[pos].EndRoutePos = oRStore.EndRoutePos;
                return false;
            }

            /*if (oAGVRoute.lRouteSegments.Count() == pos + 3 && eCD == CHE_Direction.South)
            {
                int position_cd = 0;
                while (true)
                {
                    if (oAGVRoute.lRouteTPInfos[position_cd].TPID == oRST[0].StartTPID)
                    {
                        break;
                    }
                    position_cd++;
                }

                int count_out = oAGVRoute.lRouteTPInfos.Count() - position_cd;
                oAGVRoute.lRouteTPInfos.RemoveRange(position_cd, count_out);
                if (Changebackline != oAGVRoute.lRouteSegments[pos + 1].AGVLineID)
                {

                }
            }
            else
            {
             */
            switch (oAGVRoute.lRouteSegments[pos].eCD)
            {
                case CHE_Direction.East:
                    lTPUnUsed = this.dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oAGVRoute.lRouteSegments[pos].AGVLineID
                        && u.LogicPosX >= oAGVRoute.lRouteSegments[pos].EndLinePos).OrderBy(u => u.LogicPosX).ToList();
                    break;
                case CHE_Direction.West:
                    lTPUnUsed = this.dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oAGVRoute.lRouteSegments[pos].AGVLineID
                        && u.LogicPosX <= oAGVRoute.lRouteSegments[pos].EndLinePos).OrderByDescending(u => u.LogicPosX).ToList();
                    break;
                case CHE_Direction.South:
                    lTPUnUsed = this.dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oAGVRoute.lRouteSegments[pos].AGVLineID
                        && u.LogicPosY >= oAGVRoute.lRouteSegments[pos].EndLinePos).OrderBy(u => u.LogicPosY).ToList();
                    break;
                case CHE_Direction.North:
                    lTPUnUsed = this.dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oAGVRoute.lRouteSegments[pos].AGVLineID
                        && u.LogicPosY <= oAGVRoute.lRouteSegments[pos].EndLinePos).OrderByDescending(u => u.LogicPosY).ToList();
                    break;
            }

            oAGVRoute.lRouteSegments[pos].ChangeEndTPID(oRST[0].StartTPID);
            oAGVRoute.lRouteSegments[pos + 1].ChangeStartPoint(oRST[1].EndPoint);
            oAGVRoute.lRouteSegments[pos + 1].ChangeStartTPID(oRST[0].EndTPID);
            //double PreStartLinepos = oAGVRoute.lRouteSegments[pos + 1].StartLinePos;

            if (this.oSimDataStore.dAGVLines[oAGVRoute.lRouteSegments[pos + 1].AGVLineID].eFlowDir == CoordinateDirection.X_POSITIVE ||
                this.oSimDataStore.dAGVLines[oAGVRoute.lRouteSegments[pos + 1].AGVLineID].eFlowDir == CoordinateDirection.X_NEGATIVE)
            {
                oAGVRoute.lRouteSegments[pos + 1].ChangeStartlinePos(Math.Round(oRST[1].EndPoint.X, this.DecimalNum));
            }
            else
            {
                oAGVRoute.lRouteSegments[pos + 1].ChangeStartlinePos(Math.Round(oRST[1].EndPoint.Y, this.DecimalNum));
            }

            double POS1Recore = oAGVRoute.lRouteSegments[pos + 1].StartLinePos;
            oAGVRoute.lRouteSegments[pos + 1].ChangeStartPoint(oRST[1].EndPoint);
            oAGVRoute.lRouteSegments[pos + 1].ChangeStartlinePos(this.oSimDataStore.dAGVLines[oRST[1].AGVLineID].FeaturePosition);

            oAGVRoute.lRouteSegments[pos + 1].ChangeStartRoutePos(oRST[1].EndRoutePos);
            oAGVRoute.lRouteSegments[pos + 1].ChangeEndRoutePos(oRST[1].EndRoutePos + Math.Abs(oAGVRoute.lRouteSegments[pos + 1].EndLinePos - oAGVRoute.lRouteSegments[pos + 1].StartLinePos));
            oAGVRoute.lRouteSegments[pos + 1].ChangeID(oAGVRoute.lRouteSegments[pos + 1].ID + 2);



            switch (oAGVRoute.lRouteSegments[pos].eCD)
            {
                case CHE_Direction.East:
                    oTPUnUsed = this.dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oAGVRoute.lRouteSegments[pos + 1].AGVLineID
                        && u.LogicPosX <= oAGVRoute.lRouteSegments[pos + 1].StartLinePos).OrderBy(u => u.LogicPosX).ToList();
                    break;
                case CHE_Direction.West:
                    oTPUnUsed = this.dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oAGVRoute.lRouteSegments[pos + 1].AGVLineID
                        && u.LogicPosX >= oAGVRoute.lRouteSegments[pos + 1].StartLinePos).OrderByDescending(u => u.LogicPosX).ToList();
                    break;
                case CHE_Direction.South:
                    oTPUnUsed = this.dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oAGVRoute.lRouteSegments[pos + 1].AGVLineID
                        && u.LogicPosY <= oAGVRoute.lRouteSegments[pos + 1].StartLinePos).OrderBy(u => u.LogicPosY).ToList();
                    break;
                case CHE_Direction.North:
                    oTPUnUsed = this.dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oAGVRoute.lRouteSegments[pos + 1].AGVLineID
                        && u.LogicPosY >= oAGVRoute.lRouteSegments[pos + 1].StartLinePos).OrderByDescending(u => u.LogicPosY).ToList();
                    break;
            }

            int insertpos = 1000;
            if (lTPUnUsed.Count > 0)
            {
                foreach (SimTransponder oTP in lTPUnUsed)
                {
                    if (oTP.dRouteTPDivisions.ContainsKey(oAGV.ID))
                    {
                        oTP.dRouteTPDivisions.Remove(oAGV.ID);
                    }

                    if (oAGVRoute.lRouteTPInfos.Exists(u => u.TPID == oTP.ID))
                    {
                        for (int i = oAGVRoute.lRouteTPInfos.Count() - 1; i >= 0; i--)
                        {
                            if (oAGVRoute.lRouteTPInfos[i].TPID == oTP.ID)
                            {
                                oAGVRoute.lRouteTPInfos.RemoveAt(i);
                                if (insertpos > i)
                                {
                                    insertpos = i;
                                }
                            }
                        }
                    }
                }
            }

            if (oTPUnUsed.Count > 0)
            {
                foreach (SimTransponder oTP in oTPUnUsed)
                {
                    if (oTP.dRouteTPDivisions.ContainsKey(oAGV.ID))
                    {
                        oTP.dRouteTPDivisions.Remove(oAGV.ID);
                    }

                    if (oAGVRoute.lRouteTPInfos.Exists(u => u.TPID == oTP.ID))
                    {
                        for (int i = oAGVRoute.lRouteTPInfos.Count() - 1; i >= 0; i--)
                        {
                            if (oAGVRoute.lRouteTPInfos[i].TPID == oTP.ID)
                            {
                                oAGVRoute.lRouteTPInfos.RemoveAt(i);
                            }
                        }

                    }
                }
            }

            for (int i = 0; i < 2; i++)
            {
                foreach (SimTransponder oTP in lrecord[i])
                {
                    if (!oTP.dRouteTPDivisions.ContainsKey(oAGV.ID))
                        oTP.dRouteTPDivisions.Add(oAGV.ID, StatusEnums.RouteTPDivision.Detect);
                    if (!oAGVRoute.lRouteTPInfos.Exists(u => u.TPID == oTP.ID))
                    {
                        oAGVRoute.lRouteTPInfos.Insert(insertpos, new TPInfoEnRoute(oTP.ID, oRST[i].eCD, oRST[i].StartRoutePos, oRST[i].StartLinePos, oTP.LogicPosX, oTP.LogicPosY));
                        insertpos++;
                    }

                }
            }

            for (int i = pos + 2; i < oAGVRoute.lRouteSegments.Count(); i++)
            {
                oAGVRoute.lRouteSegments[i].ChangeID(oAGVRoute.lRouteSegments[i].ID + 2);
                oAGVRoute.lRouteSegments[i].ChangeEndRoutePos(oAGVRoute.lRouteSegments[i - 1].EndRoutePos + Math.Abs(oAGVRoute.lRouteSegments[i].EndLinePos - oAGVRoute.lRouteSegments[i].StartLinePos));
                oAGVRoute.lRouteSegments[i].ChangeStartRoutePos(oAGVRoute.lRouteSegments[i - 1].EndRoutePos);
            }

            oAGVRoute.lRouteSegments.Insert(pos + 1, oRST[0]);
            oAGVRoute.lRouteSegments.Insert(pos + 2, oRST[1]);

            oAGVRoute.lRouteTPInfos.RemoveRange(insertpos, oAGVRoute.lRouteTPInfos.Count() - insertpos);

            for (int i = pos + 3; i < oAGVRoute.lRouteSegments.Count(); i++)
            {
                List<SimTransponder> TPinRS = new List<SimTransponder>();

                RouteSegment oRS = oAGVRoute.lRouteSegments[i];

                switch (oRS.eCD)
                {
                    case CHE_Direction.East:
                        TPinRS = this.dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oRS.AGVLineID
                            && u.LogicPosX >= oRS.StartLinePos && u.LogicPosX <= oRS.EndLinePos).OrderBy(u => u.LogicPosX).ToList();
                        break;
                    case CHE_Direction.West:
                        TPinRS = this.dQCAndEssentialTPs.Values.Where(u => u.HorizontalLineID == oRS.AGVLineID
                            && u.LogicPosX <= oRS.StartLinePos && u.LogicPosX >= oRS.EndLinePos).OrderByDescending(u => u.LogicPosX).ToList();
                        break;
                    case CHE_Direction.South:
                        TPinRS = this.dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oRS.AGVLineID
                            && u.LogicPosY >= oRS.StartLinePos && u.LogicPosY <= oRS.EndLinePos).OrderBy(u => u.LogicPosY).ToList();
                        break;
                    case CHE_Direction.North:
                        TPinRS = this.dQCAndEssentialTPs.Values.Where(u => u.VerticalLineID == oRS.AGVLineID
                            && u.LogicPosY <= oRS.StartLinePos && u.LogicPosY >= oRS.EndLinePos).OrderByDescending(u => u.LogicPosY).ToList();
                        break;
                }

                foreach (SimTransponder oTP in TPinRS)
                {
                    if (!oTP.dRouteTPDivisions.ContainsKey(oAGV.ID))
                        oTP.dRouteTPDivisions.Add(oAGV.ID, StatusEnums.RouteTPDivision.Detect);
                    if (!oAGVRoute.lRouteTPInfos.Exists(u => u.TPID == oTP.ID))
                    {
                        oAGVRoute.lRouteTPInfos.Add(new TPInfoEnRoute(oTP.ID, oRS.eCD, oRS.StartRoutePos, oRS.StartLinePos, oTP.LogicPosX, oTP.LogicPosY));
                        insertpos++;
                    }
                }
            }

            oAGVRoute.TotalLength = oAGVRoute.lRouteSegments[oAGVRoute.lRouteSegments.Count() - 1].EndRoutePos;


            oAGVRoute.IsChanged = true;
            oAGVRoute.lRouteTPInfos[oAGVRoute.lRouteTPInfos.Count() - 1].EnterLaneID = this.dQCAndEssentialTPs[oAGVRoute.lRouteTPInfos[oAGVRoute.lRouteTPInfos.Count() - 1].TPID].LaneID;

            return true;
        }

        /// <summary>
        /// 从坐标到路径位置点
        /// </summary>
        /// <param name="oA">AGV对象</param>
        /// <param name="X">X</param>
        /// <param name="Y">Y</param>
        /// <param name="EnRoutePos">输出RoutePos</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool SearchForRoutePosByCoor(uint AGVID, double X, double Y, out double EnRoutePos)
        {
            bool bRet = true;
            uint LineID1, LineID2;
            double Pos1, Pos2;

            EnRoutePos = -1;

            if (this.SearchForLineIDAndPosByCoor(X, Y, out LineID1, out Pos1, out LineID2, out Pos2))
            {
                if (this.dAGVRoutes[AGVID].lRouteSegments.Exists(u => u.AGVLineID == LineID1))
                    EnRoutePos = this.dAGVRoutes[AGVID].GetRoutePosByLineAndPos(LineID1, Pos1);
                // 可能在某路段的延长线的另一路段的交点上
                if (EnRoutePos < 0 && this.dAGVRoutes[AGVID].lRouteSegments.Exists(u => u.AGVLineID == LineID2))
                    EnRoutePos = this.dAGVRoutes[AGVID].GetRoutePosByLineAndPos(LineID2, Pos2);
                if (EnRoutePos < 0)
                    bRet = false;
                else
                    EnRoutePos = Math.Round(EnRoutePos, this.DecimalNum);
            }
            else
                bRet = false;

            return bRet;
        }

        /// <summary>
        /// 从路径位置点到坐标
        /// </summary>
        /// <param name="AGVID">AGV编号</param>
        /// <param name="EnRoutePos">路径距离</param>
        /// <param name="X">输出X</param>
        /// <param name="Y">输出Y</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool SearchForCoorByRoutePos(uint AGVID, double RoutePos, out double X, out double Y)
        {
            bool bRet = true;
            uint Line1, Line2;
            double Pos1, Pos2;

            X = -1; Y = -1;

            bRet = this.dAGVRoutes[AGVID].SearchForLineAndPosByRoutePos(RoutePos, out Line1, out Pos1, out Line2, out Pos2);

            if (bRet)
            {
                if (Pos1 >= 0)
                    bRet = this.SearchForCoorByLineAndPos(Line1, Pos1, out X, out Y);
                else
                    bRet = this.SearchForCoorByLineAndPos(Line2, Pos2, out X, out Y);
            }

            X = Math.Round(X, this.DecimalNum);
            Y = Math.Round(Y, this.DecimalNum);

            return bRet;
        }


        /// <summary>
        /// 返回 AGV 在当前路径上的前进角度
        /// </summary>
        /// <param name="AGVID">AGV编号</param>
        /// <returns>车身角度。失败返回0</returns>
        private double GetAGVRouteAngle(AGV oA)
        {
            Vector vTemp;
            Vector vVeh = new Vector();
            double RouteAngle = 0;
            double X1, Y1, X2, Y2;

            List<AGVOccuLineSeg> lAOLSs = this.lAGVOccupyLineSegs.Where(u => u.AGVID == oA.ID).ToList();

            // 用向量
            if (lAOLSs.Count > 0)
            {
                foreach (AGVOccuLineSeg obj in lAOLSs)
                {
                    this.SearchForCoorByLineAndPos(obj.AGVLineID, obj.StartPos, out X1, out Y1);
                    this.SearchForCoorByLineAndPos(obj.AGVLineID, obj.EndPos, out X2, out Y2);
                    vTemp = new Vector(X2 - X1, Y2 - Y1);
                    vVeh = vVeh + vTemp;
                }
                vTemp = new Vector(1, 0);
                // 从 vTemp 到 vVeh
                RouteAngle = Vector.AngleBetween(vTemp, vVeh);
            }

            return RouteAngle;
        }


        /// <summary>
        /// 针对某一 TP 的死锁检测
        /// </summary>
        /// <returns>检测到死锁返回true，否则返回false</returns>
        private bool DeadlockDetect(uint AGVID, uint TPID)
        {
            bool IsDeadlockFound = false;
            int LoopTime = 0;
            uint uTemp;
            DateTime dtTemp;
            Dictionary<uint, List<uint>> dDetectClaimRelations;
            List<TPInfoEnRoute> lTempTPInfos;
            StrongConnectedComponentsSolver oSCCS;
            List<uint> lTemp, lSelect;

            dDetectClaimRelations = new Dictionary<uint, List<uint>>();

            // 归纳路径阻碍关系
            foreach (uint AgvID in this.dAGVRoutes.Keys)
            {
                // Detect 拎出
                lTempTPInfos = this.dAGVRoutes[AgvID].lRouteTPInfos.Where(u => u.eRouteTPDivision == StatusEnums.RouteTPDivision.Detect).ToList();

                foreach (TPInfoEnRoute oTPInfo in lTempTPInfos)
                {
                    if (this.dQCAndEssentialTPs[oTPInfo.TPID].dRouteTPDivisions.Count > 1
                        && this.dQCAndEssentialTPs[oTPInfo.TPID].dRouteTPDivisions.Values.Any(u => u == StatusEnums.RouteTPDivision.Claim))
                    {
                        uTemp = this.dQCAndEssentialTPs[oTPInfo.TPID].dRouteTPDivisions.Keys.First(u => this.dQCAndEssentialTPs[oTPInfo.TPID].dRouteTPDivisions[u] == StatusEnums.RouteTPDivision.Claim);

                        if (this.dAGVRoutes[uTemp].lRouteTPInfos.First(u => u.TPID == oTPInfo.TPID).IsUnSurpassable)
                        {
                            if (!dDetectClaimRelations.ContainsKey(AgvID))
                                dDetectClaimRelations.Add(AgvID, new List<uint>());
                            if (!dDetectClaimRelations[AgvID].Contains(uTemp))
                                dDetectClaimRelations[AgvID].Add(uTemp);
                        }
                    }
                }
            }

            // 如果关系矩阵中某 AGV 的入度或出度为零，则舍弃。注意该步可能循环
            lTemp = new List<uint>();
            do
            {
                // 检查是否有出入为零的点
                lSelect = new List<uint>();
                foreach (uint uKey in dDetectClaimRelations.Keys)
                {
                    if (dDetectClaimRelations[uKey].Count == 0 || !dDetectClaimRelations.Values.Any(u => u.Contains(uKey)))
                    {
                        if (!lSelect.Contains(uKey))
                            lSelect.Add(uKey);
                    }
                }

                if (lSelect.Count == 0)
                    break;

                lTemp.AddRange(lSelect);

                foreach (uint uLoopInd in lSelect)
                {
                    if (dDetectClaimRelations.ContainsKey(uLoopInd))
                        dDetectClaimRelations.Remove(uLoopInd);
                    foreach (uint uKey in dDetectClaimRelations.Keys)
                    {
                        if (dDetectClaimRelations[uKey].Contains(uLoopInd))
                            dDetectClaimRelations[uKey].Remove(uLoopInd);
                    }
                }

                LoopTime++;
            }
            while (LoopTime < 50);

            if (dDetectClaimRelations.Count < 2)
                return false;
            else
                this.DeadlockDetectTime++;

            dtTemp = DateTime.Now;

            if (this.IsDeadLockDetectInputLog)
            {
                this.PrintOutInputsOfFullCycleDetect(AGVID, TPID);
                this.PrintInputsOfSolver(dDetectClaimRelations, lTemp);
            }
                
            oSCCS = new StrongConnectedComponentsSolver();
            oSCCS.Init(dDetectClaimRelations);
            if (oSCCS.eSolverStatus != SolverEnums.SolverStatus.Inited)
                Console.WriteLine("");
            oSCCS.Solve();

            IsDeadlockFound = false;
            foreach (Dictionary<uint, List<uint>> oSCC in oSCCS.lStrongConnComponents)
            {
                if (oSCC.Count > 1)
                {
                    IsDeadlockFound = true;
                    break;
                }
            }

            if (this.IsDeadLockDetectOutputLog)
                this.PrintOutputsOfSolver(IsDeadlockFound, oSCCS.lStrongConnComponents);

            this.lDeadlockDetectTimes.Add(new TimeSpan(DateTime.Now.Ticks).Subtract(new TimeSpan(dtTemp.Ticks)).TotalSeconds);

            return IsDeadlockFound;
        }

        /// <summary>
        /// 更新某 AGV 路径上 TPInfoEnRoute 的 IsClaimSurpassable 属性
        /// </summary>
        /// <param name="AGVID"></param>
        private void RenewSurpassableClaimTPs(AGV oA, double NewClaimPos)
        {
            List<TPInfoEnRoute> lTempTPInfos;

            this.dAGVRoutes[oA.ID].CurrClaimLength = NewClaimPos;

            lTempTPInfos = this.dAGVRoutes[oA.ID].lRouteTPInfos.Where(u => u.IsUnSurpassable == true).ToList();

            if (lTempTPInfos.Count > 0)
                lTempTPInfos.ForEach(u => u.IsUnSurpassable = false);

            lTempTPInfos = this.dAGVRoutes[oA.ID].lRouteTPInfos.Where(u => u.RoutePos <= this.dAGVRoutes[oA.ID].CurrClaimLength 
                && u.RoutePos >= this.dAGVRoutes[oA.ID].CurrClaimLength - oA.oType.Length - this.CompelAGVIntvToTP 
                && u.eRouteTPDivision == StatusEnums.RouteTPDivision.Claim).ToList();

            lTempTPInfos.ForEach(u => u.IsUnSurpassable = true);
        }

        /// <summary>
        /// 刷新移动的AGV列表及其移动顺序
        /// </summary>
        private void RenewMovingAGVsAndSeqs(double TimeLength)
        {
            Dictionary<uint, List<uint>> dNearFrontAGVIDs = new Dictionary<uint, List<uint>>();
            double CurrRoutePos, HeadRoutePos, MaxRoutePos, TempPos;

            this.lMovingAGVs = this.oSimDataStore.dAGVs.Values.Where(u => this.dAGVRoutes.ContainsKey(u.ID)).ToList();
            this.lMovingAGVs.ForEach(u => dNearFrontAGVIDs.Add(u.ID, new List<uint>()));

            foreach (AGV oA in this.lMovingAGVs)
            {
                this.SearchForRoutePosByCoor(oA.ID, oA.MidPoint.X, oA.MidPoint.Y, out CurrRoutePos);
                HeadRoutePos = CurrRoutePos + oA.oType.Length / 2;
                MaxRoutePos = HeadRoutePos + this.dAGVRoutes[oA.ID].CurrClaimLength;

                foreach (AGVOccuLineSeg oAOLS in this.lAGVOccupyLineSegs)
                {
                    if (oAOLS.AGVID != oA.ID && !(oAOLS.bStartPointHinge && oAOLS.bEndPointHinge))
                    {
                        TempPos = -1;
                        if (!oAOLS.bStartPointHinge)
                        {
                            TempPos = this.dAGVRoutes[oA.ID].GetRoutePosByLineAndPos(oAOLS.AGVLineID, oAOLS.StartPos);
                            if (TempPos >= 0 && TempPos > HeadRoutePos && TempPos <= MaxRoutePos)
                                if (!dNearFrontAGVIDs[oA.ID].Contains(oAOLS.AGVID))
                                    dNearFrontAGVIDs[oA.ID].Add(oAOLS.AGVID);
                        }
                        if (!oAOLS.bEndPointHinge)
                        {
                            TempPos = this.dAGVRoutes[oA.ID].GetRoutePosByLineAndPos(oAOLS.AGVLineID, oAOLS.EndPos);
                            if (TempPos >= 0 && TempPos > HeadRoutePos && TempPos <= MaxRoutePos)
                                if (!dNearFrontAGVIDs[oA.ID].Contains(oAOLS.AGVID))
                                    dNearFrontAGVIDs[oA.ID].Add(oAOLS.AGVID);
                        }
                    }
                }
            }

            this.lMovingAGVs.Sort((u1, u2) => u1.Equals(u2)? 0 : (dNearFrontAGVIDs[u1.ID].Contains(u2.ID) ? 1 : -1));
        }

        // 一元两次方程组求解器
        private bool QuadraticEquationWithOneUnknownSolver(double a, double b, double c, out double x1, out double x2)
        {
            bool bRet = true;
            double KeyValue;

            x1 = -1; x2 = -1;
            KeyValue = b * b - 4 * a * c;
            if (KeyValue < 0)
                bRet = false;
            else
            {
                x1 = Math.Round((-1 * b - Math.Sqrt(KeyValue)) / (2 * a), this.DecimalNum);
                x2 = Math.Round((-1 * b + Math.Sqrt(KeyValue)) / (2 * a), this.DecimalNum);
            }
            return bRet;
        }

        /// <summary>
        /// 解释用，打出 AGV 预约磁钉时的输入
        /// </summary>
        /// <param name="AGVID">预约 AGV 编号</param>
        /// <param name="TPID">磁钉编号</param>
        private void PrintOutInputsOfFullCycleDetect(uint AGVID, uint TPID)
        {
            uint ResvAGVID;

            FileStream fs = new FileStream(System.Environment.CurrentDirectory +
                "\\PrintOut\\FullCycleDetectInputs\\FullCycleDetectInputs_" + this.DeadlockDetectTime.ToString().PadLeft(8, '0') + ".txt", FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, Encoding.Default);

            if (this.DeadlockDetectTime >= this.TPReclaimRecordNum
                && File.Exists(System.Environment.CurrentDirectory + "\\PrintOut\\FullCycleDetectInputs\\FullCycleDetectInputs_" 
                + (this.DeadlockDetectTime - this.TPReclaimRecordNum + 1).ToString().PadLeft(8, '0') + ".txt"))
            File.Delete(System.Environment.CurrentDirectory + "\\PrintOut\\FullCycleDetectInputs\\FullCycleDetectInputs_" 
                + (this.DeadlockDetectTime - this.TPReclaimRecordNum + 1).ToString().PadLeft(8, '0') + ".txt");

            sw.WriteLine(DateTime.Now.ToString());
            sw.WriteLine("\r\nAGV : " + AGVID.ToString() + " Reserve Transponder ID : " + TPID.ToString());

            // Route.
            // 点X注释：+X 表示路径交点，X= 表示已过点，X- 表示未占点，X| 表示已占点，X|| 表示已占的终到点
            sw.WriteLine("\r\n\r\nAGVRoutes :");

            foreach (AGVRoute oAR in this.dAGVRoutes.Values)
            {
                sw.WriteLine("\r\nAGV : " + oAR.AGVID.ToString() + "\tLength : " + oAR.TotalLength.ToString() + "\tClaimedLength : " + oAR.CurrClaimLength);
                sw.Write("Lanes : ");
                foreach (uint LaneID in oAR.lRouteLaneIDs)
                {
                    sw.Write("\t" + LaneID.ToString());
                }
                sw.Write("\r\nTransponders : ");
                foreach (TPInfoEnRoute oTPInfo in oAR.lRouteTPInfos)
                {
                    sw.Write("\t");
                    // 区分 Passed = 、Claim | 、Claim终到 || 和 Detect -
                    if (this.dQCAndEssentialTPs[oTPInfo.TPID].dRouteTPDivisions.Count > 1)
                        sw.Write("+");
                    sw.Write(oTPInfo.TPID.ToString());
                    switch (oTPInfo.eRouteTPDivision)
                    {
                        case StatusEnums.RouteTPDivision.Passed:
                            sw.Write("=");
                            break;
                        case StatusEnums.RouteTPDivision.Claim:
                            {
                                if (oTPInfo.IsUnSurpassable)
                                    sw.Write("||");
                                else
                                    sw.Write("|");
                            }
                            break;
                        case StatusEnums.RouteTPDivision.Detect:
                            sw.Write("-");
                            break;
                        default:
                            break;
                    }
                }
                sw.WriteLine("");
            }

            // Intersected Or Reserved Transponder
            sw.WriteLine("\r\n\r\nIntersected and Reserved TPIDs :");

            foreach (SimTransponder oTP in this.dQCAndEssentialTPs.Values)
            {
                ResvAGVID = oTP.dRouteTPDivisions.Keys.FirstOrDefault(u => oTP.dRouteTPDivisions[u] == StatusEnums.RouteTPDivision.Claim);

                if (oTP.dRouteTPDivisions.Count > 1)
                {
                    if (ResvAGVID > 0)
                        sw.WriteLine("\r\nTP ID : {0};\tResv AGV : {1};\tIntersected", oTP.ID, ResvAGVID);
                    else
                        sw.WriteLine("\r\nTP ID : {0};\tNot Reserved;\tIntersected", oTP.ID);
                }
                else if (oTP.dRouteTPDivisions.Count == 1 && ResvAGVID > 0)
                    sw.WriteLine("\r\nTP ID : {0};\tResv AGV : {1}", oTP.ID, ResvAGVID);
                else
                    continue;

                sw.Write("RouteAGVs (Passed): ");
                foreach (uint AgvId in oTP.dRouteTPDivisions.Keys)
                {
                    if (oTP.dRouteTPDivisions[AgvId] == StatusEnums.RouteTPDivision.Passed)
                        sw.Write("\t(" + AgvId.ToString() + ")");                
                    else
                        sw.Write("\t" + AgvId.ToString());
                }

                sw.WriteLine("");
            }
            
            sw.Close();
            fs.Close();
        }

        /// <summary>
        /// 解释用，打出强连通分量求解器的输入
        /// </summary>
        /// <param name="dDirGraph">关系图输入</param>
        private void PrintInputsOfSolver(Dictionary<uint, List<uint>> dDirGraph, List<uint> lZeroInOut)
        {
            FileStream fs = new FileStream(System.Environment.CurrentDirectory +
                "\\PrintOut\\SolverInputs\\SolverInput_" + this.DeadlockDetectTime.ToString().PadLeft(8, '0') + ".txt", FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, Encoding.Default);

            if (this.DeadlockDetectTime >= this.TPReclaimRecordNum
                && File.Exists(System.Environment.CurrentDirectory + "\\PrintOut\\SolverInputs\\SolverInput_"
                    + (this.DeadlockDetectTime - this.TPReclaimRecordNum + 1).ToString().PadLeft(8, '0') + ".txt"))
                File.Delete(System.Environment.CurrentDirectory + "\\PrintOut\\SolverInputs\\SolverInput_"
                    + (this.DeadlockDetectTime - this.TPReclaimRecordNum + 1).ToString().PadLeft(8, '0') + ".txt");

            sw.WriteLine(DateTime.Now.ToString() + "\r\n");

            // Directed Graph in Cycle Seg IDs
            sw.WriteLine("Input Relation Graph in AGV IDs :\r\n");
            foreach (uint i in dDirGraph.Keys)
            {
                sw.Write("Detect AGV: " + i.ToString() + "\t");
                foreach (uint j in dDirGraph[i])
                    sw.Write("Claim AGV: " + j.ToString() + "\t");
                sw.Write("\r\n");
            }
            sw.Write("\r\n\r\n");

            // Zero In Or Zero Out Nodes(AGVs)
            sw.WriteLine("Zero In Or Zero Out Nodes in AGV IDs :\r\n");
            foreach (uint uTemp in lZeroInOut)
            {
                sw.Write("\t" + uTemp.ToString());
            }
            sw.Write("\r\n\r\n");

            sw.Close();
            fs.Close();
        }

        /// <summary>
        /// 解释用，打出强连通分量求解器的输出
        /// </summary>
        /// <param name="lRemovedSCCs">输出的强连通分量</param>
        private void PrintOutputsOfSolver(bool IsDeadlockFound, List<Dictionary<uint, List<uint>>> lOutPutSCCs)
        {
            FileStream fs = new FileStream(System.Environment.CurrentDirectory +
                "\\PrintOut\\SolverOutputs\\SolverOutput_" + this.DeadlockDetectTime.ToString().PadLeft(8, '0') + ".txt", FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, Encoding.Default);

            if (this.DeadlockDetectTime >= this.TPReclaimRecordNum
                && File.Exists(System.Environment.CurrentDirectory + "\\PrintOut\\SolverOutputs\\SolverOutput_"
                    + (this.DeadlockDetectTime - this.TPReclaimRecordNum + 1).ToString().PadLeft(8, '0') + ".txt"))
                File.Delete(System.Environment.CurrentDirectory + "\\PrintOut\\SolverOutputs\\SolverOutput_"
                    + (this.DeadlockDetectTime - this.TPReclaimRecordNum + 1).ToString().PadLeft(8, '0') + ".txt");


            sw.WriteLine(DateTime.Now.ToString() + "\r\n");

            sw.WriteLine("IsDeadlockFound :" + IsDeadlockFound.ToString() + "\r\n");

            // 强连通分量
            sw.WriteLine("Strong Connect Components in Cycle Seg IDs :\r\n");

            foreach (Dictionary<uint, List<uint>> dSCC in lOutPutSCCs)
            {
                sw.WriteLine("SCC ID :" + lOutPutSCCs.IndexOf(dSCC).ToString());
                foreach (uint i in dSCC.Keys)
                {
                    sw.Write("Detect AGV: " + i.ToString() + "\t");
                    foreach (uint j in dSCC[i])
                        sw.Write("Claim AGV: " + j.ToString() + "\t");
                    sw.Write("\r\n");
                }
                sw.Write("\r\n");
            }
            sw.Write("\r\n\r\n");

            sw.Close();
            fs.Close();
        }

        /// <summary>
        /// 死锁检测判断
        /// </summary>
        /// <returns>AGV全部停止返回true，否则返回false</returns>
        private bool CheckIfAllStopped()
        {
            bool bRet = false;

            if (this.dAGVRoutes.Count == this.oSimDataStore.dAGVs.Count 
                && this.lMovingAGVs.All(u => u.eMotionStatus == StatusEnums.MotionStatus.Moving && u.eStepTravelStatus == StatusEnums.StepTravelStatus.Wait))
                bRet = true;

            return bRet;
        }

        /// <summary>
        /// 车距检测判断
        /// </summary>
        /// <returns>两车过近返回true，否则返回false</returns>
        private bool CheckIfTooClose(out uint AgvID1, out uint AgvID2)
        {
            bool bRet = false;
            double Intv;
            AgvID1 = 0;
            AgvID2 = 0;
            List<uint> lAGVIDs = this.dAGVRoutes.Where(u => !u.Value.lRouteLaneIDs.Exists(v =>
                this.oSimDataStore.dLanes[v].eStatus == LaneStatus.OCCUPIED || this.oSimDataStore.dLanes[v].eStatus == LaneStatus.PASSTHROUGH)).Select(u => u.Key).ToList();

            foreach (uint i in lAGVIDs)
            {
                Intv = this.CompelAGVIntvToAGV;
                foreach (uint j in lAGVIDs)
                {
                    if (i != j 
                        && Intv >= this.GetManhattanDistance(this.oSimDataStore.dAGVs[i].MidPoint, this.oSimDataStore.dAGVs[j].MidPoint))
                    {
                        bRet = true;
                        AgvID1 = i;
                        AgvID2 = j;
                        break;
                    }
                }
            }

            return bRet;
        }

        /// <summary>
        /// 检测是否有最远 Claim 点的 IsUnSurpassable 为 false
        /// </summary>
        /// <returns></returns>
        private bool IsUnSurpassableTPToClaimEndTest(out List<uint> lErrorAGVIDList)
        {
            lErrorAGVIDList = new List<uint>();
            TPInfoEnRoute oTPInfo;
            bool bRet = true;

            foreach (AGVRoute oAR in this.dAGVRoutes.Values)
            {
                if (oAR.lRouteTPInfos.Exists(u => u.eRouteTPDivision == StatusEnums.RouteTPDivision.Claim))
                {
                    oTPInfo = oAR.lRouteTPInfos.LastOrDefault(u => u.eRouteTPDivision == StatusEnums.RouteTPDivision.Claim);
                    if (!oTPInfo.IsUnSurpassable)
                    {
                        lErrorAGVIDList.Add(oAR.AGVID);
                        bRet = false;
                    }
                }
            }

            return bRet;
        }


        #endregion

    }
}
