using System.Collections.Generic;
using System.Linq;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.Define.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Define;

namespace ZECS.Schedule.ECSSchedule
{
    public class AscPreTask
    {
        public AscPreTask(Asc asc, ASC_Task ascTask)
        {
            Asc = asc;
            PreTask = ascTask;
        }
        public readonly Asc Asc;
        public readonly ASC_Task PreTask;
    }

    public class AscTaskDispatcher
    {
        // input
        private readonly List<ASC_Task> m_listTask = new List<ASC_Task>();

        private readonly Asc m_mainAsc;
        private readonly Asc m_sencondAsc;

        public AscTaskDispatcher(List<Asc> listAsc, List<ASC_Task> listTask)
        {
            // 一个Block最多两台ASC
            m_mainAsc = listAsc.FirstOrDefault(x => x.CanBeScheduled());

            if (m_mainAsc != null)
            {
                m_sencondAsc = listAsc.FirstOrDefault(x => x.Status.CHE_ID != m_mainAsc.Status.CHE_ID);
            }

            m_listTask.AddRange(listTask);
        }

        /// <summary>
        /// 任务分配：根据每个ASC的任务执行状态、设备状态，进行任务匹配。
        /// </summary>
        public bool PreAssignTask(DBData_Schedule dbDataSchedule, out List<AscPreTask> listAscPreTask)
        {
            listAscPreTask = new List<AscPreTask>();

            if (m_mainAsc == null || m_listTask.Count <= 0)
                return false;

            // Select main ASC task
            ASC_Task taskMainToDo = SelectTaskToDo(dbDataSchedule, m_mainAsc.IsWaterSide);

            if (taskMainToDo == null)
            {
                if (m_sencondAsc == null)
                    return false;

                if (m_sencondAsc.IsMaintenaceMode())
                {
                    taskMainToDo = SelectTaskToDo(dbDataSchedule, !m_mainAsc.IsWaterSide);
                }
            }

            if (taskMainToDo != null)
            {
                listAscPreTask.Add(new AscPreTask(m_mainAsc, taskMainToDo));
            }

            // Select second ASC task
            if (m_sencondAsc == null 
                || !m_sencondAsc.CanBeScheduled())
            {
                return listAscPreTask.Count > 0;
            }

            ASC_Task taskSecondToDo = SelectTaskToDo(dbDataSchedule, m_sencondAsc.IsWaterSide);

            if (taskSecondToDo != null)
            {
                listAscPreTask.Add(new AscPreTask(m_sencondAsc, taskSecondToDo));
            }

            return listAscPreTask.Count > 0;
        }

        /// <summary>
        /// 从海侧或陆侧任务列表中选择一条任务，作为待执行任务
        /// </summary>
        /// <param name="dbDataSchedule"></param>
        /// <param name="isWaterSide"></param>
        /// <returns></returns>
        private ASC_Task SelectTaskToDo(DBData_Schedule dbDataSchedule, bool isWaterSide)
        {
            var listAscOrder = dbDataSchedule.m_DBData_BMS.m_listASC_Order;

            foreach (var task in m_listTask)
            {
                //已经下发过的Order，不再重复发送。如有更新或取消，通过JobManager_TOS的Event操作。
                if (listAscOrder.Exists(order => order.JOB_ID == task.Task.JOB_ID))
                {
                    continue;
                }

                AscTaskSide taskSide = task.Task.TaskSide();
                if (isWaterSide && taskSide == AscTaskSide.WaterSide
                    || !isWaterSide && taskSide != AscTaskSide.WaterSide)
                {
                    return task;
                }
            }

            return null;
        }

        private ASC_Task SelectTaskToHelp(DBData_Schedule dbDataSchedule)
        {
            var listTaskToHelp = m_listTask.FindAll(x =>
                Utility.IsTaskInitial(x.TaskState)
                && x.Task.TaskSide() == AscTaskSide.Sblock);

            ASC_Task taskToHelp = 
                listTaskToHelp.FirstOrDefault(x => 
                    dbDataSchedule.m_DBData_BMS.m_listASC_Order.
                        All(order => order.JOB_ID != x.Task.JOB_ID));

            return taskToHelp;
        }

        private TaskSet<ASC_Task> CreateTaskBundle(DBData_Schedule dbDataSchedule, ASC_Task task)
        {
            TaskSet<ASC_Task> taskSet = new TaskSet<ASC_Task>();

            if (task == null)
                return taskSet;

            taskSet.TaskList.Add(task);

            if (!string.IsNullOrEmpty(task.Task.JOB_LINK))
            {
                ASC_Task linkTask = dbDataSchedule.m_DBData_TOS.m_listASC_Task
                    .Find(x => x.Task.JOB_ID == task.Task.JOB_LINK);
                if (linkTask != null)
                {
                    taskSet.TaskList.Add(linkTask);
                }
            }

            return taskSet;
        }
    }
}