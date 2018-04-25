using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// 二部图的权值矩阵类
    /// </summary>
    public class WeightMatrixOfBigraphEntity
    {
        #region [ 属性 ]
        
        /// <summary>
        /// 权值矩阵
        /// </summary>
        public double[,] WeightedMatrix;
        
        /// <summary>
        /// 二部图中Y顶点集顶点数
        /// </summary>
        private UInt32 _uColumnSize;
        
        /// <summary>
        /// 二部图中Y顶点集顶点数
        /// </summary>
        private UInt32 _uRowSize;
        
        #endregion
        
        #region [ 方法 ]

        #region [ 构造函数 ]

        public WeightMatrixOfBigraphEntity(double[,] iMatrix, UInt32 uColmSize, UInt32 uLineSize)
        {
            this.WeightedMatrix = iMatrix;
            this._uColumnSize = uColmSize;
            this._uRowSize = uLineSize;
        }

        public WeightMatrixOfBigraphEntity(ref WeightMatrixOfBigraphEntity stMatrix)
        {
            this._uColumnSize = stMatrix._uColumnSize;
            this._uRowSize = stMatrix._uRowSize;
            this.WeightedMatrix = new double[_uRowSize, _uColumnSize];
            for (UInt32 i = 0; i < _uRowSize; i++)
                for (UInt32 j = 0; j < _uColumnSize; j++)
                    stMatrix.GetMatrixElementByIndex(i, j, out this.WeightedMatrix[i, j]);
        }

        public WeightMatrixOfBigraphEntity(UInt32 uLineSize, UInt32 uColmSize)
        {
            this._uRowSize = uLineSize;
            this._uColumnSize = uColmSize;
            WeightedMatrix = new double[uLineSize, uColmSize];
        }

        public WeightMatrixOfBigraphEntity(UInt32 uMatrSize)
        {
            this._uColumnSize = uMatrSize;
            this._uRowSize = uMatrSize;
            WeightedMatrix = new double[_uRowSize, _uColumnSize];
        }
        
        #endregion

        public bool SetMatrixElement(UInt32 uRowIdx, UInt32 uColumnIdx, double iVal)
        {
            if (uRowIdx > _uRowSize || uColumnIdx > _uColumnSize) return false;
            WeightedMatrix[uRowIdx,uColumnIdx] = iVal;
            return true;
        }

        public double GetMatrixElementByIndex(UInt32 uRowIdx, UInt32 uColumnIdx)
        {
            if (uRowIdx > _uRowSize || uColumnIdx > _uColumnSize) return -1;
            return WeightedMatrix[uRowIdx, uColumnIdx];
        }

        public bool GetMatrixElementByIndex(UInt32 uRowIdx, UInt32 uColumnIdx, out double iVal)
        {
            if (uRowIdx > _uRowSize || uColumnIdx > _uColumnSize)
            {
                iVal = 0;
                return false;
            }
            iVal = WeightedMatrix[uRowIdx, uColumnIdx];
            return true;
        }

        public UInt32 GetColumnSize()
        {
            return this._uColumnSize;
        }

        public UInt32 GetRowSize()
        {
            return this._uRowSize;
        }

        public bool GetSumOfColumn(UInt32 uColumnIdx, out double iSum)
        {
            if (uColumnIdx > _uColumnSize)
            {
                iSum = 0;
                return false;
            }
            iSum = 0;
            for (int i = 0; i < _uRowSize; i++)
            {
                iSum += WeightedMatrix[i, uColumnIdx];
            }
            return true;
        }

        /// <summary>
        /// 寻找给定行中取最大值的列Index列表
        /// </summary>
        /// <param name="rowIndex"></param>
        /// <returns></returns>
        public List<UInt32> GetMaxValueColumnIndexList(UInt32 rowIndex)
        {
            double tmpMax = 0;
            List<UInt32> tmpMaxIndexList = new List<UInt32>();

            for (UInt32 i = 0; i < _uColumnSize; i++)
            {
                if (tmpMax > WeightedMatrix[rowIndex,i])
                {
                    tmpMax = WeightedMatrix[rowIndex, i];
                    tmpMaxIndexList = new List<UInt32>();
                    tmpMaxIndexList.Add(i);
                }
                else
                    if (tmpMax > 0 && tmpMax == WeightedMatrix[rowIndex, i])
                    {
                        tmpMaxIndexList.Add(i);
                    }
            }

            return tmpMaxIndexList;    
        }

        public bool GetSumOfRow(UInt32 uRowIdx, out double iSum)
        {
            if (uRowIdx > _uRowSize)
            {
                iSum = 0;
                return false;
            }
            iSum = 0;
            for (int i = 0; i < _uColumnSize; i++)
            {
                iSum += WeightedMatrix[uRowIdx, i];
            }
            return true;       
        }

        public Int32 GetNextInRow(UInt32 uRowIdx, UInt32 uColumnIdx, double iVal)
        {
            if (uRowIdx > _uRowSize || uColumnIdx > _uColumnSize) return -1;
            for (UInt32 i = uColumnIdx; i < _uColumnSize; i++)
            {
                if (WeightedMatrix[uRowIdx, i] == iVal) return (Int32)i;
            }
            return -1;
        }

        public Int32 GetNextInColumn(UInt32 uRowIdx, UInt32 uColumnIdx, double iVal)
        {
            if (uRowIdx > _uRowSize || uColumnIdx > _uColumnSize) return -1;
            for (UInt32 i = uRowIdx; i < _uRowSize; i++)
            {
                if (WeightedMatrix[i, uColumnIdx] == iVal) return (Int32)i;
            }
            return -1;
        }

        public string LineToString(UInt32 uLineIdx)
        { 
            string buf = string.Empty;
            for (UInt32 i = 0; i < _uColumnSize; i++)
            {
                buf += WeightedMatrix[uLineIdx, i] + " ";
            }
            return buf;
        }

        public string MatrixToString()
        {
            string buf = "line_size = " + _uRowSize + "; colum_size = " + _uColumnSize + ";\n";
            for (UInt32 i = 0; i < _uRowSize; i++)
            {
                buf += LineToString(i);
                buf += "\n";
            }
            return buf;
        }

        public double GetMaximalValue()
        {
            double resultValue = 0;
            for (int i = 0; i < this._uRowSize; i++)
            {
                for (int j = 0; j < this._uColumnSize; j++)
                {
                    if (this.WeightedMatrix[i, j] > resultValue)
                        resultValue = this.WeightedMatrix[i, j];
                }
            }
            return resultValue;
        }
        #endregion
    }
 }

