using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Reflection;
using ZECS.Schedule.DBDefine.YardMap;
using ZECS.Schedule.Define;
using System.IO;
using SSWPF.Define;

namespace ZECS.Schedule.DB
{
    public static class DataAccess
    {
        public static SimDataStore oSimDataStore;
        private static String m_strConnString = "User ID=ecs_tos;Password=ecs123;Data Source=10.28.254.102/ecs";
        private static Dictionary<ushort, List<Transponder>> m_dictLine = new Dictionary<ushort, List<Transponder>>();    // 线号所包含磁钉字典

        public static string ConnString
        {
            get { return m_strConnString; }
            set { m_strConnString = value; }
        }

        public static void ConnectDB(string ConnStr)
        {
        }

        public static void DisConnectDB()
        {
        }

        // 加载堆场磁钉列表
        public static Hashtable LoadTransponder()
        {
            if (m_dictLine != null)
                m_dictLine.Clear();

            if (oSimDataStore == null) return null;

            Transponder[] aryTp = new Transponder[oSimDataStore.dTransponders.Values.Count];
            Hashtable htTransponder = new Hashtable();

            int i = 0;
            foreach (SimTransponder ost in oSimDataStore.dTransponders.Values)
            {
                aryTp[i] = new Transponder();
                aryTp[i].ID = ost.ID;
                aryTp[i].HorizontalLineID = ost.HorizontalLineID;
                aryTp[i].VerticalLineID = ost.VerticalLineID;
                aryTp[i].LogicPosX = ost.LogicPosX;
                aryTp[i].LogicPosY = ost.LogicPosY;
                aryTp[i].PhysicalPosX = ost.PhysicalPosX;
                aryTp[i].PhysicalPosY = ost.PhysicalPosY;
                aryTp[i].AreaType = ost.AreaType;
                aryTp[i].AreaNo = ost.AreaNo;
                aryTp[i].LaneNo = ost.LaneNo;
                aryTp[i].Enabled = ost.Enabled;
                aryTp[i].NoStop = ost.NoStop;

                if (aryTp[i].HorizontalLineID > 0)
                {
                    if (!m_dictLine.ContainsKey((ushort)aryTp[i].HorizontalLineID))
                        m_dictLine.Add((ushort)aryTp[i].HorizontalLineID, new List<Transponder>());

                    m_dictLine[(ushort)aryTp[i].HorizontalLineID].Add(aryTp[i]);
                }

                if (aryTp[i].VerticalLineID > 0)
                {
                    if (!m_dictLine.ContainsKey((ushort)aryTp[i].VerticalLineID))
                        m_dictLine.Add((ushort)aryTp[i].VerticalLineID, new List<Transponder>());

                    m_dictLine[(ushort)aryTp[i].VerticalLineID].Add(aryTp[i]);
                }

                htTransponder.Add(aryTp[i].ID, aryTp[i]);

                i++;
            }

            return htTransponder;
        }

        // 获得线上最大磁钉数
        private static int GetMaxTransponderCount()
        {
            if (m_dictLine == null || m_dictLine.Count <= 0) return 0;

            int nCount = 0;
            foreach (List<Transponder> val in m_dictLine.Values)
            {
                if (val.Count >= nCount)
                    nCount = val.Count;
            }

            return nCount;
        }

        // 加载磁钉线列表
        public static ushort[,] LoadLine()
        {
            if (oSimDataStore == null) return null;

            int nCount = GetMaxTransponderCount();  // 线包含的最大磁钉数量

            ushort[,] aryLine = null;

            int nRow = oSimDataStore.dAGVLines.Values.Count;

            // 利用反射，得到公共字段的数量。注意去除 bool 型的 ifDefinitionCompleted 字段
            FieldInfo[] afi = typeof(AGVLine).GetFields();
            int nCol = afi.Length - 1;
            aryLine = new ushort[nRow, nCol + nCount];

            for (uint i = 0; i < oSimDataStore.dAGVLines.Values.Count; i++)
            {
                for (int j = 0; j < nCol; j++)
                {
                    switch (j)
                    {
                        case 0:
                            aryLine[i, j] = (ushort)oSimDataStore.dAGVLines[i + 1].ID;
                            break;
                        case 1:
                            aryLine[i, j] = (ushort)oSimDataStore.dAGVLines[i + 1].eFlowDir;
                            break;
                    }
                }

                CoordinateDirection direction = (CoordinateDirection)Enum.Parse(typeof(CoordinateDirection), aryLine[i, 1].ToString());
                List<Transponder> lstSortTp = CvtCoordinate.SortTransponder(direction, m_dictLine[aryLine[i, 0]]);

                if (lstSortTp != null)
                {
                    for (int nIdx = 0; nIdx < lstSortTp.Count; nIdx++)
                    {
                        aryLine[i, nCol + nIdx] = (ushort)lstSortTp[nIdx].ID;
                    }
                }
            }

            return aryLine;
        }

