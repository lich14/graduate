using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DBDefine.Schedule;


namespace ZECS.Schedule.DBDefine.CiTOS
{
    public class AGV_ReqUpdateJob : DBClass_BASE
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
        public virtual string JOB_STATUS { get; set; }
        public virtual string EXCEPTION_CODE { get; set; }
        public virtual string FROM_TRUCK_ID { get; set; }
        public virtual string FROM_TRUCK_TYPE { get; set; }
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
        public virtual DateTime START_TIME { get; set; }
        public virtual DateTime END_TIME { get; set; }
        public virtual DateTime DATETIME { get; set; }
        public virtual int VERSION { get; set; }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            AGV_ReqUpdateJob updateJob = other as AGV_ReqUpdateJob;
            base.Copy(updateJob);
            this.ID = updateJob.ID;
            this.MESSAGE_ID = updateJob.MESSAGE_ID;
            this.JOB_TYPE = updateJob.JOB_TYPE;
            this.JOB_ID = updateJob.JOB_ID;
            this.RESERVE = updateJob.RESERVE;
            this.JOB_LINK = updateJob.JOB_LINK;
            this.CHE_ID = updateJob.CHE_ID;
            this.YARD_ID = updateJob.YARD_ID;
            this.QUAY_ID = updateJob.QUAY_ID;
            this.JOB_STATUS = updateJob.JOB_STATUS;
            this.FROM_TRUCK_TYPE = updateJob.FROM_TRUCK_TYPE;
            this.FROM_TRUCK_ID = updateJob.FROM_TRUCK_ID;
            this.FROM_TRUCK_POS = updateJob.FROM_TRUCK_POS;
            this.FROM_BLOCK = updateJob.FROM_BLOCK;
            this.FROM_BAY_TYPE = updateJob.FROM_BAY_TYPE;
            this.FROM_BAY = updateJob.FROM_BAY;
            this.FROM_LANE = updateJob.FROM_LANE;
            this.FROM_TIER = updateJob.FROM_TIER;
            this.TO_TRUCK_ID = updateJob.TO_TRUCK_ID;
            this.TO_TRUCK_TYPE = updateJob.TO_TRUCK_TYPE;
            this.TO_TRUCK_POS = updateJob.TO_TRUCK_POS;
            this.TO_BLOCK = updateJob.TO_BLOCK;
            this.TO_BAY_TYPE = updateJob.TO_BAY_TYPE;
            this.TO_BAY = updateJob.TO_BAY;
            this.TO_LANE = updateJob.TO_LANE;
            this.TO_TIER = updateJob.TO_TIER;
            this.CONTAINER_ID = updateJob.CONTAINER_ID;
            this.CONTAINER_ISO = updateJob.CONTAINER_ISO;
            this.CONTAINER_LENGTH = updateJob.CONTAINER_LENGTH;
            this.CONTAINER_HEIGHT = updateJob.CONTAINER_HEIGHT;
            this.CONTAINER_WEIGHT = updateJob.CONTAINER_WEIGHT;
            this.CONTAINER_IS_EMPTY = updateJob.CONTAINER_IS_EMPTY;
            this.CONTAINER_DOOR_DIRECTION = updateJob.CONTAINER_DOOR_DIRECTION;
            this.START_TIME = updateJob.START_TIME;
            this.END_TIME = updateJob.END_TIME;
            this.DATETIME = updateJob.DATETIME;
            this.VERSION = updateJob.VERSION;
            this.EXCEPTION_CODE = updateJob.EXCEPTION_CODE;

        }

        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
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
                        CHE_ID = Convert.ToString(propertyValue);
                        break;
                    case "YARD_ID":
                        YARD_ID = Convert.ToString(propertyValue);
                        break;
                    case "QUAY_ID":
                        QUAY_ID = Convert.ToString(propertyValue);
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
                    case "START_TIME":
                        START_TIME = Convert.ToDateTime(propertyValue);
                        break;
                    case "END_TIME":
                        END_TIME = Convert.ToDateTime(propertyValue);
                        break;
                    case "DATETIME":
                        DATETIME = Convert.ToDateTime(propertyValue);
                        break;
                    case "EXCEPTION_CODE":
                        EXCEPTION_CODE = Convert.ToString(propertyValue);
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
