using System;
using ZECS.Schedule.DBDefine.Schedule;

namespace ZECS.Schedule.ECSSchedule
{
    public class Sts
    {
        public STS_STATUS Status { get; set; }

        /// <summary>
        /// 作业路权重，权重大的STS，应分配更多AGV。
        /// 权重为1.0，则STS之间平均分配。
        /// </summary>
        public double WeightOfSts { get; set; }

        /// <summary>
        /// 装船相对于卸船，相同的任务数，应分配的AGV数量比值。一般而言，装船需要更多的AGV。
        /// 权重为1.0，则装船和卸船之间平均分配。
        /// </summary>
        public readonly double WeightOfLoad;

        public readonly int AgvCountMin;
        public readonly int AgvCountMax;

        private int _agvCountPlanned;
        /// <summary>
        /// STS应分配的AGV总数量
        /// </summary>
        public int AgvCountPlanned
        {
            get {return _agvCountPlanned;}
            set
            {
                _agvCountPlanned = Math.Min(Math.Max(value, AgvCountMin), AgvCountMax);
            }
        }
        public int AgvCountOccupied { get { return AgvCountOccupiedLoad + AgvCountOccupiedDisc; } }
        public int AgvCountOccupiedLoad { get; set; }
        public int AgvCountOccupiedDisc { get; set; }
        public int AgvCountNeedForLoad { get; set; }
        public int AgvCountNeedForDisc { get; set; }

        public int AgvCountRemainForAssign { get { return Math.Max(AgvCountPlanned - AgvCountOccupied, 0); } }

        public Sts(STS_STATUS status)
        {
            Status = status;

            WeightOfSts = 1.0;
            WeightOfLoad = 1.0;

            AgvCountMin = (int)status.nAGVCountMin;
            AgvCountMax = (int)status.nAGVCountMax;

            AgvCountPlanned = AgvCountMax;
        }

        public double AgvCountWeight()
        {
            return ((AgvCountNeedForLoad + AgvCountOccupiedLoad) * WeightOfLoad + AgvCountNeedForDisc + AgvCountOccupiedDisc) * WeightOfSts;
        }

        public void CalcAgvCountPlanned(int nTotalAgvCount, double dTotalWeight)
        {
            AgvCountPlanned = (int) Math.Round(nTotalAgvCount * AgvCountWeight() / dTotalWeight);
        }

        public override string ToString()
        {
            return Status + string.Format(", AgvCountRemainForAssign={0}, WeightOfSts={1}, WeightOfLoad={2}, AgvCountPlanned={3}, AgvCountOccupied=L{4} D{5}, AgvCountNeed=L{6} D{7}",
                AgvCountRemainForAssign, WeightOfSts, WeightOfLoad, AgvCountPlanned, AgvCountOccupiedLoad, AgvCountOccupiedDisc, AgvCountNeedForLoad, AgvCountNeedForDisc);
        }
    }
}