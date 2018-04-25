using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DBDefine.YardMap;
using log4net;
using SSWPF.Define;
using ZECS.Schedule.Define;

namespace ZECS.Schedule.VmsAlgoApplication
{
    public class VmsAlgoAdapter
    {
        public static uint NODE_CPAT_INVALID = 0;
        public static uint NODE_CPAT_NOSTOP = 1;
        private static ILog EventLogger = Logger.VmsAlgoAdapter;
        private static List<node_t> lNodes;
        public static SimDataStore oSimDataStore;

        public VmsAlgoAdapter()
        {
        }

        public static bool ConvertFrontToCenterPosition(ushort nTransponderID, byte nDirection, ref float fXPos, ref float fYPos)
        {
            return true;
        }

        // 退出算法库
        public static void ExitAlgo()
        {
            // 源代码为释放DLL，仿真中不用管
        }

        // 初始化磁钉
        public static int InitTransponderList(TransponderNode[] aryTp)
        {
			int nRet = -1;
            lNodes = new List<node_t>();

			for(int i=0; i<aryTp.Count(); i++)
			{
                node_t oNT = new node_t();
                oNT.id = aryTp[i].nID;
                oNT.linex = aryTp[i].nRow;
				oNT.liney = aryTp[i].nCol;
				oNT.logx = aryTp[i].nLogicPosX;
				oNT.logy = aryTp[i].nLogicPosY;
				oNT.phyx = aryTp[i].nPhysicalPosX;
				oNT.phyy = aryTp[i].nPhysicalPosY;
				oNT.area_type = Convert.ToSByte(aryTp[i].AreaType);
				oNT.area_id = Convert.ToSByte(aryTp[i].AreaID);
				oNT.area_lane_id = Convert.ToSByte(aryTp[i].AreaLaneID);
				oNT.availability = aryTp[i].Enabled;
                lNodes.Add(oNT);
			}

            if (lNodes.Count == 0)
            {
                EventLogger.Error("InitTransponderList : No node Initialized.");
            }
            else
            {
                nRet = 1;
            }

			return nRet;
		}

        // 初始化岸桥车道
        public static bool InitSTSLaneAlgo(ushort[] arySTS, float[] arySTSPos)
        {
            return true;
        }


        public static ushort GetAreaLaneID(ushort nLaneID)
        {
            return 0;
        }

        // 拿到进出 QC 的 LaneID 数组，返回数量
        public static ushort GetAvaiableSTSBufferLanesWithPB(ushort nSTSNo, byte nType, ushort nPBLaneID, ushort max_num, ref ushort[] aryPBs)
        {
            double BaseX = 0;
            ushort PBNum = 0;
            ushort PBReturnNum = 0;
            List<LaneIDWithX> lLIWXs = new List<LaneIDWithX>();
            LaneIDWithX oLIWX;

            foreach (Lane ol in oSimDataStore.dLanes.Values)
            {
                if (ol.CheNo == nSTSNo)
                {
                    if ((nType == 1 && ol.eAttr == LaneAttribute.STS_PB_ONLY_IN) || (nType == 2 && ol.eAttr == LaneAttribute.STS_PB_IN_OUT))
                    {
                        PBNum++;
                        oLIWX = new LaneIDWithX();
                        oLIWX.ID = ol.ID;
                        oLIWX.PosX = ol.pMid.X;
                        lLIWXs.Add(oLIWX);
                    }
                }
            }

            // 排序以备筛选。若不指定首车道，则以岸桥目标位置为起点排序。否则，以首车道X位置排序。
            if (nPBLaneID > 0)  BaseX = oSimDataStore.dLanes[nPBLaneID].pMid.X;
            else BaseX = oSimDataStore.dQCs[nSTSNo].AimPos;
            lLIWXs.Sort((u, v) => (Math.Abs(u.PosX - BaseX)).CompareTo((Math.Abs(v.PosX - BaseX))));

            // 只取前面若干位
            PBReturnNum = Math.Min(PBNum, max_num);
            aryPBs = new ushort[PBReturnNum];
            for (int i = 0; i < PBReturnNum; i++)
            {
                aryPBs[i] = Convert.ToUInt16(lLIWXs[i].ID);
            }

            return PBReturnNum;
        }

        // 用于排序
        private class LaneIDWithX
        {
            public uint ID;
            public double PosX;
        }

