using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DB;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.DBDefine.YardMap;
using ZECS.Schedule.Define.DBDefine.Schedule;
using ZECS.Schedule.Define;
using JobType = ZECS.Schedule.DBDefine.Schedule.JobType;

namespace ZECS.Schedule.ECSSchedule
{
    public class Schedule_ASC 
    {
        private static Schedule_ASC s_instance;
        public static Schedule_ASC Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new Schedule_ASC();
                }
                return s_instance;
            }
        }

        private Object m_objAscScheduleLock = new object();
        private DBData_Schedule m_dbDataSchedule;

        private List<Asc> m_listAsc = new List<Asc>();
        private AscConfig[] m_arrAscConfig;
        private LaneCoordinator m_laneCoordinator = new LaneCoordinator();

        private bool m_bAddedJobManagerEvent;
        private LanePlan m_lanePlan;

        public  bool Start()
        {
            m_bAddedJobManagerEvent = false;
            m_laneCoordinator.Clear();
            return true;
        }

        public void Stop()
        {
            m_bAddedJobManagerEvent = false;
            DB_TOS.Instance.ASC_JobManagerScheduleEvent -= OnJobManagerEvent;
            m_laneCoordinator.Clear();
        }

        //选一个Job任务,理箱和集卡任务顺序安排
        //卸船时,AGV出来至PB时生成Job任务.
        //装船时,生成Job任务.
        //理箱任务,集卡任务.
        public bool Schedule(ref DBData_Schedule dbDataSchedule, List<WORK_INSTRUCTION_STATUS> listWiTopLogicSorted, LanePlan lanePlan)
        {
            lock (m_objAscScheduleLock)
            {
                m_dbDataSchedule = dbDataSchedule;

                if (!m_bAddedJobManagerEvent)
                {
                    DB_TOS.Instance.ASC_JobManagerScheduleEvent += OnJobManagerEvent;
                    m_bAddedJobManagerEvent = true;
                }

                m_lanePlan = lanePlan;

                InitCheInstance();

                LogScheduleSnapshot();

                // 将Command状态的变化更新到Order和TOS：任务完成、任务异常、任务状态变化等
                CheckCommandUpdate();

                UpdateAscOrderLane();

                List<string> listBlockNo = GetBlockNoList();

                foreach (var strBlockNo in listBlockNo)
                {
                    // 选出当前堆场的ASC
                    List<Asc> listAscOfCurBlock = m_listAsc.FindAll(x => x.BlockNo == strBlockNo && x.Status != null);

                    List<ASC_Task> listNotSentAscTaskOfCurBlock =
                        m_dbDataSchedule.m_DBData_TOS.m_listASC_Task
                            .FindAll(x => x.Task.YARD_ID == strBlockNo
                                          && !Utility.IsTaskBind(x.TaskState)
                                          && !IsTaskSent(x));

                    if (!listAscOfCurBlock.Exists(IsAscSchedulable))
                    {
                        if (listNotSentAscTaskOfCurBlock.Count > 0)
                        {
                            Logger.ECSSchedule.Error("[ASC] There are task(s), but no available ASC of block " + strBlockNo);
                        }

                        continue;
                    }

                    // 处理处于维修模式的任务
                    UpdateOrderOfMaintenanceAsc(listAscOfCurBlock);

                    UpdateLsOrderToLsAsc(listAscOfCurBlock);

                    // 选择该Block的任务并发送Order
                    if (listNotSentAscTaskOfCurBlock.Count <= 0)
                    {
                        continue;
                    }

                    SendWsTask(listWiTopLogicSorted, listAscOfCurBlock, listNotSentAscTaskOfCurBlock, strBlockNo);

                    SendSblockTask(listAscOfCurBlock, listNotSentAscTaskOfCurBlock, strBlockNo);

                    SendLsTask(listAscOfCurBlock, listNotSentAscTaskOfCurBlock, strBlockNo);
                }

                //4.将TOS任务状态的变化更新到Order：TOS取消任务、更新任务等 
                //通过JobManager_TOS delegate OnJobManagerEvent()实现
            }

            return true;
        }

        /// <summary>
        /// 维修模式任务分配：
        /// 原则1： 若堆场中的一台设备设置维修，另一台未设置维修，则未设置维修的设备表示所有任务都分配给该设备
        /// 原则2： 若设备已经分配给262设备，任务未完成时262设置维修模式，调度更新该任务设备号为272，此时就算262设置为非维修任务也由272来执行
        /// 原则3： 若设备已经分配给262设备，任务未完成时262设置维修模式，调度更新该任务设备号为272，此时272又设置为维修模式，
        ///         此时由于两台设备都设置了维修模式任务还是由272来执行，然后将262设置为非维修模式，此时该任务由调度更新分配给262来执行
        /// 原则4： 维修模式去掉后，对新收到的任务，恢复默认的分配规则。
        ///         比如说一开始都非维修的，后来海侧设备设置维修模式，这时候陆侧设备全场作业；然后海侧设备维修模式去掉了，这时候海测设备分配海侧的任务，陆侧设备分配陆侧的任务
        /// </summary>
        /// <param name="listAscOfCurBlock"></param>
        private void UpdateOrderOfMaintenanceAsc(List<Asc> listAscOfCurBlock)
        {
            if (listAscOfCurBlock.Count <= 1)
                return;

            Asc ascNormal = listAscOfCurBlock.Find(x => x.CanBeScheduled());
            if (ascNormal == null)
                return;

            Asc ascMaintenance = listAscOfCurBlock.Find(x => x.IsMaintenaceMode());
            if (ascMaintenance == null)
                return;

            //bool isAscAutomaticWorking = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
            //        .Exists(x => x.CHE_ID == ascAutomatic.Status.CHE_ID && IsOrderInWorking(x));
            //if (isAscAutomaticWorking)
            //    return; // ASC is working.

            List<ASC_Order> listOrderMaintenance = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                .FindAll(x => x.CHE_ID == ascMaintenance.Status.CHE_ID && IsOrderInWorking(x));
            if (listOrderMaintenance.Count <= 0)
                return;

            foreach (var orderMaintenance in listOrderMaintenance)
            {
                ASC_Order order = new ASC_Order();
                order.Copy(orderMaintenance);
                order.CHE_ID = ascNormal.Status.CHE_ID;

                bool bRet = UpdateOrderToDbAndCacheEx(order, MethodBase.GetCurrentMethod().Name);

                Logger.ECSSchedule.Warn(string.Format("[ASC] MAINTENANCE_MODE cause update ASC from {0} to {1} for order: {2}",
                    ascMaintenance, ascNormal, order));
            }
        }

        /// <summary>
        /// 当陆侧ASC不再是维修模式式，将集卡任务更新为陆侧ASC做。
        /// </summary>
        /// <param name="listAscOfCurBlock"></param>
        private void UpdateLsOrderToLsAsc(List<Asc> listAscOfCurBlock)
        {
            Asc lsAsc = listAscOfCurBlock.Find(x => !x.IsWaterSide && x.CanBeScheduled());
            if (lsAsc == null)
                return;

            foreach (ASC_Order ascOrder in m_dbDataSchedule.m_DBData_BMS.m_listASC_Order)
            {
                // 集卡任务，CHE_ID不为空且不是陆侧，且未开始，需更新为陆侧
                if (ascOrder.YARD_ID == lsAsc.BlockNo
                    && ascOrder.TaskSide() == AscTaskSide.LandSide
                    && ascOrder.CHE_ID != lsAsc.Status.CHE_ID)
                {
                    ASC_Task ascTask = m_dbDataSchedule.m_DBData_TOS.m_listASC_Task
                        .Find(x => x.Task.JOB_ID == ascOrder.JOB_ID && x.Task.JOB_ID == ascOrder.YARD_ID);

                    if (ascTask != null && !Utility.IsAscTaskInExecuting(ascTask) && !Helper.IsTaskComplete(ascTask.TaskState))
                    {
                        ASC_Order order = new ASC_Order();
                        order.Copy(ascOrder);
                        order.CHE_ID = lsAsc.Status.CHE_ID;

                        bool bRet = UpdateOrderToDbAndCacheEx(order, MethodBase.GetCurrentMethod().Name);

                        Logger.ECSSchedule.Warn(string.Format("[ASC] {0} {1} NOT MAINTENANCE_MODE cause update order's CHE_ID to Land side ASC of order: {2}",
                            MethodBase.GetCurrentMethod().Name, bRet ? "Success" : "Fail",
                            order));
                    }
                }
            }
        }

        /// <summary>
        /// 为该Block的ASC选择WS任务并发送
        /// </summary>
        /// <param name="listWiTopLogicSorted"></param>
        /// <param name="listAscOfCurBlock"></param>
        /// <param name="listNotSentAscTaskOfCurBlock"></param>
        /// <param name="strBlockNo"></param>
        private void SendWsTask(List<WORK_INSTRUCTION_STATUS> listWiTopLogicSorted, List<Asc> listAscOfCurBlock,
            List<ASC_Task> listNotSentAscTaskOfCurBlock, string strBlockNo)
        {
            // 一. 选出可接受海侧任务的ASC
            Asc theAsc = null;

            Asc fullBlockAsc = Utility.GetFullBlockWorkAsc(listAscOfCurBlock);

            ASC_Order orderAscDoing = null;

            if (fullBlockAsc != null)
            {
                orderAscDoing = OrderAscDoing(fullBlockAsc, AscTaskSide.WaterSide | AscTaskSide.LandSide);

                if (null == orderAscDoing)
                {
                    theAsc = fullBlockAsc;
                }
            }
            else
            {
                Asc wsAsc = listAscOfCurBlock.Find(x => x.IsWaterSide);

                orderAscDoing = OrderAscDoing(wsAsc, AscTaskSide.WaterSide);

                if (wsAsc != null && wsAsc.CanBeScheduled() && null == orderAscDoing)
                {
                    theAsc = wsAsc;
                }
            }

            if (theAsc == null)
            {
                Logger.ECSSchedule.Info("[ASC] no available ASC for Water side task. Perhaps ASC is busy. block "
                    + strBlockNo + ", orderAscDoing: " + (orderAscDoing == null ? "null" : orderAscDoing.ToString()));
                return;
            }

            // 二、根据AGV Order和偏序表选出当前可做的海侧任务列表
            List<LaneInfoEx> listLaneOfCurBlock =
                m_lanePlan.GetTpLanes(strBlockNo, BayType.WS.ToString())
                    .FindAll(li => li.LaneStatus != LaneStatus.DISABLED);

            // OCCUPIED任务在前
            listLaneOfCurBlock.Sort((x, y) => y.LaneStatus - x.LaneStatus);

            // 1. 箱子在车道上的AGV Order对应的ASC任务
            List<ASC_Task> listAscTaskOnLaneToDo = SelectAscTaskOnLaneToDo(listLaneOfCurBlock, strBlockNo);

            foreach (ASC_Task ascTask in listAscTaskOnLaneToDo)
            {
                Logger.ECSSchedule.Info("[ASC] Try Select ASC Task for container is on lane. Block: " + strBlockNo +
                                ", ASC Job: " + Utility.GetString(ascTask.Task));

                // 海侧任务只能发送一个
                if (CreateAscOrder(ascTask.Task, theAsc))
                    return;
            }

            // 2. 车道全忙或非伴侣车道全忙。此时从中选择一个任务执行，避免锁死
            List<ASC_Task> listAscTaskForLaneBusyToDo = SelectAscTaskForLaneBusyToDo(listLaneOfCurBlock, strBlockNo);

            foreach (ASC_Task ascTask in listAscTaskForLaneBusyToDo)
            {
                Logger.ECSSchedule.Info("[ASC] Try Select ASC Task by AGV Order for all (normal) lane of block is busy. Block: " + strBlockNo +
                                ", ASC Job: " + Utility.GetString(ascTask.Task));

                // 海侧任务只能发送一个
                if (CreateAscOrder(ascTask.Task, theAsc))
                    return;
            }

            // 3. WI 已排序任务
            List<ASC_Task> listWiTaskAscCanDo = listWiTopLogicSorted
                .Select(wi => Utility.FindTaskByWi(wi, m_dbDataSchedule.m_DBData_TOS.m_listASC_Task))
                .Where(task => task != null && task.Task.YARD_ID == strBlockNo)
                .ToList();

            // 4. Water Side task which ASC_Order not sent, but STS or AGV sent.
            List<ASC_Task> listNotSentWsAscTask =
                listNotSentAscTaskOfCurBlock.FindAll(x => x.Task.TaskSide() == AscTaskSide.WaterSide);

            List<ASC_Task> listNotSentWsDblockTaskAscCanDo = new List<ASC_Task>();
            List<ASC_Task> listNotSentWsLoadDiscTaskAscCanDo = new List<ASC_Task>();

            foreach (var ascTask in listNotSentWsAscTask)
            {
                JobType jobType = Helper.GetEnum(ascTask.Task.JOB_TYPE, JobType.UNKNOWN);

                if (jobType == JobType.DBLOCK)
                {
                    listNotSentWsDblockTaskAscCanDo.Add(ascTask);
                }
                else if (jobType == JobType.DISC || jobType == JobType.LOAD)
                {
                    AGV_Task agvTask = m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task.Find(x => x.Task.JOB_ID == ascTask.Task.JOB_ID);
                    if (agvTask != null && !Helper.IsTaskInitial(agvTask.TaskState))
                    {
                        listNotSentWsLoadDiscTaskAscCanDo.Add(ascTask);
                    }
                }
            }

            // 5. Water Side task TOS指定AGV的任务.
            List<ASC_Task> listTosAssignedAgvTaskAscCanDo =
                listNotSentWsAscTask.FindAll(x => Utility.IsAgvAssignedByTos(x.Task));

            // 下发任务到ECS
            // 偏序表中的任务将优先发送
            List <ASC_Task> listTaskAscToDo =
                    listWiTaskAscCanDo
                    .Union(listNotSentWsLoadDiscTaskAscCanDo)
                    .Union(listNotSentWsDblockTaskAscCanDo)
                    .Union(listTosAssignedAgvTaskAscCanDo)
                    .Distinct()
                    .ToList();

            foreach (ASC_Task ascTask in listTaskAscToDo)
            {
                // 海侧任务只能发送一个
                if (CreateAscOrder(ascTask.Task, theAsc))
                    return;
            }
        }

        /// <summary>
        /// 发送SBLOCK任务
        /// </summary>
        /// <param name="listAscOfCurBlock"></param>
        /// <param name="listNotSentAscTaskOfCurBlock"></param>
        /// <param name="strBlockNo"></param>
        private void SendSblockTask(List<Asc> listAscOfCurBlock, List<ASC_Task> listNotSentAscTaskOfCurBlock, string strBlockNo)
        {
            List<ASC_Task> listSblockAscTask = listNotSentAscTaskOfCurBlock
                .FindAll(x => x.Task.TaskSide() == AscTaskSide.Sblock || Helper.GetEnum(x.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.SBLOCK);

            if (listSblockAscTask.Count <= 0)
                return;

            Asc wsAsc = listAscOfCurBlock.Find(x => x.IsWaterSide && x.CanBeScheduled());
            Asc lsAsc = listAscOfCurBlock.Find(x => !x.IsWaterSide && x.CanBeScheduled());

            var listSblockNotRestowTask = new List<ASC_Task>();

            foreach (ASC_Task ascTask in listSblockAscTask)
            {
                string strCheId;
                bool bPermitHelpMode;
                if (IsRestow(ascTask.Task, out strCheId, out bPermitHelpMode))
                {
                    bool bRet = SendRestowOrder(ascTask.Task, strCheId, bPermitHelpMode);
                    if (!bRet)
                    {
                        Logger.ECSSchedule.Error("[ASC] Failed to send restow order. ASC Task: " +
                                                 Utility.GetString(ascTask.Task));
                    }
                }
                else
                {
                    // 选最上层SBLCOK
                    ASC_Task taskSameBayLane = listSblockNotRestowTask
                        .Find(x => x.Task.FROM_BAY.EqualsEx(ascTask.Task.FROM_BAY)
                                   && x.Task.FROM_LANE.EqualsEx(ascTask.Task.FROM_LANE));

                    if (taskSameBayLane != null)
                    {
                        if (Utility.CompareStringAsInt(ascTask.Task.FROM_TIER, taskSameBayLane.Task.FROM_TIER) > 0)
                        {
                            listSblockNotRestowTask.Remove(taskSameBayLane);
                            listSblockNotRestowTask.Add(ascTask);
                        }
                    }
                    else
                    {
                        listSblockNotRestowTask.Add(ascTask);
                    }
                }
            }

            bool isSentSblockOrder = false;

            // 理箱任务初始排序规则：
            // 1.按优先级排序
            // 2.按plan_start_time排序
            // 3.按jobid排序
            listSblockNotRestowTask
                .Sort((x, y) =>
                {
                    if (x.Task.PRIORITY != y.Task.PRIORITY)
                        return y.Task.PRIORITY - x.Task.PRIORITY;
                    if (x.Task.PLAN_START_TIME != y.Task.PLAN_START_TIME)
                        return DateTime.Compare(x.Task.PLAN_START_TIME, y.Task.PLAN_START_TIME);
                    
                    return Utility.CompareStringAsInt(x.Task.JOB_ID, y.Task.JOB_ID);
                });

            foreach (ASC_Task ascTask in listSblockNotRestowTask)
            {
                if (lsAsc != null && null == OrderAscDoing(lsAsc, AscTaskSide.WaterSide | AscTaskSide.LandSide | AscTaskSide.Sblock))
                {
                    isSentSblockOrder = CreateAscOrder(ascTask.Task, lsAsc);
                }
                else if (wsAsc != null && null == OrderAscDoing(wsAsc, AscTaskSide.WaterSide | AscTaskSide.LandSide | AscTaskSide.Sblock))
                {
                    isSentSblockOrder = CreateAscOrder(ascTask.Task, wsAsc);
                }

                // 一个堆场 理箱任务只发送一个，且接受理箱任务的ASC必须是完全空闲（无海陆理箱捣箱任务）。
                // 但有可能一个ASC在做捣箱，此时另一个ASC会接受理箱。
                if (isSentSblockOrder)
                    break;
            }
        }

        /// <summary>
        /// 发送陆侧任务
        /// </summary>
        /// <param name="listAscOfCurBlock"></param>
        /// <param name="listNotSentAscTaskOfCurBlock"></param>
        /// <param name="strBlockNo"></param>
        private void SendLsTask(List<Asc> listAscOfCurBlock, List<ASC_Task> listNotSentAscTaskOfCurBlock, string strBlockNo)
        {
            // 1. 选出可接受陆侧任务的ASC
            Asc theAsc = Utility.GetFullBlockWorkAsc(listAscOfCurBlock) ??
                         listAscOfCurBlock.Find(x => !x.IsWaterSide && x.CanBeScheduled());

            if (theAsc == null)
            {
                Logger.ECSSchedule.Info("[ASC] no available ASC for Land side task. Perhaps ASC is busy. block " + strBlockNo);
                return;
            }

            // 2.
            List<ASC_Task> listLsAscTask = listNotSentAscTaskOfCurBlock
                .FindAll(x => x.Task.TaskSide() == AscTaskSide.LandSide);

            foreach (ASC_Task ascTask in listLsAscTask)
            {
                // 陆侧任务连续发送
                CreateAscOrder(ascTask.Task, theAsc);
            }
        }

        //根据CommandVersion和OrderVersion的比对来判断任务是否有更新，如有，则更新至Order和TOS
        private void CheckCommandUpdate()
        {
            var listAscCommand = m_dbDataSchedule.m_DBData_BMS.m_listASC_Command;
            var listAscOrder = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order;

            foreach (ASC_Command cmd in listAscCommand)
            {
                ASC_Order order = listAscOrder.Find(x => x.ORDER_ID == cmd.ORDER_ID);

                if (!Utility.IsCommandChanged(cmd, order))
                    continue;

                var cmdStatus = cmd.GetCmdStatus();
                bool bRet = false;
                switch (cmdStatus)
                {
                    case TaskStatus.Complete:
                    case TaskStatus.Exception_Complete:
                        bRet = OnCommandComplete(order, cmd);
                        break;
                    case TaskStatus.Complete_From:
                        bRet = true;
                        break;
                    default:
                        bRet = true;
                        break;
                }

                //if (bRet)
                {
                    UpdateOrderAndTaskByCommand(order, cmd);
                }
            }
        }

        /// <summary>
        /// 处理双箱Link任务。
        /// 集卡无双箱Link。
        /// </summary>
        /// <param name="order"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private bool OnCommandComplete(ASC_Order order, ASC_Command cmd)
        {
            UnpairOrder(cmd);

            var listResJob = m_dbDataSchedule.m_DBData_TOS.m_listASC_ResJob;
            ASC_ResJob resJobLink = listResJob.Find(x => x.JOB_LINK == order.JOB_ID);

            if (resJobLink == null)
            {
                // 单箱，此时不需额外处理，返回true
                return true;
            }

            if (resJobLink.JobStatus() != ResJobStatus.New && resJobLink.JobStatus() != ResJobStatus.Update)
                return true;    // 任务已取消，不要再发送

            ASC_Order orderLink = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                .Find(x => x.JOB_ID == resJobLink.JOB_ID);
            if (orderLink != null)
            {
                return true; // 双箱另一任务已下发，不需再处理，返回true。
            }

            // 新建并发送orderLink

            orderLink = (ASC_Order)Utility.CreateOrderByResJob(resJobLink, true, true, m_dbDataSchedule);
            if (orderLink == null)
                return false;

            UpdateOrderLink(resJobLink, ref orderLink);

            ASC_Order orderLinkOld = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order.Find(x => x.ORDER_ID == orderLink.ORDER_ID);
            if (orderLinkOld != null)
                return false; // error

            Asc asc = m_listAsc.Find(x => x.Status.CHE_ID == order.CHE_ID);
            orderLink.CHE_ID = asc.Status.CHE_ID;
            JobType jobType = orderLink.GetJobType();
            if (jobType == JobType.LOAD || jobType == JobType.DBLOCK)
            {
                orderLink.TO_LANE = order.TO_LANE;
            }
            else if (jobType == JobType.DISC)
            {
                orderLink.FROM_LANE = order.FROM_LANE;
            }

            bool bRet = InsertOrderToDbAndCache(orderLink, "Create ASC Order Link");

            return bRet;
        }

        private ASC_Order GetOrderLink(ASC_Order order)
        {
            ASC_ResJob jobLink = m_dbDataSchedule.m_DBData_TOS.m_listASC_ResJob
                .Find(x => x.JOB_LINK == order.JOB_ID);

            if (jobLink == null)
                return null;

            ASC_Order orderLink = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                .Find(x => x.JOB_ID == jobLink.JOB_ID);

            return orderLink;
        }

        private void InitCheInstance()
        {
            m_arrAscConfig = Asc.LoadConfig();

            m_listAsc = m_dbDataSchedule.m_DBData_BMS.m_listASC_Status
                .Where(x => x != null)
                .Select(ascStatus => new Asc(ascStatus, m_arrAscConfig))
                .ToList();

            m_listAsc.Sort(
                (x, y) =>
                {
                    int iRet = string.CompareOrdinal(x.BlockNo, y.BlockNo);
                    return iRet == 0 ? string.CompareOrdinal(x.Status.CHE_ID, y.Status.CHE_ID) : iRet;
                });
        }

        /// <summary>
        ///指定车道 
        /// </summary>
        private void UpdateAscOrderLane()
        {
            var listAscOrder = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order;

            foreach (var order in listAscOrder)
            {
                UpdateAscOrderLane(order);
            }
        }

        /// <summary>
        ///指定车道 
        /// </summary>
        /// <param name="orderIn"></param>
        private void UpdateAscOrderLane(ASC_Order orderIn)
        {
            ASC_Order order = new ASC_Order();
            order.Copy(orderIn);

            bool bRet = TryFillAscOrderLane(ref order);

            if (bRet)
            {
                ASC_ResJob job = m_dbDataSchedule.m_DBData_TOS.m_listASC_ResJob
                    .Find(x => x.JOB_ID == order.JOB_ID && x.YARD_ID == order.YARD_ID);
                UpdateOrderLink(job, ref order);

                UpdateOrderToDbAndCacheEx(order, MethodBase.GetCurrentMethod().Name);
            }
        }

        private ASC_Order_Type GetAscOrderType(ASC_Order order)
        {
            JobType jobType = order.GetJobType();

            ASC_Command cmd = m_dbDataSchedule.m_DBData_BMS.m_listASC_Command.Find(x => x.ORDER_ID == order.ORDER_ID);

            BayType fromBayType = Helper.GetEnum(order.FROM_BAY_TYPE, BayType.UnKnown);
            BayType toBayType = Helper.GetEnum(order.TO_BAY_TYPE, BayType.UnKnown);

            if (cmd == null || cmd.IsInFirstHalf())
            {
                switch (jobType)
                {
                    case JobType.DISC:
                        return (fromBayType == BayType.WS) ? ASC_Order_Type.PickingUpFromWstp : ASC_Order_Type.PickingUpFromLstp;
                    case JobType.LOAD:
                        return (toBayType == BayType.WS) ? ASC_Order_Type.PickingUpFromBlockToWs : ASC_Order_Type.PickingUpFromBlockToLs;
                    case JobType.DBLOCK:
                        {
                            if (fromBayType == BayType.WS)
                                return ASC_Order_Type.PickingUpFromWstp;
                            if (fromBayType == BayType.Block)
                                return (toBayType == BayType.WS) ? ASC_Order_Type.PickingUpFromBlockToWs : ASC_Order_Type.PickingUpFromBlockToLs;
                            if (fromBayType == BayType.LS)
                                return ASC_Order_Type.PickingUpFromLstp;
                            return ASC_Order_Type.Unknown;
                        }
                }
            }
            else if (cmd.IsCompleteFrom())
            {
                switch (jobType)
                {
                    case JobType.DISC:
                        return ASC_Order_Type.PuttingDownToBlock;
                    case JobType.LOAD:
                        return (toBayType == BayType.WS) ? ASC_Order_Type.PuttingDownToWstp : ASC_Order_Type.PuttingDownToLstp;
                    case JobType.DBLOCK:
                        {
                            if (toBayType == BayType.WS)
                                return ASC_Order_Type.PuttingDownToWstp;
                            if (toBayType == BayType.Block)
                                return ASC_Order_Type.PuttingDownToBlock;
                            if (toBayType == BayType.LS)
                                return ASC_Order_Type.PuttingDownToLstp;
                            return ASC_Order_Type.Unknown;
                        }
                }
            }

            return ASC_Order_Type.Unknown;
        }

        private bool IsOrderInWorking(ASC_Order order)
        {
            if (order == null)
                return false;

            var cmd = m_dbDataSchedule.m_DBData_BMS.m_listASC_Command
                .Find(x => x.ORDER_ID == order.ORDER_ID);

            if (cmd == null || !cmd.IsComplete())
            {
                return true;
            }

            return false;
        }

        private bool IsOrderComplete(ASC_Order order)
        {
            if (order == null)
                return false;

            var cmd = m_dbDataSchedule.m_DBData_BMS.m_listASC_Command
                .Find(x => x.ORDER_ID == order.ORDER_ID);

            return cmd != null && cmd.IsComplete();
        }

        private bool IsOrderReady(ASC_Order order)
        {
            if (order == null)
                return false;

            var cmd = m_dbDataSchedule.m_DBData_BMS.m_listASC_Command
                .Find(x => x.ORDER_ID == order.ORDER_ID);

            return cmd != null && cmd.GetCmdStatus() == TaskStatus.Ready;
        }

        private bool IsOrderCompleteFrom(AGV_Order order)
        {
            if (order == null)
                return false;

            var cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command
                .Find(x => x.ORDER_ID == order.ORDER_ID);

            if (cmd != null && cmd.IsCompleteFrom())
                return true;

            return false;
        }

        private List<string> GetBlockNoList()
        {
            List<string> listBlockNo = m_arrAscConfig.Select(x => x.BlockNo).Distinct().ToList();
            return listBlockNo;
        }

        /// <summary>
        /// 带箱AGV已到达WSTP车道或箱子已放到伴侣上(包括双箱装、单双卸及DBLOCK)
        /// 双箱装时，如果AGV已收一箱，ASC装无法换AGV，只能选择指定AGV完成双箱装第二箱
        /// </summary>
        /// <param name="listLaneOfCurBlock"></param>
        /// <param name="strBlockNo"></param>
        /// <returns></returns>
        private List<ASC_Task> SelectAscTaskOnLaneToDo(List<LaneInfoEx> listLaneOfCurBlock, string strBlockNo)
        {
            List<ASC_Task> listAscTaskOnLaneToDo =
                listLaneOfCurBlock
                    .Where(li => li.LaneStatus == LaneStatus.OCCUPIED || li.IsMateLane())
                    .SelectMany(li => m_lanePlan.GetOrderIdOfLaneInUsing(li).FindAll(x => IsContainerOnLane(x, li)))
                    .Distinct()
                    .Select(agvOrderId => m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order.Find(x => x.ORDER_ID == agvOrderId))
                    .Where(agvOrder => agvOrder != null)
                    .Select(x => GetAscTaskByAgvOrder(x, strBlockNo))
                    .Where(ascTask => ascTask != null)
                    .ToList();

            return listAscTaskOnLaneToDo;
        }

        /// <summary>
        /// 车道全已分配，且AGV全已带箱（卸船带箱或Dblock、或装船双箱已收一箱）
        /// </summary>
        /// <param name="listLaneOfCurBlock"></param>
        /// <param name="strBlockNo"></param>
        /// <returns></returns>
        private List<ASC_Task> SelectAscTaskForLaneBusyToDo(List<LaneInfoEx> listLaneOfCurBlock, string strBlockNo)
        {
            List<LaneInfoEx> listLaneOfAllBusy = null;

            bool isAllLaneBusy = listLaneOfCurBlock.TrueForAll(li => m_lanePlan.IsLaneInUsing(li));
            if (isAllLaneBusy)
            {
                listLaneOfAllBusy = listLaneOfCurBlock;
            }
            else
            {
                var listNormalLaneOfCurBlock = listLaneOfCurBlock.FindAll(li => !li.IsMateLane());

                bool isAllNormalLaneBusy = listNormalLaneOfCurBlock.Count > 0
                                           && listNormalLaneOfCurBlock.TrueForAll(li => m_lanePlan.IsLaneInUsing(li)); ;
                if (isAllNormalLaneBusy)
                {
                    listLaneOfAllBusy = listNormalLaneOfCurBlock;
                }
            }

            if (listLaneOfAllBusy == null || listLaneOfAllBusy.Count <= 0)
                return new List<ASC_Task>();

            List<AGV_Order> listAgvOrderOfLaneBusy = listLaneOfAllBusy
                .SelectMany(li => m_lanePlan.GetOrderIdOfLaneInUsing(li))
                .Distinct()
                .Select(agvOrderId => m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order.Find(x => x.ORDER_ID==agvOrderId))
                .Where(agvOrder => agvOrder != null)
                .ToList();

            bool isAllAgvOrLaneWithContainer =
                listAgvOrderOfLaneBusy.TrueForAll(IsCtnMovingToWstp);

            if (!isAllAgvOrLaneWithContainer)
                return new List<ASC_Task>();

            List<ASC_Task> listAscTaskForLaneBusyToDo =
                listAgvOrderOfLaneBusy
                    .Select(x => GetAscTaskByAgvOrder(x, strBlockNo))
                    .Where(ascTask => ascTask != null)
                    .ToList();

            return listAscTaskForLaneBusyToDo;
        }

        /// <summary>
        /// 判断该Order的箱子是否在车道上
        /// </summary>
        /// <param name="agvOrderId"></param>
        /// <param name="li"></param>
        /// <returns></returns>
        private bool IsContainerOnLane(string agvOrderId, LaneInfoEx li)
        {
            if (li == null || string.IsNullOrWhiteSpace(agvOrderId))
                return false;

            AGV_Order agvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order.Find(x => x.ORDER_ID == agvOrderId);
            if (agvOrder == null)
                return false;

            AGV_Command agvCmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command.Find(x => x.ORDER_ID == agvOrder.ORDER_ID);

            AGV_Order_Type agvOrderType = Utility.GetAgvOrderType(agvOrder, agvCmd);

            if (agvOrderType == AGV_Order_Type.DelieverToWstp)
            {
                return li.LaneStatus == LaneStatus.OCCUPIED;
            }

            if (agvOrderType == AGV_Order_Type.DelieverToWstpComplete)
            {
                return Utility.IsAscDoingCompletedAgvOrder(m_dbDataSchedule.m_DBData_BMS, agvOrder);
            }

            return false;
        }

        /// <summary>
        /// 判断AGV Order及其link中是否有一个Order正带箱去WSTP
        /// </summary>
        /// <param name="agvOrder"></param>
        /// <returns></returns>
        private bool IsCtnMovingToWstp(AGV_Order agvOrder)
        {
            if (IsAgvCarringCtnToWstpOrCtnOnLane(agvOrder))
                return true;

            if (!string.IsNullOrWhiteSpace(agvOrder.GetOrderLink()))
            {
                AGV_Order agvOrderLink = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                    .Find(x => x.GetOrderLink() == agvOrder.GetOrderLink());

                if (agvOrderLink == null)
                    return false;   // error

                return IsAgvCarringCtnToWstpOrCtnOnLane(agvOrderLink);
            }

            return false;
        }

        /// <summary>
        /// 判断AGV Order是否正带箱去WSTP
        /// </summary>
        /// <param name="agvOrder"></param>
        /// <returns></returns>
        private bool IsAgvCarringCtnToWstpOrCtnOnLane(AGV_Order agvOrder)
        {
            AGV_Command agvCmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command.Find(x => x.ORDER_ID == agvOrder.ORDER_ID);

            AGV_Order_Type agvOrderType = Utility.GetAgvOrderType(agvOrder, agvCmd);

            if (agvOrderType == AGV_Order_Type.DelieverToWstp)
            {
                return true;
            }

            if (agvOrderType == AGV_Order_Type.DelieverToWstpComplete)
            {
                return Utility.IsAscDoingCompletedAgvOrder(m_dbDataSchedule.m_DBData_BMS, agvOrder);
            }

            return false;
        }

        /// <summary>
        /// Find ASC Task by AGV Order ID
        /// </summary>
        /// <param name="agvOrder"></param>
        /// <param name="strBlockNo"></param>
        /// <returns></returns>
        private ASC_Task GetAscTaskByAgvOrder(AGV_Order agvOrder, string strBlockNo)
        {
            ASC_Task ascTask = m_dbDataSchedule.m_DBData_TOS.m_listASC_Task
                .Find(x => x.Task.JOB_ID == agvOrder.JOB_ID
                        && (agvOrder.GetJobType() != JobType.DBLOCK || x.Task.YARD_ID == strBlockNo));

            return ascTask;
        }

        /// <summary>
        /// 选择ASC可做任务
        /// </summary>
        /// <param name="listWiTopLogicSorted"></param>
        /// <param name="strBlockNo"></param>
        /// <returns></returns>
        private List<ASC_Task> SelectTaskAscCanDo(List<WORK_INSTRUCTION_STATUS> listWiTopLogicSorted, string strBlockNo)
        {
            // WI Task Sorted
            List<ASC_Task> listWiTaskAscCanDo = listWiTopLogicSorted
                .Select(wi => Utility.FindTaskByWi(wi, m_dbDataSchedule.m_DBData_TOS.m_listASC_Task))
                .Where(task => task != null && task.Task.YARD_ID == strBlockNo)
                .ToList();

            //
            List<ASC_Task> listAscTaskOfCurBlockNotSent =
                m_dbDataSchedule.m_DBData_TOS.m_listASC_Task.FindAll(
                    x => x.Task.YARD_ID == strBlockNo
                     && !Utility.IsTaskBind(x.TaskState)
                     && !IsTaskOrLinkSent(x));

            // ASC_Order not sent, but STS or AGV sent.
            List<ASC_Task> listNotSentTaskAscCanDo = new List<ASC_Task>();
            foreach (var ascTask in listAscTaskOfCurBlockNotSent)
            {
                AGV_Task agvTask = m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task.Find(x => x.Task.JOB_ID == ascTask.Task.JOB_ID);
                if (agvTask != null && !Helper.IsTaskInitial(agvTask.TaskState))
                {
                    listNotSentTaskAscCanDo.Add(ascTask);
                }
                else
                {
                    STS_Task stsTask = m_dbDataSchedule.m_DBData_TOS.m_listSTS_Task.Find(x => x.Task.JOB_ID == ascTask.Task.JOB_ID);
                    if (stsTask != null && !Helper.IsTaskInitial(stsTask.TaskState))
                    {
                        listNotSentTaskAscCanDo.Add(ascTask);
                    }
                }
            }

            // ASC_Task not in WI List (SBLOCK, DBLOCK, RECEIVE, DELIEVERY, LOAD/DISC LS?)
            List<ASC_Task> listNotWiTaskAscCanDo =
                listAscTaskOfCurBlockNotSent
                    .FindAll(x => !m_dbDataSchedule.m_DBData_TOS.m_listWORK_INSTRUCTION_STATUS.Exists(wi => wi.JOB_ID == x.Task.JOB_ID));

            return listWiTaskAscCanDo
                    .Union(listNotSentTaskAscCanDo)
                    .Union(listNotWiTaskAscCanDo)
                    .Distinct()
                    .ToList();
        }

        private bool IsTaskSent(ASC_Task task)
        {
            return m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                    .Exists(x => x.JOB_ID == task.Task.JOB_ID && x.YARD_ID == task.Task.YARD_ID);
        }

        private bool IsTaskOrLinkSent(ASC_Task task)
        {
            var listAscOrder = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order;

            if (listAscOrder.Exists(x => x.JOB_ID == task.Task.JOB_ID && x.YARD_ID == task.Task.YARD_ID))
                return true;

            var jobLink = m_dbDataSchedule.m_DBData_TOS.m_listASC_ResJob
                .Find(x => x.JOB_ID == task.Task.JOB_LINK);
            if (jobLink == null)
                return false;

            return (listAscOrder.Exists(x => x.JOB_ID == jobLink.JOB_ID && x.YARD_ID == jobLink.YARD_ID));
        }

        //选一个任务下发给ECS
        private bool SendPreTask(List<AscPreTask> listAscPreTask)
        {
            foreach (var preTask in listAscPreTask)
            {
                SendTaskToAsc(preTask.PreTask, preTask.Asc);
            }

            return true;
        }

        //在指派任务时，除了考虑空闲的ASC外，也考虑即将完成任务的ASC。
        private bool IsAscSchedulable(Asc asc)
        {
            if (!asc.CanBeScheduled())
            {
                return false;
            }

            ////根据Command表来判断ASC是否空闲或正在执行任务的后半程
            //if (!IsAscIdleOrDoingLastHalfTask(asc.Status.CHE_ID))
            //{
            //    return false;
            //}

            return true;
        }

        /// <summary>
        /// 如果ASC无Order或正在执行任务的后半程(根据Command表判断)，则其参与调度
        /// </summary>
        private bool IsAscIdleOrDoingLastHalfTask(string strCheId)
        {
            var listTheAscOrder = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                .FindAll(x => x.CHE_ID == strCheId);

            foreach (var order in listTheAscOrder)
            {
                var cmd = m_dbDataSchedule.m_DBData_BMS.m_listASC_Command
                    .Find(x => x.ORDER_ID == order.ORDER_ID);

                if (cmd == null || cmd.IsInFirstHalf())
                {
                    //有Order但无Command，或有处于上半程的任务
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 如果ASC已分配Order或有Order正在执行中(根据Command表判断)，则不可向其派发新Order。
        /// 3类任务
        ///     1、集卡任务-LS
        ///     2、海侧及理箱任务-WS & Block (Sblock非捣箱)
        ///     3、捣箱任务-Restow
        /// 如果ASC已分配WS & Block 任务，则不可向其派发新的 LS & WS & Block任务，但可派发捣箱任务。
        /// 如果ASC已分配LS 任务，则不可向其派发新的WS & Block 任务，但可派发新的LS 任务和捣箱任务。
        /// </summary>
        private bool IsAscDoingLsOrWsOrBlockOrder(string strCheId)
        {
            var listTheAscOrder = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                .FindAll(x => x.CHE_ID == strCheId
                            && (Helper.GetEnum(x.JOB_TYPE, JobType.UNKNOWN) != JobType.SBLOCK));

            var listAscCmd = m_dbDataSchedule.m_DBData_BMS.m_listASC_Command;
            foreach (var order in listTheAscOrder)
            {
                var cmd = listAscCmd.Find(x => x.ORDER_ID == order.ORDER_ID);

                if (cmd == null || !cmd.IsComplete())
                {
                    //有Order但无Command，或有未完成的任务
                    return true;
                }
            }

            return false;
        }

        private bool IsAscDoingWsOrBlockOrder(string strCheId)
        {
            var listTheAscOrder = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                .FindAll(x => x.CHE_ID == strCheId
                            && !Utility.IsTruckTask(x.FROM_BAY_TYPE, x.TO_BAY_TYPE));

            var listAscCmd = m_dbDataSchedule.m_DBData_BMS.m_listASC_Command;
            foreach (var order in listTheAscOrder)
            {
                var cmd = listAscCmd.Find(x => x.ORDER_ID == order.ORDER_ID);

                if (cmd == null || !cmd.IsComplete())
                {
                    //有Order但无Command，或有未完成的任务
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 1. SBLOCK, 2. 海侧任务， 3. 陆侧任务
        /// </summary>
        /// <param name="asc"></param>
        /// <param name="taskSide"></param>
        /// <returns></returns>
        private ASC_Order OrderAscDoing(Asc asc, AscTaskSide taskSide)
        {
            var listTheAscOrder = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                .FindAll(x => x.CHE_ID == asc.Status.CHE_ID
                            && (x.TaskSide() & taskSide) != 0);

            foreach (var order in listTheAscOrder)
            {
                if (order.IsTruckTask())
                {
                    var task = m_dbDataSchedule.m_DBData_TOS.m_listASC_Task
                        .Find(x => x.Task.JOB_ID == order.JOB_ID && x.Task.YARD_ID == asc.BlockNo);

                    if (task != null && Utility.IsAscTaskInExecuting(task))
                    {
                        return order;
                    }
                }
                else
                {
                    var cmd = m_dbDataSchedule.m_DBData_BMS.m_listASC_Command
                        .Find(x => x.ORDER_ID == order.ORDER_ID);

                    if (cmd == null || !cmd.IsComplete())
                    {
                        //有Order但无Command，或有未完成的任务
                        return order;
                    }
                }
            }

            return null;
        }

        // 发送任务包中的任务
        // 同一时刻，ASC和AGV应只有一个主任务（Load或Disc）。
        private void SendTaskToAsc(ASC_Task ascTask, Asc asc)
        {
            var job = ascTask.Task;

            JobType jobType = Helper.GetEnum(job.JOB_TYPE, JobType.UNKNOWN);

            if (Utility.IsTruckTask(job.FROM_BAY_TYPE, job.TO_BAY_TYPE))
            {
                // 集卡任务，可连续多条发送

                if (!IsAscDoingWsOrBlockOrder(asc.Status.CHE_ID))
                {
                    CreateAscOrder(job, asc);
                }
            }
            else
            {
                string strCheId;
                bool bPermitHelpMode;
                if (jobType == JobType.SBLOCK && IsRestow(job, out strCheId, out bPermitHelpMode))
                {
                    // 捣箱任务立即发送
                    SendRestowOrder(job, strCheId, bPermitHelpMode);
                }
                else
                {
                    // 海侧任务或DBLOCK理箱任务或SBLOCK理箱任务
                    if (!IsAscDoingLsOrWsOrBlockOrder(asc.Status.CHE_ID))
                    {
                        CreateAscOrder(job, asc);
                    }
                }
            }
        }

        /// <summary>
        /// 根据Job和Asc创建Order
        /// </summary>
        /// <param name="job"></param>
        /// <param name="asc"></param>
        /// <returns></returns>
        private bool CreateAscOrder(ASC_ResJob job, Asc asc)
        {
            if (job == null || asc == null)
                return false;

            if (job.YARD_ID != asc.BlockNo)
                return false;

            if (job.JobStatus() != ResJobStatus.New && job.JobStatus() != ResJobStatus.Update)
                return false;

            if (job.JOB_LINK != null)
            {
                ASC_Order orderLink = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                    .Find(x => x.JOB_ID == job.JOB_LINK);

                if (orderLink != null && !IsOrderComplete(orderLink))
                {
                    // ASC双箱第一箱已下发，在第一箱未完成前，不能发送第二箱任务。
                    return false;
                }
            }

            ASC_Order order = (ASC_Order)Utility.CreateOrderByResJob(job, true, true, m_dbDataSchedule);
            if (order == null)
                return false;

            ASC_Order orderOld = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order.Find(x => x.ORDER_ID == order.ORDER_ID);

            if (orderOld != null)
                return false;

            order.CHE_ID = asc.Status.CHE_ID;

            if (TryFillAscOrderLane(ref order))
            {
                ASC_ResJob jobOfOrder = m_dbDataSchedule.m_DBData_TOS.m_listASC_ResJob
                    .Find(x => x.JOB_ID == order.JOB_ID && x.YARD_ID == order.YARD_ID);

                UpdateOrderLink(jobOfOrder, ref order);
            }

            bool bRet = InsertOrderToDbAndCache(order, MethodBase.GetCurrentMethod().Name);

            return bRet;
        }

        /// <summary>
        ///尝试填充ASC Order中的Lane
        /// </summary>
        /// <param name="ascOrder"></param>
        /// <returns>true - 成功， false - 失败或无需指定车道</returns>
        private bool TryFillAscOrderLane(ref ASC_Order ascOrder)
        {
            if (Utility.IsTruckTask(ascOrder.FROM_BAY_TYPE, ascOrder.TO_BAY_TYPE))
                return false;

            JobType jobType = ascOrder.GetJobType();
            if (jobType != JobType.LOAD && jobType != JobType.DISC && jobType != JobType.DBLOCK)
                return false;

            ASC_Order_Type ascOrderType = GetAscOrderType(ascOrder);

            if (!IsAscLaneEmpty(ascOrder, ascOrderType))
                return false;

            AGV_Order agvOrder = FindAgvOrderMatchUpAsc(ascOrder, ascOrderType);

            if (agvOrder == null)
                return false;

            bool isSameJobId = agvOrder.JOB_ID == ascOrder.JOB_ID;

            if (ascOrderType == ASC_Order_Type.PickingUpFromBlockToWs
                || ascOrderType == ASC_Order_Type.PuttingDownToWstp)
            {
                if (!isSameJobId && !ExchangeAgvOrderByAscJob(ascOrder, agvOrder))
                {
                    return false;
                }

                ascOrder.TO_LANE = agvOrder.FROM_LANE;
            }
            else if (ascOrderType == ASC_Order_Type.PickingUpFromWstp)
            {
                if (!isSameJobId && !ExchangeAscOrderByAgvJob(ref ascOrder, agvOrder))
                {
                    return false;
                }

                ascOrder.FROM_LANE = agvOrder.TO_LANE;
            }
            else
            {
                return false;
            }

            m_laneCoordinator.PairOrder(agvOrder.ORDER_ID, ascOrder.ORDER_ID);

            return true;
        }

        /// <summary>
        /// 在Order完成后清除AGV Order与ASC Order匹配记录
        /// </summary>
        /// <param name="agvCmd"></param>
        public void UnpairOrder(AGV_Command agvCmd)
        {
            if (agvCmd != null && agvCmd.IsComplete())
            {
                string ascOrderIdPaired = m_laneCoordinator.FindPairedAscOrder(agvCmd.ORDER_ID);
                if (!string.IsNullOrWhiteSpace(ascOrderIdPaired))
                {
                    ASC_Command ascCmdPaired =
                        m_dbDataSchedule.m_DBData_BMS.m_listASC_Command.Find(x => x.ORDER_ID == ascOrderIdPaired);

                    if (ascCmdPaired != null && ascCmdPaired.IsComplete())
                    {
                        m_laneCoordinator.UnpairOrder(agvCmd.ORDER_ID);
                    }
                }
            }
        }

        /// <summary>
        /// 在Order完成后清除AGV Order与ASC Order匹配记录
        /// </summary>
        /// <param name="ascCmd"></param>
        public void UnpairOrder(ASC_Command ascCmd)
        {
            if (ascCmd != null && ascCmd.IsComplete())
            {
                string agvOrderIdPaired = m_laneCoordinator.FindPairedAgvOrder(ascCmd.ORDER_ID);
                if (!string.IsNullOrWhiteSpace(agvOrderIdPaired))
                {
                    AGV_Command agvCmdPaired =
                        m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command.Find(x => x.ORDER_ID == agvOrderIdPaired);

                    if (agvCmdPaired != null && agvCmdPaired.IsComplete())
                    {
                        m_laneCoordinator.UnpairOrder(agvCmdPaired.ORDER_ID);
                    }
                }
            }
        }

        /// <summary>
        ///ASC出箱时，根据ASC来决定AGV Order
        /// </summary>
        /// <param name="ascOrder"></param>
        /// <param name="agvOrder"></param>
        /// <returns></returns>
        private bool ExchangeAgvOrderByAscJob(ASC_Order ascOrder, AGV_Order agvOrder)
        {
            Logger.ECSSchedule.Info(string.Format("[ASC] ExchangeAgvOrderByAscJob 0 agv order 1:{0}", agvOrder));

            AGV_Order agvOrder1 = new AGV_Order();
            agvOrder1.Copy(agvOrder);

            AGV_Order agvOrderOfAscJobId = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order.Find(x => x.JOB_ID == ascOrder.JOB_ID);

            bool bUpdated = false;

            if (agvOrderOfAscJobId == null)
            {
                // 更新AGV Order，包括JobId
                AGV_ResJob agvResJob2 = m_dbDataSchedule.m_DBData_TOS.m_listAGV_ResJob.Find(x => x.JOB_ID == ascOrder.JOB_ID);
                if (agvResJob2 == null)
                    return false;

                Utility.FillOrderFromPosition(agvOrder1, agvResJob2);
                Utility.FillOrderMiscellaneous(agvOrder1, agvResJob2);
                agvOrder1.JOB_ID = agvResJob2.JOB_ID;
                agvOrder1.FROM_LANE = agvOrder.FROM_LANE; // 取回AGV既有车道

                bUpdated = Schedule_AGV.Instance.UpdateOrderToDbAndCacheEx(agvOrder1, string.Format("[ASC] ExchangeAgvOrderByAscJob 1 agv order 1 JobId {0}->{1}", agvOrder.JOB_ID, agvOrder1.JOB_ID));
            }
            else
            {
                return false;   // 不交换AGV Order。因为时延等因素，存在很多不确定性。

                #region AGV Order Exists
                //bool isOrderInFirstHalf = IsOrderInFirstHalf(agvOrderOfAscJobId);

                //Logger.ECSSchedule.Info(string.Format("[ASC] ExchangeAgvOrderByAscJob 2 agv order 2: IsOrderInFirstHalf={0} {1}", isOrderInFirstHalf, agvOrderOfAscJobId));

                //if (!isOrderInFirstHalf)
                //    return false;

                //AGV_Order agvOrder2 = new AGV_Order();
                //agvOrder2.Copy(agvOrderOfAscJobId);

                //Utility.FillOrderFromPosition(agvOrder1, agvOrder2);
                //Utility.FillOrderMiscellaneous(agvOrder1, agvOrder2);
                //agvOrder1.JOB_ID = agvOrder2.JOB_ID;
                //agvOrder1.FROM_LANE = agvOrder.FROM_LANE; // 取回AGV既有车道, agvOrder的车道应不为空。

                //Utility.FillOrderFromPosition(agvOrder2, agvOrder);
                //Utility.FillOrderMiscellaneous(agvOrder2, agvOrder);
                //agvOrder2.JOB_ID = agvOrder.JOB_ID;
                //agvOrder2.FROM_LANE = agvOrderOfAscJobId.FROM_LANE; // 取回AGV既有车道，agvOrderOfAscJobId的车道应为空，否则其和ASC应匹配上。

                //bUpdated = Schedule_AGV.Instance.UpdateOrderToDbAndCacheEx(agvOrder1, "[ASC] ExchangeAgvOrderByAscJob 3 agv order 1");

                //if (bUpdated)
                //{
                //    bUpdated = Schedule_AGV.Instance.UpdateOrderToDbAndCacheEx(agvOrder2, "[ASC] ExchangeAgvOrderByAscJob 4 agv order 2");
                //}
                #endregion
            }

            return bUpdated;
        }

        /// <summary>
        ///ASC进箱时，根据AGV决定ASC Order
        /// </summary>
        /// <param name="ascOrder"></param>
        /// <param name="agvOrder"></param>
        /// <returns></returns>
        private bool ExchangeAscOrderByAgvJob(ref ASC_Order ascOrder, AGV_Order agvOrder)
        {
            string blockNo = ascOrder.YARD_ID;

            ASC_Order ascOrderOfAgvJobId = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                .Find(x => x.JOB_ID == agvOrder.JOB_ID && x.YARD_ID == blockNo);

            Logger.ECSSchedule.Info(string.Format("[ASC] ExchangeAscOrderByAgvJob 0 asc order 1:{0}", ascOrder));

            if (ascOrderOfAgvJobId != null)
            {
                // error! This should not happen.
                Logger.ECSSchedule.Error(string.Format("[ASC] ExchangeAscOrderByAgvJob 2 asc order 1:{0} exchanged with ascOrderOfAgvJobId:{1}",
                        ascOrder, Utility.GetString(ascOrderOfAgvJobId)));

                return false;

                //if (!IsOrderInFirstHalf(ascOrderOfAgvJobId))
                //    return false;

                //Logger.ECSSchedule.Info(string.Format("[ASC] ExchangeAscOrderByAgvJob 2 asc order 2:{0}", ascOrderOfAgvJobId));

                //ASC_Order ascOrder2 = new ASC_Order();
                //ascOrder2.Copy(ascOrderOfAgvJobId);

                //Utility.FillOrderContent(ascOrder1, ascOrder2);
                //ascOrder1.JOB_ID = ascOrder2.JOB_ID;
                //ascOrder1.FROM_LANE = ascOrder.FROM_LANE;

                //Utility.FillOrderContent(ascOrder2, ascOrder);
                //ascOrder2.JOB_ID = ascOrder.JOB_ID;
                //ascOrder2.FROM_LANE = ascOrderOfAgvJobId.FROM_LANE;

                //if (UpdateOrderToDbAndCacheEx(ascOrder1, "[ASC] ExchangeAscOrderByAgvJob 3 asc order 1"))
                //{
                //    return UpdateOrderToDbAndCacheEx(ascOrder2, "[ASC] ExchangeAscOrderByAgvJob 4 asc order 2");
                //}
            }

            ASC_ResJob ascResJob2 = m_dbDataSchedule.m_DBData_TOS.m_listASC_ResJob
                .Find(x => x.JOB_ID == agvOrder.JOB_ID && x.YARD_ID == blockNo);
            if (ascResJob2 == null)
                return false;

            Utility.FillOrderContent(ascOrder, ascResJob2);
            ascOrder.JOB_ID = ascResJob2.JOB_ID;

            Logger.ECSSchedule.Info(string.Format("[ASC] ExchangeAscOrderByAgvJob 1 asc order 1:{0} updated by ascResJob2:{1}",
                ascOrder, Utility.GetString(ascResJob2)));

            return true;
        }

        /// <summary>
        ///查找AGV和ASC匹配的Order
        /// </summary>
        /// <param name="ascOrder"></param>
        /// <param name="ascOrderType"></param>
        /// <returns></returns>
        private AGV_Order FindAgvOrderMatchUpAsc(ASC_Order ascOrder, ASC_Order_Type ascOrderType)
        {
            var listAgvOrderMatched = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                    .FindAll(x => !m_laneCoordinator.IsPaired(x.ORDER_ID)
                               && IsAgvAscOrderMatched(x, ascOrder, ascOrderType));

            if (listAgvOrderMatched.Count <= 0)
                return null;

            AGV_Order agvOrderJobIdMatched = listAgvOrderMatched.FirstOrDefault(x => x.JOB_ID == ascOrder.JOB_ID);
            if (agvOrderJobIdMatched != null)
                return agvOrderJobIdMatched;

            string strAscJobLink = GetTosJobLink(ascOrder);

            // ASC双箱装船第二箱必须放到同一AGV(JOB ID相同)
            if ((ascOrderType == ASC_Order_Type.PickingUpFromBlockToWs
                   || ascOrderType == ASC_Order_Type.PuttingDownToWstp)
                 && !string.IsNullOrWhiteSpace(strAscJobLink))
            {
                ASC_Order ascOrderLink = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                    .Find(x => x.JOB_ID == strAscJobLink);

                if (ascOrderLink != null)   // 第二箱
                    return null;
            }
     
            AGV_Order agvOrderJobLinkMatched = listAgvOrderMatched.FirstOrDefault(x => x.JOB_ID == strAscJobLink);
            if (agvOrderJobLinkMatched != null)
                return agvOrderJobLinkMatched;

            return listAgvOrderMatched.First();
        }

        /// <summary>
        /// 判断ASC车道是否已分配？
        /// </summary>
        /// <param name="order"></param>
        /// <param name="orderType"></param>
        /// <returns></returns>
        private static bool IsAscLaneEmpty(ASC_Order order, ASC_Order_Type orderType)
        {
            switch (orderType)
            {
                case ASC_Order_Type.PickingUpFromWstp:
                case ASC_Order_Type.PickingUpFromLstp:
                    return string.IsNullOrWhiteSpace(order.FROM_LANE);
                case ASC_Order_Type.PickingUpFromBlockToWs:
                case ASC_Order_Type.PuttingDownToWstp:
                case ASC_Order_Type.PuttingDownToLstp:
                    return string.IsNullOrWhiteSpace(order.TO_LANE);
                case ASC_Order_Type.PuttingDownToBlock:
                    return false;
                case ASC_Order_Type.Unknown:
                    return false;
            }
            return false;
        }

        /// <summary>
        /// 双箱装船第二个任务不同场只有JobId相等才能匹配，卸船只要同场有AGV车道即可匹配。
        /// </summary>
        /// <param name="agvOrder"></param>
        /// <param name="ascOrder"></param>
        /// <param name="ascOrderType"></param>
        /// <returns></returns>
        private bool IsAgvAscOrderMatched(AGV_Order agvOrder, ASC_Order ascOrder, ASC_Order_Type ascOrderType)
        {
            AGV_Command cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command.Find(x => x.ORDER_ID == agvOrder.ORDER_ID);
            AGV_Order_Type agvOrderType = Utility.GetAgvOrderType(agvOrder, cmd);

            if (agvOrderType == AGV_Order_Type.ReceiveFromWstp
                && (ascOrderType == ASC_Order_Type.PickingUpFromBlockToWs || ascOrderType == ASC_Order_Type.PuttingDownToWstp)
                && agvOrder.FROM_BLOCK == ascOrder.YARD_ID
                && !string.IsNullOrWhiteSpace(agvOrder.FROM_LANE)
                && Helper.GetEnum(agvOrder.FROM_BAY_TYPE, BayType.UnKnown) == BayType.WS
                )
            {
                if (string.IsNullOrWhiteSpace(agvOrder.GetOrderLink())
                    || agvOrder.JOB_ID == ascOrder.JOB_ID)
                {
                    return true;
                }
            }

            if ((agvOrderType == AGV_Order_Type.DelieverToWstp || agvOrderType == AGV_Order_Type.DelieverToWstpComplete)
                && ascOrderType == ASC_Order_Type.PickingUpFromWstp
                && agvOrder.TO_BLOCK == ascOrder.YARD_ID
                && !string.IsNullOrWhiteSpace(agvOrder.TO_LANE)
                && Helper.GetEnum(agvOrder.TO_BAY_TYPE, BayType.UnKnown) == BayType.WS
                )
            {
                if (agvOrderType == AGV_Order_Type.DelieverToWstpComplete)
                {
                    return Utility.IsAscDoingCompletedAgvOrder(m_dbDataSchedule.m_DBData_BMS, agvOrder);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 在ASC Order车道选择完成后，即ASC要做的箱子已确定不变后，才能更新ORDER_LINK
        /// </summary>
        /// <param name="job"></param>
        /// <param name="order"></param>
        private void UpdateOrderLink(ASC_ResJob job, ref ASC_Order order)
        {
            if (Utility.IsTwinAscOrder(job, m_dbDataSchedule.m_DBData_TOS.m_listASC_ResJob))
            {
                ASC_Order orderLink = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                    .Find(x => x.JOB_ID == job.JOB_LINK);

                if (orderLink == null)
                {
                    // order link is not created yet.
                    long curMaxOrderId = 0;
                    long.TryParse(order.ORDER_ID, out curMaxOrderId);

                    order.ORDER_LINK = Convert.ToString(Utility.CreateNewOrderId("BMS", m_dbDataSchedule, curMaxOrderId));
                }
                else
                {
                    // host order is created.
                    if (!string.IsNullOrWhiteSpace(orderLink.ORDER_LINK))
                    {
                        order.ORDER_ID = orderLink.ORDER_LINK;
                        order.ORDER_LINK = orderLink.ORDER_ID;
                    }
                }
            }
            else
            {
                order.ORDER_LINK = "";
            }
        }

        private string GetTosJobLink(Order_Base order)
        {
            if (order is ASC_Order)
            {
                ASC_Order ascOrder = (ASC_Order)order;

                var resJobLink = m_dbDataSchedule.m_DBData_TOS.m_listASC_ResJob
                    .Find(x => x.JOB_LINK == ascOrder.JOB_ID);

                if (resJobLink != null)
                {
                    return resJobLink.JOB_ID;
                }
            }
            else if (order is AGV_Order)
            {
                AGV_Order agvOrder = (AGV_Order)order;

                var resJobLink = m_dbDataSchedule.m_DBData_TOS.m_listAGV_ResJob
                    .Find(x => x.JOB_LINK == agvOrder.JOB_ID);

                if (resJobLink != null)
                {
                    return resJobLink.JOB_ID;
                }
            }

            return "";
        }

        //根据CommandVersion和OrderVersion的比对来判断任务是否有更新，如有，则更新至Order和TOS
        private bool UpdateOrderAndTaskByCommand(ASC_Order order, ASC_Command cmd)
        {
            bool bRet = false;

            bRet = UpdateTaskByCommand(cmd, order);

            if (bRet)
            {
                bRet = UpdateOrderVersionByCommand(order, cmd);
            }

            return bRet;
        }

        private bool UpdateOrderVersionByCommand(ASC_Order orderIn, ASC_Command cmdIn)
        {
            ASC_Order order = new ASC_Order();
            order.Copy(orderIn);

            order.COMMAND_ID = cmdIn.COMMAND_ID;
            order.COMMAND_VERSION = cmdIn.COMMAND_VERSION;
            order.CHE_ID = cmdIn.CHE_ID; // MS可能换设备执行任务

            //Utility.FillOrderToPosition(order, cmdIn);

            FillDataByCmd(order, cmdIn);

            order.DATETIME = DateTime.Now;

            bool bRet = UpdateOrderToDbAndCache(order, MethodBase.GetCurrentMethod().Name);

            return bRet;
        }

        // BMS需要返回给TOS的任务数据：
        // AGV卸船：COMPLETE,Execption_Complete 的时候tobay, tolane, totier, tobaytype, container_door_direction
        // AGV装船：COPLETE,Execption_Complete的时候tobaytype,tobay
        // lhv - ASC_ReqUpdateJob or ASC_Order
        private static void FillDataByCmd(dynamic lhv, ASC_Command cmd)
        {
            if (cmd.IsAgvLoad() && cmd.IsComplete())
            {
                lhv.TO_BAY_TYPE = cmd.TO_BAY_TYPE;
                lhv.TO_BAY = cmd.TO_BAY;
            }
            else if (cmd.IsAgvDisc() && cmd.IsComplete())
            {
                lhv.TO_BAY_TYPE = cmd.TO_BAY_TYPE;
                lhv.TO_BAY = cmd.TO_BAY;
                lhv.TO_LANE = cmd.TO_LANE;
                lhv.TO_TIER = cmd.TO_TIER;

                lhv.CONTAINER_DOOR_DIRECTION = cmd.CONTAINER_DOOR_DIRECTION;
            }
            else if (cmd.IsTruckTask() && (cmd.IsComplete() || cmd.IsReady()))
            {
                lhv.FROM_BAY_TYPE = cmd.FROM_BAY_TYPE;
                lhv.FROM_BAY = cmd.FROM_BAY;
                lhv.FROM_LANE = cmd.FROM_LANE;
                lhv.FROM_TIER = cmd.FROM_TIER;
                lhv.FROM_TRUCK_POS = cmd.FROM_TRUCK_POS;

                lhv.TO_BAY_TYPE = cmd.TO_BAY_TYPE;
                lhv.TO_BAY = cmd.TO_BAY;
                lhv.TO_LANE = cmd.TO_LANE;
                lhv.TO_TIER = cmd.TO_TIER;
                lhv.TO_TRUCK_POS = cmd.TO_TRUCK_POS;

                lhv.CONTAINER_DOOR_DIRECTION = cmd.CONTAINER_DOOR_DIRECTION;

                lhv.OPERATOR_ID = cmd.OPERATOR_ID;
            }
        }

        /// <summary>
        /// 根据Command JOB_ID，更新相应TOS任务
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="order"></param>
        /// <returns>true - 成功更新Task或不必更新Task； false - 更新Task但失败</returns>
        private bool UpdateTaskByCommand(ASC_Command cmd, ASC_Order order)
        {
            if (Helper.GetEnum(cmd.JOB_STATUS, TaskStatus.None) == TaskStatus.None)
                return true; //不必更新Task

            var req = CreateAscReqUpdateJob(cmd, order);

            bool bRet = DB_TOS.Instance.Update_ASC_Task(req, order.YARD_ID);

            Utility.Log("ASC", MethodBase.GetCurrentMethod().Name, bRet, "order:" + order + " cmd:" + cmd.JOB_STATUS);

            Utility.Log("ASC MS->DB_TOS", "DB_TOS.Update_ASC_Task()", bRet, "ASC_ReqUpdateJob: " + Utility.GetString(req));

            if (bRet)
            {
                var listAscTask = m_dbDataSchedule.m_DBData_TOS.m_listASC_Task;

                var taskOld = listAscTask.Find(x => x.Task.JOB_ID == order.JOB_ID && x.Task.YARD_ID == order.YARD_ID);

                if (taskOld != null)
                {
                    taskOld.TaskState = cmd.GetTosStatus();
                } 
            }

            return bRet;
        }

        private ASC_ReqUpdateJob CreateAscReqUpdateJob(ASC_Command cmd, ASC_Order order)
        {
            ASC_ReqUpdateJob req = new ASC_ReqUpdateJob
            {
                COMMAND_ID = cmd.COMMAND_ID,
                ORDER_ID = cmd.ORDER_ID,
                OPERATOR_ID = order.OPERATOR_ID,
                VERSION = cmd.VERSION,
                JOB_TYPE = order.JOB_TYPE,
                JOB_ID = order.JOB_ID,
                JOB_LINK = GetTosJobLink(order),
                CHE_ID = order.CHE_ID,
                YARD_ID = order.YARD_ID,

                FROM_TRUCK_TYPE = order.FROM_TRUCK_TYPE,
                FROM_TRUCK_ID = order.FROM_TRUCK_ID,
                FROM_TRUCK_POS = order.FROM_TRUCK_POS,
                FROM_BLOCK = order.FROM_BLOCK,
                FROM_BAY = order.FROM_BAY,
                FROM_BAY_TYPE = order.FROM_BAY_TYPE,
                FROM_LANE = order.FROM_LANE,
                FROM_TIER = order.FROM_TIER,
                FROM_RFID = order.FROM_RFID,
                FROMPALLETTYPE = order.FROM_PALLET_TYPE,

                TO_TRUCK_TYPE = cmd.TO_TRUCK_TYPE,
                TO_TRUCK_ID = cmd.TO_TRUCK_ID,
                TO_TRUCK_POS = cmd.TO_TRUCK_POS,
                TO_BLOCK = cmd.TO_BLOCK,
                TO_BAY_TYPE = cmd.TO_BAY_TYPE,
                TO_BAY = cmd.TO_BAY,
                TO_LANE = cmd.TO_LANE,
                TO_TIER = cmd.TO_TIER,
                TO_RFID = cmd.TO_RFID,
                TOPALLETTYPE = cmd.TO_PALLET_TYPE,

                CONTAINER_ID = order.CONTAINER_ID,
                CONTAINER_ISO = order.CONTAINER_ISO,
                CONTAINER_LENGTH = order.CONTAINER_LENGTH,
                CONTAINER_HEIGHT = order.CONTAINER_HEIGHT,
                CONTAINER_WEIGHT = order.CONTAINER_WEIGHT,
                CONTAINER_IS_EMPTY = order.CONTAINER_IS_EMPTY,
                CONTAINER_DOOR_DIRECTION = order.CONTAINER_DOOR_DIRECTION,

                // Command Status
                JOB_STATUS = cmd.JOB_STATUS,
                EXCEPTION_CODE = cmd.EXCEPTION_CODE,
                START_TIME = cmd.START_TIME,
                END_TIME = cmd.END_TIME,
                DATETIME = cmd.DATETIME
            };

            //req.RESERVE = cmd.RESERVE;
            //req.QUAY_ID = cmd.QUAY_ID;

            FillDataByCmd(req, cmd);

            return req;
        }

        private ASC_ReqUpdateJob CreateAscReqUpdateJob(ASC_ResJob job)
        {
            ASC_ReqUpdateJob req = new ASC_ReqUpdateJob
            {
                COMMAND_ID = job.COMMAND_ID,
                ORDER_ID = job.ORDER_ID,
                OPERATOR_ID = job.OPERATOR_ID,
                VERSION = job.VERSION,
                JOB_TYPE = job.JOB_TYPE,
                JOB_ID = job.JOB_ID,
                JOB_LINK = job.JOB_LINK,
                CHE_ID = job.CHE_ID,
                YARD_ID = job.YARD_ID,

                FROM_TRUCK_TYPE = job.FROM_TRUCK_TYPE,
                FROM_TRUCK_ID = job.FROM_TRUCK_ID,
                FROM_TRUCK_POS = job.FROM_TRUCK_POS,
                FROM_BLOCK = job.FROM_BLOCK,
                FROM_BAY = job.FROM_BAY,
                FROM_BAY_TYPE = job.FROM_BAY_TYPE,
                FROM_LANE = job.FROM_LANE,
                FROM_TIER = job.FROM_TIER,
                FROM_RFID = job.FROM_RFID,
                FROMPALLETTYPE = job.FROMPALLETTYPE,

                TO_TRUCK_TYPE = job.TO_TRUCK_TYPE,
                TO_TRUCK_ID = job.TO_TRUCK_ID,
                TO_TRUCK_POS = job.TO_TRUCK_POS,
                TO_BLOCK = job.TO_BLOCK,
                TO_BAY_TYPE = job.TO_BAY_TYPE,
                TO_BAY = job.TO_BAY,
                TO_LANE = job.TO_LANE,
                TO_TIER = job.TO_TIER,
                TO_RFID = job.TO_RFID,
                TOPALLETTYPE = job.TOPALLETTYPE,

                CONTAINER_ID = job.CONTAINER_ID,
                CONTAINER_ISO = job.CONTAINER_ISO,
                CONTAINER_LENGTH = job.CONTAINER_LENGTH,
                CONTAINER_HEIGHT = job.CONTAINER_HEIGHT,
                CONTAINER_WEIGHT = job.CONTAINER_WEIGHT,
                CONTAINER_IS_EMPTY = job.CONTAINER_IS_EMPTY,
                CONTAINER_DOOR_DIRECTION = job.CONTAINER_DOOR_DIRECTION,

                // Command Status
                JOB_STATUS = job.JOB_STATUS,
                //EXCEPTION_CODE = job.EXCEPTION_CODE,
                //START_TIME = job.START_TIME,
                //END_TIME = job.END_TIME,
                DATETIME = job.DATETIME
            };

            req.RESERVE = job.RESERVE;
            req.QUAY_ID = job.QUAY_ID;
            return req;
        }

        /// <summary>
        /// 回复Tos取消
        /// </summary>
        /// <param name="job"></param>
        /// <param name="cmd"></param>
        /// <param name="exceptionCode"></param>
        /// <returns></returns>
        private bool UpdateTaskByCancel(ASC_ResJob job, ASC_Command cmd, Exception_Code exceptionCode)
        {
            ASC_ReqUpdateJob req = CreateAscReqUpdateJob(job);

            req.EXCEPTION_CODE = ((int)exceptionCode).ToString();

            req.JOB_STATUS = TaskStatus.Cancel_OK.ToString();

            if (cmd != null)
            {
                req.COMMAND_ID = cmd.COMMAND_ID;
                req.ORDER_ID = cmd.ORDER_ID;
            }

            bool bRet = DB_TOS.Instance.Update_ASC_Task(req, req.YARD_ID);

            Utility.Log("ASC->DB_TOS", "DB_TOS.Update_ASC_Task()", bRet, "ASC_ReqUpdateJob: " + Utility.GetString(req));

            return bRet;
        }

        private bool InsertOrderToDbAndCache(ASC_Order order, string source)
        {
            order.DATETIME = DateTime.Now;

            bool bRet = false;

            try
            {
                bRet = DB_ECS.Instance.Insert_ASC_Order(order);
            }
            catch (Exception e)
            {
                Logger.ECSSchedule.Error("[ASC] " + source + ": " + order, e);
                throw;
            }

            Utility.Log("ASC", source, bRet, "Insert " + order);

            if (bRet)
            {
                m_dbDataSchedule.m_DBData_BMS.m_listASC_Order.Add(order);
            }

            return true;
        }

        private bool UpdateOrderToDbAndCache(ASC_Order order, string source)
        {
            order.DATETIME = DateTime.Now;

            bool bRet = DB_ECS.Instance.Update_ASC_Order(order);

            Utility.Log("ASC", source, bRet, "Update " + order);

            if (!bRet)
            {
                return false;
            }

            var listAscOrder = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order;

            var orderOld = listAscOrder.Find(x => x.ORDER_ID == order.ORDER_ID);

            if (orderOld == null)
            {
                listAscOrder.Add(order);
            }
            else
            {
                orderOld.Copy(order);
            }

            return true;
        }

        /// <summary>
        /// ORDER_VERSION加1，并根据order的CommandVersion判断是New还是Update
        /// </summary>
        private bool UpdateOrderToDbAndCacheEx(ASC_Order order, string source)
        {
            order.JOB_STATUS = ResJobStatus.Update.ToString();

            order.ORDER_VERSION = Utility.IncrementStringAsInt(order.ORDER_VERSION);

            return UpdateOrderToDbAndCache(order, source);
        }

        /// <summary>
        /// 响应JobManager_TOS的Event
        /// </summary>
        /// <param name="job">ASC_ResJob</param>
        /// <param name="e">NA</param>
        /// <returns>Update/Cancel  0：不允许更新，1：允许更新，2：未知. New    不关心返回值</returns>
        private int OnJobManagerEvent(ASC_ResJob job, EventArgs e)
        {
            lock (m_objAscScheduleLock)
            {
                if (job == null)
                {
                    Utility.Log("ASC", MethodBase.GetCurrentMethod().Name, "result=1", "job is null");
                    return (int)TosUpdateResult.Permit;
                }

                ResJobStatus jobStatus = Helper.GetEnum(job.JOB_STATUS, ResJobStatus.Unknown);

                ASC_Order order = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                        .Find(x => x.JOB_ID == job.JOB_ID && x.YARD_ID == job.YARD_ID);

                Utility.Log("ASC", MethodBase.GetCurrentMethod().Name + " " + jobStatus, "Order", order == null ? "null" : order.ToString());
                Utility.Log("ASC", MethodBase.GetCurrentMethod().Name + " " + jobStatus, "ResJob", Utility.GetString(job));

                bool bRet = false;
                if (jobStatus == ResJobStatus.New)
                {
                    bRet = NewOrderByResJob(job);
                }
                else if (jobStatus == ResJobStatus.Update)
                {
                    if (order == null)
                        return (int)TosUpdateResult.Permit;

                    bRet = UpdateOrderByResJob(order, job);
                }
                else if (jobStatus == ResJobStatus.Cancel)
                {
                    bRet = CancelOrderByTos(order, job);
                }
                else
                {
                    Utility.Log("ASC", MethodBase.GetCurrentMethod().Name, "result=2", order == null ? "null" : order.ToString());
                    return (int)TosUpdateResult.UnKnown;
                }

                int iRet = (int) (bRet ? TosUpdateResult.Permit : TosUpdateResult.NotPermit);

                Utility.Log("ASC", MethodBase.GetCurrentMethod().Name + " " + jobStatus, "result=" + iRet, order == null ? "null" : order.ToString());

                return iRet;
            }
        }

        private bool NewOrderByResJob(ASC_ResJob job)
        {
            bool bRet = true;

            JobType jobType = Helper.GetEnum(job.JOB_TYPE, JobType.UNKNOWN);
            if (jobType == JobType.SBLOCK)
            {
                string strCheId;
                bool bPermitHelpMode;
                if (IsRestow(job, out strCheId, out bPermitHelpMode))
                {
                    bRet = SendRestowOrder(job, strCheId, bPermitHelpMode);

                    return bRet;
                }
            }
            else
            {
                if (Utility.FindWiByJob(job, m_dbDataSchedule.m_DBData_TOS.m_listWORK_INSTRUCTION_STATUS) != null)
                {
                    // 任务已包含在WI中
                    return true;
                }

                Asc asc = SelectAscForJob(job);
                bRet = CreateAscOrder(job, asc);
            }

            return bRet;
        }

        /// <summary>
        /// 选择一个ASC执行任务
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        private Asc SelectAscForJob(ASC_ResJob job)
        {
            List<Asc> listAscOfTheBlock = m_listAsc.FindAll(x => x.BlockNo == job.YARD_ID && IsAscSchedulable(x));
            if (listAscOfTheBlock.Count <= 0)
                return null;

            bool isWaterSide = job.TaskSide() == AscTaskSide.WaterSide;

            Asc ascSameSide = listAscOfTheBlock.Find(x => x.IsWaterSide == isWaterSide);
            if (ascSameSide != null)
                return ascSameSide;

            Asc ascOtherSide = m_listAsc.Find(x => x.IsWaterSide != isWaterSide);
            if (ascOtherSide != null)
                return ascOtherSide;

            return m_listAsc[0];
        }

        /// <summary>
        /// helpmode使用方法：
        ///   协同作业即为捣箱任务选择另一个设备来帮忙作业，
        ///   协同作业目前只有在海测AGV装船，海测转场（出箱）任务时才有该功能，判断依据为：
        ///   海测的捣箱任务：当收到堆场内理箱任务时先判断该箱FROM位置的下层箱是否有 海测AGV装船，海测转场（出箱）任务，如果有则为海测的捣箱任务
        ///   查看视图的陆侧设备是否是协同模式，如果是则分配捣箱任务分配给陆侧设备作业
        /// 如果该堆场有集卡任务，则协调失效，不要协同。
        /// 
        /// 理箱导致的捣箱，不要协同
        /// </summary>
        /// <param name="job">SBLOCK任务</param>
        /// <param name="strCheId"></param>
        /// <param name="bPermitHelpMode"></param>
        /// <returns></returns>
        private bool IsRestow(ASC_ResJob job, out string strCheId, out bool bPermitHelpMode)
        {
            strCheId = null;
            bPermitHelpMode = false;

            int fromTierOfRestow;
            if (!int.TryParse(job.FROM_TIER, out fromTierOfRestow))
                return false;

            // 是否有任务
            List<ASC_Order> listOrderReady =
                m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                    .FindAll(x =>
                        x.FROM_BLOCK == job.FROM_BLOCK
                        && Utility.CompareStringAsInt(x.FROM_BAY, job.FROM_BAY) == 0
                        && Utility.CompareStringAsInt(x.FROM_LANE, job.FROM_LANE) == 0
                        && IsOrderReady(x)
                        //&& (x.GetJobType() == JobType.LOAD || x.GetJobType() == JobType.DBLOCK || x.GetJobType() == JobType.DELIVERY || x.GetJobType() == JobType.SBLOCK)
                        );

            if (listOrderReady.Count <= 0)
            {
                Utility.Log("ASC", MethodBase.GetCurrentMethod().Name, "Restow", "Warn! Received SBLOCK task, but the host task is not found! ASC Job: " + Utility.GetString(job));
                return false;
            }

            // 是否同步
            List<Block_Container> listBlockContainer = DB_ECS.Instance.Get_Block_Container(job.FROM_BLOCK, job.FROM_BAY, job.FROM_LANE);
            if (!listBlockContainer.Exists(x => x.CONTAINER_ID == job.CONTAINER_ID))
            {
                // 可能堆场箱与TOS未同步。GUI->ARMG->Sync container from TOS进行同步即可。
                Utility.Log("ASC", MethodBase.GetCurrentMethod().Name, "Restow", "Error! Perhaps block containers are not sync eith TOS.");
                return false;
            }
            // todo
            int highestTier = listBlockContainer.Select(x => x.TIER).Max();
            Block_Container bc = listBlockContainer.Find(x => x.TIER == highestTier);
            if (fromTierOfRestow != highestTier || job.CONTAINER_ID != bc.CONTAINER_ID)
            {
                Utility.Log("ASC", MethodBase.GetCurrentMethod().Name, "Restow", "Error! the tier is not highest or container id is not right.");
                //return false;
            }

            // 判断该SBLCOK任务的箱层数是否高于Ready任务的箱层数
            int maxReadyTaskTier = listOrderReady.Max(x => Utility.GetNumberFromString(x.FROM_TIER));
            if (fromTierOfRestow < maxReadyTaskTier)
                return false;

            // CHE_ID
            ASC_Order hostOrder = listOrderReady.Find(x =>
                Utility.GetNumberFromString(x.FROM_TIER) <= maxReadyTaskTier
                && !string.IsNullOrWhiteSpace(x.CHE_ID));

            if (hostOrder != null)
            {
                // 理箱导致的捣箱，不要协同
                bPermitHelpMode = hostOrder.GetJobType() != JobType.SBLOCK;

                strCheId = hostOrder.CHE_ID;

                Utility.Log("ASC", MethodBase.GetCurrentMethod().Name, "Restow", "restow che_id=" + strCheId + " host order: " + hostOrder);
            }

            return true;
        }

        private bool SendRestowOrder(ASC_ResJob job, string strCheId, bool bPermitHelpMode)
        {
            //指定ASC
            Asc asc = null;
            if (strCheId == null)
            {
                asc = m_listAsc.Find(x => x.BlockNo == job.FROM_BLOCK && !x.IsWaterSide && x.CanBeScheduled()) ??
                      m_listAsc.Find(x => x.BlockNo == job.FROM_BLOCK && x.IsWaterSide && x.CanBeScheduled());
            }
            else
            {
                asc = m_listAsc.Find(x => x.Status.CHE_ID == strCheId);
            }

            if (asc == null)
            {
                Logger.ECSSchedule.Error("[ASC] Can not find ASC to do Restow Order. che id=" + strCheId + ". ASC job: " + Utility.GetString(job));
                return false; // error
            }

            bool isTruckTaskComing = m_dbDataSchedule.m_DBData_TOS.m_listASC_Task
                .Exists(x => x.Task.YARD_ID == job.FROM_BLOCK
                           && Utility.IsTruckTask(x.Task.FROM_BAY_TYPE, x.Task.TO_BAY_TYPE)
                           && !Helper.IsTaskComplete(x.TaskState));

            Asc ascRestow = asc;
            if (!isTruckTaskComing)
            {
                if (bPermitHelpMode)
                {
                    Asc otherAsc =
                        m_listAsc.Find(x => x.Status.CHE_ID == Utility.GetAscCheId(job.FROM_BLOCK, !asc.IsWaterSide));

                    ascRestow = (otherAsc != null && otherAsc.HelpMode && IsAscSchedulable(otherAsc)) ? otherAsc : asc;
                }
            }

            bool bRet = CreateAscOrder(job, ascRestow);
            
            return bRet;
        }

        private bool UpdateOrderByResJob(ASC_Order orderIn, ASC_ResJob job)
        {
            if (orderIn == null || job == null)
                return false;

            bool bRet = false;

            ASC_Order order = new ASC_Order();
            order.Copy(orderIn);

            Utility.FillOrderFromPosition(order, job);
            Utility.FillOrderToPosition(order, job);
            Utility.FillOrderContainer(order, job);
            Utility.FillOrderMiscellaneous(order, job);

            order.DATETIME = DateTime.Now;

            bRet = UpdateOrderToDbAndCacheEx(order, MethodBase.GetCurrentMethod().Name);

            return bRet;
        }

        private bool CancelOrderByTos(ASC_Order orderIn, ASC_ResJob job)
        {
            bool bRet = false;

            if (orderIn == null)
            {
                Logger.ECSSchedule.Info("[ASC] CancelOrder order is not start.");

                bRet = UpdateTaskByCancel(job, null, Exception_Code.CancelCode_NoStart);
            }
            else
            {
                ASC_Command cmd = m_dbDataSchedule.m_DBData_BMS.m_listASC_Command.Find(x => x.ORDER_ID == orderIn.ORDER_ID);
                if (cmd != null && cmd.IsComplete())
                {
                    Logger.ECSSchedule.Info("[ASC] CancelOrder order is complete.");

                    bRet = UpdateTaskByCancel(job, cmd, Exception_Code.CancelCode_AlmostComplete);
                }
                else
                {
                    Logger.ECSSchedule.Info("[ASC] CancelOrder order is working.");

                    ASC_Order order = new ASC_Order();
                    order.Copy(orderIn);

                    order.JOB_STATUS = ResJobStatus.Cancel.ToString();

                    order.ORDER_VERSION = Utility.IncrementStringAsInt(order.ORDER_VERSION);

                    bRet = UpdateOrderToDbAndCache(order, MethodBase.GetCurrentMethod().Name);
                }
            }

            return bRet;
        }

        private void LogScheduleSnapshot()
        {
            foreach (Asc asc in m_listAsc)
            {
                var listOrderInDoing = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                    .FindAll(x => x.CHE_ID == asc.Status.CHE_ID && IsOrderInWorking(x));
                string orderLog = listOrderInDoing.Count == 0 ? " no order" : " order count = " + listOrderInDoing.Count + ", order 1: " + listOrderInDoing[0];

                Logger.ScheduleSnapshot.Debug(asc + orderLog);
            }

            m_laneCoordinator.LogSnapshot(m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order, m_dbDataSchedule.m_DBData_BMS.m_listASC_Order);
        }
    }
}
