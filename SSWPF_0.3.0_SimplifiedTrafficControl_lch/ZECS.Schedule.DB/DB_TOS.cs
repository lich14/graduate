using System;
using System.Linq;
using System.Collections.Generic;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Define;
using SSWPF;
using SSWPF.Define;

namespace ZECS.Schedule.DB
{
    public delegate int STS_JobManagerScheduleEventDelegate(STS_ResJob sender, EventArgs e);//用于TOS更新和删除任务时的委托
    public delegate int AGV_JobManagerScheduleEventDelegate(AGV_ResJob sender, EventArgs e);//用于TOS更新和删除任务时的委托
    public delegate int ASC_JobManagerScheduleEventDelegate(ASC_ResJob sender, EventArgs e);//用于TOS更新和删除任务时的委托

    public class DB_TOS
    {
        public event STS_JobManagerScheduleEventDelegate STS_JobManagerScheduleEvent;
        public event AGV_JobManagerScheduleEventDelegate AGV_JobManagerScheduleEvent;
        public event ASC_JobManagerScheduleEventDelegate ASC_JobManagerScheduleEvent;

        private static DB_TOS s_instance;
        public static DB_TOS Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new DB_TOS();
                }
                return s_instance;
            }
        }
        public SimDataStore oSimDataStore;

        public void Start(DatabaseConfig dbTOS)
        {
            //Logger.JobManager_TOS.Info("DB_TOS Started");
        }

        public bool Stop()
        {
            return false;
        }

        /// <summary>
        /// 生成一个新的任务编号，从100000000000000开始编号，每次加1，此接口主要供调度模块使用
        /// </summary>
        /// <returns>新的任务编号</returns>
        public long CreateNewJobID()
        {
            long lMaxJobID = 0;
            long lNewJobID = 0;
            try
            {
                lMaxJobID = GetMaxJobID();
                if (lMaxJobID < 100000000000000)
                    lNewJobID = 100000000000000;
                else
                    lNewJobID = lMaxJobID + 1;
            }
            catch (Exception ex)
            {
                return -1;
            }
            return lNewJobID;

        }

        /// <summary>
        /// 获取最大的任务编号
        /// </summary>
        /// <returns>返回最大的任务编号</returns>
        private long GetMaxJobID()
        {
            long nJobID = 0;
            List<string> lJobIDsInStr;
            
            lJobIDsInStr = oSimDataStore.dSTSResJobs.Values.Select(u => u.JOB_ID).ToList();
            lJobIDsInStr.AddRange(oSimDataStore.dASCResJobs.Values.Select(u => u.JOB_ID).ToList());
            lJobIDsInStr.AddRange(oSimDataStore.dAGVResJobs.Values.Select(u => u.JOB_ID).ToList());

            lJobIDsInStr.ForEach(u => nJobID = Convert.ToInt64(u) > nJobID ? Convert.ToInt64(u) : nJobID);

            return nJobID;
        }

        /// <summary>
        /// 生成一个新的OrderID，比当前最大的OrderID加1
        /// </summary>
        /// <param name="systemName">BMS，VMS，QCMS</param>
        /// <returns>一个新的OrderID</returns>
        public long CreateNewOrderID(string systemName)
        {
            long lMaxOrderID = 0;
            long lNewOrderID = 0;
            try
            {
                lMaxOrderID = GetMaxOrderID(systemName);
                if (lMaxOrderID < 0)
                    lNewOrderID = 0;
                else
                    lNewOrderID = lMaxOrderID;
                lNewOrderID = lMaxOrderID + 1;
            }
            catch (Exception ex)
            {
                return -1;
            }
            return lNewOrderID;
        }

        /// <summary>
        /// 获取最大的任务编号
        /// </summary>
        /// <returns>返回最大的任务编号</returns>
        private long GetMaxOrderID(string systemName)
        {
            long nOrderID = 0;
            List<string> lOrderIDsInStr;

            lOrderIDsInStr = oSimDataStore.dSTSOrders.Values.Select(u => u.ORDER_ID).ToList();
            lOrderIDsInStr.AddRange(oSimDataStore.dASCOrders.Values.Select(u => u.ORDER_ID).ToList());
            lOrderIDsInStr.AddRange(oSimDataStore.dAGVOrders.Values.Select(u => u.ORDER_ID).ToList());

            lOrderIDsInStr.ForEach(u => nOrderID = Convert.ToInt64(u) > nOrderID ? Convert.ToInt64(u) : nOrderID);

            return nOrderID;
        }

        /// 获取泊位状态列表
        public List<BERTH_STATUS> GetList_BERTH_STATUS()
        {
            BERTH_STATUS oBS;
            List<BERTH_STATUS> lBSs = new List<BERTH_STATUS>();

            try
            {
                foreach (BERTH_STATUS obj in oSimDataStore.dViewBerthStatus.Values)
                {
                    oBS = Helper.Clone<BERTH_STATUS>(obj);
                    lBSs.Add(oBS);
                }
            }
            catch (Exception ex)
            {
                Logger.JobManager_TOS.Info(ex.ToString());
                return null;
            }

            return lBSs;
        }

        /// 获取STS的WorkQueue状态列表
        public List<STS_WORK_QUEUE_STATUS> GetList_STS_WORK_QUEUE_STATUS()
        {
            STS_WORK_QUEUE_STATUS oSWQS;
            List<STS_WORK_QUEUE_STATUS> lSWQSs = new List<STS_WORK_QUEUE_STATUS>();

            try
            {
                foreach (STS_WORK_QUEUE_STATUS obj in oSimDataStore.dViewWorkQueues.Values)
                {
                    oSWQS = Helper.Clone<STS_WORK_QUEUE_STATUS>(obj);
                    lSWQSs.Add(oSWQS);
                }
            }
            catch (Exception ex)
            {
                Logger.JobManager_TOS.Info(ex.ToString());
                return null;
            }

            return lSWQSs;
        }

        /// 获取STS的WorkInstruction状态列表
        public List<WORK_INSTRUCTION_STATUS> GetList_WORK_INSTRUCTION_STATUS()
        {
            WORK_INSTRUCTION_STATUS oWIS;
            List<WORK_INSTRUCTION_STATUS> lWISs = new List<WORK_INSTRUCTION_STATUS>();

            try
            {
                foreach (WORK_INSTRUCTION_STATUS obj in oSimDataStore.dViewWorkInstructions.Values)
                {
                    oWIS = Helper.Clone<WORK_INSTRUCTION_STATUS>(obj);
                    lWISs.Add(oWIS);
                }
            }
            catch (Exception ex)
            {
                Logger.JobManager_TOS.Info(ex.ToString());
                return null;
            }

            return lWISs;
        }

        /// 获取 STS_ResJob 列表
        public List<STS_ResJob> GetList_STS_ResJob()
        {
            List<STS_ResJob> stsResJobList = null;
            List<STS_Task> stsTaskList = null;

            try
            {
                stsResJobList = new List<STS_ResJob>();
                stsTaskList = GetList_STS_Task();

                foreach (STS_Task oT in stsTaskList)
                    stsResJobList.Add(Helper.Clone<STS_ResJob>(oT.Task));
            }
            catch (Exception ex)
            {
                Logger.JobManager_TOS.Info(ex.ToString());
                return null;
            }
            return stsResJobList;
        }

        /// 获取 AGV_ResJob 列表
        public List<AGV_ResJob> GetList_AGV_ResJob()
        {
            List<AGV_ResJob> agvResJobList = null;
            List<AGV_Task> agvTaskList = null;

            try
            {
                agvResJobList = new List<AGV_ResJob>();
                agvTaskList = GetList_AGV_Task();

                foreach (AGV_Task oT in agvTaskList)
                    agvResJobList.Add(Helper.Clone<AGV_ResJob>(oT.Task));
            }
            catch (Exception ex)
            {
                Logger.JobManager_TOS.Info(ex.ToString());
                return null;
            }
            return agvResJobList;
        }

        /// 获取 ASC_ResJob 列表。注意目标不全的任务（卸船）不读。
        public List<ASC_ResJob> GetList_ASC_ResJob()
        {
            List<ASC_ResJob> ascResJobList = null;
            List<ASC_Task> ascTaskList = null;

            try
            {
                ascResJobList = new List<ASC_ResJob>();
                ascTaskList = GetList_ASC_Task();

                foreach (ASC_Task oT in ascTaskList)
                    ascResJobList.Add(Helper.Clone<ASC_ResJob>(oT.Task));
            }
            catch (Exception ex)
            {
                Logger.JobManager_TOS.Info(ex.ToString());
                return null;
            }
            return ascResJobList;
        }

        /// 获取 STS_Task 列表
        public List<STS_Task> GetList_STS_Task()
        {
            List<STS_Task> stsTaskList = new List<STS_Task>();
            try
            {
                foreach (STS_Task oT in oSimDataStore.dSTSTasks.Values)
                    stsTaskList.Add(Helper.Clone<STS_Task>(oT));
            }
            catch (Exception ex)
            {
                Logger.JobManager_TOS.Info(ex.ToString());
                return null;
            }

            return stsTaskList;
        }

        /// 获取 AGV_Task 列表
        public List<AGV_Task> GetList_AGV_Task()
        {
            List<AGV_Task> agvTaskList = new List<AGV_Task>();
            try
            {
                foreach (AGV_Task obj in oSimDataStore.dAGVTasks.Values)
                    agvTaskList.Add(Helper.Clone<AGV_Task>(obj));
            }
            catch (Exception ex)
            {
                Logger.JobManager_TOS.Info(ex.ToString());
                return null;
            }

            return agvTaskList;
        }

        /// 获取 ASC_Task 列表。注意目标不全的任务（卸船）不读。
        public List<ASC_Task> GetList_ASC_Task()
        {
            List<ASC_Task> ascTaskList, lTempList;

            ascTaskList = new List<ASC_Task>();
            lTempList = oSimDataStore.dASCTasks.Values.Where(u => !string.IsNullOrWhiteSpace(u.Task.TO_BAY_TYPE) && !string.IsNullOrWhiteSpace(u.Task.TO_BLOCK)).ToList();
            try
            {
                foreach (ASC_Task obj in lTempList)
                    ascTaskList.Add(Helper.Clone<ASC_Task>(obj));
            }
            catch (Exception ex)
            {
                Logger.JobManager_TOS.Info(ex.ToString());
                return null;
            }

            return ascTaskList;
        }

        /// <summary>
        /// 向ResJob表插入一条新的任务
        /// </summary>
        /// <param name="resJob">新任务数据</param>
        /// <returns>true-成功，false-失败</returns>
        public bool Insert_ASC_ResJob(ASC_ResJob resJob)
        {
            oSimDataStore.dASCResJobs.Add(resJob.ID, resJob);
            return true;
        }

        /// <summary>
        /// 向ResJob表插入一条新的任务
        /// </summary>
        /// <param name="resJob">新任务数据</param>
        /// <returns>true-成功，false-失败</returns>
        public bool Insert_AGV_ResJob(AGV_ResJob resJob)
        {
            oSimDataStore.dAGVResJobs.Add(resJob.ID, resJob);
            return true;
        }

        /// <summary>
        /// 向ResJob表插入一条新的任务
        /// </summary>
        /// <param name="resJob">新任务数据</param>
        /// <returns>true-成功，false-失败</returns>
        public bool Insert_STS_ResJob(STS_ResJob resJob)
        {
            oSimDataStore.dSTSResJobs.Add(resJob.ID, resJob);
            return true;
        }

        // STS更新或删除事件 （封）
        /// <returns>ECS是否允许更新或删除，允许返回：1，不允许则返回：0</returns>
        private int STS_Event(object sender, EventArgs e)
        {
            int eventResult = 0;
            //if (sender != null)
            //{
            //    eventResult = STS_JobManagerScheduleEvent((STS_ResJob)sender, EventArgs.Empty);
            //}
            return eventResult;
        }

        /// AGV更新或删除事件 （封）
        /// <returns>ECS是否允许更新或删除，允许返回：1，不允许则返回：0</returns>
        private int AGV_Event(object sender, EventArgs e)
        {
            int eventResult = 0;
            //if (sender != null)
            //{
            //    eventResult = AGV_JobManagerScheduleEvent((AGV_ResJob)sender, EventArgs.Empty);
            //}
            return eventResult;
        }

        /// ASC更新或删除事件 （封）
        /// <returns>ECS是否允许更新或删除，允许返回：1，不允许则返回：0</returns>
        private int ASC_Event(object sender, EventArgs e)
        {
            int eventResult = 0;
            //if (sender != null)
            //{
            //    eventResult = ASC_JobManagerScheduleEvent((ASC_ResJob)sender, EventArgs.Empty);
            //}
            return eventResult;
        }

        /// STS任务更新，数据返回给TOS （封）
        public bool Update_STS_Task(STS_ReqUpdateJob updateJob, string cheID_OR_blockNo)
        {
            return false;
            //bool updateResult = false;
            //updateResult = m_jobServer_TOS_QCMS.UpdateJobState(updateJob, cheID_OR_blockNo);
            //return updateResult;
        }

        /// AGV任务更新，数据返回给TOS （封）
        public bool Update_AGV_Task(AGV_ReqUpdateJob updateJob, string cheID_OR_blockNo)
        {
            return false;
            //bool updateResult = false;
            //updateResult = m_jobServer_TOS_VMS.UpdateJobState(updateJob, cheID_OR_blockNo);
            //return updateResult;
        }

        /// <summary>
        /// ASC任务更新，数据返回给TOS （封）
        /// </summary>>
        public bool Update_ASC_Task(ASC_ReqUpdateJob updateJob, string cheID_OR_blockNo)
        {
            return false;
            //bool updateResult = false;
            //updateResult = m_jobServer_TOS_BMS.UpdateJobState(updateJob, cheID_OR_blockNo);
            //return updateResult;

        }
    }
}
