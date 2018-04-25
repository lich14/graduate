using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DBDefine.Schedule;

namespace ZECS.Schedule.DBDefine.CiTOS
{
    //update sq add DBClass_BASE 和SetPropertyValue copy方法
    [Serializable]
    public class AGV_ResJob : DBClass_BASE
    {
        public virtual string ORDER_ID { get; set; }
        public virtual string COMMAND_ID { get; set; }
        public virtual Int64 ID { get; set; }
        public virtual string MESSAGE_ID { get; set; }
        public virtual string JOB_TYPE { get; set; }
        public virtual string JOB_ID { get; set; }
        public virtual string RESERVE { get; set; }
        public virtual string JOB_LINK { get; set; }
        public virtual string CHE_ID { get; set; }
        public virtual string YARD_ID { get; set; }
        public virtual string QUAY_ID { get; set; }
        public virtual int PRIORITY { get; set; }
        public virtual string JOB_STATUS { get; set; }
        public virtual string FROM_TRUCK_TYPE { get; set; }
        public virtual string FROM_TRUCK_ID { get; set; }
        public virtual string FROM_TRUCK_POS { get; set; }
        public virtual string FROM_BLOCK { get; set; }
        public virtual string FROM_BAY_TYPE { get; set; }
        public virtual string FROM_BAY { get; set; }
        public virtual string FROM_LANE { get; set; }
        public virtual string FROM_TIER { get; set; }
        public virtual string TO_TRUCK_ID { get; set; }
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
        public virtual DateTime PLAN_START_TIME { get; set; }
        public virtual DateTime PLAN_END_TIME { get; set; }
        public virtual DateTime DATETIME { get; set; }
        public virtual int VERSION { get; set; }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            AGV_ResJob resJob = other as AGV_ResJob;
            base.Copy(resJob);
            this.ORDER_ID = resJob.ORDER_ID;
            this.COMMAND_ID = resJob.COMMAND_ID;
            this.ID = resJob.ID;
            this.MESSAGE_ID = resJob.MESSAGE_ID;
            this.JOB_TYPE = resJob.JOB_TYPE;
            this.JOB_ID = resJob.JOB_ID;
            this.RESERVE = resJob.RESERVE;
            this.JOB_LINK = resJob.JOB_LINK;
            this.CHE_ID = resJob.CHE_ID;
            this.YARD_ID = resJob.YARD_ID;
            this.QUAY_ID = resJob.QUAY_ID;
            this.PRIORITY = resJob.PRIORITY;
            this.JOB_STATUS = resJob.JOB_STATUS;
            this.FROM_TRUCK_TYPE = resJob.FROM_TRUCK_TYPE;
            this.FROM_TRUCK_ID = resJob.FROM_TRUCK_ID;
            this.FROM_TRUCK_POS = resJob.FROM_TRUCK_POS;
            this.FROM_BLOCK = resJob.FROM_BLOCK;
            this.FROM_BAY_TYPE = resJob.FROM_BAY_TYPE;
            this.FROM_BAY = resJob.FROM_BAY;
            this.FROM_LANE = resJob.FROM_LANE;
            this.FROM_TIER = resJob.FROM_TIER;
            this.TO_TRUCK_ID = resJob.TO_TRUCK_ID;
            this.TO_TRUCK_TYPE = resJob.TO_TRUCK_TYPE;
            this.TO_TRUCK_POS = resJob.TO_TRUCK_POS;
            this.TO_BLOCK = resJob.TO_BLOCK;
            this.TO_BAY_TYPE = resJob.TO_BAY_TYPE;
            this.TO_BAY = resJob.TO_BAY;
            this.TO_LANE = resJob.TO_LANE;
            this.TO_TIER = resJob.TO_TIER;
            this.CONTAINER_ID = resJob.CONTAINER_ID;
            this.CONTAINER_ISO = resJob.CONTAINER_ISO;
            this.CONTAINER_LENGTH = resJob.CONTAINER_LENGTH;
            this.CONTAINER_HEIGHT = resJob.CONTAINER_HEIGHT;
            this.CONTAINER_WEIGHT = resJob.CONTAINER_WEIGHT;
            this.CONTAINER_IS_EMPTY = resJob.CONTAINER_IS_EMPTY;
            this.CONTAINER_DOOR_DIRECTION = resJob.CONTAINER_DOOR_DIRECTION;
            this.PLAN_START_TIME = resJob.PLAN_START_TIME;
            this.PLAN_END_TIME = resJob.PLAN_END_TIME;
            this.DATETIME = resJob.DATETIME;
            this.VERSION = resJob.VERSION;

        }

        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "ORDER_ID":
                        ORDER_ID = Convert.ToString(propertyValue);
                        break;
                    case "COMMAND_ID":
                        COMMAND_ID = Convert.ToString(propertyValue);
                        break;
                    case "ID":
                        ID = Convert.ToInt64(propertyValue);
                        break;
                    case "MESSAGE_ID":
                        MESSAGE_ID = Convert.ToString(propertyValue);
                        break;
                    case "JOB_TYPE":
                        JOB_TYPE = Convert.ToString(propertyValue);
                        break;

