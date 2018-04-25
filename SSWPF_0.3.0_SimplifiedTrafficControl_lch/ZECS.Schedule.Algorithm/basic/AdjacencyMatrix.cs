using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.Algorithm
{
    // 摘要:
    //     普通一维邻接矩阵，描述行、列对象之间的关系，通常0表示无关系，1表示有关系。
    //     提供了拓扑排序、深度优先搜索算法

    public class AdjacencyMatrix : SimpleIntMatrix
    {
        private Int32[] aResult;
        private UInt32 iLen;

        // 摘要:
        //     初始化 AdjacencyMatrix 类的新实例，该实例为空并且具有默认初始容量0。
        public AdjacencyMatrix()
            :base()
        { }

        public AdjacencyMatrix(ref SimpleIntMatrix cSim):base(cSim.IMatrix, cSim.ULineSize, cSim.UColmSize)
        { 
            this.iLen = (cSim.ULineSize > cSim.UColmSize ? cSim.ULineSize : cSim.UColmSize);
            aResult = new Int32[this.iLen];
        }

        public AdjacencyMatrix(UInt32 uLineSize, UInt32 uColmSize)
            : base(uLineSize, uColmSize)
        { 
            this.iLen = (uLineSize > uColmSize ? uLineSize : uColmSize);
            aResult = new Int32[this.iLen];
        }

        public AdjacencyMatrix(UInt32 uMatrSize) 
            : base(uMatrSize, uMatrSize)
        { 
            this.iLen = uMatrSize;
            aResult = new Int32[this.iLen];
        }

        public AdjacencyMatrix(Int32[,] iMatrix, UInt32 uLineSize, UInt32 uColmSize)
            : base(iMatrix, uLineSize, uColmSize)
        { 
            this.iLen = (uLineSize > uColmSize ? uLineSize : uColmSize);
            aResult = new Int32[this.iLen];
        }

        public AdjacencyMatrix( ref AdjacencyMatrix stMatrix)
            :base(stMatrix.IMatrix, stMatrix.ULineSize, stMatrix.UColmSize)
        {        
            this.iLen = (stMatrix.ULineSize > stMatrix.UColmSize ? stMatrix.ULineSize : stMatrix.UColmSize);
            aResult = new Int32[this.iLen];
        }

        // 摘要:
        //     拓扑排序，根据0/1关系将矩阵元素之间的关系进行拓扑排序。
        //
        // 参数:
        //     iRst，输出结果，用于保存最后排序的序列；
        //     uLen，输出有效长度，即iRst中前面uLen个元素为有效元素。
        public void TopoSort(out Int32[] iRst, out UInt32 uLen)
        {
            Int32 iStart = -1;
            Int32 iEnd = -1;
            Int32 iTmp = 0;
            
            AdjacencyMatrix cTempMatrix = new AdjacencyMatrix(this.IMatrix, this.ULineSize, this.UColmSize);
            cTempMatrix.SetPivot(1);
            //1. 查找入度为0的点集
            do{
                iStart = iEnd + 1;
                iTmp = 0;
                while ((iTmp = cTempMatrix.GetNextColmIndxBySum((UInt32)iTmp, 1)) != -1)
                {                
                    aResult[++ iEnd] = iTmp ++;          
                }
                //2. 删除该点集
                for(Int32 i  =iStart; i <= iEnd; i++)
                {
                    cTempMatrix.SetMatrixLineValue((UInt32)aResult[i], 0);
                }         
            }while(iStart <= iEnd);
            SimpleIntMatrix.CopyArray(this.aResult, out iRst, this.iLen);
            uLen = (UInt32)iEnd + 1;
        }

        // 摘要:
        //     搜索图中不重复的匹配集；这里不是最大匹配，而是所有匹配。 将上一次计算的结果作为下一次的参数，继续调用可以继续查找，直到返回结果为空。初始时为空，表示从头开始查找。
        // 参数:
        //     iSolt，输出结果，用于保存匹配关系；
        //     uCount，表示匹配的个数。
        public bool DfsGetNextMatchSolution(Int32[,] iSolt, ref UInt32 uCount)
        {
            bool bFlag = false;
            UInt32 uTmpColIdx = 0;
            UInt32 uTmpLinIdx = 0;
            bool[] bMarkVisited = new bool[this.iLen];
            for (int i = 0; i < uCount; i++) bMarkVisited[iSolt[i, 1]] = true; //标记已经分配的列

            if (uCount == 0)
            {
                bFlag = true;
                iSolt[0, 1] = -1;
            }

            do
            {
                if (bFlag)
                {//向前搜索 
                    uTmpLinIdx = (UInt32)iSolt[uCount, 0];
                    uTmpColIdx = (UInt32)iSolt[uCount, 1];
                    uTmpColIdx++;
                    bFlag = !bFlag;
                    while (this.GetNextIndexInMatrix(ref uTmpLinIdx, ref uTmpColIdx, 1))
                    {
                        if (bMarkVisited[uTmpColIdx])
                        {
                            uTmpColIdx++;
                            continue;
                        }
                        iSolt[uCount, 0] = (Int32)uTmpLinIdx;
                        iSolt[uCount, 1] = (Int32)uTmpColIdx;
                        bMarkVisited[uTmpColIdx] = true;
                        uTmpLinIdx++;
                        uTmpColIdx = 0;
                        uCount++;
                        bFlag = true;
                    }
                    //if (bFlag) break;
                    break;
                }
                else
                { //向后回退
                    if (uCount == 0) break;
                    uCount--;
                    bMarkVisited[iSolt[uCount, 1]] = false;
                    bFlag = true;
                }
            } while (uCount >= 0);
            return uCount > 0;
        }

        // 摘要:
        //     扩展对象之间的关系，使得能够连通的对象都标注连通性。
        //     采用的是矩阵乘法运算，效率未做任何优化。
        //
        public bool SpreadRelation()
        {
            if (this.UColmSize != this.ULineSize) return false; 
            SimpleIntMatrix simOrig = new SimpleIntMatrix(this.IMatrix, this.ULineSize, this.UColmSize);

            for (int i = 2; i < this.ULineSize; i++)
            {
                SimpleIntMatrix simTmp = new SimpleIntMatrix(this.IMatrix, this.ULineSize, this.UColmSize);
                if (!simTmp.MatrixPower(i)) return false;
                if (!SimpleIntMatrix.MatrixPlus(simOrig, simTmp)) return false;
            }

            for (int i = 0; i < this.ULineSize; i++)
            {
                for (int j = 0; j < this.UColmSize; j++)
                {
                    this.IMatrix[i, j] = (simOrig.IMatrix[i, j] > 0 ? 1 : 0);
                }
            }
            return true;
        }

    }
}
