using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using ZECS.Schedule.DBDefine.YardMap;
using SSWPF.Define.SimClasses;
using SSWPF.Define;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using SharpSim;

namespace SSWPF.Define.SimManagers
{
    public class SimTrafficController
    {
        public SimDataStore oSimDataStore;
        private List<SimTransponder> lIntersectedTPs;
        private Dictionary<uint, CycleSeg> dCycleSegs;
        public Dictionary<uint, AGVLine> dEssentialAGVLines;
        public Dictionary<uint, SimTransponder> dEssentialAndQCTPs;
        private Dictionary<uint, AGVRoute> dAGVRoutes;
        private Dictionary<uint, double> dAGVRouteAngles;
        private List<AGVOccuLineSeg> lAGVOccupyLineSegs;
        private List<AGV> lMovingAGVs;
        AGVOccuLineSeg oAOLS;
        AGVLine oAL;
        private bool IfMapInited, IfRouteFeatureExpired;
        // 必须距磁钉2米（除非约到），必须距前车3米，每个AGV相当于20米距离
        private readonly double CompelAGVIntvToTP, CompelAGVIntvToAGV, AGVEquivalentLength;
        private readonly int DecimalNum;
        
        // For Debug Use
        private FileStream fs_Routing, fs_AgvResvRelease, fs_Search;
        private StreamWriter sw_Routing, sw_AgvResvRelease, sw_Search;
        private long FullCycleDetectTime, TPReclaimTime;
        private int CurrCSVNum, CSVReserveNumInOneRecord, TPReclaimRecordNum, CSVRecordNum;
        private List<long> lFullCycleDetectRecords = new List<long>();
        private bool IfRouteLog, IfStrongComponentLog, IfDeadLockDetectLog, IfSelfCheckAndThrowException;
        public List<double> lCycleDetectTimeLengths = new List<double>();
        private SimException DeadLockExpection = new SimException("DeadLock Detected");

        // 强制保留的 AGVLine 集合，主干道，可能没有 LaneID
        private List<uint> lKeptLines = new List<uint> { 38, 39, 40, 41, 42, 43, 346, 351, 356, 376, 381, 386 };

        public SimTrafficController(int DecimalNum = 2)
        {
            this.IfMapInited = false;
            this.IfRouteLog = false;
            this.IfStrongComponentLog = false;
            this.IfDeadLockDetectLog = false;
            this.lAGVOccupyLineSegs = new List<AGVOccuLineSeg>();
            this.CompelAGVIntvToTP = 2;
            this.CompelAGVIntvToAGV = 3;
            this.AGVEquivalentLength = 20;
            this.DecimalNum = DecimalNum;

            this.CurrCSVNum = -1;
            this.CSVReserveNumInOneRecord = 3000;
            this.TPReclaimRecordNum = 10000;
            this.CSVRecordNum = 3;
        }

        public SimTrafficController(SimDataStore oSDS)
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
            List<SimTransponder> lQCTPTs;

            if (this.IfMapInited || this.oSimDataStore == null) return false;

            this.dAGVRoutes = new Dictionary<uint, AGVRoute>();
            this.dAGVRouteAngles = new Dictionary<uint, double>();
            this.dCycleSegs = new Dictionary<uint, CycleSeg>();
            this.IfRouteFeatureExpired = false;

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
            if (Directory.Exists(System.Environment.CurrentDirectory + "\\PrintOut\\SearchProcesses"))
                Directory.Delete(System.Environment.CurrentDirectory + "\\PrintOut\\SearchProcesses", true);
            if (Directory.Exists(System.Environment.CurrentDirectory + "\\PrintOut\\TPActions"))
                Directory.Delete(System.Environment.CurrentDirectory + "\\PrintOut\\TPActions", true);
            Directory.CreateDirectory(System.Environment.CurrentDirectory + "\\PrintOut\\FullCycleDetectInputs");
            Directory.CreateDirectory(System.Environment.CurrentDirectory + "\\PrintOut\\SearchProcesses");
            Directory.CreateDirectory(System.Environment.CurrentDirectory + "\\PrintOut\\TPActions");

            // 理出必要 AGVLine
            this.dEssentialAGVLines = new Dictionary<uint, AGVLine>();

            foreach (AGVLine oAL in this.oSimDataStore.dAGVLines.Values)
            {
                if (oAL.lLaneIDs.Count > 0 || this.lKeptLines.Contains(oAL.ID))
                {
                    oAL.bIfEssential = true;
                    this.dEssentialAGVLines.Add(oAL.ID, oAL);
                }
            }

            // 理出必要磁钉并且标记
            this.dEssentialAndQCTPs = this.oSimDataStore.dTransponders.Values.Where(u => u.LaneID > 0 || u.MateID > 0
                || (this.dEssentialAGVLines.ContainsKey(u.HorizontalLineID) && this.dEssentialAGVLines.ContainsKey(u.VerticalLineID))).ToDictionary(u => u.ID);
            this.dEssentialAndQCTPs.Values.Where(u => this.dEssentialAGVLines.ContainsKey(u.HorizontalLineID)
                && this.dEssentialAGVLines.ContainsKey(u.VerticalLineID)).ToList().ForEach(u => u.bIfEssential = true);
            lQCTPTs = this.oSimDataStore.dTransponders.Values.Where(u => u.HorizontalLineID >= 44 && u.HorizontalLineID <= 47).ToList();
            foreach (SimTransponder oTP in lQCTPTs)
            {
                if (!this.dEssentialAndQCTPs.ContainsKey(oTP.ID))
                    this.dEssentialAndQCTPs.Add(oTP.ID, oTP);
            }

            // AGVLines 相互连接
            foreach (SimTransponder oTP in this.dEssentialAndQCTPs.Values)
            {
                if (this.dEssentialAGVLines.Keys.Contains(oTP.HorizontalLineID) && this.dEssentialAGVLines.Keys.Contains(oTP.VerticalLineID))
                {
                    this.dEssentialAGVLines[oTP.HorizontalLineID].lLineIDs.Add(oTP.VerticalLineID);
                    this.dEssentialAGVLines[oTP.VerticalLineID].lLineIDs.Add(oTP.HorizontalLineID);
                }
            }

            // 至少包含两个 Lane 和一个 AGVLine
            if (this.dEssentialAndQCTPs.Count < 4 && this.dEssentialAGVLines.Count == 0)
            {
                Logger.Simulate.Error("Traffic Controller Initialization Failed For No Enough Transponders Or AGVLines");
                return false;
            }

            // AGV 占据 AGVLine，并且初始化 AGV 的车辆角度。
            // 注意保证各 AgvOccuLineSeg 在 AGVLine 上的特征位置 StartPos 和 EndPos 的严格大小关系
            foreach (uint iKey in this.oSimDataStore.dAGVs.Keys)
            {
                oAL = this.dEssentialAGVLines.Values.First(u => u.lLaneIDs.Contains(this.oSimDataStore.dAGVs[iKey].CurrLaneID));

                // 占据 LineSeg
                oAOLS = new AGVOccuLineSeg() { AGVID = iKey, AGVLineID = oAL.ID };
                if (oAL.Dir == CoordinateDirection.X_POSITIVE || oAL.Dir == CoordinateDirection.X_NEGATIVE)
                {
                    oAOLS.StartPos = this.oSimDataStore.dAGVs[iKey].MidPoint.X - this.oSimDataStore.dAGVs[iKey].oType.Length / 2;
                    oAOLS.EndPos = this.oSimDataStore.dAGVs[iKey].MidPoint.X + this.oSimDataStore.dAGVs[iKey].oType.Length / 2;
                    this.dAGVRouteAngles.Add(iKey, 0);
                }
                else if (oAL.Dir == CoordinateDirection.Y_NEGATIVE || oAL.Dir == CoordinateDirection.Y_POSITIVE)
                {
                    oAOLS.StartPos = this.oSimDataStore.dAGVs[iKey].MidPoint.Y - this.oSimDataStore.dAGVs[iKey].oType.Length / 2;
                    oAOLS.EndPos = this.oSimDataStore.dAGVs[iKey].MidPoint.Y + this.oSimDataStore.dAGVs[iKey].oType.Length / 2;
                    this.dAGVRouteAngles.Add(iKey, 90);
                }
                oAOLS.bStartPointHinge = false;
                oAOLS.bEndPointHinge = false;
                this.lAGVOccupyLineSegs.Add(oAOLS);

                // 占据磁钉
                List<SimTransponder> lTPs = this.dEssentialAndQCTPs.Values.Where(u => oAL.lTPIDs.Contains(u.ID)).ToList();
                if (oAL.Dir == CoordinateDirection.X_POSITIVE || oAL.Dir == CoordinateDirection.X_NEGATIVE)
                    lTPs = lTPs.Where(u => u.LogicPosX >= oAOLS.StartPos && u.LogicPosX <= oAOLS.EndPos).ToList();
                else
                    lTPs = lTPs.Where(u => u.LogicPosY >= oAOLS.StartPos && u.LogicPosY <= oAOLS.EndPos).ToList();

                lTPs.ForEach(u => u.ResvAGVID = iKey);
            }

            this.IfMapInited = true;

            return true;
        }

        /// <summary>
        /// 有调试输出的初始化
        /// </summary>
        /// <param name="bRouteLog">是否打印路径</param>
        /// <param name="IfStrongCommLog">是否打印强连通分量</param>
        /// <param name="IfDeadLockDetectLog">是否打印路径段、搜索过程和路径段更新</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool Init(bool bRouteLog = false, bool IfStrongCommLog = false, bool IfDeadLockDetectLog = false, bool IfSelfCheck = true)
        {
            bool bRet = false;
            
            this.IfRouteLog = bRouteLog;
            this.IfDeadLockDetectLog = IfDeadLockDetectLog;
            this.IfStrongComponentLog = IfStrongCommLog;
            this.IfSelfCheckAndThrowException = IfSelfCheck;
            if (!this.IfMapInited) 
                bRet = this.Init();
            return bRet;
        }

        #region AGV路径生成

