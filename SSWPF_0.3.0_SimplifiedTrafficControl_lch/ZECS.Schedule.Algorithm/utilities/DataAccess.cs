// Copyright (C) 2015-2016  ZPMC 
// 文件名 : DataAccess.cs 
// 作者： zhangxuemin
// 日期： 2015/10/15 
// 描述： 操作数据库
// 版 本： 1.0

// 修改历史记录 
// 版本       修改时间        修改人     修改内容

using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.OracleClient;
using System.IO;
using ZECS.Schedule.DBDefine.YardMap;
using ZECS.Schedule.Define;
using ZECS.Common.DBAccess;

namespace ZECS.Schedule.Algorithm.Utilities
{
    public class DataAccess
    {
        ////////////////////////////////////////////////////////////////////////
        #region AlgoDBFunction

        private static ConnForOracle m_conn = null;
        private static string m_sConnString = "";

        private static Dictionary<ushort, List<Transponder>> m_dictLine = new Dictionary<ushort, List<Transponder>>();    // 线号所包含磁钉字典

        public static void ConnectDB(String strConn)
        {
            m_sConnString = strConn;
            try
            {
                m_conn = new ConnForOracle(m_sConnString);
                m_conn.OpenConn();
            }
            catch (Exception ex)
            {
                String strError = "Algorithm connect DB failed " + ex.ToString();
                Console.WriteLine(strError);
                throw ex;
            }
        }

        public static void DisConnectDB()
        {
            if (m_conn != null)
            {
                m_conn.CloseConn();
            }
        }

        /// <summary>
        /// 加载堆场磁钉列表
        /// </summary>
        /// <returns></returns>
        public static Hashtable LoadTransponder()
        {
            if (m_dictLine != null)
                m_dictLine.Clear();

            Transponder[] aryTp = null;
            Hashtable htTransponder = new Hashtable();

            try
            {
                DataTable data = new DataTable();
                String strSql = "SELECT * FROM AGV_TRANSPONDER";

                DataSet ds = m_conn.ReturnDataSet(strSql, "DataSet");
                if (ds == null || ds.Tables == null || ds.Tables.Count <= 0) return null;

                data = ds.Tables[0];

                if (data.Rows.Count <= 0) return null;

                aryTp = new Transponder[data.Rows.Count];
                for (int i = 0; i < data.Rows.Count; i++)
                {
                    DataRow row = data.Rows[i];
                    if (row == null) continue;

                    aryTp[i] = new Transponder();
                    aryTp[i].ID = uint.Parse(row["ID"].ToString());
                    aryTp[i].HorizontalLineID = uint.Parse(row["HORIZONTAL_LINE_ID"].ToString());
                    aryTp[i].VerticalLineID = uint.Parse(row["VERTICAL_LINE_ID"].ToString());
                    aryTp[i].LogicPosX = int.Parse(row["LOGIC_POSX"].ToString());
                    aryTp[i].LogicPosY = int.Parse(row["LOGIC_POSY"].ToString());
                    aryTp[i].PhysicalPosX = float.Parse(row["PHYSICAL_POSX"].ToString());
                    aryTp[i].PhysicalPosY = float.Parse(row["PHYSICAL_POSY"].ToString());
                    aryTp[i].AreaType = (AreaType)Enum.Parse(typeof(AreaType), row["AREA_TYPE"].ToString());
                    aryTp[i].AreaNo = int.Parse(row["AREA_NO"].ToString());
                    aryTp[i].LaneNo = row["AREA_LANE_ID"].ToString();
                    aryTp[i].Enabled = int.Parse(row["ENABLED"].ToString()) >= 1 ? true : false;
                    aryTp[i].NoStop = ushort.Parse(row["NoStop"].ToString());

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
                }
            }
            catch (Exception ex)
            {
                aryTp = null;
                htTransponder = null;
                throw ex;
            }

            return htTransponder;
        }

        /// <summary>
        /// 获取线上最大磁钉数
        /// </summary>
        /// <returns>线上最大磁钉数</returns>
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

