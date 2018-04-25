using System;
using System.Collections.Generic;
using System.Reflection;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DB;
using ZECS.Schedule.Define;
using ZECS.Schedule.Define.DBDefine.Schedule;

namespace ZECS.Schedule.ECSSchedule
{
    public class Schedule_STS 
    {
        private static Schedule_STS s_instance;
        public static Schedule_STS Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new Schedule_STS();
                }
                return s_instance;
            }
        }

        private Object m_lockListTaskCanDo = new object();
        private List<STS_Task> m_listTaskCanDo;

        private Object m_objStsScheduleLock = new object();
        private DBData_Schedule m_dbDataSchedule;
        private bool m_bAddedJobManagerEvent;

        public  bool Start()
        {
            m_bAddedJobManagerEvent = false;
            return true;
        }

        public void Stop()
        {
            m_bAddedJobManagerEvent = false;
            DB_TOS.Instance.STS_JobManagerScheduleEvent -= OnJobManagerEvent;
        }


        /*每QC输出一条Job,处理状态反馈.
         * 1.选一个任务下发给ECS
         * 2.卸船时通知AGV至PB,装船至WSTP
         * 3.OCR信息,箱号更新
         * 4.处理状态反馈
         */
        public void Schedule(ref DBData_Schedule dbData_Schedule,
                              List<WORK_INSTRUCTION_STATUS> listWITopLogicSorted)
        {
            lock (m_objStsScheduleLock)
            {
                m_dbDataSchedule = dbData_Schedule;

                if (!m_bAddedJobManagerEvent)
                {
                    DB_TOS.Instance.STS_JobManagerScheduleEvent += OnJobManagerEvent;
                    m_bAddedJobManagerEvent = true;
                }

                //1.从偏序表中选择任务，缓存到待发任务列表，以备QCMS调用ReqOrder时可从中选一个任务下发
                SetTaskStsCanDo(listWITopLogicSorted);

                //2.反馈任务状态给TOS
                UpdateOrderAndTaskByCommand();
            }
        }

        public List<STS_Order> ReqOrder(object objParam)
        {
            List<STS_Order> listNewOrder = null;

            ReqOrderParam rop = objParam as ReqOrderParam;
            if (rop == null
                || (rop.Move_Kind != ZECS.Common.Define.JobType.DISC && rop.Move_Kind != ZECS.Common.Define.JobType.LOAD)
                )
            {
                return null;
            }

            STS_Order stsOrder = null;
            STS_Task taskMatched = null;

            lock (m_lockListTaskCanDo)
            {
                taskMatched = FindTask(rop, m_listTaskCanDo);
            }

            if (taskMatched != null)
            {
                CreateStsOrder(taskMatched, rop.COMMAND_ID, out stsOrder);
            }

            if (stsOrder != null)
            {
                // 写入Order到DB
                bool bRet = InsertOrderToDbAndCache(stsOrder, MethodBase.GetCurrentMethod().Name);

                if (bRet)
                {
                    listNewOrder = new List<STS_Order>();
                    listNewOrder.Add(stsOrder);
                }
            }

            return listNewOrder;
        }

        private STS_Task FindTask(ReqOrderParam rop, List<STS_Task> listTask)
        {
            foreach (var task in listTask)
            {
                if (string.Compare(rop.QUAY_ID, task.Task.QUAY_ID, true) != 0)
                {
                    continue;
                }

                if (Helper.GetEnum(task.Task.JOB_TYPE, ZECS.Common.Define.JobType.UNKNOWN) != rop.Move_Kind)
                {
                    continue;
                }

                int nContrainerLength = 0;
                int.TryParse(task.Task.CONTAINER_LENGTH, out nContrainerLength);
                if (Utility.ContainerLength2Type(nContrainerLength) != rop.CTN_Type)
                {
                    continue;
                }

                if (rop.Move_Kind == ZECS.Common.Define.JobType.DISC
                    || (rop.Move_Kind == ZECS.Common.Define.JobType.LOAD && string.Compare(task.Task.CONTAINER_ID, rop.CTN_ID, true) == 0)
                    )
                {
                    return task;
                }
            }

            return null;
        }

        private List<STS_Task> WiList2StsTaskList(List<WORK_INSTRUCTION_STATUS> listWi, List<STS_Task> listStsTask)
        {
            var listStsTaskConverted = new List<STS_Task>();

            foreach (var wi in listWi)
            {
                STS_Task task = Utility.FindTaskByWi(wi, listStsTask);
                if (task != null)
                {
                    listStsTaskConverted.Add(task);
                }
            }

            return listStsTaskConverted;
        }

        /// <summary>
        /// 根据还未开始执行的Task产生Order
        /// </summary>
        /// <param name="task"></param>
        /// <param name="COMMAND_ID"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        private bool CreateStsOrder(STS_Task task, string COMMAND_ID, out STS_Order order)
        {
            order = null;
            STS_ResJob resJob = task.Task;

            if (task.TaskState != TaskStatus.None && task.TaskState != TaskStatus.Almost_Ready)
            {
                return false;
            }

            order = (STS_Order)Utility.CreateOrderByResJob(resJob);

            CreateOrderIdAndOrderLink(resJob, ref order);

            return true;
        }

        private void CreateOrderIdAndOrderLink(STS_ResJob job, ref STS_Order order)
        {
            long newOrderId = Utility.CreateNewOrderId("QCMS", m_dbDataSchedule);

            if (string.IsNullOrWhiteSpace(job.JOB_LINK))
            {
                order.ORDER_ID = Convert.ToString(newOrderId);
                return;
            }
            
            var orderLink = m_dbDataSchedule.m_DBData_STSMS.m_listSTS_Order
                .Find(x => x.JOB_ID == job.JOB_LINK);

            if (orderLink == null)
            {
                order.ORDER_ID = Convert.ToString(newOrderId);
                order.ORDER_LINK = Convert.ToString(newOrderId+1);
            }
            else
            {
                order.ORDER_ID = orderLink.ORDER_LINK;
                order.ORDER_LINK = orderLink.ORDER_ID;
            }
        }

        /// <summary>
        /// 从偏序表中选择可下发的Order并缓存,以供ECS请求时从缓存中直接选一个匹配的Order
        /// </summary>
        /// <returns></returns>
        private bool SetTaskStsCanDo(List<WORK_INSTRUCTION_STATUS> listWiTopLogicSorted)
        {
            var listTaskCanDo = WiList2StsTaskList(listWiTopLogicSorted, m_dbDataSchedule.m_DBData_TOS.m_listSTS_Task);

            lock (m_lockListTaskCanDo)
            {
                m_listTaskCanDo = listTaskCanDo;
            }

            return true;
        }

        /// <summary>
        /// 反馈任务状态给TOS
        /// 根据CommandVersion和OrderVersion的比对来判断任务是否有更新，如有，则更新至TOS
        /// </summary>
        /// <param name="dbDataSchedule"></param>
        /// <returns></returns>
        private bool UpdateOrderAndTaskByCommand()
        {
            bool bRet = false;
            var listStsCommand = m_dbDataSchedule.m_DBData_STSMS.m_listSTS_Command;
            var listStsOrder = m_dbDataSchedule.m_DBData_STSMS.m_listSTS_Order;

            foreach (STS_Command cmd in listStsCommand)
            {
                STS_Order order = listStsOrder.Find(x => x.COMMAND_ID == cmd.COMMAND_ID);

                // order本应在ECS调用ReqOrder()时创建，但目前ECS或模拟器中还没该功能，
                // 所以在调度监测到ECS或模拟器已产生Command时创建Order
                if (order == null)
                {
                    order = CreateOrderByCommand(cmd);
                    if (order != null)
                    {
                        InsertOrderToDbAndCache(order, MethodBase.GetCurrentMethod().Name);
                    }
                }

                if (Utility.IsCommandChanged(cmd, order))
                {
                    bRet = UpdateTaskByCommand(cmd, order);

                    if (bRet)
                    {
                        bRet = UpdateOrderVersionByCommand(cmd, ref order);
                    }
                }
            }
            
            return bRet;
        }

        /// <summary>
        /// 根据Command创建Order。
        /// Command list中可能会存在一个JobId对应多个Command，此时进行容错处理，保证一个JobId只有一个对应的Order。
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private STS_Order CreateOrderByCommand(STS_Command cmd)
        {
            if (cmd == null)
            {
                return null;
            }

            STS_ResJob job = m_dbDataSchedule.m_DBData_TOS.m_listSTS_ResJob
                .Find(x => x.JOB_ID == cmd.JOB_ID);

            if (job == null)
                return null;
            //当前任务为Cancel或Unknown时不发Order
            if (job.JobStatus() != ResJobStatus.New && job.JobStatus() != ResJobStatus.Update)
                return null;

            var listStsCmd = m_dbDataSchedule.m_DBData_STSMS.m_listSTS_Command;

            // 检查Command中的JobID是否重复
            var listCmd = listStsCmd.FindAll(x => x.JOB_ID == cmd.JOB_ID);
            if (listCmd.Count >= 2)
            {
                Logger.ECSSchedule.Error("[STS] ERROR!!! duplicate commands with SAME Job ID! command: " + cmd);
                foreach (var dupCmd in listCmd)
                {
                    Logger.ECSSchedule.Error("[STS] ERROR!!! duplicate commands with SAME Job ID in db: " + dupCmd);
                }
                //return null;
            }

            // 一个JobId只有一个对应的Order
            STS_Order oldOrder = m_dbDataSchedule.m_DBData_STSMS.m_listSTS_Order.Find(x => x.JOB_ID == cmd.JOB_ID);
            if (oldOrder != null)
            {
                Logger.ECSSchedule.Warn("[STS] order is already created for command: " + cmd);
                return null;
            }

            STS_Order order = new STS_Order();

            order.CopyCmd(cmd);

            order.JOB_STATUS = ResJobStatus.New.ToString();
            order.JOB_ID = cmd.JOB_ID;

            order.COMMAND_ID = cmd.COMMAND_ID;
            order.ORDER_ID = cmd.ORDER_ID;
            order.COMMAND_VERSION = cmd.COMMAND_VERSION;
            order.ORDER_VERSION = "1";
            order.DATETIME = DateTime.Now;

            STS_ResJob resJob = m_dbDataSchedule.m_DBData_TOS.m_listSTS_ResJob
                .Find(x => x.JOB_ID == order.JOB_ID);

            if (resJob != null)
            {
                order.PRIORITY = resJob.PRIORITY;
                order.PLATFORM_CONFIRM = resJob.PLATFORM_CONFIRM;
                order.MOVE_NO = resJob.MOVE_NO;
                order.VESSEL_ID = resJob.VESSEL_ID;
                order.VERSION = resJob.VERSION;
            }

            // 填充双箱
            if (resJob != null && !string.IsNullOrWhiteSpace(resJob.JOB_LINK))
            {
                STS_ResJob jobLink = m_dbDataSchedule.m_DBData_TOS.m_listSTS_ResJob
                    .Find(x => x.JOB_ID == resJob.JOB_LINK);
                
                //jobLink的状态为Cancel时，job.OrderLink = null，
                //其他状态job.OrderLink = jobLink.OrderID ;
                if (jobLink != null)
                {
                    //jobLink为New或者Update时更新job.OrderLink
                    if (jobLink.JobStatus() == ResJobStatus.New
                        || jobLink.JobStatus() == ResJobStatus.Update)
                    {
                        var cmdLink = listStsCmd.Find(x => x.JOB_ID == resJob.JOB_LINK);
                        if (cmdLink == null)
                        {
                            Logger.ECSSchedule.Error(
                                "[STS] Job's JOB_LINK is not empty, but the link command does not exist in db. cmd: " + cmd +
                                " ResJob: " + Utility.GetString(resJob));
                            return null;
                        }

                        order.ORDER_LINK = cmdLink.ORDER_ID;
                    }
                    else
                    {
                        //jobLink为Cancel或者Unknown时job.OrderLink=null
                        //order.ORDER_LINK = null; 
                    }
                } 
            }

            return order;
        }

        private string GetTosJobLink(string jobId)
        {
            var resJobLink = m_dbDataSchedule.m_DBData_TOS.m_listSTS_ResJob
                .Find(x => x.JOB_LINK == jobId);

            if (resJobLink != null)
            {
                return resJobLink.JOB_ID;
            }

            return "";
        }

        // 根据Command，更新相应的Order表
        private bool UpdateOrderVersionByCommand(STS_Command cmd, ref STS_Order order)
        {
            order.DATETIME = DateTime.Now;

            order.COMMAND_VERSION = cmd.COMMAND_VERSION;
            order.CHE_ID = cmd.CHE_ID; // MS可能换设备执行任务

            return UpdateOrderToDbAndCache(order, MethodBase.GetCurrentMethod().Name);
        }

        /// <summary>
        /// 根据Command JOB_ID，更新相应TOS任务
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="order"></param>
        /// <returns>true - 成功更新Task或不必更新Task； false - 更新Task但失败</returns>
        private bool UpdateTaskByCommand(STS_Command cmd, STS_Order order)
        {
            // 新产生的Command不必更新到Tos，其变化只是Command.Command_Version变为1。
            //if (cmd.IsInitial() || cmd.IsReady())
            //if (cmd.IsInitial())
            //    return false;
            if (Helper.GetEnum(cmd.JOB_STATUS, TaskStatus.None) == TaskStatus.None)
                return true; //不必更新Task

            var req = CreateStsReqUpdateJob(cmd);

            bool bRet = DB_TOS.Instance.Update_STS_Task(req, req.QUAY_ID);

            Utility.Log("STS", MethodBase.GetCurrentMethod().Name, bRet, "order:" + order + " cmd:" + cmd.JOB_STATUS);

            Utility.Log("STS MS->DB_TOS", "DB_TOS.Update_STS_Task()", bRet, "STS_ReqUpdateJob: " + Utility.GetString(req));

            if (bRet)
            {
                var taskOld = m_dbDataSchedule.m_DBData_TOS.m_listSTS_Task.Find(x => x.Task.JOB_ID == order.JOB_ID);

                if (taskOld != null)
                {
                    taskOld.TaskState = cmd.GetTosStatus();
                }
            }

            return bRet;
        }

        private bool UpdateTaskByCancel(STS_ResJob job, STS_Command cmd, Exception_Code exceptionCode)
        {
            STS_ReqUpdateJob req = CreateStsReqUpdateJob(job);

            req.EXCEPTION_CODE = ((int)exceptionCode).ToString();

            req.JOB_STATUS = TaskStatus.Cancel_OK.ToString();

            if (cmd != null)
            {
                req.COMMAND_ID = cmd.COMMAND_ID;
                req.ORDER_ID = cmd.ORDER_ID;
            }

            bool bRet = DB_TOS.Instance.Update_STS_Task(req, req.QUAY_ID);

            Utility.Log("STS->DB_TOS", "DB_TOS.Update_STS_Task()", bRet, " STS_ReqUpdateJob: " + Utility.GetString(req));

            return bRet;
        }

        private STS_ReqUpdateJob CreateStsReqUpdateJob(STS_Command cmd)
        {
            STS_ReqUpdateJob req = new STS_ReqUpdateJob
            {
                COMMAND_ID = cmd.COMMAND_ID,
                ORDER_ID = cmd.ORDER_ID,
                VERSION = cmd.VERSION,
                JOB_TYPE = cmd.JOB_TYPE,
                JOB_ID = cmd.JOB_ID,
                JOB_LINK = GetTosJobLink(cmd.JOB_ID),
                CHE_ID = cmd.CHE_ID,
                QUAY_ID = cmd.QUAY_ID,
                JOB_STATUS = cmd.JOB_STATUS,
                EXCEPTION_CODE = cmd.EXCEPTION_CODE,
                FROM_TRUCK_TYPE = cmd.FROM_TRUCK_TYPE,
                FROM_TRUCK_ID = cmd.FROM_TRUCK_ID,
                FROM_TRUCK_POS = cmd.FROM_TRUCK_POS,
                FROM_BAY = cmd.FROM_BAY,
                FROM_BAY_TYPE = cmd.FROM_BAY_TYPE,
                FROM_LANE = cmd.FROM_LANE,
                FROM_TIER = cmd.FROM_TIER,
                TO_TRUCK_TYPE = cmd.TO_TRUCK_TYPE,
                TO_TRUCK_ID = cmd.TO_TRUCK_ID,
                TO_TRUCK_POS = cmd.TO_TRUCK_POS,
                TO_BAY_TYPE = cmd.TO_BAY_TYPE,
                TO_BAY = cmd.TO_BAY,
                TO_LANE = cmd.TO_LANE,
                TO_TIER = cmd.TO_TIER,
                CONTAINER_ID = cmd.CONTAINER_ID,
                CONTAINER_ISO = cmd.CONTAINER_ISO,
                CONTAINER_LENGTH = cmd.CONTAINER_LENGTH,
                CONTAINER_HEIGHT = cmd.CONTAINER_HEIGHT,
                CONTAINER_WEIGHT = cmd.CONTAINER_WEIGHT,
                CONTAINER_IS_EMPTY = cmd.CONTAINER_IS_EMPTY,
                CONTAINER_DOOR_DIRECTION = cmd.CONTAINER_DOOR_DIRECTION,
                VESSEL_ID = cmd.VESSEL_ID,
                START_TIME = cmd.START_TIME,
                END_TIME = cmd.END_TIME,
                DATETIME = cmd.DATETIME,
                FROM_RFID = cmd.FROM_RFID,
                TO_RFID = cmd.TO_RFID
            };

            //req.RESERVE = cmd.RESERVE;
            //req.CHE_TYPE = cmd.CHE_TYPE;
            //req.YARD_ID = cmd.YARD_ID;
            return req;
        }

        private STS_ReqUpdateJob CreateStsReqUpdateJob(STS_ResJob job)
        {
            STS_ReqUpdateJob req = new STS_ReqUpdateJob
            {
                COMMAND_ID = job.COMMAND_ID,
                ORDER_ID = job.ORDER_ID,
                VERSION = job.VERSION,
                JOB_TYPE = job.JOB_TYPE,
                JOB_ID = job.JOB_ID,
                JOB_LINK = job.JOB_LINK,
                CHE_ID = job.CHE_ID,
                QUAY_ID = job.QUAY_ID,
                FROM_TRUCK_TYPE = job.FROM_TRUCK_TYPE,
                FROM_TRUCK_ID = job.FROM_TRUCK_ID,
                FROM_TRUCK_POS = job.FROM_TRUCK_POS,
                FROM_BAY = job.FROM_BAY,
                FROM_BAY_TYPE = job.FROM_BAY_TYPE,
                FROM_LANE = job.FROM_LANE,
                FROM_TIER = job.FROM_TIER,
                TO_TRUCK_TYPE = job.TO_TRUCK_TYPE,
                TO_TRUCK_ID = job.TO_TRUCK_ID,
                TO_TRUCK_POS = job.TO_TRUCK_POS,
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
                VESSEL_ID = job.VESSEL_ID,
                //START_TIME = job.START_TIME,
                //END_TIME = job.END_TIME,
                DATETIME = job.DATETIME,
                FROM_RFID = job.FROM_RFID,
                TO_RFID = job.TO_RFID,

                RESERVE = job.RESERVE,
                //req.CHE_TYPE = job.CHE_TYPE,
                YARD_ID = job.YARD_ID,
            };

            var task = m_dbDataSchedule.m_DBData_TOS.m_listSTS_Task.Find(x => x.Task.JOB_ID == job.JOB_ID);
            if (task != null)
            {
                req.JOB_STATUS = task.TaskState.ToString();
            }

            return req;
        }

        private bool InsertOrderToDbAndCache(STS_Order order, string source)
        {
            order.DATETIME = DateTime.Now;

            bool bRet = DB_ECS.Instance.Insert_STS_Order(order);

            Utility.Log("STS", source, bRet, "Insert " + order);

            if (bRet)
            {
                m_dbDataSchedule.m_DBData_STSMS.m_listSTS_Order.Add(order);
            }

            return bRet;
        }

        private bool UpdateOrderToDbAndCache(STS_Order order, string source)
        {
            order.DATETIME = DateTime.Now;

            bool bRet = DB_ECS.Instance.Update_STS_Order(order);

            Utility.Log("STS", source, bRet, "Update " + order);

            if (!bRet)
            {
                return false;
            }

            var listStsOrder = m_dbDataSchedule.m_DBData_STSMS.m_listSTS_Order;

            var orderOld = listStsOrder.Find(x => x.ORDER_ID == order.ORDER_ID);
            if (orderOld == null)
            {
                listStsOrder.Add(order);
            }
            else
            {
                orderOld.Copy(order);
            }

            return true;
        }

        /// <summary>
        /// 响应JobManager_TOS的Event
        /// </summary>
        /// <param name="job">STS_ResJob</param>
        /// <param name="e">NA</param>
        /// <returns>0：不允许更新，1：允许更新，2：未知</returns>
        private int OnJobManagerEvent(STS_ResJob job, EventArgs e)
        {
            lock (m_objStsScheduleLock)
            {
                if (job == null)
                {
                    Utility.Log("STS", MethodBase.GetCurrentMethod().Name, "result=1", "job is null");
                    return (int)TosUpdateResult.Permit;
                }

                ResJobStatus jobStatus = Helper.GetEnum(job.JOB_STATUS, ResJobStatus.Unknown);

                STS_Order order = m_dbDataSchedule.m_DBData_STSMS.m_listSTS_Order
                        .Find(x => x.JOB_ID == job.JOB_ID);

                Utility.Log("STS", MethodBase.GetCurrentMethod().Name + " " + jobStatus, " Order", order == null ? "null" : order.ToString());
                Utility.Log("STS", MethodBase.GetCurrentMethod().Name + " " + jobStatus, " ResJob", Utility.GetString(job));

                bool bRet = false;
                if (jobStatus == ResJobStatus.New)
                {
                    // STS 创建Command，不应有New Job。
                    return (int)TosUpdateResult.Permit;
                }
                else if (jobStatus == ResJobStatus.Update)
                {
                    if (order == null)
                        return (int)TosUpdateResult.Permit;

                    bRet = UpdateOrderByResJob(ref order, job);

                    if (!bRet)
                    {
                        Logger.ECSSchedule.Error("[STS] OnJobManagerEvent UpdateOrderByResJob() Failed! STS Job: " + Utility.GetString(job));
                    }
                }
                else if (jobStatus == ResJobStatus.Cancel)
                {
                    bRet = CancelOrderByTos(order, job);

                    if (!bRet)
                    {
                        Logger.ECSSchedule.Error("[STS] OnJobManagerEvent CancelOrderByTos() Failed! STS Job: " + Utility.GetString(job));
                    }
                }
                else
                {
                    //return (int)TosUpdateResult.UnKnown;
                    return (int) TosUpdateResult.Permit;    // 无条件返回成功
                }

                bRet = true;   // 无条件返回成功

                int iRet = (int)(bRet ? TosUpdateResult.Permit : TosUpdateResult.NotPermit);

                Utility.Log("STS", MethodBase.GetCurrentMethod().Name + " " + jobStatus, "result=" + iRet, order == null ? "null" : order.ToString());

                return iRet;
            }
        }

        private bool UpdateOrderByResJob(ref STS_Order order, STS_ResJob job)
        {
            bool bRet = false;

            order.ORDER_VERSION = Utility.IncrementStringAsInt(order.ORDER_VERSION);

            order.JOB_STATUS = ResJobStatus.Update.ToString();

            order.JOB_TYPE = job.JOB_TYPE;
            //order.JOB_ID = job.JOB_ID;
            //order.ORDER_LINK = job.JOB_LINK;

            Utility.FillOrderContent(order, job);

            order.DATETIME = DateTime.Now;

            bRet = UpdateOrderToDbAndCache(order, MethodBase.GetCurrentMethod().Name);

            return bRet;
        }

        /// <summary>
        /// 根据Tos的指示取消Order
        /// </summary>
        /// <param name="order"></param>
        /// <param name="job"></param>
        /// <returns></returns>
        private bool CancelOrderByTos(STS_Order order, STS_ResJob job)
        {
            bool bRet = false;

            if (order == null)
            {
                Logger.ECSSchedule.Info("[STS] CancelOrder order is not start.");

                bRet = UpdateTaskByCancel(job, null, Exception_Code.CancelCode_NoStart);
            }
            else
            {
                STS_Command cmd = m_dbDataSchedule.m_DBData_STSMS.m_listSTS_Command.Find(x => x.ORDER_ID == order.ORDER_ID);
                if (cmd != null && cmd.IsComplete())
                {
                    Logger.ECSSchedule.Info("[STS] CancelOrder order is complete.");

                    bRet = UpdateTaskByCancel(job, cmd, Exception_Code.CancelCode_AlmostComplete);
                }
                else
                {
                    Logger.ECSSchedule.Info("[STS] CancelOrder order is working.");

                    order.JOB_STATUS = ResJobStatus.Cancel.ToString();

                    order.ORDER_VERSION = Utility.IncrementStringAsInt(order.ORDER_VERSION);

                    bRet = UpdateOrderToDbAndCache(order, MethodBase.GetCurrentMethod().Name);
                }
            }

            return bRet;
        }
    }
}
