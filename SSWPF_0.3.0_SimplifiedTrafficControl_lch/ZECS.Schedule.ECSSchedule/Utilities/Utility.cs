using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Common.Define;
using ZECS.Schedule.DB;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Define;
using ZECS.Schedule.Define.DBDefine.Schedule;
using BayType = ZECS.Schedule.DBDefine.Schedule.BayType;
using JobType = ZECS.Schedule.DBDefine.Schedule.JobType;
using TaskStatus = ZECS.Schedule.DBDefine.CiTOS.TaskStatus;

namespace ZECS.Schedule.ECSSchedule
{
    public static class Utility
    {
        #region Helper Functions
        /// <summary>
        /// 从字符串中获取数值数据
        /// </summary>
        /// <param name="str"></param>
        /// <returns>失败：-1， 成功：>=0</returns>
        public static int GetNumberFromString(String str)
        {
            int nRet = -1;

            if (str == null)
                return nRet;

            string number = str.Where(char.IsDigit).Aggregate("", (seqence, next) => seqence + next);
            int.TryParse(number, out nRet);

            return nRet;
        }

        public static string GetAscCheId(string strBlockNo, bool isWaterSide)
        {
            // load config from file
            // temporarily hard code the Block No <-->CHE_ID // todo
            switch (strBlockNo.ToUpper())
            {
                case "A02":
                    return isWaterSide ? "262" : "272";
                case "A03":
                    return isWaterSide ? "263" : "273";
                case "A04":
                    return isWaterSide ? "264" : "274";
                case "A05":
                    return isWaterSide ? "265" : "275";
                case "A06":
                    return isWaterSide ? "266" : "276";
                case "A07":
                    return isWaterSide ? "267" : "277";
                case "A08":
                    return isWaterSide ? "268" : "278";
                case "A09":
                    return isWaterSide ? "269" : "279";
                default:
                    return null;
            }
        }

        public static bool IsSameQcId(string strQcId1, string strQcId2)
        {
            int nQcId1 = GetNumberFromString(strQcId1);
            int nQcId2 = GetNumberFromString(strQcId2);

            return (nQcId1 != -1 && nQcId1 == nQcId2);
        }

        public static bool IsMateLane(string laneId)
        {
            return (laneId == "3" || string.Compare(laneId, "C", true) == 0);
        }

        public static bool IsCommandChanged(Command_Base cmd, Order_Base order, bool bSyncedWithOrder = false)
        {
            if (cmd == null || order == null)
                return false;

            if (Utility.CompareStringAsInt(cmd.COMMAND_VERSION, order.COMMAND_VERSION) <= 0)
                return false;

            if (bSyncedWithOrder)
            {
                if (Utility.CompareStringAsInt(cmd.ORDER_VERSION, order.ORDER_VERSION) != 0)
                    return false;
            }

            return true;
        }

        public static ContainerType ContainerLength2Type(int nContainerLength /*单位:mm*/)
        {
            if (0 < nContainerLength && nContainerLength < 7000)
                return ContainerType.Single20Ft;
            else if (7000 < nContainerLength && nContainerLength < 13000)
                return ContainerType.Center40Ft;
            else if (13000 < nContainerLength && nContainerLength < 14000)
                return ContainerType.Center45Ft;
            else
                return ContainerType.Unknown;
        }

        public static int CompareStringAsInt(string str1, string str2)
        {
            int num1;
            int num2;

            int.TryParse(str1, out num1);
            int.TryParse(str2, out num2);

            return num1 - num2;
        }

        public static string IncrementStringAsInt(string strInt)
        {
            try
            {
                return (int.Parse(strInt) + 1).ToString();
            }
            catch (Exception)
            {
                return "0";
            }
        }

        /// <summary>
        /// Find AGV_Task/ASC_Task/STS_Task by Work Instruction
        /// </summary>
        public static T FindTaskByWi<T>(WORK_INSTRUCTION_STATUS wi, List<T> listTask) where T : class
        {
            dynamic task = listTask.Find(x => ((dynamic)x).Task.JOB_ID == wi.JOB_ID);

            return (T)task;
        }

        /// <summary>
        /// Find Work Instruction by Job
        /// </summary>
        public static WORK_INSTRUCTION_STATUS FindWiByJob<T>(T job, List<WORK_INSTRUCTION_STATUS> listWi) where T : class
        {
            WORK_INSTRUCTION_STATUS wi = listWi.Find(x => x.JOB_ID == ((dynamic)job).JOB_ID);

            return wi;
        }

        public static ResJobStatus JobStatus(this ASC_ResJob o)
        {
            return Helper.GetEnum(o.JOB_STATUS, ResJobStatus.Unknown);
        }

        public static ResJobStatus JobStatus(this STS_ResJob o)
        {
            return Helper.GetEnum(o.JOB_STATUS, ResJobStatus.Unknown);
        }

