using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace SSWPF.Define
{
    // AGVManager 给 TosManager 的消息，为进口箱分配堆场位置
    public class AlloPlanLocsForDiscContsEventArgs : EventArgs
    {
        public List<string> lContIDs;
        public bool IsSucc;

        public AlloPlanLocsForDiscContsEventArgs()
        {
            this.lContIDs = new List<string>();
        }

        public AlloPlanLocsForDiscContsEventArgs(List<string> lContIDs)
            : this()
        {
            this.lContIDs = lContIDs;
        }
    }

    // 给 YardManager 的消息，要求申请或者释放Pile
    public class PilesReclaimAndReleaseEventArgs : EventArgs
    {
        public List<PileReclaimMsg> lReclaimMsgs;
        public List<string> lReleasePileNames;
        public bool IsSucc;

        public PilesReclaimAndReleaseEventArgs()
        {
            this.lReclaimMsgs = new List<PileReclaimMsg>();
            this.lReleasePileNames = new List<string>();
        }

        public PilesReclaimAndReleaseEventArgs(List<PileReclaimMsg> lReclaimPileMsgsInput, List<string> lReleasePilesInput)
            : this()
        {
            if (lReclaimPileMsgsInput != null)
                this.lReclaimMsgs = lReclaimPileMsgsInput;
            if (lReleasePilesInput != null)
                this.lReleasePileNames = lReleasePilesInput;
        }
    }

    /// <summary>
    /// 给 Projector 的向 ViewFrame 投影消息
    /// </summary>
    public class ProjectToViewFrameEventArgs : EventArgs
    {
        public StatusEnums.ProjectType eProjectType;
        public ProjectPackageToViewFrame oPPTViewFrame;

        public ProjectToViewFrameEventArgs()
        {
        }
    }

    /// <summary>
    /// 给 Projector 的向 InfoFrame 投影的消息
    /// </summary>
    public class ProjectToInfoFrameEventArgs : EventArgs
    {
        public StatusEnums.ProjectType eProjectType;
        public ProjectPackageToInfoFrame oPPTInfoFrame;

        public ProjectToInfoFrameEventArgs()
        {
        }
    }

    /// <summary>
    /// 给 Projector 的向 SimPanel 投影的消息
    /// </summary>
    public class ProjectToSimPanelEventArgs : EventArgs
    {
        public ProjectPackageToSimPanel oPPTSimPanel;

        public ProjectToSimPanelEventArgs()
        {
        }

        public ProjectToSimPanelEventArgs(ProjectPackageToSimPanel oPPTSimPanel)
            : this()
        {
            this.oPPTSimPanel = oPPTSimPanel;
        }
    }

    /// <summary>
    /// 从 AGVManager 到 TrafficController，用于生成 AGV 路径
    /// </summary>
    public class GenerateAGVRouteEventArgs : EventArgs
    {
        public AGV oA;
        public uint AimLaneID;
        public bool IsGenerationSucc;
    }

    /// <summary>
    /// 重置所有 AGV 路径的事件参数
    /// </summary>
    public class ResetAGVRoutesEventArgs : EventArgs
    {
    }

    /// <summary>
    /// 删去 AGV 对 AGVLine 的占用段记录事件参数
    /// </summary>
    public class DeleteAGVOccupyLineSegArgs : EventArgs
    {
        public List<uint> lAGVIDs;

        public DeleteAGVOccupyLineSegArgs()
        {
            this.lAGVIDs = new List<uint>();
        }
    }

    /// <summary>
    /// 用户在 SimPanel 操作时生成的消息，提交给 SimConductor
    /// </summary>
    public class SimPanelToConductorEventArgs : EventArgs
    {
        public bool IsStartClicked;
        public bool IsStopClicked;
        public bool IsResetClicked;
        public bool IsInitClicked;
        public bool IsStepClicked;
        public bool? IsRealTimeChecked;
        public bool? IsInformChecked;
        public bool? IsAnimationChecked;
        public double? NewSpeedValue;

        public SimPanelToConductorEventArgs()
        {
            this.IsRealTimeChecked = null;
            this.IsInformChecked = null;
            this.IsAnimationChecked = null;
            this.NewSpeedValue = null;
        }
    }

    public class SimException : ApplicationException
    {
        public int ThrowTime { get; set; }
        public SimException()
            :base()
        {
        }

        public SimException(string StrInput)
            : base(StrInput)
        {
        }
    }
}