        // 返回与 nLaneID 对应的 LaneInfo
        public static LaneInfo GetLane(ushort nLaneID)
        {
            LaneInfo oLI = new LaneInfo();
            Lane ol = oSimDataStore.dLanes[nLaneID];

            oLI.ID = Convert.ToUInt16(ol.ID);
            oLI.LineID = Convert.ToUInt16(ol.LineID);
            oLI.StartTransponderID = Convert.ToUInt16(ol.TPIDStart);
            oLI.EndTransponderID = Convert.ToUInt16(ol.TPIDEnd);
            oLI.Type = Convert.ToByte(ol.eType);
            if (ol.AGVNo > 0 && ol.eStatus == LaneStatus.OCCUPIED)
                oLI.OccupyAGVID = Convert.ToUInt16(ol.AGVNo);
            oLI.Status = ol.eStatus;
            oLI.RelateEqpID = Convert.ToByte(ol.CheNo);
            oLI.AreaLaneID = ol.AreaLaneID;
            oLI.Attr = ol.eAttr;
            oLI.BufferType = (byte)ol.eType; 

            return oLI;
        }


        public static ushort GetLaneStopPointID(ushort nLaneID, byte nDirection, byte nTruckPos)
        {
            return 0;
        }

        // 拿到与 QC 有关的开行时间
        public static bool GetLaneToLaneTimeAboutQC(ref LaneTolaneTime[] aryLaneTolaneTime, ref ushort nCount)
        {
            if (oSimDataStore == null) return false;

            nCount = Convert.ToUInt16(oSimDataStore.lQCExpectTimes.Count);

            LaneTolaneTime[] tempArray = new LaneTolaneTime[nCount];

            int i = 0;
            foreach (SimExpectTime oSET in oSimDataStore.lQCExpectTimes)
            {
                tempArray[i] = new LaneTolaneTime();
                tempArray[i].type = oSET.l2LType;
                tempArray[i].fromid = Convert.ToInt32(oSET.fromID);
                tempArray[i].toid = Convert.ToInt32(oSET.toID);
                tempArray[i].expectTime = oSET.expectTime;
                i++;
            }

            aryLaneTolaneTime = tempArray;

            return true;
        }

        public static bool GetNearestLaneAndCostTime(ushort nNodeID, short nHeading, ref ushort nLaneID, ref float tCostTime, ushort nAgvID)
        {
            return false;
        }

        public static bool GetNearestLaneAndCostTime(int x, int y, short nHeading, ref ushort nLaneID, ref float tCostTime, ushort nAgvID)
        {
            return false;
        }

        public static byte GetTransponderArea(short transponderID, byte direction)
        {
            return 0;
        }

        public static bool InitBlockLaneAlgo(ushort[] aryBlock)
        {
            return true;
        }

        public static bool InitLineInfo(ushort[,] aryLineInfo)
        {
            // AGVLine已经读入，没必要初始化LineInfo
            return true;
        }

        public static bool InitLineList(LineInfo[] aryLine, uint nRow, uint nCol)
        {
            // 实际上 Line List 在仿真开始前读入，因此这里只需要返回true
            return true;
        }

        public static bool RecoverAllLanes(LaneInfo[] aryLane)
        {
            return true;
        }


        public static bool SetNodeProperty(uint nNodePropertyType, NodeProperty[] aryProperty)
        {
            return true;
        }

        public static bool UpdateSTSPosition(ushort nSTSNo, float nPos)
        {
            return true;
        }

        #region  自定义函数，取代C++算法库

        // 拿到与 QC 有关的 PB 间静态运行时间列表
        private static bool GetTimeListQCLaneToLane(ref List<LaneTolaneTime> lLane2LaneTime)
        {
            bool bRet = true;

            if (oSimDataStore == null) return false;

            foreach (SimExpectTime oSET in oSimDataStore.lQCExpectTimes)
            {
                LaneTolaneTime oLTLT = new LaneTolaneTime();
                oLTLT.type = oSET.l2LType;
                oLTLT.fromid = Convert.ToInt32(oSET.fromID);
                oLTLT.toid = Convert.ToInt32(oSET.toID);
                oLTLT.expectTime = oSET.expectTime;
                lLane2LaneTime.Add(oLTLT);
            }

            return bRet;
        }


        #endregion
    }
}
