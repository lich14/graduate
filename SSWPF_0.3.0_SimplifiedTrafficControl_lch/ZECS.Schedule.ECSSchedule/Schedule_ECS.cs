using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.Define;
using ZECS.Schedule.DB;
using System.Threading;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Algorithm;
using ZECS.Schedule.Algorithm.Utilities;
using ZECS.Schedule.Define.DBDefine.Schedule;
using SharpSim;
using SSWPF.Define;

namespace ZECS.Schedule.ECSSchedule
{
    public class Schedule_ECS : ThreadBase
    {
        private static Schedule_ECS s_instance;
        public static Schedule_ECS Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new Schedule_ECS();
                }
                return s_instance;
            }
        }

        private Schedule_ECS()
        {
           
        }

        protected DBData_Schedule m_ScheduleDBData = new DBData_Schedule();
        protected PartialOrderGraph m_partialOrderGraph = null;
        protected List<WORK_INSTRUCTION_STATUS> m_listWITopLogicSorted = null; 

        public  bool Start()
        {
            m_nInterval = 10;
            base.Start(null);

            Schedule_STS.Instance.Start();
            Schedule_AGV.Instance.Start();
            Schedule_ASC.Instance.Start();

            return true;
        }

        public override void Stop()
        {
            Schedule_STS.Instance.Stop();
            Schedule_AGV.Instance.Stop();
            Schedule_ASC.Instance.Stop();

            base.Stop();
        }
        public override void ThreadDeal(object param)
        {
            //0.从ECS获取Order,Command,Status信息 
            GetEcsDbData();

            //1.从TOS获取任务列表信息,WorkQueue
            GetTosDbData();

            //2.总调度算法,输出带建议的Work_Instruction和偏序表 
            m_listWITopLogicSorted = new List<WORK_INSTRUCTION_STATUS>();
            m_partialOrderGraph = new PartialOrderGraph();
            bool bRet = Schedule_Algo.Instance.GeneralSchedule(ref m_ScheduleDBData, ref m_partialOrderGraph);

            if (bRet)
            {
                //3.子调度预处理
                OrderedDecisionTable odt = new OrderedDecisionTable();
                odt.SetPartialOrderedTable(m_partialOrderGraph.m_WIList, m_partialOrderGraph.m_DecisionTable);
                odt.TopoLogicSort(out m_listWITopLogicSorted);
                //odt.GetSourceNodes(out m_listWITopLogicSorted);
            }
            else
            {
                Logger.ECSSchedule.Error("[GeneralSchedule] return false. FAIL!!! ");
            }

            //执行子调度前的预处理
            CheckQcPosition(m_ScheduleDBData.m_DBData_STSMS.m_listSTS_Status);

            //更新算法库车道状态
            UpdateAlgoLaneStatus();

            //4.3个子系统分别调度,处理状态反馈.
            LanePlan lanePlan = new LanePlan();
            InitLanePlan(lanePlan, m_ScheduleDBData);

            Schedule_AGV.Instance.Schedule(ref m_ScheduleDBData, m_listWITopLogicSorted, lanePlan);
            Schedule_STS.Instance.Schedule(ref m_ScheduleDBData, m_listWITopLogicSorted);
            Schedule_ASC.Instance.Schedule(ref m_ScheduleDBData, m_listWITopLogicSorted, lanePlan);           
        }

        private void GetTosDbData()
        {
            //1.从TOS获取任务列表信息,WorkQueue
            try
            {
                m_ScheduleDBData.m_DBData_TOS.m_listSTS_Task = DB_TOS.Instance.GetList_STS_Task();
                m_ScheduleDBData.m_DBData_TOS.m_listAGV_Task = DB_TOS.Instance.GetList_AGV_Task();
                m_ScheduleDBData.m_DBData_TOS.m_listASC_Task = DB_TOS.Instance.GetList_ASC_Task();

                m_ScheduleDBData.m_DBData_TOS.m_listSTS_ResJob = DB_TOS.Instance.GetList_STS_ResJob();
                m_ScheduleDBData.m_DBData_TOS.m_listAGV_ResJob = DB_TOS.Instance.GetList_AGV_ResJob();
                m_ScheduleDBData.m_DBData_TOS.m_listASC_ResJob = DB_TOS.Instance.GetList_ASC_ResJob();

                m_ScheduleDBData.m_DBData_TOS.m_listBERTH_STATUS = DB_TOS.Instance.GetList_BERTH_STATUS();
                m_ScheduleDBData.m_DBData_TOS.m_listSTS_WORK_QUEUE_STATUS = DB_TOS.Instance.GetList_STS_WORK_QUEUE_STATUS();
                m_ScheduleDBData.m_DBData_TOS.m_listWORK_INSTRUCTION_STATUS = DB_TOS.Instance.GetList_WORK_INSTRUCTION_STATUS();
            }
            catch (Exception e)
            {
                Logger.ECSSchedule.Error("DB_TOS or Database Error:", e);
                //throw;
            }
        }

        private void GetEcsDbData()
        {
            try
            {
                //1.收集STSMS数据
                m_ScheduleDBData.m_DBData_STSMS.m_listSTS_Order = DB_ECS.Instance.GetList_STS_Order();
                m_ScheduleDBData.m_DBData_STSMS.m_listSTS_Command = DB_ECS.Instance.GetList_STS_Command();
                m_ScheduleDBData.m_DBData_STSMS.m_listSTS_Status = DB_ECS.Instance.GetList_STS_STATUS();

                //2.收集VMS数据 
                m_ScheduleDBData.m_DBData_VMS.m_listAGV_Command = DB_ECS.Instance.GetList_AGV_Command();
                m_ScheduleDBData.m_DBData_VMS.m_listAGV_Order = DB_ECS.Instance.GetList_AGV_Order();
                m_ScheduleDBData.m_DBData_VMS.m_listAGV_Status = DB_ECS.Instance.GetList_AGV_STATUS();

                //3.收集BMS数据
                m_ScheduleDBData.m_DBData_BMS.m_listASC_Command = DB_ECS.Instance.GetList_ASC_Command();
                m_ScheduleDBData.m_DBData_BMS.m_listASC_Order = DB_ECS.Instance.GetList_ASC_Order();
                m_ScheduleDBData.m_DBData_BMS.m_listASC_Status = DB_ECS.Instance.GetList_ASC_STATUS();
            }
            catch (Exception e)
            {
                Logger.ECSSchedule.Error("DB_ECS or Database Error:", e);
                //throw;
            }
            m_ScheduleDBData.m_dtUpdate = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            //m_ScheduleDBData.m_dtUpdate = DateTime.Now;
        }

        /// <summary>
        /// 检查QC是否有移动，如果有移动
        /// 更新时间估算模块的QC位置，单位是毫米
        /// </summary>
        /// <param name="listStatus"></param>
        private void CheckQcPosition(List<STS_STATUS> listStatus)
        {
            if (listStatus == null)
                return;

            if (m_listSTSPosition.Count != listStatus.Count)
            {
                m_listSTSPosition = listStatus.Select(pos => pos.nQCPosition).ToList<int>();
                foreach (STS_STATUS status in listStatus)
                {
                    UpdateQcPosition(status, null);
                }
            }
            else
            {
                for (int i = 0; i < m_listSTSPosition.Count; i++)
                {
                    if (m_listSTSPosition[i] != listStatus[i].nQCPosition)
                    {
                        UpdateQcPosition(listStatus[i], null);
                        m_listSTSPosition[i] = listStatus[i].nQCPosition;
                    }
                }
            }
        }

        private void UpdateAlgoLaneStatus()
        {
            if (AgvTimeEstimate.Instance.bInitialize)
                AgvTimeEstimate.Instance.UpdateLaneStatus(); 
        }

        private void UpdateQcPosition(STS_STATUS status, EventArgs e)
        {
            int QCID = Utility.GetNumberFromString(status.QC_ID);
            if (QCID != 0 && AgvTimeEstimate.Instance.bInitialize)
                AgvTimeEstimate.Instance.UpdateQcPosition((ushort)QCID, status.nQCPosition / 1000);
        }

        private List<int> m_listSTSPosition = new List<int>();

        private void InitLanePlan(LanePlan lanePlan, DBData_Schedule dbDataSchedule)
        {
            //
            lanePlan.Init();

            //
            foreach (var order in dbDataSchedule.m_DBData_VMS.m_listAGV_Order)
            {
                LaneInfoEx li = null;

                AGV_Command cmd = dbDataSchedule.m_DBData_VMS.m_listAGV_Command.Find(x => x.ORDER_ID == order.ORDER_ID);

                AGV_Order_Type agvOrderType = Utility.GetAgvOrderType(order, cmd);

                if (agvOrderType == AGV_Order_Type.ReceiveFromWstp)
                {
                    li = lanePlan.GetLane(order.FROM_BLOCK, order.FROM_BAY_TYPE, order.FROM_LANE);
                }
                else if (agvOrderType == AGV_Order_Type.DelieverToWstp)
                {
                    li = lanePlan.GetLane(order.TO_BLOCK, order.TO_BAY_TYPE, order.TO_LANE);
                }
                else if (agvOrderType == AGV_Order_Type.RepositionToPb)
                {
#if ASSIGN_PB_BY_SCHD
                    li = lanePlan.GetLane("0", BayType.PB.ToString(), order.TO_LANE);
#endif
                }
                else if (agvOrderType == AGV_Order_Type.DelieverToWstpComplete)
                {
                    if (Utility.IsAscDoingCompletedAgvOrder(dbDataSchedule.m_DBData_BMS, order))
                    {
                        li = lanePlan.GetLane(order.TO_BLOCK, order.TO_BAY_TYPE, order.TO_LANE);

                        string log =
                            "[AGV] agv order is complete but asc order is not sent or not complete from. AGV Order: " +
                            order + " lane=" + (li == null ? "null" : li.ToString());

                        if (li != null && !li.IsMateLane())
                            Logger.ECSSchedule.Error(log);
                        else
                            Logger.ECSSchedule.Info(log);
                    }
                }

                if (li != null)
                {
                    lanePlan.AddLaneInUsing(li, order.ORDER_ID);

                    Logger.ScheduleSnapshot.Debug(string.Format("Lane: {0}, {1, -22}, AGV Order: {2}, Cmd: {3}",
                        li, agvOrderType, order, cmd == null ? "null" : cmd.ToString()));
                }
            }
        }
    }

}
