using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading;
using System.Linq;
using SharpSim;
using System.IO;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.Define;
using ZECS.Schedule.DB;
using ZECS.Schedule.ECSSchedule;
using ZECS.Schedule.VmsAlgoApplication;
using SSWPF.Define;
using SSWPF.SimManagers;
using solutionfordata;

namespace SSWPF
{
    /// <summary>
    /// 控制线程和时间的类
    /// </summary>
    public class SimConductor
    {
        #region 定义

        // Conductor 向 Projector 的事件，用于重置仿真界面
        public event EventHandler<ProjectToViewFrameEventArgs> ProjectToViewFrameEvent;
        public event EventHandler<ProjectToInfoFrameEventArgs> ProjectToInfoFrameEvent;
        public event EventHandler<ProjectToSimPanelEventArgs> ProjectToSimPanelEvent;

        // 配件句柄。必须有的配件，架构化
        public SimPanel oSimPanel;
        public SimProjector oSimProjector;
        public SimDataStore oSimDataStore;
        public RandomStepTester oScheduleTester;
        private ExcelInputter oExcelInputter;
        private SimBerthManager oSimBerthManager;
        private SimHandleLineManager oSimHandleLineManager;
        private SimQCManager oSimQCManager;
        private SimAGVManager oSimAGVManager;
        private SimASCManager oSimASCManager;
        public SimYardManager oSimYardManager;
        private SimTosManager oSimTosManager;
        private SimSimplifiedTrafficController oSimSimplifiedTrafficController;
        private SimOrderCommUnifier oSimOrdCommUnifier;
        private ECSSchedule oECSSchedule;

        // 测试配件
        public AGVTrafficTester oAGVTrafficTester;

        // 参数字段
        public StatusEnums.SimPhrase eSimPhrase;
        public DateTime realDtStart;
        public DateTime realDtlastStep;
        public bool IfRealTime;
        public double realTimeSpeedX;
        public int stepLengthInSeconds = 1;
        public string m_strProjectDirectory;
        public int CurrEventNo = 0;
        public int CurrTokenNo = 0;
        private int TestDayNum = 5;
        public int _nums = 0;
        public int iRowNum = 0;

        // SharpSim 定义
        public Simulation sim;

        private Event eRun;
        private Event eStep;
        private Event eTerminate;

        private Edge egRunStep;
        private Edge egStepStep;
        private Edge egStepTerminate;

        private SimToken oSimToken;

        private Thread oSimThread;

        #endregion

        #region 动作函数

        // 配件类实例化，子线程中开启显示界面窗口，并准备Logger
        public SimConductor(SimPanel oSimPanel)
        {
            this.eSimPhrase = StatusEnums.SimPhrase.None;
            this.oSimPanel = oSimPanel;

            // 基本参数
            this.m_strProjectDirectory = SchedulePath.GetActiveProjectDirectory();
            
            // 启动Logger（与Schedule一致）
            Logger.Config(this.m_strProjectDirectory + "\\log4net.config");

            // 生成Projector，打通事件
            this.oSimProjector = new SimProjector(this);
        }

        // 用于响应 SimPanel 操作
        public void OnSimPanelEvent(object sender, SimPanelToConductorEventArgs e)
        {
            if (e.IsAnimationChecked == true)
            {
                this.oSimProjector.IfAnimation = true;
                if (StatusEnums.IsInited(this.eSimPhrase))
                    this.RenewViewFrame();
            }
            else if (e.IsAnimationChecked == false)
                this.oSimProjector.IfAnimation = false;

            if (e.IsInformChecked == true)
            {
                this.oSimProjector.IfScheduleInfo = true;
                if (StatusEnums.IsInited(this.eSimPhrase))
                    this.RefreshScheduleInfo();
            }
            else if (e.IsInformChecked == false)
                this.oSimProjector.IfScheduleInfo = false;

            if (e.IsRealTimeChecked == true)
                this.IfRealTime = true;
            else if (e.IsRealTimeChecked == false)
                this.IfRealTime = false;

            if (e.NewSpeedValue != null)
                this.realTimeSpeedX = (double)e.NewSpeedValue;

            if (e.IsInitClicked)
                this.InitClicked();
            else if (e.IsStartClicked)
                this.StartClicked();
            else if (e.IsStepClicked)
                this.StepClicked();
            else if (e.IsStopClicked)
                this.StopClicked();
            else if (e.IsResetClicked)
                this.ResetClicked();
        }

