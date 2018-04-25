using System.Collections.Generic;
using System.Windows.Media;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.DBDefine.Schedule;
using System.Reflection;

namespace SSWPF.Define
{
    public class SimDataStore
    {
        #region 基本数据库表

        public TerminalRegion oTerminalRegion;
        public Dictionary<uint, AGV> dAGVs;
        public Dictionary<uint, AGVLine> dAGVLines;
        public Dictionary<uint, AGVType> dAGVTypes;
        public Dictionary<uint, ASC> dASCs;
        public Dictionary<uint, ASCType> dASCTypes;
        public Dictionary<string, ASCWorkPoint> dASCWorkPoints;
        public Dictionary<uint, BlockDiv> dBlockDivs;
        public Dictionary<uint, BlockDivsType> dBlockDivsTypes;
        public Dictionary<string, Block> dBlocks;
        public Dictionary<string, Color> dCarryColors;
        public Dictionary<StatusEnums.IndexType, long> dIndexNums;
        public Dictionary<string, ISORef> dISORefs;
        public Dictionary<uint, Lane> dLanes;
        public Dictionary<string, Color> dLaneAttrColors;
        public Dictionary<string, Color> dLaneStatusColors;
        public Dictionary<uint, Mate> dMates;
        public Dictionary<string, Color> dMoveKindColors;
        public Dictionary<string, Pile> dPiles;
        public Dictionary<StatusEnums.ContSize, PileType> dPileTypes;
        public Dictionary<string, PlanGroup> dPlanGroups;
        public Dictionary<string, PlanPlac> dPlanPlacs;
        public Dictionary<string, PlanRange> dPlanRanges;
        public Dictionary<string, SimContainerInfo> dSimContainerInfos;
        public Dictionary<uint, QCDT> dQCs;
        public Dictionary<uint, QCActionTimeStat> dQCActionStats;
        public Dictionary<uint, QCTrolleyTimeStat> dQCTrolleyTimeStats;
        public Dictionary<uint, QCType> dQCTypes;
        public Dictionary<string, QCWorkPoint> dQCWorkPoints;
        public Dictionary<string, Color> dTierColors;
        public Dictionary<uint, SimTransponder> dTransponders;
        public Dictionary<string, Color> dStepTravelStatusColors;
        public Dictionary<uint, Vessel> dVessels;
        public Dictionary<uint, VesselType> dVesselTypes;
        public Dictionary<uint, Voyage> dVoyages;
        public Dictionary<string, WORK_INSTRUCTION_STATUS> dWorkInstructions;
        public Dictionary<string, STS_WORK_QUEUE_STATUS> dWorkQueues;
        public Dictionary<string, YardSlot> dYardSlots;
        public Dictionary<int, int> dfindlineforagv;

        #endregion

        #region ECS 和 Schedule 接口字典

        public Dictionary<string, STS_Order> dSTSOrders;
        public Dictionary<string, STS_Command> dSTSCommands;
        public Dictionary<string, STS_STATUS> dSTSStatus;

        public Dictionary<string, AGV_Order> dAGVOrders;
        public Dictionary<string, AGV_Command> dAGVCommands;
        public Dictionary<string, AGV_STATUS> dAGVStatus;

        public Dictionary<string, ASC_Order> dASCOrders;
        public Dictionary<string, ASC_Command> dASCCommands;
        public Dictionary<string, ASC_STATUS> dASCStatus;

        // ExpectTime 分动静两部分，以 .l2LType 区分。
        // 这里是静态部分，作为参数输入。
        public List<SimExpectTime> lVMSExpectTimes = new List<SimExpectTime>();

        // 接口代表视图，与表不同
        public Dictionary<long, STS_ResJob> dSTSResJobs;
        public Dictionary<long, STS_Task> dSTSTasks;
        public Dictionary<long, ASC_ResJob> dASCResJobs;
        public Dictionary<long, ASC_Task> dASCTasks;
        public Dictionary<long, AGV_ResJob> dAGVResJobs;
        public Dictionary<long, AGV_Task> dAGVTasks;
        public Dictionary<string, BERTH_STATUS> dViewBerthStatus;
        public Dictionary<string, STS_WORK_QUEUE_STATUS> dViewWorkQueues;
        public Dictionary<string, WORK_INSTRUCTION_STATUS> dViewWorkInstructions;

