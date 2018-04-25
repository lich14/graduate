using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DBDefine.Schedule;
namespace ZECS.Schedule.Define.DBDefine.CiTOS
{

    [Serializable]
    public class ScheduleStatus : DBClass_BASE
    {
        public virtual string SERVICE_STATUS { get; set; }
        public virtual string IS_SCHEDULING { get; set; }
        public virtual string FORCE_SCHEDULE_PAUSE { get; set; }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            ScheduleStatus scheduleStatus = other as ScheduleStatus;
            base.Copy(scheduleStatus);
            this.SERVICE_STATUS = scheduleStatus.SERVICE_STATUS;
            this.IS_SCHEDULING = scheduleStatus.IS_SCHEDULING;
            this.FORCE_SCHEDULE_PAUSE = scheduleStatus.FORCE_SCHEDULE_PAUSE;

        }

        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "SERVICE_STATUS":
                        SERVICE_STATUS = Convert.ToString(propertyValue);
                        break;
                    case "IS_SCHEDULING":
                        IS_SCHEDULING = Convert.ToString(propertyValue);
                        break;
                    case "FORCE_SCHEDULE_PAUSE":
                        FORCE_SCHEDULE_PAUSE = Convert.ToString(propertyValue);
                        break;

                }
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