        // 按钮 Init 的执行逻辑。有效的 Init 只有一种可能，从NONE到INITEND。若失败则到INITERROR
        private void InitClicked()
        {
            Solute SJK = new Solute();
            SJK.initSJK();
            SJK.DeleteAllData("agv");
            
            if (this.eSimPhrase != StatusEnums.SimPhrase.None)
                return;
            this.InitLogic();
        }

        // 有效的 Start 有两种可能：从 NONE 到 RUNNING，失败则到INITERROR；或者从 INITDONE 到 RUNNING，失败则到 RUNERROR。
        private void StartClicked()
        {
            if (this.eSimPhrase == StatusEnums.SimPhrase.None)
                this.InitLogic();
            if (this.eSimPhrase == StatusEnums.SimPhrase.InitDone)
                this.StartLogic(StatusEnums.SimPhrase.Running);
            else
            {
                switch (this.eSimPhrase)
                {
                    case StatusEnums.SimPhrase.Stepping:
                    case StatusEnums.SimPhrase.Stopped:
                    case StatusEnums.SimPhrase.Stopping:
                        this.eSimPhrase = StatusEnums.SimPhrase.Running;
                        break;
                    default:
                        break;
                }
            }
        }

        // 有效的 Reset 随时可以
        private void ResetClicked()
        {
            while (this.eSimPhrase == StatusEnums.SimPhrase.Initing)
                Thread.Sleep(50);
            if (this.eSimPhrase == StatusEnums.SimPhrase.InitDone || this.eSimPhrase == StatusEnums.SimPhrase.InitError
                || this.eSimPhrase == StatusEnums.SimPhrase.Terminated || this.eSimPhrase == StatusEnums.SimPhrase.RunError)
                this.ResetLogic();
            else if (this.eSimPhrase == StatusEnums.SimPhrase.Running || this.eSimPhrase == StatusEnums.SimPhrase.Stopped
                || this.eSimPhrase == StatusEnums.SimPhrase.Stepping)
            {
                this.eSimPhrase = StatusEnums.SimPhrase.Reseting;
                return;
            }
        }

        // 有效的 Stop 在 RUNNING 期间
        private void StopClicked()
        {
            if (this.eSimPhrase != StatusEnums.SimPhrase.Running)
                return;

            this.eSimPhrase = StatusEnums.SimPhrase.Stopping;
        }

        // 有效的 Step 仅在 Stopped 期间
        private void StepClicked()
        {
            if (this.eSimPhrase == StatusEnums.SimPhrase.None)
                this.InitLogic();
            switch (this.eSimPhrase)
            {
                case StatusEnums.SimPhrase.InitDone:
                    this.StartLogic(StatusEnums.SimPhrase.Stepping);
                    break;
                case StatusEnums.SimPhrase.Running:
                case StatusEnums.SimPhrase.Stopped:
                    this.eSimPhrase = StatusEnums.SimPhrase.Stepping;
                    break;
                default:
                    return;
            }
        }

        #endregion


        #region 逻辑函数

