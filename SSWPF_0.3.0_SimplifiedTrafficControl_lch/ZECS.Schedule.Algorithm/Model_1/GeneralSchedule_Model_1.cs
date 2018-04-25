using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// 总体调度类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-29       ver1.0
    /// </summary>
    public class GeneralSchedule_Model_1
    {
        private static GeneralSchedule_Model_1 instance;
        private static TimeSpan m_tTaskEstimate = new TimeSpan(0,0,5,0);
        private static TimeSpan m_tBalanceTime = new TimeSpan(0,0,9,0);


        /// <summary>
        /// 当前桥机作业计划列表
        /// </summary>
        private List<VesselCWPEntity> CWPList = new List<VesselCWPEntity>();
   

        public static GeneralSchedule_Model_1 Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GeneralSchedule_Model_1();
                }
                return instance;
            }
        }

        private GeneralSchedule_Model_1()
        {
        }

        private void Initialize()
        {
           
        }

        /*最初思路：
         //任务合并,调用作业路平衡,多点平衡.
            //1.合并任务
            bool bRet = ZECS.Schedule.Algorithm.Schedule_Algo.Instance.CombineJobs(ref dbData_Schedule.m_DBData_TOS);


            //2.计算各个桥机在当前时间段内需新增待执行任务列表的任务数量
            List<STSWorkBalanceData> stsBalance = new List<STSWorkBalanceData>();
            bRet = Schedule_Algo.Instance.CalSTSWorkBalance(dbData_Schedule, ref stsBalance);
           
            //3.计算各个箱区为各个QC的各Mask装船出箱的数量
            List<BlockMaskQCLoadCount> listBlockMaskQCLoadCount = Schedule_Algo.Instance.CalBlockMaskQCLoadCount(dbData_Schedule);


            //4.选择任务,顺序,优先级.输出Work_Instruction表
            //为各QC选择给定数量的待执行任务，并分别给出这些任务间的建议执行次序
            //暂定返回的待执行任务保存在dbData_Schedule.m_DBData_TOS中 
            bRet = Schedule_Algo.Instance.CalSTSWorkInstruction(stsBalance, listBlockMaskQCLoadCount, ref dbData_Schedule, ref jobsOrderedDecisionTable);
        */

        /// <summary>
        /// 接口函数，总体调度Mode1
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="jobsOrderedDecisionTable"></param>
        /// <returns></returns>
        public bool Schedule_Mode1(DBData_TOS dbData_Schedule,
                                      ref OrderedDecisionTable jobsOrderedDecisionTable)
        {
            if (dbData_Schedule == null)
                return false;

            DBData_TOS ScheduleDBData = null;
            try
            {
                ScheduleDBData = Helper.Clone<DBData_TOS>(dbData_Schedule);
                if (ScheduleDBData == null)
                    return false;
            }
            catch (Exception ex)
            {
                Logger.JobManager_TOS.Error("ScheduleECS_Mode1()->Clone Error:" + ex.Message);
                return false;
            }

            Initialize();

            List<List<WORK_INSTRUCTION_STATUS>> WQWIList = new List<List<WORK_INSTRUCTION_STATUS>>();

            DateTime tEarliest = new DateTime(1,1,1,0,0,0);
            DateTime tLatest   = new DateTime(1,1,1,0,0,0);
            if (!WQRelateWI(ScheduleDBData, ref tEarliest, ref tLatest, ref WQWIList))
                return false;

            if (!CutWIListByTime(tEarliest + new TimeSpan(0, 0, 20, 0), ref  WQWIList))
                return false;

            List<WORK_INSTRUCTION_STATUS> WIList = CombineWIList(WQWIList);
            if (WIList == null)
                return false;

            if (!JobListIntersectWIList(ScheduleDBData, ref WIList))
                return false;

            if (!GetRidOfAllExcutingJob(ScheduleDBData, ref WIList))
                return false;

            if (!SortWIListByTime(ref  WIList))
                return false;

            Dictionary<string, SimpleBlockInfo> htBlockInfo = new Dictionary<string, SimpleBlockInfo>();

            //ASC任务/Order在OCR确认后才产生
            if (CreateBlockInfoHashTable(ScheduleDBData, WIList, ref htBlockInfo))
            {
                if (CaculateAscCurTaskWeight(ScheduleDBData.m_listASC_Task, ref htBlockInfo))
                {
                    DistributeAscWorkTask(ref WIList, htBlockInfo);
                }
            }

            CreateTopological( WIList,ScheduleDBData.m_listSTS_WORK_QUEUE_STATUS, ref jobsOrderedDecisionTable );
                                      
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
        private bool WQRelateWI(DBData_TOS dbData_TOS, ref DateTime tEarliest, ref DateTime tLatest, 
                                            ref List<List<WORK_INSTRUCTION_STATUS>> WQWIList)
        {
            bool bRet = false;

            if (dbData_TOS == null || WQWIList == null)
                return bRet;

            List<STS_WORK_QUEUE_STATUS> WQList = dbData_TOS.m_listSTS_WORK_QUEUE_STATUS;
            List<WORK_INSTRUCTION_STATUS> WIList = dbData_TOS.m_listWORK_INSTRUCTION_STATUS;

            if (WQList == null || WIList == null)
                return bRet;

            if (WQList.Count < 1 || WIList.Count < 1)
                return bRet;

            WQList = WQList.Where(   wq => wq.MOVE_KIND == Move_Kind.LOAD 
                                  || wq.MOVE_KIND == Move_Kind.DSCH).ToList<STS_WORK_QUEUE_STATUS>();
            //debug
            //WQList = WQList.Where(wq => wq.VESSEL_VISIT == "2504").ToList();
            //WQList.Sort((wq1, wq2) => wq1.START_TIME.CompareTo(wq2.START_TIME));
            //debug

            STS_WORK_QUEUE_STATUS aWQ = WQList[0];
            tEarliest = aWQ.START_TIME;
            tLatest = aWQ.END_TIME;

            foreach (STS_WORK_QUEUE_STATUS WQ in WQList)
            {
                List<WORK_INSTRUCTION_STATUS> childWIlist = WIList.Where(u => u.WORK_QUEUE == WQ.WORK_QUEUE).ToList();
                
                if (childWIlist == null) 
                    continue;
                if (childWIlist.Count < 1) 
                    continue;

               // if (WQ.MOVE_KIND == Move_Kind.LOAD)
                    childWIlist.Sort((u1, u2) => u1.ORDER_SEQ.CompareTo(u2.ORDER_SEQ));
               // else if(WQ.MOVE_KIND == Move_Kind.DSCH)
               //     childWIlist.Sort((u1, u2) => u1.ORDER_SEQ.CompareTo(u2.ORDER_SEQ));

                DateTime tWQStart = WQ.START_TIME;
                if (tWQStart < tEarliest) tEarliest = tWQStart;
                DateTime tWQEnd = WQ.END_TIME;
                if (tWQEnd > tLatest) tLatest = tWQEnd;

                TimeSpan tSpan = new TimeSpan((tWQEnd - tWQStart).Ticks / childWIlist.Count);

                foreach (WORK_INSTRUCTION_STATUS WI in childWIlist)
                {
                    WI.T_StartTime = tWQStart;
                    WI.T_EndTime = tWQStart + tSpan;
                    tWQStart = WI.T_EndTime;
                }

                Debug.Assert(childWIlist != null && childWIlist.Count > 0, "WQRelateWI(): Illegal ChildWIlist");
                WQWIList.Add(childWIlist);
            }

            if (WQWIList.Count > 0) bRet = true;
               
            return bRet;
        }


        /// <summary>
        /// 按给定时间截取WorkInstrution任务集
        /// </summary>
        /// <param name="tTime"></param>
        /// <param name="WQWIList"></param>
        /// <returns></returns>
        private bool CutWIListByTime(DateTime tTime, ref List<List<WORK_INSTRUCTION_STATUS>> WQWIList)
        {
            bool bRet = false;

            if (WQWIList == null)
                return bRet;

            List<List<WORK_INSTRUCTION_STATUS>> tempWQWIList = new List<List<WORK_INSTRUCTION_STATUS>>();
            //tempWQWIList = Clone<List<List<WORK_INSTRUCTION_STATUS>>>(WQWIList);

            foreach (List<WORK_INSTRUCTION_STATUS> WIList in WQWIList)
            {
                List<WORK_INSTRUCTION_STATUS> tempWIList = WIList.Where(u => u.T_EndTime < tTime).ToList();
                
                tempWIList.Sort((u1, u2) => u1.ORDER_SEQ.CompareTo(u2.ORDER_SEQ));

                if (tempWIList != null && tempWIList.Count > 0)
                {
                    //双箱任务不可分离
                    if (WIList.Count > tempWIList.Count && tempWIList.Count > 0 && WIList.Count > 1)
                    {
                        if (IsOneLift(WIList[tempWIList.Count], tempWIList[tempWIList.Count - 1]))
                            tempWIList.Add(WIList[tempWIList.Count]);
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

        private bool IsOneLift(WORK_INSTRUCTION_STATUS wi1, WORK_INSTRUCTION_STATUS wi2)
        {
            if (!string.IsNullOrWhiteSpace(wi1.LIFT_REFERENCE) && wi1.LIFT_REFERENCE == wi2.LIFT_REFERENCE)
                return true;

            //if (wi1.ORDER_SEQ == wi2.ORDER_SEQ)
            //    return true;

            return false;
        }

        /// <summary>
        /// 合并WorkInstruction任务集
        /// </summary>
        /// <param name="WQWIList"></param>
        /// <returns></returns>
        private List<WORK_INSTRUCTION_STATUS> CombineWIList(List<List<WORK_INSTRUCTION_STATUS>> WQWIList)
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
        private bool JobListIntersectWIList(DBData_TOS dbData_TOS, ref List<WORK_INSTRUCTION_STATUS> WIList)
        {
            List<STS_Task> STSTaskList = dbData_TOS.m_listSTS_Task;
            if (STSTaskList == null || WIList == null)
                return false;

            //List<string> taskContainerIDList = STSTaskList.Select(u => u.Task.CONTAINER_ID).ToList();
            //List<string> wiContainerIDList = WIList.Select(u => u.CONTAINER_ID).ToList();

            //List<string> intersectList = taskContainerIDList.Intersect(wiContainerIDList).ToList();

            //List<WORK_INSTRUCTION_STATUS> tempWIList = new List<WORK_INSTRUCTION_STATUS>();

            //foreach (WORK_INSTRUCTION_STATUS wi in WIList)
            //{
            //    foreach (string containerID in intersectList)
            //    {
            //        if (wi.CONTAINER_ID == containerID)
            //        {
            //            tempWIList.Add(wi);break;
            //        }
            //    }
            //}

            List<string> taskJobIDList = STSTaskList.Select(u => u.Task.JOB_ID).ToList();
            List<string> wiJobIDList = WIList.Select(u => u.JOB_ID).ToList();

            List<string> intersectList = taskJobIDList.Intersect(wiJobIDList).ToList();

            List<WORK_INSTRUCTION_STATUS> tempWIList = new List<WORK_INSTRUCTION_STATUS>();

            foreach (WORK_INSTRUCTION_STATUS wi in WIList)
            {
                if (intersectList.Any(jobId => wi.JOB_ID == jobId))
                {
                    tempWIList.Add(wi);
                }
            }

            if (tempWIList.Count > 0)
            {
                WIList = tempWIList;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 从WorkInstruction任务集中清除正在工作的任务
        /// </summary>
        /// <param name="dbData_TOS"></param>
        /// <param name="WIList"></param>
        /// <returns></returns>
        private bool GetRidOfAllExcutingJob(DBData_TOS dbData_TOS, ref List<WORK_INSTRUCTION_STATUS> WIList)
        {
            if (dbData_TOS == null || WIList == null)
                return false;

            List<ASC_Task> ascTaskList = dbData_TOS.m_listASC_Task;
            List<AGV_Task> agvTaskList = dbData_TOS.m_listAGV_Task;
            List<STS_Task> stsTaskList = dbData_TOS.m_listSTS_Task;

            //List<string> ascContainerIDList = null;
            //List<string> agvContainerIDList = null;
            //List<string> stsContainerIDList = null;

            //if (ascTaskList != null)
            //    ascContainerIDList = 
            //        ascTaskList.Where(u => u.TaskState != TaskStatus.None 
            //            && u.TaskState != TaskStatus.Almost_Ready).Select(u => u.Task.CONTAINER_ID).ToList<string>();

            //if (agvTaskList != null)
            //    agvContainerIDList = 
            //        agvTaskList.Where(u => u.TaskState != TaskStatus.None 
            //            && u.TaskState != TaskStatus.Almost_Ready).Select(u => u.Task.CONTAINER_ID).ToList<string>();

            //if (stsTaskList != null)
            //    stsContainerIDList = 
            //        stsTaskList.Where(u => u.TaskState != TaskStatus.None
            //            && u.TaskState != TaskStatus.Almost_Ready).Select(u => u.Task.CONTAINER_ID).ToList<string>();

            //List<string> unionList = ascContainerIDList.Union(agvContainerIDList).ToList().Union(stsContainerIDList).ToList();

            //List<WORK_INSTRUCTION_STATUS> tempWIList = WIList.Where(WI => null == unionList.Find(x => x == WI.CONTAINER_ID)).ToList();

            List<string> ascJobIDList = new List<string>();
            List<string> agvJobIDList = new List<string>();
            List<string> stsJobIDList = new List<string>();

            if (ascTaskList != null)
                ascJobIDList =
                    ascTaskList.Where(u => u.TaskState != TaskStatus.None
                        && u.TaskState != TaskStatus.Almost_Ready).Select(u => u.Task.JOB_ID).ToList<string>();

            if (agvTaskList != null)
                agvJobIDList =
                    agvTaskList.Where(u => u.TaskState != TaskStatus.None
                        && u.TaskState != TaskStatus.Almost_Ready).Select(u => u.Task.JOB_ID).ToList<string>();

            if (stsTaskList != null)
                stsJobIDList =
                    stsTaskList.Where(u => u.TaskState != TaskStatus.None
                        && u.TaskState != TaskStatus.Almost_Ready).Select(u => u.Task.JOB_ID).ToList<string>();

            List<string> unionList = ascJobIDList.Union(agvJobIDList).ToList().Union(stsJobIDList).ToList();

            List<WORK_INSTRUCTION_STATUS> tempWIList = WIList.Where(WI => null == unionList.Find(x => x == WI.JOB_ID)).ToList();

            if (tempWIList.Count > 0)
            {
                WIList = tempWIList;
                return true;
            }

            return false;
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
        /// 创建堆场的哈希表
        /// </summary>
        /// <param name="dbData_TOS"></param>
        /// <param name="WIList"></param>
        /// <param name="htBlockInfo"></param>
        /// <returns></returns>
        private bool CreateBlockInfoHashTable(DBData_TOS dbData_TOS, List<WORK_INSTRUCTION_STATUS> WIList,
                                         ref Dictionary<string, SimpleBlockInfo> htBlockInfo)
        {
            if (dbData_TOS == null || dbData_TOS.m_listASC_Task == null || WIList == null)
                return false;

            List<ASC_Task> ascTaskList = dbData_TOS.m_listASC_Task;

            List<string> IDList = ascTaskList.Select(u => u.Task.YARD_ID).ToList<string>();
            List<string> blockIDList = IDList.Distinct().ToList<string>();

            htBlockInfo.Clear();
            foreach (string blockID in blockIDList)
                htBlockInfo.Add(blockID, new SimpleBlockInfo(blockID));

            foreach (ASC_Task ascTask in ascTaskList)
            {
                string CONTAINER_ID = ascTask.Task.CONTAINER_ID;
                string CONTAINER_ISO = ascTask.Task.CONTAINER_ISO;

                List<WORK_INSTRUCTION_STATUS> wiList = WIList.Where(u => u.CONTAINER_ID == CONTAINER_ID
                                                                      && u.MOVE_KIND == Move_Kind.LOAD).ToList();
                if (wiList.Count > 0)
                {
                    string factor = wiList[0].CONTAINER_STOW_FACTOR;
                    double weightMargin = wiList[0].CONTAINER_WEIGHT_MARGIN_KG;
                    ContainerMask mask = new ContainerMask(factor, weightMargin);

                    string YARD_ID = ascTask.Task.YARD_ID;
                    if (htBlockInfo.ContainsKey(YARD_ID))
                    {
                        int length = 0, height = 0, weight = 0;
                        int.TryParse(ascTask.Task.CONTAINER_LENGTH, out length);
                        int.TryParse(ascTask.Task.CONTAINER_HEIGHT, out height);
                        int.TryParse(ascTask.Task.CONTAINER_WEIGHT, out weight);

                        int bay = 0, lane = 0, tier = 0;
                        string JOB_TYPE = ascTask.Task.JOB_TYPE;
                        if (JOB_TYPE.ToLower() == "load")
                        {
                           int.TryParse( ascTask.Task.FROM_BAY,  out bay  );
                           int.TryParse( ascTask.Task.FROM_LANE, out lane );
                           int.TryParse( ascTask.Task.FROM_TIER, out tier );

                           ContainerInfo containerInfo = new ContainerInfo(CONTAINER_ID, CONTAINER_ISO,
                                                          length, height, weight, 0, 0, bay, lane, tier, mask);
                           htBlockInfo[YARD_ID].AddContainer(containerInfo);
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 估算堆场Block当前待执行任务队列的耗时，给出Block当前任务耗时权值
        /// </summary>
        /// <param name="ascTaskList"></param>
        /// <param name="htBlockInfo"></param>
        /// <returns></returns>
        private bool CaculateAscCurTaskWeight( List<ASC_Task> ascTaskList, 
                                               ref Dictionary<string, SimpleBlockInfo> htBlockInfo )
        {
            if (ascTaskList == null || htBlockInfo.Count < 1)
                return false;

            foreach (SimpleBlockInfo block in htBlockInfo.Values)
            {
                List<ASC_Task> curList = ascTaskList.Where(u => (int)u.TaskState >= (int)TaskStatus.Ready
                                                             && (int)u.TaskState <= (int)TaskStatus.Complete
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
        private bool DistributeAscWorkTask( ref List<WORK_INSTRUCTION_STATUS> WIList,
                                            Dictionary<string, SimpleBlockInfo> htBlockInfo )
        {
            if (WIList == null)
                return false;

            foreach (WORK_INSTRUCTION_STATUS wi in WIList)
            {
                String blockID = null;
                String containerID = null;
                TimeSpan tMinWeightValue = new TimeSpan(1, 0, 0, 0);
                foreach (SimpleBlockInfo block in htBlockInfo.Values)
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
                    htBlockInfo[blockID].WeightValue = tMinWeightValue;
                    wi.T_Load_BlockNO = blockID;

                    if(containerID != null)
                        htBlockInfo[blockID].RemoveContainer(containerID);
                }
            }


            return true;
        }

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
        private bool CreateTopological( List<WORK_INSTRUCTION_STATUS> WIList, 
                                        List<STS_WORK_QUEUE_STATUS> WQList, ref OrderedDecisionTable partialOrderedTable )
        {
            if (WIList == null)
                return false;

            //创建WorkQueue表
            Dictionary<String, STS_WORK_QUEUE_STATUS> WQTable = new Dictionary<String, STS_WORK_QUEUE_STATUS>();
            foreach (STS_WORK_QUEUE_STATUS wq in WQList)
                WQTable.Add(wq.WORK_QUEUE, wq);

          
            int[,] matrix = new int[WIList.Count, WIList.Count];

            //初始化矩阵
            InitMatrix(ref matrix);

            //取所有装船WorkQueue中第一条任务的完成时间，并比较获得最迟完成时间
            DateTime tFirstEnd = new DateTime(1, 1, 1, 0, 0, 0);
            Dictionary<String,WORK_INSTRUCTION_STATUS> htFirstTask = new Dictionary<String,WORK_INSTRUCTION_STATUS>();

            foreach (STS_WORK_QUEUE_STATUS WQ in WQList)
            {
                List<WORK_INSTRUCTION_STATUS> childWIlist = WIList.Where(u => u.WORK_QUEUE == WQ.WORK_QUEUE
                                                                         && u.MOVE_KIND == Move_Kind.LOAD).ToList();

                if (childWIlist == null ||childWIlist.Count < 1 )
                    continue;
  
                childWIlist.Sort((u1, u2) => u1.T_StartTime.CompareTo(u2.T_StartTime));

                if (childWIlist[0].T_EndTime > tFirstEnd)
                    tFirstEnd = childWIlist[0].T_EndTime;

                if(!htFirstTask.ContainsKey(childWIlist[0].JOB_ID))
                    htFirstTask.Add(childWIlist[0].JOB_ID, childWIlist[0]);
            }

            //根据偏序关系填充矩阵，三角形法操作
            for (int i = 0; i < WIList.Count; i++)
            {
                for (int j = i; j < WIList.Count; j++)
                {
                    if (j > i)
                    {
                        if (WIList[i].MOVE_KIND == Move_Kind.LOAD && WIList[j].MOVE_KIND == Move_Kind.LOAD)
                        {
                            if (WIList[i].WORK_QUEUE == WIList[j].WORK_QUEUE)
                            {
                                if (WIList[i].T_StartTime > WIList[j].T_StartTime)
                                {
                                    matrix[i, j] = 1;
                                    matrix[j, i] = -1;
                                }
                                else if (WIList[i].T_StartTime == WIList[j].T_StartTime)
                                {
                                    matrix[i, j] = 0;
                                    matrix[j, i] = 0;
                                }
                                else
                                {
                                    matrix[i, j] = -1;
                                    matrix[j, i] = 1;
                                }
                            }
                            else
                            {
                                if (WIList[i].T_StartTime - WIList[j].T_StartTime > m_tBalanceTime)
                                {
                                    matrix[i, j] = 1;
                                    matrix[j, i] = -1;
                                }
                            }
                        }
                        else if (WIList[i].MOVE_KIND == Move_Kind.LOAD && WIList[j].MOVE_KIND == Move_Kind.DSCH
                                || WIList[i].MOVE_KIND == Move_Kind.DSCH && WIList[j].MOVE_KIND == Move_Kind.LOAD)
                        {
                            if (WQTable[WIList[i].WORK_QUEUE].QC_ID == WQTable[WIList[j].WORK_QUEUE].QC_ID)
                            {
                                if (WIList[i].MOVE_KIND == Move_Kind.LOAD)
                                {
                                    if (WQTable[WIList[i].WORK_QUEUE].START_TIME >= WQTable[WIList[j].WORK_QUEUE].START_TIME)
                                    {
                                        matrix[i, j] = 1;
                                        matrix[j, i] = -1;
                                    }
                                }
                                else
                                {
                                    if (WQTable[WIList[i].WORK_QUEUE].END_TIME >= WQTable[WIList[j].WORK_QUEUE].START_TIME)
                                    {
                                        matrix[i, j] = 1;
                                        matrix[j, i] = -1;
                                    }
                                }

                            }
                        }

                        //确定不同QC间卸船与装船任务的偏序关系
                        if (   WIList[i].WORK_QUEUE != WIList[j].WORK_QUEUE
                            && htFirstTask.ContainsKey(WIList[i].JOB_ID) && WIList[j].MOVE_KIND == Move_Kind.DSCH )
                        {
                            if (WIList[j].T_StartTime > tFirstEnd && tFirstEnd > new DateTime(1, 1, 1, 0, 0, 0))
                            {
                                matrix[i, j] = -1;
                                matrix[j, i] =  1;
                            }
                        }
                    }
                }
            }

            partialOrderedTable.SetPartialOrderedTable(WIList, matrix);
           
            //debug
            PrintMatrix(matrix);
            List<WORK_INSTRUCTION_STATUS> testList = new List<WORK_INSTRUCTION_STATUS>();
            TopoLogicSort(WIList,matrix,ref testList);
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
                        line += String.Format("{0,3}",matrix[i, j].ToString());
                    }
                    WriteFile(line);
                }
                catch (System.Exception ex)
                {
                    string error = ex.Message;
                }
              
            }

        }

        //debug
        public bool TopoLogicSort(List<WORK_INSTRUCTION_STATUS> WIList, int[,] inMatrix, ref List<WORK_INSTRUCTION_STATUS> outWIList)
        {
            if (WIList == null)
                return false;

            int count = WIList.Count;
            int dimension0 = inMatrix.GetLength(0);
            int dimension1 = inMatrix.GetLength(1);

            if (!(dimension0 == dimension1 && dimension0 == count))
                return false;

            //int[,] matrix = (int[,])m_DecisionTable.Clone();
            int[,] matrix = new int[count, count];
            Array.Copy(inMatrix, matrix, inMatrix.Length);

            List<WORK_INSTRUCTION_STATUS> newWIList = new List<WORK_INSTRUCTION_STATUS>();
            newWIList.Clear();

            while (count - newWIList.Count > 0)
            {
                int guard = newWIList.Count;

                for (int i = 0; i < count; i++)
                {
                    bool bDependence = false;
                    List<int> tempJList = new List<int>();
                    tempJList.Clear();

                    for (int j = 0; j < count; j++)
                    {
                        if (matrix[i, j] == -1)
                            tempJList.Add(j);

                        if (matrix[i, j] == 1 && !bDependence)
                            bDependence = true;

                    }

                    if (!bDependence)
                    {
                        //如果newWIList存在相同的WI，说明偏序集合存在有向环，则集合非法
                        if (newWIList.Any(tempWI => tempWI.JOB_ID == WIList[i].JOB_ID))
                        {
                            return false;
                        }

                        foreach (int x in tempJList)
                        {
                            matrix[i, x] = 0;
                            matrix[x, i] = 0;
                        }

                        newWIList.Add(WIList[i]);
                    }
                }
                //检查偏序矩阵是否非法
                if (guard == newWIList.Count)
                    return false;
            }

            //debug
            WriteFile("\n");
            PrintMatrix(matrix);
            //debug

            outWIList = Helper.Clone<List<WORK_INSTRUCTION_STATUS>>(newWIList);

            return true;
        }

        //debug
        public static void WriteFile(string sMessage)
        {
            //创建文件夹
            String logpath = System.Environment.CurrentDirectory;
            if (!File.Exists(logpath))
            {
                System.IO.Directory.CreateDirectory(logpath);
            }
            //创建文件
            string strFilePath = logpath + "\\MaxtixTest" + ".txt";
            if (!File.Exists(strFilePath))
            {
                FileStream f = File.Create(strFilePath);
                f.Close();
            }
            FileStream fs = new FileStream(strFilePath, FileMode.OpenOrCreate, FileAccess.Write);
            StreamWriter m_streamWriter = new StreamWriter(fs);
            m_streamWriter.BaseStream.Seek(0, SeekOrigin.End);
            m_streamWriter.WriteLine(sMessage);
            m_streamWriter.Flush();
            m_streamWriter.Close();
            fs.Close();
        }
   
    };
}
