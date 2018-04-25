using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.Algorithm;

namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// 包含一些关于排序，搜索等通用基础算法的类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2015-10-08       ver1.0
    /// </summary>
    public class CommonAlgorithm
    {
        /// <summary>
        /// 给定顺序列表（不含有相同元素，且元素定义好-，大小等关系）和相应元素（统一为double型），利用类二分法查找元素在列表中的索引值，
        /// 若存在返回指定索引值，若不存在返回该元素按顺序应该插入的索引值
        /// </summary>
        /// <param name="toSearchList"></param>
        /// <param name="element"></param>
        /// <returns>返回element将在sortedList中已有或即将插入的索引</returns>
        public static int FindIndexAt<T>(List<T> haveSortedList, T element, int beginIndex, int endIndex) where T : IConvertible
        {
            double proportion = 0;
            int totalNum = endIndex - beginIndex;
            int propIndex = 0;

            List<double> tmpSortedList = haveSortedList as List<double>;
            //double tmpElement = element.ToDouble(null);
            double tmpElement = (double)Convert.ChangeType(element, typeof(double));
            if (totalNum > 0)
            {

                double theDiff = tmpElement - tmpSortedList[beginIndex];
                double totalDiff = tmpSortedList[endIndex] - tmpSortedList[beginIndex];

                if (theDiff >= 0)
                {

                    if (theDiff == 0)
                        return beginIndex;

                    proportion = theDiff / totalDiff;
                    propIndex = beginIndex + (int)(totalNum * proportion);

                    if (tmpSortedList[propIndex] <= tmpElement)
                    {
                        if (propIndex < endIndex)
                        {
                            if (tmpSortedList[propIndex + 1] > tmpElement)
                                return propIndex + 1;
                            else
                                return FindIndexAt(tmpSortedList, tmpElement, propIndex + 1, endIndex);
                        }
                        else
                        {
                            //此时propIndex==endIndex
                            if (tmpSortedList[endIndex] >= tmpElement)
                                return endIndex;
                            else
                                return endIndex + 1;
                        }
                    }
                    else
                        return FindIndexAt(tmpSortedList, tmpElement, beginIndex, propIndex);

                }
                else
                    return beginIndex;

            }
            else
                return -1;
        }
        
        public static int FindIndexAt<T>(List<T> haveSortedList, T element, int beginIndex) where T : IConvertible
        {
            return (FindIndexAt(haveSortedList, element, beginIndex, haveSortedList.Count - 1));
        }

        public static int FindIndexAt<T>(List<T> mayNotSortedList, T element)
        {
            int index=0;
            foreach(T a in mayNotSortedList)
            {
                if (a.Equals(element) == true)
                    return index;

                index = index + 1;
            }
            return -1;
        }

        /// <summary>
        /// 在有序序列中插入元素
        /// </summary>
        /// <typeparam name="T">序列属性</typeparam>
        /// <param name="toInsertList">有序序列</param>
        /// <param name="element">待插入元素</param>
        public static void InsertAt<T>(List<T> toInsertList,T element) where T : IConvertible
        {
            int toInsertIndex = FindIndexAt(toInsertList,element,0);

            toInsertList.Insert(toInsertIndex,element);

        }


        /// <summary>
        /// 返回列表的最末项
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="SourceList"></param>
        /// <returns></returns>
        public static T TheLastElement<T>(List<T> SourceList)
        {
            if (SourceList.Count > 0)
            {
                return (SourceList[SourceList.Count - 1]);
            }
            else
                return default(T);

        }

        /// <summary>
        /// 选出N阶集合中的k阶子集
        /// </summary>
        /// <param name="n"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        public static List<List<int>> KSubSetOfN(int n, int k)
        {

            int[] S = new int[k];
            int[] T = new int[k];
            int m = 0;
            int i = 0;
            int j = 0;

            List<int> TheSet = new List<int>();

            for (i = 0; i < n; i++)
            {
                TheSet.Add(i);
            }

            List<List<int>> TheFamily = new List<List<int>>();
            for (i = 0; i < k; i++)
            {
                S[i] = i;
                T[i] = n - k + i;
            }

            int Choose = NChooseK(n, k);
            for (i = 1; i <= Choose; i++)
            {
                //m=max{j|S[j]!=n-k-j}
                List<int> tmpSubSet = new List<int>();
                for (j = 0; j < k; j++)
                {
                    if (S[k - 1 - j] != T[k - 1 - j])
                    {
                        m = k - 1 - j;
                        break;
                    }
                }

                for (j = 0; j < k; j++)
                {
                    tmpSubSet.Add(TheSet[S[j]]);       //构造k阶子集

                    //迭代时1)S'[1]=S[1],...,S'[m-1]=S[m-1]; 
                    //2)S'[m]=S[m]+1;S'[m+1]=S'[m]+1,...;

                    if (j == m)
                    {
                        S[j] = S[j] + 1;
                    }
                    else if (j > m)
                    {
                        S[j] = S[j - 1] + 1;
                    }
                }

                TheFamily.Add(tmpSubSet);
            }

            return (TheFamily);
        }

        /// <summary>
        /// 计算n取k组合数
        /// </summary>
        /// <param name="n">n</param>
        /// <param name="k">k</param>
        /// <returns>n取k组合数</returns>
        private static int NChooseK(int n, int k)
        {
            int result1 = 1;
            int result2 = 1;
            for (int i = 1; i <= k; i++)
            {
                result1 = i * result1;
                result2 = result2 * (n + 1 - i);
            }

            return result2 / result1;
        }

        /// <summary>
        /// 初始化指定大小的全零矩阵
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static List<List<int>> IniZeroMatrix(int size)
        {
            List<List<int>> zeroMatrix = new List<List<int>>();

            for (int i = 0; i < size; i++)
            {
                List<int> zeroList = new List<int>();
                for (int j = 0; j < size; j++)
                {
                    zeroList.Add(0);

                }
                zeroMatrix.Add(zeroList);
            }

            return zeroMatrix;

        }

        
    }

    
}
