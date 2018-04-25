using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; 
using System.Reflection;
using System.IO;
using System.Xml.Serialization;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.CiTOS;

namespace ZECS.Schedule.DBDefine.NewBlockInfo
{
    [Serializable]
    public enum ContainerDoorDirection
    {
        None = 0,
        Positive = 1,
        Negative = 2
    }

    //[Serializable]
    //public enum SpreaderSize
    //{
    //    Null = 0,
    //    TwentyFeets,
    //    FortyFeets,
    //    FortyFiveFeets,
    //    DoubleTwenty,
    //    Motion
    //}

    [Serializable]
    public class ContainerMask
    {
        public string CONTAINER_STOW_FACTOR { get; set; }//String(25)
        public double CONTAINER_WEIGHT_MARGIN_KG { get; set; }

        public ContainerMask(string factor,double weightMargin)
        {
            CONTAINER_STOW_FACTOR = factor;
            CONTAINER_WEIGHT_MARGIN_KG = weightMargin;
        }

        public bool IsEqual(ContainerMask mask)
        {
            if(CONTAINER_STOW_FACTOR == mask.CONTAINER_STOW_FACTOR
                && Math.Abs(CONTAINER_WEIGHT_MARGIN_KG- mask.CONTAINER_WEIGHT_MARGIN_KG) < 0.00001)
                return true;
            else
                return false;
        }

        public void Copy(ContainerMask mask)
        {
            CONTAINER_STOW_FACTOR = mask.CONTAINER_STOW_FACTOR;
            CONTAINER_WEIGHT_MARGIN_KG = mask.CONTAINER_WEIGHT_MARGIN_KG;
        }
    }

    [Serializable]
    public class ContainerSize
    {
        // 集装箱宽度(20/40/45) mm 
        public const int CTN_WIDTH = 2350;
        // 20尺集装箱长度 mm 
        public const int CTN_20_LENGTH = 6060;
        // 40尺集装箱长度 mm 
        public const int CTN_40_LENGTH = 12200;
        // 45尺集装箱长度 mm 
        public const int CTN_45_LENGTH = 13715;

        public const String Code_20 = "20";
        public const String Code_40 = "40";
        public const String Code_45 = "45";

        public String Code;

        public ContainerSize(String sizeCode)
        {
            Code = sizeCode;
        }
    }

    [Serializable]
    public enum BmsBayType
    {
        None = 0,
        LS = 1,
        Block = 2,
        WS = 3
    }

    [Serializable]
    public enum BmsBaySize
    {
        None = 0,
        CTN_20 = 1,
        CTN_40 = 2,
        CTN_45 = 3
    }

    [Serializable]
    public class BmsPosition
    {
        public String  BlockName; 
        public BmsBayType BayType;   

        public int Bay; 
        public int Lane;  
        public int Tier;

        public BmsPosition()
        {

        }

        public BmsPosition(BmsPosition pos)
        {
            if (pos != null)
            {
                Bay = pos.Bay;
                Lane = pos.Lane;
                Tier = pos.Tier;
                BayType = pos.BayType;
                BlockName = pos.BlockName;
            }
        }

         public BmsPosition(String blockName, BmsBayType bayType, int bay, int lane, int tier)
         {
             Bay = bay;
             Lane = lane;
             Tier = tier;
             BayType = bayType;
             BlockName = blockName;
         }

        public override string ToString()
        {
            return BlockName + "-" + BayType + "-" + Bay + "-" + Lane + "-" + Tier;
        }
    }

    [Serializable]
    public class BlockCoordinate
    {
        public int GantryPosition;
        public int TrolleyPosition;
        public int HoistPosition; 
    }

    [Serializable]
    public class BmsPositionEx : BmsPosition
    {
        public BlockCoordinate Coordinate = new BlockCoordinate();
   
        public BmsPositionEx(BmsPosition pos, int gantryPos, int trolleyPos, int hoistPos)
            : base(pos)
        {
            Coordinate.GantryPosition = gantryPos;
            Coordinate.TrolleyPosition = trolleyPos;
            Coordinate.HoistPosition = hoistPos;
        }

        public BmsPositionEx(String blockName, BmsBayType bayType,
            int bay, int lane, int tier, int gantryPos, int trolleyPos, int hoistPos)
            : base( blockName, bayType, bay, lane, tier)
        {
            Coordinate.GantryPosition = gantryPos;
            Coordinate.TrolleyPosition = trolleyPos;
            Coordinate.HoistPosition = hoistPos;
        }
    }

