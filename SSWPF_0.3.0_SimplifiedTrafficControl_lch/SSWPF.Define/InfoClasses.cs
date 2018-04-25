using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Linq;
using System.Text;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.YardMap;
using ZECS.Schedule.DBDefine.CiTOS;

// 由于排序的需要，本命名空间的对象不允许空值。
namespace SSWPF.Define
{
    public static class StringHandle
    {
        public static String DefaultString = "";

        public static bool IfEquivalentString(object obj1, object obj2)
        {
            string str1, str2;

            if (obj1 != null && obj1.GetType().FullName != "System.String")
                return false;
            else
                str1 = (string)obj1;

            if (obj2 != null && obj2.GetType().FullName != "System.String")
                return false;
            else
                str2 = (string)obj2;

            if (str1 == null || str1.Length == 0)
            {
                if (str2 == null || str2.Length == 0) 
                    return true;
                else
                    return false;
            }
            else
            {
                if (str1 == str2)
                    return true;
                else
                    return false;
            }
        }

        public static string GetFixedString(string FromStr)
        {
            if (FromStr == null)
                return  "";
            else
                return FromStr;
        }
    }

    /// <summary>
    /// InfoXXX的父类，包含用反射写的属性更新代码
    /// </summary>
    public class InfoBase
    {
        /// <summary>
        /// 按照obj的属性和状态更新属性值，返回更新次数
        /// </summary>
        /// <param name="obj">参照对象</param>
        /// <returns>更新次数</returns>
        public int RenewChangedProperties(Object obj)
        {
            int renewTime = 0;
            PropertyInfo[] aPIs = obj.GetType().GetProperties();
            PropertyInfo oPIThis;

            foreach (PropertyInfo oPI in aPIs)
            {
                oPIThis = this.GetType().GetProperty(oPI.Name);
                if (oPIThis != null)
                {
                    if (oPI.PropertyType.FullName == "System.String")
                    {
                        if (!StringHandle.IfEquivalentString(oPI.GetValue(obj, null), oPIThis.GetValue(this, null)))
                        {
                            oPIThis.SetValue(this, oPI.GetValue(obj, null), null);
                            renewTime++;
                        }
                    }
                    else
                    {
                        if (oPI.GetValue(obj, null) != oPIThis.GetValue(this, null))
                        {
                            oPIThis.SetValue(this, oPI.GetValue(obj, null), null);
                            renewTime++;
                        }
                    }
                }
            }
            return renewTime;
        }
    }

    public class InfoWorkQueue : InfoBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public InfoWorkQueue()
        {
        }

        public InfoWorkQueue(STS_WORK_QUEUE_STATUS obj)
            : this()
        {
            this.ABOVE_BELOW = obj.ABOVE_BELOW;
            this.CONFIGURATION = obj.CONFIGURATION;
            this.END_TIME = obj.END_TIME;
            this.MOVE_KIND = obj.MOVE_KIND;
            this.QC_BOLLARD = obj.QC_BOLLARD == null ? StringHandle.DefaultString : obj.QC_BOLLARD;
            this.QC_BOLLARD_OFFSET_CM = obj.QC_BOLLARD_OFFSET_CM;
            this.QC_ID = obj.QC_ID == null ? StringHandle.DefaultString : obj.QC_ID;
            this.START_TIME = obj.START_TIME;
            this.UPDATED = obj.UPDATED;
            this.VESSEL_BAY = obj.VESSEL_BAY == null ? StringHandle.DefaultString : obj.VESSEL_BAY;
            this.VESSEL_VISIT = obj.VESSEL_VISIT == null ? StringHandle.DefaultString : obj.VESSEL_VISIT;
            this.WORK_QUEUE = obj.WORK_QUEUE == null ? StringHandle.DefaultString : obj.WORK_QUEUE;
            this.WQ_SEQ = obj.WQ_SEQ;
            this.WQ_STATUS = obj.WQ_STATUS;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(STS_WORK_QUEUE_STATUS obj)
        {
            int iRet;
            // 以作业组号为索引
            if (this.workQueue != obj.WORK_QUEUE)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);

            return iRet;
        }

