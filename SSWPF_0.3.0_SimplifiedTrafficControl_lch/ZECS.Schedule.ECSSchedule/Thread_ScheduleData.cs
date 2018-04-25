using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DB;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.CiTOS;
using System.Threading;
using ZECS.Schedule.Algorithm.Utilities;
using ZECS.Schedule.Define.DBDefine.Schedule;

namespace ZECS.Schedule.ECSSchedule
{
    /// <summary>
    /// 每30秒收集一次TOS相关数据,供调度系统使用.
    /// </summary>
    public class Thread_DBData_TOS : ThreadBase
    {
        private static Thread_DBData_TOS s_instance;
        public static Thread_DBData_TOS Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new Thread_DBData_TOS();
                }
                return s_instance;
            }
        }
        //获取数据的周期时间，单位秒,默认60s
        public UInt32 m_nGetDataPeriod = 60;

        public virtual bool Start()
        {
            m_nInterval = 1000; 
            bool bRet = base.Start(null);

            return bRet;
        }

        public override void Stop()
        {
            base.Stop(); 
        }

        public override void ThreadDeal(object param)
        {
            //m_nGetDataPeriod 60s周期控制
            DateTime dtNow = DateTime.Now;
            TimeSpan ts = dtNow.Subtract(m_dtDBDataLastTime);
            if (ts.TotalSeconds < m_nGetDataPeriod)
                return;
            m_dtDBDataLastTime = dtNow;

            DBData_TOS scheDBData = new DBData_TOS();

            //1.收集TOS数据
            //Job任务
            scheDBData.m_listSTS_ResJob = DB_TOS.Instance.GetList_STS_ResJob();
            scheDBData.m_listAGV_ResJob = DB_TOS.Instance.GetList_AGV_ResJob();
            scheDBData.m_listASC_ResJob = DB_TOS.Instance.GetList_ASC_ResJob();

            scheDBData.m_listSTS_Task = DB_TOS.Instance.GetList_STS_Task();
            scheDBData.m_listAGV_Task = DB_TOS.Instance.GetList_AGV_Task();
            scheDBData.m_listASC_Task = DB_TOS.Instance.GetList_ASC_Task();

            scheDBData.m_listBERTH_STATUS = DB_TOS.Instance.GetList_BERTH_STATUS();
            scheDBData.m_listSTS_WORK_QUEUE_STATUS = DB_TOS.Instance.GetList_STS_WORK_QUEUE_STATUS();
            scheDBData.m_listWORK_INSTRUCTION_STATUS = DB_TOS.Instance.GetList_WORK_INSTRUCTION_STATUS();

            SetData(scheDBData); 
        }

        public DBData_TOS GetData()
        {
            DBData_TOS dbData = null;
            if (m_mutexDBData.WaitOne())
            {
                dbData = m_DBData_TOS;
                m_DBData_TOS = null;//是否要清空?

                m_mutexDBData.ReleaseMutex();

            }
            return dbData;
        }

        public void SetData(DBData_TOS dbData)
        {
            if (dbData == null)
                return;
            if (m_mutexDBData.WaitOne())
            {
                m_DBData_TOS = dbData;

                m_mutexDBData.ReleaseMutex();
            }
        }

        protected DateTime m_dtDBDataLastTime = DateTime.MinValue; //上一次获取数据的时刻
        protected DBData_TOS m_DBData_TOS = null; 
        protected Mutex m_mutexDBData = new Mutex();
        
    }

    /// <summary>
    /// 每1秒收集一次ECS3个MS相关数据,供调度系统使用.
    /// </summary>
    public class Thread_DBData_ECS : ThreadBase
    {
        private static Thread_DBData_ECS s_instance;
        public static Thread_DBData_ECS Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new Thread_DBData_ECS();
                }
                return s_instance;
            }
        }

        public virtual bool Start()
        {
            m_nInterval = 1000;
            MoveSTSEvent += new MoveSTSHandler(UpdateQcPosition);
            bool bRet = base.Start(null);

            return bRet;
        }

        public override void Stop()
        {
            base.Stop();
            MoveSTSEvent -= new MoveSTSHandler(UpdateQcPosition);
        }

        public override void ThreadDeal(object param)
        {
            DBData_Schedule scheDBData = new DBData_Schedule();
             
            //1.收集STSMS数据
            scheDBData.m_DBData_STSMS.m_listSTS_Order = DB_ECS.Instance.GetList_STS_Order();
            scheDBData.m_DBData_STSMS.m_listSTS_Command = DB_ECS.Instance.GetList_STS_Command();
            scheDBData.m_DBData_STSMS.m_listSTS_Status = DB_ECS.Instance.GetList_STS_STATUS();

            //2.收集VMS数据 
            scheDBData.m_DBData_VMS.m_listAGV_Order = DB_ECS.Instance.GetList_AGV_Order();
            scheDBData.m_DBData_VMS.m_listAGV_Command = DB_ECS.Instance.GetList_AGV_Command();
            scheDBData.m_DBData_VMS.m_listAGV_Status = DB_ECS.Instance.GetList_AGV_STATUS(); 
           
            //3.收集BMS数据
            scheDBData.m_DBData_BMS.m_listASC_Order = DB_ECS.Instance.GetList_ASC_Order();
            scheDBData.m_DBData_BMS.m_listASC_Command = DB_ECS.Instance.GetList_ASC_Command();
            scheDBData.m_DBData_BMS.m_listASC_Status = DB_ECS.Instance.GetList_ASC_STATUS(); 
             
            SetData(scheDBData);
            CheckQcPosition(scheDBData.m_DBData_STSMS.m_listSTS_Status);
        }

        public DBData_Schedule GetData()
        {
            DBData_Schedule dbData = null;
            if (m_mutexDBData.WaitOne())
            {
                dbData = m_DBData_ECS;
                m_DBData_ECS = null;//是否要清空?

                m_mutexDBData.ReleaseMutex();
                    
            }
            return dbData;
        }

        public void SetData(DBData_Schedule dbData)
        {
            if (dbData == null)
                return;
            if(m_mutexDBData.WaitOne())
            {
                m_DBData_ECS = dbData;

                m_mutexDBData.ReleaseMutex();
            }
        }
        
        /// <summary>
        /// 检查QC是否有移动，如果有移动
        /// 更新时间估算模块的QC位置，单位是毫米
        /// </summary>
        /// <param name="listStatus"></param>
        private void CheckQcPosition(List<STS_STATUS> listStatus)
        {
            if (listStatus == null)
                return;

            if (m_listSTSPosition.Count != listStatus.Count)
            {
                m_listSTSPosition = listStatus.Select(pos => pos.nQCPosition).ToList<int>();
                foreach (STS_STATUS status in listStatus)
                {
                    MoveSTSEvent(status, null);
                }
            }
            else
            {
                for (int i = 0; i < m_listSTSPosition.Count; i++)
                {
                    if (m_listSTSPosition[i] != listStatus[i].nQCPosition)
                    {
                        MoveSTSEvent(listStatus[i], null);
                        m_listSTSPosition[i] = listStatus[i].nQCPosition;
                    }
                }
            }
        }

        private void UpdateQcPosition(STS_STATUS status, EventArgs e)
        {
            int QCID = Utility.GetNumberFromString(status.QC_ID);
            if (QCID != 0 && AgvTimeEstimate.Instance.bInitialize)
                AgvTimeEstimate.Instance.UpdateQcPosition((ushort)QCID, status.nQCPosition / 1000);
        }


        protected DBData_Schedule m_DBData_ECS = null;
        private List<int> m_listSTSPosition = new List<int>();

        protected Mutex m_mutexDBData = new Mutex();
        public delegate void MoveSTSHandler(STS_STATUS status, EventArgs e);
        public event MoveSTSHandler MoveSTSEvent;
    }
}
