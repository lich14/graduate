  
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Xml;
using ZECS.Common.Define;

namespace ZECS.Schedule.Define
{
    /// <summary>
    /// SchedulePath路径类
    /// </summary>
    public class SchedulePath
    {
        private static String m_strBinPath = null;               // Bin目录
       
        private static RegistryAccess m_regAccess = new RegistryAccess();

        /// <summary>
        /// 获得程序所在的Bin路径
        /// </summary>
        /// <returns></returns>
        public static String GetBinPath()
        {
            if (String.IsNullOrEmpty(m_strBinPath))
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                if (asm == null) return null;

                m_strBinPath = Path.GetDirectoryName(asm.Location);
            }

            return m_strBinPath;
        }

       

         

        /// <summary>
        /// 获取活跃工程全路径目录
        /// </summary>
        /// <returns></returns>
        public static String GetActiveProjectDirectory()
        {
            try
            {
                //String strActiveProjectDirectory = m_regAccess.ReadRegistryValue("ActiveProjectDirectory_Schedule");
                String strActiveProjectDirectory = Path.Combine(Helper.GetAsmDirectory(), "Project"); // 总是读取当前主程序目录下Project目录下的配置文件

                return strActiveProjectDirectory;
            }
            catch (System.Exception ex)
            {
                return null;
            }
        }
        /// <summary>
        /// 设置活跃工程全路径目录
        /// </summary>
        /// <returns></returns>
        public static bool SetActiveProjectDirectory(String strActiveProjectDirectory)
        {  
            try
            {
                m_regAccess.WriteRegistryValue("ActiveProjectDirectory_Schedule", strActiveProjectDirectory);

                return true;
            }
            catch (System.Exception ex)
            {
                return false; 
            }
            
        }

        
    }
}
