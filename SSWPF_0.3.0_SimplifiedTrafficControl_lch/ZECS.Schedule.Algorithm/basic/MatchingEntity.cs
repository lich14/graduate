using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// 二部图匹配方案类
    /// </summary>
   public class MatchingEntity
   {
        #region [ 属性 ]
        /// <summary>
        /// 匹配方案，记录顶点集合X中匹配的Y中顶点的Index
        /// </summary>
        public int[] MatchingPlanOfX;

        /// <summary>
        /// 顶点集合Y中匹配的X中元素的Index
        /// </summary>
        public int[] MatchingPlanOfY;

        /// <summary>
        /// 匹配数量
        /// </summary>
        public int MatchingCardinalNumber;

        /// <summary>
        /// 是否是最大匹配
        /// </summary>
        public bool IsMaxMatching;

        #endregion

        #region [ 方法 ]

        #region [ 构造函数 ]
        
        public MatchingEntity(UInt32 xSize, UInt32 ySize)
        {
            this.MatchingPlanOfX = new int[xSize];
            this.MatchingPlanOfY = new int[ySize];

            //空匹配
            for (int i = 0; i < xSize; i++)
            {
                this.MatchingPlanOfX[i] = -1;
            }

            for (int i = 0; i < ySize;i++ )
            {
                this.MatchingPlanOfY[i] = -1;
            }

            this.MatchingCardinalNumber = 0;
            this.IsMaxMatching = false;
        }

        public MatchingEntity(MatchingEntity a)
        {
            this.MatchingPlanOfX = new int[a.MatchingPlanOfX.Length];
            this.MatchingPlanOfY = new int[a.MatchingPlanOfY.Length];

            this.MatchingPlanOfX = a.MatchingPlanOfX;
            this.MatchingPlanOfY = a.MatchingPlanOfY;

            this.IsMaxMatching = a.IsMaxMatching;
            this.MatchingCardinalNumber = a.MatchingCardinalNumber;
        }

        
        #endregion


        /// <summary>
        /// 判断当前的匹配是否是极大匹配，即至少其中一个顶点集匹配完毕
        /// </summary>
        public void DetermineMaxMatching()
        {
            bool isXMatching = true;
            bool isYMatching = true;

            foreach (int a in this.MatchingPlanOfX)
            {
                if (a == -1)
                {
                    isXMatching = false;
                    break;
                }
            }

            if (isXMatching)
            {
                this.IsMaxMatching = true;
                this.MatchingCardinalNumber = this.MatchingPlanOfX.Length;
                return;
            }
            
            foreach (int a in this.MatchingPlanOfY)
            {
                if (a == -1)
                {
                    isYMatching = false;
                    break;
                }
            }

            if (isYMatching)
            {
                this.IsMaxMatching = true;
                this.MatchingCardinalNumber = this.MatchingPlanOfY.Length;
            }
        }

        /// <summary>
        /// 寻找集合X中所有未被匹配的顶点
        /// </summary>
        /// <returns></returns>
        public List<UInt32> FreeVertexOfX()
        {
            List<UInt32> freeVertexList = new List<UInt32>();
            int i= 0;
            foreach (int a in this.MatchingPlanOfX)
            {
                if (a == -1)
                    freeVertexList.Add((UInt32)i);

                i = i + 1;
            }
            return freeVertexList;
        }

        public int FirstFreeVertexOfX()
        {
            int i = 0;
            foreach (int a in this.MatchingPlanOfX)
            {
                if (a == -1)
                    return i;
            }

            return -1;
        }

        public int[] FirstMatching()
        {
            int[] firstMatching = new int[2];
            int i = 0;
            foreach (int a in this.MatchingPlanOfX)
            {
                if (a >= 0)
                {
                    firstMatching[0] = i;
                    firstMatching[1] = a;
                }
                i = i + 1;
            }

            return firstMatching;
        }

        /// <summary>
        /// 寻找集合Y中所有未被匹配的顶点
        /// </summary>
        /// <returns></returns>
        public List<int> FreeVertexOfY()
        {
            List<int> freeVertexList = new List<int>();
            int i = 0;
            foreach (int a in this.MatchingPlanOfY)
            {
                if (a == -1)
                    freeVertexList.Add(i);

                i = i + 1;
            }
            return freeVertexList;
        }

        

        #endregion
    }
}