        // Init 部分的逻辑，可能被 InitClick 或者 StartClick 事件调用
        private bool InitLogic()
        {
            bool bRet;

            bRet = true;

            this.eSimPhrase = StatusEnums.SimPhrase.Initing;

            this.ProjectToViewFrameEvent += this.oSimProjector.OnProjectToViewFrame;
            this.ProjectToInfoFrameEvent += this.oSimProjector.OnProjectToInfoFrame;
            this.ProjectToSimPanelEvent += this.oSimProjector.OnProjectToSimPanel;

            // 结构初始化

            // 数据源
            this.oSimDataStore = new SimDataStore();

            // 调度类
            this.oECSSchedule = new ECSSchedule();

            // 配件载入
            this.oSimSimplifiedTrafficController = new SimSimplifiedTrafficController(this.oSimDataStore);
            this.oExcelInputter = new ExcelInputter(this.oSimDataStore, this.m_strProjectDirectory);
            this.oSimBerthManager = new SimBerthManager(this.oSimDataStore);
            this.oSimYardManager = new SimYardManager(this.oSimDataStore);
            this.oSimHandleLineManager = new SimHandleLineManager(this.oSimDataStore);
            this.oSimQCManager = new SimQCManager(this.oSimDataStore);
            this.oSimASCManager = new SimASCManager(this.oSimDataStore);
            this.oSimSimplifiedTrafficController = new SimSimplifiedTrafficController(this.oSimDataStore);
            this.oSimTosManager = new SimTosManager(this.oSimDataStore);
            this.oSimOrdCommUnifier = new SimOrderCommUnifier(this.oSimDataStore);
            this.oSimAGVManager = new SimAGVManager(this.oSimDataStore);

            // 测试模块
            this.oAGVTrafficTester = new AGVTrafficTester(this.oSimDataStore);
            
            // 静态类数据源指定
            DataAccess.oSimDataStore = this.oSimDataStore;
            VmsAlgoAdapter.oSimDataStore = this.oSimDataStore;
            DB_TOS.Instance.oSimDataStore = this.oSimDataStore;
            DB_ECS.Instance.oSimDataStore = this.oSimDataStore;
            SimStaticParas.SimDtStart = new DateTime(2016, 1, 1, 0, 0, 0);

            // 配件间事件收发关系建立
            this.oExcelInputter.ProjectToViewFrameEvent += this.oSimProjector.OnProjectToViewFrame;
            this.oExcelInputter.ProjectToInfoFrameEvent += this.oSimProjector.OnProjectToInfoFrame;
            this.oExcelInputter.PilesReclaimAndReleaseEvent += this.oSimYardManager.OnPileRecliamAndRelease;
            this.oSimBerthManager.ProjectToViewFrameEvent += this.oSimProjector.OnProjectToViewFrame;
            this.oSimBerthManager.ProjectToInfoFrameEvent += this.oSimProjector.OnProjectToInfoFrame;
            this.oSimHandleLineManager.ProjectToViewFrameEvent += this.oSimProjector.OnProjectToViewFrame;
            this.oSimHandleLineManager.ProjectToInfoFrameEvent += this.oSimProjector.OnProjectToInfoFrame;
            this.oSimQCManager.ProjectToViewFrameEvent += this.oSimProjector.OnProjectToViewFrame;
            this.oSimQCManager.ProjectToInfoFrameEvent += this.oSimProjector.OnProjectToInfoFrame;
            this.oSimASCManager.ProjectToViewFrameEvent += this.oSimProjector.OnProjectToViewFrame;
            this.oSimASCManager.ProjectToInfoFrameEvent += this.oSimProjector.OnProjectToInfoFrame;
            this.oSimAGVManager.AlloPlanLocsForDiscContsEvent += this.oSimTosManager.OnAlloPlanPlacsForDiscConts;
            this.oSimAGVManager.ProjectToViewFrameEvent += this.oSimProjector.OnProjectToViewFrame;
            this.oSimAGVManager.ProjectToInfoFrameEvent += this.oSimProjector.OnProjectToInfoFrame;
            this.oSimAGVManager.GenerateAGVRouteEvent += this.oSimSimplifiedTrafficController.OnGenerateAGVRoute;
            this.oSimYardManager.ProjectToViewFrameEvent += this.oSimProjector.OnProjectToViewFrame;
            this.oSimTosManager.PileReclaimAndReleaseEvent += this.oSimYardManager.OnPileRecliamAndRelease;
            this.oSimTosManager.ProjectToInfoFrameEvent += this.oSimProjector.OnProjectToInfoFrame;
            this.oSimSimplifiedTrafficController.ProjectToViewFrameEvent += this.oSimProjector.OnProjectToViewFrame;
            this.oSimSimplifiedTrafficController.ProjectToInfoFrameEvent += this.oSimProjector.OnProjectToInfoFrame;

            // 测试事件的收发关系
            this.oAGVTrafficTester.GenerateAGVRouteEvent += this.oSimSimplifiedTrafficController.OnGenerateAGVRoute;
            this.oAGVTrafficTester.ProjectToViewFrameEvent += this.oSimProjector.OnProjectToViewFrame;
            this.oAGVTrafficTester.ProjectToInfoFrameEvent += this.oSimProjector.OnProjectToInfoFrame;
            this.oAGVTrafficTester.DeleteAOLSEvent += this.oSimSimplifiedTrafficController.OnDeleteAOLS;
            this.oAGVTrafficTester.ResetAGVRoutesEvent += this.oSimSimplifiedTrafficController.OnResetAGVRoutes;

            // 信息初始化。初始化兼做事件的检查函数(待实现)

            // YardManager 初始化。注意在 ExcelInputter 前面
            if (bRet)
                bRet = this.oSimYardManager.Init();

            // 载入信息
            bRet = this.oExcelInputter.Init();
            if (bRet)
                bRet = this.oExcelInputter.LoadTerminalAndPlan();

            // TosManager 初始化
            if (bRet)
                bRet = this.oSimTosManager.Init();

            // BerthManager 初始化
            if (bRet)
                bRet = this.oSimBerthManager.Init();

            // QCManager 初始化
            if (bRet)
                bRet = this.oSimQCManager.Init();

            // ASCManager 初始化
            if (bRet)
                bRet = this.oSimASCManager.Init();

            // AGVManager 初始化
            if (bRet)
                bRet = this.oSimAGVManager.Init();

            // HandleLineManager 初始化
            if (bRet)
                bRet = this.oSimHandleLineManager.Init();

            // TrafficController 初始化
            if (bRet)
                bRet = this.oSimSimplifiedTrafficController.Init(false, false, false, false);

            // AGVTrafficTester 初始化
            if (bRet)
                bRet = this.oAGVTrafficTester.Init();

            if (bRet)
                this.eSimPhrase = StatusEnums.SimPhrase.InitDone;
            else
                this.eSimPhrase = StatusEnums.SimPhrase.InitError;

            // 改变 SimPanel 的显示状态，反应 Init 的结果
            this.ProjectToSimPanelEvent.Invoke(this, new ProjectToSimPanelEventArgs()
                {
                    oPPTSimPanel = new ProjectPackageToSimPanel()
                    {
                        eSimPhrase = this.eSimPhrase,
                        dtSimDateTime = SimStaticParas.SimDtStart
                    }
                });

            return bRet;
        }

