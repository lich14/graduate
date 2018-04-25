using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Linq;
using SSWPF.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.YardMap;

namespace SSWPF
{
    /// <summary>
    /// 指示岸桥小车的状态，显示时边框指示专用
    /// </summary>
    public enum TroActionType : byte { Null = 0, Move = 1, Wait = 2, Rise = 3, Fall = 4 }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ViewFrame : Window
    {
        private decimal ratio = 2.5M;
        private Canvas oSimCanvas;

        // 可变元素注册处
        private Dictionary<string, Rectangle> dChangableRectangles = new Dictionary<string, Rectangle>();
        private Dictionary<string, Ellipse> dChangableEllipses = new Dictionary<string, Ellipse>();
        private Dictionary<string, Canvas> dChangableCanvases = new Dictionary<string, Canvas>();
        private Dictionary<string, Label> dChangableLabels = new Dictionary<string, Label>();
        private Dictionary<uint, Line> dChangableLines = new Dictionary<uint, Line>();

        // 颜色字典
        private Dictionary<string, Color> dMotionStatusColors = new Dictionary<string, Color>();
        private Dictionary<string, Color> dMoveKindColors = new Dictionary<string, Color>();
        private Dictionary<string, Color> dStoreTypeColors = new Dictionary<string, Color>();
        private Dictionary<string, Color> dLaneStatusColors = new Dictionary<string, Color>();
        private Dictionary<string, Color> dLaneAttrColors = new Dictionary<string, Color>();
        private Dictionary<string, Color> dTierColors = new Dictionary<string, Color>();
        
        // 所有磁钉位置字典，定位用
        private Dictionary<uint, Point> dTPPoints = new Dictionary<uint, Point>();

        public ViewFrame()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // oSimCanvas 默认一直生成，仅在合适的时机调整大小
            this.oSimCanvas = new Canvas();
            this.oSimCanvas.Name = "SimCanvas";
            this.oSimCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            this.oSimCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            this.MainGrid.Children.Add(this.oSimCanvas);
        }

        /// <summary>
        /// 在ViewFrame中新建对象
        /// </summary>
        /// <param name="oPPTViewFrame">投射包</param>
        public void CreateInView(ProjectPackageToViewFrame oPPTViewFrame)
        {
            if (oPPTViewFrame.oTR != null)
                this.CreateTerminalRegion(oPPTViewFrame.oTR);
            if (oPPTViewFrame.dColorDics != null && oPPTViewFrame.dColorDics.Count > 0)
                foreach (string sKey in oPPTViewFrame.dColorDics.Keys)
                    this.LoadColorDictionary(sKey, oPPTViewFrame.dColorDics[sKey]);
            if (oPPTViewFrame.lBlocks != null && oPPTViewFrame.lBlocks.Count > 0)
                this.CreateBlocks(oPPTViewFrame.lBlocks);
            if (oPPTViewFrame.lTPs != null && oPPTViewFrame.lTPs.Count > 0)
                this.CreateTransponders(oPPTViewFrame.lTPs);
            if (oPPTViewFrame.lAGVLines != null && oPPTViewFrame.lAGVLines.Count > 0)
                this.CreateAGVLines(oPPTViewFrame.lAGVLines);
            if (oPPTViewFrame.lLanes != null && oPPTViewFrame.lLanes.Count > 0)
                this.CreateLanes(oPPTViewFrame.lLanes);
            if (oPPTViewFrame.lMates != null && oPPTViewFrame.lMates.Count > 0)
                this.CreateMates(oPPTViewFrame.lMates);
            if (oPPTViewFrame.lAGVs != null && oPPTViewFrame.lAGVs.Count > 0)
                this.CreateAGVs(oPPTViewFrame.lAGVs);
            if (oPPTViewFrame.lQCs != null && oPPTViewFrame.lQCs.Count > 0)
                this.CreateQCs(oPPTViewFrame.lQCs);
            if (oPPTViewFrame.lASCs != null && oPPTViewFrame.lASCs.Count > 0)
                this.CreateASCs(oPPTViewFrame.lASCs);
            if (oPPTViewFrame.lPiles != null && oPPTViewFrame.lPiles.Count > 0)
                this.CreatePiles(oPPTViewFrame.lPiles);
            if (oPPTViewFrame.lBerthVessels != null && oPPTViewFrame.lBerthVessels.Count > 0)
                this.CreateVessels(oPPTViewFrame.lBerthVessels);
        }

        /// <summary>
        /// 在 ViewFrame 中更新指定的对象
        /// </summary>
        /// <param name="oPPTViewFrame">投射包，包含刷新对象的类型列表。没包含的对象不刷新。</param>
        public void RenewInView(ProjectPackageToViewFrame oPPTViewFrame)
        {
            // 暂时只有以下项需要刷新
            if (oPPTViewFrame.lAGVs != null && oPPTViewFrame.lAGVs.Count > 0)
                this.RenewAGVs(oPPTViewFrame.lAGVs);
            if (oPPTViewFrame.lASCs != null && oPPTViewFrame.lASCs.Count > 0)
                this.RenewASCs(oPPTViewFrame.lASCs);
            if (oPPTViewFrame.lLanes != null && oPPTViewFrame.lLanes.Count > 0)
                this.RenewLanes(oPPTViewFrame.lLanes);
            if (oPPTViewFrame.lMates != null && oPPTViewFrame.lMates.Count > 0)
                this.RenewMates(oPPTViewFrame.lMates);
            if (oPPTViewFrame.lPiles != null && oPPTViewFrame.lPiles.Count > 0)
                this.RenewPiles(oPPTViewFrame.lPiles);
            if (oPPTViewFrame.lQCs != null && oPPTViewFrame.lQCs.Count > 0)
                this.RenewQCs(oPPTViewFrame.lQCs);
            if (oPPTViewFrame.lTPs != null && oPPTViewFrame.lTPs.Count > 0)
                this.RenewTransponders(oPPTViewFrame.lTPs);
            if (oPPTViewFrame.lAGVLines != null && oPPTViewFrame.lAGVLines.Count > 0)
                this.RenewAGVLines(oPPTViewFrame.lAGVLines);
            if (oPPTViewFrame.lBerthVessels != null && oPPTViewFrame.lBerthVessels.Count > 0)
                this.RenewVessels(oPPTViewFrame.lBerthVessels);
        }

        /// <summary>
        /// 在 ViewFrame 中删除指定的对象
        /// </summary>
        /// <param name="oPPTViewFrame"></param>
        public void DeleteInView(ProjectPackageToViewFrame oPPTViewFrame)
        {
            // Refresh 时候的 Delete 项拿不到原对象，因此要用Name和ID列表
            List<string> lDeleteNames, lDeleteNames2;
            List<uint> lDeleteIDs;

            if (oPPTViewFrame.oTR != null)
                this.DeleteTerminalRegion();
            if (oPPTViewFrame.dColorDics != null && oPPTViewFrame.dColorDics.Count > 0)
                foreach (string sKey in oPPTViewFrame.dColorDics.Keys)
                    this.DeleteColorDictionary(sKey);
            if (oPPTViewFrame.lBlocks != null && oPPTViewFrame.lBlocks.Count > 0)
                this.DeleteBlocks(oPPTViewFrame.lBlocks);
            if (oPPTViewFrame.lAGVLines != null && oPPTViewFrame.lAGVLines.Count > 0)
                this.DeleteAGVLines(oPPTViewFrame.lAGVLines);
            if (oPPTViewFrame.lPiles != null && oPPTViewFrame.lPiles.Count > 0)
            {
                lDeleteNames = oPPTViewFrame.lPiles.Select(u => u.Name).ToList();
                this.DeletePiles(lDeleteNames);
            }
            if (oPPTViewFrame.lAGVs != null && oPPTViewFrame.lAGVs.Count > 0)
            {
                lDeleteNames = oPPTViewFrame.lAGVs.Select(u => u.Name).ToList();
                this.DeleteAGVs(lDeleteNames);
            }
            if (oPPTViewFrame.lTPs != null && oPPTViewFrame.lTPs.Count > 0)
            {
                lDeleteNames = oPPTViewFrame.lTPs.Select(u => u.Name).ToList();
                lDeleteIDs = oPPTViewFrame.lTPs.Select(u => u.ID).ToList();
                this.DeleteTransponders(lDeleteNames, lDeleteIDs);
            }
            if (oPPTViewFrame.lLanes != null && oPPTViewFrame.lLanes.Count > 0)
            {
                lDeleteNames = oPPTViewFrame.lLanes.Select(u => u.Name).ToList();
                lDeleteNames2 = oPPTViewFrame.lLanes.Where(u => u.oDirSign != null).Select(u => u.oDirSign.Name).ToList();
                this.DeleteLanes(lDeleteNames, lDeleteNames2);
            }
            if (oPPTViewFrame.lMates != null && oPPTViewFrame.lMates.Count > 0)
            {
                lDeleteNames = oPPTViewFrame.lMates.Select(u => u.Name).ToList();
                this.DeleteMates(lDeleteNames);
            }
            if (oPPTViewFrame.lQCs != null && oPPTViewFrame.lQCs.Count > 0)
            {
                lDeleteNames = oPPTViewFrame.lQCs.Select(u => u.Name).ToList();
                this.DeleteQCs(lDeleteNames);
            }
            if (oPPTViewFrame.lASCs != null && oPPTViewFrame.lASCs.Count > 0)
            {
                lDeleteNames = oPPTViewFrame.lASCs.Select(u => u.Name).ToList();
                this.DeleteASCs(lDeleteNames);
            }
            if (oPPTViewFrame.lBerthVessels != null && oPPTViewFrame.lBerthVessels.Count > 0)
            {
                lDeleteNames = oPPTViewFrame.lBerthVessels.Select(u => u.Name).ToList();
                this.DeleteVessels(lDeleteNames);
            }
        }

