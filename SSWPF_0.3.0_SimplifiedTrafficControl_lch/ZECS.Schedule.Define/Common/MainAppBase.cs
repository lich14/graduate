using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace ZECS.Schedule.Define
{
    /// <summary>
    /// 应用基类
    /// </summary>
    public class MainAppBase
    {
        private string m_strAppName = null;      // Application名字，命名规则： 模块名后缀_App

        public virtual string AppName
        {
            get { return m_strAppName; }
            set { m_strAppName = value; }
        }

        public MainAppBase()
        {

        }

        /// <summary>
        /// 处理命令接口
        /// </summary>
        /// <param name="unCommandID">命令ID</param>
        /// <param name="objCmdParam">命令参数</param>
        /// <param name="objResult">返回结果</param>
        /// <returns></returns>
        public virtual bool OnCommand(UInt32 unCommandID, Object objCmdParam, ref Object objResult)
        {
            return false;
        }

        /// <summary>
        /// 开始运行Application
        /// </summary>
        /// <returns></returns>
        public virtual bool Start()
        {
            return false;
        }

        /// <summary>
        /// 停止运行App
        /// </summary>
        /// <returns></returns>
        public virtual bool Stop()
        {
            return false;
        }

        /// <summary>
        /// 重启App
        /// </summary>
        /// <returns></returns>
        public virtual bool ReStart()
        {
            return false;
        }

        
    }
}
