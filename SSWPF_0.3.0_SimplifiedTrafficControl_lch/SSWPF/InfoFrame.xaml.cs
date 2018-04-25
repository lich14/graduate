using System;
using System.Data;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SSWPF.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.CiTOS;

namespace SSWPF
{

    /// <summary>
    /// InfoFrame.xaml 的交互逻辑
    /// </summary>
    public partial class InfoFrame : Window
    {
        // 窗口后台数据
        private List<InfoWorkQueue> lWorkQueueInfos;
        private List<InfoWorkInstruction> lWorkInstructionInfos;
        private List<InfoBerthStatus> lBerthStatusInfos;
        private List<InfoSTSResJob> lSTSResJobInfos;
        private List<InfoSTSTask> lSTSTaskInfos;
        private List<InfoSTSOrder> lSTSOrderInfos;
        private List<InfoSTSCommand> lSTSCommandInfos;
        private List<InfoSTSStatus> lSTSStatusInfos;
        private List<InfoASCResJob> lASCResJobInfos;
        private List<InfoASCTask> lASCTaskInfos;
        private List<InfoASCOrder> lASCOrderInfos;
        private List<InfoASCCommand> lASCCommandInfos;
        private List<InfoASCStatus> lASCStatusInfos;
        private List<InfoAGVResJob> lAGVResJobInfos;
        private List<InfoAGVTask> lAGVTaskInfos;
        private List<InfoAGVOrder> lAGVOrderInfos;
        private List<InfoAGVCommand> lAGVCommandInfos;
        private List<InfoAGVStatus> lAGVStatusInfos;
        private DataTable dtPartialOrderInfos;

        public InfoFrame()
        {
            InitializeComponent();
            this.Preparation();
        }

        // 初始化 List 和 ItemSource
        private void Preparation()
        {
            // List
            this.lWorkQueueInfos = new List<InfoWorkQueue>();
            this.lWorkInstructionInfos = new List<InfoWorkInstruction>();
            this.lBerthStatusInfos = new List<InfoBerthStatus>();
            this.lSTSResJobInfos = new List<InfoSTSResJob>();
            this.lSTSTaskInfos = new List<InfoSTSTask>();
            this.lSTSOrderInfos = new List<InfoSTSOrder>();
            this.lSTSCommandInfos = new List<InfoSTSCommand>();
            this.lSTSStatusInfos = new List<InfoSTSStatus>();
            this.lASCCommandInfos = new List<InfoASCCommand>();
            this.lASCOrderInfos = new List<InfoASCOrder>();
            this.lASCResJobInfos = new List<InfoASCResJob>();
            this.lASCStatusInfos = new List<InfoASCStatus>();
            this.lASCTaskInfos = new List<InfoASCTask>();
            this.lAGVCommandInfos = new List<InfoAGVCommand>();
            this.lAGVOrderInfos = new List<InfoAGVOrder>();
            this.lAGVResJobInfos = new List<InfoAGVResJob>();
            this.lAGVStatusInfos = new List<InfoAGVStatus>();
            this.lAGVTaskInfos = new List<InfoAGVTask>();
            this.dtPartialOrderInfos = new DataTable();

            // ItemSource 指定
            this.lv_WorkQueue.ItemsSource = this.lWorkQueueInfos;
            this.lv_WorkInstruction.ItemsSource = this.lWorkInstructionInfos;
            this.lv_BerthStatus.ItemsSource = this.lBerthStatusInfos;
            this.lv_STSResJob.ItemsSource = this.lSTSResJobInfos;
            this.lv_STSTask.ItemsSource = this.lSTSTaskInfos;
            this.lv_STSOrder.ItemsSource = this.lSTSOrderInfos;
            this.lv_STSCommand.ItemsSource = this.lSTSCommandInfos;
            this.lv_STSStatus.ItemsSource = this.lSTSStatusInfos;
            this.lv_ASCResJob.ItemsSource = this.lASCResJobInfos;
            this.lv_ASCTask.ItemsSource = this.lASCTaskInfos;
            this.lv_ASCOrder.ItemsSource = this.lASCOrderInfos;
            this.lv_ASCCommand.ItemsSource = this.lASCCommandInfos;
            this.lv_ASCStatus.ItemsSource = this.lASCStatusInfos;
            this.lv_AGVResJob.ItemsSource = this.lAGVResJobInfos;
            this.lv_AGVTask.ItemsSource = this.lAGVTaskInfos;
            this.lv_AGVOrder.ItemsSource = this.lAGVOrderInfos;
            this.lv_AGVCommand.ItemsSource = this.lAGVCommandInfos;
            this.lv_AGVStatus.ItemsSource = this.lAGVStatusInfos;
            this.dg_OrderTable.ItemsSource = this.dtPartialOrderInfos.DefaultView;
        }

        /// <summary>
        /// 刷新Info，更新内容，多退少补
        /// </summary>
        /// <param name="oPPTInfoFrame">投射包</param>
        public void RefreshInfos(ProjectPackageToInfoFrame oPPTInfoFrame)
        {
            this.CreateInfos(oPPTInfoFrame);
            this.RenewInfos(oPPTInfoFrame);
            this.DeleteInfos(oPPTInfoFrame, false);
        }