    [Serializable]
    public class ContainerInfo
    {
        public String ContainerID;
        public String ISO;
        public BmsPosition Location;
        public ContainerSize Size;
        public int Weight;
        public ContainerDoorDirection DoorDirection;
        public int Color;
        public ContainerMask Mask;

        public ContainerInfo(String containerID, String iso, String sizeCode, int weight, 
                             int doorDirection, int color, int bay, int lane, int tier, ContainerMask mask)
        {
            ContainerID = containerID;
            ISO = iso;
            Location = new BmsPosition(null, BmsBayType.None, bay, lane, tier);
            Size = new ContainerSize(sizeCode);
            Weight = weight;
            DoorDirection = (ContainerDoorDirection)doorDirection;
            Color = color;
            Mask.Copy(mask);
        }
    }

    [Serializable]
    public class Lane
    {
        public int LaneNo;
        public String LaneID;
        public int Coordinate;
        public int MaxTier;
    }

    [Serializable]
    public class WSLane : Lane
    {
        public bool IsMate;
    }

    [Serializable]
    public class Bay
    {
        public int BayNo;
        public BmsBayType BayType = BmsBayType.None;
        public BmsBaySize BaySize = BmsBaySize.None;
        public int Coordinate;
        public List<Lane> LaneList;
    }
     
    /// <summary>
    /// SimpleBlockInfo
    /// </summary>
    [Serializable]
    public class SimpleBlockInfo
    {
        Dictionary<String ,ContainerInfo> m_htContainer;
        string m_blockID;
        TimeSpan m_weightValue = TimeSpan.Zero;
        public SimpleBlockInfo(string blockID)
        {
            m_blockID = blockID;
            m_htContainer = new Dictionary<String, ContainerInfo>();
        }

        public TimeSpan WeightValue
        {
            get { return m_weightValue; }
            set { m_weightValue = value; }
        }

        public Dictionary<String, ContainerInfo> ContainerHashTable
        {
            get { return m_htContainer; }
        }

        public string BlockID
        {
            get { return m_blockID; }
            set { m_blockID = value; }
        }

        public bool AddContainer(ContainerInfo container)
        {
            try
            {
                if (!m_htContainer.ContainsKey(container.ContainerID))
                {
                    m_htContainer.Add(container.ContainerID, container);
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Logger.ECSSchedule.Error("SimpleBlockInfo add container Error:" + ex.Message);
                return false;
            }
          
            return false;
        }

        public bool IsExistContainer(string containerID)
        {
            return m_htContainer.ContainsKey(containerID);
        }

        public bool RemoveContainer(string containerID)
        {
            if (m_htContainer.ContainsKey(containerID))
            {
                return m_htContainer.Remove(containerID);
            }

            return false;
        }
    }

    [Serializable]
    public enum RMGType
    {
        None = 0,
        LS = 1,  //陆侧
        WS = 2   //海侧
    }

    [Serializable]
    public class RMG
    {
        public String ID;
        public String Name;
        public List<JobType> JobTypeList;
        public RMGType Type = RMGType.None;
        public bool Enable;
    }

    [Serializable]
    public class BlockArea
    {
        public List<Bay> BayList = new List<Bay>();
        public List<Lane> LaneList = new List<Lane>();
    }

    [Serializable]
    public class WSArea
    {
        public List<Bay> BayList = new List<Bay>();
        public List<WSLane> LaneList = new List<WSLane>();
    }

    public class BlockInfo
    {
        public string BlockName;
        public int SafetyRange;
        public int ParkTier;
        public int ParkPosition;
        public bool Enable;

        public BlockArea YardArea = new BlockArea();
        public BlockArea LSExchangeArea = new BlockArea();
        public WSArea WSExchangeArea = new WSArea();

        public BmsPosition LSWaitingArea = new BmsPosition();
        public BmsPosition WSWaitingArea = new BmsPosition();

        public List<RMG> RMGList = new List<RMG>();

        public int TierHeight = 2900;

        public Bay this[BmsBayType bayType, int bayNo]
        {
            get
            {
                if (YardArea.BayList != null)
                    return YardArea.BayList.Find(obj => obj.BayType == bayType && obj.BayNo == bayNo);
                return null;
            }
        }

        public const string YARD = "YARD";
        public const string WSTP = "WSTP";
        public const string LSTP = "LSTP";

        public static BmsBayType ToBayType(string bayType)
        {
            switch (bayType)
            {
                case YARD:
                    return BmsBayType.Block;
                case WSTP:
                    return BmsBayType.WS;
                case LSTP:
                    return BmsBayType.LS;
            }
            return BmsBayType.None;
        }