        /// <summary>
        /// 加载磁钉线列表
        /// </summary>
        /// <returns></returns>
        public static ushort[,] LoadLine()
        {
            int nCount = GetMaxTransponderCount();  // 线包含的最大磁钉数量

            ushort[,] aryLine = null;

            try
            {
                DataTable data = new DataTable();
                String strSql = "SELECT * FROM AGV_LINE ORDER BY ID ASC";

                DataSet ds = m_conn.ReturnDataSet(strSql, "DataSet");
                if (ds == null || ds.Tables == null || ds.Tables.Count <= 0) return null;

                data = ds.Tables[0];

                if (data.Rows.Count <= 0) return null;

                int nRow = data.Rows.Count;
                int nCol = data.Columns.Count;
                aryLine = new ushort[nRow, nCol + nCount];
                for (int i = 0; i < nRow; i++)
                {
                    DataRow row = data.Rows[i];
                    if (row == null) continue;

                    for (int j = 0; j < nCol; j++)
                    {
                        ushort nValue = ushort.Parse(row[j].ToString());
                        if (nValue <= 0) break;

                        aryLine[i, j] = nValue;
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
            }
            catch (Exception ex)
            {
                aryLine = null;
                throw ex;
            }

            return aryLine;
        }

        /// <summary>
        /// 加载堆场车道状态信息列表
        /// </summary>
        /// <returns></returns>
        public static Hashtable LoadLane()
        {
            // if (String.IsNullOrEmpty(m_strConnString)) return null;

            LaneInfo[] aryLane = null;
            Hashtable htLane = new Hashtable();

            try
            {
                DataTable data = new DataTable();
                String strSql = "SELECT * FROM AGV_LANE_PLANNING";

                DataSet ds = m_conn.ReturnDataSet(strSql, "DataSet");
                if (ds == null || ds.Tables == null || ds.Tables.Count <= 0) return null;

                data = ds.Tables[0];

                if (data.Rows.Count <= 0) return null;

                aryLane = new LaneInfo[data.Rows.Count];
                for (int i = 0; i < data.Rows.Count; i++)
                {
                    DataRow row = data.Rows[i];
                    if (row == null) continue;

                    aryLane[i] = new LaneInfo();
                    aryLane[i].ID = ushort.Parse(row["ID"].ToString());
                    aryLane[i].LineID = ushort.Parse(row["LINE_ID"].ToString());
                    aryLane[i].StartTransponderID = ushort.Parse(row["TRANSPONDER_START"].ToString());
                    aryLane[i].EndTransponderID = ushort.Parse(row["TRANSPONDER_END"].ToString());
                    aryLane[i].Type = byte.Parse(row["TYPE"].ToString());
                    aryLane[i].OccupyAGVID = ushort.Parse(row["AGV_NO"].ToString());
                    aryLane[i].Status = (LaneStatus)Enum.Parse(typeof(LaneStatus), row["STATUS"].ToString());
                    aryLane[i].RelateEqpID = ushort.Parse(row["CHE_NO"].ToString());
                    aryLane[i].AreaLaneID = row["AREA_LANE_ID"].ToString();
                    aryLane[i].Attr = (LaneAttribute)Enum.Parse(typeof(LaneAttribute), row["ATTR"].ToString());

                    htLane.Add(aryLane[i].ID, aryLane[i]);
                }
            }
            catch (System.Exception ex)
            {
                aryLane = null;
                htLane = null;
                throw ex;
            }

            return htLane;
        }

        /// <summary>
        /// 加载通行时间
        /// </summary>
        /// <param name="isQCRelated">true：从T_QCMS_EXPECTTIME;false: 从T_VMS_EXPECTTIME</param>
        /// <returns></returns>
        public static Hashtable LoadExpectTime()
        {
            //if (String.IsNullOrEmpty(m_strConnString)) return null;

            ExpectTimeRow[] timeArray = null;
            Hashtable timeHashTable = new Hashtable();

            try
            {
                DataTable data = new DataTable();
                String strSql = "SELECT * FROM T_VMS_EXPECTTIME";

                DataSet ds = m_conn.ReturnDataSet(strSql, "DataSet");
                if (ds == null || ds.Tables == null || ds.Tables.Count <= 0) 
                    return null;

                data = ds.Tables[0];

                if (data.Rows.Count <= 0) return null;

                timeArray = new ExpectTimeRow[data.Rows.Count];
                for (int i = 0; i < data.Rows.Count; i++)
                {
                    DataRow row = data.Rows[i];
                    if (row == null) continue;

                    timeArray[i] = new ExpectTimeRow();
                    timeArray[i].ID = ushort.Parse(row["ID"].ToString());
                    timeArray[i].type = ushort.Parse(row["TYPE"].ToString());
                    timeArray[i].fromID = ushort.Parse(row["FROMID"].ToString());
                    timeArray[i].toID = ushort.Parse(row["TOID"].ToString());
                    timeArray[i].expectTime = int.Parse(row["EXPECTTIME"].ToString());

                    timeHashTable.Add(timeArray[i].ID, timeArray[i]);
                }
            }
            catch (System.Exception ex)
            {
                timeArray = null;
                timeHashTable = null;
                throw ex;
            }

            return timeHashTable;
        }

        public static ushort[,] LoadLineInfo()
        {
            ushort[,] aryLineInfo = null;
            List<object> lstLine = new List<object>();

            //string sPath = Path.Combine(Application.StartupPath, "LineInfo.csv");
            string sPath = Path.Combine(SchedulePath.GetActiveProjectDirectory(), "LineInfo.csv");
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
        #endregion AlgoDBFunction
    }
}
