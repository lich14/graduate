// Copyright (C) 2013-2014  ZPMC 软件自动化部 
// 文件名 : VmsAlgo.cs 
// 作者： 
// 日期： 2015/10/15
// 描述： 初始化VmsAlgo.dll算法库
// 版 本： 1.0.0.0 

// 修改历史记录 
// 版本       修改时间        修改人     修改内容


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;
using ZECS.Schedule.DB;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.YardMap;
using ZECS.Schedule.Algorithm.Utilities;
using ZECS.Schedule.VmsAlgoApplication; 


namespace ZECS.Schedule.Algorithm.Utilities
{
    /// <summary>
    /// VMS算法库接口函数
    /// </summary>
    public class VmsAlgorithm
    {
        public static ushort MAX_AVAILABLE_PB_INOUT = 68;

        private static VmsAlgorithm m_Instance;
 
        public static VmsAlgorithm Instance
        {
            get
            {
                if (m_Instance == null)
                    m_Instance = new VmsAlgorithm();
                return m_Instance;
            }
        }

        private VmsAlgorithm()
        {

        }

        #region InitializeAlgo

        private bool m_bInit = false;      
        public bool bInitialized { get { return m_bInit; } }

        public bool InitAlgo()
        {
            if (!m_bInit)
            {
                try
                {
                    // 初始化算法库
                    if (!InitAlgoLib())
                        return false;
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("VmsAlgorithm.InitAlgo: " + ex.Message);
                    throw new Exception("VmsAlgorithm.InitAlgo: ", ex);
                }

                m_bInit = true;
            }

            return true;
        }

        public void ExitAlgo()
        {
            try
            {
                // 退出算法
                VmsAlgoAdapter.ExitAlgo();
                m_bInit = false;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("VmsAlgorithm.ExitAlgo: " + ex.Message);
                throw new Exception("VmsAlgorithm.ExitAlgo: ", ex);
            }

        }

