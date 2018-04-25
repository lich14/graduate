using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using log4net.Config;
//using ZECS.Common.LogService;

namespace ZECS.Schedule.Define
{
    public static class Logger
    {
        /// <summary>
        /// 设置log4net的xml配置文件路径
        /// </summary>
        /// <param name="configFilePath"></param>
        public static void Config(string configFilePath)
        {
            XmlConfigurator.ConfigureAndWatch(new FileInfo(configFilePath));
        }

        public static ILog ECSSchedule
        {
            get { return LogManager.GetLogger("ZECS.Schedule.ECSSchedule"); }
        }

        public static ILog ECSScheduleDebug
        {
            get { return LogManager.GetLogger("ZECS.Schedule.ECSScheduleDebug"); }
        }

        public static ILog ScheduleSnapshot
        {
            get { return LogManager.GetLogger("ZECS.Schedule.ScheduleSnapshot"); }
        }

        public static ILog Algorithm
        {
            get { return LogManager.GetLogger("ZECS.Schedule.Algorithm"); }
        }

        public static ILog VmsAlgoAdapter
        {
            get { return LogManager.GetLogger("ZECS.Schedule.VmsAlgoAdapter"); }
        }

        public static ILog JobManager_ECS
        {
            get { return LogManager.GetLogger("ZECS.Schedule.JobManager_ECS"); }
        }

        public static ILog JobManager_TOS
        {
            get { return LogManager.GetLogger("ZECS.Schedule.JobManager_TOS"); }
        }

        public static ILog ServiceManager
        {
            get { return LogManager.GetLogger("ZECS.Schedule.ServiceManager"); }
        }

        public static ILog Simulate
        {
            get { return LogManager.GetLogger("ZECS.Schedule.ScheduleSimulate"); }
        }

        #region remove ZECS.Common.LogService

        //    /// <summary>
        //    /// 设置log4net.config的目录
        //    /// </summary>
        //    /// <param name="configDirectory"></param>
        //    public static void SetLogConfigDirectory(string configDirectory)
        //    {
        //        LoggerWrapper.SetLogConfigDirectory(configDirectory);
        //    }


        //    private static Logger s_ECSScheduleLog = null; 
        //    public static Logger Schedule
        //    {
        //        get
        //        {
        //            if (s_ECSScheduleLog == null)
        //                s_ECSScheduleLog = new Logger(LogSystemType.LogECSSchedule, "ZECS.Schedule.ECSSchedule");

        //            return s_ECSScheduleLog;
        //        }
        //    }


        //    private static Logger s_Job_TOS = null;
        //    public static Logger JobManager_TOS
        //    {
        //        get
        //        {
        //            if (s_Job_TOS == null)
        //                s_Job_TOS = new Logger(LogSystemType.LogECSSchedule, "ZECS.Schedule.JobManager_TOS");

        //            return s_Job_TOS;
        //        }
        //    }

        //    private static Logger s_Job_ECS = null;
        //    public static Logger JobManager_ECS
        //    {
        //        get
        //        {
        //            if (s_Job_ECS == null)
        //                s_Job_ECS = new Logger(LogSystemType.LogECSSchedule, "ZECS.Schedule.JobManager_ECS");

        //            return s_Job_ECS;
        //        }
        //    }

        //    private static Logger s_ServiceManager = null;
        //    public static Logger ServiceManagerLog
        //    {
        //        get
        //        {
        //            if (s_ServiceManager == null)
        //                s_ServiceManager = new Logger(LogSystemType.LogECSSchedule, "ZECS.Schedule.ServiceManager");

        //            return s_ServiceManager;
        //        }
        //    }
        //}


        //public class Logger
        //{
        //    private  ECSLogger m_logger = null;

        //    public Logger(LogSystemType sysType, string moduleName)
        //    {
        //        m_logger = LoggerWrapper.GetLogger(sysType, moduleName);

        //    } 

        //    // 不带level参数, 默认为info
        //    public void WriteLog(string message)
        //    {
        //        if (m_logger == null)
        //            return;
        //        m_logger.WriteLog(LogLevel.LOG_INFO, message);
        //    }

        //    /// <summary>
        //    /// 写日志
        //    /// </summary>
        //    /// <param name="level">level级别</param>
        //    /// <param name="message"></param>
        //    public void WriteLog(LogLevel level, string message)
        //    {
        //        if (m_logger == null)
        //            return;
        //        m_logger.WriteLog(level, message);
        //    }

        //    public void WriteErrorLog(string message)
        //    {
        //        if (m_logger == null)
        //            return;
        //        m_logger.WriteLog(LogLevel.LOG_ERROR, message);
        //    }

        //    public void WriteErrorLog(string message, Exception ex)
        //    {
        //        if (m_logger == null)
        //            return;
        //        m_logger.WriteLog(LogLevel.LOG_ERROR, message, ex);
        //    }

        //    /// <summary>
        //    /// 采用CustomLogmessage记录数据库时,请使用此方法
        //    /// </summary>
        //    /// <param name="level"></param>
        //    /// <param name="message"></param>
        //    public void WriteLog_db(LogLevel level, object message)
        //    {
        //        if (m_logger == null)
        //            return;
        //        m_logger.WriteLog_db(level, message);
        //    }

        #endregion
    }
}