        public static AscTaskSide TaskSide(this ASC_ResJob o)
        {
            return TaskSide(o.FROM_BAY_TYPE, o.TO_BAY_TYPE);
        }

        public static AscTaskSide TaskSide(this Order_Command_Base o)
        {
            return TaskSide(o.FROM_BAY_TYPE, o.TO_BAY_TYPE);
        }

        private static AscTaskSide TaskSide(string strFromBayType, string strToBayType)
        {
            BayType fromBayType = Helper.GetEnum(strFromBayType, BayType.UnKnown);
            BayType toBayType = Helper.GetEnum(strToBayType, BayType.UnKnown);

            if ((fromBayType == BayType.WS || fromBayType == BayType.AGV || fromBayType == BayType.Mate)
                || (toBayType == BayType.WS || toBayType == BayType.AGV || toBayType == BayType.Mate))
            {
                return AscTaskSide.WaterSide;
            }

            if (fromBayType == BayType.LS || toBayType == BayType.LS)
            {
                return AscTaskSide.LandSide;
            }

            if (fromBayType == BayType.Block && toBayType == BayType.Block)
            {
                return AscTaskSide.Sblock;
            }

            Logger.ECSSchedule.Error("[ASC] ASC Task Side return AscTaskSide.UnKnown! This should not be happen!");

            return AscTaskSide.UnKnown;
        }

        public static bool IsContainerIdEmpty(string CONTAINER_ID)
        {
            if (string.IsNullOrWhiteSpace(CONTAINER_ID))
                return true;

            int iRet;
            if (int.TryParse(CONTAINER_ID, out iRet) && iRet == 0)
                return true;

            return false;
        }

        public static string SafeValue(string lhv, string rhv)
        {
            if (string.IsNullOrWhiteSpace(lhv) || !string.IsNullOrWhiteSpace(rhv))
            {
                return rhv;
            }

            return lhv;
        }

        public static string GetString(dynamic job)
        {
            string str = "";

            if (job is AGV_ResJob || job is ASC_ResJob || job is STS_ResJob)
            {
                str = string.Format("{0}, JOB={1}, LINK={2}, CHE={3}, CTN={4}, STATUS={5}, YARD_ID={6}, FROM=({7}), TO=({8})",
                        job.JOB_TYPE, job.JOB_ID, job.JOB_LINK, job.CHE_ID,
                        job.CONTAINER_ID, job.JOB_STATUS, job.YARD_ID,
                        (job is AGV_ResJob || job is ASC_ResJob)
                        ? string.Format("{0},{1},{2},{3},{4}", job.FROM_BLOCK, job.FROM_BAY_TYPE, job.FROM_BAY, job.FROM_LANE, job.FROM_TIER)
                        : string.Format("{0},{1},{2},{3}", job.FROM_BAY_TYPE, job.FROM_BAY, job.FROM_LANE, job.FROM_TIER),
                        (job is AGV_ResJob || job is ASC_ResJob)
                        ? string.Format("{0},{1},{2},{3}, {4}", job.TO_BLOCK, job.TO_BAY_TYPE, job.TO_BAY, job.TO_LANE, job.TO_TIER)
                        : string.Format("{0},{1},{2},{3}", job.TO_BAY_TYPE, job.TO_BAY, job.TO_LANE, job.TO_TIER)
                        );
            }
            else if (job is STS_ReqUpdateJob || job is AGV_ReqUpdateJob || job is ASC_ReqUpdateJob)
            {
                Exception_Code ec = Helper.GetEnum(job.EXCEPTION_CODE, Exception_Code.Unknown);
                string strEc = ec == Exception_Code.Unknown ? "" : ec.ToString();

                str = string.Format("{0}, ORDER_ID={1}, JOB={2}, LINK={3}, CTN={4}, STATUS={5}, EXCEPTION_CODE={6}, YARD_ID={7}, FROM=({8}), TO=({9})",
                        job.JOB_TYPE, job.ORDER_ID, job.JOB_ID, job.JOB_LINK, job.CONTAINER_ID, job.JOB_STATUS,
                        strEc, job.YARD_ID,
                        (job is AGV_ReqUpdateJob || job is ASC_ReqUpdateJob)
                        ? string.Format("{0},{1},{2},{3},{4}", job.FROM_BLOCK, job.FROM_BAY_TYPE, job.FROM_BAY, job.FROM_LANE, job.FROM_TIER)
                        : string.Format("{0},{1},{2},{3}", job.FROM_BAY_TYPE, job.FROM_BAY, job.FROM_LANE, job.FROM_TIER),
                        (job is AGV_ReqUpdateJob || job is ASC_ReqUpdateJob)
                        ? string.Format("{0},{1},{2},{3},{4}", job.TO_BLOCK, job.TO_BAY_TYPE, job.TO_BAY, job.TO_LANE, job.TO_TIER)
                        : string.Format("{0},{1},{2},{3}", job.TO_BAY_TYPE, job.TO_BAY, job.TO_LANE, job.TO_TIER)
                        );
            }
            else
            {
                str = job.ToString();
            }

            return str;
        }

