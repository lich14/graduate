using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DBDefine.Schedule;

namespace ZECS.Schedule.DBDefine.CiTOS
{
    [Serializable]
    public class ASC_Task
    {
        public virtual Int64 ID { get; set; }
        public virtual ASC_ResJob Task { get; set; }
        public virtual ASC_Order Order { get; set; }
        public virtual TaskStatus TaskState { get; set; }
        public virtual int ErrorCode { get; set; }
        public virtual Int32 TaskProcess { get; set; }
        public virtual string ExceptionCode { get; set; } 
    }
}
