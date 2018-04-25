using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.DBDefine.YardMap
{    
    /// <summary>
    /// AGV车头朝向
    /// </summary>
    [Serializable]
    public enum CHE_Direction { Unknown = 0, East = 1, South, West, North };

    /// <summary>
    /// 堆场箱门朝向
    /// </summary>
    [Serializable]
    public enum BlockCtnDoorDirection
    {
        East = 1,
        South,
        West,
        North
    }

    public enum LaneStatus
    {
        IDLE = 0,
        NA = 1,
        DISABLED = 2,
        PASSTHROUGH = 3,
        RESERVED = 4,
        OCCUPIED = 5,
    }

    public enum LaneAttribute
    {
        NONE = 0,
        STS_PB_ONLY_IN = 1,
        STS_PB_ONLY_OUT = 2,
        STS_PB_IN_OUT = 3,
        STS_TP_WORK = 11,
        STS_TP_PASS = 12,
        WSTP_CAN_CHARGE = 13,
        WSTP_MATE_AND_CHARGE = 14,
        WSTP_ONLY_MATE = 18
    }

    [Serializable]
    public class LaneInfo
    {
        public ushort ID;
        public ushort LineID;
        public ushort OccupyAGVID;
        public ushort RelateEqpID;
        public ushort StartTransponderID;
        public ushort EndTransponderID;
        public LaneStatus Status;
        public byte Type;
        public byte BufferType;
        public String AreaLaneID;
        public LaneAttribute Attr;
    }

    // LANE TYPE
    [Serializable]
    public enum LANE_TYPE
    {
        LT_QC_WORKLANE = 1,
        LT_QC_BUFFER = 2,
        LT_BLOCK_EXCHANGE = 3,
        LT_BLOCK_BUFFER = 4,
        LT_MAINTAIN_LANE = 5
    };

    [Serializable]
    public enum LaneToLaneType
    {
        INVALID_TYPE = 0,
        LANE_2_LANE = 1,
        QC_2_QCPB = 2,
        QCPB_2_QC = 3,
        QCTP_2_QCTP = 4,
    };

    public enum STSBufLaneType
    {
        QCBUF_IN = 1,
        QCBUF_OUT = 2,
        QCBUF_IN_PASSTHROUGH = 3,
        QCBUF_OUT_PASSTHROUGH = 4,
        QCBUF_SPECIAL_IN = 6,
        QCBUF_SPECIAL_OUT = 7
    }


    [Serializable]
    public class PB
    {
        public ushort laneID; // 车道号
        public ushort cheID; // QC ID
        public STSBufLaneType direction; // 方向： 1-入；2-出,3...
    }

    /// <summary>
    /// 设备类型
    /// </summary>
    [Serializable]
    public enum DeviceType
    {
        QC = 0,             // 桥吊
        ARMG,               // 轨道吊
        Mate,               // AGV交换区伴侣
        BlockWaitingArea,   // 堆垛等待区
        QCWaitingArea,      // QC等待区
        WSParkArea,          // 堆场停车区
        UnKnown             // 未知
    }

    public enum AreaType
    {
        Unknown = 0,
        STS_TP = 1,                       // QC作业车道
        STS_PB = 2,                       // QC等待区车道        
        WS_TP = 3,                        // 海侧交换区作业车道
        WS_PB = 4,                        // 海侧交换区缓冲车道
        MZ_TP = 5,                        // 维修区车道
        WSParkLane = 7                    // 海侧交换区停车车道
    }

    public enum CoordinateDirection
    {
        X_POSITIVE = 1,     // X轴正方向
        Y_NEGATIVE = 2,    // Y轴反方向
        Y_POSITIVE,           // Y轴正方向
        X_NEGATIVE,          // X轴反方向                
    };

    /// <summary>
    /// 堆场磁钉结构
    /// </summary>
    [Serializable]
    public class Transponder
    {
        public DeviceType deviceType;
        public uint ID;                                 // 磁钉编号
        public float PhysicalPosX;                      // 实际X坐标
        public float PhysicalPosY;                      // 实际Y坐标
        public int LogicPosX;                           // 逻辑X坐标
        public int LogicPosY;                           // 逻辑Y坐标
        public uint HorizontalLineID;                   // 水平线号
        public uint VerticalLineID;                     // 垂直线号
        public AreaType AreaType;                       // 所在区域类型
        public int AreaNo;                              // 所在区域编号
        public String LaneNo;                           // 车道编号
        public bool Enabled;                            // 是否可用
        public ushort NoStop;                           // 不可停车点
    }

    /// <summary>
    /// 估算AGV车道间开行时间的数据库字段
    /// </summary>
    [Serializable]
    public class ExpectTimeRow
    {
        public ushort ID; // primary key, not to be used
        public ushort type; // 1: lane->lane; 2: QC->PB; 3: PB->QC; 4: QC->QC;
        public ushort fromID;
        public ushort toID;
        public int expectTime;
    }

    /// <summary>
    /// 坐标的转换、磁钉的排序等功能
    /// </summary>
    public class CvtCoordinate
    {
        public static List<Transponder> SortTransponder(CoordinateDirection direction, List<Transponder> lstTp)
        {
            if (lstTp == null || lstTp.Count <= 0) return null;

            List<Transponder> lstNew = new List<Transponder>();
            lstNew.Add(lstTp[0]);

            for (int i = 1; i < lstTp.Count; i++)
            {
                for (int j = 0; j < lstNew.Count; j++)
                {
                    if (direction == CoordinateDirection.X_POSITIVE)
                    {
                        if (lstTp[i].LogicPosX < lstNew[j].LogicPosX)
                        {
                            lstNew.Insert(j, lstTp[i]);
                            break;
                        }
                        else
                        {
                            if (j + 1 < lstNew.Count)
                            {
                                if (lstTp[i].LogicPosX < lstNew[j + 1].LogicPosX)
                                {
                                    lstNew.Insert(j + 1, lstTp[i]);
                                    break;
                                }
                            }
                            else
                            {
                                lstNew.Insert(j + 1, lstTp[i]);
                                break;
                            }
                        }
                    }
                    else if (direction == CoordinateDirection.X_NEGATIVE)
                    {
                        if (lstTp[i].LogicPosX > lstNew[j].LogicPosX)
                        {
                            lstNew.Insert(j, lstTp[i]);
                            break;
                        }
                    }
                    else if (direction == CoordinateDirection.Y_NEGATIVE)
                    {
                        if (lstTp[i].LogicPosY > lstNew[j].LogicPosY)
                        {
                            lstNew.Insert(j, lstTp[i]);
                            break;
                        }
                        else
                        {
                            if (j + 1 < lstNew.Count)
                            {
                                if (lstTp[i].LogicPosY > lstNew[j + 1].LogicPosY)
                                {
                                    lstNew.Insert(j + 1, lstTp[i]);
                                    break;
                                }
                            }
                            else
                            {
                                lstNew.Insert(j + 1, lstTp[i]);
                                break;
                            }
                        }
                    }
                    else if (direction == CoordinateDirection.Y_POSITIVE)
                    {
                        if (lstTp[i].LogicPosY > lstNew[j].LogicPosY)
                        {
                            lstNew.Insert(j, lstTp[i]);
                            break;
                        }
                    }
                }
            }

            return lstNew;
        }
    }
}
