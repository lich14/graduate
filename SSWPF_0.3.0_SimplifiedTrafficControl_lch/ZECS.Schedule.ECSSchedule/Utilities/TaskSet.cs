using System;
using System.Collections.Generic;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Define;

namespace ZECS.Schedule.ECSSchedule
{
    // T must be AGV_Task, ASC_Task, or STS_Task, or AGV_Order
    [Serializable]
    public class TaskSet<T> where T : class
    {
        public readonly List<T> TaskList = new List<T>();

        public JobType JobType
        {
            get
            {
                if (TaskList.Count <= 0)
                    return JobType.UNKNOWN;

                dynamic task = TaskList[0];

                if (task is AGV_Order)
                {
                    return Helper.GetEnum(task.JOB_TYPE, JobType.UNKNOWN);
                }
                else
                {
                    return (task.Task == null) ? JobType.UNKNOWN : Helper.GetEnum(task.Task.JOB_TYPE, JobType.UNKNOWN);
                }
            }
        }

        /// <summary>
        /// 合并任务列表的双箱任务。
        /// </summary>
        public static List<TaskSet<T>> GetTaskSetList(List<T> listTask)
        {
            List<TaskSet<T>> listTaskSet = new List<TaskSet<T>>();
            foreach (T taskT in listTask)
            {
                dynamic task = taskT;
                TaskSet<T> taskSet = null;

                if (task is AGV_Order)
                {
                    if (!string.IsNullOrWhiteSpace(task.GetOrderLink()))
                    {
                        taskSet = listTaskSet.Find(
                            x => null != x.TaskList.Find(y => ((dynamic)y).ORDER_ID == task.GetOrderLink()));
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(task.Task.JOB_LINK))
                    {
                        taskSet = listTaskSet.Find(
                            x => null != x.TaskList.Find(y => ((dynamic)y).Task.JOB_ID == task.Task.JOB_LINK));
                    }
                }

                if (taskSet == null)
                {
                    taskSet = new TaskSet<T>();
                    taskSet.TaskList.Add(task);
                    listTaskSet.Add(taskSet);
                }
                else
                {
                    taskSet.TaskList.Add(task);
                }
            }

            return listTaskSet;
        }
    }
}