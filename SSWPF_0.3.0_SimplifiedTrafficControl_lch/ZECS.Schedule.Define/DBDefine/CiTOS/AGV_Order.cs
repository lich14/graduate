using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.Define.DBDefine.CiTOS
{
    public class AGV_Order
    {
        public virtual string ORDER_ID { get; set; }
        public virtual string COMMAND_ID { get; set; }
        public virtual string ORDER_VERSION { get; set; }
        public virtual string COMMAND_VERSION { get; set; }
        public virtual string JOB_TYPE { get; set; }
        public virtual string JOB_ID { get; set; }
        public virtual string JOB_LINK { get; set; }
        public virtual string CHE_ID { get; set; }
        public virtual string YARD_ID { get; set; }
        public virtual string QUAY_ID { get; set; }
        public virtual int PRIORITY { get; set; }
        public virtual string JOB_STATUS { get; set; }
        public virtual string FROM_TRUCK_TYPE { get; set; }
        public virtual string FROM_TRUCK_ID { get; set; }
        public virtual string FROM_RFID { get; set; }
        public virtual string FROM_TRUCK_POS { get; set; }
        public virtual string FROM_BLOCK { get; set; }
        public virtual string FROM_BAY_TYPE { get; set; }
        public virtual string FROM_BAY { get; set; }
        public virtual string FROM_LANE { get; set; }
        public virtual string FROM_TIER { get; set; }
        public virtual string TO_TRUCK_ID { get; set; }
        public virtual string TO_RFID { get; set; }
        public virtual string TO_TRUCK_TYPE { get; set; }
        public virtual string TO_TRUCK_POS { get; set; }
        public virtual string TO_BLOCK { get; set; }
        public virtual string TO_BAY_TYPE { get; set; }
        public virtual string TO_BAY { get; set; }
        public virtual string TO_LANE { get; set; }
        public virtual string TO_TIER { get; set; }
        public virtual string CONTAINER_ID { get; set; }
        public virtual string CONTAINER_ISO { get; set; }
        public virtual string CONTAINER_LENGTH { get; set; }
        public virtual string CONTAINER_HEIGHT { get; set; }
        public virtual string CONTAINER_WEIGHT { get; set; }
        public virtual string CONTAINER_IS_EMPTY { get; set; }
        public virtual string CONTAINER_DOOR_DIRECTION { get; set; }
        public virtual string QC_REFID { get; set; }
        public virtual DateTime PLAN_START_TIME { get; set; }
        public virtual DateTime PLAN_END_TIME { get; set; }
        public virtual DateTime DATETIME { get; set; }
    }
}
