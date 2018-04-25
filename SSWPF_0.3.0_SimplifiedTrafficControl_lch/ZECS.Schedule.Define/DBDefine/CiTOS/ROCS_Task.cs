using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.DBDefine.CiTOS
{
        [Serializable]
         public enum ROCS_TaskType
        {
            Unknown = 0,
            ROS_PickUp = 1,
            ROS_Ground = 2,
            MROS_General = 3,
            MROS_Serious = 4,
            ROS_RepeatLanding = 5,
            MROS_DataViewConnectARMG = 6,
            MROS_BMSGeneral = 7,
            MROS_BMSSerious = 8,
            MROS_DataViewConnectARMGReadOnly=9


        }

        [Serializable]
        public enum ROCS_TaskStatus
        {
            GenerateButUndistributed = 0,
            DistributedButUnaccept = 1,
            AcceptAndExecuting = 2,
            Cancel = 3,
            Complete = 4,
            Abort = 5
        }

    [Serializable]
    public class ROCS_Task
    {
        public virtual Int64 ID { get; set; }

        public virtual Int64 TaskID { get; set; }

       // public virtual int Lane_No { get; set; }

        public virtual int RMG_No { get; set; }

        public virtual ROCS_TaskType TaskType { get; set; }

        public virtual ROCS_TaskStatus TaskStatus { get; set; }

        public virtual string ContainerNo { get; set; }

        public virtual int RCS_No { get; set; }

        public virtual DateTime TaskHappenTime { get; set; }

        public virtual DateTime TaskAssignTime { get; set; }

        public virtual DateTime TaskFinishTime { get; set; }
    }
}