        public static void Log(string subModuleName, string summary, bool bRet, string info)
        {
            Logger.ECSSchedule.Info(
                string.Format("[{0}] {1, -30} {2, -8}: {3}",
                    subModuleName,
                    summary,
                    (bRet ? "Success" : "Fail"),
                    info));
        }
        public static void Log(string subModuleName, string summary, string title, string info)
        {
            Logger.ECSSchedule.Info(
                string.Format("[{0}] {1, -30} {2, -8}: {3}",
                    subModuleName,
                    summary,
                    title,
                    info));
        }

        #endregion

        #region Order/Command/ResJob
        /// <summary>
        /// 
        /// </summary>
        /// <param name="systemName">BMS，VMS，QCMS</param>
        /// <param name="dbDataSchedule"></param>
        /// <param name="curMaxOrderId"></param>
        /// <returns>Fail: -1, or > 0</returns>
        public static long CreateNewOrderId(string systemName, DBData_Schedule dbDataSchedule, long curMaxOrderId = 0)
        {
            long newOrderId = DB_TOS.Instance.CreateNewOrderID(systemName);
            if (newOrderId > 0)
                return newOrderId;

            long maxOrderId = 0;
            switch (systemName.ToUpper())
            {
                case "BMS":
                    if (dbDataSchedule.m_DBData_BMS.m_listASC_Order.Count > 0)
                        maxOrderId = dbDataSchedule.m_DBData_BMS.m_listASC_Order.Max(x => GetMaxOrderId(x));
                    break;
                case "VMS":
                    if (dbDataSchedule.m_DBData_VMS.m_listAGV_Order.Count > 0)
                        maxOrderId = dbDataSchedule.m_DBData_VMS.m_listAGV_Order.Max(x => GetMaxOrderId(x));
                    break;
                case "QCMS":
                    if (dbDataSchedule.m_DBData_STSMS.m_listSTS_Order.Count > 0)
                        maxOrderId = dbDataSchedule.m_DBData_STSMS.m_listSTS_Order.Max(x => GetMaxOrderId(x));
                    break;
            }

            maxOrderId = Math.Max(maxOrderId, curMaxOrderId);

            newOrderId = Math.Max(newOrderId, maxOrderId + 1);

            return newOrderId;
        }

        private static long GetMaxOrderId(Order_Base order)
        {
            long orderId = 0;
            long orderLinkId = 0;
            long.TryParse(order.ORDER_ID, out orderId);
            long.TryParse(order.GetOrderLink(), out orderLinkId);

            long maxOrderId = Math.Max(orderId, orderLinkId);

            return maxOrderId;
        }

        public static AGV_Order CreateAgvOrder(AGV_ResJob job, DBData_Schedule dbDataSchedule, bool bFillTo = true, bool bFillContainer = true)
        {
            return (AGV_Order)CreateOrderByResJob(job, bFillTo, bFillContainer, dbDataSchedule);
        }

        public static Order_Base CreateOrderByResJob(dynamic resJob, bool bFillTo = true, bool bFillContainer = true, DBData_Schedule dbDataSchedule=null)
        {
            if (resJob == null)
                return null;

            ResJobStatus jobStatus = Helper.GetEnum(resJob.JOB_STATUS, ResJobStatus.Unknown);
            if (jobStatus != ResJobStatus.New && jobStatus != ResJobStatus.Update)
                return null;

            Order_Base order = null;

            // Special fields
            if (resJob is AGV_ResJob)
            {
                var agvOrder = new AGV_Order();

                agvOrder.ORDER_ID = Convert.ToString(Utility.CreateNewOrderId("VMS", dbDataSchedule));

                agvOrder.FROM_LINK = "";
                agvOrder.TO_LINK = "";

                order = agvOrder;
            }
            else if (resJob is ASC_ResJob)
            {
                var ascOrder = new ASC_Order();

                ascOrder.ORDER_ID = Convert.ToString(Utility.CreateNewOrderId("BMS", dbDataSchedule));
                ascOrder.ORDER_LINK = "";

                order = ascOrder;
            }
            else if (resJob is STS_ResJob)
            {
                var stsOrder = new STS_Order();
                //order.ORDER_ID = ;
                //order.ORDER_LINK = ;

                order = stsOrder;
            }
            else
            {
                return null;
            }

            #region //Common fields
            JobType jobType = Helper.GetEnum(resJob.JOB_TYPE, JobType.UNKNOWN);
            order.JOB_TYPE = jobType.ToString();

            order.ORDER_VERSION = "1";  // first order version
            order.COMMAND_ID = ""; //新建Order时Command_Id为空
            order.COMMAND_VERSION = "0"; // command is not created yet

            order.JOB_STATUS = ResJobStatus.New.ToString();
            order.CHE_ID = resJob.CHE_ID;
            order.JOB_ID = resJob.JOB_ID;

            FillOrderFromPosition(order, resJob);

            if (bFillTo)
            {
                FillOrderToPosition(order, resJob);
            }

            if (bFillContainer)
            {
                FillOrderContainer(order, resJob);
            }

            FillOrderMiscellaneous(order, resJob);

            if (order is ASC_Order)
            {
                ASC_Order ascOrder = (ASC_Order) order;

                if (IsFromLaneAssignedByTos(ascOrder)
                    || IsAgvAssignedByTos(ascOrder))
                {
                    ascOrder.ORDER_ASSIGNER = "TOS";
                }
            }

            order.DATETIME = DateTime.Now;
            #endregion

            return order;
        }

