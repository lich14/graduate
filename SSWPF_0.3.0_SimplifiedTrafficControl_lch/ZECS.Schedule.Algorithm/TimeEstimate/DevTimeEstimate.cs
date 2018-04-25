using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Data;
using ZECS.Schedule.Define;
using ZECS.Schedule.Algorithm.Utilities;

namespace ZECS.Schedule.Algorithm
{
    /*
     * 设备负载类型，目前只分为满载和空载两种
     */
    public enum CarryType
    { 
        FULL,
        EMPTY
    }

    /*
     *时间估算公式
     *时间单位为秒S
     *距离单位为米M
     */
    public static class EstimateTime
    {
        public static float PRECISION = 0.00000001f;

        /// <summary>
        /// 根据设备移动的距离、加速度、减速度、速度估算设备移动的时间
        /// </summary>
        /// <param name="acceleration"></param>
        /// <param name="deceleration"></param>
        /// <param name="speed"></param>
        /// <param name="distance></param>
        /// <returns></returns>   
        /// S = 1/2*a1*t1^2 + 1/2*a2*t2^2 + vt3 
        /// t1 = v/a1,t2 =v/a2, t3 = t- t1 -t2
        /// => t = s/v + 1/2*v*(a1+a2)/a1*a2
        public static TimeSpan? CalculateMoveTime2(double acceleration, double deceleration,
                                                   double speed, double distance)
        {
            if (distance < 0 || speed < 0 || acceleration < 0 || deceleration < 0)
                return null;

            double v = speed;
            double s = distance;
            double acc = acceleration;
            double dec = deceleration;

            double t = 0;

            if (s <= PRECISION)
                return null;
            else if (v <= PRECISION)
                return null;
            else if (acc <= PRECISION && dec <= PRECISION)
                t = s / v;
            else if (acc > PRECISION && dec <= PRECISION)
                t = s / v + 1 / 2 * v / acc;
            else if (acc <= PRECISION && dec > PRECISION)
                t = s / v + 1 / 2 * v / dec;
            else if (acc > PRECISION && dec > PRECISION)
                t = s / v + 1 / 2 * v * (acc + dec) / (acc * dec);

            return new TimeSpan(0, 0, 0, (int)t);

        }

        /// <summary>
        /// 加减速度相同的时间估算
        /// </summary>
        /// <param name="acceleration，加速度或减速度"></param>
        /// <param name="speedMax，速度上限"></param>
        /// <param name="distance，距离"></param>
        /// <returns></returns>
        /// 三角形或梯形法计算
        public static TimeSpan? CalculateMoveTime(double acceleration, double speedMax, double distance)
        {
            if (acceleration < 0 || speedMax < 0 || distance < 0)
                return null;

             double t = 0;

             if (distance <= PRECISION)
                 return null;
             else if (acceleration <= PRECISION)
                 return null;
             else if (speedMax > PRECISION)
             {
                 try
                 {
                     if (speedMax * speedMax / acceleration > distance)
                         t = 2 * Math.Sqrt(distance / acceleration);
                     else
                         t = distance / speedMax + speedMax / acceleration;
                 }
                 catch (System.DivideByZeroException ex)
                 {
                     throw ex;
                 }
             }
          
            return new TimeSpan(0,0,0,(int)t);
        }

        /// <summary>
        /// 加减速时间不相同时的时间估算
        /// </summary>
        /// <param name="minAccTime，加速到speedMax的最小加速时间"></param>
        /// <param name="minDecTime，从speedMax减速到0的最小减速时间"></param>
        /// <param name="speedMax，速度上限"></param>
        /// <param name="distance，距离"></param>
        /// <returns></returns>
        /// 三角形或梯形法计算
        public static TimeSpan? CalculateMoveTime( double minAccTime, double minDecTime, 
                                                    double speedMax, double distance)
        {
            if (minAccTime < 0 || minDecTime < 0 || speedMax < 0 || distance < 0)
                return null;

            double t = 0;

            if (distance <= PRECISION)
                return null;
            else if (speedMax <= PRECISION)
                return  null;
            else if (minAccTime > PRECISION && minDecTime > PRECISION)
            {
                try
                {
                    if (0.5 * speedMax * (minAccTime + minDecTime) > distance)
                    {
                        //未加速到speedMax,用两个相似三角形的面积求解
                        t = Math.Sqrt(2 * distance * (minAccTime + minDecTime) / speedMax);
                    }
                    else
                    {
                        //已加速到speedMax
                        t = distance / speedMax + 0.5 * (minAccTime + minDecTime);
                    }
                }
                catch (System.DivideByZeroException ex)
                {
                    throw ex;
                }
            }
            else
                return null;

            return new TimeSpan(0,0,0,(int)t);
        }

