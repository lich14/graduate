using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.DBDefine.Schedule;

namespace ZECS.Schedule.Define.DBDefine.Schedule
{
    //TOS数据
    [Serializable]
    public class DBData_TOS
    {
        public List<STS_Task> m_listSTS_Task = null;
        public List<ASC_Task> m_listASC_Task = null;
        public List<AGV_Task> m_listAGV_Task = null;

        public List<STS_ResJob> m_listSTS_ResJob = null;
        public List<ASC_ResJob> m_listASC_ResJob = null;
        public List<AGV_ResJob> m_listAGV_ResJob = null;

        public List<BERTH_STATUS> m_listBERTH_STATUS = null;
        public List<STS_WORK_QUEUE_STATUS> m_listSTS_WORK_QUEUE_STATUS = null;
        public List<WORK_INSTRUCTION_STATUS> m_listWORK_INSTRUCTION_STATUS = null;

    }
    //STSMS数据
    [Serializable]
    public class DBData_STSMS
    {
        public List<STS_Order> m_listSTS_Order = null;
        public List<STS_Command> m_listSTS_Command = null;
        public List<STS_STATUS> m_listSTS_Status = null;

        //已经下发的任务,正在执行的.
        public List<STS_Order> m_listSTS_Order_Doing = null;



    }
    //VMS数据
    [Serializable]
    public class DBData_VMS
    {
        public List<AGV_Order> m_listAGV_Order = null;
        public List<AGV_Command> m_listAGV_Command = null;

        public List<AGV_STATUS> m_listAGV_Status = null;

        //已经下发的任务,正在执行的.
        public List<AGV_Order> m_listAGV_Order_Doing = null;
    }
    //BMS数据
    [Serializable]
    public class DBData_BMS
    {
        public List<ASC_Order> m_listASC_Order = null;
        public List<ASC_Command> m_listASC_Command = null;

        public List<ASC_STATUS> m_listASC_Status = null;

        //已经下发的任务,正在执行的.
        public List<ASC_Order> m_listASC_Order_Doing = null;
    }

    [Serializable]
    public class DBData_Schedule
    {
        //TOS数据
        public DBData_TOS m_DBData_TOS = new DBData_TOS();

        //STSMS数据
        public DBData_STSMS m_DBData_STSMS = new DBData_STSMS();

        //VMS数据
        public DBData_VMS m_DBData_VMS = new DBData_VMS();

        //BMS数据
        public DBData_BMS m_DBData_BMS = new DBData_BMS();

        public DateTime m_dtUpdate = DateTime.Now;

        public DBData_Schedule()
        {

        }
    }

    /// <summary>
    /// 某MaskID对应的各QC装箱数量
    /// </summary>
    public class MaskQCLoadCount
    {
        /// <summary>
        /// MaskID,对应Factor
        /// </summary>
        public int MaskID = 0;
        /// <summary>
        /// QC装箱数量,QCID对应装箱数量
        /// </summary>
        public Dictionary<int, int> Dic_QC_LoadCount = new Dictionary<int, int>();

    }
    /// <summary>
    /// 某堆垛为各QC的Mask数量.
    /// 各MaskID对应的各QC装箱数量
    /// </summary>
    [Serializable]
    public class BlockMaskQCLoadCount
    {
        public String BlockNO;

        /// <summary>
        /// 各MaskID对应的各QC装箱数量
        /// MaskID对应MaskQCLoadCount
        /// </summary>
        public Dictionary<int, MaskQCLoadCount> Dic_MaskID_QCLoadCount = new Dictionary<int, MaskQCLoadCount>();

     }

    public class PartialOrderGraph
    {
        public List<WORK_INSTRUCTION_STATUS> m_WIList = null;
        public int[,] m_DecisionTable = null;
    }
    
    
    //偏序决策表  Ordered decision table
    [Serializable]
    public class OrderedDecisionTable : PartialOrderGraph
    {
        private bool m_bValid = false;

        public bool Valid { get { return m_bValid; } }

        public List<WORK_INSTRUCTION_STATUS> WIList
        {
            get { return m_WIList; }
            //set { m_WIList = value; }
        }

        /// <summary>
        /// int[i,j]的值只能为0和1或-1
        /// int[i,j] =  1, 表示任务i在任务j之后
        /// int[i,j] =  0, 表示任务i与任务j无依赖关系
        /// int[i,j] = -1， 表示任务i在任务j之前
        /// </summary>
        public int[,] DecisionTable
        {
            get { return m_DecisionTable; }
            // set { m_DecisionTable = value; }
        }


        public OrderedDecisionTable()
        {

        }

        public OrderedDecisionTable(List<WORK_INSTRUCTION_STATUS> WIList, int[,] matrix)
        {
            SetPartialOrderedTable(WIList, matrix);
        }