        public static bool IsFromLaneAssignedByTos(ASC_Order order)
        {
            return !string.IsNullOrWhiteSpace(order.FROM_LANE)
                    && order.GetJobType() == JobType.DISC;
        }
        public static bool IsAgvAssignedByTos(ASC_Order order)
        {
            return Helper.GetEnum(order.FROM_BAY_TYPE, BayType.UnKnown) == BayType.WS
                   && !string.IsNullOrWhiteSpace(order.FROM_TRUCK_ID);
        }

        public static bool IsAgvAssignedByTos(ASC_ResJob job)
        {
            return Helper.GetEnum(job.FROM_BAY_TYPE, BayType.UnKnown) == BayType.WS
                   && !string.IsNullOrWhiteSpace(job.FROM_TRUCK_ID);
        }

        public static bool IsFinalToAssigned(AGV_Order order)
        {
            BayType toBayType = Helper.GetEnum(order.TO_BAY_TYPE, BayType.UnKnown);

            if (toBayType == BayType.WS
                && !string.IsNullOrWhiteSpace(order.TO_LANE)
                && !string.IsNullOrWhiteSpace(order.TO_BLOCK))
            {
                return true;
            }

            if (toBayType == BayType.QC
                && !string.IsNullOrWhiteSpace(order.TO_BLOCK))
            {
                return true;
            }

            return false;
        }

        public static bool IsTempToAssigned(AGV_Order order)
        {
            BayType toBayType = Helper.GetEnum(order.TO_BAY_TYPE, BayType.UnKnown);

            if (toBayType == BayType.PB
#if ASSIGN_PB_BY_SCHD
                && !string.IsNullOrWhiteSpace(order.TO_LANE)
#endif
                )
            {
                return true;
            }

            return false;
        }

        public static bool IsAnyToAssigned(AGV_Order order)
        {
            BayType toBayType = Helper.GetEnum(order.TO_BAY_TYPE, BayType.UnKnown);

            if (toBayType == BayType.WS
                && !string.IsNullOrWhiteSpace(order.TO_LANE)
                && !string.IsNullOrWhiteSpace(order.TO_BLOCK))
            {
                return true;
            }

            if (toBayType == BayType.QC
                && !string.IsNullOrWhiteSpace(order.TO_BLOCK))
            {
                return true;
            }

            if (toBayType == BayType.PB
#if ASSIGN_PB_BY_SCHD
                && !string.IsNullOrWhiteSpace(order.TO_LANE)
#endif
                )
            {
                return true;
            }

            return false;
        }

        public static bool IsNextWstpLaneAssigned(AGV_Order order, AGV_Order_Type agvOrderType)
        {
            if (agvOrderType == AGV_Order_Type.ReceiveFromWstp
                && !string.IsNullOrWhiteSpace(order.FROM_LANE))
            {
                return true;
            }

            if (agvOrderType == AGV_Order_Type.DelieverToWstp
                && Helper.GetEnum(order.TO_BAY_TYPE, BayType.UnKnown) == BayType.WS // exclude pb
                && !string.IsNullOrWhiteSpace(order.TO_LANE))
            {
                return true;
            }

            return false;
        }

        public static bool IsOneLiftFrom(AGV_Order order, AGV_Order orderLink)
        {
            if (string.IsNullOrWhiteSpace(order.FROM_BLOCK)
                || order.FROM_BLOCK != orderLink.FROM_BLOCK)
            {
                return false;
            }

            JobType jobType = order.GetJobType();

            if (jobType == JobType.DISC
                || ((jobType == JobType.LOAD || jobType == JobType.DBLOCK)
                    && Utility.IsMateLane(order.FROM_LANE))
                )
            {
                return true;
            }

            return false;
        }

