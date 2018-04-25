using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Common.Define;
using BayType = ZECS.Schedule.DBDefine.Schedule.BayType;
using JobType = ZECS.Schedule.DBDefine.Schedule.JobType;
using TaskStatus = ZECS.Schedule.DBDefine.CiTOS.TaskStatus;

namespace ZECS.Schedule.Define
{
    public static class Helper
    {

        /// <summary>
        /// 深度拷贝函数
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T Clone<T>(T obj)
        {
            using (Stream objectStream = new MemoryStream())
            {
                IFormatter formatter = new BinaryFormatter();
                formatter.Serialize(objectStream, obj);
                objectStream.Seek(0, SeekOrigin.Begin);
                return (T)formatter.Deserialize(objectStream);
            }
        }
        /// <summary>
        /// 字符串相等比较 扩展方法
        /// </summary>
        /// <param name="strSrc"></param>
        /// <param name="strDest"></param>
        /// <returns></returns>
        static public bool EqualsEx(this String strSrc, String strDest)
        {
            if (String.IsNullOrWhiteSpace(strSrc) || String.IsNullOrWhiteSpace(strDest))
                return false;

            if (strSrc.Trim().Equals(strDest.Trim()))
                return true;

            return false;
        }

        /// <summary>
        /// 将字符串转换成指定枚举类型的值
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="str">待转换的字符串</param>
        /// <param name="enumDefault">转换失败时，返回此缺省值</param>
        /// <returns>与字符串匹配的枚举类型值</returns>
        public static TEnum GetEnum<TEnum>(string str, TEnum enumDefault) where TEnum : struct
        {
            TEnum en = enumDefault;
            if (str != null)
            {
                Enum.TryParse(str, true, out en);
            }
            return en;
        }

        public static string GetExeDirectory()
        {
            Assembly asm = Assembly.GetEntryAssembly();

            return Path.GetDirectoryName(asm.Location);
        }

        public static string GetAsmDirectory()
        {
            Assembly asm = Assembly.GetExecutingAssembly();

            return Path.GetDirectoryName(asm.Location);
        }

        public static bool IsTaskInitial(TaskStatus status)
        {
            return (status == TaskStatus.None
                 || status == TaskStatus.Almost_Ready);
        }

        public static bool IsTaskCompleteFrom(TaskStatus status)
        {
            return (status == TaskStatus.Complete_From);
        }

        public static bool IsTaskWorking(TaskStatus status)
        {
            return !IsTaskInitial(status) && !IsTaskComplete(status);
        }

        public static bool IsTaskComplete(TaskStatus status)
        {
            return (status == TaskStatus.Cancel_OK
                 || status == TaskStatus.Complete
                 || status == TaskStatus.Exception_Complete);
        }

        public static bool IsTruckTask(BayType fromBayType, BayType toBayType)
        {
            return (fromBayType == BayType.LS || toBayType == BayType.LS);
        }
    }
}