        public static string ToBayTypeString(BmsBayType type)
        {
            switch (type)
            {
                case BmsBayType.Block:
                    return YARD;
                case BmsBayType.WS:
                    return WSTP;
                case BmsBayType.LS:
                    return LSTP;
                default:
                    return string.Empty;
            }
        }

        public bool IsExistBay(BmsPosition pos)
        {
            return IsExistBay(pos.Bay, pos.Lane, pos.Tier, pos.BayType);
        }

        public bool IsExistBay(int bay, int lane, int tier, BmsBayType bayType)
        {
            if (YardArea.BayList == null)
                return false;
            return YardArea.BayList.Exists(obj => obj.BayType == bayType
                && obj.BayNo == bay
                && obj.LaneList.Exists(l => l.LaneNo == lane && tier <= (l.MaxTier + 1)));
        }

        /// <summary>
        /// 贝位置转换为坐标位置
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool GetPositionEx(BmsPosition pos, out BmsPositionEx posEx)
        {
            posEx = null;

            var bay = YardArea.BayList.Find(obj => obj.BayType == pos.BayType
                        && obj.BayNo == pos.Bay);

            if (bay == null) 
                return false;

            int GantryPosition = bay.Coordinate;
            var lane = bay.LaneList.Find(obj => obj.LaneNo == pos.Lane);
            if (lane == null)
                return false;

            int TrolleyPosition = lane.Coordinate;

            int HoistPosition = ParkPosition;
            if (pos.Tier < ParkTier)
                HoistPosition = TierHeight * pos.Tier;

            posEx = new BmsPositionEx(pos, GantryPosition, TrolleyPosition, HoistPosition);

            return true;
        }

        public bool GetPositionEx( BlockCoordinate coordinate, BmsBaySize baySize , out BmsPositionEx posEx)
        {
            posEx = null;
            BmsPosition pos = new BmsPosition();
            if (coordinate.GantryPosition > 0)
            {
                pos.BlockName = BlockName;
                int nBaySize = ContainerSize.CTN_20_LENGTH;
                if (baySize == BmsBaySize.CTN_40)
                    nBaySize = ContainerSize.CTN_40_LENGTH;
                else if (baySize == BmsBaySize.CTN_45)
                    nBaySize = ContainerSize.CTN_45_LENGTH;

                int GantryPosition = coordinate.GantryPosition;
                var lst = YardArea.BayList.Where(obj => Math.Abs(GantryPosition - obj.Coordinate) < nBaySize / 2.0);
                foreach (var bay in lst)
                {
                    if (bay.BaySize == baySize)
                    {
                        pos.BayType = bay.BayType;
                        pos.Bay = bay.BayNo;
                        if (coordinate.TrolleyPosition > 0)
                        {
                            int TrolleyPosition = coordinate.TrolleyPosition;
                            var lane = bay.LaneList.Find(obj => Math.Abs(TrolleyPosition - obj.Coordinate) < ContainerSize.CTN_WIDTH / 2.0);
                            if (lane != null)
                                pos.Lane = lane.LaneNo;
                        }

                        if (coordinate.HoistPosition > 0)
                        {
                            pos.Tier = coordinate.HoistPosition / this.TierHeight;
                            if (pos.Tier <= 0)
                                pos.Tier = 1;
                        }

                        posEx = new BmsPositionEx(pos, 
                            coordinate.GantryPosition, coordinate.TrolleyPosition, coordinate.HoistPosition);

                        return true;
                    }
                }
          
            }
            return false;
        }

        /// <summary>
        /// 根据目的贝位置获取避让贝位置
        /// </summary>
        /// <param name="xTo"></param>
        /// <param name="bPositiveDirect">陆侧到海侧为正</param>
        /// <param name="safetyRange"></param>
        /// <returns></returns>
        public Bay GetAvoidBay(int xTo, bool bPositiveDirect)
        {
            if (bPositiveDirect)
            {
                var bayList = YardArea.BayList.OrderBy(c => c.Coordinate);//从小到大排序
                var tmpbay = bayList.FirstOrDefault(obj => obj.Coordinate >= xTo + SafetyRange);
                if (tmpbay == null)
                    tmpbay = bayList.Last();
                return tmpbay;
            }
            else
            {
                var bayList = YardArea.BayList.OrderByDescending(c => c.Coordinate);//从大到小排序
                var tmpbay = bayList.FirstOrDefault(obj => obj.Coordinate <= xTo - SafetyRange);
                if (tmpbay == null)
                    tmpbay = bayList.Last();
                return tmpbay;
            }
        }

    }

