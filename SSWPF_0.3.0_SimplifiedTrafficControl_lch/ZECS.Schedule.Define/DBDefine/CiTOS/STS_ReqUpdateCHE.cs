using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.DBDefine.CiTOS
{
    [Serializable]
    public class STS_ReqUpdateCHE
    {
        public virtual Int64 ID { get; set; }
        public virtual string MESSAGE_ID { get; set; }
        public virtual string JOB_ID { get; set; }
        public virtual string CHE_TYPE { get; set; }
        public virtual string CHE_ID { get; set; }
        public virtual string WORK_MODE { get; set; }
        public virtual string WORK_STATUS { get; set; }
        public virtual string WORK_STATUS_PARA { get; set; }
        public virtual string LOC_BAY_TYPE { get; set; }
        public virtual string LOC_BAY { get; set; }
        public virtual string SEQUENCE_STATUS { get; set; }
        public virtual string SEQUENCE_STATUS_PARA { get; set; }
        public virtual DateTime DATETIME { get; set; }
    }
}
