using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using SSWPF.Define;
using System.Threading;

namespace SSWPF
{
    public class SimProjector
    {
        #region 定义

        // 声明投射用委托类型。
        delegate void SimPanelRenewDelegate(ProjectPackageToSimPanel oPPTSimPanel);
        delegate void ViewFrameCreateDelegate(ProjectPackageToViewFrame oPPTViewFrame);
        delegate void ViewFrameRenewDelegate(ProjectPackageToViewFrame oPPTViewFrame);
        delegate void ViewFrameDeleteDelegate(ProjectPackageToViewFrame oPPTViewFrame);
        delegate void ViewFrameRefreshDelegate(ProjectPackageToViewFrame oPPTViewFrame);
        delegate void ViewFrameResetDelegate();
        delegate void InfoFrameCreateDelegate(ProjectPackageToInfoFrame oPPTInfoFrame);
        delegate void InfoFrameRenewDelegate(ProjectPackageToInfoFrame oPPTInfoFrame);
        delegate void InfoFrameDeleteDelegate(ProjectPackageToInfoFrame oPPTInfoFrame, bool IsDeleteIncluded);
        delegate void InfoFrameRefreshDelegate(ProjectPackageToInfoFrame oPPTInfoFrame);
        delegate void InfoFrameResetDelegate();

        // 声明委托对象
        SimPanelRenewDelegate dRenewSimPanel;
        ViewFrameCreateDelegate dCreateInViewFrame;
        ViewFrameRenewDelegate dRenewInViewFrame;
        ViewFrameDeleteDelegate dDeleteInViewFrame;
        ViewFrameRefreshDelegate dRefreshInViewFrame;
        ViewFrameResetDelegate dResetInViewFrame;
        InfoFrameCreateDelegate dCreateInInfoFrame;
        InfoFrameRenewDelegate dRenewInInfoFrame;
        InfoFrameDeleteDelegate dDeleteInInfoFrame;
        InfoFrameRefreshDelegate dRefreshInInfoFrame;
        InfoFrameResetDelegate dResetInInfoFrame;
        
        // 投射窗口
        private ViewFrame oViewFrame;
        private SimPanel oSimPanel;
        private InfoFrame oInfoFrame;

        private Thread oViewFrameThread;
        private Thread oInfoFrameThread;

        // 参数
        public bool IfAnimation;
        public bool IfScheduleInfo;
        private SimConductor oSimConductor;
        public bool IsViewFrameConstructed;
        public bool IsInfoFrameConstructed;

        #endregion

        public SimProjector(SimConductor oSimConductor)
        {
            // 初始化窗口，挂上委托方法
            this.oSimConductor = oSimConductor;
            this.oSimPanel = oSimConductor.oSimPanel;

            // SimFrame窗口，单独开 UI 线程
            this.oViewFrameThread = new Thread(new ThreadStart(CreateViewFrameThread));
            this.oViewFrameThread.SetApartmentState(ApartmentState.STA);
            this.oViewFrameThread.Start();

            // InfoFrame窗口，单独开线程
            this.oInfoFrameThread = new Thread(new ThreadStart(CreateInfoFrameThread));
            this.oInfoFrameThread.SetApartmentState(ApartmentState.STA);
            this.oInfoFrameThread.Start();

            while (!this.IsInfoFrameConstructed || !this.IsViewFrameConstructed)
            {
                Thread.Sleep(50);
            }

            this.dCreateInViewFrame = this.oViewFrame.CreateInView;
            this.dRenewInViewFrame = this.oViewFrame.RenewInView;
            this.dDeleteInViewFrame = this.oViewFrame.DeleteInView;
            this.dResetInViewFrame = this.oViewFrame.ResetView;
            this.dRefreshInViewFrame = this.oViewFrame.RefreshInView;
            this.dCreateInInfoFrame = this.oInfoFrame.CreateInfos;
            this.dRenewInInfoFrame = this.oInfoFrame.RenewInfos;
            this.dDeleteInInfoFrame = this.oInfoFrame.DeleteInfos;
            this.dRefreshInInfoFrame = this.oInfoFrame.RefreshInfos;
            this.dResetInInfoFrame = this.oInfoFrame.ResetInfos;
            this.dRenewSimPanel = this.oSimPanel.RenewSimPanel;
        }