    public class BMSInfo
    {
        public Dictionary<String, BlockInfo> dicBlockInfo = new Dictionary<String, BlockInfo>();
        public bool IsInitialize = false;

        public BMSInfo()
        {
            InitBmsInfo();
        }

        private bool LanesRelateToBays<T>(List<Bay> bayList, List<T> laneList) where T:Lane
        {
            if (bayList == null || laneList == null)
                return false;

            foreach (Bay bay in bayList)
            {
                if (bay.LaneList == null)
                    bay.LaneList = new List<Lane>();
                bay.LaneList.Clear();

                for (int i = 0; i < laneList.Count; i++)
                    bay.LaneList.Add(laneList[i]);
            }

            return true;
        }

        public bool InitBmsInfo()
        {
            BMSInfoFactory factory = new BMSInfoFactory();
            List<BlockInfo> blockInfoList = factory.GetBlockInfoList();
            IsInitialize = false;
            if (blockInfoList == null)
                return false;

            foreach (BlockInfo item in blockInfoList)
            {
                dicBlockInfo.Add(item.BlockName, item);
                LanesRelateToBays(item.YardArea.BayList, item.YardArea.LaneList);
                LanesRelateToBays(item.LSExchangeArea.BayList, item.LSExchangeArea.LaneList);
                LanesRelateToBays(item.WSExchangeArea.BayList, item.WSExchangeArea.LaneList);
            }

            return true;
        }
    }

    public class BMSInfoFactory
    {
        private  BMSInfoFactory m_Instance;
        private  String m_DefaultPath = "BMSInfo.xml";

        public  List<BlockInfo> BlockInfoList = new List<BlockInfo>();

        public BMSInfoFactory()
        {
        }

        public  List<BlockInfo> GetBlockInfoList()
        {
            BlockInfoList.Clear(); 
            for (int i = 2; i <= 9; i++)
                BlockInfoList.Add(CreateDefaultBlockInfo(i));

           // SaveConfig(this);
            return BlockInfoList;
        }

