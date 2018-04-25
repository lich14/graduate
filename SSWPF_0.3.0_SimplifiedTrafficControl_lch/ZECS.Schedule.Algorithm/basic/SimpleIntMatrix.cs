using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.Algorithm
{
    public class SimpleIntMatrix
    {
        private Int32[,] iMatrix;
        private UInt32 uColmSize;
        private UInt32 uLineSize;

        public SimpleIntMatrix()
        {
            this.iMatrix = new Int32[0,0];
            this.uColmSize = 0;
            this.uLineSize = 0;
        }

        public SimpleIntMatrix(UInt32 uLineSize, UInt32 uColmSize)
        {
            this.uLineSize = uLineSize;
            this.uColmSize = uColmSize;
            this.iMatrix = new Int32[uLineSize, uColmSize];
        }

        public SimpleIntMatrix(Int32[,] iMatrix, UInt32 uLineSize, UInt32 uColmSize)
        {
            this.uColmSize = uColmSize;
            this.uLineSize = uLineSize;
            this.iMatrix = new Int32[this.uLineSize, this.uColmSize];
            for (int i = 0; i < uLineSize; i++)
            {
                for (int j = 0; j < uColmSize; j++)
                {
                    this.iMatrix[i, j] = iMatrix[i, j];
                }
            }
        }

        public Int32[,] IMatrix
        {
            get
            {
                return this.iMatrix;
            }
        }

        public UInt32 UColmSize
        {
            get
            {
                return uColmSize;
            }
        }

        public UInt32 ULineSize
        {
            get
            {
                return uLineSize;
            }
        }

        public bool CopyMatrix(out SimpleIntMatrix cSim)
        {
            if (iMatrix == null)
            {
                cSim = null;
                return false;
            }
            cSim = new SimpleIntMatrix(this.iMatrix, this.uLineSize, this.uColmSize);
            return true;
        }

        public void InitMatrix(Int32 iVal)
        {
            for (int i = 0; i < this.uLineSize; i++)
            {
                for (int j = 0; j < this.uColmSize; j++)
                {
                    this.iMatrix[i, j] = iVal;
                }
            }
        }

        public void SetPivot(Int32 iVal)
        {
            for (int i = 0; i < this.uLineSize && i < this.uColmSize; i++)
            {
                this.iMatrix[i, i] = iVal;
            }
        }

        public bool SetMatrixValue(UInt32 uLineIndx, UInt32 uColmIndx, Int32 iVal)
        {
            if (uLineIndx >= this.uLineSize || uColmIndx >= this.uColmSize) return false;
            iMatrix[uLineIndx, uColmIndx] = iVal;
            return true;
        }

        public bool SetMatrixLineValue(UInt32 uLineIndx, Int32 iVal)
        {
            if (uLineIndx >= this.uLineSize) return false;
            for (UInt32 i = 0; i < this.uColmSize; i++)
            {
                this.iMatrix[uLineIndx, i] = iVal;
            }
            return true;
        }

        public bool SetMatrixColmValue(UInt32 uColmIndx, Int32 iVal)
        {
            if (uColmIndx >= this.uColmSize) return false;
            for (UInt32 i = 0; i < this.uLineSize; i++)
            {
                this.iMatrix[i, uColmIndx] = iVal;
            }
            return true;
        }

        public bool GetMatrixValue(UInt32 uLineIndx, UInt32 uColmIndx, out Int32 iVal)
        {
            if (uLineIndx >= this.uLineSize || uColmIndx >= this.uColmSize)
            {
                iVal = 0;
                return false;
            }
            iVal = iMatrix[uLineIndx, uColmIndx];
            return true;
        }

        public bool GetMinInLine(UInt32 uLineIndx, ref Int32 iVal)
        {
            if(uLineIndx >= this.uLineSize) return false;
            iVal = this.iMatrix[uLineIndx, 0];
            for (int i = 0; i < this.uColmSize; i++)
            {
                iVal = (iVal > this.iMatrix[uLineIndx, i] ? this.iMatrix[uLineIndx, i] : iVal);                    
            }
            return true;
        }

        public bool GetMinInColm(UInt32 uColmIndx, ref Int32 iVal)
        {
            if (uColmIndx >= this.uColmSize) return false;
            iVal = this.iMatrix[0, uColmIndx]; ;
            for (int i = 0; i < this.uLineSize; i++)
            {
                iVal = (iVal > this.iMatrix[i, uColmIndx] ? this.iMatrix[i, uColmIndx] : iVal);
            }
            return true;
        }

        public bool AddConstNumInLine(UInt32 uLineIndx, Int32 iVal)
        {
            if (uLineIndx >= this.uLineSize) return false;
            for (int i = 0; i < this.uColmSize; i++)
            {
                this.iMatrix[uLineIndx, i] += iVal;
            }
            return true;
        }

        public bool AddConstNumInColm(UInt32 uColumIndx, Int32 iVal)
        {
            if (uColumIndx >= this.uColmSize) return false;
            for (int i = 0; i < this.uLineSize; i++)
            {
                this.iMatrix[i, uColumIndx] += iVal;
            }
            return true;
        }

        public Int32 CountElemInLine(UInt32 uLineIndx, Int32 iVal)
        {
            if (uLineIndx >= this.uLineSize) return -1;
            Int32 iSum = 0;
            for (int i = 0; i < this.uColmSize; i++)
            {
                if (this.iMatrix[uLineIndx, i] == iVal) iSum++;
            }
            return iSum;
        }

        public Int32 CountElemInColum(UInt32 uColmIndx, Int32 iVal)
        {
            if (uColmIndx >= this.uColmSize) return -1;
            Int32 iSum = 0;
            for (int i = 0; i < this.uLineSize; i++)
            {
                if (this.iMatrix[i, uColmIndx] == iVal) iSum++;
            }
            return iSum;
        }

        public Int32 CountMinsInLine(UInt32 uLineIndx)
        {
            if (uLineIndx >= this.uLineSize) return -1;
            Int32 iMin = this.iMatrix[uLineIndx, 0];
            Int32 iSum = 0;
            for(int i=0; i< this.uColmSize; i++)
            {
                if (this.iMatrix[uLineIndx, i] == iMin) 
                {
                    iSum ++;
                } else if (this.iMatrix[uLineIndx, i] < iMin)
                {
                    iSum = 1;
                    iMin = this.iMatrix[uLineIndx, i];
                } else {
                    continue;
                }
            }
            return iSum;
        }

        public Int32 CountMinsInColum(UInt32 uColumIndx)
        {
            if (uColumIndx >= this.uColmSize) return -1;
            Int32 iMin = this.iMatrix[0, uColumIndx];
            Int32 iSum = 0;
            for(int i=0; i< this.uLineSize; i++)
            {
                if (this.iMatrix[i, uColumIndx] == iMin) 
                {
                    iSum ++;
                } else if (this.iMatrix[i, uColumIndx] < iMin)
                {
                    iSum = 1;
                    iMin = this.iMatrix[i, uColumIndx];
                } else {
                    continue;
                }
            }
            return iSum;
        }

        public UInt32 GetColmSize()
        {
            return this.uColmSize;
        }

        public UInt32 GetLineSize()
        {
            return this.uLineSize;
        }

        public bool GetSumOfColm(UInt32 uColumIdx, ref Int32 iSum)
        {
            if (uColumIdx >= uColmSize) return false;
            iSum = 0;
            for (int i = 0; i < this.uLineSize; i++)
            {
                iSum += iMatrix[i, uColumIdx];
            }
            return true;
        }

        public bool GetSumOfLine(UInt32 uLineIdx, ref Int32 iSum)
        {
            if (uLineIdx >= this.uColmSize) return false;
            iSum = 0;
            for (int i = 0; i < this.uColmSize; i++)
            {
                iSum += iMatrix[uLineIdx, i];
            }
            return true;
        }

        public int GetSumOfMatrix()
        {
            Int32 iSum = 0;
            Int32 iTmp = 0;
            for (UInt32 i = 0; i < this.uLineSize; i++)
            {
                if (GetSumOfLine(i, ref iTmp))
                {
                    iSum += iTmp;
                }
            }
            return iSum;
        }

        public Int32 GetNextLineIndxBySum(UInt32 uLineIdx, Int32 iSum)
        {
            if (uLineIdx > this.uLineSize) return -1;
            Int32 iTmp = 0;
            for (UInt32 i = uLineIdx; i < this.uLineSize; i++)
            {
                if (GetSumOfLine(i, ref iTmp) && iSum == iTmp) return (Int32)i;
            }
            return -1;
        }

        public Int32 GetNextColmIndxBySum(UInt32 uColmIdx, Int32 iSum)
        {
            if (uColmIdx > this.uColmSize) return -1;
            Int32 iTmp = 0;
            for (UInt32 i = uColmIdx; i < this.uColmSize; i++)
            {
                if (GetSumOfColm(i, ref iTmp) && iSum == iTmp) return (Int32)i;
            }
            return -1;
        }

        public Int32 GetNextIndxInLine(UInt32 uLineIdx, UInt32 uColmIdx, Int32 iVal)
        {
            if (uLineIdx >= uLineSize || uColmIdx >= uColmSize) return -1;
            for (UInt32 i = uColmIdx; i < uColmSize; i++)
            {
                if (iMatrix[uLineIdx, i] == iVal) return (Int32)i;
            }
            return -1;
        }

        public Int32 GetNextIndxInColm(UInt32 uLineIdx, UInt32 uColmIdx, Int32 iVal)
        {
            if (uLineIdx > uLineSize || uColmIdx > uColmSize) return -1;
            for (UInt32 i = uLineIdx; i < uLineSize; i++)
            {
                if (iMatrix[i, uColmIdx] == iVal) return (Int32)i;
            }
            return -1;
        }

        public string LineToString(UInt32 uLineIdx)
        {
            string buf = string.Empty;
            for (UInt32 i = 0; i < uColmSize; i++)
            {
                buf += string.Format("{0,-5}", iMatrix[uLineIdx, i]);
            }
            return buf;
        }

        public string MatrixToString()
        {
            string buf = "line_size = " + uLineSize + "; colum_size = " + uColmSize + ";\n";
            for (UInt32 i = 0; i < uLineSize; i++)
            {
                buf += LineToString(i);
                buf += "\n";
            }
            return buf;
        }

        public bool ExistInLine(UInt32 uLineIndx, Int32 iVal)
        {
            if (uLineIndx >= this.uLineSize) return false;
            for (int i = 0; i < this.uColmSize; i++)
            {
                if (iVal == this.iMatrix[uLineIndx, i]) return true;
            }
            return false;
        }

        public bool ExistInColm(UInt32 uColmIndx, Int32 iVal)
        {
            if (uColmIndx >= this.uColmSize) return false;
            for (int i = 0; i < this.uLineSize; i++)
            {
                if (iVal == this.iMatrix[i, uColmIndx]) return true;
            }
            return false;
        }

        public static string ArrayToString(Int32[] iArray, UInt32 uLen)
        {
            string buf = string.Empty;
            for (Int32 i = 0; i < uLen; i++)
            {
                buf += string.Format("{0,-5}",iArray[i]);
            }
            return buf;
        }

        public static string MatrixToString(Int32[,] iMatrix, UInt32 uLineSize, UInt32 uColmSize)
        {
            string buf = string.Empty;
            for (Int32 i = 0; i < iMatrix.GetLength(0) && i < uLineSize; i++)
            {
                for (Int32 j = 0; j < iMatrix.GetLength(1) && j < uColmSize; j++)
                {
                    buf += string.Format("{0,-5}", iMatrix[i, j]);
                }
                buf += "\n";
            }
            return buf;
        }

        public static void CopyArray(Int32[] iSrc, out Int32[] iTar,  UInt32 uLen)
        {
            iTar = new Int32[uLen];

            for (Int32 i = 0; i < uLen; i++)
            {
                iTar[i] = iSrc[i];
            }
        }

        public bool GetNextIndexInMatrix(ref UInt32 uLineIndx, ref UInt32 uColmIndx, Int32 iVal)
        {
            for (UInt32 i= uLineIndx; i < this.uLineSize; i++)
            {
                for (UInt32 j = (i == uLineIndx ? uColmIndx : 0); j < this.uColmSize; j++)
                {
                    if (this.iMatrix[i, j] == iVal)
                    {
                        uLineIndx = i;
                        uColmIndx = j;
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool CompareMatrix(SimpleIntMatrix simA, SimpleIntMatrix simB)
        {
            if (simB == null || simA == null || simA.uLineSize != simB.uLineSize || simA.uColmSize != simB.uColmSize) return false;
            for (int i = 0; i < simA.uLineSize; i++)
            {
                for (int j = 0; j < simA.uColmSize; j++)
                {
                    if (simA.iMatrix[i, j] != simB.iMatrix[i, j]) return false;
                }
            }
            return true;
        }

        public bool MatrixPower(int iN)
        {
            SimpleIntMatrix simTmpResult = this;
            for (int i = iN; i > 1; i--)
            {
                if (!MatrixMultiply(simTmpResult, this, out simTmpResult)) return false;
            }
            return ResetValue(simTmpResult);
        }

        public bool ResetValue(SimpleIntMatrix simInMatrix)
        {
            if (this.uLineSize != simInMatrix.uLineSize || this.uColmSize != simInMatrix.uColmSize) return false;
            for (int i = 0; i < this.uLineSize; i++)
            {
                for (int j = 0; j < this.uColmSize; j++)
                { 
                    this.iMatrix[i,j] = simInMatrix.iMatrix[i,j];
                }
            }
            return true;
        }

        public static bool MatrixMultiply(SimpleIntMatrix simA, SimpleIntMatrix simB, out SimpleIntMatrix simResult)
        {
            simResult = null;
            if (simA.uColmSize != simB.uLineSize) return false;
            simResult = new SimpleIntMatrix(simA.uLineSize, simB.uColmSize);
            int iTmp = 0;
            for (UInt32 i = 0; i < simA.uLineSize; i++)
            {
                for (UInt32 j = 0; j < simB.uColmSize; j++)
                {
                    if (!MatrixLineMultiplyColm(simA, i, simB, j, ref iTmp)) return false;
                    simResult.SetMatrixValue(i, j, iTmp);
                }
            }
            return true;
        }

        public static bool MatrixLineMultiplyColm(SimpleIntMatrix simA , UInt32 iLineIndx, SimpleIntMatrix simB, UInt32 iColmIndx, ref Int32 iVal)
        {
            if (simB.uLineSize != simA.uColmSize) return false;
            iVal = 0;
            for (int i = 0; i < simB.uLineSize; i++)
            {
                iVal += simA.iMatrix[iLineIndx, i] * simB.iMatrix[i, iColmIndx];
            }
            return true;
        }

        public static bool MatrixPlus(SimpleIntMatrix simA, SimpleIntMatrix simB)
        {
            if (simB.uLineSize != simA.uLineSize || simB.uColmSize != simA.uColmSize) return false;
            for (int i = 0; i < simB.uLineSize; i++)
            {
                for (int j = 0; j < simB.uColmSize; j++)
                {
                    simA.iMatrix[i, j] += simB.iMatrix[i, j];
                }
            }
            return true;
        }
    }
}