        public static bool IsSameFromBlockOrQc(AGV_Order order, AGV_Order orderLink)
        {
            JobType jobType = order.GetJobType();
            BayType fromBayType = BayType.UnKnown;
            if (jobType == JobType.LOAD || jobType == JobType.DBLOCK)
                fromBayType = BayType.WS;
            else if (jobType == JobType.DISC)
                fromBayType = BayType.QC;
            else
                return false;

            return Helper.GetEnum(order.FROM_BAY_TYPE, BayType.UnKnown) == fromBayType
                   && Helper.GetEnum(orderLink.FROM_BAY_TYPE, BayType.UnKnown) == fromBayType
                   && !string.IsNullOrWhiteSpace(order.FROM_BLOCK)
                   && order.FROM_BLOCK == orderLink.FROM_BLOCK;
        }

        public static bool IsSameToBlockOrQc(AGV_Order order, dynamic agvOrderOrJobLink)
        {
            if (order == null || agvOrderOrJobLink == null)
                return false;

            JobType jobType = order.GetJobType();
            JobType rhvJobType = Helper.GetEnum(agvOrderOrJobLink.JOB_TYPE, JobType.UNKNOWN);

            if (jobType != rhvJobType)
                return false;

            if (string.IsNullOrWhiteSpace(order.TO_BLOCK) || order.TO_BLOCK != agvOrderOrJobLink.TO_BLOCK)
                return false;

            BayType lhvToBayType = Helper.GetEnum(order.TO_BAY_TYPE, BayType.UnKnown);
            BayType rhvToBayType = Helper.GetEnum(agvOrderOrJobLink.TO_BAY_TYPE, BayType.UnKnown);

            if (jobType == JobType.LOAD || jobType == JobType.DBLOCK)
            {
                if (lhvToBayType != rhvToBayType)
                    return false;
            }
            else if (jobType == JobType.DISC)
            {
                if (lhvToBayType != BayType.WS && lhvToBayType != BayType.PB)
                    return false;

                if (rhvToBayType != BayType.WS && rhvToBayType != BayType.PB)
                    return false;
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// [同场][装卸]双箱到[一个AGV]时，才填充ASC OrderLink，不管是伴侣车道还是非伴侣车道 
        /// </summary>
        /// <param name="job"></param>
        /// <param name="listAscResJob"></param>
        /// <returns></returns>
        public static bool IsTwinAscOrder(ASC_ResJob job, List<ASC_ResJob> listAscResJob)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.JOB_LINK))
                return false;

            ASC_ResJob jobLink = listAscResJob.Find(x => x.JOB_ID == job.JOB_LINK);

            if (jobLink == null || job.YARD_ID != jobLink.YARD_ID)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// AGV order 为DelieverToWstpComplete时，判断ASC是否已提起该Agv order对应的箱子
        /// </summary>
        /// <param name="dbDataBms"></param>
        /// <param name="agvOrder"></param>
        /// <returns></returns>
        public static bool IsAscDoingCompletedAgvOrder(DBData_BMS dbDataBms, AGV_Order agvOrder)
        {
            ASC_Order ascOrder = dbDataBms.m_listASC_Order
                .Find(x => x.JOB_ID == agvOrder.JOB_ID && x.YARD_ID == agvOrder.TO_BLOCK);

            if (ascOrder == null)
                return true;

            ASC_Command ascCmd = dbDataBms.m_listASC_Command
                .Find(x => x.ORDER_ID == ascOrder.ORDER_ID);

            return (ascCmd == null || ascCmd.IsInFirstHalf());
        }

        public static void FillOrderFromLane(AGV_Order lhv, AGV_Order rhv)
        {
            lhv.FROM_BAY_TYPE = rhv.FROM_BAY_TYPE;
            lhv.FROM_LANE = rhv.FROM_LANE;
        }

        public static void FillOrderToLane(AGV_Order lhv, AGV_Order rhv)
        {
            lhv.TO_BAY_TYPE = rhv.TO_BAY_TYPE;
            lhv.TO_LANE = rhv.TO_LANE;
        }

