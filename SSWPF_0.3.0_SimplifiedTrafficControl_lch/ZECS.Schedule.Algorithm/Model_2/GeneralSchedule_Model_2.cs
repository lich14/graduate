using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.DBDefine.BlockInfo;
using System.Xml.Serialization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;
using ZECS.Schedule.Define.DBDefine.Schedule;
using ZECS.Schedule.DB;

namespace ZECS.Schedule.Algorithm
{
    //模型2新版本，当前使用版本
    public class GeneralSchedule_Model_2
    {
        private static GeneralSchedule_Model_2 instance;
        private static TimeSpan m_tTaskEstimate = new TimeSpan(0, 0, 5, 0);
        private static TimeSpan m_tBalanceTime = new TimeSpan(0, 0, 9, 0);

        private DBData_Schedule m_DbData_Schedule = null;

        private enum BackOrFront
        {
            None = 0,
            Back = 1,
            Front = 2
        }
       
        public static GeneralSchedule_Model_2 Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GeneralSchedule_Model_2();
                }
                return instance;
            }
        }

        private GeneralSchedule_Model_2()
        {
        }

        private void Initialize()
        {

        }

        class QCTask
        {
            public List<WORK_INSTRUCTION_STATUS> SendTaskList;
            public List<WORK_INSTRUCTION_STATUS> UnExcuteTaskList;
            public List<WORK_INSTRUCTION_STATUS> CanExcuteTaskList;
            public String QCID;
            public int MaxAgvNumber;
            public int MinAgvNumber;
            public int UsedAgvNumber;

            public QCTask(String QCID)
            {
                this.QCID = QCID;
                this.SendTaskList = new List<WORK_INSTRUCTION_STATUS>();
                this.UnExcuteTaskList = new List<WORK_INSTRUCTION_STATUS>();
                this.CanExcuteTaskList = new List<WORK_INSTRUCTION_STATUS>();
                this.MaxAgvNumber = 8;
                this.MinAgvNumber = 3;
                this.UsedAgvNumber = 0;
            }
        }

        /// <summary>
        /// 判断是否双箱任务
        /// </summary>
        /// <param name="stsTaskList"></param>
        /// <param name="jobID"></param>
        /// <returns></returns>
        private int IsTwinedJob(DBData_Schedule dbData_Schedule, List<String> jobIDList, String jobID)
        {
            if (dbData_Schedule == null || jobIDList == null || String.IsNullOrWhiteSpace(jobID))
                return -1;

            List<STS_Task> stsTaskList = dbData_Schedule.m_DBData_TOS.m_listSTS_Task;
            if (stsTaskList == null)
                return -1;

            if (stsTaskList.Exists(job => job.Task.JOB_ID.EqualsEx(jobID) && !String.IsNullOrWhiteSpace(job.Task.JOB_LINK)
                && jobIDList.Contains(job.Task.JOB_LINK)))
                return  1;

            return 0;
        }

        private int CalcAgvNumber(DBData_Schedule dbData_Schedule, List<String> jobIDList)
        {
            if (dbData_Schedule == null || jobIDList == null)
                return -1;

            float fAgvCount = 0;
            foreach (String jobID in jobIDList)
            {
                if (IsTwinedJob(dbData_Schedule, jobIDList, jobID) > 0)
                    fAgvCount += 0.5f;
                else
                    fAgvCount += 1.0f;
            }

            return fAgvCount - (int)fAgvCount > 0 ? (int)fAgvCount + 1 : (int)fAgvCount;
        }

        /// <summary>
        /// 判断队列里是否有合法的箱号
        /// </summary>
        /// <param name="containerList"></param>
        /// <returns></returns>
        private bool IsExistLegalContainer(List<String> containerList)
        {
            if (containerList.Count < 1)
                return false;

            return containerList.Exists(u => !String.IsNullOrWhiteSpace(u));
        }

        /// <summary>
        /// 获取WI的前置任务箱
        /// </summary>
        /// <param name="wi"></param>
        /// <returns></returns>
        private List<String> GetPreContainers(WORK_INSTRUCTION_STATUS wi)
        {
            List<String> preList = new List<String>();

            if (wi == null)
                return preList;

            if (!String.IsNullOrWhiteSpace(wi.LOGICAL_PREDECESSOR))
            {
                String[] aryLogicalPre = wi.LOGICAL_PREDECESSOR.Split(';');
                preList.AddRange(aryLogicalPre);
            }
            if (!String.IsNullOrWhiteSpace(wi.PHYSICAL_PREDECESSOR))
            {
                String[] aryPhysicalPre = wi.PHYSICAL_PREDECESSOR.Split(';');
                preList.AddRange(aryPhysicalPre);
            }
            
            return preList.Distinct().Select(u => u.Trim()).ToList();
        }

        /// <summary>
        /// 计算可以执行的WORK_INSTRUCTION任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="WIList"></param>
        /// <returns></returns>
        private List<WORK_INSTRUCTION_STATUS> GetCanExcuteWIList(
            DBData_Schedule dbData_Schedule,List<WORK_INSTRUCTION_STATUS> WIList)
        {
            List<WORK_INSTRUCTION_STATUS> emptyList = new List<WORK_INSTRUCTION_STATUS>();
            if (dbData_Schedule == null || WIList == null)
                return emptyList;

            List<WORK_INSTRUCTION_STATUS> unExcuteWIList = GetUnExcuteWIList(dbData_Schedule, WIList);
            List<WORK_INSTRUCTION_STATUS> agvUnExcuteDschWIList = GetSpecifiedUnExcuteDschWIList(dbData_Schedule, WIList);
            unExcuteWIList = unExcuteWIList.Union(agvUnExcuteDschWIList).ToList();

            List<WORK_INSTRUCTION_STATUS> unionWIList = GetExcutingWIList(dbData_Schedule, WIList)
                .Union(GetAnyCompletedWIList(dbData_Schedule, WIList)).ToList();
            List<WORK_INSTRUCTION_STATUS> canExcuteWIList = new List<WORK_INSTRUCTION_STATUS>();

            foreach (WORK_INSTRUCTION_STATUS wi in unExcuteWIList)
            {
                List<String> preList = GetPreContainers(wi);
                if (wi.MOVE_KIND == Move_Kind.LOAD && IsExistLegalContainer(preList))
                {
                    if( preList.All(pre => unionWIList.Exists(uwi => uwi.CONTAINER_ID.EqualsEx(pre))))
                         canExcuteWIList.Add(wi);
                }
                else
                    canExcuteWIList.Add(wi);
            }

            return canExcuteWIList;
        }

        /// <summary>
        /// 根据WI集合计算对应的STS集合
        /// </summary>
        /// <param name="WIList"></param>
        /// <returns></returns>
        private List<String> GetSTSIDList(List<WORK_INSTRUCTION_STATUS> WIList)
        {
            List<String> emptyList = new List<string>();

            if (WIList == null)
                return emptyList;

            WIList = WIList.Where(wi => wi.MOVE_KIND == Move_Kind.LOAD
                                  || wi.MOVE_KIND == Move_Kind.DSCH).ToList<WORK_INSTRUCTION_STATUS>();

            return  WIList.Select(u => u.POINT_OF_WORK).Distinct().ToList<String>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        private bool IsOrderInWorking(DBData_Schedule dbData_Schedule ,AGV_Order order)
        {
            if (dbData_Schedule == null || order == null)
                return false;

            var cmd = dbData_Schedule.m_DBData_VMS.m_listAGV_Command
                .Find(x => x.ORDER_ID == order.ORDER_ID);

            if (cmd == null || !cmd.IsComplete())
            {
                //有Order但无Command，或未完成
                return true;
            }

            return false;
        }

        /// <summary>
        /// 通过正在执行的任务量计算对应的AGV数量
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="excutingWIList"></param>
        /// <returns></returns>
        private int CalcAssignedAgvNumber(DBData_Schedule dbData_Schedule, String QCID)
        {
            if (dbData_Schedule == null || String.IsNullOrWhiteSpace(QCID))
                return -1;

            List<AGV_Order> listAgvOrderWorking = 
                dbData_Schedule.m_DBData_VMS.m_listAGV_Order.Where(u => IsOrderInWorking(dbData_Schedule, u) &&
                    (Helper.GetEnum(u.JOB_TYPE, JobType.UNKNOWN) == JobType.LOAD ||
                     Helper.GetEnum(u.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC)).ToList();

            listAgvOrderWorking = listAgvOrderWorking.Where(u => u.QUAY_ID.EqualsEx(QCID)).Distinct().ToList();

            return CalcAgvNumber(dbData_Schedule, listAgvOrderWorking.Select(u => u.JOB_ID).ToList());
        }

        private int CalcNeedAgvNumber(DBData_Schedule dbData_Schedule, List<String> jobIDList)
        {
            if (dbData_Schedule == null || jobIDList == null)
                return -1;

            List<STS_Task> stsTaskList = dbData_Schedule.m_DBData_TOS.m_listSTS_Task;
            if (stsTaskList == null)
                return -1;

            if (jobIDList.Count < 1)
                return  0;

            int count = stsTaskList.Where(u =>
                 Helper.GetEnum(u.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC &&
                 jobIDList.Contains(u.Task.JOB_ID.Trim())).ToList().Count;

            List<STS_Task> jobIDTaskList = stsTaskList.Where(tsk =>
                Helper.GetEnum(tsk.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.LOAD &&
                jobIDList.Contains(tsk.Task.JOB_ID.Trim())).ToList();

            List<String> agvExcutingJobIDList = GetAGVExcutingJobID(dbData_Schedule);
            List<STS_Task> agvExcutingLoadTaskList = stsTaskList.Where(u =>
                Helper.GetEnum(u.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.LOAD &&
                agvExcutingJobIDList.Contains(u.Task.JOB_ID)).ToList();

            List<String> remainderList = jobIDTaskList.Where(jobTsk =>
                !agvExcutingLoadTaskList.Exists(agvTsk => agvTsk.Task.JOB_LINK.EqualsEx(jobTsk.Task.JOB_ID)))
                .Select(u => u.Task.JOB_ID).ToList();

            return CalcAgvNumber(dbData_Schedule, remainderList) + count;
        }

        /// <summary>
        /// 分配装船和卸船的Minimum条任务给STS
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="jobWIList"></param>
        /// <param name="canExcuteWIList"></param>
        /// <param name="qcTask"></param>
        /// <returns></returns>
        private bool AssignMinTask(DBData_Schedule dbData_Schedule, List<WORK_INSTRUCTION_STATUS> jobWIList, 
            List<WORK_INSTRUCTION_STATUS> canExcuteWIList, ref List<QCTask> qcTaskList)
        {
            if (dbData_Schedule == null || jobWIList == null || canExcuteWIList == null)
                return false;

            //按照QC计划的AGV数量下界分配任务
            foreach (QCTask qcTask in qcTaskList)
            {
                String QCID = qcTask.QCID.Trim();
                List<WORK_INSTRUCTION_STATUS> jobQCWIList = jobWIList.Where(u => u.POINT_OF_WORK.EqualsEx(QCID)).ToList();

                List<WORK_INSTRUCTION_STATUS> excutingWIList = GetExcutingWIList(dbData_Schedule, jobQCWIList);
                SortWIListByTime(ref excutingWIList);
                //debug
                PrintWIList(DateTime.Now.ToString() + "： " + "QC" + QCID + ":执行中任务", excutingWIList);
                
                List<WORK_INSTRUCTION_STATUS> unExcuteWIList = GetUnExcuteWIList(dbData_Schedule, jobQCWIList);
                List<WORK_INSTRUCTION_STATUS> agvUnExcuteDschWIList = GetSpecifiedUnExcuteDschWIList(dbData_Schedule, jobQCWIList);
                unExcuteWIList = unExcuteWIList.Union(agvUnExcuteDschWIList).ToList();
                SortWIListByTime(ref unExcuteWIList);
                //debug
                PrintWIList(DateTime.Now.ToString() + "： " + "QC" + QCID + ":未执行任务", unExcuteWIList);
               
                List<WORK_INSTRUCTION_STATUS> myCanExcuteWIList = canExcuteWIList.Intersect(unExcuteWIList).ToList();
                SortWIListByTime(ref myCanExcuteWIList);
                //debug
                PrintWIList(DateTime.Now.ToString() + "： " + "QC" + QCID + ":可执行的任务", myCanExcuteWIList);

                if (unExcuteWIList.Count < 1 || qcTask.MinAgvNumber < 1)
                    continue;

                int minAgvNumber = qcTask.MinAgvNumber;
                int assignedAgvNumber = CalcAssignedAgvNumber(dbData_Schedule, QCID);

                int minLoadCount = minAgvNumber - assignedAgvNumber;
                if (minLoadCount <= 0)
                {
                    qcTask.SendTaskList = new List<WORK_INSTRUCTION_STATUS>();
                    qcTask.UnExcuteTaskList = unExcuteWIList;
                    qcTask.CanExcuteTaskList = myCanExcuteWIList;
                }
                else
                {
                    if (myCanExcuteWIList.Count >= minLoadCount)
                    {
                        qcTask.SendTaskList = myCanExcuteWIList.GetRange(0, minLoadCount);
                        qcTask.CanExcuteTaskList = myCanExcuteWIList.Skip(minLoadCount).ToList();
                    }
                    else
                    {
                        qcTask.SendTaskList = myCanExcuteWIList;
                        qcTask.CanExcuteTaskList = new List<WORK_INSTRUCTION_STATUS>();
                    }
                    qcTask.UnExcuteTaskList = unExcuteWIList.Where(unwi => 
                               !qcTask.SendTaskList.Exists(sdwi => sdwi.JOB_ID.EqualsEx(unwi.JOB_ID))).ToList();
                }
           }

            return true;
        }

        /// <summary>
        /// 为每个STS分配任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="jobWIList"></param>
        /// <param name="canExcuteWIList"></param>
        /// <param name="qcTaskList"></param>
        /// <returns></returns>
        private bool AssignTask(DBData_Schedule dbData_Schedule, List<WORK_INSTRUCTION_STATUS> jobWIList,
            List<WORK_INSTRUCTION_STATUS> canExcuteWIList, ref List<QCTask> qcTaskList)
        {
            if (!AssignMinTask(dbData_Schedule, jobWIList, canExcuteWIList, ref  qcTaskList))
                return false;

            List<AGV_STATUS> agvStatusList = dbData_Schedule.m_DBData_VMS.m_listAGV_Status;
            if (agvStatusList == null)
                return false;

            int qcNum = qcTaskList.Count;
            int agvTotalCount = agvStatusList.Count;

            int usedAgvNum = 0;
            foreach (QCTask qcTask in qcTaskList)
            {
                int qcUsedAgvNum = CalcAssignedAgvNumber(dbData_Schedule, qcTask.QCID);
                if (qcUsedAgvNum < 0)
                    continue;

                int qcSendAgvNum = CalcNeedAgvNumber(dbData_Schedule, qcTask.SendTaskList.Select(u => u.JOB_ID).ToList());
                if (qcSendAgvNum > 0)
                    qcUsedAgvNum += qcSendAgvNum;

                if (qcUsedAgvNum - qcTask.MinAgvNumber < 0)
                {
                    if (qcTask.UnExcuteTaskList.Count > qcTask.MinAgvNumber - qcUsedAgvNum)
                        qcUsedAgvNum = qcTask.MinAgvNumber;
                    else
                        qcUsedAgvNum += qcTask.UnExcuteTaskList.Count; 
                }

                qcTask.UsedAgvNumber = qcUsedAgvNum;
                usedAgvNum += qcTask.UsedAgvNumber;
            }

            int canAssignTaskNum = agvTotalCount - usedAgvNum;

            List<QCTask> tmpQcTaskList = new List<QCTask>();
            tmpQcTaskList.AddRange(qcTaskList);

            for (int index = 0; index < canAssignTaskNum;)
            {
                if (tmpQcTaskList.Count < 1)
                    break;

                QCTask qcTask = tmpQcTaskList[index % tmpQcTaskList.Count];
                if (qcTask.CanExcuteTaskList.Count < 1 || qcTask.SendTaskList.Count > qcTask.MaxAgvNumber)
                {
                    tmpQcTaskList.Remove(qcTask);
                }
                else
                {
                    qcTask.SendTaskList.Add(qcTask.CanExcuteTaskList[0]);
                    qcTask.CanExcuteTaskList.Remove(qcTask.CanExcuteTaskList[0]);
                    index++;
                }
            }

            foreach (QCTask qcTask in qcTaskList)
                PrintWIList(DateTime.Now.ToString() + "： " + "QC" + qcTask.QCID + ":本次发送任务", qcTask.SendTaskList);

            return true;
        }

        private bool SetBackOrFront(ref int[,] matrix ,int i, int j, BackOrFront order)
        {
            if (matrix == null || i < 0 || j < 0)
                return false;

            if (matrix.GetLength(0) < 1 || matrix.GetLength(0) != matrix.GetLength(1))
                return false;

            if (order == BackOrFront.Back)
            {
                matrix[i, j] = 1;
                matrix[j, i] = -1;
            }
            else if(order == BackOrFront.Front)
            {
                matrix[i, j] = -1;
                matrix[j, i] = 1;
            }
            else
            {
                matrix[i, j] = 0;
                matrix[j, i] = 0;
            }

            return true;
        }

        /// <summary>
        /// 接口函数，总体调度Mode2
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="jobsOrderedDecisionTable"></param>
        /// <returns></returns>
        public bool Schedule_Mode2(DBData_Schedule dbData_Schedule,
                                      ref PartialOrderGraph partialOrderTable)
        {
            WriteLogFile(DateTime.Now.ToString() + "： " + "Enter ScheduleECS_Mode2()！");

            m_DbData_Schedule = dbData_Schedule;

            if (dbData_Schedule == null)
                return false;

            DBData_Schedule ScheduleDBData = null;
            try
            {
                ScheduleDBData = Helper.Clone<DBData_Schedule>(dbData_Schedule);
                if (ScheduleDBData == null)
                    return false;
            }
            catch (Exception ex)
            {
                WriteLogFile("ScheduleECS_Mode2()->Clone Error:" + ex.Message);
                Logger.JobManager_TOS.Error("ScheduleECS_Mode2()->Clone Error:" + ex.Message);
                return false;
            }

            //Initialize();
            DateTime tPlanEarliest; DateTime tPlanLatest;
            List<List<WORK_INSTRUCTION_STATUS>> WQWIList = null;

            if (!WQRelateWI(ScheduleDBData.m_DBData_TOS, out tPlanEarliest, out tPlanLatest, out WQWIList))
                return false;

            Debug.Assert(WQWIList != null);
          
            List<WORK_INSTRUCTION_STATUS> WIList = CombineMultiWIList(WQWIList);
            if (WIList == null)
                return false;

            //Dictionary<string, SimpleBlockInfo> dicYardInfo = CreateYardInfoMap(dbData_Schedule, WIList);// new Dictionary<string, SimpleBlockInfo>();
            //if (dicYardInfo == null)
            //    return false;

            List<WORK_INSTRUCTION_STATUS> IntersectWIList = JobListIntersectWIList(ScheduleDBData.m_DBData_TOS, ref WIList);
            if (IntersectWIList == null)
                return false;

            if (!SortWIListByTime(ref IntersectWIList))
                return false;

            ////////////////////////
             List<STS_STATUS> QCStatusList = ScheduleDBData.m_DBData_STSMS.m_listSTS_Status;
            if (QCStatusList == null)
                return false;

            List<String> QCIDList = GetSTSIDList(IntersectWIList); 

            //List<QCTask> QCTaskList = new List<QCTask>();
            List<List<WORK_INSTRUCTION_STATUS>> allUnWorkingWIList = new List<List<WORK_INSTRUCTION_STATUS>>();
            List<WORK_INSTRUCTION_STATUS> wiCanExcuteList = GetCanExcuteWIList(dbData_Schedule,WIList);
            List<QCTask> qcTaskList = new List<QCTask>();

            foreach (String QCID in QCIDList)
            {
                QCTask qctask = new QCTask(QCID);
                STS_STATUS qcStatus = QCStatusList.Find(u => u.QC_ID.Contains(QCID));
                if (qcStatus != null)
                {
                    qctask.MaxAgvNumber = (int)qcStatus.nAGVCountMax;
                    qctask.MinAgvNumber = (int)qcStatus.nAGVCountMin;
                }
                qcTaskList.Add(qctask);
            }

            if (AssignTask(dbData_Schedule, IntersectWIList, wiCanExcuteList, ref qcTaskList))
            {
                foreach (QCTask qctask in qcTaskList)
                   allUnWorkingWIList.Add(qctask.SendTaskList); 
            }

            List<WORK_INSTRUCTION_STATUS> CombineWIList = CombineMultiWIList(allUnWorkingWIList);

            if (!SortWIListByTime(ref  CombineWIList))
                return false;

            SetTwinCarryRef(ref  CombineWIList);

            //if (CaculateAscCurTaskWeight(ScheduleDBData.m_listASC_Task, ref dicYardInfo))
            //    DistributeAscWorkTask(dicYardInfo, ref WIList);

            OrderedDecisionTable jobsOrderedDecisionTable = new OrderedDecisionTable();
            CreateTopological(CombineWIList, ScheduleDBData.m_DBData_TOS.m_listSTS_WORK_QUEUE_STATUS, ref jobsOrderedDecisionTable);
            partialOrderTable.m_WIList = jobsOrderedDecisionTable.m_WIList;
            partialOrderTable.m_DecisionTable = jobsOrderedDecisionTable.m_DecisionTable;
            return true;
        }

        /// <summary>
        /// 把WorkQueue和WorkInstruction做关联
        /// </summary>
        /// <param name="dbData_TOS"></param>
        /// <param name="tEarliest"></param>
        /// <param name="tLatest"></param>
        /// <param name="WQWIList"></param>
        /// <returns></returns>
        private bool WQRelateWI(DBData_TOS dbData_TOS, out DateTime tEarliest, out DateTime tLatest,
                                            out List<List<WORK_INSTRUCTION_STATUS>> WQWIList)
        {
            tEarliest = DateTime.MinValue; tLatest = DateTime.MinValue;
            WQWIList = new List<List<WORK_INSTRUCTION_STATUS>>();

            if (dbData_TOS == null || WQWIList == null)
                return false;

            List<STS_WORK_QUEUE_STATUS> WQList = dbData_TOS.m_listSTS_WORK_QUEUE_STATUS;
            List<WORK_INSTRUCTION_STATUS> WIList = dbData_TOS.m_listWORK_INSTRUCTION_STATUS;
            WQList = WQList.Where(wq => WIList.Exists(wi => wi.VESSEL_ID.EqualsEx(wq.SHIP_NO))).ToList();

            if (WQList == null || WQList.Count < 1 || WIList == null || WIList.Count < 1)
                return false;

            WQList = WQList.Where(wq => wq.MOVE_KIND == Move_Kind.LOAD
                                  || wq.MOVE_KIND == Move_Kind.DSCH).ToList<STS_WORK_QUEUE_STATUS>();

            tEarliest = WQList[0].START_TIME;
            tLatest = WQList[0].END_TIME;

            foreach (STS_WORK_QUEUE_STATUS WQ in WQList)
            {
                List<WORK_INSTRUCTION_STATUS> childWIlist =
                    WIList.Where(wi => wi.WORK_QUEUE.EqualsEx(WQ.WORK_QUEUE) && wi.VESSEL_ID.EqualsEx(WQ.SHIP_NO)).ToList();

                if (childWIlist == null)
                    continue;
                if (childWIlist.Count < 1)
                    continue;

                childWIlist.Sort((u1, u2) => u1.ORDER_SEQ.CompareTo(u2.ORDER_SEQ));

                if (WQ.MOVE_KIND == Move_Kind.LOAD)
                {
                    int[,] matrix = new int[childWIlist.Count, childWIlist.Count];

                    //初始化矩阵
                    InitMatrix(ref matrix);

                    for (int i = 0; i < childWIlist.Count; i++)
                    {
                        List<String> preList = GetPreContainers(childWIlist[i]);
                        foreach (String pre in preList)
                        {
                            if (String.IsNullOrEmpty(pre)) continue;
                            int index = childWIlist.FindIndex(u => u.CONTAINER_ID == pre);
                            if (index >= 0)
                                SetBackOrFront(ref matrix, i, index, BackOrFront.Back);
                        }
                    }
                    OrderedDecisionTable orderedTable = new OrderedDecisionTable(childWIlist, matrix);
                    List<WORK_INSTRUCTION_STATUS> newWIlist = new List<WORK_INSTRUCTION_STATUS>();
                    if (orderedTable.TopoLogicSort(out newWIlist))
                        childWIlist = newWIlist;
                }

                DateTime tWQStart = WQ.START_TIME;
                if (tWQStart < tEarliest) tEarliest = tWQStart;
                DateTime tWQEnd = WQ.END_TIME;
                if (tWQEnd > tLatest) tLatest = tWQEnd;

                TimeSpan tSpan = new TimeSpan((tWQEnd - tWQStart).Ticks / childWIlist.Count);
                foreach (WORK_INSTRUCTION_STATUS wi in childWIlist)
                {
                    wi.T_StartTime = tWQStart;
                    wi.T_EndTime = tWQStart + tSpan;
                    tWQStart = wi.T_EndTime;
                }

                Debug.Assert(childWIlist != null && childWIlist.Count > 0, "WQRelateWI(): Illegal ChildWIlist");
                WQWIList.Add(childWIlist);
            }

            if (WQWIList.Count > 0)
                return true;

            return false;
        }

        /// <summary>
        /// 设置卸船任务的TWIN_CARRY_REFERNCE字段
        /// </summary>
        /// <param name="WIList"></param>
        /// <returns></returns>
        private void SetTwinCarryRef(ref List<WORK_INSTRUCTION_STATUS> WIList)
        {
            foreach (WORK_INSTRUCTION_STATUS wi in WIList)
            {
                if (wi.MOVE_KIND == Move_Kind.DSCH)
                    wi.TWIN_CARRY_REFERNCE = wi.CNTR_SIZ_COD == "20" ? "HALF" : null;
            }
        }

        /// <summary>
        /// 按给定时间截取WorkInstrution任务集
        /// </summary>
        /// <param name="tTime"></param>
        /// <param name="WQWIList"></param>
        /// <returns></returns>
        //暂不使用
        private bool CutWIListByTime(DateTime tTime, ref List<List<WORK_INSTRUCTION_STATUS>> WQWIList)
        {
            bool bRet = false;

            if (WQWIList == null)
                return bRet;

            List<List<WORK_INSTRUCTION_STATUS>> tempWQWIList = new List<List<WORK_INSTRUCTION_STATUS>>();

            foreach (List<WORK_INSTRUCTION_STATUS> WIList in WQWIList)
            {
                // List<WORK_INSTRUCTION_STATUS> tempWIList = WIList.Where(u => u.T_EndTime < tTime).ToList();
                List<WORK_INSTRUCTION_STATUS> tempWIList = WIList.Where(u => u.T_StartTime < tTime).ToList();

                tempWIList.Sort((u1, u2) => u1.ORDER_SEQ.CompareTo(u2.ORDER_SEQ));

                if (tempWIList != null && tempWIList.Count > 0)
                {
                    //双箱任务不可分离
                    if (WIList.Count > tempWIList.Count && tempWIList.Count > 0 && WIList.Count > 1)
                    {
                         //..........................
                    }
                    tempWQWIList.Add(tempWIList);
                }
            }

            if (tempWQWIList.Count > 0)
            {
                WQWIList = tempWQWIList;
                bRet = true;
            }

            return bRet;
        }

        /// <summary>
        /// 合并WorkInstruction任务集
        /// </summary>
        /// <param name="WQWIList"></param>
        /// <returns></returns>
        private List<WORK_INSTRUCTION_STATUS> CombineMultiWIList(List<List<WORK_INSTRUCTION_STATUS>> WQWIList)
        {
            if (WQWIList == null)
                return null;

            List<WORK_INSTRUCTION_STATUS> tempWIList = new List<WORK_INSTRUCTION_STATUS>();
            foreach (List<WORK_INSTRUCTION_STATUS> childWIList in WQWIList)
            {
                if (childWIList != null && childWIList.Count > 0)
                    tempWIList.AddRange(childWIList);
            }

            return tempWIList;
        }

     
        /// <summary>
        /// 计算TOS给定JobList任务集与WorkInstruction任务集的交集
        /// </summary>
        /// <param name="dbData_TOS"></param>
        /// <param name="WIList"></param>
        /// <returns></returns>
        private List<WORK_INSTRUCTION_STATUS> JobListIntersectWIList(DBData_TOS dbData_TOS,
             ref List<WORK_INSTRUCTION_STATUS> WIList)
        {
            List<STS_Task> STSTaskList = dbData_TOS.m_listSTS_Task;
            if (STSTaskList == null || WIList == null || WIList.Count < 1)
                return null;

            List<WORK_INSTRUCTION_STATUS> tmpWIList = WIList;

            List<String> vesselIDList = STSTaskList.Where(u => Helper.GetEnum(u.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC)
                .Select(u => u.Task.VESSEL_ID).Distinct().ToList();

            for (int i = 0; i < vesselIDList.Count; i++)
            {
                if(String.IsNullOrWhiteSpace(vesselIDList[i]))
                    continue;

                String vesselID = vesselIDList[i].Trim();
                List<STS_Task> unFixDschJobList = STSTaskList.Where(u => u.Task.VESSEL_ID.EqualsEx(vesselID) 
                    && Helper.GetEnum(u.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC && String.IsNullOrWhiteSpace(u.Task.CONTAINER_ID.Trim('0'))).ToList();/*Regex.IsMatch(u.Task.CONTAINER_ID,@"^0{1,9}$")*/
                List<WORK_INSTRUCTION_STATUS> unFixDschWIList = tmpWIList.Where(u => u.VESSEL_ID.EqualsEx(vesselID) 
                    && u.MOVE_KIND == Move_Kind.DSCH && String.IsNullOrWhiteSpace(u.JOB_ID)).ToList();

                //if(unFixDschWIList.Count < unFixDschJobList.Count)
                //    throw new Exception("JobListIntersectWIList() error!  " + "QCID:stsIDList[i]" + " unFixDschWIList.Count < unFixDschJobList.Count"); ;

                unFixDschJobList = unFixDschJobList.Where(job => !tmpWIList.Exists(wi => wi.JOB_ID.EqualsEx(job.Task.JOB_ID))).ToList();
                if (unFixDschJobList.Count > 0)
                    unFixDschJobList.Sort((u1, u2) => u1.Task.PLAN_START_TIME.CompareTo(u2.Task.PLAN_START_TIME));
                
                SortWIListByTime(ref unFixDschWIList);

                if (unFixDschJobList.Count <= unFixDschWIList.Count)
                {
                    int count = unFixDschJobList.Count;
                    for (int j = 0; j < count; j++)
                    {
                        //if (!WIList.Exists(u => u.JOB_ID == unFixDschJobList[j].Task.JOB_ID))
                        {
                            unFixDschWIList[j].JOB_ID = unFixDschJobList[j].Task.JOB_ID;
                            WORK_INSTRUCTION_STATUS wiSource = m_DbData_Schedule.m_DBData_TOS.m_listWORK_INSTRUCTION_STATUS
                                .Find(u => u.CONTAINER_ID == unFixDschWIList[j].CONTAINER_ID);
                            if (wiSource != null) wiSource.CONTAINER_ID = String.Empty;
                            unFixDschWIList[j].CONTAINER_ID = String.Empty;
                            unFixDschWIList[j].POINT_OF_WORK = unFixDschJobList[j].Task.CHE_ID;
                        }
                    }
                }
                else
                {
                    int count = unFixDschJobList.Count - unFixDschWIList.Count;
                    for (int k = 0; k < count; k++)
                    {
                        WORK_INSTRUCTION_STATUS unFixDschWI = new WORK_INSTRUCTION_STATUS();
                        unFixDschWI.VESSEL_ID = vesselIDList[i];
                        unFixDschWI.JOB_ID = unFixDschJobList[unFixDschWIList.Count + k].Task.JOB_ID;
                        unFixDschWI.MOVE_KIND = Move_Kind.DSCH;
                        unFixDschWI.POINT_OF_WORK = unFixDschJobList[unFixDschWIList.Count + k].Task.CHE_ID;
                        if (unFixDschWIList.Count > 0)
                        {
                            unFixDschWI.T_StartTime = unFixDschWIList[unFixDschWIList.Count - 1].T_StartTime;
                            unFixDschWI.T_EndTime = unFixDschWIList[unFixDschWIList.Count - 1].T_EndTime;
                        }
                        else
                        {
                            unFixDschWI.T_StartTime = tmpWIList[0].T_StartTime;
                            unFixDschWI.T_EndTime = tmpWIList[0].T_EndTime;
                        }
                        WIList.Add(unFixDschWI);
                    }
                }
               // int count = Math.Min(unFixDschWIList.Count, unFixDschJobList.Count);
            }
            
            List<String> taskJobIDList = STSTaskList.Select(u => u.Task.JOB_ID).ToList();
            List<String> wiJobIDList = WIList.Select(u => u.JOB_ID).ToList();

            List<String> intersectList = taskJobIDList.Intersect(wiJobIDList).ToList();

            List<WORK_INSTRUCTION_STATUS> intersectWIList = WIList.Where(wi => 
                intersectList.Exists(jobID => jobID == wi.JOB_ID) &&
                (wi.MOVE_KIND == Move_Kind.LOAD || wi.MOVE_KIND == Move_Kind.DSCH)).Distinct().ToList();
            
            return intersectWIList;
        }

        /// <summary>
        /// 获取STS正在执行的装船任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetSTSExcutingLoadJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null || dbData_Schedule.m_DBData_TOS == null)
                return emptyList;

            List<STS_Task> stsTaskList = dbData_Schedule.m_DBData_TOS.m_listSTS_Task;
            if (stsTaskList == null)
                return emptyList;
            
            stsTaskList = stsTaskList.Where(u =>
                Helper.GetEnum(u.Task.JOB_TYPE,JobType.UNKNOWN) == JobType.LOAD).ToList();

            return stsTaskList.Where( u =>
                Helper.IsTaskWorking(u.TaskState)).Select(u => u.Task.JOB_ID).ToList<String>();
                
        }
        /// <summary>
        /// 获取STS正在执行的卸船任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetSTSExcutingDschJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null || dbData_Schedule.m_DBData_TOS == null)
                return emptyList;

            List<STS_Task> stsTaskList = dbData_Schedule.m_DBData_TOS.m_listSTS_Task;
            if (stsTaskList == null)
                return emptyList;

            stsTaskList = stsTaskList.Where(u => 
                Helper.GetEnum(u.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC).ToList();

            return stsTaskList.Where(u => 
                Helper.IsTaskWorking(u.TaskState) && u.TaskState != TaskStatus.Ready)
                .Select(u => u.Task.JOB_ID).ToList();
        }

        /// <summary>
        /// 获取STS正在执行的任务,含卸船时JOB状态为Ready但该JOB未执行的任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetSTSExcutingJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null || dbData_Schedule.m_DBData_TOS == null)
                return emptyList;

            return GetSTSExcutingLoadJobID(dbData_Schedule)
                .Union(GetSTSExcutingDschJobID(dbData_Schedule)).ToList();
        }

        /// <summary>
        /// 获取STS未执行的任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetSTSUnExcuteJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null || dbData_Schedule.m_DBData_TOS == null)
                return emptyList;

            List<STS_Task> stsTaskList = dbData_Schedule.m_DBData_TOS.m_listSTS_Task;
            if (stsTaskList == null)
                return emptyList;

            return stsTaskList.Where(u => 
                Helper.IsTaskInitial(u.TaskState) ||
                (Helper.GetEnum(u.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC && u.TaskState == TaskStatus.Ready))
               .Select(u => u.Task.JOB_ID).ToList<String>();
        }

        /// <summary>
        /// 获取AGV正在执行的装船任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetAGVExcutingLoadJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null)
                return emptyList;

            List<AGV_Order> agvOrderList = dbData_Schedule.m_DBData_VMS.m_listAGV_Order;
            List<AGV_Command> agvCommandList = dbData_Schedule.m_DBData_VMS.m_listAGV_Command;
            Debug.Assert(agvOrderList != null && agvCommandList != null);

            agvOrderList = agvOrderList.Where(u =>
                Helper.GetEnum(u.JOB_TYPE, JobType.UNKNOWN) == JobType.LOAD).ToList();

            return agvOrderList.Where(order =>
                  agvCommandList.Exists(cmd => (order.ORDER_ID == cmd.ORDER_ID) &&
                      !cmd.IsComplete() &&
                      cmd.GetCmdStatus() == TaskStatus.Complete_From) ||
                      !agvCommandList.Exists(cmd => order.ORDER_ID == cmd.ORDER_ID))
                  .Select(order => order.JOB_ID).ToList<String>();
        }

        /// <summary>
        /// 获取AGV正在执行的卸船任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetAGVExcutingDschJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null)
                return emptyList;

            List<AGV_Order> agvOrderList = dbData_Schedule.m_DBData_VMS.m_listAGV_Order;
            List<AGV_Command> agvCommandList = dbData_Schedule.m_DBData_VMS.m_listAGV_Command;
            Debug.Assert(agvOrderList != null && agvCommandList != null);

            agvOrderList = agvOrderList.Where(u =>
                Helper.GetEnum(u.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC).ToList();

            return agvOrderList.Where(order =>
                  agvCommandList.Exists(cmd => (order.ORDER_ID == cmd.ORDER_ID) &&
                      !cmd.IsComplete())  ||
                  !agvCommandList.Exists(cmd => order.ORDER_ID == cmd.ORDER_ID))
                  .Select(order => order.JOB_ID).ToList<String>();
        }

        /// <summary>
        /// 获取AGV正在执行的任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetAGVExcutingJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null)
                return emptyList;

            return GetAGVExcutingLoadJobID(dbData_Schedule)
                .Union(GetAGVExcutingDschJobID(dbData_Schedule)).ToList();
        }
        
        /// <summary>
        /// 获取AGV未执行的任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetAGVUnExcuteJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null || dbData_Schedule.m_DBData_TOS == null)
                return emptyList;

            List<AGV_Task> agvTaskList = dbData_Schedule.m_DBData_TOS.m_listAGV_Task;
            if (agvTaskList == null)
                return emptyList;
            else
                agvTaskList = agvTaskList.Where(u => !Helper.IsTaskComplete(u.TaskState)).ToList();

            List<AGV_Order> agvOrderList = dbData_Schedule.m_DBData_VMS.m_listAGV_Order;
            Debug.Assert(agvOrderList != null);

            return agvTaskList.Where(u => !agvOrderList.Exists(order => order.JOB_ID == u.Task.JOB_ID))
               .Select(u => u.Task.JOB_ID).ToList<String>();
        }

        /// <summary>
        /// 获取ASC正在执行的装船任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetASCExcutingLoadJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null)
                return emptyList;

            List<ASC_Command> ascCommandList = dbData_Schedule.m_DBData_BMS.m_listASC_Command;
            List<ASC_Order> ascOrderList = dbData_Schedule.m_DBData_BMS.m_listASC_Order;
            Debug.Assert(ascCommandList != null && ascOrderList != null);

            ascOrderList = ascOrderList.Where(u =>
                Helper.GetEnum(u.JOB_TYPE, JobType.UNKNOWN) == JobType.LOAD).ToList();

            return ascOrderList.Where(order => 
                ascCommandList.Exists(cmd => (order.ORDER_ID == cmd.ORDER_ID) && 
                    !cmd.IsComplete()) ||
                !ascCommandList.Exists(cmd => order.ORDER_ID == cmd.ORDER_ID))
                .Select(order => order.JOB_ID).ToList<String>();
        }

        /// <summary>
        /// 获取ASC正在执行的卸船任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetASCExcutingDschJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null)
                return emptyList;

            List<ASC_Command> ascCommandList = dbData_Schedule.m_DBData_BMS.m_listASC_Command;
            List<ASC_Order> ascOrderList = dbData_Schedule.m_DBData_BMS.m_listASC_Order;
            Debug.Assert(ascCommandList != null && ascOrderList != null);

            ascOrderList = ascOrderList.Where(u =>
                Helper.GetEnum(u.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC).ToList();

            return ascOrderList.Where(order =>
                ascCommandList.Exists(cmd => (order.ORDER_ID == cmd.ORDER_ID) &&
                    !cmd.IsComplete() &&
                     cmd.GetCmdStatus() == TaskStatus.Complete_From) ||
                !ascCommandList.Exists(cmd => order.ORDER_ID == cmd.ORDER_ID))
                .Select(order => order.JOB_ID).ToList<String>();
        }

        /// <summary>
        /// 获取ASC正在执行的任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetASCExcutingJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null)
                return emptyList;

            return GetASCExcutingLoadJobID(dbData_Schedule)
                .Union(GetASCExcutingDschJobID(dbData_Schedule)).ToList();

        }

        /// <summary>
        /// 获取ASC未执行的任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetASCUnExcuteJobID(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();

            if (dbData_Schedule == null || dbData_Schedule.m_DBData_TOS == null)
                return emptyList;

            List<ASC_Task> ascTaskList = dbData_Schedule.m_DBData_TOS.m_listASC_Task;
            List<STS_Task> stsTaskList = dbData_Schedule.m_DBData_TOS.m_listSTS_Task;
            if (ascTaskList == null || stsTaskList == null)
                return emptyList;

            List<String> ascCompletedList = ascTaskList.Where(u => Helper.IsTaskComplete(u.TaskState)).Select(u => u.Task.JOB_ID).ToList();
            List<String> unExcutingJobID = stsTaskList.Where(u => !ascCompletedList.Contains(u.Task.JOB_ID)).Select(u => u.Task.JOB_ID).ToList();

            List<ASC_Order> ascOrderList = dbData_Schedule.m_DBData_BMS.m_listASC_Order;
            Debug.Assert(ascOrderList != null);

            return unExcutingJobID.Where(u => !ascOrderList.Exists(order => order.JOB_ID == u)).ToList();
        }

        /// <summary>
        /// 获取正在执行的所有任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
         private List<String> GetExcutingJobIDList(DBData_Schedule dbData_Schedule)
         {
             List<String> emptyList = new List<String>();
             if (dbData_Schedule == null)
                 return null;
             
             //卸船，只要AGV有任务，可以认为任务已开始执行，但此时该任务的JOBID是未确定的，JOBID可能会被换掉
             //装船，只要ASC有任务，可以认为任务已开始执行，且此时该任务的JOBID是确定的，但在AGV已经获得JOBID而ASC未获得的情况下，AGV的JOBID可能会被换掉
             List<String> ascJobIDList = GetASCExcutingJobID(dbData_Schedule);
             List<String> agvJobIDList = GetAGVExcutingJobID(dbData_Schedule);
             List<String> stsJobIDList = GetSTSExcutingJobID(dbData_Schedule);

             return ascJobIDList.Union(agvJobIDList).Union(stsJobIDList).ToList();
         }

               
        /// <summary>
        /// 获取正在执行的WorkInstruction任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="WIList"></param>
        /// <returns></returns>
        private List<WORK_INSTRUCTION_STATUS> GetExcutingWIList(DBData_Schedule dbData_Schedule, 
            List<WORK_INSTRUCTION_STATUS> WIList)
        {
            List<WORK_INSTRUCTION_STATUS> emptyList = new List<WORK_INSTRUCTION_STATUS>(); 
            if (dbData_Schedule == null || WIList == null)
                return emptyList;

            List<string> excutingJobIDList = GetExcutingJobIDList(dbData_Schedule);
            return WIList.Where(wi => excutingJobIDList.Contains(wi.JOB_ID)).ToList();
        }

        /// <summary>
        /// 获取未执行任务集合的JobIDList
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetUnExcuteJobIDList(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();
            if (dbData_Schedule == null)
                return emptyList;

            List<String> ascJobIDList = GetASCUnExcuteJobID(dbData_Schedule);
            List<String> agvJobIDList = GetAGVUnExcuteJobID(dbData_Schedule);
            List<String> stsJobIDList = GetSTSUnExcuteJobID(dbData_Schedule); ;

            return ascJobIDList.Intersect(agvJobIDList).Intersect(stsJobIDList).ToList();
        }
        
        /// <summary>
        /// 计算未执行的AGV卸船任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="WIList"></param>
        /// <returns></returns>
        private List<WORK_INSTRUCTION_STATUS> GetSpecifiedUnExcuteDschWIList(
            DBData_Schedule dbData_Schedule, List<WORK_INSTRUCTION_STATUS> WIList)
        {
            List<WORK_INSTRUCTION_STATUS> emptyList = new List<WORK_INSTRUCTION_STATUS>();
            if (dbData_Schedule == null || WIList == null)
                return emptyList
                    ;
            List<string> agvUnExcuteJobIDList = GetAGVUnExcuteJobID(dbData_Schedule);
            List<string> ascUnExcuteJobIDList = GetASCUnExcuteJobID(dbData_Schedule);
            List<string> stsExcutingDschJobIDList = GetSTSExcutingDschJobID(dbData_Schedule);

            return WIList.Where(u => u.MOVE_KIND == Move_Kind.DSCH &&
                  agvUnExcuteJobIDList.Contains(u.JOB_ID) &&
                  ascUnExcuteJobIDList.Contains(u.JOB_ID)  /*&&
                !stsExcutingDschJobIDList.Contains(u.JOB_ID)*/).ToList();
        }

        /// <summary>
        /// 获取未执行的WorkInstruction任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="WIList"></param>
        /// <returns></returns>
        private List<WORK_INSTRUCTION_STATUS> GetUnExcuteWIList(
            DBData_Schedule dbData_Schedule, List<WORK_INSTRUCTION_STATUS> WIList)
        {
            List<WORK_INSTRUCTION_STATUS> emptyList = new List<WORK_INSTRUCTION_STATUS>();
            if (dbData_Schedule == null || WIList == null)
                return emptyList;

            List<string> unExcutingJobIDList = GetUnExcuteJobIDList(dbData_Schedule);
            return WIList.Where(wi => unExcutingJobIDList.Contains(wi.JOB_ID)).ToList();
        }

        /// <summary>
        /// 获取任意设备上已完成任务的JobIDList
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <returns></returns>
        private List<String> GetAnyCompletedJobIDList(DBData_Schedule dbData_Schedule)
        {
            List<String> emptyList = new List<String>();
            if (dbData_Schedule == null)
                return emptyList;
           
            List<ASC_Task> ascTaskList = dbData_Schedule.m_DBData_TOS.m_listASC_Task;
            List<AGV_Task> agvTaskList = dbData_Schedule.m_DBData_TOS.m_listAGV_Task;
            List<STS_Task> stsTaskList = dbData_Schedule.m_DBData_TOS.m_listSTS_Task;
            if (stsTaskList == null || ascTaskList == null || agvTaskList == null)
                return emptyList;

            List<String> ascJobIDList = ascTaskList.Where(u => Helper.IsTaskComplete(u.TaskState)).Select(u => u.Task.JOB_ID).ToList();
            List<String> agvJobIDList = agvTaskList.Where(u => Helper.IsTaskComplete(u.TaskState)).Select(u => u.Task.JOB_ID).ToList();
            List<String> stsJobIDList = stsTaskList.Where(u => Helper.IsTaskComplete(u.TaskState)).Select(u => u.Task.JOB_ID).ToList();

            return ascJobIDList.Union(agvJobIDList).Union(stsJobIDList).ToList();
        }

        /// <summary>
        /// 获取在任意设备上已完成任务的WorkInstruction任务
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="WIList"></param>
        /// <returns></returns>
        private List<WORK_INSTRUCTION_STATUS> GetAnyCompletedWIList(
            DBData_Schedule dbData_Schedule, List<WORK_INSTRUCTION_STATUS> WIList)
        {
            List<WORK_INSTRUCTION_STATUS> emptyList = new List<WORK_INSTRUCTION_STATUS>();
            if (dbData_Schedule == null || WIList == null)
                return emptyList;

            List<string> completedJobIDList = GetAnyCompletedJobIDList(dbData_Schedule);
            return WIList.Where(wi => completedJobIDList.Contains(wi.JOB_ID)).ToList();
        }

        /// <summary>
        /// 按时间对WorkInstruction任务集排序
        /// </summary>
        /// <param name="WIList"></param>
        /// <returns></returns>
        private bool SortWIListByTime(ref List<WORK_INSTRUCTION_STATUS> WIList)
        {
            if (WIList == null)
                return false;

            if (WIList.Count > 0)
                WIList.Sort((u1, u2) => u1.T_StartTime.CompareTo(u2.T_StartTime));

            return true;
        }

        /// <summary>
        /// 创建堆场哈希表
        /// </summary>
        /// <param name="dbData_TOS"></param>
        /// <param name="WIList"></param>
        /// <param name="dicBlockInfo"></param>
        /// <returns></returns>
        private Dictionary<string, SimpleBlockInfo> CreateYardInfoMap(
                  DBData_TOS dbData_TOS, List<WORK_INSTRUCTION_STATUS> WIList)
        {
            Dictionary<string, SimpleBlockInfo> dicBlockInfo = new Dictionary<string, SimpleBlockInfo>();
            if (dbData_TOS == null || dbData_TOS.m_listASC_Task == null || WIList == null)
                return null;

            List<Block_Container> blockContainerList = DB_ECS.Instance.Get_All_Block_Container();
            if (blockContainerList == null || blockContainerList.Count == 0)
                return null;

            List<string> blockIDList = blockContainerList.Where(bc => WIList.Exists(wi => wi.CONTAINER_ID == bc.CONTAINER_ID))
                .Select(u => u.BLOCK_NO).Distinct().ToList<string>();

            //创建堆场
            foreach (string blockID in blockIDList)
                dicBlockInfo.Add(blockID, new SimpleBlockInfo(blockID));

            foreach (Block_Container bc in blockContainerList)
            {
                WORK_INSTRUCTION_STATUS wi = WIList.Find(u => u.CONTAINER_ID == bc.CONTAINER_ID);

                if (wi != null)
                {
                    string factor = wi.CONTAINER_STOW_FACTOR;
                    double weightMargin = wi.CONTAINER_WEIGHT_MARGIN_KG;
                    ContainerMask mask = new ContainerMask(factor, weightMargin);

                    if (dicBlockInfo.ContainsKey(bc.BLOCK_NO))
                    {
                        int length = wi.CONTAINER_LENGTH_CM;
                        int height = wi.CONTAINER_HEIGHT_CM;
                        int weight = wi.CONTAINER_WEIGHT_KG;

                        ContainerInfo containerInfo = new ContainerInfo(wi.CONTAINER_ID, wi.CONTAINER_ISO,
                                                        length, height, weight, 0, 0, bc.BAY, bc.LANE, bc.TIER, mask);
                        dicBlockInfo[bc.BLOCK_NO].AddContainer(containerInfo);
                    }
                }
            }

            return dicBlockInfo;
        }

        /// <summary>
        /// 估算堆场Block当前待执行任务队列的耗时，给出Block当前任务耗时权值
        /// </summary>
        /// <param name="ascTaskList"></param>
        /// <param name="htBlockInfo"></param>
        /// <returns></returns>
        private bool CaculateAscCurTaskWeight(List<ASC_Task> ascTaskList,
                                               ref Dictionary<string, SimpleBlockInfo> htBlockInfo)
        {
            if (ascTaskList == null || htBlockInfo.Count < 1)
                return false;

            foreach (SimpleBlockInfo block in htBlockInfo.Values)
            {
                List<ASC_Task> curList = ascTaskList.Where(u => Helper.IsTaskWorking(u.TaskState)
                                                             && u.Task.YARD_ID == block.BlockID).ToList<ASC_Task>();
                if (curList.Count > 0)
                {
                    foreach (ASC_Task ascTask in curList)
                    {
                        block.WeightValue += m_tTaskEstimate;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 为每个WorkInstruction任务分配堆场Block箱区
        /// </summary>
        /// <param name="WIList"></param>
        /// <param name="htBlockInfo"></param>
        /// <returns></returns>
        private bool DistributeAscWorkTask(Dictionary<string, SimpleBlockInfo> dicYardInfo,
            ref List<WORK_INSTRUCTION_STATUS> WIList)
        {
            if (WIList == null)
                return false;

            foreach (WORK_INSTRUCTION_STATUS wi in WIList)
            {
                String blockID = null;
                String containerID = null;
                TimeSpan tMinWeightValue = TimeSpan.MaxValue;
                foreach (SimpleBlockInfo block in dicYardInfo.Values)
                {
                    if (block.WeightValue < tMinWeightValue)
                    {
                        if (wi.MOVE_KIND == Move_Kind.DSCH)
                        {
                            if (tMinWeightValue > block.WeightValue + m_tTaskEstimate/*tContainerEstimate*/)
                            {
                                tMinWeightValue = block.WeightValue + m_tTaskEstimate/*tContainerEstimate*/;
                                if (blockID != block.BlockID)
                                    blockID = block.BlockID;
                            }
                        }
                        else if (wi.MOVE_KIND == Move_Kind.LOAD)
                        {
                            foreach (ContainerInfo container in block.ContainerHashTable.Values)
                            {
                                //寻找blcok中与wi任务mask相同并且耗时最少的箱子
                                if (container.Mask.CONTAINER_STOW_FACTOR == wi.CONTAINER_STOW_FACTOR
                                    && container.Mask.CONTAINER_WEIGHT_MARGIN_KG == wi.CONTAINER_WEIGHT_MARGIN_KG)
                                {
                                    if (tMinWeightValue > block.WeightValue + m_tTaskEstimate/*tContainerEstimate*/)
                                    {
                                        tMinWeightValue = block.WeightValue + m_tTaskEstimate/*tContainerEstimate*/;
                                        if (blockID != block.BlockID)
                                            blockID = block.BlockID;
                                        containerID = container.ContainerID;
                                    }
                                }
                            }
                        }
                    }
                }

                if (blockID != null)
                {
                    dicYardInfo[blockID].WeightValue = tMinWeightValue;
                    wi.T_Load_BlockNO = blockID;

                    if (containerID != null)
                        dicYardInfo[blockID].RemoveContainer(containerID);
                }
            }


            return true;
        }
        /// <summary>
        /// 初始化偏序表矩阵结构
        /// </summary>
        /// <param name="matrix"></param>
        private void InitMatrix(ref int[,] matrix)
        {
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    matrix[i, j] = 0;
                }
            }
        }


        /// <summary>
        /// 针对WorkInstruction任务集建立具有偏序关系的集合
        /// </summary>
        /// <param name="WIList"></param>
        /// <param name="WQList"></param>
        /// <param name="partialOrderedTable"></param>
        /// <returns></returns>
        private bool CreateTopological(List<WORK_INSTRUCTION_STATUS> WIList,
                                        List<STS_WORK_QUEUE_STATUS> WQList, ref OrderedDecisionTable partialOrderedTable)
        {
            if (WIList == null || WQList == null)
                return false;

            WQList = WQList.Where(wq => WIList.Exists(wi => wi.VESSEL_ID == wq.SHIP_NO)).ToList();

            //创建WorkQueue表
            //Dictionary<String, STS_WORK_QUEUE_STATUS> WQTable = new Dictionary<String, STS_WORK_QUEUE_STATUS>();
            //foreach (STS_WORK_QUEUE_STATUS wq in WQList)
            //    WQTable.Add(wq.WORK_QUEUE, wq);


            int[,] matrix = new int[WIList.Count, WIList.Count];

            //初始化矩阵
            InitMatrix(ref matrix);

            //取所有装船WorkQueue中第一条任务的完成时间，并比较获得最迟完成时间
            //DateTime tFirstEnd = DateTime.MinValue;
            //Dictionary<String, WORK_INSTRUCTION_STATUS> htFirstTask = new Dictionary<String, WORK_INSTRUCTION_STATUS>();

            //foreach (STS_WORK_QUEUE_STATUS WQ in WQList)
            //{
            //    List<WORK_INSTRUCTION_STATUS> childWIlist = WIList.Where(u => u.WORK_QUEUE == WQ.WORK_QUEUE
            //                                                             && u.MOVE_KIND == Move_Kind.LOAD).ToList();

            //    if (childWIlist == null || childWIlist.Count < 1)
            //        continue;

            //    childWIlist.Sort((u1, u2) => u1.T_StartTime.CompareTo(u2.T_StartTime));

            //    if (childWIlist[0].T_EndTime > tFirstEnd)
            //        tFirstEnd = childWIlist[0].T_EndTime;

            //    if (!htFirstTask.ContainsKey(childWIlist[0].JOB_ID))
            //        htFirstTask.Add(childWIlist[0].JOB_ID, childWIlist[0]);
            //}

            //根据偏序关系填充矩阵，三角形法操作
            for (int i = 0; i < WIList.Count; i++)
            {
                for (int j = i + 1; j < WIList.Count; j++)
                {
                    if (WIList[i].POINT_OF_WORK == WIList[j].POINT_OF_WORK)
                    {
                        if (WIList[i].MOVE_KIND == Move_Kind.LOAD && WIList[j].MOVE_KIND == Move_Kind.LOAD)
                        {
                            if (WIList[i].T_StartTime > WIList[j].T_StartTime)
                                SetBackOrFront(ref matrix, i, j, BackOrFront.Back);
                            else if (WIList[i].T_StartTime < WIList[j].T_StartTime)
                                SetBackOrFront(ref matrix, i, j, BackOrFront.Front);
                        }
                        else if (WIList[i].MOVE_KIND == Move_Kind.LOAD && WIList[j].MOVE_KIND == Move_Kind.DSCH)
                        {
                            STS_WORK_QUEUE_STATUS wqLoad = WQList.Find(u => u.WORK_QUEUE == WIList[i].WORK_QUEUE);
                            if (wqLoad != null)
                            {
                                if (wqLoad.START_TIME > WIList[j].T_StartTime)
                                    SetBackOrFront(ref matrix, i, j, BackOrFront.Back);
                                else if (wqLoad.START_TIME < WIList[j].T_StartTime)
                                    SetBackOrFront(ref matrix, i, j, BackOrFront.Front);
                            }
                        }
                        else if (WIList[i].MOVE_KIND == Move_Kind.DSCH && WIList[j].MOVE_KIND == Move_Kind.LOAD)
                        {
                            STS_WORK_QUEUE_STATUS wqLoad = WQList.Find(u => u.WORK_QUEUE == WIList[j].WORK_QUEUE);
                            if (wqLoad != null)
                            {
                                if (WIList[i].T_StartTime > wqLoad.START_TIME)
                                    SetBackOrFront(ref matrix, i, j, BackOrFront.Back);
                                else if (WIList[i].T_StartTime < wqLoad.START_TIME)
                                    SetBackOrFront(ref matrix, i, j, BackOrFront.Front);
                            }
                        }
                        else
                        {
                            //.......................
                        }
                    }
                }
            }

            partialOrderedTable.SetPartialOrderedTable(WIList, matrix);

            //debug
            //PrintMatrix(matrix);
            //debug

            return true;
        }

        //public static T Clone<T>(T obj)
        //{
        //    using (Stream objectStream = new MemoryStream())
        //    {
        //        IFormatter formatter = new BinaryFormatter();
        //        formatter.Serialize(objectStream, obj);
        //        objectStream.Seek(0, SeekOrigin.Begin);
        //        return (T)formatter.Deserialize(objectStream);
        //    }
        //}

        public static T XmlClone<T>(T obj)
        {
            using (Stream stream = new MemoryStream())
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                serializer.Serialize(stream, obj);
                stream.Seek(0, SeekOrigin.Begin);
                return (T)serializer.Deserialize(stream);
            }
        }

        //debug
        public static void PrintMatrix(int[,] matrix)
        {
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                try
                {
                    String line = "";
                    for (int j = 0; j < matrix.GetLength(1); j++)
                    {
                        line += String.Format("{0,3}", matrix[i, j].ToString());
                    }
                    WriteLogFile(line);
                }
                catch (System.Exception ex)
                {
                    string error = ex.Message;
                }
            }
        }

        //debug
        public static void PrintWIList(String describe, List<WORK_INSTRUCTION_STATUS> wiList)
        {
            WriteLogFile(describe);
            foreach (WORK_INSTRUCTION_STATUS item in wiList)
                PrintWorkInstruction(item, wiList.IndexOf(item));
        }

        public static void PrintWorkInstruction(WORK_INSTRUCTION_STATUS wi,int index)
        {
            Type InfoType = wi.GetType();
            System.Reflection.PropertyInfo[] fields = InfoType.GetProperties();
            
            String[] filterArray = { "CONTAINER_ID", "JOB_ID", "WORK_QUEUE", "MOVE_KIND", /*"MOVE_STAGE",*/ "ORDER_SEQ", "VESSEL_ID",
                              "LOGICAL_PREDECESSOR","PHYSICAL_PREDECESSOR","LIFT_REFERENCE","CARRY_REFERENCE","DOOR_DIRECTION",
                              "PLAN_TIM","T_StartTime","T_EndTime"};
            String text = "";
            foreach (System.Reflection.PropertyInfo field in fields)
            {
                if (filterArray.Contains(field.Name))
                {
                    String str = field.GetValue(wi, null) == null ? " " : field.GetValue(wi, null).ToString();
                    str = field.Name + ":" + str + " ";
                    text += str;
                }
            }
            WriteLogFile(index.ToString() + "、 " + text);
        }
       
       //debug
        public static void WriteLogFile(string sMessage)
        {
            //创建文件夹
            String logpath = System.Environment.CurrentDirectory;
            if (!File.Exists(logpath))
            {
                System.IO.Directory.CreateDirectory(logpath);
                logpath += "\\log\\";
            }
            //创建文件
            String  fileName = "NewModel2";
            string strFilePath = logpath + fileName + ".txt";
            if (!File.Exists(strFilePath))
            {
                FileStream f = File.Create(strFilePath);
                f.Close();
            }

            FileInfo fi = new FileInfo(strFilePath);
            if(fi.Length > 50*1024*1024)
            {
                String oldFileName = "NewModel2_" + DateTime.Now.Day.ToString() + DateTime.Now.Hour.ToString() + DateTime.Now.Minute;
                File.Move(strFilePath, logpath + oldFileName + ".txt");
                FileStream f = File.Create(strFilePath);
                f.Close();
            }


            FileStream fs = new FileStream(strFilePath, FileMode.OpenOrCreate, FileAccess.Write);
            StreamWriter m_streamWriter = new StreamWriter(fs);
            m_streamWriter.BaseStream.Seek(0, SeekOrigin.End);
            m_streamWriter.WriteLine(/*DateTime.Now.ToString() + "： " +*/ sMessage + "\n");
            m_streamWriter.Flush();
            m_streamWriter.Close();
            fs.Close();
        }
        //End
    };
}