        /// <summary>
        /// 创建Info
        /// </summary>
        /// <param name="oPPTInfoFrame">投射包</param>
        public void CreateInfos(ProjectPackageToInfoFrame oPPTInfoFrame)
        {
            if (oPPTInfoFrame == null)
                return;

            if (oPPTInfoFrame.lWQs != null && oPPTInfoFrame.lWQs.Count > 0)
                this.CreateInfo(oPPTInfoFrame.lWQs);
            if (oPPTInfoFrame.lWIs != null && oPPTInfoFrame.lWIs.Count > 0)
                this.CreateInfo(oPPTInfoFrame.lWIs);
            if (oPPTInfoFrame.lBerthStatuses != null && oPPTInfoFrame.lBerthStatuses.Count > 0)
                this.CreateInfo(oPPTInfoFrame.lBerthStatuses);
            if (oPPTInfoFrame.lSTSResJobs != null && oPPTInfoFrame.lSTSResJobs.Count > 0)
                this.CreateInfo(oPPTInfoFrame.lSTSResJobs);
            if (oPPTInfoFrame.lSTSTasks != null && oPPTInfoFrame.lSTSTasks.Count > 0)
                this.CreateInfo(oPPTInfoFrame.lSTSTasks);
            if (oPPTInfoFrame.lSTSOrders != null && oPPTInfoFrame.lSTSOrders.Count > 0)
                this.CreateInfo(oPPTInfoFrame.lSTSOrders);
            if (oPPTInfoFrame.lSTSCommands != null)
                this.CreateInfo(oPPTInfoFrame.lSTSCommands);
            if (oPPTInfoFrame.lSTSStatuses != null)
                this.CreateInfo(oPPTInfoFrame.lSTSStatuses);
            if (oPPTInfoFrame.lASCResJobs != null)
                this.CreateInfo(oPPTInfoFrame.lASCResJobs);
            if (oPPTInfoFrame.lASCTasks != null)
                this.CreateInfo(oPPTInfoFrame.lASCTasks);
            if (oPPTInfoFrame.lASCOrders != null)
                this.CreateInfo(oPPTInfoFrame.lASCOrders);
            if (oPPTInfoFrame.lASCCommands != null)
                this.CreateInfo(oPPTInfoFrame.lASCCommands);
            if (oPPTInfoFrame.lASCStatuses != null)
                this.CreateInfo(oPPTInfoFrame.lASCStatuses);
            if (oPPTInfoFrame.lAGVResJobs != null)
                this.CreateInfo(oPPTInfoFrame.lAGVResJobs);
            if (oPPTInfoFrame.lAGVTasks != null)
                this.CreateInfo(oPPTInfoFrame.lAGVTasks);
            if (oPPTInfoFrame.lAGVOrders != null)
                this.CreateInfo(oPPTInfoFrame.lAGVOrders);
            if (oPPTInfoFrame.lAGVCommands != null)
                this.CreateInfo(oPPTInfoFrame.lAGVCommands);
            if (oPPTInfoFrame.lAGVStatuses != null)
                this.CreateInfo(oPPTInfoFrame.lAGVStatuses);

            //  偏序表，创建等同于刷新
            if (oPPTInfoFrame.lContIDs != null && oPPTInfoFrame.OrderArray != null && oPPTInfoFrame.OrderArray.Length > 0 && oPPTInfoFrame.lOrderChecks != null)
                this.RenewInfo(oPPTInfoFrame.lContIDs, oPPTInfoFrame.OrderArray, oPPTInfoFrame.lOrderChecks);

        }

        /// <summary>
        /// 删除 Info
        /// </summary>
        /// <param name="oPPTInfoFrame">投射包</param>
        /// <param name="IsDeleteIncluded">删除包含在oPPTInfoFrame里的内容时为true（默认），删除不包含在InfoFrames里的内容时为false</param>
        public void DeleteInfos(ProjectPackageToInfoFrame oPPTInfoFrame, bool IsDeleteIncluded)
        {
            if (oPPTInfoFrame == null)
                return;

            if (oPPTInfoFrame.lWQs != null)
                this.DeleteInfo(oPPTInfoFrame.lWQs, IsDeleteIncluded);
            if (oPPTInfoFrame.lWIs != null)
                this.DeleteInfo(oPPTInfoFrame.lWIs, IsDeleteIncluded);
            if (oPPTInfoFrame.lBerthStatuses != null)
                this.DeleteInfo(oPPTInfoFrame.lBerthStatuses, IsDeleteIncluded);
            if (oPPTInfoFrame.lSTSResJobs != null)
                this.DeleteInfo(oPPTInfoFrame.lSTSResJobs, IsDeleteIncluded);
            if (oPPTInfoFrame.lSTSTasks != null)
                this.DeleteInfo(oPPTInfoFrame.lSTSTasks, IsDeleteIncluded);
            if (oPPTInfoFrame.lSTSOrders != null)
                this.DeleteInfo(oPPTInfoFrame.lSTSOrders, IsDeleteIncluded);
            if (oPPTInfoFrame.lSTSCommands != null)
                this.DeleteInfo(oPPTInfoFrame.lSTSCommands, IsDeleteIncluded);
            if (oPPTInfoFrame.lSTSStatuses != null)
                this.DeleteInfo(oPPTInfoFrame.lSTSStatuses, IsDeleteIncluded);
            if (oPPTInfoFrame.lASCResJobs != null)
                this.DeleteInfo(oPPTInfoFrame.lASCResJobs, IsDeleteIncluded);
            if (oPPTInfoFrame.lASCTasks != null)
                this.DeleteInfo(oPPTInfoFrame.lASCTasks, IsDeleteIncluded);
            if (oPPTInfoFrame.lASCOrders != null)
                this.DeleteInfo(oPPTInfoFrame.lASCOrders, IsDeleteIncluded);
            if (oPPTInfoFrame.lASCCommands != null)
                this.DeleteInfo(oPPTInfoFrame.lASCCommands, IsDeleteIncluded);
            if (oPPTInfoFrame.lASCStatuses != null)
                this.DeleteInfo(oPPTInfoFrame.lASCStatuses, IsDeleteIncluded);
            if (oPPTInfoFrame.lAGVResJobs != null)
                this.DeleteInfo(oPPTInfoFrame.lAGVResJobs, IsDeleteIncluded);
            if (oPPTInfoFrame.lAGVTasks != null)
                this.DeleteInfo(oPPTInfoFrame.lAGVTasks, IsDeleteIncluded);
            if (oPPTInfoFrame.lAGVOrders != null)
                this.DeleteInfo(oPPTInfoFrame.lAGVOrders, IsDeleteIncluded);
            if (oPPTInfoFrame.lAGVCommands != null)
                this.DeleteInfo(oPPTInfoFrame.lAGVCommands, IsDeleteIncluded);
            if (oPPTInfoFrame.lAGVStatuses != null)
                this.DeleteInfo(oPPTInfoFrame.lAGVStatuses, IsDeleteIncluded);

            // Delete不改变偏序表
        }

        /// <summary>
        /// 清除 Info
        /// </summary>
        public void ResetInfos()
        {
            this.DeleteInfos(new ProjectPackageToInfoFrame(), false);
        }

