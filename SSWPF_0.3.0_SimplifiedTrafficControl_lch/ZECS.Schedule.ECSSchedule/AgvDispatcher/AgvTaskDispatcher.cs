using System;
using System.Collections.Generic;
using System.Linq;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.Define.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Algorithm;
using ZECS.Schedule.DBDefine.YardMap;
using ZECS.Schedule.Define;

namespace ZECS.Schedule.ECSSchedule
{
    class AgvPreTask
    {
        public Agv Agv = null;
        public TaskSet<AGV_Task> PreTask = null;
        public LaneInfoEx OccupiedLane = null;
        public AGV_Task MatchedAgvTask = null;
    }

    class AgvTaskDispatcher
    {
        // input
        private List<Agv> m_listSchedulableAgv = new List<Agv>();
        private List<AGV_Task> m_listTask = new List<AGV_Task>();

        // temp output
        private int[,] m_iTimeMatrixOfAgvDoTask;
        private List<TaskSet<AGV_Task>> m_listTaskSet = new List<TaskSet<AGV_Task>>();
        private int[,] m_iTimeMatrixOfAgvDoTaskSet;

        private const int infinity = 9999;

        /// <summary>
        /// 任务分配：根据每个AGV完成每个任务（如果是双箱任务，取min(task1,task2)）的时间来对AGV和任务进行匹配。
        /// </summary>
        public List<AgvPreTask> PreAssignAgvTask(
            List<Agv> listSchedulableAgv,
            List<TaskSet<AGV_Task>> listTaskSet,
            DBData_Schedule dbDataSchedule,
            LanePlan lanePlan)
        {
            List<AgvPreTask> listAgvOnLanePreTask = MatchAgvOnLaneOfTask(listSchedulableAgv, listTaskSet, lanePlan);

            InitAlgoData(listSchedulableAgv, listTaskSet, listAgvOnLanePreTask);

            if (!CalTimeMatrixOfAgvDoTask(dbDataSchedule, lanePlan))
            {
                return listAgvOnLanePreTask;
            }

            CalTimeMatrixOfAgvDoTaskSet();

            List<AgvPreTask> listAgvPreTask = CalPreAgvTask();

            listAgvPreTask = listAgvPreTask.Union(listAgvOnLanePreTask).ToList();

            LogTimeMatrixAndResult(listAgvPreTask);

            return listAgvPreTask;
        }

        /// <summary>
        /// 为任务选车时，优先选择停在车道上的空闲AGV
        /// </summary>
        /// <param name="listSchedulableAgv"></param>
        /// <param name="listTaskSet"></param>
        /// <param name="lanePlan"></param>
        /// <returns></returns>
        private static List<AgvPreTask> MatchAgvOnLaneOfTask(List<Agv> listSchedulableAgv, List<TaskSet<AGV_Task>> listTaskSet, LanePlan lanePlan)
        {
            List<AgvPreTask> listAgvOnLanePreTask = new List<AgvPreTask>();

            foreach (var agv in listSchedulableAgv)
            {
                var li = lanePlan.GetAgvOccupiedLane(agv.Status.CHE_ID);
                if (li == null || li.LaneType != LANE_TYPE.LT_BLOCK_EXCHANGE)
                    continue;

                if (lanePlan.IsLaneInUsing(li))
                    continue;

                foreach (var taskBundle in listTaskSet)
                {
                    AGV_Task matchedAgvTask = null;
                    if (!IsMatchedAgvAndTaskSet(taskBundle, li, ref matchedAgvTask))
                        continue;

                    AgvPreTask aptMatched = listAgvOnLanePreTask
                        .Find(x => x.Agv.Status.CHE_ID == agv.Status.CHE_ID);

                    if (aptMatched == null)
                    {
                        AgvPreTask apt = new AgvPreTask
                        {
                            Agv = agv,
                            PreTask = taskBundle,
                            OccupiedLane = li,
                            MatchedAgvTask = matchedAgvTask,
                        };

                        listAgvOnLanePreTask.Add(apt);
                    }
                    else
                    {
                        if (Utility.IsMateLane(Convert.ToString(li.GetLaneNo())))
                        {
                            aptMatched.Agv = agv;
                            aptMatched.OccupiedLane = li;
                            aptMatched.MatchedAgvTask = matchedAgvTask;
                        }
                    }

                    break;
                }
            }

            return listAgvOnLanePreTask;
        }

