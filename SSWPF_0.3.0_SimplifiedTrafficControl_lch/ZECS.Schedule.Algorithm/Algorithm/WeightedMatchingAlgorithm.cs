using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// 寻找二部图X∪Y最大权值匹配的算法(Kuhn-Munkras算法)
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2015-10-08       ver1.0
    /// </summary>
    public class WeightedMatchingAlgorithm
    {
        #region [ 属性 ]
        /// <summary>
        /// 集合X中顶点的个数
        /// </summary>
        //private int VertexNumOfX;

        /// <summary>
        /// 集合Y中顶点的个数
        /// </summary>
        //private int VertexNumOfY;

        /// <summary>
        /// 集合X中顶点对应的权值
        /// </summary>
        private List<double> WeightOfX;

        /// <summary>
        /// 集合Y中顶点对应的权值
        /// </summary>
        private List<double> WeightOfY;
        
        /// <summary>
        /// 二部图邻接矩阵(子阵），矩阵元素非负，且0代表没有边
        /// </summary>
        public WeightMatrixOfBigraphEntity WeightMatrix;

        /// <summary>
        /// 模型将通过修改边权值来生成多个匹配方案
        /// </summary>
        private WeightMatrixOfBigraphEntity _currentWeightMatrix;
                    
        /// <summary>
        /// 最优匹配方案集合
        /// </summary>
        public List<MatchingEntity>  OptimalMatchingList;

        /// <summary>
        /// 最优匹配方案集合中相应权值列表
        /// </summary>
        public List<double> OptimalMatchingWeightList;

               
        /// <summary>
        /// 最优匹配方案集合及其权值
        /// </summary>
        private SortedList<double,MatchingForWeightedGraphEntity> _currentOptimalMatchingList;
        
        /// <summary>
        /// 最优匹配方案建议数目，算法将给出指定数目的最大权值匹配方案
        /// </summary>
        public int PreferMatchingNum;

        /// <summary>
        /// 点权值weightOfX[i]+WeightOf[j]=weightMatrix[i,j]的相等子图
        /// </summary>
        private AdjacencyMatrix _currentEqSubGraph;

        /// <summary>
        /// 相当于当前带权值图的最大权值
        /// </summary>
        private double _maximalValue;

        /// <summary>
        /// 最小权值，接近于无边相连
        /// </summary>
        private double _minimalValue;

        /// <summary>
        /// 带权值的匹配类
        /// </summary>
        private class MatchingForWeightedGraphEntity : MatchingEntity
        {
            #region [ 属性 ]
            
            public UInt32[] ChangedVetexOfX;

            public double WeightSum;

            public double MinimalWeightOfEdge;

            public int MinimalWeightIndexOfX;

            public bool HasMinimalWeight;

            /// <summary>
            /// 匹配方案产生的权值矩阵
            /// </summary>
            public WeightMatrixOfBigraphEntity TempWeightMatrix;
            
            #endregion

            #region [ 方法 ]

            public MatchingForWeightedGraphEntity(MatchingEntity a):base(a)
            {
                this.ChangedVetexOfX = new UInt32[a.MatchingPlanOfX.Length];
            }

            /// <summary>
            /// 返回匹配对应的权值
            /// </summary>
            /// <param name="a"></param>
            /// <returns></returns>
            public void CalMatchingWeight(double[,] weightMatrix)
            {
                UInt32 i = 0;
                this.WeightSum = 0;
                foreach (UInt32 b in this.MatchingPlanOfX)
                {
                    if (b >= 0)
                    {
                        this.WeightSum = this.WeightSum + weightMatrix[i, b];
                    }
                    i = i + 1;
                }
                                
            }

            /// <summary>
            /// 判断该匹配方案是否包含最小权值边
            /// </summary>
            /// <param name="a">匹配方案 a</param>
            /// <returns>是否包含最小权值</returns>
            public void MatchingHasMinWeightEdge(double[,] weightMatrix,double minimalValue)
            {
                int i = 0;
                
                int[] tmpMatchingIndex = new int[2];
                tmpMatchingIndex = this.FirstMatching();
                double currentMinimalWeight = weightMatrix[tmpMatchingIndex[0], tmpMatchingIndex[1]];
                
                this.MinimalWeightIndexOfX = -1;
                this.MinimalWeightOfEdge = 0;

                foreach (UInt32 b in this.MatchingPlanOfX)
                {
                    if (this.ChangedVetexOfX[i] == 0 && weightMatrix[i, b] < currentMinimalWeight)
                    {
                        currentMinimalWeight = weightMatrix[i, b];
                        this.MinimalWeightIndexOfX = i;
                    }
                    i = i + 1;
                }

                if (currentMinimalWeight == minimalValue)
                {
                    this.HasMinimalWeight = true;
                }
                else
                {
                    this.HasMinimalWeight = false;

                    this.MinimalWeightOfEdge = currentMinimalWeight;
                }
            }

            #endregion
        }
        
        #endregion

        #region [ 方法 ]

        #region [ 构造函数 ]
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="weightMatrix">权值矩阵</param>
        /// <param name="preferMatchingNum">最优匹配方案数</param>
        public WeightedMatchingAlgorithm(WeightMatrixOfBigraphEntity weightMatrix, int preferMatchingNum)
        {
            this.OptimalMatchingWeightList = new List<double>();
            this.OptimalMatchingList = new List<MatchingEntity>();

            this._currentOptimalMatchingList = new SortedList<double, MatchingForWeightedGraphEntity>();

            this.WeightMatrix = new WeightMatrixOfBigraphEntity(ref weightMatrix);
            this._currentWeightMatrix = new WeightMatrixOfBigraphEntity(ref WeightMatrix);

            UInt32 vertexNumOfX = this.WeightMatrix.GetRowSize();
            UInt32 vertexNumOfY = this.WeightMatrix.GetColumnSize();
            
            this.WeightOfX = new List<double>();
            this.WeightOfY = new List<double>();

            this._currentEqSubGraph = new AdjacencyMatrix(vertexNumOfX, vertexNumOfY);

            this._maximalValue = this.WeightMatrix.GetMaximalValue();
            this._minimalValue = 0.00001;
        }

        public WeightedMatchingAlgorithm(WeightMatrixOfBigraphEntity weightMatrix)
            : this(weightMatrix, 1)
        {

        }
        #endregion


        /// <summary>
        /// 匹配初始化
        /// </summary>
        private void IniGenerateMatching()
        {
            UInt32 vertexNumOfX = this.WeightMatrix.GetRowSize();
            UInt32 vertexNumOfY = this.WeightMatrix.GetColumnSize();


            //初始集合X中的顶点的权值定义为其邻边的最大权值,并由此生成初始的相等子图
            for (UInt32 i = 0; i < vertexNumOfX; i++)
            {
                List<UInt32> tmpMaxIndexList = new List<UInt32>();

                tmpMaxIndexList = this._currentWeightMatrix.GetMaxValueColumnIndexList(i);

                if (tmpMaxIndexList.Count > 0)
                {
                    this.WeightOfX.Add(this._currentWeightMatrix.GetMatrixElementByIndex(i, tmpMaxIndexList[0]));
                    foreach (UInt32 a in tmpMaxIndexList)
                    {
                        this._currentEqSubGraph.IMatrix[i, a] = 1;
                    }
                }
                else
                    this.WeightOfX.Add(0);

            }

            //初始集合Y中的顶点权值定义为0
            for (int i = 0; i < vertexNumOfX; i++)
            {
                this.WeightOfY.Add(0);
            }
        }

        /// <summary>
        /// 利用Kuhn-Munkras算法得到最大匹配方案集
        /// </summary>
        public MaxMatchingAlgorithm GenerateOneWeightMatching()
        {
            //在相等子图中用匈牙利方法寻找最大匹配
            MaxMatchingAlgorithm currentMatching = new MaxMatchingAlgorithm(this._currentEqSubGraph);

            IniGenerateMatching();
                        
            //修正相等子图,直至某个顶点集匹配完毕
            while (!currentMatching.ResultMatching.IsMaxMatching)
            {
                currentMatching.GenerateMaxMatching();

                currentMatching.ResultMatching.DetermineMaxMatching();

                List<List<UInt32>> setSAndT = new List<List<UInt32>>();

                //修正相等子图:在当前相等子图的情况下，通过修改权值，增加相等子图的边
                setSAndT = currentMatching.GetSetSAndT();

                double toAdjustWeight = CalAdjustWeight(setSAndT[0], setSAndT[1]);

                AdjustGraphWeight(setSAndT[0], setSAndT[1], toAdjustWeight);

                #region [简单版本]
                /*
                //1.寻找在集合X中寻找未匹配的非饱和点x，并构造集合S与T
                List<int> setS = new List<int>();
                setS.Add(currentMatching.resultMatching.FirstFreeVertexOfX());
                
                List<int> setT = new List<int>();

                //遍历S在相等子图中的邻域却不属于集合T中的元素
                int yOfneighbourSOutT = FindVertexOfNeighbourOfSOutT(setS, setT, this._currentEqSubGraph);

                while (yOfneighbourSOutT == -1 || (yOfneighbourSOutT >= 0 && currentMatching.resultMatching.MatchingPlanOfY[yOfneighbourSOutT] != -1))
                {
                    if (yOfneighbourSOutT == -1)
                    {
                        double toAdjustWeight = CalAdjustWeight(setS, setT);

                        AdjustGraphWeight(setS, setT, toAdjustWeight);

                        
                    }
                    else
                    {

                        CommonAlgorithm.InsertAt(setS, currentMatching.resultMatching.MatchingPlanOfY[yOfneighbourSOutT]);

                        if (setT.Count > 0)
                            CommonAlgorithm.InsertAt(setT, yOfneighbourSOutT);
                        else
                            setT.Add(yOfneighbourSOutT);

                    }

                    yOfneighbourSOutT = FindVertexOfNeighbourOfSOutT(setS, setT, this._currentEqSubGraph);
                }
                */
                #endregion

            }

            return currentMatching;

        }

        

        /// <summary>
        /// 生成给定个数的较优的最大权值匹配方案
        /// </summary>
        public void GenerateAllWeighttedMatchings()
        {
            //算法将置权值矩阵中各匹配方案中相对权值（边权值比上匹配权值）最小的匹配边为0，再迭代出新的匹配方案
            //直至达到给定数量或者匹配方案的某条匹配边为最小值
            bool hasMinWeight = false;

            for (int i = 0; i < this.PreferMatchingNum && hasMinWeight == false; i++)
            {
                if (i > 0 && this._currentOptimalMatchingList.Count > 0)
                {
                    //找到将要迭代的匹配方案
                    double tmpMin = 1;
                    int toAdjustMatchingIndex = -1;
                    for (int j = 0; j < this._currentOptimalMatchingList.Count; j++)
                    {
                        double tmpMatchingIndex = this._currentOptimalMatchingList.Values[j].MinimalWeightOfEdge / this._currentOptimalMatchingList.Values[j].WeightSum;
                        if (tmpMatchingIndex < tmpMin)
                        {
                            tmpMin = tmpMatchingIndex;
                            toAdjustMatchingIndex = j;
                        }
                    }

                    //更改将要迭代匹配方案对应的权值矩阵,将选出的匹配方案以及最小匹配边置成最小值
                    this._currentWeightMatrix = this._currentOptimalMatchingList.Values[toAdjustMatchingIndex].TempWeightMatrix;
                    int tempI = this._currentOptimalMatchingList.Values[toAdjustMatchingIndex].MinimalWeightIndexOfX;
                    int tempJ = this._currentOptimalMatchingList.Values[toAdjustMatchingIndex].MatchingPlanOfX[tempI];
                    this._currentOptimalMatchingList.Values[toAdjustMatchingIndex].ChangedVetexOfX[tempI] = 1;
                    this._currentWeightMatrix.WeightedMatrix[tempI, tempJ] = this._minimalValue;
                                        
                }
                else
                {
                    this._currentWeightMatrix = this.WeightMatrix;
                }

                MaxMatchingAlgorithm tempMaxMatching = new MaxMatchingAlgorithm(this.WeightMatrix.GetRowSize(), this.WeightMatrix.GetColumnSize());
                tempMaxMatching = this.GenerateOneWeightMatching();
                MatchingForWeightedGraphEntity tempWeightMaxMatching = new MatchingForWeightedGraphEntity(tempMaxMatching.ResultMatching);
                tempWeightMaxMatching.CalMatchingWeight(this.WeightMatrix.WeightedMatrix);
                tempWeightMaxMatching.MatchingHasMinWeightEdge(this.WeightMatrix.WeightedMatrix, this._minimalValue);

                if (tempWeightMaxMatching.HasMinimalWeight == true)
                    hasMinWeight = true;
                else
                {
                    tempWeightMaxMatching.TempWeightMatrix = this._currentWeightMatrix;
                    this._currentOptimalMatchingList.Add(tempWeightMaxMatching.WeightSum, tempWeightMaxMatching);

                }
            }

        }

        /// <summary>
        /// 寻找顶点集合S的邻域去掉集合T之后的元素
        /// </summary>
        /// <param name="s">X部的顶点集S</param>
        /// <param name="t">Y部的顶点集T</param>
        /// <param name="m">X与Y间的邻接矩阵M</param>
        /// <returns>T中不属于S邻域的元素，若无返回-1</returns>
        private int FindVertexOfNeighbourOfSOutT(List<int> s, List<int> t,AdjacencyMatrix m)
        {
            //得到顶点S的邻域
            UInt32 setSize = m.GetColmSize();
            int[] neighbourOfS = new int[setSize];
            foreach (int a in s)
            {
                for (int i = 0; i < setSize; i++)
                {
                    if (neighbourOfS[i] == 0 && m.IMatrix[a, i] > 0)
                        neighbourOfS[i] = 1;
                }
            }

            foreach (int a in t)
            {
                if (neighbourOfS[a] == 0)
                    return a;
            }

            return -1;
        }

        /// <summary>
        /// 计算KM算法中中间调整的权值
        /// </summary>
        /// <param name="s"></param>
        /// <param name="t"></param>
        /// <param name="m"></param>
        /// <returns></returns>
        private double CalAdjustWeight(List<UInt32> s, List<UInt32> t)
        {
            //选取端点在S（X)内，但另一端点不在T(Y)内权值最小的边权值作为待修正的权值
            double tmpMinValue = this._maximalValue;
            UInt32 setSize = this._currentWeightMatrix.GetColumnSize();

            foreach (int a in s)
            {
                for (UInt32 i = 0; i < setSize; i++)
                {
                    if (this._currentWeightMatrix.WeightedMatrix[a, i] > 0)
                    {
                        int tmpIndex = CommonAlgorithm.FindIndexAt(t, i, 0);
                        
                        if (tmpIndex != -1 && t[tmpIndex] != i)
                        {
                            if (this._currentWeightMatrix.WeightedMatrix[a, i] < tmpMinValue)
                                tmpMinValue = this._currentWeightMatrix.WeightedMatrix[a, i];
                        }
                    }
                }
            }

            return tmpMinValue;
        }
        
        /// <summary>
        /// 调整图的权值，从而调整相等子图
        /// </summary>
        /// <param name="s"></param>
        /// <param name="t"></param>
        private void AdjustGraphWeight(List<UInt32> s, List<UInt32> t,double weight)
        {
            //对于集合S中的点集，其权值减去weight
            foreach (int a in s)
            {
                this.WeightOfX[a] = this.WeightOfX[a] - weight;
            }
            //对于集合T中的点集，其权值加上weight
            foreach (int a in t)
            {
                this.WeightOfY[a] = this.WeightOfY[a] + weight;

            }

            //修正相等子图
            foreach (int a in s)
            {
                for (int b = 0; b < this._currentWeightMatrix.GetColumnSize(); b++)
                {
                    //已是相等子图的边将出现在新的相等子图之中
                    if (this._currentEqSubGraph.IMatrix[a, b] == 0 && this.WeightOfX[a] + this.WeightOfY[b] == this._currentWeightMatrix.WeightedMatrix[a, b])
                        this._currentEqSubGraph.IMatrix[a, b] = 1;
                }
                
            }

            foreach (int a in t)
            {
                for (int b = 0; b < this._currentWeightMatrix.GetRowSize(); b++)
                {
                    if (this._currentEqSubGraph.IMatrix[b, a] == 0 && this.WeightOfX[b] + this.WeightOfY[a] == this._currentWeightMatrix.WeightedMatrix[b, a])
                        this._currentEqSubGraph.IMatrix[b, a] = 1;
                }
            }
        }

        #endregion
    }
}
