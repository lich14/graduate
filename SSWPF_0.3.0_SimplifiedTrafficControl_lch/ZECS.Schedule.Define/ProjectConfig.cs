using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using ZECS.Schedule.Define;

namespace ZECS.Schedule.Define
{
    /// <summary>
    /// 系统定义
    /// </summary>
    public class Schedule_Sys_Define
    {
        public const String SYSTEM_NAME = "ZECS.Schedule";
        public const String SYSTEM_INTERFACE_NAME = "ZECS.Schedule Interface";
        public const String MODULE_PREFIX = "ZECS.Schedule.";
        public const String PROJECT_FILENAME = "ECSSchedule.xml"; // 工程配置文件名


        // Application name definition 
        public const String MAIN_APP_NAME = MODULE_PREFIX + "ECSSchedule";
    }

    /// <summary>
    /// 对外接口Command定义
    /// </summary>
    public class Schedule_Command_Define
    {
        public const UInt32 CMD_UnKnown = 0;
        public const UInt32 CMD_IS_SERVICE_ALIVE = 1;
        public const UInt32 CMD_STS_ReqOrders = 10; //STSMS请求任务,带箱子类型和数量参数

    }

    /// <summary>
    /// STS请求调度产生新Order时的输入参数
    /// </summary>
    [Serializable]
    public class ReqOrderParam
    {
        public string COMMAND_ID { get; set; }
        public string QUAY_ID { get; set; }
        public ZECS.Common.Define.JobType Move_Kind { get; set; } //装船、卸船
        public ZECS.Common.Define.ContainerType CTN_Type { get; set; }
        public string CTN_ID { get; set; } //用于装船时的参数
    }


    /// <summary>
    /// 工程配置文件的读写
    /// </summary>
    [Serializable]
    public class Schedule_ProjectConfig
    {
        [XmlIgnore]
        public String ProjectDirectory
        {
            get { return m_strPrjDirectory; }
            set { m_strPrjDirectory = value; }
        }

        public DatabaseConfig Database_TOS
        {
            get { return m_dbConfig_TOS; }
            set { m_dbConfig_TOS = value; }
        }

        public DatabaseConfig Database_ECS_STSMS
        {
            get { return m_dbConfig_ECS_STSMS; }
            set { m_dbConfig_ECS_STSMS = value; }
        }

        public DatabaseConfig Database_ECS_VMS
        {
            get { return m_dbConfig_ECS_VMS; }
            set { m_dbConfig_ECS_VMS = value; }
        }

        public DatabaseConfig Database_ECS_BMS
        {
            get { return m_dbConfig_ECS_BMS; }
            set { m_dbConfig_ECS_BMS = value; }
        }

        public HeartbeatConfig Heartbeat_SCHD
        {
            get { return _mHeartbeatSchd; }
            set { _mHeartbeatSchd = value; }
        }


        // 工程文件名及路径 
        private String m_strPrjDirectory = ""; // 工程配置目录

        private DatabaseConfig m_dbConfig_TOS = new DatabaseConfig();
        private DatabaseConfig m_dbConfig_ECS_STSMS = new DatabaseConfig();
        private DatabaseConfig m_dbConfig_ECS_VMS = new DatabaseConfig();
        private DatabaseConfig m_dbConfig_ECS_BMS = new DatabaseConfig();
        private HeartbeatConfig _mHeartbeatSchd = new HeartbeatConfig();

        //private List<AGVConfig> m_lstAGVConfig = new List<AGVConfig>();

        private XmlSerializer m_xmlSerializer = new XmlSerializer(typeof (Schedule_ProjectConfig));

        /// <summary>
        /// 保存工程配置
        /// </summary>
        /// <param name="strPrjFile"></param>
        /// <returns></returns>
        public bool Save(String strPrjFile)
        {
            // 1. 保存活动工程目录下的Schedule.xml
            bool bSucc = SaveXml(strPrjFile);
            return bSucc;
        }