        private static bool IsMatchedAgvAndTaskSet(TaskSet<AGV_Task> taskSet, LaneInfoEx li, ref AGV_Task matchedAgvTask)
        {
            foreach (var task in taskSet.TaskList)
            {
                JobType jobType = Helper.GetEnum(task.Task.JOB_TYPE, JobType.UNKNOWN);
                string block = null;
                if (jobType == JobType.LOAD || jobType == JobType.DBLOCK)
                {
                    block = task.Task.FROM_BLOCK;
                }
                else if (jobType == JobType.DISC)
                {
                    block = task.Task.TO_BLOCK;
                }

                // AGV停在该堆场？
                if (li.BlockOrQcId == block)
                {
                    matchedAgvTask = task;
                    return true;
                }
            }

            return false;
        }

        private void InitAlgoData(List<Agv> listSchedulableAgv, List<TaskSet<AGV_Task>> listTaskSet, List<AgvPreTask> listAgvOnLanePreTask)
        {
            m_listSchedulableAgv =
                listSchedulableAgv.FindAll(x => !listAgvOnLanePreTask.Exists(y => x.Status.CHE_ID == y.Agv.Status.CHE_ID));

            List<AGV_Task> listTaskMatched = listAgvOnLanePreTask.SelectMany(x => x.PreTask.TaskList).ToList();

            m_listTask = listTaskSet.SelectMany(ts => ts.TaskList).ToList()
                .FindAll(x => !listTaskMatched.Exists(y => y.ID == x.ID));

            if (m_listSchedulableAgv.Count > 0 && m_listTask.Count > 0)
            {
                m_listTaskSet = TaskSet<AGV_Task>.GetTaskSetList(m_listTask);
                m_iTimeMatrixOfAgvDoTask = new int[m_listSchedulableAgv.Count, m_listTask.Count];
                m_iTimeMatrixOfAgvDoTaskSet = new int[m_listSchedulableAgv.Count, m_listTaskSet.Count];
            }
        }

        /// <summary>
        ///根据时间估算方法，得到每辆AGV做每个任务所需要的时间，输出AGV完成任务所需的时间估算二维数组
        /// </summary>
        /// <param name="dbDataSchedule"></param>
        /// <param name="lanePlan"></param>
        /// <returns></returns>
        private bool CalTimeMatrixOfAgvDoTask(DBData_Schedule dbDataSchedule, LanePlan lanePlan)
        {
            if (m_listSchedulableAgv.Count <= 0 || m_listTaskSet.Count <= 0)
            {
                return false;
            }

            var listAgvOccupyLane = new List<LaneInfoEx>();
            var dictTask2FromLane = new Dictionary<AGV_Task, LaneInfoEx>();
            var dictTask2ToLane = new Dictionary<AGV_Task, LaneInfoEx>();

            // 如果AGV在车道上，则使用车道估算时间，否则使用坐标进行时间估算。
            for (int i = 0; i < m_listSchedulableAgv.Count; i++)
            {
                var li = lanePlan.GetAgvOccupiedLane(m_listSchedulableAgv[i].Status.CHE_ID);
                listAgvOccupyLane.Add(li);
            }

            foreach (var task in m_listTask)
            {
                AGV_ResJob job = task.Task;
                dictTask2FromLane[task] = lanePlan.GetOneTpLane(job.FROM_BLOCK, job.FROM_BAY_TYPE);
                dictTask2ToLane[task] = lanePlan.GetOneTpLane(job.TO_BLOCK, job.TO_BAY_TYPE);
            }

            for (int i = 0; i < m_listSchedulableAgv.Count; i++)
            {
                var agv = m_listSchedulableAgv[i];
                var cmdDoing = dbDataSchedule.m_DBData_VMS.m_listAGV_Command
                        .Find(x => x.CHE_ID == agv.Status.CHE_ID);

                int iTimeAgvFinishDoingTask = 0;
                LaneInfoEx toLaneInfoOfDoingTask = null;

                if (cmdDoing != null && cmdDoing.IsCompleteFrom())
                {
                    iTimeAgvFinishDoingTask = CalTimeAgvFinishDoingTask(agv.Status, listAgvOccupyLane[i], lanePlan, cmdDoing, ref toLaneInfoOfDoingTask);
                }

                for (int j = 0; j < m_listTask.Count; j++)
                {
                    AGV_Task task = m_listTask[j];

                    int iTimeOfAgvDoTask = CalTimeOfAgvDoTask(
                        agv.Status,
                        iTimeAgvFinishDoingTask > 0 ? toLaneInfoOfDoingTask : listAgvOccupyLane[i], 
                        dictTask2FromLane[task], 
                        dictTask2ToLane[task]);

                    m_iTimeMatrixOfAgvDoTask[i, j] = iTimeAgvFinishDoingTask + iTimeOfAgvDoTask;
                }
            }

            return true;
        }

