using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSWPF.Define
{
    public class StatusEnums
    {
        // 枚举结构
        public enum StoreType : byte { Empty = 0, STEU = 1, DTEU = 2, FEU = 3, FFEU = 4 };

        public enum StoreStage : byte { None = 0, Reserved = 1, Stored = 2 };

        public enum ContSize : byte { Unknown = 0, TEU = 1, FEU = 2, FFEU = 3 };

        public enum ContType : byte { Unknown = 0, GP = 1, HC = 2, HQ = 3, OT = 4, RF = 5 };

        public enum EF : byte { Unknown = 0, E = 1, F = 2 };

        public enum CraneType : byte { Null = 0, QC = 1, ASC = 2 };

        public enum BerthWay : byte { Null = 0, L = 1, R = 2 };

        public enum IndexType : byte { Null = 0, JobID = 1, TaskID = 2, STSOrdComm = 3, ASCOrdComm = 4, AGVOrdComm = 5 };

        public enum ConsCapaStatus : byte { Null = 0, CapacityLeft = 1, NoCapacityLeft = 2, CapacityExceeded = 3 };

        public enum ConsType : byte { Null = 0, OpenCycle = 1, ClosedCycle = 2, ReversedSegsPair = 3 };

        public enum ProjectType : byte { Null = 0, Create = 1, Renew = 2, Refresh = 3, Delete = 4, Reset = 5 };

        public enum ClickType : byte { Null = 0, Init = 1, Stop = 2, Reset = 3, Start = 4, Step = 5 }

        public enum RouteTPDivision : byte { Detect = 0, Try = 1, Claim = 2, Passed = 3 }

        // 刷新类型，InfoFrame专用
        public enum ListViewType : byte
        {
            Null = 0, WorkQueue = 1, WorkInstruction = 2, BerthStatus = 3, PartialOrder = 4,
            STSResJob = 10, STSTask = 11, STSOrder = 12, STSCommand = 13, STSStatus = 14,
            ASCResJob = 20, ASCTask = 21, ASCOrder = 22, ASCCommand = 23, ASCStatus = 24,
            AGVResJob = 30, AGVTask = 31, AGVOrder = 32, AGVCommand = 33, AGVStatus = 34
        };

        // 设备状态部分

        public enum AGVWorkStage : byte
        {
            Null = 0, ToWSTP = 1, AtWSTP = 2, ToQCTP = 3, AtQCTP = 4,
            ToWSPBIn = 5, AtWSPBIn = 6, ToWSPBOut = 7, AtWSPBOut = 8, ToWSPB = 9, AtWSPB = 10,
            ToQCPBIn = 11, AtQCPBIn = 12, ToQCPBOut = 13, AtQCPBOut = 14, ToQCPB = 15, AtQCPB = 16
        };

        public enum AGVSubWorkStage : byte
        {
            Null = 0, FirstSub = 1, MidSub = 2, LastSub = 3
        };

        // 用于设备的每步状态，区分推进和暂停(到达目标的暂停和没到目标的暂停)
        public enum StepTravelStatus : byte { Null = 0, Move = 1, Wait = 2 };                               

        // 用于动作的状态
        public enum ActionStatus : byte { Null = 0, Ready = 1, Doing = 2, Done = 3 };

        // 用于设备的阶段状态: 无任务、可分配任务、正在移动、正在等待、正在作业、任务完成
        // Free 时可以接受下一任务，Done仅表示当前任务完成
        public enum MotionStatus : byte { Free = 0, Ready = 1, Moving = 2, Waiting = 3, Working = 4, Done = 5 };       

        public enum QCTrolleyStage : byte
        {
            BHigh = 0, BFall = 1, BLow = 2, BRise = 3,
            BToLS = 4, LSHigh = 5, LSFall = 6, LSLow = 7, LSRise = 8, LSToB = 9, 
            BToWS = 10, WSHigh = 11, WSFall = 12, WSLow = 13, WSRise = 14, WSToB = 15
        };

        public enum QCMainTrolleySubProc : byte
        { 
            Null = 0, Ready = 1, Done = 2, Maintain = 3,
            LoadContNormal = 10, DiscContNormal = 11, LoadContFromApron = 12, DiscContToApron = 13, LoadCover = 14, DiscCover = 15, ChangeSpreader = 16
        };

        public enum QCViceTrolleySubProc : byte { Null = 0, Ready = 1, Done = 2, Maintain = 3, LoadContNormal = 10, DiscContNormal = 11 };

        public enum ASCTrolleyStage : byte { HighFree = 0, HighMove = 1, HighArrived = 2, Fall = 3, Low = 4, Rise = 5 };

        public enum QCContStage : byte { Null = 0, Vessel = 1, MainTro = 2, PlatformConfirm = 3, Platform = 4, ViceTro = 5, AGV = 6 };

        public enum ASCSubProc : byte { Free = 0, PickUp = 1, Delivery = 2 };

        public enum ASCSide : byte { Null = 0, WS = 1, LS = 2 };

        public enum SimPhrase : byte { None = 0, Initing = 1, InitDone = 2, InitError = 3, Running = 4, RunError = 5, Stopping = 6, Stopped = 7, Stepping = 8, Terminated = 9, Reseting = 10 };

        public enum MateStatus : byte { Normal = 0, RisedUp = 1, Opened = 2 };

        public enum STSVisitDir : byte { Null = 0, Clockwise = 0, AntiClockwise = 1 };

        public enum AxisDir : byte { X = 0, Y = 1 };

        public enum VesselVisitPhrase : byte { Null = 0, Forecasted = 1, InPortArriving = 2, AtBerthPreparing = 3, AtBerthDoing = 4, AtBerthDone = 5, InPortLeaving = 6, Departed = 7 };

        public static ContSize GetContSize(string Str)
        {
            ContSize eContsize;
            if (Str == "20" || Str == "2210" || Str == "TEU") 
                eContsize = ContSize.TEU;
            else if (Str == "40" || Str == "4510" || Str == "4210" || Str == "FEU") 
                eContsize = ContSize.FEU;
            else if (Str == "45" || Str == "9510" || Str == "FFEU") 
                eContsize = ContSize.FFEU;
            else 
                eContsize = ContSize.Unknown;
            return eContsize;
        }

        public static bool IsInited(SimPhrase eSimPhrase)
        {
            switch (eSimPhrase)
            {
                case SimPhrase.None:
                case SimPhrase.Initing:
                case SimPhrase.InitError:
                    return false;
                default:
                    return true;
            }
        }

        public static bool IsStarted(SimPhrase eSimPhrase)
        {
            switch (eSimPhrase)
            {
                case SimPhrase.Running:
                case SimPhrase.RunError:
                case SimPhrase.Stepping:
                case SimPhrase.Stopped:
                case SimPhrase.Stopping:
                case SimPhrase.Terminated:
                    return true;
                default:
                    return false;
            }
        }
    }
}
