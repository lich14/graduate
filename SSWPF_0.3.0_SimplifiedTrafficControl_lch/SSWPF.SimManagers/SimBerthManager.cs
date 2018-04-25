using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SSWPF.Define;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using SharpSim;

namespace SSWPF.SimManagers
{
    /// <summary> 
    /// 管靠泊的离泊的，维护视图 ViewBerthStatus。可能修改岸桥的作业位置 dQCWorkPoints，暂缓实现
    /// </summary>
    public class SimBerthManager
    {
        public event EventHandler<ProjectToViewFrameEventArgs> ProjectToViewFrameEvent;
        public event EventHandler<ProjectToInfoFrameEventArgs> ProjectToInfoFrameEvent;

        private SimDataStore oSimDataStore;

        private readonly double QCYBasePos = 309.2;

        public SimBerthManager()
        {
        }

        public SimBerthManager(SimDataStore oSimDataStore)
            :this()
        {
            this.oSimDataStore = oSimDataStore;
        }

        /// <summary>
        /// 初始化函数。检查定义，并默认靠泊第一条船
        /// </summary>
        /// <returns></returns>
        public bool Init()
        {
            if (this.oSimDataStore == null)
            {
                Logger.Simulate.Error("SimBerthManager: Null SimDataStore!");
                return false;
            }

            if (this.oSimDataStore.dVessels.Count(u => u.Value.eVesselVisitPhrase == StatusEnums.VesselVisitPhrase.InPortArriving
                || u.Value.eVesselVisitPhrase == StatusEnums.VesselVisitPhrase.Forecasted) == 0)
            {
                Logger.Simulate.Error("SimBerthManager: No Vessel To Be Berthed!");
                return false;
            }

            return true;
        }


        /// <summary>
        /// 靠一艘船上来
        /// </summary>
        /// <returns></returns>
        public bool BerthOneVessel()
        {
            if (this.oSimDataStore.dVessels == null || this.oSimDataStore.dVessels.Count == 0)
                return false;

            foreach (uint VesID in this.oSimDataStore.dVessels.Keys)
            {
                if (this.BerthVesselByID(VesID))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 对编号为 VesID 的船舶执行靠泊操作
        /// </summary>
        /// <param name="VesID"></param>
        /// <returns></returns>
        public bool BerthVesselByID(uint VesID)
        {
            BERTH_STATUS oBS;

            if (!this.oSimDataStore.dVessels.ContainsKey(VesID))
                return false;

            if (this.IsVesselBerthable(VesID) 
                && this.GenerateBerthStatusFromVessel(this.oSimDataStore.dVessels[VesID], out oBS))
            {
                // 状态改变
                this.oSimDataStore.dVessels[VesID].eVesselVisitPhrase = StatusEnums.VesselVisitPhrase.AtBerthDoing;
                this.oSimDataStore.dVessels[VesID].Updated = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                oBS.VESSEL_VISIT_PHASE = StatusEnums.VesselVisitPhrase.AtBerthDoing.ToString();
                oBS.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                this.oSimDataStore.dViewBerthStatus.Add(oBS.VESSEL_NAME, oBS);
                
                // 界面显示
                this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                    {
                        eProjectType = StatusEnums.ProjectType.Create,
                        oPPTViewFrame = new ProjectPackageToViewFrame()
                        {
                            lBerthVessels = new List<Vessel>() { this.oSimDataStore.dVessels[VesID] }
                        }
                    });
                this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
                    {
                        eProjectType = StatusEnums.ProjectType.Create,
                        oPPTInfoFrame = new ProjectPackageToInfoFrame()
                        {
                            lBerthStatuses = new List<BERTH_STATUS>() { oBS }
                        }
                    });
                return true;
            }

            return false;
        }