        /// <summary>
        /// 更新 Info，只更新本地和投射包里都有的内容
        /// </summary>
        /// <param name="oPPTInfoFrame">投射包</param>
        public void RenewInfos(ProjectPackageToInfoFrame oPPTInfoFrame)
        {
            if (oPPTInfoFrame == null)
                return;

            if (oPPTInfoFrame.lWQs != null)
                this.RenewInfo(oPPTInfoFrame.lWQs);
            if (oPPTInfoFrame.lWIs != null)
                this.RenewInfo(oPPTInfoFrame.lWIs);
            if (oPPTInfoFrame.lBerthStatuses != null)
                this.RenewInfo(oPPTInfoFrame.lBerthStatuses);
            if (oPPTInfoFrame.lSTSResJobs != null)
                this.RenewInfo(oPPTInfoFrame.lSTSResJobs);
            if (oPPTInfoFrame.lSTSTasks != null)
                this.RenewInfo(oPPTInfoFrame.lSTSTasks);
            if (oPPTInfoFrame.lSTSOrders != null)
                this.RenewInfo(oPPTInfoFrame.lSTSOrders);
            if (oPPTInfoFrame.lSTSCommands != null)
                this.RenewInfo(oPPTInfoFrame.lSTSCommands);
            if (oPPTInfoFrame.lSTSStatuses != null)
                this.RenewInfo(oPPTInfoFrame.lSTSStatuses);
            if (oPPTInfoFrame.lASCResJobs != null)
                this.RenewInfo(oPPTInfoFrame.lASCResJobs);
            if (oPPTInfoFrame.lASCTasks != null)
                this.RenewInfo(oPPTInfoFrame.lASCTasks);
            if (oPPTInfoFrame.lASCOrders != null)
                this.RenewInfo(oPPTInfoFrame.lASCOrders);
            if (oPPTInfoFrame.lASCCommands != null)
                this.RenewInfo(oPPTInfoFrame.lASCCommands);
            if (oPPTInfoFrame.lASCStatuses != null)
                this.RenewInfo(oPPTInfoFrame.lASCStatuses);
            if (oPPTInfoFrame.lAGVResJobs != null)
                this.RenewInfo(oPPTInfoFrame.lAGVResJobs);
            if (oPPTInfoFrame.lAGVTasks != null)
                this.RenewInfo(oPPTInfoFrame.lAGVTasks);
            if (oPPTInfoFrame.lAGVOrders != null)
                this.RenewInfo(oPPTInfoFrame.lAGVOrders);
            if (oPPTInfoFrame.lAGVCommands != null)
                this.RenewInfo(oPPTInfoFrame.lAGVCommands);
            if (oPPTInfoFrame.lAGVStatuses != null)
                this.RenewInfo(oPPTInfoFrame.lAGVStatuses);

            // 偏序表处理
            if (oPPTInfoFrame.lContIDs != null && oPPTInfoFrame.OrderArray != null && oPPTInfoFrame.OrderArray.Length > 0 && oPPTInfoFrame.lOrderChecks != null)
                this.RenewInfo(oPPTInfoFrame.lContIDs, oPPTInfoFrame.OrderArray, oPPTInfoFrame.lOrderChecks);
        }

        // 新建WQ
        private void CreateInfo(List<STS_WORK_QUEUE_STATUS> lWQs)
        {
            foreach (STS_WORK_QUEUE_STATUS oWQ in lWQs)
                if (!this.lWorkQueueInfos.Exists(u => u.WORK_QUEUE == oWQ.WORK_QUEUE))
                    this.lWorkQueueInfos.Add(new InfoWorkQueue(oWQ));
            this.RefreshListView(StatusEnums.ListViewType.WorkQueue);
        }

        // 新建 WI
        private void CreateInfo(List<WORK_INSTRUCTION_STATUS> lWIs)
        {
            foreach (WORK_INSTRUCTION_STATUS oWI in lWIs)
                if (!this.lWorkInstructionInfos.Exists(u => u.CONTAINER_ID == oWI.CONTAINER_ID))
                    this.lWorkInstructionInfos.Add(new InfoWorkInstruction(oWI));
            this.RefreshListView(StatusEnums.ListViewType.WorkInstruction);
        }

        // 新建 BerthStatus
        private void CreateInfo(List<BERTH_STATUS> lBSs)
        {
            foreach (BERTH_STATUS oBS in lBSs)
                if (!this.lBerthStatusInfos.Exists(u => u.VESSEL_NAME == oBS.VESSEL_NAME && u.VESSEL_VISIT == oBS.VESSEL_VISIT))
                    this.lBerthStatusInfos.Add(new InfoBerthStatus(oBS));
            this.RefreshListView(StatusEnums.ListViewType.BerthStatus);
        }

        // 新建 STSResJob
        private void CreateInfo(List<STS_ResJob> lRJs)
        {
            foreach (STS_ResJob oRJ in lRJs)
                if (!this.lSTSResJobInfos.Exists(u => u.ID == oRJ.ID))
                    this.lSTSResJobInfos.Add(new InfoSTSResJob(oRJ));
            this.RefreshListView(StatusEnums.ListViewType.STSResJob);
        }

        // 新建STSTask
        private void CreateInfo(List<STS_Task> lTasks)
        {
            foreach (STS_Task oT in lTasks)
                if (!this.lSTSTaskInfos.Exists(u => u.ID == oT.ID))
                    this.lSTSTaskInfos.Add(new InfoSTSTask(oT));
            this.RefreshListView(StatusEnums.ListViewType.STSTask);
        }

        // 新建STSOrder
        private void CreateInfo(List<STS_Order> lOrds)
        {
            foreach (STS_Order oOrd in lOrds)
                if (!this.lSTSOrderInfos.Exists(u => u.ORDER_ID == oOrd.ORDER_ID))
                    this.lSTSOrderInfos.Add(new InfoSTSOrder(oOrd));
            this.RefreshListView(StatusEnums.ListViewType.STSOrder);
        }

        // 新建STSCommand
        private void CreateInfo(List<STS_Command> lComms)
        {
            foreach (STS_Command oComm in lComms)
                if (!this.lSTSCommandInfos.Exists(u => u.COMMAND_ID == oComm.COMMAND_ID))
                    this.lSTSCommandInfos.Add(new InfoSTSCommand(oComm));
            this.RefreshListView(StatusEnums.ListViewType.STSCommand);
        }

