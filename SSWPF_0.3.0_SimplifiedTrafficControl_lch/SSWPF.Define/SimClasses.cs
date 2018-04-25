using System;
using System.Collections.Generic;
using SharpSim;
using System.Windows;
using System.Windows.Media;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.CiTOS;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.YardMap;
using System.Reflection;

namespace SSWPF.Define
{
    /// <summary>
    /// 刚性存储单元接口
    /// </summary>
    public interface IRigidStoreUnit
    {
        StatusEnums.StoreStage eUnitStoreStage { get; set; }
        StatusEnums.StoreType eUnitStoreType { get; set; }
        bool IfContReservable(string ContID, string SizeStr);
        bool IfContOccupiable(string ContID);
        bool IfContRemovable(string ContID);
        bool ContReserve(string ContID, string SizeStr);
        bool ContOccupy(string ContID);
        bool ContRemove(string ContID);
        void Reset();
        StatusEnums.StoreStage CheckContStoreStatus(string ContID);
    }

    /// <summary>
    /// 对象的仿真事件、状态与令牌的初始化接口
    /// </summary>
    public interface ISimuComponent
    {
        void InitEvents(ref int CurrEventNo);
        void InitEdges();
        void InitTokens(ref int CurrTokenNo);
    }

    /// <summary>
    /// 定义完整性自检接口
    /// </summary>
    public interface IDefinitionCompletenessSelfCheckable
    {
        /// <summary>
        /// 只读
        /// </summary>
        bool IsDefinitionCompleted { get; set; }
        bool CheckDefinitionCompleteness();
    }

    /// <summary>
    /// AGV
    /// </summary>
    public class AGV : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public string Name;
        public AGVType oType;
        public uint CurrLaneID;
        public uint NextLaneID;
        public uint AimLaneID;
        public uint CurrAGVLineID;
        public double Deviation;
        public JobType eJobType;
        public StatusEnums.AGVWorkStage eAGVStage;
        public StatusEnums.ActionStatus eAGVStageStatus;
        public StatusEnums.MotionStatus eMotionStatus;
        public StatusEnums.StepTravelStatus eStepTravelStatus;
        public TwinRigidStoreUnit oTwinStoreUnit;
        public Point MidPoint;
        public double RotateAngle;
        public double CurrVelo;
        public int ZIndex;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public AGV()
        {
            this.eJobType = JobType.UNKNOWN;
            this.oTwinStoreUnit = new TwinRigidStoreUnit();
            this.ZIndex = 5;
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.CurrLaneID == 0) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 反锁记录，用于路径段的更新
    /// </summary>
    public class AntiLockRecord
    {
        public uint StartTPID;
        public uint EndTPID;
        public Dictionary<uint, List<uint>> dAntiLockAGVIDLists;

        public AntiLockRecord()
        {
            this.dAntiLockAGVIDLists = new Dictionary<uint, List<uint>>();
        }
    }

    /// <summary>
    /// AGV 占据 AGVLine 的小段
    /// </summary>
    public class AGVOccuLineSeg
    {
        public uint AGVID;
        public uint AGVLineID;
        public double StartPos;
        public double EndPos;
        public bool bStartPointHinge;
        public bool bEndPointHinge;
    }

    /// <summary>
    /// AGV道路划线
    /// </summary>
    public class AGVLine : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public CoordinateDirection eFlowDir;
        public CHE_Direction eMoveDir;
        public double FeaturePosition;
        public List<uint> lTPIDs;
        public List<uint> lLaneIDs;
        public List<uint> lLinkLineIDs;
        public bool bIfEssential;
        public int ZIndex;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public AGVLine()
        {
            this.lTPIDs = new List<uint>();
            this.lLaneIDs = new List<uint>();
            this.lLinkLineIDs = new List<uint>();
            this.ZIndex = 3;
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.FeaturePosition == 0 || this.lTPIDs.Count < 2) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    public class AGVRoute
    {
        public uint AGVID;
        public List<RouteSegment> lRouteSegments;
        public List<TPInfoEnRoute> lRouteTPInfos;
        public List<uint> lRouteLaneIDs;
        public int CurrLaneSeq;
        public double TotalLength;
        public double CurrClaimLength;
        public readonly int DecimalNum;
        public bool IsChanged = false;

        public AGVRoute(int DecimalNum = 2)
        {
            this.lRouteSegments = new List<RouteSegment>();
            this.lRouteTPInfos = new List<TPInfoEnRoute>();
            this.lRouteLaneIDs = new List<uint>();
            this.TotalLength = -1;
            this.DecimalNum = DecimalNum;
        }

        /// <summary>
        /// 判断 AGVLine 上，坐标为 Pos 的点，是否在 AGVRoute 范围内
        /// </summary>
        /// <param name="AGVLineID">AGVLine的编号</param>
        /// <param name="Pos">点在AGVLine上的投影坐标</param>
        /// <returns>若在AGVRoute上返回true，否则返回false</returns>
        public bool IsEnRoute(uint AGVLineID, double Pos)
        {
            bool bRet = false;
            double StartPos, EndPos;
            double PosInside = Math.Round(Pos, this.DecimalNum);
            List<RouteSegment> lRSs;

            lRSs = this.lRouteSegments.FindAll(u => u.AGVLineID == AGVLineID);

            if (lRSs.Count > 0)
            {
                foreach (RouteSegment oRS in lRSs)
                {
                    StartPos = Math.Round(oRS.StartLinePos, this.DecimalNum);
                    EndPos = Math.Round(oRS.EndLinePos, this.DecimalNum);
                    if (oRS.AGVLineID == AGVLineID
                        && (StartPos <= PosInside && EndPos >= PosInside) || (StartPos >= PosInside && EndPos <= PosInside))
                    {
                        bRet = true;
                        break;
                    }
                }
            }

            return bRet;
        }

        /// <summary>
        /// 返回点 (AGVLineID，Pos) 在当前 AGVRoute 上的位置
        /// </summary>
        /// <param name="AGVLineID">AGVLine编号</param>
        /// <param name="Pos">在AGVLine上的特征位置</param>
        /// <returns>在 Route 上时返回 RoutePos，不在则返回 -1</returns>
        public double GetRoutePosByLineAndPos(uint AGVLineID, double Pos)
        {
            double RoutePos = -1;
            double PosIn = Math.Round(Pos, this.DecimalNum);
            double StartPosIn, EndPosIn;
            List<RouteSegment> lRSs;

            // 注意可能有多段 RS 在同一 AGVLine 上

            lRSs = this.lRouteSegments.FindAll(u => u.AGVLineID == AGVLineID);

            if (lRSs.Count > 0)
            {
                foreach (RouteSegment oRS in lRSs)
                {
                    StartPosIn = Math.Round(oRS.StartLinePos, this.DecimalNum);
                    EndPosIn = Math.Round(oRS.EndLinePos, this.DecimalNum);
                    if ((StartPosIn <= PosIn && PosIn <= EndPosIn) || (StartPosIn >= PosIn && PosIn >= EndPosIn))
                        RoutePos = oRS.StartRoutePos + Math.Abs(PosIn - StartPosIn);
                }
            }

            RoutePos = Math.Round(RoutePos, this.DecimalNum);

            return RoutePos;
        }

        /// <summary>
        /// 根据路径位置 RoutePos 返回 AGVLineID 和 Pos
        /// </summary>
        /// <param name="RoutePos">在路径上的相对位置</param>
        /// <param name="LineID1">AGVLineID1</param>
        /// <param name="Pos1">在 AGVLine1 上的 Pos</param>
        /// <param name="LineID2">AGVLineID2</param>
        /// <param name="Pos2">在 AGVLine2 上的 Pos</param>
        /// <returns>找到点返回true，找不到点返回false</returns>
        public bool SearchForLineAndPosByRoutePos(double RoutePos, out uint LineID1, out double Pos1, out uint LineID2, out double Pos2)
        {
            bool bRet = false;
            double RoutePosIn = Math.Round(RoutePos, this.DecimalNum);
            List<RouteSegment> lRSs = new List<RouteSegment>();
            List<uint> lLineIDs = new List<uint>();
            List<double> lPoses = new List<double>();
            double TempPos;

            LineID1 = 0; Pos1 = -1; LineID2 = 0; Pos2 = -1;

            if (RoutePosIn >= 0 && RoutePosIn <= Math.Round(this.TotalLength, this.DecimalNum))
            {
                lRSs = this.lRouteSegments.FindAll(u => u.StartRoutePos <= RoutePosIn && u.EndRoutePos >= RoutePosIn);

                foreach (RouteSegment oRS in lRSs)
                {
                    switch (oRS.eCD)
                    {
                        case CHE_Direction.West:
                        case CHE_Direction.North:
                            TempPos = Math.Round(oRS.StartLinePos - (RoutePosIn - oRS.StartRoutePos), DecimalNum);
                            break;
                        case CHE_Direction.East:
                        case CHE_Direction.South:
                            TempPos = Math.Round(oRS.StartLinePos + (RoutePos - oRS.StartRoutePos), DecimalNum);
                            break;
                        default:
                            TempPos = -1;
                            break;
                    }
                    if (TempPos >= 0)
                    {
                        lPoses.Add(TempPos);
                        lLineIDs.Add(oRS.AGVLineID);
                        bRet = true;
                    }
                }
            }

            if (lRSs.Count >= 1)
            {
                LineID1 = lLineIDs[0];
                Pos1 = lPoses[0];
            }
            if (lRSs.Count == 2)
            {
                LineID2 = lLineIDs[1];
                Pos2 = lPoses[1];
            }

            return bRet;
        }

        /// <summary>
        /// 返回与 AGVLineID 和 Pos 对应的 RouteSegment 在 lRouteSegments 的位置索引 SegID
        /// </summary>
        /// <param name="AGVLineID">AGVLine编号</param>
        /// <returns>若找到返回索引值，没找到返回-1</returns>
        public int GetRouteSegIDByAGVLine(uint AGVLineID, double Pos)
        {
            int SegID;

            SegID = lRouteSegments.FindIndex(u => u.AGVLineID == AGVLineID && ((u.StartLinePos >= Pos && Pos >= u.EndLinePos) || (u.StartLinePos <= Pos && Pos <= u.EndLinePos)));

            return SegID;
        }

        /// <summary>
        /// 返回与 RoutePos 对应的 RouteSegmrnt 在 lRouteSegments 的第一个位置索引 SegID
        /// </summary>
        /// <param name="RoutePos">RoutePos</param>
        /// <returns>若找到则返回索引值，找不到返回-1</returns>
        public int GetRouteSegIDByRoutePos(double RoutePos)
        {
            int SegID;

            double RoutePosIn = Math.Round(RoutePos, DecimalNum);

            SegID = this.lRouteSegments.FindIndex(u => Math.Round(u.StartRoutePos, this.DecimalNum) <= RoutePosIn && Math.Round(u.EndRoutePos, this.DecimalNum) >= RoutePosIn);

            return SegID;
        }
    }

