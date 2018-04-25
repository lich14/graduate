using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.Algorithm
{
    public class SquareMatrix : SimpleIntMatrix
    {
        private UInt32 uSize;
        public UInt32 USize
        {
            get
            {
                return uSize;
            }
        }

        public SquareMatrix()
            : base()
        {
            this.uSize = 0;
        }

        public SquareMatrix(UInt32 uSize) 
            : base(uSize, uSize)
        { 
            this.uSize = uSize;
        }

        public SquareMatrix(Int32[,] iMatrix, UInt32 uSize)
            : base(iMatrix,uSize, uSize)
        {
            this.uSize = uSize;
        }

        public SquareMatrix(ref SimpleIntMatrix rcSim) : base(rcSim.ULineSize > rcSim.UColmSize ? rcSim.ULineSize : rcSim.UColmSize, rcSim.ULineSize > rcSim.UColmSize ? rcSim.ULineSize : rcSim.UColmSize)
        { 
            this.uSize = rcSim.ULineSize > rcSim.UColmSize ? rcSim.ULineSize : rcSim.UColmSize;
            Int32 iTmp;
            for (UInt32 i = 0; i < this.uSize; i++)
            {
                for (UInt32 j = 0; j < this.uSize; j++)
                {
                    if (rcSim.GetMatrixValue(i, j, out iTmp))
                    {
                        this.SetMatrixValue(i, j, iTmp);
                    }
                }
            }
        }

    }
}