        // 新建STSStatus
        private void CreateInfo(List<STS_STATUS> lStss)
        {
            foreach (STS_STATUS oSts in lStss)
                if (!this.lSTSStatusInfos.Exists(u => u.QC_ID == oSts.QC_ID))
                    this.lSTSStatusInfos.Add(new InfoSTSStatus(oSts));
            this.RefreshListView(StatusEnums.ListViewType.STSStatus);
        }

        // 新建 ASCResJob
        private void CreateInfo(List<ASC_ResJob> lRJs)
        {
            foreach (ASC_ResJob oRJ in lRJs)
                if (!this.lASCResJobInfos.Exists(u => u.ID == oRJ.ID))
                    this.lASCResJobInfos.Add(new InfoASCResJob(oRJ));
            this.RefreshListView(StatusEnums.ListViewType.ASCResJob);
        }

        // 新建ASCTask
        private void CreateInfo(List<ASC_Task> lTasks)
        {
            foreach (ASC_Task oT in lTasks)
                if (!this.lASCTaskInfos.Exists(u => u.ID == oT.ID))
                    this.lASCTaskInfos.Add(new InfoASCTask(oT));
            this.RefreshListView(StatusEnums.ListViewType.ASCTask);
        }

        // 新建ASCOrder
        private void CreateInfo(List<ASC_Order> lOrds)
        {
            foreach (ASC_Order oOrd in lOrds)
                if (!this.lASCOrderInfos.Exists(u => u.ORDER_ID == oOrd.ORDER_ID))
                    this.lASCOrderInfos.Add(new InfoASCOrder(oOrd));
            this.RefreshListView(StatusEnums.ListViewType.ASCOrder);
        }

        // 新建ASCCommand
        private void CreateInfo(List<ASC_Command> lComms)
        {
            foreach (ASC_Command oComm in lComms)
                if (!this.lASCCommandInfos.Exists(u => u.COMMAND_ID == oComm.COMMAND_ID))
                    this.lASCCommandInfos.Add(new InfoASCCommand(oComm));
            this.RefreshListView(StatusEnums.ListViewType.ASCCommand);
        }

        // 新建ASCStatus
        private void CreateInfo(List<ASC_STATUS> lStss)
        {
            foreach (ASC_STATUS oSts in lStss)
                if (!this.lASCStatusInfos.Exists(u => u.CHE_ID == oSts.CHE_ID))
                    this.lASCStatusInfos.Add(new InfoASCStatus(oSts));
            this.RefreshListView(StatusEnums.ListViewType.ASCStatus);
        }

        // 新建AGVResJob
        private void CreateInfo(List<AGV_ResJob> lRJs)
        {
            foreach (AGV_ResJob oRJ in lRJs)
                if (!this.lAGVResJobInfos.Exists(u => u.ID == oRJ.ID))
                    this.lAGVResJobInfos.Add(new InfoAGVResJob(oRJ));
            this.RefreshListView(StatusEnums.ListViewType.AGVResJob);
        }

        // 新建AGVTask
        private void CreateInfo(List<AGV_Task> lTasks)
        {
            foreach (AGV_Task oT in lTasks)
                if (!this.lAGVTaskInfos.Exists(u => u.ID == oT.ID))
                    this.lAGVTaskInfos.Add(new InfoAGVTask(oT));
            this.RefreshListView(StatusEnums.ListViewType.AGVTask);
        }

        // 新建AGVOrder
        private void CreateInfo(List<AGV_Order> lOrds)
        {
            foreach (AGV_Order oOrd in lOrds)
                if (!this.lAGVOrderInfos.Exists(u => u.ORDER_ID == oOrd.ORDER_ID))
                    this.lAGVOrderInfos.Add(new InfoAGVOrder(oOrd));
            this.RefreshListView(StatusEnums.ListViewType.AGVOrder);
        }

        // 新建AGVCommand
        private void CreateInfo(List<AGV_Command> lComms)
        {
            foreach (AGV_Command oComm in lComms)
                if (!this.lAGVCommandInfos.Exists(u => u.COMMAND_ID == oComm.COMMAND_ID))
                    this.lAGVCommandInfos.Add(new InfoAGVCommand(oComm));
            this.RefreshListView(StatusEnums.ListViewType.AGVCommand);
        }

        // 新建AGVStatus
        private void CreateInfo(List<AGV_STATUS> lStss)
        {
            foreach (AGV_STATUS oSts in lStss)
                if (!this.lAGVStatusInfos.Exists(u => u.CHE_ID == oSts.CHE_ID))
                    this.lAGVStatusInfos.Add(new InfoAGVStatus(oSts));
            this.RefreshListView(StatusEnums.ListViewType.AGVStatus);
        }

        // 新建PartialOrderTable



        // 删除WorkQueue
        private void DeleteInfo(List<STS_WORK_QUEUE_STATUS> lWQs, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lWorkQueueInfos.RemoveAll(u => lWQs.Exists(v => v.WORK_QUEUE == u.WORK_QUEUE));
            else
                DeleteNum = this.lWorkQueueInfos.RemoveAll(u => !lWQs.Exists(v => v.WORK_QUEUE == u.WORK_QUEUE));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.WorkQueue);
        }

        // 删除WorkInstruction
        private void DeleteInfo(List<WORK_INSTRUCTION_STATUS> lWIs, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lWorkInstructionInfos.RemoveAll(u => lWIs.Exists(v => v.CONTAINER_ID == u.CONTAINER_ID));
            else
                DeleteNum = this.lWorkInstructionInfos.RemoveAll(u => !lWIs.Exists(v => v.CONTAINER_ID == u.CONTAINER_ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.WorkInstruction);
        }

        // 删除BerthStatus
        private void DeleteInfo(List<BERTH_STATUS> lBSs, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lBerthStatusInfos.RemoveAll(u => lBSs.Exists(v => v.VESSEL_NAME == u.VESSEL_NAME && v.VESSEL_VISIT == u.VESSEL_VISIT));
            else
                DeleteNum = this.lBerthStatusInfos.RemoveAll(u => !lBSs.Exists(v => v.VESSEL_NAME == u.VESSEL_NAME && v.VESSEL_VISIT == u.VESSEL_VISIT));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.BerthStatus);
        }