        // Start执行逻辑
        private void StartLogic(StatusEnums.SimPhrase eSimPhrase)
        {
            this.sim = new Simulation(false, 1, false);

            this.eRun = new Event(this.CurrEventNo.ToString(), "Start", this.CurrEventNo++, 0);
            this.eStep = new Event(this.CurrEventNo.ToString(), "Step", this.CurrEventNo++);
            this.eTerminate = new Event(this.CurrEventNo.ToString(), "Termimnate", this.CurrEventNo++);

            this.eRun.EventExecuted += this.OnRunEvent;
            this.eStep.EventExecuted += this.OnStepEvent;
            this.eTerminate.EventExecuted += this.OnTerminateEvent;

            this.egRunStep = new Edge("dStartStep", eRun, eStep);
            this.egStepStep = new Edge("dStepStep", eStep, eStep);
            this.egStepTerminate = new Edge("dStepTerminate", eStep, eTerminate);

            this.egStepStep.interEventTime = this.stepLengthInSeconds;

            // QC 和 ASC 的 Event 触发时机由外部确定
            foreach (QCDT oQC in this.oSimDataStore.dQCs.Values)
            {
                // QC 的 eStep，以及MT，VT和各PT的eAction
                oQC.InitEvents(ref this.CurrEventNo);
                oQC.eStep.EventExecuted += this.oSimQCManager.OnQCStepEvent;
                oQC.MainTrolley.eAction.EventExecuted += this.oSimQCManager.OnQCMainTroActionEvent;
                oQC.ViceTrolley.eAction.EventExecuted += this.oSimQCManager.OnQCViceTroActionEvent;
                foreach (QCPlatformSlot oSlot in oQC.Platform)
                    oSlot.eAction.EventExecuted += this.oSimQCManager.OnPlatformSlotActionEvent;

                // 从外面的eStep到QC的eStep，以及其他
                oQC.gSimToStep = new Edge("QC:" + oQC.ID.ToString() + "SimToStep", this.eStep, oQC.eStep);
                oQC.InitEdges();
                oQC.InitTokens(ref this.CurrTokenNo);
                oQC.SetNextActionDateTime(SimStaticParas.SimDtStart);
            }

            this.realDtStart = DateTime.Now;
            Logger.Simulate.Info("Simulation Started at : " + DateTime.Now.ToString() + " For " + this.TestDayNum.ToString() + " Days");

            this.eSimPhrase = eSimPhrase;

            this.oSimThread = new Thread(new ThreadStart(this.sim.Run));
            this.oSimThread.SetApartmentState(ApartmentState.STA);
            this.oSimThread.Start();
        }

