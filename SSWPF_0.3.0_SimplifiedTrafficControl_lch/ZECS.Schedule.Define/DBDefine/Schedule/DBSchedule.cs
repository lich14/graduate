using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Reflection;
using System.ComponentModel;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.Define;

namespace ZECS.Schedule.DBDefine.Schedule
{

    /// <summary>
    /// 任务状态
    /// </summary>
    [Serializable]
    [Obsolete("Do not use this define.", true)]
    public enum JobStatus
    {
        Unknown = 0,
        New,                    // 新收到的任务 Navis Command状态变为ENTERED
        Ready,                  // 准备执行    Navis  Command状态变为WORKING
        RouteGenerated,         // 路径已生成
        OnTheWay,               // 开往目的地
        ArrivedDest,            // 到达目的地
        Transfer_In_Process,    // 正在交互
        Adjusting,          //  -> TCT
        AdjustOK,           //  -> TCT
        Completed,              // 任务完成     Navis Command状态变为Complete
        Exception,              // 异常
        Rejected,               // 拒绝
        Interrupted,            // 中断
        Abort,                  // 终止执行
        Cancel,                  //取消
        Manual_Cleared          //人工清除，调试程序用。
    };

    /// <summary>
    /// TOS的任务状态
    /// </summary>
    [Serializable]
    [Obsolete("Do not use this define.", true)]
    public enum TOS_JobStatus
    {
        Unknown,
        ENTERED,
        WORKING,
        COMPLETE,
        REJECTED,
        CANCELED,
        ABORTED
    }

    /// <summary>
    /// ResJob,Order 任务状态
    /// </summary>
    [Serializable]
    public enum ResJobStatus
    {
        Unknown,
        New,           //新加
        Update,        //更新
        Cancel         //取消
    };

    /// <summary>
    /// 任务类型
    /// </summary>
    [Serializable]
    public enum JobType
    {
        LOAD,                       // 装船
        DISC,                        // 卸船
        SBLOCK,                    //同场理箱
        DBLOCK,                  // 不同堆场间转场
        CHARGE,                  // 充电
        RECEIVE,                // 堆场收集卡箱
        DELIVERY,               // 堆场出箱到集卡
        PARK,                      // 停靠（无任务）
        REPOSITION,             // 
        DUAL_CYCLE,
        MAINTENANCE,       // 维修
        UNKNOWN             // 未知
    }

     

