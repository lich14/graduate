using ZECS.Schedule.DBDefine.Schedule;

namespace ZECS.Schedule.ECSSchedule
{
    public class Agv
    {
        public AGV_STATUS Status { get; set; }
        public Agv(AGV_STATUS status)
        {
            Status = status;
        }

        public bool CanBeScheduled()
        {
            if (Status == null)
            {
                return false;
            }

            if (Status.OPERATIONAL_STATUS != Operational_Status.AUTOMATIC)
            {
                return false;
            }

            if (Status.TECHNICAL_STATUS == Technical_Status.RED
                || Status.TECHNICAL_STATUS == Technical_Status.ORANGE)
            {
                return false;
            }

            return true;
        }

        public override string ToString()
        {
            return Status != null ? Status.ToString() : "";
        }
    }
}