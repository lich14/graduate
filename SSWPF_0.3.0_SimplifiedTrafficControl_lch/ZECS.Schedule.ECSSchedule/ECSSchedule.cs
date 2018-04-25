using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.Define;
using ZECS.Schedule.DB;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Algorithm.Utilities;

namespace ZECS.Schedule.ECSSchedule
{
    public class ECSSchedule : MainAppBase
    {
        public ECSSchedule()
        {
            base.AppName = Schedule_Sys_Define.MAIN_APP_NAME; 
        }
        /// <summary>
        /// 数据
        /// </summary>
        private Schedule_ProjectConfig m_prjConfig = null;
        private string m_strProjectDirectory = null;


        public override bool Start()
        {
            try
            {
                // 1. 获取工程目录
                m_strProjectDirectory = SchedulePath.GetActiveProjectDirectory();

                // 2. 加载xml文件
                m_prjConfig = new Schedule_ProjectConfig();
                m_prjConfig.ProjectDirectory = m_strProjectDirectory;
                m_prjConfig.Load(null);

                //Logger.SetLogConfigDirectory(m_strProjectDirectory);
                Logger.Config(m_strProjectDirectory + "\\log4net.config");
                Logger.ECSSchedule.Info("******ECSSchedule start.****** Project directory is " + m_strProjectDirectory);
                Logger.ECSSchedule.Info("Database_ECS_STSMS.ConnectString is: " + m_prjConfig.Database_ECS_STSMS.ConnectString);

                //初始化vmsAlgo.dll算法库
                DataAccess.ConnectDB(m_prjConfig.Database_ECS_STSMS.ConnectString);

                if (!VmsAlgorithm.Instance.InitAlgo())
                    throw new Exception("InitAlgo error!");

                // 初始化时间估算
                if (!AgvTimeEstimate.Instance.InitTimeEstimate())
                    throw new Exception("InitTimeEstimate error!");

                DB_TOS.Instance.Start(m_prjConfig.Database_TOS);
                DB_ECS.Instance.Start(m_prjConfig.Database_ECS_STSMS,
                    m_prjConfig.Database_ECS_VMS, m_prjConfig.Database_ECS_BMS);

                //Thread_DBData_TOS.Instance.Start();
                //Thread_DBData_ECS.Instance.Start();

                //StartDbHeartbeat(m_prjConfig.Database_ECS_STSMS.ConnectString);
            }
            catch (Exception ex)
            {
                Logger.ECSSchedule.Error("ECSSchedule.Start() Error.",ex);
                return false;
            }
            return true;
        }

        public override bool Stop()
        {
            try
            {
                TryStopDbHeartbeat();

                Schedule_ECS.Instance.Stop();

                //Thread_DBData_ECS.Instance.Stop();
                //Thread_DBData_TOS.Instance.Stop();

                DB_TOS.Instance.Stop();
                DB_ECS.Instance.Stop();

                //释放vmsAlgo.dll算法库
                VmsAlgorithm.Instance.ExitAlgo();
                DataAccess.DisConnectDB();
            }
            catch (Exception ex)
            {
                Logger.ECSSchedule.Error("ECSSchedule.Stop() Error.", ex);
            }
            return true;
        }


        public override bool OnCommand(UInt32 unCommandID, Object objCmdParam, ref Object objResult)
        {
            try
            {
                bool bRet = false;
                string strMsg = string.Empty;

                switch (unCommandID)
                {
                    case Schedule_Command_Define.CMD_UnKnown:
                        return false;
                    case Schedule_Command_Define.CMD_IS_SERVICE_ALIVE:
                        objResult = true;
                        return true;
                    case Schedule_Command_Define.CMD_STS_ReqOrders:
                        objResult = STS_ReqOrders(objCmdParam);
                        return true;
                }

                return bRet;
            }
            catch (System.Exception ex)
            {
                string szMsg = string.Format("ECSSchedule::OnCommand Error.CMD ID:{0:D} ", unCommandID);
                Logger.ECSSchedule.Error(szMsg, ex); 
                return false;
            }

        }


        public List<STS_Order> STS_ReqOrders(object objCmdParam)
        {
            return Schedule_STS.Instance.ReqOrder(objCmdParam);
        }

        #region Heartbeat
        private ZPMC.Heartbeat.Heartbeat m_DbHeartbeat;

        private void StartDbHeartbeat(String strConnectString)
        {
            if (m_DbHeartbeat == null)
            {
                m_DbHeartbeat = new ZPMC.Heartbeat.Heartbeat(strConnectString);
            }

            if (!m_DbHeartbeat.StartHeartbeat(
                    m_prjConfig.Heartbeat_SCHD.ServerName,
                    m_prjConfig.Heartbeat_SCHD.Interval)
               )
            {
                Logger.ECSSchedule.Error("Start database heartbeat fail.");

                //throw new Exception("Start database heartbeat fail.");
            }
        }


        private void TryStopDbHeartbeat()
        {
            try
            {
                if (m_DbHeartbeat == null)
                {
                    return;
                }
                m_DbHeartbeat.StopHeartbeat();
            }
            catch (Exception ex)
            {
                Logger.ECSSchedule.Error("Stop database heartbeat fail", ex);
            }
        }
        #endregion
    }
}