        // 调和函数
        // y = (-(x - 1)^2 + 1) * a
        // y = a[arctg(x + a) + a]
        public static double HarmonyResult(double dSample, double dResult)
        {
            if (dSample > dResult)
            {//harmony result between [0,1], target betweeen [0,1]
                return dSample * (1 - Math.Pow(dResult / dSample, 2));
            }
            else 
            {
                //harmony result between [1, 2], target between[1, +infinite]
                return (Math.Atan(dResult - dSample) + 1) * dSample;
            }
        }

    }

    public class Point3DF  
    {
        private PointF m_PointF = new PointF(0,0);
        private float m_z = 0;

        public Point3DF(float x, float y, float z)
        {
            m_z = z;
            m_PointF.X = x;
            m_PointF.Y = y;
        }

        public float Z { get { return m_z; } set { m_z = value; } }
        public float X { get { return m_PointF.X; } set { m_PointF.X = value; } }
        public float Y { get { return m_PointF.Y; } set { m_PointF.Y = value; } }
    }

    /*
     * 直线运动
     * 提供运行时间的估算方法
     * 可作为轨道车、吊具的基类
     */
    public class TE_LinearMoveDevice
    {
        public virtual TimeSpan? EstimateMoveTime(double speedMax, double minAccTime, double minDecTime, double distance)
        {
            return EstimateTime.CalculateMoveTime(minAccTime, minDecTime, speedMax, distance);
        }
    }

    /*
    * 带负载的直线运行设备
    * 可沿直线从一个位置移动到指定位置
    */
    public abstract class TE_CarryDevice : TE_LinearMoveDevice
    {
        //protected bool m_bInit = false;

        public double MinAccTime; //{ get; set; }
        public double MinDecTime; //{ get; set; }
        public double NoloadSpeed; //{ get; set; }
        public double LoadedSpeed; //{ get; set; }
        public double Position; //{ get; set; }

        public TE_CarryDevice() { }

        public virtual TimeSpan? EstimateMoveTime(double distance, CarryType carryType)
        {
            switch(carryType)
            {
                case CarryType.EMPTY:
                    return EstimateMoveTime(NoloadSpeed, MinAccTime, MinDecTime, distance);
                case CarryType.FULL:
                    return EstimateMoveTime(LoadedSpeed, MinAccTime, MinDecTime, distance);
                default: break;
            }

            return null;
        }

        public virtual TimeSpan? EstimateMoveToTime(double toPosition, CarryType carryType)
        {
            return EstimateMoveTime(Math.Abs(Position - toPosition), carryType);
        }
    }
    
    //吊具
    public class TE_HoistDevice : TE_CarryDevice
    {
        public double SafePosition;// { get; set; }  // 垂直方向吊具的安全位置，或park位置

        public virtual TimeSpan? EstimatePickUpTime(double pickUpPosition)
        {
            TimeSpan? tNoloadDown = EstimateMoveTime(Math.Abs(SafePosition - pickUpPosition), CarryType.EMPTY);
            TimeSpan? tLoadUp = EstimateMoveTime(Math.Abs(SafePosition - pickUpPosition), CarryType.FULL);

            if (tNoloadDown == null || tLoadUp == null)
                return null;

            return tNoloadDown + tLoadUp;
        }

        public virtual TimeSpan? EstimatePutDownTime(double putDownPosition)
        {
            TimeSpan? tLoadDown = EstimateMoveTime(Math.Abs(SafePosition - putDownPosition), CarryType.FULL);
            TimeSpan? tNoloadUp = EstimateMoveTime(Math.Abs(SafePosition - putDownPosition), CarryType.EMPTY);

            if (tLoadDown == null || tNoloadUp == null)
                return null;

            return tLoadDown + tNoloadUp;
        }
    }

    //梁上水平运动的小车，小车含一个垂直方向运动的吊具
    public class TE_TrolleyDevice : TE_CarryDevice
    {
        public TE_HoistDevice Hoister = new TE_HoistDevice(); //{ get { return m_Hoister;} }