        // 删除STS_ResJob
        private void DeleteInfo(List<STS_ResJob> lRJs, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lSTSResJobInfos.RemoveAll(u => lRJs.Exists(v => v.ID == u.ID));
            else
                DeleteNum = this.lSTSResJobInfos.RemoveAll(u => !lRJs.Exists(v => v.ID == u.ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.STSResJob);
        }

        // 删除STS_Task
        private void DeleteInfo(List<STS_Task> lTasks, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lSTSTaskInfos.RemoveAll(u => lTasks.Exists(v => v.ID == u.ID));
            else
                DeleteNum = this.lSTSTaskInfos.RemoveAll(u => !lTasks.Exists(v => v.ID == u.ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.STSTask);
        }

        // 删除STS_Order
        private void DeleteInfo(List<STS_Order> lOrds, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lSTSOrderInfos.RemoveAll(u => lOrds.Exists(v => v.ORDER_ID == u.ORDER_ID));
            else
                DeleteNum = this.lSTSOrderInfos.RemoveAll(u => !lOrds.Exists(v => v.ORDER_ID == u.ORDER_ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.STSOrder);
        }

        // 删除STS_Command
        private void DeleteInfo(List<STS_Command> lComms, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lSTSCommandInfos.RemoveAll(u => lComms.Exists(v => v.ORDER_ID == u.ORDER_ID));
            else
                DeleteNum = this.lSTSCommandInfos.RemoveAll(u => !lComms.Exists(v => v.ORDER_ID == u.ORDER_ID));
                 
            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.STSCommand);
        }

        // 删除STS_STATUS
        private void DeleteInfo(List<STS_STATUS> lStatuses, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lSTSStatusInfos.RemoveAll(u => lStatuses.Exists(v => v.QC_ID == u.QC_ID));
            else
                DeleteNum = this.lSTSStatusInfos.RemoveAll(u => !lStatuses.Exists(v => v.QC_ID == u.QC_ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.STSStatus);
        }

        // 删除ASC_ResJob
        private void DeleteInfo(List<ASC_ResJob> lRJs, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lASCResJobInfos.RemoveAll(u => lRJs.Exists(v => v.ID == u.ID));
            else
                DeleteNum = this.lASCResJobInfos.RemoveAll(u => !lRJs.Exists(v => v.ID == u.ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.ASCResJob);
        }

        // 删除ASC_Task
        private void DeleteInfo(List<ASC_Task> lTasks, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lASCTaskInfos.RemoveAll(u => lTasks.Exists(v => v.ID == u.ID));
            else
                DeleteNum = this.lASCTaskInfos.RemoveAll(u => !lTasks.Exists(v => v.ID == u.ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.ASCTask);
        }

        // 删除ASC_Order
        private void DeleteInfo(List<ASC_Order> lOrds, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lASCOrderInfos.RemoveAll(u => lOrds.Exists(v => v.ORDER_ID == u.ORDER_ID));
            else
                DeleteNum = this.lASCOrderInfos.RemoveAll(u => !lOrds.Exists(v => v.ORDER_ID == u.ORDER_ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.ASCOrder);
        }

        // 删除ASC_Command
        private void DeleteInfo(List<ASC_Command> lComms, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lASCCommandInfos.RemoveAll(u => lComms.Exists(v => v.COMMAND_ID == u.COMMAND_ID));
            else
                DeleteNum = this.lASCCommandInfos.RemoveAll(u => !lComms.Exists(v => v.COMMAND_ID == u.COMMAND_ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.ASCCommand);
        }

        // 删除ASC_STATUS
        private void DeleteInfo(List<ASC_STATUS> lStatuses, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lASCStatusInfos.RemoveAll(u => lStatuses.Exists(v => v.CHE_ID == u.CHE_ID));
            else
                DeleteNum = this.lASCStatusInfos.RemoveAll(u => !lStatuses.Exists(v => v.CHE_ID == u.CHE_ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.ASCStatus);
        }

        // 删除AGV_ResJob
        private void DeleteInfo(List<AGV_ResJob> lRJs, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lAGVResJobInfos.RemoveAll(u => lRJs.Exists(v => v.ID == u.ID));
            else
                DeleteNum = this.lAGVResJobInfos.RemoveAll(u => !lRJs.Exists(v => v.ID == u.ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.AGVResJob);
        }

        // 删除AGV_Task
        private void DeleteInfo(List<AGV_Task> lTasks, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lAGVTaskInfos.RemoveAll(u => lTasks.Exists(v => v.ID == u.ID));
            else
                DeleteNum = this.lAGVTaskInfos.RemoveAll(u => !lTasks.Exists(v => v.ID == u.ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.AGVTask);
        }

        // 删除AGV_Order
        private void DeleteInfo(List<AGV_Order> lOrds, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lAGVOrderInfos.RemoveAll(u => lOrds.Exists(v => v.ORDER_ID == u.ORDER_ID));
            else
                DeleteNum = this.lAGVOrderInfos.RemoveAll(u => !lOrds.Exists(v => v.ORDER_ID == u.ORDER_ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.AGVOrder);
        }

        // 删除AGV_Command
        private void DeleteInfo(List<AGV_Command> lComms, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lAGVCommandInfos.RemoveAll(u => lComms.Exists(v => v.COMMAND_ID == u.COMMAND_ID));
            else
                DeleteNum = this.lAGVCommandInfos.RemoveAll(u => !lComms.Exists(v => v.COMMAND_ID == u.COMMAND_ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.AGVCommand);
        }

        // 删除AGV_STATUS
        private void DeleteInfo(List<AGV_STATUS> lStatuses, bool IsDeleteIncluded)
        {
            int DeleteNum;

            if (IsDeleteIncluded)
                DeleteNum = this.lAGVStatusInfos.RemoveAll(u => lStatuses.Exists(v => v.CHE_ID == u.CHE_ID));
            else
                DeleteNum = this.lAGVStatusInfos.RemoveAll(u => !lStatuses.Exists(v => v.CHE_ID == u.CHE_ID));

            if (DeleteNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.AGVStatus);
        }

        // 更新WorkQueue
        private void RenewInfo(List<STS_WORK_QUEUE_STATUS> lWQs)
        {
            InfoWorkQueue IWQ;
            bool IsRenewed = false;

            foreach (STS_WORK_QUEUE_STATUS obj in lWQs)
            {
                IWQ = this.lWorkQueueInfos.FirstOrDefault(u => u.WORK_QUEUE == obj.WORK_QUEUE);
                if (IWQ != null)
                {
                    IWQ.Update(obj);
                    if (!IsRenewed)
                        IsRenewed = true;
                }
            }
            if (IsRenewed)
                this.RefreshListView(StatusEnums.ListViewType.WorkQueue);
        }

        // 更新WorkInstruction
        private void RenewInfo(List<WORK_INSTRUCTION_STATUS> lWIs)
        {
            InfoWorkInstruction IWI;
            bool IsRenewed = false;

            foreach (WORK_INSTRUCTION_STATUS obj in lWIs)
            {
                IWI = this.lWorkInstructionInfos.FirstOrDefault(u => u.CONTAINER_ID == obj.CONTAINER_ID);
                if (IWI != null)
                {
                    IWI.Update(obj);
                    if (!IsRenewed)
                        IsRenewed = true;
                }
            }
            if (IsRenewed)
                this.RefreshListView(StatusEnums.ListViewType.WorkInstruction);
        }

        // 更新BERTH_STATUS
        private void RenewInfo(List<BERTH_STATUS> lBSs)
        {
            InfoBerthStatus IBS;
            bool IsRenewed = false;
            
            foreach (BERTH_STATUS obj in lBSs)
            {
                IBS = this.lBerthStatusInfos.FirstOrDefault(u => u.VESSEL_NAME == obj.VESSEL_NAME &&  u.VESSEL_VISIT == obj.VESSEL_VISIT);
                if (IBS != null)
                {
                    IBS.Update(obj);
                    if (!IsRenewed)
                        IsRenewed = true;
                }
            }
            if (IsRenewed)
                this.RefreshListView(StatusEnums.ListViewType.BerthStatus);
        }

        // 更新STS_ResJob
        private void RenewInfo(List<STS_ResJob> lRJs)
        {
            InfoSTSResJob IRJ;
            int RenewNum = 0;
            
            foreach (STS_ResJob obj in lRJs)
            {
                IRJ = this.lSTSResJobInfos.FirstOrDefault(u => u.ID == obj.ID);
                if (IRJ != null)
                    RenewNum = RenewNum + IRJ.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.STSResJob);
        }

        // 更新STS_Task
        private void RenewInfo(List<STS_Task> lTasks)
        {
            InfoSTSTask ITask;
            int RenewNum = 0;
            
            foreach (STS_Task obj in lTasks)
            {
                ITask = this.lSTSTaskInfos.FirstOrDefault(u => u.ID == obj.ID);
                if (ITask != null)
                    RenewNum = RenewNum + ITask.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.STSTask);
        }

        // 更新STS_Order
        private void RenewInfo(List<STS_Order> lOrds)
        {
            InfoSTSOrder IOrd;
            int RenewNum = 0;
            
            foreach (STS_Order obj in lOrds)
            {
                IOrd = this.lSTSOrderInfos.FirstOrDefault(u => u.ORDER_ID == obj.ORDER_ID);
                if (IOrd != null)
                    RenewNum = RenewNum + IOrd.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.STSOrder);
        }

        // 更新STS_COMMAND
        private void RenewInfo(List<STS_Command> lComms)
        {
            InfoSTSCommand IComm;
            int RenewNum = 0;
            
            foreach (STS_Command obj in lComms)
            {
                IComm = this.lSTSCommandInfos.FirstOrDefault(u => u.COMMAND_ID == obj.COMMAND_ID);
                if (IComm != null)
                    RenewNum = RenewNum + IComm.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.STSCommand);
        }

        // 更新STS_STATUS
        private void RenewInfo(List<STS_STATUS> lStatuses)
        {
            InfoSTSStatus IStatus;
            int RenewNum = 0;
            
            foreach (STS_STATUS obj in lStatuses)
            {
                IStatus = this.lSTSStatusInfos.FirstOrDefault(u => u.QC_ID == obj.QC_ID);
                if (IStatus != null)
                    RenewNum = RenewNum + IStatus.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.STSStatus);
        }

        // 更新ASC_ResJob
        private void RenewInfo(List<ASC_ResJob> lRJs)
        {
            InfoASCResJob IRJ;
            int RenewNum = 0;
            
            foreach (ASC_ResJob obj in lRJs)
            {
                IRJ = this.lASCResJobInfos.FirstOrDefault(u => u.ID == obj.ID);
                if (IRJ != null)
                    RenewNum = RenewNum + IRJ.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.ASCResJob);
        }

        // 更新ASC_Task
        private void RenewInfo(List<ASC_Task> lTasks)
        {
            InfoASCTask ITask;
            int RenewNum = 0;
            
            foreach (ASC_Task obj in lTasks)
            {
                ITask = this.lASCTaskInfos.FirstOrDefault(u => u.ID == obj.ID);
                if (ITask != null)
                    RenewNum = ITask.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.ASCTask);
        }
        
        // 更新ASC_Order
        private void RenewInfo(List<ASC_Order> lOrds)
        {
            InfoASCOrder IOrd;
            int RenewNum = 0;
            
            foreach (ASC_Order obj in lOrds)
            {
                IOrd = this.lASCOrderInfos.FirstOrDefault(u => u.ORDER_ID == obj.ORDER_ID);
                if (IOrd != null)
                    RenewNum = RenewNum + IOrd.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.ASCOrder);
        }

        // 更新ASC_Command
        private void RenewInfo(List<ASC_Command> lComms)
        {
            InfoASCCommand IComm;
            int RenewNum = 0;
            
            foreach (ASC_Command obj in lComms)
            {
                IComm = this.lASCCommandInfos.FirstOrDefault(u => u.COMMAND_ID == obj.COMMAND_ID);
                if (IComm != null)
                    RenewNum = RenewNum + IComm.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.ASCCommand);
        }

        // 更新ASC_STATUS
        private void RenewInfo(List<ASC_STATUS> lStatuses)
        {
            InfoASCStatus IStatus;
            int RenewNum = 0;
            
            foreach (ASC_STATUS obj in lStatuses)
            {
                IStatus = this.lASCStatusInfos.FirstOrDefault(u => u.CHE_ID == obj.CHE_ID);
                if (IStatus != null)
                    RenewNum = RenewNum + IStatus.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.ASCStatus);
        }

        // 更新AGV_ResJob
        private void RenewInfo(List<AGV_ResJob> lRJs)
        {
            InfoAGVResJob IRJ;
            int RenewNum = 0;
            
            foreach (AGV_ResJob obj in lRJs)
            {
                IRJ = this.lAGVResJobInfos.FirstOrDefault(u => u.ID == obj.ID);
                if (IRJ != null)
                    RenewNum = RenewNum + IRJ.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.AGVResJob);
        }

        // 更新AGV_Task
        private void RenewInfo(List<AGV_Task> lTasks)
        {
            InfoAGVTask ITask;
            int RenewNum = 0;
            
            foreach (AGV_Task obj in lTasks)
            {
                ITask = this.lAGVTaskInfos.FirstOrDefault(u => u.ID == obj.ID);
                if (ITask != null)
                    RenewNum = RenewNum + ITask.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.AGVTask);
        }

        // 更新AGV_Order
        private void RenewInfo(List<AGV_Order> lOrds)
        {
            InfoAGVOrder IOrd;
            int RenewNum = 0;
            
            foreach (AGV_Order obj in lOrds)
            {
                IOrd = this.lAGVOrderInfos.FirstOrDefault(u => u.ORDER_ID == obj.ORDER_ID);
                if (IOrd != null)
                    RenewNum = RenewNum + IOrd.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.AGVOrder);
        }

        // 更新AGV_Command
        private void RenewInfo(List<AGV_Command> lComms)
        {
            InfoAGVCommand IComm;
            int RenewNum = 0;
            
            foreach (AGV_Command obj in lComms)
            {
                IComm = this.lAGVCommandInfos.FirstOrDefault(u => u.COMMAND_ID == obj.COMMAND_ID);
                if (IComm != null)
                    RenewNum = RenewNum + IComm.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.AGVCommand);
        }

        // 更新AGV_Status
        private void RenewInfo(List<AGV_STATUS> lStatuses)
        {
            InfoAGVStatus IStatus;
            int RenewNum = 0;
            
            foreach (AGV_STATUS obj in lStatuses)
            {
                IStatus = this.lAGVStatusInfos.FirstOrDefault(u => u.CHE_ID == obj.CHE_ID);
                if (IStatus != null)
                    RenewNum = RenewNum + IStatus.Update(obj);
            }
            if (RenewNum > 0)
                this.RefreshListView(StatusEnums.ListViewType.AGVStatus);
        }

        // 刷新 ListView
        private void RefreshListView(StatusEnums.ListViewType eListViewType)
        {
            switch (eListViewType)
            {
                case StatusEnums.ListViewType.WorkQueue:
                    if (this.TOS_SCH_WORK.IsSelected && this.lv_WorkQueue.Items.NeedsRefresh)
                        this.lv_WorkQueue.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.WorkInstruction:
                    if (this.TOS_SCH_WORK.IsSelected && this.lv_WorkInstruction.Items.NeedsRefresh)
                        this.lv_WorkInstruction.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.BerthStatus:
                    if (this.TOS_SCH_WORK.IsSelected && this.lv_BerthStatus.Items.NeedsRefresh)
                        this.lv_BerthStatus.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.STSResJob:
                    if (this.TOS_SCH_STS.IsSelected && this.lv_STSResJob.Items.NeedsRefresh)
                        this.lv_STSResJob.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.STSTask:
                    if (this.TOS_SCH_STS.IsSelected && this.lv_STSTask.Items.NeedsRefresh)
                        this.lv_STSTask.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.STSOrder:
                    if (this.SCH_ECS_STS.IsSelected && this.lv_STSOrder.Items.NeedsRefresh)
                        this.lv_STSOrder.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.STSCommand:
                    if (this.SCH_ECS_STS.IsSelected && this.lv_STSCommand.Items.NeedsRefresh)
                        this.lv_STSCommand.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.STSStatus:
                    if (this.SCH_ECS_STS.IsSelected && this.lv_STSStatus.Items.NeedsRefresh)
                        this.lv_STSStatus.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.ASCResJob:
                    if (this.TOS_SCH_ASC.IsSelected && this.lv_ASCResJob.Items.NeedsRefresh)
                        this.lv_ASCResJob.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.ASCTask:
                    if (this.TOS_SCH_ASC.IsSelected && this.lv_ASCTask.Items.NeedsRefresh)
                        this.lv_ASCTask.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.ASCOrder:
                    if (this.SCH_ECS_ASC.IsSelected && this.lv_ASCOrder.Items.NeedsRefresh)
                        this.lv_ASCOrder.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.ASCCommand:
                    if (this.SCH_ECS_ASC.IsSelected && this.lv_ASCCommand.Items.NeedsRefresh)
                        this.lv_ASCCommand.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.ASCStatus:
                    if (this.SCH_ECS_ASC.IsSelected && this.lv_ASCStatus.Items.NeedsRefresh)
                        this.lv_ASCStatus.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.AGVResJob:
                    if (this.TOS_SCH_AGV.IsSelected && this.lv_AGVResJob.Items.NeedsRefresh)
                        this.lv_AGVResJob.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.AGVTask:
                    if (this.TOS_SCH_AGV.IsSelected && this.lv_AGVTask.Items.NeedsRefresh)
                        this.lv_AGVTask.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.AGVOrder:
                    if (this.SCH_ECS_AGV.IsSelected && this.lv_AGVOrder.Items.NeedsRefresh)
                        this.lv_AGVOrder.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.AGVCommand:
                    if (this.SCH_ECS_AGV.IsSelected && this.lv_AGVCommand.Items.NeedsRefresh)
                        this.lv_AGVCommand.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.AGVStatus:
                    if (this.SCH_ECS_AGV.IsSelected && this.lv_AGVStatus.Items.NeedsRefresh)
                        this.lv_AGVStatus.Items.Refresh();
                    break;
                case StatusEnums.ListViewType.PartialOrder:
                    if (this.SCH_ORDERSEQ.IsSelected)
                        this.dg_OrderTable.Items.Refresh();
                    break;
                default:
                    break;
            }
        }


        // 从 WIList 和 OrderDecisionTable 到 dtOrderInfos
        public void RenewInfo(List<string> lContIDs, int[,] OrderArray, List<OrderCheck> lOC)
        {
            DataRow dr;

            // 得到数组维度
            int count = Convert.ToInt32(Math.Ceiling(Math.Pow(OrderArray.Length, 0.5)));

            if (count == 0)
                return;

            this.dtPartialOrderInfos.Clear();
            this.dtPartialOrderInfos.Columns.Clear();
            this.dtPartialOrderInfos.Rows.Clear();
            this.dg_OrderTable.Columns.Clear();

            // ID 列，ContID列和IfErr列
            this.AddBindedColumnsInPairs("ID");
            this.AddBindedColumnsInPairs("ContID");
            this.AddBindedColumnsInPairs("IfErr");

            for (int i = 0; i < count; i++)
                this.AddBindedColumnsInPairs((i + 1).ToString());

            for (int i = 0; i < count; i++)
            {
                dr = this.dtPartialOrderInfos.NewRow();
                
                dr[0] = i + 1;
                dr[1] = lContIDs[i];
                dr[2] = Convert.ToInt16(lOC.Find(u => u.ContID == lContIDs[i]).IfError);
                for (int j = 0; j < count; j++)
                    dr[j + 3] = OrderArray[i, j];

                this.dtPartialOrderInfos.Rows.Add(dr);
            }

            this.RefreshListView(StatusEnums.ListViewType.PartialOrder);
        }

        private void AddBindedColumnsInPairs(string HeadStr)
        {
            DataColumn oDC;
            DataGridTextColumn oDGTC;

            oDC = new DataColumn(HeadStr, Type.GetType("System.Int32"));
            oDGTC = new DataGridTextColumn() { Header = HeadStr };
            oDGTC.Binding = new Binding() { Path = new PropertyPath(HeadStr, null), Mode = BindingMode.OneWay };
            this.dtPartialOrderInfos.Columns.Add(oDC);
            this.dg_OrderTable.Columns.Add(oDGTC);
        }


        // SelectionChanged 事件触发对应逻辑，选择 TabItem 时刷新数据
        private void tabControl1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (Object obj in e.AddedItems)
            {
                TabItem oTI = obj as TabItem;
                if (oTI != null)
                {
                    switch (oTI.Name)
                    {
                        case "TOS_SCH_STS":
                            if (this.lv_STSResJob.Items.NeedsRefresh) 
                                this.lv_STSResJob.Items.Refresh();
                            if (this.lv_STSTask.Items.NeedsRefresh) 
                                this.lv_STSTask.Items.Refresh();
                            break;
                        case "TOS_SCH_ASC":
                            if (this.lv_ASCResJob.Items.NeedsRefresh)
                                this.lv_ASCResJob.Items.Refresh();
                            if (this.lv_ASCTask.Items.NeedsRefresh) 
                                this.lv_ASCTask.Items.Refresh();
                            break;
                        case "TOS_SCH_AGV":
                            if (this.lv_AGVResJob.Items.NeedsRefresh) 
                                this.lv_AGVResJob.Items.Refresh();
                            if (this.lv_AGVTask.Items.NeedsRefresh) 
                                this.lv_AGVTask.Items.Refresh();
                            break;
                        case "TOS_SCH_WORK":
                            if (this.lv_WorkQueue.Items.NeedsRefresh) 
                                this.lv_WorkQueue.Items.Refresh();
                            if (this.lv_WorkInstruction.Items.NeedsRefresh) 
                                this.lv_WorkInstruction.Items.Refresh();
                            if (this.lv_BerthStatus.Items.NeedsRefresh) 
                                this.lv_BerthStatus.Items.Refresh();
                            break;
                        case "SCH_ECS_STS":
                            if (this.lv_STSOrder.Items.NeedsRefresh) 
                                this.lv_STSOrder.Items.Refresh();
                            if (this.lv_STSCommand.Items.NeedsRefresh) 
                                this.lv_STSCommand.Items.Refresh();
                            if (this.lv_STSStatus.Items.NeedsRefresh) 
                                this.lv_STSStatus.Items.Refresh();
                            break;
                        case "SCH_ECS_ASC":
                            if (this.lv_ASCOrder.Items.NeedsRefresh) 
                                this.lv_ASCOrder.Items.Refresh();
                            if (this.lv_ASCCommand.Items.NeedsRefresh) 
                                this.lv_ASCCommand.Items.Refresh();
                            if (this.lv_ASCStatus.Items.NeedsRefresh) 
                                this.lv_ASCStatus.Items.Refresh();
                            break;
                        case "SCH_ECS_AGV":
                            if (this.lv_AGVOrder.Items.NeedsRefresh) 
                                this.lv_AGVOrder.Items.Refresh();
                            if (this.lv_AGVCommand.Items.NeedsRefresh) 
                                this.lv_AGVCommand.Items.Refresh();
                            if (this.lv_AGVStatus.Items.NeedsRefresh) 
                                this.lv_AGVStatus.Items.Refresh();
                            break;
                        case "SCH_ORDERSEQ":
                            //if (this.dg_OrderTable.Items.NeedsRefresh) 
                            this.dg_OrderTable.Items.Refresh();
                            break;
                        default:
                            Console.WriteLine("UnExpected TabItem {0}", oTI.Name);
                            break;
                    }
                }
            }
        }

        // 点击排序。尼玛这么高级
        private void Column_Click(object sender, RoutedEventArgs e)
        {
            ListView oLV = sender as ListView;
            GridViewColumn oGVC = (e.OriginalSource as GridViewColumnHeader).Column;

            if (oLV != null && oGVC != null)
            {
                //Get binding property of clicked column
                string bindingProperty = (oGVC.DisplayMemberBinding as Binding).Path.Path;
                SortDescriptionCollection sdc = oLV.Items.SortDescriptions;
                ListSortDirection sortDirection = ListSortDirection.Ascending;
                if (sdc.Count > 0)
                {
                    SortDescription sd = sdc[0];
                    sortDirection = (ListSortDirection)((((int)sd.Direction) + 1) % 2);
                    sdc.Clear();
                }
                sdc.Add(new SortDescription(bindingProperty, sortDirection));
            }
        }
    }
}