        public static void FillOrderFromPosition(Order_Base lhv, dynamic rhv, bool bForce = false)
        {
            lhv.FROM_BAY =        rhv.FROM_BAY;
            lhv.FROM_BAY_TYPE =   rhv.FROM_BAY_TYPE;
            lhv.FROM_LANE =       bForce ? rhv.FROM_LANE : SafeValue(lhv.FROM_LANE, rhv.FROM_LANE);
            lhv.FROM_TIER =       rhv.FROM_TIER; 
            lhv.FROM_TRUCK_ID =   rhv.FROM_TRUCK_ID;
            lhv.FROM_TRUCK_POS =  rhv.FROM_TRUCK_POS;
            lhv.FROM_TRUCK_TYPE = rhv.FROM_TRUCK_TYPE;

            if (lhv is AGV_Order)
            {
                AGV_Order lhvAgvOrder = (AGV_Order) lhv;
                lhvAgvOrder.FROM_BLOCK = rhv.FROM_BLOCK;
            }
            else if (lhv is ASC_Order)
            {
                ASC_Order lhvAscOrder = (ASC_Order) lhv;
                if (rhv is ASC_ResJob)
                {
                    ASC_ResJob rhvAscResJob = (ASC_ResJob) rhv;
                    lhvAscOrder.FROM_BLOCK = rhvAscResJob.FROM_BLOCK;
                    lhvAscOrder.FROM_RFID = rhvAscResJob.FROM_RFID;
                    lhvAscOrder.FROM_PALLET_TYPE = rhvAscResJob.FROMPALLETTYPE;
                    //lhvAscOrder.FROM_TRUCK_STATUS = rhvAscResJob.FROM_TRUCK_STATUS;
                }
                else if (rhv is ASC_Order)
                {
                    ASC_Order rhvAscOrder = (ASC_Order) rhv;
                    lhvAscOrder.FROM_BLOCK = rhvAscOrder.FROM_BLOCK;
                    lhvAscOrder.FROM_RFID = rhvAscOrder.FROM_RFID;
                    lhvAscOrder.FROM_PALLET_TYPE = rhvAscOrder.FROM_PALLET_TYPE;
                    lhvAscOrder.FROM_TRUCK_STATUS = rhvAscOrder.FROM_TRUCK_STATUS;
                }
            }
            else if (lhv is STS_Order)
            {
                STS_Order lhvStsOrder = (STS_Order) lhv;
                if (rhv is STS_ResJob)
                {
                    STS_ResJob rhvStsResJob = (STS_ResJob) rhv;
                    lhvStsOrder.FROM_RFID = rhvStsResJob.FROM_RFID;
                }
            }
        }

        public static void FillOrderToPosition(Order_Base lhv, dynamic rhv, bool bForce = false)
        {
            lhv.TO_BAY =        rhv.TO_BAY;
            lhv.TO_BAY_TYPE =   rhv.TO_BAY_TYPE;
            lhv.TO_LANE =       bForce ? rhv.TO_LANE : SafeValue(lhv.TO_LANE, rhv.TO_LANE);
            lhv.TO_TIER =       rhv.TO_TIER; 
            lhv.TO_TRUCK_ID =   rhv.TO_TRUCK_ID;
            lhv.TO_TRUCK_POS =  rhv.TO_TRUCK_POS;
            lhv.TO_TRUCK_TYPE = rhv.TO_TRUCK_TYPE;

            if (lhv is AGV_Order)
            {
                AGV_Order lhvAgvOrder = lhv as AGV_Order;
                lhvAgvOrder.TO_BLOCK = rhv.TO_BLOCK;
            }
            else if (lhv is ASC_Order)
            {
                ASC_Order lhvAscOrder = lhv as ASC_Order;
                if (rhv is ASC_ResJob)
                {
                    ASC_ResJob rhvAscResJob = rhv as ASC_ResJob;
                    lhvAscOrder.TO_BLOCK = rhvAscResJob.TO_BLOCK;
                    lhvAscOrder.TO_RFID = rhvAscResJob.TO_RFID;
                    lhvAscOrder.TO_PALLET_TYPE = rhvAscResJob.TOPALLETTYPE;
                    //lhvAscOrder.TO_TRUCK_STATUS = rhvAscResJob.TO_TRUCK_STATUS;
                }
                else if (rhv is ASC_Order)
                {
                    ASC_Order rhvAscOrder = rhv as ASC_Order;
                    lhvAscOrder.TO_BLOCK = rhvAscOrder.TO_BLOCK;
                    lhvAscOrder.TO_RFID = rhvAscOrder.TO_RFID;
                    lhvAscOrder.TO_PALLET_TYPE = rhvAscOrder.TO_PALLET_TYPE;
                    lhvAscOrder.TO_TRUCK_STATUS = rhvAscOrder.TO_TRUCK_STATUS;
                }
                else if (rhv is ASC_Command)
                {
                    ASC_Command rhvAscCmd = rhv as ASC_Command;
                    lhvAscOrder.TO_BLOCK = rhvAscCmd.TO_BLOCK;
                    lhvAscOrder.TO_RFID = rhvAscCmd.TO_RFID;
                    lhvAscOrder.TO_PALLET_TYPE = rhvAscCmd.TO_PALLET_TYPE;
                    lhvAscOrder.TO_TRUCK_STATUS = rhvAscCmd.TO_TRUCK_STATUS;
                }
            }
            else if (lhv is STS_Order)
            {
                STS_Order lhvStsOrder = (STS_Order)lhv;
                if (rhv is STS_ResJob)
                {
                    STS_ResJob rhvStsResJob = (STS_ResJob)rhv;
                    lhvStsOrder.TO_RFID = rhvStsResJob.TO_RFID;
                }
            }
        }