        // Reset执行逻辑
        private void ResetLogic()
        {
            if (StatusEnums.IsStarted(this.eSimPhrase))
            {
                // 停止仿真事件递进
                if (Edge.edgeList.Count > 0)
                    Edge.edgeList.All(u => u.condition = false);

                if (Simulation.tSim != null)
                {
                    Simulation.tSim.Abort();
                    Simulation.tSim = null;
                }
            }

            // 界面清除
            this.eSimPhrase = StatusEnums.SimPhrase.None;

            this.ProjectToSimPanelEvent.Invoke(this, new ProjectToSimPanelEventArgs()
                {
                    oPPTSimPanel = new ProjectPackageToSimPanel()
                    {
                        eSimPhrase = StatusEnums.SimPhrase.None,
                        dtSimDateTime = SimStaticParas.SimDtStart
                    }
                });
            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Reset,
                    oPPTViewFrame = new ProjectPackageToViewFrame()
                });
            this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Reset,
                    oPPTInfoFrame = new ProjectPackageToInfoFrame()
                });

            // 事件监听关系拆除（待完善）
            this.oExcelInputter.ProjectToViewFrameEvent -= this.oSimProjector.OnProjectToViewFrame;
            this.oExcelInputter.ProjectToInfoFrameEvent -= this.oSimProjector.OnProjectToInfoFrame;
            this.oExcelInputter.PilesReclaimAndReleaseEvent -= this.oSimYardManager.OnPileRecliamAndRelease;
            this.oSimBerthManager.ProjectToViewFrameEvent -= this.oSimProjector.OnProjectToViewFrame;
            this.oSimBerthManager.ProjectToInfoFrameEvent -= this.oSimProjector.OnProjectToInfoFrame;
            this.oSimHandleLineManager.ProjectToViewFrameEvent -= this.oSimProjector.OnProjectToViewFrame;
            this.oSimHandleLineManager.ProjectToInfoFrameEvent -= this.oSimProjector.OnProjectToInfoFrame;
            this.oSimQCManager.ProjectToViewFrameEvent -= this.oSimProjector.OnProjectToViewFrame;
            this.oSimQCManager.ProjectToInfoFrameEvent -= this.oSimProjector.OnProjectToInfoFrame;
            this.oSimASCManager.ProjectToViewFrameEvent -= this.oSimProjector.OnProjectToViewFrame;
            this.oSimASCManager.ProjectToInfoFrameEvent -= this.oSimProjector.OnProjectToInfoFrame;
            this.oSimAGVManager.AlloPlanLocsForDiscContsEvent -= this.oSimTosManager.OnAlloPlanPlacsForDiscConts;
            this.oSimAGVManager.ProjectToViewFrameEvent -= this.oSimProjector.OnProjectToViewFrame;
            this.oSimAGVManager.ProjectToInfoFrameEvent -= this.oSimProjector.OnProjectToInfoFrame;
            this.oSimAGVManager.GenerateAGVRouteEvent -= this.oSimSimplifiedTrafficController.OnGenerateAGVRoute;
            this.oSimYardManager.ProjectToViewFrameEvent -= this.oSimProjector.OnProjectToViewFrame;
            this.oSimTosManager.PileReclaimAndReleaseEvent -= this.oSimYardManager.OnPileRecliamAndRelease;
            this.oSimTosManager.ProjectToInfoFrameEvent -= this.oSimProjector.OnProjectToInfoFrame;
            this.oSimSimplifiedTrafficController.ProjectToViewFrameEvent -= this.oSimProjector.OnProjectToViewFrame;
            this.oSimSimplifiedTrafficController.ProjectToInfoFrameEvent -= this.oSimProjector.OnProjectToInfoFrame;
            this.ProjectToViewFrameEvent -= this.oSimProjector.OnProjectToViewFrame;
            this.ProjectToInfoFrameEvent -= this.oSimProjector.OnProjectToInfoFrame;
            this.ProjectToSimPanelEvent -= this.oSimProjector.OnProjectToSimPanel;


            // 测试器的事件关系拆除
            this.oAGVTrafficTester.GenerateAGVRouteEvent -= this.oSimSimplifiedTrafficController.OnGenerateAGVRoute;
            this.oAGVTrafficTester.ProjectToViewFrameEvent -= this.oSimProjector.OnProjectToViewFrame;
            this.oAGVTrafficTester.ProjectToInfoFrameEvent -= this.oSimProjector.OnProjectToInfoFrame;
            this.oAGVTrafficTester.DeleteAOLSEvent -= this.oSimSimplifiedTrafficController.OnDeleteAOLS;
            this.oAGVTrafficTester.ResetAGVRoutesEvent -= this.oSimSimplifiedTrafficController.OnResetAGVRoutes;

            // 配件卸载（待完善）
            this.oSimDataStore = null;
            this.oECSSchedule = null;
            this.oExcelInputter = null;
            this.oSimYardManager = null;
            this.oSimHandleLineManager = null;
            this.oSimQCManager = null;
            this.oSimSimplifiedTrafficController = null;
            this.oSimTosManager = null;
            this.oSimOrdCommUnifier = null;
            this.oSimAGVManager = null;

        }

        private void OnRunEvent(object obj1, EventInfoArgs e)
        {
            // 靠泊
            this.oSimBerthManager.BerthOneVessel();

            // 刷任务
            this.oSimTosManager.RenewTosOutput();

            // 岸桥更新任务组
            this.oSimQCManager.RefreshWQForQCs();

            // 刷作业线布置
            this.oSimHandleLineManager.RefreshPlans();

            // 调度开启，在初始化QC到Lane时间之后
            this.oECSSchedule.Start();

            Thread.Sleep(0);
            this.realDtlastStep = DateTime.Now;

            // 整体驱动
            oSimToken = new SimToken(this.CurrTokenNo++, "Stepper");
            this.egRunStep.attribute = oSimToken;
        }

        private void OnStepEvent(object obj, EventInfoArgs e)
        {
            while (this.eSimPhrase == StatusEnums.SimPhrase.Stopped)
            {
                Thread.Sleep(100);
            }
            if (this.eSimPhrase == StatusEnums.SimPhrase.Running || this.eSimPhrase == StatusEnums.SimPhrase.Stepping || this.eSimPhrase == StatusEnums.SimPhrase.Stopping)
            {
                if (this.eSimPhrase == StatusEnums.SimPhrase.Running && this.IfRealTime && this.realTimeSpeedX > 0)
                {
                    while ((DateTime.Now - this.realDtlastStep).TotalSeconds * this.realTimeSpeedX < (double)this.stepLengthInSeconds)
                        Thread.Sleep(50);
                }

                this.realDtlastStep = DateTime.Now;

                this.oAGVTrafficTester.AGVTrafficStepTest();

                this.oSimSimplifiedTrafficController.MoveAGVsInStep(this.stepLengthInSeconds);

                if (this.Endjudge())
                {
                    this.egStepTerminate.condition = true;
                    this.egStepStep.condition = false;
                    this.egStepTerminate.attribute = e.evnt.parameter;
                }
                else
                {
                    this.egStepTerminate.condition = false;
                    this.egStepStep.condition = true;
                    this.egStepStep.attribute = e.evnt.parameter;
                    if (this.eSimPhrase == StatusEnums.SimPhrase.Stepping || this.eSimPhrase == StatusEnums.SimPhrase.Stopping)
                        this.eSimPhrase = StatusEnums.SimPhrase.Stopped;
                }

                // 更新 SimPanel 面版
                this.ProjectToSimPanelEvent.Invoke(this, new ProjectToSimPanelEventArgs()
                    {
                        oPPTSimPanel = new ProjectPackageToSimPanel()
                        {
                            eSimPhrase = this.eSimPhrase,
                            dtSimDateTime = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock)
                        }
                    });
            }
            else if (this.eSimPhrase == StatusEnums.SimPhrase.Reseting)
            {
                this.ResetLogic();
            }
        }

        private void OnTerminateEvent(object obj1, EventInfoArgs e)
        {
            this.TrafficControlTestLog();

            // ECSSchedule 结束
            this.oECSSchedule.Stop();

            this.eSimPhrase = StatusEnums.SimPhrase.Terminated;

            this.ProjectToSimPanelEvent.Invoke(this, new ProjectToSimPanelEventArgs()
            {
                oPPTSimPanel = new ProjectPackageToSimPanel()
                {
                    eSimPhrase = this.eSimPhrase,
                    dtSimDateTime = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock)
                }
            });
        }

        // 向 InfoFrame 投射 TOS-SCH 接口。
        private void RefreshTOSInfo()
        {
            ProjectPackageToInfoFrame oPPTInfoFrame = new ProjectPackageToInfoFrame();

            oPPTInfoFrame.lBerthStatuses = new List<BERTH_STATUS>(this.oSimDataStore.dViewBerthStatus.Values);
            oPPTInfoFrame.lWQs = new List<STS_WORK_QUEUE_STATUS>(this.oSimDataStore.dViewWorkQueues.Values);
            oPPTInfoFrame.lWIs = new List<WORK_INSTRUCTION_STATUS>(this.oSimDataStore.dViewWorkInstructions.Values);
            oPPTInfoFrame.lSTSResJobs = new List<STS_ResJob>(this.oSimDataStore.dSTSResJobs.Values);
            oPPTInfoFrame.lSTSTasks = new List<STS_Task>(this.oSimDataStore.dSTSTasks.Values);
            oPPTInfoFrame.lASCResJobs = new List<ASC_ResJob>(this.oSimDataStore.dASCResJobs.Values);
            oPPTInfoFrame.lASCTasks = new List<ASC_Task>(this.oSimDataStore.dASCTasks.Values);
            oPPTInfoFrame.lAGVResJobs = new List<AGV_ResJob>(this.oSimDataStore.dAGVResJobs.Values);
            oPPTInfoFrame.lAGVTasks = new List<AGV_Task>(this.oSimDataStore.dAGVTasks.Values);

            this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs() 
            { 
                eProjectType = StatusEnums.ProjectType.Refresh,
                oPPTInfoFrame = oPPTInfoFrame 
            });
        }

        // 向 InfoFrame 投射 SCH-ECS 接口。
        private void RefreshScheduleInfo()
        {
            ProjectPackageToInfoFrame oPPTInfoFrame = new ProjectPackageToInfoFrame();

            oPPTInfoFrame.lSTSOrders = new List<STS_Order>(this.oSimDataStore.dSTSOrders.Values);
            oPPTInfoFrame.lSTSCommands = new List<STS_Command>(this.oSimDataStore.dSTSCommands.Values);
            oPPTInfoFrame.lASCOrders = new List<ASC_Order>(this.oSimDataStore.dASCOrders.Values);
            oPPTInfoFrame.lASCCommands = new List<ASC_Command>(this.oSimDataStore.dASCCommands.Values);
            oPPTInfoFrame.lAGVOrders = new List<AGV_Order>(this.oSimDataStore.dAGVOrders.Values);
            oPPTInfoFrame.lAGVCommands = new List<AGV_Command>(this.oSimDataStore.dAGVCommands.Values);
            oPPTInfoFrame.OrderArray = this.oSimDataStore.mOrderTable;
            oPPTInfoFrame.lContIDs = this.oSimDataStore.lWIContIDs;
            oPPTInfoFrame.lOrderChecks = this.oSimDataStore.lSortedOrderChecks;

            this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs() 
            { 
                eProjectType = StatusEnums.ProjectType.Refresh,
                oPPTInfoFrame = oPPTInfoFrame 
            });
        }

        // 向 InfoFrame 投射 设备的 Status
        private void RefreshStatusInfo()
        {
            ProjectPackageToInfoFrame oPPTInfoFrame = new ProjectPackageToInfoFrame();

            oPPTInfoFrame.lSTSStatuses = new List<STS_STATUS>(this.oSimDataStore.dSTSStatus.Values);
            oPPTInfoFrame.lASCStatuses = new List<ASC_STATUS>(this.oSimDataStore.dASCStatus.Values);
            oPPTInfoFrame.lAGVStatuses = new List<AGV_STATUS>(this.oSimDataStore.dAGVStatus.Values);

            this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs() 
            { 
                eProjectType = StatusEnums.ProjectType.Refresh,
                oPPTInfoFrame = oPPTInfoFrame 
            });
        }

        // 主动刷新 ViewFrame
        private void RenewViewFrame()
        {
            ProjectPackageToViewFrame oPPTViewFrame = new ProjectPackageToViewFrame();

            oPPTViewFrame.lAGVs = new List<AGV>(this.oSimDataStore.dAGVs.Values);
            oPPTViewFrame.lASCs = new List<ASC>(this.oSimDataStore.dASCs.Values);
            oPPTViewFrame.lLanes = new List<Lane>(this.oSimDataStore.dLanes.Values);
            oPPTViewFrame.lMates = new List<Mate>(this.oSimDataStore.dMates.Values);
            oPPTViewFrame.lPiles = new List<Pile>(this.oSimDataStore.dPiles.Values);
            oPPTViewFrame.lQCs = new List<QCDT>(this.oSimDataStore.dQCs.Values);
            oPPTViewFrame.lTPs = new List<SimTransponder>(this.oSimDataStore.dTransponders.Values);

            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs() 
            { 
                eProjectType = StatusEnums.ProjectType.Renew,
                oPPTViewFrame = oPPTViewFrame
            });
        }

        private void TrafficControlTestLog()
        {
            Logger.Simulate.Info("Simulation Terminated at : " + DateTime.Now.ToString());
            Logger.Simulate.Info("Total time used in Seconds : " + new TimeSpan(DateTime.Now.Ticks).Subtract(new TimeSpan(this.realDtStart.Ticks)).Duration().TotalSeconds.ToString());
            List<double> lTest = this.oSimSimplifiedTrafficController.lDeadlockDetectTimes.OrderBy(u => u).ToList();
            if (lTest.Count > 0)
            {
                Logger.Simulate.Info("DeadLock Detection Times : " + lTest.Count.ToString());
                Logger.Simulate.Info("Max DeadLock Detection Time : " + lTest.Last());
                Logger.Simulate.Info("Avg DeadLock Detection Time : " + lTest.Average());
                Logger.Simulate.Info("Mid DeadLock Detection Time : " + lTest[lTest.Count / 2]);
            }
        }

        public bool Endjudge()
        {
            if (Simulation.clock >= this.TestDayNum * 24 * 3600)
                return true;
            return false;
        }

        #endregion

    }

}