using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.Define.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.Define;
using ZECS.Schedule.DB;
using System.IO;

namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// 调度模型2
    /// </summary>
    public class ScheduleModel2
    {
        //调度模型2实例
        private static ScheduleModel2 instance = null;
        //
        private DBData_Schedule dbSchedule = null;
        //
        private List<STS_WORK_QUEUE_STATUS> wqList = null;
        private List<WORK_INSTRUCTION_STATUS> wiList = null;
        //
        private List<STS_Task> stsTaskList = null;
        private List<ASC_Task> ascTaskList = null;
        private List<AGV_Task> agvTaskList = null;
        //
        private List<STS_Order> stsOrderList = null;
        private List<STS_Command> stsCommandList = null;
        private List<ASC_Order> ascOrderList = null;
        private List<ASC_Command> ascCommandList = null;
        private List<AGV_Order> agvOrderList = null;
        private List<AGV_Command> agvCommandList = null;
        //
        private List<Block_Container> blockContainerList = null;
        //模型
        private List<WorkQc> workQcList = new List<WorkQc>();
        private List<WorkBlock> workBlockList = new List<WorkBlock>();
        private List<WorkMask> workMaskList = new List<WorkMask>();
        private List<BestChoice> bestChoiceList = new List<BestChoice>();
        


        /// <summary>
        /// 同一箱区出箱的WI间的时间差阈值，超过此阈值的WI间增加先后序值关系
        /// </summary>
        private double threshMinForSameBlockWI = 10;

        private DateTime schduleStartTime;

        private DateTime schduleEndTime;

        /// <summary>
        /// 构造函数
        /// </summary>
        private ScheduleModel2()
        {

        }

        /// <summary>
        /// 实例化调度模型2
        /// </summary>
        public static ScheduleModel2 Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ScheduleModel2();
                }
                return instance;
            }
        }

        /// <summary>
        /// 总体调度模式2
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="partialOrderTable"></param>
        /// <returns></returns>
        public bool Schedule_Mode2(DBData_Schedule dbData_Schedule, ref PartialOrderGraph partialOrderTable)
        {
            dbSchedule = dbData_Schedule;
            bool success = Init();
            if (success)
            {
                Build();
                Working();
                Excuting(); 
                UnExcute();
                Reset(ref partialOrderTable);
                FreeTime();
                Mask();
                Greedy();
                Output(ref partialOrderTable);
            }
            return success;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <returns></returns>
        private bool Init()
        {
            //Total
            if (dbSchedule == null)
            {
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule is null Error");
                return false;
            }
            //Tos
            if (dbSchedule.m_DBData_TOS == null)
            {
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_TOS is null Error");
                return false;
            }
            //Tos WorkQueue
            if (dbSchedule.m_DBData_TOS.m_listSTS_WORK_QUEUE_STATUS != null)
            {
                wqList = dbSchedule.m_DBData_TOS.m_listSTS_WORK_QUEUE_STATUS.Where(t => t.MOVE_KIND == Move_Kind.LOAD || t.MOVE_KIND == Move_Kind.DSCH).ToList();
            }
            else
            {
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_TOS->STS_WORK_QUEUE_STATUS is null Ignore");
                return false;
            }
            //Tos Instruction
            if (dbSchedule.m_DBData_TOS.m_listWORK_INSTRUCTION_STATUS != null)
            {
                wiList = dbSchedule.m_DBData_TOS.m_listWORK_INSTRUCTION_STATUS.Where(t => t.MOVE_KIND == Move_Kind.LOAD || t.MOVE_KIND == Move_Kind.DSCH).ToList();
            }
            else
            {
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_TOS->WORK_INSTRUCTION_STATUS is null Ignore");
                return false;
            }
            //STS Task
            if (dbSchedule.m_DBData_TOS.m_listSTS_Task != null)
            {
                stsTaskList = dbSchedule.m_DBData_TOS.m_listSTS_Task;
            }
            else
            {
                stsTaskList = new List<STS_Task>();
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_TOS->STS_Task is null Ignore");
            }
            //ASC Task
            if (dbSchedule.m_DBData_TOS.m_listASC_Task != null)
            {
                ascTaskList = dbSchedule.m_DBData_TOS.m_listASC_Task;
            }
            else
            {
                ascTaskList = new List<ASC_Task>();
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_TOS->ASC_Task is null Ignore");
            }
            //AGV Task
            if (dbSchedule.m_DBData_TOS.m_listAGV_Task != null)
            {
                agvTaskList = dbSchedule.m_DBData_TOS.m_listAGV_Task;
            }
            else
            {
                agvTaskList = new List<AGV_Task>();
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_TOS->AGV_Task is null Ignore");
            }
            //STS Order
            if (dbSchedule.m_DBData_STSMS != null && dbSchedule.m_DBData_STSMS.m_listSTS_Order != null)
            {
                stsOrderList = dbSchedule.m_DBData_STSMS.m_listSTS_Order;
            }
            else
            {
                stsOrderList = new List<STS_Order>();
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_STSMS->STS_Order is null Ignore");
            }
            //STS Command
            if (dbSchedule.m_DBData_STSMS != null && dbSchedule.m_DBData_STSMS.m_listSTS_Command != null)
            {
                stsCommandList = dbSchedule.m_DBData_STSMS.m_listSTS_Command;
            }
            else
            {
                stsCommandList = new List<STS_Command>();
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_STSMS->STS_Command is null Ignore");
            }
            //ASC Order
            if (dbSchedule.m_DBData_BMS != null && dbSchedule.m_DBData_BMS.m_listASC_Order != null)
            {
                ascOrderList = dbSchedule.m_DBData_BMS.m_listASC_Order;
            }
            else
            {
                ascOrderList = new List<ASC_Order>();
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_BMS->ASC_Order is null Ignore");
            }
            //ASC Command
            if (dbSchedule.m_DBData_BMS != null && dbSchedule.m_DBData_BMS.m_listASC_Command != null)
            {
                ascCommandList = dbSchedule.m_DBData_BMS.m_listASC_Command;
            }
            else
            {
                ascCommandList = new List<ASC_Command>();
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_BMS->ASC_Command is null Ignore");
            }
            //AGV Order
            if (dbSchedule.m_DBData_VMS != null && dbSchedule.m_DBData_VMS.m_listAGV_Order != null)
            {
                agvOrderList = dbSchedule.m_DBData_VMS.m_listAGV_Order;
            }
            else
            {
                agvOrderList = new List<AGV_Order>();
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_VMS->AGV_Order is null Ignore");
            }
            //AGV Command
            if (dbSchedule.m_DBData_VMS != null && dbSchedule.m_DBData_VMS.m_listAGV_Command != null)
            {
                agvCommandList = dbSchedule.m_DBData_VMS.m_listAGV_Command;
            }
            else
            {
                agvCommandList = new List<AGV_Command>();
                Logger.Algorithm.Error("ScheduleModel2->DBData_Schedule->m_DBData_VMS->AGV_Command is null Ignore");
            }
            //Block Container
            blockContainerList = DB_ECS.Instance.Get_All_Block_Container();
            //Clear
            workQcList.Clear();
            workBlockList.Clear();
            workMaskList.Clear();
            bestChoiceList.Clear();
            //WorkQcArray            
            string[] WorkQcArray = new string[3] { "118", "119", "120" };
            foreach (string qc in WorkQcArray)
            {
                workQcList.Add(new WorkQc(qc));
            }
            //WorkBlockArray
            string[] WorkBlockArray = new string[8] { "A02", "A03", "A04", "A05", "A06", "A07", "A08", "A09" };
            foreach (string block in WorkBlockArray)
            {
                workBlockList.Add(new WorkBlock(block));
            }
            return true;
        }

        /// <summary>
        /// 构建WQ,WI以及QC
        /// </summary>
        private void Build()
        {
            //foreach (STS_WORK_QUEUE_STATUS wq in wqList)
            //{
            //    //WorkQueue
            //    WorkQueue workQueue = new WorkQueue(wq);
            //    workQueue.QC_ID = wq.QC_ID;
            //    foreach (WORK_INSTRUCTION_STATUS wi in wiList)
            //    {
            //        //WorkInstruction
            //        if (wi.WORK_QUEUE == wq.WORK_QUEUE)
            //        {
            //            WorkInstruction workInstruction = new WorkInstruction(wi);
            //            workInstruction.QC_ID = wq.QC_ID;
            //            workInstruction.WORK_QUEUE = wq.WORK_QUEUE;
            //            //包含所有的WI
            //            workQueue.WorkInstructionList.Add(workInstruction);
            //        }
            //    }
            //    //WorkQc
            //    WorkQc workQc = workQcList.Find(t => t.QC_ID == wq.QC_ID);
            //    if (workQc == null)
            //    {
            //        workQc = new WorkQc(wq.QC_ID);
            //        workQcList.Add(workQc);
            //    }
            //    //包含所有的WQ(是否需要？）
            //    workQc.WorkQueueList.Add(workQueue);
            //}

            foreach (STS_WORK_QUEUE_STATUS wq in wqList)
            {
                //WorkQueue
                WorkQueue workQueue = new WorkQueue(wq);
                workQueue.QC_ID = wq.QC_ID;

                //WorkQc
                WorkQc workQc = workQcList.Find(t => t.QC_ID == wq.QC_ID);
                if (workQc == null)
                {
                    workQc = new WorkQc(wq.QC_ID);
                    workQcList.Add(workQc);
                }
                //包含所有的WQ(是否需要？）
                workQc.WorkQueueList.Add(workQueue);
            }

            foreach (WORK_INSTRUCTION_STATUS wi in wiList)
            {
                int tmpWQIndex_1 = wqList.FindIndex(t => t.WORK_QUEUE == wi.WORK_QUEUE);

                //存在WI不属于任何一个WQ
                if (tmpWQIndex_1 == -1)
                {
                    
                    continue;
                }

                int tmpQcIndex = workQcList.FindIndex(t => t.QC_ID == wqList[tmpWQIndex_1].QC_ID);

                if (tmpQcIndex == -1)
                    continue;

                int tmpWQIndex_2 = workQcList[tmpQcIndex].WorkQueueList.FindIndex(t => t.WORK_QUEUE == wi.WORK_QUEUE);

                //if (tmpWQIndex_2 == -1)
                //    continue;

                WorkInstruction workInstruction = new WorkInstruction(wi);
                workInstruction.QC_ID = workQcList[tmpQcIndex].QC_ID;
                workInstruction.WORK_QUEUE = workQcList[tmpQcIndex].WorkQueueList[tmpWQIndex_2].WORK_QUEUE;
                
                
                //包含所有的WI
                workQcList[tmpQcIndex].WorkQueueList[tmpWQIndex_2].WorkInstructionList.Add(workInstruction);

            }

            if (wqList.Count > 0 && this.schduleStartTime.CompareTo(new DateTime(1,1,1,0,0,0)) == 0)
                this.schduleStartTime = wqList[0].START_TIME;
            
        }

        /// <summary>
        /// JobList与WorkInstructions交集，发箱作业(假定TOS传的WQ，不存在）
        /// </summary> 
        private void Working()
        {
            //取所有未完成的jobList中的任务箱
            List<STS_Task> stsWorkingList = stsTaskList.Where(t => !Helper.IsTaskComplete(t.TaskState)).ToList();

            //List<STS_Task> stsCompleteList = stsTaskList.Where(t => Helper.IsTaskComplete(t.TaskState)).ToList();
            
            //对于各QC所有装船的wq，在WIList与jobList中取两者交集
            foreach (WorkQc qc in workQcList)
            {
                //将QC中的Wq列表按时间排序(同一QC的wq的计划时间段是不应该相交的）
                qc.WorkQueueList.Sort((t1, t2) => t1.StartTime.CompareTo(t2.StartTime));

                //修正调度决策时间段起始时间 //this.schduleStartTime == null || (this.schduleStartTime != null && 
                if (DateTime.Compare(schduleStartTime, qc.WorkQueueList[0].StartTime)> 0)
                {
                    this.schduleStartTime = qc.WorkQueueList[0].StartTime;
                    
                }

                bool isCurrentWQStart = false;

                foreach (WorkQueue wq in qc.WorkQueueList)
                {


                    if (wq.Load)
                    {
                        //取出所有未完成joblist中的装船任务
                        List<STS_Task> loadStsList = stsWorkingList.Where(t => Helper.GetEnum(t.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.LOAD).ToList();

                        foreach (WorkInstruction wi in wq.WorkInstructionList)
                        {
                            foreach (STS_Task sts in loadStsList)
                            {

                                if (wi.WI.JOB_ID == sts.Task.JOB_ID)
                                {
                                    //Wi装船箱不在箱区中？
                                    Block_Container blockContainer = blockContainerList.Find(t => t.CONTAINER_ID == wi.WI.CONTAINER_ID);
                                    if (blockContainer != null)
                                    {
                                        wi.BLOCK_NO = blockContainer.BLOCK_NO;
                                    }

                                    //对于装船，Working为true说明，中控已发箱，可能已开始作业，也可能还未决策
                                    wi.Working = true;

                                    //判断该WI是否是双箱作业
                                    if (wi.WI.LIFT_REFERENCE != "")
                                    {
                                        //双箱后箱
                                        if (!wi.WI.LIFT_REFERENCE.Substring(5).Equals(wi.CONTAINER_ID))
                                        {
                                            int tmpIndex = wq.WorkInstructionList.FindIndex(t => t.CONTAINER_ID == wi.WI.LIFT_REFERENCE.Substring(5));

                                            if (tmpIndex != -1)
                                            {
                                                wi.TwinWorkInstruction = wq.WorkInstructionList[tmpIndex];
                                                //wq.WorkInstructionList[tmpIndex].TwinWorkInstruction = wi;
                                            }
                                            wq.TwinWINum++;
                                        }
                                    }

                                    wq.ToDecideWorkInstructionList.Add(wi);
                                    wq.Enable = true;

                                    isCurrentWQStart = true;

                                    if (qc.SourceWorkQueueList.FindIndex(t => t.WORK_QUEUE == wq.WORK_QUEUE) == -1)
                                        qc.SourceWorkQueueList.Add(wq);
                                    break;
                                }
                            }
                        }

                        if (wq.ToDecideWorkInstructionList.Count == 0 && isCurrentWQStart == true)
                            break;
                    }
                    else
                    {
                        List<STS_Task> discStsList = stsWorkingList.Where(t => Helper.GetEnum(t.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC
                            && String.IsNullOrWhiteSpace(t.Task.CONTAINER_ID.Trim('0'))).ToList();

                        foreach (WorkInstruction wi in wq.WorkInstructionList)
                        {
                            //nullDiscStsList
                            foreach (STS_Task sts in discStsList)
                            {
                                if (wi.WI.JOB_ID == sts.Task.JOB_ID)
                                {
                                    //对于卸船，属性Working为true，则卸船箱已开始作业
                                    wi.Working = true;
                                    wq.Enable = true;
                                    break;
                                }
                            }

                            
                        }
                        //if (wq.Enable)
                        qc.SourceWorkQueueList.Add(wq);
                    }

                    //计算WorkQueue平均计划效率
                    if (wq.WorkInstructionList.Count > 0)
                        wq.PlanTime = new TimeSpan((wq.EndTime - wq.StartTime).Ticks / (wq.WorkInstructionList.Count - wq.TwinWINum));

                    #region 昝略
                    //else
                    //{
                    //    //卸船只需截取任务数量？
                    //    List<STS_Task> nullDiscStsList = stsWorkingList.Where(t => Helper.GetEnum(t.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC
                    //        && String.IsNullOrWhiteSpace(t.Task.CONTAINER_ID.Trim('0'))).ToList();
                        
                    //    foreach (WorkInstruction wi in wq.WorkInstructionList)
                    //    {
                    //        //nullDiscStsList
                    //        foreach (STS_Task sts in nullDiscStsList)
                    //        {
                    //            if (wi.WI.JOB_ID == sts.Task.JOB_ID)
                    //            {
                    //                Block_Container blockContainer = blockContainerList.Find(t => t.CONTAINER_ID == wi.WI.CONTAINER_ID);
                    //                if (blockContainer != null)
                    //                {
                    //                    wi.BLOCK_NO = blockContainer.BLOCK_NO;
                    //                }
                    //                wi.Working = true;
                    //                wq.Enable = true;
                    //                break;
                    //            }
                    //        }
                    //    }
                        
                    //    List<STS_Task> createDiscStsList = stsWorkingList.Where(t => Helper.GetEnum(t.Task.JOB_TYPE, JobType.UNKNOWN) == JobType.DISC
                    //        && !String.IsNullOrWhiteSpace(t.Task.CONTAINER_ID.Trim('0'))).ToList();
                        
                    //    //createDiscStsList
                    //    foreach (STS_Task sts in createDiscStsList)
                    //    {
                    //        WorkInstruction wi = new WorkInstruction(null);
                    //        wi.QC_ID = qc.QC_ID;
                    //        wi.WORK_QUEUE = wq.WQ.WORK_QUEUE;
                    //        wi.JOB_ID = sts.Task.JOB_ID;
                    //        wi.Working = true;
                    //        wq.Enable = true;
                    //        wq.ToDecideWorkInstructionList.Add(wi);
                    //    }
                    //}
                    #endregion
                }

                if (qc.SourceWorkQueueList != null && qc.SourceWorkQueueList.Count > 0 && qc.SourceWorkQueueList[qc.SourceWorkQueueList.Count - 1].Load && qc.SourceWorkQueueList[qc.SourceWorkQueueList.Count - 1].ToDecideWorkInstructionList.Count == 0)
                    continue;
            }
            
            //对于卸船WQ，由于只有当ocr识别后，才会在jobList中生成，故对于卸船做取交集处理，
            //则所考虑的卸船任务是滞后的，且会引起AGV子调度中对卸船箱任务的响应滞后。
            //当前处理方法为
            //1.卸船只认数量,不区分WI,并最终决策出相应卸船目的箱区;
            //2.各QC的SourceWorkQueueList中非最末wq中的卸船wq中的所有卸船任务作业量将被加入决策;
            //3.计算各QC的最末装船wq的计划任务截止时刻,选取其中最晚的时刻，用来截取卸船wq的任务量;
            //4.对于所有QC都是卸的情形，模型将用固定的决策截止时刻来截取卸船任务数

            DateTime largestTimeLength = this.schduleStartTime;

            foreach (WorkQc qc in workQcList)
            {
                if (qc.SourceWorkQueueList == null)
                    continue;

                for (int i = qc.SourceWorkQueueList.Count - 1; i >= 0; i--)
                {
                    if (qc.SourceWorkQueueList[i].Load)
                    {
                        DateTime tmpEndTime = qc.SourceWorkQueueList[i].StartTime.AddMinutes(qc.SourceWorkQueueList[i].PlanTime.TotalMinutes * (double)qc.SourceWorkQueueList[i].ToDecideWorkInstructionList.Count);
                        if (DateTime.Compare(largestTimeLength, tmpEndTime) < 0)
                            largestTimeLength = tmpEndTime;
                    }
                }
            }

            //若各QC各wq为全卸,则暂取15分钟
            if (DateTime.Compare(this.schduleStartTime, largestTimeLength) >= 0)
            {
                largestTimeLength = this.schduleStartTime.AddMinutes(15);
            }
            
            foreach (WorkQc qc in workQcList)
            {
                for (int i = qc.SourceWorkQueueList.Count - 1; i >= 0; i--)
                {
                    if (!qc.SourceWorkQueueList[i].Load)
                    {
                        if (DateTime.Compare(largestTimeLength, qc.SourceWorkQueueList[i].StartTime) <= 0)
                        {
                            qc.SourceWorkQueueList.RemoveAt(i);
                        }
                        else
                        {
                            qc.SourceWorkQueueList[i].Enable = true;
                            if (DateTime.Compare(largestTimeLength, qc.SourceWorkQueueList[i].EndTime) <= 0)
                            {

                                qc.SourceWorkQueueList[i].ToDecideDischargeNum = (int)((largestTimeLength - qc.SourceWorkQueueList[i].StartTime).TotalMinutes / qc.SourceWorkQueueList[i].PlanTime.TotalMinutes);
                            }
                            else
                                qc.SourceWorkQueueList[i].ToDecideDischargeNum = qc.SourceWorkQueueList[i].WorkInstructionList.Count;

                            for (int j = 0; j< qc.SourceWorkQueueList[i].WorkInstructionList.Count;j++ )
                            {
                                if (j < qc.SourceWorkQueueList[i].ToDecideDischargeNum)
                                    qc.SourceWorkQueueList[i].ToDecideWorkInstructionList.Add(qc.SourceWorkQueueList[i].WorkInstructionList[j]);

                                qc.SourceWorkQueueList[i].WorkInstructionList[j].IndexOfWQIfDISC = j;
                            }
                        }
                    }

                }

            }
        }

        /// <summary>
        /// 判断任务是否是双箱作业，且双箱任务在给定任务列表中
        /// </summary>
        /// <param name="dbData_Schedule"></param>
        /// <param name="excutingWIList"></param>
        /// <param name="jobID"></param>
        /// <returns></returns>
        private int IsTwinJob(List<WORK_INSTRUCTION_STATUS> excutingWIList, String jobID)
        {
            
            if (stsTaskList.Exists(job => job.Task.JOB_ID == jobID && !String.IsNullOrWhiteSpace(job.Task.JOB_LINK) &&
                excutingWIList.Exists(wi => wi.JOB_ID == job.Task.JOB_LINK)))
                return 1;

            return 0;
        }

        //private int IsTwinWorkInstruction(List<WorkInstruction> wiList,)



        /// <summary>
        /// 正在执行的任务状态
        /// </summary>
        private void Excuting()
        {
            //STS
            List<string> stsList = stsTaskList.Where(t => Helper.IsTaskWorking(t.TaskState)).Select(t => t.Task.JOB_ID).ToList();
            //ASC
            List<string> ascList = ascOrderList.Where(order => ascCommandList.Exists(cmd => (order.ORDER_ID == cmd.ORDER_ID) && !cmd.IsComplete()) ||
                    !ascCommandList.Exists(cmd => order.ORDER_ID == cmd.ORDER_ID)).Select(order => order.JOB_ID).ToList();
            //AGV
            List<string> agvList = agvOrderList.Where(order => agvCommandList.Exists(cmd => (order.ORDER_ID == cmd.ORDER_ID) && !cmd.IsComplete()) ||
                    !agvCommandList.Exists(cmd => order.ORDER_ID == cmd.ORDER_ID)).Select(order => order.JOB_ID).ToList();
            //求并集
            //List<string> excutingList = stsList.Union(ascList).ToList().Union(agvList).ToList();
            
            //设置状态
            foreach (WorkQc qc in workQcList)
            {
                foreach (WorkQueue wq in qc.SourceWorkQueueList)
                {
                    if (wq.Enable)
                    {
                        //foreach (WorkInstruction wi in wq.ToDecideWorkInstructionList)
                        foreach (WorkInstruction wi in wq.WorkInstructionList)
                        {
                            if (wi.Working)
                            {
                                
                                foreach (string sts in stsList)
                                {
                                    if (wi.JOB_ID == sts)
                                    {
                                        wi.Excute = 1;
                                        wi.ExcuteType = 0;
                                        break;
                                    }
                                }
                                
                                foreach (string asc in ascList)
                                {
                                    if (wi.JOB_ID == asc)
                                    {
                                        wi.Excute = 1;
                                        wi.ExcuteType = 1;
                                        break;
                                    }
                                }
                                foreach (string agv in agvList)
                                {
                                    if (wi.JOB_ID == agv)
                                    {
                                        wi.Excute = 1;
                                        wi.ExcuteType = 2;
                                        break;
                                    }
                                }

                                //估计正在执行WI的freeTime（是否应参考wi间的邻接关系来计算free时刻）
                                EstimateWorkingWITime(wi, wq.Load);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 未执行的状态
        /// </summary>
        private void UnExcute()
        {
            //STS
            List<string> stsList = stsTaskList.Where(t => Helper.IsTaskInitial(t.TaskState)).Select(t => t.Task.JOB_ID).ToList();
            //ASC
            List<string> ascList = ascTaskList.Where(task => !Helper.IsTaskComplete(task.TaskState) && !ascOrderList.Exists(order => order.JOB_ID == task.Task.JOB_ID))
                    .Select(task => task.Task.JOB_ID).ToList();
            //AGV
            List<string> agvList = agvTaskList.Where(task => !Helper.IsTaskComplete(task.TaskState) && !agvOrderList.Exists(order => order.JOB_ID == task.Task.JOB_ID))
                    .Select(task => task.Task.JOB_ID).ToList();
            //求交集
            List<string> noExcuteList = stsList.Intersect(ascList).ToList().Intersect(agvList).ToList();
            //设置状态
            foreach (WorkQc qc in workQcList)
            {
                foreach (WorkQueue wq in qc.SourceWorkQueueList)
                {
                    if (wq.Enable)
                    {
                        //非todecideWIList
                        foreach (WorkInstruction wi in wq.WorkInstructionList)
                        {
                            if (wi.Working)
                            {
                                foreach (string noExcute in noExcuteList)
                                {
                                    if (wi.JOB_ID == noExcute)
                                    {
                                        wi.Excute = 0;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 重置
        /// </summary>
        private void Reset(ref PartialOrderGraph partialOrderTable)
        {
            ////可用计算
            //foreach (WorkQc qc in workQcList)
            //{
            //    foreach (WorkQueue wq in qc.SourceWorkQueueList)
            //    {
            //        if (wq.Enable)
            //        {
            //            qc.WorkQueueList.Add(wq);
            //            foreach (WorkInstruction wi in wq.ToDecideWorkInstructionList)
            //            {
            //                if (wi.Working)
            //                {
            //                    wq.WorkInstructionList.Add(wi);
            //                }
            //            }
            //        }
            //    }
            //}
            //阈值计算
            partialOrderTable.m_WIList = new List<WORK_INSTRUCTION_STATUS>();

            foreach (WorkQc qc in workQcList)
            {
                
                foreach (WorkQueue wq in qc.SourceWorkQueueList)
                {
                    //生成WorkInstruction索引
                    for (int i = 0; i < wq.ToDecideWorkInstructionList.Count; i++)
                    {
                        wq.ToDecideWorkInstructionList[i].IndexOfToDecideWI = i;

                        if (!wq.Load)
                        {
                            int tmpIndex = this.wiList.FindIndex(t => t.CONTAINER_ID == wq.ToDecideWorkInstructionList[i].CONTAINER_ID);
                            if (tmpIndex == -1)
                                continue;

                            partialOrderTable.m_WIList.Add(this.wiList[tmpIndex]);
                        }
                        
                    }
                    
                    //生成WorkBlock(暂未加入Mask）
                    foreach (WorkInstruction wi in wq.ToDecideWorkInstructionList)
                    {
                        WorkBlock workBlock = workBlockList.Find(t => t.BLOCK_NO == wi.BLOCK_NO);
                        if (workBlock == null)
                        {
                            workBlock = new WorkBlock(wi.BLOCK_NO);
                            workBlockList.Add(workBlock);
                        }
                    }
                    
                    //计算Qc正在作业的数量
                    foreach (WorkInstruction wi in wq.WorkInstructionList)
                    {
                        if (wi.Excute == 1)
                        {
                            qc.CurrentNumber++;

                            //计算Block正在作业的数量
                            WorkBlock workBlock = workBlockList.Find(t => t.BLOCK_NO == wi.BLOCK_NO);
                            if (workBlock != null && !wq.Load)
                            {
                                workBlock.CurrentNumber++;
                            }
                        }

                        if (wq.Load)
                        {
                            int tmpIndex = this.wiList.FindIndex(t => t.CONTAINER_ID == wi.CONTAINER_ID);
                            
                            if (tmpIndex == -1)
                                continue;

                            partialOrderTable.m_WIList.Add(this.wiList[tmpIndex]);
                        }
                        
                    }

                    
                    //初始化WI中的前置与后置任务集以及该WQ中所有任务的偏序图
                    if (wq.Load)
                    {
                        wq.IniPreAndProIndexList();
                        wq.IniPartialGraphOfWQ();
                    }
                }
            }
        }

        /// <summary>
        /// 正在作业任务的FreeTime时间
        /// </summary>
        private void FreeTime()
        {
            foreach (WorkQc qc in workQcList)
            {
                foreach (WorkQueue wq in qc.WorkQueueList)
                {
                    //todo 应参考WI间的偏序关系，仅遍历所有source任务集即可
                    foreach (WorkInstruction wi in wq.ToDecideWorkInstructionList)
                    {
                        if (wi.Excute == 1)
                        {
                            if (wq.Load)
                            {
                                //计算WorkQc的FreeTime时间
                                if (qc.FreeTime.CompareTo(wi.FreeTime) < 0)
                                {
                                    qc.FreeTime = wi.FreeTime;
                                }
                            }
                            else
                            {
                                //计算WorkBlock的FreeTime时间
                                WorkBlock workBlock = workBlockList.Find(t => t.BLOCK_NO == wi.BLOCK_NO);
                                if (workBlock != null && workBlock.FreeTime < wi.FreeTime)
                                {
                                    workBlock.FreeTime = wi.FreeTime;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 交换箱
        /// </summary>
        private void Mask()
        {
            foreach (WorkQc qc in workQcList)
            {
                foreach (WorkQueue wq in qc.SourceWorkQueueList)
                {
                                        
                    foreach (WorkInstruction wi in wq.WorkInstructionList)
                    {
                        if (wq.Load && wi.Excute == 0)
                        {
                            
                            WorkBlock workBlock = workBlockList.Find(t => t.BLOCK_NO == wi.BLOCK_NO);
                            
                            if (workBlock != null)
                            {
                                if (wi.MASK_ID == "")
                                {
                                    //无Mask标志的，暂已该箱箱号作为MaskID
                                    wi.MASK_ID = wi.CONTAINER_ID;
                                }

                                if (wi.MASK_ID == "" || (wi.MASK_ID != "" && workMaskList.FindIndex(t => t.MASK_ID == wi.MASK_ID) == -1))
                                {
                                    //生成WorkMask
                                    WorkMask workMask = new WorkMask(wi.MASK_ID);
                                    workMaskList.Add(workMask);

                                    //生成WorkMaskBlock
                                    WorkMaskAtBlock workMaskBlock = workMask.WorkMaskBlockList.Find(t => t.MaskWorkBlock.BLOCK_NO == workBlock.BLOCK_NO);
                                    if (workMaskBlock == null)
                                    {
                                        workMaskBlock = new WorkMaskAtBlock(workBlock);
                                        workMask.WorkMaskBlockList.Add(workMaskBlock);
                                    }
                                    workMaskBlock.WorkInstructionList.Add(wi);
                                }
                                
                            }
                        }
                    }

                }
            }

            foreach (WorkMask ms in this.workMaskList)
            {
                foreach (WorkMaskAtBlock a in ms.WorkMaskBlockList)
                {
                    a.CurrentLoadNum = a.WorkInstructionList.Count;
                }
            }
        }

        /// <summary>
        /// 时间估算(ToDo)
        /// </summary>
        /// <param name="type"></param>
        /// <param name="qc"></param>
        /// <param name="block"></param>
        /// <returns></returns>
        private TimeSpan TimeEstimate(int type, WorkQc qc, WorkBlock block)
        {
            TimeSpan ts = new TimeSpan(0, 0, 0, 0, 0);
            if (type == 1)
            {
                //ASC移动时长（装船）
                ts = new TimeSpan(0, 0, 1, 0, 0);
            }
            else if (type == 2)
            {
                //AGV到达Qc时长
                ts = new TimeSpan(0, 0, 5, 0, 0);
            }
            else if (type == 3)
            {
                //AGV到达时长Block平均时长（固定）
                ts = new TimeSpan(0, 0, 5, 0, 0);
            }
            else if (type == 4)
            {
                //AGV到达Block时长
                ts = new TimeSpan(0, 0, 4, 0, 0);
            }
            else if (type == 5)
            {
                //ASC移动时长（卸船）
                ts = new TimeSpan(0, 0, 2, 0, 0);
            }
            return ts;
        }

        /// <summary>
        /// 估计已开始执行WI的完成时长(装船指到目的QC，卸船指到目的箱区）
        /// 赋值Wi.freeTime(ToDo)
        /// </summary>
        /// <param name="wi"></param>
        /// <returns></returns>
        private void EstimateWorkingWITime(WorkInstruction wi,bool isLoad)
        {
            if (isLoad)
            {
                if (wi.ExcuteType == 0)
                {
                    wi.FreeTime = TimeSpan.Zero;
                }
                else
                    if (wi.ExcuteType == 1)
                    {
                        //估算该箱对应AGV从当前位置至目的QC下AGV运行的时长
                        wi.FreeTime = new TimeSpan(0, 0, 5, 0, 0);
                    }
                    else
                        if (wi.ExcuteType == 2)
                        {
                            //估算对应ASC从当前位置至箱区海侧，以及AGV从箱区海侧到目的QC的运行时长
                            wi.FreeTime = new TimeSpan(0, 0, 8, 0, 0);
                        }
                
            }
            else
            {
                if (wi.ExcuteType == 0)
                {
                    //若已指定箱区，则估算从QC移动到目的箱区海侧，再移至箱区中间位置所需时长
                    wi.FreeTime = new TimeSpan(0, 0, 8, 0, 0);
                }
                else
                    if (wi.ExcuteType == 1)
                    {
                        //估算该箱对应AGV从当前位置至目的箱区海侧，以及再移至箱区中间所需时长
                        new TimeSpan(0, 0, 5, 0, 0);
                    }
                    else
                        if (wi.ExcuteType == 2)
                        {
                            //估算对应ASC从当前位置至箱区目的箱位运行时长
                            new TimeSpan(0, 0, 5, 0, 0);
                        }

            }

        }


        
        
        
        /// <summary>
        /// 在贪婪算法中比较两个不同的QC
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private int CompareTwoQC(WorkQc a, WorkQc b)
        {
            TimeSpan qcANextPlanTime = a.FreeTime.Add(a.WorkQueueList[a.CurrentWorkQueueIndex].PlanTime);
            TimeSpan qcBNextPlanTime = b.FreeTime.Add(b.WorkQueueList[b.CurrentWorkQueueIndex].PlanTime);
            if (a.Enable == true && b.Enable == false)
                return -1;
            else
                if (a.Enable == false && b.Enable == true)
                    return 1;
                else
                    if (a.Enable == false && b.Enable == false)
                        return 0;
                    else
                    {
                        if (qcANextPlanTime < qcBNextPlanTime)
                            return -1;
                        else
                            if (qcANextPlanTime > qcBNextPlanTime)
                                return 1;
                            else
                                return 0;
                    }
            
        }

        /// <summary>
        /// 返回WI列表中可出箱点不是给定Block的所有WI数量
        /// </summary>
        /// <param name="wiIndexList"></param>
        /// <param name="block"></param>
        /// <returns></returns>
        private int GetWINumWithOutBlock(WorkQueue wq,List<int> wiIndexList, WorkBlock block)
        {
            int retInt = 0;
            foreach (int index in wiIndexList)
            {
                if (!wq.WorkInstructionList[index].BLOCK_NO.Equals(block.BLOCK_NO))
                    retInt = retInt + 1;
            }

            return retInt;
        }

        /// <summary>
        /// 贪婪算法
        /// </summary>
        private void Greedy()
        {
            while (workQcList.Count > 0) 
            {
                //////////////////////////////////////////////////////////////////////////
                BestChoice bestChoice = new BestChoice();
                //////////////////////////////////////////////////////////////////////////
                //选择当前WorkQc（注：这里现在只根据时间，还可以根据其它要素综合来考虑选择Qc的顺序）
                //可能存在评价指标相同的多个QC
                workQcList.Sort(CompareTwoQC);
                WorkQc qc = null;
                foreach(WorkQc workQc in workQcList)
                {
                    if (workQc.Enable)
                    {
                        qc = workQc;
                        break;
                    }
                }
                //跳出
                if (qc == null)
                {
                    break;
                }
                //////////////////////////////////////////////////////////////////////////
                //选择当前WorkQueue
                WorkQueue wq = null;
                
                foreach (WorkQueue workQueue in qc.SourceWorkQueueList)
                {
                    bool wqIsAvailable = false;
                    if (workQueue.Load)
                    {//当前WorkQueue索引选择
                        List<int> indexList = bestChoiceList.Where(t => t.BestQc.QC_ID == qc.QC_ID && t.BestQueue.WORK_QUEUE == workQueue.WORK_QUEUE).Select(s => s.BestWorkInstruction.IndexOfToDecideWI).ToList();
                        //找出可作业任务
                        workQueue.GetNextToDecideWorkInstructionList(indexList);

                        if (workQueue.CurrentSourceWorkInstructionList.Count > 0)
                        {
                            wqIsAvailable = true;
                        }
                    }
                    else
                    {
                        if (workQueue.ToDecideDischargeNum > 0)
                        {
                            wqIsAvailable = true;
                        }
                    }

                    if (wqIsAvailable == true)
                    {
                        wq = workQueue;
                        qc.CurrentWorkQueueIndex = wq.IndexInQC;
                        break;
                    }
                }
                //进入下一个QC
                if (wq == null)
                {
                    qc.Enable = false;
                    continue;
                }
                //////////////////////////////////////////////////////////////////////////
                //贪婪算法（按装船和卸船分开选择）
                if (wq.Load)
                {

                    TimeSpan minTimeLength = TimeSpan.MaxValue;//最小时长
                    TimeSpan goalTime = qc.FreeTime + wq.PlanTime; //qc.FreeTime+wq.PlanTime
                    
                    foreach (WorkInstruction wi in wq.CurrentSourceWorkInstructionList)
                    {
                        WorkMask mask = workMaskList.Find(t => t.MASK_ID == wi.MASK_ID);
                        if (mask == null)
                            continue;
                        
                        foreach (WorkMaskAtBlock maskBlock in mask.WorkMaskBlockList)
                        {
                            if (maskBlock.CurrentLoadNum <= 0)
                                continue;

                            WorkBlock block = maskBlock.MaskWorkBlock;
                            //可能存在出箱箱区一致但对同一WQ中后续影响不同的WI
                            if (bestChoice.Flag &&  block.BLOCK_NO.Equals(bestChoice.BestBlock.BLOCK_NO))
                            {
                                //比较这两个WI的出箱对同一wq中后续WI出箱的影响
                                //考虑后续出箱WI的出箱箱量以及涉及的非本箱区的出箱箱量
                                if (wi.ProWIIndexInWQList.Count > bestChoice.BestWorkInstruction.ProWIIndexInWQList.Count)
                                    bestChoice.BestWorkInstruction = wi;
                                else
                                {
                                    if (wi.ProWIIndexInWQList.Count == bestChoice.BestWorkInstruction.ProWIIndexInWQList.Count)
                                        if (GetWINumWithOutBlock(wq, wi.ProWIIndexInWQList, block) > GetWINumWithOutBlock(wq, bestChoice.BestWorkInstruction.ProWIIndexInWQList, block))
                                            bestChoice.BestWorkInstruction = wi;
                                }

                            }
                            else
                            {
                                TimeSpan currentTime = block.FreeTime + TimeEstimate(1, qc, block) + TimeEstimate(2, qc, block);//block.FreeTime+ASC移动时长（装船）+AGV到达Qc时长
                                TimeSpan diffTimeLength;

                                if (currentTime > goalTime)
                                    diffTimeLength = currentTime - goalTime;
                                else
                                    diffTimeLength = goalTime - currentTime;
                                if (diffTimeLength < minTimeLength)
                                {
                                    minTimeLength = diffTimeLength;
                                    bestChoice.Flag = true;
                                    bestChoice.QcTime = currentTime;
                                    bestChoice.BlockTime = TimeEstimate(1, qc, block); //ASC移动时长
                                    bestChoice.BestQc = qc;
                                    bestChoice.BestBlock = block;
                                    bestChoice.BestQueue = wq;
                                    bestChoice.BestWorkInstruction = wi;
                                    bestChoice.BestMask = maskBlock;

                                }
                            }
                        }
                    }
                }//Load
                else
                {
                    if (wq.ToDecideDischargeNum <= 0)
                        continue;

                    TimeSpan goalTime = qc.FreeTime + wq.PlanTime + TimeEstimate(3, qc, null); //qc.FreeTime+wq.PlanTime+AGV到达时长Block平均时长（固定）
                    TimeSpan minTimeLength = TimeSpan.MaxValue;//最大时长

                    foreach (WorkBlock block in workBlockList)
                    {
                        TimeSpan currentTime = qc.FreeTime + wq.PlanTime + TimeEstimate(4, qc, block); //qc.FreeTime+wq.PlanTime+AGV到达Block时长
                        currentTime = (currentTime > block.FreeTime) ? currentTime : block.FreeTime;
                        
                        TimeSpan diffTimeLength;

                        if (currentTime > goalTime)
                            diffTimeLength = currentTime - goalTime;
                        else
                            diffTimeLength = goalTime - currentTime;
                        
                        if (diffTimeLength < minTimeLength)
                        {
                            bestChoice.Flag = true;
                            bestChoice.QcTime = qc.FreeTime + wq.PlanTime;
                            bestChoice.BlockTime = currentTime + TimeEstimate(5, qc, block); //最大时长+ASC移动时长（卸船）
                            bestChoice.BestQc = qc;
                            bestChoice.BestBlock = block;
                            bestChoice.BestQueue = wq;
                            bestChoice.BestWorkInstruction = wq.ToDecideWorkInstructionList[wq.ToDecideWorkInstructionList.Count - wq.ToDecideDischargeNum];
                            minTimeLength = diffTimeLength;
                        }

                    }

                }//UnLoad
                //////////////////////////////////////////////////////////////////////////
                if (bestChoice.Flag)
                {
                    WorkQc workQc = workQcList.Find(t => t.QC_ID == bestChoice.BestQc.QC_ID);
                    WorkBlock workBlock = workBlockList.Find(t => t.BLOCK_NO == bestChoice.BestBlock.BLOCK_NO);
                    
                    if (workQc != null && workBlock != null)
                    {
                        //Qc数量限制
                        int qcNumber = bestChoiceList.Where(t => t.BestQc.QC_ID == workQc.QC_ID).Count();
                        //Block数据限制
                        int blockNumber = bestChoiceList.Where(t => t.BestBlock.BLOCK_NO == workBlock.BLOCK_NO).Count();
                        
                        if (qcNumber <= workQc.MaxNumber - workQc.CurrentNumber && blockNumber <= workBlock.MaxNumber - qc.CurrentNumber)
                        {
                            qc.Enable = true;
                            workQc.FreeTime += bestChoice.QcTime;
                            workBlock.FreeTime += bestChoice.BlockTime;
                            
                            bestChoiceList.Add(bestChoice);

                            if (bestChoice.BestQueue.Load)
                            {
                                if (workBlock.CurrentNumber > 0)
                                    workBlock.CurrentNumber--;
                                bestChoice.BestMask.CurrentLoadNum--;
                            }
                            else
                                bestChoice.BestQueue.ToDecideDischargeNum--;
                        }
                        else
                        {
                            qc.Enable = false;
                        }
                    }
                    else
                    {
                        qc.Enable = false;
                    }
                }
                else
                {
                    qc.Enable = false;
                }
                //////////////////////////////////////////////////////////////////////////
             }
        }

        /// <summary>
        /// 输出当前所有任务涉及的偏序图
        /// </summary>
        /// <param name="partialOrderTable"></param>
        private void Output(ref PartialOrderGraph partialOrderTable)
        {

            partialOrderTable.m_DecisionTable = new int[partialOrderTable.m_WIList.Count, partialOrderTable.m_WIList.Count];

            //由于某些条件，没有决策完毕所有待决策的WI（maxNumber)
            if (this.bestChoiceList.Count != partialOrderTable.m_WIList.Count)
            {
                
            }

            //
            //对于不同QC的两个任务，按照其执行时刻的
            List<int> indexOfBestChoice = new List<int>();

            foreach (WORK_INSTRUCTION_STATUS a in partialOrderTable.m_WIList)
            {
                //if (a.MOVE_KIND == Move_Kind.LOAD)

                int tmpIndex = bestChoiceList.FindIndex(t => t.BestWorkInstruction.CONTAINER_ID.Equals(a.CONTAINER_ID));
                                
                indexOfBestChoice.Add(tmpIndex);

                if (tmpIndex == -1)
                    continue;

                //出箱箱区与卸箱箱区赋值
                a.T_Load_BlockNO = this.bestChoiceList[tmpIndex].BestBlock.BLOCK_NO;

            }

            int i = 0;
            foreach (WORK_INSTRUCTION_STATUS a in partialOrderTable.m_WIList)
            {
                if (indexOfBestChoice[i] == -1)
                    continue;
                for (int j = i + 1; j < partialOrderTable.m_WIList.Count; j++)
                {
                    if (indexOfBestChoice[j] == -1)
                        continue;

                    //若对于同一QC的两个任务，按照贪婪算法给出的次序，设立偏序关系
                    int iPriorityj = 0;

                    if (bestChoiceList[indexOfBestChoice[i]].BestQc.QC_ID.Equals(bestChoiceList[indexOfBestChoice[i]].BestQc.QC_ID))
                    {
                        if (indexOfBestChoice[i] > indexOfBestChoice[j])
                        {
                            iPriorityj = -1;
                        }
                        else
                            iPriorityj = -1;

                    }
                    else
                    {
                        //对于不同QC的WI，若处于同一目的箱区，则根据其任务时刻差值超过阈值，则定义其偏序关系
                        if(bestChoiceList[indexOfBestChoice[i]].BestBlock.BLOCK_NO.Equals(bestChoiceList[indexOfBestChoice[j]].BestBlock.BLOCK_NO))
                            if(bestChoiceList[indexOfBestChoice[i]].BestWorkInstruction.WI.MOVE_KIND==Move_Kind.LOAD && bestChoiceList[indexOfBestChoice[j]].BestWorkInstruction.WI.MOVE_KIND==Move_Kind.LOAD)
                                if (Math.Abs(bestChoiceList[indexOfBestChoice[i]].BlockTime.Subtract(bestChoiceList[indexOfBestChoice[j]].BlockTime).TotalMinutes) >= this.threshMinForSameBlockWI)
                                {
                                    if (bestChoiceList[indexOfBestChoice[i]].BlockTime.CompareTo(bestChoiceList[indexOfBestChoice[j]].BlockTime) < 0)
                                        iPriorityj = -1;
                                    else
                                        iPriorityj = 1;
                                }
                    }

                    partialOrderTable.m_DecisionTable[i, j] = iPriorityj;
                    partialOrderTable.m_DecisionTable[j, i] = -1 * iPriorityj;
                }
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
        

    }

    

    /// <summary>
    /// BestChoice
    /// </summary>
    public class BestChoice
    {
        public BestChoice()
        {
            Flag = false;
            QcTime = new TimeSpan(0, 0, 0, 0, 0);
            BlockTime = new TimeSpan(0, 0, 0, 0, 0);
            BestQc = null;
            BestBlock = null;
            BestQueue = null;
            BestWorkInstruction = null;
            BestMask = null;
        }
        public bool Flag { get; set; }
        public TimeSpan QcTime { get; set; }
        public TimeSpan BlockTime { get; set; }
        public WorkQc BestQc { get; set; }
        public WorkBlock BestBlock { get; set; }
        public WorkQueue BestQueue { get; set; }
        public WorkInstruction BestWorkInstruction { get; set; }
        public WorkMaskAtBlock BestMask { get; set; }
    }

    /// <summary>
    /// WorkQc
    /// </summary>
    public class WorkQc
    {
        public WorkQc(string qcid)
        {
            QC_ID = qcid;
            SourceWorkQueueList = new List<WorkQueue>();
            WorkQueueList = new List<WorkQueue>();
            FreeTime = new TimeSpan(0, 0, 0, 0, 0);
            Enable = true;
            MaxNumber = int.MaxValue;
            CurrentNumber = 0;
        }
        public string QC_ID { get; set; }
        /// <summary>
        /// 当前决策中涉及的WQ
        /// </summary>
        public List<WorkQueue> SourceWorkQueueList { get; set; }
        public List<WorkQueue> WorkQueueList { get; set; }
        /// <summary>
        /// QC在贪婪算法中当前待决策的WQ索引
        /// </summary>
        public int CurrentWorkQueueIndex { get; set; }
        public TimeSpan FreeTime { get; set; }
        public bool Enable { get; set; }
        public int MaxNumber { get; set; }
        public int CurrentNumber { get; set; }
    }

    /// <summary>
    /// WorkQueue
    /// </summary>
    public class WorkQueue
    {
        public WorkQueue(STS_WORK_QUEUE_STATUS wq)
        {
            WQ = wq;
            WORK_QUEUE = wq.WORK_QUEUE;
            Load = wq.MOVE_KIND == Move_Kind.LOAD ? true : false;
            StartTime = wq.START_TIME;
            EndTime = wq.END_TIME;
            ToDecideWorkInstructionList = new List<WorkInstruction>();
            WorkInstructionList = new List<WorkInstruction>();
            QC_ID = "";
            PlanTime = new TimeSpan(0, 0, 0, 0, 0);
            Enable = false;
            CurrentSourceWorkInstructionList = new List<WorkInstruction>();
        }
        public STS_WORK_QUEUE_STATUS WQ { get; set; }
        public string WORK_QUEUE { get; set; }
        public bool Load { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<WorkInstruction> ToDecideWorkInstructionList { get; set; }
        public List<WorkInstruction> WorkInstructionList { get; set; }
        public int ToDecideDischargeNum { get; set; }
        public int TwinWINum{get;set;}
        public string QC_ID { get; set; }
        public TimeSpan PlanTime { get; set; }
        public bool Enable { get; set; }
        public List<WorkInstruction> CurrentSourceWorkInstructionList { get; set; }
        public int IndexInQC { get; set; }

        public List<List<int>> WIOrderMatrix;


        private List<WorkInstruction> GetALLSourceWIList(List<WorkInstruction> currentWIList)
        {
            List<WorkInstruction> sourceList = new List<WorkInstruction>();
            foreach (WorkInstruction wi in currentWIList)
            {
                if (currentWIList.Where(t => wi.PrefixList.Contains(t.CONTAINER_ID)).Count() == 0)
                {
                    sourceList.Add(wi);
                }
            }

            return sourceList;
        }

        public void GetNextToDecideWorkInstructionList(List<int> indexList)
        {
            List<WorkInstruction> workInstructionList = WorkInstructionList.Where(t => !indexList.Contains(t.IndexOfToDecideWI) && t.Excute == 0).ToList();
            if (Load)
            {

                CurrentSourceWorkInstructionList = GetALLSourceWIList(workInstructionList);
            }
            else
            {
                CurrentSourceWorkInstructionList = workInstructionList;
            }
        }

        /// <summary>
        /// 初始化各WI的前置任务以及后置任务索引
        /// </summary>
        public void IniPreAndProIndexList()
        {
            int i = 0;
            foreach (WorkInstruction wi in this.WorkInstructionList)
            {
                foreach (string pre in wi.PrefixList)
                {
                    int tmpIndex = this.WorkInstructionList.FindIndex(t => t.CONTAINER_ID.Equals(pre));
                    if (tmpIndex == -1)
                    {
                        //前置箱号有误
                        continue;

                    }
                    wi.PreWIIndexInWQList.Add(tmpIndex);
                    this.WorkInstructionList[tmpIndex].ProWIIndexInWQList.Add(i);

                }

                wi.IndexOfWIList = i;
                i = i + 1;
            }

        }


        /// <summary>
        /// 初始化WQ中各WI间的偏序图
        /// </summary>
        public void IniPartialGraphOfWQ()
        {
            List<WorkInstruction> currentWIList = new List<WorkInstruction>();

            this.WIOrderMatrix = CommonAlgorithm.IniZeroMatrix(this.WorkInstructionList.Count);

            currentWIList = GetALLSourceWIList(this.WorkInstructionList);

            //利用广度搜索初始化邻接矩阵
            while (currentWIList.Count > 0)
            {
                //记录广度搜索下一层的所有顶点
                List<WorkInstruction> nextWIList = new List<WorkInstruction>();

                foreach (WorkInstruction wi in currentWIList)
                {

                    foreach (int proIndex in wi.ProWIIndexInWQList)
                    {
                        int tmpIndex = nextWIList.FindIndex(t => t.IndexOfWIList == proIndex);

                        if (tmpIndex == -1)
                        {
                            nextWIList.Add(this.WorkInstructionList[proIndex]);

                            tmpIndex = nextWIList.Count - 1;
                        }

                        this.WIOrderMatrix[wi.IndexOfWIList][proIndex] = 1;
                        this.WIOrderMatrix[proIndex][wi.IndexOfWIList] = -1;

                        foreach (int preIndex in wi.PreWIIndexInWQList)
                        {
                            if (nextWIList[tmpIndex].PreWIIndexInWQList.FindIndex(t => t == preIndex) == -1)
                            {
                                nextWIList[tmpIndex].PreWIIndexInWQList.Add(preIndex);

                                this.WorkInstructionList[preIndex].ProWIIndexInWQList.Add(nextWIList[tmpIndex].IndexOfWIList);


                            }

                            this.WIOrderMatrix[preIndex][nextWIList[tmpIndex].IndexOfWIList] = 1;

                            this.WIOrderMatrix[nextWIList[tmpIndex].IndexOfWIList][preIndex] = -1;
                        }


                    }

                }

                currentWIList = nextWIList;
            }
        }
        
    }

    /// <summary>
    /// WorkInstruction
    /// </summary>
    public class WorkInstruction
    {
        public WorkInstruction(WORK_INSTRUCTION_STATUS wi)
        {
            WI = wi;
            QC_ID = "";
            WORK_QUEUE = "";
            BLOCK_NO = "";
            CONTAINER_ID = ContainerId(wi);
            JOB_ID = JobId(wi);
            MASK_ID = MaskId(wi);
            PrefixList = Prefix(wi);
            //FreeTime = EstimateFreeTime(wi); 
            Working = false;
            Excute = -1;
            ExcuteType = -1;
            IndexOfToDecideWI = -1;
            IndexOfWIList = -1;
            IndexOfWQIfDISC = -1;
            this.PreWIIndexInWQList = new List<int>();
            this.ProWIIndexInWQList = new List<int>();


        }

        
        public WORK_INSTRUCTION_STATUS WI { get; set; }
        public string QC_ID { get; set; }
        public string WORK_QUEUE { get; set; }
        public string BLOCK_NO { get; set; }
        public string CONTAINER_ID { get; set; }
        public string JOB_ID { get; set; }
        public string MASK_ID { get; set; }
        public bool Working { get; set; }
        public int Excute { get; set; }
        public int ExcuteType { get; set; }
        public int IndexOfToDecideWI { get; set; }
        public int IndexOfWIList { get; set; }
        public int IndexOfWQIfDISC { get; set; }
        public List<string> PrefixList { get; set; }
        public List<int> PreWIIndexInWQList { get; set; }
        public List<int> ProWIIndexInWQList { get; set; }
        
        public WorkInstruction TwinWorkInstruction { get; set; }
        
        public TimeSpan FreeTime { get; set; }
        private string ContainerId(WORK_INSTRUCTION_STATUS wi)
        {
            return wi.CONTAINER_ID != null ? wi.CONTAINER_ID : "";
        }
        private string JobId(WORK_INSTRUCTION_STATUS wi)
        {
            return wi.JOB_ID != null ? wi.JOB_ID : "";
        }
        private string MaskId(WORK_INSTRUCTION_STATUS wi)
        {
            return wi.CONTAINER_STOW_FACTOR != null ? wi.CONTAINER_STOW_FACTOR : "";
        }
        private List<string> Prefix(WORK_INSTRUCTION_STATUS wi)
        {
            List<String> seqList = new List<String>();
            seqList.AddRange(wi.LOGICAL_PREDECESSOR != null ? wi.LOGICAL_PREDECESSOR.Split(';') : "".Split(';'));
            seqList.AddRange(wi.PHYSICAL_PREDECESSOR != null ? wi.PHYSICAL_PREDECESSOR.Split(';') : "".Split(';'));
            return seqList.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
        }
        private TimeSpan EstimateFreeTime(WORK_INSTRUCTION_STATUS wi)
        {
            //根据ExcuteType和装卸状态来估算时间
            //sts==0  asc==1   agv==2
            return new TimeSpan(0, 0, 5, 0, 0);
        }
    }

    /// <summary>
    /// WorkBlock
    /// </summary>
    public class WorkBlock
    {
        public WorkBlock(string blockno)
        {
            BLOCK_NO = blockno;
            MaxNumber = int.MaxValue;
            CurrentNumber = 0;
            FreeTime = new TimeSpan(0, 0, 0, 0, 0);
        }
        public string BLOCK_NO { get; set; }
        public TimeSpan FreeTime { get; set; }
        public int MaxNumber { get; set; }
        public int CurrentNumber { get; set; }
    }

    /// <summary>
    /// WorkMask
    /// </summary>
    public class WorkMask
    {
        public WorkMask(string MaskId)
        {
            MASK_ID = MaskId;
            WorkMaskBlockList = new List<WorkMaskAtBlock>();
        }
        public string MASK_ID { get; set; }
        public List<WorkMaskAtBlock> WorkMaskBlockList { get; set; }
    }

    /// <summary>
    /// WorkMaskBlock
    /// </summary>
    public class WorkMaskAtBlock
    {
        public WorkMaskAtBlock(WorkBlock workBlock)
        {
            MaskWorkBlock = workBlock;
            WorkInstructionList = new List<WorkInstruction>();
        }
        public WorkBlock MaskWorkBlock { get; set; }
        public List<WorkInstruction> WorkInstructionList { get; set; }
        public int CurrentLoadNum { get; set; }
    }

}
