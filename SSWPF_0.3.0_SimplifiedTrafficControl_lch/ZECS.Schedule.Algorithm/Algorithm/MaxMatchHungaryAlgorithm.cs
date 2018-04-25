using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.Algorithm
{
    //摘要
    //      最大匹配算法器：使用探索增广路径的方法寻找最大匹配
    public class MaxMatchHungaryAlgorithm
    {
        private AdjacencyMatrix stMatrix;
        private Int32[] aResult;
        private bool[] aState;
        private int iMatchTotal;
        private UInt32 uSize;

        public MaxMatchHungaryAlgorithm(ref AdjacencyMatrix stMatrix)
        {
            this.stMatrix = new AdjacencyMatrix( ref stMatrix);
            this.uSize = stMatrix.GetColmSize() > stMatrix.GetLineSize() ? stMatrix.GetColmSize() : stMatrix.GetLineSize();
            aResult = new Int32[uSize];
            aState = new bool[uSize];
            for (int i = 0; i < uSize; i++)
            {
                aResult[i] = -1;
                aState[i] = false;
            }
        }

        // 摘要:
        //     计算最大匹配解，并输出它
        // 参数
        //      mMatch: 保存匹配的解，下标对应行号，值对应列号
        //      uSize: 保存解的有效长度，即uSize之前的内容为有效内容。
        public int GetMaxMatchingSolution(out Int32[] aMatch, out UInt32 uSize)
        {
            if (iMatchTotal < 1)
            {
                CalcMaxMatchingSolution();
            }
            aMatch = new Int32[this.uSize];
            for (int i = 0; i < this.uSize; i++) aMatch[i] = aResult[i];
                uSize = this.uSize;
            return iMatchTotal;
        }

        // 摘要:
        //     输出算法器内部状态
        public string HunAlgToString()
        {
            return "uSize = " + uSize + "; iMatchTotal = " + iMatchTotal + ";\n" + stMatrix.MatrixToString()
                + StateToString() + "\n" + ResultToString();
            
            
        }

        //内部使用： 输出状态矩阵
        private string StateToString()
        {
            string buf = "state = [";
            for (UInt32 i = 0; i < uSize; i++)
            {
                buf += aState[i] + " ";
            }
            buf += "]";
            return buf;
        }

        //内部使用： 输出结果
        private string ResultToString()
        {
            string buf = "result = [";
            for (UInt32 i = 0; i < uSize; i++)
            {
                buf += aResult[i] + " ";
            }
            buf += "]";
            return buf;
        }

        //内部使用： 计算最大匹配解
        private int CalcMaxMatchingSolution()
        {
            iMatchTotal = 0;
            for (UInt32 i = 0; i < stMatrix.GetColmSize(); i++)
            {
                ClearState();
                if (GetSpreadPath(i)) iMatchTotal++;
            }
            return iMatchTotal;
        }

        //内部使用： 获得一个增广路径
        private bool GetSpreadPath(UInt32 uIdx)
        { 
            /*1. get the next connected target*/
            Int32 iStart = 0;
            while ((iStart = stMatrix.GetNextIndxInColm((UInt32)iStart, uIdx, 1)) != -1)
            {
                if (!aState[iStart])
                {
                    aState[iStart] = true;
                    if (aResult[iStart] == -1 || GetSpreadPath((UInt32)aResult[iStart]))
                    {
                        aResult[iStart] = (Int32)uIdx;
                        return true;
                    }
                }
                iStart++;
            }
            return false;
        }

        //内部使用： 清理内部状态矩阵
        private void ClearState()
        {
            for (UInt32 i = 0; i < stMatrix.GetLineSize() || i < stMatrix.GetColmSize(); i++)
            {
                aState[i] = false;
            }
        }
    }
}