        /// <summary>
        /// 在 ViewFrame 中刷新指定对象，无则添，有则更新，多则删去
        /// </summary>
        /// <param name="oPPTViewFrame"></param>
        public void RefreshInView(ProjectPackageToViewFrame oPPTViewFrame)
        {
            // 注意只接受部分的 Refresh，且不接受 Null List
            if (oPPTViewFrame.lTPs != null)
                this.RefreshTransponders(oPPTViewFrame.lTPs);
            if (oPPTViewFrame.lPiles != null)
                this.RefreshPiles(oPPTViewFrame.lPiles);
            if (oPPTViewFrame.lAGVs != null)
                this.RefreshAGVs(oPPTViewFrame.lAGVs);
            if (oPPTViewFrame.lTPs != null)
                this.RefreshTransponders(oPPTViewFrame.lTPs);
            if (oPPTViewFrame.lLanes != null)
                this.RefreshLanes(oPPTViewFrame.lLanes);
            if (oPPTViewFrame.lMates != null)
                this.RefreshMates(oPPTViewFrame.lMates);
            if (oPPTViewFrame.lQCs != null)
                this.RefreshQCs(oPPTViewFrame.lQCs);
            if (oPPTViewFrame.lASCs != null)
                this.RefreshASCs(oPPTViewFrame.lASCs);
            if (oPPTViewFrame.lBerthVessels != null)
                this.RefreshVessels(oPPTViewFrame.lBerthVessels);
        }

        /// <summary>
        /// 在 ViewFrame 中清除所有对象
        /// </summary>
        public void ResetView()
        {
            this.oSimCanvas.Children.Clear();
            this.dChangableCanvases.Clear();
            this.dChangableEllipses.Clear();
            this.dChangableRectangles.Clear();
            this.dChangableLabels.Clear();
            this.dChangableLines.Clear();
            this.dMotionStatusColors.Clear();
            this.dMoveKindColors.Clear();
            this.dStoreTypeColors.Clear();
            this.dLaneStatusColors.Clear();
            this.dLaneAttrColors.Clear();
            this.dTierColors.Clear();
            this.dTPPoints.Clear();
            this.oSimCanvas.Width = 0;
            this.oSimCanvas.Height = 0;
            this.Width = this.MinWidth;
            this.Height = this.MinHeight;
        }


        #region Create

        // 不变的东西不注册

        /// <summary>
        /// 新建码头区域 TerminalRegion
        /// </summary>
        /// <param name="oTR">码头区域定义</param>
        private void CreateTerminalRegion(TerminalRegion oTR)
        {
            if (this.oSimCanvas == null || this.oSimCanvas.Children.Count > 0)
                return;

            // Canvas 尺寸调整
            this.oSimCanvas.Margin = new Thickness(oTR.X, oTR.Y, 0, 0);
            this.oSimCanvas.Width = oTR.Width * (double)this.ratio;
            this.oSimCanvas.Height = (oTR.LandHeight + oTR.WaterHeight) * (double)this.ratio; ;

            // 陆域在上
            Rectangle RecLand = new Rectangle();
            RecLand.Name = "LandScope";
            RecLand.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            RecLand.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            RecLand.Margin = new Thickness(0, 0, 0, 0);
            RecLand.Width = oTR.Width * (double)this.ratio;
            RecLand.Height = oTR.LandHeight * (double)this.ratio;
            RecLand.Stroke = Brushes.Black;
            RecLand.Fill = Brushes.Gainsboro;
            this.oSimCanvas.Children.Add(RecLand);
            Canvas.SetZIndex(RecLand, oTR.Zindex);

            // 海域在下
            Rectangle RecWater = new Rectangle();
            RecWater.Name = "WaterScope";
            RecWater.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            RecWater.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            RecWater.Margin = new Thickness(0, oTR.LandHeight * (double)this.ratio, 0, 0);
            RecWater.Width = oTR.Width * (double)this.ratio;
            RecWater.Height = oTR.WaterHeight * (double)this.ratio;
            RecWater.Stroke = Brushes.Black;
            RecWater.Fill = Brushes.PaleTurquoise;
            this.oSimCanvas.Children.Add(RecWater);
            Canvas.SetZIndex(RecWater, oTR.Zindex);

            // 调整窗口大小
            this.Width = this.oSimCanvas.Width;
            this.Height = this.oSimCanvas.Height;
        }

        /// <summary>
        /// 新建磁钉，注意是否显示
        /// </summary>
        /// <param name="lTPs">磁钉列表</param>
        private void CreateTransponders(List<SimTransponder> lTPs)
        {
            foreach (SimTransponder oT in lTPs)
            {
                if (!this.dTPPoints.ContainsKey(oT.ID)) 
                    this.dTPPoints.Add(oT.ID, new Point(oT.LogicPosX, oT.LogicPosY));
                this.JustifyAppearance(oT);
            }
        }

        /// <summary>
        /// 新建 AGVLine
        /// </summary>
        /// <param name="lAGVLs"> AGVLine列表</param>
        private void CreateAGVLines(List<AGVLine> lAGVLs)
        {
            Line oL;
            bool bRet;

            if (this.dTPPoints.Count == 0)
                return;

            foreach (AGVLine oAL in lAGVLs)
            {
                oL = new Line();
                bRet = true;
                if (this.dTPPoints.ContainsKey(oAL.lTPIDs[0]))
                {
                    oL.X1 = this.dTPPoints[oAL.lTPIDs[0]].X * (double)this.ratio;
                    oL.Y1 = this.dTPPoints[oAL.lTPIDs[0]].Y * (double)this.ratio;
                }
                else 
                    bRet = false;
                if (this.dTPPoints.ContainsKey(oAL.lTPIDs[oAL.lTPIDs.Count - 1]))
                {
                    oL.X2 = this.dTPPoints[oAL.lTPIDs[oAL.lTPIDs.Count - 1]].X * (double)this.ratio;
                    oL.Y2 = this.dTPPoints[oAL.lTPIDs[oAL.lTPIDs.Count - 1]].Y * (double)this.ratio;
                }
                else 
                    bRet = false;

                if (bRet)
                {
                    oL.Stroke = new SolidColorBrush(Colors.DimGray);
                    this.dChangableLines.Add(oAL.ID, oL);
                    this.oSimCanvas.Children.Add(oL);
                    Canvas.SetZIndex(oL, oAL.ZIndex);
                    this.JustifyAppearance(oAL);
                }
            }
        }

        /// <summary>
        /// 新建 Block
        /// </summary>
        /// <param name="lBlocks"> Block 列表</param>
        private void CreateBlocks(List<Block> lBlocks)
        {
            Canvas oCanvas;
            Rectangle oRec;
            Line sLine;

            foreach (Block oB in lBlocks)
            {
                // 容器
                oCanvas = new Canvas();
                oCanvas.Name = oB.Name;
                oCanvas.Margin = new Thickness(oB.X * (double)this.ratio, oB.Y * (double)this.ratio, 0, 0);
                oCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oCanvas.Width = (oB.MarginX + 2) * (double)this.ratio;
                oCanvas.Height = (oB.MarginY + 2) * (double)this.ratio;
                oCanvas.Background = new SolidColorBrush(Colors.Transparent);
 
                // 外廓
                oRec = new Rectangle();
                oRec.Margin = new Thickness(0, 0, 0, 0);
                oRec.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oRec.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oRec.Width = oB.MarginX * (double)this.ratio;
                oRec.Height = oB.MarginY * (double)this.ratio;
                oRec.Stroke = Brushes.Black;
                oRec.Fill = Brushes.Transparent;
                oRec.StrokeThickness = (double)this.ratio;
                oCanvas.Children.Add(oRec);
                
                // 竖线
                for (int i = 0; i < oB.lBlockDivsX.Count; i++)
                {
                    sLine = new Line();
                    sLine.Stroke = Brushes.Black;
                    sLine.X1 = oB.lBlockDivsX[i].StartPos * (double)this.ratio;
                    sLine.Y1 = 0;
                    sLine.X2 = oB.lBlockDivsX[i].StartPos * (double)this.ratio;
                    sLine.Y2 = oB.MarginY * (double)this.ratio;
                    oCanvas.Children.Add(sLine);
                }

                // 横线
                for (int i = 0; i < oB.lBlockDivsY.Count; i++)
                {
                    sLine = new Line();
                    sLine.Stroke = Brushes.Black;
                    sLine.X1 = 0;
                    sLine.Y1 = oB.lBlockDivsY[i].StartPos * (double)this.ratio;
                    sLine.X2 = oB.MarginX * (double)this.ratio;
                    sLine.Y2 = oB.lBlockDivsY[i].StartPos * (double)this.ratio;
                    oCanvas.Children.Add(sLine);
                }

                this.dChangableCanvases.Add(oCanvas.Name, oCanvas);
                this.oSimCanvas.Children.Add(oCanvas);
                Canvas.SetZIndex(oCanvas, oB.ZIndex);
            }
        }

