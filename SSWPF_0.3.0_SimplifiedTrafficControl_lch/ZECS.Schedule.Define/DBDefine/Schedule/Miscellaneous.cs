using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.DBDefine.Schedule
{
    public enum BayType
    {
        UnKnown, //未知

        STS,    // 桥吊
        STS_PB, // 岸桥PB车道
        AGV,    // 从AGV上开始

        QC,     // 桥吊
        WS,     // AGV交换区, 海测,WaterSide
        PB,     // PB车道

        LS, //陆侧,LandSide

        Block, // 堆区
        Spreader, // 吊具
        Mate
    }

    [Flags]
    public enum AscTaskSide
    {
        UnKnown = 0x00,
        WaterSide = 0x01,
        LandSide = 0x02,
        Sblock = 0x04,
    }

    /// <summary>
    /// 0：不允许更新，1：允许更新，2：未知
    /// </summary>
    public enum TosUpdateResult : int
    {
        NotPermit = 0,
        Permit = 1,
        UnKnown = 2
    }

    public enum AGV_Heading
    {
        Unknown = 0,
        HighBollard,    // 高桩，GUI地图右侧
        LowBollard,     // 低桩
        LandSide,       // 陆侧
        WaterSide,      // 海侧
    }

    public enum CTN_DoorDirection
    {
        Unknown,

        /// <summary>
        /// 场箱门方向，WSTP AGV上箱门方向
        /// </summary>
        WaterSide,      // 海侧
        LandSide,       // 陆侧

        /// <summary>
        /// AGV上箱门方向
        /// </summary>
        Head,           // 箱门在车头
        Rear,           // 箱门在车尾

        /// <summary>
        /// 平台和船上箱门方向
        /// </summary>
        HighBollard,    // 高桩，GUI地图右侧
        LowBollard,     // 低桩
    }

    public enum WSTP_Bay
    {
        Unknown = 0,
        LandSide = 1,
        Middle = 2,
        WaterSide = 3,
    }

    public enum WSTP_Lane
    {
        Unknown = 0,
        A = 1,
        B = 2,
        C = 3,
        Mate = 3,
    }

    public enum LSTP_Bay
    {
        Unknown = 0,
        LandSide = 1,
        Middle = 2,
        WaterSide = 3,
    }

    public enum LSTP_Lane
    {
        Unknown = 0,
        A = 1,
        B = 2,
        C = 3,
    }

    public enum AGV_Order_Type
    {
        Unknown,
        ReceiveFromWstp,
        ReceiveFromQctp,
        DelieverToWstp,
        DelieverToQctp,
        RepositionToPb,
        DelieverToWstpComplete,
        DelieverToWstpCancelComplete,
        DelieverToQctpComplete,
        DelieverToQctpCancelComplete,
    }

    public enum ASC_Order_Type
    {
        Unknown,
        PickingUpFromWstp,
        PickingUpFromBlockToWs,
        PickingUpFromBlockToLs,
        PickingUpFromLstp,
        PuttingDownToWstp,
        PuttingDownToBlock,
        PuttingDownToLstp,
    }

    public enum Exception_Code : int
    {
        Unknown = 0,    // only used for log

        // CancelCode: 用于返回TOS Cancel任务是的处理代码，
        // 从900000开始定义：
        CancelCode_NoStart              = 900001, // TOS取消任务时，任务未开始执行    
        CancelCode_Start_noContainer    = 900002, // TOS取消任务时，任务已开始执行，但还没有带箱 
        CancelCode_Start_withContainter = 900003, // TOS取消任务时，任务已开始执行，并已经带箱  
        CancelCode_Start_interactive    = 900004, // TOS取消任务时，任务正在交互中，带箱情况不明 
        CancelCode_AlmostComplete       = 900005, // TOS取消任务时，任务几乎已经完成或已经完成  

        //Exception Code（带箱）：此补充的Exception Code主要用于针对设备带箱出错情况。
        //从200000开始补充定义Exception Code:
        ExCode_Container_UnKnown               = 200000, //	任务带箱异常结束时，并且箱子位置未知             
        ExCode_Container_Return2Orign          = 200001, //	任务带箱异常结束时，并且箱子返回到任务起始点     
        ExCode_Container_InSpreader            = 200002, //	任务带箱异常结束时，并且箱子在吊具上             
        ExCode_Container_InPlatform            = 200003, //	任务带箱异常结束时，并且箱子在岸桥平台上         
        ExCode_Container_inMaintenanceBlock    = 200004, //	任务带箱异常结束时，并且箱子在堆场维修区上       
        ExCode_Container_return2AGV            = 200005, //	任务带箱异常结束时，并且箱子在AGV车上            
        ExCode_Container_return2Ship           = 200006, //	任务带箱异常结束时，并且箱子在船上               
        ExCode_Container_return2Truck          = 200007, //	任务带箱异常结束时，并且箱子在集卡上             
        ExCode_Container_return2YardBlock      = 200008, //	任务带箱异常结束时，并且箱子在堆场上             
        ExCode_Container_return2Mate           = 200009, //	任务带箱异常结束时，并且箱子在堆场伴侣上         
        ExCode_Container_return2QCFrame        = 200010, //	任务带箱异常结束时，并且箱子在岸桥大梁中间车道上 
        ExCode_Container_none                  = 200011, //	任务带箱异常结束时，并且AGV不带箱子                  
    }
}