    [Serializable]
    public class DBClass_BASE
    {
        //设置对象属性，子类需重写
        public virtual bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            return false;
        }
        //转换enum类型，子类无需重写
        public virtual T ConvertEnum<T>(object propertyValue)
        {
            if (propertyValue == null)
                return default(T);

            String strValue = propertyValue.ToString();
            return (T)Enum.Parse(typeof(T), strValue, true);
        }
        //克隆复制，子类无需重写
        public virtual DBClass_BASE Clone()
        {
            DBClass_BASE newDB = new DBClass_BASE();
            newDB.Copy(this);
            return newDB;
        }
        //复制拷贝，子类需重写
        public virtual void Copy(DBClass_BASE other)
        {  
        }
        //获取Hashtable数据，
        //属性名（数据表列名） ==》数值
        public virtual Hashtable GetHashtableValue()
        {
            Hashtable ht = new Hashtable();
            foreach (System.Reflection.PropertyInfo stsCommandProperty in GetType().GetProperties())
            {
                ht[stsCommandProperty.Name] = stsCommandProperty.GetValue(this, null);

                if (ht[stsCommandProperty.Name] == null)
                {
                    ht[stsCommandProperty.Name] = "";
                }
            } 

            return ht;
        }

          
    }

    public class ConvertDbData<T> where T : DBClass_BASE, new()
    {

        /// <summary>
        /// 将数据库查询到的内容，转换成列表
        /// </summary>
        /// <param name="dataSet">从数据库读取到的数据</param>
        /// <param name="type">需要转换成的类型</param>
        /// <returns>数据列表</returns>
        public static List<T> ConvertDataToObjectList(DataSet dataSet)
        {
            List<T> objList = new List<T>();
            try
            {
                if (dataSet == null ||
                    dataSet.Tables == null
                    || dataSet.Tables.Count == 0)
                    return null;

                DataTable dt = dataSet.Tables[0];
                DataColumnCollection Columns = dt.Columns;

                for (int r = 0; r < dt.Rows.Count; r++)
                {
                    T newObj = CreateObj(dt.Rows[r], Columns);
                    if (newObj != null)
                        objList.Add(newObj);
                }

                return objList;
            }
            catch (Exception ex)
            {
                Logger.JobManager_TOS.Info(ex.ToString());
                return null;
            }
        }

        private static T CreateObj(DataRow dataRow, DataColumnCollection Columns)
        {
            if (dataRow == null
                || Columns == null)
                return default(T);

            T newObj = new T();

            String columnName = "";
            for (int c = 0; c < Columns.Count; c++)
            {
                columnName = Columns[c].ColumnName;

                newObj.SetPropertyValue(columnName, dataRow[c]);

            }

            return newObj;
        }



    }


    [Serializable]
    public class Order_Command_Base : DBClass_BASE
    {
        public virtual string ORDER_ID { get; set; }
        public virtual string COMMAND_ID { get; set; }
        public virtual string ORDER_VERSION { get; set; }
        public virtual string COMMAND_VERSION { get; set; }

        //public virtual string VERSION { get; set; }
        public virtual int VERSION { get; set; }

        public virtual string JOB_STATUS { get; set; }

        public virtual string JOB_TYPE { get; set; }
        public virtual string JOB_ID { get; set; }
        public virtual string CHE_ID { get; set; }

        public virtual string FROM_TRUCK_TYPE { get; set; }
        public virtual string FROM_TRUCK_ID { get; set; }
        public virtual string FROM_TRUCK_POS { get; set; }
        public virtual string FROM_RFID { get; set; }
        public virtual string FROM_BAY_TYPE { get; set; }
        public virtual string FROM_BAY { get; set; }
        public virtual string FROM_LANE { get; set; }
        public virtual string FROM_TIER { get; set; }

        public virtual string TO_TRUCK_TYPE { get; set; }
        public virtual string TO_TRUCK_ID { get; set; }
        public virtual string TO_TRUCK_POS { get; set; }
        public virtual string TO_RFID { get; set; }
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
        public virtual long RECORDSEQUENCE { get; set; }

        public virtual DateTime DATETIME { get; set; }
        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            Order_Command_Base orderCmd = other as Order_Command_Base;
            base.Copy(orderCmd);

            this.ORDER_ID = orderCmd.ORDER_ID;
            this.COMMAND_ID = orderCmd.COMMAND_ID;
            this.ORDER_VERSION = orderCmd.ORDER_VERSION;
            this.COMMAND_VERSION = orderCmd.COMMAND_VERSION;

            this.VERSION = orderCmd.VERSION;

            this.JOB_STATUS = orderCmd.JOB_STATUS;

            this.JOB_TYPE = orderCmd.JOB_TYPE;
            this.JOB_ID = orderCmd.JOB_ID;
            this.CHE_ID = orderCmd.CHE_ID;

            this.FROM_BAY = orderCmd.FROM_BAY;
            this.FROM_BAY_TYPE = orderCmd.FROM_BAY_TYPE;
            this.FROM_LANE = orderCmd.FROM_LANE;
            this.FROM_RFID = orderCmd.FROM_RFID;
            this.FROM_TIER = orderCmd.FROM_TIER;
            this.FROM_TRUCK_ID = orderCmd.FROM_TRUCK_ID;
            this.FROM_TRUCK_POS = orderCmd.FROM_TRUCK_POS;
            this.FROM_TRUCK_TYPE = orderCmd.FROM_TRUCK_TYPE;

            this.TO_BAY = orderCmd.TO_BAY;
            this.TO_BAY_TYPE = orderCmd.TO_BAY_TYPE;
            this.TO_LANE = orderCmd.TO_LANE;
            this.TO_RFID = orderCmd.TO_RFID;
            this.TO_TIER = orderCmd.TO_TIER;
            this.TO_TRUCK_ID = orderCmd.TO_TRUCK_ID;
            this.TO_TRUCK_POS = orderCmd.TO_TRUCK_POS;
            this.TO_TRUCK_TYPE = orderCmd.TO_TRUCK_TYPE;

            this.CONTAINER_ID = orderCmd.CONTAINER_ID;
            this.CONTAINER_DOOR_DIRECTION = orderCmd.CONTAINER_DOOR_DIRECTION;
            this.CONTAINER_HEIGHT = orderCmd.CONTAINER_HEIGHT;
            this.CONTAINER_IS_EMPTY = orderCmd.CONTAINER_IS_EMPTY;
            this.CONTAINER_ISO = orderCmd.CONTAINER_ISO;
            this.CONTAINER_LENGTH = orderCmd.CONTAINER_LENGTH;
            this.CONTAINER_WEIGHT = orderCmd.CONTAINER_WEIGHT;

            this.RECORDSEQUENCE = orderCmd.RECORDSEQUENCE;

            this.DATETIME = orderCmd.DATETIME;
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
                    case "ORDER_VERSION":
                        ORDER_VERSION = Convert.ToString(propertyValue);
                        break;
                    case "COMMAND_VERSION":
                        COMMAND_VERSION = Convert.ToString(propertyValue);
                        break;
                    
                    case "VERSION":
                        //VERSION = Convert.ToString(propertyValue);
                        VERSION = Convert.ToInt32(propertyValue);
                        break;

                    case "JOB_STATUS":
                        JOB_STATUS = Convert.ToString(propertyValue);
                        break;
                    case "JOB_TYPE":
                        JOB_TYPE = Convert.ToString(propertyValue);
                        break;
                    case "JOB_ID":
                        JOB_ID = Convert.ToString(propertyValue);
                        break;
                    case "CHE_ID":
                        CHE_ID = Convert.ToString(propertyValue);
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
                    case "FROM_RFID":
                        FROM_RFID = Convert.ToString(propertyValue);
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

                    case "TO_TRUCK_TYPE":
                        TO_TRUCK_TYPE = Convert.ToString(propertyValue);
                        break;
                    case "TO_TRUCK_ID":
                        TO_TRUCK_ID = Convert.ToString(propertyValue);
                        break;
                    case "TO_TRUCK_POS":
                        TO_TRUCK_POS = Convert.ToString(propertyValue);
                        break;
                    case "TO_RFID":
                        TO_RFID = Convert.ToString(propertyValue);
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

                    case "RECORDSEQUENCE":
                        RECORDSEQUENCE = Convert.ToInt64(propertyValue);
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

        public virtual JobType GetJobType()
        {
            return Helper.GetEnum(JOB_TYPE, JobType.UNKNOWN);
        }

        public virtual string GetFrom()
        {
            return string.Format("{0},{1},{2},{3}", FROM_BAY_TYPE, FROM_BAY, FROM_LANE, FROM_TIER);
        }

        public virtual string GetTo()
        {
            return string.Format("{0},{1},{2},{3}", TO_BAY_TYPE, TO_BAY, TO_LANE, TO_TIER);
        }

        public virtual string GetOrderLink()
        {
            return "";
        }

        public virtual bool IsAgvLoad()
        {
            return (GetJobType() == JobType.LOAD && Helper.GetEnum(TO_BAY_TYPE, BayType.UnKnown) == BayType.AGV);
        }

        public virtual bool IsAgvDisc()
        {
            return (GetJobType() == JobType.DISC && Helper.GetEnum(FROM_BAY_TYPE, BayType.UnKnown) == BayType.AGV);
        }

        public virtual bool IsTruckTask()
        {
            BayType fromBayType = Helper.GetEnum(FROM_BAY_TYPE, BayType.UnKnown);
            BayType toBayType = Helper.GetEnum(TO_BAY_TYPE, BayType.UnKnown);

            return Helper.IsTruckTask(fromBayType, toBayType);
        }

        public override string ToString()
        {
            return string.Format("O={0}, C={1}, OV={2}, CV={3}, {4}, JOB={5}, LINK={6}, CHE={7}, CTN={8}, STATUS={9}, FROM=({10}), TO=({11}), SEQ={12}",
                ORDER_ID, COMMAND_ID, ORDER_VERSION, COMMAND_VERSION, JOB_TYPE, JOB_ID, GetOrderLink(), CHE_ID, CONTAINER_ID, JOB_STATUS,
                GetFrom(), GetTo(), RECORDSEQUENCE);
        }
    }

    [Serializable]
    public class Order_Base : Order_Command_Base
    {
        public virtual int PRIORITY { get; set; }

        public virtual DateTime PLAN_START_TIME { get; set; }
        public virtual DateTime PLAN_END_TIME { get; set; }

        public void CopyCmd(Command_Base cmd)
        {
            if (cmd == null)
                return;
            base.Copy(cmd);             
        }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            Order_Base orderBase = other as Order_Base;
            base.Copy(orderBase);

            this.PRIORITY = orderBase.PRIORITY;

            this.PLAN_START_TIME = orderBase.PLAN_START_TIME;
            this.PLAN_END_TIME = orderBase.PLAN_END_TIME; 
        }

        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "PRIORITY":
                        PRIORITY = Convert.ToInt32(propertyValue);
                        break;

                    case "PLAN_START_TIME":
                        PLAN_START_TIME = Convert.ToDateTime(propertyValue);
                        break; 
                    case "PLAN_END_TIME":
                        PLAN_END_TIME = Convert.ToDateTime(propertyValue);
                        break;

                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
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

    [Serializable]
    public class Command_Base : Order_Command_Base
    {
        public virtual string COMMAND_STATUS { get; set; }
        public virtual string EXCEPTION_CODE { get; set; }

        public virtual DateTime START_TIME { get; set; }
        public virtual DateTime END_TIME { get; set; }
        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            Command_Base cmdBase = other as Command_Base;
            base.Copy(cmdBase);
            
            this.COMMAND_STATUS = cmdBase.COMMAND_STATUS;
            this.EXCEPTION_CODE = cmdBase.EXCEPTION_CODE;

            this.START_TIME = cmdBase.START_TIME;
            this.END_TIME = cmdBase.END_TIME;
        }

        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "COMMAND_STATUS":
                        COMMAND_STATUS = Convert.ToString(propertyValue);
                        break;
                    
                    case "EXCEPTION_CODE":
                        EXCEPTION_CODE = Convert.ToString(propertyValue);
                        break;

                    case "START_TIME":
                        START_TIME = Convert.ToDateTime(propertyValue);
                        break;
                    case "END_TIME":
                        END_TIME = Convert.ToDateTime(propertyValue);
                        break;

                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
                        break;
                }


                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public virtual TaskStatus GetCmdStatus()
        {
            TaskStatus cmdStatus = Helper.GetEnum(COMMAND_STATUS, TaskStatus.None);
            TaskStatus jobStatus = Helper.GetEnum(JOB_STATUS, TaskStatus.None);
            if (jobStatus == TaskStatus.Cancel_OK)
                return jobStatus;

            return cmdStatus;
        }

        public virtual TaskStatus GetTosStatus()
        {
            return Helper.GetEnum(JOB_STATUS, TaskStatus.None);
        }

        public bool IsInitial()
        {
            TaskStatus status = GetCmdStatus();

            return Helper.IsTaskInitial(status);
        }

        public bool IsReady()
        {
            TaskStatus status = GetCmdStatus();

            return status == TaskStatus.Ready;
        }

        public bool IsInFirstHalf()
        {
            TaskStatus status = GetCmdStatus();

            return status == TaskStatus.None
                   || status == TaskStatus.Almost_Ready
                   || status == TaskStatus.Ready
                   || status == TaskStatus.Enter
                   || status == TaskStatus.Platform_Confirm
                   || status == TaskStatus.Platform_Pickup;
        }

        public bool IsWorking()
        {
            TaskStatus status = GetCmdStatus();

            return Helper.IsTaskWorking(status);
        }

        public bool IsCompleteFrom()
        {
            TaskStatus status = GetCmdStatus();

            return Helper.IsTaskCompleteFrom(status);
        }

        public bool IsComplete()
        {
            TaskStatus status = GetCmdStatus();

            return Helper.IsTaskComplete(status);
        }
    }

    [Serializable]
    public class STS_Order : Order_Base
    {
        public virtual string ORDER_LINK { get; set; }
        public virtual string VESSEL_ID { get; set; }
        public virtual string QUAY_ID { get; set; }
        public virtual string MOVE_NO { get; set; }

        public virtual string PLATFORM_CONFIRM { get; set; }

        public void CopyCmd(STS_Command cmd)
        {
            if (cmd == null)
                return;
            base.CopyCmd(cmd);

            this.VESSEL_ID = cmd.VESSEL_ID;
            this.QUAY_ID = cmd.QUAY_ID;
            //this.MOVE_NO = cmd.MOVE_NO;
            //this.PLATFORM_CONFIRM = order.PLATFORM_CONFIRM;
        }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            STS_Order order = other as STS_Order;
            base.Copy(order);

            this.ORDER_LINK = order.ORDER_LINK;
            this.VESSEL_ID = order.VESSEL_ID;
            this.QUAY_ID = order.QUAY_ID;
            this.MOVE_NO = order.MOVE_NO;
            this.PLATFORM_CONFIRM = order.PLATFORM_CONFIRM; 
        }

        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "ORDER_LINK":
                        ORDER_LINK = Convert.ToString(propertyValue);
                        break;
                    case "VESSEL_ID":
                        VESSEL_ID = Convert.ToString(propertyValue);
                        break;
                    case "QUAY_ID":
                        QUAY_ID = Convert.ToString(propertyValue);
                        break;
                    case "MOVE_NO":
                        MOVE_NO = Convert.ToString(propertyValue);
                        break;
                    case "PLATFORM_CONFIRM":
                        PLATFORM_CONFIRM = Convert.ToString(propertyValue);
                        break;
                     
                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
                        break;
                }


                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public override string GetOrderLink()
        {
            return ORDER_LINK;
        }

    }

    [Serializable]
    public class STS_Command : Command_Base
    {
        public virtual string VESSEL_ID { get; set; }
        public virtual string QUAY_ID { get; set; }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            STS_Command cmd = other as STS_Command;
            base.Copy(cmd);

            this.VESSEL_ID = cmd.VESSEL_ID;
            this.QUAY_ID = cmd.QUAY_ID; 
        }

        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "VESSEL_ID":
                        VESSEL_ID = Convert.ToString(propertyValue);
                        break;
                    case "QUAY_ID":
                        QUAY_ID = Convert.ToString(propertyValue);
                        break;
                     
                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
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

    [Serializable]
    public class AGV_Order : Order_Base
    {
        public virtual string FROM_LINK { get; set; }
        public virtual string TO_LINK { get; set; }
        public virtual string CARRY_LINK { get; set; }

        public virtual string YARD_ID { get; set; }
        public virtual string QUAY_ID { get; set; }

        public virtual string FROM_BLOCK { get; set; }
        public virtual string TO_BLOCK { get; set; }

        public virtual string QC_REFID { get; set; }

        public void CopyCmd(AGV_Command cmd)
        {
            if (cmd == null)
                return;
            base.CopyCmd(cmd);

            this.YARD_ID = cmd.YARD_ID;
            this.QUAY_ID = cmd.QUAY_ID;
            this.FROM_BLOCK = cmd.FROM_BLOCK;
            this.TO_BLOCK = cmd.TO_BLOCK;
            //QC_REFID
        }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            AGV_Order order = other as AGV_Order;
            base.Copy(order);

            this.FROM_LINK = order.FROM_LINK;
            this.TO_LINK = order.TO_LINK;
            this.CARRY_LINK = order.CARRY_LINK;
            this.YARD_ID = order.YARD_ID;
            this.QUAY_ID = order.QUAY_ID;
            this.FROM_BLOCK = order.FROM_BLOCK;
            this.TO_BLOCK = order.TO_BLOCK;
            this.QC_REFID = order.QC_REFID;
        }

        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "FROM_LINK":
                        FROM_LINK = Convert.ToString(propertyValue);
                        break;
                    case "TO_LINK":
                        TO_LINK = Convert.ToString(propertyValue);
                        break;
                    case "CARRY_LINK":
                        CARRY_LINK = Convert.ToString(propertyValue);
                        break;
                    case "YARD_ID":
                        YARD_ID = Convert.ToString(propertyValue);
                        break;
                    case "QUAY_ID":
                        QUAY_ID = Convert.ToString(propertyValue);
                        break;
                    case "FROM_BLOCK":
                        FROM_BLOCK = Convert.ToString(propertyValue);
                        break;
                    case "TO_BLOCK":
                        TO_BLOCK = Convert.ToString(propertyValue);
                        break;
                    case "QC_REFID":
                        QC_REFID = Convert.ToString(propertyValue);
                        break;

                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
                        break;
                }


                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public override string GetFrom()
        {
            return string.Format("{0},{1},{2},{3},{4}", FROM_BLOCK, FROM_BAY_TYPE, FROM_BAY, FROM_LANE, FROM_TIER);
            //return string.Format("BLOCK:{0}, BAY_TYPE:{1}, BAY:{2}, LANE:{3}, TIER: {4}", FROM_BLOCK, FROM_BAY_TYPE, FROM_BAY, FROM_LANE, FROM_TIER);
        }

        public override string GetTo()
        {
            return string.Format("{0},{1},{2},{3},{4}", TO_BLOCK, TO_BAY_TYPE, TO_BAY, TO_LANE, TO_TIER);
            //return string.Format("BLOCK:{0}, BAY_TYPE:{1}, BAY:{2}, LANE:{3}, TIER: {4}", TO_BLOCK, TO_BAY_TYPE, TO_BAY, TO_LANE, TO_TIER);
        }
        public override string GetOrderLink()
        {
            if (!string.IsNullOrWhiteSpace(CARRY_LINK))
                return CARRY_LINK;
            if (!string.IsNullOrWhiteSpace(FROM_LINK))
                return FROM_LINK;
            return TO_LINK;
        }

        public virtual bool IsSameFrom()
        {
            return !string.IsNullOrWhiteSpace(FROM_LINK);
        }

        public virtual bool IsSameTo()
        {
            return !string.IsNullOrWhiteSpace(TO_LINK);
        }

        public virtual bool IsTwinLoadFromDiffBlock()
        {
            return GetJobType() == JobType.LOAD
                    && !string.IsNullOrWhiteSpace(GetOrderLink())
                    && !IsSameFrom();
        }

        public override string ToString()
        {
            return string.Format("O={0, -4}, C={1, -4}, OV={2}, CV={3}, {4, -11}, JOB={5}, LINK={6, -17}, CHE={7, -3}, CTN={8}, STATUS={9, -6}, FROM=({10, -19}), TO=({11, -19}), YARD_ID={12, -3}, QUAY_ID={13, -3}, SEQ={14}",
                ORDER_ID, COMMAND_ID, ORDER_VERSION, COMMAND_VERSION, JOB_TYPE, JOB_ID, 
                string.Format("F{0},T{1},C{2}", FROM_LINK, TO_LINK, CARRY_LINK), CHE_ID, CONTAINER_ID, JOB_STATUS,
                GetFrom(), GetTo(), YARD_ID, QUAY_ID, RECORDSEQUENCE);
        }
    }

    [Serializable]
    public class AGV_Command : Command_Base
    {
        public virtual string QUAY_ID { get; set; }

        public virtual string YARD_ID { get; set; }

        public virtual string FROM_BLOCK { get; set; }
        public virtual string TO_BLOCK { get; set; }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            AGV_Command cmd = other as AGV_Command;
            base.Copy(cmd);

            this.QUAY_ID = cmd.QUAY_ID;
            this.YARD_ID = cmd.YARD_ID;
            this.FROM_BLOCK = cmd.FROM_BLOCK;
            this.TO_BLOCK = cmd.TO_BLOCK;
        }

        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "QUAY_ID":
                        QUAY_ID = Convert.ToString(propertyValue);
                        break;
                    case "YARD_ID":
                        YARD_ID = Convert.ToString(propertyValue);
                        break;
                    case "FROM_BLOCK":
                        FROM_BLOCK = Convert.ToString(propertyValue);
                        break;
                    case "TO_BLOCK":
                        TO_BLOCK = Convert.ToString(propertyValue);
                        break;

                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
                        break;
                }


                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public override string GetFrom()
        {
            return string.Format("BLOCK:{0}, BAY_TYPE:{1}, BAY:{2}, LANE:{3}, TIER: {4}", FROM_BLOCK, FROM_BAY_TYPE, FROM_BAY, FROM_LANE, FROM_TIER);
        }

        public override string GetTo()
        {
            return string.Format("BLOCK:{0}, BAY_TYPE:{1}, BAY:{2}, LANE:{3}, TIER: {4}", TO_BLOCK, TO_BAY_TYPE, TO_BAY, TO_LANE, TO_TIER);
        }
    }

    [Serializable]
    public class ASC_Order : Order_Base
    {
        public virtual string ORDER_LINK { get; set; }
        public virtual string ORDER_ASSIGNER { get; set; }

        public virtual string YARD_ID { get; set; }
        public virtual string OPERATOR_ID { get; set; }

        public virtual string FROM_BLOCK { get; set; }
        public virtual string FROM_PALLET_TYPE { get; set; }
        public virtual string FROM_TRUCK_STATUS { get; set; }
        public virtual string TO_BLOCK { get; set; }
        public virtual string TO_PALLET_TYPE { get; set; }
        public virtual string TO_TRUCK_STATUS { get; set; }

        public void CopyCmd(ASC_Command cmd)
        {
            if (cmd == null)
                return;
            base.CopyCmd(cmd);

            this.YARD_ID = cmd.YARD_ID;
            this.FROM_BLOCK = cmd.FROM_BLOCK;
            this.FROM_PALLET_TYPE = cmd.FROM_PALLET_TYPE;
            this.FROM_TRUCK_STATUS = cmd.FROM_TRUCK_STATUS;
            this.TO_BLOCK = cmd.TO_BLOCK;
            this.TO_PALLET_TYPE = cmd.TO_PALLET_TYPE;
            this.TO_TRUCK_STATUS = cmd.TO_TRUCK_STATUS;
        }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            ASC_Order order = other as ASC_Order;
            base.Copy(order);

            this.ORDER_LINK = order.ORDER_LINK;
            this.ORDER_ASSIGNER = order.ORDER_ASSIGNER;
            this.YARD_ID = order.YARD_ID;
            this.OPERATOR_ID = order.OPERATOR_ID;
            this.FROM_BLOCK = order.FROM_BLOCK;
            this.FROM_PALLET_TYPE = order.FROM_PALLET_TYPE;
            this.FROM_TRUCK_STATUS = order.FROM_TRUCK_STATUS;
            this.TO_BLOCK = order.TO_BLOCK;
            this.TO_PALLET_TYPE = order.TO_PALLET_TYPE;
            this.TO_TRUCK_STATUS = order.TO_TRUCK_STATUS;
        }

        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "ORDER_LINK":
                        ORDER_LINK = Convert.ToString(propertyValue);
                        break;
                    case "ORDER_ASSIGNER":
                        ORDER_ASSIGNER = Convert.ToString(propertyValue);
                        break;
                    case "YARD_ID":
                        YARD_ID = Convert.ToString(propertyValue);
                        break;
                    case "OPERATOR_ID":
                        OPERATOR_ID = Convert.ToString(propertyValue);
                        break;
                    case "FROM_BLOCK":
                        FROM_BLOCK = Convert.ToString(propertyValue);
                        break;
                    case "FROM_PALLET_TYPE":
                        FROM_PALLET_TYPE = Convert.ToString(propertyValue);
                        break;
                    case "FROM_TRUCK_STATUS":
                        FROM_TRUCK_STATUS = Convert.ToString(propertyValue);
                        break;
                    case "TO_BLOCK":
                        TO_BLOCK = Convert.ToString(propertyValue);
                        break;
                    case "TO_PALLET_TYPE":
                        TO_PALLET_TYPE = Convert.ToString(propertyValue);
                        break;
                    case "TO_TRUCK_STATUS":
                        TO_TRUCK_STATUS = Convert.ToString(propertyValue);
                        break;
                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
                        break;
                }


                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public override string GetOrderLink()
        {
            return ORDER_LINK;
        }

        public override string GetFrom()
        {
            return string.Format("{0},{1},{2},{3},{4}", FROM_BLOCK, FROM_BAY_TYPE, FROM_BAY, FROM_LANE, FROM_TIER);
        }

        public override string GetTo()
        {
            return string.Format("{0},{1},{2},{3},{4}", TO_BLOCK, TO_BAY_TYPE, TO_BAY, TO_LANE, TO_TIER);
        }

        public override string ToString()
        {
            return string.Format("O={0, -4}, C={1, -4}, OV={2}, CV={3}, {4, -11}, JOB={5}, LINK={6, -17}, CHE={7, -3}, CTN={8}, STATUS={9, -6}, FROM=({10, -19}), TO=({11, -19}), YARD_ID={12, -3}, ORDER_ASSIGNER={13}, OPERATOR_ID={14}, SEQ={15}",
                ORDER_ID, COMMAND_ID, ORDER_VERSION, COMMAND_VERSION, JOB_TYPE, JOB_ID, GetOrderLink(), CHE_ID, CONTAINER_ID, JOB_STATUS,
                GetFrom(), GetTo(), YARD_ID, ORDER_ASSIGNER, OPERATOR_ID, RECORDSEQUENCE);
        }
    }

    [Serializable] 
    public class ASC_Command : Command_Base
    {
        public virtual string YARD_ID { get; set; }
        public virtual string OPERATOR_ID { get; set; }

        public virtual string FROM_BLOCK { get; set; }
        public virtual string FROM_PALLET_TYPE { get; set; }
        public virtual string FROM_TRUCK_STATUS { get; set; }
        public virtual string TO_BLOCK { get; set; }
        public virtual string TO_PALLET_TYPE { get; set; }
        public virtual string TO_TRUCK_STATUS { get; set; }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            ASC_Command cmd = other as ASC_Command;
            base.Copy(cmd);

            this.YARD_ID = cmd.YARD_ID;
            this.OPERATOR_ID = cmd.OPERATOR_ID;
            this.FROM_BLOCK = cmd.FROM_BLOCK;
            this.FROM_PALLET_TYPE = cmd.FROM_PALLET_TYPE;
            this.FROM_TRUCK_STATUS = cmd.FROM_TRUCK_STATUS;
            this.TO_BLOCK = cmd.TO_BLOCK;
            this.TO_PALLET_TYPE = cmd.TO_PALLET_TYPE;
            this.TO_TRUCK_STATUS = cmd.TO_TRUCK_STATUS;
        }

        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "YARD_ID":
                        YARD_ID = Convert.ToString(propertyValue);
                        break;
                    case "OPERATOR_ID":
                        OPERATOR_ID = Convert.ToString(propertyValue);
                        break;
                    case "FROM_BLOCK":
                        FROM_BLOCK = Convert.ToString(propertyValue);
                        break;
                    case "FROM_PALLET_TYPE":
                        FROM_PALLET_TYPE = Convert.ToString(propertyValue);
                        break;
                    case "FROM_TRUCK_STATUS":
                        FROM_TRUCK_STATUS = Convert.ToString(propertyValue);
                        break;
                    case "TO_BLOCK":
                        TO_BLOCK = Convert.ToString(propertyValue);
                        break;
                    case "TO_PALLET_TYPE":
                        TO_PALLET_TYPE = Convert.ToString(propertyValue);
                        break;
                    case "TO_TRUCK_STATUS":
                        TO_TRUCK_STATUS = Convert.ToString(propertyValue);
                        break;

                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
                        break;
                }


                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public override string GetFrom()
        {
            return string.Format("{0},{1},{2},{3},{4}", FROM_BLOCK, FROM_BAY_TYPE, FROM_BAY, FROM_LANE, FROM_TIER);
        }

        public override string GetTo()
        {
            return string.Format("{0},{1},{2},{3},{4}", TO_BLOCK, TO_BAY_TYPE, TO_BAY, TO_LANE, TO_TIER);
        }

        public override string ToString()
        {
            return string.Format("O={0}, C={1}, OV={2}, CV={3}, {4}, JOB={5}, LINK={6}, CHE={7}, CTN={8}, STATUS={9}, FROM=({10}), TO=({11}), YARD_ID={12}, OPERATOR_ID={13}, SEQ={14}",
                ORDER_ID, COMMAND_ID, ORDER_VERSION, COMMAND_VERSION, JOB_TYPE, JOB_ID, GetOrderLink(), CHE_ID, CONTAINER_ID, JOB_STATUS,
                GetFrom(), GetTo(), YARD_ID, OPERATOR_ID, RECORDSEQUENCE);
        }
    }



    /////////////////////////////////  WorkQueue   ////////////////////////////////


    [Serializable] public enum Work_Status { UNDEFINED, IDLE, JOB_EXECUTION, WAITING_FOR_OTHER_ASC, WAITING_FOR_RCC, SUSPENDED };
    [Serializable] public enum Battery_State { GREEN, YELLOW, RED };
    [Serializable] public enum Fuel_Type { BATTERY, FUEL };
    [Serializable] public enum Technical_Status { GREEN, YELLOW, ORANGE, RED };
    [Serializable] public enum Operational_Status { OFFLINE, AUTOMATIC, MAINTENANCE_MODE };
    [Serializable] public enum Rack_Suitable { NULL, YES, NO };
    [Serializable] public enum WQ_Status { NULL, WORKING, EMPTY };
    [Serializable] public enum Status { NULL, ENTERED, WORKING, COMPLETE, REJECTED, CANCELED, ABORTED };
    [Serializable] public enum Has { NULL, YES, NO, NA, UNKNOWN };
    [Serializable] public enum Container_STS_LOC_Type { NULL, AGV, APRON, TRUCK, VESSEL, YARD };
    [Serializable] public enum Hold { NULL, NONE, BYPASSED, SUSPENDED };
    [Serializable] public enum Is_Tank { NULL, TES, NO };
    [Serializable] public enum Configuration { NULL, CONFIGURED, UNCONFIGURED };
    [Serializable] public enum Above_Below { NULL, A, B };
    [Serializable] public enum Move_Kind { NULL, RECV, DLVR, DSCH, LOAD, RDSC, RLOD, SHOB, YARD, DUAL_CYCLE };
    [Serializable] public enum Move_Stage { NULL, PLANNED, FETCH_UNDERWAY, CARRY_READY, CARRY_UNDERWAY, CARRY_COMPLETE, PUT_UNDERWAY, PUT_COMPLETE, COMPLETE };
    [Serializable] public enum ASC_Order_Progress { NULL, UNDEFINED, PLANNED, DRIVING_ONLY, DRIVING_TO_PICK, PICKING, LIFTING_AFTER_PICK, DRIVING_TO_DEPOSIT, DEPOSITING, LIFTING_AFTER_DEPOSIT };
    [Serializable] public enum ECS_Problem_Type { NULL, ASC_NOT_AVAILABLE, VALIDATION_ERROR, ECS_RESTART, ABORTED_BY_OPERATOR, BLOCKMAP_ERROR, EXECUTION_FAILED, REEFER_ON_POWER };
    [Serializable] public enum Orientation { NULL, WATERSIDE, LANDSIDE, HIGHBOLLARD, LOWBOLLARD, UNKNOWN, NA };
   
    
    
    [Serializable]
    public class STS_WORK_QUEUE_STATUS : DBClass_BASE
    {
        public String WORK_QUEUE{ get; set; }//String(30)
        public Move_Kind MOVE_KIND{ get; set; }//String(10)
        public Above_Below ABOVE_BELOW{ get; set; }//String(1)
        public String QC_ID { get; set; }//String(7)
        public String VESSEL_VISIT{ get; set; }//String(30)
        public String SHIP_NO { get; set; }
        public String VESSEL_BAY{ get; set; }//String(12)
        public String QC_BOLLARD { get; set; }//String(7)
        public int QC_BOLLARD_OFFSET_CM { get; set; }
        public int WQ_SEQ{ get; set; }//String(7)
        public Configuration CONFIGURATION{ get; set; }
        public DateTime START_TIME{ get; set; }
        public DateTime END_TIME{ get; set; }
        public WQ_Status WQ_STATUS{ get; set; }//String(7)
        public DateTime UPDATED{ get; set; }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            STS_WORK_QUEUE_STATUS workQueue = other as STS_WORK_QUEUE_STATUS;
            base.Copy(workQueue);

            this.WORK_QUEUE = workQueue.WORK_QUEUE;
            this.MOVE_KIND = workQueue.MOVE_KIND;
            this.ABOVE_BELOW = workQueue.ABOVE_BELOW;
            this.QC_ID = workQueue.QC_ID;
            this.VESSEL_VISIT = workQueue.VESSEL_VISIT;
            this.SHIP_NO = workQueue.SHIP_NO;
            this.VESSEL_BAY = workQueue.VESSEL_BAY;
            this.QC_BOLLARD = workQueue.QC_BOLLARD;
            this.QC_BOLLARD_OFFSET_CM = workQueue.QC_BOLLARD_OFFSET_CM;
            this.WQ_SEQ = workQueue.WQ_SEQ;
            this.CONFIGURATION = workQueue.CONFIGURATION;
            this.START_TIME = workQueue.START_TIME;
            this.END_TIME = workQueue.END_TIME;
            this.WQ_STATUS = workQueue.WQ_STATUS;
            this.UPDATED = workQueue.UPDATED;
        }
        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "WORK_QUEUE":
                        WORK_QUEUE = Convert.ToString(propertyValue);
                        break;
                    case "MOVE_KIND":
                        MOVE_KIND = ConvertEnum<Move_Kind>(propertyValue);
                        break;
                    case "ABOVE_BELOW":
                        ABOVE_BELOW = ConvertEnum<Above_Below>(propertyValue);
                        break;
                    case "QC_ID":
                        QC_ID = Convert.ToString(propertyValue);
                        break;
                    case "VESSEL_VISIT":
                        VESSEL_VISIT = Convert.ToString(propertyValue);
                        break;
                    case "SHIP_NO":
                        SHIP_NO = Convert.ToString(propertyValue);
                        break;
                    case "VESSEL_BAY":
                        VESSEL_BAY = Convert.ToString(propertyValue);
                        break;
                    case "QC_BOLLARD":
                        QC_BOLLARD = Convert.ToString(propertyValue);
                        break;
                    case "QC_BOLLARD_OFFSET_CM":
                        QC_BOLLARD_OFFSET_CM = Convert.ToInt32(propertyValue);
                        break;
                    case "WQ_SEQ":
                        WQ_SEQ = Convert.ToInt32(propertyValue);
                        break;
                    case "CONFIGURATION":
                        CONFIGURATION = ConvertEnum<Configuration>(propertyValue);
                        break;
                    case "START_TIME":
                        START_TIME = Convert.ToDateTime(propertyValue);
                        break;
                    case "END_TIME":
                        END_TIME = Convert.ToDateTime(propertyValue);
                        break;
                    case "WQ_STATUS":
                        WQ_STATUS = ConvertEnum<WQ_Status>(propertyValue);
                        break;
                    case "UPDATED":
                        UPDATED = Convert.ToDateTime(propertyValue);
                        break;
                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
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

    [Serializable]
    public class BERTH_STATUS : DBClass_BASE
    {
        public String VESSEL_VISIT{ get; set; }//String(30)
        public String VESSEL_CALL_SIGN { get; set; }//String(12)
        public String VESSEL_NAME { get; set; }//String(32)
        public String BOW_BOLLARD { get; set; }//String(7)
        public int BOW_BOLLARD_OFFSET_CM { get; set; }
        public String STERN_BOLLARD { get; set; }//String(7)
        public int STERN_BOLLARD_OFFSET_CM { get; set; }
        public String VESSEL_CLASSIFICATION { get; set; }//String(7)
        public String VESSEL_VISIT_PHASE { get; set; }//String(8)
        public DateTime UPDATED { get; set; }

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            BERTH_STATUS berth = other as BERTH_STATUS;
            base.Copy(berth);

            this.VESSEL_VISIT = berth.VESSEL_VISIT;
            this.VESSEL_CALL_SIGN = berth.VESSEL_CALL_SIGN;
            this.VESSEL_NAME = berth.VESSEL_NAME;
            this.BOW_BOLLARD = berth.BOW_BOLLARD;
            this.BOW_BOLLARD_OFFSET_CM = berth.BOW_BOLLARD_OFFSET_CM;
            this.STERN_BOLLARD = berth.STERN_BOLLARD;
            this.STERN_BOLLARD_OFFSET_CM = berth.STERN_BOLLARD_OFFSET_CM;
            this.VESSEL_CLASSIFICATION = berth.VESSEL_CLASSIFICATION;
            this.VESSEL_VISIT_PHASE = berth.VESSEL_VISIT_PHASE;
            this.UPDATED = berth.UPDATED; 
        }
        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {
                switch (strPropertyName)
                {
                    case "VESSEL_VISIT":
                        VESSEL_VISIT = Convert.ToString(propertyValue);
                        break;
                    case "VESSEL_CALL_SIGN":
                        VESSEL_CALL_SIGN = Convert.ToString(propertyValue);
                        break;
                    case "VESSEL_NAME":
                        VESSEL_NAME = Convert.ToString(propertyValue);
                        break;
                    case "BOW_BOLLARD":
                        BOW_BOLLARD = Convert.ToString(propertyValue);
                        break;
                    case "BOW_BOLLARD_OFFSET_CM":
                        BOW_BOLLARD_OFFSET_CM = Convert.ToInt32(propertyValue);
                        break;
                    case "STERN_BOLLARD":
                        STERN_BOLLARD = Convert.ToString(propertyValue);
                        break;
                    case "STERN_BOLLARD_OFFSET_CM":
                        STERN_BOLLARD_OFFSET_CM = Convert.ToInt32(propertyValue);
                        break;
                    case "VESSEL_CLASSIFICATION":
                        VESSEL_CLASSIFICATION = Convert.ToString(propertyValue);
                        break;
                    case "VESSEL_VISIT_PHASE":
                        VESSEL_VISIT_PHASE = Convert.ToString(propertyValue);
                        break;
                    case "UPDATED":
                        UPDATED = Convert.ToDateTime(propertyValue);
                        break;
                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
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

    [Serializable]
    public class WORK_INSTRUCTION_STATUS : DBClass_BASE
    {
        public enum Column { ORIGIN_CARRIER_SLOT, CARRY_REFERENCE, WORK_QUEUE, CONTAINER_ID, CONTAINER_WI_REF };

        public String CONTAINER_ID{ get; set; }//String(12)
        public string JOB_ID { get; set; }
        public long CONTAINER_WI_REF{ get; set; }
        public Int16 CONTAINER_LENGTH_CM { get; set; }
        public Int16 CONTAINER_HEIGHT_CM { get; set; }
        public int CONTAINER_WEIGHT_KG { get; set; }
        public String CONTAINER_ISO { get; set; }//String(4)
        public Is_Tank IS_TANK { get; set; }//String(5)
        public Rack_Suitable RACK_SUITABLE { get; set; }//String(5)
        public Hold HOLD { get; set; }//String(5)
        public String WORK_QUEUE { get; set; }//String(30)
        public String POINT_OF_WORK { get; set; }//String(30)
        public Move_Kind MOVE_KIND { get; set; }//String(5)
        public Move_Stage MOVE_STAGE{ get; set; }//String(25)
        public int ORDER_SEQ{ get; set; }
        public string VESSEL_ID { get; set; }
        public String LOGICAL_PREDECESSOR{ get; set; }//String(12)
        public String PHYSICAL_PREDECESSOR{ get; set; }//String(12)
        public String CONTAINER_STOW_FACTOR{ get; set; }//String(25)
        public double CONTAINER_WEIGHT_MARGIN_KG{ get; set; }
        public String LIFT_REFERENCE { get; set; }
        public int CARRY_REFERENCE { get; set; }
        public String TWIN_CARRY_REFERNCE { get; set; } // twin container task
        public String RELATIVE_POS_ON_CARRIER{ get; set; }//String(8)
        public String ORIGIN_CARRIER_SLOT{ get; set; }//String(50)
        public String DESTINATION_CARRIER_SLOT{ get; set; }//String(50)
        //public Orientation DOOR_DIRECTION_ON_CARRIER{ get; set; }
        public Orientation DOOR_DIRECTION { get; set; }
        public string PLAT_POSITION_ID { get; set; }
        public Int16 OFFSET_TO_BAY_CENTER_CM{ get; set; }
        public Container_STS_LOC_Type CONTAINER_QC_LOC_TYPE { get; set; }//String(6)
        public Container_STS_LOC_Type CONTAINER_NEXT_QC_LOC_TYPE { get; set; }//String(6)
        public Has HAS_TOP_RAILS{ get; set; }
        public Has HAS_BOTTOM_RAILS{ get; set; }
        public DateTime UPDATED{ get; set; }
        public DateTime PLAN_TIM { get; set; }
        public string FUTURE_PLAC { get; set; }
        public string CNTR_SIZ_COD { get; set; }


        //临时算法数据
        public String T_Load_BlockNO{ get; set; }// 装船时的出箱箱区
        public DateTime T_StartTime { get; set; } // 任务起始时间
        public DateTime T_EndTime { get; set; } // 任务结束时间

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            WORK_INSTRUCTION_STATUS workInstruction = other as WORK_INSTRUCTION_STATUS;
            base.Copy(workInstruction);

            this.CONTAINER_ID = workInstruction.CONTAINER_ID;
            this.JOB_ID = workInstruction.JOB_ID;
            this.CONTAINER_WI_REF = workInstruction.CONTAINER_WI_REF;
            this.CONTAINER_LENGTH_CM = workInstruction.CONTAINER_LENGTH_CM;
            this.CONTAINER_HEIGHT_CM = workInstruction.CONTAINER_HEIGHT_CM;
            this.CONTAINER_WEIGHT_KG = workInstruction.CONTAINER_WEIGHT_KG;
            this.CONTAINER_ISO = workInstruction.CONTAINER_ISO;
            this.IS_TANK = workInstruction.IS_TANK;
            this.RACK_SUITABLE = workInstruction.RACK_SUITABLE;
            this.HOLD = workInstruction.HOLD;
            this.WORK_QUEUE = workInstruction.WORK_QUEUE;
            this.POINT_OF_WORK = workInstruction.POINT_OF_WORK;
            this.MOVE_KIND = workInstruction.MOVE_KIND;
            this.MOVE_STAGE = workInstruction.MOVE_STAGE;
            this.ORDER_SEQ = workInstruction.ORDER_SEQ;
            this.VESSEL_ID = workInstruction.VESSEL_ID;
            this.LOGICAL_PREDECESSOR = workInstruction.LOGICAL_PREDECESSOR;
            this.PHYSICAL_PREDECESSOR = workInstruction.PHYSICAL_PREDECESSOR;
            this.CONTAINER_STOW_FACTOR = workInstruction.CONTAINER_STOW_FACTOR;
            this.CONTAINER_WEIGHT_MARGIN_KG = workInstruction.CONTAINER_WEIGHT_MARGIN_KG;
            this.LIFT_REFERENCE = workInstruction.LIFT_REFERENCE;
            this.CARRY_REFERENCE = workInstruction.CARRY_REFERENCE;
            this.RELATIVE_POS_ON_CARRIER = workInstruction.RELATIVE_POS_ON_CARRIER;
            this.ORIGIN_CARRIER_SLOT = workInstruction.ORIGIN_CARRIER_SLOT;
            this.DESTINATION_CARRIER_SLOT = workInstruction.DESTINATION_CARRIER_SLOT;
            //this.DOOR_DIRECTION_ON_CARRIER = workInstruction.DOOR_DIRECTION_ON_CARRIER;
            this.DOOR_DIRECTION = workInstruction.DOOR_DIRECTION;
            this.PLAT_POSITION_ID = workInstruction.PLAT_POSITION_ID;
            this.OFFSET_TO_BAY_CENTER_CM = workInstruction.OFFSET_TO_BAY_CENTER_CM;
            this.CONTAINER_QC_LOC_TYPE = workInstruction.CONTAINER_QC_LOC_TYPE;
            this.CONTAINER_NEXT_QC_LOC_TYPE = workInstruction.CONTAINER_NEXT_QC_LOC_TYPE;
            this.HAS_TOP_RAILS = workInstruction.HAS_TOP_RAILS;
            this.HAS_BOTTOM_RAILS = workInstruction.HAS_BOTTOM_RAILS;
            this.UPDATED = workInstruction.UPDATED;
            this.PLAN_TIM = workInstruction.PLAN_TIM;
            this.FUTURE_PLAC = workInstruction.FUTURE_PLAC;

            //临时算法数据
            this.T_Load_BlockNO = workInstruction.T_Load_BlockNO;
            this.T_StartTime = workInstruction.T_StartTime;
            this.T_EndTime = workInstruction.T_EndTime;
             
        }
        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {

                switch (strPropertyName)
                {
                    case "CONTAINER_ID":
                        CONTAINER_ID = Convert.ToString(propertyValue);
                        break;
                    case "JOB_ID":
                        JOB_ID = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_WI_REF":
                        CONTAINER_WI_REF = Convert.ToInt64(propertyValue);
                        break;
                    case "CONTAINER_LENGTH_CM":
                        CONTAINER_LENGTH_CM = Convert.ToInt16(propertyValue);
                        break;
                    case "CONTAINER_HEIGHT_CM":
                        CONTAINER_HEIGHT_CM = Convert.ToInt16(propertyValue);
                        break;
                    case "CONTAINER_WEIGHT_KG":
                        CONTAINER_WEIGHT_KG = Convert.ToInt32(propertyValue);
                        break;
                    case "CONTAINER_ISO":
                        CONTAINER_ISO = Convert.ToString(propertyValue);
                        break;
                    case "WORK_QUEUE":
                        WORK_QUEUE = Convert.ToString(propertyValue);
                        break;
                    case "ORDER_SEQ":
                    case "SEQ_NO":
                        ORDER_SEQ = Convert.ToInt32(propertyValue);
                        break;
                    case "VESSEL_ID":
                        VESSEL_ID = Convert.ToString(propertyValue);
                        break;
                    case "POINT_OF_WORK":
                        POINT_OF_WORK = Convert.ToString(propertyValue);
                        break;
                    case "MOVE_KIND":
                        MOVE_KIND = ConvertEnum<Move_Kind>(propertyValue);
                        break;
                    case "MOVE_STAGE":
                        MOVE_STAGE = ConvertEnum<Move_Stage>(propertyValue);
                        break;
                    case "LOGICAL_PREDECESSOR":
                        LOGICAL_PREDECESSOR = Convert.ToString(propertyValue);
                        break;
                    case "PHYSICAL_PREDECESSOR":
                        PHYSICAL_PREDECESSOR = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_STOW_FACTOR":
                        CONTAINER_STOW_FACTOR = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_WEIGHT_MARGIN_KG":
                        CONTAINER_WEIGHT_MARGIN_KG = Convert.ToDouble(propertyValue);
                        break;
                    case "LIFT_REFERENCE":
                        LIFT_REFERENCE = Convert.ToString(propertyValue);
                        break;
                    case "ORIGIN_CARRIER_SLOT":
                        ORIGIN_CARRIER_SLOT = Convert.ToString(propertyValue);
                        break;
                    case "DESTINATION_CARRIER_SLOT":
                        DESTINATION_CARRIER_SLOT = Convert.ToString(propertyValue);
                        break;
                    //case "DOOR_DIRECTION_ON_CARRIER":
                    //    DOOR_DIRECTION_ON_CARRIER = ConvertEnum<Orientation>(propertyValue);
                    //    break;
                    case "DOOR_DIRECTION":
                        DOOR_DIRECTION = ConvertEnum<Orientation>(propertyValue);
                        break;
                    case "PLAT_POSITION_ID":
                        PLAT_POSITION_ID = Convert.ToString(propertyValue);
                        break;
                    case "OFFSET_TO_BAY_CENTER_CM":
                        OFFSET_TO_BAY_CENTER_CM = Convert.ToInt16(propertyValue);
                        break;
                    case "CONTAINER_QC_LOC_TYPE":
                        CONTAINER_QC_LOC_TYPE = ConvertEnum<Container_STS_LOC_Type>(propertyValue);
                        break;
                    case "CONTAINER_NEXT_QC_LOC_TYPE":
                        CONTAINER_NEXT_QC_LOC_TYPE = ConvertEnum<Container_STS_LOC_Type>(propertyValue);
                        break;
                    case "UPDATED":
                        UPDATED = Convert.ToDateTime(propertyValue);
                        break;
                    case "PLAN_TIM":
                        PLAN_TIM = Convert.ToDateTime(propertyValue);
                        T_StartTime = PLAN_TIM;
                        T_EndTime = T_StartTime + new TimeSpan(0, 2, 0);
                        break;
                    case "FUTURE_PLAC":
                        FUTURE_PLAC = Convert.ToString(propertyValue);
                        break;

                    case "CARRY_REFERENCE":
                        CARRY_REFERENCE = Convert.ToInt32(propertyValue);
                        break;
                    case "RELATIVE_POS_ON_CARRIER":
                        RELATIVE_POS_ON_CARRIER = Convert.ToString(propertyValue);
                        break;
                    case "IS_TANK":
                        IS_TANK = ConvertEnum<Is_Tank>(propertyValue);
                        break;
                    case "RACK_SUITABLE":
                        RACK_SUITABLE = ConvertEnum<Rack_Suitable>(propertyValue);
                        break;
                    case "HOLD":
                        HOLD = ConvertEnum<Hold>(propertyValue);
                        break;
                    case "HAS_TOP_RAILS":
                        HAS_TOP_RAILS = ConvertEnum<Has>(propertyValue);
                        break;
                    case "HAS_BOTTOM_RAILS":
                        HAS_BOTTOM_RAILS = ConvertEnum<Has>(propertyValue);
                        break;  
                    case "CNTR_SIZ_COD":
                        CNTR_SIZ_COD =  Convert.ToString(propertyValue);//hemin add 20160524
                        break;

                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
                        break;
                }

                return true;
            }
            catch (Exception e)
            {
                return false;
            }

           
        }

        public override string ToString()
        {
            return string.Format("WI: CTN={0}, JOB_ID={1}, WQ={2}, MOVE_KIND={3}, SEQ={4}, LOGICAL_PREDECESSOR={5}, PHYSICAL_PREDECESSOR={6}, STOW_FACTOR={7}, TWIN_CARRY_REFERNCE={8}, T_Load_BlockNO={9}, T_StartTime={10}, T_EndTime={11}",
                CONTAINER_ID, JOB_ID, WORK_QUEUE, MOVE_KIND, ORDER_SEQ, LOGICAL_PREDECESSOR, PHYSICAL_PREDECESSOR, CONTAINER_STOW_FACTOR, TWIN_CARRY_REFERNCE, T_Load_BlockNO, T_StartTime, T_EndTime);
        }
    }


    [Serializable]
    public class CHESTATUS_BASE : DBClass_BASE
    {
        public Technical_Status TECHNICAL_STATUS = Technical_Status.GREEN;
        public DateTime UPDATED = DateTime.Now;

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            CHESTATUS_BASE cheStatus = other as CHESTATUS_BASE;
            base.Copy(cheStatus);

            this.TECHNICAL_STATUS = cheStatus.TECHNICAL_STATUS;
            this.UPDATED = cheStatus.UPDATED; 
        }
        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {

                switch (strPropertyName)
                {
                    case "TECHNICAL_STATUS":
                        TECHNICAL_STATUS = ConvertEnum<Technical_Status>(propertyValue);
                        break;
                    case "UPDATED":
                        UPDATED = Convert.ToDateTime(propertyValue);
                        break;
                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
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

    [Serializable]
    public class AGV_STATUS : CHESTATUS_BASE
    {
        public String CHE_ID { get; set; }
        public Operational_Status OPERATIONAL_STATUS { get; set; }
        public String TECHNICAL_DETAILS { get; set; }//String(80) Detailed description of the fault status. Null when TECHNICAL_STATUS is GREEN
        public String LIFT_CAPABILITY { get; set; }//String(12) "YES": lift tables present and in working order; "OUT_OF_ORDER": lift tables present but out of order; "NOT PRESENT": s/e
        public Int16 STARTUP_DELAY { get; set; }//Time in seconds after which AGV can start performing an order
        public long ORDER_GKEY { get; set; }
        public long COMMAND_GKEY { get; set; }
        public String LOCATION { get; set; }    //mm
        public String NEXT_LOCATION { get; set; }
        public int LOCATION_X { get; set; }//X coordinate of AGV location. Updated every 15 meters.
        public int LOCATION_Y { get; set; }
        public Int16 ORIENTATION { get; set; }//Current heading of AGV in degrees, 0--359
        public String REFERENCE_ID_1 { get; set; }//String(32)Reference ID generated by the QC system during container transfers
        public String REFERENCE_ID_2 { get; set; }//String(32)
        public String CONTAINER_ID_1 { get; set; }//String(12)
        public String CONTAINER_ID_2 { get; set; }//String(12)
        public Fuel_Type FUEL_TYPE { get; set; }
        public Battery_State BATTERY_STATE { get; set; }
        public String BATTERY_STATION_FACTORS { get; set; }//String(125)
        public Int16 REMAINING_FUEL { get; set; }//Battery charge / fuel level percentage
        public Int16 RUNNING_HOURS { get; set; }//Current running hours of the AGV

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            AGV_STATUS cheStatus = other as AGV_STATUS;
            base.Copy(cheStatus);

            this.CHE_ID = cheStatus.CHE_ID;
            this.OPERATIONAL_STATUS = cheStatus.OPERATIONAL_STATUS;
            this.TECHNICAL_DETAILS = cheStatus.TECHNICAL_DETAILS;
            this.LIFT_CAPABILITY = cheStatus.LIFT_CAPABILITY;
            this.STARTUP_DELAY = cheStatus.STARTUP_DELAY;
            this.ORDER_GKEY = cheStatus.ORDER_GKEY;
            this.COMMAND_GKEY = cheStatus.COMMAND_GKEY;
            this.LOCATION = cheStatus.LOCATION;
            this.NEXT_LOCATION = cheStatus.NEXT_LOCATION;
            this.LOCATION_X = cheStatus.LOCATION_X;
            this.LOCATION_Y = cheStatus.LOCATION_Y;
            this.ORIENTATION = cheStatus.ORIENTATION;
            this.REFERENCE_ID_1 = cheStatus.REFERENCE_ID_1;
            this.REFERENCE_ID_2 = cheStatus.REFERENCE_ID_2;
            this.CONTAINER_ID_1 = cheStatus.CONTAINER_ID_1;
            this.CONTAINER_ID_2 = cheStatus.CONTAINER_ID_2;
            this.FUEL_TYPE = cheStatus.FUEL_TYPE;
            this.BATTERY_STATE = cheStatus.BATTERY_STATE;
            this.BATTERY_STATION_FACTORS = cheStatus.BATTERY_STATION_FACTORS;
            this.REMAINING_FUEL = cheStatus.REMAINING_FUEL;
            this.RUNNING_HOURS = cheStatus.RUNNING_HOURS; 
        }
        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {

                switch (strPropertyName)
                {
                    case "CHE_ID":
                        CHE_ID = Convert.ToString(propertyValue);
                        break;
                    case "OPERATIONAL_STATUS":
                        OPERATIONAL_STATUS = ConvertEnum<Operational_Status>(propertyValue);
                        break;
                    case "TECHNICAL_DETAILS":
                        TECHNICAL_DETAILS = Convert.ToString(propertyValue);
                        break;
                    case "LIFT_CAPABILITY":
                        LIFT_CAPABILITY = Convert.ToString(propertyValue);
                        break;
                    case "STARTUP_DELAY":
                        STARTUP_DELAY = Convert.ToInt16(propertyValue);
                        break;
                    case "ORDER_GKEY":
                        ORDER_GKEY = Convert.ToInt32(propertyValue);
                        break;
                    case "COMMAND_GKEY":
                        COMMAND_GKEY = Convert.ToInt32(propertyValue);
                        break;
                    case "LOCATION":
                        LOCATION = Convert.ToString(propertyValue);
                        break;
                    case "NEXT_LOCATION":
                        NEXT_LOCATION = Convert.ToString(propertyValue);
                        break;
                    case "LOCATION_X":
                        LOCATION_X = Convert.ToInt32(propertyValue);
                        break;
                    case "LOCATION_Y":
                        LOCATION_Y = Convert.ToInt32(propertyValue);
                        break;
                    case "ORIENTATION":
                        ORIENTATION = Convert.ToInt16(propertyValue);
                        break;
                    case "REFERENCE_ID_1":
                        REFERENCE_ID_1 = Convert.ToString(propertyValue);
                        break;
                    case "REFERENCE_ID_2":
                        REFERENCE_ID_2 = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_ID_1":
                        CONTAINER_ID_1 = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_ID_2":
                        CONTAINER_ID_2 = Convert.ToString(propertyValue);
                        break;
                    case "FUEL_TYPE":
                        FUEL_TYPE = ConvertEnum<Fuel_Type>(propertyValue);
                        break;
                    case "BATTERY_STATE":
                        BATTERY_STATE = ConvertEnum<Battery_State>(propertyValue);
                        break;
                    case "BATTERY_STATION_FACTORS":
                        BATTERY_STATION_FACTORS = Convert.ToString(propertyValue);
                        break;
                    case "REMAINING_FUEL":
                        REMAINING_FUEL = Convert.ToInt16(propertyValue);
                        break;
                    case "RUNNING_HOURS":
                        RUNNING_HOURS = Convert.ToInt16(propertyValue);
                        break;
                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
                        break;
                }

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public override string ToString()
        {
            return string.Format("AGV ID {0}: OPERATIONAL_STATUS={1, -10}, LOCATION=({2, -15}, X={3, -6}, Y={4, -6}, ORIENTATION={5, -4}), BATTERY_STATE={6, -6}, TECHNICAL_STATUS={7, -6}",
                CHE_ID, OPERATIONAL_STATUS, LOCATION, LOCATION_X, LOCATION_Y, ORIENTATION, BATTERY_STATE, TECHNICAL_STATUS);
        }
    }

    [Serializable]
    public class ASC_STATUS : CHESTATUS_BASE
    {
        public String CHE_ID { get; set; }
        public Work_Status WORK_STATUS { get; set; }
        public Operational_Status OPERATIONAL_STATUS{ get; set; }
        public String TECHNICAL_DETAILS { get; set; }//String(80) Detailed description of the fault status. Null when TECHNICAL_STATUS is GREEN
        public long ORDER_GKEY{ get; set; }
        public long COMMAND_GKEY{ get; set; }
        public String LOCATION{ get; set; }
        public String CONTAINER_ID{ get; set; }//String(12)
        public string HELP_MODE { get; set; } // "YES" or other value

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            ASC_STATUS cheStatus = other as ASC_STATUS;
            base.Copy(cheStatus);

            this.CHE_ID = cheStatus.CHE_ID;
            this.WORK_STATUS = cheStatus.WORK_STATUS;
            this.OPERATIONAL_STATUS = cheStatus.OPERATIONAL_STATUS;
            this.TECHNICAL_DETAILS = cheStatus.TECHNICAL_DETAILS;
            this.ORDER_GKEY = cheStatus.ORDER_GKEY;
            this.COMMAND_GKEY = cheStatus.COMMAND_GKEY;
            this.LOCATION = cheStatus.LOCATION;
            this.CONTAINER_ID = cheStatus.CONTAINER_ID;
            this.HELP_MODE = cheStatus.HELP_MODE;
        }
        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {

                switch (strPropertyName.ToUpper())
                {
                    case "CHE_ID":
                        CHE_ID = Convert.ToString(propertyValue);
                        break;
                    case "WORK_STATUS":
                        WORK_STATUS = ConvertEnum<Work_Status>(propertyValue);
                        break;
                    case "OPERATIONAL_STATUS":
                        OPERATIONAL_STATUS = ConvertEnum<Operational_Status>(propertyValue);
                        break;
                    case "TECHNICAL_DETAILS":
                        TECHNICAL_DETAILS = Convert.ToString(propertyValue);
                        break; 
                    case "ORDER_GKEY":
                        ORDER_GKEY = Convert.ToInt32(propertyValue);
                        break;
                    case "COMMAND_GKEY":
                        COMMAND_GKEY = Convert.ToInt32(propertyValue);
                        break;
                    case "LOCATION":
                        LOCATION = Convert.ToString(propertyValue);
                        break;
                    case "CONTAINER_ID":
                        CONTAINER_ID = Convert.ToString(propertyValue);
                        break;
                    case "HELP_MODE":
                        HELP_MODE = Convert.ToString(propertyValue);
                        break; 
                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
                        break;
                }

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public override string ToString()
        {
            return string.Format("ASC ID {0, -3}: OPERATIONAL_STATUS={1, -16}, HELP_MODE={2, -3}, LOCATION={3, -10}, TECHNICAL_STATUS={4, -6}", CHE_ID, OPERATIONAL_STATUS, HELP_MODE, LOCATION, TECHNICAL_STATUS);
        }
    }

    [Serializable]
    public class STS_STATUS : CHESTATUS_BASE
    {
        public String QC_ID { get; set; }

        public String QC_BOLLARD { get; set; }//String(7) Number of bollard closest to QC
        public int QC_BOLLARD_OFFSET_CM { get; set; }//Bollard offset in cm

        public UInt32 nAGVCountMax { get; set; }//分配的AGV最大数量
        public UInt32 nAGVCountMin { get; set; }//分配的AGV最小数量
        public Int32 nQCPosition{ get; set; }//QC相对码头原点的坐标X，即岸线的位置。

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            STS_STATUS cheStatus = other as STS_STATUS;
            base.Copy(cheStatus);

            this.QC_ID = cheStatus.QC_ID;
            this.QC_BOLLARD = cheStatus.QC_BOLLARD;
            this.QC_BOLLARD_OFFSET_CM = cheStatus.QC_BOLLARD_OFFSET_CM;
            this.nAGVCountMax = cheStatus.nAGVCountMax;
            this.nAGVCountMin = cheStatus.nAGVCountMin;
            this.nQCPosition = cheStatus.nQCPosition; 
        }
        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {

                switch (strPropertyName.ToUpper())
                {
                    case "QC_ID":
                        QC_ID = Convert.ToString(propertyValue);
                        break;
                    case "QC_BOLLARD":
                        QC_BOLLARD = Convert.ToString(propertyValue);
                        break;
                    case "QC_BOLLARD_OFFSET_CM":
                        QC_BOLLARD_OFFSET_CM = Convert.ToInt32(propertyValue);
                        break;
                    case "NAGVCOUNTMAX":
                        nAGVCountMax = Convert.ToUInt32(propertyValue);
                        break;
                    case "NAGVCOUNTMIN":
                        nAGVCountMin = Convert.ToUInt32(propertyValue);
                        break;
                    case "NQCPOSITION":
                        nQCPosition = Convert.ToInt32(propertyValue);
                        break; 
                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
                        break;
                }

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public override string ToString()
        {
            return string.Format("STS ID {0, -3}: nAGVCountMax={1, -2}, nAGVCountMin={2}, nQCPosition={3}, TECHNICAL_STATUS={4, -6}", QC_ID, nAGVCountMax, nAGVCountMin, nQCPosition, TECHNICAL_STATUS);
        }
    }

    [Serializable]
    public class TRANSFER_POINT_STATUS : DBClass_BASE
    {
        public String TRANSFER_POINT_ID = "";//ID of the transfer point. Always corresponds to the 40 ft location.Primary key, Indexed field - no duplicates
        public String ASSIGNABLE = "";//YES(default)/NO.
        public String CLAIM_OWNER = "";//AGV ID if an AGV is using the TP, otherwise null
        public String LANE_TYPE = "";//AGV, UTR, OTR, TOS
        public DateTime UPDATED = DateTime.Now;

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            TRANSFER_POINT_STATUS cheStatus = other as TRANSFER_POINT_STATUS;
            base.Copy(cheStatus);

            this.TRANSFER_POINT_ID = cheStatus.TRANSFER_POINT_ID;
            this.ASSIGNABLE = cheStatus.ASSIGNABLE;
            this.CLAIM_OWNER = cheStatus.CLAIM_OWNER;
            this.LANE_TYPE = cheStatus.LANE_TYPE;
            this.UPDATED = cheStatus.UPDATED; 
        }
        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {

                switch (strPropertyName)
                {
                    case "TRANSFER_POINT_ID":
                        TRANSFER_POINT_ID = Convert.ToString(propertyValue);
                        break;
                    case "ASSIGNABLE":
                        ASSIGNABLE = Convert.ToString(propertyValue);
                        break;
                    case "CLAIM_OWNER":
                        CLAIM_OWNER = Convert.ToString(propertyValue);
                        break;
                    case "LANE_TYPE":
                        LANE_TYPE = Convert.ToString(propertyValue);
                        break;
                    case "UPDATED":
                        UPDATED = Convert.ToDateTime(propertyValue);
                        break; 
                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
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

    [Serializable]
    public class Block_Container : DBClass_BASE
    {
        public String BLOCK_NO = "";//"A02"
        public int BAY = 0;//42
        public int LANE = 0;//2
        public int TIER = 0;//2
        public string CONTAINER_ID = "";
        public DateTime TIME = DateTime.Now;

        //复制拷贝，子类需重写
        public override void Copy(DBClass_BASE other)
        {
            if (other == null)
                return;
            Block_Container blockContainer = other as Block_Container;
            base.Copy(blockContainer);

            this.BLOCK_NO = blockContainer.BLOCK_NO;
            this.BAY = blockContainer.BAY;
            this.LANE = blockContainer.LANE;
            this.TIER = blockContainer.TIER;
            this.TIME = blockContainer.TIME;
        }
        //设置对象属性，子类需重写
        public override bool SetPropertyValue(string strPropertyName, object propertyValue)
        {
            try
            {

                switch (strPropertyName.ToUpper())
                {
                    case "BLOCK_NO":
                        BLOCK_NO = Convert.ToString(propertyValue);
                        break;
                    case "BAY":
                        BAY = Convert.ToInt32(propertyValue);
                        break;
                    case "LANE":
                        LANE = Convert.ToInt32(propertyValue);
                        break;
                    case "TIER":
                        TIER = Convert.ToInt32(propertyValue);
                        break;
                    case "TIME":
                        TIME = Convert.ToDateTime(propertyValue);
                        break;
                    case "CONTAINER_ID":
                        CONTAINER_ID = Convert.ToString(propertyValue);
                        break;
                    default:
                        //关键地方，此处不可遗漏。
                        return base.SetPropertyValue(strPropertyName, propertyValue);
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