    /// <summary>
    /// AGV车型
    /// </summary>
    public class AGVType : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public double Length;
        public double Width;
        public double VeloFullUpper;
        public double VeloEmptyUpper;
        public double VeloTurnUpper;
        public double Acceleration;
        public double TurnRadius;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.Length <= 0 || this.Width <= 0 || this.VeloFullUpper <= 0 || this.VeloEmptyUpper <= 0
                || this.VeloTurnUpper <= 0 || this.TurnRadius <= 0) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// ASC
    /// </summary>
    public class ASC : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public string Name;
        public ASCTrolley oTrolley;
        public Point BasePoint;
        public string BlockName;
        public StatusEnums.ASCSide eSide;
        public ASCType oType;
        public double AimPos;
        public string CurrBay;
        public StatusEnums.MotionStatus eMotionStatus;
        public StatusEnums.ASCSubProc eSubProc;
        public StatusEnums.StepTravelStatus eTravelStatus;
        public int ZIndex;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public ASC()
        {
            this.Name = "";
            this.BlockName = "";
            this.BasePoint = new Point(0, 0);
            this.oTrolley = new ASCTrolley();
            this.ZIndex = 7;
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || string.IsNullOrWhiteSpace(this.Name) || string.IsNullOrWhiteSpace(this.BlockName) 
                || this.BasePoint.X == 0 || this.BasePoint.Y == 0 || !this.oTrolley.CheckDefinitionCompleteness())
                 bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
   }

    /// <summary>
    /// ASC小车
    /// </summary>
    public class ASCTrolley : IDefinitionCompletenessSelfCheckable
    {
        public SingleRigidStoreUnit oSingleStoreUnit;
        public StatusEnums.ASCTrolleyStage eTroStage;
        public StatusEnums.MotionStatus eMotionStatus;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public ASCTrolley()
        {
            this.oSingleStoreUnit = new SingleRigidStoreUnit();
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (!oSingleStoreUnit.CheckDefinitionCompleteness())
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }
    
    /// <summary>
    /// ASC类型
    /// </summary>
    public class ASCType : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public double BaseGauge;
        public double TrackGauge;
        public double Thickness;
        public double TravelSpeed;
        public double TrolleySpeed;
        public double FullLiftSpeed;
        public double EmptyLiftSpeed;
        public double MaxLiftHeight;
        public bool bGanTroSimulMove;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.BaseGauge <= 0 || this.TrackGauge <= 0 || this.TravelSpeed <= 0 || this.TrolleySpeed <= 0
                || this.FullLiftSpeed <= 0 || this.EmptyLiftSpeed <= 0 || this.MaxLiftHeight <= 0) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 场桥作业点
    /// </summary>
    public class ASCWorkPoint : IDefinitionCompletenessSelfCheckable
    {
        public string IndStr;
        public string BlockName;
        public uint Bay;
        public Point BasePoint;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public ASCWorkPoint()
        {
            this.IndStr = "";
            this.BlockName = "";
            this.BasePoint = new Point();
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.IndStr.Length == 0 || this.Bay == 0 || this.BasePoint.X < 0 || this.BasePoint.Y < 0 || String.IsNullOrWhiteSpace(this.BlockName))
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 箱区单元
    /// </summary>
    public class Block : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public string Name;
        public double X;
        public double Y;
        public double MarginX;
        public double MarginY;
        public string BayVaryDir;
        public uint WSASC;
        public uint LSASC;

        public List<BlockDiv> lBlockDivsX;
        public List<BlockDiv> lBlockDivsY;
        public int ZIndex;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public Block()
        {
            this.ID = 0;
            this.Name = "";
            this.X = 0;
            this.Y = 0;
            this.MarginX = 0;
            this.MarginY = 0;
            this.ZIndex = 1;
            this.BayVaryDir = "";
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.Name.Length == 0 || this.X <= 0 || this.Y <= 0 || this.MarginX <= 0 || this.MarginY <= 0 || this.lBlockDivsX == null
                || this.lBlockDivsY == null || this.BayVaryDir.Length == 0 || (this.BayVaryDir != "X" && this.BayVaryDir == "Y")) bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 箱区倍向划分单元
    /// </summary>
    public struct BlockDiv : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public double StartPos;
        public int Cont45Permitted;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.StartPos < 0) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 箱区倍向划分类型
    /// </summary>
    public class BlockDivsType : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public List<BlockDiv> lBlockDivs;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public BlockDivsType()
        {
            this.lBlockDivs = new List<BlockDiv>();
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.lBlockDivs.Count == 0) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }


    /// <summary>
    /// STS_PB 的方向指示。
    /// </summary>
    [Serializable]
    public class DirSign
    {
        public string Name;
        public int ZIndex;

        public DirSign()
        {
            this.Name = "";
            this.ZIndex = 2;
        }
    }

    /// <summary>
    /// 基本结构对象，开环或者闭环，死锁检测时用
    /// </summary>
    public class ElementConstruction
    {
        public List<uint> lCycleSegIDs;
        public double Length;
        public int Capacity;
        public StatusEnums.ConsType eConsType;

        public ElementConstruction()
        {
            this.lCycleSegIDs = new List<uint>();
        }
    }

    /// <summary>
    /// 作业线计划
    /// </summary>
    public class HandleLinePlan
    {
        public string WQInd;
        public uint QCID;
        public uint VesID;
        public string Voyage;
        public string QCWorkPointInd;
        public StatusEnums.STSVisitDir eSTSVisitDir;

        public HandleLinePlan()
        {
            this.WQInd = "";
            this.Voyage = "";
            this.QCWorkPointInd = "";
        }
    }

    /// <summary>
    /// 箱型的ISO标准
    /// </summary>
    public class ISORef : IDefinitionCompletenessSelfCheckable
    {
        public string ISO;
        public StatusEnums.ContType eContType;
        public StatusEnums.ContSize eContSize;
        public short ContainerLengthCM;
        public short ContainerHeightCM;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public ISORef()
        {
            this.ISO = "";
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ISO.Length == 0 || this.ContainerHeightCM <= 0 || this.ContainerLengthCM <= 0
                 || this.eContType == StatusEnums.ContType.Unknown || this.eContSize == StatusEnums.ContSize.Unknown) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 车道
    /// </summary>
    [Serializable]
    public class Lane : IDefinitionCompletenessSelfCheckable
    {
        // necessary, fixed
        public uint ID;
        public uint LineID;
        public double InitLen;
        public AreaType eType;
        public string AreaLaneID;
        public double RotateAngle;
        public string Name;
        public double Width;
        public int Zindex;

        // necessary, flexible
        public uint TPIDStart;
        public uint TPIDEnd;
        public LaneStatus eStatus;
        public LaneAttribute eAttr;
        public Point pMid;
        public Point pWork;
        /* 目前 pWork 的值作为 AGV 在 Lane 停车的依据。
         * 对于 WSTP，其X值根据 Rack 确定
         * 对于 WSPB 、 QCPB 和 QCTP，其 Y 值取进出 TP 的均值 */

        // unnecessary, flexible
        public uint AGVNo;
        public uint CheNo;
        public uint ASCWorkPointID;
        public uint MateID;
        public DirSign oDirSign;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public Lane()
        {
            this.AreaLaneID = "";
            this.Name = "";
            this.Zindex = 1;
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.LineID == 0 || this.TPIDStart == 0 || this.TPIDEnd == 0 || this.InitLen == 0 || this.Name.Length == 0 
                || this.AreaLaneID.Length == 0 || (this.eType == AreaType.STS_PB && this.oDirSign == null)) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 支架，或者叫伴侣
    /// </summary>
    public class Mate : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public string Name;
        public uint LaneID;
        public string BlockName;
        public string AreaLaneID;
        public uint TPIDStart;
        public uint TPIDEnd;
        public double Width;
        public uint ASCWorkPointID;
        public TwinRigidStoreUnit oStorageUnit;
        public StatusEnums.MateStatus eMateStatus;
        public int ZIndex;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public Mate()
        {
            this.Name = "";
            this.BlockName = "";
            this.AreaLaneID = "";
            this.oStorageUnit = new TwinRigidStoreUnit();
            this.eMateStatus = StatusEnums.MateStatus.Normal;
            this.ZIndex = 5;
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || string.IsNullOrWhiteSpace(this.Name) || this.LaneID == 0 || string.IsNullOrWhiteSpace(this.BlockName) 
                || string.IsNullOrWhiteSpace(this.AreaLaneID) || !this.oStorageUnit.CheckDefinitionCompleteness())
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 用于 Check 排序结果的类
    /// </summary>
    public class OrderCheck
    {
        public string ContID;
        public int OriSeq;
        public int OrderSeq;
        public bool IfError;

        public OrderCheck()
        {
            this.ContID = "";
        }
    }

    /// <summary>
    /// 箱堆，表示一摞箱子的容纳空间。暂时采用SingleRigidStoreUnit，翻箱需交换单元信息
    /// </summary>
    public class Pile : IDefinitionCompletenessSelfCheckable
    {
        public string Name { get; set; }
        public string BlockName { get; set; }
        public uint Bay { get; set; }
        public uint Row { get; set; }
        public YardSlot Slot1 { get; set; }
        public YardSlot Slot2 { get; set; }
        public PileType oType { get; set; }
        public List<SingleRigidStoreUnit> lUnits { get; set; }
        public int StackedNum { get; set; }
        public int ReservedNum { get; set; }
        public int ZIndex { get; set; }

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public Pile()
        {
            this.Name = "";
            this.lUnits = new List<SingleRigidStoreUnit>();
            this.BlockName = "";
            this.ZIndex = 6;
        }

        /// <summary>
        /// 定义完整性检查
        /// </summary>
        /// <returns>完整返回true，否则返回false</returns>
        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (string.IsNullOrWhiteSpace(this.Name) || string.IsNullOrWhiteSpace(this.BlockName) || this.Bay == 0 || this.Row == 0
                || this.oType.eContSize == StatusEnums.ContSize.Unknown
                || (this.Bay % 2 == 0 && this.oType.eContSize == StatusEnums.ContSize.TEU) 
                || (this.Bay % 2 == 1 && this.oType.eContSize != StatusEnums.ContSize.TEU)) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }

        /// <summary>
        /// 刷新串内的预约和占据箱子数量
        /// </summary>
        public void RenewNums()
        {
            int ReserveNum = 0;
            int StackNum = 0;

            foreach (SingleRigidStoreUnit oSSU in this.lUnits)
            {
                if (oSSU.eContStoreStage == StatusEnums.StoreStage.Stored)
                {
                    StackNum++;
                    ReserveNum++;
                }
                else if (oSSU.eContStoreStage == StatusEnums.StoreStage.Reserved)
                    ReserveNum++;
            }

            this.StackedNum = StackNum;
            this.ReservedNum = ReserveNum;
        }

        /// <summary>
        /// 判断集装箱是否能够预约本串。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <param name="SizeStr">尺寸</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool IfContReservable(string ContID, string SizeStr)
        {
            bool bRet = false;
            StatusEnums.ContSize eContSize = StatusEnums.GetContSize(SizeStr);
            if (eContSize != this.oType.eContSize)
                return false;

            // Index 与层高同增减
            foreach (SingleRigidStoreUnit oSSU in this.lUnits)
            {
                bRet = oSSU.IfContReservable(ContID, SizeStr);
                if (bRet) 
                    break;
            }

            return bRet;
        }

        /// <summary>
        /// 判断集装箱是否能够占据本串。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <returns>若能占据，返回此时占据将导致的翻箱数；若不能占据，返回-1</returns>
        public bool IfContOccupiable(string ContID)
        {
            bool bRet = false;

            if (this.CheckContStoreStatus(ContID) == StatusEnums.StoreStage.Reserved)
                bRet = true;

            return bRet;

        }

        /// <summary>
        /// 判断集装箱是否能移出本串。只要有箱就能出
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <returns>能移出返回true，不能移出返回false</returns>
        public bool IfContRemovable(string ContID)
        {
            bool bRet = false;

            if (this.CheckContStoreStatus(ContID) == StatusEnums.StoreStage.Stored)
                bRet = true;

            return bRet;
        }

        /// <summary>
        /// 集装箱预约本串
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <param name="SizeStr">尺寸</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool ContReserve(string ContID, string SizeStr)
        {
            if (!this.IfContReservable(ContID, SizeStr))
                return false;

            for (int i = 0; i < this.lUnits.Count; i++)
            {
                if (this.lUnits[i].IfContReservable(ContID, SizeStr))
                {
                    this.lUnits[i].ContReserve(ContID, SizeStr);
                    break;
                }
            }
            
            this.RenewNums();
            return true;
        }

        /// <summary>
        /// 集装箱存入本串。注意可能交换预约位置。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool ContOccupy(string ContID)
        {
            bool bRet = false;
            int ReservedTier = 0;
            int ToppedTier = this.StackedNum;

            for (int i = 0; i < this.lUnits.Count; i++)
            {
                if (this.lUnits[i].IfContOccupiable(ContID))
                {
                    ReservedTier = i;
                    bRet = true;
                    break;
                }
            }

            // 交换预约位置，换个箱号就行
            if (bRet && ReservedTier > ToppedTier)
            {
                this.lUnits[ReservedTier].ContID = this.lUnits[ToppedTier].ContID;
                this.lUnits[ToppedTier].ContID = ContID;
            }
            if (bRet)
            {
                this.lUnits[ToppedTier].ContOccupy(ContID);
                this.RenewNums();
            }

            return bRet;
        }

        /// <summary>
        /// 集装箱从本串取出。暂时允许去被压箱，上方箱瞬间自然下落
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool ContRemove(string ContID)
        {
            bool bRet = false;

            int RemoveTier = 0;
            int TopTier = this.StackedNum - 1;

            for (int i = 0; i < this.lUnits.Count; i++)
            {
                if (this.lUnits[i].IfContRemovable(ContID))
                {
                    RemoveTier = i;
                    bRet = true;
                    break;
                }
            }
            if (bRet)
            {
                this.lUnits[RemoveTier].ContRemove(ContID);
                if (RemoveTier < TopTier)
                {
                    for (int i = RemoveTier; i < TopTier; i++)
                    {
                        this.lUnits[i] = this.lUnits[i + 1];
                    }
                    this.lUnits[TopTier] = new SingleRigidStoreUnit();
                }
                this.RenewNums();
            }

            return bRet;
        }

        /// <summary>
        /// 检查集装箱的储存信息。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <returns>预约、堆存或者不相关</returns>
        public StatusEnums.StoreStage CheckContStoreStatus(string ContID)
        {
            StatusEnums.StoreStage eStoreStage = StatusEnums.StoreStage.None;

            foreach (SingleRigidStoreUnit oU in this.lUnits)
            {
                if (oU.ContID == ContID)
                {
                    eStoreStage = oU.eContStoreStage;
                    break;
                }
            }

            return eStoreStage;
        }

        /// <summary>
        /// 重置，仅针对箱
        /// </summary>
        public void Reset()
        {
            this.lUnits.ForEach(u => u.Reset());
            this.RenewNums();
        }

    }

    /// <summary>
    /// 箱堆类型
    /// </summary>
    public class PileType : IDefinitionCompletenessSelfCheckable
    {
        public StatusEnums.ContSize eContSize;
        public double Length;
        public double Width;
        public int MaxStackNum;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public PileType()
        {
            this.MaxStackNum = 5;
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.Length <= 0 || this.Width <= 0 || this.eContSize == StatusEnums.ContSize.Unknown) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// Pile 申请信息
    /// </summary>
    public class PileReclaimMsg
    {
        public string PileName;
        public PileType oPileType;

        public PileReclaimMsg()
        {
            this.PileName = "";
        }
    }

    /// <summary>
    /// 进箱计划类，只到箱型尺寸
    /// </summary>
    public class PlanGroup : IDefinitionCompletenessSelfCheckable
    {
        public string Name;
        public Move_Kind MoveKind;
        public StatusEnums.ContSize eContSize;
        public StatusEnums.ContType eContType;
        public string ISO;
        public int TotalNum;
        public int UsedNum;
        public List<string> lPlanRangeNames;
        public List<string> lPlanPlacNames;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public PlanGroup()
        {
            this.Name = "";
            this.ISO = "";
            this.lPlanRangeNames = new List<string>();
            this.lPlanPlacNames = new List<string>();
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (string.IsNullOrWhiteSpace(this.Name) || this.eContSize == StatusEnums.ContSize.Unknown || this.eContType == StatusEnums.ContType.Unknown
                || string.IsNullOrWhiteSpace(this.ISO) || this.TotalNum < 0 || this.UsedNum < 0)
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 进箱计划位，到每个箱位
    /// </summary>
    public class PlanPlac : IDefinitionCompletenessSelfCheckable
    {
        public string Name;
        public string PlanRangeName;
        public string PlanGroupName;
        public int SeqNo;
        public int OrderID;
        public string Plac;
        public string BlockName;
        public int Bay;
        public int Row;
        public int Tier;
        public bool IsUsed;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public PlanPlac()
        {
            this.Name = "";
            this.PlanGroupName = "";
            this.Plac = "";
            this.PlanRangeName = "";
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (string.IsNullOrWhiteSpace(this.Name) || string.IsNullOrWhiteSpace(PlanGroupName) || string.IsNullOrWhiteSpace(this.Plac) || string.IsNullOrWhiteSpace(this.PlanRangeName)
                || string.IsNullOrWhiteSpace(this.BlockName) || this.SeqNo <= 0 || this.OrderID <= 0 || this.Bay <= 0 || this.Row <= 0 || this.Tier <= 0)
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 进箱计划组，到箱区范围
    /// </summary>
    public class PlanRange : IDefinitionCompletenessSelfCheckable
    {
        public string Name;
        public string PlanGroupName;
        public int SeqNo;
        public string BeginPlac;
        public string EndPlac;
        public string BlockName;
        public int BeginBay;
        public int EndBay;
        public int BeginRow;
        public int EndRow;
        public int BeginTier;
        public int EndTier;
        public int PriNo;
        public int TotalNum;
        public int UsedNum;
        public List<string> lPlanPlacNames;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public PlanRange()
        {
            this.Name = "";
            this.PlanGroupName = "";
            this.BeginPlac = "";
            this.EndPlac = "";
            this.BlockName = "";
            this.lPlanPlacNames = new List<string>();
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (string.IsNullOrWhiteSpace(this.Name) || string.IsNullOrWhiteSpace(this.PlanGroupName) || string.IsNullOrWhiteSpace(this.BeginPlac)
                || string.IsNullOrWhiteSpace(this.EndPlac) || string.IsNullOrWhiteSpace(this.BlockName) || this.SeqNo < 0 || this.BeginBay <= 0 || this.EndBay <= 0
                || this.BeginRow <= 0 || this.EndRow <= 0 || this.BeginTier <= 0 || this.EndTier <= 0 || this.PriNo < 0 || this.TotalNum < 0 || this.UsedNum < 0)
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 向 InfoFrame 的投射包
    /// </summary>
    public class ProjectPackageToInfoFrame
    {
        public List<STS_WORK_QUEUE_STATUS> lWQs;
        public List<WORK_INSTRUCTION_STATUS> lWIs;
        public List<BERTH_STATUS> lBerthStatuses;
        public List<STS_ResJob> lSTSResJobs;
        public List<STS_Task> lSTSTasks;
        public List<STS_Order> lSTSOrders;
        public List<STS_Command> lSTSCommands;
        public List<STS_STATUS> lSTSStatuses;
        public List<ASC_ResJob> lASCResJobs;
        public List<ASC_Task> lASCTasks;
        public List<ASC_Order> lASCOrders;
        public List<ASC_Command> lASCCommands;
        public List<ASC_STATUS> lASCStatuses;
        public List<AGV_ResJob> lAGVResJobs;
        public List<AGV_Task> lAGVTasks;
        public List<AGV_Order> lAGVOrders;
        public List<AGV_Command> lAGVCommands;
        public List<AGV_STATUS> lAGVStatuses;
        public List<string> lContIDs;
        public int[,] OrderArray;
        public List<OrderCheck> lOrderChecks;

        public ProjectPackageToInfoFrame()
        {
            this.lWQs = new List<STS_WORK_QUEUE_STATUS>();
            this.lWIs = new List<WORK_INSTRUCTION_STATUS>();
            this.lBerthStatuses = new List<BERTH_STATUS>();
            this.lSTSResJobs = new List<STS_ResJob>();
            this.lSTSTasks = new List<STS_Task>();
            this.lSTSOrders = new List<STS_Order>();
            this.lSTSCommands = new List<STS_Command>();
            this.lSTSStatuses = new List<STS_STATUS>();
            this.lASCResJobs = new List<ASC_ResJob>();
            this.lASCTasks = new List<ASC_Task>();
            this.lASCOrders = new List<ASC_Order>();
            this.lASCCommands = new List<ASC_Command>();
            this.lASCStatuses = new List<ASC_STATUS>();
            this.lAGVResJobs = new List<AGV_ResJob>();
            this.lAGVTasks = new List<AGV_Task>();
            this.lAGVOrders = new List<AGV_Order>();
            this.lAGVCommands = new List<AGV_Command>();
            this.lAGVStatuses = new List<AGV_STATUS>();
            this.lContIDs = new List<string>();
            this.OrderArray = null;
            this.lOrderChecks = new List<OrderCheck>();
        }
    }

    /// <summary>
    /// 向 Panel 的投射包
    /// </summary>
    public class ProjectPackageToSimPanel
    {
        public DateTime dtSimDateTime;
        public StatusEnums.SimPhrase eSimPhrase;
    }

    /// <summary>
    /// 向 ViewFrame 的投射包
    /// </summary>
    public class ProjectPackageToViewFrame
    {
        public TerminalRegion oTR;
        public List<SimTransponder> lTPs;
        public List<Block> lBlocks;
        public List<Lane> lLanes;
        public List<Mate> lMates;
        public List<AGV> lAGVs;
        public List<AGVLine> lAGVLines;
        public List<QCDT> lQCs;
        public List<ASC> lASCs;
        public List<Pile> lPiles;
        public List<Vessel> lBerthVessels;
        public Dictionary<string, Dictionary<string, Color>> dColorDics;

        public ProjectPackageToViewFrame()
        {
            this.lTPs = new List<SimTransponder>();
            this.lBlocks = new List<Block>();
            this.lLanes = new List<Lane>();
            this.lMates = new List<Mate>();
            this.lAGVs = new List<AGV>();
            this.lAGVLines = new List<AGVLine>();
            this.lQCs = new List<QCDT>();
            this.lASCs = new List<ASC>();
            this.lPiles = new List<Pile>();
            this.lBerthVessels = new List<Vessel>();
            this.dColorDics = new Dictionary<string, Dictionary<string, Color>>();
        }
    }

    /// <summary>
    /// 对岸桥基本动作的时间统计单元
    /// </summary>
    public class QCActionTimeStat : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public double Min;
        public double Max;
        public double Avg;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public QCActionTimeStat()
        {
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.Min < 0 || this.Max < 0 || this.Avg < 0)
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 双小车岸桥，两个小车，两个双箱存放单元
    /// </summary>
    public class QCDT : ISimuComponent, IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public string Name;
        public QCType oType;
        public QCMainTrolley MainTrolley;
        public QCViceTrolley ViceTrolley;
        public List<QCPlatformSlot> Platform;
        public Point BasePoint;
        public int ZIndex;
        public string CurrWQ;
        public Move_Kind eMoveKind;
        public double AimPos;
        public bool Reachable;
        public StatusEnums.MotionStatus eMotionStatus;
        public StatusEnums.StepTravelStatus eStepTravelStatus;
        public List<string> lLoadableContList;
        public uint nAGVCountMin;
        public uint nAGVCountMax;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public Event eStep;
        public Edge gSimToStep;
        public SimToken oQCToken;

        // 属性和事件分布初始化
        public QCDT()
        {
            this.Name = "";
            this.MainTrolley = new QCMainTrolley(this);
            this.ViceTrolley = new QCViceTrolley(this);
            this.Platform = new List<QCPlatformSlot>();
            this.BasePoint = new Point();
            this.lLoadableContList = new List<string>();
            this.CurrWQ = "";
            this.ZIndex = 8;
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || string.IsNullOrWhiteSpace(this.Name) || this.oType == null || this.nAGVCountMax == 0
                || !this.MainTrolley.CheckDefinitionCompleteness() || !this.ViceTrolley.CheckDefinitionCompleteness()|| this.Platform.Count == 0 
                || this.Platform.Exists(u => !u.CheckDefinitionCompleteness()) || this.BasePoint.X <= 0 || this.BasePoint.Y <= 0)
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }

        public void InitEvents(ref int CurrEventNo)
        {
            this.eStep = new Event(this.ID.ToString(), CurrEventNo.ToString(), "QCStep", CurrEventNo++);
            foreach (QCPlatformSlot oSlot in this.Platform)
                oSlot.InitEvents(ref CurrEventNo);
            this.MainTrolley.InitEvents(ref CurrEventNo);
            this.ViceTrolley.InitEvents(ref CurrEventNo);
        }

        public void InitEdges()
        {
            foreach (QCPlatformSlot oSlot in this.Platform)
            {
                oSlot.InitEdges();
                oSlot.gStep2Action = new Edge("QC:" + this.ID.ToString() + ":Step2ActionOfPlatform:" + oSlot.ID.ToString(), this.eStep, oSlot.eAction) { condition = false, interEventTime = 0 };
            }
            this.MainTrolley.InitEdges();
            this.MainTrolley.gStep2Action = new Edge("QC" + this.ID.ToString() + "Step2ActionMainTro", this.eStep, this.MainTrolley.eAction) { condition = false, interEventTime = 0 };
            this.ViceTrolley.InitEdges();
            this.ViceTrolley.gStep2Action = new Edge("QC" + this.ID.ToString() + "Step2ActionViceTro", this.eStep, this.ViceTrolley.eAction) { condition = false, interEventTime = 0 };
        }

        public void InitTokens(ref int CurrTokenNo)
        {
            this.oQCToken = new SimToken(CurrTokenNo++, "QCToken");
            this.MainTrolley.InitTokens(ref CurrTokenNo);
            this.ViceTrolley.InitTokens(ref CurrTokenNo);
            foreach (QCPlatformSlot oSlot in this.Platform)
                oSlot.InitTokens(ref CurrTokenNo);
        }

        public void SetNextActionDateTime(DateTime dtInput)
        {
            if (!IsDefinitionCompleted)
                return;
            this.MainTrolley.dtNextAction = dtInput;
            this.ViceTrolley.dtNextAction = dtInput;
            this.Platform.ForEach(u => u.dtNextAction = dtInput);
        }
    }

    /// <summary>
    /// 岸桥小车原型
    /// </summary>
    public class QCTrolley : ISimuComponent, IDefinitionCompletenessSelfCheckable
    {
        public TwinRigidStoreUnit oTwinStoreUnit;
        public QCTrolleyTimeStat oQCTrolleyTimeStat;
        public StatusEnums.QCTrolleyStage eTroStage;
        public QCDT oQC;
        public DateTime dtNextAction;
        public bool bLockPlatform;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public QCTrolley(QCDT oQCInput)
        {
            this.oTwinStoreUnit = new TwinRigidStoreUnit();
            this.oQC = oQCInput;
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.oQCTrolleyTimeStat == null || this.oTwinStoreUnit == null 
                || !this.oTwinStoreUnit.CheckDefinitionCompleteness() || this.oQC == null)
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }

        public Event eAction;
        public Edge gStep2Action;
        public SimToken oToken;

        public virtual void InitEvents(ref int CurrEventNo)
        {
        }

        public virtual void InitEdges()
        {
        }

        public virtual void InitTokens(ref int CurrTokenNo)
        {
        }

    }

    /// <summary>
    /// 主小车
    /// </summary>
    public class QCMainTrolley : QCTrolley, ISimuComponent
    {
        public StatusEnums.QCMainTrolleySubProc eTroSubProc;

        public QCMainTrolley(QCDT oQCInput)
            : base(oQCInput)
        {
        }

        public override void InitEvents(ref int CurrEventNo)
        {
            this.eAction = new Event(oQC.ID.ToString(), CurrEventNo.ToString(), "QCMainTroAction", CurrEventNo++);
        }

        public override void InitEdges()
        {
        }

        public override void InitTokens(ref int CurrTokenNo)
        {
            this.oToken = new SimToken(CurrTokenNo++, "QC:" + oQC.ID.ToString() + "MainTroToken");
        }
        
    }

    /// <summary>
    /// 副小车
    /// </summary>
    public class QCViceTrolley : QCTrolley, ISimuComponent
    {
        public StatusEnums.QCViceTrolleySubProc eTroSubProc;

        public QCViceTrolley(QCDT oQCInput)
            : base(oQCInput)
        {
        }

        public override void InitEvents(ref int CurrEventNo)
        {
            this.eAction = new Event(oQC.ID.ToString(), CurrEventNo.ToString(), "QCViceTroAction", CurrEventNo++);
        }

        public override void InitEdges()
        {
        }

        public override void InitTokens(ref int CurrTokenNo)
        {
            this.oToken = new SimToken(CurrTokenNo++, "QC:" + oQC.ID.ToString() + "ViceTroToken");
        }
    }

    /// <summary>
    /// 岸桥作业点
    /// </summary>
    public class QCWorkPoint : IDefinitionCompletenessSelfCheckable
    {
        public string IndStr;
        public uint VesID;
        public uint Bay;
        public Point BasePoint;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public QCWorkPoint()
        {
            this.IndStr = "";
            this.BasePoint = new Point();
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.IndStr.Length == 0 || this.Bay == 0 || this.BasePoint.X < 0 || this.BasePoint.Y < 0 || this.VesID == 0)
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 岸桥平台箱位
    /// </summary>
    public class QCPlatformSlot : ISimuComponent, IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public TwinRigidStoreUnit oTwinStoreUnit;
        public QCActionTimeStat oConfirmStat;
        public QCDT oQC;
        public DateTime dtNextAction;
        public StatusEnums.ActionStatus eConfirmStatus;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public QCPlatformSlot(QCDT oQC)
        {
            this.oQC = oQC;
            this.oTwinStoreUnit = new TwinRigidStoreUnit();
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.oTwinStoreUnit == null || this.oConfirmStat == null || this.oQC == null)
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }

        public Event eAction;
        public Edge gStep2Action;
        public SimToken oToken;

        public void InitEvents(ref int CurrEventNo)
        {
            this.eAction = new Event(oQC.ID.ToString() + ':' + this.ID.ToString(), CurrEventNo.ToString(), "QCPlatformSlot", CurrEventNo++);
        }

        public void InitEdges()
        {
        }

        public void InitTokens(ref int CurrTokenNo)
        {
            this.oToken = new SimToken(CurrTokenNo++, "QC:" + oQC.ID.ToString() + "PlatformSlot:" + this.ID.ToString() + "Token");
        }
    }

    /// <summary>
    /// 岸桥位置计划
    /// </summary>
    public class QCPosPlan
    {
        public uint QCID;
        public StatusEnums.StepTravelStatus eStepTravelStatus;
        public string CurrWQ;
        public bool Reachable;
        public double WQPos;
        public double CurrPos;
        public double AimPos;
        public List<Lane> lQCTPs;

        public QCPosPlan()
        {
            this.CurrWQ = "";
            this.lQCTPs = new List<Lane>();
        }
    }



    /// <summary>
    /// 岸桥各基本动作时间统计单元的集合
    /// </summary>
    public class QCTrolleyTimeStat : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public QCActionTimeStat oStatWSRise;
        public QCActionTimeStat oStatWSFall;
        public QCActionTimeStat oStatWSToB;
        public QCActionTimeStat oStatBToWS;
        public QCActionTimeStat oStatBRise;
        public QCActionTimeStat oStatBFall;
        public QCActionTimeStat oStatBToLS;
        public QCActionTimeStat oStatLSToB;
        public QCActionTimeStat oStatLSFall;
        public QCActionTimeStat oStatLSRise;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.oStatLSRise == null || this.oStatLSFall == null || this.oStatBRise == null
                || this.oStatBFall == null || this.oStatWSRise == null || this.oStatWSFall == null
                || this.oStatBToLS == null || this.oStatLSToB == null || this.oStatBToWS == null || this.oStatWSToB == null) 
                bRet = false;
            this.isDefinitionCompleted = false;
            return bRet;
        }
    }

    /// <summary>
    /// 岸桥类型
    /// </summary>
    public class QCType : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public double TrackGauge;
        public double BaseGauge;
        public double FrontReach;
        public double BackReach;
        public double CantiWidth;
        public double Thickness;
        public double TravelSpeed;
        public int PlatformSlotNum;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }
        public QCType()
        {
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.TrackGauge <= 0 || this.BaseGauge <= 0 || this.FrontReach <= 0 || this.BackReach <= 0 || this.CantiWidth <= 0
                || this.Thickness <= 0 || this.TravelSpeed <= 0 || this.PlatformSlotNum <= 0) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 单行线范围
    /// </summary>
    public struct DirectedRange
    {
        public double Head;
        public double Tail;
        public bool IfExist;

        public DirectedRange(double NumHead, double NumTail)
            : this()
        {
            this.Head = NumHead;
            this.Tail = NumTail;
            this.IfExist = false;
        }

        public void CutFromTail(DirectedRange R2)
        {
            Vector v0 = new Vector(this.Tail - this.Head, 0);
            Vector vH = new Vector(R2.Head - this.Head, 0);
            Vector vT = new Vector(R2.Tail - this.Head, 0);
            if (vH.Length == 0 || vT.Length == 0 || Vector.AngleBetween(vH, vT) != 0) this.IfExist = false;
            else
            {
                if (Vector.AngleBetween(v0, vH) == 0 && v0.Length > vH.Length) v0 = vH;
                if (Vector.AngleBetween(v0, vT) == 0 && v0.Length > vT.Length) v0 = vT;
                this.Tail = this.Head + v0.X;
            }
        }
    }

    /// <summary>
    /// AGV路径的直线段部分
    /// </summary>
    public class RouteSegment
    {
        public uint AGVID;
        public uint ID;
        public uint AGVLineID;
        public Point StartPoint;
        public uint StartTPID;
        public double StartLinePos;
        public double StartRoutePos;
        public Point EndPoint;
        public uint EndTPID;
        public double EndLinePos;
        public double EndRoutePos;
        public CHE_Direction eCD;
        

        public void ChangeEndPoint(Point opint)
        {
            EndPoint = opint;
        }

        public void ChangeStartPoint(Point opint)
        {
            StartPoint = opint;
        }

        public void ChangeStartTPID(uint num)
        {
            StartTPID = num;
        }

        public void ChangeEndTPID(uint num)
        {
            EndTPID = num;
        }

        public void ChangeEndlinePos(double num)
        {
            EndLinePos = num;
        }

        public void ChangeStartRoutePos(double num)
        {
            StartRoutePos = num;
        }

        public void ChangeEndRoutePos(double num)
        {
            EndRoutePos = num;
        }

        public void ChangeID(uint num)
        {
            ID = num;
        }

        public void ChangeStartlinePos(double num)
        {
            StartLinePos = num;
        }
    }

    /// <summary>
    /// 集装箱信息
    /// </summary>
    public class SimContainerInfo : IDefinitionCompletenessSelfCheckable
    {
        public string ContainerID;
        public Move_Kind MoveKind;
        public string ISO;
        public StatusEnums.ContSize eSize;
        public StatusEnums.ContType eType;
        public StatusEnums.EF eEF;
        public int TareWeight;
        public int GrossWeight;
        public int DoorDirection;
        // 装货（按华东）
        public string PortOfLoad;
        // 卸货（按华东）
        public string PortOfTrans;
        // 目的（按华东）
        public string PortOfDisc;

        public string YardLoc;
        public string YardBlock;
        public int YardBay;
        public int YardRow;
        public int YardTier;

        public uint VesNo;
        public uint VoyageNo;

        public string VesLoc;
        public int VesBay;
        public int VesRow;
        public int VesTier;

        public string PlanLoc;
        public string PlanBlock;
        public int PlanBay;
        public int PlanRow;
        public int PlanTier;

        public String StowLoc;
        public int StowBay;
        public int StowRow;
        public int StowTier;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public SimContainerInfo()
        {
            this.ContainerID = "";
            this.ISO = "";
            this.PortOfLoad = "";
            this.PortOfDisc = "";
            this.PortOfTrans = "";
            this.YardLoc = "";
            this.VesLoc = "";
            this.PlanLoc = "";
            this.StowLoc = "";
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;

            if (string.IsNullOrWhiteSpace(this.ContainerID) || string.IsNullOrWhiteSpace(this.ISO) || this.eSize == StatusEnums.ContSize.Unknown
                || this.eType == StatusEnums.ContType.Unknown || this.eEF == StatusEnums.EF.Unknown || this.TareWeight <= 0 || this.GrossWeight <= 0
                || string.IsNullOrWhiteSpace(this.PortOfDisc) || string.IsNullOrWhiteSpace(this.PortOfLoad) || string.IsNullOrWhiteSpace(this.PortOfTrans))
                bRet = false;
            switch (this.MoveKind)
            {
                case Move_Kind.DSCH:
                    if (string.IsNullOrWhiteSpace(this.VesLoc) || this.VesNo == 0 || this.VoyageNo == 0
                        || this.VesBay <= 0 || this.VesRow < 0 || this.VesTier <= 0)
                        bRet = false;
                    break;
                case Move_Kind.LOAD:
                    if (string.IsNullOrWhiteSpace(this.YardLoc) || string.IsNullOrWhiteSpace(this.YardBlock)
                        || this.VesNo == 0 || this.VoyageNo == 0 || this.YardBay <= 0 || this.YardRow <= 0 || this.YardTier <= 0)
                        bRet = false;
                    break;
                default:
                    break;
            }
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// AGV移动的期望时间
    /// </summary>
    [Serializable]
    public class SimExpectTime : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public uint fromID;
        public uint toID;
        public int expectTime;
        public LaneToLaneType l2LType;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public SimExpectTime()
        {
        }

        public void DependentDefinition()
        {
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || this.fromID == 0 || this.toID == 0 || this.expectTime < 0) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 磁钉
    /// </summary>
    [Serializable]
    public class SimTransponder : Transponder, IDefinitionCompletenessSelfCheckable
    {
        //public DeviceType deviceType;
        //public uint ID;                                 // 磁钉编号
        //public float PhysicalPosX;                      // 实际X坐标
        //public float PhysicalPosY;                      // 实际Y坐标
        //public int LogicPosX;                           // 逻辑X坐标
        //public int LogicPosY;                           // 逻辑Y坐标
        //public uint HorizontalLineID;                   // 水平线号
        //public uint VerticalLineID;                     // 垂直线号
        //public AreaType AreaType;                       // 所在区域类型
        //public int AreaNo;                              // 所在区域编号
        //public String LaneNo;                           // 车道编号
        //public bool Enabled;                            // 是否可用
        //public ushort NoStop;                           // 不可停车点
        public int Zindex;
        public string Name;
        public uint LaneID;
        public uint MateID;
        public bool bIfEssential;
        public Dictionary<uint, StatusEnums.RouteTPDivision> dRouteTPDivisions;  // 是否属于某 AGV 的路径，以及其属于路径的哪个分段。

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public SimTransponder()
            : base()
        {
            this.Zindex = 4;
            this.Name = "";
            this.dRouteTPDivisions = new Dictionary<uint, StatusEnums.RouteTPDivision>();
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.PhysicalPosX <= 0 || this.PhysicalPosY <= 0 || this.LogicPosX <= 0 || this.LogicPosY <= 0 
                || string.IsNullOrWhiteSpace(this.Name))
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 单箱位柔性存放单元，允许预约与存放不一致。用于箱堆
    /// </summary>
    public class SingleFlexStoreUnit
    {
        // 对箱
        public string ReserveContID { get; set; }
        public string StoreContID { get; set; }
        public StatusEnums.ContSize eReserveContSize { get; set; }
        public StatusEnums.ContSize eStoreContSize { get; set; }
        // 对单元
        public StatusEnums.StoreType eUnitStoreType { get; set; }
        public StatusEnums.StoreStage eUnitStoreStage { get; set; }
        public StatusEnums.StoreType eStoreStatus { get; set; }
        public StatusEnums.StoreType eReserveStatus { get; set; }
        public double DefaultLength { get; set; }
        public double DefaultHeight { get; set; }

        public void Reset()
        {

        }
    }


    /// <summary>
    /// 单箱位刚性存放单元，对应场桥小车
    /// </summary>
    public class SingleRigidStoreUnit : IRigidStoreUnit, IDefinitionCompletenessSelfCheckable
    {
        // 相关箱箱号、尺寸与状态
        public string ContID { get; set; }
        public StatusEnums.ContSize eContSize { get; set; }
        public StatusEnums.StoreStage eContStoreStage { get; set; }
        // 单元状态
        public StatusEnums.StoreType eUnitStoreType { get; set; }
        public StatusEnums.StoreStage eUnitStoreStage { get; set; }
        public double DefaultLength { get; set; }
        public double DefaultHeight { get; set; }

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public SingleRigidStoreUnit()
        {
            this.ContID = "";
            this.eContStoreStage = StatusEnums.StoreStage.None;
            this.DefaultLength = 7;
            this.DefaultHeight = 3;
            this.UnitStatusRenew();
        }

        // 刷新 eReserveStatus 和 eStoreStatus
        public void UnitStatusRenew()
        {
            if (string.IsNullOrWhiteSpace(this.ContID))
            {
                this.eUnitStoreType = StatusEnums.StoreType.Empty;
                this.eUnitStoreStage = StatusEnums.StoreStage.None;
            }
            else
            {
                this.eUnitStoreStage = this.eContStoreStage;
                switch (this.eContSize)
                {
                    case StatusEnums.ContSize.TEU:
                        this.eUnitStoreType = StatusEnums.StoreType.STEU;
                        break;
                    case StatusEnums.ContSize.FEU:
                        this.eUnitStoreType = StatusEnums.StoreType.FEU;
                        break;
                    case StatusEnums.ContSize.FFEU:
                        this.eUnitStoreType = StatusEnums.StoreType.FFEU;
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// 判断集装箱是否能预约本存储单元。注意本函数不关注箱号和尺寸的对应关系。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <param name="SizeStr">尺寸</param>
        /// <returns>能够预约返回true，不能预约返回false</returns>
        public bool IfContReservable(string ContID, string SizeStr)
        {
            StatusEnums.ContSize eContSize = StatusEnums.GetContSize(SizeStr);
            if (eContSize == StatusEnums.ContSize.Unknown)
                return false;

            bool bRet = false;

            if (this.CheckContStoreStatus(ContID) == StatusEnums.StoreStage.None) 
                bRet = true;

            return bRet;
        }

        /// <summary>
        /// 判断集装箱是否能占据本存储单元。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <param name="SizeStr">尺寸</param>
        /// <returns>能够占据返回true，不能返回false</returns>
        public bool IfContOccupiable(string ContID)
        {
            bool bRet = false;

            if (this.CheckContStoreStatus(ContID) == StatusEnums.StoreStage.Reserved)
                bRet = true;

            return bRet;
        }

        /// <summary>
        /// 判断集装箱是否能够移出本存储单元。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <returns>能够移出返回true，不能返回false</returns>
        public bool IfContRemovable(string ContID)
        {
            bool bRet = false;

            if (this.CheckContStoreStatus(ContID) == StatusEnums.StoreStage.Stored)
                bRet = true;

            return bRet;
        }

        /// <summary>
        /// 集装箱预约本存储单元。注意本函数不关注箱号和尺寸的对应关系。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <param name="SizeStr">尺寸</param>
        /// <returns>预约成功返回true，失败返回false</returns>
        public bool ContReserve(string ContID, string SizeStr)
        {
            if (!this.IfContReservable(ContID, SizeStr))
                return false;

            this.ContID = ContID;
            this.eContSize = StatusEnums.GetContSize(SizeStr);
            this.eContStoreStage = StatusEnums.StoreStage.Reserved;

            this.UnitStatusRenew();

            return true;
        }

        /// <summary>
        /// 集装箱占据本存储单元。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool ContOccupy(string ContID)
        {
            if (!this.IfContOccupiable(ContID))
                return false;

            this.eContStoreStage = StatusEnums.StoreStage.Stored;
            this.UnitStatusRenew();

            return true;
        }

        /// <summary>
        /// 集装箱移出本存储单元
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool ContRemove(string ContID)
        {
            if (!this.IfContRemovable(ContID))
                return false;

            this.ContID = "";
            this.eContSize = StatusEnums.ContSize.Unknown;
            this.eContStoreStage = StatusEnums.StoreStage.None;
            this.UnitStatusRenew();

            return true;
        }

        /// <summary>
        /// 检查集装箱在本存储单元的状态
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <returns>状态值</returns>
        public StatusEnums.StoreStage CheckContStoreStatus(string ContID)
        {
            StatusEnums.StoreStage eStoreStage = StatusEnums.StoreStage.None;

            if (this.ContID == ContID)
                eStoreStage = this.eContStoreStage;

            return eStoreStage;
        }

        /// <summary>
        /// 重置
        /// </summary>
        public void Reset()
        {
            this.ContID = "";
            this.eContSize = StatusEnums.ContSize.Unknown;
            this.eContStoreStage = StatusEnums.StoreStage.None;
            this.UnitStatusRenew();
        }

        /// <summary>
        /// 定义完整性检查
        /// </summary>
        /// <returns>完整返回true，否则返回false</returns>
        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.DefaultHeight <= 0 || this.DefaultLength <= 0)
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// QC用集装箱状态记录
    /// </summary>
    public class QCContStageRec
    {
        public STS_ResJob oResJob;
        public WORK_INSTRUCTION_STATUS oWI;
        public StatusEnums.QCContStage eQCContLocType;
    }
     
    /// <summary>
    /// 双箱位刚性存放单元，可以对应支架、AGV、岸桥小车和岸桥平台槽。默认总是先使用1号位。默认单元内箱号唯一。
    /// </summary>
    public class TwinRigidStoreUnit : IRigidStoreUnit, IDefinitionCompletenessSelfCheckable
    {
        // 相关箱，箱号、尺寸和状态
        public string ContID1 { get; set; }
        public StatusEnums.ContSize eContSize1 { get; set; }
        public StatusEnums.StoreStage eContStoreStage1 { get; set; }
        public string ContID2 { get; set; }
        public StatusEnums.ContSize eContSize2 { get; set; }
        public StatusEnums.StoreStage eContStoreStage2 { get; set; }
        // 单元，预约状态和储存状态
        public StatusEnums.StoreType eUnitStoreType { get; set; }
        public StatusEnums.StoreStage eUnitStoreStage { get; set; }
        public double DefaultLength { get; set; }
        public double DefaultHeight { get; set; }

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public TwinRigidStoreUnit()
        {
            this.ContID1 = "";
            this.ContID2 = "";
            this.DefaultLength = 7;
            this.DefaultHeight = 3;
            this.UnitStatusRenew();
        }

        // 更新 eCarryStatus 和 eReserveStatus 两个属性
        private void UnitStatusRenew()
        {
            if (string.IsNullOrWhiteSpace(this.ContID1) && string.IsNullOrWhiteSpace(this.ContID2))
            {
                this.eUnitStoreStage = StatusEnums.StoreStage.None;
                this.eUnitStoreType = StatusEnums.StoreType.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(this.ContID1) && string.IsNullOrWhiteSpace(this.ContID2))
            {
                this.eUnitStoreType = StatusEnums.StoreType.STEU;
                this.eUnitStoreStage = this.eContStoreStage2;
            }
            else if (string.IsNullOrWhiteSpace(this.ContID1) && !string.IsNullOrWhiteSpace(this.ContID2))
            {
                this.eUnitStoreType = StatusEnums.StoreType.STEU;
                this.eUnitStoreStage = this.eContStoreStage1;
            }
            else
            {
                if (this.ContID1 == this.ContID2)
                {
                    this.eUnitStoreStage = this.eContStoreStage1;
                    switch (this.eContSize1)
                    {
                        case StatusEnums.ContSize.FEU:
                            this.eUnitStoreType = StatusEnums.StoreType.FEU;
                            break;
                        case StatusEnums.ContSize.FFEU:
                            this.eUnitStoreType = StatusEnums.StoreType.FFEU;
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    // 俩20尺箱。有一个Store时本Unit就算是Stored
                    this.eUnitStoreType = StatusEnums.StoreType.DTEU;
                    if (this.eContStoreStage1 == StatusEnums.StoreStage.Reserved && this.eContStoreStage2 == StatusEnums.StoreStage.Reserved)
                        this.eUnitStoreStage = StatusEnums.StoreStage.Reserved;
                    else
                        this.eUnitStoreStage = StatusEnums.StoreStage.Stored;
                }
            }
        }

        /// <summary>
        /// 判断箱子是否能够预约。注意函数并不核对箱号和尺寸的关系。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <param name="SizeStr">尺寸</param>
        /// <returns>能够预约成功返回true，不能返回false</returns>
        public bool IfContReservable(string ContID, string SizeStr)
        {
            bool bRet = true;
            StatusEnums.ContSize eContSize = StatusEnums.GetContSize(SizeStr);

            switch (eContSize)
            {
                case (StatusEnums.ContSize.TEU):
                    if (string.IsNullOrWhiteSpace(this.ContID1) || string.IsNullOrWhiteSpace(this.ContID2)) 
                        bRet = true;
                    else
                        bRet = false;
                    break;
                case (StatusEnums.ContSize.FEU):
                case (StatusEnums.ContSize.FFEU):
                    if (string.IsNullOrWhiteSpace(this.ContID1) && string.IsNullOrWhiteSpace(this.ContID2)) 
                        bRet = true;
                    else 
                        bRet = false;
                    break;
                default:
                    bRet = false;
                    break;
            }

            return bRet;
        }

        /// <summary>
        /// 判断箱子是否能够占据
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <param name="SizeStr">尺寸</param>
        /// <returns>能够占据成功返回true，不能返回false</returns>
        public bool IfContOccupiable(string ContID)
        {
            bool bRet;

            if (this.CheckContStoreStatus(ContID) == StatusEnums.StoreStage.Reserved)
                bRet = true;
            else 
                bRet = false;

            return bRet;
        }

        /// <summary>
        /// 判断箱子是否可以移除
        /// </summary>
        /// <param name="ContID"></param>
        /// <param name="SizeStr"></param>
        /// <returns></returns>
        public bool IfContRemovable(string ContID)
        {
            bool bRet;

            if (this.CheckContStoreStatus(ContID) == StatusEnums.StoreStage.Stored)
                bRet = true;
            else 
                bRet = false;

            return bRet;
        }

        /// <summary>
        /// 集装箱预约本存储单元。注意函数并不核对箱号和尺寸的关系。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <param name="SizeStr">尺寸</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool ContReserve(string ContID, string SizeStr)
        {
            if (!this.IfContReservable(ContID, SizeStr))
                return false;

            bool bRet = true;

            StatusEnums.ContSize eContSize = StatusEnums.GetContSize(SizeStr);
            switch (eContSize)
            {
                case (StatusEnums.ContSize.TEU):
                    if (string.IsNullOrWhiteSpace(this.ContID1) && this.eContStoreStage1 == StatusEnums.StoreStage.None)
                    {
                        this.ContID1 = ContID;
                        this.eContSize1 = eContSize;
                        this.eContStoreStage1 = StatusEnums.StoreStage.Reserved;
                    }
                    else if (string.IsNullOrWhiteSpace(this.ContID2) && this.eContStoreStage2 == StatusEnums.StoreStage.None)
                    {
                        this.ContID2 = ContID;
                        this.eContSize2 = eContSize;
                        this.eContStoreStage2 = StatusEnums.StoreStage.Reserved;
                    }
                    else 
                        bRet = false;
                    break;
                case (StatusEnums.ContSize.FEU):
                case (StatusEnums.ContSize.FFEU):
                    if (string.IsNullOrWhiteSpace(this.ContID1) && this.eContSize1 == StatusEnums.ContSize.Unknown && this.eContStoreStage1 == StatusEnums.StoreStage.None
                        && string.IsNullOrWhiteSpace(this.ContID2) && this.eContSize2 == StatusEnums.ContSize.Unknown && this.eContStoreStage2 == StatusEnums.StoreStage.None)
                    {
                        this.ContID1 = ContID;
                        this.eContSize1 = eContSize;
                        this.eContStoreStage1 = StatusEnums.StoreStage.Reserved;
                        this.ContID2 = ContID;
                        this.eContSize2 = eContSize;
                        this.eContStoreStage2 = StatusEnums.StoreStage.Reserved;
                    }
                    else 
                        bRet = false;
                    break;
                default:
                    bRet = false;
                    break;
            }

            if (bRet)
                this.UnitStatusRenew();

            return bRet;
        }

        /// <summary>
        /// 集装箱占据本存储单元。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <param name="SizeStr">尺寸</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool ContOccupy(string ContID)
        {
            if (this.CheckContStoreStatus(ContID) != StatusEnums.StoreStage.Reserved)
                return false;

            bool bRet = false;

            if (this.ContID1 == ContID && this.eContStoreStage1 == StatusEnums.StoreStage.Reserved)
            {
                this.eContStoreStage1 = StatusEnums.StoreStage.Stored;
                bRet = true;
            }
            if (this.ContID2 == ContID && this.eContStoreStage2 == StatusEnums.StoreStage.Reserved)
            {
                this.eContStoreStage2 = StatusEnums.StoreStage.Stored;
                bRet = true;
            }

            if (bRet) 
                this.UnitStatusRenew();

            return bRet;
        }

        /// <summary>
        /// 集装箱脱离本存储单元。
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <param name="SizeStr">尺寸</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool ContRemove(string ContID)
        {
            if (this.CheckContStoreStatus(ContID) != StatusEnums.StoreStage.Stored)
                return false;

            bool bRet = false;

            if (this.ContID1 == ContID && this.eContStoreStage1 == StatusEnums.StoreStage.Stored)
            {
                this.ContID1 = "";
                this.eContSize1 = StatusEnums.ContSize.Unknown;
                this.eContStoreStage1 = StatusEnums.StoreStage.None;
                bRet = true;
            }
            if (this.ContID2 == ContID && this.eContStoreStage2 == StatusEnums.StoreStage.Stored)
            {
                this.ContID2 = "";
                this.eContSize2 = StatusEnums.ContSize.Unknown;
                this.eContStoreStage2 = StatusEnums.StoreStage.None;
                bRet = true;
            }

            if (bRet) 
                this.UnitStatusRenew();

            return bRet;
        }

        /// <summary>
        /// 检查某箱在单元内的状态
        /// </summary>
        /// <param name="ContID">箱号</param>
        /// <returns>无关，预约或者储存</returns>
        public StatusEnums.StoreStage CheckContStoreStatus(string ContID)
        {
            StatusEnums.StoreStage eStoreStage = StatusEnums.StoreStage.None;

            if (this.ContID1 == ContID)
                eStoreStage = this.eContStoreStage1;
            else if (this.ContID2 == ContID)
                eStoreStage = this.eContStoreStage2;

            return eStoreStage;
        }

        /// <summary>
        /// 重置
        /// </summary>
        public void Reset()
        {
            this.ContID1 = "";
            this.eContStoreStage1 = StatusEnums.StoreStage.None;
            this.eContSize1 = StatusEnums.ContSize.Unknown;
            this.ContID2 = "";
            this.eContStoreStage2 = StatusEnums.StoreStage.None;
            this.eContSize2 = StatusEnums.ContSize.Unknown;
            this.UnitStatusRenew();
        }

        /// <summary>
        /// 定义完整性检查
        /// </summary>
        /// <returns>完整返回true，不完整返回false</returns>
        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.DefaultHeight <= 0 || this.DefaultLength <= 0)
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 码头区域
    /// </summary>
    public class TerminalRegion : IDefinitionCompletenessSelfCheckable
    {
        public double X;
        public double Y;
        public double Width;
        public double LandHeight;
        public double WaterHeight;
        public int Zindex;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public TerminalRegion()
        {
            this.Zindex = 0;
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.X < 0 || this.Y < 0 || this.Width <= 0 || this.LandHeight <= 0 || this.WaterHeight <= 0)
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 路径磁钉信息
    /// </summary>
    public class TPInfoEnRoute
    {
        public uint TPID;
        public double RoutePos;
        public uint EnterLaneID;
        public uint ExitLaneID;
        public bool IfPassed;       // 这个的目标是不用
        public StatusEnums.RouteTPDivision eRouteTPDivision;
        public bool IsUnSurpassable;

        public TPInfoEnRoute()
        {
        }

        public TPInfoEnRoute(uint TPID, double RoutePos)
            : this()
        {
            this.TPID = TPID;
            this.RoutePos = RoutePos;
        }

        public TPInfoEnRoute(uint TPID, CHE_Direction eCD, double StartRoutePos, double StartRouteFeaturePos, double TPFeaturePosX, double TPFeaturePosY)
            :this()
        {
            this.TPID = TPID;
            switch (eCD)
            {
                case CHE_Direction.East:
                    this.RoutePos = StartRoutePos + (TPFeaturePosX - StartRouteFeaturePos);
                    break;
                case CHE_Direction.West:
                    this.RoutePos = StartRoutePos + (StartRouteFeaturePos - TPFeaturePosX);
                    break;
                case CHE_Direction.South:
                    this.RoutePos = StartRoutePos + (TPFeaturePosY - StartRouteFeaturePos);
                    break;
                case CHE_Direction.North:
                    this.RoutePos = StartRoutePos + (StartRouteFeaturePos - TPFeaturePosY);
                    break;
            }
        }
    }

    /// <summary>
    /// 船舶
    /// </summary>
    public class Vessel : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public string Name;
        public string ShipName;
        public string ShipCode;
        public StatusEnums.BerthWay eBerthWay;
        public double BeginMeter;
        public double EndMeter;
        public double ExpBeginMeter;     // Exp 中的船位置原点与磁钉位置原点不一致。
        public double ExpEndMeter;
        public double YAppend;
        public string BowBollard;
        public string SternBollard;
        public int BowBollardOffset;
        public int SternBollardOffset;
        public VesselType oType;
        public StatusEnums.VesselVisitPhrase eVesselVisitPhrase;
        public DateTime Updated;
        public int ZIndex;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public Vessel()
        {
            this.Name = "";
            this.ShipName = "";
            this.ShipCode = "";
            this.ZIndex = 1;
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || string.IsNullOrWhiteSpace(this.Name) || string.IsNullOrWhiteSpace(this.ShipName) || string.IsNullOrWhiteSpace(this.ShipCode)
                || this.eBerthWay == StatusEnums.BerthWay.Null || this.BeginMeter < 0 || this.EndMeter < 0 || this.ExpBeginMeter <= 0 || this.ExpEndMeter <= 0 || this.oType == null) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 船型
    /// </summary>
    public class VesselType : IDefinitionCompletenessSelfCheckable
    {
        public uint VesNo;
        public double Length;
        public double Width;
        public double BowSpaceLen;
        public double SternSpaceLen;
        public double CabinRange;
        public double SingleCabinLength;
        public int CabinNum;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.VesNo == 0 || this.Length <= 0 || this.BowSpaceLen <= 0 || this.SternSpaceLen <= 0
                || this.CabinRange <= 0 || this.CabinNum == 0 || this.SingleCabinLength <= 0) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }

    /// <summary>
    /// 航次
    /// </summary>
    public class Voyage : IDefinitionCompletenessSelfCheckable
    {
        public uint ID;
        public string Name;
        public uint VesID;
        public string IE;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public Voyage()
        {
            this.Name = "";
            this.IE = "";
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (this.ID == 0 || string.IsNullOrWhiteSpace(this.Name) || this.VesID == 0 || string.IsNullOrWhiteSpace(this.IE))
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }

    }

    /// <summary>
    /// 堆场串空间
    /// </summary>
    public class YardSlot : IDefinitionCompletenessSelfCheckable
    {
        public string Name;
        public string BlockName;
        public int Bay;
        public int Row;
        public string PileNameOnSlot;
        public Point BasePointUL;
        public bool Cont45Permitted;

        private bool isDefinitionCompleted;
        public bool IsDefinitionCompleted
        {
            get { return isDefinitionCompleted; }
            set { }
        }

        public YardSlot()
        {
            this.Name = "";
            this.BlockName = "";
            this.PileNameOnSlot = "";
            this.BasePointUL = new Point();
        }

        public bool CheckDefinitionCompleteness()
        {
            bool bRet = true;
            if (string.IsNullOrWhiteSpace(this.Name) || string.IsNullOrWhiteSpace(this.BlockName) 
                || this.Bay <= 0 || this.Row <= 0 || this.BasePointUL == null || this.BasePointUL.X == 0 || this.BasePointUL.Y == 0) 
                bRet = false;
            this.isDefinitionCompleted = bRet;
            return bRet;
        }
    }



    /// <summary>
    /// 仿真驱动令牌
    /// </summary>
    public class SimToken : Entity
    {
        public string Type;

        public SimToken(int id)
            :base(id)
        {
            this.identifier = id;
        }

        public SimToken(int id, string Type)
            : this(id)
        {
            this.Type = Type;
        }

    }



}
