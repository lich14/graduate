using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using SSWPF.Define;

namespace SSWPF
{
    
    /// <summary>
    /// SimPanel.xaml 的交互逻辑
    /// </summary>
    public partial class SimPanel : Window
    {
        SimConductor oSimConductor;
        public event EventHandler<SimPanelToConductorEventArgs> SimPanelToConductorEvent;
        public StatusEnums.SimPhrase eSimPhraseLocal;

        public SimPanel()
        {
            SimPanelToConductorEventArgs e;
            this.eSimPhraseLocal = StatusEnums.SimPhrase.None;

            InitializeComponent();
            this.oSimConductor = new SimConductor(this);

            this.SimPanelToConductorEvent += this.oSimConductor.OnSimPanelEvent;
            this.RenewSimPhrase(StatusEnums.SimPhrase.None);

            e = new SimPanelToConductorEventArgs()
            {
                IsAnimationChecked = this.cb_IfAnimation.IsChecked,
                IsRealTimeChecked = this.cb_IfRealTime.IsChecked,
                IsInformChecked = this.cb_IfInform.IsChecked,
                NewSpeedValue = this.s_SpeedX.Value
            };

            this.SimPanelToConductorEvent.Invoke(this, e);
        }

        private void b_Start_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsCurrClickValid(StatusEnums.ClickType.Start))
            {
                if (this.eSimPhraseLocal == StatusEnums.SimPhrase.None)
                {
                    this.RenewSimPhrase(StatusEnums.SimPhrase.Initing);
                    if (this.SimPanelToConductorEvent != null)
                        this.SimPanelToConductorEvent.BeginInvoke(this, new SimPanelToConductorEventArgs() { IsStartClicked = true }, null, null);
                }
                else
                {
                    this.RenewSimPhrase(StatusEnums.SimPhrase.Running);
                    if (this.SimPanelToConductorEvent != null)
                        this.SimPanelToConductorEvent.Invoke(this, new SimPanelToConductorEventArgs() { IsStartClicked = true });
                }
            }
        }

        private void b_Stop_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsCurrClickValid(StatusEnums.ClickType.Stop))
            {
                this.RenewSimPhrase(StatusEnums.SimPhrase.Stopping);
                if (this.SimPanelToConductorEvent != null)
                    this.SimPanelToConductorEvent.Invoke(this, new SimPanelToConductorEventArgs() { IsStopClicked = true });
            }
        }

        private void b_Reset_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsCurrClickValid(StatusEnums.ClickType.Reset))
            {
                this.RenewSimPhrase(StatusEnums.SimPhrase.Reseting);
                if (this.SimPanelToConductorEvent != null)
                    this.SimPanelToConductorEvent.Invoke(this, new SimPanelToConductorEventArgs() { IsResetClicked = true });
            }
        }

        private void b_Init_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsCurrClickValid(StatusEnums.ClickType.Init))
            {
                this.RenewSimPhrase(StatusEnums.SimPhrase.Initing);
                this.SimPanelToConductorEvent.BeginInvoke(this, new SimPanelToConductorEventArgs() { IsInitClicked = true }, null, null);
            }
        }

        private void b_Step_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsCurrClickValid(StatusEnums.ClickType.Step))
            {
                this.RenewSimPhrase(StatusEnums.SimPhrase.Stepping);
                if (this.SimPanelToConductorEvent != null)
                    this.SimPanelToConductorEvent.Invoke(this, new SimPanelToConductorEventArgs() { IsStepClicked = true });
            }
        }

        private void cb_IfRealTime_Checked(object sender, RoutedEventArgs e)
        {
            if (this.SimPanelToConductorEvent != null && StatusEnums.IsInited(this.eSimPhraseLocal))
                this.SimPanelToConductorEvent.Invoke(this, new SimPanelToConductorEventArgs() { IsRealTimeChecked = true });
        }

        private void cb_IfRealTime_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.SimPanelToConductorEvent != null && StatusEnums.IsInited(this.eSimPhraseLocal))
                this.SimPanelToConductorEvent.Invoke(this, new SimPanelToConductorEventArgs() { IsRealTimeChecked = false });
        }

        private void s_SpeedX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.SimPanelToConductorEvent != null && StatusEnums.IsInited(this.eSimPhraseLocal))
                this.SimPanelToConductorEvent.Invoke(this, new SimPanelToConductorEventArgs() { NewSpeedValue = e.NewValue });
        }

        private void cb_IfAnimation_Checked(object sender, RoutedEventArgs e)
        {
            if (this.SimPanelToConductorEvent != null && StatusEnums.IsInited(this.eSimPhraseLocal))
                this.SimPanelToConductorEvent.Invoke(this, new SimPanelToConductorEventArgs() { IsAnimationChecked = true });
        }

        private void cb_IfAnimation_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.SimPanelToConductorEvent != null && StatusEnums.IsInited(this.eSimPhraseLocal))
                this.SimPanelToConductorEvent.Invoke(this, new SimPanelToConductorEventArgs() { IsAnimationChecked = false });
        }

        private void cb_IfInform_Checked(object sender, RoutedEventArgs e)
        {
            if (this.SimPanelToConductorEvent != null && StatusEnums.IsInited(this.eSimPhraseLocal))
                this.SimPanelToConductorEvent.Invoke(this, new SimPanelToConductorEventArgs() { IsInformChecked = true });
        }

        private void cb_IfInform_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.SimPanelToConductorEvent != null && StatusEnums.IsInited(this.eSimPhraseLocal))
                this.SimPanelToConductorEvent.Invoke(this, new SimPanelToConductorEventArgs() { IsAnimationChecked = false });
        }

        /// <summary>
        /// 前端按钮点击过滤
        /// </summary>
        /// <param name="eClickType">点击按钮指示</param>
        /// <returns>有效返回true，无效返回false</returns>
        private bool IsCurrClickValid(StatusEnums.ClickType eClickType)
        {
            switch (this.eSimPhraseLocal)
            {
                case StatusEnums.SimPhrase.None:
                    switch (eClickType)
                    {
                        case StatusEnums.ClickType.Init:
                        case StatusEnums.ClickType.Start:
                        case StatusEnums.ClickType.Step:
                        case StatusEnums.ClickType.Reset:
                            return true;
                        default:
                            break;
                    }
                    break;
                case StatusEnums.SimPhrase.InitDone:
                    switch (eClickType)
                    {
                        case StatusEnums.ClickType.Start:
                        case StatusEnums.ClickType.Step:
                        case StatusEnums.ClickType.Reset:
                            return true;
                        default:
                            break;
                    }
                    break;
                case StatusEnums.SimPhrase.InitError:
                case StatusEnums.SimPhrase.RunError:
                case StatusEnums.SimPhrase.Terminated:
                    switch (eClickType)
                    {
                        case StatusEnums.ClickType.Reset:
                            return true;
                        default:
                            break;
                    }
                    break;
                case StatusEnums.SimPhrase.Running:
                    switch (eClickType)
                    {
                        case StatusEnums.ClickType.Step:
                        case StatusEnums.ClickType.Stop:
                        case StatusEnums.ClickType.Reset:
                            return true;
                        default:
                            break;
                    }
                    break;
                case StatusEnums.SimPhrase.Stepping:
                case StatusEnums.SimPhrase.Stopping:
                    switch (eClickType)
                    {
                        case StatusEnums.ClickType.Start:
                        case StatusEnums.ClickType.Reset:
                            return true;
                        default:
                            break;
                    }
                    break;
                case StatusEnums.SimPhrase.Stopped:
                    switch (eClickType)
                    {
                        case StatusEnums.ClickType.Start:
                        case StatusEnums.ClickType.Step:
                        case StatusEnums.ClickType.Reset:
                            return true;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }

            return false;
        }

        public void RenewSimPanel(ProjectPackageToSimPanel oPPTSimPanel)
        {
            if (oPPTSimPanel == null)
                return;

            this.RenewSimPhrase(oPPTSimPanel.eSimPhrase);
            this.RenewSimTime(oPPTSimPanel.dtSimDateTime);
        }

        private void RenewSimTime(DateTime dtSim)
        {
            this.l_SimTime.Content = dtSim.ToString();
        }

        private void RenewSimPhrase(StatusEnums.SimPhrase eSimPhrase)
        {
            if (this.eSimPhraseLocal != eSimPhrase)
            {
                this.eSimPhraseLocal = eSimPhrase;
                this.l_SimPhrase.Content = eSimPhrase.ToString();
            }
        }

    }
}
