using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// 二部图最大匹配算法(利用Hopcroft-Karp算法）
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2015-10-08       ver1.0
    /// </summary>
    public class MaxMatchingAlgorithm
    {
        #region [ 属性 ]
        /// <summary>
        /// 二部图的邻接矩阵（0-1矩阵）
        /// </summary>
        private AdjacencyMatrix stMatrix;

        /// <summary>
        /// 最大匹配方案结果
        /// </summary>
        public MatchingEntity ResultMatching;

        /// <summary>
        /// 由最大匹配诱导出的所有最大匹配方案
        /// </summary>
        public List<MatchingEntity> InducedMatchingList;

        /// <summary>
        /// 算法过程中记录集合X中的未匹配顶点
        /// </summary>
        private List<UInt32> freeVertexSetOfX;
        /// <summary>
        /// 算法生成交错路时采用的匹配
        /// </summary>
        private MatchingEntity currentMatching;

        /// <summary>
        /// 达到最大匹配时按照BFS搜索各层顶点集合
        /// </summary>
        private List<LayerInBFS> layerListAtMaxMatching;

        /// <summary>
        /// 算法中间过程中的匹配方案
        /// </summary>
        //private MatchingEntity iMatching;

        /// <summary>
        /// BFS中涉及的层类
        /// </summary>
        private class LayerInBFS
        {
           /// <summary>
           /// 当前层顶点索引列表
           /// </summary>
            public List<UInt32> CurrentLayerVertexIndexList;
           
            /// <summary>
            /// 记录当前层顶点属于集合X(true)还是集合Y(false)
            /// </summary>
            public bool CurrentLayerProperty;

            /// <summary>
            /// CurrentLayerProperty为false,且存在未匹配的顶点
            /// </summary>
            public bool IsExistFreeY;
            
            
            public LayerInBFS()
           {
               this.CurrentLayerVertexIndexList = new List<UInt32>();
               this.IsExistFreeY = false;
           }
        }

        /// <summary>
        /// 交错路类
        /// </summary>
        private class AugmentingPath
        {
            public List<UInt32> Path;

            public bool IsStartAtX;

            public UInt32 EndVertex()
            {
                //if (this.Path.Count > 0)
                    return this.Path[this.Path.Count - 1];
                //else
                //    return -1;
            }
        }
        
        #endregion

        #region [ 方法 ]
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="stMatrix"></param>
        public MaxMatchingAlgorithm( AdjacencyMatrix stMatrix)
        {
            UInt32 xSize = stMatrix.GetLineSize();
            UInt32 ySize = stMatrix.GetColmSize(); 
            
            this.stMatrix = new AdjacencyMatrix( ref stMatrix);
            this.ResultMatching = new MatchingEntity(xSize,ySize);
            this.currentMatching = new MatchingEntity(xSize, ySize);

            //记录当前未匹配的集合X的顶点
            List<UInt32> freeVertexSetOfX = new List<UInt32>();

            //记录当前未匹配的集合Y的顶点
            //List<UInt32> freeVertexSetOfY = new List<UInt32>();

            for (UInt32 i = 0; i < xSize; i++)
            {
                freeVertexSetOfX.Add(i);
            }
            this.layerListAtMaxMatching = new List<LayerInBFS>();
            
            //this.iMatching = new MatchingEntity(stMatrix.GetLineSize(), stMatrix.GetColmSize());
        }

        public MaxMatchingAlgorithm(UInt32 xSize,UInt32 ySize)
        {
            this.stMatrix = new AdjacencyMatrix(xSize, ySize);
            this.ResultMatching = new MatchingEntity(xSize, ySize);
            this.currentMatching = new MatchingEntity(xSize, ySize);
            List<UInt32> freeVertexSetOfX = new List<UInt32>();

            //记录当前未匹配的集合Y的顶点
            //List<UInt32> freeVertexSetOfY = new List<UInt32>();

            for (UInt32 i = 0; i < xSize; i++)
            {
                freeVertexSetOfX.Add(i);
            }
            this.layerListAtMaxMatching = new List<LayerInBFS>();
        }
        
        /// <summary>
        /// 根据Hopcroft-Karp算法，生成最大匹配(由this.freeVertexSetOfX与this.currentMatching作为起始，开始迭代）
        /// </summary>
        public void GenerateMaxMatching()
        {
            
            //是否存在增广交错路
            bool hasAugmentingPath = true;

            LayerInBFS iniLayer = new LayerInBFS();
                        
            //判断是否存在增广交错路
            while (hasAugmentingPath)
            {
                List<AugmentingPath> currentAugmentingPathList = new List<AugmentingPath>();

                iniLayer.CurrentLayerVertexIndexList = this.freeVertexSetOfX;
                iniLayer.CurrentLayerProperty = true;
                iniLayer.IsExistFreeY = false;

                currentAugmentingPathList = GenerateAugmentPath(iniLayer,this.currentMatching);
                
                //根据各顶点不交的可扩交错路，修改当前匹配方案
                foreach (AugmentingPath a in currentAugmentingPathList)
                {
                    GetMatchingByAugmentingPath(a);
                }
                
                if (currentAugmentingPathList.Count == 0)
                {
                    hasAugmentingPath = false;

                    this.ResultMatching = currentMatching;
                }
                
            }

            
        }

        

        /// <summary>
        /// 根据可扩交错路更改现有匹配方案this.currentMatching
        /// </summary>
        /// <param name="a">可扩路a</param>
        private void GetMatchingByAugmentingPath(AugmentingPath a)
        {
            //匹配从可扩路首个顶点开始，生成一对，取消一对
            for (int i = 0; i < a.Path.Count - 1; i = i + 2)
            {
                //生成一对新匹配(实际也取消一对）
                if (a.IsStartAtX == true)
                {
                    this.currentMatching.MatchingPlanOfX[a.Path[i]] = (int)a.Path[i + 1];
                    this.currentMatching.MatchingPlanOfY[a.Path[i + 1]] = (int)a.Path[i];
                }
                else
                {
                    this.currentMatching.MatchingPlanOfY[a.Path[i]] = (int)a.Path[i + 1];
                    this.currentMatching.MatchingPlanOfX[a.Path[i + 1]] = (int)a.Path[i];
                }

            }
        }

        /// <summary>
        /// 寻找点不相交的可扩交错路
        /// </summary>
        /// <param name="iniLayer"></param>
        /// <param name="currentMatching"></param>
        /// <returns></returns>
        private List<AugmentingPath> GenerateAugmentPath(LayerInBFS iniLayer, MatchingEntity currentMatching)
        {
            LayerInBFS nextLayer = new LayerInBFS();
            nextLayer = iniLayer;
            List<LayerInBFS> currentLayerList = new List<LayerInBFS>();
            currentLayerList.Add(iniLayer);

            //初始时保留所有集合Y中的顶点
            List<UInt32> remainderVertexOfYList = new List<UInt32>();
            for (UInt32 i = 0; i < currentMatching.MatchingPlanOfY.GetLength(0); i++)
            {
                remainderVertexOfYList.Add(i);
            }
            //按照BFS方式按层排列顶点，直至某一层出现集合y中的未匹配顶点
            while (nextLayer.IsExistFreeY == false ||  nextLayer.CurrentLayerVertexIndexList.Count > 0)
            {
                nextLayer = GetNextLayerByBFS(nextLayer, ref remainderVertexOfYList, currentMatching);
                if (nextLayer.CurrentLayerVertexIndexList.Count > 0)
                    currentLayerList.Add(nextLayer);
                else
                    this.layerListAtMaxMatching = currentLayerList;
            }


            //列出点不相交的各最短可扩交错路
            List<AugmentingPath> resultAugmentingPath = new List<AugmentingPath>();
            
            if (currentLayerList.Count > 1)
            {
                while (currentLayerList[currentLayerList.Count - 1].CurrentLayerVertexIndexList.Count > 0)
                {
                    for (int i = currentLayerList[currentLayerList.Count - 1].CurrentLayerVertexIndexList.Count - 1; i >= 0; i--)
                    {
                        //若最末层Y集合中的顶点为free，则产生可扩交错路
                        UInt32 currentLastLayerVertex = currentLayerList[currentLayerList.Count - 1].CurrentLayerVertexIndexList[i];
                        
                        if (currentMatching.MatchingPlanOfY[currentLastLayerVertex] == -1)
                        {
                            AugmentingPath currentPath = new AugmentingPath();
                            //往交错路里添加顶点
                            currentPath.Path.Add(currentLastLayerVertex);
                            currentPath.IsStartAtX = false;

                            //遍历每一层
                            for (int j = currentLayerList.Count - 2; j >= 0; j--)
                            {
                                //若当前层为X中的顶点，则遍历这些顶点，直至其中某个顶点与currentpath末端点有边相连
                                if (currentLayerList[j].CurrentLayerProperty == true)
                                {
                                    int k = 0;
                                    foreach (UInt32 currentVertex in currentLayerList[j].CurrentLayerVertexIndexList)
                                    {
                                        if (this.stMatrix.IMatrix[currentVertex, currentPath.EndVertex()] == 1)
                                        {
                                            currentPath.Path.Add(currentVertex);
                                            currentLayerList[j].CurrentLayerVertexIndexList.RemoveAt(k);
                                            //首层肯定为集合X中的顶点，且此时一条完整的可扩交错路已生成
                                            if (j == 0)
                                            {
                                                //修正集合X中的未匹配点
                                                int tmpIndex = CommonAlgorithm.FindIndexAt(this.freeVertexSetOfX, currentVertex, 0);
                                                if (this.freeVertexSetOfX[tmpIndex] == currentVertex)
                                                    this.freeVertexSetOfX.RemoveAt(tmpIndex);
                                                resultAugmentingPath.Add(currentPath);
                                            }
                                            break;
                                        }
                                        k = k + 1;
                                    }
                                }
                                else
                                {
                                    //非最末层的集合Y中的顶点必是已匹配顶点
                                    currentPath.Path.Add((UInt32)currentMatching.MatchingPlanOfY[currentPath.EndVertex()]);
                                    //不需要删除该层顶点
                                }
                            }
                        }

                        currentLayerList[currentLayerList.Count - 1].CurrentLayerVertexIndexList.RemoveAt(i);


                    }
                }
            }

            return resultAugmentingPath;
        }

        
        
        /// <summary>
        /// 利用BFS寻找下一层顶点
        /// </summary>
        /// <param name="currentLayer">当前层顶点列表</param>
        /// <param name="remainderVertexIndexList">未考虑的顶点列表</param>
        /// <param name="currentMatching">当前匹配</param>
        /// <returns>下一层顶点列表</returns>
        private LayerInBFS GetNextLayerByBFS(LayerInBFS currentLayer,ref List<UInt32> remainderVertexIndexList, MatchingEntity currentMatching)
        {
            //若当前层的顶点为X中的顶点，下一层的顶点为Y中与当前层顶点关联单没有匹配边的那些顶点
            //若当前层的顶点为Y中的顶点，下一层的顶点为X中与当前层以匹配边关联的那些顶点
            LayerInBFS nextLayer = new LayerInBFS();

            nextLayer.CurrentLayerProperty = !currentLayer.CurrentLayerProperty;

            if (currentLayer.CurrentLayerProperty == true)
            {
                foreach (UInt32 vertexIndex in currentLayer.CurrentLayerVertexIndexList)
                {
                    //遍历该顶点的邻接关系
                    for (UInt32 i = 0; i < this.stMatrix.UColmSize; i++)
                    {
                        int tmpIndex = CommonAlgorithm.FindIndexAt(remainderVertexIndexList, i, 0);
                        //若有边相连且i存在于remainderVertexIndexList之中，且此时该邻接边一定是非匹配边
                        if (this.stMatrix.IMatrix[vertexIndex, i] == 1 && remainderVertexIndexList[tmpIndex] == i)
                        {
                            //此时该顶点加入下一层，且从remainderVertexIndexList中移除该顶点
                            nextLayer.CurrentLayerVertexIndexList.Add(i);
                            remainderVertexIndexList.RemoveAt(tmpIndex);

                            if (nextLayer.IsExistFreeY == false && currentMatching.MatchingPlanOfX[tmpIndex] == -1)
                                nextLayer.IsExistFreeY = true;

                        }
                    }

                }

            }
            else
            {
                foreach (UInt32 vertexIndex in currentLayer.CurrentLayerVertexIndexList)
                {
                    //将匹配中的相应X中的顶点加入下一层,Y应该都是已匹配的顶点
                    UInt32 toAddIndex = (UInt32)currentMatching.MatchingPlanOfY[vertexIndex];
                    nextLayer.CurrentLayerVertexIndexList.Add(toAddIndex);
                    //不需要更新remainderVertexIndexList
                    
                }
            }

            return nextLayer;
        }

        /// <summary>
        /// 返回得到最大匹配时（无交错可扩路），集合S(属于X)与T(属于Y)；用以最大权值匹配中，计算修正权值。
        /// </summary>
        /// <returns>首层为集合S，此层为集合T,最大匹配时将满足S的邻域是T</returns>
        public List<List<UInt32>> GetSetSAndT()
        {
            //第一层必为X中元素，最后一层也为X中元素，且其邻域在以上各层的Y中元素
            int k = 0;
            List<List<UInt32>> resultSAndT = new List<List<UInt32>>();
            List<UInt32> s = new List<UInt32>();
            List<UInt32> t = new List<UInt32>();

            foreach (LayerInBFS a in this.layerListAtMaxMatching)
            {

                foreach (UInt32 b in a.CurrentLayerVertexIndexList)
                {
                    if (k % 2 == 0)
                    {

                        if (s.Count == 0)
                            s.Add(b);
                        else
                            CommonAlgorithm.InsertAt(s, b);
                    }
                    else
                    {
                        if (t.Count == 0)
                            t.Add(b);
                        else
                            CommonAlgorithm.InsertAt(t, b);
                    }
                }
                k = k + 1;        
            }
            resultSAndT.Add(s);
            resultSAndT.Add(t);

            return resultSAndT;
        }

        
        
        /// <summary>
        /// 生成由当前最大匹配方案以及相应的各层顶点
        /// </summary>
        public void GenerateInducedMaxMatching()
        {
            //若当前的最大匹配中仍有未匹配X中顶点,则将这些点作为第一层顶点，否则任取X中一个顶点作为第一层顶点。
            int firstFreeVertexOfX = this.ResultMatching.FirstFreeVertexOfX();
            this.currentMatching = this.ResultMatching;
            
            if (firstFreeVertexOfX >=0)
            {
                this.freeVertexSetOfX = this.ResultMatching.FreeVertexOfX();
                

            }
            else
            {
                this.freeVertexSetOfX.Clear();
                this.freeVertexSetOfX.Add(0);
            }
        }

        #endregion

    }
}