        /// <summary>
        /// 新建 Lane
        /// </summary>
        /// <param name="lLanes"> Lane 列表</param>
        private void CreateLanes(List<Lane> lLanes)
        {
            Rectangle oRec;
            Ellipse oEll;
            foreach (Lane oL in lLanes)
            {
                if (this.dChangableRectangles.ContainsKey(oL.Name) || (oL.oDirSign != null && this.dChangableEllipses.ContainsKey(oL.oDirSign.Name)))
                    continue;

                oRec = new Rectangle();
                oRec.Name = oL.Name;
                oRec.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oRec.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oRec.Stroke = new SolidColorBrush(Colors.Black);
                oRec.StrokeThickness = (double)this.ratio / 2;
                this.oSimCanvas.Children.Add(oRec);
                Canvas.SetZIndex(oRec, oL.Zindex);
                this.dChangableRectangles.Add(oRec.Name, oRec);
                if (oL.oDirSign != null)
                {
                    oEll = new Ellipse();
                    oEll.Name = oL.oDirSign.Name;
                    oEll.Width = (double)(4 * this.ratio);
                    oEll.Height = (double)(4 * this.ratio);
                    oEll.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    oEll.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                    oEll.Stroke = new SolidColorBrush(Colors.Black);
                    this.oSimCanvas.Children.Add(oEll);
                    Canvas.SetZIndex(oEll, oL.Zindex);
                    this.dChangableEllipses.Add(oEll.Name, oEll);
                }
                this.JustifyAppearance(oL);
            }
        }

        /// <summary>
        /// 新建 Mate
        /// </summary>
        /// <param name="lMates"> Mate 列表</param>
        private void CreateMates(List<Mate> lMates)
        {
            Rectangle oRec;
            foreach (Mate oM in lMates)
            {
                if (this.dChangableRectangles.ContainsKey(oM.Name))
                    continue;

                oRec = new Rectangle();
                oRec.Name = oM.Name;
                oRec.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oRec.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oRec.Width = (this.dTPPoints[oM.TPIDEnd].X - this.dTPPoints[oM.TPIDStart].X) * (double)this.ratio;
                oRec.Height = oM.Width * (double)this.ratio;
                oRec.Margin = new Thickness(this.dTPPoints[oM.TPIDStart].X * (double)this.ratio, (this.dTPPoints[oM.TPIDStart].Y - oM.Width / 2) * (double)this.ratio, 0, 0);
                oRec.Fill = new SolidColorBrush(Colors.Transparent);
                oRec.StrokeThickness = oM.Width * (double)ratio * 0.3;
                this.oSimCanvas.Children.Add(oRec);
                Canvas.SetZIndex(oRec, oM.ZIndex);
                this.dChangableRectangles.Add(oRec.Name, oRec);
                this.JustifyAppearance(oM);
            }
        }

        /// <summary>
        /// 新建 AGV
        /// </summary>
        /// <param name="lAGVs"> AGV 列表</param>
        private void CreateAGVs(List<AGV> lAGVs)
        {
            Label oLabel;
            foreach (AGV oA in lAGVs)
            {
                if (this.dChangableLabels.ContainsKey(oA.Name))
                    continue; 

                oLabel = new Label();
                oLabel.Width = oA.oType.Length * (double)this.ratio;
                oLabel.Height = oA.oType.Width * (double)this.ratio;
                oLabel.BorderThickness = new Thickness(0);
                oLabel.HorizontalContentAlignment = HorizontalAlignment.Right;
                oLabel.VerticalContentAlignment = VerticalAlignment.Top;
                oLabel.HorizontalAlignment = HorizontalAlignment.Left;
                oLabel.VerticalAlignment = VerticalAlignment.Top;
                oLabel.Name = oA.Name;
                oLabel.Content = oA.ID;
                oLabel.FontSize = 2.4 * (double)this.ratio;
                oLabel.FontWeight = FontWeights.UltraBold;
                oLabel.Padding = new Thickness(0);
                this.oSimCanvas.Children.Add(oLabel);
                Canvas.SetZIndex(oLabel, oA.ZIndex);
                this.dChangableLabels.Add(oLabel.Name, oLabel);
                this.JustifyAppearance(oA);
            }
        }

        /// <summary>
        /// 新建 QC
        /// </summary>
        /// <param name="lQCs"> QC 列表</param>
        private void CreateQCs(List<QCDT> lQCs)
        {
            Canvas oCanvas;
            Rectangle oRec;

            foreach (QCDT oQC in lQCs)
            {
                if (this.dChangableCanvases.ContainsKey(oQC.Name))
                    continue;

                // 一个 Canvas 表示岸桥范围
                oCanvas = new Canvas();
                oCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oCanvas.Width = oQC.oType.BaseGauge * (double)this.ratio;
                oCanvas.Height = (oQC.oType.BackReach + oQC.oType.TrackGauge + oQC.oType.FrontReach) * (double)this.ratio;
                oCanvas.Background = new SolidColorBrush(Colors.Transparent);
                oCanvas.Name = oQC.Name;

                // 一个 Rec 给大车
                oRec = new Rectangle();
                oRec.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oRec.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oRec.Width = oQC.oType.BaseGauge * (double)this.ratio;
                oRec.Height = oQC.oType.TrackGauge * (double)this.ratio;
                oRec.Margin = new Thickness(0, oQC.oType.BackReach * (double)this.ratio, 0, 0);
                oRec.Fill = new SolidColorBrush(Colors.Transparent);
                oRec.StrokeThickness = oQC.oType.Thickness * (double)this.ratio;
                oRec.Name = oQC.Name + "Gantry";
                oCanvas.Children.Add(oRec);
                Canvas.SetZIndex(oRec, 0);
                this.dChangableRectangles.Add(oRec.Name, oRec);

                // 一个 Rec 给大梁
                oRec = new Rectangle();
                oRec.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oRec.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oRec.Width = oQC.oType.CantiWidth * (double)this.ratio;
                oRec.Height = (oQC.oType.TrackGauge + oQC.oType.FrontReach + oQC.oType.BackReach) * (double)this.ratio;
                oRec.Margin = new Thickness((oQC.oType.BaseGauge - oQC.oType.CantiWidth) / 2 * (double)this.ratio, 0, 0, 0);
                oRec.Fill = new SolidColorBrush(Colors.Transparent);
                oRec.StrokeThickness = oQC.oType.Thickness * (double)this.ratio;
                oRec.Name = oQC.Name + "Cantilever";
                oCanvas.Children.Add(oRec);
                Canvas.SetZIndex(oRec, 0);
                this.dChangableRectangles.Add(oRec.Name, oRec);

                // 一个 Rec 给主小车
                oRec = new Rectangle();
                oRec.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oRec.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oRec.Width = oQC.MainTrolley.oTwinStoreUnit.DefaultLength * (double)this.ratio;
                oRec.Height = oQC.MainTrolley.oTwinStoreUnit.DefaultHeight * (double)this.ratio;
                oRec.StrokeThickness = 0;
                oRec.Name = oQC.Name + "MainTrolley";
                oCanvas.Children.Add(oRec);
                Canvas.SetZIndex(oRec, 1);
                this.dChangableRectangles.Add(oRec.Name, oRec);

                // 一个 Rec 给副小车
                oRec = new Rectangle();
                oRec.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oRec.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oRec.Width = oQC.MainTrolley.oTwinStoreUnit.DefaultLength * (double)this.ratio;
                oRec.Height = oQC.MainTrolley.oTwinStoreUnit.DefaultHeight * (double)this.ratio;
                oRec.StrokeThickness = 0;
                oRec.Name = oQC.Name + "ViceTrolley";
                oCanvas.Children.Add(oRec);
                Canvas.SetZIndex(oRec, 1);
                this.dChangableRectangles.Add(oRec.Name, oRec);

                // 若干 Rec 给平台
                foreach (QCPlatformSlot oSlot in oQC.Platform)
                {
                    oRec = new Rectangle();
                    oRec.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    oRec.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                    oRec.Width = oQC.MainTrolley.oTwinStoreUnit.DefaultLength * (double)this.ratio;
                    oRec.Height = oQC.MainTrolley.oTwinStoreUnit.DefaultHeight * (double)this.ratio;
                    oRec.Margin = new Thickness((oQC.oType.BaseGauge / 2 + oQC.oType.CantiWidth / 2 - oSlot.oTwinStoreUnit.DefaultLength) * (double)this.ratio,
                        (oQC.oType.BackReach + oQC.oType.TrackGauge / (oQC.Platform.Count + 1) * oSlot.ID) * (double)this.ratio, 0, 0);
                    oRec.StrokeThickness = 0;
                    oRec.Name = oQC.Name + "PlatformSlot" + oSlot.ID.ToString();
                    oCanvas.Children.Add(oRec);
                    Canvas.SetZIndex(oRec, 1);
                    this.dChangableRectangles.Add(oRec.Name, oRec);
                }

                // 岸桥调整上线
                this.oSimCanvas.Children.Add(oCanvas);
                Canvas.SetZIndex(oCanvas, oQC.ZIndex);
                this.dChangableCanvases.Add(oCanvas.Name, oCanvas);
                this.JustifyAppearance(oQC);
            }
        }

