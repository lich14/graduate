using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.Algorithm
{
    /* 指派问题匈牙利算法器
     * 本模型使用优化之后的KM算法，通过效率矩阵来求最佳匹配，其中效率矩阵中数值越高则效率越差；
     * 最终匹配结果是以匹配总和最小为最优解。
     */
    public class AppointQuestionHungaryAlgorithm
    {
        private SquareMatrix cApppointMatrix;   //效率矩阵，保存指派任务和执行者的对应效率
        private SquareMatrix cMarkMatrix; //用来标记矩阵中元素的状态，保存作为KM算法的中间数据；用来刻画已经分派的任务、可以分派的任务、以及被最小点集覆盖的元素
        private Int32[] iLineMark;  //用来标记行的状态，保存KM算法的中间数据
        private Int32[] iColmMark;  //用来标记列的状态，保存KM算法的中间数据
        private UInt32 uSize;   //矩阵的阶
        private Int32[] iResult;    //保存最终的分配结果
        private Int32 iMatchTotal; //保存最大匹配的数
        private UInt32 uLineSize; //保存输入矩阵的行数，供非方正时输出调整
        private UInt32 uColmSize; //保存输入矩阵的列数，暂未使用
        
        public AppointQuestionHungaryAlgorithm(Int32[,] iMatrix, UInt32 uSize)
        {
            cApppointMatrix = new SquareMatrix(iMatrix, uSize);
            cMarkMatrix = new SquareMatrix(uSize);
            iLineMark = new Int32[uSize];
            iColmMark = new Int32[uSize];
            InitResult(uSize);
            iMatchTotal = 0;
            this.uLineSize = uSize;
            this.uColmSize = uSize;    
        }

        public AppointQuestionHungaryAlgorithm(ref SimpleIntMatrix rcMatrix)
        {
            this.cApppointMatrix = new SquareMatrix(ref rcMatrix);
            /*
            if (rcMatrix.UColmSize > rcMatrix.ULineSize)
            {
                Int32 iTmp = 0;
                 for(UInt32 j=0; j<rcMatrix.UColmSize; j++)
                 {
                    this.cApppointMatrix.GetSumOfColm(j, ref iTmp);
                    for (UInt32 i = rcMatrix.ULineSize; i < rcMatrix.UColmSize; i++)
                    {
                        this.cApppointMatrix.SetMatrixValue(i, j, iTmp);
                    }
                 }
            }
            else if (rcMatrix.UColmSize < rcMatrix.ULineSize)
            {
                Int32 iTmp = 0;
                for (UInt32 j = 0; j < rcMatrix.ULineSize; j++)
                {
                    this.cApppointMatrix.GetSumOfLine(j, ref iTmp);
                    for (UInt32 i = rcMatrix.UColmSize; i < rcMatrix.ULineSize; i++)
                    {
                        this.cApppointMatrix.SetMatrixValue(j, i, iTmp);
                    }
                }
            }
            */
            InitResult(cApppointMatrix.USize);
            cMarkMatrix = new SquareMatrix(uSize);
            iLineMark = new Int32[uSize];
            iColmMark = new Int32[uSize];
            iMatchTotal = 0;
            this.uLineSize = rcMatrix.ULineSize;
            this.uColmSize = rcMatrix.UColmSize;
        }

        // 摘要:
        //     输出算法器的所有内容，用于打印算法器当前状态。
        //
        public string AllToString()
        {
            return "Appoint Matrix:\n" + this.cApppointMatrix.MatrixToString() +
                  "Mark Martrix:\n" + this.cMarkMatrix.MatrixToString() +
                  "uSize = [" + this.uSize + "]\n" +
                  "Line Mark:\n" + SimpleIntMatrix.ArrayToString(iLineMark, this.uSize) +
                  "\nColm Mark:\n" + SimpleIntMatrix.ArrayToString(iColmMark, this.uSize) +
                  "\nResult:\n" + SimpleIntMatrix.ArrayToString(iResult, this.uSize) +
                  "\nTotal Matched:" + this.iMatchTotal + "\n";
        }

        // 摘要:
        //     计算指派问题的一个最优解。
        //
        // 参数:
        //     iSolt，输出结果，用于保存最终的解，数组下标表示行，值表示列；
        //     uLen，输出有效长度，即iSolt中前面uLen个元素为有效元素。
        public bool CalcSolution(out Int32[] iSolt, out UInt32 uLen)
        {
            iSolt = null;
            uLen = 0;
            /*1. 优化步骤， 通过行、列中最小值出现的次数来决定先做行转化还是先做列转化*/
            if (CountMinsInLines() < CountMinsInColums())
            {
                GenZeroElemInLines();
                GenZeroElemInColms();
            }
            else {
                GenZeroElemInColms();
                GenZeroElemInLines();
            }
            while (true)
            {
                /*清除中间数据记录*/
                ResetEnvnt();
                //将效率矩阵转化为0、1矩阵，其中已经转化为0的，则标记为1； 其余的则标记为0；               
                MaxMatchHungaryAlgorithm cMaxMatHunAlg;
                this.AppointToMaxMatchMatrix(out cMaxMatHunAlg);
                UInt32 iTmp;
                Int32[] iRst;
                //对转化之后的0、1矩阵使用匈牙利算法求最大匹配
                this.iMatchTotal = cMaxMatHunAlg.GetMaxMatchingSolution(out iRst, out iTmp);
                if ( this.iMatchTotal == this.uSize)
                {//已经达到最大匹配则保存结果后退出
                    InitResult(this.uSize);
                    CopyResult(iRst, iTmp);
                    break;
                }
                InitResult(this.uSize);
                CopyResult(iRst, iTmp);
                // 没有达到最大匹配，则需要调整效率矩阵，增加更多的0元素*/
                if (!AdjustAppointMatrix()) return false;
            }
            AdjustResult();
            SimpleIntMatrix.CopyArray(iResult, out iSolt, this.uLineSize);
            uLen = this.uLineSize;
            return true;
        }

        //内部使用：调整结果，跟输入矩阵的结果保持一致，即如果输入矩阵不是方阵，则将补充的部分置为-1.
        private void AdjustResult()
        {
            for (int i = 0; i < this.uLineSize; i++)
            {
                if (iResult[i] >= this.uColmSize) this.iResult[i] = -1;
            }
        }

        //内部使用：初始化分配结果，默认值为-1，表示无匹配，否则表示对应的匹配值
        private void InitResult(UInt32 uSize)
        {
            if (0 == this.uSize)
            {
                this.uSize = uSize;
                iResult = new Int32[this.uSize];
            }
            for (int i = 0; i < this.uSize; i++)
            {
                iResult[i] = -1;
            }
        }

        //内部使用：按值拷贝结果数组到指定输出
        private void CopyResult(Int32[] iSrc, UInt32 uLen)
        {
            for (Int32 i = 0; i < uLen && i < this.uSize; i++)
            {
                iResult[i] = iSrc[i];
            }
        }

        //内部使用：初始化环境
        private bool ResetEnvnt()
        {
            for (UInt32 i = 0; i < this.uSize; i++)
            {
                this.iLineMark[i] = 0;
                this.iColmMark[i] = 0;
                this.iResult[i] = -1;
                this.cMarkMatrix.SetMatrixLineValue(i, 0);
                this.iMatchTotal = 0;
            }
            return true;
        }

        //内部使用：计算画线结果，用于调整效率矩阵
        private bool AdjustAppointMatrix()
        {
            /*1. 通过konig定理，求最小覆盖点集； 并划去覆盖点，即对应的行列，这里做法是标记cMarkMatrix中对应元素为3*/
            if (CalcMiniCoverPoints() != this.iMatchTotal) return false;
            /*2. 从未被覆盖的点中，寻找最小值*/
            Int32 iTmp = 0;
            if ((iTmp = CalcMinumElemInMarkedMatrix()) <= 0) return false;
            /*3. 调整矩阵，将对应的行减去最小值，而列加上最小值；则保证在原有的0元素不改变的情况下，增加新的0元素*/
            AdjustMatrixByMarkMatrix(iTmp);
            return true;
        }

        //内部使用：执行画线之后的结果，增加更多0元素
        private void AdjustMatrixByMarkMatrix(Int32 iVal)
        {
            for (UInt32 i = 0; i < this.uSize; i++)
            {
                //标注的行减去最小值
                if (1 == this.iLineMark[i])
                {
                    this.cApppointMatrix.AddConstNumInLine(i, (-1)*iVal);
                }
                //标注的列加上最小值
                if (1 == this.iColmMark[i])
                {
                    this.cApppointMatrix.AddConstNumInColm(i, iVal);
                }
            }
        }

        //内部使用：在标记矩阵下计算最小元素
        private Int32 CalcMinumElemInMarkedMatrix()
        {
            Int32 iTmp = 0;
            Int32 iMin = 0;
            bool bFlag = false;
            for (UInt32 i = 0; i < this.uSize; i++ )
            {
                for (UInt32 j = 0; j < this.uSize; j++)
                {
                    if (this.cMarkMatrix.GetMatrixValue(i, j, out iTmp) && iTmp != 3 && this.cApppointMatrix.GetMatrixValue(i, j, out iTmp))
                    {
                        if (!bFlag)
                        {
                            iMin = iTmp;
                            bFlag = true;
                        }
                        else { 
                            iMin = iTmp < iMin ? iTmp : iMin; 
                        }
                    }
                }
            }
            return iMin;
        }

        //内部使用：基于konig定理计算最小覆盖点集
        private UInt32 CalcMiniCoverPoints()
        {
            /*1. 标记已经匹配的对象*/
            for (UInt32 i = 0; i < uSize; i++)
            {
                if (iResult[i] != -1)
                {
                    cMarkMatrix.SetMatrixValue(i, (UInt32)this.iResult[i], 1);
                }
            }
            /*2. 标记未被匹配的对象*/
            Int32 iTmp;
            for (UInt32 i = 0; i < uSize; i++)
            {
                for (UInt32 j = 0; j < uSize; j++)
                {
                    if (this.cApppointMatrix.GetMatrixValue(i, j, out iTmp) && 0 == iTmp && this.cMarkMatrix.GetMatrixValue(i, j, out iTmp) && iTmp != 1)
                    {
                        this.cMarkMatrix.SetMatrixValue(i, j, 2);
                    }
                }
            }
            /*3. mark the spread path points*/
            /*3.1 从点集A(行对象)中，查找未被分配的点，加入集合(即在行数组中标注成1)*/
            for (UInt32 i = 0; i < uSize; i++)
            {
                if (!this.cMarkMatrix.ExistInLine(i, 1))
                {
                    this.iLineMark[i] = 1;
                }
            }
            /*3.2 */
            bool bFlag = true;
            while(bFlag)
            {
                bFlag = false;
                /*3.2.1. 从点集B(列对象)中，找到跟A中标注的点关联且尚未处理过的点，加入集合(即在列数组中标注成1)*/
                for (UInt32 i = 0; i < this.uSize; i++)
                {
                    if (this.iLineMark[i] != 1) continue;
                    for (UInt32 j = 0; j < this.uSize; j++)
                    {
                        if (this.cMarkMatrix.GetMatrixValue(i, j, out iTmp) && 2 == iTmp && this.iColmMark[j] != 1)
                        {
                            this.iColmMark[j] = 1;
                            bFlag = true;
                        }
                    }
                }
                if (!bFlag) break;
                /*3.2.2. 从点集A中查找跟B中标注的点关联的对象，加入到集合*/
                for (UInt32 i = 0; i < this.uSize; i++)
                {
                    if (this.iColmMark[i] != 1) continue;
                    for (UInt32 j = 0; j < this.uSize; j++)
                    {
                        if (this.cMarkMatrix.GetMatrixValue(j, i, out iTmp) && 1 == iTmp && this.iLineMark[j] != 1)
                        {
                            this.iLineMark[j] = 1;
                        }
                    }
                }
                //循环处理，直到没有可以添加的对象为止
            }
            /*4. 从上述处理结果中，找出最小覆盖点集，如果是行中的对象，则将所在的行划去，即该行所有元素在cMarkMatrix中标记为3；同样划去列*/
            UInt32 uSum = 0;
            for(UInt32 i=0; i<this.uSize; i++)
            {
                if (this.iLineMark[i] != 1)
                {
                    this.cMarkMatrix.SetMatrixLineValue(i, 3);
                    uSum++;
                }
                if (this.iColmMark[i] == 1)
                {
                    this.cMarkMatrix.SetMatrixColmValue(i, 3);
                    uSum++;
                }
            }
            return uSum;
        }

        //内部使用：使用最大匹配算法器(寻找增广路径的匈牙利算法)求一个最大匹配
        private void AppointToMaxMatchMatrix(out MaxMatchHungaryAlgorithm cMaxMatHunAlg)
        {
            AdjacencyMatrix stMatrix = new AdjacencyMatrix(this.uSize);
            Int32 iTmp = 0;
            for(UInt32 i=0; i<this.uSize; i++)
            {
                for(UInt32 j=0; j < this.uSize; j++)
                {                  
                    cApppointMatrix.GetMatrixValue(i,j, out iTmp);
                    stMatrix.SetMatrixValue(i, j, 0 == iTmp ? 1: 0);
                }
            }
           cMaxMatHunAlg = new MaxMatchHungaryAlgorithm(ref stMatrix);
        }

        //内部使用：生成行中的0元素
        private void GenZeroElemInLines()
        {
            Int32 iTmp = 0;
            for (UInt32 i = 0; i < this.uSize; i++)
            {
                this.cApppointMatrix.GetMinInLine(i, ref iTmp);
                this.cApppointMatrix.AddConstNumInLine(i, (-1) * iTmp);
            }
        }

        //内部使用：生成列中的0元素
        private void GenZeroElemInColms()
        {
            Int32 iTmp = 0;
            for (UInt32 i = 0; i < this.uSize; i++)
            {
                this.cApppointMatrix.GetMinInColm(i, ref iTmp);
                this.cApppointMatrix.AddConstNumInColm(i, (-1) * iTmp);
            }
        }

        //内部使用： 计算行中最小元素的个数，用于优化算法，可以加快速度。
        private Int32 CountMinsInLines()
        {
            Int32 iSum = 0;
            for (int i = 0; i < this.uSize; i++)
            {
               iSum += this.cApppointMatrix.CountMinsInLine((UInt32)i);
            }
            return iSum;
        }

        //内部使用： 计算列中最小元素的个数，用于优化算法，加快计算速速。
        private Int32 CountMinsInColums()
        {
            Int32 iSum = 0;
            for (int i = 0; i < this.uSize; i++)
            {
                iSum += this.cApppointMatrix.CountMinsInColum((UInt32)i);
            }
            return iSum;
        }

    }
}