        // 排序前的箱号列表、偏序表，以及排序后的箱号列表
        public List<string> lWIContIDs;
        public int[,] mOrderTable;
        public List<OrderCheck> lSortedOrderChecks;

        #endregion

        #region 作业数据。本部分对应ECS，数据操作由Manager进行

        // 岸桥作业状态记录，维护者为 QCManager
        public Dictionary<uint, List<QCContStageRec>> dQCContStageRecs;
        // 作业线计划、岸桥位置计划和岸桥间移动时间（ExpectTime的动态部分），维护者为 HandleLineManager
        public Dictionary<string, HandleLinePlan> dHandleLinePlans;
        public Dictionary<uint, QCPosPlan> dQCPosPlans;
        public List<SimExpectTime> lQCExpectTimes;

        #endregion

        public SimDataStore()
        {
            #region 初始化数据库表

            this.oTerminalRegion = new TerminalRegion();
            this.dASCTypes = new Dictionary<uint, ASCType>();
            this.dASCs = new Dictionary<uint, ASC>();
            this.dASCWorkPoints = new Dictionary<string, ASCWorkPoint>();
            this.dAGVs = new Dictionary<uint, AGV>();
            this.dAGVTypes = new Dictionary<uint, AGVType>();
            this.dAGVLines = new Dictionary<uint, AGVLine>();
            this.dBlocks = new Dictionary<string, Block>();
            this.dBlockDivs = new Dictionary<uint, BlockDiv>();
            this.dBlockDivsTypes = new Dictionary<uint, BlockDivsType>();
            this.dCarryColors = new Dictionary<string, Color>();
            this.dIndexNums = new Dictionary<StatusEnums.IndexType, long>();
            this.dISORefs = new Dictionary<string, ISORef>();
            this.dLanes = new Dictionary<uint, Lane>();
            this.dLaneStatusColors = new Dictionary<string, Color>();
            this.dLaneAttrColors = new Dictionary<string, Color>();
            this.dMates = new Dictionary<uint, Mate>();
            this.dMoveKindColors = new Dictionary<string, Color>();
            this.dPiles = new Dictionary<string, Pile>();
            this.dPileTypes = new Dictionary<StatusEnums.ContSize, PileType>();
            this.dPlanGroups = new Dictionary<string, PlanGroup>();
            this.dPlanPlacs = new Dictionary<string, PlanPlac>();
            this.dPlanRanges = new Dictionary<string, PlanRange>();
            this.dQCs = new Dictionary<uint, QCDT>();
            this.dQCActionStats = new Dictionary<uint, QCActionTimeStat>();
            this.dQCTrolleyTimeStats = new Dictionary<uint, QCTrolleyTimeStat>();
            this.dQCTypes = new Dictionary<uint, QCType>();
            this.dQCWorkPoints = new Dictionary<string, QCWorkPoint>();
            this.dSimContainerInfos = new Dictionary<string, SimContainerInfo>();
            this.dTierColors = new Dictionary<string, Color>();
            this.dTransponders = new Dictionary<uint, SimTransponder>();
            this.dStepTravelStatusColors = new Dictionary<string, Color>();
            this.dVessels = new Dictionary<uint, Vessel>();
            this.dVesselTypes = new Dictionary<uint, VesselType>();
            this.dVoyages = new Dictionary<uint, Voyage>();
            this.dWorkInstructions = new Dictionary<string, WORK_INSTRUCTION_STATUS>();
            this.dWorkQueues = new Dictionary<string, STS_WORK_QUEUE_STATUS>();
            this.dYardSlots = new Dictionary<string, YardSlot>();

            #endregion

            #region 初始化 ECS 和 Schedule 接口字典

            this.dSTSOrders = new Dictionary<string, STS_Order>();
            this.dSTSCommands = new Dictionary<string, STS_Command>();
            this.dSTSStatus = new Dictionary<string, STS_STATUS>();

            this.dAGVOrders = new Dictionary<string, AGV_Order>();
            this.dAGVCommands = new Dictionary<string, AGV_Command>();
            this.dAGVStatus = new Dictionary<string, AGV_STATUS>();

            this.dASCOrders = new Dictionary<string, ASC_Order>();
            this.dASCCommands = new Dictionary<string, ASC_Command>();
            this.dASCStatus = new Dictionary<string, ASC_STATUS>();

            // ExpectTime 分动静两部分，以 .l2LType 区分。静态部分作为参数输入，动态部分由 SimYardManager 更新
            this.lQCExpectTimes = new List<SimExpectTime>();
            this.lVMSExpectTimes = new List<SimExpectTime>();

            // 接口代表视图，与表不同
            this.dSTSResJobs = new Dictionary<long, STS_ResJob>();
            this.dSTSTasks = new Dictionary<long, STS_Task>();
            this.dASCResJobs = new Dictionary<long, ASC_ResJob>();
            this.dASCTasks = new Dictionary<long, ASC_Task>();
            this.dAGVResJobs = new Dictionary<long, AGV_ResJob>();
            this.dAGVTasks = new Dictionary<long, AGV_Task>();
            this.dViewBerthStatus = new Dictionary<string, BERTH_STATUS>();
            this.dViewWorkQueues = new Dictionary<string, STS_WORK_QUEUE_STATUS>();
            this.dViewWorkInstructions = new Dictionary<string, WORK_INSTRUCTION_STATUS>();

            #endregion

            this.lWIContIDs = new List<string>();
            this.lSortedOrderChecks = new List<OrderCheck>();
            this.mOrderTable = new int[0, 0];
        }