        public static void FillOrderContainer(dynamic order, dynamic rhvOrderOrTosJob)
        {
            order.CONTAINER_ID = rhvOrderOrTosJob.CONTAINER_ID;
            order.CONTAINER_DOOR_DIRECTION = rhvOrderOrTosJob.CONTAINER_DOOR_DIRECTION;
            order.CONTAINER_HEIGHT = rhvOrderOrTosJob.CONTAINER_HEIGHT;
            order.CONTAINER_ISO = rhvOrderOrTosJob.CONTAINER_ISO;
            order.CONTAINER_IS_EMPTY = rhvOrderOrTosJob.CONTAINER_IS_EMPTY;
            order.CONTAINER_LENGTH = rhvOrderOrTosJob.CONTAINER_LENGTH;
            order.CONTAINER_WEIGHT = rhvOrderOrTosJob.CONTAINER_WEIGHT;
        }

        public static void FillOrderMiscellaneous(Order_Base lhv, dynamic rhv)
        {
            lhv.PRIORITY = rhv.PRIORITY;
            lhv.PLAN_START_TIME = rhv.PLAN_START_TIME;
            lhv.PLAN_END_TIME = rhv.PLAN_END_TIME;

            if (lhv is AGV_Order)
            {
                AGV_Order lhvAgvOrder = lhv as AGV_Order;
                if (rhv is AGV_Order)
                {
                    AGV_Order rhvAgvOrder = rhv as AGV_Order;
                    lhvAgvOrder.YARD_ID = rhvAgvOrder.YARD_ID;
                    lhvAgvOrder.QUAY_ID = rhvAgvOrder.QUAY_ID;
                    lhvAgvOrder.VERSION = rhvAgvOrder.VERSION;
                    lhvAgvOrder.QC_REFID = rhvAgvOrder.QC_REFID;
                }
                else if (rhv is AGV_ResJob)
                {
                    AGV_ResJob rhvAgvResJob = rhv as AGV_ResJob;
                    lhvAgvOrder.YARD_ID = rhvAgvResJob.YARD_ID;
                    lhvAgvOrder.QUAY_ID = rhvAgvResJob.QUAY_ID;
                    lhvAgvOrder.VERSION = rhvAgvResJob.VERSION;
                }
            }
            else if (lhv is ASC_Order)
            {
                ASC_Order lhvAscOrder = lhv as ASC_Order;
                if (rhv is ASC_ResJob)
                {
                    ASC_ResJob rhvAscResJob = rhv as ASC_ResJob;
                    lhvAscOrder.YARD_ID = rhvAscResJob.YARD_ID;
                    lhvAscOrder.OPERATOR_ID = rhvAscResJob.OPERATOR_ID;
                    lhvAscOrder.VERSION = rhvAscResJob.VERSION;
                    lhvAscOrder.ORDER_ASSIGNER = rhvAscResJob.ORDER_ASSIGNER;

                }
                else if (rhv is ASC_Order)
                {
                    ASC_Order rhvAscOrder = rhv as ASC_Order;
                    lhvAscOrder.YARD_ID = rhvAscOrder.YARD_ID;
                    lhvAscOrder.OPERATOR_ID = rhvAscOrder.OPERATOR_ID;
                    lhvAscOrder.VERSION = rhvAscOrder.VERSION;
                    lhvAscOrder.ORDER_ASSIGNER = rhvAscOrder.ORDER_ASSIGNER;
                }
            }
            else if (lhv is STS_Order)
            {
                STS_Order lhvStsOrder = (STS_Order)lhv;
                if (rhv is STS_ResJob)
                {
                    STS_ResJob rhvStsResJob = (STS_ResJob)rhv;
                    lhvStsOrder.VESSEL_ID = rhvStsResJob.VESSEL_ID;
                    lhvStsOrder.QUAY_ID = rhvStsResJob.QUAY_ID;
                    lhvStsOrder.MOVE_NO = rhvStsResJob.MOVE_NO ?? "1";
                    lhvStsOrder.PLATFORM_CONFIRM = rhvStsResJob.PLATFORM_CONFIRM;
                }
            }
        }

        public static void FillOrderContent(Order_Base lhv, dynamic rhvOrderOrTosJob)
        {
            FillOrderFromPosition(lhv, rhvOrderOrTosJob);
            FillOrderToPosition(lhv, rhvOrderOrTosJob);
            FillOrderContainer(lhv, rhvOrderOrTosJob);
            FillOrderMiscellaneous(lhv, rhvOrderOrTosJob);
        }