        /// <summary>
        /// 针对AGV生成路径。注意 AGV 的 .AimLaneID 和 .CurrLaneID 必须有合法值
        /// </summary>
        /// <param name="oA">AGV对象</param>
        /// <returns>路径生成成功返回true，失败返回false</returns>
        public bool GenerateAGVRouteToGivenLane(AGV oA, ref ProjectPackageToViewFrame oPPTViewFrame)
        {
            bool bRet = true;
            string Flag = "";
            List<uint> lAGVLineIDs = new List<uint>();
            List<Lane> lLanes = new List<Lane>();
            List<Lane> lTempLanes1 = new List<Lane>();
            List<Lane> lTempLanes2 = new List<Lane>();
            List<SimTransponder> lTempTPs = new List<SimTransponder>();
            AGVRoute oAGVRoute;
            RouteSegment oRS;
            List<TPInfoEnRoute> lTPInfoERs;
            uint AGVLineID;

            if (oA.CurrLaneID == 0 || !this.oSimDataStore.dLanes.ContainsKey(oA.CurrLaneID))
            {
                bRet = false;
                Flag = "Invalid Orientation LaneID : " + oA.CurrLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " when Searching Route for AGV : " + oA.ID.ToString();
            }
            else if (oA.AimLaneID == 0 || !this.oSimDataStore.dLanes.ContainsKey(oA.AimLaneID) || this.oSimDataStore.dLanes[oA.AimLaneID].eStatus != LaneStatus.IDLE)
            {
                bRet = false;
                Flag = "Invalid Destination LaneID : " + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " when Searching Route for AGV : " + oA.ID.ToString();
            }
            else if (this.oSimDataStore.dLanes[oA.CurrLaneID].eType != AreaType.STS_PB && this.oSimDataStore.dLanes[oA.CurrLaneID].eType != AreaType.STS_TP
                && this.oSimDataStore.dLanes[oA.CurrLaneID].eType != AreaType.WS_PB && this.oSimDataStore.dLanes[oA.CurrLaneID].eType != AreaType.WS_TP)
            {
                bRet = false;
                Flag = "UnExpected Lane Type : " + this.oSimDataStore.dLanes[oA.CurrLaneID].eType.ToString() + " of CurrLane when searching Route for AGV : " + oA.ID.ToString();
            }
            else if (this.oSimDataStore.dLanes[oA.AimLaneID].eType != AreaType.STS_PB && this.oSimDataStore.dLanes[oA.AimLaneID].eType != AreaType.STS_TP
                && this.oSimDataStore.dLanes[oA.AimLaneID].eType != AreaType.WS_PB && this.oSimDataStore.dLanes[oA.AimLaneID].eType != AreaType.WS_TP)
            {
                bRet = false;
                Flag = "UnExpected Lane Type : " + this.oSimDataStore.dLanes[oA.AimLaneID].eType.ToString() + " of AimLane when searching Route for AGV : " + oA.ID.ToString();
            }

            // 先收集经过的 AGVLine 集合，再补充车道和磁钉
            // 起止于同一类车道的直接路径（没有Lane间隔的路径），应选择较远的 AGVLine，以避免车辆运动半径明显小于转弯半径的情况
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
                                        lAGVLineIDs.Add(this.GetOneDirLineIDWithMinRouteIntvInEvenRate(oA.oType.Length - Math.Abs(this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y - this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y),
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.AimLaneID].TPIDEnd].PhysicalPosX, CoordinateDirection.Y_POSITIVE));
                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
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
                                            lLanes.Add(lTempLanes1[0]);
                                            lLanes.Add(lTempLanes2[0]);
                                            if (lTempLanes1[0].pWork.Y != this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y)
                                            {
                                                lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_POSITIVE));
                                                lAGVLineIDs.Add(lTempLanes1[0].LineID);
                                            }
                                            // 两个 WSPB 之间的道路长度要控制
                                            lAGVLineIDs.Add(this.GetOneDirLineIDWithMinRouteIntvInEvenRate(oA.oType.Length - Math.Abs(lTempLanes1[0].pWork.Y - lTempLanes2[0].pWork.Y),
                                                this.oSimDataStore.dTransponders[lTempLanes1[0].TPIDEnd].PhysicalPosX, CoordinateDirection.Y_NEGATIVE));
                                            if (lTempLanes2[0].pWork.Y != this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y)
                                            {
                                                lAGVLineIDs.Add(lTempLanes2[0].LineID);
                                                lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_POSITIVE));
                                            }
                                            lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                            lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                        }
                                        else
                                        {
                                            bRet = false;
                                            Flag = "Generate Route from WSTP Lane : " + oA.CurrLaneID.ToString() + " to WSTP Lane : " + oA.AimLaneID.ToString()
                                                + " of AGV : " + oA.ID.ToString() + " Failed for no WSPB";
                                        }
                                    }
                                    break;
                                case AreaType.WS_PB:
                                    // From WSTP to WSPB
                                    if (this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.AimLaneID].LineID].FeaturePosition
                                        > this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].FeaturePosition)
                                    {
                                        // 在下方，加一个向下的单行道即可
                                        lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_POSITIVE));
                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    }
                                    else if (this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.AimLaneID].LineID].FeaturePosition
                                        < this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].FeaturePosition)
                                    {
                                        // 在上方，要垫一个靠下的PB绕过去
                                        lTempLanes1 = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.WS_PB && u.eStatus == LaneStatus.IDLE
                                            && u.pWork.Y >= this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y).OrderBy(u => u.pWork.Y).ToList();

                                        if (lTempLanes1.Count > 0)
                                        {
                                            lLanes.Add(lTempLanes1[0]);
                                            if (lTempLanes1[0].pWork.Y != this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y)
                                            {
                                                lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_POSITIVE));
                                                lAGVLineIDs.Add(lTempLanes1[0].LineID);
                                            }
                                            lAGVLineIDs.Add(this.GetOneDirLineIDWithMinRouteIntvInEvenRate(oA.oType.Length - (lTempLanes1[0].pWork.Y - this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y),
                                                this.oSimDataStore.dTransponders[lTempLanes1[0].TPIDEnd].LogicPosX, CoordinateDirection.Y_NEGATIVE));
                                            lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                            lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                        }
                                        else
                                        {
                                            bRet = false;
                                            Flag = "Generate Route from WSTP Lane : " + oA.CurrLaneID.ToString() + " to WSPB Lane : " + oA.AimLaneID.ToString()
                                                + " of AGV : " + oA.ID.ToString() + " Failed for no WSPB Found";
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
                                    if (!this.oSimDataStore.dOneDirLineSegments.ContainsKey(this.oSimDataStore.dLanes[oA.AimLaneID].LineID)
                                        || this.oSimDataStore.dOneDirLineSegments[this.oSimDataStore.dLanes[oA.AimLaneID].LineID].Dir != CoordinateDirection.Y_POSITIVE)
                                    {
                                        if (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X < this.oSimDataStore.dAGVLines[346].FeaturePosition)
                                        {
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_POSITIVE));
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.X_NEGATIVE));
                                        }
                                        else
                                        {
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_POSITIVE));
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.X_POSITIVE));
                                        }
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
                                        lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_POSITIVE));
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
                                            lLanes.Add(lTempLanes1[0]);
                                            lAGVLineIDs.Add(this.GetOneDirLineIDWithMinRouteIntvInEvenRate(oA.oType.Length - (this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y - lTempLanes1[0].pWork.Y),
                                                this.oSimDataStore.dTransponders[lTempLanes1[0].TPIDEnd].LogicPosX, CoordinateDirection.Y_NEGATIVE));
                                            if (lTempLanes1[0].pWork.Y != this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y)
                                            {
                                                lAGVLineIDs.Add(lTempLanes1[0].LineID);
                                                lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_POSITIVE));
                                            }
                                            lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                            lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
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
                                        lAGVLineIDs.Add(this.GetOneDirLineIDWithMinRouteIntvInEvenRate(oA.oType.Length - (this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y - this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y),
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.CurrLaneID].TPIDEnd].LogicPosX, CoordinateDirection.Y_NEGATIVE));
                                    else if (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y > this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y)
                                        lAGVLineIDs.Add(this.GetOneDirLineIDWithMinRouteIntvInEvenRate(oA.oType.Length - (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.Y - this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.Y),
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.CurrLaneID].TPIDStart].LogicPosX, CoordinateDirection.Y_POSITIVE));
                                    lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                    lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    break;
                                case AreaType.STS_PB:
                                    // From WSPB to QCPB
                                    // 如果可以直达(符合单行线约束的条件下)，直接到线避免转弯，不用加AGVLine；否则，随机
                                    if (!this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].lLineIDs.Contains(this.oSimDataStore.dLanes[oA.AimLaneID].LineID)
                                        || (this.oSimDataStore.dOneDirLineSegments.ContainsKey(this.oSimDataStore.dLanes[oA.AimLaneID].LineID)
                                        && (this.oSimDataStore.dOneDirLineSegments[this.oSimDataStore.dLanes[oA.AimLaneID].LineID].Dir != CoordinateDirection.Y_POSITIVE)))
                                    {
                                        if (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X < 353)
                                        {
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_POSITIVE));
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.X_NEGATIVE));
                                        }
                                        else
                                        {
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_POSITIVE));
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.X_POSITIVE));
                                        }
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
                                        lLanes.Add(lTempLanes1[0]);
                                        // 如果不能直接到 PB，加线
                                        if (!this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].lLineIDs.Contains(lTempLanes1[0].LineID)
                                            || (this.oSimDataStore.dOneDirLineSegments.ContainsKey(this.oSimDataStore.dLanes[oA.CurrLaneID].LineID)
                                            && (this.oSimDataStore.dOneDirLineSegments[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].Dir != CoordinateDirection.Y_NEGATIVE)))
                                        {
                                            if (this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X < 389)
                                            {
                                                lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.X_POSITIVE));
                                                lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_NEGATIVE));
                                            }
                                            else
                                            {
                                                lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.X_NEGATIVE));
                                                lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_NEGATIVE));
                                            }
                                        }
                                        if (lTempLanes1[0].LineID != this.oSimDataStore.dLanes[oA.AimLaneID].LineID)
                                        {
                                            lAGVLineIDs.Add(lTempLanes1[0].LineID);
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_POSITIVE));
                                        }
                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                        lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    }
                                    else
                                    {
                                        bRet = false;
                                        Flag = "Generate Route from QCPB Lane : " + oA.CurrLaneID.ToString() + " to WSTP Lane : "
                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed";
                                    }
                                    break;
                                case AreaType.WS_PB:
                                    // From QCPB to WSPB
                                    // 如果不能直接到 PB，加线
                                    if (!this.oSimDataStore.dAGVLines[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].lLineIDs.Contains(this.oSimDataStore.dLanes[oA.AimLaneID].LineID)
                                        || (this.oSimDataStore.dOneDirLineSegments.ContainsKey(this.oSimDataStore.dLanes[oA.CurrLaneID].LineID)
                                        && (this.oSimDataStore.dOneDirLineSegments[this.oSimDataStore.dLanes[oA.CurrLaneID].LineID].Dir != CoordinateDirection.Y_NEGATIVE)))
                                    {
                                        if (this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X < 389)
                                        {
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.X_POSITIVE));
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_NEGATIVE));
                                        }
                                        else
                                        {
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.X_NEGATIVE));
                                            lAGVLineIDs.Add(this.GetOneDirLineIDInEvenRate(CoordinateDirection.Y_NEGATIVE));
                                        }
                                    }
                                    lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                    lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    break;
                                case AreaType.STS_PB:
                                    // From QCPB to QCPB
                                    if (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X < this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X)
                                        lAGVLineIDs.Add(this.GetOneDirLineIDWithMinRouteIntvInEvenRate(oA.oType.Length - (this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X - this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X),
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.AimLaneID].TPIDStart].LogicPosY, CoordinateDirection.X_NEGATIVE));
                                    else
                                        lAGVLineIDs.Add(this.GetOneDirLineIDWithMinRouteIntvInEvenRate(oA.oType.Length - (this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X - this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X),
                                            this.oSimDataStore.dTransponders[this.oSimDataStore.dLanes[oA.AimLaneID].TPIDStart].LogicPosY, CoordinateDirection.X_POSITIVE));
                                    lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                    lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                    break;
                                case AreaType.STS_TP:
                                    // From QCPB to QCTP
                                    // 起始点必须有进入资格，不然不能保证 QCTP 首尾磁钉的进出关系
                                    if ((this.oSimDataStore.dLanes[oA.CurrLaneID].eAttr == LaneAttribute.STS_PB_ONLY_IN
                                        && this.oSimDataStore.dLanes[oA.CurrLaneID].CheNo == this.oSimDataStore.dLanes[oA.AimLaneID].CheNo)
                                        || this.oSimDataStore.dLanes[oA.CurrLaneID].eAttr == LaneAttribute.STS_PB_IN_OUT)
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
                                                        AGVLineID = this.GetSwitchLineIDBetweenQCTPsInEvenRate(lTempLanes1.Last(), this.oSimDataStore.dLanes[oA.AimLaneID], oA.oType.Length / 2);
                                                        if (AGVLineID > 0)
                                                        {
                                                            lAGVLineIDs.Add(44);
                                                            lAGVLineIDs.Add(AGVLineID);
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
                                                        AGVLineID = this.GetSwitchLineIDBetweenQCTPsInEvenRate(lTempLanes1.Last(), this.oSimDataStore.dLanes[oA.AimLaneID], oA.oType.Length / 2);
                                                        if (AGVLineID > 0)
                                                        {
                                                            lAGVLineIDs.Add(47);
                                                            lAGVLineIDs.Add(AGVLineID);
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
                                        Flag = "Route from No QCPBIN of the same QC or No QCPBINOUT to QCTP is not Allowed";
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
                                    // From QCTP to QCPB，注意终点必须有出行资格
                                    if ((this.oSimDataStore.dLanes[oA.AimLaneID].eAttr == LaneAttribute.STS_PB_ONLY_OUT
                                        && this.oSimDataStore.dLanes[oA.AimLaneID].CheNo == this.oSimDataStore.dLanes[oA.CurrLaneID].CheNo)
                                        || this.oSimDataStore.dLanes[oA.AimLaneID].eAttr == LaneAttribute.STS_PB_IN_OUT)
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
                                                    lTempLanes1 = lTempLanes1.Where(u => u.LineID == 44).ToList();
                                                    if (lTempLanes1.Count > 0 && lTempLanes1.All(u => u.eStatus == LaneStatus.IDLE))
                                                    {
                                                        AGVLineID = this.GetSwitchLineIDBetweenQCTPsInEvenRate(this.oSimDataStore.dLanes[oA.CurrLaneID], lTempLanes1[0], oA.oType.Length / 2);
                                                        if (AGVLineID > 0)
                                                        {
                                                            lAGVLineIDs.Add(AGVLineID);
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
                                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Idle QCTP";
                                                    }
                                                    break;
                                                case 46:
                                                    lTempLanes1 = lTempLanes1.Where(u => u.LineID == 47).ToList();
                                                    if (lTempLanes1.Count > 0 && lTempLanes1.All(u => u.eStatus == LaneStatus.IDLE))
                                                    {
                                                        AGVLineID = this.GetSwitchLineIDBetweenQCTPsInEvenRate(this.oSimDataStore.dLanes[oA.CurrLaneID], lTempLanes1[0], oA.oType.Length / 2);
                                                        if (AGVLineID > 0)
                                                        {
                                                            lAGVLineIDs.Add(AGVLineID);
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
                                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Idle QCTP";
                                                    }
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        bRet = false;
                                        Flag = "Route to No QCPBOUT of the same QC or No QCPBINOUT is not Allowed";
                                    }
                                    break;
                                case AreaType.STS_TP:
                                    // From QCTP to QCTP
                                    // 不能跨工作车道线
                                    if ((this.oSimDataStore.dLanes[oA.CurrLaneID].LineID <= 45 && this.oSimDataStore.dLanes[oA.AimLaneID].LineID >= 46)
                                        || (this.oSimDataStore.dLanes[oA.CurrLaneID].LineID >= 46 && this.oSimDataStore.dLanes[oA.AimLaneID].LineID <= 45))
                                    {
                                        bRet = false;
                                        Flag = "Route between QCTPs across working QCTP AGVLine is not Allowed";
                                    }
                                    // 不能去同一岸桥的 QCTP
                                    if (Math.Abs(this.oSimDataStore.dLanes[oA.CurrLaneID].pWork.X - this.oSimDataStore.dLanes[oA.AimLaneID].pWork.X) < 0.1)
                                    {
                                        bRet = false;
                                        Flag = "Route between QCTPs under a same QC is not Allowed";
                                    }
                                    if (bRet)
                                    {
                                        // 看看中间有没有夹着若干Lane
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

                                        if (lTempLanes1.Count == 0)
                                        {
                                            // 仅在需要时换线
                                            if (this.oSimDataStore.dLanes[oA.CurrLaneID].LineID != this.oSimDataStore.dLanes[oA.AimLaneID].LineID)
                                            {
                                                AGVLineID = this.GetSwitchLineIDBetweenQCTPsInEvenRate(this.oSimDataStore.dLanes[oA.CurrLaneID],
                                                    this.oSimDataStore.dLanes[oA.AimLaneID], oA.oType.Length / 2);
                                                if (AGVLineID == 0)
                                                {
                                                    bRet = false;
                                                    Flag = "Generate Route from QCTP Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                        + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Switching AGVLine";
                                                }
                                                else
                                                {
                                                    lAGVLineIDs.Add(AGVLineID);
                                                    lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
                                                }
                                            }
                                            lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                        }
                                        else
                                        {
                                            // 先确定中间垫的QCTP，以及路径的lLanes
                                            if (this.oSimDataStore.dLanes[oA.CurrLaneID].LineID <= 45)
                                                lTempLanes1 = lTempLanes1.Where(u => u.LineID == 44).ToList();
                                            else
                                                lTempLanes1 = lTempLanes1.Where(u => u.LineID == 47).ToList();
                                            if (lTempLanes1.All(u => u.eStatus == LaneStatus.IDLE))
                                            {
                                                lLanes.AddRange(lTempLanes1);
                                                lLanes.Add(this.oSimDataStore.dLanes[oA.AimLaneID]);
                                            }
                                            else
                                            {
                                                bRet = false;
                                                Flag = "Generate Route from QCTP Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                    + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Idle QCTP";
                                            }

                                            // 然后再看 lAGVLineIDs
                                            if (bRet)
                                            {
                                                // 头端换线
                                                if (this.oSimDataStore.dLanes[oA.CurrLaneID].eAttr != LaneAttribute.STS_TP_PASS)
                                                {
                                                    AGVLineID = this.GetSwitchLineIDBetweenQCTPsInEvenRate(this.oSimDataStore.dLanes[oA.CurrLaneID], lTempLanes1[0], oA.oType.Length / 2);
                                                    if (AGVLineID == 0)
                                                    {
                                                        bRet = false;
                                                        Flag = "Generate Route from QCTP Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Switch AGVLine";
                                                    }
                                                    else
                                                    {
                                                        lAGVLineIDs.Add(AGVLineID);
                                                        if (this.oSimDataStore.dLanes[oA.CurrLaneID].LineID <= 45) 
                                                            lAGVLineIDs.Add(44);
                                                        if (this.oSimDataStore.dLanes[oA.CurrLaneID].LineID >= 46) 
                                                            lAGVLineIDs.Add(47);
                                                    }
                                                }

                                                // 尾端换线
                                                if (bRet && this.oSimDataStore.dLanes[oA.AimLaneID].eAttr != LaneAttribute.STS_TP_PASS)
                                                {
                                                    AGVLineID = this.GetSwitchLineIDBetweenQCTPsInEvenRate(lTempLanes1.Last(), this.oSimDataStore.dLanes[oA.AimLaneID], oA.oType.Length / 2);
                                                    if (AGVLineID == 0)
                                                    {
                                                        bRet = false;
                                                        Flag = "Generate Route from QCTP Lane : " + oA.CurrLaneID.ToString() + " to QCTP Lane : "
                                                            + oA.AimLaneID.ToString() + " of AGV : " + oA.ID.ToString() + " Failed for lack of Switch AGVLine";
                                                    }
                                                    else
                                                    {
                                                        lAGVLineIDs.Add(AGVLineID);
                                                        lAGVLineIDs.Add(this.oSimDataStore.dLanes[oA.AimLaneID].LineID);
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
                if (this.IfRouteLog)
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
                if (this.IfRouteLog)
                {
                    this.fs_Routing = new FileStream(System.Environment.CurrentDirectory + "\\PrintOut\\Routings.txt", FileMode.Append);
                    this.sw_Routing = new StreamWriter(this.fs_Routing, Encoding.Default);
                    this.sw_Routing.WriteLine(DateTime.Now.ToString() + " : Route Generated for AGV: " + oA.ID.ToString() + ": From " + oA.CurrLaneID.ToString() + " To : " + oA.AimLaneID.ToString());
                    this.sw_Routing.Close();
                    this.fs_Routing.Close();
                }

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
                        if (this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].Dir == CoordinateDirection.X_POSITIVE
                            || this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].Dir == CoordinateDirection.X_NEGATIVE)
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
                        if (this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].Dir == CoordinateDirection.X_POSITIVE
                            || this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].Dir == CoordinateDirection.X_NEGATIVE)
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
                    if (this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].Dir == CoordinateDirection.X_POSITIVE
                        || this.oSimDataStore.dAGVLines[lAGVLineIDs[i]].Dir == CoordinateDirection.X_NEGATIVE)
                    {
                        if (oRS.EndPoint.X > oRS.StartPoint.X) 
                            oRS.eCD = CoordinateDirection.X_POSITIVE;
                        else 
                            oRS.eCD = CoordinateDirection.X_NEGATIVE;
                    }
                    else
                    {
                        if (oRS.EndPoint.Y > oRS.StartPoint.Y)
                            oRS.eCD = CoordinateDirection.Y_POSITIVE;
                        else
                            oRS.eCD = CoordinateDirection.Y_NEGATIVE;
                    }

                    // 修正 StartPoint and EndPoint, 并决定各 RouteSegment 的起止 RoutePos.
                    if (i == 0)
                    {
                        switch (oRS.eCD)
                        {
                            case CoordinateDirection.X_POSITIVE:
                                oRS.StartPoint.X = Math.Round(oRS.StartPoint.X - oA.oType.Length / 2, this.DecimalNum);
                                oRS.StartLinePos = Math.Round(oRS.StartPoint.X, this.DecimalNum);
                                break;
                            case CoordinateDirection.X_NEGATIVE:
                                oRS.StartPoint.X = Math.Round(oRS.StartPoint.X + oA.oType.Length / 2, this.DecimalNum);
                                oRS.StartLinePos = Math.Round(oRS.StartPoint.X, this.DecimalNum);
                                break;
                            case CoordinateDirection.Y_POSITIVE:
                                oRS.StartPoint.Y = Math.Round(oRS.StartPoint.Y - oA.oType.Length / 2, this.DecimalNum);
                                oRS.StartLinePos = Math.Round(oRS.StartPoint.Y, this.DecimalNum);
                                break;
                            case CoordinateDirection.Y_NEGATIVE:
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
                            case CoordinateDirection.X_POSITIVE:
                                oRS.EndPoint.X = Math.Round(oRS.EndPoint.X + oA.oType.Length / 2, this.DecimalNum);
                                oRS.EndLinePos = Math.Round(oRS.EndPoint.X, this.DecimalNum);
                                break;
                            case CoordinateDirection.X_NEGATIVE:
                                oRS.EndPoint.X = Math.Round(oRS.EndPoint.X - oA.oType.Length / 2, this.DecimalNum);
                                oRS.EndLinePos = Math.Round(oRS.EndPoint.X, this.DecimalNum);
                                break;
                            case CoordinateDirection.Y_POSITIVE:
                                oRS.EndPoint.Y = Math.Round(oRS.EndPoint.Y + oA.oType.Length / 2, this.DecimalNum);
                                oRS.EndLinePos = Math.Round(oRS.EndPoint.Y, this.DecimalNum);
                                break;
                            case CoordinateDirection.Y_NEGATIVE:
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
                        case CoordinateDirection.X_POSITIVE:
                            lTempTPs = this.dEssentialAndQCTPs.Values.Where(u => u.HorizontalLineID == oRS.AGVLineID
                                && u.LogicPosX >= oRS.StartLinePos && u.LogicPosX <= oRS.EndLinePos).OrderBy(u => u.LogicPosX).ToList();
                            break;
                        case CoordinateDirection.X_NEGATIVE:
                            lTempTPs = this.dEssentialAndQCTPs.Values.Where(u => u.HorizontalLineID == oRS.AGVLineID
                                && u.LogicPosX <= oRS.StartLinePos && u.LogicPosX >= oRS.EndLinePos).OrderByDescending(u => u.LogicPosX).ToList();
                            break;
                        case CoordinateDirection.Y_POSITIVE:
                            lTempTPs = this.dEssentialAndQCTPs.Values.Where(u => u.VerticalLineID == oRS.AGVLineID
                                && u.LogicPosY >= oRS.StartLinePos && u.LogicPosY <= oRS.EndLinePos).OrderBy(u => u.LogicPosY).ToList();
                            break;
                        case CoordinateDirection.Y_NEGATIVE:
                            lTempTPs = this.dEssentialAndQCTPs.Values.Where(u => u.VerticalLineID == oRS.AGVLineID
                                && u.LogicPosY <= oRS.StartLinePos && u.LogicPosY >= oRS.EndLinePos).OrderByDescending(u => u.LogicPosY).ToList();
                            break;
                    }
                    if (lTempTPs.Count > 0)
                    {
                        oRS.StartTPID = lTempTPs[0].ID;
                        oRS.EndTPID = lTempTPs.Last().ID;
                        foreach (SimTransponder obj in lTempTPs)
                        {
                            if (!oPPTViewFrame.lTPs.Contains(obj)) 
                                oPPTViewFrame.lTPs.Add(obj);
                            if (!obj.lRouteAGVIDs.Exists(u => u == oA.ID))
                                obj.lRouteAGVIDs.Add(oA.ID);
                            if (!obj.lUnPassedAGVIDs.Exists(u => u == oA.ID))
                                obj.lUnPassedAGVIDs.Add(oA.ID);
                            if (!oAGVRoute.lRouteTPInfos.Exists(u => u.TPID == obj.ID))
                                oAGVRoute.lRouteTPInfos.Add(new TPInfoEnRoute(obj.ID, oRS.eCD, oRS.StartRoutePos, oRS.StartLinePos, obj.LogicPosX, obj.LogicPosY));
                        }
                    }
                    oAGVRoute.lRouteSegments.Add(oRS);
                }

                // 整理 oAGVRoute.lTPinfosEnRoute，标记 EnterLaneID 和 ExitLaneID
                lTPInfoERs = oAGVRoute.lRouteTPInfos.Where(u => this.dEssentialAndQCTPs[u.TPID].LaneID > 0).OrderBy(u => u.RoutePos).ToList();
                if (lTPInfoERs.Count > 0)
                {
                    if (lTPInfoERs.Count == 1 ||
                        this.dEssentialAndQCTPs[lTPInfoERs[0].TPID].LaneID != this.dEssentialAndQCTPs[lTPInfoERs[1].TPID].LaneID)
                    {
                        lTPInfoERs[0].ExitLaneID = this.dEssentialAndQCTPs[lTPInfoERs[0].TPID].LaneID;
                        lTPInfoERs.RemoveAt(0);
                    }
                }
                if (lTPInfoERs.Count > 0)
                {
                    if (lTPInfoERs.Count == 1 ||
                        this.dEssentialAndQCTPs[lTPInfoERs[lTPInfoERs.Count - 1].TPID].LaneID != this.dEssentialAndQCTPs[lTPInfoERs[lTPInfoERs.Count - 2].TPID].LaneID)
                    {
                        lTPInfoERs[lTPInfoERs.Count - 1].EnterLaneID = this.dEssentialAndQCTPs[lTPInfoERs[lTPInfoERs.Count - 1].TPID].LaneID;
                        lTPInfoERs.RemoveAt(lTPInfoERs.Count - 1);
                    }
                }
                while (lTPInfoERs.Count > 0)
                {
                    lTPInfoERs[0].EnterLaneID = this.dEssentialAndQCTPs[lTPInfoERs[0].TPID].LaneID;
                    lTPInfoERs[1].ExitLaneID = this.dEssentialAndQCTPs[lTPInfoERs[1].TPID].LaneID;
                    lTPInfoERs.RemoveRange(0, 2);
                }

                oAGVRoute.lRouteTPInfos.OrderBy(u => u.RoutePos);
                oAGVRoute.TotalLength = oAGVRoute.lRouteSegments[oAGVRoute.lRouteSegments.Count - 1].EndRoutePos;
                this.dAGVRoutes.Add(oAGVRoute.AGVID, oAGVRoute);
                
                // 生成路径之后 dAGVRouteAngle 要更新一下
                switch (oAGVRoute.lRouteSegments[0].eCD)
                {
                    case CoordinateDirection.X_POSITIVE:
                        this.dAGVRouteAngles[oA.ID] = 0;
                        break;
                    case CoordinateDirection.X_NEGATIVE:
                        this.dAGVRouteAngles[oA.ID] = 180;
                        break;
                    case CoordinateDirection.Y_POSITIVE:
                        this.dAGVRouteAngles[oA.ID] = 90;
                        break;
                    case CoordinateDirection.Y_NEGATIVE:
                        this.dAGVRouteAngles[oA.ID] = 270;
                        break;
                }

                // 可能某些 TP 的 ResvAGVID 也需要更新
                lTempTPs = this.dEssentialAndQCTPs.Values.Where(u =>
                    (u.HorizontalLineID == oAGVRoute.lRouteSegments[0].AGVLineID && u.LogicPosX > oA.MidPoint.X - oA.oType.Length / 2 && u.LogicPosX < oA.MidPoint.X + oA.oType.Length / 2)
                    || (u.VerticalLineID == oAGVRoute.lRouteSegments[0].AGVLineID && u.LogicPosY > oA.MidPoint.Y - oA.oType.Length / 2 && u.LogicPosY < oA.MidPoint.Y + oA.oType.Length / 2)).ToList();
                if (lTempTPs.Count > 0)
                    lTempTPs.ForEach(u => u.ResvAGVID = oA.ID);

                this.IfRouteFeatureExpired = true;
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
        private uint GetSwitchLineIDBetweenQCTPsInEvenRate(Lane Lane1, Lane Lane2, double HalfAGVLength)
        {
            uint SwitchLineID = 0;
            double dMin;
            double dMax;
            Random rand = new Random();
            List<double> lStarts = new List<double>();
            List<double> lEnds = new List<double>();

            if (Lane1.eType != AreaType.STS_TP || Lane2.eType != AreaType.STS_TP)
                return SwitchLineID;

            if (Math.Abs(Lane1.pMid.X - Lane2.pMid.X) < 0.1) 
                return SwitchLineID;

            if (Lane1.pMid.X > Lane2.pMid.X)
            {
                dMin = Math.Max(this.dEssentialAndQCTPs[Lane2.TPIDEnd].LogicPosX, this.dEssentialAndQCTPs[Lane2.TPIDStart].LogicPosX) + HalfAGVLength;
                dMax = Math.Min(this.dEssentialAndQCTPs[Lane1.TPIDEnd].LogicPosX, this.dEssentialAndQCTPs[Lane1.TPIDStart].LogicPosX) - HalfAGVLength;
            }
            else
            {
                dMin = Math.Max(this.dEssentialAndQCTPs[Lane1.TPIDEnd].LogicPosX, this.dEssentialAndQCTPs[Lane1.TPIDStart].LogicPosX) + HalfAGVLength;
                dMax = Math.Min(this.dEssentialAndQCTPs[Lane2.TPIDEnd].LogicPosX, this.dEssentialAndQCTPs[Lane2.TPIDStart].LogicPosX) - HalfAGVLength;
            }

            // 初筛，可能中间还有车道挡住
            if (dMin >= dMax) 
                return SwitchLineID;

            List<AGVLine> lSelectedAGVLine = this.dEssentialAGVLines.Values.Where(u => u.FeaturePosition > dMin && u.FeaturePosition < dMax
                && (u.Dir == CoordinateDirection.Y_NEGATIVE || u.Dir == CoordinateDirection.Y_POSITIVE)).ToList();

            if (lSelectedAGVLine.Count == 0) 
                return SwitchLineID;

            List<Lane> lQCTPs = this.oSimDataStore.dLanes.Values.Where(u => u.eType == AreaType.STS_TP && u.pWork.X > dMin && u.pWork.X < dMax).ToList();

            if (lQCTPs.Count == 0)
                return SwitchLineID;

            // 中间车道挡住的 AGVLine 要滤掉
            for (int i = lSelectedAGVLine.Count; i >= 0; i--)
            {
                for (int j = 0; j < lQCTPs.Count; j++)
                {
                    dMin = Math.Min(this.dEssentialAndQCTPs[lQCTPs[i].TPIDStart].LogicPosX, this.dEssentialAndQCTPs[lQCTPs[i].TPIDEnd].LogicPosX);
                    dMax = Math.Max(this.dEssentialAndQCTPs[lQCTPs[i].TPIDStart].LogicPosX, this.dEssentialAndQCTPs[lQCTPs[i].TPIDEnd].LogicPosX);
                    if (lSelectedAGVLine[i].FeaturePosition > dMin - HalfAGVLength && lSelectedAGVLine[i].FeaturePosition < dMax + HalfAGVLength)
                        lSelectedAGVLine.RemoveAt(i);
                }
            }

            if (lSelectedAGVLine.Count == 0) 
                return SwitchLineID;

            SwitchLineID = lSelectedAGVLine[rand.Next(0, lSelectedAGVLine.Count)].ID;

            return SwitchLineID;
        }

        /// <summary>
        /// 返回满足距离和方向要求的转接用单向 AGVLineID
        /// </summary>
        /// <param name="IntvLack">尚缺距离</param>
        /// <param name="ZeroFeaturePos">零距离特征位置</param>
        /// <param name="eCD">AGVLine的方向</param>
        /// <returns>AGVLineID</returns>
        private uint GetOneDirLineIDWithMinRouteIntvInEvenRate(double MinIntv, double ZeroFeaturePos, CoordinateDirection eCD)
        {
            uint iRet = 0;
            Random rand = new Random();
            List<AGVLine> lAGVLines;
            List<uint> lAGVLineIDs;

            lAGVLines = this.oSimDataStore.dAGVLines.Values.Where(u => this.oSimDataStore.dOneDirLineSegments.ContainsKey(u.ID)
                && this.oSimDataStore.dOneDirLineSegments[u.ID].Dir == eCD).ToList();

            lAGVLineIDs = lAGVLines.Where(u => Math.Abs(u.FeaturePosition - ZeroFeaturePos) * 2 > MinIntv).Select(u => u.ID).ToList();

            if (lAGVLineIDs.Count > 0) iRet = lAGVLineIDs[rand.Next(0, lAGVLineIDs.Count)];

            return iRet;
        }

        /// <summary>
        /// 返回满足方向要求的转接用单行 AGVLineID
        /// </summary>
        /// <param name="eCD">道路方向</param>
        /// <returns>车道线号</returns>
        private uint GetOneDirLineIDInEvenRate(CoordinateDirection eCD)
        {
            uint iRet = 0;
            Random rand = new Random();
            List<uint> lAGVLineIDs;

            lAGVLineIDs = this.oSimDataStore.dAGVLines.Values.Where(u => this.oSimDataStore.dOneDirLineSegments.ContainsKey(u.ID)
                && this.oSimDataStore.dOneDirLineSegments[u.ID].Dir == eCD).Select(u => u.ID).ToList();

            if (lAGVLineIDs.Count > 0) iRet = lAGVLineIDs[rand.Next(0, lAGVLineIDs.Count)];

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

        #endregion


        #region AGV运动模拟

        /// <summary>
        /// 按照时间步长移动，返回变动后的磁钉、AGV和车道对象列表
        /// </summary>
        /// <param name="TimeLength">步长，单位秒</param>
        /// <param name="oPPTViewFrame">ViewFrame投射单元</param>
        public bool MoveAGVsInStep(double TimeLength, out ProjectPackageToViewFrame oPPTViewFrame)
        {
            double MaxReclaimLength, ActReclaimLength, ActVeloStepEnd, ActMoveLength;
            uint AgvID1, AgvID2;

            oPPTViewFrame = new ProjectPackageToViewFrame();

            this.RenewMovingAGVsAndSeqs(TimeLength); 

            // 更新 lIntersectedTPs, lCycleSegs 以及 lCycles
            if (this.IfRouteFeatureExpired) 
                this.RenewRouteFeatures(ref oPPTViewFrame);

            oPPTViewFrame.lAGVs = this.lMovingAGVs;

            if (this.lMovingAGVs.Count == 0)
                return true;

            for (int i = 0; i < this.lMovingAGVs.Count; i++)
            {
                if (this.IfDeadLockDetectLog)
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

                // 本步需要考虑的最大距离。不超过道路末端。
                MaxReclaimLength = this.GetMaxReclaimLength(this.lMovingAGVs[i], TimeLength);

                // 在最大范围内预约磁钉，返回本次实际预约到的最远位置，顺带捎回预约的磁钉
                // 可能触发相关 CycleSeg 的状态变化（进入）
                if (!this.ReclaimTransponderAndCycleSeg(this.lMovingAGVs[i], MaxReclaimLength, out ActReclaimLength, ref oPPTViewFrame.lTPs))
                {
                    Logger.Simulate.Error("Traffic Controller: Sth Wrong When AGV: " + this.lMovingAGVs[i].ID.ToString() 
                        + " Reclaiming Transponder With TP Reclaim Time: " + this.TPReclaimTime.ToString() 
                        + " And Full Cycle Detect Time: " + this.FullCycleDetectTime.ToString());
                    return false;
                }

                // AGV 步末速度和本步移动距离决定，考虑道路总长、加速度、弯道限速和前车安全距离
                this.AmendMaxVeloStepEnd(this.lMovingAGVs[i], TimeLength, ActReclaimLength, out ActVeloStepEnd, out ActMoveLength);

                // 在移动范围内处理相关事件，可能触发：到达事件、进入事件、离开事件，以及相关 CycleSeg 的状态变化（离开）
                if (!this.RenewAGVStatus(this.lMovingAGVs[i], ActVeloStepEnd, ActMoveLength, ref oPPTViewFrame.lTPs, ref oPPTViewFrame.lLanes))
                {
                    Logger.Simulate.Error("Traffic Controller: Sth Wrong When AGV: " + this.lMovingAGVs[i].ID.ToString()
                        + " Renewing Status With TP Reclaim Time: " + this.TPReclaimTime.ToString()
                        + " And Full Cycle Detect Time: " + this.FullCycleDetectTime.ToString());
                    return false;
                }

                if (this.IfDeadLockDetectLog)
                {
                    this.sw_AgvResvRelease.Close();
                    this.fs_AgvResvRelease.Close();
                }
            }

            if (this.CheckIfAllStopped())
            {
                this.DeadLockExpection.ThrowTime++;
                Logger.Simulate.Info("DeadLocked At Cycle Detect Time " + this.FullCycleDetectTime.ToString());
            }

            if (this.CheckIfTooClose(out AgvID1, out AgvID2))
            {
                Logger.Simulate.Info("AGV:" + AgvID1.ToString() + " And AGV:" + AgvID2.ToString() + " Too Close");
            }

            return true;
        }

        /// <summary>
        /// 本步需要考虑的最大距离。以车头为基准，不考虑任何安全距离和道路长度限制。
        /// </summary>
        /// <param name="oA">AGV 对象</param>
        /// <param name="TimeLength">时间步长，单位毫秒</param>
        /// <returns>返回本步需要向前考虑的距离</returns>
        private double GetMaxReclaimLength(AGV oA, double TimeLength)
        {
            double MaxVeloVeh, MaxVeloStepEnd, MaxReclaimLength;

            if (this.lAGVOccupyLineSegs.Where(u => u.AGVID == oA.ID).ToList().Count > 1)
                MaxVeloVeh = oA.oType.VeloTurnUpper;
            else
            {
                if (oA.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.None)
                    MaxVeloVeh = oA.oType.VeloEmptyUpper;
                else
                    MaxVeloVeh = oA.oType.VeloFullUpper;
            }

            MaxVeloStepEnd = Math.Min(oA.CurrVelo + oA.oType.Acceleration * TimeLength, MaxVeloVeh);

            // 本时段全力行使，车头到停止为止经过的距离。
            MaxReclaimLength = (oA.CurrVelo + MaxVeloStepEnd) / 2 * TimeLength + (MaxVeloStepEnd * MaxVeloStepEnd) / 2 * oA.oType.Acceleration;

            MaxReclaimLength = Math.Round(MaxReclaimLength, this.DecimalNum);

            return MaxReclaimLength;
        }

        /// <summary>
        /// 尝试预约路径前方给定距离范围内的磁钉，返回能申请到的最大静态距离。注意不包含到磁钉的安全距离。
        /// </summary>
        /// <param name="oA">AGV对象</param>
        /// <param name="MaxReclaimLength">最大静态申请距离</param>
        /// <param name="ActReclaimLength">实际静态申请距离</param>
        /// <param name="lTPs">预约到的磁钉列表，注意是各 AGV 预约的累积</param>
        /// <returns>正常返回true，否则返回false</returns>
        private bool ReclaimTransponderAndCycleSeg(AGV oA, double MaxReclaimLength, out double ActReclaimLength, ref List<SimTransponder> lTPs)
        {
            bool IsToTail, IsDirLockSucc, IsResvCauseDeadlock;
            uint CurrTPID, TailTPID;
            double RoutePosMid, MaxReclaimRoutePos, CurrReclaimRoutePos, StartReclaimRoutePos;
            List<TPInfoEnRoute> lTempTPInfos;
            SimTransponder oTP;
            CycleSeg oResvSeg, oTempSeg, oTempReversedSeg;
            List<uint> lTempAntiLockedAGVIDs, lTempDirLockTPIDs, lTempDirLockCSIDs;
            Dictionary<uint, List<uint>> dTempAntiLockedAGVIDLists;

            ActReclaimLength = -1;
            RoutePosMid = -1;

            if (!this.SearchForRoutePosByCoor(oA.ID, oA.MidPoint.X, oA.MidPoint.Y, out RoutePosMid))
            {
                if (this.IfSelfCheckAndThrowException)
                    throw new SimException("MidPoint of AGV: " + oA.ID.ToString() + " Not On AGVLine");
                return false;
            }

            // 考虑预约磁钉的开始 RoutePos 和结束 RoutePos。此时考虑车头到磁钉的安全距离。
            StartReclaimRoutePos = RoutePosMid + oA.oType.Length / 2;
            CurrReclaimRoutePos = StartReclaimRoutePos;
            MaxReclaimRoutePos = Math.Round(Math.Min(this.dAGVRoutes[oA.ID].TotalLength + this.CompelAGVIntvToTP, StartReclaimRoutePos + MaxReclaimLength + this.CompelAGVIntvToTP), this.DecimalNum);

            lTempTPInfos = this.dAGVRoutes[oA.ID].lRouteTPInfos.Where(u => u.RoutePos > StartReclaimRoutePos && u.RoutePos <= MaxReclaimRoutePos)
                .OrderBy(u => u.RoutePos).ToList();

            IsToTail = true;
            if (lTempTPInfos.Count > 0)
            {
                foreach (TPInfoEnRoute oInfo in lTempTPInfos)
                {
                    oTP = this.dEssentialAndQCTPs[oInfo.TPID];
                    CurrReclaimRoutePos = oInfo.RoutePos;
                    // 若已经Resv，除非是被自己Resv才继续
                    if (oTP.ResvAGVID > 0)
                    {
                        if (oTP.ResvAGVID != oA.ID)
                        {
                            IsToTail = false;
                            break;
                        }
                    }
                    // 若有首锁且不是锁向自己路径的下一个磁钉点，或者有尾锁且不是自己上的，就不允许预约。
                    else if ((oTP.dDirLockAGVLists.Count > 0 && !oTP.dDirLockAGVLists.Keys.Any(u => this.dAGVRoutes[oA.ID].lRouteTPInfos.Exists(v => v.TPID == u) 
                        && this.dAGVRoutes[oA.ID].lRouteTPInfos.IndexOf(this.dAGVRoutes[oA.ID].lRouteTPInfos.Find(v => v.TPID == oTP.ID)) < this.dAGVRoutes[oA.ID].lRouteTPInfos.IndexOf(this.dAGVRoutes[oA.ID].lRouteTPInfos.Find(v => v.TPID == u))))
                        || (oTP.dDirLockTailAGVLists.Count > 0 && !oTP.dDirLockTailAGVLists.Values.Any(u => u.Contains(oA.ID))))
                    {
                        IsToTail = false;
                        if (this.IfDeadLockDetectLog)
                            this.sw_AgvResvRelease.WriteLine(oA.ID.ToString() + ",Reserve," + oTP.ID.ToString() + ",DirLockedAtResvTP,false");
                        break;
                    }
                    else
                    {
                        if (this.IfDeadLockDetectLog)
                            this.sw_AgvResvRelease.Write(oA.ID.ToString() + ",Reserve," + oTP.ID.ToString());

                        // 可能需要加方向锁
                        oResvSeg = this.dCycleSegs.Values.FirstOrDefault<CycleSeg>(u => u.lOrderedTPIDs[0] == oTP.ID && u.lRouteAGVIDs.Exists(v => v == oA.ID));
                        if (oResvSeg != null)
                        {
                            if (oResvSeg.dAntiLockedAGVIDs.ContainsKey(oA.ID))
                            {
                                if (this.IfDeadLockDetectLog)
                                    this.sw_AgvResvRelease.WriteLine(",NoWayRightAtResvTP,false");
                                IsToTail = false;
                                break;
                            }

                            // 预约点和路径段
                            oTP.ResvAGVID = oA.ID;
                            oResvSeg.lContainAGVIDs.Add(oA.ID);

                            // 方向锁点和路径段
                            lTempAntiLockedAGVIDs = new List<uint>();
                            lTempDirLockTPIDs = new List<uint>();
                            lTempDirLockCSIDs = new List<uint>();
                            dTempAntiLockedAGVIDLists = new Dictionary<uint, List<uint>>();     //在哪个路径段上反锁哪些AGV
                            oTempSeg = oResvSeg;
                            CurrTPID = oResvSeg.lOrderedTPIDs[0];
                            IsDirLockSucc = true;
                            while (oTempSeg != null && oTempSeg.IfHasReversedCycleSeg)
                            {
                                // 路径段被反锁则失败返回
                                if (oTempSeg.dAntiLockedAGVIDs.ContainsKey(oA.ID))
                                {
                                    if (this.IfDeadLockDetectLog)
                                        this.sw_AgvResvRelease.WriteLine(",NoWayRightAtCS,false");
                                    IsDirLockSucc = false;
                                    break;
                                }

                                // 如果路径段尾磁钉被预约则失败返回。保证上锁车优先通过
                                if (this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs.Last()].ResvAGVID > 0 
                                    && this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs.Last()].ResvAGVID != oA.ID)
                                {
                                    if (this.IfDeadLockDetectLog)
                                        this.sw_AgvResvRelease.WriteLine(",DirLockTailResved,false");
                                    IsDirLockSucc = false;
                                    break;
                                }

                                // 若路径段首磁钉被加方向首锁，且对应尾锁不在下一路径段范围内，则失败返回
                                if (this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[0]].dDirLockAGVLists.Count > 0
                                    && !this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[0]].dDirLockAGVLists.ContainsKey(oTempSeg.lOrderedTPIDs[1]))
                                {
                                    if (this.IfDeadLockDetectLog)
                                        this.sw_AgvResvRelease.WriteLine(",DiffDirLocked,false");
                                    IsDirLockSucc = false;
                                    break;
                                }

                                // 若路径段首磁钉被加方向尾锁，且对应首锁在下一路径段范围内，则失败返回
                                else if (this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[0]].dDirLockTailAGVLists.Count > 0
                                    && this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[0]].dDirLockTailAGVLists.ContainsKey(oTempSeg.lOrderedTPIDs[1]))
                                {
                                    if (this.IfDeadLockDetectLog)
                                        this.sw_AgvResvRelease.WriteLine(",DiffDirLockedTail,false");
                                    IsDirLockSucc = false;
                                    break;
                                }

                                // 没有反向路径段的，或者反向路径段已经被反锁的，不能加方向锁
                                oTempReversedSeg = this.dCycleSegs.Values.FirstOrDefault<CycleSeg>(u => u.IfHasReversedCycleSeg && u.ReversedCycleSegID == oTempSeg.ID);
                                if (oTempReversedSeg == null)
                                    break;
                                if (lTempAntiLockedAGVIDs.Count == 0)
                                    lTempAntiLockedAGVIDs = new List<uint>(oTempReversedSeg.lRouteAGVIDs);
                                else
                                    lTempAntiLockedAGVIDs = lTempAntiLockedAGVIDs.Intersect(oTempReversedSeg.lRouteAGVIDs).ToList();
                                if (lTempAntiLockedAGVIDs.Count == 0)
                                    break;

                                lTempDirLockCSIDs.Add(oTempSeg.ID);

                                // 方向锁点(路径段内的非末点)，留记录。
                                for (int i = 0; i < oTempSeg.lOrderedTPIDs.Count - 1; i++)
                                {
                                    if (!this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[i]].dDirLockAGVLists.ContainsKey(oTempSeg.lOrderedTPIDs[i + 1]))
                                        this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[i]].dDirLockAGVLists.Add(oTempSeg.lOrderedTPIDs[i + 1], new List<uint>());
                                    if (!this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[i]].dDirLockAGVLists[oTempSeg.lOrderedTPIDs[i + 1]].Contains(oA.ID))
                                    {
                                        // 总是成对的加，记录方向首锁就行了
                                        lTempDirLockTPIDs.Add(oTempSeg.lOrderedTPIDs[i]);
                                        this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[i]].dDirLockAGVLists[oTempSeg.lOrderedTPIDs[i + 1]].Add(oA.ID);
                                    }

                                    if (!this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[i + 1]].dDirLockTailAGVLists.ContainsKey(oTempSeg.lOrderedTPIDs[i]))
                                        this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[i + 1]].dDirLockTailAGVLists.Add(oTempSeg.lOrderedTPIDs[i], new List<uint>());
                                    if (!this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[i + 1]].dDirLockTailAGVLists[oTempSeg.lOrderedTPIDs[i]].Contains(oA.ID))
                                        this.dEssentialAndQCTPs[oTempSeg.lOrderedTPIDs[i + 1]].dDirLockTailAGVLists[oTempSeg.lOrderedTPIDs[i]].Add(oA.ID);
                                }

                                // 方向锁路径段，留记录
                                foreach (uint AgvId in lTempAntiLockedAGVIDs)
                                {
                                    if (!oTempReversedSeg.dAntiLockedAGVIDs.ContainsKey(AgvId))
                                        oTempReversedSeg.dAntiLockedAGVIDs.Add(AgvId, new List<uint>());
                                    if (!oTempReversedSeg.dAntiLockedAGVIDs[AgvId].Contains(oA.ID))
                                    {
                                        oTempReversedSeg.dAntiLockedAGVIDs[AgvId].Add(oA.ID);
                                        // 本次加锁记录，在哪个路径段锁了哪个车
                                        if (!dTempAntiLockedAGVIDLists.ContainsKey(oTempReversedSeg.ID))
                                            dTempAntiLockedAGVIDLists.Add(oTempReversedSeg.ID, new List<uint>());
                                        dTempAntiLockedAGVIDLists[oTempReversedSeg.ID].Add(AgvId);
                                    }
                                }

                                CurrTPID = oTempSeg.lOrderedTPIDs.Last();
                                oTempSeg = this.dCycleSegs.Values.FirstOrDefault<CycleSeg>(u => u.lRouteAGVIDs.Contains(oA.ID) && u.lOrderedTPIDs[0] == CurrTPID);
                            }

                            // 预约以及加锁是否引起死锁？
                            IsResvCauseDeadlock = false;
                            if (IsDirLockSucc)
                                IsResvCauseDeadlock = this.IfCauseFullCycle(oA.ID, oResvSeg.ID, lTempDirLockCSIDs);

                            if (IsDirLockSucc && !IsResvCauseDeadlock)
                            {
                                if (!lTPs.Contains(oTP))
                                    lTPs.Add(oTP);
                                foreach (uint TpId in lTempDirLockTPIDs)
                                {
                                    if (!lTPs.Contains(this.dEssentialAndQCTPs[TpId]))
                                        lTPs.Add(this.dEssentialAndQCTPs[TpId]);
                                    foreach (uint TailTpId in this.dEssentialAndQCTPs[TpId].dDirLockAGVLists.Keys)
                                    {
                                        if (!lTPs.Contains(this.dEssentialAndQCTPs[TailTpId]))
                                            lTPs.Add(this.dEssentialAndQCTPs[TailTpId]);
                                    }
                                }

                                if (this.IfDeadLockDetectLog)
                                    this.sw_AgvResvRelease.WriteLine("," + this.FullCycleDetectTime.ToString().PadLeft(8, '0') + ",true");
                            }
                            else
                            {
                                IsToTail = false;

                                // 退回预约
                                oTP.ResvAGVID = 0;
                                oResvSeg.lContainAGVIDs.Remove(oA.ID);

                                // 退回磁钉方向锁
                                foreach (uint TpId in lTempDirLockTPIDs)
                                {
                                    TailTPID = this.dEssentialAndQCTPs[TpId].dDirLockAGVLists.Keys.First(u => this.dEssentialAndQCTPs[TpId].dDirLockAGVLists[u].Contains(oA.ID));
                                    this.dEssentialAndQCTPs[TpId].dDirLockAGVLists[TailTPID].Remove(oA.ID);
                                    if (this.dEssentialAndQCTPs[TpId].dDirLockAGVLists[TailTPID].Count == 0)
                                        this.dEssentialAndQCTPs[TpId].dDirLockAGVLists.Remove(TailTPID);

                                    this.dEssentialAndQCTPs[TailTPID].dDirLockTailAGVLists[TpId].Remove(oA.ID);
                                    if (this.dEssentialAndQCTPs[TailTPID].dDirLockTailAGVLists[TpId].Count == 0)
                                        this.dEssentialAndQCTPs[TailTPID].dDirLockTailAGVLists.Remove(TpId);
                                }

                                // 退回路径段反向锁
                                foreach (uint SegID in dTempAntiLockedAGVIDLists.Keys)
                                {
                                    foreach (uint AgvId in dTempAntiLockedAGVIDLists[SegID])
                                    {
                                        this.dCycleSegs[SegID].dAntiLockedAGVIDs[AgvId].Remove(oA.ID);
                                        if (this.dCycleSegs[SegID].dAntiLockedAGVIDs[AgvId].Count == 0)
                                            this.dCycleSegs[SegID].dAntiLockedAGVIDs.Remove(AgvId);
                                    }
                                }

                                if (this.IfDeadLockDetectLog && IsDirLockSucc)
                                    this.sw_AgvResvRelease.WriteLine("," + this.FullCycleDetectTime.ToString().PadLeft(8, '0') + ",false");
                                break;
                            }
                        }
                        else
                        {
                            oTP.ResvAGVID = oA.ID;
                            if (!lTPs.Contains(oTP))
                                lTPs.Add(oTP);
                            if (this.IfDeadLockDetectLog)
                                this.sw_AgvResvRelease.WriteLine(",NotIntTP,true");
                        }
                        this.TPReclaimTime++;
                    }
                }
            }

            if (IsToTail && CurrReclaimRoutePos < MaxReclaimRoutePos) 
                CurrReclaimRoutePos = MaxReclaimRoutePos;

            ActReclaimLength = Math.Round(CurrReclaimRoutePos - StartReclaimRoutePos - this.CompelAGVIntvToTP, this.DecimalNum);

            if (this.IfSelfCheckAndThrowException && ActReclaimLength < 0)
                throw new SimException("Minus Reclaim Length Is Not Permitted");

            // Essential debug
            if (this.IfSelfCheckAndThrowException && this.CheckIfContainAGVLackingOrRepete())
                throw new SimException("Sth Wrong in List lContainAGVIDs of Some CycleSeg");

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
        private void AmendMaxVeloStepEnd(AGV oA, double TimeLength, double ActReclaimLength, out double ActVeloStepEnd, out double ActMoveLength)
        {
            double MaxVeloVeh, HeadRoutePos, TempFrontAGVPos, MidRoutePos;
            double CurrMaxVeloStepEnd, CurrMoveLengthStepEnd, FrontAGVPosInThisRoute, FrontAGVPosInSelfRoute, VeloRelatedDir;
            double CurrMaxVeloStepEnd_ori, CurrMoveLengthStepEnd_ori;
            double CurrMaxVeloStepEnd_ActRecLength, CurrMoveLengthStepEnd_ActRecLength;
            double CurrMaxVeloStepEnd_Turn, CurrMoveLengthStepEnd_Turn;
            double CurrMaxVeloStepEnd_Safe, CurrMoveLengthStepEnd_Safe;
            double x1_ActRecLength, x2_ActRecLength, x1_Turn, x2_Turn, x1_Safe, x2_Safe;
            int SegID, SegID2;
            AGVOccuLineSeg oAOLS;
            List<AGVOccuLineSeg> lAOLSs;
            bool bCycelSegStart, bEmergentStop;

            CurrMaxVeloStepEnd = -1;
            FrontAGVPosInThisRoute = -1;
            VeloRelatedDir = 0;

            // 按照车辆加速能力和道路限速，本步末端的理论最大速度
            if (this.lAGVOccupyLineSegs.Count(u => u.AGVID == oA.ID) > 1)
                // 转弯时会有一段以上的 AGVOccuSeg
                MaxVeloVeh = oA.oType.VeloTurnUpper;  
            else
            {
                if (oA.oTwinStoreUnit.eUnitStoreType == StatusEnums.StoreType.Empty) 
                    MaxVeloVeh = oA.oType.VeloEmptyUpper;
                else 
                    MaxVeloVeh = oA.oType.VeloFullUpper;
            }
            CurrMaxVeloStepEnd_ori = Math.Round(Math.Min(oA.CurrVelo + oA.oType.Acceleration * TimeLength, MaxVeloVeh), this.DecimalNum);
            CurrMoveLengthStepEnd_ori = Math.Round((oA.CurrVelo + CurrMaxVeloStepEnd_ori) * TimeLength / 2, this.DecimalNum);

            // 按照实际预约长度修正
            CurrMaxVeloStepEnd_ActRecLength = -1;
            CurrMoveLengthStepEnd_ActRecLength = -1;
            if ((oA.CurrVelo + CurrMaxVeloStepEnd_ori) * TimeLength / 2 +
                (CurrMaxVeloStepEnd_ori * CurrMaxVeloStepEnd_ori) / 2 / oA.oType.Acceleration > ActReclaimLength)
            {
                if (this.QuadraticEquationWithOneUnknownSolver(1, oA.oType.Acceleration * TimeLength,
                    oA.oType.Acceleration * (oA.CurrVelo * TimeLength - 2 * ActReclaimLength), out x1_ActRecLength, out x2_ActRecLength))
                {
                    if (x1_ActRecLength >= 0 && x1_ActRecLength <= CurrMaxVeloStepEnd_ori)
                    {
                        CurrMaxVeloStepEnd_ActRecLength = x1_ActRecLength;
                        CurrMoveLengthStepEnd_ActRecLength = Math.Round((oA.CurrVelo + CurrMaxVeloStepEnd_ActRecLength) * TimeLength / 2, this.DecimalNum);
                    }
                    else if (x2_ActRecLength >= 0 && x2_ActRecLength <= CurrMaxVeloStepEnd_ori)
                    {
                        CurrMaxVeloStepEnd_ActRecLength = x2_ActRecLength;
                        CurrMoveLengthStepEnd_ActRecLength = Math.Round((oA.CurrVelo + CurrMaxVeloStepEnd_ActRecLength) * TimeLength / 2, this.DecimalNum);
                    }
                    else
                    {
                        CurrMaxVeloStepEnd_ActRecLength = 0;
                        CurrMoveLengthStepEnd_ActRecLength = 0;
                    }
                }
                else
                {
                    CurrMaxVeloStepEnd_ActRecLength = 0;
                    CurrMoveLengthStepEnd_ActRecLength = 0;
                }
                
            }

            // 按照转弯位置修正
            HeadRoutePos = -1;
            CurrMaxVeloStepEnd_Turn = -1;
            CurrMoveLengthStepEnd_Turn = -1;
            if (this.SearchForRoutePosByCoor(oA.ID, oA.MidPoint.X, oA.MidPoint.Y, out MidRoutePos))
            {
                HeadRoutePos = MidRoutePos + oA.oType.Length / 2;
                SegID = this.dAGVRoutes[oA.ID].GetRouteSegIDByRoutePos(HeadRoutePos);
                if (SegID >= 0 && SegID < this.dAGVRoutes[oA.ID].lRouteSegments.Count - 1)
                {
                    if ((oA.CurrVelo + CurrMaxVeloStepEnd_ori) * TimeLength / 2 + (CurrMaxVeloStepEnd_ori * CurrMaxVeloStepEnd_ori - oA.oType.VeloTurnUpper * oA.oType.VeloTurnUpper)
                        / 2 / oA.oType.Acceleration > this.dAGVRoutes[oA.ID].lRouteSegments[SegID].EndRoutePos - HeadRoutePos)
                    {
                        if (this.QuadraticEquationWithOneUnknownSolver(1, oA.oType.Acceleration * TimeLength,
                            oA.oType.Acceleration * oA.CurrVelo * TimeLength - (oA.oType.VeloTurnUpper * oA.oType.VeloTurnUpper)
                                - 2 * oA.oType.Acceleration * (this.dAGVRoutes[oA.ID].lRouteSegments[SegID].EndRoutePos - HeadRoutePos), out x1_Turn, out x2_Turn))
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
                    if (this.IfSelfCheckAndThrowException)
                        throw new SimException("AGV: " + oA.ID.ToString() + " Not EnRoute");
            }
            else
                if (this.IfSelfCheckAndThrowException)
                    throw new SimException("AGV: " + oA.ID.ToString() + " Not On AGVLine");

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
                        else if (TempFrontAGVPos - HeadRoutePos < ActReclaimLength + this.CompelAGVIntvToAGV)
                        {
                            if (FrontAGVPosInThisRoute < 0 || TempFrontAGVPos < FrontAGVPosInThisRoute)
                            {
                                oAOLS = obj;
                                FrontAGVPosInThisRoute = TempFrontAGVPos;
                                bCycelSegStart = true;
                            }
                        }
                    }
                    else if (this.IfSelfCheckAndThrowException && TempFrontAGVPos > MidRoutePos)
                        Console.WriteLine("Sth Wrong");
                }

                if (!obj.bEndPointHinge)
                {
                    TempFrontAGVPos = this.dAGVRoutes[oA.ID].GetRoutePosByLineAndPos(obj.AGVLineID, obj.EndPos);
                    if (TempFrontAGVPos > HeadRoutePos)
                    {
                        if (TempFrontAGVPos < HeadRoutePos + this.CompelAGVIntvToAGV)
                            bEmergentStop = true;
                        else if (TempFrontAGVPos - HeadRoutePos < ActReclaimLength + this.CompelAGVIntvToAGV)
                        {
                            if (FrontAGVPosInThisRoute < 0 || TempFrontAGVPos < FrontAGVPosInThisRoute)
                            {
                                oAOLS = obj;
                                FrontAGVPosInThisRoute = TempFrontAGVPos;
                                bCycelSegStart = false;
                            }
                        }
                    }
                    else if (this.IfSelfCheckAndThrowException && TempFrontAGVPos > MidRoutePos)
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
                    if (this.IfSelfCheckAndThrowException)
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
            if (CurrMaxVeloStepEnd_ActRecLength >= 0)
            {
                CurrMaxVeloStepEnd = Math.Min(CurrMaxVeloStepEnd, CurrMaxVeloStepEnd_ActRecLength);
                CurrMoveLengthStepEnd = Math.Min(CurrMoveLengthStepEnd, CurrMoveLengthStepEnd_ActRecLength);
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

            if (this.IfSelfCheckAndThrowException && ActMoveLength > Math.Max(oA.CurrVelo, CurrMaxVeloStepEnd) * TimeLength)
                throw new SimException("AGV: Crossed");
        }

        /// <summary>
        /// 更新AGV的单步速度和位置，占线状态，并将释放的磁钉加入列表
        /// </summary>
        /// <param name="oA">AGV 对象</param>
        /// <param name="ActVeloStepEnd">步末实际速度</param>
        /// <param name="ActMoveLength">本步实际移动距离</param>
        /// <param name="lTPs">状态改变的磁钉列表</param>
        /// <param name="lLanes">状态改变的车道列表</param>
        /// <returns>正常返回true，否则返回false</returns>
        private bool RenewAGVStatus(AGV oA, double ActVeloStepEnd, double ActMoveLength, ref List<SimTransponder> lTPs, ref List<Lane> lLanes)
        {
            double CurrMidRoutePos, NextMidRoutePos, SegLowRoutePos, SegHighRoutePos, Pos1, Pos2, X, Y, NextRouteAngle;
            uint LineID1, LineID2, TempTPID;
            bool bArrive;
            List<AGVOccuLineSeg> lOccuSegs;
            CycleSeg oTempCycleSeg;
            List<TPInfoEnRoute> lTPInfos;
            List<uint> lTempAGVIDs;
            AGV_STATUS oAgvStatus;
            KeyValuePair<uint, List<uint>> oKVP;

            if (!this.SearchForRoutePosByCoor(oA.ID, oA.MidPoint.X, oA.MidPoint.Y, out CurrMidRoutePos))
            {
                throw new SimException("AGV: " + oA.ID.ToString() + "Not EnRoute");
                //return false;
            }

            NextMidRoutePos = CurrMidRoutePos + ActMoveLength;

            // 到达目标点，CurrMidRoutePos 和 NextMidRoutePos 的微小误差修正
            bArrive = false;
            if (Math.Abs(this.dAGVRoutes[oA.ID].TotalLength - CurrMidRoutePos - oA.oType.Length / 2) <= 0.02)
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
            oA.RotateAngle = oA.RotateAngle + (NextRouteAngle - this.dAGVRouteAngles[oA.ID]);
            while (oA.RotateAngle < 0)
                oA.RotateAngle = oA.RotateAngle + 360;
            while (oA.RotateAngle >= 360)
                oA.RotateAngle = oA.RotateAngle - 360;
            this.dAGVRouteAngles[oA.ID] = NextRouteAngle;

            // 释放 TP 和 CycleSeg，可能轧过出 Lane TP，触发 Lane 离开事件
            // 注意不影响交点的lRouteAGVIDs属性
            lTPInfos = this.dAGVRoutes[oA.ID].lRouteTPInfos.Where(u => u.RoutePos >= CurrMidRoutePos - oA.oType.Length / 2
                && u.RoutePos < NextMidRoutePos - oA.oType.Length / 2).ToList();

            foreach (TPInfoEnRoute obj in lTPInfos)
            {
                obj.IfPassed = true;
                this.dEssentialAndQCTPs[obj.TPID].ResvAGVID = 0;
                this.dEssentialAndQCTPs[obj.TPID].lUnPassedAGVIDs.Remove(oA.ID);

                oKVP = this.dEssentialAndQCTPs[obj.TPID].dDirLockTailAGVLists.FirstOrDefault(u => u.Value.Contains(oA.ID));
                if (oKVP.Value != null)
                {
                    TempTPID = oKVP.Key;
                    this.dEssentialAndQCTPs[TempTPID].dDirLockAGVLists[obj.TPID].Remove(oA.ID);
                    if (this.dEssentialAndQCTPs[TempTPID].dDirLockAGVLists[obj.TPID].Count == 0)
                        this.dEssentialAndQCTPs[TempTPID].dDirLockAGVLists.Remove(obj.TPID);
                    oKVP.Value.Remove(oA.ID);
                    if (oKVP.Value.Count == 0)
                        this.dEssentialAndQCTPs[obj.TPID].dDirLockTailAGVLists.Remove(oKVP.Key);
                }

                if (!lTPs.Exists(u => u.ID == obj.TPID)) 
                    lTPs.Add(this.dEssentialAndQCTPs[obj.TPID]);
                if (oKVP.Value != null && !lTPs.Exists(u => u.ID == oKVP.Key))
                    lTPs.Add(this.dEssentialAndQCTPs[oKVP.Key]);
                if (obj.ExitLaneID > 0)
                {
                    this.oSimDataStore.dLanes[obj.ExitLaneID].eStatus = LaneStatus.IDLE;
                    lLanes.Add(this.oSimDataStore.dLanes[obj.ExitLaneID]);
                    if (oA.CurrLaneID == obj.ExitLaneID) 
                        oA.CurrLaneID = 0;
                }

                // CycleSeg 状态变化，甚至拆除
                oTempCycleSeg = this.dCycleSegs.Values.FirstOrDefault<CycleSeg>(u => u.lOrderedTPIDs.Last() == obj.TPID && u.lContainAGVIDs.Contains(oA.ID));
                if (oTempCycleSeg != null)
                {
                    if (oTempCycleSeg.IfHasReversedCycleSeg && this.dCycleSegs[oTempCycleSeg.ReversedCycleSegID].dAntiLockedAGVIDs.Count > 0)
                    {
                        // 拆锁
                        lTempAGVIDs = new List<uint>(this.dCycleSegs[oTempCycleSeg.ReversedCycleSegID].dAntiLockedAGVIDs.Keys);
                        foreach (uint AgvId in lTempAGVIDs)
                        {
                            if (this.dCycleSegs[oTempCycleSeg.ReversedCycleSegID].dAntiLockedAGVIDs[AgvId].Contains(oA.ID))
                                this.dCycleSegs[oTempCycleSeg.ReversedCycleSegID].dAntiLockedAGVIDs[AgvId].Remove(oA.ID);
                            if (this.dCycleSegs[oTempCycleSeg.ReversedCycleSegID].dAntiLockedAGVIDs[AgvId].Count == 0)
                                this.dCycleSegs[oTempCycleSeg.ReversedCycleSegID].dAntiLockedAGVIDs.Remove(AgvId);
                        }
                    }
                    oTempCycleSeg.lContainAGVIDs.Remove(oA.ID);
                    oTempCycleSeg.lRouteAGVIDs.Remove(oA.ID);
                    if (oTempCycleSeg.lRouteAGVIDs.Count == 0)
                    {
                        if (oTempCycleSeg.IfHasReversedCycleSeg)
                            this.dCycleSegs[oTempCycleSeg.ReversedCycleSegID].IfHasReversedCycleSeg = false;
                        this.dCycleSegs.Remove(oTempCycleSeg.ID);
                    }
                }

                if (this.IfDeadLockDetectLog && this.lIntersectedTPs.Exists(u => u.ID == obj.TPID))
                    this.sw_AgvResvRelease.WriteLine(oA.ID.ToString() + ",Release," + obj.TPID.ToString());

                /// Essential debug
                if (this.CheckIfContainAGVLackingOrRepete())
                    throw new SimException("Sth Wrong in List lContainAGVIDs of Some CycleSeg");
            }

            // 可能轧过进 Lane 的 TP，触发 Lane 进入事件。一进入就往前推 Lane，不管上一 Lane 是否已经退出
            lTPInfos = this.dAGVRoutes[oA.ID].lRouteTPInfos.Where(u => u.RoutePos > CurrMidRoutePos + oA.oType.Length / 2
                && u.RoutePos <= NextMidRoutePos + oA.oType.Length / 2 && u.EnterLaneID > 0).ToList();
            foreach (TPInfoEnRoute obj in lTPInfos)
            {
                this.oSimDataStore.dLanes[obj.EnterLaneID].eStatus = LaneStatus.OCCUPIED;
                lLanes.Add(this.oSimDataStore.dLanes[obj.EnterLaneID]);
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

            if (bArrive)
            {
                oA.eMotionStatus = StatusEnums.MotionStatus.Waiting;
                // WSTP ： Mate 的进出点和 Lane 靠近箱区一侧的点只能这样子处理
                foreach (TPInfoEnRoute oInfo in this.dAGVRoutes[oA.ID].lRouteTPInfos)
                {
                    if (this.dEssentialAndQCTPs.ContainsKey(oInfo.TPID))
                    {
                        if (this.dEssentialAndQCTPs[oInfo.TPID].ResvAGVID == oA.ID)
                            this.dEssentialAndQCTPs[oInfo.TPID].ResvAGVID = 0;
                        if (this.dEssentialAndQCTPs[oInfo.TPID].lUnPassedAGVIDs.Contains(oA.ID))
                            this.dEssentialAndQCTPs[oInfo.TPID].lUnPassedAGVIDs.Remove(oA.ID);
                        this.dEssentialAndQCTPs[oInfo.TPID].lRouteAGVIDs.Remove(oA.ID);
                    }
                }
                this.dAGVRoutes.Remove(oA.ID);
                this.IfRouteFeatureExpired = true;
            }

            if (this.IfSelfCheckAndThrowException && this.CheckIfContainAGVLackingOrRepete())
                throw new SimException("Sth Wrong in List lContainAGVIDs of Some CycleSeg");

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
                switch (oAL.Dir)
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
                switch (this.dEssentialAGVLines[AGVLineID].Dir)
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
        /// <param name="AGVIDInput">申请占据的AGV编号</param>
        /// <param name="CycleSegIDInput">想占据的TP编号</param>
        /// <param name="lDirLockCSIDs">加方向锁的路径段编号列表</param>
        /// <returns>检测到死锁返回true，否则返回false</returns>
        private bool IfCauseFullCycle(uint AGVIDInput, uint CycleSegIDInput, List<uint> lDirLockCSIDs)
        {
            bool bFullCycleFound;
            double MaxLength, TempLength;
            string FileName;
            CycleSeg oCycleSeg;
            uint TempSegID;
            DateTime dTemp;
            List<CycleSeg> lTempCycleSegs;
            List<uint> lAGVIDs, lDeadLockCycleExpInCycleSegIDs, lCycleSegIDsOfPotentialFullCycles, lDeadLockCycle, lDeadLockJudgeStartSegIDs, lCurrSegPassingAGVIDs, lCurrInConsAGVIDs;
            List<Dictionary<uint, uint>> lElementCycles;
            StrongConnectedComponentsSolver oSCCS;
            Dictionary<uint, List<uint>> dDirGraphInCycleSegIDVectors;
            List<Dictionary<uint, List<uint>>>  lKeptSCCs, lRemovedSCCs;

            dTemp = DateTime.Now;

            if (this.IfDeadLockDetectLog)
            {
                this.FullCycleDetectTime++;
                this.PrintOutInputsOfFullCycleDetect(AGVIDInput, CycleSegIDInput);
            }

            /* 以基本环段为单元搜索死锁结构
             * 这样做的好处有:
             * 1) 求解空间规模严格限制在环段的总数
             * 2) 得到强连通分量后直接用环段的属性判断是否存在死锁结构，不用再转磁钉
             */
            dDirGraphInCycleSegIDVectors = new Dictionary<uint, List<uint>>();
            lCycleSegIDsOfPotentialFullCycles = new List<uint>() { CycleSegIDInput };

            foreach (CycleSeg oCS in this.dCycleSegs.Values)
                dDirGraphInCycleSegIDVectors.Add(oCS.ID, this.dCycleSegs.Values.Where(u => u.lOrderedTPIDs[0] == oCS.lOrderedTPIDs.Last()).Select(u => u.ID).ToList());

            if (this.IfStrongComponentLog)
                this.PrintOutInputsOfSolvers(dDirGraphInCycleSegIDVectors, lCycleSegIDsOfPotentialFullCycles);

            lElementCycles = new List<Dictionary<uint, uint>>();
            lDeadLockCycleExpInCycleSegIDs = new List<uint>();
            lKeptSCCs = new List<Dictionary<uint, List<uint>>>();
            lRemovedSCCs = new List<Dictionary<uint, List<uint>>>();
            lDeadLockCycle = new List<uint>();

            // 调用求解器，在以环段为点的点图中找到包含起始环段的强连通分量（唯一）
            oSCCS = new StrongConnectedComponentsSolver(dDirGraphInCycleSegIDVectors);
            oSCCS.Solve(lCycleSegIDsOfPotentialFullCycles);
            if (oSCCS.lStrongConnComponents.Count > 0)
            {
                if (this.IfSelfCheckAndThrowException && oSCCS.lStrongConnComponents.Count > 1)
                    throw new SimException("Strong Connection Component Should Never Be More Than One");
                if (oSCCS.lStrongConnComponents[0].Count > 1)
                    lKeptSCCs.Add(oSCCS.lStrongConnComponents[0]);
                else
                    lRemovedSCCs.Add(oSCCS.lStrongConnComponents[0]);
            }
            oSCCS = null;

            if (this.IfStrongComponentLog)
                this.PrintOutStrongConnComponents(lRemovedSCCs, lKeptSCCs);

            // 用递归（深度优先搜索）在强连通分量中寻找死锁结构，可能是环结构，也可能是非环结构（某AGV在下一段没有通行权）。
            /* 在搜索过程中的某步，判断不会形成死锁结构的条件
             * 1) 结构未确定，AGV 将从当前结构中全部流失
             * 2) 结构未确定，当前结构长度超出上限 
             * 3) 结构已经确定，且结构的长度足够包含全部AGV
             */
            bFullCycleFound = false;
            if (lKeptSCCs.Count > 0)
            {
                foreach (uint i in lKeptSCCs[0].Keys)
                    lKeptSCCs[0][i].OrderBy(u => this.dCycleSegs[u].Length);

                lTempCycleSegs = this.dCycleSegs.Values.Where(u => lKeptSCCs[0].ContainsKey(u.ID)).ToList();
                lAGVIDs = new List<uint>();
                lTempCycleSegs.ForEach(u => lAGVIDs = lAGVIDs.Union(u.lContainAGVIDs).ToList());
                MaxLength = lAGVIDs.Count * this.AGVEquivalentLength;

                // 死锁起始判断路径段列表。从占领(点、方向锁路径段)到离开，涉及的所有路径段
                if (lDirLockCSIDs.Count == 0)
                {
                    lDeadLockJudgeStartSegIDs = new List<uint>() { CycleSegIDInput };
                    TempSegID = CycleSegIDInput;
                    TempLength = this.dCycleSegs[CycleSegIDInput].Length;
                }
                else
                {
                    lDeadLockJudgeStartSegIDs = new List<uint>(lDirLockCSIDs);
                    TempSegID = lDirLockCSIDs.First(u => !lDirLockCSIDs.Exists(v => this.dCycleSegs[u].lOrderedTPIDs.Last() == this.dCycleSegs[v].lOrderedTPIDs[0]));
                    TempLength = 0;
                }
                while (TempLength <= this.AGVEquivalentLength)
                {
                    oCycleSeg = this.dCycleSegs.Values.FirstOrDefault<CycleSeg>(u => u.lRouteAGVIDs.Contains(AGVIDInput) && u.lOrderedTPIDs[0] == this.dCycleSegs[TempSegID].lOrderedTPIDs.Last());
                    if (oCycleSeg == null)
                        break;
                    TempSegID = oCycleSeg.ID;
                    lDeadLockJudgeStartSegIDs.Add(TempSegID);
                    TempLength = TempLength + oCycleSeg.Length;
                }

                // 反转以保证车的顺序和路径延伸顺序一致，利于反锁的相关判断
                lCurrSegPassingAGVIDs = new List<uint>(this.dCycleSegs[CycleSegIDInput].lContainAGVIDs);
                lCurrSegPassingAGVIDs.Reverse();
                lCurrInConsAGVIDs = new List<uint>(lCurrSegPassingAGVIDs);
                if (this.dEssentialAndQCTPs[this.dCycleSegs[CycleSegIDInput].lOrderedTPIDs.Last()].ResvAGVID > 0
                    && lCurrInConsAGVIDs.Contains(this.dEssentialAndQCTPs[this.dCycleSegs[CycleSegIDInput].lOrderedTPIDs.Last()].ResvAGVID))
                    lCurrInConsAGVIDs.Remove(this.dEssentialAndQCTPs[this.dCycleSegs[CycleSegIDInput].lOrderedTPIDs.Last()].ResvAGVID);
                if (this.dEssentialAndQCTPs[this.dCycleSegs[CycleSegIDInput].lOrderedTPIDs.Last()].ResvAGVID == lCurrInConsAGVIDs.Last())
                    lCurrInConsAGVIDs.RemoveAt(lCurrInConsAGVIDs.Count - 1);

                if (this.IfDeadLockDetectLog)
                {
                    this.fs_Search = new FileStream(System.Environment.CurrentDirectory + "\\PrintOut\\SearchProcesses\\SearchProcess_" + this.FullCycleDetectTime.ToString().PadLeft(8, '0') + ".txt", FileMode.Create);
                    this.sw_Search = new StreamWriter(this.fs_Search, Encoding.Default);
                    this.sw_Search.WriteLine(DateTime.Now.ToString() + "\r\n");
                    this.sw_Search.WriteLine("AGV : " + AGVIDInput.ToString() + " Search For DeadLock Construction From Point : " + this.dCycleSegs[CycleSegIDInput].lOrderedTPIDs[0].ToString()
                         + " And Cycle Seg ID : " + CycleSegIDInput.ToString() + " Within MaxLength : " + MaxLength.ToString());
                    this.sw_Search.Write("AGVIDs In Strong Connect Components : ");
                    lAGVIDs.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                    this.sw_Search.Write("\r\nDeadlock Judge Related Cycle Seg IDs : ");
                    lDeadLockJudgeStartSegIDs.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                    this.sw_Search.Write("\r\nCurrent Seg Passing AGVIDs : ");
                    lCurrSegPassingAGVIDs.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                    this.sw_Search.Write("\r\nCurrent In Construction AGVIDs : ");
                    lCurrInConsAGVIDs.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                    this.sw_Search.WriteLine("");
                }
                
                bFullCycleFound = this.SearchForDeadLockConstruction(AGVIDInput, lDeadLockJudgeStartSegIDs, CycleSegIDInput, MaxLength, 0, 
                    ref lDeadLockCycle, lCurrSegPassingAGVIDs, lCurrInConsAGVIDs, new List<uint>(), lKeptSCCs[0]);
                if (this.IfDeadLockDetectLog)
                {
                    this.sw_Search.Close();
                    this.fs_Search.Close();
                }
            }

            if (this.IfDeadLockDetectLog)
            {
                this.PrintOutDeadLockCycle(bFullCycleFound, lDeadLockCycle);
                this.lFullCycleDetectRecords.Add(this.FullCycleDetectTime);
                if (this.FullCycleDetectTime - this.TPReclaimRecordNum > 0 && this.lFullCycleDetectRecords.Contains(this.FullCycleDetectTime - this.TPReclaimRecordNum))
                {
                    FileName = System.Environment.CurrentDirectory + "\\PrintOut\\FullCycleDetectInputs\\FullCycleDetectInputs_" 
                        + (this.FullCycleDetectTime - this.TPReclaimRecordNum).ToString().PadLeft(8, '0') + ".txt";
                    if (File.Exists(FileName))
                        File.Delete(FileName);
                    FileName = System.Environment.CurrentDirectory + "\\PrintOut\\SolverInputs\\SolverInput_"
                        + (this.FullCycleDetectTime - this.TPReclaimRecordNum).ToString().PadLeft(8, '0') + ".txt";
                    if (File.Exists(FileName))
                        File.Delete(FileName);
                    FileName = System.Environment.CurrentDirectory + "\\PrintOut\\SolverOutputs\\SolverOutput_"
                        + (this.FullCycleDetectTime - this.TPReclaimRecordNum).ToString().PadLeft(8, '0') + ".txt";
                    if (File.Exists(FileName))
                        File.Delete(FileName);
                    FileName = System.Environment.CurrentDirectory + "\\PrintOut\\SearchProcesses\\SearchProcess_"
                        + (this.FullCycleDetectTime - this.TPReclaimRecordNum).ToString().PadLeft(8, '0') + ".txt";
                    if (File.Exists(FileName))
                        File.Delete(FileName);
                    this.lFullCycleDetectRecords.Remove(this.FullCycleDetectTime - this.TPReclaimRecordNum);
                }
            }

            this.lCycleDetectTimeLengths.Add(new TimeSpan(DateTime.Now.Ticks).Subtract(new TimeSpan(dTemp.Ticks)).TotalSeconds);

            return bFullCycleFound;
        }


        /// <summary>
        /// 确认当前的环段下是否能找到死锁结构
        /// </summary>
        /// <param name="ReclaimAGVID">预约磁钉的AGV编号</param>
        /// <param name="lStartSegIDs">起始路径段编号列表</param>
        /// <param name="CurrSegID">当前路径段编号</param>
        /// <param name="MaxLength">最大结构长度</param>
        /// <param name="PreTotalLength">累积结构长度</param>
        /// <param name="lCurrConstruction">累积路径段编号集合</param>
        /// <param name="lCurrSegPassingAGVIDs">将通过当前路径段的 AGV 编号列表</param>
        /// <param name="lCurrInConsAGVIDs">搜索到 CurrSegID 时，确定留在结构内的 AGV 编号列表</param>
        /// <param name="lFixedInConsAGVIDs">不管最后路径段为何，都会留在结构内的 AGV 列表</param>
        /// <param name="StrongConnComponent">强连通分量</param>
        /// <returns>能找到返回true，不能找到返回false</returns>
        private bool SearchForDeadLockConstruction(uint ReclaimAGVID, List<uint> lStartSegIDs, uint CurrSegID, double MaxLength, double PreTotalLength, ref List<uint> lCurrConstruction, 
            List<uint> lCurrSegPassingAGVIDs, List<uint> lCurrInConsAGVIDs, List<uint> lFixedInConsAGVIDs, Dictionary<uint, List<uint>> StrongConnComponent)
        {
            bool bRet, IfGoOnForRouteExtend, IfGoOnForRouteBlocked, IfGoOnForDirLocked, IfGoOnForBackToStart;
            double PostTotalLength;
            uint LinkTPID, LastExtendAGVID, TempCSID, NextSegFirstAntiLockedAGVID, TempAGVID;
            List<uint> lNextSegIDs, lNextInConsAGVIDs, lTempAGVIDs, lTempCSIDs, lNextSegPassingAGVIDs, lCurrToNextAGVIDs, lNextFixedInConsAGVIDs, lDirLockAGVIDsAtLinkTP;

            if (this.IfDeadLockDetectLog)
            {
                this.sw_Search.WriteLine("\r\nCome To Cycle Seg : " + CurrSegID.ToString());
                this.sw_Search.Write("Current Seg Passing AGVs :");
                lCurrSegPassingAGVIDs.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                this.sw_Search.Write("\r\nCurrent In Construction AGVs :");
                lCurrInConsAGVIDs.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                this.sw_Search.Write("\r\nFixed In Construction AGVs :");
                lFixedInConsAGVIDs.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                this.sw_Search.WriteLine("");
            }

            // 如果超过达到长度上限，自本 CS 往后不具备成为死锁结构的资格。
            if (this.IfDeadLockDetectLog)
                this.sw_Search.WriteLine("\r\nLength Before Adding New CycleSeg : {0} And CurrCycleSeg Length : {1}", PreTotalLength, this.dCycleSegs[CurrSegID].Length);
            PostTotalLength = PreTotalLength + this.dCycleSegs[CurrSegID].Length;
            if (PostTotalLength > MaxLength)
            {
                if (this.IfDeadLockDetectLog)
                    this.sw_Search.WriteLine("\r\nExceed Maximal Length : {0} Hence Return", MaxLength);
                return false;
            }

            // 如果回到起始路径段，判断有没有死锁环结构
            // 已经搜索过的路径段，分情况考虑，不应形成溢出闭环
            if (lCurrConstruction.Contains(CurrSegID))
            {
                if (this.IfDeadLockDetectLog)
                    this.sw_Search.Write("\r\n Current Seg Has Been Searched");

                // 非起始段无所谓，NextSegID之后的路径段跳过。
                if (!lStartSegIDs.Contains(CurrSegID))
                {
                    if (this.IfDeadLockDetectLog)
                        this.sw_Search.WriteLine("\r\n But Not In The Start Cycle Seg List Hence No Further Search");
                    return false;
                }
                // 起始段，可能形成死锁。若未检出死锁，随后的路径段应继续。
                else
                {
                    if (this.IfDeadLockDetectLog)
                        this.sw_Search.WriteLine("\r\n And In The Start Cycle Seg List Hence Potential DeadLock Structure Found");

                    lTempCSIDs = new List<uint>(lCurrConstruction);
                    lTempCSIDs.RemoveRange(0, lTempCSIDs.IndexOf(CurrSegID));

                    bRet = this.IsConstructionDeadLocked(ReclaimAGVID, StatusEnums.ConsType.ClosedCycle, lTempCSIDs, lFixedInConsAGVIDs.Union(lCurrInConsAGVIDs).ToList());

                    return bRet;
                }
            }

            // 加入新路径段
            lCurrConstruction.Add(CurrSegID);
            if (this.IfDeadLockDetectLog)
            {
                this.sw_Search.Write("\r\nConstruction With Current Seg Added : {");
                lCurrConstruction.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                this.sw_Search.WriteLine("}");
            }

            // 继续往下搜索，反向路径段排除，按照终点据起始点的距离排序
            bRet = false;
            lNextSegIDs = new List<uint>(StrongConnComponent[CurrSegID]);
            LinkTPID = this.dCycleSegs[CurrSegID].lOrderedTPIDs.Last();
            if (this.dCycleSegs[CurrSegID].IfHasReversedCycleSeg)
                lNextSegIDs.Remove(this.dCycleSegs[CurrSegID].ReversedCycleSegID);
            if (lNextSegIDs.Count > 0)
            {
                TempCSID = lCurrConstruction[0];
                lNextSegIDs.Sort((u, v) => this.NextSegDisCompareFunc(this.dCycleSegs[TempCSID].lOrderedTPIDs[0], u, v));
            }
            if (this.IfDeadLockDetectLog)
            {
                this.sw_Search.Write("\r\nNext Seg IDs : {");
                lNextSegIDs.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                this.sw_Search.WriteLine("}");
            }

            lDirLockAGVIDsAtLinkTP = new List<uint>();
            foreach (List<uint> lDirLockAgvIds in this.dEssentialAndQCTPs[LinkTPID].dDirLockAGVLists.Values)
                lDirLockAGVIDsAtLinkTP = lDirLockAGVIDsAtLinkTP.Union(lDirLockAgvIds).ToList();
            foreach (List<uint> lDirLockAgvIds in this.dEssentialAndQCTPs[LinkTPID].dDirLockTailAGVLists.Values)
                lDirLockAGVIDsAtLinkTP = lDirLockAGVIDsAtLinkTP.Union(lDirLockAgvIds).ToList();

            foreach (uint NextSegID in lNextSegIDs)
            {
                if (this.IfDeadLockDetectLog)
                    this.sw_Search.Write("\r\nNext Seg ID : {0}", NextSegID.ToString());

                /* 能够向路径段 NextSeg 继续搜索的条件：
                 * 1) 延续条件：lCurrSegPassingAGVIDs 集合内，有驶向路径段 NextSeg 的 AGV
                 * 2）阻隔条件：CurrSeg 出口磁钉被占据，占据磁钉的车辆驶向 NextSeg 但不经过 CurrSeg
                 * 3) 方向锁条件：磁钉上有 AGV 向 NextSeg 方向加方向锁，因此为这些 AGV 预留优先级
                 */

                IfGoOnForRouteExtend = false;
                IfGoOnForRouteBlocked = false;
                IfGoOnForDirLocked = false;
                IfGoOnForBackToStart = false;

                lCurrToNextAGVIDs = lCurrSegPassingAGVIDs.Intersect(this.dCycleSegs[NextSegID].lRouteAGVIDs).ToList();
                if (lCurrToNextAGVIDs.Count > 0)
                    IfGoOnForRouteExtend = true;
                if (this.dEssentialAndQCTPs[LinkTPID].ResvAGVID > 0
                    && !this.dCycleSegs[CurrSegID].lContainAGVIDs.Contains(this.dEssentialAndQCTPs[LinkTPID].ResvAGVID)
                    && this.dCycleSegs[NextSegID].lContainAGVIDs.Contains(this.dEssentialAndQCTPs[LinkTPID].ResvAGVID))
                    IfGoOnForRouteBlocked = true;
                if (this.dCycleSegs[NextSegID].lRouteAGVIDs.Exists(u => lDirLockAGVIDsAtLinkTP.Contains(u)))
                    IfGoOnForDirLocked = true;
                if (lStartSegIDs.Contains(NextSegID))
                    IfGoOnForBackToStart = true;

                // 如果延续或者阻隔或者方向锁则继续
                if (IfGoOnForRouteExtend || IfGoOnForRouteBlocked || IfGoOnForDirLocked || IfGoOnForBackToStart)
                {
                    if (this.IfDeadLockDetectLog)
                        this.sw_Search.WriteLine(" Is Searched On For : Extend ? {0}, Blocked ? {1}, Dir Locked ? {2}, Back To Start ? {3}",
                            IfGoOnForRouteExtend, IfGoOnForRouteBlocked, IfGoOnForDirLocked, IfGoOnForBackToStart);
                }
                // 否则，没有搜索的必要
                else
                {
                    if (this.IfDeadLockDetectLog)
                        this.sw_Search.WriteLine(" Is Neither Extended Nor Blocked Nor Locked Hence Excluded From Candidates");
                    continue;
                }

                // lNextFixedInConsAGVIDs 和 lPostInConsAGVIDs 更新，前者是指一定锁在结构里面的AGV列表，后者是除了前者以外，因路径段选择而暂时无法自行脱离的AGV
                lNextFixedInConsAGVIDs = new List<uint>(lFixedInConsAGVIDs);
                lNextInConsAGVIDs = new List<uint>(lCurrInConsAGVIDs);
                if (((IfGoOnForRouteBlocked || IfGoOnForDirLocked) && !IfGoOnForRouteExtend) || IfGoOnForBackToStart)
                {
                    lNextFixedInConsAGVIDs = lNextFixedInConsAGVIDs.Union(lCurrInConsAGVIDs).ToList();
                    lNextInConsAGVIDs.RemoveAll(u => lNextFixedInConsAGVIDs.Contains(u));
                }

                // 因为确定了要走下一段，因此在第一个开进下一段的车脱离 CurrSeg 之前，跟着它的 AGV 暂时无法脱离结构
                if (lCurrSegPassingAGVIDs.Exists(u => this.dCycleSegs[NextSegID].lRouteAGVIDs.Contains(u)))
                {
                    LastExtendAGVID = lCurrSegPassingAGVIDs.Last(u => this.dCycleSegs[NextSegID].lRouteAGVIDs.Contains(u));
                    lTempAGVIDs = new List<uint>(lCurrSegPassingAGVIDs);
                    lTempAGVIDs.RemoveAll(u => lCurrSegPassingAGVIDs.IndexOf(u) > lCurrSegPassingAGVIDs.IndexOf(LastExtendAGVID));
                    lNextInConsAGVIDs = lNextInConsAGVIDs.Union(lTempAGVIDs).ToList();
                }
                else if (lNextInConsAGVIDs.Exists(u => this.dCycleSegs[NextSegID].lRouteAGVIDs.Contains(u)))
                {
                    LastExtendAGVID = lNextInConsAGVIDs.Last(u => this.dCycleSegs[NextSegID].lRouteAGVIDs.Contains(u));
                    lNextInConsAGVIDs.RemoveAll(u => lNextInConsAGVIDs.IndexOf(u) > lNextInConsAGVIDs.IndexOf(LastExtendAGVID));
                }
                else
                    lNextInConsAGVIDs.Clear();

                // lNextSegPassingAGVIDs 更新
                lNextSegPassingAGVIDs = new List<uint>(lCurrSegPassingAGVIDs);

                // 上方向锁的 AGV 排在前面
                if (IfGoOnForDirLocked)
                    lNextSegPassingAGVIDs = lNextSegPassingAGVIDs.Union(lDirLockAGVIDsAtLinkTP.Intersect(this.dCycleSegs[NextSegID].lRouteAGVIDs)).ToList();
                lNextSegPassingAGVIDs = lNextSegPassingAGVIDs.Intersect(this.dCycleSegs[NextSegID].lRouteAGVIDs).ToList();
                lTempAGVIDs = new List<uint>(this.dCycleSegs[NextSegID].lContainAGVIDs);
                lTempAGVIDs.Reverse();
                lNextSegPassingAGVIDs = lNextSegPassingAGVIDs.Union(lTempAGVIDs).ToList();

                // 注意走下一段的 AGV 也应该包含在结构内，但不应包含已经占据下一段出口磁钉的 AGV
                lTempAGVIDs = new List<uint>(lNextSegPassingAGVIDs);
                if (this.dEssentialAndQCTPs[this.dCycleSegs[NextSegID].lOrderedTPIDs.Last()].ResvAGVID > 0
                    && lTempAGVIDs.Contains(this.dEssentialAndQCTPs[this.dCycleSegs[NextSegID].lOrderedTPIDs.Last()].ResvAGVID))
                    lTempAGVIDs.Remove(this.dEssentialAndQCTPs[this.dCycleSegs[NextSegID].lOrderedTPIDs.Last()].ResvAGVID);
                lNextInConsAGVIDs = lNextInConsAGVIDs.Union(lTempAGVIDs).ToList();

                // 如果有去 NextSegID 的 AGV 被反锁，需要确认其不会对上锁车的路径通过造成影响
                if (lNextSegPassingAGVIDs.Exists(u => this.dCycleSegs[NextSegID].dAntiLockedAGVIDs.ContainsKey(u)))
                {
                    lTempAGVIDs = new List<uint>(lNextFixedInConsAGVIDs).Union(lNextInConsAGVIDs).Union(lNextSegPassingAGVIDs).ToList();
                    lTempCSIDs = new List<uint>(lCurrConstruction);

                    // 第一个到达 NextSeg 的被反锁 AGV
                    NextSegFirstAntiLockedAGVID = lNextSegPassingAGVIDs.Last(u => this.dCycleSegs[NextSegID].dAntiLockedAGVIDs.ContainsKey(u));

                    if (this.IfDeadLockDetectLog)
                        this.sw_Search.WriteLine("\r\nAGV : {0} Is The First Anti-Locked AGV At Next Seg", NextSegFirstAntiLockedAGVID);

                    // 第一被反锁车之前到达 NextSeg 的车都去掉
                    lTempAGVIDs.RemoveRange(lTempAGVIDs.IndexOf(NextSegFirstAntiLockedAGVID) + 1, lTempAGVIDs.Count - lTempAGVIDs.IndexOf(NextSegFirstAntiLockedAGVID) - 1);

                    // 如果是被路径内的车反锁，则希望不会对上锁车先通过的优先级造成影响。
                    if (lTempAGVIDs.Exists(u => this.dCycleSegs[NextSegID].dAntiLockedAGVIDs[NextSegFirstAntiLockedAGVID].Contains(u)))
                    {
                        TempAGVID = lTempAGVIDs.First(u => this.dCycleSegs[NextSegID].dAntiLockedAGVIDs[NextSegFirstAntiLockedAGVID].Contains(u));

                        if (this.IfDeadLockDetectLog)
                            this.sw_Search.Write("\r\nAnti Locked By AGV : {0} Hence Adjust", TempAGVID);

                        lTempCSIDs.RemoveRange(0, lTempCSIDs.IndexOf(this.dCycleSegs[NextSegID].ReversedCycleSegID) + 1);
                        lTempAGVIDs.RemoveRange(0, lTempAGVIDs.IndexOf(TempAGVID));
                    }

                    if (this.IfDeadLockDetectLog)
                    {
                        this.sw_Search.Write("\r\nOpen Cycle Construction : ");
                        lTempCSIDs.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                        this.sw_Search.Write("\r\nAGVs In Construction : ");
                        lTempAGVIDs.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                        this.sw_Search.WriteLine("");
                    }

                    // 开结构死锁判断
                    bRet = this.IsConstructionDeadLocked(ReclaimAGVID, StatusEnums.ConsType.OpenCycle, lTempCSIDs, lTempAGVIDs);

                    if (bRet)
                        break;
                }

                // 如果不能确定当前路径段是否会形成死锁，则进一步递归
                if (this.IfDeadLockDetectLog)
                    this.sw_Search.WriteLine("\r\nFurther Search In Need At Cycle Seg : {0}", NextSegID);

                bRet = this.SearchForDeadLockConstruction(ReclaimAGVID, lStartSegIDs, NextSegID, MaxLength, PostTotalLength, 
                    ref lCurrConstruction, lNextSegPassingAGVIDs, lNextInConsAGVIDs, lNextFixedInConsAGVIDs, StrongConnComponent);

                if (!bRet && this.IfDeadLockDetectLog)
                    this.sw_Search.Write("\r\nBack From Seg : " + NextSegID.ToString() + " Into Seg : " + CurrSegID.ToString() + "  With No DeadLock Cycle Found");

                if (bRet)
                    break;
            }

            if (!bRet)
            {
                lCurrConstruction.Remove(CurrSegID);
                if (this.IfDeadLockDetectLog)
                {
                    this.sw_Search.Write("\r\nConstruction With Last Cycle Seg Removed : {");
                    lCurrConstruction.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                    this.sw_Search.WriteLine("}");
                }
            }

            return bRet;
        }

        /// <summary>
        /// 下一路径段的排序函数，路径段出口离起始点越近越优先
        /// </summary>
        /// <param name="StartTPID">起始磁钉号</param>
        /// <param name="Seg1">比较路径段编号1</param>
        /// <param name="Seg2">比较路径段编号2</param>
        /// <returns></returns>
        private int NextSegDisCompareFunc(uint StartTPID, uint Seg1, uint Seg2)
        {
            int iRet = 0;
            double dis1, dis2;

            if (Seg1 == Seg2)
                return iRet;

            dis1 = this.GetManhattanDistance(this.dEssentialAndQCTPs[StartTPID].LogicPosX, this.dEssentialAndQCTPs[StartTPID].LogicPosY,
                this.dEssentialAndQCTPs[this.dCycleSegs[Seg1].lOrderedTPIDs.Last()].LogicPosX, this.dEssentialAndQCTPs[this.dCycleSegs[Seg1].lOrderedTPIDs.Last()].LogicPosY);

            dis2 = this.GetManhattanDistance(this.dEssentialAndQCTPs[StartTPID].LogicPosX, this.dEssentialAndQCTPs[StartTPID].LogicPosY,
                this.dEssentialAndQCTPs[this.dCycleSegs[Seg2].lOrderedTPIDs.Last()].LogicPosX, this.dEssentialAndQCTPs[this.dCycleSegs[Seg2].lOrderedTPIDs.Last()].LogicPosY);

            if (dis1 > dis2)
                iRet = 1;
            else if (dis1 < dis2)
                iRet = -1;

            return iRet;
        }


        /// <summary>
        /// 判断给出的结构是否会形成死锁
        /// </summary>
        /// <param name="ReclaimAGVID">预约 AGV 编号</param>
        /// <param name="eConsType">结构的总体类型</param>
        /// <param name="lConstruction">路径段序列</param>
        /// <param name="lInConsAGVIDs">AGV编号列表，上方向锁的车包含在内</param>
        /// <returns>是死锁结构返回true，否则返回false</returns>
        private bool IsConstructionDeadLocked(uint ReclaimAGVID, StatusEnums.ConsType eConsType, List<uint> lConstruction, List<uint> lInConsAGVIDs)
        {
            List<uint> lTemp, lNodes, lCrossedNodes;
            List<ElementConstruction> lEleConses;

            if (lConstruction.Count == 0)
            {
                Console.WriteLine("Null Construction List!");
                return true;
            }

            if (this.IfDeadLockDetectLog)
            {
                this.sw_Search.Write("\r\nDeadLock Construction Judge :\r\nConstruction : {");
                lConstruction.ForEach(u => this.sw_Search.Write("\t{0}", u));
                this.sw_Search.WriteLine("}\r\nConstruction Type : " + eConsType.ToString());
                this.sw_Search.Write("AGVIDs In Construction : {");
                lInConsAGVIDs.ForEach(u => this.sw_Search.Write("\t{0}", u));
                this.sw_Search.WriteLine("}");
            }
            
            // 分出子结构，可能是环也可能是受阻段
            lNodes = new List<uint>();
            lCrossedNodes = new List<uint>();
            foreach (uint SegID in lConstruction)
            {
                if (!lNodes.Exists(u => u == this.dCycleSegs[SegID].lOrderedTPIDs.Last()))
                    lNodes.Add(this.dCycleSegs[SegID].lOrderedTPIDs.Last());
                else
                    lCrossedNodes.Add(this.dCycleSegs[SegID].lOrderedTPIDs.Last());
            }

            // 拆分路径段子结构集合，再在总环条件下合并首尾段，然后合单段成环，再合两段成环。这里仅仅对环段编号进行操作
            lEleConses = new List<ElementConstruction>() { new ElementConstruction() };
            lTemp = new List<uint>(lConstruction);
            foreach (uint i in lTemp)
            {
                lEleConses.Last().lCycleSegIDs.Add(i);
                if (lTemp.IndexOf(i) < lTemp.Count - 1 && lCrossedNodes.Contains(this.dCycleSegs[i].lOrderedTPIDs.Last()))
                    lEleConses.Add(new ElementConstruction());
            }
            // 各子结构定性
            foreach (ElementConstruction oEC in lEleConses)
            {
                if (this.dCycleSegs[oEC.lCycleSegIDs[0]].lOrderedTPIDs[0] == this.dCycleSegs[oEC.lCycleSegIDs.Last()].lOrderedTPIDs.Last()
                    && !(eConsType == StatusEnums.ConsType.OpenCycle && lEleConses.IndexOf(oEC) == lEleConses.Count - 1))
                    // 开环的最后一段，即使返回起始点，也不能算是子环
                    oEC.eConsType = StatusEnums.ConsType.ClosedCycle;
                else
                    oEC.eConsType = StatusEnums.ConsType.OpenCycle;
            }
            // 总闭环条件下，首尾开环且相连，则将首尾开环合并成一个环
            if (eConsType == StatusEnums.ConsType.ClosedCycle && lEleConses.Count >= 3
                && lEleConses[0].eConsType == StatusEnums.ConsType.OpenCycle
                && lEleConses.Last().eConsType == StatusEnums.ConsType.OpenCycle
                && this.dCycleSegs[lConstruction[0]].lOrderedTPIDs[0] == this.dCycleSegs[lConstruction.Last()].lOrderedTPIDs.Last())
            {
                lEleConses[0].lCycleSegIDs = lEleConses.Last().lCycleSegIDs.Union(lEleConses[0].lCycleSegIDs).ToList();
                if (this.dCycleSegs[lEleConses[0].lCycleSegIDs[0]].lOrderedTPIDs[0] == this.dCycleSegs[lEleConses[0].lCycleSegIDs.Last()].lOrderedTPIDs.Last())
                    lEleConses[0].eConsType = StatusEnums.ConsType.ClosedCycle;
                else
                    lEleConses[0].eConsType = StatusEnums.ConsType.OpenCycle;
                lEleConses.Remove(lEleConses.Last());
            }
            // 开环两两合并
            if (lEleConses.Count > 2)
            {
                for (int i = lEleConses.Count - 1; i >= 1; i--)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (lEleConses[i].eConsType == StatusEnums.ConsType.OpenCycle && lEleConses[j].eConsType == StatusEnums.ConsType.OpenCycle
                            && (this.dCycleSegs[lEleConses[i].lCycleSegIDs[0]].lOrderedTPIDs[0] == this.dCycleSegs[lEleConses[j].lCycleSegIDs.Last()].lOrderedTPIDs.Last()
                            && this.dCycleSegs[lEleConses[i].lCycleSegIDs.Last()].lOrderedTPIDs.Last() == this.dCycleSegs[lEleConses[j].lCycleSegIDs[0]].lOrderedTPIDs[0]))
                        {
                            lEleConses[j].lCycleSegIDs = lEleConses[j].lCycleSegIDs.Union(lEleConses[i].lCycleSegIDs).ToList();
                            if (lEleConses[j].lCycleSegIDs.Count == 2 
                                && this.dCycleSegs[lEleConses[j].lCycleSegIDs[0]].IfHasReversedCycleSeg
                                && this.dCycleSegs[lEleConses[j].lCycleSegIDs[0]].ReversedCycleSegID == lEleConses[j].lCycleSegIDs[1])
                                lEleConses[j].eConsType = StatusEnums.ConsType.ReversedSegsPair;
                            else
                                lEleConses[j].eConsType = StatusEnums.ConsType.ClosedCycle;
                            lEleConses.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            // 子结构赋值，Length 和 Capacity。注意，首结构非闭环时，容量+1
            for (int i = 0; i < lEleConses.Count; i++)
            {
                foreach (uint SegID in lEleConses[i].lCycleSegIDs)
                    lEleConses[i].Length = lEleConses[i].Length + this.dCycleSegs[SegID].Length;
                switch (lEleConses[i].eConsType)
                {
                    case StatusEnums.ConsType.ReversedSegsPair:
                        lEleConses[i].Capacity = Convert.ToInt32(Math.Floor(lEleConses[i].Length / this.AGVEquivalentLength / 2));
                        break;
                    case StatusEnums.ConsType.ClosedCycle:
                        lEleConses[i].Capacity = Convert.ToInt32(Math.Floor(lEleConses[i].Length / this.AGVEquivalentLength));
                        break;
                    case StatusEnums.ConsType.OpenCycle:
                        lEleConses[i].Capacity = Convert.ToInt32(Math.Floor(lEleConses[i].Length / this.AGVEquivalentLength)) + 1;
                        break;
                }

                if (lEleConses[i].eConsType != StatusEnums.ConsType.ClosedCycle
                    && lEleConses[i].lCycleSegIDs.Exists(u => dCycleSegs[u].lContainAGVIDs.Contains(ReclaimAGVID)))
                    lEleConses[i].Capacity++;
            }

            // Log
            if (this.IfDeadLockDetectLog)
            {
                // 打出结构
                this.sw_Search.Write("\r\nElement Constructions : ");
                foreach (ElementConstruction oEC in lEleConses)
                {
                    this.sw_Search.Write("\r\nCycleSegs : ");
                    oEC.lCycleSegIDs.ForEach(u => this.sw_Search.Write("\t" + u.ToString()));
                    this.sw_Search.WriteLine("\r\nType : {0}, Length : {1}, Capacity : {2}", oEC.eConsType, oEC.Length, oEC.Capacity);
                }
                this.sw_Search.WriteLine("\r\nTotal Capacity : {0}", lEleConses.Sum(u => u.Capacity));
            }

            if (lEleConses.Count == 1)
            {
                if (this.IfDeadLockDetectLog)
                    this.sw_Search.Write("\r\nStructure With Single Element Construction");

                if (lInConsAGVIDs.Count <= lEleConses[0].Capacity)
                {
                    if (this.IfDeadLockDetectLog)
                        this.sw_Search.WriteLine(" And Will Not Be Deadlocked");
                    return false;
                }
                else
                {
                    if (this.IfDeadLockDetectLog)
                        this.sw_Search.Write(" And Will Be Deadlocked");
                    return true;
                }
            }
            else
            {
                if (this.IfDeadLockDetectLog)
                    this.sw_Search.Write("\r\nStructure With Multiple Element Constructions");

                if (lInConsAGVIDs.Count < lEleConses.Sum(u => u.Capacity))
                {
                    if (this.IfDeadLockDetectLog)
                        this.sw_Search.WriteLine(" And Will Not Be Deadlocked");
                    return false;
                }
                else
                {
                    if (this.IfDeadLockDetectLog)
                        this.sw_Search.Write(" And Will Be Deadlocked");
                    return true;
                }
            }
        }


        /// <summary>
        /// 刷新移动的AGV列表及其移动顺序
        /// </summary>
        private void RenewMovingAGVsAndSeqs(double TimeLength)
        {
            Dictionary<uint, List<uint>> dFrontAGVIDs = new Dictionary<uint, List<uint>>();
            double FrontConsiderLength, CurrRoutePos, HeadRoutePos, MaxRoutePos, TempPos;

            this.lMovingAGVs = this.oSimDataStore.dAGVs.Values.Where(u => this.dAGVRoutes.ContainsKey(u.ID)).ToList();
            this.lMovingAGVs.ForEach(u => dFrontAGVIDs.Add(u.ID, new List<uint>()));

            foreach (AGV oA in this.lMovingAGVs)
            {
                FrontConsiderLength = this.GetMaxReclaimLength(oA, TimeLength);
                this.SearchForRoutePosByCoor(oA.ID, oA.MidPoint.X, oA.MidPoint.Y, out CurrRoutePos);
                HeadRoutePos = CurrRoutePos + oA.oType.Length / 2;
                MaxRoutePos = HeadRoutePos + FrontConsiderLength;
                if (MaxRoutePos > this.dAGVRoutes[oA.ID].TotalLength)
                    MaxRoutePos = this.dAGVRoutes[oA.ID].TotalLength;

                foreach (AGVOccuLineSeg oAOLS in this.lAGVOccupyLineSegs)
                {
                    if (oAOLS.AGVID != oA.ID && !(oAOLS.bStartPointHinge && oAOLS.bEndPointHinge))
                    {
                        TempPos = -1;
                        if (!oAOLS.bStartPointHinge)
                        {
                            TempPos = this.dAGVRoutes[oA.ID].GetRoutePosByLineAndPos(oAOLS.AGVLineID, oAOLS.StartPos);
                            if (TempPos >= 0 && TempPos > HeadRoutePos && TempPos <= MaxRoutePos)
                                if (!dFrontAGVIDs[oA.ID].Contains(oAOLS.AGVID))
                                    dFrontAGVIDs[oA.ID].Add(oAOLS.AGVID);
                        }
                        if (!oAOLS.bEndPointHinge)
                        {
                            TempPos = this.dAGVRoutes[oA.ID].GetRoutePosByLineAndPos(oAOLS.AGVLineID, oAOLS.EndPos);
                            if (TempPos >= 0 && TempPos > HeadRoutePos && TempPos <= MaxRoutePos)
                                if (!dFrontAGVIDs[oA.ID].Contains(oAOLS.AGVID))
                                    dFrontAGVIDs[oA.ID].Add(oAOLS.AGVID);
                        }
                    }
                }
            }

            this.lMovingAGVs.Sort((u1, u2) => u1.Equals(u2)? 0 : (dFrontAGVIDs[u1.ID].Contains(u2.ID) ? 1 : -1));
        }

        /// <summary>
        /// 刷新道路交点和基本路径段，有新路径生成时调用。
        /// </summary>
        private void RenewRouteFeatures(ref ProjectPackageToViewFrame oPPTViewFrame)
        {
            bool bIfIntersection, bIfCycleSeg;
            CycleSeg oCS;
            AGV oA;
            List<CycleSeg> lTempCSs, lTempOccuCSs, lTempAntiCSs, lTempAntiOccuCSs;
            TPInfoEnRoute oTPInfo1, oTPInfo2;
            double TempLength;
            List<SimTransponder> lTempTPs;
            List<AntiLockRecord> lAntiLockRecords;
            AntiLockRecord oALR;
            List<uint> lTPIDs;
            int TPRouteInd1, TPRouteInd2;
            uint TPID11, TPID12, TPID21, TPID22, AGVID, AGVLineID;

            this.IfRouteFeatureExpired = false;

            // 从磁钉到磁钉的维度记录当前的反锁。
            lAntiLockRecords = new List<AntiLockRecord>();
            foreach (CycleSeg obj in this.dCycleSegs.Values)
            {
                if (obj.dAntiLockedAGVIDs.Count > 0)
                {
                    for (int i = 0; i < obj.lOrderedTPIDs.Count - 1; i++)
                    {
                        lAntiLockRecords.Add(new AntiLockRecord() 
                        { 
                            StartTPID = obj.lOrderedTPIDs[i], 
                            EndTPID = obj.lOrderedTPIDs[i+1], 
                            dAntiLockAGVIDLists = new Dictionary<uint,List<uint>>(obj.dAntiLockedAGVIDs) 
                        });
                    }
                }
            }

            this.dCycleSegs.Clear();

            // 刷新交点列表，AGV 路径的交点
            // 交点只和路径有关，与 AGV 是否驶过本点（RouteTPInfo.IfPassed）无关
            // 只要存在另一路径，使这两条路径上该点的前后两点不完全相同，该点即成为交点
            this.lIntersectedTPs = new List<SimTransponder>();
            lTempTPs = this.dEssentialAndQCTPs.Values.Where(u => u.lRouteAGVIDs.Count > 1).ToList();

            foreach (SimTransponder oTP in lTempTPs)
            {
                bIfIntersection = false;
                for (int i = 0; i < oTP.lRouteAGVIDs.Count - 1; i++)   // 路径,i为车号索引
                {
                    oTPInfo1 = this.dAGVRoutes[oTP.lRouteAGVIDs[i]].lRouteTPInfos.Find(u => u.TPID == oTP.ID);
                    if (oTPInfo1 == null) 
                        continue;
                    TPRouteInd1 = this.dAGVRoutes[oTP.lRouteAGVIDs[i]].lRouteTPInfos.IndexOf(oTPInfo1);
                    if (TPRouteInd1 <= 0 || TPRouteInd1 >= this.dAGVRoutes[oTP.lRouteAGVIDs[i]].lRouteTPInfos.Count - 1)
                        continue;
                    else
                    {
                        TPID11 = this.dAGVRoutes[oTP.lRouteAGVIDs[i]].lRouteTPInfos[TPRouteInd1 - 1].TPID;
                        TPID12 = this.dAGVRoutes[oTP.lRouteAGVIDs[i]].lRouteTPInfos[TPRouteInd1 + 1].TPID;
                    }
                    for (int j = i + 1; j < oTP.lRouteAGVIDs.Count; j++)
                    {
                        oTPInfo2 = this.dAGVRoutes[oTP.lRouteAGVIDs[j]].lRouteTPInfos.Find(u => u.TPID == oTP.ID && !u.IfPassed);
                        if (oTPInfo2 == null)
                            continue;
                        TPRouteInd2 = this.dAGVRoutes[oTP.lRouteAGVIDs[j]].lRouteTPInfos.IndexOf(oTPInfo2);
                        if (TPRouteInd2 <= 0 || TPRouteInd2 >= this.dAGVRoutes[oTP.lRouteAGVIDs[j]].lRouteTPInfos.Count - 1)
                            continue;
                        else
                        {
                            TPID21 = this.dAGVRoutes[oTP.lRouteAGVIDs[j]].lRouteTPInfos[TPRouteInd2 - 1].TPID;
                            TPID22 = this.dAGVRoutes[oTP.lRouteAGVIDs[j]].lRouteTPInfos[TPRouteInd2 + 1].TPID;
                        }
                        if (!(TPID11 == TPID21 && TPID12 == TPID22))
                        {
                            bIfIntersection = true;
                            break;
                        }
                    }
                    if (bIfIntersection)
                        break;
                }
                if (bIfIntersection)
                    this.lIntersectedTPs.Add(oTP);
            }

            // 列出所有基本路径段。
            /* 注意：（1）路径段只在交点间产生，路径段内没有其他交点。
             * （2）按照各 AGV 的路径路径段，CycleSeg 有 lContainAGVIDs 属性，用于收集还没穿过环段的 AGVID 。过环段两端但不入的 AGVID 仅从磁钉读取。
             * （3）如果 AGV 已经驶过环段终点，则此环段不计入其考虑范围
             * 先整理 CycleSeg，再刷新其 lContainAGVIDs, IfFull 和 IfExtiBlocked 属性
             */
            for (int i = 0; i < this.lIntersectedTPs.Count; i++)
            {
                foreach (uint AgvId in this.lIntersectedTPs[i].lUnPassedAGVIDs)
                {
                    bIfCycleSeg = false;
                    oTPInfo2 = this.dAGVRoutes[AgvId].lRouteTPInfos.Find(u => u.TPID == this.lIntersectedTPs[i].ID);
                    if (oTPInfo2 == null || oTPInfo2.IfPassed)
                        continue;
                    TPRouteInd2 = this.dAGVRoutes[AgvId].lRouteTPInfos.IndexOf(oTPInfo2);
                    lTPIDs = new List<uint>() { oTPInfo2.TPID };
                    TempLength = 0;
                    TPRouteInd1 = TPRouteInd2 - 1;

                    while (TPRouteInd1 >= 0)
                    {
                        oTPInfo1 = this.dAGVRoutes[AgvId].lRouteTPInfos[TPRouteInd1];
                        lTPIDs.Add(oTPInfo1.TPID);
                        TempLength = TempLength + this.GetManhattanDistance(
                            this.dEssentialAndQCTPs[oTPInfo1.TPID].LogicPosX,
                            this.dEssentialAndQCTPs[oTPInfo1.TPID].LogicPosY,
                            this.dEssentialAndQCTPs[oTPInfo2.TPID].LogicPosX,
                            this.dEssentialAndQCTPs[oTPInfo2.TPID].LogicPosY);
                        if (this.lIntersectedTPs.Exists(u => u.ID == oTPInfo1.TPID))
                        {
                            bIfCycleSeg = true;
                            lTPIDs.Reverse();
                            break;
                        }
                        else
                        {
                            TPRouteInd1--;
                            oTPInfo2 = oTPInfo1;
                        }
                    }

                    if (bIfCycleSeg)
                    {
                        lTempCSs = this.dCycleSegs.Values.Where(u => u.lOrderedTPIDs.SequenceEqual(lTPIDs)).ToList();
                        if (lTempCSs.Count > 0)
                            lTempCSs[0].lRouteAGVIDs.Add(AgvId);
                        else
                        {
                            oCS = new CycleSeg()
                            {
                                ID = (uint)this.dCycleSegs.Count,
                                lRouteAGVIDs = new List<uint>() { AgvId },
                                Length = TempLength,
                                lOrderedTPIDs = new List<uint>(lTPIDs),
                            };
                            this.dCycleSegs.Add(oCS.ID, oCS);
                        }
                    }
                }
            }

            // 给基本环段赋值。注意lContainerAGVIDs要求AGV必须占据路径段的一部分而不只是占据磁钉，且这个集合有排序的问题。
            foreach (CycleSeg obj in this.dCycleSegs.Values)
            {
                // 对环段上的任意两个相邻磁钉，可能磁钉上有占据AGV，可能无占据但位于两个磁钉之间。对于后者，应该不在转弯状态。
                for (int i = obj.lOrderedTPIDs.Count - 1; i >= 0; i--)
                {
                    if (this.dEssentialAndQCTPs[obj.lOrderedTPIDs[i]].ResvAGVID > 0 
                        && obj.lRouteAGVIDs.Contains(this.dEssentialAndQCTPs[obj.lOrderedTPIDs[i]].ResvAGVID))
                    {
                        AGVID = this.dEssentialAndQCTPs[obj.lOrderedTPIDs[i]].ResvAGVID;
                        if (obj.lRouteAGVIDs.Contains(AGVID) && !obj.lContainAGVIDs.Contains(AGVID))
                            obj.lContainAGVIDs.Add(AGVID);
                    }
                    if (i > 0)
                    {
                        TPID11 = obj.lOrderedTPIDs[i - 1];
                        TPID12 = obj.lOrderedTPIDs[i];
                        foreach (uint AgvId in obj.lRouteAGVIDs)
                        {
                            if (!obj.lContainAGVIDs.Contains(AgvId) && this.lAGVOccupyLineSegs.Count(u => u.AGVID == AgvId) == 1)
                            {
                                oA = this.lMovingAGVs.First(u => u.ID == AgvId);
                                AGVLineID = this.lAGVOccupyLineSegs.First(u => u.AGVID == AgvId).AGVLineID;
                                if (this.dEssentialAndQCTPs[TPID11].HorizontalLineID == AGVLineID && this.dEssentialAndQCTPs[TPID12].HorizontalLineID == AGVLineID)
                                {
                                    if ((this.dEssentialAndQCTPs[TPID11].LogicPosX > oA.MidPoint.X && oA.MidPoint.X > this.dEssentialAndQCTPs[TPID12].LogicPosX)
                                        || (this.dEssentialAndQCTPs[TPID11].LogicPosX < oA.MidPoint.X && oA.MidPoint.X < this.dEssentialAndQCTPs[TPID12].LogicPosX))
                                        obj.lContainAGVIDs.Add(AgvId);
                                }
                                else if (this.dEssentialAndQCTPs[TPID11].VerticalLineID == AGVLineID && this.dEssentialAndQCTPs[TPID12].VerticalLineID == AGVLineID)
                                {
                                    if ((this.dEssentialAndQCTPs[TPID11].LogicPosY > oA.MidPoint.Y && oA.MidPoint.Y > this.dEssentialAndQCTPs[TPID12].LogicPosY)
                                        || (this.dEssentialAndQCTPs[TPID11].LogicPosY < oA.MidPoint.Y && oA.MidPoint.Y < this.dEssentialAndQCTPs[TPID12].LogicPosY))
                                        obj.lContainAGVIDs.Add(AgvId);
                                }
                            }
                        }
                    }
                }
            }

            // 确定正反环段
            foreach (uint iKey1 in this.dCycleSegs.Keys)
            {
                foreach (uint iKey2 in this.dCycleSegs.Keys)
                {
                    if (iKey1 < iKey2)
                    {
                        if (this.dCycleSegs[iKey1].lOrderedTPIDs.Union(this.dCycleSegs[iKey2].lOrderedTPIDs).Count()
                            == this.dCycleSegs[iKey1].lOrderedTPIDs.Intersect(this.dCycleSegs[iKey2].lOrderedTPIDs).Count())
                        {
                            this.dCycleSegs[iKey1].IfHasReversedCycleSeg = true;
                            this.dCycleSegs[iKey1].ReversedCycleSegID = iKey2;
                            this.dCycleSegs[iKey2].IfHasReversedCycleSeg = true;
                            this.dCycleSegs[iKey2].ReversedCycleSegID = iKey1;
                        }
                    }
                }
            }

            // 补上更新前的反锁
            foreach (CycleSeg obj in this.dCycleSegs.Values)
            {
                for (int i = 0; i < obj.lOrderedTPIDs.Count - 1; i++)
                {
                    oALR = lAntiLockRecords.FirstOrDefault<AntiLockRecord>(u => u.StartTPID == obj.lOrderedTPIDs[i] && u.EndTPID == obj.lOrderedTPIDs[i + 1]);
                    if (oALR != null)
                    {
                        foreach (KeyValuePair<uint, List<uint>> oKVP in oALR.dAntiLockAGVIDLists)
                        {
                            if (!obj.dAntiLockedAGVIDs.ContainsKey(oKVP.Key))
                                obj.dAntiLockedAGVIDs.Add(oKVP.Key, new List<uint>(oKVP.Value));
                            else
                                obj.dAntiLockedAGVIDs[oKVP.Key] = obj.dAntiLockedAGVIDs[oKVP.Key].Union(oKVP.Value).ToList();
                        }
                    }
                }
            }

            // 新增路径反向被占段的反锁，仅对lContainAGVIDs.
            foreach (CycleSeg obj in this.dCycleSegs.Values)
            {
                if (obj.IfHasReversedCycleSeg && this.dCycleSegs[obj.ReversedCycleSegID].lContainAGVIDs.Count > 0)
                {
                    foreach (uint AgvId in obj.lRouteAGVIDs)
                    {
                        if (!obj.dAntiLockedAGVIDs.ContainsKey(AgvId))
                            obj.dAntiLockedAGVIDs.Add(AgvId, new List<uint>());
                        if (obj.dAntiLockedAGVIDs[AgvId].Count < this.dCycleSegs[obj.ReversedCycleSegID].lContainAGVIDs.Count)
                            obj.dAntiLockedAGVIDs[AgvId] = obj.dAntiLockedAGVIDs[AgvId].Union(this.dCycleSegs[obj.ReversedCycleSegID].lContainAGVIDs).ToList();
                    }
                }
            }

            #region 暂时封存
            /*
            // 补上本车的方向锁（磁钉）和其他车的反锁（路径段）
            // 可能导致上锁磁钉点已被其他AGV占用，但是鉴于上锁的原因是新生成的路径，且这些AGV还没进入路网，因此这种情况应该不会引起死锁。
            foreach (uint AgvId in this.dAGVRoutes.Keys)
            {
                // AgvId的剩余路径段集合
                lTempCSs = this.dCycleSegs.Values.Where(u => u.lRouteAGVIDs.Contains(AgvId)).ToList();
                // AgvId的占据路径段集合
                lTempOccuCSs = this.dCycleSegs.Values.Where(u => u.lContainAGVIDs.Contains(AgvId)).ToList();
                if (lTempOccuCSs.Count == 0)
                    continue;
                // 与 lTempOccuCSs 的某一段反向的路径段集合
                lTempAntiOccuCSs = this.dCycleSegs.Values.Where(u => u.IfHasReversedCycleSeg && lTempOccuCSs.Exists(v => u.ReversedCycleSegID == v.ID)).ToList();
                // lTempOccuCSs 的最后一段
                oCS = lTempOccuCSs[0];
                while (lTempOccuCSs.Exists(u => u.lOrderedTPIDs[0] == oCS.lOrderedTPIDs.Last()))
                    oCS = lTempOccuCSs.First(u => u.lOrderedTPIDs[0] == oCS.lOrderedTPIDs.Last());
                // 从 lTempOccuCSs 连出去的，有反向路径段的反向路径段集合。正向上反锁，反向上方向锁的范围
                lTempAntiCSs = new List<CycleSeg>(lTempAntiOccuCSs);
                while (lTempCSs.Exists(u => u.lOrderedTPIDs[0] == oCS.lOrderedTPIDs.Last() && u.IfHasReversedCycleSeg))
                {
                    oCS = lTempCSs.First(u => u.lOrderedTPIDs[0] == oCS.lOrderedTPIDs.Last() && u.IfHasReversedCycleSeg);
                    lTempAntiCSs.Add(this.dCycleSegs[oCS.ReversedCycleSegID]);
                }

                if (lTempAntiOccuCSs.Count > 0)
                {
                    foreach (CycleSeg Seg in lTempAntiCSs)
                    {
                        oCS = this.dCycleSegs[Seg.ReversedCycleSegID];
                        for (int i = 1; i < oCS.lOrderedTPIDs.Count; i++)
                        {
                            oTPInfo1 = this.dAGVRoutes[AgvId].lRouteTPInfos.Find(u => u.TPID == oCS.lOrderedTPIDs[i]);
                            if (!oTPInfo1.IfPassed)
                            {
                                if (!this.dEssentialAndQCTPs[oCS.lOrderedTPIDs[i - 1]].dDirLockAGVLists.ContainsKey(oCS.lOrderedTPIDs[i]))
                                {
                                    this.dEssentialAndQCTPs[oCS.lOrderedTPIDs[i - 1]].dDirLockAGVLists.Add(oCS.lOrderedTPIDs[i], new List<uint>());
                                    oPPTViewFrame.lTPs.Add(this.dEssentialAndQCTPs[oCS.lOrderedTPIDs[i - 1]]);
                                }
                                if (!this.dEssentialAndQCTPs[oCS.lOrderedTPIDs[i - 1]].dDirLockAGVLists[oCS.lOrderedTPIDs[i]].Contains(AgvId))
                                    this.dEssentialAndQCTPs[oCS.lOrderedTPIDs[i - 1]].dDirLockAGVLists[oCS.lOrderedTPIDs[i]].Add(AgvId);
                                if (!this.dEssentialAndQCTPs[oCS.lOrderedTPIDs[i]].dDirLockTailAGVLists.ContainsKey(oCS.lOrderedTPIDs[i - 1]))
                                {
                                    this.dEssentialAndQCTPs[oCS.lOrderedTPIDs[i]].dDirLockTailAGVLists.Add(oCS.lOrderedTPIDs[i - 1], new List<uint>());
                                    oPPTViewFrame.lTPs.Add(this.dEssentialAndQCTPs[oCS.lOrderedTPIDs[i]]);
                                }
                                if (!this.dEssentialAndQCTPs[oCS.lOrderedTPIDs[i]].dDirLockTailAGVLists[oCS.lOrderedTPIDs[i - 1]].Contains(AgvId))
                                    this.dEssentialAndQCTPs[oCS.lOrderedTPIDs[i]].dDirLockTailAGVLists[oCS.lOrderedTPIDs[i - 1]].Add(AgvId);
                            }
                        }
                    }

                    foreach (uint AgvId2 in this.dAGVRoutes.Keys)
                    {
                        if (AgvId != AgvId2)
                        {
                            // AgvId2 的未通过路径段
                            lTempCSs = this.dCycleSegs.Values.Where(u => u.lRouteAGVIDs.Contains(AgvId2)).ToList();
                            // 与 AgvId 的未通过路径段反向段的交集
                            lTempOccuCSs = lTempCSs.Intersect(lTempAntiCSs).ToList();
                            foreach (CycleSeg obj in lTempOccuCSs)
                            {
                                if (!obj.dAntiLockedAGVIDs.ContainsKey(AgvId2))
                                    obj.dAntiLockedAGVIDs.Add(AgvId2, new List<uint>());
                                if (!obj.dAntiLockedAGVIDs[AgvId2].Contains(AgvId))
                                    obj.dAntiLockedAGVIDs[AgvId2].Add(AgvId);
                            }
                        }
                    }
                }
            }
             */

            #endregion

            // Essential debug
            if (this.IfSelfCheckAndThrowException && this.CheckIfContainAGVLackingOrRepete())
                throw new SimException("Sth Wrong in List lContainAGVIDs of Some CycleSeg");

            if (this.IfDeadLockDetectLog)
            {
                this.fs_AgvResvRelease = new FileStream(System.Environment.CurrentDirectory + "\\PrintOut\\TPActions\\TPResvRelease" + (this.CurrCSVNum < 0 ? 0 : this.CurrCSVNum).ToString().PadLeft(4, '0') + ".csv", FileMode.Append);
                this.sw_AgvResvRelease = new StreamWriter(fs_AgvResvRelease, Encoding.Default);
                this.sw_AgvResvRelease.WriteLine("Route Feature Renewed");
                this.sw_AgvResvRelease.Close();
                this.fs_AgvResvRelease.Close();
            }
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
        /// 解释用，打出整个满闭环检测逻辑的输入
        /// </summary>
        /// <param name="AGVID">预约磁钉的 AGV 编号</param>
        /// <param name="CycleSegID">被预约的环段编号</param>
        private void PrintOutInputsOfFullCycleDetect(uint AGVID, uint CycleSegID)
        {
            FileStream fs = new FileStream(System.Environment.CurrentDirectory +
                "\\PrintOut\\FullCycleDetectInputs\\FullCycleDetectInputs_" + this.FullCycleDetectTime.ToString().PadLeft(8, '0') + ".txt", FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, Encoding.Default);

            sw.WriteLine(DateTime.Now.ToString());
            sw.WriteLine("\r\nAGV : " + AGVID.ToString() + " Reserve Cycle Seg ID : " + CycleSegID.ToString() + " at TP ID : " + this.dCycleSegs[CycleSegID].lOrderedTPIDs[0].ToString());

            // Route. 交点由 <> 表示
            sw.WriteLine("\r\n\r\nAGVRoutes :");

            foreach (AGVRoute oAR in this.dAGVRoutes.Values)
            {
                sw.WriteLine("\r\nAGV : " + oAR.AGVID);
                sw.Write("Lanes : ");
                foreach (uint LaneID in oAR.lRouteLaneIDs)
                {
                    sw.Write("\t" + LaneID.ToString());
                }
                sw.Write("\r\nTransponders : ");
                foreach (TPInfoEnRoute TPInfo in oAR.lRouteTPInfos)
                {
                    if (this.lIntersectedTPs.Exists(u => u.ID == TPInfo.TPID))
                        sw.Write("\t<" + TPInfo.TPID.ToString() + ">");
                    else
                        sw.Write("\t"+TPInfo.TPID.ToString());
                }
                sw.WriteLine("");
            }

            // Intersected Or Reserved Transponder
            sw.WriteLine("\r\n\r\nIntersected and Reserved TPIDs :");

            foreach (SimTransponder oTP in this.dEssentialAndQCTPs.Values)
            {
                if (this.lIntersectedTPs.Exists(u => u.ID == oTP.ID))
                {
                    if (oTP.ResvAGVID > 0)
                        sw.WriteLine("\r\nTP ID : {0};\tResv AGV : {1};\tIntersected", oTP.ID, oTP.ResvAGVID);
                    else
                        sw.WriteLine("\r\nTP ID : {0};\tNot Reserved;\tIntersected", oTP.ID);
                }
                else if (oTP.ResvAGVID > 0)
                    sw.WriteLine("\r\nTP ID : {0};\tResv AGV : {1}", oTP.ID, oTP.ResvAGVID);
                else
                    continue;
                sw.Write("RouteAGVs (Passed): ");
                foreach (uint AgvId in oTP.lRouteAGVIDs)
                {
                    if (oTP.lUnPassedAGVIDs.Exists(u => u == AgvId))
                        sw.Write("\t" + AgvId.ToString());
                    else
                        sw.Write("\t(" + AgvId.ToString() + ")");
                }
                sw.WriteLine("\r\nDir Locked Next TP Num : {0}; Dir Locked In Tail Prev TP Num : {1}", oTP.dDirLockAGVLists.Count, oTP.dDirLockTailAGVLists.Count);
                sw.WriteLine("Dir Lock List :");
                foreach (KeyValuePair<uint, List<uint>> oKVP in oTP.dDirLockAGVLists)
                {
                    sw.Write("\tNext TP : " + oKVP.Key.ToString() + "; Lock AGVs :");
                    oKVP.Value.ForEach(u => sw.Write("\t" + u.ToString()));
                    sw.WriteLine("");
                }
                sw.WriteLine("Dir Tail Lock List :");
                foreach (KeyValuePair<uint, List<uint>> oKVP in oTP.dDirLockTailAGVLists)
                {
                    sw.Write("\tPrev TP : " + oKVP.Key.ToString() + "; Lock AGVs :");
                    oKVP.Value.ForEach(u => sw.Write("\t" + u.ToString()));
                    sw.WriteLine("");
                }
            }
            
            // Cycle Segs. 想要占据的点用 () 表示
            sw.WriteLine("\r\n\r\nCycleSegs :");
            foreach (CycleSeg oCS in this.dCycleSegs.Values)
            {
                sw.Write("\r\nCycleSeg ID :\t" + oCS.ID.ToString() + "\t");

                if (oCS.ID == CycleSegID)
                    sw.WriteLine("(Reserved)");

                if (oCS.lOrderedTPIDs[0] == this.dCycleSegs[CycleSegID].lOrderedTPIDs[0]
                    || oCS.lOrderedTPIDs[0] == this.dCycleSegs[CycleSegID].lOrderedTPIDs.Last())
                    sw.Write("(" + oCS.lOrderedTPIDs[0].ToString() + ")");
                else
                    sw.Write(oCS.lOrderedTPIDs[0].ToString());

                for (int i = 1; i < oCS.lOrderedTPIDs.Count; i++)
                {
                    if (oCS.lOrderedTPIDs[i] == this.dCycleSegs[CycleSegID].lOrderedTPIDs[0]
                        || oCS.lOrderedTPIDs[i] == this.dCycleSegs[CycleSegID].lOrderedTPIDs.Last())
                        sw.Write(" --> (" + oCS.lOrderedTPIDs[i].ToString() + ")");
                    else
                        sw.Write(" --> " + oCS.lOrderedTPIDs[i].ToString());
                }

                sw.Write("\r\nRouteAGVIDs:\t");
                foreach (uint AgvID in oCS.lRouteAGVIDs)
                    sw.Write(AgvID.ToString() + "\t");
                sw.WriteLine("\r\nAntiBlockedAGVIDs:");
                foreach (uint AgvId in oCS.dAntiLockedAGVIDs.Keys)
                {
                    sw.Write("\t" + AgvId.ToString() + "\t: Blocked By :");
                    oCS.dAntiLockedAGVIDs[AgvId].ForEach(u => sw.Write("\t" + u.ToString()));
                    sw.Write("\r\n");
                }
                
                sw.Write("ContainAGVIDs:\t");
                foreach (uint AgvID in oCS.lContainAGVIDs)
                    sw.Write(AgvID.ToString() + "\t");
                sw.Write("\r\n");

                if (oCS.IfHasReversedCycleSeg)
                    sw.WriteLine("Reversed Cycle Seg ID : " + oCS.ReversedCycleSegID.ToString());

                sw.WriteLine("Length : " + oCS.Length.ToString() + "\t");
            }

            sw.Close();
            fs.Close();
        }

        /// <summary>
        /// 解释用，打出强连通分量求解器的输入
        /// </summary>
        /// <param name="bFullCycleFound">是否需要调用求解器</param>
        /// <param name="flag">不需要调用的理由</param>
        /// <param name="dDirGraph">点图输入</param>
        /// <param name="lCycleSegIDsOfPotentialFullCycles">必须包含的环段编号列表</param>
        private void PrintOutInputsOfSolvers(Dictionary<uint, List<uint>> dDirGraph, List<uint> lCycleSegIDsOfPotentialFullCycles)
        {
            FileStream fs = new FileStream(System.Environment.CurrentDirectory +
                "\\PrintOut\\SolverInputs\\SolverInput_" + this.FullCycleDetectTime.ToString().PadLeft(8, '0') + ".txt", FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, Encoding.Default);

            sw.WriteLine(DateTime.Now.ToString() + "\r\n");

            // Directed Graph in Cycle Seg IDs
            sw.WriteLine("Input Graph in Cycle Seg IDs :\r\n");
            foreach (uint i in dDirGraph.Keys)
            {
                sw.Write("F: " + i.ToString() + "\t");
                foreach (uint j in dDirGraph[i])
                    sw.Write("T: " + j.ToString() + "\t");
                sw.Write("\r\n");
            }
            sw.Write("\r\n\r\n");

            // Cycle Seg Must be contained in SCCs
            sw.Write("Cycle Seg For Potential Full Cycles :");
            foreach (uint i in lCycleSegIDsOfPotentialFullCycles)
                sw.Write("\t" + i.ToString());
            sw.Write("\r\n\r\n\r\n");

            sw.Close();
            fs.Close();
        }

        /// <summary>
        /// 解释用，打出强连通分量求解器的输出
        /// </summary>
        /// <param name="lRemovedSCCs">舍弃的强连通分量</param>
        /// <param name="lKeptSCCs">保留的强连通分量</param>
        private void PrintOutStrongConnComponents(List<Dictionary<uint, List<uint>>> lRemovedSCCs, List<Dictionary<uint, List<uint>>> lKeptSCCs)
        {
            FileStream fs = new FileStream(System.Environment.CurrentDirectory +
                "\\PrintOut\\SolverOutputs\\SolverOutput_" + this.FullCycleDetectTime.ToString().PadLeft(8, '0') + ".txt", FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, Encoding.Default);

            sw.WriteLine(DateTime.Now.ToString() + "\r\n");

            // 强连通分量
            sw.WriteLine("Strong Connect Components Removed in Cycle Seg IDs :\r\n");

            foreach (Dictionary<uint, List<uint>> dSCC in lRemovedSCCs)
            {
                sw.WriteLine("SCC ID :" + lRemovedSCCs.IndexOf(dSCC).ToString());
                foreach (uint i in dSCC.Keys)
                {
                    sw.Write("F: " + i.ToString() + "\t");
                    foreach (uint j in dSCC[i])
                        sw.Write("T: " + j.ToString() + "\t");
                    sw.Write("\r\n");
                }
                sw.Write("\r\n");
            }
            sw.Write("\r\n\r\n");


            sw.WriteLine("Strong Connect Components Kept in Cycle Seg IDs :\r\n");

            foreach (Dictionary<uint, List<uint>> dSCC in lKeptSCCs)
            {
                sw.WriteLine("SCC ID :" + lKeptSCCs.IndexOf(dSCC).ToString());
                foreach (uint i in dSCC.Keys)
                {
                    sw.Write("F: " + i.ToString() + "\t");
                    foreach (uint j in dSCC[i])
                        sw.Write("T: " + j.ToString() + "\t");
                    sw.Write("\r\n");
                }
                sw.Write("\r\n");
            }
            sw.Write("\r\n\r\n");

            sw.Close();
            fs.Close();
        }

        /// <summary>
        /// 解释用，打出死锁环
        /// </summary>
        /// <param name="bIfFullCycleFound">是否找到死锁环</param>
        /// <param name="lFullCycle">死锁环的环段编号列表</param>
        private void PrintOutDeadLockCycle(bool bIfFullCycleFound, List<uint> lFullCycle)
        {
            FileStream fs = new FileStream(System.Environment.CurrentDirectory +
                "\\PrintOut\\SolverOutputs\\SolverOutput_" + this.FullCycleDetectTime.ToString().PadLeft(8, '0') + ".txt", FileMode.Append);
            StreamWriter sw = new StreamWriter(fs, Encoding.Default);

            sw.WriteLine("If Deadlock Cycle Found : " + bIfFullCycleFound.ToString() + "\r\n\r\n");

            // 死锁环
            sw.WriteLine("Full Cycle Example In Cycle Seg IDs :\r\n");

            foreach (uint i in lFullCycle)
            {
                sw.Write(i.ToString() + "\t");
            }

            sw.Close();
            fs.Close();
        }

        // 调试用，检查基本环段 lContainAGVIDs 属性是否合法
        private bool CheckIfContainAGVLackingOrRepete()
        {
            foreach (CycleSeg oCS in this.dCycleSegs.Values)
            {
                // TP 有 lContainAGVIDs 没有
                foreach (uint TPID in oCS.lOrderedTPIDs)
                {
                    if (this.dEssentialAndQCTPs[TPID].ResvAGVID > 0
                        && oCS.lRouteAGVIDs.Contains(this.dEssentialAndQCTPs[TPID].ResvAGVID)
                        && !oCS.lContainAGVIDs.Contains(this.dEssentialAndQCTPs[TPID].ResvAGVID))
                    {
                        Console.WriteLine("TP ：" + TPID.ToString() + " Reserved By AGV : " + this.dEssentialAndQCTPs[TPID].ResvAGVID  
                            + " While Not Contained In CycleSeg : " + oCS.ID);
                        return true;
                    }
                }

                // lContainerAGVIDs 不唯一
                foreach (uint AgvId in oCS.lContainAGVIDs)
                {
                    if (oCS.lContainAGVIDs.Count(u => u == AgvId) > 1)
                    {
                        Console.WriteLine("CycleSeg : " + oCS.ID + " Contains Repeted AGV : " + AgvId.ToString());
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 死锁检测判断
        /// </summary>
        /// <returns>AGV全部停止返回true，否则返回false</returns>
        private bool CheckIfAllStopped()
        {
            bool bRet = false;

            if (this.dAGVRoutes.Count == this.oSimDataStore.dAGVs.Count && !this.lMovingAGVs.Exists(u => u.eStepTravelStatus == StatusEnums.StepTravelStatus.Move))
                bRet = true;

            return bRet;
        }

        /// <summary>
        /// 车距检测判断
        /// </summary>
        /// <returns>有一对AGV的曼哈顿距离小于车长返回true，否则返回false</returns>
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

        #endregion

    }
}
