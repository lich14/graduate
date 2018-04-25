using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.DBDefine.CiTOS
{
    [Serializable]
    public class ECS_Container_Move_Log 
    {
        public virtual Int32 ID { get; set; }
        public virtual string CONTAINER_ID { get; set; }
        public virtual int CHE_NO { get; set; }
        public virtual string POSITION { get; set; }
        public virtual DateTime RECORD_TIME { get; set; }
        public virtual string DOOR_DIRCETION { get; set; }
    }
}