                    case "JOB_ID":
                        JOB_ID = Convert.ToString(propertyValue);
                        break;
                    case "RESERVE":
                        RESERVE = Convert.ToString(propertyValue);
                        break;
                    case "JOB_LINK":
                        JOB_LINK = Convert.ToString(propertyValue);
                        break;
                    case "CHE_ID":
                        CHE_ID = Convert.ToString(propertyValue).Trim();
                        break;
                    case "YARD_ID":
                        YARD_ID = Convert.ToString(propertyValue);
                        break;
                    case "QUAY_ID":
                        QUAY_ID = Convert.ToString(propertyValue);
                        break;
                    case "PRIORITY":
                        PRIORITY = Convert.ToInt32(propertyValue);
                        break;
                    case "JOB_STATUS":
                        JOB_STATUS = Convert.ToString(propertyValue);
                        break;
                    case "FROM_TRUCK_TYPE":
                        FROM_TRUCK_TYPE = Convert.ToString(propertyValue);
                        break;
                    case "FROM_TRUCK_ID":
                        FROM_TRUCK_ID = Convert.ToString(propertyValue);
                        break;
                    case "FROM_TRUCK_POS":
                        FROM_TRUCK_POS = Convert.ToString(propertyValue);
                        break;
                    case "FROM_BLOCK":
                        FROM_BLOCK = Convert.ToString(propertyValue);
                        break;
                    case "FROM_BAY_TYPE":
                        FROM_BAY_TYPE = Convert.ToString(propertyValue);
                        break;
                    case "FROM_BAY":
                        FROM_BAY = Convert.ToString(propertyValue);
                        break;
                    case "FROM_LANE":
                        FROM_LANE = Convert.ToString(propertyValue);
                        break;
                    case "FROM_TIER":
                        FROM_TIER = Convert.ToString(propertyValue);
                        break;
                    case "TO_TRUCK_ID":
                        TO_TRUCK_ID = Convert.ToString(propertyValue);
                        break;
                    case "TO_TRUCK_TYPE":
                        TO_TRUCK_TYPE = Convert.ToString(propertyValue);
                        break;
                    case "TO_TRUCK_POS":
                        TO_TRUCK_POS = Convert.ToString(propertyValue);
                        break;
                    case "TO_BLOCK":
                        TO_BLOCK = Convert.ToString(propertyValue);
                        break;
                    case "TO_BAY_TYPE":
                        TO_BAY_TYPE = Convert.ToString(propertyValue);
                        break;
                    case "TO_BAY":
                        TO_BAY = Convert.ToString(propertyValue);
                        break;
                    case "TO_LANE":
                        TO_LANE = Convert.ToString(propertyValue);
                        break;
                    case "TO_TIER":
                        TO_TIER = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_ID":
                        CONTAINER_ID = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_ISO":
                        CONTAINER_ISO = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_LENGTH":
                        CONTAINER_LENGTH = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_HEIGHT":
                        CONTAINER_HEIGHT = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_WEIGHT":
                        CONTAINER_WEIGHT = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_IS_EMPTY":
                        CONTAINER_IS_EMPTY = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_DOOR_DIRECTION":
                        CONTAINER_DOOR_DIRECTION = Convert.ToString(propertyValue);
                        break;
                    case "PLAN_START_TIME":
                        PLAN_START_TIME = Convert.ToDateTime(propertyValue);
                        break;
                    case "PLAN_END_TIME":
                        PLAN_END_TIME = Convert.ToDateTime(propertyValue);
                        break;
                    case "DATETIME":
                        DATETIME = Convert.ToDateTime(propertyValue);
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
