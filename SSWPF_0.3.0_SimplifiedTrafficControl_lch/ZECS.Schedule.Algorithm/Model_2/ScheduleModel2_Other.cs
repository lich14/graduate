using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.Define.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.Define;
using ZECS.Schedule.DB;

namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// 调度模型2
    /// </summary>
    public class ScheduleModel2_Other
    {
        //调度模型2实例
        private static ScheduleModel2_Other instance = null;
        //原数据
        private DBData_TOS tos = null;
        private List<STS_WORK_QUEUE_STATUS> wqList = null;
        private List<WORK_INSTRUCTION_STATUS> wiList = null;
        private List<STS_Task> stsList = null;
        private List<ASC_Task> ascList = null;
        private List<AGV_Task> agvList = null;
        private List<Block_Container> blockList = null;
        
        //模型
        private List<WorkQc_Other> workQcList = null;
        private List<WorkBlock_Other> workBlockList = null;
        private List<WorkInstruction_Other> workInstructionList = null;
        
        /// <summary>
        /// 当前涉及所有任务的偏序图（元素对应workInstructionList）
        /// </summary>
        private PartialOrderTable _partialOrderTableForTotal;

        /// <summary>
        /// 当前各QC的最大AGV数量配置列表
        /// </summary>
        private List<int> _maxAGVNumToQCList;

        /// <summary>
        /// 当前各QC的最小AGV数量配置列表
        /// </summary>
        private List<int> _minAGVNumToQCList;

        /// <summary>
        /// Mask列表(包括各Mask对应的分箱区在场箱信息以及对应船箱位）
        /// </summary>
        private List<Mask_Other> _maskList;

        /// <summary>
        /// 桥机效率调节因子，[0,1]，当偏0时，效率按照计划效率，否则按照实际效率
        /// </summary>
        private double _qCEffAdjustFactor;

        /// <summary>
        /// 决策时长（分钟）
        /// (若TOS未给出jobList，则以当前时刻至此时长作为决策时间段；
        /// 否则用jobList中计划最迟完成的装船船箱位任务时刻修正改值，并作为卸船WQ截止的参考
        /// </summary>
        private double _toDecideTimeLength;

        /// <summary>
        /// 卸船时ASC在场地时的平均作业时长(以卸箱至箱区中间所需时间来估算）
        /// </summary>
        private double _meanUnloadTimeLengthInYard;

        /// <summary>
        /// 贪婪算法中得到的结果
        /// </summary>
        private List<currentBetterChoice> _betterChoiceListInGreedyAlg;

        /// <summary>
        /// 构造函数
        /// </summary>
        private ScheduleModel2_Other()
        {

        }

        /// <summary>
        /// 实例化调度模型2
        /// </summary>
        public static ScheduleModel2_Other Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ScheduleModel2_Other();
                }
                return instance;
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void Init()
        {
            if (tos.m_listSTS_WORK_QUEUE_STATUS != null)
            {
                wqList = tos.m_listSTS_WORK_QUEUE_STATUS.Where(t => t.MOVE_KIND == Move_Kind.LOAD || t.MOVE_KIND == Move_Kind.DSCH).ToList();
            }
            else
            {
                wqList = new List<STS_WORK_QUEUE_STATUS>();
                Logger.Algorithm.Error("ScheduleModel2->STS_WORK_QUEUE_STATUS is null Ignore");
            }
            if (tos.m_listWORK_INSTRUCTION_STATUS != null)
            {
                wiList = tos.m_listWORK_INSTRUCTION_STATUS.Where(t => t.MOVE_KIND == Move_Kind.LOAD || t.MOVE_KIND == Move_Kind.DSCH).ToList();
            }
            else
            {
                wiList = new List<WORK_INSTRUCTION_STATUS>();
                Logger.Algorithm.Error("ScheduleModel2->WORK_INSTRUCTION_STATUS is null Ignore");
            }
            if (tos.m_listSTS_Task != null)
            {
                stsList = tos.m_listSTS_Task;
            }
            else
            {
                stsList = new List<STS_Task>();
                Logger.Algorithm.Error("ScheduleModel2->STS_Task is null Ignore");
            }
            if (tos.m_listASC_Task != null)
            {
                ascList = tos.m_listASC_Task;
            }
            else
            {
                ascList = new List<ASC_Task>();
                Logger.Algorithm.Error("ScheduleModel2->ASC_Task is null Ignore");
            }
            if (tos.m_listAGV_Task != null)
            {
                agvList = tos.m_listAGV_Task;
            }
            else
            {
                agvList = new List<AGV_Task>();
                Logger.Algorithm.Error("ScheduleModel2->AGV_Task is null Ignore");
            }
            List<Block_Container> tempblockList = DB_ECS.Instance.Get_All_Block_Container();
            if (tempblockList != null)
            {
                blockList = tempblockList;
            }
            else
            {
                blockList = new List<Block_Container>();
                Logger.Algorithm.Error("ScheduleModel2->Block_Container is null Ignore");
            }
        }

        /// <summary>
        /// 清理
        /// </summary>
        private void Clear()
        {
            if (workQcList == null)
            {
                workQcList = new List<WorkQc_Other>();
            }
            workQcList.Clear();
            if (workBlockList == null)
            {
                workBlockList = new List<WorkBlock_Other>();
            }
            workBlockList.Clear();
            if (workInstructionList == null)
            {
                workInstructionList = new List<WorkInstruction_Other>();
            }
            workInstructionList.Clear();
        }

        /// <summary>
        /// 构建
        /// </summary>
        private void Build()
        {
            foreach (STS_WORK_QUEUE_STATUS wq in wqList)
            {
                WorkQueue_Other workQueue = new WorkQueue_Other(wq);
                workQueue.QC_ID = wq.QC_ID;
                foreach (WORK_INSTRUCTION_STATUS wi in wiList)
                {
                    if (wi.WORK_QUEUE == wq.WORK_QUEUE)
                    {
                        WorkInstruction_Other workInstruction = new WorkInstruction_Other(wi);
                        workInstruction.WORK_QUEUE = wq.WORK_QUEUE;
                        workInstruction.QC_ID = wq.QC_ID;
                        //start find blcok
                        Block_Container bc = blockList.Find(t => t.CONTAINER_ID == wi.CONTAINER_ID);
                        if (bc == null)
                        {
                            Logger.Algorithm.Error("ScheduleModel2->WORK_INSTRUCTION_STATUS and  Block_Container is null Ignore:" + wi.CONTAINER_ID);
                        }
                        else
                        {
                            workInstruction.BLOCK_NO = bc.BLOCK_NO;
                        }
                        //end find block
                        //start block
                        WorkBlock_Other workBlock = workBlockList.Find(t => t.BLOCK_NO == workInstruction.BLOCK_NO);
                        if (workBlock == null)
                        {
                            workBlock = new WorkBlock_Other(bc.BLOCK_NO);
                            workBlock.WorkInstructionList.Add(workInstruction);
                            workBlockList.Add(workBlock);
                        }
                        else
                        {
                            workBlock.WorkInstructionList.Add(workInstruction);
                        }
                        //end block
                        workQueue.WorkInstructionList.Add(workInstruction);
                    }
                }
                //start qc
                WorkQc_Other workQc = workQcList.Find(t => t.QC_ID == wq.QC_ID);
                if (workQc == null)
                {
                    workQc = new WorkQc_Other(wq.QC_ID);
                    workQc.WorkQueueList.Add(workQueue);
                    workQcList.Add(workQc);
                }
                else
                {
                    workQc.WorkQueueList.Add(workQueue);
                }
                //end qc
            }
        }

        /// <summary>
        /// 对WorkQueue排序
        /// </summary>
        private void WorkQueueSort()
        {
            foreach (WorkQc_Other qc in workQcList)
            {
                qc.WorkQueueList.Sort((s1, s2) => s1.WQ.START_TIME.CompareTo(s2.WQ.START_TIME));
            }
        }

        /// <summary>
        /// 重新生成WorkInstruction
        /// </summary>
        private void ResetBuildWorkInstruction()
        {
            foreach (WorkQc_Other qc in workQcList)
            {
                foreach (WorkQueue_Other wq in qc.WorkQueueList)
                {
                    foreach (WorkInstruction_Other wi in wq.WorkInstructionList)
                    {
                        workInstructionList.Add(wi);
                    }
                }
            }
        }

        /// <summary>
        /// 设置偏序关系队列
        /// </summary>
        private void SetPartialOrderList()
        {
            foreach (WorkInstruction_Other wi in workInstructionList)
            {
                List<String> seqList = new List<String>();
                seqList.AddRange(wi.WI.LOGICAL_PREDECESSOR.Split(';'));
                seqList.AddRange(wi.WI.PHYSICAL_PREDECESSOR.Split(';'));
                wi.LogicalAndPhysical = seqList.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
            }
        }

        /// <summary>
        /// 设置JobList与WorkInstructions可作业状态
        /// </summary>
        private void SetWorking()
        {
            foreach (WorkInstruction_Other wi in workInstructionList)
            {
                //stsList
                foreach (STS_Task sts in stsList)
                {
                    if (wi.WI.JOB_ID == sts.Task.JOB_ID)
                    {
                        wi.IsWorking = true;
                        break;
                    }
                }
            }

        }

        /// <summary>
        /// 设置WorkQueue的Enable状态
        /// </summary>
        private void SetWorkQueueEnable()
        {
            foreach (WorkQc_Other qc in workQcList)
            {
                foreach (WorkQueue_Other wq in qc.WorkQueueList)
                {
                    foreach (WorkInstruction_Other wi in wq.WorkInstructionList)
                    {
                        if (wi.IsWorking)
                        {
                            wq.Enable = true;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 设置JobList正在执行任务的状态
        /// </summary>
        private void SetExcuting()
        {
            //正在执行任务
            List<string> stsExcutingList = stsList.Where(t => Helper.IsTaskWorking(t.TaskState)).Select(t => t.Task.JOB_ID).ToList();
            List<string> ascExcutingList = ascList.Where(t => Helper.IsTaskWorking(t.TaskState)).Select(t => t.Task.JOB_ID).ToList();
            List<string> agvExcutingList = agvList.Where(t => Helper.IsTaskWorking(t.TaskState)).Select(t => t.Task.JOB_ID).ToList();
            //求并集
            List<string> excutingList = stsExcutingList.Union(ascExcutingList).ToList().Union(agvExcutingList).ToList();

            foreach (WorkInstruction_Other wi in workInstructionList)
            {
                //可作业状态
                if (wi.IsWorking)
                {
                    //正在执行任务
                    foreach (string excuting in excutingList)
                    {
                        if (wi.WI.JOB_ID == excuting)
                        {
                            wi.ExcuteStatus = 1;
                            break;
                        }
                    }
                }
            }
            
        }

        /// <summary>
        /// 设置JobList未执行任务的状态
        /// </summary>
        private void SetNoExcute()
        {
            //未执行任务
            List<string> stsNoExcuteList = stsList.Where(t => Helper.IsTaskInitial(t.TaskState)).Select(t => t.Task.JOB_ID).ToList();
            List<string> ascNoExcuteList = ascList.Where(t => Helper.IsTaskInitial(t.TaskState)).Select(t => t.Task.JOB_ID).ToList();
            List<string> agvNoExcuteList = agvList.Where(t => Helper.IsTaskInitial(t.TaskState)).Select(t => t.Task.JOB_ID).ToList();
            //求交集
            List<string> noExcuteList = stsNoExcuteList.Intersect(ascNoExcuteList).ToList().Intersect(agvNoExcuteList).ToList();

            foreach (WorkInstruction_Other wi in workInstructionList)
            {
                //可作业状态
                if (wi.IsWorking)
                {
                    //未执行任务
                    foreach (string noExcute in noExcuteList)
                    {
                        if (wi.WI.JOB_ID == noExcute)
                        {
                            wi.ExcuteStatus = 0;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 设置可交换箱队列
        /// </summary>
        private void SetMaskContainerList()
        {
            for (int i = 0; i < workInstructionList.Count; i++)
            {
                for (int j = i + 1; j < workInstructionList.Count; j++)
                {
                    //现在只管STOW_FACTOR相等
                    if (workInstructionList[i].IsWorking && workInstructionList[j].IsWorking
                        && workInstructionList[i].ExcuteStatus == 0 && workInstructionList[j].ExcuteStatus == 0
                        && workInstructionList[i].WI.CONTAINER_STOW_FACTOR == workInstructionList[j].WI.CONTAINER_STOW_FACTOR)
                    {
                        workInstructionList[i].MaskContainer.Add(workInstructionList[j].WI.CONTAINER_ID);
                    }
                }
            }
        }

        /// <summary>
        /// 估算正在执行任务的时间
        /// </summary>
        public void EstimateExcutingTime()
        {
            //时间估算
            foreach (WorkInstruction_Other wi in workInstructionList)
            {
                if (wi.IsWorking && wi.ExcuteStatus == 1)
                {
                    wi.EstimateTime = new TimeSpan(0, 0, 0, 0, 0);
                }
            }
            //求qc最晚完成正在作业任务时长
            foreach (WorkQc_Other qc in workQcList)
            {
                foreach (WorkQueue_Other wq in qc.WorkQueueList)
                {
                    if (wq.Enable)
                    {
                        foreach (WorkInstruction_Other wi in wq.WorkInstructionList)
                        {
                            if (wi.IsWorking && wi.ExcuteStatus == 1)
                            {
                                if (qc.EstimateTime.CompareTo(wi.EstimateTime) < 0)
                                {
                                    qc.EstimateTime = wi.EstimateTime;
                                }
                            }
                        }
                        break;
                    }
                }
            }
            //求block最晚完成正在作业任务时长
            foreach (WorkBlock_Other wb in workBlockList)
            {
                if (!string.IsNullOrWhiteSpace(wb.BLOCK_NO))
                {
                    foreach (WorkInstruction_Other wi in wb.WorkInstructionList)
                    {
                        if (wi.IsWorking && wi.ExcuteStatus == 1)
                        {
                            if (wb.EstimateTime.CompareTo(wi.EstimateTime) < 0)
                            {
                                wb.EstimateTime = wi.EstimateTime;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 贪婪算法
        /// </summary>
        public void GreedyAlgorithm()
        {
            //贪婪方式1：选择free时刻最早的QC，
            //1.若是装船，对于该QC的所有可作业船箱位以及所有可出箱箱区，
            //选择与其下个任务计划作业时刻最接近的船箱位以及出箱箱区作为当前决策作业船箱位以及出箱箱区
            //2.若是卸船，对于所有可卸箱区，选择理论上最早可处理该卸箱的箱区作为当前决策卸船箱区


            List<WorkQc_Other> currentQCList = new List<WorkQc_Other>();

            List<WorkBlock_Other> currentBlockList = new List<WorkBlock_Other>();

            List<Mask_Other> currentMaskList = new List<Mask_Other>();

            //克隆
            //DeepCloneTool b = new DeepCloneTool();

            //currentQCList = b.GetCloneList(this.workQcList);

            //currentMaskList = b.GetCloneList(this._maskList);

            //currentBlockList = b.GetCloneList(this.workBlockList);



            //记录下临时最优待定的箱区，QC，Mask等信息;
            List<currentBetterChoice> betterChoiceList = new List<currentBetterChoice>();
                        
            while (currentQCList.Count > 0)
            {
                DateTime earlistFreeTimeOfQC = currentQCList.Min(t => t.CurrentFreeTime);

                var qcS = from x in currentQCList where x.CurrentFreeTime.Equals(earlistFreeTimeOfQC) select x;

                currentBetterChoice tmpBetterChoice = new currentBetterChoice();

                double minDiffTimeLength = 10000;

                //可能存在多个相同free时刻的QC
                foreach (var qc in qcS)
                {
                    if (qc.WorkQueueList != null && qc.WorkQueueList.Count > 0)
                    {
                        if (qc.WorkQueueList[0].Enable == true)
                        {
                            double tmpDiffMin = 0;

                            //QC
                            DateTime goalTime = qc.CurrentFreeTime.AddMinutes(qc.PlanTimeLengthForEachWI);

                            if (qc.WorkQueueList[0].WQ.MOVE_KIND == Move_Kind.LOAD)
                            {
                                //遍历所有可能的可作业船箱位
                                List<PartialOrderTable.PartialOrderVertex> currentWIIndexList = new List<PartialOrderTable.PartialOrderVertex>();
                                currentWIIndexList = qc.WorkQueueList[0].PartialOrderForWQ.GetAllSourceVertices();

                                
                                foreach (PartialOrderTable.PartialOrderVertex a in currentWIIndexList)
                                {
                                    //遍历所有可能的出箱箱区,计算各箱区的最快出箱到达QC时刻

                                    Mask_Other currentMask = currentMaskList[qc.WorkQueueList[0].WorkInstructionList[a.VertexIndex].MaskIndex];

                                    foreach (SubYardContainerOfMask_Other block in currentMask.SubYardCntrList)
                                    {
                                        //当前箱区已被判断停止出箱
                                        if (currentBlockList.FindIndex(t => t.BLOCK_NO == block.YardBlock.BLOCK_NO) == -1)
                                            continue;

                                        DateTime currentTime = EstimateArriveQCTime(block.YardBlock, currentMask, qc);

                                        //当估算到达时刻早于计划时刻，是否进行一定的奖励？
                                        if (currentTime > goalTime)
                                            tmpDiffMin = (currentTime - goalTime).TotalMinutes;
                                        else
                                            tmpDiffMin = (goalTime - currentTime).TotalMinutes;

                                        if (tmpDiffMin < minDiffTimeLength)
                                        {
                                            minDiffTimeLength = tmpDiffMin;

                                            tmpBetterChoice.betterBlock = block.YardBlock;

                                            tmpBetterChoice.betterQC = qc;

                                            tmpBetterChoice.betterWI = qc.WorkQueueList[0].WorkInstructionList[a.VertexIndex];

                                            tmpBetterChoice.betterTime = currentTime;

                                            tmpBetterChoice.BetterWIVertexIndex=a.VertexIndex;
                                        }

                                    }
                                }


                            }
                            else
                                if (qc.WorkQueueList[0].WQ.MOVE_KIND == Move_Kind.DSCH)
                                {
                                    //goalTime增加从桥机到最近箱区的AGV运行时长（分钟）
                                    //goalTime.AddMinutes();

                                    foreach (WorkBlock_Other block in qc.WorkQueueList[0].CouldUnloadBlockList)
                                    {
                                        int tmpIndex=currentBlockList.FindIndex(t => t.BLOCK_NO == block.BLOCK_NO);

                                        //考虑箱区卸箱数量，暂不考虑区分20尺，40尺
                                        if (tmpIndex == -1 || (tmpIndex !=-1 && currentBlockList[tmpIndex].couldUnloadCntrNum[0] <= 0))
                                            continue;

                                        DateTime currentTime = EstimateArriveBlockTime(qc, block);

                                        if (currentTime > goalTime)
                                            tmpDiffMin = (currentTime - goalTime).TotalMinutes;
                                        else
                                            tmpDiffMin = (goalTime - currentTime).TotalMinutes;

                                        if (tmpDiffMin < minDiffTimeLength)
                                        {
                                            minDiffTimeLength = tmpDiffMin;

                                            tmpBetterChoice.betterBlock = block;

                                            tmpBetterChoice.betterQC = qc;

                                            
                                        }
                                    }
                                }
                        }
                    }
                }

                if (tmpBetterChoice == null)
                {
                    //"决策失败"
                    break;
                }
                
                betterChoiceList.Add(tmpBetterChoice);

                int tmpQCIndex = currentQCList.FindIndex(t => t.QC_ID == tmpBetterChoice.betterQC.QC_ID);

                int tmpBlockIndex = currentBlockList.FindIndex(t => t.BLOCK_NO == tmpBetterChoice.betterBlock.BLOCK_NO);

                currentQCList[tmpQCIndex].WorkQueueList[0].NeedToDoWINum--;
                               

                //对于当前决策的修正相应信息
                if (tmpBetterChoice.betterQC.WorkQueueList[0].WQ.MOVE_KIND == Move_Kind.LOAD)
                {
                    //1.修正桥机的信息

                    currentQCList[tmpQCIndex].CurrentFreeTime = tmpBetterChoice.betterTime;

                    currentQCList[tmpQCIndex].WorkQueueList[0].RemoveWIAt(tmpBetterChoice.betterWI.IndexInWQ);
                    

                    //2.修正箱区信息

                    int tmpMaskOfBlockIndex = currentMaskList[tmpBetterChoice.betterWI.MaskIndex].SubYardCntrList.FindIndex(t => t.YardBlock.BLOCK_NO == tmpBetterChoice.betterBlock.BLOCK_NO);

                    currentBlockList[tmpBlockIndex].CurrentFreeTime.AddMinutes(currentMaskList[tmpBetterChoice.betterWI.MaskIndex].SubYardCntrList[tmpMaskOfBlockIndex].MeanASCTransferTimeLength);


                    //3.修正当前任务的偏序表
                    if (currentQCList[tmpQCIndex].WorkQueueList[0].NeedToDoWINum > 0)
                    {
                        currentQCList[tmpQCIndex].WorkQueueList[0].PartialOrderForWQ.RemoveSouceVertexAt(tmpBetterChoice.BetterWIVertexIndex);
                    }

                    //删去free时刻超过决策截止时刻的堆场
                    //if (currentBlockList[tmpBlockIndex].CurrentFreeTime > DateTime.Now.AddMinutes(this._toDecideTimeLength+))
                    //{
                    //    currentBlockList.RemoveAt(tmpBlockIndex);


                    //foreach (Mask_Other a in currentMaskList)
                    //{
                    //    foreach (SubYardContainerOfMask_Other b in a.SubYardCntrList)
                    //    {

                    //    }
                    //}
                    //}

                    if (currentMaskList[tmpBetterChoice.betterWI.MaskIndex].SubYardCntrList[tmpMaskOfBlockIndex].currentLoadNum == 1)
                        currentMaskList[tmpBetterChoice.betterWI.MaskIndex].SubYardCntrList.RemoveAt(tmpMaskOfBlockIndex);
                    else
                        currentMaskList[tmpBetterChoice.betterWI.MaskIndex].SubYardCntrList[tmpMaskOfBlockIndex].currentLoadNum--;
                }

                if (tmpBetterChoice.betterQC.WorkQueueList[0].WQ.MOVE_KIND == Move_Kind.DSCH)
                {
                    //卸船时QC的freeTime按照计划效率递进
                    currentBlockList[tmpBlockIndex].CurrentFreeTime = ((currentBlockList[tmpBlockIndex].CurrentFreeTime > tmpBetterChoice.betterTime) ? currentBlockList[tmpBlockIndex].CurrentFreeTime : tmpBetterChoice.betterTime).AddMinutes(this._meanUnloadTimeLengthInYard);
                    currentQCList[tmpQCIndex].CurrentFreeTime.AddMinutes(currentQCList[tmpQCIndex].PlanTimeLengthForEachWI);

                    currentBlockList[tmpBlockIndex].couldUnloadCntrNum[0]--;

                }

                if (currentQCList[tmpQCIndex].WorkQueueList[0].NeedToDoWINum == 0)
                {
                    if (currentQCList[tmpQCIndex].WorkQueueList.Count == 1)
                    {
                        currentQCList.RemoveAt(tmpQCIndex);
                    }
                    else
                    {
                        double lateTimeLength = (currentQCList[tmpQCIndex].CurrentFreeTime - currentQCList[tmpQCIndex].WorkQueueList[0].WQ.END_TIME).TotalMinutes;

                        currentQCList[tmpQCIndex].WorkQueueList.RemoveAt(0);

                        currentQCList[tmpQCIndex].CurrentFreeTime = currentQCList[tmpQCIndex].WorkQueueList[0].WQ.START_TIME.AddMinutes(lateTimeLength);
                                                
                    }
                }
            }

            if (currentQCList.Count == 0)
                this._betterChoiceListInGreedyAlg = betterChoiceList;
            //贪婪方式2：昝略

        }

        /// <summary>
        /// 从指定箱区的Free时刻起估计从指定箱区装指定Mask箱至指定QC的到达时刻
        /// </summary>
        /// <param name="startBlock"></param>
        /// <param name="currentMask"></param>
        /// <param name="finishQC"></param>
        /// <returns></returns>
        private DateTime EstimateArriveQCTime(WorkBlock_Other startBlock,Mask_Other currentMask,WorkQc_Other finishQC)
        {
            return DateTime.Now;
        }

        /// <summary>
        /// 估算从指定QC卸船到指定箱区的时刻
        /// </summary>
        /// <param name="startQC"></param>
        /// <param name="finishBlock"></param>
        /// <returns></returns>
        private DateTime EstimateArriveBlockTime(WorkQc_Other startQC, WorkBlock_Other finishBlock)
        {
            return DateTime.Now;
        }



        /// <summary>
        /// 保存贪婪算法中单次决策得到的结果
        /// </summary>
        class currentBetterChoice
        {
            public WorkQc_Other betterQC;

            public WorkBlock_Other betterBlock;

            public WorkInstruction_Other betterWI;

            public int BetterWIVertexIndex;

            public DateTime betterTime;

            public currentBetterChoice()
            {

            }
        }


        /// <summary>
        /// 输出偏序结构
        /// </summary>
        public void OutputPartial()
        {
            //合并所有已决策的各WQ的偏序表


            //增加不同QC间

        }


        //private void AddSameQCPartialTable(ref Pa)
        

        /// <summary>
        /// 调度
        /// </summary>
        /// <param name="dbTos"></param>
        /// <returns></returns>
        public bool Schedule(DBData_TOS dbTos)
        {
            //校验错误
            if (dbTos == null)
            {
                Logger.Algorithm.Error("ScheduleModel2->DBData_TOS is null Error");
                return false;
            }
            //初始化
            Init();
            //清理
            Clear();
            //构建
            Build();
            //重新生成WorkInstruction
            ResetBuildWorkInstruction();
            //对WorkQueue排序
            WorkQueueSort();
            //设置偏序关系队列
            SetPartialOrderList();
            //设置JobList与WorkInstructions可作业状态
            SetWorking();
            //设置WorkQueue的Enable状态
            SetWorkQueueEnable();
            //设置JobList正在执行任务的状态
            SetExcuting();
            //设置JobList未执行任务的状态
            SetNoExcute();
            //设置可交换箱队列
            SetMaskContainerList();
            //估算正在执行任务的时间
            EstimateExcutingTime();
            //贪婪算法
            GreedyAlgorithm();
            //输出偏序结构
            OutputPartial();
            //设置效力
            return true;
        }
    }

    /// <summary>
    /// WorkQc
    /// </summary>
    public class WorkQc_Other
    {
        public WorkQc_Other(String qcid)
        {
            QC_ID = qcid;
            WorkQueueList = new List<WorkQueue_Other>();
            EstimateTime = new TimeSpan(0, 0, 0, 0, 0);
        }
        public string QC_ID { get; set; }
        public List<WorkQueue_Other> WorkQueueList { get; set; }
        public TimeSpan EstimateTime { get; set; }

        /// <summary>
        /// 计划作业效率(单个任务计划作业时长：分钟)
        /// </summary>
        public double PlanTimeLengthForEachWI;

        /// <summary>
        /// 估算的完成正在执行任务的时刻
        /// </summary>
        public DateTime CurrentFreeTime;
        
        /// <summary>
        /// QC索引
        /// </summary>
        public int Index;
    }

    /// <summary>
    /// WorkQueue
    /// </summary>
    public class WorkQueue_Other
    {
        public WorkQueue_Other(STS_WORK_QUEUE_STATUS wq)
        {
            WQ = wq;
            WorkInstructionList = new List<WorkInstruction_Other>();
            QC_ID = "";
            Enable = false;
        }
        public STS_WORK_QUEUE_STATUS WQ { get; set; }
        public List<WorkInstruction_Other> WorkInstructionList { get; set; }
        public string QC_ID { get; set; }
        public bool Enable { get; set; }
        /// <summary>
        /// 该WQ中所有船箱位对应的偏序图
        /// </summary>
        public PartialOrderTable PartialOrderForWQ;
        /// <summary>
        /// WQ索引
        /// </summary>
        public int Index;

        /// <summary>
        /// 待完成WI任务数（双箱算一个任务，此值只在jobList非空时有效）
        /// </summary>
        public int NeedToDoWINum;

        /// <summary>
        /// 若该WQ包含卸船，则其允许的卸箱箱区列表
        /// </summary>
        public List<WorkBlock_Other> CouldUnloadBlockList;

        /// <summary>
        /// 移去指定索引的WI（修正WIList以及偏序图中相应信息）
        /// </summary>
        /// <param name="Index"></param>
        public void RemoveWIAt(int Index)
        {

        }
    }

    /// <summary>
    /// WorkInstruction
    /// </summary>
    public class WorkInstruction_Other
    {
        public WorkInstruction_Other(WORK_INSTRUCTION_STATUS wi)
        {
            WI = wi;
            WORK_QUEUE = "";
            QC_ID = "";
            BLOCK_NO = "";
            IsWorking = false;
            ExcuteStatus = -1;
            LogicalAndPhysical = new List<string>();
            MaskContainer = new List<string>();
            EstimateTime = new TimeSpan(0, 0, 0, 0, 0);
            
        }
        public WORK_INSTRUCTION_STATUS WI { get; set; }
        public string WORK_QUEUE { get; set; }
        public string QC_ID { get; set; }
        public string BLOCK_NO { get; set; }
        public bool IsWorking { get; set; }
        public int ExcuteStatus { get; set; }
        public List<string> LogicalAndPhysical { get; set; }
        public List<string> MaskContainer { get; set; }
        public TimeSpan EstimateTime { get; set; }
        
        /// <summary>
        /// 对应Mask索引
        /// </summary>
        public int MaskIndex;

        /// <summary>
        /// WI在所在WQ中的索引
        /// </summary>
        public int IndexInWQ;

    }

    /// <summary>
    /// WorkBlock
    /// </summary>
    public class WorkBlock_Other
    {
        public WorkBlock_Other(string blockno)
        {
            BLOCK_NO = blockno;
            WorkInstructionList = new List<WorkInstruction_Other>();
            EstimateTime = new TimeSpan(0, 0, 0, 0, 0);
        }
        public string BLOCK_NO { get; set; }
        public List<WorkInstruction_Other> WorkInstructionList { get; set; }
        public TimeSpan EstimateTime { get; set; }
        
        /// <summary>
        /// 该Block的Free时刻
        /// 若该时刻晚于决策截止时刻+AGV从QC至箱区的时长，则该箱区不接受卸箱
        /// </summary>
        public DateTime CurrentFreeTime;

        /// <summary>
        /// 当前箱区允许卸箱数,[0]记录40尺，[1]记录20尺等
        /// </summary>
        public int[] couldUnloadCntrNum;
    }


    public class Mask_Other
    {
        /// <summary>
        /// MSAK ID
        /// </summary>
        public int MaskID;

        /// <summary>
        /// Mask索引
        /// </summary>
        public int MaskIndex;


        /// <summary>
        /// 待定配载在场箱队列
        /// </summary>
        public List<YardContainerEntity> YardCntrList = new List<YardContainerEntity>();

        /// <summary>
        /// 待定配载在场箱分箱区子队列
        /// </summary>
        public List<SubYardContainerOfMask_Other> SubYardCntrList = new List<SubYardContainerOfMask_Other>();

        
    }

    /// <summary>
    /// 属于同一Mask的在场箱类
    /// </summary>
    public class SubYardContainerOfMask_Other
    {
        public WorkBlock_Other YardBlock;

        /// <summary>
        /// 在场箱子某mask箱区子队列
        /// </summary>
        public List<YardContainerEntity> SubYardContainerList = new List<YardContainerEntity>();

        /// <summary>
        /// 该箱区该Mask箱的ASC平均移动时长（从箱区海侧起计）
        /// </summary>
        public double MeanASCTransferTimeLength;

        /// <summary>
        /// 该箱区当前可提供该Mask的箱量
        /// </summary>
        public int currentLoadNum;
    }
}