        // 加载堆场车道状态信息列表
        public static Hashtable LoadLane()
        {
            if (oSimDataStore == null) return null;

            LaneInfo[] aryLane = null;
            Hashtable htLane = new Hashtable();

            aryLane = new LaneInfo[oSimDataStore.dLanes.Values.Count];

            int i = 0;
            foreach (Lane ol in oSimDataStore.dLanes.Values)
            {
                aryLane[i] = new LaneInfo();
                aryLane[i].ID = Convert.ToUInt16(ol.ID);
                aryLane[i].LineID = Convert.ToUInt16(ol.LineID);
                aryLane[i].StartTransponderID = Convert.ToUInt16(ol.TPIDStart);
                aryLane[i].EndTransponderID = Convert.ToUInt16(ol.TPIDEnd);
                aryLane[i].Type = Convert.ToByte(ol.eType);
                if (ol.AGVNo > 0 && ol.eStatus == LaneStatus.OCCUPIED)
                    aryLane[i].OccupyAGVID = Convert.ToUInt16(ol.AGVNo);
                aryLane[i].Status = ol.eStatus;
                aryLane[i].RelateEqpID = Convert.ToByte(ol.CheNo);
                aryLane[i].AreaLaneID = ol.AreaLaneID;
                aryLane[i].Attr = ol.eAttr;
                aryLane[i].BufferType = (byte)ol.eType;

                htLane.Add(aryLane[i].ID, aryLane[i]);
                i++;
            }
            return htLane;
        }

        // 加载通行时间。仅来自 T_VMS_EXPECTTIME
        public static Hashtable LoadExpectTime()
        {
            if (oSimDataStore == null) return null;

            ExpectTimeRow[] timeArray = null;
            Hashtable timeHashTable = new Hashtable();
            timeArray = new ExpectTimeRow[oSimDataStore.lVMSExpectTimes.Count];

            for (int i = 0; i < oSimDataStore.lVMSExpectTimes.Count; i++)
            {
                timeArray[i] = new ExpectTimeRow();
                timeArray[i].ID = Convert.ToUInt16(oSimDataStore.lVMSExpectTimes[i].ID);
                timeArray[i].type = Convert.ToUInt16(oSimDataStore.lVMSExpectTimes[i].l2LType);
                timeArray[i].fromID = Convert.ToUInt16(oSimDataStore.lVMSExpectTimes[i].fromID);
                timeArray[i].toID = Convert.ToUInt16(oSimDataStore.lVMSExpectTimes[i].toID);
                timeArray[i].expectTime = oSimDataStore.lVMSExpectTimes[i].expectTime;
                timeHashTable.Add(timeArray[i].ID, timeArray[i]);
            }

            return timeHashTable;
        }

        // 加载LineInfo。来自LineInfo.csv
        public static ushort[,] LoadLineInfo()
        {
            ushort[,] aryLineInfo = null;
            List<object> lstLine = new List<object>();

            string sPath = SchedulePath.GetActiveProjectDirectory() + "\\lineInfo.csv";
            if (!System.IO.File.Exists(sPath)) return null;

            String sLine = "";
            String[] aryCell = null;
            int nCol = 0;
            using (TextReader tr = new StreamReader(sPath))
            {
                while (true)
                {
                    sLine = tr.ReadLine();
                    if (String.IsNullOrEmpty(sLine)) break;

                    aryCell = sLine.Split(',');
                    if (aryCell == null || aryCell.Length <= 0) continue;

                    nCol = aryCell.Length;

                    lstLine.Add(aryCell);
                }
            }

            aryLineInfo = new ushort[lstLine.Count, nCol];

            for (int i = 0; i < lstLine.Count; i++)
            {
                String[] aryCellTemp = lstLine[i] as String[];
                if (aryCellTemp == null) continue;

                for (int j = 0; j < aryCellTemp.Length; j++)
                {
                    ushort nValue = ushort.Parse(aryCellTemp[j]);
                    if (nValue <= 0) break;

                    aryLineInfo[i, j] = nValue;
                }
            }

            return aryLineInfo;
        }

    }
}