        /// <summary>
        /// 对编号为 VesID 的船舶进行离泊操作
        /// </summary>
        /// <param name="VesID"></param>
        /// <returns></returns>
        public bool DepartVesselByID(uint VesID)
        {
            BERTH_STATUS oBS;

            if (!this.oSimDataStore.dVessels.ContainsKey(VesID) || !this.oSimDataStore.dViewBerthStatus.ContainsKey(this.oSimDataStore.dVessels[VesID].ShipName))
                return false;

            if (this.oSimDataStore.dVessels[VesID].eVesselVisitPhrase == StatusEnums.VesselVisitPhrase.AtBerthDone)
            {
                // 状态改变
                this.oSimDataStore.dVessels[VesID].eVesselVisitPhrase = StatusEnums.VesselVisitPhrase.Departed;
                this.oSimDataStore.dVessels[VesID].Updated = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

                oBS = this.oSimDataStore.dViewBerthStatus[this.oSimDataStore.dVessels[VesID].ShipName];
                oBS.VESSEL_VISIT_PHASE = StatusEnums.VesselVisitPhrase.Departed.ToString();
                oBS.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                
                // 界面显示
                this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Delete,
                    oPPTViewFrame = new ProjectPackageToViewFrame()
                    {
                        lBerthVessels = new List<Vessel>() { this.oSimDataStore.dVessels[VesID] }
                    }
                });
                this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Delete,
                    oPPTInfoFrame = new ProjectPackageToInfoFrame()
                    {
                        lBerthStatuses = new List<BERTH_STATUS>() { oBS }
                    }
                });

                this.oSimDataStore.dViewBerthStatus.Remove(oBS.VESSEL_NAME);

                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断编号为 BerthID 的船舶是否能在泊位停靠
        /// </summary>
        /// <param name="VesID">船舶编号</param>
        /// <returns>能靠泊返回true，否则返回false</returns>
        private bool IsVesselBerthable(uint VesID)
        {
            double BerthStart, BerthEnd, CurrStart, CurrEnd;

            if (this.oSimDataStore.dVessels[VesID].eVesselVisitPhrase != StatusEnums.VesselVisitPhrase.InPortArriving)
                return false;

            if (this.oSimDataStore.dViewBerthStatus.Count == 0)
                return true;

            BerthStart = this.oSimDataStore.dVessels[VesID].BeginMeter;
            BerthEnd = this.oSimDataStore.dVessels[VesID].EndMeter;

            foreach (BERTH_STATUS oBS in this.oSimDataStore.dViewBerthStatus.Values)
            {
                CurrStart = Math.Min(oBS.BOW_BOLLARD_OFFSET_CM, oBS.STERN_BOLLARD_OFFSET_CM) / 100;
                CurrEnd = Math.Max(oBS.BOW_BOLLARD_OFFSET_CM, oBS.STERN_BOLLARD_OFFSET_CM) / 100;

                if (this.IsOverlap(BerthStart, BerthEnd, CurrStart, CurrEnd))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 判断区间是否重叠
        /// </summary>
        /// <param name="BerthStart">区间1起点</param>
        /// <param name="BerthEnd">区间1终点</param>
        /// <param name="CurrStart">区间2起点</param>
        /// <param name="CurrEnd">区间2终点</param>
        /// <returns>重叠返回true，不重叠返回false</returns>
        private bool IsOverlap(double BerthStart, double BerthEnd, double CurrStart, double CurrEnd)
        {
            double Temp;

            if (BerthStart > BerthEnd)
            {
                Temp = BerthStart;
                BerthStart = BerthEnd;
                BerthEnd = Temp;
            }

            if (CurrStart > CurrEnd)
            {
                Temp = CurrStart;
                CurrStart = CurrEnd;
                CurrEnd = Temp;
            }

            if (Math.Max(BerthStart, CurrStart) <= Math.Min(BerthEnd, CurrEnd))
                return true;

            return false;
        }

        /// <summary>
        /// 从 Vessel 生成 BERTH_STATUS
        /// </summary>
        /// <param name="oVes"></param>
        /// <param name="oBS"></param>
        /// <returns></returns>
        private bool GenerateBerthStatusFromVessel(Vessel oVes, out BERTH_STATUS oBS)
        {
            oBS = new BERTH_STATUS();
            oBS.VESSEL_NAME = oVes.ShipName;
            oBS.VESSEL_VISIT_PHASE = oVes.eVesselVisitPhrase.ToString();
            switch (oVes.eBerthWay)
            {
                case StatusEnums.BerthWay.L:
                    oBS.BOW_BOLLARD_OFFSET_CM = Convert.ToInt32(oVes.EndMeter);
                    oBS.STERN_BOLLARD_OFFSET_CM = Convert.ToInt32(oVes.BeginMeter);
                    break;
                case StatusEnums.BerthWay.R:
                    oBS.BOW_BOLLARD_OFFSET_CM = Convert.ToInt32(oVes.BeginMeter);
                    oBS.STERN_BOLLARD_OFFSET_CM = Convert.ToInt32(oVes.EndMeter);
                    break;
                default:
                    break;
            }
            oBS.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

            return true;
        }

    }
}