        private String workQueue;
        public String WORK_QUEUE
        {
            get { return workQueue; }
            set
            {
                workQueue = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("WORK_QUEUE"));
                }
            }
        }

        private Move_Kind moveKind;
        public Move_Kind MOVE_KIND
        { 
            get {return moveKind;}
            set
            {
                moveKind = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("MOVE_KIND"));
                }
            }
        }

        private Above_Below aboveBelow;
        public Above_Below ABOVE_BELOW
        { 
            get{return aboveBelow;}
            set
            {
                aboveBelow = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ABOVE_BELOW"));
                }
            }
        }

        private String qcID; 
        public String QC_ID
        { 
            get {return qcID;}
            set
            {
                qcID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QC_ID"));
                }
            }
        }

        private String vesselVisit;
        public String VESSEL_VISIT
        {
            get { return vesselVisit; }
            set
            {
                vesselVisit = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VESSEL_VISIT"));
                }
            }
        }

        private String vesselBay;
        public String VESSEL_BAY 
        {
            get { return vesselBay; }
            set
            {
                vesselBay = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VESSEL_BAY"));
                }
            }
        }

        private String qcBollard;
        public String QC_BOLLARD
        {
            get { return qcBollard; }
            set
            {
                qcBollard = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QC_BOLLARD"));
                }
            }
        }

        private int qcBollardOffsetCM;
        public int QC_BOLLARD_OFFSET_CM
        {
            get { return qcBollardOffsetCM; }
            set
            {
                qcBollardOffsetCM = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QC_BOLLARD_OFFSET_CM"));
                }
            }
        }

        private int wqSeq;
        public int WQ_SEQ
        {
            get { return wqSeq; }
            set
            {
                wqSeq = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("WQ_SEQ"));
                }
            }
        }

        private Configuration configuration;
        public Configuration CONFIGURATION
        {
            get { return configuration; }
            set
            {
                configuration = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONFIGURATION"));
                }
            }
        }

        private DateTime startTime;
        public DateTime START_TIME
        {
            get { return startTime; }
            set
            {
                startTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("START_TIME"));
                }
            }
        }

        private DateTime endTime;
        public DateTime END_TIME
        {
            get { return endTime; }
            set
            {
                endTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("END_TIME"));
                }
            }
        }

        private WQ_Status wqStatus;
        public WQ_Status WQ_STATUS
        {
            get { return wqStatus; }
            set
            {
                wqStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("WQ_STATUS"));
                }
            }
        }

        private DateTime updated;
        public DateTime UPDATED
        {
            get { return updated; }
            set
            {
                updated = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("UPDATED"));
                }
            }
        }

    }

    public class InfoWorkInstruction : InfoBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public InfoWorkInstruction()
        {
        }

        public InfoWorkInstruction(WORK_INSTRUCTION_STATUS obj)
            : this()
        {
            this.CARRY_REFERENCE = obj.CARRY_REFERENCE;
            this.CONTAINER_HEIGHT_CM = obj.CONTAINER_HEIGHT_CM;
            this.CONTAINER_ID = obj.CONTAINER_ID == null ? StringHandle.DefaultString : obj.CONTAINER_ID;
            this.CONTAINER_ISO = obj.CONTAINER_ISO == null ? StringHandle.DefaultString : obj.CONTAINER_ISO;
            this.CONTAINER_LENGTH_CM = obj.CONTAINER_LENGTH_CM;
            this.CONTAINER_NEXT_QC_LOC_TYPE = obj.CONTAINER_NEXT_QC_LOC_TYPE;
            this.CONTAINER_QC_LOC_TYPE = obj.CONTAINER_QC_LOC_TYPE;
            this.CONTAINER_STOW_FACTOR = obj.CONTAINER_STOW_FACTOR == null ? StringHandle.DefaultString : obj.CONTAINER_STOW_FACTOR;
            this.CONTAINER_WEIGHT_KG = obj.CONTAINER_WEIGHT_KG;
            this.CONTAINER_WEIGHT_MARGIN_KG = obj.CONTAINER_WEIGHT_MARGIN_KG;
            this.CONTAINER_WI_REF = obj.CONTAINER_WI_REF;
            this.DESTINATION_CARRIER_SLOT = obj.DESTINATION_CARRIER_SLOT == null ? StringHandle.DefaultString : obj.DESTINATION_CARRIER_SLOT;
            this.DOOR_DIRECTION = obj.DOOR_DIRECTION;
            this.FUTURE_PLAC = obj.FUTURE_PLAC == null ? StringHandle.DefaultString : obj.FUTURE_PLAC;
            this.HAS_BOTTOM_RAILS = obj.HAS_BOTTOM_RAILS;
            this.HAS_TOP_RAILS = obj.HAS_TOP_RAILS;
            this.HOLD = obj.HOLD;
            this.IS_TANK = obj.IS_TANK;
            this.JOB_ID = obj.JOB_ID == null ? StringHandle.DefaultString : obj.JOB_ID;
            this.LIFT_REFERENCE = obj.LIFT_REFERENCE == null ? StringHandle.DefaultString : obj.LIFT_REFERENCE;
            this.LOGICAL_PREDECESSOR = obj.LOGICAL_PREDECESSOR == null ? StringHandle.DefaultString : obj.LOGICAL_PREDECESSOR;
            this.MOVE_KIND = obj.MOVE_KIND;
            this.MOVE_STAGE = obj.MOVE_STAGE;
            this.OFFSET_TO_BAY_CENTER_CM = obj.OFFSET_TO_BAY_CENTER_CM;
            this.ORDER_SEQ = obj.ORDER_SEQ;
            this.ORIGIN_CARRIER_SLOT = obj.ORIGIN_CARRIER_SLOT == null ? StringHandle.DefaultString : obj.ORIGIN_CARRIER_SLOT;
            this.PHYSICAL_PREDECESSOR = obj.PHYSICAL_PREDECESSOR == null ? StringHandle.DefaultString : obj.PHYSICAL_PREDECESSOR;
            this.PLAN_TIM = obj.PLAN_TIM;
            this.PLAT_POSITION_ID = obj.PLAT_POSITION_ID == null ? StringHandle.DefaultString : obj.PLAT_POSITION_ID;
            this.POINT_OF_WORK = obj.POINT_OF_WORK == null ? StringHandle.DefaultString : obj.POINT_OF_WORK;
            this.RACK_SUITABLE = obj.RACK_SUITABLE;
            this.RELATIVE_POS_ON_CARRIER = obj.RELATIVE_POS_ON_CARRIER == null ? StringHandle.DefaultString : obj.RELATIVE_POS_ON_CARRIER;
            this.T_EndTime = obj.T_EndTime;
            this.T_Load_BlockNO = obj.T_Load_BlockNO == null ? StringHandle.DefaultString : obj.T_Load_BlockNO;
            this.T_StartTime = obj.T_StartTime;
            this.UPDATED = obj.UPDATED;
            this.VESSEL_ID = obj.VESSEL_ID == null ? StringHandle.DefaultString : obj.VESSEL_ID;
            this.WORK_QUEUE = obj.WORK_QUEUE == null ? StringHandle.DefaultString : obj.WORK_QUEUE;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(WORK_INSTRUCTION_STATUS obj)
        {
            int iRet;
            // 以箱号为索引
            if (this.CONTAINER_ID != obj.CONTAINER_ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String containerID;
        public String CONTAINER_ID
        {
            get { return containerID; }
            set
            {
                containerID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ID"));
                }
            }
        }

        private String jobID;
        public String JOB_ID
        {
            get { return jobID; }
            set
            {
                jobID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_ID"));
                }
            }
        }

        private long containerWIRef;
        public long CONTAINER_WI_REF
        {
            get { return containerWIRef; }
            set
            {
                containerWIRef = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_WI_REF"));
                }
            }
        }

        private Int16 containerLengthCM;
        public Int16 CONTAINER_LENGTH_CM
        {
            get { return containerLengthCM; }
            set
            {
                containerLengthCM = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_LENGTH_CM"));
                }
            }
        }

        private Int16 containerHeightCM;
        public Int16 CONTAINER_HEIGHT_CM
        {
            get { return containerLengthCM; }
            set
            {
                containerHeightCM = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_HEIGHT_CM"));
                }
            }
        }

        private int containerWeightKG;
        public int CONTAINER_WEIGHT_KG
        {
            get { return containerWeightKG; }
            set
            {
                containerWeightKG = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_WEIGHT_KG"));
                }
            }
        }

        private String containerISO;
        public String CONTAINER_ISO
        {
            get { return containerISO; }
            set
            {
                containerISO = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ISO"));
                }
            }
        }

        private Is_Tank isTank;
        public Is_Tank IS_TANK
        {
            get { return isTank; }
            set
            {
                isTank = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("IS_TANK"));
                }
            }
        }

        private Rack_Suitable rackSuitable;
        public Rack_Suitable RACK_SUITABLE
        {
            get { return rackSuitable; }
            set
            {
                rackSuitable = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("RACK_SUITABLE"));
                }
            }
        }

        private Hold hold;
        public Hold HOLD
        {
            get { return hold; }
            set
            {
                hold = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("HOLD"));
                }
            }
        }

        private String workQueue;
        public String WORK_QUEUE
        {
            get { return workQueue; }
            set
            {
                workQueue = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("WORK_QUEUE"));
                }
            }
        }

        private String pointOfWork;
        public String POINT_OF_WORK
        {
            get { return pointOfWork;}
            set
            {
                pointOfWork = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("POINT_OF_WORK"));
                }
            }
        }

        private Move_Kind moveKind;
        public Move_Kind MOVE_KIND
        {
            get { return moveKind; }
            set
            {
                moveKind = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("MOVE_KIND"));
                }
            }
        }

        private Move_Stage moveStage; 
        public Move_Stage MOVE_STAGE
        {
            get { return moveStage; }
            set
            {
                moveStage = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("MOVE_STAGE"));
                }
            }
        }

        private int orderSeq;
        public int ORDER_SEQ
        {
            get { return orderSeq; }
            set
            {
                orderSeq = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ORDER_SEQ"));
                }
            }
        }

        private String vesselID;
        public String VESSEL_ID
        {
            get { return vesselID; }
            set
            {
                vesselID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VESSEL_ID"));
                }
            }
        }

        private String logicalPredecessor;
        public String LOGICAL_PREDECESSOR
        {
            get { return logicalPredecessor; }
            set
            {
                logicalPredecessor = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("LOGICAL_PREDECESSOR"));
                }
            }
        }

        private String physicalPredecessor;
        public String PHYSICAL_PREDECESSOR
        {
            get { return physicalPredecessor; }
            set
            {
                physicalPredecessor = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PHYSICAL_PREDECESSOR"));
                }
            }
        }

        private String containerStowFactor;
        public String CONTAINER_STOW_FACTOR
        {
            get { return containerStowFactor; }
            set
            {
                containerStowFactor = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_STOW_FACTOR"));
                }
            }
        }

        private double containerWeightMarginKG;
        public double CONTAINER_WEIGHT_MARGIN_KG
        {
            get { return containerWeightMarginKG; }
            set
            {
                containerWeightMarginKG = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_WEIGHT_MARGIN_KG"));
                }
            }
        }

        private String liftReference;
        public String LIFT_REFERENCE
        {
            get { return liftReference; }
            set
            {
                liftReference = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("LIFT_REFERENCE"));
                }
            }
        }

        private int carryReference;
        public int CARRY_REFERENCE
        {
            get { return carryReference; }
            set
            {
                carryReference = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CARRY_REFERENCE"));
                }
            }
        }

        private String relativePosOnCarrier;
        public String RELATIVE_POS_ON_CARRIER
        {
            get { return relativePosOnCarrier; }
            set
            {
                relativePosOnCarrier = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("RELATIVE_POS_ON_CARRIER"));
                }
            }
        }

        private String originCarrierSlot;
        public String ORIGIN_CARRIER_SLOT
        {
            get { return originCarrierSlot; }
            set
            {
                originCarrierSlot = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ORIGIN_CARRIER_SLOT"));
                }
            }
        }

        private String destinationCarrierSlot;
        public String DESTINATION_CARRIER_SLOT
        {
            get { return destinationCarrierSlot; }
            set
            {
                destinationCarrierSlot = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("DESTINATION_CARRIER_SLOT"));
                }
            }
        }

        private Orientation doorDirection;
        public Orientation DOOR_DIRECTION
        {
            get { return doorDirection; }
            set
            {
                doorDirection = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("DOOR_DIRECTION"));
                }
            }
        }

        private String platPositionID; 
        public String PLAT_POSITION_ID
        {
            get { return platPositionID; }
            set
            {
                platPositionID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLAT_POSITION_ID"));
                }
            }
        }

        private Int16 offsetToBayCenterCM;
        public Int16 OFFSET_TO_BAY_CENTER_CM
        {
            get { return offsetToBayCenterCM; }
            set
            {
                offsetToBayCenterCM = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("OFFSET_TO_BAY_CENTER_CM"));
                }
            }
        }

        private Container_STS_LOC_Type containerQCLocType;
        public Container_STS_LOC_Type CONTAINER_QC_LOC_TYPE
        {
            get { return containerQCLocType; }
            set
            {
                containerQCLocType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_QC_LOC_TYPE"));
                }
            }
        }

        private Container_STS_LOC_Type containerNextQCLocType;
        public Container_STS_LOC_Type CONTAINER_NEXT_QC_LOC_TYPE
        {
            get { return containerNextQCLocType; }
            set
            {
                containerNextQCLocType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_NEXT_QC_LOC_TYPE"));
                }
            }
        }

        private Has hasTopRails;
        public Has HAS_TOP_RAILS
        {
            get { return hasTopRails; }
            set
            {
                hasTopRails = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("HAS_TOP_RAILS"));
                }
            }
        }

        private Has hasBottomRails;
        public Has HAS_BOTTOM_RAILS
        {
            get { return hasBottomRails; }
            set
            {
                hasBottomRails = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("HAS_BOTTOM_RAILS"));
                }
            }
        }

        private DateTime updated;
        public DateTime UPDATED
        {
            get { return updated; }
            set
            {
                updated = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("UPDATED"));
                }
            }
        }

        private DateTime planTime;
        public DateTime PLAN_TIM
        {
            get { return planTime; }
            set
            {
                planTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLAN_TIM"));
                }
            }
        }

        private String futurePlac;
        public String FUTURE_PLAC
        {
            get { return futurePlac; }
            set
            {
                futurePlac = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FUTURE_PLAC"));
                }
            }
        }

        private String tLoadBlockNo;
        public String T_Load_BlockNO
        {
            get { return tLoadBlockNo; }
            set
            {
                tLoadBlockNo = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("T_Load_BlockNO"));
                }

            }
        }

        // 任务起始时间
        private DateTime tStartTime;
        public DateTime T_StartTime
        {
            get { return tStartTime; }
            set
            {
                tStartTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("T_StartTime"));
                }
            }
        }

        // 任务结束时间
        private DateTime tEndTime;
        public DateTime T_EndTime
        {
            get { return tEndTime; }
            set
            {
                tEndTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("T_EndTime"));
                }
            }
        } 

    }

    public class InfoBerthStatus : InfoBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public InfoBerthStatus()
        {
        }

        public InfoBerthStatus(BERTH_STATUS obj)
            : this()
        {
            this.VESSEL_VISIT = obj.VESSEL_VISIT == null ? StringHandle.DefaultString : obj.VESSEL_VISIT;
            this.VESSEL_CALL_SIGN = obj.VESSEL_CALL_SIGN == null ? StringHandle.DefaultString : obj.VESSEL_CALL_SIGN;
            this.VESSEL_NAME = obj.VESSEL_NAME == null ? StringHandle.DefaultString : obj.VESSEL_NAME;
            this.BOW_BOLLARD = obj.BOW_BOLLARD == null ? StringHandle.DefaultString : obj.BOW_BOLLARD;
            this.BOW_BOLLARD_OFFSET_CM = obj.BOW_BOLLARD_OFFSET_CM;
            this.STERN_BOLLARD = obj.STERN_BOLLARD == null ? StringHandle.DefaultString : obj.STERN_BOLLARD;
            this.STERN_BOLLARD_OFFSET_CM = obj.STERN_BOLLARD_OFFSET_CM;
            this.VESSEL_CLASSIFICATION = obj.VESSEL_CLASSIFICATION == null ? StringHandle.DefaultString : obj.VESSEL_CLASSIFICATION;
            this.VESSEL_VISIT_PHASE = obj.VESSEL_VISIT_PHASE == null ? StringHandle.DefaultString : obj.VESSEL_VISIT_PHASE;
            this.UPDATED = obj.UPDATED;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(BERTH_STATUS obj)
        {
            int iRet;
            if (this.vesselName != obj.VESSEL_NAME)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String vesselVisit;
        public String VESSEL_VISIT 
        {
            get { return vesselVisit; }
            set
            {
                vesselVisit = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VESSEL_VISIT"));
                }
            }
        }

        private String vesselCallSign;
        public String VESSEL_CALL_SIGN 
        {
            get { return vesselCallSign; }
            set
            {
                vesselCallSign = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VESSEL_CALL_SIGN"));
                }
            }
        }

        private String vesselName;
        public String VESSEL_NAME
        {
            get { return vesselName; }
            set
            {
                vesselName = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VESSEL_NAME"));
                }
            }
        }

        private String bowBollard;
        public String BOW_BOLLARD
        {
            get { return bowBollard; }
            set
            {
                bowBollard = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("BOW_BOLLARD"));
                }
            }
        }

        private int bowBollardOffsetCM;
        public int BOW_BOLLARD_OFFSET_CM 
        {
            get { return bowBollardOffsetCM; }
            set
            {
                bowBollardOffsetCM = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("BOW_BOLLARD_OFFSET_CM"));
                }
            }
        }

        private String sternBollard;
        public String STERN_BOLLARD
        {
            get { return sternBollard; }
            set
            {
                sternBollard = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("STERN_BOLLARD"));
                }
            }
        }

        private int sternBollardOffsetCM;
        public int STERN_BOLLARD_OFFSET_CM 
        {
            get { return sternBollardOffsetCM; }
            set
            {
                sternBollardOffsetCM = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("STERN_BOLLARD_OFFSET_CM"));
                }
            }
        }

        private String vesselClassification; 
        public String VESSEL_CLASSIFICATION
        {
            get { return vesselClassification; }
            set
            {
                vesselClassification = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VESSEL_CLASSIFICATION"));
                }
            }
        }

        // 唉。。。将错就错吧
        private String vesselVisitPhase;
        public String VESSEL_VISIT_PHASE
        {
            get { return vesselVisitPhase; }
            set
            {
                vesselVisitPhase = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VESSEL_VISIT_PHASE"));
                }

            }
        }

        private DateTime updated;
        public DateTime UPDATED 
        {
            get { return updated; }
            set
            {
                updated = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("UPDATED"));
                }
            }
        }

    }

    public class InfoSTSResJob : InfoBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public InfoSTSResJob()
        {
        }

        public InfoSTSResJob(STS_ResJob obj)
            : this()
        {
            this.ORDER_ID = obj.ORDER_ID == null ? StringHandle.DefaultString : obj.ORDER_ID;
            this.COMMAND_ID = obj.COMMAND_ID == null ? StringHandle.DefaultString : obj.COMMAND_ID;
            this.ID = obj.ID;
            this.MESSAGE_ID = obj.MESSAGE_ID == null ? StringHandle.DefaultString : obj.MESSAGE_ID;
            this.JOB_TYPE = obj.JOB_TYPE == null ? StringHandle.DefaultString : obj.JOB_TYPE;
            this.MOVE_NO = obj.MOVE_NO == null ? StringHandle.DefaultString : obj.MOVE_NO;
            this.JOB_ID = obj.JOB_ID == null ? StringHandle.DefaultString : obj.JOB_ID;
            this.RESERVE = obj.RESERVE == null ? StringHandle.DefaultString : obj.RESERVE;
            this.JOB_LINK = obj.JOB_LINK == null ? StringHandle.DefaultString : obj.JOB_LINK;
            this.CHE_ID = obj.CHE_ID == null ? StringHandle.DefaultString : obj.CHE_ID;
            this.YARD_ID = obj.YARD_ID == null ? StringHandle.DefaultString : obj.YARD_ID;
            this.QUAY_ID = obj.QUAY_ID == null ? StringHandle.DefaultString : obj.QUAY_ID;
            this.PRIORITY = obj.PRIORITY;
            this.JOB_STATUS = obj.JOB_STATUS == null ? StringHandle.DefaultString : obj.JOB_STATUS;
            this.PLATFORM_CONFIRM = obj.PLATFORM_CONFIRM == null ? StringHandle.DefaultString : obj.PLATFORM_CONFIRM;
            this.FROM_TRUCK_TYPE = obj.FROM_TRUCK_TYPE == null ? StringHandle.DefaultString : obj.FROM_TRUCK_TYPE;
            this.FROM_TRUCK_ID = obj.FROM_TRUCK_ID == null ? StringHandle.DefaultString : obj.FROM_TRUCK_ID;
            this.FROM_TRUCK_POS = obj.FROM_TRUCK_POS == null ? StringHandle.DefaultString : obj.FROM_TRUCK_POS;
            this.FROM_BAY_TYPE = obj.FROM_BAY_TYPE == null ? StringHandle.DefaultString : obj.FROM_BAY_TYPE;
            this.FROM_BAY = obj.FROM_BAY == null ? StringHandle.DefaultString : obj.FROM_BAY;
            this.FROM_LANE = obj.FROM_LANE == null ? StringHandle.DefaultString : obj.FROM_LANE;
            this.FROM_TIER = obj.FROM_TIER == null ? StringHandle.DefaultString : obj.FROM_TIER;
            this.TO_TRUCK_ID = obj.TO_TRUCK_ID == null ? StringHandle.DefaultString : obj.TO_TRUCK_ID;
            this.TO_TRUCK_TYPE = obj.TO_TRUCK_TYPE == null ? StringHandle.DefaultString : obj.TO_TRUCK_TYPE;
            this.TO_TRUCK_POS = obj.TO_TRUCK_POS == null ? StringHandle.DefaultString : obj.TO_TRUCK_POS;
            this.TO_BAY_TYPE = obj.TO_BAY_TYPE == null ? StringHandle.DefaultString : obj.TO_BAY_TYPE;
            this.TO_BAY = obj.TO_BAY == null ? StringHandle.DefaultString : obj.TO_BAY;
            this.TO_LANE = obj.TO_LANE == null ? StringHandle.DefaultString : obj.TO_LANE;
            this.TO_TIER = obj.TO_TIER == null ? StringHandle.DefaultString : obj.TO_TIER;
            this.CONTAINER_ID = obj.CONTAINER_ID == null ? StringHandle.DefaultString : obj.CONTAINER_ID;
            this.CONTAINER_ISO = obj.CONTAINER_ISO == null ? StringHandle.DefaultString : obj.CONTAINER_ISO;
            this.CONTAINER_LENGTH = obj.CONTAINER_LENGTH == null ? StringHandle.DefaultString : obj.CONTAINER_LENGTH;
            this.CONTAINER_HEIGHT = obj.CONTAINER_HEIGHT == null ? StringHandle.DefaultString : obj.CONTAINER_HEIGHT;
            this.CONTAINER_WEIGHT = obj.CONTAINER_WEIGHT == null ? StringHandle.DefaultString : obj.CONTAINER_WEIGHT;
            this.CONTAINER_IS_EMPTY = obj.CONTAINER_IS_EMPTY == null ? StringHandle.DefaultString : obj.CONTAINER_IS_EMPTY;
            this.CONTAINER_DOOR_DIRECTION = obj.CONTAINER_DOOR_DIRECTION == null ? StringHandle.DefaultString : obj.CONTAINER_DOOR_DIRECTION;
            this.VESSEL_ID = obj.VESSEL_ID == null ? StringHandle.DefaultString : obj.VESSEL_ID;
            this.PLAN_START_TIME = obj.PLAN_START_TIME;
            this.PLAN_END_TIME = obj.PLAN_END_TIME;
            this.DATETIME = obj.DATETIME;
            this.VERSION = obj.VERSION;
            this.FROM_RFID = obj.FROM_RFID == null ? StringHandle.DefaultString : obj.FROM_RFID;
            this.TO_RFID = obj.TO_RFID == null ? StringHandle.DefaultString : obj.TO_RFID;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(STS_ResJob obj)
        {
            int iRet;
            if (this.id != obj.ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String orderID;
        public String ORDER_ID
        {
            get { return orderID; }
            set
            {
                orderID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ORDER_ID"));
                }
            }
        }

        private String commandID;
        public String COMMAND_ID
        {
            get { return commandID; }
            set
            {
                commandID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("COMMAND_ID"));
                }
            }
        }

        private Int64 id;
        public Int64 ID
        {
            get { return id; }
            set
            {
                id = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ID"));
                }
            }
        }

        private String messageID;
        public String MESSAGE_ID
        {
            get { return messageID; }
            set
            {
                messageID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("MESSAGE_ID"));
                }
            }
        }

        private String jobType;
        public String JOB_TYPE
        {
            get { return jobType; }
            set
            {
                jobType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_TYPE"));
                }
            }
        }

        private String moveNo;
        public String MOVE_NO
        {
            get { return moveNo; }
            set
            {
                moveNo = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("MOVE_NO"));
                }
            }
        }

        private String jobID;
        public String JOB_ID
        {
            get { return jobID; }
            set
            {
                jobID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_ID"));
                }
            }
        }

        private String reserve;
        public String RESERVE
        {
            get { return reserve; }
            set
            {
                reserve = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("RESERVE"));
                }
            }
        }

        private String jobLink;
        public String JOB_LINK
        {
            get { return jobLink; }
            set
            {
                jobLink = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_LINK"));
                }
            }
        }

        private String cheID;
        public String CHE_ID
        {
            get { return cheID; }
            set
            {
                cheID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CHE_ID"));
                }
            }
        }

        private String yardID; 
        public String YARD_ID
        {
            get { return yardID; }
            set
            {
                yardID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("YARD_ID"));
                }
            }
        }

        private String quayID;
        public String QUAY_ID
        {
            get { return quayID; }
            set
            {
                quayID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QUAY_ID"));
                }
            }
        }

        private int priority;
        public int PRIORITY
        {
            get { return priority; }
            set
            {
                priority = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PRIORITY"));
                }
            }
        }

        private String jobStatus;
        public String JOB_STATUS
        {
            get { return jobStatus; }
            set
            {
                jobStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_STATUS"));
                }
            }
        }

        private String platformConfirm; 
        public String PLATFORM_CONFIRM
        {
            get { return platformConfirm; }
            set
            {
                platformConfirm = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLATFORM_CONFIRM"));
                }
            }
        }

        private String fromTruckType;
        public String FROM_TRUCK_TYPE
        {
            get { return fromTruckType; }
            set
            {
                fromTruckType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_TYPE"));
                }
            }
        }

        private String fromTruckID;
        public String FROM_TRUCK_ID
        {
            get { return fromTruckID; }
            set
            {
                fromTruckID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_ID"));
                }
            }
        }

        private String fromTruckPos;
        public String FROM_TRUCK_POS
        {
            get { return fromTruckPos; }
            set
            {
                fromTruckPos = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_POS"));
                }
            }
        }

        private String fromBayType;
        public String FROM_BAY_TYPE
        {
            get { return fromBayType; }
            set
            {
                fromBayType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BAY_TYPE"));
                }
            }
        }

        private String fromBay;
        public String FROM_BAY
        {
            get { return fromBay; }
            set
            {
                fromBay = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BAY"));
                }
            }
        }

        private String fromLane;
        public String FROM_LANE
        {
            get { return fromLane; }
            set
            {
                fromLane = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_LANE"));
                }
            }
        }

        private String fromTier;
        public String FROM_TIER 
        {
            get { return fromTier; }
            set
            {
                fromTier = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TIER"));
                }
            }
        }

        private String toTruckID;
        public String TO_TRUCK_ID
        {
            get { return toTruckID; }
            set
            {
                toTruckID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_ID"));
                }
            }
        }

        private String toTruckType;
        public String TO_TRUCK_TYPE
        {
            get { return toTruckType; }
            set
            {
                toTruckType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_TYPE"));
                }
            }
        }

        private String toTruckPos;
        public String TO_TRUCK_POS
        {
            get { return toTruckPos; }
            set
            {
                toTruckPos = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_POS"));
                }
            }
        }

        private String toBayType;
        public String TO_BAY_TYPE
        {
            get { return toBayType; }
            set
            {
                toBayType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BAY_TYPE"));
                }
            }
        }

        private String toBay;
        public String TO_BAY
        {
            get { return toBay; }
            set
            {
                toBay = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BAY"));
                }
            }
        }

        private String toLane;
        public String TO_LANE
        {
            get { return toLane; }
            set
            {
                toLane = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_LANE"));
                }
            }
        }

        private String toTier;
        public String TO_TIER
        {
            get { return toTier; }
            set
            {
                toTier = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TIER"));
                }
            }
        }

        private String containerID;
        public String CONTAINER_ID 
        {
            get { return containerID; }
            set
            {
                containerID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ID"));
                }
            }
        }

        private String containerISO;
        public String CONTAINER_ISO
        {
            get { return containerISO; }
            set
            {
                containerISO = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ISO"));
                }
            }
        }

        private String containerLength;
        public String CONTAINER_LENGTH
        {
            get { return containerLength; }
            set
            {
                containerLength = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_LENGTH"));
                }
            }
        }

        private String containerHeight; 
        public String CONTAINER_HEIGHT
        { 
            get { return containerHeight; }
            set
            {
                containerHeight = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_HEIGHT"));
                }
            }
        }

        private String containerWeight;
        public String CONTAINER_WEIGHT
        {
            get { return containerWeight; }
            set
            {
                containerWeight = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_WEIGHT"));
                }
            }
        }

        private String containerIsEmpty;
        public String CONTAINER_IS_EMPTY
        {
            get { return containerIsEmpty; }
            set
            {
                containerIsEmpty = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_IS_EMPTY"));
                }
            }
        }

        private String containerDoorDirection;
        public String CONTAINER_DOOR_DIRECTION
        {
            get { return containerDoorDirection; }
            set
            {
                containerDoorDirection = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_DOOR_DIRECTION"));
                }
            }
        }

        private String vesselID;
        public String VESSEL_ID
        {
            get { return vesselID; }
            set
            {
                vesselID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VESSEL_ID"));
                }
            }
        }

        private DateTime planStartTime;
        public DateTime PLAN_START_TIME
        {
            get { return planStartTime; }
            set
            {
                planStartTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLAN_START_TIME"));
                }
            }
        }

        private DateTime planEndTime;
        public DateTime PLAN_END_TIME
        {
            get { return planEndTime; }
            set
            {
                planEndTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLAN_END_TIME"));
                }
            }
        }

        private DateTime datetime;
        public DateTime DATETIME
        {
            get { return datetime; }
            set
            {
                datetime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("DATETIME"));
                }
            }
        }

        private int version;
        public int VERSION
        {
            get { return version; }
            set
            {
                version = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VERSION"));
                }
            }
        }

        private String fromRFID;
        public String FROM_RFID
        {
            get { return fromRFID; }
            set
            {
                fromRFID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_RFID"));
                }
            }
        }

        private String toRFID;
        public String TO_RFID
        {
            get { return toRFID; }
            set
            {
                toRFID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_RFID"));
                }
            }
        }
    }

    public class InfoSTSTask : InfoBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public InfoSTSTask()
        {
        }

        public InfoSTSTask(STS_Task obj)
            : this()
        {
            this.ID = obj.ID;
            this.Task = obj.Task == null ? new STS_ResJob { ID = -1 } : obj.Task;
            this.Order = obj.Order == null ? new STS_Order { ORDER_ID = StringHandle.DefaultString } : obj.Order;
            this.TaskState = obj.TaskState;
            this.ErrorCode = obj.ErrorCode;
            this.TaskPlatform = obj.TaskPlatform == null ? StringHandle.DefaultString : obj.TaskPlatform;
            this.ContainerLocation = obj.ContainerLocation == null ? StringHandle.DefaultString : obj.ContainerLocation;
            this.LastContainerLocation = obj.LastContainerLocation == null ? StringHandle.DefaultString : obj.LastContainerLocation;
            this.TaskSpreaderPosition = obj.TaskSpreaderPosition == null ? StringHandle.DefaultString : obj.TaskSpreaderPosition;
            this.TaskProcess = obj.TaskProcess;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(STS_Task obj)
        {
            int iRet;
            if (this.id != obj.ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private Int64 id;
        public Int64 ID
        {
            get { return id; }
            set
            {
                id = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ID"));
                }

            }
        }

        private STS_ResJob task;
        public STS_ResJob Task
        {
            get { return task; }
            set
            {
                task = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("Task"));
                }
            }
        }

        private STS_Order order;
        public STS_Order Order
        {
            get { return order; }
            set
            {
                order = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("Order"));
                }
            }
        }

        private TaskStatus taskState;
        public TaskStatus TaskState
        {
            get { return taskState; }
            set
            {
                taskState = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TaskState"));
                }
            }
        }

        private int errorCode;
        public int ErrorCode
        {
            get { return errorCode; }
            set
            {
                errorCode = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ErrorCode"));
                }
            }
        }

        private String taskPlatform;
        public String TaskPlatform
        {
            get { return taskPlatform; }
            set
            {
                taskPlatform = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TaskPlatform"));
                }
            }
        }

        private String containerLocation;
        public String ContainerLocation
        {
            get { return containerLocation; }
            set
            {
                containerLocation = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ContainerLocation"));
                }
            }
        }

        private String lastContainerLocation;
        public String LastContainerLocation
        {
            get { return lastContainerLocation; }
            set
            {
                lastContainerLocation = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("LastContainerLocation"));
                }
            }
        }

        private String taskSpreaderPosition;
        public String TaskSpreaderPosition
        {
            get { return taskSpreaderPosition; }
            set
            {
                taskSpreaderPosition = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TaskSpreaderPosition"));
                }
            }
        }

        private Int32 taskProcess;
        public Int32 TaskProcess
        {
            get { return taskProcess; }
            set
            {
                taskProcess = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TaskProcess"));
                }
            }
        }

    }

    public class InfoASCResJob : InfoBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public InfoASCResJob()
        {
        }

        public InfoASCResJob(ASC_ResJob obj)
            : this()
        {
            this.ORDER_ID = obj.ORDER_ID == null ? StringHandle.DefaultString : obj.ORDER_ID;
            this.COMMAND_ID = obj.COMMAND_ID == null ? StringHandle.DefaultString : obj.COMMAND_ID;
            this.ID = obj.ID;
            this.MESSAGE_ID = obj.MESSAGE_ID == null ? StringHandle.DefaultString : obj.MESSAGE_ID;
            this.JOB_TYPE = obj.JOB_TYPE == null ? StringHandle.DefaultString : obj.JOB_TYPE;
            this.JOB_ID = obj.JOB_ID == null ? StringHandle.DefaultString : obj.JOB_ID;
            this.RESERVE = obj.RESERVE == null ? StringHandle.DefaultString : obj.RESERVE;
            this.JOB_LINK = obj.JOB_LINK == null ? StringHandle.DefaultString : obj.JOB_LINK;
            this.CHE_ID = obj.CHE_ID == null ? StringHandle.DefaultString : obj.CHE_ID;
            this.YARD_ID = obj.YARD_ID == null ? StringHandle.DefaultString : obj.YARD_ID;
            this.QUAY_ID = obj.QUAY_ID == null ? StringHandle.DefaultString : obj.QUAY_ID;
            this.PRIORITY = obj.PRIORITY;
            this.JOB_STATUS = obj.JOB_STATUS == null ? StringHandle.DefaultString : obj.JOB_STATUS;
            this.FROM_TRUCK_TYPE = obj.FROM_TRUCK_TYPE == null ? StringHandle.DefaultString : obj.FROM_TRUCK_TYPE;
            this.FROMPALLETTYPE = obj.FROMPALLETTYPE == null ? StringHandle.DefaultString : obj.FROMPALLETTYPE;
            this.FROM_TRUCK_ID = obj.FROM_TRUCK_ID == null ? StringHandle.DefaultString : obj.FROM_TRUCK_ID;
            this.FROM_TRUCK_POS = obj.FROM_TRUCK_POS == null ? StringHandle.DefaultString : obj.FROM_TRUCK_POS;
            this.FROM_RFID = obj.FROM_RFID == null ? StringHandle.DefaultString : obj.FROM_RFID;
            this.FROM_BLOCK = obj.FROM_BLOCK == null ? StringHandle.DefaultString : obj.FROM_BLOCK;
            this.FROM_BAY_TYPE = obj.FROM_BAY_TYPE == null ? StringHandle.DefaultString : obj.FROM_BAY_TYPE;
            this.FROM_BAY = obj.FROM_BAY == null ? StringHandle.DefaultString : obj.FROM_BAY;
            this.FROM_LANE = obj.FROM_LANE == null ? StringHandle.DefaultString : obj.FROM_LANE;
            this.FROM_TIER = obj.FROM_TIER == null ? StringHandle.DefaultString : obj.FROM_TIER;
            this.TO_TRUCK_ID = obj.TO_TRUCK_ID == null ? StringHandle.DefaultString : obj.TO_TRUCK_ID;
            this.TO_TRUCK_TYPE = obj.TO_TRUCK_TYPE == null ? StringHandle.DefaultString : obj.TO_TRUCK_TYPE;
            this.TOPALLETTYPE = obj.TOPALLETTYPE == null ? StringHandle.DefaultString : obj.TOPALLETTYPE;
            this.TO_TRUCK_POS = obj.TO_TRUCK_POS == null ? StringHandle.DefaultString : obj.TO_TRUCK_POS;
            this.TO_RFID = obj.TO_RFID == null ? StringHandle.DefaultString : obj.TO_RFID;
            this.TO_BLOCK = obj.TO_BLOCK == null ? StringHandle.DefaultString : obj.TO_BLOCK;
            this.TO_BAY_TYPE = obj.TO_BAY_TYPE == null ? StringHandle.DefaultString : obj.TO_BAY_TYPE;
            this.TO_BAY = obj.TO_BAY == null ? StringHandle.DefaultString : obj.TO_BAY;
            this.TO_LANE = obj.TO_LANE == null ? StringHandle.DefaultString : obj.TO_LANE;
            this.TO_TIER = obj.TO_TIER == null ? StringHandle.DefaultString : obj.TO_TIER;
            this.CONTAINER_ID = obj.CONTAINER_ID == null ? StringHandle.DefaultString : obj.CONTAINER_ID;
            this.CONTAINER_ISO = obj.CONTAINER_ISO == null ? StringHandle.DefaultString : obj.CONTAINER_ISO;
            this.CONTAINER_LENGTH = obj.CONTAINER_LENGTH == null ? StringHandle.DefaultString : obj.CONTAINER_LENGTH;
            this.CONTAINER_HEIGHT = obj.CONTAINER_HEIGHT == null ? StringHandle.DefaultString : obj.CONTAINER_HEIGHT;
            this.CONTAINER_WEIGHT = obj.CONTAINER_WEIGHT == null ? StringHandle.DefaultString : obj.CONTAINER_WEIGHT;
            this.CONTAINER_IS_EMPTY = obj.CONTAINER_IS_EMPTY == null ? StringHandle.DefaultString : obj.CONTAINER_IS_EMPTY;
            this.CONTAINER_DOOR_DIRECTION = obj.CONTAINER_DOOR_DIRECTION == null ? StringHandle.DefaultString : obj.CONTAINER_DOOR_DIRECTION;
            this.PLAN_START_TIME = obj.PLAN_START_TIME;
            this.PLAN_END_TIME = obj.PLAN_END_TIME;
            this.DATETIME = obj.DATETIME;
            this.OPERATOR_ID = obj.OPERATOR_ID == null ? StringHandle.DefaultString : obj.OPERATOR_ID;
            this.VERSION = obj.VERSION;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(ASC_ResJob obj)
        {
            int iRet;
            if (this.id != obj.ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String orderID;
        public String ORDER_ID
        {
            get { return orderID; }
            set
            {
                orderID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ORDER_ID"));
                }
            }
        }

        private String commandID;
        public String COMMAND_ID
        {
            get { return commandID; }
            set
            {
                commandID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("COMMAND_ID"));
                }
            }
        }

        private Int64 id;
        public Int64 ID
        {
            get { return id; }
            set
            {
                id = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ID"));
                }
            }
        }

        private String messageID;
        public String MESSAGE_ID
        {
            get { return messageID; }
            set
            {
                messageID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("MESSAGE_ID"));
                }
            }
        }

        private String jobType;
        public String JOB_TYPE 
        {
            get { return jobType; }
            set
            {
                jobType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_TYPE"));
                }
            }
        }

        private String jobID;
        public String JOB_ID
        {
            get { return jobID; }
            set
            {
                jobID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_ID"));
                }
            }
        }

        private String reserve;
        public String RESERVE
        {
            get { return reserve; }
            set
            {
                reserve = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("RESERVE"));
                }
            }
        }

        private String jobLink;
        public String JOB_LINK
        {
            get { return jobLink; }
            set
            {
                jobLink = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_LINK"));
                }
            }
        }

        private String cheID;
        public String CHE_ID
        {
            get { return cheID; }
            set
            {
                cheID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CHE_ID"));
                }
            }
        }

        private String yardID;
        public String YARD_ID
        {
            get { return yardID; }
            set
            {
                yardID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("YARD_ID"));
                }
            }
        }

        private String quayID;
        public String QUAY_ID
        {
            get { return quayID; }
            set
            {
                quayID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QUAY_ID"));
                }
            }
        }

        private int priority;
        public int PRIORITY
        {
            get { return priority; }
            set
            {
                priority = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PRIORITY"));
                }
            }
        }

        private String jobStatus;
        public String JOB_STATUS
        {
            get { return jobStatus; }
            set
            {
                jobStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_STATUS"));
                }
            }
        }

        private String fromTruckType;
        public String FROM_TRUCK_TYPE
        {
            get { return fromTruckType; }
            set
            {
                fromTruckType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_TYPE"));
                }
            }
        }

        private String fromPalletType; 
        public String FROMPALLETTYPE
        {
            get { return fromPalletType; }
            set
            {
                fromPalletType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROMPALLETTYPE"));
                }
            }
        }

        private String fromTruckID;
        public String FROM_TRUCK_ID
        {
            get { return fromTruckID; }
            set
            {
                fromTruckID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_ID"));
                }
            }
        }

        private String fromTruckPos;
        public String FROM_TRUCK_POS
        {
            get { return fromTruckPos; }
            set
            {
                fromTruckPos = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_POS"));
                }
            }
        }

        private String fromRFID;
        public String FROM_RFID
        {
            get { return fromRFID; }
            set
            {
                fromRFID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_RFID"));
                }
            }
        }

        private String fromBlock;
        public String FROM_BLOCK
        {
            get { return fromBlock; }
            set
            {
                fromBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BLOCK"));
                }
            }
        }

        private String fromBayType;
        public String FROM_BAY_TYPE
        {
            get { return fromBayType; }
            set
            {
                fromBayType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BAY_TYPE"));
                }
            }
        }

        private String fromBay;
        public String FROM_BAY 
        {
            get { return fromBay; }
            set
            {
                fromBay = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BAY"));
                }
            }
        }

        private String fromLane;
        public String FROM_LANE
        {
            get { return fromLane; }
            set
            {
                fromLane = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_LANE"));
                }
            }
        }

        private String fromTier;
        public String FROM_TIER
        {
            get { return fromTier; }
            set
            {
                fromTier = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TIER"));
                }
            }
        }

        private String toTruckID;
        public String TO_TRUCK_ID
        {
            get { return toTruckID; }
            set
            {
                toTruckID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_ID"));
                }
            }
        }

        private String toTruckType;
        public String TO_TRUCK_TYPE
        {
            get { return toTruckType; }
            set
            {
                toTruckType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_TYPE"));
                }
            }
        }

        private String toPalletType;
        public String TOPALLETTYPE 
        {
            get { return toTruckType; }
            set
            {
                toPalletType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TOPALLETTYPE"));
                }
            }
        }

        private String toTruckPos;
        public String TO_TRUCK_POS
        {
            get { return toTruckPos; }
            set
            {
                toTruckPos = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_POS"));
                }
            }
        }

        private String toRFID;
        public String TO_RFID
        {
            get { return toRFID; }
            set
            {
                toRFID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_RFID"));
                }
            }
        }

        private String toBlock;
        public String TO_BLOCK
        {
            get { return toBlock; }
            set
            {
                toBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BLOCK"));
                }
            }
        }

        private String toBayType;
        public String TO_BAY_TYPE
        {
            get { return toBayType; }
            set
            {
                toBayType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BAY_TYPE"));
                }
            }
        }

        private String toBay;
        public String TO_BAY
        {
            get { return toBay; }
            set
            {
                toBay = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BAY"));
                }
            }
        }

        private String toLane;
        public String TO_LANE
        {
            get { return toLane; }
            set
            {
                toLane = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_LANE"));
                }
            }
        }

        private String toTier;
        public String TO_TIER
        {
            get { return toTier; }
            set
            {
                toTier = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TIER"));
                }
            }
        }

        private String containerID;
        public String CONTAINER_ID
        {
            get { return containerID; }
            set
            {
                containerID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ID"));
                }
            }
        }

        private String containerISO;
        public String CONTAINER_ISO
        {
            get { return containerISO; }
            set
            {
                containerISO = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ISO"));
                }
            }
        }

        private String containerLength;
        public String CONTAINER_LENGTH 
        {
            get { return containerLength; }
            set
            {
                containerLength = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_LENGTH"));
                }
            }
        }

        private String containerHeight;
        public String CONTAINER_HEIGHT 
        {
            get { return containerHeight; }
            set
            {
                containerHeight = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_FEIGHT"));
                }
            }
        }

        private String containerWeight;
        public String CONTAINER_WEIGHT
        {
            get { return containerWeight; }
            set
            {
                containerWeight = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_WEIGHT"));
                }
            }
        }

        private String containerIsEmpty;
        public String CONTAINER_IS_EMPTY
        {
            get { return containerIsEmpty; }
            set
            {
                containerIsEmpty = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_IS_EMPTY"));
                }
            }
        }

        private String containerDoorDirection;
        public String CONTAINER_DOOR_DIRECTION
        {
            get { return containerDoorDirection; }
            set
            {
                containerDoorDirection = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_DOOR_DIRECTION"));
                }
            }
        }

        private DateTime planStartTime;
        public DateTime PLAN_START_TIME
        {
            get { return planStartTime; }
            set
            {
                planStartTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLAN_START_TIME"));
                }
            }
        }

        private DateTime planEndTime;
        public DateTime PLAN_END_TIME
        {
            get { return planStartTime; }
            set
            {
                planEndTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLAN_END_TIME"));
                }
            }
        }

        private DateTime datetime;
        public DateTime DATETIME
        {
            get { return datetime; }
            set
            {
                datetime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("DATETIME"));
                }
            }
        }

        private String operatorID;
        public String OPERATOR_ID
        {
            get { return operatorID; }
            set
            {
                operatorID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("OPERATOR_ID"));
                }
            }
        }

        private int version;
        public int VERSION
        {
            get { return version; }
            set
            {
                version = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VERSION"));
                }
            }
        }

    }

    public class InfoASCTask : InfoBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public InfoASCTask()
        {
        }

        public InfoASCTask(ASC_Task obj)
            : this()
        {
            this.ID = obj.ID;
            this.Task = obj.Task == null ? new ASC_ResJob { ID = -1 } : obj.Task;
            this.Order = obj.Order == null ? new ASC_Order { ORDER_ID = StringHandle.DefaultString } : obj.Order;
            this.TaskState = obj.TaskState;
            this.ErrorCode = obj.ErrorCode;
            this.TaskProcess = obj.TaskProcess;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(ASC_Task obj)
        {
            int iRet;
            if (this.id != obj.ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private Int64 id;
        public Int64 ID
        {
            get { return id; }
            set
            {
                id = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ID"));
                }
            }
        }

        private ASC_ResJob task;
        public ASC_ResJob Task 
        {
            get { return task; }
            set
            {
                task = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("Task"));
                }
            }
        }

        private ASC_Order order;
        public ASC_Order Order
        {
            get { return order; }
            set
            {
                order = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("Order"));
                }
            }
        }

        private TaskStatus taskState;
        public TaskStatus TaskState 
        {
            get { return taskState; }
            set
            {
                taskState = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TaskState"));
                }
            }
        }

        private Int32 errorCode;
        public Int32 ErrorCode
        {
            get { return errorCode; }
            set
            {
                errorCode = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ErrorCode"));
                }
            }
        }

        private Int32 taskProcess;
        public Int32 TaskProcess
        {
            get { return taskProcess; }
            set
            {
                taskProcess = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TaskProcess"));
                }
            }
        }
    }

    public class InfoAGVResJob : InfoBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public InfoAGVResJob()
        {
        }

        public InfoAGVResJob(AGV_ResJob obj)
            : this()
        {
            this.ORDER_ID = obj.ORDER_ID == null ? StringHandle.DefaultString : obj.ORDER_ID;
            this.COMMAND_ID = obj.COMMAND_ID == null ? StringHandle.DefaultString : obj.COMMAND_ID;
            this.ID = obj.ID;
            this.MESSAGE_ID = obj.MESSAGE_ID == null ? StringHandle.DefaultString : obj.MESSAGE_ID;
            this.JOB_TYPE = obj.JOB_TYPE == null ? StringHandle.DefaultString : obj.JOB_TYPE;
            this.JOB_ID = obj.JOB_ID == null ? StringHandle.DefaultString : obj.JOB_ID;
            this.RESERVE = obj.RESERVE == null ? StringHandle.DefaultString : obj.RESERVE;
            this.JOB_LINK = obj.JOB_LINK == null ? StringHandle.DefaultString : obj.JOB_LINK;
            this.CHE_ID = obj.CHE_ID == null ? StringHandle.DefaultString : obj.CHE_ID;
            this.YARD_ID = obj.YARD_ID == null ? StringHandle.DefaultString : obj.YARD_ID;
            this.QUAY_ID = obj.QUAY_ID == null ? StringHandle.DefaultString : obj.QUAY_ID;
            this.PRIORITY = obj.PRIORITY;
            this.JOB_STATUS = obj.JOB_STATUS == null ? StringHandle.DefaultString : obj.JOB_STATUS;
            this.FROM_TRUCK_TYPE = obj.FROM_TRUCK_TYPE == null ? StringHandle.DefaultString : obj.FROM_TRUCK_TYPE;
            this.FROM_TRUCK_ID = obj.FROM_TRUCK_ID == null ? StringHandle.DefaultString : obj.FROM_TRUCK_ID;
            this.FROM_TRUCK_POS = obj.FROM_TRUCK_POS == null ? StringHandle.DefaultString : obj.FROM_TRUCK_POS;
            this.FROM_BLOCK = obj.FROM_BLOCK == null ? StringHandle.DefaultString : obj.FROM_BLOCK;
            this.FROM_BAY_TYPE = obj.FROM_BAY_TYPE == null ? StringHandle.DefaultString : obj.FROM_BAY_TYPE;
            this.FROM_BAY = obj.FROM_BAY == null ? StringHandle.DefaultString : obj.FROM_BAY;
            this.FROM_LANE = obj.FROM_LANE == null ? StringHandle.DefaultString : obj.FROM_LANE;
            this.FROM_TIER = obj.FROM_TIER == null ? StringHandle.DefaultString : obj.FROM_TIER;
            this.TO_TRUCK_ID = obj.TO_TRUCK_ID == null ? StringHandle.DefaultString : obj.TO_TRUCK_ID;
            this.TO_TRUCK_TYPE = obj.TO_TRUCK_TYPE == null ? StringHandle.DefaultString : obj.TO_TRUCK_TYPE;
            this.TO_TRUCK_POS = obj.TO_TRUCK_POS == null ? StringHandle.DefaultString : obj.TO_TRUCK_POS;
            this.TO_BLOCK = obj.TO_BLOCK == null ? StringHandle.DefaultString : obj.TO_BLOCK;
            this.TO_BAY_TYPE = obj.TO_BAY_TYPE == null ? StringHandle.DefaultString : obj.TO_BAY_TYPE;
            this.TO_BAY = obj.TO_BAY == null ? StringHandle.DefaultString : obj.TO_BAY;
            this.TO_LANE = obj.TO_LANE == null ? StringHandle.DefaultString : obj.TO_LANE;
            this.TO_TIER = obj.TO_TIER == null ? StringHandle.DefaultString : obj.TO_TIER;
            this.CONTAINER_ID = obj.CONTAINER_ID == null ? StringHandle.DefaultString : obj.CONTAINER_ID;
            this.CONTAINER_ISO = obj.CONTAINER_ISO == null ? StringHandle.DefaultString : obj.CONTAINER_ISO;
            this.CONTAINER_LENGTH = obj.CONTAINER_LENGTH == null ? StringHandle.DefaultString : obj.CONTAINER_LENGTH;
            this.CONTAINER_HEIGHT = obj.CONTAINER_HEIGHT == null ? StringHandle.DefaultString : obj.CONTAINER_HEIGHT;
            this.CONTAINER_WEIGHT = obj.CONTAINER_WEIGHT == null ? StringHandle.DefaultString : obj.CONTAINER_WEIGHT;
            this.CONTAINER_IS_EMPTY = obj.CONTAINER_IS_EMPTY == null ? StringHandle.DefaultString : obj.CONTAINER_IS_EMPTY;
            this.CONTAINER_DOOR_DIRECTION = obj.CONTAINER_DOOR_DIRECTION == null ? StringHandle.DefaultString : obj.CONTAINER_DOOR_DIRECTION;
            this.PLAN_START_TIME = obj.PLAN_START_TIME;
            this.PLAN_END_TIME = obj.PLAN_END_TIME;
            this.DATETIME = obj.DATETIME;
            this.VERSION = obj.VERSION;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(AGV_ResJob obj)
        {
            int iRet;
            if (this.id != obj.ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String orderID;
        public String ORDER_ID
        {
            get { return orderID; }
            set
            {
                orderID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ORDER_ID"));
                }
            }
        }

        private String commandID;
        public String COMMAND_ID
        {
            get { return commandID; }
            set
            {
                commandID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("COMMAND_ID"));
                }
            }
        }

        private Int64 id;
        public Int64 ID
        {
            get { return id; }
            set
            {
                id = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ID"));
                }
            }
        }

        private String messageID;
        public String MESSAGE_ID
        {
            get { return messageID; }
            set
            {
                messageID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("MESSAGE_ID"));
                }
            }
        }

        private String jobType;
        public String JOB_TYPE 
        {
            get { return jobType; }
            set
            {
                jobType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_TYPE"));
                }
            }
        }

        private String jobID;
        public String JOB_ID
        {
            get { return jobID; }
            set
            {
                jobID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_ID"));
                }
            }
        }

        private String reserve;
        public String RESERVE 
        {
            get { return reserve; }
            set
            {
                reserve = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("RESERVE"));
                }
            }
        }

        private String jobLink;
        public String JOB_LINK
        {
            get { return jobLink; }
            set
            {
                jobLink = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_LINK"));
                }
            }
        }

        private String cheID;
        public String CHE_ID
        {
            get { return cheID; }
            set
            {
                cheID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CHE_ID"));
                }
            }
        }

        private String yardID;
        public String YARD_ID 
        {
            get { return yardID; }
            set
            {
                yardID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("YARD_ID"));
                }
            }
        }

        private String quayID;
        public String QUAY_ID
        {
            get { return quayID; }
            set
            {
                quayID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QUAY_ID"));
                }
            }
        }

        private int priority; 
        public int PRIORITY
        {
            get { return priority; }
            set
            {
                priority = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PRIORITY"));
                }
            }
        }

        private String jobStatus;
        public String JOB_STATUS
        {
            get { return jobStatus; }
            set
            {
                jobStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_STATUS"));
                }
            }
        }

        private String fromTruckType;
        public String FROM_TRUCK_TYPE
        {
            get { return fromTruckType; }
            set
            {
                fromTruckType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_TYPE"));
                }
            }
        }

        private String fromTruckID;
        public String FROM_TRUCK_ID
        {
            get { return fromTruckID; }
            set
            {
                fromTruckID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_ID"));
                }
            }
        }

        private String fromTruckPos;
        public String FROM_TRUCK_POS 
        {
            get { return fromTruckPos; }
            set
            {
                fromTruckPos = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_POS"));
                }
            }
        }

        private String fromBlock;
        public String FROM_BLOCK 
        {
            get { return fromBlock; }
            set
            {
                fromBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BLOCK"));
                }
            }
        }

        private String fromBayType;
        public String FROM_BAY_TYPE 
        {
            get { return fromBayType; }
            set
            {
                fromBayType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BAY_TYPE"));
                }
            }
        }

        private String fromBay;
        public String FROM_BAY 
        {
            get { return fromBay; }
            set
            {
                fromBay = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BAY"));
                }
            }
        }

        private String fromLane;
        public String FROM_LANE 
        {
            get { return fromLane; }
            set
            {
                fromLane = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_LANE"));
                }
            }
        }

        private String fromTier;
        public String FROM_TIER 
        {
            get { return fromTier; }
            set
            {
                fromTier = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TIER"));
                }
            }
        }

        private String toTruckID;
        public String TO_TRUCK_ID
        {
            get { return toTruckID; }
            set
            {
                toTruckID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_ID"));
                }
            }
        }

        private String toTruckType;
        public String TO_TRUCK_TYPE 
        {
            get { return toTruckType; }
            set
            {
                toTruckType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_TYPE"));
                }
            }
        }

        private String toTruckPos;
        public String TO_TRUCK_POS
        {
            get { return toTruckPos; }
            set
            {
                toTruckPos = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_POS"));
                }
            }
        }

        private String toBlock;
        public String TO_BLOCK 
        {
            get { return toBlock; }
            set
            {
                toBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BLOCK"));
                }
            }
        }

        private String toBayType;
        public String TO_BAY_TYPE 
        {
            get { return toBayType; }
            set
            {
                toBayType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BAY_TYPE"));
                }
            }
        }

        private String toBay;
        public String TO_BAY 
        {
            get { return toBay; }
            set
            {
                toBay = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BAY"));
                }
            }
        }

        private String toLane;
        public String TO_LANE 
        {
            get { return toLane; }
            set
            {
                toLane = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_LANE"));
                }
            }
        }

        private String toTier;
        public String TO_TIER 
        {
            get { return toTier; }
            set
            {
                toTier = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TIER"));
                }
            }
        }

        private String containerID;
        public String CONTAINER_ID 
        {
            get { return containerID; }
            set
            {
                containerID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ID"));
                }
            }
        }

        private String containerISO;
        public String CONTAINER_ISO 
        {
            get { return containerISO; }
            set
            {
                containerISO = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ISO"));
                }
            }
        }

        private String containerLength;
        public String CONTAINER_LENGTH 
        {
            get { return containerLength; }
            set
            {
                containerLength = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_LENGTH"));
                }
            }
        }

        private String containerHeight;
        public String CONTAINER_HEIGHT 
        {
            get { return containerHeight; }
            set
            {
                containerHeight = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_HEIGHT"));
                }
            }
        }

        private String containerWeight;
        public String CONTAINER_WEIGHT 
        {
            get { return containerWeight; }
            set
            {
                containerWeight = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_WEIGHT"));
                }
            }
        }

        private String containerIsEmpty;
        public String CONTAINER_IS_EMPTY 
        {
            get { return containerIsEmpty; }
            set
            {
                containerIsEmpty = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_IS_EMPTY"));
                }
            }
        }

        private String containerDoorDirection;
        public String CONTAINER_DOOR_DIRECTION 
        {
            get { return containerDoorDirection; }
            set
            {
                containerDoorDirection = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_DOOR_DIRECTION"));
                }
            }
        }

        private DateTime planStartTime;
        public DateTime PLAN_START_TIME
        {
            get { return planStartTime; }
            set
            {
                planStartTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLAN_START_TIME"));
                }
            }
        }

        private DateTime planEndTime;
        public DateTime PLAN_END_TIME 
        {
            get { return planEndTime; }
            set
            {
                planEndTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLAN_END_TIME"));
                }
            }
        }

        private DateTime datetime;
        public DateTime DATETIME 
        {
            get { return datetime; }
            set
            {
                datetime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("DATETIME"));
                }
            }
        }

        private int version;
        public int VERSION 
        {
            get { return version; }
            set
            {
                version = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VERSION"));
                }
            }
        }
    }

    public class InfoAGVTask : InfoBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public InfoAGVTask()
        {
        }

        public InfoAGVTask(AGV_Task obj)
            : this()
        {
            this.ID = obj.ID;
            this.Task = obj.Task == null ? new AGV_ResJob { ID = -1 } : obj.Task;
            this.Order = obj.Order == null ? new AGV_Order { ORDER_ID = StringHandle.DefaultString } : obj.Order;
            this.TaskState = obj.TaskState;
            this.ErrorCode = obj.ErrorCode; 
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(AGV_Task obj)
        {
            int iRet;
            if (this.id != obj.ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private Int64 id;
        public Int64 ID 
        {
            get { return id; }
            set
            {
                id = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ID"));
                }
            }
        }

        private AGV_ResJob task;
        public AGV_ResJob Task 
        {
            get { return task; }
            set
            {
                task = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TASK"));
                }
            }
        }

        private AGV_Order order;
        public AGV_Order Order
        {
            get { return order; }
            set
            {
                order = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("Order"));
                }
            }
        }

        private TaskStatus taskState;
        public TaskStatus TaskState
        {
            get { return taskState; }
            set
            {
                taskState = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TaskState"));
                }
            }
        }

        private int errorCode;
        public int ErrorCode 
        {
            get { return errorCode; }
            set
            {
                errorCode = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ErrorCode"));
                }
            }
        } 
    }

    public class InfoOrderCommandBase : InfoBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public InfoOrderCommandBase()
        {
        }

        public InfoOrderCommandBase(Order_Command_Base obj)
            : this()
        {
            this.ORDER_ID = obj.ORDER_ID == null ? StringHandle.DefaultString : obj.ORDER_ID;
            this.COMMAND_ID = obj.COMMAND_ID == null ? StringHandle.DefaultString : obj.COMMAND_ID;
            this.ORDER_VERSION = obj.ORDER_VERSION == null ? StringHandle.DefaultString : obj.ORDER_VERSION;
            this.COMMAND_VERSION = obj.COMMAND_VERSION == null ? StringHandle.DefaultString : obj.COMMAND_VERSION;

            this.VERSION = obj.VERSION;

            this.JOB_TYPE = obj.JOB_TYPE == null ? StringHandle.DefaultString : obj.JOB_TYPE;
            this.JOB_ID = obj.JOB_ID == null ? StringHandle.DefaultString : obj.JOB_ID;
            this.CHE_ID = obj.CHE_ID == null ? StringHandle.DefaultString : obj.CHE_ID;

            this.FROM_TRUCK_TYPE = obj.FROM_TRUCK_TYPE == null ? StringHandle.DefaultString : obj.FROM_TRUCK_TYPE;
            this.FROM_TRUCK_ID = obj.FROM_TRUCK_ID == null ? StringHandle.DefaultString : obj.FROM_TRUCK_ID;
            this.FROM_TRUCK_POS = obj.FROM_TRUCK_POS == null ? StringHandle.DefaultString : obj.FROM_TRUCK_POS;
            this.FROM_RFID = obj.FROM_RFID == null ? StringHandle.DefaultString : obj.FROM_RFID;
            this.FROM_BAY_TYPE = obj.FROM_BAY_TYPE == null ? StringHandle.DefaultString : obj.FROM_BAY_TYPE;
            this.FROM_BAY = obj.FROM_BAY == null ? StringHandle.DefaultString : obj.FROM_BAY;
            this.FROM_LANE = obj.FROM_LANE == null ? StringHandle.DefaultString : obj.FROM_LANE;
            this.FROM_TIER = obj.FROM_TIER == null ? StringHandle.DefaultString : obj.FROM_TIER;

            this.TO_TRUCK_TYPE = obj.TO_TRUCK_TYPE == null ? StringHandle.DefaultString : obj.TO_TRUCK_TYPE;
            this.TO_TRUCK_ID = obj.TO_TRUCK_ID == null ? StringHandle.DefaultString : obj.TO_TRUCK_ID;
            this.TO_TRUCK_POS = obj.TO_TRUCK_POS == null ? StringHandle.DefaultString : obj.TO_TRUCK_POS;
            this.TO_RFID = obj.TO_RFID == null ? StringHandle.DefaultString : obj.TO_RFID;
            this.TO_BAY_TYPE = obj.TO_BAY_TYPE == null ? StringHandle.DefaultString : obj.TO_BAY_TYPE;
            this.TO_BAY = obj.TO_BAY == null ? StringHandle.DefaultString : obj.TO_BAY;
            this.TO_LANE = obj.TO_LANE == null ? StringHandle.DefaultString : obj.TO_LANE;
            this.TO_TIER = obj.TO_TIER == null ? StringHandle.DefaultString : obj.TO_TIER;

            this.CONTAINER_ID = obj.CONTAINER_ID == null ? StringHandle.DefaultString : obj.CONTAINER_ID;
            this.CONTAINER_ISO = obj.CONTAINER_ISO == null ? StringHandle.DefaultString : obj.CONTAINER_ISO;
            this.CONTAINER_LENGTH = obj.CONTAINER_LENGTH == null ? StringHandle.DefaultString : obj.CONTAINER_LENGTH;
            this.CONTAINER_HEIGHT = obj.CONTAINER_HEIGHT == null ? StringHandle.DefaultString : obj.CONTAINER_HEIGHT;
            this.CONTAINER_WEIGHT = obj.CONTAINER_WEIGHT == null ? StringHandle.DefaultString : obj.CONTAINER_WEIGHT;
            this.CONTAINER_IS_EMPTY = obj.CONTAINER_IS_EMPTY == null ? StringHandle.DefaultString : obj.CONTAINER_IS_EMPTY;
            this.CONTAINER_DOOR_DIRECTION = obj.CONTAINER_DOOR_DIRECTION == null ? StringHandle.DefaultString : obj.CONTAINER_DOOR_DIRECTION;

            this.DATETIME = obj.DATETIME;
        }

        private String orderID;
        public String ORDER_ID 
        {
            get { return orderID; }
            set
            {
                orderID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ORDER_ID"));
                }
            }
        }

        private String commandID;
        public String COMMAND_ID 
        {
            get { return commandID; }
            set
            {
                commandID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("COMMAND_ID"));
                }
            }
        }

        private String orderVersion;
        public String ORDER_VERSION 
        {
            get { return orderVersion; }
            set
            {
                orderVersion = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ORDER_VERSION"));
                }
            }
        }

        private String commandVersion;
        public String COMMAND_VERSION
        {
            get { return commandVersion; }
            set
            {
                commandVersion = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("COMMAND_VERSION"));
                }
            }
        }

        private int version;
        public int VERSION
        {
            get { return version; }
            set
            {
                version = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VERSION"));
                }
            }
        }

        private String jobType;
        public String JOB_TYPE 
        {
            get { return jobType; }
            set
            {
                jobType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_TYPE"));
                }
            }
        }

        private String jobID;
        public String JOB_ID 
        {
            get { return jobID; }
            set
            {
                jobID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_ID"));
                }
            }
        }

        private String jobLink;
        public String JOB_LINK 
        {
            get { return jobLink; }
            set
            {
                jobLink = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_LINK"));
                }
            }
        }

        private String cheID;
        public String CHE_ID 
        {
            get { return cheID; }
            set
            {
                cheID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CHE_ID"));
                }
            }
        }

        private String fromTruckType;
        public String FROM_TRUCK_TYPE 
        {
            get { return fromTruckType; }
            set
            {
                fromTruckType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_TYPE"));
                }
            }
        }

        private String fromTruckID;
        public String FROM_TRUCK_ID
        {
            get { return fromTruckID; }
            set
            {
                fromTruckID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_ID"));
                }
            }
        }

        private String fromTruckPos;
        public String FROM_TRUCK_POS 
        {
            get { return fromTruckPos; }
            set
            {
                fromTruckPos = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_POS"));
                }
            }
        }

        private String fromRFID;
        public String FROM_RFID 
        {
            get { return fromRFID; }
            set
            {
                fromRFID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_RFID"));
                }
            }
        }

        private String fromBayType;
        public String FROM_BAY_TYPE
        {
            get { return fromBayType; }
            set
            {
                fromBayType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BAY_TYPE"));
                }
            }
        }

        private String fromBay;
        public String FROM_BAY 
        {
            get { return fromBay; }
            set
            {
                fromBay = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BAY"));
                }
            }
        }

        private String fromLane;
        public String FROM_LANE 
        {
            get { return fromLane; }
            set
            {
                fromLane = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_LANE"));
                }
            }
        }

        private String fromTier;
        public String FROM_TIER 
        {
            get { return fromTier; }
            set
            {
                fromTier = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TIER"));
                }
            }
        }

        private String toTruckType;
        public String TO_TRUCK_TYPE 
        {
            get { return toTruckType; }
            set
            {
                toTruckType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_TYPE"));
                }
            }
        }

        private String toTruckID;
        public String TO_TRUCK_ID 
        {
            get { return toTruckID; }
            set
            {
                toTruckID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_ID"));
                }
            }
        }

        private String toTruckPos;
        public String TO_TRUCK_POS
        {
            get { return toTruckPos; }
            set
            {
                toTruckPos = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_POS"));
                }
            }
        }

        private String toRFID;
        public String TO_RFID 
        {
            get { return toRFID; }
            set
            {
                toRFID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_RFID"));
                }
            }
        }

        private String toBayType;
        public String TO_BAY_TYPE 
        {
            get { return toBayType; }
            set
            {
                toBayType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BAY_TYPE"));
                }
            }
        }

        private String toBay;
        public String TO_BAY 
        {
            get { return toBay; }
            set
            {
                toBay = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BAY"));
                }
            }
        }

        private String toLane;
        public String TO_LANE 
        {
            get { return toLane; }
            set
            {
                toLane = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_LANE"));
                }
            }
        }

        private String toTier;
        public String TO_TIER 
        {
            get { return toTier; }
            set
            {
                toTier = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TIER"));
                }
            }
        }

        private String containerID;
        public String CONTAINER_ID 
        {
            get { return containerID; }
            set
            {
                containerID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ID"));
                }
            }
        }

        private String containerISO;
        public String CONTAINER_ISO 
        {
            get { return containerISO; }
            set
            {
                containerISO = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ISO"));
                }
            }
        }

        private String containerLength;
        public String CONTAINER_LENGTH
        {
            get { return containerLength; }
            set
            {
                containerLength = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_LENGTH"));
                }
            }
        }

        private String containerHeight;
        public String CONTAINER_HEIGHT 
        {
            get { return containerHeight; }
            set
            {
                containerHeight = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_HEIGHT"));
                }
            }
        }

        private String containerWeight;
        public String CONTAINER_WEIGHT
        {
            get { return containerWeight; }
            set
            {
                containerWeight = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_WEIGHT"));
                }
            }
        }

        private String containerIsEmpty;
        public String CONTAINER_IS_EMPTY
        {
            get { return containerIsEmpty; }
            set
            {
                containerIsEmpty = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_IS_EMPTY"));
                }
            }
        }

        private String containerDoorDirection;
        public String CONTAINER_DOOR_DIRECTION
        {
            get { return containerDoorDirection; }
            set
            {
                containerDoorDirection = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_DOOR_DIRECTION"));
                }
            }
        }

        private DateTime datetime;
        public DateTime DATETIME 
        {
            get { return datetime; }
            set
            {
                datetime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("DATETIME"));
                }
            }
        }

    }

    public class InfoOrderBase : InfoOrderCommandBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoOrderBase()
            : base()
        {
        }

        public InfoOrderBase(Order_Base obj)
            : base(obj)
        {
            this.JOB_STATUS = obj.JOB_STATUS == null ? StringHandle.DefaultString : obj.JOB_STATUS;
            this.PRIORITY = obj.PRIORITY;
            this.PLAN_START_TIME = obj.PLAN_START_TIME;
            this.PLAN_END_TIME = obj.PLAN_END_TIME;
        }

        private String jobStatus;
        public String JOB_STATUS 
        {
            get { return jobStatus; }
            set
            {
                jobStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_STATUS"));
                }
            }
        }

        private int priority;
        public int PRIORITY 
        {
            get { return priority; }
            set
            {
                priority = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PRIORITY"));
                }
            }
        }

        private DateTime planStartTime;
        public DateTime PLAN_START_TIME 
        {
            get { return planStartTime; }
            set
            {
                planStartTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLAN_START_TIME"));
                }
            }
        }

        private DateTime planEndTime;
        public DateTime PLAN_END_TIME 
        {
            get { return planEndTime; }
            set
            {
                planEndTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLAN_END_TIME"));
                }
            }
        }
    }

    public class InfoSTSOrder : InfoOrderBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoSTSOrder()
            : base()
        {
        }

        public InfoSTSOrder(STS_Order obj)
            : base(obj)
        {
            this.VESSEL_ID = obj.VESSEL_ID == null ? StringHandle.DefaultString : obj.VESSEL_ID;
            this.QUAY_ID = obj.QUAY_ID == null ? StringHandle.DefaultString : obj.QUAY_ID;
            this.MOVE_NO = obj.MOVE_NO == null ? StringHandle.DefaultString : obj.MOVE_NO;
            this.PLATFORM_CONFIRM = obj.PLATFORM_CONFIRM == null ? StringHandle.DefaultString : obj.PLATFORM_CONFIRM;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(STS_Order obj)
        {
            int iRet;
            if (this.ORDER_ID != obj.ORDER_ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String vesselID;
        public String VESSEL_ID
        {
            get { return vesselID; }
            set
            {
                vesselID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VESSEL_ID"));
                }
            }
        }

        private String quayID;
        public String QUAY_ID
        {
            get { return quayID; }
            set
            {
                quayID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QUAY_ID"));
                }
            }
        }

        private String moveNo;
        public String MOVE_NO
        {
            get { return moveNo; }
            set
            {
                moveNo = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("MOVE_NO"));
                }
            }
        }

        private String platformConfirm;
        public virtual string PLATFORM_CONFIRM
        {
            get { return platformConfirm; }
            set
            {
                platformConfirm = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PLATFORM_CONFIRM"));
                }
            }
        }
    }

    public class InfoASCOrder : InfoOrderBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoASCOrder()
            : base()
        {
        }

        public InfoASCOrder(ASC_Order obj)
            : base(obj)
        {
            this.YARD_ID = obj.YARD_ID == null ? StringHandle.DefaultString : obj.YARD_ID;
            this.OPERATOR_ID = obj.OPERATOR_ID == null ? StringHandle.DefaultString : obj.OPERATOR_ID;

            this.FROM_BLOCK = obj.FROM_BLOCK == null ? StringHandle.DefaultString : obj.FROM_BLOCK;
            this.FROM_PALLET_TYPE = obj.FROM_PALLET_TYPE == null ? StringHandle.DefaultString : obj.FROM_PALLET_TYPE;
            this.FROM_TRUCK_STATUS = obj.FROM_TRUCK_STATUS == null ? StringHandle.DefaultString : obj.FROM_TRUCK_STATUS;
            this.TO_BLOCK = obj.TO_BLOCK == null ? StringHandle.DefaultString : obj.TO_BLOCK;
            this.TO_PALLET_TYPE = obj.TO_PALLET_TYPE == null ? StringHandle.DefaultString : obj.TO_PALLET_TYPE;
            this.TO_TRUCK_STATUS = obj.TO_TRUCK_STATUS == null ? StringHandle.DefaultString : obj.TO_TRUCK_STATUS;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(ASC_Order obj)
        {
            int iRet;
            if (this.ORDER_ID != obj.ORDER_ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String yardID;
        public String YARD_ID 
        {
            get { return yardID; }
            set
            {
                yardID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("YARD_ID"));
                }
            }
        }

        private String operatorID;
        public String OPERATOR_ID
        {
            get { return operatorID; }
            set
            {
                operatorID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("OPERATOR_ID"));
                }
            }
        }

        private String fromBlock;
        public String FROM_BLOCK
        {
            get { return fromBlock; }
            set
            {
                fromBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BLOCK"));
                }
            }
        }

        private String fromPalletType;
        public String FROM_PALLET_TYPE
        {
            get { return fromPalletType; }
            set
            {
                fromPalletType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_PALLET_TYPE"));
                }
            }
        }

        private String fromTruckStatus;
        public String FROM_TRUCK_STATUS 
        {
            get { return fromTruckStatus; }
            set
            {
                fromTruckStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_STATUS"));
                }
            }
        }

        private String toBlock;
        public String TO_BLOCK
        {
            get { return toBlock; }
            set
            {
                toBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BLOCK"));
                }
            }
        }

        private String toPalletType;
        public String TO_PALLET_TYPE
        {
            get { return toPalletType; }
            set
            {
                toPalletType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_PALLET_TYPE"));
                }
            }
        }

        private String toTruckStatus;
        public String TO_TRUCK_STATUS
        {
            get { return toTruckStatus; }
            set
            {
                toTruckStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_STATUS"));
                }
            }
        }

    }

    public class InfoAGVOrder : InfoOrderBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoAGVOrder()
            : base()
        {
        }

        public InfoAGVOrder(AGV_Order obj)
            : base(obj)
        {
            this.YARD_ID = obj.YARD_ID == null ? StringHandle.DefaultString : obj.YARD_ID;
            this.QUAY_ID = obj.QUAY_ID == null ? StringHandle.DefaultString : obj.QUAY_ID;
            this.FROM_BLOCK = obj.FROM_BLOCK == null ? StringHandle.DefaultString : obj.FROM_BLOCK;
            this.TO_BLOCK = obj.TO_BLOCK == null ? StringHandle.DefaultString : obj.TO_BLOCK;
            this.QC_REFID = obj.QC_REFID == null ? StringHandle.DefaultString : obj.QC_REFID;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(AGV_Order obj)
        {
            int iRet;
            if (this.ORDER_ID != obj.ORDER_ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String yardID;
        public String YARD_ID 
        {
            get { return yardID; }
            set
            {
                yardID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("YARD_ID"));
                }
            }
        }

        private String quayID;
        public String QUAY_ID 
        {
            get { return quayID; }
            set
            {
                quayID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QUAY_ID"));
                }
            }
        }

        private String fromBlock;
        public String FROM_BLOCK
        {
            get { return fromBlock; }
            set
            {
                fromBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BLOCK"));
                }
            }
        }

        private String toBlock;
        public String TO_BLOCK 
        {
            get { return toBlock; }
            set
            {
                toBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BLOCK"));
                }
            }
        }

        private String qcREFID;
        public String QC_REFID
        {
            get { return qcREFID; }
            set
            {
                qcREFID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QC_REFID"));
                }
            }
        }

    }

    public class InfoCommandBase : InfoOrderCommandBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoCommandBase()
            : base()
        {
        }

        public InfoCommandBase(Command_Base obj)
            : base(obj)
        {
            this.JOB_STATUS = obj.JOB_STATUS == null ? StringHandle.DefaultString : obj.JOB_STATUS;
            this.EXCEPTION_CODE = obj.EXCEPTION_CODE == null ? StringHandle.DefaultString : obj.EXCEPTION_CODE;
            this.START_TIME = obj.START_TIME;
            this.END_TIME = obj.END_TIME;
        }

        private String jobStatus;
        public String JOB_STATUS 
        {
            get { return jobStatus; }
            set
            {
                jobStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("JOB_STATUS"));
                }
            }
        }

        private String exceptionCode;
        public String EXCEPTION_CODE
        {
            get { return exceptionCode; }
            set
            {
                exceptionCode = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("EXCEPTION_CODE"));
                }
            }
        }

        private DateTime startTime;
        public DateTime START_TIME 
        {
            get { return startTime; }
            set
            {
                startTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("START_TIME"));
                }
            }
        }

        private DateTime endTime;
        public DateTime END_TIME
        {
            get { return endTime; }
            set
            {
                endTime = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("END_TIME"));
                }
            }
        }
    }

    public class InfoSTSCommand : InfoCommandBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoSTSCommand()
            : base()
        {
        }

        public InfoSTSCommand(STS_Command obj)
            : base(obj)
        {
            this.VESSEL_ID = obj.VESSEL_ID == null ? StringHandle.DefaultString : obj.VESSEL_ID;
            this.QUAY_ID = obj.QUAY_ID == null ? StringHandle.DefaultString : obj.QUAY_ID;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(STS_Command obj)
        {
            int iRet;
            if (this.COMMAND_ID != obj.COMMAND_ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String vesselID;
        public String VESSEL_ID
        {
            get { return vesselID; }
            set
            {
                vesselID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VESSEL_ID"));
                }
            }
        }

        private String quayID;
        public String QUAY_ID 
        {
            get { return quayID; }
            set
            {
                quayID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QUAY_ID"));
                }
            }
        }
    }

    public class InfoASCCommand : InfoCommandBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoASCCommand()
            : base()
        {
        }

        public InfoASCCommand(ASC_Command obj)
            : base(obj)
        {
            this.YARD_ID = obj.YARD_ID == null ? StringHandle.DefaultString : obj.YARD_ID;
            this.OPERATOR_ID = obj.OPERATOR_ID == null ? StringHandle.DefaultString : obj.OPERATOR_ID;
            this.FROM_BLOCK = obj.FROM_BLOCK == null ? StringHandle.DefaultString : obj.FROM_BLOCK;
            this.FROM_PALLET_TYPE = obj.FROM_PALLET_TYPE == null ? StringHandle.DefaultString : obj.FROM_PALLET_TYPE;
            this.FROM_TRUCK_STATUS = obj.FROM_TRUCK_STATUS == null ? StringHandle.DefaultString : obj.FROM_TRUCK_STATUS;
            this.TO_BLOCK = obj.TO_BLOCK == null ? StringHandle.DefaultString : obj.TO_BLOCK;
            this.TO_PALLET_TYPE = obj.TO_PALLET_TYPE == null ? StringHandle.DefaultString : obj.TO_PALLET_TYPE;
            this.TO_TRUCK_STATUS = obj.TO_TRUCK_POS == null ? StringHandle.DefaultString : obj.TO_TRUCK_POS;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(ASC_Command obj)
        {
            int iRet;
            if (this.COMMAND_ID != obj.COMMAND_ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String yardID;
        public String YARD_ID 
        {
            get { return yardID; }
            set
            {
                yardID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("YARD_ID"));
                }
            }
        }

        private String operatorID;
        public String OPERATOR_ID
        {
            get { return operatorID; }
            set
            {
                operatorID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("OPERATOR_ID"));
                }
            }
        }

        private String fromBlock;
        public String FROM_BLOCK
        {
            get { return fromBlock; }
            set
            {
                fromBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BLOCK"));
                }
            }
        }

        private String fromPalletType;
        public String FROM_PALLET_TYPE
        {
            get { return fromPalletType; }
            set
            {
                fromPalletType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_PALLET_TYPE"));
                }
            }
        }

        private String fromTruckStatus;
        public String FROM_TRUCK_STATUS 
        {
            get { return fromTruckStatus; }
            set
            {
                fromTruckStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_TRUCK_STATUS"));
                }
            }
        }

        private String toBlock; 
        public String TO_BLOCK 
        {
            get { return toBlock; }
            set
            {
                toBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BLOCK"));
                }
            }
        }

        private String toPalletType; 
        public String TO_PALLET_TYPE 
        {
            get { return toPalletType; }
            set
            {
                toPalletType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_PALLET_TYPE"));
                }
            }
        }

        private String toTruckStatus;
        public String TO_TRUCK_STATUS
        {
            get { return toTruckStatus; }
            set
            {
                toTruckStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_TRUCK_STATUS"));
                }
            }
        }
    }

    public class InfoAGVCommand : InfoCommandBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoAGVCommand()
            : base()
        {
        }

        public InfoAGVCommand(AGV_Command obj)
            : base(obj)
        {
            this.QUAY_ID = obj.QUAY_ID == null ? StringHandle.DefaultString : obj.QUAY_ID;
            this.YARD_ID = obj.YARD_ID == null ? StringHandle.DefaultString : obj.YARD_ID;
            this.FROM_BLOCK = obj.FROM_BLOCK == null ? StringHandle.DefaultString : obj.FROM_BLOCK;
            this.TO_BLOCK = obj.TO_BLOCK == null ? StringHandle.DefaultString : obj.TO_BLOCK;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(AGV_Command obj)
        {
            int iRet;
            if (this.COMMAND_ID != obj.COMMAND_ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String quayID;
        public String QUAY_ID 
        {
            get { return quayID; }
            set
            {
                quayID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QUAY_ID"));
                }
            }
        }

        private String yardID;
        public String YARD_ID
        {
            get { return yardID; }
            set
            {
                yardID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("YARD_ID"));
                }
            }
        }

        private String fromBlock;
        public String FROM_BLOCK
        {
            get { return fromBlock; }
            set
            {
                fromBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FROM_BLOCK"));
                }
            }
        }

        private String toBlock;
        public String TO_BLOCK
        {
            get { return toBlock; }
            set
            {
                toBlock = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TO_BLOCK"));
                }
            }
        }
    }

    public class InfoStatusBase : InfoBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public InfoStatusBase()
        {
        }

        public InfoStatusBase(CHESTATUS_BASE obj)
            : this()
        {
            this.TECHNICAL_STATUS = obj.TECHNICAL_STATUS;
            this.UPDATED = obj.UPDATED;
        }

        private Technical_Status technicalStatus;
        public Technical_Status TECHNICAL_STATUS
        {
            get { return technicalStatus; }
            set
            {
                technicalStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TECHNICAL_STATUS"));
                }
            }
        }

        private DateTime updated;
        public DateTime UPDATED
        {
            get { return updated; }
            set
            {
                updated = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("UPDATED"));
                }
            }
        }
    }

    public class InfoSTSStatus : InfoStatusBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoSTSStatus()
            : base()
        {
        }

        public InfoSTSStatus(STS_STATUS obj)
            : base(obj)
        {
            this.QC_ID = obj.QC_ID == null ? StringHandle.DefaultString : obj.QC_ID;
            this.QC_BOLLARD = obj.QC_BOLLARD == null ? StringHandle.DefaultString : obj.QC_BOLLARD;
            this.QC_BOLLARD_OFFSET_CM = obj.QC_BOLLARD_OFFSET_CM;
            this.nAGVCountMax = obj.nAGVCountMax;
            this.nAGVCountMin = obj.nAGVCountMin;
            this.nQCPosition = obj.nQCPosition;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(STS_STATUS obj)
        {
            int iRet;
            if (this.qcid != obj.QC_ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String qcid;
        public String QC_ID
        {
            get { return qcid; }
            set
            {
                qcid = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QC_ID"));
                }
            }
        }

        private String qcBollard;
        public String QC_BOLLARD
        {
            get { return qcBollard; }
            set
            {
                qcBollard = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QC_BOLLARD"));
                }
            }
        }

        private int qcBollardOffsetCM;
        public int QC_BOLLARD_OFFSET_CM
        {
            get { return qcBollardOffsetCM; }
            set
            {
                qcBollardOffsetCM = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("QC_BOLLARD_OFFSET_CM"));
                }
            }
        }

        private UInt32 nAgvCountMax;
        public UInt32 nAGVCountMax
        {
            get { return nAgvCountMax; }
            set
            {
                nAgvCountMax = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("nAGVCountMax"));
                }
            }
        }

        private UInt32 nAgvCountMin;
        public UInt32 nAGVCountMin 
        {
            get { return nAgvCountMin; }
            set
            {
                nAgvCountMin = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("nAGVCountMin"));
                }
            }
        }

        private Int32 nQcPosition;
        public Int32 nQCPosition
        {
            get { return nQcPosition; }
            set
            {
                nQcPosition = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("nQCPosition"));
                }
            }
        }
    }

    public class InfoASCStatus : InfoStatusBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoASCStatus()
            : base()
        {
        }

        public InfoASCStatus(ASC_STATUS obj)
            : base(obj)
        {
            this.CHE_ID = obj.CHE_ID == null ? StringHandle.DefaultString : obj.CHE_ID;
            this.WORK_STATUS = obj.WORK_STATUS;
            this.OPERATIONAL_STATUS = obj.OPERATIONAL_STATUS;
            this.TECHNICAL_DETAILS = obj.TECHNICAL_DETAILS == null ? StringHandle.DefaultString : obj.TECHNICAL_DETAILS;
            this.ORDER_GKEY = obj.ORDER_GKEY;
            this.COMMAND_GKEY = obj.COMMAND_GKEY;
            this.LOCATION = obj.LOCATION == null ? StringHandle.DefaultString : obj.LOCATION;
            this.CONTAINER_ID = obj.CONTAINER_ID == null ? StringHandle.DefaultString : obj.CONTAINER_ID;
            this.HELP_MODE = obj.HELP_MODE == null ? StringHandle.DefaultString : obj.HELP_MODE;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(ASC_STATUS obj)
        {
            int iRet;
            if (this.cheID != obj.CHE_ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String cheID;
        public String CHE_ID
        {
            get { return cheID; }
            set
            {
                cheID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CHE_ID"));
                }
            }
        }

        private Work_Status workStatus;
        public Work_Status WORK_STATUS
        {
            get { return workStatus; }
            set
            {
                workStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("WORK_STATUS"));
                }
            }
        }

        private Operational_Status operationalStatus;
        public Operational_Status OPERATIONAL_STATUS
        {
            get { return operationalStatus; }
            set
            {
                operationalStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("OPERATIONAL_STATUS"));
                }
            }
        }

        private String technicalDetails;
        public String TECHNICAL_DETAILS
        {
            get { return technicalDetails; }
            set
            {
                technicalDetails = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TECHNICAL_DETAILS"));
                }
            }
        }

        private long orderGKey;
        public long ORDER_GKEY
        {
            get { return orderGKey; }
            set
            {
                orderGKey = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ORDER_GKEY"));
                }
            }
        }

        private long commandGKey;
        public long COMMAND_GKEY
        {
            get { return commandGKey; }
            set
            {
                commandGKey = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("COMMAND_GKEY"));
                }
            }
        }

        private String location;
        public String LOCATION 
        {
            get { return location; }
            set
            {
                location = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("LOCATION"));
                }
            }
        }

        private String containerID;
        public String CONTAINER_ID
        {
            get { return containerID; }
            set
            {
                containerID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ID"));
                }
            }
        }

        private String helpMode;
        public String HELP_MODE
        {
            get { return helpMode; }
            set
            {
                helpMode = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("HELP_MODE"));
                }
            }
        }
    }

    public class InfoAGVStatus : InfoStatusBase, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoAGVStatus()
            : base()
        {
        }

        public InfoAGVStatus(AGV_STATUS obj)
            : base(obj)
        {
            this.CHE_ID = obj.CHE_ID == null ? StringHandle.DefaultString : obj.CHE_ID;
            this.OPERATIONAL_STATUS = obj.OPERATIONAL_STATUS;
            this.TECHNICAL_DETAILS = obj.TECHNICAL_DETAILS == null ? StringHandle.DefaultString : obj.TECHNICAL_DETAILS;
            this.LIFT_CAPABILITY = obj.LIFT_CAPABILITY == null ? StringHandle.DefaultString : obj.LIFT_CAPABILITY;
            this.STARTUP_DELAY = obj.STARTUP_DELAY;
            this.ORDER_GKEY = obj.ORDER_GKEY;
            this.COMMAND_GKEY = obj.COMMAND_GKEY;
            this.LOCATION = obj.LOCATION == null ? StringHandle.DefaultString : obj.LOCATION;
            this.NEXT_LOCATION = obj.NEXT_LOCATION == null ? StringHandle.DefaultString : obj.NEXT_LOCATION;
            this.LOCATION_X = obj.LOCATION_X;
            this.LOCATION_Y = obj.LOCATION_Y;
            this.ORIENTATION = obj.ORIENTATION;
            this.REFERENCE_ID_1 = obj.REFERENCE_ID_1 == null ? StringHandle.DefaultString : obj.REFERENCE_ID_1;
            this.REFERENCE_ID_2 = obj.REFERENCE_ID_2 == null ? StringHandle.DefaultString : obj.REFERENCE_ID_2;
            this.CONTAINER_ID_1 = obj.CONTAINER_ID_1 == null ? StringHandle.DefaultString : obj.CONTAINER_ID_1;
            this.CONTAINER_ID_2 = obj.CONTAINER_ID_2 == null ? StringHandle.DefaultString : obj.CONTAINER_ID_2;
            this.FUEL_TYPE = obj.FUEL_TYPE;
            this.BATTERY_STATE = obj.BATTERY_STATE;
            this.BATTERY_STATION_FACTORS = obj.BATTERY_STATION_FACTORS == null ? StringHandle.DefaultString : obj.BATTERY_STATION_FACTORS;
            this.REMAINING_FUEL = obj.REMAINING_FUEL;
            this.RUNNING_HOURS = obj.RUNNING_HOURS;
        }

        /// <summary>
        /// 参照目标更新
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>更新失败返回-1，否则返回更新属性值个数</returns>
        public int Update(AGV_STATUS obj)
        {
            int iRet;
            if (this.cheID != obj.CHE_ID)
                iRet = -1;
            else
                iRet = this.RenewChangedProperties(obj);
            return iRet;
        }

        private String cheID;
        public String CHE_ID
        {
            get { return cheID; }
            set
            {
                cheID = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CHE_ID"));
                }
            }
        }

        private Operational_Status operationalStatus;
        public Operational_Status OPERATIONAL_STATUS
        {
            get { return operationalStatus; }
            set
            {
                operationalStatus = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("OPERATIONAL_STATUS"));
                }
            }
        }

        private String technicalDetails;
        public String TECHNICAL_DETAILS
        {
            get { return technicalDetails; }
            set
            {
                technicalDetails = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("TECHNICAL_DETAILS"));
                }
            }
        }

        /// <summary>
        /// String(12) "YES": lift tables present and in working order; "OUT_OF_ORDER": lift tables present but out of order; "NOT PRESENT": s/e
        /// </summary>
        private String liftCapability;
        public String LIFT_CAPABILITY
        {
            get { return liftCapability; }
            set
            {
                liftCapability = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("LIFT_CAPABILITY"));
                }
            }
        }

        /// <summary>
        /// Time in seconds after which AGV can start performing an order
        /// </summary>
        private Int16 startupDelay;
        public Int16 STARTUP_DELAY 
        {
            get { return startupDelay; }
            set
            {
                startupDelay = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("STARTUP_DELAY"));
                }
            }
        }

        private long orderGKey;
        public long ORDER_GKEY
        {
            get { return orderGKey; }
            set
            {
                orderGKey = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ORDER_GKEY"));
                }
            }
        }

        private long commandGKey;
        public long COMMAND_GKEY 
        {
            get { return commandGKey; }
            set
            {
                commandGKey = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("COMMAND_GKEY"));
                }
            }
        }

        /// <summary>
        /// in mm
        /// </summary>
        private String location;
        public String LOCATION 
        {
            get { return location; }
            set
            {
                location = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("LOCATION"));
                }
            }
        }

        private String nextLocation;
        public String NEXT_LOCATION
        {
            get { return nextLocation; }
            set
            {
                nextLocation = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("NEXT_LOCATION"));
                }
            }
        }

        /// <summary>
        /// X coordinate of AGV location. Updated every 15 meters.
        /// </summary>
        private int locationX;
        public int LOCATION_X
        {
            get { return locationX; }
            set
            {
                locationX = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("LOCATION_X"));
                }
            }
        }

        private int locationY;
        public int LOCATION_Y 
        {
            get { return locationY; }
            set
            {
                locationY = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("LOCATION_Y"));
                }
            }
        }

        /// <summary>
        /// Current heading of AGV in degrees, 0--359
        /// </summary>
        private Int16 orientation;
        public Int16 ORIENTATION
        {
            get { return orientation; }
            set
            {
                orientation = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ORIENTATION"));
                }
            }
        }

        /// <summary>
        /// String(32)Reference ID generated by the QC system during container transfers
        /// </summary>
        private String referenceID1;
        public String REFERENCE_ID_1
        {
            get { return referenceID1; }
            set
            {
                referenceID1 = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("REFERENCE_ID_1"));
                }
            }
        }

        private String referenceID2;
        public String REFERENCE_ID_2 
        {
            get { return referenceID2; }
            set
            {
                referenceID2 = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("REFERENCE_ID_2"));
                }
            }
        }

        private String containerID1;
        public String CONTAINER_ID_1
        {
            get { return containerID1; }
            set
            {
                containerID1 = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ID_1"));
                }
            }
        }

        private String containerID2;
        public String CONTAINER_ID_2
        {
            get { return containerID2; }
            set
            {
                containerID2 = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CONTAINER_ID_2"));
                }
            }
        }

        private Fuel_Type fuelType;
        public Fuel_Type FUEL_TYPE
        {
            get { return fuelType; }
            set
            {
                fuelType = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FUEL_TYPE"));
                }
            }
        }

        private Battery_State batteryState;
        public Battery_State BATTERY_STATE
        {
            get { return batteryState; }
            set
            {
                batteryState = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("BATTERY_STATE"));
                }
            }
        }

        private String batteryStationFactors;
        public String BATTERY_STATION_FACTORS
        {
            get { return batteryStationFactors; }
            set
            {
                batteryStationFactors = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("BATTERY_STATION_FACTORS"));
                }
            }
        }

        /// <summary>
        /// Battery charge / fuel level percentage
        /// </summary>
        private Int16 remainingFuel;
        public Int16 REMAINING_FUEL
        {
            get { return remainingFuel; }
            set
            {
                remainingFuel = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("REMAINING_FUEL"));
                }
            }
        }

        /// <summary>
        /// Current running hours of the AGV
        /// </summary>
        private Int16 runningHours;
        public Int16 RUNNING_HOURS
        {
            get { return runningHours; }
            set
            {
                runningHours = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("RUNNING_HOURS"));
                }
            }
        }
    }

}
