using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// 偏序表类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0 
    /// </summary>
    public class PartialOrderTable
    {
        #region [ 属性 ]
        /// <summary>
        /// 偏序表元素个数
        /// </summary>
        public int TableSize;

        
        //偏序元素类
        public class PartialOrderVertex
        {
            #region [ 属性 ]
            /// <summary>
            /// 该元素在表中的前置顶点集
            /// </summary>
            public List<PartialOrderVertex> PreVertex = new List<PartialOrderVertex>();

            /// <summary>
            /// 该元素在表中的后置顶点集
            /// </summary>
            public List<PartialOrderVertex> ProVertex = new List<PartialOrderVertex>();

            /// <summary>
            /// 顶点索引
            /// </summary>
            public int VertexIndex;

            /// <summary>
            /// 顶点着色
            /// </summary>
            public int VertexColor;

            /// <summary>
            /// 入度数
            /// </summary>
            public int InDegree;

            /// <summary>
            /// 出度数
            /// </summary>
            public int OutDegree;

            #endregion

            #region [ 方法 ]

            /// <summary>
            /// 克隆
            /// </summary>
            /// <returns></returns>
            public PartialOrderVertex Clone()
            {
                
                return null;

            }

            #endregion

        }

        /// <summary>
        /// 偏序表元素列表（按拓扑排序的逆序,即按照此序，所有有向边都是单向从后至前的）
        /// </summary>
        public List<PartialOrderVertex> PartialOrderVertexList;

        /// <summary>
        /// 拓扑逆序或者顺序
        /// </summary>
        public bool ReverseOrNot;
        
        /// <summary>
        /// TableSize*TableSize二维表，记录元素间的序关系
        /// </summary>
        public List<List<int>> OrderMatrix;

        #endregion

        #region [ 方法 ]

        /// <summary>
        /// 构造函数
        /// </summary>
        public PartialOrderTable()
        {
            this.PartialOrderVertexList = new List<PartialOrderVertex>();

        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="tableSize">偏序表顶点个数</param>
        public PartialOrderTable(int tableSize)
        {
            this.PartialOrderVertexList = new List<PartialOrderVertex>();

            this.TableSize = tableSize;

            this.OrderMatrix = new List<List<int>>();

            this.OrderMatrix = CommonAlgorithm.IniZeroMatrix(tableSize);

        }
        
        /// <summary>
        /// 给定偏序表中元素的颜色以及给定的颜色，返回该颜色最小可能出现的序值
        /// </summary>
        /// <param name="maskForVertexList"></param>
        /// <param name="givenMask"></param>
        /// <returns></returns>
        public int FirstOrderOfGivenMask(List<int> maskForVertexList, int givenMask)
        {
            return 0;
        }

        /// <summary>
        /// 给定偏序表中元素的颜色以及给定的颜色，返回该颜色最大可能出现的序值
        /// </summary>
        /// <param name="maskForVertexList"></param>
        /// <param name="givenMask"></param>
        /// <returns></returns>
        public int LastOrderOfGivenMask(List<int> maskForVertexList, int givenMask)
        {
            return 0;
        }


        /// <summary>
        /// 利用深度搜索为偏序表拓扑排序
        /// </summary>
        public void TopologicalSort()
        {


        }

        /// <summary>
        /// 列出偏序表中所有零入度点
        /// </summary>
        /// <returns></returns>
        public List<PartialOrderVertex> GetAllSourceVertices()
        {
            List<PartialOrderVertex> result = new List<PartialOrderVertex>();

            foreach(PartialOrderVertex a in this.PartialOrderVertexList)
            {
                if (a.PreVertex.Count == 0)
                    result.Add(a);
            }

            return result;
        }

        /// <summary>
        /// 列出偏序表中所有零出度点
        /// </summary>
        /// <returns></returns>
        public List<PartialOrderVertex> GetAllSinkVertices()
        {
            List<PartialOrderVertex> result = new List<PartialOrderVertex>();

            foreach (PartialOrderVertex a in this.PartialOrderVertexList)
            {
                if (a.ProVertex.Count == 0)
                    result.Add(a);
            }

            return result;

        }

        /// <summary>
        /// 初始化邻接矩阵
        /// </summary>
        public void IniOrderMatrix()
        {

            List<PartialOrderVertex> currentVerticesList = new List<PartialOrderVertex>();

            currentVerticesList = GetAllSourceVertices();
            
            //利用广度搜索初始化邻接矩阵
            while (currentVerticesList.Count > 0)
            {
                //记录广度搜索下一层的所有顶点
                List<PartialOrderVertex> nextVerticesList = new List<PartialOrderVertex>();
                
                foreach (PartialOrderVertex a in currentVerticesList)
                {

                    foreach (PartialOrderVertex b in a.ProVertex)
                    {
                        int tmpIndex = FindVertexIndex(nextVerticesList, b);

                        if (tmpIndex == -1)
                        {
                            nextVerticesList.Add(b.Clone());

                            tmpIndex = nextVerticesList.Count - 1;
                        }
                        
                        
                        foreach (PartialOrderVertex c in a.PreVertex)
                        {
                            if (FindVertexIndex(nextVerticesList[tmpIndex].PreVertex, c) == -1)
                            {
                                nextVerticesList[tmpIndex].PreVertex.Add(c);

                                
                            }

                            this.OrderMatrix[c.VertexIndex][nextVerticesList[tmpIndex].VertexIndex] = 1;
                        }
                                                

                    }

                }

                currentVerticesList = nextVerticesList;
            }
        }

        /// <summary>
        /// 返回给定顶点子图的入度与出度
        /// </summary>
        /// <param name="vertexIndexList">子图顶点索引列表</param>
        /// <returns>二元数组列表，其中数组首项为入度，次项为出度</returns>
        private List<int[]> IniDegree(List<int> vertexIndexList)
        {
            List<int[]> degreeResultList = new List<int[]>();

            return degreeResultList;

        }

        /// <summary>
        /// 初始化已拓扑排序的当前顶点的出度与入度
        /// </summary>
        public void IniDegree()
        {
            

            int[] outDegreeArray = new int[TableSize];

            int[] inDegreeAaary = new int[TableSize];

            for (int row = TableSize - 1; row >= 0; row--)
            {
                for (int column = row - 1; column >= 0; column--)
                {
                    if (this.OrderMatrix[row][column] == 1)
                    {
                        outDegreeArray[row] = outDegreeArray[row] + 1;
                        inDegreeAaary[column] = inDegreeAaary[column] + 1;
                    }

                }

                this.PartialOrderVertexList[row].OutDegree = outDegreeArray[row];


            }

            for (int i = 0; i < this.PartialOrderVertexList.Count; i++)
            {
                this.PartialOrderVertexList[i].InDegree = inDegreeAaary[i];

            }

        }

        /// <summary>
        /// 取出当前偏序表的子表(当前偏序表已拓扑排序）
        /// </summary>
        /// <param name="consideredOrNot">1 代表将纳入子集 否则为0 数组大小等于TableSize</param>
        /// <returns>子表(顶点子集与邻接关系子阵)</returns>
        public PartialOrderTable GetSubTable(int[] consideredOrNot)
        {
            if (consideredOrNot.Length != this.TableSize)
                return null;

            PartialOrderTable subTable = new PartialOrderTable(consideredOrNot.Sum());

            int tmpSubTableSize = 0;

            int[] newIndexList = new int[this.TableSize];
            //生成子图的顶点集以及相关信息(保持拓扑排序的逆序)
            for (int i = 0; i <this.TableSize; i++)
            {
                if (consideredOrNot[i] == 1)
                {
                    PartialOrderVertex a = new PartialOrderVertex();

                    a.VertexColor = this.PartialOrderVertexList[i].VertexColor;

                    a.VertexIndex = tmpSubTableSize;

                    foreach (PartialOrderVertex b in this.PartialOrderVertexList[i].ProVertex)
                    {
                        if (consideredOrNot[b.VertexIndex] == 1)
                        {
                            a.ProVertex.Add(subTable.PartialOrderVertexList[newIndexList[b.VertexIndex]]);
                            subTable.PartialOrderVertexList[newIndexList[b.VertexIndex]].PreVertex.Add(a);

                            subTable.OrderMatrix[a.VertexIndex][ newIndexList[b.VertexIndex]] = 1;
                        }
                    }
                    
                    newIndexList[i] = a.VertexIndex;
                    
                    subTable.PartialOrderVertexList.Add(a);

                    tmpSubTableSize = tmpSubTableSize + 1;
                }
                else
                    newIndexList[i] = -1;

            }

            subTable.IniDegree();

            return subTable;
        }

        /// <summary>
        /// 删去给定行列生成的子图(假定图已按照拓扑逆序排序）
        /// </summary>
        /// <param name="removeOrNot">待删去的（source点）行列索引</param>
        /// <returns>剩余子图</returns>
        public PartialOrderTable RemoveVerticesBySource(List<int> toRemoveVertices)
        {
            
            PartialOrderTable resultPartialOrderTable = new PartialOrderTable(this.TableSize - toRemoveVertices.Count);

            return resultPartialOrderTable;

        }

        /// <summary>
        /// 删去指定索引的Source顶点
        /// </summary>
        /// <param name="x"></param>
        public void RemoveSouceVertexAt(int toRemoveIndex)
        {
            if (toRemoveIndex >= 0 && toRemoveIndex < this.TableSize && this.PartialOrderVertexList[toRemoveIndex].PreVertex.Count == 0)
            {
                this.TableSize--;

                foreach (List<int> rowOfTable in this.OrderMatrix)
                {
                    rowOfTable.RemoveAt(toRemoveIndex);
                }

                this.OrderMatrix.RemoveAt(toRemoveIndex);

                foreach (PartialOrderVertex proVer in this.PartialOrderVertexList[toRemoveIndex].ProVertex)
                {
                    proVer.PreVertex.Remove(this.PartialOrderVertexList[toRemoveIndex]);
                    proVer.InDegree--;
                }

                this.PartialOrderVertexList.RemoveAt(toRemoveIndex);

                
            }
        }
        /// <summary>
        /// 生成前n个点可能的偏序表
        /// </summary>
        /// <returns></returns>
        public PartialOrderTable GetConsideredSubTable(int n, bool[] vertexColorList)
        {
            //if (n >= this.TableSize)
            //{
            //    for(int i=this.PartialOrderVertexList

            //}
            //else
            //{
            int[] consideredVertexFlags = new int[this.TableSize];

            foreach (PartialOrderTable.PartialOrderVertex a in this.PartialOrderVertexList)
            {
                if (vertexColorList[a.VertexIndex] == true && a.InDegree < n && consideredVertexFlags[a.VertexIndex] == 0)
                {
                    consideredVertexFlags[a.VertexIndex] = 1;

                    for (int i = a.VertexIndex + 1; i < this.TableSize; i++)
                    {
                        if (this.OrderMatrix[i][a.VertexIndex] == 1 && consideredVertexFlags[i] == 0)
                            consideredVertexFlags[i] = 1;

                    }

                }

            }

            return (this.GetSubTable(consideredVertexFlags));


        }
        
        /// <summary>
        /// 返回列表中包含偏序顶点a的索引，若不包含返回-1
        /// </summary>
        /// <param name="verticesList"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        private int FindVertexIndex(List<PartialOrderVertex> verticesList, PartialOrderVertex a)
        {
            int i = 0;
            foreach (PartialOrderVertex b in verticesList)
            {


                if (b.VertexIndex == a.VertexIndex)

                    return i;


                i = i + 1;

            }

            return -1;
            
        }

        /// <summary>
        /// 对该偏序表的偏序关系取反，生成新的偏序表
        /// </summary>
        /// <returns></returns>
        public PartialOrderTable Inverse()
        {
            PartialOrderTable inverseTable = new PartialOrderTable(this.TableSize);

            return inverseTable;

        }

        #endregion
    }
}