        private BlockInfo CreateDefaultBlockInfo(int blockNo)
        {
            int i = 0;

            BlockInfo defBlockInfo = new BlockInfo();

            defBlockInfo.BlockName = "A0"+blockNo.ToString();
            defBlockInfo.Enable = true;
            defBlockInfo.ParkPosition = 18000;
            defBlockInfo.ParkTier = 6;
            defBlockInfo.SafetyRange = 8500;

            for (i = 1; i <= 7; i++)
            {
                Lane lane = new Lane();
                lane.LaneNo = i;
                lane.LaneID = "";
                lane.MaxTier = 5;
                lane.Coordinate = 17800 - (i - 1) * 2900;
                defBlockInfo.YardArea.LaneList.Add(lane);
            }

            int lastBayCoordinate = 0;
            for (i = 1; i <= 67; i++)
            {
                if (i == 4 || i == 64)
                    continue;

                Bay bay = new Bay();
                bay.BayNo = i;
                bay.BayType = BmsBayType.Block;
                if (i == 2 || i == 66)
                    bay.BaySize = BmsBaySize.CTN_45;
                else if (i % 2 == 0)
                    bay.BaySize = BmsBaySize.CTN_40;
                else
                    bay.BaySize = BmsBaySize.CTN_20;

                if (i < 5)
                    bay.Coordinate = 44035 + (i - 1) * 3250;
                else if (i == 5 || i == 65)
                    bay.Coordinate = lastBayCoordinate + 7090;
                else
                    bay.Coordinate = lastBayCoordinate + 3250;

                lastBayCoordinate = bay.Coordinate;

                defBlockInfo.YardArea.BayList.Add(bay);
            }

            for (i = 1; i <= 3; i++)
            {
                Lane lane = new Lane();
                lane.LaneNo = i;
                lane.LaneID = "LSTP." + defBlockInfo.BlockName + "." + Convert.ToString((char)('A' + i - 1));
                lane.MaxTier = 5;
                lane.Coordinate = 14900 - 5800 * (i - 1);
                defBlockInfo.LSExchangeArea.LaneList.Add(lane);
            }

            for (i = 1; i <= 6; i++)
            {
                if (i == 4 || i == 5)
                    continue;
                Bay bay = new Bay();
                bay.BayNo = i;
                if (i % 2 == 0)
                    bay.BaySize = BmsBaySize.CTN_40;
                else
                    bay.BaySize = BmsBaySize.CTN_20;
                bay.BayType = BmsBayType.LS;

                if (i == 1) bay.Coordinate = 25370;
                if (i == 2) bay.Coordinate = 28440;
                if (i == 3) bay.Coordinate = 31585;
                if (i == 6) bay.Coordinate = 8090;

                defBlockInfo.LSExchangeArea.BayList.Add(bay);

            }

            for (i = 1; i <= 3; i++)
            {
                WSLane lane = new WSLane();
                lane.LaneNo = i;
                lane.LaneID = "WSTP." + defBlockInfo.BlockName + "." + Convert.ToString((char)('A' + i - 1));
                lane.MaxTier = 5;
                if (i == 1) lane.Coordinate = 15166;
                if (i == 2) lane.Coordinate = 9178;
                if (i == 3) lane.Coordinate = 3137;
                lane.IsMate = true;
                defBlockInfo.WSExchangeArea.LaneList.Add(lane);
            }

            for (i = 1; i <= 6; i++)
            {
                if (i == 4 || i == 5)
                    continue;

                Bay bay = new Bay();
                bay.BayNo = i;
                if (i == 3)
                    bay.BaySize = BmsBaySize.CTN_20;
                else
                    bay.BaySize = BmsBaySize.CTN_40;
                bay.BayType = BmsBayType.WS;

                if (i == 1) bay.Coordinate = 269685;
                if (i == 2) bay.Coordinate = 273505;
                if (i == 3) bay.Coordinate = 277329;
                if (i == 6) bay.Coordinate = 295010;

                defBlockInfo.WSExchangeArea.BayList.Add(bay);

            }
            defBlockInfo.LSWaitingArea.Bay = 2;
            defBlockInfo.LSWaitingArea.BayType = BmsBayType.Block;
            defBlockInfo.LSWaitingArea.Lane = 3;
            defBlockInfo.LSWaitingArea.Tier = 6;

            defBlockInfo.WSWaitingArea.Bay = 2;
            defBlockInfo.WSWaitingArea.BayType = BmsBayType.WS;
            defBlockInfo.WSWaitingArea.Lane = 2;
            defBlockInfo.WSWaitingArea.Tier = 6;

            RMG LSRmg = new RMG();
            LSRmg.ID = "27" + blockNo.ToString();
            LSRmg.Name = "27" + blockNo.ToString();
            LSRmg.Type = RMGType.LS;
            LSRmg.Enable = true;
            LSRmg.JobTypeList = new List<JobType>() { JobType.SBLOCK, JobType.RECEIVE, JobType.DELIVER };
            defBlockInfo.RMGList.Add(LSRmg);

            RMG WSRmg = new RMG();
            WSRmg.ID = "26" + blockNo.ToString();
            WSRmg.Name = "26" + blockNo.ToString();
            WSRmg.Type = RMGType.WS;
            WSRmg.Enable = true;
            WSRmg.JobTypeList = new List<JobType>() { JobType.SBLOCK, JobType.DBLOCK, JobType.LOAD, JobType.DISC };
            defBlockInfo.RMGList.Add(WSRmg);

            return defBlockInfo;
        }

        /// <summary>
        /// 使用默认路径加载配置文件
        /// </summary>
        /// <returns></returns>
        public BMSInfoFactory LoadConfig()
        {
            if (m_Instance == null)
                m_Instance = LoadConfig(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, m_DefaultPath));
            return m_Instance;
        }

        /// <summary>
        /// 使用指定路径加载配置文件
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public BMSInfoFactory LoadConfig(string path)
        {
            //xml来源可能是外部文件，也可能是从其他系统获得
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                XmlSerializer xmlSearializer = new XmlSerializer(typeof(BMSInfoFactory));
                return (BMSInfoFactory)xmlSearializer.Deserialize(file);
            }
        }

        /// <summary>
        /// 使用默认路径保存配置文件
        /// </summary>
        /// <param name="cfg"></param>
        /// <returns></returns>
        public bool SaveConfig(BMSInfoFactory info)
        {
            return SaveConfig(info, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, m_DefaultPath));
        }

        /// <summary>
        /// 保存配置文件到指定路径
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool SaveConfig(BMSInfoFactory info, string path)
        {
            try
            {
                if (info == null || string.IsNullOrEmpty(path))
                    return false;

                using (FileStream file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    XmlSerializer xmlSearializer = new XmlSerializer(typeof(BMSInfoFactory));
                    xmlSearializer.Serialize(file, info);
                }
                return true;
            }
            catch 
            {

            }
            return false;
        }
    }
}