        /// <summary>
        /// 加载堆场磁钉列表
        /// </summary>
        /// <returns></returns>
        private Transponder[] LoadTransponder()
        {
            Hashtable htTransponder = null;
            Transponder[] aryTp = null; 
            try
            {
                htTransponder = DataAccess.LoadTransponder();
                aryTp = new Transponder[htTransponder.Count];
                htTransponder.Values.CopyTo(aryTp, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine("VmsAlgorithm.LoadTransponder: " + ex.Message + ex.StackTrace);
                throw new Exception("VmsAlgorithm.LoadTransponder: ",ex);
            }

            return aryTp;
        }

        /// <summary>
        /// 加载磁钉线列表
        /// </summary>
        /// <returns></returns>
        private ushort[,] LoadLine()
        {
            ushort[,] aryLine = null;
            try
            {
                aryLine = DataAccess.LoadLine();
                if (aryLine == null || aryLine.Length <= 0) 
                    return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("VmsAlgorithm.LoadLine: " + ex.Message + ex.StackTrace);
                throw new Exception("VmsAlgorithm.LoadLine: ", ex);
            }

            return aryLine;
        }

        private bool InitAlgoLib()
        {
            try
            {
                Transponder[] aryTp = LoadTransponder();
                if (aryTp == null)
                    return false;

                if (!InitTransponderList(aryTp))
                    return false;

                ushort[,] aryLine = LoadLine();
                if (aryLine == null)
                    return false;

                if (!InitLineList(aryLine))
                    return false;

                // 初始化线与磁钉流向信息
                ushort[,] aryLineInfo = DataAccess.LoadLineInfo();
                if (aryLineInfo == null || aryLineInfo.GetLength(0) <= 0) 
                    return false;

                if (!InitSparse(aryLineInfo))
                    return false;

                Hashtable htLane = LoadLane();
                if (htLane != null && htLane.Count > 0)
                {
                    LaneInfo[] aryLane = new LaneInfo[htLane.Count];
                    htLane.Values.CopyTo(aryLane, 0);

                    if (!InitLane(aryLane))
                        return false;
                }
                else
                    return false;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("VmsAlgorithm.InitAlgoLib: " + ex.Message + ex.StackTrace);
                throw new Exception("VmsAlgorithm.InitAlgoLib: ", ex);
            }

            return true;
        }

        /// <summary>
        /// 初始化堆场磁钉列表
        /// </summary>
        /// <param name="aryTp">磁钉数组</param>
        private bool InitTransponderList(Transponder[] aryTp)
        {
            try
            {
                TransponderNode[] aryNode = new TransponderNode[aryTp.Length];
                NodeProperty[] aryNoStopProperty = new NodeProperty[aryTp.Length];

                for (int i = 0; i < aryTp.Length; i++)
                {
                    aryNode[i] = new TransponderNode();
                    aryNode[i].nID = (ushort)aryTp[i].ID;
                    aryNode[i].nRow = (ushort)aryTp[i].HorizontalLineID;
                    aryNode[i].nCol = (ushort)aryTp[i].VerticalLineID;
                    aryNode[i].nLogicPosX = (float)aryTp[i].LogicPosX;
                    aryNode[i].nLogicPosY = (float)aryTp[i].LogicPosY;
                    aryNode[i].nPhysicalPosX = (float)aryTp[i].PhysicalPosX;
                    aryNode[i].nPhysicalPosY = (float)aryTp[i].PhysicalPosY;
                    aryNode[i].AreaType = (byte)aryTp[i].AreaType;
                    aryNode[i].AreaID = (byte)aryTp[i].AreaNo;
                    aryNode[i].AreaLaneID = byte.Parse(aryTp[i].LaneNo);
                    aryNode[i].Enabled = aryTp[i].Enabled;

                    aryNoStopProperty[i] = new NodeProperty();
                    aryNoStopProperty[i].NodeID = (ushort)aryTp[i].ID;
                    aryNoStopProperty[i].PropertyValue = (byte)aryTp[i].NoStop;
                }

                if (VmsAlgoAdapter.InitTransponderList(aryNode) < 0)
                    return false;

                if (!VmsAlgoAdapter.SetNodeProperty(VmsAlgoAdapter.NODE_CPAT_NOSTOP, aryNoStopProperty))
                    return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("VmsAlgorithm.InitTransponderList: " + ex.Message + ex.StackTrace);
                throw ex;
            }

            return true;
        }

        /// <summary>
        /// 初始化堆场磁钉线列表
        /// </summary>
        /// <param name="aryTp">线列表</param>
        private bool InitLineList(UInt16[,] aryTp)
        {
            bool bRet = false;

            try
            {
                UInt16 nRow = (UInt16)aryTp.GetLength(0);
                UInt16 nCol = (UInt16)(aryTp.GetLength(1) - 2);     // 去除表中前两列

                LineInfo[] aryLine = new LineInfo[nRow];
                for (int i = 0; i < nRow; i++)
                {
                    aryLine[i] = new LineInfo();
                    aryLine[i].nID = aryTp[i, 0];
                    aryLine[i].nDirection = (byte)aryTp[i, 1];
                    aryLine[i].aryTp = new ushort[nCol];
                    aryLine[i].nHead = 0;

                    int j = 0;
                    for (j = 0; j < nCol; j++)
                    {
                        if (aryTp[i, j + 2] <= 0) break;

                        aryLine[i].aryTp[j] = aryTp[i, j + 2];
                    }

                    aryLine[i].nTail = (ushort)(j - 1);
                }

                bRet = VmsAlgoAdapter.InitLineList(aryLine, nRow, nCol);
            }
            catch (Exception ex)
            {
                Console.WriteLine("VmsAlgorithm.InitLineList: " + ex.Message + ex.StackTrace);
                throw ex;
            }

            return bRet;
        }

        private bool InitSparse(ushort[,] aryPath)
        {
            bool bRet = false; 
            try
            {
                UInt16 nRow = (UInt16)aryPath.GetLength(0);
                UInt16 nCol = (UInt16)(aryPath.GetLength(1) - 2);

               bRet = VmsAlgoAdapter.InitLineInfo(aryPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("VmsAlgorithm.InitSparse: " + ex.Message + ex.StackTrace);
                throw ex;
            }

            return bRet;
        }

        /// <summary>
        /// 初始化车道
        /// </summary>
        /// <param name="aryLane"></param>
        private bool InitLane(LaneInfo[] aryLane)
        {
            try
            {
                // 这里仅针对厦门项目
                ushort[] arySTSNo = new ushort[3];
                float[] arySTSPos = new float[3];//
                arySTSNo[0] = 118; arySTSPos[0] = 156;// 158;
                arySTSNo[1] = 119; arySTSPos[1] = 238;// 236;
                arySTSNo[2] = 120; arySTSPos[2] = 320;// 316;

                if (!VmsAlgoAdapter.InitSTSLaneAlgo(arySTSNo, arySTSPos))
                    return false;

                ushort[] aryBlockNo = new ushort[8] { 2, 3, 4, 5, 6, 7, 8, 9 };//Project.GetBlockList(); //new ushort[8] { 2, 3, 4, 5, 6, 7, 8, 9 };
                if (!VmsAlgoAdapter.InitBlockLaneAlgo(aryBlockNo))
                    return false;

                if(!VmsAlgoAdapter.RecoverAllLanes(aryLane))
                    return false ;

                VmsAlgoAdapter.UpdateSTSPosition(arySTSNo[0], arySTSPos[0]);
                VmsAlgoAdapter.UpdateSTSPosition(arySTSNo[1], arySTSPos[1]);
                VmsAlgoAdapter.UpdateSTSPosition(arySTSNo[2], arySTSPos[2]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("VmsAlgorithm.InitLane: " + ex.Message + ex.StackTrace);
                throw ex;
            }

            return true;
        }

        #endregion InitializeAlgo

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region AlgoLibFunction

        public Hashtable LoadLane()
        {
            Hashtable htLane = new Hashtable();

            try
            {
                htLane = DataAccess.LoadLane();
            }
            catch (Exception ex)
            {
                Console.WriteLine("VmsAlgorithm.LoadLane: " + ex.Message + ex.StackTrace);
                //throw new Exception("VmsAlgorithm.LoadLane: ", ex);
                return null;
            }

            return htLane;
        }
        
        public bool RecoverAllLanes()
        {
            if (!m_bInit)
                return false;

            Hashtable htLane = LoadLane();

            if (htLane != null && htLane.Count > 0)
            {
                LaneInfo[] aryLane = new LaneInfo[htLane.Count];
                htLane.Values.CopyTo(aryLane, 0);

                VmsAlgoAdapter.RecoverAllLanes(aryLane);

                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取QC对应的PB List
        /// </summary>
        /// <param name="qcID"></param>
        /// <param name="isIn"></param>
        /// <returns></returns>
        public List<ushort> GetPBList(int qcNo, bool isIn)
        {
            if (!m_bInit)
                return null;

            if (qcNo <= 0)
                return null;

            int laneCount = 0;
            ushort[] laneArrays = null;

            STSBufLaneType direction = isIn ? STSBufLaneType.QCBUF_IN : STSBufLaneType.QCBUF_OUT;

            laneCount = VmsAlgoAdapter.GetAvaiableSTSBufferLanesWithPB((ushort)qcNo, (byte)direction, 0,
                                                                        MAX_AVAILABLE_PB_INOUT, ref laneArrays);
            if (laneCount > 0 && laneArrays != null)
            {
                List<ushort> PBList = new List<ushort>();
                PBList.AddRange(laneArrays);

                return PBList;
            }

            return null;
        }
        #endregion AlgoLibFunction

    }

     /// <summary>
    /// AGV行车时间估算
    /// </summary>
    public class AgvTimeEstimate
    {
        private static AgvTimeEstimate m_Instance;
        private bool m_bInit = false;

        private static System.Object m_LockObj = new System.Object();

        public bool bInitialize { get { return m_bInit; } }

        public static AgvTimeEstimate Instance
        {
            get
            {
                if (m_Instance == null)
                    m_Instance = new AgvTimeEstimate();
                return m_Instance;
            }
        }

        private AgvTimeEstimate()
        {
            InitOrigin();
        }
        #region TimeEstimate

        // 4个开行时间查询字典
        private  Dictionary<String, int> LaneToLaneDistanceTable; // 从fromLane到toLane的开行时间字典, < fromID_toID, distance >, 如 < [117,118], 15 >
        private  Dictionary<int, int> QCToPBDistanceTable; // 从QC到出PB的开行时间字典 < QCID, distance >
        private  Dictionary<int, int> PBToQCDistanceTable; // 从入PB到QC的开行时间字典 < QCID, distance >
        private  Dictionary<String, int> QCTPToQCTPDistanceTable; // 从一QC下到另一QC下的开行时间, < fromID_toID, distance >/*QCTP*/

        //private Hashtable TransponderHashTable;
        private const int infinity = 9999;

        // 所有车道列表，初始化时由外部传入
        private List<LaneInfo> InitLaneList = new List<LaneInfo>();
       
        private void InitOrigin()
        {
            if (LaneToLaneDistanceTable == null)
                LaneToLaneDistanceTable = new Dictionary<String, int>();
            if (QCToPBDistanceTable == null)
                QCToPBDistanceTable = new Dictionary<int, int>();
            if (PBToQCDistanceTable == null)
                PBToQCDistanceTable = new Dictionary<int, int>();
            if (QCTPToQCTPDistanceTable == null)
                QCTPToQCTPDistanceTable = new Dictionary<String, int>();
        }

         /// <summary>
         /// 构造开行时间列表
         /// </summary>
         /// <param name="distanceList"></param>
         private bool InitExpectTimeListFromDB(ref List<ExpectTimeRow> timeList)
         {
             Hashtable timeHashTable = DataAccess.LoadExpectTime();
             if (timeHashTable == null)
                 return false;

             if (timeList.Count > 0)
                 timeList.Clear();

             if (timeHashTable != null && timeHashTable.Count > 0)
             {
                 foreach (DictionaryEntry entry in timeHashTable)
                     timeList.Add((ExpectTimeRow)entry.Value);
             }

             return true;
         }

         public bool InitTimeEstimate()
         {
             m_bInit = false;

             if (!VmsAlgorithm.Instance.bInitialized)
                 return false;

             lock (m_LockObj)
             {
                 // 初始化车道列表
                 Hashtable htLaneInfo = VmsAlgorithm.Instance.LoadLane();
                 if (htLaneInfo == null)
                     return false;

                 InitLaneInfoList(htLaneInfo);

                 // 构造具有固定位置的Lane与Lane的距离时间表,从数据库读取
                 List<ExpectTimeRow> laneExpectTimeList = new List<ExpectTimeRow>(); // 开行时间列表，从数据库中读取
                 if (!InitExpectTimeListFromDB(ref laneExpectTimeList))
                     return false;

                 // 初始化具有固定位置的Lane与Lane之间的开行时间字典
                 InitExpectTimeLaneToLaneDictionary(laneExpectTimeList);

                 // 初始化QC相关的开行时间字典 (动大车后，需要调用该接口更新时间数据）
                 if (!InitExpectTimeAboutQCDictionary())
                     return false;

                 //TransponderHashTable = DataAccess.LoadTransponder();
             }

             return m_bInit = true;
         }

     
         private class StatisticInOutQCTime
         {
             public int count { get; set; }
             public int total { get; set; }
             public int qcid  { get; set; }
             public StatisticInOutQCTime(int id) { qcid = id; }
             public int GetAverage(){ return total / count;}
         }

         private bool InitExpectTimeAboutQCDictionary()
         {
             bool bRet = false;

             LaneTolaneTime[] laneToLaneTime = null; 
             ushort count = 0;
             bRet = VmsAlgoAdapter.GetLaneToLaneTimeAboutQC(ref laneToLaneTime, ref count);
             if (bRet)
             {
                 QCToPBDistanceTable.Clear();
                 PBToQCDistanceTable.Clear();
                 QCTPToQCTPDistanceTable.Clear();
                 
                 Dictionary<int,StatisticInOutQCTime> dicPbToQc = new Dictionary<int,StatisticInOutQCTime>();
                 Dictionary<int,StatisticInOutQCTime> dicQcToPb = new Dictionary<int,StatisticInOutQCTime>();

                 for (int i = 0; i < laneToLaneTime.GetLength(0); i++)
                 {
                     LaneToLaneType type = laneToLaneTime[i].type;
                     String key = laneToLaneTime[i].fromid.ToString() + "_" + laneToLaneTime[i].toid.ToString();
                     
                     if (type == LaneToLaneType.QCPB_2_QC)
                     {
                         if (laneToLaneTime[i].expectTime < infinity)
                         {
                             if (!dicPbToQc.ContainsKey(laneToLaneTime[i].toid))
                                 dicPbToQc.Add(laneToLaneTime[i].toid, new StatisticInOutQCTime(laneToLaneTime[i].toid));
                             dicPbToQc[laneToLaneTime[i].toid].total += laneToLaneTime[i].expectTime;
                             dicPbToQc[laneToLaneTime[i].toid].count++;
                         }
                     }
                     else if (type == LaneToLaneType.QC_2_QCPB)
                     {
                         if (laneToLaneTime[i].expectTime < infinity)
                         {
                             if (!dicQcToPb.ContainsKey(laneToLaneTime[i].fromid))
                                 dicQcToPb.Add(laneToLaneTime[i].fromid, new StatisticInOutQCTime(laneToLaneTime[i].fromid));
                             dicQcToPb[laneToLaneTime[i].fromid].total += laneToLaneTime[i].expectTime;
                             dicQcToPb[laneToLaneTime[i].fromid].count++;
                         }
                         //if (!QCToPBDistanceTable.ContainsKey(laneToLaneTime[i].fromid))
                         //     QCToPBDistanceTable.Add(laneToLaneTime[i].fromid, laneToLaneTime[i].expectTime);
                     }
                     else if (type == LaneToLaneType.QCTP_2_QCTP)
                     {
                         if (!QCTPToQCTPDistanceTable.ContainsKey(key))
                              QCTPToQCTPDistanceTable.Add(key, laneToLaneTime[i].expectTime);
                     }
                     else
                     {
                         //.......
                     }
                 }

                 foreach (StatisticInOutQCTime inOutPbToQcTime in dicPbToQc.Values)
                 {
                     if (!PBToQCDistanceTable.ContainsKey(inOutPbToQcTime.qcid))
                         PBToQCDistanceTable.Add(inOutPbToQcTime.qcid, inOutPbToQcTime.GetAverage());
                 }

                 foreach (StatisticInOutQCTime inOutQcToPbTime in dicQcToPb.Values)
                 {
                     if (!QCToPBDistanceTable.ContainsKey(inOutQcToPbTime.qcid))
                         QCToPBDistanceTable.Add(inOutQcToPbTime.qcid, inOutQcToPbTime.GetAverage());
                 }
             }

             return bRet;
         }


        /// <summary>
        /// 使用数据库表T_QCMS_EXPECTTIME中读取到的distanceTable初始化3个与QC相关的时间字典
        /// </summary>
        /// <param name="table"></param>
        private void InitExpectTimeLaneToLaneDictionary(List<ExpectTimeRow> tableList)
        {
            LaneToLaneDistanceTable.Clear();

            foreach (ExpectTimeRow row in tableList)
            {
                String key = row.fromID + "_" + row.toID;

                if (row.type == 1)
                {
                    //LaneToLaneDistanceTable
                    LaneToLaneDistanceTable.Add(key, row.expectTime);
                }
            }
        }

        /// <summary>
        /// 接口，初始化Lane List,传入Hashtable
        /// </summary>
        /// <param name="list"></param>
        private void InitLaneInfoList(Hashtable table)
        {
            if (InitLaneList != null)
                InitLaneList.Clear();

            foreach (DictionaryEntry entry in table)
                InitLaneList.Add((LaneInfo)entry.Value);
        }

        private LaneInfo GetLaneInfo(int laneID)
        {
            LaneInfo laneInfo = VmsAlgoAdapter.GetLane((ushort)laneID);

            if (laneInfo != null && laneInfo.ID != 0)
                return laneInfo;

            return null;
        }

        private LANE_TYPE? GetlaneType(int laneID)
        {
            LaneInfo lane = GetLaneInfo(laneID);
            if (lane != null)
                return (LANE_TYPE)lane.Type;

            return null;
        }
        
        /// <summary>
        /// 通过lineID和transponderID，获得对应的lane信息
        /// </summary>
        /// <param name="lineID"></param>
        /// <param name="transponderID"></param>
        /// <param name="laneID"></param>
        /// <param name="type"></param>
        /// <param name="deviceID"></param>
        private void GetLaneInfoByLineIdAndTpId(int lineID, int transponderID, out ushort laneID, out LANE_TYPE type, out int deviceID)
        {
            laneID = 0;
            type = 0;
            deviceID = 0;

            foreach (LaneInfo initlane in InitLaneList)
            {
                LaneInfo laneInfo = VmsAlgoAdapter.GetLane(initlane.ID);
                if ((laneInfo.LineID == lineID) &&
                    ((laneInfo.StartTransponderID <= transponderID && laneInfo.EndTransponderID >= transponderID) ||
                     (laneInfo.StartTransponderID >= transponderID && laneInfo.EndTransponderID <= transponderID)))
                {
                    laneID = laneInfo.ID;
                    type = (LANE_TYPE)laneInfo.Type;
                    deviceID = laneInfo.RelateEqpID;
                    break;
                }
            }
        }

        /// <summary>
        /// 获取Block对应的交换车道List,或QC对应的工作车道List
        /// </summary>
        /// <param name="deviceID">Block的ID或是QC的ID</param>
        /// <returns></returns>
        private List<ushort> GetBlockLaneList(int deviceID)
        {
            return InitLaneList.Where(u => u.RelateEqpID == deviceID).Select(u => u.ID).ToList();
        }

        /// <summary>
        /// 获取QC对应的PB List
        /// </summary>
        /// <param name="qcID"></param>
        /// <param name="isIn"></param>
        /// <returns></returns>
        private  List<ushort> GetPBList(int qcNo, bool isIn)
        {
            if (qcNo <= 0)
                return null;

            return VmsAlgorithm.Instance.GetPBList(qcNo, isIn);
        }

      
        /// <summary>
        /// 查询从一QCTP下到另一QCTP下的开行时间
        /// </summary>
        /// <param name="fromQC"></param>
        /// <param name="toQC"></param>
        /// <returns></returns>
        private  int GetExpectTimeFromQCTPToQCTP(int fromQCTP, int toQCTP) //动大车后需修改QCWorklane to QC
        {
            String key = fromQCTP + "_" + toQCTP;
            if (QCTPToQCTPDistanceTable.ContainsKey(key))
                return QCTPToQCTPDistanceTable[key];
            else
                return infinity;
        }

        /// <summary>
        /// 查询从QC入车道到QC下的开行时间
        /// </summary>
        /// <param name="qcID"></param>
        /// <returns></returns>
        private int GetExpectTimeFromPBToQC(int qcID)
        {
            if (PBToQCDistanceTable.ContainsKey(qcID))
            {
                return PBToQCDistanceTable[qcID];
            }
            else
                return infinity;
        }

        /// <summary>
        /// 查询从QC下到QC出车道的开行时间
        /// </summary>
        /// <param name="qcID"></param>
        /// <returns></returns>
        private  int GetExpectTimeFromQCToPB(int qcID)
        {
            if (QCToPBDistanceTable.ContainsKey(qcID))
                return QCToPBDistanceTable[qcID];
            else
                return infinity;
        }

        /// <summary>
        /// 查询从fromLane到toLane的开行时间
        /// </summary>
        /// <param name="fromLane"></param>
        /// <param name="toLane"></param>
        /// <returns></returns>
        private int GetExpectTimeFromLaneToLane(ushort fromLane, ushort toLane)
        {
            String key = fromLane + "_" + toLane;
            if (LaneToLaneDistanceTable.ContainsKey(key))
                return LaneToLaneDistanceTable[key];
            else
                return infinity;
        }

        /// <summary>
        /// 查询AGV从一般车道到某个堆场的开行时间
        /// </summary>
        /// <param name="fromLaneID"></param>
        /// <param name="blockID"></param>
        /// <returns></returns>
        private  int GetExpectTimeFromLaneToBlock(ushort fromLaneID, int blockID)
        {
            // 获取堆场的交换车道，计算最短时间
            // 暂时保留只选择第0个，为了与路径计算时一致
            List<ushort> toLaneList = GetBlockLaneList(blockID);
            
            int shortesTime = infinity;
            shortesTime = GetExpectTimeFromLaneToLane(fromLaneID, toLaneList[0]);
         
            return shortesTime;
        }

        /// <summary>
        /// 查询AGV从QC下工作车道到某个堆场的开行时间
        /// </summary>
        /// <param name="qcID"></param>
        /// <param name="blockID"></param>
        /// <returns></returns>
        private int GetExpectTimeFromQCWorkLaneToBlock(int qcID, int blockID)
        {
            int time1 = GetExpectTimeFromQCToPB(qcID); //出QC到PB需要的时间

            List<ushort> pbList = GetPBList(qcID, false); // 获取目标QC的出PB List,出为false
            if (pbList == null)
                return -1;

            int time2 = infinity; // 从PB到block需要的时间 
            int count = 0;
            int tTotal = 0;

            //求平均时间
            foreach (ushort pbLaneID in pbList)
            {
                int temp = GetExpectTimeFromLaneToBlock(pbLaneID, blockID);
                if (temp >= infinity)
                    break;
                tTotal += temp;
                count++;
            }

            if(count > 0)
                time2 = tTotal/count;

            return time1 + time2;
        }


        /// <summary>
        /// 查询AGV从某个QC下到一般车道的开行时间
        /// </summary>
        /// <param name="qcID"></param>
        /// <param name="blockID"></param>
        /// <returns></returns>
        private int GetExpectTimeFromQCToLane(int qcID, ushort toLane)
        {
            int time1 = GetExpectTimeFromQCToPB(qcID); //从PB入QC需要的时间

            List<ushort> pbList = GetPBList(qcID, true); // 获取目标QC的入PB List,入为true
            if (pbList == null)
                return -1;

            int time2 = infinity; // 从PB到block需要的时间
            int count = 0;
            int tTotal = 0;

            //求平均时间
            foreach (ushort pbLaneID in pbList)
            {
                int temp = GetExpectTimeFromLaneToLane(pbLaneID, toLane);
                if (temp >= infinity)
                    break;
                tTotal += temp;
                count++;
            }

            if (count > 0)
                time2 = tTotal / count;

            return time1 + time2;
        }

        /// <summary>
        /// 查询AGV从一般车道到某个QC下的开行时间
        /// </summary>
        /// <param name="qcID"></param>
        /// <param name="blockID"></param>
        /// <returns></returns>
        private int GetExpectTimeFromLaneToQC(ushort fromLane, int qcID)
        {
            int time1 = GetExpectTimeFromPBToQC(qcID); //从PB入QC需要的时间

            List<ushort> pbList = GetPBList(qcID, true); // 获取目标QC的入PB List,入为true
            if (pbList == null)
                return -1;

            int time2 = infinity; // 从PB到block需要的时间
            int count = 0;
            int tTotal = 0;

            //求平均时间
            foreach (ushort pbLaneID in pbList)
            {
                int temp = GetExpectTimeFromLaneToLane(fromLane, pbLaneID);
                if (temp >= infinity)
                    break;
                tTotal += temp;
                count++;
            }

            if (count > 0)
                time2 = tTotal / count;

            return time1 + time2;
        }

        /// <summary>
        /// 查询从AGV中心点到最近车道的时间
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="nHeading"></param>
        /// <param name="nLaneID"></param>
        /// <param name="tCostTime"></param>
        /// <param name="nAgvID"></param>
        /// <returns></returns>
        public bool GetNearestLaneAndCostTime(int x, int y, short nHeading, ref ushort nLaneID, ref int tCostTime, ushort nAgvID = 0)
        {
            ushort laneID = 0;
            float costTime = 0;

            lock (m_LockObj)
            {
                if (VmsAlgoAdapter.GetNearestLaneAndCostTime(x, y, nHeading, ref laneID, ref costTime, nAgvID))
                {
                    nLaneID = laneID;
                    tCostTime = (int)costTime;
                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// 查询从AGV前天线磁钉点到最近车道的时间
        /// </summary>
        /// <param name="nNodeID"></param>
        /// <param name="nHeading"></param>
        /// <param name="nLaneID"></param>
        /// <param name="tCostTime"></param>
        /// <param name="nAgvID"></param>
        /// <returns></returns>
        public bool GetNearestLaneAndCostTime(ushort nNodeID, short nHeading, ref ushort nLaneID, ref int tCostTime,ushort nAgvID = 0)
        {
            ushort laneID = 0;
            float  costTime = 0;

            lock (m_LockObj)
            {
                if (VmsAlgoAdapter.GetNearestLaneAndCostTime(nNodeID, nHeading, ref laneID, ref costTime, nAgvID))
                {
                    nLaneID = laneID;
                    tCostTime = (int)costTime;
                    return true;
                }  
            }

            return false;
         }

        /// <summary>
        /// 更新时间估算模块的QC位置，单位：米
        /// </summary>
        /// <param name="QCNo"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool UpdateQcPosition(ushort QCNo, float position)
        {
            lock (m_LockObj)
            {
                if (VmsAlgoAdapter.UpdateSTSPosition(QCNo, position))
                {
                    return InitExpectTimeAboutQCDictionary();
                }
            }

            return false;
        }

        public bool UpdateLaneStatus()
        {
            lock (m_LockObj)
            {
                if (!VmsAlgorithm.Instance.RecoverAllLanes())
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 根据Agv所在车道ID估算Agv行车时间
        /// </summary>
        /// <param name="fromLaneId">Agv所在车道ID</param>
        /// <param name="toLaneId">目标车道ID</param>
        /// <returns></returns>
        public int EstimateRunTime(ushort fromLaneId, ushort toLaneId)
        {
            int nRet = -1;

            LaneInfo   fromLaneInfo = GetLaneInfo(fromLaneId);
            LaneInfo   toLaneInfo   = GetLaneInfo(toLaneId);
            LANE_TYPE? fromLaneType = GetlaneType(fromLaneId);
            LANE_TYPE? toLaneType   = GetlaneType(toLaneId);


            lock (m_LockObj)
            {
                if (fromLaneType != null && toLaneType != null
                   && fromLaneInfo != null && toLaneInfo != null)
                {
                    if (fromLaneType == LANE_TYPE.LT_QC_WORKLANE && toLaneType == LANE_TYPE.LT_QC_WORKLANE)
                        return GetExpectTimeFromQCTPToQCTP(fromLaneId, toLaneId);
                    else if (fromLaneType == LANE_TYPE.LT_QC_WORKLANE && toLaneType != LANE_TYPE.LT_QC_WORKLANE)
                        return GetExpectTimeFromQCToLane(fromLaneInfo.RelateEqpID, toLaneId);
                    else if (fromLaneType != LANE_TYPE.LT_QC_WORKLANE && toLaneType == LANE_TYPE.LT_QC_WORKLANE)
                        return GetExpectTimeFromLaneToQC(fromLaneId, toLaneInfo.RelateEqpID);
                    else
                        return GetExpectTimeFromLaneToLane(fromLaneId, toLaneId);

                }
            }

            return nRet;
        }

        /// <summary>
        /// 根据Agv前天线磁钉号估算Agv行车时间
        /// </summary>
        /// <param name="fromNodeId">前天线磁钉号</param>
        /// <param name="fromHeading">车头朝向，角度</param>
        /// <param name="toLaneId">目标车道</param>
        /// <returns></returns>
        public int EstimateRunTime(ushort fromNodeId, short fromHeading, ushort toLaneId)
        {
            int nRet = -1;

            ushort fromLaneId = 0;
            int time1 = 0;

            lock (m_LockObj)
            {
                if (GetNearestLaneAndCostTime(fromNodeId, fromHeading, ref fromLaneId, ref time1))
                {
                    if (fromLaneId != 0)
                        return EstimateRunTime(fromLaneId, toLaneId);
                }
            }

            return nRet;
        }

        /// <summary>
        /// 根据Agv中心点坐标估算Agv行车时间
        /// </summary>
        /// <param name="x">Agv中心点坐标x</param>
        /// <param name="y">Agv中心点坐标y</param>
        /// <param name="fromHeading">车头朝向，角度</param>
        /// <param name="toLaneId">目标车道</param>
        /// <returns></returns>
        public int EstimateRunTime(int x, int y, short fromHeading, ushort toLaneId)
        {
            int nRet = -1;

            ushort fromLaneId = 0;
            int time1 = 0;

            lock (m_LockObj)
            {
                if (GetNearestLaneAndCostTime(x, y, fromHeading, ref fromLaneId, ref time1))
                {
                    if (fromLaneId != 0)
                        return EstimateRunTime(fromLaneId, toLaneId);
                }
            }

            return nRet;
        }

        #endregion TimeEstimate

    }
   
}
