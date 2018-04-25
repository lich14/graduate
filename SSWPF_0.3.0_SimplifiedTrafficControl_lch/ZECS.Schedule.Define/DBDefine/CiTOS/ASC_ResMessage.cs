using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MessageParser;

namespace ZECS.Schedule.DBDefine.CiTOS
{
    public class ASC_ResMessage
    {
        public virtual Int64 ID { get; set; }
        public virtual string MessageID { get; set; }
        public virtual string Action { get; set; }
        public virtual string MessageCode { get; set; }
        //public virtual string MessageValue { get; set; }
        public virtual MessageValue MessageValue { get; set; }
        public virtual DateTime DateTime { get; set; }
    }
}