        /// <summary>
        /// 序列化工程配置文件
        /// </summary>
        /// <param name="strPrjFile"></param>
        /// <returns></returns>
        public bool SaveXml(String strPrjFile)
        {
            if (String.IsNullOrEmpty(strPrjFile))
            {
                if (String.IsNullOrEmpty(ProjectDirectory))
                    return false;
                strPrjFile = Path.Combine(ProjectDirectory, Schedule_Sys_Define.PROJECT_FILENAME);
            }

            if (String.IsNullOrEmpty(strPrjFile))
                return false;

            bool bRet = true;
            FileStream fs = null;
            try
            {
                fs = new FileStream(strPrjFile, FileMode.Create);
                m_xmlSerializer.Serialize(fs, this);
            }
            catch (System.Exception ex)
            {
                bRet = false;
                String sMsg = ex.Message;
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }

            return bRet;
        }


        /// <summary>
        /// 加载工程配置
        /// </summary>
        /// <returns></returns>
        public bool Load(String strPrjFile)
        {
            // 1. 加载活动工程目录下的Schedule.xml
            bool bSucc = LoadXml(strPrjFile);

            return bSucc;

        }

        /// <summary>
        /// 反序列化工程配置文件
        /// </summary>
        /// <returns></returns>
        private bool LoadXml(String strPrjFile)
        {
            if (String.IsNullOrEmpty(strPrjFile))
            {
                if (String.IsNullOrEmpty(ProjectDirectory))
                    return false;
                strPrjFile = Path.Combine(ProjectDirectory, Schedule_Sys_Define.PROJECT_FILENAME);
            }

            if (!System.IO.File.Exists(strPrjFile))
                return false;

            bool bRet = true;
            FileStream fs = null;

            try
            {
                File.SetAttributes(strPrjFile, File.GetAttributes(strPrjFile) & ~FileAttributes.ReadOnly);

                fs = new FileStream(strPrjFile, FileMode.Open);
                Schedule_ProjectConfig config = m_xmlSerializer.Deserialize(fs) as Schedule_ProjectConfig;

                if (config == null)
                {
                    bRet = false;
                }
                else
                {
                    Copy(config);
                }
            }
            catch (System.Exception ex)
            {
                bRet = false;
                String sMsg = ex.Message;
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }

            return bRet;
        }


        public void Copy(Schedule_ProjectConfig config)
        {
            m_dbConfig_TOS = config.m_dbConfig_TOS;
            m_dbConfig_ECS_STSMS = config.m_dbConfig_ECS_STSMS;
            m_dbConfig_ECS_VMS = config.m_dbConfig_ECS_VMS;
            m_dbConfig_ECS_BMS = config.m_dbConfig_ECS_BMS;
            _mHeartbeatSchd = config._mHeartbeatSchd;
        }



    }

    //////////////////////////////////////////////////////////////////////////

    [Serializable]
    public class DatabaseConfig
    {
        public const String DatabaseType_SQLServer = "SQLServer";
        public const String DatabaseType_Oracle = "Oracle";

        // 数据库配置信息
        // 数据库类型,SQLServer,Oracle
        private String m_strDatabaseType = DatabaseType_SQLServer;
        //连接字符串
        private String m_strConnectString =
            "Data Source=10.28.251.18;Initial Catalog=ecs;Persist Security Info=True;User ID=ECSUser;Password=ecsuser";

        public String DatabaseType
        {
            get { return m_strDatabaseType; }
            set { m_strDatabaseType = value; }
        }

        public String ConnectString
        {
            get { return m_strConnectString; }
            set { m_strConnectString = value; }
        }
    }

    [Serializable]
    public class HeartbeatConfig
    {
        private string m_strServerName = "SCHD";
        private int m_nInterval = 3*1000; // ms
        public string ServerName
        {
            get { return m_strServerName; }
            set { m_strServerName = value; }
        }

        public int Interval
        {
            get { return m_nInterval; }
            set { m_nInterval = value; }
        }
    }
}