        /// <summary>
        /// 新建 ASC
        /// </summary>
        /// <param name="lASCs"></param>
        private void CreateASCs(List<ASC> lASCs)
        {
            Canvas oCanvas;
            Rectangle oRec;

            foreach (ASC oASC in lASCs)
            {
                if (this.dChangableCanvases.ContainsKey(oASC.Name))
                    continue;

                // 一个 Canvas 表示场桥范围
                oCanvas = new Canvas();
                oCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oCanvas.Width = oASC.oType.BaseGauge * (double)this.ratio;
                oCanvas.Height = oASC.oType.TrackGauge * (double)this.ratio;
                oCanvas.Background = new SolidColorBrush(Colors.Transparent);
                oCanvas.Name = oASC.Name;

                // 一个 Rec 给大车
                oRec = new Rectangle();
                oRec.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oRec.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oRec.Width = oASC.oType.BaseGauge * (double)this.ratio;
                oRec.Height = oASC.oType.TrackGauge * (double)this.ratio;
                oRec.Margin = new Thickness(0, 0, 0, 0);
                oRec.Fill = new SolidColorBrush(Colors.Transparent);
                oRec.StrokeThickness = oASC.oType.Thickness * (double)this.ratio;
                oRec.Name = oASC.Name + "Gantry";
                oCanvas.Children.Add(oRec);
                Canvas.SetZIndex(oRec, 0);
                this.dChangableRectangles.Add(oRec.Name, oRec);

                // 一个 Rec 给小车
                oRec = new Rectangle();
                oRec.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oRec.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oRec.Width = oASC.oTrolley.oSingleStoreUnit.DefaultLength * (double)this.ratio;
                oRec.Height = oASC.oTrolley.oSingleStoreUnit.DefaultHeight * (double)this.ratio;
                oRec.Margin = new Thickness(0, (oASC.oType.TrackGauge / 2 * (double)this.ratio), 0, 0);
                oRec.StrokeThickness = 0;
                oRec.Name = oASC.Name + "Trolley";
                oCanvas.Children.Add(oRec);
                Canvas.SetZIndex(oRec, 1);
                this.dChangableRectangles.Add(oRec.Name, oRec);

                // 场桥调整上线
                
                this.oSimCanvas.Children.Add(oCanvas);
                Canvas.SetZIndex(oCanvas, oASC.ZIndex);
                this.dChangableCanvases.Add(oCanvas.Name, oCanvas);
                this.JustifyAppearance(oASC);
            }

        }

        /// <summary>
        /// 新建 Piles
        /// </summary>
        /// <param name="lPs"> Pile 对象列表</param>
        private void CreatePiles(List<Pile> lPs)
        {
            Rectangle oRec;

            foreach (Pile oP in lPs)
            {
                if (this.dChangableRectangles.ContainsKey(oP.Name))
                    continue;

                oRec = new Rectangle();
                oRec.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oRec.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oRec.Width = oP.oType.Length * (double)this.ratio;
                oRec.Height = oP.oType.Width * (double)this.ratio;
                oRec.Margin = new Thickness(oP.Slot1.BasePointUL.X * (double)ratio,oP.Slot1.BasePointUL.Y * (double)this.ratio ,0 ,0);
                oRec.Name = oP.Name;
                oRec.StrokeThickness = 0;
                this.oSimCanvas.Children.Add(oRec);
                Canvas.SetZIndex(oRec, oP.ZIndex);
                this.dChangableRectangles.Add(oRec.Name, oRec);
                this.JustifyAppearance(oP);
            }
        }

        /// <summary>
        /// 新建 Vessel
        /// </summary>
        /// <param name="lVs">新建 Vessel 列表</param>
        private void CreateVessels(List<Vessel> lVs)
        {
            Canvas oCanvas;
            Rectangle oRec;
            Line oLine;
            double LeftX;
            double RightX;

            foreach (Vessel oVes in lVs)
            {
                if (this.dChangableCanvases.ContainsKey(oVes.Name))
                    continue;

                // Canvas 承载船体
                oCanvas = new Canvas();
                oCanvas.Name = oVes.Name;
                oCanvas.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                oCanvas.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                oCanvas.Width = oVes.oType.Length * (double)this.ratio;
                oCanvas.Height = oVes.oType.Width * (double)this.ratio;
                oCanvas.Background = new SolidColorBrush(Colors.Transparent);

                // Rectangle 表示船舱。不用变化，无需注册
                oRec = new Rectangle();
                oRec.HorizontalAlignment = HorizontalAlignment.Left;
                oRec.VerticalAlignment = VerticalAlignment.Top;
                oRec.Width = (oVes.oType.Length - oVes.oType.BowSpaceLen) * (double)this.ratio;
                oRec.Height = oVes.oType.Width * (double)this.ratio;
                oRec.Fill = new SolidColorBrush(Colors.Transparent);
                oRec.Stroke = new SolidColorBrush(Colors.Black);
                oRec.StrokeThickness = 1.5 * (double)this.ratio;

                // 船身位置与船头尖角
                if (oVes.eBerthWay == StatusEnums.BerthWay.L)
                {
                    oRec.Margin = new Thickness(0, 0, 0, 0);
                    LeftX = oVes.oType.SternSpaceLen;
                    RightX = oVes.oType.Length - oVes.oType.BowSpaceLen;

                    oLine = new Line();
                    oLine.Margin = new Thickness(0, 0, 0, 0);
                    oLine.StrokeThickness = 1.5 * (double)this.ratio;
                    oLine.Stroke = new SolidColorBrush(Colors.Black);
                    oLine.X1 = RightX * (double)this.ratio;
                    oLine.Y1 = 0 * (double)this.ratio;
                    oLine.X2 = oVes.oType.Length * (double)this.ratio;
                    oLine.Y2 = oVes.oType.Width / 2 * (double)this.ratio;
                    oCanvas.Children.Add(oLine);

                    oLine = new Line();
                    oLine.Margin = new Thickness(0, 0, 0, 0);
                    oLine.StrokeThickness = 1.5 * (double)this.ratio;
                    oLine.Stroke = new SolidColorBrush(Colors.Black);
                    oLine.X1 = oVes.oType.Length * (double)this.ratio;
                    oLine.Y1 = oVes.oType.Width / 2 * (double)this.ratio;
                    oLine.X2 = RightX * (double)this.ratio;
                    oLine.Y2 = oVes.oType.Width * (double)this.ratio;
                    oCanvas.Children.Add(oLine);
                }
                else
                {
                    oRec.Margin = new Thickness(oVes.oType.BowSpaceLen * (double)this.ratio, 0, 0, 0);
                    LeftX = oVes.oType.BowSpaceLen;
                    RightX = oVes.oType.Length - oVes.oType.SternSpaceLen;

                    oLine = new Line();
                    oLine.Margin = new Thickness(0, 0, 0, 0);
                    oLine.StrokeThickness = 1.5 * (double)this.ratio;
                    oLine.Stroke = new SolidColorBrush(Colors.Black);
                    oLine.X1 = LeftX * (double)this.ratio;
                    oLine.Y1 = 0 * (double)this.ratio;
                    oLine.X2 = 0 * (double)this.ratio;
                    oLine.Y2 = oVes.oType.Width / 2 * (double)this.ratio;
                    oCanvas.Children.Add(oLine);

                    oLine = new Line();
                    oLine.Margin = new Thickness(0, 0, 0, 0);
                    oLine.StrokeThickness = 1.5 * (double)this.ratio;
                    oLine.Stroke = new SolidColorBrush(Colors.Black);
                    oLine.X1 = 0 * (double)this.ratio;
                    oLine.Y1 = oVes.oType.Width / 2 * (double)this.ratio;
                    oLine.X2 = LeftX * (double)this.ratio;
                    oLine.Y2 = oVes.oType.Width * (double)this.ratio;
                    oCanvas.Children.Add(oLine);
                }
                oCanvas.Children.Add(oRec);

                // 舱隔线，船身范围内正反不分
                for (int i = 0; i <= oVes.oType.CabinNum; i++)
                {
                    oLine = new Line();
                    oLine.Margin = new Thickness(0, 0, 0, 0);
                    oLine.StrokeThickness = 1.5 * (double)this.ratio;
                    oLine.Stroke = new SolidColorBrush(Colors.Black);
                    oLine.X1 = (LeftX + i * (oVes.oType.SingleCabinLength)) * (double)this.ratio;
                    oLine.Y1 = 0 * (double)this.ratio;
                    oLine.X2 = (LeftX + i * (oVes.oType.SingleCabinLength)) * (double)this.ratio;
                    oLine.Y2 = oVes.oType.Width * (double)this.ratio;
                    oCanvas.Children.Add(oLine);
                }

                this.dChangableCanvases.Add(oCanvas.Name, oCanvas);
                this.JustifyAppearance(oVes);
                this.oSimCanvas.Children.Add(oCanvas);
                Canvas.SetZIndex(oCanvas, oVes.ZIndex);
                
            }
        }

        /// <summary>
        /// 载入颜色字典
        /// </summary>
        /// <param name="DicName">字典名</param>
        /// <param name="dColors">字典实例</param>
        private void LoadColorDictionary(string DicName, Dictionary<string, Color> dColors)
        {
            switch (DicName)
            {
                case "MotionStatus":
                    this.dMotionStatusColors = dColors;
                    break;
                case "MoveKinds":
                    this.dMoveKindColors = dColors;
                    break;
                case "StoreTypes":
                    this.dStoreTypeColors = dColors;
                    break;
                case "LaneStatus":
                    this.dLaneStatusColors = dColors;
                    break;
                case "LaneAttrs":
                    this.dLaneAttrColors = dColors;
                    break;
                case "TierColors":
                    this.dTierColors = dColors;
                    break;
                default:
                    Console.WriteLine("Unexpected Dictionary Ind Str " + DicName);
                    break;
            }
        }

        #endregion


        #region Renew

        // 变动行车线 AGVLine
        private void RenewAGVLines(List<AGVLine> lAGVLines)
        {
            foreach (AGVLine oAL in lAGVLines)
                this.JustifyAppearance(oAL);
        }

        // 变动车道 Lane
        private void RenewLanes(List<Lane> lLs)
        {
            foreach (Lane oL in lLs)
                this.JustifyAppearance(oL);
        }

        // 变动支架 Mate
        private void RenewMates(List<Mate> lMs)
        {
            foreach (Mate oM in lMs)
                this.JustifyAppearance(oM);
        }

        // 变动 AGV
        private void RenewAGVs(List<AGV> lAs)
        {
            foreach (AGV oA in lAs)
                this.JustifyAppearance(oA);
        }

        // 变动 QC
        private void RenewQCs(List<QCDT> lQCs)
        {
            foreach (QCDT oQC in lQCs)
                this.JustifyAppearance(oQC);
        }