        public static AGV_Order_Type GetAgvOrderType(AGV_Order order, AGV_Command cmd)
        {
            JobType jobType = Helper.GetEnum(order.JOB_TYPE, JobType.UNKNOWN);

            if (jobType == JobType.REPOSITION
                && (cmd == null || !cmd.IsComplete()))
            {
                return AGV_Order_Type.RepositionToPb;
            }

            if (Helper.GetEnum(order.FROM_BAY_TYPE, BayType.UnKnown) == BayType.AGV
                || (cmd != null && cmd.IsCompleteFrom()))
            {
                switch (jobType)
                {
                    case JobType.DISC:
                    case JobType.DBLOCK:
                        return AGV_Order_Type.DelieverToWstp;
                    case JobType.LOAD:
                        return AGV_Order_Type.DelieverToQctp;
                }
                return AGV_Order_Type.Unknown;
            }

            if (cmd == null || cmd.IsInFirstHalf())
            {
                switch (jobType)
                {
                    case JobType.DISC:
                        return AGV_Order_Type.ReceiveFromQctp;
                    case JobType.LOAD:
                    case JobType.DBLOCK:
                        return AGV_Order_Type.ReceiveFromWstp;
                }
                return AGV_Order_Type.Unknown;
            }

            if (cmd.IsComplete())
            {
                bool bCancel = Helper.GetEnum(order.JOB_STATUS, ResJobStatus.Unknown) == ResJobStatus.Cancel;
                switch (jobType)
                {
                    case JobType.DISC:
                    case JobType.DBLOCK:
                        return bCancel ? AGV_Order_Type.DelieverToWstpCancelComplete : AGV_Order_Type.DelieverToWstpComplete;
                    case JobType.LOAD:
                        return bCancel ? AGV_Order_Type.DelieverToQctpCancelComplete : AGV_Order_Type.DelieverToQctpComplete;
                }
                return AGV_Order_Type.Unknown;
            }

            return AGV_Order_Type.Unknown;
        }

        public static bool IsTaskInitial(TaskStatus status)
        {
            return (status == TaskStatus.None
                 || status == TaskStatus.Almost_Ready
                 || status == TaskStatus.Update_OK
                 || status == TaskStatus.Update_FALSE
                 || status == TaskStatus.Cancel_FALSE);
        }

        public static bool IsTaskInFirstHalfDoing(TaskStatus status)
        {
            return (!Helper.IsTaskInitial(status) && !Utility.IsTaskBind(status));
        }

        public static bool IsTaskBind(TaskStatus status)
        {
            return (status == TaskStatus.Cancel_OK
                 || status == TaskStatus.Complete
                 || status == TaskStatus.Exception_Complete
                 || status == TaskStatus.Complete_From);
        }

        public static bool IsAgvTaskAssignedByTos(AGV_ResJob job)
        {
            return IsAgvTaskAssignedByTos(job.FROM_BAY_TYPE, job.CHE_ID);
        }

        public static bool IsAgvTaskAssignedByTos(string fromBayType, string cheId)
        {
            if (Helper.GetEnum(fromBayType, BayType.UnKnown) == BayType.AGV
                && !string.IsNullOrWhiteSpace(cheId))
            {
                return true;
            }

            return false;
        }

        public static bool IsTruckTask(string FROM_BAY_TYPE, string TO_BAY_TYPE)
        {
            BayType fromBayType = Helper.GetEnum(FROM_BAY_TYPE, BayType.UnKnown);
            BayType toBayType = Helper.GetEnum(TO_BAY_TYPE, BayType.UnKnown);

            return Helper.IsTruckTask(fromBayType, toBayType);
        }

        //public enum TaskProcess 
        //{ 
        //    NULL, 
        //    Cancel_OK, 
        //    Exception,//任务发生异常         
        //    WaitUpdate, 
        //    Ready, //=4
        //    Entered,        
        //    CompleteFrom, 
        //    CompleteCtn, //交换区抓放箱完成通知 
        //    AlmostComplete, //=8
        //    CompleteTo,//Abort, 
        //    Exception_Complete//异常位置不在目标位置 
        //}
        // taskprocess变成大于等于4小于等于8就是任务执行中
        public static bool IsAscTaskInExecuting(ASC_Task ascTask)
        {
            int taskProcess = ascTask.TaskProcess;

            return taskProcess >= 4 && taskProcess <= 8;
        }

        public static Asc GetFullBlockWorkAsc(List<Asc> listAscOfCurBlock)
        {
            if (listAscOfCurBlock.Exists(x => x.IsMaintenaceMode())
                && listAscOfCurBlock.Exists(x => x.CanBeScheduled()))
            {
                return listAscOfCurBlock.Find(x => x.CanBeScheduled());
            }

            return null;
        }
#endregion
    }
}
