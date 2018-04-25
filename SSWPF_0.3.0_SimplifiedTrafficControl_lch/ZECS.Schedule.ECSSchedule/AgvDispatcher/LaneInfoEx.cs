
using System;
using ZECS.Schedule.DBDefine.YardMap;
using ZECS.Schedule.Define;

namespace ZECS.Schedule.ECSSchedule
{
    [Serializable]
    public class LaneInfoEx
    {
        public ushort ID { get; set; }
        public LANE_TYPE LaneType { get; set; }
        public LaneStatus LaneStatus { get; set; }
        public String AreaLaneId { get; set; }
        public ushort OccupyAgvId { get; set; }
        public string BlockOrQcId { get; set; }
        public STSBufLaneType PbType { get; set; }
        public LaneAttribute Attr { get; set; }

        public LaneInfoEx(LaneInfo li)
        {
            ID = li.ID;
            LaneType = (LANE_TYPE)li.Type;
            LaneStatus = li.Status;
            AreaLaneId = li.AreaLaneID;
            OccupyAgvId = li.OccupyAGVID;

            if (LaneType == LANE_TYPE.LT_BLOCK_EXCHANGE || LaneType == LANE_TYPE.LT_BLOCK_BUFFER)
            {
                BlockOrQcId = string.Format("A0{0}", li.RelateEqpID);
            }
            else
            {
                BlockOrQcId = Convert.ToString(li.RelateEqpID);
            }

            //PbType = li.BufferType;
            Attr = li.Attr;
        }

        public int GetLaneNo()
        {
            int iLaneNo = 0;
            switch (LaneType)
            {
                case LANE_TYPE.LT_QC_BUFFER:
                case LANE_TYPE.LT_BLOCK_BUFFER:
                    iLaneNo = ID;
                    break;
                case LANE_TYPE.LT_QC_WORKLANE:
                case LANE_TYPE.LT_MAINTAIN_LANE:
                    iLaneNo = Convert.ToInt32(AreaLaneId);
                    break;
                case LANE_TYPE.LT_BLOCK_EXCHANGE:
                    switch (AreaLaneId)
                    {
                        case "A":
                            iLaneNo = 1;
                            break;
                        case "B":
                            iLaneNo = 2;
                            break;
                        case "C":
                            iLaneNo = 3;
                            break;
                    }
                    break;
                default:
                    iLaneNo = ID;
                    break;
            }

            return iLaneNo;
        }

        public bool IsMateLane()
        {
            return (string.Compare(AreaLaneId, "C", true) == 0);
        }

        public bool IsChargerLane()
        {
            return (string.Compare(AreaLaneId, "B", true) == 0);
        }

        public override string ToString()
        {
            string str = string.Format("LaneID={0, 3}, OccupyAGVID={1, -3}, BlockOrQC={2, -3}, Status={3, -11}, Type={4, -17}, LaneNo={5}, Attr={6, -20}",
                            ID, OccupyAgvId, BlockOrQcId, LaneStatus, LaneType, AreaLaneId, Attr);

            return str;
        }
    }
}