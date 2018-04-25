//#define ASSIGN_PB_BY_SCHD

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
    public class Schedule_AGV
    {
        private static Schedule_AGV s_instance;
        public static Schedule_AGV Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new Schedule_AGV();
                }
                return s_instance;
            }
        }

        private Object m_objAgvScheduleLock = new object();
        private DBData_Schedule m_dbDataSchedule;

        private LanePlan m_lanePlan;
        private List<Agv> m_listAgv;
        private List<Sts> m_listSts;

        private bool m_bWriteLog = true; // 避免重复写log
        private bool m_bAddedJobManagerEvent;

        public bool Start()
        {
            m_bAddedJobManagerEvent = false;
            return true;
        }

        public void Stop()
        {
            m_bAddedJobManagerEvent = false;
            DB_TOS.Instance.AGV_JobManagerScheduleEvent -= OnJobManagerEvent;
        }

        //根据装卸需求生成Order,状态回应.
        public bool Schedule(ref DBData_Schedule dbDataSchedule, List<WORK_INSTRUCTION_STATUS> listWiTopLogicSorted, LanePlan lanePlan)
        {
            lock (m_objAgvScheduleLock)
            {
                m_dbDataSchedule = dbDataSchedule;

                if (!m_bAddedJobManagerEvent)
                {
                    DB_TOS.Instance.AGV_JobManagerScheduleEvent += OnJobManagerEvent;
                    m_bAddedJobManagerEvent = true;
                }

                m_lanePlan = lanePlan;

                InitCheInstance();

                LogScheduleSnapshot(listWiTopLogicSorted);

                CheckCommandUpdate();

                // Reposition Order
                SendRepositionOrderByResJob();

                // 处理CompleteFrom
                UpdateCompleteFromOneOfOrderSetFromLane();
                
                // 处理CompleteFrom
                UpdateCompleteFromOrderSetTo();

                // 处理Complete
                UpdateCompleteOneOfOrderSetToLane();

                //选出可以安排任务的AGV
                List<Agv> listAgvSchedulable = m_listAgv.FindAll(IsAgvSchedulable);
                if (listAgvSchedulable.Count <= 0)
                {
                    Logger.ECSSchedule.Error("[AGV] No Schedulable AGV!");
                    return false;
                }

                List<AGV_Task> listNotSentAgvTask =
                    m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task
                        .FindAll(x => !Utility.IsTaskBind(x.TaskState)
                                      && !IsTaskSent(x));

                var listAgvUnAssigned = GetListAgvUnAssigned(listNotSentAgvTask, listAgvSchedulable);

                SendTaskAgvAssignedByTos(listNotSentAgvTask);

                SendStsAscDoingWsTask(listNotSentAgvTask, listAgvUnAssigned);

                SendWiTask(listWiTopLogicSorted, listAgvUnAssigned);

                SendOtherTask(listNotSentAgvTask, listAgvUnAssigned);

                ////1.选出当前可做的任务列表
                //var listTaskSetAgvCanDo = SelectTaskSetAgvCanDo(listWiTopLogicSorted);
                //if (listTaskSetAgvCanDo.Count <= 0)
                //{
                //    Logger.ECSSchedule.Info("[AGV] No task for AGV to do now.");
                //    return false;
                //}

                ////2.选车匹配：针对可做任务和可用或即将可用的AGV，根据AGV完成任务所需时间的估算值，
                ////  对哪个AGV做哪个任务做一个预分配
                ////3.下发任务到ECS
                //SendTaskAgvCanDo(listAgvSchedulable, listTaskSetAgvCanDo);
            }

            return true;
        }

        private static List<Agv> GetListAgvUnAssigned(List<AGV_Task> listNotSentAgvTask, List<Agv> listAgvSchedulable)
        {
            List<AGV_Task> listTaskAgvAssignedByTos =
                listNotSentAgvTask
                    .FindAll(x => Utility.IsAgvTaskAssignedByTos(x.Task));

            List<Agv> listAgvAssigned = listAgvSchedulable
                .FindAll(
                    agv =>
                        listTaskAgvAssignedByTos.Exists(task => task.Task.CHE_ID == agv.Status.CHE_ID));

            List<Agv> listAgvUnAssigned =
                listAgvSchedulable.FindAll(x => !listAgvAssigned.Exists(agv => agv.Status.CHE_ID == x.Status.CHE_ID));
            return listAgvUnAssigned;
        }

        private void InitCheInstance()
        {
            m_listAgv = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Status.Select(agvStatus => new Agv(agvStatus)).ToList();

            m_listSts = m_dbDataSchedule.m_DBData_STSMS.m_listSTS_Status.Select(stsStatus => new Sts(stsStatus)).ToList();
        }

        /// <summary>
        /// 打印日志
        /// </summary>
        /// <param name="listWiTopLogicSorted"></param>
        private void LogScheduleSnapshot(List<WORK_INSTRUCTION_STATUS> listWiTopLogicSorted)
        {
            LogWi(listWiTopLogicSorted);

            LogAgv();

            Logger.ScheduleSnapshot.Debug("STS count: " + m_listSts.Count);
        }

        private void LogWi(List<WORK_INSTRUCTION_STATUS> listWiTopLogicSorted)
        {
            if (listWiTopLogicSorted == null || listWiTopLogicSorted.Count <= 0)
            {
                Logger.ScheduleSnapshot.Debug("List of TopLogicSorted workinstruction is empty.***************************");
            }
            else
            {
                Logger.ScheduleSnapshot.Debug("List of TopLogicSorted workinstruction:***************************");
                for (int i = 0; i < listWiTopLogicSorted.Count; ++i)
                {
                    Logger.ScheduleSnapshot.Debug(i + " " + listWiTopLogicSorted[i]);

                    ASC_ResJob ascResJob =
                        m_dbDataSchedule.m_DBData_TOS.m_listASC_ResJob.Find(
                            x => x.JOB_ID == listWiTopLogicSorted[i].JOB_ID);

                    Logger.ScheduleSnapshot.Debug("  ASC Job:" + (ascResJob != null ? Utility.GetString(ascResJob) : " no job"));
                }
            }
        }

        private void LogAgv()
        {
            foreach (Agv agv in m_listAgv)
            {
                var listOrderInDoing = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                    .FindAll(x => x.CHE_ID == agv.Status.CHE_ID && IsOrderInWorking(x));
                string orderLog = listOrderInDoing.Count == 0
                    ? " no order"
                    : " order count = " + listOrderInDoing.Count + ", order 1: " + listOrderInDoing[0];

                Logger.ScheduleSnapshot.Debug(agv + orderLog);
            }
        }

        private void LogSts()
        {
            foreach (Sts sts in m_listSts)
            {
                Logger.ScheduleSnapshot.Debug(sts);
            }
        }

        // 根据Command的Command_Status更新Order和TOS
        private void CheckCommandUpdate()
        {
            var listAgvCommand = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command;
            var listAgvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order;

            foreach (AGV_Command cmd in listAgvCommand)
            {
                AGV_Order order = listAgvOrder.Find(x => x.ORDER_ID == cmd.ORDER_ID);

                // 检测MS命令状态是否有改变
                if (!Utility.IsCommandChanged(cmd, order))
                    continue;

                var cmdStatus = cmd.GetCmdStatus();

                bool bRet = true;
                switch (cmdStatus)
                {
                    case TaskStatus.Complete:
                    case TaskStatus.Exception_Complete:
                        bRet = OnCommandComplete(order, cmd);
                        break;
                    case TaskStatus.Complete_From:
                        bRet = OnCommandCompleteFrom(order, cmd);
                        break;
                    default:
                        bRet = true;
                        break;
                }

                // 更新Order和Tos任务状态
                if (bRet)
                {
                    UpdateOrderAndTaskByCommand(order, cmd);
                }
            }
        }

        /// <summary>
        /// 计算双箱且送箱到不同目的地的link order的目的地，并更新link order到DB。
        /// </summary>
        /// <param name="order">已完成的order</param>
        /// <param name="cmd"></param>
        /// <returns>true - 成功处理link order</returns>
        private bool OnCommandComplete(AGV_Order order, AGV_Command cmd)
        {
            Utility.Log("AGV", "Order is Completed", "Order", order.ToString());

            Schedule_ASC.Instance.UnpairOrder(cmd);

            if (string.IsNullOrWhiteSpace(order.GetOrderLink())
                || order.IsSameTo()
                || order.GetJobType() == JobType.REPOSITION)
            {
                return true;
            }

            // 处理To link
            AGV_Order orderLinkIn = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                .Find(x => x.ORDER_ID == order.GetOrderLink());

            if (orderLinkIn == null)
            {
                Logger.ECSSchedule.Error("[AGV] order's OrderLink field is not empty, but the link order is not found.");
                return false; //error
            }

            ResJobStatus jobStatus = Helper.GetEnum(orderLinkIn.JOB_STATUS, ResJobStatus.Unknown);
            if (jobStatus == ResJobStatus.Cancel)
            {
                Logger.ECSSchedule.Info("[AGV] order link is canceled. order link: " + orderLinkIn);
                return false;
            }

            AGV_Command cmdLink = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command
                .Find(x => x.ORDER_ID == orderLinkIn.ORDER_ID);

            if (cmdLink == null)
                return false; //error

            // Link是否需要处理？
            if (cmdLink.IsComplete())
                return true;

            AGV_Order orderLink = new AGV_Order();
            orderLink.Copy(orderLinkIn);

            // 从Tos Job中得到原始To
            if (!FillOrderToPositionByResJob(orderLink))
                return false; // error

            bool bUpdatedDb = false;

            // 指定送箱目的车道并更新Order到DB
            JobType jobType = orderLink.GetJobType();
            if (jobType == JobType.DISC || jobType == JobType.DBLOCK)
            {
                bUpdatedDb = UpdateOrderWsLane(orderLink);
            }

            if (!bUpdatedDb)
            {
                bUpdatedDb = UpdateOrderToDbAndCacheEx(orderLink, MethodBase.GetCurrentMethod().Name);
            }

            Utility.Log("AGV", "Update orderLink's To", bUpdatedDb, orderLinkIn.ToString());

            return true;
        }

        /// <summary>
        ///Command指示Order已CompleteFrom时，对order进行相应处理 
        /// </summary>
        /// <param name="orderIn"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private bool OnCommandCompleteFrom(AGV_Order orderIn, AGV_Command cmd)
        {
            Utility.Log("AGV", "Order is CompleteFrom", "Order", orderIn.ToString() + "; Cmd:" + cmd);

            AGV_Order order = new AGV_Order();
            order.Copy(orderIn);

            if (Utility.IsAnyToAssigned(order) && !string.IsNullOrWhiteSpace(order.CONTAINER_ID))
            {
                Logger.ECSSchedule.Warn("[AGV] The To and Container has been filled! This order has been processed.");
                return true;
            }

            //OrderLink已经处理过，不要重复处理
            if (!string.IsNullOrWhiteSpace(order.GetOrderLink()))
            {
                Logger.ECSSchedule.Warn("[AGV] The link has been filled! This order has been processed.");
                return true;
            }

            if (Utility.IsContainerIdEmpty(cmd.CONTAINER_ID))
            {
                Logger.ECSSchedule.Error("[AGV] Cmd is CompleteFrom, but Container ID is empty! Cmd: " + cmd);
                return false; // error
            }

            var listAgvTask = m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task;
            var listAgvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order;

            //
            bool bRet = false;
            AGV_Task actualAgvTask = FindWorkingAgvTaskByContainer(cmd);
            if (actualAgvTask == null)
            {
                Logger.ECSSchedule.Error(string.Format("[AGV] Can not find task by Actual Container ID: {0}, Actual Cmd: {1}", cmd.CONTAINER_ID, cmd));
                return false;
            }

            // 校验MS填充到cmd的箱号是否有误
            AGV_Order orderByContainer = FindWorkingAgvOrderByContainer(cmd);
            if (orderByContainer != null)
            {
                // MS重复给CompleteFrom，或MS填充到Cmd的箱号发生错误（填了一个已CompleteFrom的Order的箱号）
                Logger.ECSSchedule.Error(string.Format("[AGV] duplicate cmd: {0}, existing order: {1}", cmd, orderByContainer));
                return false; // 终止处理
            }

            // 得到link的Order Id
            string strOldJobId = order.JOB_ID;
            string strActualJobId = actualAgvTask.Task.JOB_ID;
            string strActualJobLink = actualAgvTask.Task.JOB_LINK;

            // 处理Link关系
            AGV_Order orderLink = null;
            if (!string.IsNullOrWhiteSpace(strActualJobLink))
            {
                var taskLink = listAgvTask.Find(x => x.Task.JOB_ID == strActualJobLink);
                if (taskLink != null &&
                    Helper.GetEnum(taskLink.Task.JOB_STATUS, ResJobStatus.Unknown) != ResJobStatus.Cancel)
                {
                    // orderLink箱号要填
                    orderLink = Utility.CreateAgvOrder(taskLink.Task, m_dbDataSchedule, false, true);
                }
            }

            // Order replaced。orderReplaced的JobId被用到order中了，相当于两者的JobId互相交换。
            AGV_Order orderReplaced =
                listAgvOrder.Find(x => x.ORDER_ID != order.ORDER_ID
                                    && x.GetJobType() == order.GetJobType()
                                    && (
                                        (order.GetJobType() == JobType.DISC
                                         && (x.JOB_ID == strActualJobId || x.JOB_ID == strActualJobLink))
                                        ||
                                        (order.GetJobType() != JobType.DISC
                                         && x.JOB_ID == strActualJobId)
                                       )
                                    && IsOrderInFirstHalf(x));

            if (orderReplaced != null)
            {
                string jobReplaced = orderReplaced.JOB_ID;
                orderReplaced.JOB_ID = strOldJobId;

                AGV_ResJob resJobOfReplaced =
                    m_dbDataSchedule.m_DBData_TOS.m_listAGV_ResJob.Find(x => x.JOB_ID == orderReplaced.JOB_ID);
                if (resJobOfReplaced != null)
                {
                    Utility.FillOrderFromPosition(orderReplaced, resJobOfReplaced);
                    Utility.FillOrderMiscellaneous(orderReplaced, resJobOfReplaced);
                }

                bool bReplaced = UpdateOrderToDbAndCacheEx(orderReplaced, MethodBase.GetCurrentMethod().Name);

                Utility.Log("AGV", MethodBase.GetCurrentMethod().Name, bReplaced, string.Format(" Job {0} of orderReplaced is replaced by {1}. New orderReplaced: {2}", jobReplaced, strOldJobId, orderReplaced));
            }

            // 处理主Order， 更新内容
            order.JOB_ID = actualAgvTask.Task.JOB_ID;
            if (orderLink == null)
            {
                Utility.FillOrderToPosition(order, actualAgvTask.Task);
            }
            Utility.FillOrderContainer(order, actualAgvTask.Task);
            Utility.FillOrderMiscellaneous(order, actualAgvTask.Task);

            if (orderLink != null)
            {
                // 指定AGV
                orderLink.CHE_ID = order.CHE_ID;

                // 指定Carry_Link
                orderLink.CARRY_LINK = order.ORDER_ID;
                order.CARRY_LINK = orderLink.ORDER_ID;

                // 指定From_Link
                if (Utility.IsOneLiftFrom(order, orderLink))
                {
                    //一吊
                    order.FROM_LINK = orderLink.ORDER_ID;
                    orderLink.FROM_LINK = order.ORDER_ID;
                }
            }

            // 在补发orderLink之前更新order
            bRet = UpdateOrderToDbAndCacheEx(order, MethodBase.GetCurrentMethod().Name);

            Utility.Log("AGV", "Updated Order's To OnCompleteFrom", bRet, orderIn.ToString());

            if (bRet && orderLink != null)
            {
                bool isFilledFromLane = false;
                // 填充orderLink的FromLane
                if (orderLink.IsSameFrom() || Utility.IsSameFromBlockOrQc(order, orderLink))
                {
                    // 双箱一吊，或同位置收箱
                    Utility.FillOrderFromLane(orderLink, order);
                    isFilledFromLane = true;
                }

                bRet = InsertOrderToDbAndCache(orderLink, MethodBase.GetCurrentMethod().Name);

                Utility.Log("AGV", "Inserted orderLink OnCompleteFrom", bRet, orderLink.ToString());

                // 更新orderLink的FromLane
                if (bRet && !isFilledFromLane)
                {
                    // 双箱Load且非一吊，且不同场收箱
                    bool bUpdatedWsLane = UpdateOrderWsLane(orderLink);

                    Utility.Log("AGV", "Updated orderLink Ws Lane OnCompleteFrom", bUpdatedWsLane, orderLink.ToString());
                }
            }

            return bRet;
        }

        /// <summary>
        ///双箱任务的处理较为复杂，需根据不同情况进行单独处理
        ///要根据FROM_LINK,TO_LINK,FROM,TO等字段的具体值进行相应处理
        /// </summary>
        private void UpdateCompleteFromOneOfOrderSetFromLane()
        {
            var listAgvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order;

            List<TaskSet<AGV_Order>> listOrderSet = TaskSet<AGV_Order>.GetTaskSetList(listAgvOrder);

            // 双箱收箱两吊，且FromLane为空。
            List<TaskSet<AGV_Order>> listOrderSetDiffFrom =
                listOrderSet.FindAll(
                    x => x.TaskList.Count > 1 
                         && x.TaskList.TrueForAll(y => !y.IsSameFrom())
                         && x.TaskList.Exists(y => string.IsNullOrWhiteSpace(y.FROM_LANE)));

            foreach (var orderSet in listOrderSetDiffFrom)
            {
                AGV_Order order = null;

                TaskStatus cmdStatus0 = GetOrderStatus(orderSet.TaskList[0]);
                TaskStatus cmdStatus1 = GetOrderStatus(orderSet.TaskList[1]);

                if (Helper.IsTaskCompleteFrom(cmdStatus0)
                  && !Utility.IsTaskBind(cmdStatus1))
                {
                    order = orderSet.TaskList[1];
                }
                else if (Helper.IsTaskCompleteFrom(cmdStatus1)
                       && !Utility.IsTaskBind(cmdStatus0))
                {
                    order = orderSet.TaskList[0];
                }
                else
                {
                    continue;
                }

                //
                bool bUpdatedWsLane = UpdateOrderWsLane(order);

                Utility.Log("AGV", "Updated order's Ws Lane", bUpdatedWsLane, "The other of twin order is CompleteFrom, order: " + order.ToString());
            }
        }

        /// <summary>
        ///双箱任务的处理较为复杂，需根据不同情况进行单独处理
        ///要根据FROM_LINK,TO_LINK,FROM,TO等字段的具体值进行相应处理
        /// 更新一个任务的ToLane后，再判断是否为To一吊，如果是，则更新To_Link。
        ///去同一场收箱或送箱需指定FROM_LINK或TO_LINK。
        /// </summary>
        private void UpdateCompleteFromOrderSetTo()
        {
            var listAgvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order;

            List<TaskSet<AGV_Order>> listOrderSet = TaskSet<AGV_Order>.GetTaskSetList(listAgvOrder);

            List<TaskSet<AGV_Order>> listOrderSetCompleteFrom =
                listOrderSet.FindAll(x => x.TaskList.TrueForAll(IsOrderCompleteFrom));

            foreach (var orderSet in listOrderSetCompleteFrom)
            {
                UpdateCompleteFromOrderSetTo(orderSet);
            }
        }

        /// <summary>
        /// 已CompleteFrom的Order，指定单箱或双箱的ToLane。双箱：如果是一吊则同时指定order和orderLink的ToLane，如果是两吊，则只指定一个。
        /// </summary>
        /// <param name="orderSet"></param>
        private void UpdateCompleteFromOrderSetTo(TaskSet<AGV_Order> orderSet)
        {
            AGV_Order order0 = orderSet.TaskList[0];

            // 1. 单箱
            if (orderSet.TaskList.Count == 1)
            {
                if (UpdateOrderWsLane(order0))
                {
                    Utility.Log("AGV", "Updated order's To Lane", true, "Single container: " + order0.ToString());
                }
                return;
            }

            // 2. 双箱
            if (orderSet.TaskList.Find(Utility.IsFinalToAssigned) != null)
                return; // 存在一个order已指定ToLane

            JobType jobType = order0.GetJobType();

            // 3. 双箱 两个Order的To都待定
            AGV_Order orderWithTo = null;
            if (jobType == JobType.DISC)
            {
                orderWithTo = orderSet.TaskList.Find(Utility.IsTempToAssigned);
            }

            AGV_Order orderTemp = new AGV_Order();
            if (orderWithTo != null)
            {
                orderTemp.Copy(orderWithTo);
            }
            else
            {
                orderTemp.Copy(order0);

                // 初次指定To
                FillOrderToPositionByResJob(orderTemp);
            }

            AGV_Order orderLink = orderSet.TaskList.Find(x => x.ORDER_ID == orderTemp.GetOrderLink());
            if (orderLink == null)
                return; //error
            AGV_ResJob resJobLink = m_dbDataSchedule.m_DBData_TOS.m_listAGV_ResJob.Find(x => x.JOB_ID == orderLink.JOB_ID);

            if (!Utility.IsSameToBlockOrQc(orderTemp, resJobLink))
            {
                // 4.1 双箱不同场或QC
                if(UpdateOrderWsLane(orderTemp))
                {
                    Utility.Log("AGV", "Updated order's To Lane", true, "The To is not one lift: " + orderTemp.ToString());
                }
                return;
            }

            // 4.2 双箱去同场或QC 。
            // 一个order的字段值要一次Update到位，否则VMS无法处理。

            bool bUpdated;
            if (jobType == JobType.LOAD)
            {
                UpdateToLink(ref orderTemp, orderLink); // 一吊

                UpdateOrderToDbAndCacheEx(orderTemp, MethodBase.GetCurrentMethod().Name);
            }
            else if (jobType == JobType.DISC || jobType == JobType.DBLOCK)
            {
                // 尝试指定ToLane
                LaneInfoEx toLane = null;
                if (TryAcquireOrderWstpToLane(orderTemp, out toLane))
                {
                    bUpdated = true;
                    if (Utility.IsMateLane(orderTemp.TO_LANE))
                    {  
                        bUpdated = UpdateToLink(ref orderTemp, orderLink); // 一吊
                    }

                    if (bUpdated)
                    {
                        if (UpdateOrderToDbAndCacheEx(orderTemp, MethodBase.GetCurrentMethod().Name)
                            && toLane != null)
                        {
                            m_lanePlan.AddLaneInUsing(toLane, orderTemp.ORDER_ID);
                        }
                    }
                }
                else
                {
                    if (UpdateOrderWsLane(orderTemp))
                    {
                        Utility.Log("AGV", "Updated order's To Lane", true, "It is same block: " + orderTemp.ToString());
                    }
                }
            }
        }

        // 双箱To一吊
        private bool UpdateToLink(ref AGV_Order order, AGV_Order orderLink)
        {
            var orderLinkTemp = new AGV_Order();
            orderLinkTemp.Copy(orderLink);

            order.TO_LINK = orderLink.ORDER_ID;
            orderLinkTemp.TO_LINK = order.ORDER_ID;

            // 根据Tos Job填充OrderLink的To
            FillOrderToPositionByResJob(orderLinkTemp);

            // 更新OrderLink的ToLane
            Utility.FillOrderToLane(orderLinkTemp, order);

            var bUpdatedTo = UpdateOrderToDbAndCacheEx(orderLinkTemp, MethodBase.GetCurrentMethod().Name);

            Utility.Log("AGV", "Updated orderLink's To", bUpdatedTo, orderLinkTemp.ToString());

            return bUpdatedTo;
        }

        /// <summary>
        ///将已CompleteFrom的Order中的To赋值到另一个未完成的OrderLink。 
        /// </summary>
        private void UpdateCompleteOneOfOrderSetToLane()
        {
            var listAgvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order;
            List<TaskSet<AGV_Order>> listTaskSet = TaskSet<AGV_Order>.GetTaskSetList(listAgvOrder);

            foreach (var taskSet in listTaskSet)
            {
                if (taskSet.TaskList.Count != 2)
                    continue;

                var jobType = taskSet.TaskList[0].GetJobType();
                if (jobType != JobType.DISC && jobType != JobType.DBLOCK)
                    continue;

                AGV_Order order = null;

                TaskStatus taskStatus0 = GetOrderStatus(taskSet.TaskList[0]);
                TaskStatus taskStatus1 = GetOrderStatus(taskSet.TaskList[1]);

                if (Helper.IsTaskComplete(taskStatus0)
                  && Helper.IsTaskCompleteFrom(taskStatus1))
                {
                    order = taskSet.TaskList[1];
                }
                else if (Helper.IsTaskComplete(taskStatus1)
                       && Helper.IsTaskCompleteFrom(taskStatus0))
                {
                    order = taskSet.TaskList[0];
                }
                else
                {
                    continue;
                }

                if (order != null)
                {
                    bool bUpdatedWsLane = UpdateOrderWsLane(order);

                    Utility.Log("AGV", "Updated order's To Lane", bUpdatedWsLane, "The other of twin order is Completed, order: " + order.ToString());
                }
            }
        }

        /// <summary>
        ///为AGV任务分配合适的车道 
        ///根据需要产生RePosition任务。
        /// </summary>
        /// <param name="orderIn"></param>
        /// <returns>true - 更新了WsLane到DB， false - 未更新DB</returns>
        private bool UpdateOrderWsLane(AGV_Order orderIn)
        {
            bool bRet = false;

            AGV_STATUS agvStatus = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Status
                .Find(x => x.CHE_ID == orderIn.CHE_ID);
            if (agvStatus == null)
                return false;

            AGV_Command cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command.Find(x => x.ORDER_ID == orderIn.ORDER_ID);
            AGV_Order_Type agvOrderType = Utility.GetAgvOrderType(orderIn, cmd);

            string targetBlock;
            if (!NeedAssignWstpLane(orderIn, agvOrderType, out targetBlock))
                return false;

            AGV_Order order = new AGV_Order();
            order.Copy(orderIn);

            bool canAssignToMateLane = CanAssignToMateLane(order, agvOrderType);

            ReserveLaneKind reserveLaneKind = CalcReserveLaneForAsc(order, targetBlock);

            var targetIdleWstpLane = 
                m_lanePlan.PreAssignLane(agvStatus, targetBlock, BayType.WS.ToString(), canAssignToMateLane, reserveLaneKind);

            if (targetIdleWstpLane != null)
            {
                // 有空闲WSTP车道，go
                if (agvOrderType == AGV_Order_Type.DelieverToWstp)
                {
                    order.TO_BAY_TYPE = BayType.WS.ToString();
                    order.TO_LANE = Convert.ToString(targetIdleWstpLane.GetLaneNo());
                }
                else if (agvOrderType == AGV_Order_Type.ReceiveFromWstp)
                {
                    order.FROM_LANE = Convert.ToString(targetIdleWstpLane.GetLaneNo());
                }
                else
                {
                    return false;
                }

                if (UpdateOrderToDbAndCacheEx(order, "UpdateAgvOrderLane WS"))
                {
                    m_lanePlan.AddLaneInUsing(targetIdleWstpLane, order.ORDER_ID);
                    bRet = true;
                }
            }
            else
            {
                // 无空闲WSTP车道
                if (agvOrderType == AGV_Order_Type.DelieverToWstp)
                {
                    bRet = RepositionForDelieverToWstp(targetBlock, ref order);
                }
                else if (agvOrderType == AGV_Order_Type.ReceiveFromWstp)
                {
                    bRet = false;
                    RepositionForReceiveFromWstp(targetBlock, order);
                }
            }

            return bRet;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetBlock"></param>
        /// <param name="order"></param>
        /// <returns>true - 更新了order到DB， false - 未更新DB</returns>
        private bool RepositionForDelieverToWstp(string targetBlock, ref AGV_Order order)
        {
            bool bOrderUpdated = false;

            bool bSendReposition = false;
            if (NeedRepositionAgvOnWstp(targetBlock))
            {
                // 驱赶WSTP上的占道空闲AGV
                bSendReposition = TryRepositionIdleAgvOnWstp(order.TO_BLOCK);
            }

            // WSTP被占，先停到PB空闲车道。

            // DBlock任务不要经停PB。
            // Load任务由VMS控制经停PB。
            if (Helper.GetEnum(order.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC
#if ASSIGN_PB_BY_SCHD
#else
                && Helper.GetEnum(order.TO_BAY_TYPE, BayType.UnKnown) != BayType.PB
#endif
                && string.IsNullOrWhiteSpace(order.TO_LANE))
            {
                // Disc双箱到不同场时，一Order已完成，一OrderLink CompleteFrom时，如果已成功发送驱赶任务，则不要停到PB
                if (!string.IsNullOrWhiteSpace(order.FROM_LINK)
                    && string.IsNullOrWhiteSpace(order.TO_LINK)
                    && bSendReposition)
                {
                    return false;
                }

#if ASSIGN_PB_BY_SCHD
                var pbLaneInfo = m_lanePlan.GetAnyIdlePbLanes(order.QUAY_ID, false);

                if (pbLaneInfo != null)
                {
                    order.TO_BAY_TYPE = BayType.PB.ToString();
                    order.TO_LANE = Convert.ToString(pbLaneInfo.GetLaneNo());

                    if (UpdateOrderToDbAndCacheEx(order, "UpdateAgvOrderLane PB"))
                    {
                        m_lanePlan.AddLaneInUsing(pbLaneInfo, order.ORDER_ID);
                        bOrderUpdated = true;
                    }
                }
#else
                order.TO_BAY_TYPE = BayType.PB.ToString();

                if (UpdateOrderToDbAndCacheEx(order, "UpdateAgvOrderLane PB"))
                {
                    bOrderUpdated = true;
                }
#endif
            }

            return bOrderUpdated;
        }

        /// <summary>
        /// 双箱不同场收箱，收第二个箱时可能需要驱赶
        /// </summary>
        /// <param name="targetBlock"></param>
        /// <param name="order"></param>
        private void RepositionForReceiveFromWstp(string targetBlock, AGV_Order order)
        {
            if (!string.IsNullOrWhiteSpace(order.GetOrderLink())
                && !order.IsSameFrom())
            {
                // 单箱，或双箱同场，不需驱赶。由选车处理占道空闲AGV的任务分派。
                return;
            }

            if (NeedRepositionAgvOnWstp(targetBlock))
            {
                // 驱赶WSTP上的占道空闲AGV
                TryRepositionIdleAgvOnWstp(order.FROM_BLOCK);
            }
        }

        /// <summary>
        /// 为此Block的ASC正在执行的Order保留一个WSTP车道。
        /// ASC在WS车道没有时，为提高效率，也可发送， 所以为AGV分配WSTP时，需进行控制。
        /// </summary>
        /// <param name="order"></param>
        /// <param name="targetBlock"></param>
        /// <returns></returns>
        private ReserveLaneKind CalcReserveLaneForAsc(AGV_Order order, string targetBlock)
        {
            if (!IsToFinalBlock(order))
            {
                return ReserveLaneKind.NonMate;
            }

            ASC_Order ascOrder = FindWsAscOrderInDoingByBlcok(targetBlock);

            if (ascOrder == null)
                return ReserveLaneKind.None;

            if (order.JOB_ID == ascOrder.JOB_ID || GetTosJobLink(order) == ascOrder.JOB_ID)
            {
                // Agv order match Asc order, and agv ws lane should has not been assigned.
                return ReserveLaneKind.None;
            }

            var listAscResJob = m_dbDataSchedule.m_DBData_TOS.m_listASC_ResJob;
            ASC_ResJob ascJob = listAscResJob.Find(x => x.JOB_ID == ascOrder.JOB_ID && x.YARD_ID == ascOrder.YARD_ID);
            if (ascJob == null) // error
                return ReserveLaneKind.NonMate; // 返回保守值

            var listAgvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order;
            var listAgvCmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command;

            AGV_Order agvOrder = listAgvOrder.Find(x => x.JOB_ID == ascOrder.JOB_ID);
            AGV_Order agvOrderLink = listAgvOrder.Find(x => GetTosJobLink(x) == ascOrder.JOB_ID);

            // 一吊
            if (string.IsNullOrWhiteSpace(ascJob.JOB_LINK)
                || Utility.IsTwinAscOrder(ascJob, listAscResJob))
            {
                if (agvOrder != null && IsAgvOrderLaneAssignedBlcok(agvOrder, ascOrder.YARD_ID)
                    || agvOrderLink != null && IsAgvOrderLaneAssignedBlcok(agvOrderLink, ascOrder.YARD_ID))
                {
                    return ReserveLaneKind.None;
                }

                return ReserveLaneKind.Any;
            }

            // 二吊
            if (agvOrder == null && agvOrderLink == null)
            {
                return ReserveLaneKind.NonMate; // 返回保守值
            }

            if (agvOrder != null && agvOrderLink != null)
            {
                if (IsAgvOrderLaneAssignedBlcok(agvOrder, ascOrder.YARD_ID)
                    || IsAgvOrderLaneAssignedBlcok(agvOrderLink, ascOrder.YARD_ID))
                {
                    // AGV车道已分配，不需保留车道。
                    return ReserveLaneKind.None;
                }

                AGV_Command agvCmd = listAgvCmd.Find(x => x.ORDER_ID == agvOrder.ORDER_ID);
                AGV_Order_Type agvOrderType = Utility.GetAgvOrderType(agvOrder, agvCmd);

                AGV_Command agvCmdLink = listAgvCmd.Find(x => x.ORDER_ID == agvOrderLink.ORDER_ID);
                AGV_Order_Type agvOrderTypeOfLink = Utility.GetAgvOrderType(agvOrderLink, agvCmdLink);

                if ((agvOrderType == AGV_Order_Type.DelieverToWstp
                    && agvOrder.TO_BLOCK == ascOrder.YARD_ID
                    && agvOrderTypeOfLink == AGV_Order_Type.DelieverToWstpComplete)
                    ||
                    (agvOrderTypeOfLink == AGV_Order_Type.DelieverToWstp
                    && agvOrderLink.TO_BLOCK == ascOrder.YARD_ID
                    && agvOrderType == AGV_Order_Type.DelieverToWstpComplete))
                {
                    // 双箱不同场，送第二箱
                    return ReserveLaneKind.Any;
                }

                return ReserveLaneKind.NonMate;
            }

            // 3
            AGV_Order agvOrderNotNull = agvOrder ?? agvOrderLink;

            AGV_Command cmd = listAgvCmd.Find(x => x.ORDER_ID == agvOrderNotNull.ORDER_ID);
            AGV_Order_Type orderType = Utility.GetAgvOrderType(agvOrderNotNull, cmd);

            if (IsAgvOrderLaneAssignedBlcok(agvOrderNotNull, ascOrder.YARD_ID))
            {
                // AGV车道已分配，不需保留车道。
                return ReserveLaneKind.None;
            }

            if (orderType == AGV_Order_Type.ReceiveFromWstp
                && agvOrderNotNull.FROM_BLOCK == ascOrder.YARD_ID)
            {
                // 双箱不同场，收第一箱
                return ReserveLaneKind.Any;
            }
           
            return ReserveLaneKind.NonMate;
        }

        private bool IsToFinalBlock(AGV_Order order)
        {
            var listAgvResJob = m_dbDataSchedule.m_DBData_TOS.m_listAGV_ResJob;
            AGV_ResJob job = listAgvResJob.Find(x => x.JOB_ID == order.JOB_ID);
            if (job == null)
                return false; // error

            if (string.IsNullOrWhiteSpace(job.JOB_LINK))
                return true;

            AGV_ResJob resJobLink = listAgvResJob.Find(x => x.JOB_LINK == order.JOB_ID);
            if (resJobLink == null)
                return false; // error

            JobType jobType = order.GetJobType();

            if (jobType == JobType.LOAD)
                return job.FROM_BLOCK == resJobLink.FROM_BLOCK;

            if (jobType == JobType.DISC)
            {
                // 同场
                if (job.TO_BLOCK == resJobLink.TO_BLOCK)
                    return true;

                AGV_Task taskLink = m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task
                    .Find(x => x.Task.JOB_ID == job.JOB_LINK);

                // 卸船第二箱
                if (taskLink != null && Helper.IsTaskComplete(taskLink.TaskState))
                    return true;

                return false;
            }

            if (jobType == JobType.DBLOCK)
            {
                AGV_Command cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command.Find(x => x.ORDER_ID == order.ORDER_ID);
                AGV_Order_Type agvOrderType = Utility.GetAgvOrderType(order, cmd);

                if (agvOrderType == AGV_Order_Type.ReceiveFromWstp)
                    return job.FROM_BLOCK == resJobLink.FROM_BLOCK;

                if (agvOrderType == AGV_Order_Type.DelieverToWstp)
                    return job.TO_BLOCK == resJobLink.TO_BLOCK;

                return true;
            }

            return true;
        }

        /// <summary>
        /// AGV Order是否已分配指定堆场的车道
        /// </summary>
        /// <param name="agvOrder"></param>
        /// <param name="strBlockNo"></param>
        /// <returns></returns>
        private bool IsAgvOrderLaneAssignedBlcok(AGV_Order agvOrder, string strBlockNo)
        {
            AGV_Command agvCmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command.Find(x => x.ORDER_ID == agvOrder.ORDER_ID);
            AGV_Order_Type agvOrderType = Utility.GetAgvOrderType(agvOrder, agvCmd);

            if (agvOrderType == AGV_Order_Type.ReceiveFromWstp
                && !string.IsNullOrWhiteSpace(agvOrder.FROM_LANE)
                && agvOrder.FROM_BLOCK == strBlockNo)
            {
                return true;
            }

            if ((agvOrderType == AGV_Order_Type.DelieverToWstp || agvOrderType == AGV_Order_Type.DelieverToWstpComplete)
                && Helper.GetEnum(agvOrder.TO_BAY_TYPE, BayType.UnKnown) == BayType.WS // exclude pb
                && !string.IsNullOrWhiteSpace(agvOrder.TO_LANE)
                && agvOrder.TO_BLOCK == strBlockNo)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 根据block查找正在做的ASC Order
        /// 假定条件： 一次只发送给ASC一条海侧Order
        /// </summary>
        /// <param name="targetBlock"></param>
        /// <returns></returns>
        private ASC_Order FindWsAscOrderInDoingByBlcok(string targetBlock)
        {
            ASC_Order ascOrder = m_dbDataSchedule.m_DBData_BMS.m_listASC_Order
                .FirstOrDefault(x =>
                    x.YARD_ID == targetBlock
                    && x.TaskSide() == AscTaskSide.WaterSide
                    && IsOrderInWorking(x));

            return ascOrder;
        }

        /// <summary>
        /// 新建Load或DBlock Order时尝试分配FromLane
        /// </summary>
        /// <param name="agvStatus"></param>
        /// <param name="order"></param>
        /// <param name="canAssignToMateLane"></param>
        /// <returns></returns>
        private LaneInfoEx TryAcquireAgvOrderFromLane(AGV_STATUS agvStatus, AGV_Order order, bool canAssignToMateLane)
        {
            ReserveLaneKind reserveLaneKind = CalcReserveLaneForAsc(order, order.FROM_BLOCK);

            var targetIdleWstpLane = m_lanePlan.PreAssignLane(agvStatus, order.FROM_BLOCK, order.FROM_BAY_TYPE, canAssignToMateLane, reserveLaneKind);

            return targetIdleWstpLane;
        }

        /// <summary>
        ///尝试为AGV Order指定车道，并指定保留车道类型
        /// </summary>
        /// <param name="order"></param>
        /// <param name="toLane"></param>
        /// <returns></returns>
        private bool TryAcquireOrderWstpToLane(AGV_Order order, out LaneInfoEx toLane)
        {
            toLane = null;

            AGV_STATUS agvStatus = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Status
                .Find(x => x.CHE_ID == order.CHE_ID);
            if (agvStatus == null)
                return false;

            //AGV_Order_Type agvOrderType = AGV_Order_Type.DelieverToWstp;

            ReserveLaneKind reserveLaneKind = CalcReserveLaneForAsc(order, order.TO_BLOCK);

            var targetIdleWstpLane =
                m_lanePlan.PreAssignLane(agvStatus, order.TO_BLOCK, BayType.WS.ToString(), true, reserveLaneKind);

            if (targetIdleWstpLane == null)
                return false;

            order.TO_BAY_TYPE = BayType.WS.ToString();
            order.TO_LANE = Convert.ToString(targetIdleWstpLane.GetLaneNo());

            toLane = targetIdleWstpLane;

            return true;
        }

        /// <summary>
        ///判断伴侣车道是否可用
        /// </summary>
        /// <param name="order"></param>
        /// <param name="agvOrderType"></param>
        /// <returns></returns>
        private bool CanAssignToMateLane(AGV_Order order, AGV_Order_Type agvOrderType)
        {
            string strOrderLink = order.GetOrderLink();

            if(string.IsNullOrWhiteSpace(strOrderLink))
                return true;

            switch (agvOrderType)
            {
                case AGV_Order_Type.ReceiveFromWstp:
                    {
                        if (string.IsNullOrWhiteSpace(order.FROM_LINK))
                            return false;
                    }
                    break;
                case AGV_Order_Type.DelieverToWstp:
                    {
                        var orderLink = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                            .Find(x => x.ORDER_ID == strOrderLink);

                        if (orderLink == null
                            || string.IsNullOrWhiteSpace(order.TO_BLOCK)
                            || orderLink.TO_BLOCK != order.TO_BLOCK)
                        {
                            return false;
                        }
                    }
                    break;
                default:
                    return true;
            }

            return true;
        }

        /// <summary>
        ///计算某block wstp上的AGV是否需要驱赶
        /// </summary>
        /// <param name="targetBlock"></param>
        /// <returns></returns>
        private bool NeedRepositionAgvOnWstp(string targetBlock)
        {
            int nCountOfAgvDelieveringToWstp =
                    m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                        .Where(x => x.TO_BLOCK == targetBlock
                                    && IsOrderCompleteFrom(x))
                        .Select(y => y.CHE_ID).Distinct().Count();

            int nCountOfAgvReceivingFromWstp =
                    m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                        .Where(x => x.FROM_BLOCK == targetBlock
                                    //&& x.IsTwinLoadFromDiffBlock()
                                    && IsOrderInFirstHalf(x))
                        .Select(y => y.CHE_ID).Distinct().Count();

            int nCountOfAgvBeingRepositioned =
                    m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                        .Where(x => x.FROM_BLOCK == targetBlock
                                    && x.GetJobType() == JobType.REPOSITION
                                    && IsOrderInWorking(x))
                        .Select(y => y.CHE_ID).Distinct().Count();

            return (nCountOfAgvDelieveringToWstp + nCountOfAgvReceivingFromWstp > nCountOfAgvBeingRepositioned);
        }

        /// <summary>
        ///计算WSTP Lane是否需要分配 
        /// </summary>
        /// <param name="order"></param>
        /// <param name="agvOrderType"></param>
        /// <param name="targetBlock"></param>
        /// <returns></returns>
        private bool NeedAssignWstpLane(AGV_Order order, AGV_Order_Type agvOrderType, out string targetBlock)
        {
            targetBlock = "";

            switch (agvOrderType)
            {
                case AGV_Order_Type.ReceiveFromWstp:
                    if (Helper.GetEnum(order.FROM_BAY_TYPE, BayType.UnKnown) == BayType.WS
                        && string.IsNullOrWhiteSpace(order.FROM_LANE))
                    {
                        targetBlock = order.FROM_BLOCK;
                        return true;
                    }
                    break;
                case AGV_Order_Type.DelieverToWstp:
                    if (string.IsNullOrWhiteSpace(order.TO_BLOCK))
                        return false;

                    BayType toBayType = Helper.GetEnum(order.TO_BAY_TYPE, BayType.UnKnown);
                    if (toBayType == BayType.PB
                        || (toBayType == BayType.WS && string.IsNullOrWhiteSpace(order.TO_LANE)))
                    {
                        targetBlock = order.TO_BLOCK;
                        return true;
                    }

                    break;
            }

            return false;
        }

        private bool SendRepositionResJob(string strAgvIdToRepostion, string fromBlock, LaneInfoEx fromLaneInfo)
        {
#if ASSIGN_PB_BY_SCHD
            var pbLaneInfo = m_lanePlan.GetAnyIdlePbLanes("", false);

            if (pbLaneInfo == null)
                return false;
#endif
            AGV_ResJob resRepJob = new AGV_ResJob
            {
                JOB_ID = Convert.ToString(DB_TOS.Instance.CreateNewJobID()),
                JOB_LINK = "",

                CHE_ID = strAgvIdToRepostion,
                JOB_TYPE = JobType.REPOSITION.ToString(),
                JOB_STATUS = ResJobStatus.New.ToString(),

#if ASSIGN_PB_BY_SCHD
                QUAY_ID = pbLaneInfo.BlockOrQcId,
                TO_LANE = Convert.ToString(pbLaneInfo.GetLaneNo()),
#endif
                TO_BAY_TYPE = BayType.PB.ToString(),

                CONTAINER_ID = "",
                PRIORITY = 0,
                PLAN_START_TIME = DateTime.Now,
                DATETIME = DateTime.Now,

                FROM_BLOCK = fromBlock,
                FROM_LANE = Convert.ToString(fromLaneInfo.GetLaneNo()),
            };

            if (!InsertResJobToDbAndCache(resRepJob))
                return false;

            // 立即发送Reposition Order
            SendRepositionOrderByResJob(resRepJob);

            return true;
        }

        private void SendRepositionOrderByResJob()
        {
            var listRepResJob = m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task
                .FindAll(x => Helper.GetEnum(x.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.REPOSITION
                              && Helper.IsTaskInitial(x.TaskState));

            foreach (var repResJob in listRepResJob)
            {
                if (!m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                    .Exists(x => x.JOB_ID == repResJob.Task.JOB_ID))
                {
                    SendRepositionOrderByResJob(repResJob.Task);
                }
            }
        }

        private void SendRepositionOrderByResJob(AGV_ResJob repResJob)
        {
            var order = Utility.CreateAgvOrder(repResJob, m_dbDataSchedule);

            if (InsertOrderToDbAndCache(order, MethodBase.GetCurrentMethod().Name))
            {
#if ASSIGN_PB_BY_SCHD
                var pbLane = m_lanePlan.GetLane("0", BayType.PB.ToString(), order.TO_LANE);
                m_lanePlan.AddLaneInUsing(pbLane, order.ORDER_ID);
#endif
            }
        }

        private bool TryRepositionIdleAgvOnWstp(string WSTP_BLOCK)
        {
            var listBlockLane = m_lanePlan.GetTpLanes(WSTP_BLOCK, BayType.WS.ToString());

            foreach (var li in listBlockLane)
            {
                if (li.LaneStatus != LaneStatus.OCCUPIED)
                    continue;

                string strAgvId = li.OccupyAgvId.ToString();

                if (!string.IsNullOrWhiteSpace(strAgvId)
                    && !IsAgvDoingOrder(strAgvId)
                    && !IsAgvDoingRepositionOrder(strAgvId))
                {
                    bool bSent = SendRepositionResJob(strAgvId, WSTP_BLOCK, li);

                    Utility.Log("AGV", "SendRepositionResJob", bSent, string.Format("Agv={0}, block={1}, lane={2}", strAgvId, WSTP_BLOCK, li));

                    return bSent;
                }
            }

            return false;
        }

        /// <summary>
        ///在指派任务时，除了考虑空闲的AGV外，也考虑即将完成任务的AGV。 
        /// </summary>
        /// <param name="agv"></param>
        /// <returns></returns>
        private bool IsAgvSchedulable(Agv agv)
        {
            if (!agv.CanBeScheduled())
            {
                return false;
            }

            //根据Command表来判断AGV是否空闲或正在执行任务的后半程
            if (!IsAgvIdle(agv.Status.CHE_ID))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 选择AGV可做任务
        /// </summary>
        /// <param name="listWiTopLogicSorted"></param>
        /// <returns></returns>
        private List<TaskSet<AGV_Task>> SelectTaskSetAgvCanDo(List<WORK_INSTRUCTION_STATUS> listWiTopLogicSorted)
        {
            List<AGV_Task> listWiTaskAgvCanDo = listWiTopLogicSorted
                .Select(wi => Utility.FindTaskByWi(wi, m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task))
                .Where(task => task != null)
                .ToList();

           // List<AGV_Task> listNotSentAgvTask = new List<AGV_Task>();
            List<AGV_Task> listNotSentAgvTask =
                m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task
                    .FindAll(x => !Utility.IsTaskBind(x.TaskState)
                                  && !IsTaskSent(x));

            // AGV Task List中还未发送，但可做的任务
            List<AGV_Task> listNotSentTaskAgvCanDo = new List<AGV_Task>();
            foreach (AGV_Task agvTask in listNotSentAgvTask)
            {
                if (!Utility.IsTaskInitial(agvTask.TaskState))
                    continue;

                if (Utility.IsAgvTaskAssignedByTos(agvTask.Task) // Tos指定车的任务
                    || Helper.GetEnum(agvTask.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DBLOCK       // DBLOCK
                   )
                {
                    listNotSentTaskAgvCanDo.Add(agvTask);
                }
                else
                {
                    // AGV_Order not sent, but STS or ASC sent.
                    ASC_Task ascTask = m_dbDataSchedule.m_DBData_TOS.m_listASC_Task.Find(x => x.Task.JOB_ID == agvTask.Task.JOB_ID);
                    if (ascTask != null && !Helper.IsTaskInitial(ascTask.TaskState))
                    {
                        listNotSentTaskAgvCanDo.Add(agvTask);
                    }
                    else
                    {
                        STS_Task stsTask = m_dbDataSchedule.m_DBData_TOS.m_listSTS_Task.Find
                            (x => x.Task.JOB_ID == agvTask.Task.JOB_ID && Helper.GetEnum(x.Task.JOB_TYPE, JobType.UNKNOWN) != JobType.DISC);
                        if (stsTask != null && !Helper.IsTaskInitial(stsTask.TaskState))
                        {
                            listNotSentTaskAgvCanDo.Add(agvTask);
                        }
                    }
                }
            }

            // Disc，DBLOCK和Load中TaskState为Ready/Enter无Order
            List<AGV_Task> listOrphanTaskAgvCanDo =
                listNotSentAgvTask
                    .FindAll(x =>
                        (Helper.GetEnum(x.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC||
                         Helper.GetEnum(x.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.LOAD ||
                         Helper.GetEnum(x.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DBLOCK)
                        && Utility.IsTaskInFirstHalfDoing(x.TaskState));

            List<AGV_Task> listTaskAgvCanDo = 
                listWiTaskAgvCanDo
                .Union(listOrphanTaskAgvCanDo)
                .Union(listNotSentTaskAgvCanDo)
                .Distinct()
                .ToList();

            // 双箱任务一起参与任务与AGV的配对
            // 添加可能因WI截取导致缺失的双箱任务
            List<AGV_Task> listTaskLinkLost = new List<AGV_Task>();
            foreach (var task in listTaskAgvCanDo)
            {
                if (string.IsNullOrWhiteSpace(task.Task.JOB_LINK)
                    || listTaskAgvCanDo.Exists(x => x.Task.JOB_ID == task.Task.JOB_LINK))
                {
                    continue;
                }

                var taskLink = m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task
                    .Find(x => x.Task.JOB_ID == task.Task.JOB_LINK);

                if (taskLink != null /*&& IsTaskNotSent(taskLink)*/)
                {
                    listTaskLinkLost.Add(taskLink);
                }
            }

            listTaskAgvCanDo = listTaskAgvCanDo.Union(listTaskLinkLost).Distinct().ToList();

            var listTaskSet = TaskSet<AGV_Task>.GetTaskSetList(listTaskAgvCanDo);

            var listTaskSetAgvCanDo = listTaskSet.FindAll(x => x.TaskList.TrueForAll(y => !IsTaskSent(y)));

            return listTaskSetAgvCanDo;
        }

         /// <summary>
         /// 发送AGV Task List中还未发送，但STS或ASC正在执行的任务
         /// </summary>
         /// <param name="listNotSentAgvTask"></param>
        private void SendStsAscDoingWsTask(List<AGV_Task> listNotSentAgvTask, List<Agv> listAgvUnAssigned)
        {
            List<AGV_Task> listNotSentTaskAgvCanDo = new List<AGV_Task>();

            foreach (AGV_Task agvTask in listNotSentAgvTask)
            {
                if (!Utility.IsTaskInFirstHalfDoing(agvTask.TaskState))
                    continue;

                JobType jobType = Helper.GetEnum(agvTask.Task.JOB_TYPE, JobType.UNKNOWN);

                if (jobType == JobType.LOAD || jobType == JobType.DBLOCK)
                {
                    // AGV_Order not sent, but ASC sent. // 装船根据ASC是否已开始执行
                    ASC_Task ascTask = m_dbDataSchedule.m_DBData_TOS.m_listASC_Task
                        .Find(x => x.Task.JOB_ID == agvTask.Task.JOB_ID
                                   && (jobType != JobType.DBLOCK || x.Task.YARD_ID == agvTask.Task.FROM_BLOCK));

                    if (ascTask != null && !Helper.IsTaskInitial(ascTask.TaskState))
                    {
                        listNotSentTaskAgvCanDo.Add(agvTask);
                    }
                }
                else if (jobType == JobType.DISC)
                {
                    // AGV_Order not sent, but STS sent. // 卸船任务根据STS是否已开始执行
                    STS_Task stsTask = m_dbDataSchedule.m_DBData_TOS.m_listSTS_Task
                        .Find(x => x.Task.JOB_ID == agvTask.Task.JOB_ID);

                    if (stsTask != null && !Helper.IsTaskInitial(stsTask.TaskState))
                    {
                        listNotSentTaskAgvCanDo.Add(agvTask);
                    }
                }
            }

            SendTaskAgvCanDo(listNotSentTaskAgvCanDo, listAgvUnAssigned);
        }

        private void SendWiTask(List<WORK_INSTRUCTION_STATUS> listWiTopLogicSorted, List<Agv> listAgvUnAssigned)
        {
            List<AGV_Task> listWiTaskAgvCanDo = listWiTopLogicSorted
                .Select(wi => Utility.FindTaskByWi(wi, m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task))
                .Where(task => task != null)
                .ToList();

            SendTaskAgvCanDo(listWiTaskAgvCanDo, listAgvUnAssigned);
        }

        private void SendTaskAgvAssignedByTos(List<AGV_Task> listNotSentAgvTask)
        {
            //选出可以安排任务的AGV
            List<Agv> listAgvSchedulable = m_listAgv.FindAll(IsAgvSchedulable);
            if (listAgvSchedulable.Count <= 0)
            {
                return;
            }

            // Tos指定车的任务
            List<AGV_Task> listTaskAgvAssignedByTos =
                listNotSentAgvTask
                    .FindAll(x => Utility.IsTaskInitial(x.TaskState)
                                  && Utility.IsAgvTaskAssignedByTos(x.Task));

            var listTaskSetAgvAssignedByTos =
                TaskSet<AGV_Task>.GetTaskSetList(listTaskAgvAssignedByTos);

            // 已指定车的任务，直接发送

            List<AgvPreTask> listAgvPreTaskTos = new List<AgvPreTask>();
            foreach (var taskSet in listTaskSetAgvAssignedByTos)
            {
                AGV_Task taskAgvAssigned = taskSet.TaskList.Find(x => !string.IsNullOrWhiteSpace(x.Task.CHE_ID));

                Agv agv = listAgvSchedulable.Find(x => x.Status.CHE_ID == taskAgvAssigned.Task.CHE_ID);
                if (agv == null)
                    continue;

                AgvPreTask preTask = new AgvPreTask
                {
                    PreTask = taskSet,
                    Agv = agv,
                    MatchedAgvTask = taskAgvAssigned,
                    OccupiedLane = null
                };

                listAgvPreTaskTos.Add(preTask);
            }

            SendPreTask(listAgvPreTaskTos);
        }

        private void SendOtherTask(List<AGV_Task> listNotSentAgvTask, List<Agv> listAgvUnAssigned)
        {
            List<AGV_Task> listNotSentDblockTask =
                listNotSentAgvTask
                    .FindAll(x =>
                        Helper.GetEnum(x.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DBLOCK
                        && Utility.IsTaskInitial(x.TaskState));

            // Disc，DBLOCK和Load中TaskState为Ready/Enter无Order
            List<AGV_Task> listOrphanTaskAgvCanDo =
                listNotSentAgvTask
                    .FindAll(x =>
                        (Helper.GetEnum(x.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC ||
                         Helper.GetEnum(x.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.LOAD ||
                         Helper.GetEnum(x.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DBLOCK)
                        && Utility.IsTaskInFirstHalfDoing(x.TaskState))
                     .Distinct()
                     .ToList();

            var listOtherTaskCanDo =
                    listNotSentDblockTask
                    .Union(listOrphanTaskAgvCanDo)
                    .ToList();

            SendTaskAgvCanDo(listOtherTaskCanDo, listAgvUnAssigned);
        }

        private bool IsTaskSent(AGV_Task task)
        {
            return m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                    .Exists(x => x.JOB_ID == task.Task.JOB_ID);
        }

        /// <summary>
        /// 选车并发送任务
        /// </summary>
        /// <param name="listAgvUnAssigned"></param>
        /// <param name="listTaskAgvCanDo"></param>
        private void SendTaskAgvCanDo(List<AGV_Task> listTaskAgvCanDo, List<Agv> listAgvUnAssigned)
        {
            listAgvUnAssigned = listAgvUnAssigned.FindAll(agv => !IsAgvDoingOrder(agv.Status.CHE_ID));

            if (listAgvUnAssigned.Count <= 0)
                return;

            // 双箱任务一起参与任务与AGV的配对
            // 添加可能因WI截取导致缺失的双箱任务
            List<AGV_Task> listTaskLinkLost = new List<AGV_Task>();
            foreach (var task in listTaskAgvCanDo)
            {
                if (string.IsNullOrWhiteSpace(task.Task.JOB_LINK)
                    || listTaskAgvCanDo.Exists(x => x.Task.JOB_ID == task.Task.JOB_LINK))
                {
                    continue;
                }

                var taskLink = m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task
                    .Find(x => x.Task.JOB_ID == task.Task.JOB_LINK);

                if (taskLink != null /*&& !IsTaskSent(taskLink)*/)
                {
                    listTaskLinkLost.Add(taskLink);
                }
            }

            listTaskAgvCanDo = listTaskAgvCanDo.Union(listTaskLinkLost).Distinct().ToList();

            //
            var listTaskSet = TaskSet<AGV_Task>.GetTaskSetList(listTaskAgvCanDo);

            var listTaskSetAgvCanDo = listTaskSet.FindAll(x => x.TaskList.TrueForAll(y => !IsTaskSent(y)));

            // 为未指定车的任务匹配车，并发送任务
            AgvTaskDispatcher agvTaskDispatcher = new AgvTaskDispatcher();

            List<AgvPreTask> listAgvPreTask =
                agvTaskDispatcher.PreAssignAgvTask(
                    listAgvUnAssigned, listTaskSetAgvCanDo, m_dbDataSchedule, m_lanePlan);

            // 计算每台岸桥应分配的AGV数量
            if (CalcAgvCountPlannedForSts(listTaskSetAgvCanDo.SelectMany(ts => ts.TaskList).ToList()))
            {
                // 如果服务于某台岸桥的AGV数量超过限制，则将相应的任务剔除。
                //GetRidOfTaskIfExceedAgvCount(ref listAgvPreTask);
            }

            LogSts();

            SendPreTask(listAgvPreTask);
        }

        /// <summary>
        /// 计算每台STS应分配的AGV总数，包括正占用的AGV、及为完成计划的任务而需要的AGV。
        /// 对Load和Disc任务进行区分，一般来说同样的任务数，Load任务需要的AGV比Disc多。
        /// </summary>
        /// <param name="listTaskAgvCanDo"></param>
        /// <returns>true - 成功，false - 输入数据有误（所有AGV即无正在执行的任务，也无计划任务，在此种情况下，前绪步骤中AGV子调度就应已退出）</returns>
        private bool CalcAgvCountPlannedForSts(List<AGV_Task> listTaskAgvCanDo)
        {
            var listAgvOrderWorking = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                .FindAll(IsOrderInWorking);

            double dTotalWeight = 0;

            if (m_listSts.Count <= 0)
            {
                if (m_bWriteLog)
                {
                    Logger.ECSSchedule.Error("[AGV] Failed to get STS Status list! Please check database!");
                    m_bWriteLog = false;
                }
                return false;
            }

            m_bWriteLog = true;

            foreach (var sts in m_listSts)
            {
                // 计算STS占用的AGV数量(双箱任务只占用一台AGV)
                sts.AgvCountOccupiedLoad = listAgvOrderWorking
                    .Where(x => Helper.GetEnum(x.JOB_TYPE, JobType.UNKNOWN) == JobType.LOAD
                            && Utility.IsSameQcId(sts.Status.QC_ID, x.TO_BLOCK))
                    .Select(x => x.CHE_ID).Distinct().Count();

                sts.AgvCountOccupiedDisc = listAgvOrderWorking
                    .Where(x => Helper.GetEnum(x.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC
                            && Utility.IsSameQcId(sts.Status.QC_ID, x.FROM_BLOCK))
                    .Select(x => x.CHE_ID).Distinct().Count();

                // 计算STS计划的任务数量对AGV的需求数量(双箱任务只需要一台AGV)
                List<AGV_Task> listTaskOfSts = listTaskAgvCanDo
                    .FindAll(x => Utility.IsSameQcId(x.Task.QUAY_ID, sts.Status.QC_ID));
                var listTaskSetOfSts = TaskSet<AGV_Task>.GetTaskSetList(listTaskOfSts);

                sts.AgvCountNeedForLoad = listTaskSetOfSts.Count(x => x.JobType == JobType.LOAD);
                sts.AgvCountNeedForDisc = listTaskSetOfSts.Count(x => x.JobType == JobType.DISC);

                dTotalWeight += sts.AgvCountWeight();
            }

            if (dTotalWeight <= 0)
            {
                //Logger.ECSSchedule.Warn("[AGV] Failed to calculate AGV count weight of STS! Perhaps no task to do now!");
                return false;
            }

            // 计算STS应分配的AGV数量
            int nCountOfAgvCanBeScheduled = m_listAgv.FindAll(x => x.CanBeScheduled()).Count;
            foreach (var sts in m_listSts)
            {
                sts.CalcAgvCountPlanned(nCountOfAgvCanBeScheduled, dTotalWeight);
            }

            return true;
        }

        private bool IsAgvIdle(string strCheId)
        {
            var listTheAgvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                .FindAll(x => x.CHE_ID == strCheId
                           && x.GetJobType() != JobType.REPOSITION);

            foreach (var order in listTheAgvOrder)
            {
                var cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command
                    .Find(x => x.ORDER_ID == order.ORDER_ID);

                if (cmd == null || !cmd.IsComplete())
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 如果AGV已分配Order或有Order正在执行中(根据Command表判断)，则不可向其派发新Order
        /// </summary>
        private bool IsAgvDoingOrder(string strCheId, bool includeScheduleOrder = true)
        {
            var listTheAgvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                .FindAll(x => x.CHE_ID == strCheId);

            foreach (var order in listTheAgvOrder)
            {
                //
                if (!includeScheduleOrder)
                {
                    JobType jobType = Helper.GetEnum(order.JOB_TYPE, JobType.UNKNOWN);
                    if (jobType == JobType.REPOSITION)
                        continue;
                }

                //
                if (IsOrderInWorking(order))
                    return true;
            }

            return false;
        }

        private bool IsAgvDoingRepositionOrder(string strCheId)
        {
            var listTheAgvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                .FindAll(x => x.CHE_ID == strCheId && Helper.GetEnum(x.JOB_TYPE, JobType.UNKNOWN) == JobType.REPOSITION);

            foreach (var order in listTheAgvOrder)
            {
                var cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command
                    .Find(x => x.ORDER_ID == order.ORDER_ID);

                if (cmd == null || !cmd.IsComplete())
                {
                    //有Order但无Command，或有未完成的任务
                    return true;
                }
            }

            return false;
        }

        private TaskStatus GetOrderStatus(AGV_Order order)
        {
            var cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command
                .Find(x => x.ORDER_ID == order.ORDER_ID);

            if (cmd == null)
            {
                return TaskStatus.None;
            }
            
            return cmd.GetCmdStatus();
        }

        /// <summary>
        /// Order已插入到DB，但还未完成。
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private bool IsOrderInWorking(AGV_Order order)
        {
            if (order == null)
                return false;

            var cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command
                .Find(x => x.ORDER_ID == order.ORDER_ID);

            if (cmd == null || !cmd.IsComplete())
            {
                //有Order但无Command，或未完成
                return true;
            }

            return false;
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

        private bool IsOrderInFirstHalf(AGV_Order order)
        {
            if (order == null)
                return false;

            var cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command
                .Find(x => x.ORDER_ID == order.ORDER_ID);

            if (cmd == null || cmd.IsInFirstHalf())
            {
                //有Order但无Command，或未完成
                return true;
            }

            return false;
        }

        private bool IsOrderCompleteFrom(AGV_Order order)
        {
            if (order == null)
                return false;

            var cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command
                .Find(x => x.ORDER_ID == order.ORDER_ID);

            if (cmd != null && cmd.IsCompleteFrom())
            {
                return true;
            }

            return false;
        }

        private bool IsOrderComplete(AGV_Order order)
        {
            if (order == null)
                return false;

            var cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command
                .Find(x => x.ORDER_ID == order.ORDER_ID);

            if (cmd != null && cmd.IsComplete())
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///对于可下发任务（空闲的）的AGV，可以安排任务并下发ECS 
        /// </summary>
        /// <param name="listAgvPreTask"></param>
        /// <returns></returns>
        private bool SendPreTask(List<AgvPreTask> listAgvPreTask)
        {
            foreach (var preTask in listAgvPreTask)
            {
                SendTaskSetToAgv(preTask);
            }

            return true;
        }

        private bool SendTaskSetToAgv(AgvPreTask preTask)
        {
            TaskSet<AGV_Task> taskSet = preTask.PreTask;
            Agv agv = preTask.Agv;
            var liOccupied = preTask.OccupiedLane;

            if (IsAgvDoingOrder(agv.Status.CHE_ID))
                return false;

            // 双箱任务只发送一个Order，另一个Order在AGV收箱后再补发
            if (taskSet.TaskList.Exists(IsTaskSent))
                return false;

            AGV_Task task = preTask.MatchedAgvTask ?? taskSet.TaskList[0];

            bool bRet = false;

            JobType jobType = Helper.GetEnum(task.Task.JOB_TYPE, JobType.UNKNOWN);

            if (jobType == JobType.DISC || jobType == JobType.LOAD)
            {
                // 检查是否超出STS的最大AGV数量限制

                Sts sts = m_listSts.Find(x => Utility.IsSameQcId(task.Task.QUAY_ID, x.Status.QC_ID));
                if (sts == null)
                    return false;

                bool bDoNotCheckMaxAgvCountOfSts = false;
                if (bDoNotCheckMaxAgvCountOfSts || sts.AgvCountRemainForAssign > 0)
                {
                    bRet = SendTaskToAgv(task, liOccupied, agv);

                    if (bRet)
                    {
                        if (jobType == JobType.LOAD)
                            sts.AgvCountOccupiedLoad++;
                        else
                            sts.AgvCountOccupiedDisc++;
                    }
                }
                else
                {
                    Logger.ECSSchedule.Info("[AGV] Fail to send task for the agv count exceeds MaxAgvCountOfSts, task=" + Utility.GetString(task.Task));
                    Logger.ECSSchedule.Info(sts.ToString());
                }
            }
            else
            {
                bRet = SendTaskToAgv(task, liOccupied, agv);
            }

            return bRet;
        }

        /// <summary>
        /// AGV新Order
        /// </summary>
        /// <param name="task"></param>
        /// <param name="liOccupied"></param>
        /// <param name="agv"></param>
        /// <returns></returns>
        private bool SendTaskToAgv(AGV_Task task, LaneInfoEx liOccupied, Agv agv)
        {
            if (Helper.GetEnum(task.Task.FROM_BAY_TYPE, BayType.UnKnown) == BayType.AGV
                && string.IsNullOrWhiteSpace(task.Task.CHE_ID))
            {
                Logger.ECSScheduleDebug.Error("[AGV] FROM_BAY_TYPE='AGV' but the CHE_ID is empty! " + Utility.GetString(task.Task));
                return false;
            }

            bool isAgvAssignedByTos = Utility.IsAgvTaskAssignedByTos(task.Task);

            // AGV新Order只指派From。
            AGV_Order order = Utility.CreateAgvOrder(task.Task, m_dbDataSchedule, isAgvAssignedByTos, isAgvAssignedByTos);
            if (order == null)
                return false;

            var listAgvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order;
            AGV_Order orderOld = listAgvOrder.Find(x => x.ORDER_ID == order.ORDER_ID);

            if (orderOld != null)
                return false;

            // 指派AGV
            order.CHE_ID = agv.Status.CHE_ID;

            // 指派车道
            JobType jobType = Helper.GetEnum(task.Task.JOB_TYPE, JobType.UNKNOWN);
            var li = liOccupied;

            if (liOccupied != null)
            {
                Utility.Log("AGV", MethodBase.GetCurrentMethod().Name + " Occupied", "From Lane", liOccupied.ToString());
            }

            if (!isAgvAssignedByTos && (jobType == JobType.LOAD || jobType == JobType.DBLOCK))
            {
                // 指定FromLane
                if (li == null)
                {
                    bool canAssignToMateLane = true; //从堆场收单箱或双箱的第一个箱时，可以指定伴侣车道

                    li = TryAcquireAgvOrderFromLane(agv.Status, order, canAssignToMateLane);

                    if (li == null)
                    {
                        // 无空闲WSTP车道，驱赶
                        RepositionForReceiveFromWstp(order.FROM_BLOCK, order);

                        //Utility.Log("AGV", MethodBase.GetCurrentMethod().Name + " Fail", "No Lane", order.ToString());
                        Logger.ECSScheduleDebug.Debug("[AGV] Failed to " + MethodBase.GetCurrentMethod().Name + " for no lane is available. " + order.ToString());

                        //新建Load或DBlock Order时如果分配FromLane失败，则不发送该Order
                        return false;
                    }
                }

                order.FROM_LANE = li.GetLaneNo().ToString();
            }

            if (isAgvAssignedByTos && (jobType == JobType.DISC || jobType == JobType.DBLOCK))
            {
                // 尝试指定ToLane
                LaneInfoEx toLane = null;
                if (TryAcquireOrderWstpToLane(order, out toLane))
                {
                    li = toLane;
                }
            }

            // 下发Order
            bool isOrderSent = InsertOrderToDbAndCache(order, MethodBase.GetCurrentMethod().Name);

            //标记已预分配车道的状态，此时并不保存到DB。由ECS实际分配车道后再写入DB。
            if (isOrderSent && li != null)
            {
                m_lanePlan.AddLaneInUsing(li, order.ORDER_ID);
            }

            return isOrderSent;
        }

        /// <summary>
        /// 更新Command的版本到Order，更新Command的状态到Task。
        /// </summary>
        /// <param name="order"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private bool UpdateOrderAndTaskByCommand(AGV_Order order, AGV_Command cmd)
        {
            bool bRet = UpdateTaskByCommand(cmd, order);
            
            if (bRet)
            {
                bRet = UpdateOrderVersionByCommand(order, cmd);
            }

            return bRet;
        }

        /// <summary>
        /// 根据Command，更新相应的Order表的CommandId和版本
        /// </summary>
        /// <param name="orderIn"></param>
        /// <param name="cmdIn"></param>
        /// <returns></returns>
        private bool UpdateOrderVersionByCommand(AGV_Order orderIn, AGV_Command cmdIn)
        {
            AGV_Order order = new AGV_Order();
            order.Copy(orderIn);

            order.COMMAND_ID = cmdIn.COMMAND_ID;
            order.COMMAND_VERSION = cmdIn.COMMAND_VERSION;

            order.DATETIME = DateTime.Now;

            bool bRet = UpdateOrderToDbAndCache(order, MethodBase.GetCurrentMethod().Name, ", JobStatus=" + cmdIn.JOB_STATUS);

            return bRet;
        }

        private AGV_Task FindWorkingAgvTaskByContainer(AGV_Command cmd)
        {
            var taskWithContainer = 
                m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task
                    .FirstOrDefault(x =>
                                x.Task.CONTAINER_ID == cmd.CONTAINER_ID
                                && x.Task.JOB_TYPE == cmd.JOB_TYPE
                                && !Helper.IsTaskComplete(x.TaskState)
                            );

            return taskWithContainer;
        }

        private AGV_Order FindWorkingAgvOrderByContainer(AGV_Command cmd)
        {
            var orderWithContainer = 
                m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                    .FirstOrDefault(x =>
                                x.CONTAINER_ID == cmd.CONTAINER_ID
                                && x.JOB_TYPE == cmd.JOB_TYPE
                                && IsOrderInWorking(x)
                            );

            return orderWithContainer;
        }

        private static AGV_Order FindOrderByContainer(AGV_Command cmd, List<AGV_Order> listAgvOrder, List<AGV_Command> listAgvCmd)
        {
            var listOrderWithContainer = listAgvOrder.FindAll(x =>
                    x.CONTAINER_ID == cmd.CONTAINER_ID
                    && x.JOB_TYPE == cmd.JOB_TYPE);

            foreach (var orderTemp in listOrderWithContainer)
            {
                var cmdTemp = listAgvCmd.Find(x => x.COMMAND_ID == orderTemp.COMMAND_ID);
                if (cmdTemp != null && !cmdTemp.IsComplete())
                {
                    return orderTemp;
                }
            }

            return null;
        }

        private AGV_Order GetOrderLink(string strOrderLink)
        {
            if (string.IsNullOrWhiteSpace(strOrderLink))
                return null;

            var orderLink = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                .Find(x => x.ORDER_ID == strOrderLink);

            return orderLink;
        }
        private string GetTosJobLink(AGV_Order order)
        {
            var resJobLink = m_dbDataSchedule.m_DBData_TOS.m_listAGV_ResJob
                .Find(x => x.JOB_LINK == order.JOB_ID);

            if (resJobLink != null)
            {
                return resJobLink.JOB_ID;
            }

            return "";
        }

        /// <summary>
        /// 根据Command JOB_ID，更新相应TOS任务
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="order"></param>
        /// <returns>true - 成功更新Task或不必更新Task； false - 更新Task但失败</returns>
        private bool UpdateTaskByCommand(AGV_Command cmd, AGV_Order order)
        {
            // 新产生的Command不必更新到Tos，其变化只是Command.Command_Version变为1。
            //if (cmd.IsInitial() || cmd.IsReady())
            //if (cmd.IsInitial())
            //    return false;
            if (Helper.GetEnum(cmd.JOB_STATUS, TaskStatus.None) == TaskStatus.None)
                return true; //不必更新Task

            var listAgvTask = m_dbDataSchedule.m_DBData_TOS.m_listAGV_Task;

            var taskOld = listAgvTask.Find(x => x.Task.JOB_ID == order.JOB_ID);

            if (taskOld == null)
            {
                Logger.ECSSchedule.Error("[AGV] UpdateTaskByCommand no task in DB. return false. JOB_ID=" + order.JOB_ID + "order:" + order + " cmd:" + cmd);
                return false; // error
            }

            TaskStatus cmdStatus = Helper.GetEnum(cmd.JOB_STATUS, TaskStatus.None);
            if (cmdStatus == TaskStatus.Enter)
            {
                // 校验任务状态
                if (Helper.IsTaskCompleteFrom(taskOld.TaskState) || Helper.IsTaskComplete(taskOld.TaskState))
                {
                    return true; //不必更新Task
                }
            }

            var req = CreateAgvReqUpdateJob(cmd, order, taskOld.Task);

            bool bRet = DB_TOS.Instance.Update_AGV_Task(req, "AGV");

            Logger.ECSSchedule.Info("[AGV MS->DB_TOS] DB_TOS.Update_AGV_Task() AGV_ReqUpdateJob: "+ Utility.GetString(req));

            Utility.Log("AGV", MethodBase.GetCurrentMethod().Name, bRet, "order:" + order + " cmd:" + cmd);

            if (bRet)
            {
                taskOld.TaskState = cmdStatus;
            }

            return bRet;
        }

        /// <summary>
        /// 根据Command更新，生成发送给Tos的更新数据
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        private AGV_ReqUpdateJob CreateAgvReqUpdateJob(AGV_Command cmd, AGV_Order order, AGV_ResJob job)
        {
            AGV_ReqUpdateJob req = new AGV_ReqUpdateJob
            {
                COMMAND_ID = cmd.COMMAND_ID,
                ORDER_ID = cmd.ORDER_ID,
                VERSION = cmd.VERSION,

                // Order Content
                JOB_TYPE = order.JOB_TYPE,
                JOB_ID = order.JOB_ID,
                JOB_LINK = GetTosJobLink(order),
                CHE_ID = order.CHE_ID,
                YARD_ID = order.YARD_ID,
                QUAY_ID = order.QUAY_ID,

                FROM_TRUCK_ID = order.FROM_TRUCK_ID,
                FROM_TRUCK_TYPE = order.FROM_TRUCK_TYPE,
                FROM_TRUCK_POS = order.FROM_TRUCK_POS,
                FROM_BLOCK = order.FROM_BLOCK,
                FROM_BAY_TYPE = order.FROM_BAY_TYPE,
                FROM_BAY = order.FROM_BAY,
                FROM_LANE = order.FROM_LANE,
                FROM_TIER = order.FROM_TIER,

                // Command Status
                JOB_STATUS = cmd.JOB_STATUS,
                EXCEPTION_CODE = cmd.EXCEPTION_CODE,
                START_TIME = cmd.START_TIME,
                END_TIME = cmd.END_TIME,
                DATETIME = cmd.DATETIME
            };

            if (!string.IsNullOrWhiteSpace(order.TO_LANE))
            {
                req.TO_TRUCK_ID = order.TO_TRUCK_ID;
                req.TO_TRUCK_TYPE = order.TO_TRUCK_TYPE;
                req.TO_TRUCK_POS = order.TO_TRUCK_POS;
                req.TO_BLOCK = order.TO_BLOCK;
                req.TO_BAY_TYPE = order.TO_BAY_TYPE;
                req.TO_BAY = order.TO_BAY;
                req.TO_LANE = order.TO_LANE;
                req.TO_TIER = order.TO_TIER;
            }
            else
            {
                req.TO_TRUCK_ID = job.TO_TRUCK_ID;
                req.TO_TRUCK_TYPE = job.TO_TRUCK_TYPE;
                req.TO_TRUCK_POS = job.TO_TRUCK_POS;
                req.TO_BLOCK = job.TO_BLOCK;
                req.TO_BAY_TYPE = job.TO_BAY_TYPE;
                req.TO_BAY = job.TO_BAY;
                req.TO_LANE = job.TO_LANE;
                req.TO_TIER = job.TO_TIER;
            }

            req.CONTAINER_ID = job.CONTAINER_ID;
            req.CONTAINER_ISO = job.CONTAINER_ISO;
            req.CONTAINER_LENGTH = job.CONTAINER_LENGTH;
            req.CONTAINER_HEIGHT = job.CONTAINER_HEIGHT;
            req.CONTAINER_WEIGHT = job.CONTAINER_WEIGHT;
            req.CONTAINER_IS_EMPTY = job.CONTAINER_IS_EMPTY;
            req.CONTAINER_DOOR_DIRECTION = job.CONTAINER_DOOR_DIRECTION;

            //req.RESERVE = cmd.RESERVE;
            return req;
        }

        /// <summary>
        /// 根据Tos数据，生成发送给Tos的更新数据
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        private AGV_ReqUpdateJob CreateAgvReqUpdateJob(AGV_ResJob job)
        {
            AGV_ReqUpdateJob req = new AGV_ReqUpdateJob
            {
                COMMAND_ID = job.COMMAND_ID,
                ORDER_ID = job.ORDER_ID,
                VERSION = job.VERSION,

                JOB_TYPE = job.JOB_TYPE,
                JOB_ID = job.JOB_ID,
                JOB_LINK = job.JOB_LINK,
                CHE_ID = job.CHE_ID,
                YARD_ID = job.YARD_ID,
                QUAY_ID = job.QUAY_ID,
                FROM_TRUCK_ID = job.FROM_TRUCK_ID,
                FROM_TRUCK_TYPE = job.FROM_TRUCK_TYPE,
                FROM_TRUCK_POS = job.FROM_TRUCK_POS,
                FROM_BLOCK = job.FROM_BLOCK,
                FROM_BAY_TYPE = job.FROM_BAY_TYPE,
                FROM_BAY = job.FROM_BAY,
                FROM_LANE = job.FROM_LANE,
                FROM_TIER = job.FROM_TIER,
                TO_TRUCK_ID = job.TO_TRUCK_ID,
                TO_TRUCK_TYPE = job.TO_TRUCK_TYPE,
                TO_TRUCK_POS = job.TO_TRUCK_POS,
                TO_BLOCK = job.TO_BLOCK,
                TO_BAY_TYPE = job.TO_BAY_TYPE,
                TO_BAY = job.TO_BAY,
                TO_LANE = job.TO_LANE,
                TO_TIER = job.TO_TIER,
                CONTAINER_ID = job.CONTAINER_ID,
                CONTAINER_ISO = job.CONTAINER_ISO,
                CONTAINER_LENGTH = job.CONTAINER_LENGTH,
                CONTAINER_HEIGHT = job.CONTAINER_HEIGHT,
                CONTAINER_WEIGHT = job.CONTAINER_WEIGHT,
                CONTAINER_IS_EMPTY = job.CONTAINER_IS_EMPTY,
                CONTAINER_DOOR_DIRECTION = job.CONTAINER_DOOR_DIRECTION,

                //JOB_STATUS = job.JOB_STATUS,
                //EXCEPTION_CODE = job.EXCEPTION_CODE,
                //START_TIME = job.START_TIME,
                //END_TIME = job.END_TIME,
                DATETIME = job.DATETIME
            };

            //req.RESERVE = .RESERVE;
            return req;
        }

        /// <summary>
        /// 回复Tos取消
        /// </summary>
        /// <param name="job"></param>
        /// <param name="cmd"></param>
        /// <param name="exceptionCode"></param>
        /// <returns></returns>
        private bool UpdateTaskByCancel(AGV_ResJob job, AGV_Command cmd, Exception_Code exceptionCode)
        {
            AGV_ReqUpdateJob req = CreateAgvReqUpdateJob(job);

            req.EXCEPTION_CODE = ((int)exceptionCode).ToString();

            req.JOB_STATUS = TaskStatus.Cancel_OK.ToString();

            if (cmd != null)
            {
                req.COMMAND_ID = cmd.COMMAND_ID;
                req.ORDER_ID = cmd.ORDER_ID;
            }

            bool bRet = DB_TOS.Instance.Update_AGV_Task(req, "AGV");

            Utility.Log("AGV->DB_TOS", "DB_TOS.Update_AGV_Task()", bRet, "AGV_ReqUpdateJob: " + Utility.GetString(req));

            return bRet;
        }

        private bool InsertResJobToDbAndCache(AGV_ResJob resJob)
        {
            bool bRet = DB_TOS.Instance.Insert_AGV_ResJob(resJob);

            Utility.Log("AGV", MethodBase.GetCurrentMethod().Name, bRet, Utility.GetString(resJob));

            if (bRet)
            {
                m_dbDataSchedule.m_DBData_TOS.m_listAGV_ResJob.Add(resJob);
            }

            return bRet;
        }

        private bool InsertOrderToDbAndCache(AGV_Order order, string source = "")
        {
            order.DATETIME = DateTime.Now;

            bool bRet = DB_ECS.Instance.Insert_AGV_Order(order);

            Utility.Log("AGV", source, bRet, "Insert " + order);

            if (bRet)
            {
                m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order.Add(order);
            }

            return bRet;
        }

        public bool UpdateOrderToDbAndCache(AGV_Order order, string source, string extraInfo="")
        {
            order.DATETIME = DateTime.Now;

            bool bRet = DB_ECS.Instance.Update_AGV_Order(order);

            Utility.Log("AGV", source, bRet, "Update " + order + extraInfo);

            if (!bRet)
            {
                return false;
            }

            var listAgvOrder = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order;

            var orderOld = listAgvOrder.Find(x => x.ORDER_ID == order.ORDER_ID);
            if (orderOld == null)
            {
                listAgvOrder.Add(order);
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
        public bool UpdateOrderToDbAndCacheEx(AGV_Order order, string source)
        {
            order.JOB_STATUS = ResJobStatus.Update.ToString();

            order.ORDER_VERSION = Utility.IncrementStringAsInt(order.ORDER_VERSION);

            return UpdateOrderToDbAndCache(order, source);
        }

        /// <summary>
        /// 响应JobManager_TOS的Event
        /// </summary>
        /// <param name="job">AGV_ResJob</param>
        /// <param name="e">NA</param>
        /// <returns>0：不允许更新，1：允许更新，2：未知</returns>
        private int OnJobManagerEvent(AGV_ResJob job, EventArgs e)
        {
            lock (m_objAgvScheduleLock)
            {
                if (job == null)
                {
                    Utility.Log("AGV", MethodBase.GetCurrentMethod().Name, "result=1", "job is null");
                    return (int)TosUpdateResult.Permit;
                }

                ResJobStatus jobStatus = Helper.GetEnum(job.JOB_STATUS, ResJobStatus.Unknown);

                AGV_Order order = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Order
                    .Find(x => x.JOB_ID == job.JOB_ID);

                Utility.Log("AGV", MethodBase.GetCurrentMethod().Name + " " + jobStatus, "Order", order == null ? "null" : order.ToString());
                Utility.Log("AGV", MethodBase.GetCurrentMethod().Name + " " + jobStatus, "ResJob", Utility.GetString(job));

                bool bRet = false;
                if (jobStatus == ResJobStatus.New)
                {
                    return (int)TosUpdateResult.Permit;
                }
                else if (jobStatus == ResJobStatus.Update)
                {
                    if (order == null)
                        return (int)TosUpdateResult.Permit;

                    return (int)TosUpdateResult.Permit;
                    //bRet = UpdateOrderByResJob(order, job);
                }
                else if (jobStatus == ResJobStatus.Cancel)
                {
                    bRet = CancelOrderByTos(order, job);
                }
                else
                {
                    return (int)TosUpdateResult.UnKnown;
                }

                int iRet = (int)(bRet ? TosUpdateResult.Permit : TosUpdateResult.NotPermit);

                Utility.Log("AGV", MethodBase.GetCurrentMethod().Name + " " + jobStatus, "result=" + iRet, order == null ? "null" : order.ToString());

                return iRet;
            }
        }

        private bool FillOrderToPositionByResJob(AGV_Order order)
        {
            AGV_ResJob job = m_dbDataSchedule.m_DBData_TOS.m_listAGV_ResJob
                            .Find(x => x.JOB_ID == order.JOB_ID);

            if (job == null)
            {
                return false;
            }

            Utility.FillOrderToPosition(order, job);

            return true;
        }

        /// <summary>
        /// Update Order
        /// </summary>
        /// <param name="orderIn"></param>
        /// <param name="job"></param>
        /// <returns></returns>
        public bool UpdateOrderByResJob(AGV_Order orderIn, AGV_ResJob job)
        {
            bool bRet = false;

            AGV_Order order = new AGV_Order();
            order.Copy(orderIn);

            Utility.FillOrderContent(order, job);

            bRet = UpdateOrderToDbAndCacheEx(order, "UpdateOrderByResJob");

            return bRet;
        }

        /// <summary>
        /// Cancel Order
        /// </summary>
        /// <param name="orderIn"></param>
        /// <param name="job"></param>
        /// <returns></returns>
        private bool CancelOrderByTos(AGV_Order orderIn, AGV_ResJob job)
        {
            bool bRet = false;

            if (orderIn == null)
            {
                Logger.ECSSchedule.Info("[AGV] CancelOrder order is not start.");

                bRet = UpdateTaskByCancel(job, null, Exception_Code.CancelCode_NoStart);
            }
            else
            {
                AGV_Command cmd = m_dbDataSchedule.m_DBData_VMS.m_listAGV_Command.Find(x => x.ORDER_ID == orderIn.ORDER_ID);
                if (cmd != null && cmd.IsComplete())
                {
                    Logger.ECSSchedule.Info("[AGV] CancelOrder order is complete.");

                    bRet = UpdateTaskByCancel(job, cmd, Exception_Code.CancelCode_AlmostComplete);
                }
                else
                {
                    Logger.ECSSchedule.Info("[AGV] CancelOrder order is working.");

                    AGV_Order order = new AGV_Order();
                    order.Copy(orderIn);

                    order.JOB_STATUS = ResJobStatus.Cancel.ToString();

                    order.ORDER_VERSION = Utility.IncrementStringAsInt(order.ORDER_VERSION);

                    bRet = UpdateOrderToDbAndCache(order, MethodBase.GetCurrentMethod().Name);
                }
            }

            return bRet;
        }
    }
}