        //移动到箱位提取箱子
        public virtual TimeSpan? EstimatePickUpTime(PointF ptPickUpPosition)
        {
            TimeSpan? tNoloadMoveTime = EstimateMoveToTime(ptPickUpPosition.X, CarryType.EMPTY);
            Position = ptPickUpPosition.X;

            TimeSpan? tPickUpTime = Hoister.EstimatePickUpTime(ptPickUpPosition.Y);

            if (tNoloadMoveTime == null || tPickUpTime == null )
                return null;

            return tNoloadMoveTime + tPickUpTime ;
        }

        //移动到箱位放下箱子
        public virtual TimeSpan? EstimatePutDownTime(PointF ptPutDownPosition)
        {
            TimeSpan? tLoadMoveTime = EstimateMoveToTime(ptPutDownPosition.X, CarryType.FULL);
            Position = ptPutDownPosition.X;

            TimeSpan? tPutDownTime = Hoister.EstimatePutDownTime(ptPutDownPosition.Y);

            if (tLoadMoveTime == null || tPutDownTime == null)
                return null;

            return tLoadMoveTime + tPutDownTime;
        }

        //同BAY操作，二维平面
        public virtual TimeSpan? EstimateWorkTime(PointF ptPickupPosition, PointF ptPutDownPosition)
        {
            TimeSpan? tPickUpTime = EstimatePickUpTime(ptPickupPosition);
            TimeSpan? tPutDownTime = EstimatePutDownTime(ptPutDownPosition);

            if (tPickUpTime == null || tPutDownTime == null)
                return null;

            return tPickUpTime + tPutDownTime;
        }
    }

    //梁式门吊起重机或悬臂桥机的大车,大车在移动时Trolley处于固定位置
    public class TE_GantryDevice : TE_CarryDevice
    {
        public TE_TrolleyDevice Trolley = new TE_TrolleyDevice();
    }
    
    /*
     * ARMG设备，包含一个吊车和龙门架车自身。能接受的任务是移箱任务
     */
    public class TE_ARMGDevice
    {
        public String ID;
        public TE_GantryDevice Gantry = new TE_GantryDevice();
     
        public void SetGantryPosition(double gantryPosition)
        {
            Gantry.Position = gantryPosition;
        }

        public TimeSpan? EstimateWorkTime(Point3DF fromPosition, Point3DF toPosition)
        {
            TimeSpan? tMoveFromPositonTime =Gantry.EstimateMoveToTime(fromPosition.X, CarryType.EMPTY);
            Gantry.Position = fromPosition.X;
            TimeSpan? tPickUpTime = Gantry.Trolley.EstimatePickUpTime(new PointF(fromPosition.Y, fromPosition.Z));
            TimeSpan? tMoveToPositionTime = Gantry.EstimateMoveTime(toPosition.X, CarryType.FULL);
            Gantry.Position = toPosition.X;
            TimeSpan? tPutDownTime = Gantry.Trolley.EstimatePutDownTime(new PointF(toPosition.Y, toPosition.Z));

            if (tMoveFromPositonTime == null || tPickUpTime == null || 
                tMoveToPositionTime == null || tPutDownTime == null)
                return null;

            return tMoveFromPositonTime + tPickUpTime + tMoveToPositionTime + tPutDownTime;
        }
    }

    public class TE_AGVDevice
    {
        public static bool InitQuayMapInfo()
        {
            return true;
        }

        private String m_ID;


        public TE_AGVDevice(string strID)
        {
            m_ID = strID;
        }
        //
        public TimeSpan? EstimateWorkTime(ushort fromNodeId, short fromHeading, ushort toLaneId)
        {
            int nRet = -1;
            nRet = AgvTimeEstimate.Instance.EstimateRunTime(fromNodeId, fromHeading, toLaneId); 
            
            if(nRet < 0)
               return null;

            return new TimeSpan(0,0,0,nRet);
        }

        public TimeSpan? EstimateWorkTime(int x,int y, short fromHeading, ushort toLaneId)
        {
            int nRet = -1;
            nRet = AgvTimeEstimate.Instance.EstimateRunTime(x, y, fromHeading, toLaneId);

            if (nRet < 0)
                return null;

            return new TimeSpan(0, 0, 0, nRet);
        }

        public TimeSpan? EstimateWorkTime(ushort fromLaneId, ushort toLaneId)
        {
            int nRet = -1;

            nRet = AgvTimeEstimate.Instance.EstimateRunTime(fromLaneId, toLaneId);

            if (nRet < 0)
                return null;

            return new TimeSpan(0, 0, 0, nRet);
        }
    }
}