        // UI窗口子线程窗口启动逻辑
        private void CreateViewFrameThread()
        {
            this.oViewFrame = new ViewFrame();
            this.IsViewFrameConstructed = true;
            this.oViewFrame.Show();
            this.oViewFrame.Closed += (s, e) => this.oViewFrame.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            Dispatcher.Run();
        }

        // 调试窗口子线程启动逻辑
        private void CreateInfoFrameThread()
        {
            this.oInfoFrame = new InfoFrame();
            this.IsInfoFrameConstructed = true;
            this.oInfoFrame.Show();
            this.oInfoFrame.Closed += (s, e) => this.oInfoFrame.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            Dispatcher.Run();
        }


        /// <summary>
        /// 从 sender 到 ViewFrame 的投射程序
        /// </summary>
        /// <param name="sender">触发投射的对象</param>
        /// <param name="e">投射Arg</param>
        public void OnProjectToViewFrame(object sender, ProjectToViewFrameEventArgs e)
        {
            if (this.oViewFrame == null || !this.IsViewFrameConstructed || !this.IfAnimation)
                return;

            switch (e.eProjectType)
            {
                case StatusEnums.ProjectType.Create:
                    this.oViewFrame.Dispatcher.Invoke(this.dCreateInViewFrame, e.oPPTViewFrame);
                    break;
                case StatusEnums.ProjectType.Renew:
                    this.oViewFrame.Dispatcher.Invoke(this.dRenewInViewFrame, e.oPPTViewFrame);
                    break;
                case StatusEnums.ProjectType.Delete:
                    this.oViewFrame.Dispatcher.Invoke(this.dDeleteInViewFrame, e.oPPTViewFrame);
                    break;
                case StatusEnums.ProjectType.Refresh:
                    this.oViewFrame.Dispatcher.Invoke(this.dRefreshInInfoFrame, e.oPPTViewFrame);
                    break;
                case StatusEnums.ProjectType.Reset:
                    this.oViewFrame.Dispatcher.Invoke(this.dResetInViewFrame, null);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 从 sender 到 InfoFrame 的投射程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnProjectToInfoFrame(object sender, ProjectToInfoFrameEventArgs e)
        {
            if (this.oInfoFrame == null || !this.IsInfoFrameConstructed || !this.IfScheduleInfo)
                return;
            switch (e.eProjectType)
            {
                case StatusEnums.ProjectType.Create:
                    this.oInfoFrame.Dispatcher.Invoke(this.dCreateInInfoFrame, e.oPPTInfoFrame);
                    break;
                case StatusEnums.ProjectType.Renew:
                    this.oInfoFrame.Dispatcher.Invoke(this.dRenewInInfoFrame, e.oPPTInfoFrame);
                    break;
                case StatusEnums.ProjectType.Refresh:
                    this.oInfoFrame.Dispatcher.Invoke(this.dRefreshInInfoFrame, e.oPPTInfoFrame);
                    break;
                case StatusEnums.ProjectType.Delete:
                    this.oInfoFrame.Dispatcher.Invoke(this.dDeleteInInfoFrame, e.oPPTInfoFrame, true);
                    break;
                case StatusEnums.ProjectType.Reset:
                    this.oInfoFrame.Dispatcher.Invoke(this.dResetInInfoFrame);
                    break;
                default:
                    break;
            }
        }


        /// <summary>
        /// 从 sender 到 SimPanel 的投射程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnProjectToSimPanel(object sender, ProjectToSimPanelEventArgs e)
        {
            if (this.oSimPanel == null || e.oPPTSimPanel == null)
                return;

            this.oSimPanel.Dispatcher.Invoke(this.dRenewSimPanel, e.oPPTSimPanel);
        }



    }
}
