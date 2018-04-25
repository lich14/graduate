using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DBDefine.YardMap;

namespace ZECS.Schedule.VmsAlgoApplication
{
    public class LineInfo
    {
        public UInt16 nID;
        public Byte nDirection;
        public UInt16 nHead;
        public UInt16 nTail;
        public ushort[] aryTp;
    }

    public class TransponderNode
    {
        public UInt16 nID;
        public UInt16 nRow;
        public UInt16 nCol;
        public float nLogicPosX;
        public float nLogicPosY;
        public float nPhysicalPosX;
        public float nPhysicalPosY;
        public Byte AreaType;
        public Byte AreaID;
        public Byte AreaLaneID;
        public bool Enabled;
    }

    public class NodeProperty
    {
        public UInt16 NodeID;
        public Byte PropertyValue;
    }

    public class PositionInfo
    {
        public UInt16 FromTransponderID;
        public UInt16 ToTransponderID;
        public Byte FromDirection;
        public Byte ToDirection;
    }

    public class LaneItem
    {
        public UInt16 nID;
        public Byte nDirection;
    }

    public class LaneArray
    {
        public UInt32 nCount;
        public UInt32 nScore;
        public LaneItem[] aryLaneItem;
    }

    public class LaneArrays
    {
        public UInt32 nCount;
        public LaneArray[] aryLane;
    }

    public class LaneTolaneTime
    {
        public LaneToLaneType type;
        public int fromid;
        public int toid;
        public int expectTime;
    }

    /*2.ENUMS**********************************************************************/
    /*2.1 DIRECITONS*/
    public enum direction_e
    {
        NA_DIREC = 0,
        EAST,
        SOUTH,
        WEST,
        NORTH
    }

    /*2.2 QC WORK CYCLE DIRECTION*/
    /*direction enum for into & out QC */
    public enum qc_io_e
    {
        IOQC_INVALID,
        IOQC_BOTH_CLOCKWISE,        /* into and out QC are both ClockWise*/
        IOQC_BOTH_ANTI_CLOCKWISE,   /* into and out QC are both AntiClockWise*/
        IOQC_IN_CW_OUT_ACW,         /*into QC is ClockWise, out QC is AntiClockWise*/
        IOQC_IN_ACW_OUT_CW          /*into QC is AntiClockWise, out QC is ClockWise*/
    }

    public enum lane2lane_type_e
    {
        INVALID_TYPE,
        LANE_2_LANE = 1,
        QC_2_QCPB,
        QCPB_2_QC,
        QCTP_2_QCTP,
    }

    /*3.STRUCTURES*****************************************************************/
    /*3.1 BASIC DATA STRUCTURES */
    public class set_t
    {
        public UInt32 max;
        public UInt32 count;
        public UInt32 size;
        public IntPtr cells;
    }

    /*3.2 MAP ELEMENTS*/
    public class node_t
    {
        public UInt16 id;
        public UInt16 linex;
        public UInt16 liney;
        public float logx;
        public float logy;
        public float phyx;
        public float phyy;
        public sbyte area_type;
        public sbyte area_id;
        public sbyte area_lane_id;
        public bool availability;
    }

    public class line_t
    {
        public node_t[] nodes;
        public UInt16 id;
        public UInt16 head;
        public UInt16 tail;
        public sbyte direction;
    }

    public class ctrl_attr_t
    {
        UInt32 id;
        sbyte type;
    }

    public class point_t
    {
        public Int32 X;   /* uint cm */
        public Int32 Y;
    }

    public class rectangle_t
    {
        public point_t pt_min;
        public point_t pt_max;
    }

    public class waypoint_t
    {
        public UInt16 nodeid;
        public sbyte direction;
    }

    /*3.3 ROUTES*/
    public class rtitem_t
    {
        public UInt16 nodeid;
        public UInt16 lineid;
        public sbyte type;
        public sbyte direc;
    }

    public class route_t
    {
        public rtitem_t rti;
        public point_t[] keyp;
        public UInt32 count;
        public UInt32 keyp_count;
        public sbyte new_route_flag;
    }

    /*3.4 AGVS*/
    public class agv_t
    {
        public UInt16 id;
        public Int16 heading;             /* [-180, 180] unit:degree*/
        public Int32 speed;               /* unit:0.1m/s */
        public sbyte motion_status;
        public UInt16 lineid;
        public UInt16 nodeid;
        public route_t rt;
        public UInt16 last_idx;
        public UInt16 target_idx;
        public UInt32 x;                   /* unit: cm */
        public UInt32 y;                   /* unit: cm */
        public bool enable_state;
    }

    public class task_t
    {
        public UInt16 start_id;
        public UInt16 end_id;
        public Byte start_direc;
        public Byte end_direc;
    }

    /*3.5 LANES*/
    public class lane_t
    {
        public UInt16 id;
        public sbyte type;
        public sbyte attr;
        public sbyte status;
        public UInt16 lineid;
        public UInt16 start;
        public UInt16 end;
        public UInt16 agvid;
        public UInt16 eqpid;    /* for LT_QC_WORKLANE is QC ID, for LT_QC_BUFFER is allocation QC id, for Block exchange/buffer is Block ID*/
        public sbyte buf_type; /* when type is LT_QC_BUFFER, indicate allocation status QCBUF_IN or QCBUF_OUT */
    }

    public class coords_t
    {
        public double X;   /* unit:m */
        public double Y;   /* unit:m */
    }

    public class pairitem_t
    {
        public UInt16 laneid;
        public UInt16 direc; //AGV heading direction: EAST, WEST, SOUTH, NORTH
    }

    public class routeset_t
    {
        public UInt32 pi_score; //score for the @pi list
        public UInt32 pi_count; //number of used @pi
        public UInt32 pi_cap; //all number of @pi
        public pairitem_t pi;
    }

    public class routesets_t
    {
        public UInt32 rs_count; //number of used @rs
        public UInt32 rs_cap; //all number of @rs
        public routeset_t rs;
    }

    /*when l2l_type is QC_2_QCPB or QCPB_2_QC,the fromid or toid use Qcid.*/
    public class lane2lane_time_t
    {
        public lane2lane_type_e l2l_type;
        public int fromid;
        public int toid;
        public int expect_time;
    }

    public class qcpb_inout_info_t
    {
        public UInt16 laneid;
        public UInt16 qcid;
        public int type; // QCBUF_IN, QCBUF_OUT
    }

}