        // 变动 Pile
        private void RenewPiles(List<Pile> lPs)
        {
            foreach (Pile oP in lPs)
                this.JustifyAppearance(oP);
        }

        // 变动 Transponder
        private void RenewTransponders(List<SimTransponder> lTPs)
        {
            foreach (SimTransponder oTP in lTPs)
                this.JustifyAppearance(oTP);
        }

        // 变动 ASC
        private void RenewASCs(List<ASC> lASCs)
        {
            foreach (ASC oASC in lASCs)
                this.JustifyAppearance(oASC);
        }

        // 变动 Vessels
        private void RenewVessels(List<Vessel> lVessels)
        {
            foreach (Vessel oVes in lVessels)
                this.JustifyAppearance(oVes);
        }

        #endregion


        #region JustifyAppearance

        /// <summary>
        /// 改变行车线 AGVLine 的外观
        /// </summary>
        /// <param name="oAL"></param>
        private void JustifyAppearance(AGVLine oAL)
        {
            Line oL;

            if (!this.dChangableLines.ContainsKey(oAL.ID))
                return;

            oL = this.dChangableLines[oAL.ID];

            if (oAL.bIfEssential || oAL.eMoveDir != CHE_Direction.Unknown)
                oL.StrokeThickness = (double)this.ratio / 4;
            else
                oL.StrokeThickness = 0;
        }

        /// <summary>
        /// 改变对象的外观。Lane 重载
        /// </summary>
        /// <param name="oL"> Lane 对象</param>
        private void JustifyAppearance(Lane oL)
        {
            Rectangle oRec;
            Ellipse oEll;

            if (oL != null && this.dChangableRectangles.ContainsKey(oL.Name))
                oRec = this.dChangableRectangles[oL.Name];
            else
                return;

            if (oL.RotateAngle == 0 || oL.RotateAngle == 180)
            {
                // 横向。TP的相对顺序有保证。
                oRec.Width = (this.dTPPoints[oL.TPIDEnd].X - this.dTPPoints[oL.TPIDStart].X) * (double)this.ratio;
                oRec.Height = oL.Width * (double)this.ratio;
                oRec.Margin = new Thickness((this.dTPPoints[oL.TPIDStart].X * (double)this.ratio), ((this.dTPPoints[oL.TPIDStart].Y - 2) * (double)this.ratio), 0, 0);
            }
            else
            {
                // 纵向
                oRec.Width = oL.Width * (double)this.ratio;
                oRec.Height = (this.dTPPoints[oL.TPIDEnd].Y - this.dTPPoints[oL.TPIDStart].Y) * (double)this.ratio;
                oRec.Margin = new Thickness(((this.dTPPoints[oL.TPIDStart].X - 2) * (double)this.ratio), (this.dTPPoints[oL.TPIDStart].Y * (double)this.ratio), 0, 0);
            }
            oRec.Fill = new SolidColorBrush(this.dLaneStatusColors[oL.eStatus.ToString()]);

            if (oL.oDirSign != null && this.dChangableEllipses.ContainsKey(oL.oDirSign.Name))
            {
                oEll = this.dChangableEllipses[oL.oDirSign.Name];

                if (oL.RotateAngle == 0 || oL.RotateAngle == 180)
                    oEll.Margin = new Thickness((this.dTPPoints[oL.TPIDStart].X) * (double)this.ratio, (this.dTPPoints[oL.TPIDStart].Y - 2) * (double)this.ratio, 0, 0);
                else
                    oEll.Margin = new Thickness((this.dTPPoints[oL.TPIDStart].X - 2) * (double)this.ratio, (this.dTPPoints[oL.TPIDStart].Y) * (double)this.ratio, 0, 0);
                oEll.Fill = new SolidColorBrush(this.dLaneAttrColors[oL.eAttr.ToString()]);

                if (oL.CheNo > 0)
                    oEll.StrokeThickness = (double)this.ratio;
                else
                    oEll.StrokeThickness = 0;
            }
        }

        /// <summary>
        /// 改变对象的外观。Mate 重载
        /// </summary>
        /// <param name="oM"> Mate 对象</param>
        private void JustifyAppearance(Mate oM)
        {
            Rectangle oRec;

            if (oM != null && this.dChangableRectangles.ContainsKey(oM.Name))
            {
                oRec = this.dChangableRectangles[oM.Name];
                oRec.Stroke = new SolidColorBrush(this.dStoreTypeColors[oM.oStorageUnit.eUnitStoreType.ToString()]);
            }
        }

        /// <summary>
        /// 改变对象的外观。AGV 重载
        /// </summary>
        /// <param name="oA"> AGV 对象</param>
        private void JustifyAppearance(AGV oA)
        {
            Label oLabel;

            if (oA != null && this.dChangableLabels.ContainsKey(oA.Name))
            {
                oLabel = this.dChangableLabels[oA.Name];
                oLabel.Margin = new Thickness((oA.MidPoint.X - oA.oType.Length / 2) * (double)this.ratio, (oA.MidPoint.Y - oA.oType.Width / 2) * (double)this.ratio, 0, 0);
                oLabel.RenderTransformOrigin = new Point(0.5, 0.5);
                oLabel.RenderTransform = new RotateTransform(oA.RotateAngle);
                oLabel.Background = new SolidColorBrush(this.dStoreTypeColors[oA.oTwinStoreUnit.eUnitStoreType.ToString()]);
            }
        }