        public void Reset()
        {
            #region 数据库表

            this.oTerminalRegion = null;
            
            this.dAGVs.Clear();
            this.dAGVTypes.Clear();
            this.dAGVLines.Clear();
            this.dASCs.Clear();
            this.dASCTypes.Clear();
            this.dASCWorkPoints.Clear();
            this.dBlocks.Clear();
            this.dBlockDivs.Clear();
            this.dBlockDivsTypes.Clear();
            this.dCarryColors.Clear();
            this.dIndexNums.Clear();
            this.dISORefs.Clear();
            this.dLanes.Clear();
            this.dLaneStatusColors.Clear();
            this.dLaneAttrColors.Clear();
            this.dMates.Clear();
            this.dMoveKindColors.Clear();
            this.dPiles.Clear();
            this.dPileTypes.Clear();
            this.dPlanGroups.Clear();
            this.dPlanPlacs.Clear();
            this.dPlanRanges.Clear();
            this.dQCs.Clear();
            this.dQCActionStats.Clear();
            this.dQCTrolleyTimeStats.Clear();
            this.dQCTypes.Clear();
            this.dQCWorkPoints.Clear();
            this.dSimContainerInfos.Clear();
            this.dTierColors.Clear();
            this.dTransponders.Clear();
            this.dStepTravelStatusColors.Clear();
            this.dVessels.Clear();
            this.dVesselTypes.Clear();
            this.dVoyages.Clear();
            this.dYardSlots.Clear();
            this.dWorkInstructions.Clear();
            this.dWorkQueues.Clear();

            #endregion

            #region ECS 和 Schedule 接口字典

            this.oTerminalRegion = null;

            this.dSTSOrders.Clear();
            this.dSTSCommands.Clear();
            this.dSTSStatus.Clear();

            this.dAGVOrders.Clear();
            this.dAGVCommands.Clear();
            this.dAGVStatus.Clear();

            this.dASCOrders.Clear();
            this.dASCCommands.Clear();
            this.dASCStatus.Clear();

            // ExpectTime 分动静两部分，以 .l2LType 区分。静态部分作为参数输入，动态部分由 SimYardManager 更新
            this.lQCExpectTimes.Clear();
            this.lVMSExpectTimes.Clear();

            // 接口代表视图，与表不同
            this.dSTSResJobs.Clear();
            this.dSTSTasks.Clear();
            this.dASCResJobs.Clear();
            this.dASCTasks.Clear();
            this.dAGVResJobs.Clear();
            this.dAGVTasks.Clear();
            this.dViewBerthStatus.Clear();
            this.dViewWorkQueues.Clear();
            this.dViewWorkInstructions.Clear();

            #endregion

            this.lWIContIDs.Clear();
            this.lSortedOrderChecks.Clear();
            this.mOrderTable = new int[0, 0];
        }
    }
}