using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.DBDefine.CiTOS
{
    /// <summary>
    /// 任务状态
    /// </summary>
    [Serializable]
    public enum TaskStatus : int
    {
        None = 0,

        /// <summary>
        /// 准备执行
        /// </summary>
        Ready = 1,

        /// <summary>
        /// 开始执行
        /// </summary>
        Enter = 2,

        /// <summary>
        /// 取箱完成
        /// </summary>
        Complete_From = 3,

        /// <summary>
        /// 完成任务
        /// </summary>
        Complete = 4,

        /// <summary> 
        /// 执行预备动作 VMS已从JobManager获取到该Task，但并未开始执行
        /// </summary> 
        Almost_Ready = 100,

        /// <summary>
        /// QC等待平台确认
        /// </summary>
        Platform_Confirm = 200,

        /// <summary>
        /// 装船任务，平台确认后，QC从平台抓箱
        /// </summary>
        Platform_Pickup = 201,

        /// <summary>
        /// TOS更新任务成功
        /// </summary>
        Update_OK = 300,

        /// <summary>
        /// TOS更新任务失败
        /// </summary>
        Update_FALSE = 301,

        /// <summary>
        /// TOS删除任务成功
        /// </summary>
        Cancel_OK = 400,

        /// <summary>
        /// TOS删除任务失败
        /// </summary>
        Cancel_FALSE = 401,

            /// <summary>
        /// 任务发生异常
        /// </summary>
        Exception = 500,

        /// <summary>
        /// 异常结束
        /// </summary>
        Exception_Complete = 501
    }
}