        public bool SetPartialOrderedTable(List<WORK_INSTRUCTION_STATUS> WIList, int[,] matrix)
        {
            if (WIList == null || matrix == null)
                return false;

            m_bValid = false;
         
            if (!(WIList.Count == matrix.GetLength(0) &&
                  WIList.Count == matrix.GetLength(1) && matrix.GetLength(0) == matrix.GetLength(1)))
                return m_bValid;

            m_WIList = Helper.Clone<List<WORK_INSTRUCTION_STATUS>>(WIList);
            if (m_DecisionTable == null)
                m_DecisionTable = new int[matrix.GetLength(0), matrix.GetLength(1)];
            Array.Copy(matrix, m_DecisionTable, matrix.Length);

            m_bValid = true;

            return m_bValid;
        }

        public bool TopoLogicSort(out List<WORK_INSTRUCTION_STATUS> WIList)
        {
            WIList = new List<WORK_INSTRUCTION_STATUS>();

            if (m_WIList == null)
                return false;

            int count = m_WIList.Count;

            if (!m_bValid)
                return false;

            //int[,] matrix = (int[,])m_DecisionTable.Clone();
            int[,] matrix = new int[count, count];
            Array.Copy(m_DecisionTable, matrix, m_DecisionTable.Length);

            List<WORK_INSTRUCTION_STATUS> newWIList = new List<WORK_INSTRUCTION_STATUS>();
            newWIList.Clear();

            while (count - newWIList.Count > 0)
            {
                int guard = newWIList.Count;

                Dictionary<int, List<int>> dicSource = new Dictionary<int, List<int>>();
                for (int i = 0; i < count; i++)
                {
                    bool bDependence = false;

                    List<int> columnList = new List<int>();

                    for (int j = 0; j < count; j++)
                    {
                        if (matrix[i, j] == -1)
                            columnList.Add(j);

                        if (matrix[i, j] == 1 && !bDependence)
                        {
                            bDependence = true;
                            break;
                        }
                    }

                    if (!bDependence)
                    {
                        if (newWIList.Exists(tempWI => tempWI == m_WIList[i]))
                        {

                        }
                        else
                        {
                            if (columnList.Count > 0) 
                                dicSource.Add(i, columnList);
                              
                            newWIList.Add(m_WIList[i]);
                        }
                    }
                }

                if (dicSource.Count > 0)
                {
                    foreach (int row in dicSource.Keys)
                    {
                        List<int> columnList = dicSource[row];
                        foreach (int column in columnList)
                        {
                            matrix[row, column] = 0;
                            matrix[column, row] = 0;
                        }
                    }
                }
                //检查偏序矩阵表达的图是否有循环路径
                //每次扫描矩阵必然能找出一个或多个入度或出度为0的节点，否则矩阵存在循环路径
                if (guard >= newWIList.Count)
                {
                    Logger.ECSSchedule.Error("TopoLogicSort failed!");
                    return false;
                }
            }


            WIList = Helper.Clone<List<WORK_INSTRUCTION_STATUS>>(newWIList);

            return true;
        }

        private int[,] DeleteMatrixRowAndColunm(int row, int colunm, int[,] matrix)
        {
            if (row > matrix.GetLength(0) || colunm > matrix.GetLength(1)
                || matrix.GetLength(0) < 1 || matrix.GetLength(1) < 1)
                return null;

            List<int> templst = new List<int>();

            int[,] newMatrix = new int[matrix.GetLength(0) - 1, matrix.GetLength(1) - 1];
            
            for (int m = 0; m < matrix.GetLength(0); m++)
            {
                for (int n = 0; n < matrix.GetLength(1); n++)
                {
                    if (m != row && n != colunm)
                    {
                        templst.Add(matrix[m, n]);
                    }
                }
            }

            int index = 0;
            for ( int x = 0; x < newMatrix.GetLength(0); x++ )
            {
                for ( int y = 0; y < newMatrix.GetLength(1); y++ )
                {
                    newMatrix[x, y] = templst[index];
                    index++;
                }
            }

            return newMatrix;
        }

        public bool GetSourceNodes(out List<WORK_INSTRUCTION_STATUS> sourceNodeList)
        {
            sourceNodeList = new List<WORK_INSTRUCTION_STATUS>();
            if (!m_bValid)
                return false;

            int count = m_WIList.Count;

            for (int i = 0; i < count; i++)
            {
                bool bDependence = false;
                for (int j = 0; j < count; j++)
                {
                    if (m_DecisionTable[i, j] == 1)
                    {
                        bDependence = true;
                        break;
                    }
                }

                if (!bDependence)
                    sourceNodeList.Add(m_WIList[i]);
            }

            return true;
        }

        public bool PopupSourceNodes(out List<WORK_INSTRUCTION_STATUS> sourceNodeList)
        {
             sourceNodeList = new List<WORK_INSTRUCTION_STATUS>();
             
             if (!m_bValid)
                return false;

             if (!GetSourceNodes(out sourceNodeList))
                 return false;

            for (int m = 0; m < m_WIList.Count; m++)
            {
                for (int n = 0; n < sourceNodeList.Count; n++)
                {
                    if (m_WIList[m] == sourceNodeList[n])
                    {
                        int[,] matrix = DeleteMatrixRowAndColunm(m, m, m_DecisionTable);
                        if (matrix != null)
                        {
                            m_DecisionTable = matrix;
                            m_WIList.Remove(m_WIList[m]);
                        }
                        else
                            return false;
                    }
                }
            }

            return true;
        }
    }
}