        private void CalTimeMatrixOfAgvDoTaskSet()
        {
            for (int i = 0; i < m_listSchedulableAgv.Count; i++)
            {
                for (int j = 0; j < m_listTaskSet.Count; j++)
                {
                    var taskSet = m_listTaskSet[j];
                    AGV_Task taskMinTime;
                    m_iTimeMatrixOfAgvDoTaskSet[i, j] = FindMinTimeOfAgvDoTask(i, taskSet.TaskList, out taskMinTime);
                }
            }
        }

        private List<AgvPreTask> CalPreAgvTask()
        {
            List<AgvPreTask> listAgvPreTask = new List<AgvPreTask>();

            uint uLineSize = (uint)m_iTimeMatrixOfAgvDoTaskSet.GetLength(0);
            uint uColmSize = (uint)m_iTimeMatrixOfAgvDoTaskSet.GetLength(1);
            SimpleIntMatrix sim = new SimpleIntMatrix(m_iTimeMatrixOfAgvDoTaskSet, uLineSize, uColmSize);

            AppointQuestionHungaryAlgorithm aqha = new AppointQuestionHungaryAlgorithm(ref sim);
            int[] arrAgvIndex;
            uint uLen;
            if (!aqha.CalcSolution(out arrAgvIndex, out uLen))
            {
                return listAgvPreTask;
            }

            for (int i = 0; i < uLen && i < m_listSchedulableAgv.Count; i++)
            {
                int iTaskSetIndex = arrAgvIndex[i];
                if (iTaskSetIndex >= 0 && iTaskSetIndex < m_listTaskSet.Count)
                {
                    //先发送较短任务执行时间的任务，将较短时间的任务调整到index=0的位置
                    TaskSet<AGV_Task> taskSet = m_listTaskSet[iTaskSetIndex];
                    AGV_Task taskMinTime;
                    FindMinTimeOfAgvDoTask(i, taskSet.TaskList, out taskMinTime);

                    if (taskMinTime != null && taskSet.TaskList.IndexOf(taskMinTime) != 0)
                    {
                        taskSet.TaskList.Remove(taskMinTime);
                        taskSet.TaskList.Insert(0, taskMinTime);
                    }

                    //双箱任务一起发送
                    AgvPreTask apt = new AgvPreTask
                    {
                        Agv = m_listSchedulableAgv[i],
                        PreTask = m_listTaskSet[iTaskSetIndex]
                    };

                    listAgvPreTask.Add(apt);
                }
            }

            return listAgvPreTask;
        }

        private int CalTimeAgvFinishDoingTask(AGV_STATUS agvStatus, LaneInfoEx agvLane, 
            LanePlan lanePlan, AGV_Command cmdDoing, ref LaneInfoEx toLaneInfo)
        {
            var listBlockLanes = lanePlan.GetTpLanes(cmdDoing.TO_BLOCK, cmdDoing.TO_BAY_TYPE);
            if (listBlockLanes == null || listBlockLanes.Count == 0)
            {
                return 0;
            }

            ushort fromLane = agvLane != null ? agvLane.ID : (ushort)0;
            toLaneInfo = lanePlan.GetOneTpLane(cmdDoing.TO_BLOCK, cmdDoing.TO_BAY_TYPE);
            ushort toLane = toLaneInfo != null ? toLaneInfo.ID : (ushort)0;   // 选择一个目的车道估算时间

            TE_AGVDevice tAgvDevice = new TE_AGVDevice(agvStatus.CHE_ID);
            TimeSpan? ts = tAgvDevice.EstimateWorkTime(fromLane, toLane);
            int timeMoveToToLane = ts != null ? (int)ts.Value.TotalSeconds : 0;

            return timeMoveToToLane;
        }

        private int CalTimeOfAgvDoTask(AGV_STATUS agvStatus, LaneInfoEx agvLane, LaneInfoEx taskFromLane, LaneInfoEx taskToLane)
        {
            if (taskFromLane == null && taskToLane == null)
            {
                return infinity;
            }

            int timeMoveToFromLane = 0;
            int timeMoveToToLane = 0;

            TE_AGVDevice tAgvDevice = new TE_AGVDevice(agvStatus.CHE_ID);
            if (taskFromLane != null)
            {
                TimeSpan? ts = (agvLane != null)
                                ? tAgvDevice.EstimateWorkTime(agvLane.ID, taskFromLane.ID)
                                : tAgvDevice.EstimateWorkTime(agvStatus.LOCATION_X, agvStatus.LOCATION_Y, agvStatus.ORIENTATION, taskFromLane.ID);

                timeMoveToFromLane = ts != null ? (int)ts.Value.TotalSeconds : infinity;
            }

            if (taskToLane != null)
            {
                var startLane = taskFromLane ?? agvLane;

                TimeSpan? ts = (startLane != null)
                        ? tAgvDevice.EstimateWorkTime(startLane.ID, taskToLane.ID)
                        : tAgvDevice.EstimateWorkTime(agvStatus.LOCATION_X, agvStatus.LOCATION_Y, agvStatus.ORIENTATION, taskToLane.ID);

                timeMoveToToLane = ts != null ? (int)ts.Value.TotalSeconds : infinity;
            }

            int timeMove = (timeMoveToFromLane + timeMoveToToLane);

            if (timeMove == infinity)
            {
                Logger.ECSScheduleDebug.Warn(string.Format("Algorithm TE_AGVDevice.EstimateWorkTime() return null! AGV={0}, AgvLane={1}, taskFromLane={2}, taskToLane={3}",
                    agvStatus.CHE_ID, 
                    agvLane == null ? "null" : agvLane.ToString(),
                    taskFromLane == null ? "null" : taskFromLane.ToString(),
                    taskToLane == null ? "null" : taskToLane.ToString()));
            }

            return timeMove;
        }