        /// <summary>
        /// 改变对象的外观。QC 重载
        /// </summary>
        /// <param name="oQC"> QC 对象</param>
        private void JustifyAppearance(QCDT oQC)
        {
            Rectangle oRec;
            Canvas oCanvas;
            double MinYPos, MidYPos, MaxYPos;

            if (oQC != null && this.dChangableCanvases.ContainsKey(oQC.Name))
                oCanvas = this.dChangableCanvases[oQC.Name];
            else
                return;

            // 岸桥位置
            oCanvas.Margin = new Thickness((oQC.BasePoint.X - oQC.oType.BaseGauge / 2) * (double)this.ratio,
                (oQC.BasePoint.Y - oQC.oType.BackReach) * (double)this.ratio, 0, 0);

            // 大车颜色
            if (this.dChangableRectangles.ContainsKey(oQC.Name + "Gantry"))
            {
                oRec = this.dChangableRectangles[oQC.Name + "Gantry"];
                if (this.dMotionStatusColors.ContainsKey(oQC.eMotionStatus.ToString()))
                    oRec.Stroke = new SolidColorBrush(this.dMotionStatusColors[oQC.eMotionStatus.ToString()]);
                else
                    oRec.Stroke = new SolidColorBrush(Colors.Black);
            }

            // 大梁颜色
            if (this.dChangableRectangles.ContainsKey(oQC.Name + "Cantilever"))
            {
                oRec = this.dChangableRectangles[oQC.Name + "Cantilever"];
                if (this.dMoveKindColors.ContainsKey(oQC.eMoveKind.ToString()))
                    oRec.Stroke = new SolidColorBrush(this.dMoveKindColors[oQC.eMoveKind.ToString()]);
                else
                    oRec.Stroke = new SolidColorBrush(Colors.Black);
            }

            // 主小车位置与颜色
            MinYPos = oQC.oType.BackReach + oQC.oType.TrackGauge / 2;
            MidYPos = oQC.oType.BackReach + oQC.oType.TrackGauge;
            MaxYPos = oQC.oType.BackReach + oQC.oType.TrackGauge + oQC.oType.FrontReach * 0.5;
            this.JustifyAppearance(oQC.MainTrolley, MinYPos, MidYPos, MaxYPos);

            // 副小车位置与颜色
            MinYPos = oQC.oType.BackReach / 2;
            MidYPos = oQC.oType.BackReach;
            MaxYPos = oQC.oType.BackReach + oQC.oType.TrackGauge / 2;
            this.JustifyAppearance(oQC.ViceTrolley, MinYPos, MidYPos, MaxYPos);

            // 平台位置颜色
            foreach (QCPlatformSlot oSlot in oQC.Platform)
            {
                if (this.dChangableRectangles.ContainsKey(oQC.Name + "PlatformSlot" + oSlot.ID.ToString()))
                {
                    oRec = this.dChangableRectangles[oQC.Name + "PlatformSlot" + oSlot.ID.ToString()];
                    if (oSlot.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                        oRec.Fill = new SolidColorBrush(this.dStoreTypeColors[oSlot.oTwinStoreUnit.eUnitStoreType.ToString()]);
                    else
                        oRec.Fill = new SolidColorBrush(this.dStoreTypeColors[Enum.GetName(typeof(StatusEnums.StoreType), 0)]);
                }
            }
        }

        /// <summary>
        /// 改变对象的外观。QCTrolley 重载
        /// </summary>
        /// <param name="oTro"> QCTrolley 对象</param>
        /// <param name="MinYPos">最小Y值</param>
        /// <param name="MidYPos">中间Y值</param>
        /// <param name="MaxYPos">最大Y值</param>
        private void JustifyAppearance(QCTrolley oTro, double MinYPos, double MidYPos, double MaxYPos)
        {
            Rectangle oRec;
            double CurrYPos;
            TroActionType eTrolleyActionType;

            if (oTro == null || oTro.oQC == null)
                return;

            if (oTro is QCMainTrolley)
                oRec = this.dChangableRectangles[oTro.oQC.Name + "MainTrolley"];
            else if (oTro is QCViceTrolley)
                oRec = this.dChangableRectangles[oTro.oQC.Name + "ViceTrolley"];
            else
                return;

            switch (oTro.eTroStage)
            {
                case StatusEnums.QCTrolleyStage.LSFall:
                    CurrYPos = MinYPos;
                    eTrolleyActionType = TroActionType.Fall;
                    break;
                case StatusEnums.QCTrolleyStage.LSHigh:
                case StatusEnums.QCTrolleyStage.LSLow:
                    CurrYPos = MinYPos;
                    eTrolleyActionType = TroActionType.Wait;
                    break;
                case StatusEnums.QCTrolleyStage.LSRise:
                    CurrYPos = MinYPos;
                    eTrolleyActionType = TroActionType.Rise;
                    break;
                case StatusEnums.QCTrolleyStage.LSToB:
                case StatusEnums.QCTrolleyStage.BToLS:
                    CurrYPos = (MinYPos + MidYPos) / 2;
                    eTrolleyActionType = TroActionType.Move;
                    break;
                case StatusEnums.QCTrolleyStage.BFall:
                    CurrYPos = MidYPos;
                    eTrolleyActionType = TroActionType.Fall;
                    break;
                case StatusEnums.QCTrolleyStage.BHigh:
                case StatusEnums.QCTrolleyStage.BLow:
                    CurrYPos = MidYPos;
                    eTrolleyActionType = TroActionType.Wait;
                    break;
                case StatusEnums.QCTrolleyStage.BRise:
                    CurrYPos = MidYPos;
                    eTrolleyActionType = TroActionType.Rise;
                    break;
                case StatusEnums.QCTrolleyStage.BToWS:
                case StatusEnums.QCTrolleyStage.WSToB:
                    CurrYPos = (MidYPos + MaxYPos) / 2;
                    eTrolleyActionType = TroActionType.Move;
                    break;
                case StatusEnums.QCTrolleyStage.WSFall:
                    CurrYPos = MaxYPos;
                    eTrolleyActionType = TroActionType.Fall;
                    break;
                case StatusEnums.QCTrolleyStage.WSHigh:
                case StatusEnums.QCTrolleyStage.WSLow:
                    CurrYPos = MaxYPos;
                    eTrolleyActionType = TroActionType.Wait;
                    break;
                case StatusEnums.QCTrolleyStage.WSRise:        
                    CurrYPos = MaxYPos;
                    eTrolleyActionType = TroActionType.Rise;
                    break;
                default:
                    CurrYPos = MidYPos;
                    eTrolleyActionType = TroActionType.Wait;
                    break;
            }
            
            oRec.Margin = new Thickness((oTro.oQC.oType.BaseGauge - oTro.oQC.oType.CantiWidth) / 2 * (double)this.ratio,
                CurrYPos * (double)this.ratio, 0, 0);

            if (oTro.oTwinStoreUnit.eUnitStoreStage == StatusEnums.StoreStage.Stored)
                oRec.Fill = new SolidColorBrush(this.dStoreTypeColors[oTro.oTwinStoreUnit.eUnitStoreType.ToString()]);
            else
                oRec.Fill = new SolidColorBrush(this.dStoreTypeColors[Enum.GetName(typeof(StatusEnums.StoreType), 0)]);

            switch (eTrolleyActionType)
            {
                case TroActionType.Fall:
                    oRec.StrokeThickness = (double)this.ratio / 2;
                    oRec.Stroke = new SolidColorBrush(Colors.Black);
                    oRec.StrokeDashArray = new DoubleCollection();
                    break;
                case TroActionType.Rise:
                    oRec.StrokeThickness = (double)this.ratio / 2;
                    oRec.Stroke = new SolidColorBrush(Colors.Black);
                    oRec.StrokeDashArray = new DoubleCollection() { (double)this.ratio / 2, (double)this.ratio / 2 };
                    break;
                case TroActionType.Move:
                case TroActionType.Wait:
                default:
                    oRec.StrokeThickness = 0;
                    break;
            }
        }

        /// <summary>
        /// 改变对象的外观。ASC 重载
        /// </summary>
        /// <param name="oASC"> ASC 对象</param>
        private void JustifyAppearance(ASC oASC)
        {
            Canvas oCanvas;
            Rectangle oRec;
            string Str;

            // 场桥位置
            if (this.dChangableCanvases.ContainsKey(oASC.Name))
                oCanvas = this.dChangableCanvases[oASC.Name];
            else
                return;
            oCanvas.Margin = new Thickness((oASC.BasePoint.X - oASC.oType.BaseGauge / 2) * (double)this.ratio, oASC.BasePoint.Y  * (double)this.ratio, 0, 0);

            // 场桥状态
            if (this.dChangableRectangles.ContainsKey(oASC.Name + "Gantry"))
                oRec = this.dChangableRectangles[oASC.Name + "Gantry"];
            else
                return;
            oRec = this.dChangableRectangles[oASC.Name + "Gantry"];
            if (oASC.eMotionStatus == StatusEnums.MotionStatus.Free) Str = oASC.eMotionStatus.ToString();
            else Str = oASC.eTravelStatus.ToString();
            oRec.Stroke = new SolidColorBrush(this.dMotionStatusColors[Str]);

            // 小车状态
            if (this.dChangableRectangles.ContainsKey(oASC.Name + "Trolley"))
                oRec = this.dChangableRectangles[oASC.Name + "Trolley"];
            else
                return;
            oRec = this.dChangableRectangles[oASC.Name + "Trolley"];
            oRec.Fill = new SolidColorBrush(this.dStoreTypeColors[oASC.oTrolley.oSingleStoreUnit.eUnitStoreType.ToString()]);
        }

        /// <summary>
        /// 改变对象的外观。Pile 重载
        /// </summary>
        /// <param name="oP"> Pile 对象</param>
        private void JustifyAppearance(Pile oP)
        {
            Rectangle oRec;

            if (this.dChangableRectangles.ContainsKey(oP.Name))
                oRec = this.dChangableRectangles[oP.Name];
            else
                return;

            oRec.Fill = new SolidColorBrush(this.dTierColors[oP.StackedNum.ToString()]);
        }

        /// <summary>
        /// 改变对象的外观。磁钉重载
        /// </summary>
        /// <param name="oTP">磁钉对象</param>
        private void JustifyAppearance(SimTransponder oTP)
        {
            Rectangle oRec;

            if (oTP == null || !this.dTPPoints.ContainsKey(oTP.ID))
                return;

            if (oTP.bIfEssential || oTP.LaneID > 0)
            {
                if (!this.dChangableRectangles.ContainsKey(oTP.Name))
                {
                    oRec = new Rectangle();
                    oRec.HorizontalAlignment = HorizontalAlignment.Left;
                    oRec.VerticalAlignment = VerticalAlignment.Top;
                    oRec.Stroke = new SolidColorBrush(Colors.Red);
                    oRec.Name = oTP.Name;
                    this.dChangableRectangles.Add(oRec.Name, oRec);
                    this.oSimCanvas.Children.Add(oRec);
                    Canvas.SetZIndex(oRec, oTP.Zindex);
                }
                else 
                    oRec = this.dChangableRectangles[oTP.Name];

                if (oTP.dRouteTPDivisions.Values.Any(u => u == StatusEnums.RouteTPDivision.Claim))
                {
                    oRec.Width = (double)(4 * this.ratio);
                    oRec.Height = (double)(4 * this.ratio);
                }
                else
                {
                    oRec.Width = (double)(1 * this.ratio);
                    oRec.Height = (double)(1 * this.ratio);
                }

                oRec.Margin = new Thickness(Convert.ToDouble(oTP.LogicPosX * this.ratio) - oRec.Width / 2,
                    Convert.ToDouble(oTP.LogicPosY * this.ratio) - oRec.Height / 2, 0, 0);

                if (oTP.dRouteTPDivisions.Values.Any(u => u == StatusEnums.RouteTPDivision.Detect || u == StatusEnums.RouteTPDivision.Claim)) 
                    oRec.Fill = new SolidColorBrush(Colors.Black);
                else
                    oRec.Fill = new SolidColorBrush(Colors.LightSteelBlue);

                //if (oTP.dDirLockAGVLists.Count > 0 || oTP.dDirLockTailAGVLists.Count > 0)
                //    oRec.StrokeThickness = Convert.ToDouble(this.ratio);
                //else
                //    oRec.StrokeThickness = 0;
                oRec.StrokeThickness = 0;
            }
            else
            {
                if (this.dChangableRectangles.ContainsKey(oTP.Name))
                {
                    this.oSimCanvas.Children.Remove(this.dChangableRectangles[oTP.Name]);
                    this.dChangableRectangles.Remove(oTP.Name);
                }
            }
        }

        /// <summary>
        /// 改变对象的外观。船舶重载
        /// </summary>
        /// <param name="oVes">船舶对象</param>
        private void JustifyAppearance(Vessel oVes)
        {
            Canvas oCanvas;

            // 船舶位置
            if (this.dChangableCanvases.ContainsKey(oVes.Name))
                oCanvas = this.dChangableCanvases[oVes.Name];
            else
                return;

            oCanvas.Margin = new Thickness(Math.Min(oVes.BeginMeter, oVes.EndMeter) * (double)this.ratio, oVes.YAppend * (double)this.ratio, 0, 0);
        }

        #endregion


        #region Delete

        /// <summary>
        /// 清除码头区域
        /// </summary>
        /// <param name="oTR"></param>
        private void DeleteTerminalRegion()
        {
            Rectangle oRec;
            if (this.dChangableRectangles.ContainsKey("LandScope"))
            {
                oRec = this.dChangableRectangles["LandScope"];
                this.oSimCanvas.Children.Remove(oRec);
                this.dChangableRectangles.Remove("LandScope");
            }
            if (this.dChangableRectangles.ContainsKey("WaterScope"))
            {
                oRec = this.dChangableRectangles["WaterScope"];
                this.oSimCanvas.Children.Remove(oRec);
                this.dChangableRectangles.Remove("WaterScope");
            }
        }

        /// <summary>
        /// 清除颜色字典
        /// </summary>
        /// <param name="sDicKey"></param>
        private void DeleteColorDictionary(string DicName)
        {
            switch (DicName)
            {
                case "MotionStatus":
                    this.dMotionStatusColors = new Dictionary<string, Color>();
                    break;
                case "MoveKinds":
                    this.dMoveKindColors = new Dictionary<string, Color>();
                    break;
                case "StoreTypes":
                    this.dStoreTypeColors = new Dictionary<string, Color>();
                    break;
                case "LaneStatus":
                    this.dLaneStatusColors = new Dictionary<string, Color>();
                    break;
                case "LaneAttrs":
                    this.dLaneAttrColors = new Dictionary<string, Color>();
                    break;
                case "TierColors":
                    this.dTierColors = new Dictionary<string, Color>();
                    break;
                default:
                    Console.WriteLine("Unexpected Dictionary Ind Str " + DicName);
                    break;
            }

        }

        /// <summary>
        /// 清除箱区
        /// </summary>
        /// <param name="lBlocks"></param>
        private void DeleteBlocks(List<Block> lBlocks)
        {
            foreach (Block oB in lBlocks)
            {
                if (this.dChangableCanvases.ContainsKey(oB.Name))
                {
                    this.oSimCanvas.Children.Remove(this.dChangableCanvases[oB.Name]);
                    this.dChangableCanvases.Remove(oB.Name);
                }
            }

        }

        /// <summary>
        /// 清除指定的磁钉
        /// </summary>
        /// <param name="lTPs">指定磁钉列表</param>
        private void DeleteTransponders(List<string> lTPNames, List<uint> lTPIDs)
        {
            Rectangle oRec;

            foreach (string sName in lTPNames)
            {
                if (this.dChangableRectangles.ContainsKey(sName))
                {
                    oRec = this.dChangableRectangles[sName];
                    this.oSimCanvas.Children.Remove(oRec);
                    this.dChangableRectangles.Remove(sName);
                }
            }

            foreach (uint TPID in lTPIDs)
            {
                if (this.dTPPoints.ContainsKey(TPID))
                    this.dTPPoints.Remove(TPID);
            }
        }

        /// <summary>
        /// 删除 AGVLine
        /// </summary>
        /// <param name="lAGVLines"></param>
        private void DeleteAGVLines(List<AGVLine> lAGVLines)
        {
            Line oL;

            foreach (AGVLine oAL in lAGVLines)
            {
                if (this.dChangableLines.ContainsKey(oAL.ID))
                {
                    oL = this.dChangableLines[oAL.ID];
                    this.oSimCanvas.Children.Remove(oL);
                    this.dChangableLines.Remove(oAL.ID);
                }
            }
        }

        /// <summary>
        /// 删除 Lane
        /// </summary>
        /// <param name="lLanes"></param>
        private void DeleteLanes(List<string> lLaneNames, List<string> lDirSignNames)
        {
            Rectangle oRec;
            Ellipse oEll;

            foreach (string sName in lLaneNames)
            {
                if (this.dChangableRectangles.ContainsKey(sName))
                {
                    oRec = this.dChangableRectangles[sName];
                    this.oSimCanvas.Children.Remove(oRec);
                    this.dChangableRectangles.Remove(sName);
                }
            }

            foreach (string sName in lDirSignNames)
            {
                if (this.dChangableEllipses.ContainsKey(sName))
                {
                    oEll = this.dChangableEllipses[sName];
                    this.oSimCanvas.Children.Remove(oEll);
                    this.dChangableEllipses.Remove(sName);
                }
            }
        }

        /// <summary>
        /// 删除指定的 QC
        /// </summary>
        /// <param name="lQCs"></param>
        private void DeleteQCs(List<string> lQCNames)
        {
            Canvas oCanvas;

            foreach (string sName in lQCNames)
            {
                if (this.dChangableCanvases.ContainsKey(sName))
                {
                    oCanvas = this.dChangableCanvases[sName];
                    this.oSimCanvas.Children.Remove(oCanvas);
                    this.dChangableCanvases.Remove(sName);
                }
            }
        }

        /// <summary>
        /// 删除 Mate
        /// </summary>
        /// <param name="lMates"></param>
        private void DeleteMates(List<string> lMateNames)
        {
            Rectangle oRec;

            foreach (string sName in lMateNames)
            {
                if (this.dChangableRectangles.ContainsKey(sName))
                {
                    oRec = this.dChangableRectangles[sName];
                    this.oSimCanvas.Children.Remove(oRec);
                    this.dChangableRectangles.Remove(sName);
                }
            }
        }

        /// <summary>
        /// 清除指定的 AGV
        /// </summary>
        /// <param name="lAGVNames">指定AGV名称列表</param>
        private void DeleteAGVs(List<string> lAGVNames)
        {
            Label oLabel;
            foreach (string sName in lAGVNames)
            {
                if (this.dChangableLabels.ContainsKey(sName))
                {
                    oLabel = this.dChangableLabels[sName];
                    this.oSimCanvas.Children.Remove(oLabel);
                    this.dChangableLabels.Remove(sName);
                }
            }
        }

        /// <summary>
        /// 清除指定的 Pile
        /// </summary>
        /// <param name="lPileNames">指定 Pile 名称列表</param>
        private void DeletePiles(List<string> lPileNames)
        {
            Rectangle oRec;
            foreach (string sPileName in lPileNames)
            {
                if (this.dChangableRectangles.ContainsKey(sPileName))
                {
                    oRec = this.dChangableRectangles[sPileName];
                    this.oSimCanvas.Children.Remove(oRec);
                    this.dChangableRectangles.Remove(oRec.Name);
                }
            }
        }

        /// <summary>
        /// 清除指定的船舶
        /// </summary>
        /// <param name="lVesNames">指定船舶名称列表</param>
        private void DeleteVessels(List<string> lVesNames)
        {
            Canvas oCanvas;
            foreach (string sName in lVesNames)
            {
                if (this.dChangableCanvases.ContainsKey(sName))
                {
                    oCanvas = this.dChangableCanvases[sName];
                    this.oSimCanvas.Children.Remove(oCanvas);
                    this.dChangableRectangles.Remove(sName);
                }
            }
        }

        /// <summary>
        /// 清除指定的场桥
        /// </summary>
        /// <param name="lASCNames">指定 ASC 名称列表</param>
        private void DeleteASCs(List<string> lASCNames)
        {
            Canvas oCanvas;
            foreach (string sName in lASCNames)
            {
                if (this.dChangableCanvases.ContainsKey(sName))
                {
                    oCanvas = this.dChangableCanvases[sName];
                    this.oSimCanvas.Children.Remove(oCanvas);
                    this.dChangableRectangles.Remove(sName);
                }
            }
        }

        #endregion


        #region Refresh

        /// <summary>
        /// 刷新码头区域
        /// </summary>
        /// <param name="oTR"></param>
        private void RefreshTerminalRegion(TerminalRegion oTR)
        {
            if (oTR == null)
                this.DeleteTerminalRegion();
            else
                if (!this.dChangableRectangles.ContainsKey("LandScope") && !this.dChangableRectangles.ContainsKey("WaterScope"))
                    this.CreateTerminalRegion(oTR);
        }

        /// <summary>
        /// 刷新磁钉
        /// </summary>
        /// <param name="lTPs">刷新后的磁钉对象列表</param>
        private void RefreshTransponders(List<SimTransponder> lTPs)
        {
            List<string> lDeleteNames;
            List<uint> lDeleteIDs;
            List<SimTransponder> lCreateObjs, lRenewObjs;

            lDeleteNames = new List<string>();
            lDeleteIDs = new List<uint>();
            foreach (uint uKey in this.dTPPoints.Keys)
            {
                if (!lTPs.Exists(u => u.ID == uKey) && !lDeleteIDs.Contains(uKey))
                    lDeleteIDs.Add(uKey);
            }
            foreach (string sName in this.dChangableRectangles.Keys)
            {
                if (!lTPs.Exists(u => u.Name == sName) && !lDeleteNames.Contains(sName))
                    lDeleteNames.Add(sName);
            }
            if (lDeleteIDs.Count > 0 || lDeleteNames.Count > 0)
                this.DeleteTransponders(lDeleteNames, lDeleteIDs);

            lCreateObjs = new List<SimTransponder>();
            lRenewObjs = new List<SimTransponder>();
            foreach (SimTransponder oTP in lTPs)
            {
                if (!this.dChangableRectangles.ContainsKey(oTP.Name) && !lCreateObjs.Exists(u => u.Name == oTP.Name))
                    lCreateObjs.Add(oTP);
                else if (this.dChangableRectangles.ContainsKey(oTP.Name) && !lRenewObjs.Exists(u => u.Name == oTP.Name))
                    lRenewObjs.Add(oTP);
            }
            if (lCreateObjs.Count > 0)
                this.CreateTransponders(lCreateObjs);
            if (lRenewObjs.Count > 0)
                this.RenewTransponders(lRenewObjs);
        }

        /// <summary>
        /// 刷新箱堆
        /// </summary>
        /// <param name="lPs">刷新后的箱堆对象列表</param>
        private void RefreshPiles(List<Pile> lPs)
        {
            List<string> lDeleteNames;
            List<Pile> lCreateObjs, lRenewObjs;

            lDeleteNames = new List<string>();
            foreach (string sName in this.dChangableRectangles.Keys)
            {
                if (sName.Substring(0, 1) == "A" && sName.Length == 7)
                {
                    if (!lPs.Exists(u => u.Name == sName) && !lDeleteNames.Contains(sName))
                        lDeleteNames.Add(sName);
                }
            }
            if (lDeleteNames.Count > 0)
                this.DeletePiles(lDeleteNames);

            lCreateObjs = new List<Pile>();
            lRenewObjs = new List<Pile>();
            foreach (Pile oP in lPs)
            {
                if (!this.dChangableRectangles.ContainsKey(oP.Name) && !lCreateObjs.Exists(u => u.Name == oP.Name))
                    lCreateObjs.Add(oP);
                else if (this.dChangableRectangles.ContainsKey(oP.Name) && !lRenewObjs.Exists(u => u.Name == oP.Name))
                    lRenewObjs.Add(oP);
            }
            if (lCreateObjs.Count > 0)
                this.CreatePiles(lCreateObjs);
            if (lRenewObjs.Count > 0)
                this.RenewPiles(lRenewObjs);
        }

        /// <summary>
        /// 刷新 AGV
        /// </summary>
        /// <param name="lAGVs">刷新后的AGV对象列表</param>
        private void RefreshAGVs(List<AGV> lAGVs)
        {
            List<string> lDeleteNames;
            List<AGV> lCreateObjs, lRenewObjs;

            lDeleteNames = new List<string>();
            foreach (string sName in this.dChangableRectangles.Keys)
            {
                if (sName.Substring(0, 3) == "AGV")
                {
                    if (!lAGVs.Exists(u => u.Name == sName) && !lDeleteNames.Contains(sName))
                        lDeleteNames.Add(sName);
                }
            }
            if (lDeleteNames.Count > 0)
                this.DeletePiles(lDeleteNames);

            lCreateObjs = new List<AGV>();
            lRenewObjs = new List<AGV>();
            foreach (AGV oA in lAGVs)
            {
                if (!this.dChangableLabels.ContainsKey(oA.Name) && !lCreateObjs.Exists(u => u.Name == oA.Name))
                    lCreateObjs.Add(oA);
                else if (this.dChangableLabels.ContainsKey(oA.Name) && !lRenewObjs.Exists(u => u.Name == oA.Name))
                    lRenewObjs.Add(oA);
            }
            if (lCreateObjs.Count > 0)
                this.CreateAGVs(lCreateObjs);
            if (lRenewObjs.Count > 0)
                this.RenewAGVs(lRenewObjs);
        }

        /// <summary>
        /// 刷新 Lane
        /// </summary>
        /// <param name="lLanes">刷新后的Lane对象列表</param>
        private void RefreshLanes(List<Lane> lLanes)
        {
            List<string> lDeleteLaneNames, lDeleteSignNames;
            List<Lane> lCreateObjs, lRenewObjs;

            lDeleteLaneNames = new List<string>();
            foreach (string sKey in this.dChangableRectangles.Keys)
            {
                if (sKey.Substring(0, 4) == "Lane" && !lDeleteLaneNames.Contains(sKey))
                    lDeleteLaneNames.Add(sKey);
            }
            lDeleteSignNames = new List<string>();
            foreach (string sKey in this.dChangableEllipses.Keys)
            {
                if (sKey.Substring(0, 11) == "LaneDirSign" && !lDeleteSignNames.Contains(sKey))
                    lDeleteSignNames.Add(sKey);
            }
            if (lDeleteLaneNames.Count > 0 || lDeleteSignNames.Count > 0)
                this.DeleteLanes(lDeleteLaneNames, lDeleteSignNames);

            lCreateObjs = new List<Lane>();
            lRenewObjs = new List<Lane>();
            foreach (Lane oL in lLanes)
            {
                if (!this.dChangableLabels.ContainsKey(oL.Name) && !lCreateObjs.Exists(u => u.Name == oL.Name))
                    lCreateObjs.Add(oL);
                else if (this.dChangableLabels.ContainsKey(oL.Name) && !lRenewObjs.Exists(u => u.Name == oL.Name))
                    lRenewObjs.Add(oL);
            }
            if (lCreateObjs.Count > 0)
                this.CreateLanes(lCreateObjs);
            if (lRenewObjs.Count > 0)
                this.RenewLanes(lRenewObjs);
        }

        /// <summary>
        /// 刷新 Mate
        /// </summary>
        /// <param name="lMates">刷新后的Mate列表</param>
        private void RefreshMates(List<Mate> lMates)
        {
            List<string> lDeleteNames;
            List<Mate> lCreateObjs, lRenewObjs;

            lDeleteNames = new List<string>();
            foreach (string sName in this.dChangableRectangles.Keys)
            {
                if (sName.Substring(0, 4) == "Mate")
                {
                    if (!lMates.Exists(u => u.Name == sName) && !lDeleteNames.Contains(sName))
                        lDeleteNames.Add(sName);
                }
            }
            if (lDeleteNames.Count > 0)
                this.DeleteMates(lDeleteNames);

            lCreateObjs = new List<Mate>();
            lRenewObjs = new List<Mate>();
            foreach (Mate oM in lMates)
            {
                if (!this.dChangableLabels.ContainsKey(oM.Name) && !lCreateObjs.Exists(u => u.Name == oM.Name))
                    lCreateObjs.Add(oM);
                else if (this.dChangableLabels.ContainsKey(oM.Name) && !lRenewObjs.Exists(u => u.Name == oM.Name))
                    lRenewObjs.Add(oM);
            }
            if (lCreateObjs.Count > 0)
                this.CreateMates(lCreateObjs);
            if (lRenewObjs.Count > 0)
                this.RenewMates(lRenewObjs);
        }

        /// <summary>
        /// 刷新 QC
        /// </summary>
        /// <param name="lQCs">刷新后的QC列表</param>
        private void RefreshQCs(List<QCDT> lQCs)
        {
            List<string> lDeleteNames;
            List<QCDT> lCreateObjs, lRenewObjs;

            lDeleteNames = new List<string>();
            foreach (string sName in this.dChangableCanvases.Keys)
            {
                if (sName.Substring(0, 2) == "QC")
                {
                    if (!lQCs.Exists(u => u.Name == sName) && !lDeleteNames.Contains(sName))
                        lDeleteNames.Add(sName);
                }
            }
            if (lDeleteNames.Count > 0)
                this.DeleteQCs(lDeleteNames);

            lCreateObjs = new List<QCDT>();
            lRenewObjs = new List<QCDT>();
            foreach (QCDT oQC in lQCs)
            {
                if (!this.dChangableCanvases.ContainsKey(oQC.Name) && !lCreateObjs.Exists(u => u.Name == oQC.Name))
                    lCreateObjs.Add(oQC);
                else if (this.dChangableCanvases.ContainsKey(oQC.Name) && !lRenewObjs.Exists(u => u.Name == oQC.Name))
                    lRenewObjs.Add(oQC);
            }
            if (lCreateObjs.Count > 0)
                this.CreateQCs(lCreateObjs);
            if (lRenewObjs.Count > 0)
                this.RenewQCs(lRenewObjs);
        }

        /// <summary>
        /// 刷新 ASC
        /// </summary>
        /// <param name="lASCs">刷新后的ASC列表</param>
        private void RefreshASCs(List<ASC> lASCs)
        {
            List<string> lDeleteNames;
            List<ASC> lCreateObjs, lRenewObjs;

            lDeleteNames = new List<string>();
            foreach (string sName in this.dChangableCanvases.Keys)
            {
                if (sName.Substring(0, 3) == "ASC")
                {
                    if (!lASCs.Exists(u => u.Name == sName) && !lDeleteNames.Contains(sName))
                        lDeleteNames.Add(sName);
                }
            }
            if (lDeleteNames.Count > 0)
                this.DeleteASCs(lDeleteNames);

            lCreateObjs = new List<ASC>();
            lRenewObjs = new List<ASC>();
            foreach (ASC oASC in lASCs)
            {
                if (!this.dChangableCanvases.ContainsKey(oASC.Name) && !lCreateObjs.Exists(u => u.Name == oASC.Name))
                    lCreateObjs.Add(oASC);
                else if (this.dChangableCanvases.ContainsKey(oASC.Name) && !lRenewObjs.Exists(u => u.Name == oASC.Name))
                    lRenewObjs.Add(oASC);
            }
            if (lCreateObjs.Count > 0)
                this.CreateASCs(lCreateObjs);
            if (lRenewObjs.Count > 0)
                this.RenewASCs(lRenewObjs);
        }

        /// <summary>
        /// 刷新 Vessel
        /// </summary>
        /// <param name="lVeses">刷新后的Vessel列表</param>
        private void RefreshVessels(List<Vessel> lVessels)
        {
            List<string> lDeleteNames;
            List<Vessel> lCreateObjs, lRenewObjs;

            lDeleteNames = new List<string>();
            foreach (string sName in this.dChangableCanvases.Keys)
            {
                if (sName.Substring(0, 6) == "Vessel")
                {
                    if (!lVessels.Exists(u => u.Name == sName) && !lDeleteNames.Contains(sName))
                        lDeleteNames.Add(sName);
                }
            }
            if (lDeleteNames.Count > 0)
                this.DeleteVessels(lDeleteNames);

            lCreateObjs = new List<Vessel>();
            lRenewObjs = new List<Vessel>();
            foreach (Vessel oVes in lVessels)
            {
                if (!this.dChangableCanvases.ContainsKey(oVes.Name) && !lCreateObjs.Exists(u => u.Name == oVes.Name))
                    lCreateObjs.Add(oVes);
                else if (this.dChangableCanvases.ContainsKey(oVes.Name) && !lRenewObjs.Exists(u => u.Name == oVes.Name))
                    lRenewObjs.Add(oVes);
            }
            if (lCreateObjs.Count > 0)
                this.CreateVessels(lCreateObjs);
            if (lRenewObjs.Count > 0)
                this.RenewVessels(lRenewObjs);
        }




        #endregion
    }
}