        /// <summary>
        /// 寻找任务列表中AGV完成时间最短的任务，并返回完成时间
        /// </summary>
        private int FindMinTimeOfAgvDoTask(int iAgvIndex, List<AGV_Task> listTask, out AGV_Task taskMinTime)
        {
            taskMinTime = null;

            int iTimeOfAgvDoTask = infinity;

            foreach (AGV_Task task in listTask)
            {
                int iTaskIndex = m_listTask.FindIndex(x => x == task);
                int iTime = m_iTimeMatrixOfAgvDoTask[iAgvIndex, iTaskIndex];
                if (iTime < iTimeOfAgvDoTask)
                {
                    iTimeOfAgvDoTask = iTime;
                    taskMinTime = task;
                }
            }

            return iTimeOfAgvDoTask;
        }

        /// <summary>
        /// 打印当前(AGV, 任务)的时间估算结果矩阵
        /// </summary>
        /// <param name="listAgvPreTask"></param>
        private void LogTimeMatrixAndResult(List<AgvPreTask> listAgvPreTask)
        {
            uint uLineSize = (uint)m_iTimeMatrixOfAgvDoTaskSet.GetLength(0);
            uint uColmSize = (uint)m_iTimeMatrixOfAgvDoTaskSet.GetLength(1);

            //head AGV   JobID1  JobID2  ...
            //body 951   126     330     time

            string logHeader = string.Format("{0, -4}", "AGV");
            for (uint j = 0; j < uColmSize; ++j)
            {
                int iTaskSetIndex = (int)j;
                TaskSet<AGV_Task> taskSet = m_listTaskSet[iTaskSetIndex];
                logHeader += string.Format("{0, -6}", taskSet.TaskList[0].Task.JOB_ID);
            }
            logHeader += "MinTimeJob LaneOccupied Orientation Location";

            Logger.ECSScheduleDebug.Debug(logHeader);

            for (uint i = 0; i < uLineSize; ++i)
            {
                int iAgvIndex = (int)i;
                AGV_STATUS agvStatus = m_listSchedulableAgv[iAgvIndex].Status;
                var aptMatched = listAgvPreTask.Find(x => x.Agv.Status.CHE_ID == agvStatus.CHE_ID);
                string logLine = string.Format("{0, -4}", agvStatus.CHE_ID);

                for (uint j = 0; j < uColmSize; ++j)
                {
                    int iTaskSetIndex = (int)j;
                    TaskSet<AGV_Task> taskSet = m_listTaskSet[iTaskSetIndex];
                    int iTime = m_iTimeMatrixOfAgvDoTaskSet[iAgvIndex, iTaskSetIndex];

                    logLine += string.Format("{0, -6}", iTime);
                }

                string strJob = "";
                if (aptMatched != null)
                {
                    strJob = Utility.GetString(aptMatched.PreTask.TaskList[0].Task);

                    logLine += string.Format("{0, -10} {1, -12}",
                        aptMatched.PreTask.TaskList[0].Task.JOB_ID,
                        (aptMatched.OccupiedLane != null)
                            ? Convert.ToString(aptMatched.OccupiedLane.GetLaneNo())
                            : "null");
                }
                else
                {
                    logLine += string.Format("{0, -10} {1, -12}", "nojob", "null");
                }

                logLine += string.Format(" {0, -5} {1, -13}({2, 6},{3, 6})",
                    agvStatus.ORIENTATION, agvStatus.LOCATION, agvStatus.LOCATION_X, agvStatus.LOCATION_Y);

                logLine += " " + strJob;

                Logger.ECSScheduleDebug.Debug(logLine);
            }
        }
    }
}
