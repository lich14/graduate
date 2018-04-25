using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; 
using System.Reflection;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.CiTOS;

namespace ZECS.Schedule.DBDefine.BlockInfo
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

        public ContainerMask()
        {

        }
        public ContainerMask(string factor,double weightMargin)
        {
            this.CONTAINER_STOW_FACTOR = factor;
            this.CONTAINER_WEIGHT_MARGIN_KG = weightMargin;
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
            this.CONTAINER_STOW_FACTOR = mask.CONTAINER_STOW_FACTOR;
            this.CONTAINER_WEIGHT_MARGIN_KG = mask.CONTAINER_WEIGHT_MARGIN_KG;
        }
    }

    [Serializable]
    public class ContainerSize
    {
        public ContainerSize(int length, int height)
        {
            Length = length;
            Height = height;
        }

        public int Length
        {
            get { return m_nLength; }
            set { m_nLength = value; }
        }

        public int Height
        {
            get { return m_nHeight; }
            set { m_nHeight = value; }
        }

        int m_nLength;
        int m_nHeight;

    }

    /// <summary>
    /// 堆场中的位置，指定堆场上某一位置
    /// 有两种方式，一种以贝列层，一种以具体的数据(大车位置、小车位置、吊具高度) 单位mm
    /// </summary>
    [Serializable]
    public class Position
    {
        public Position()
        {
            m_bRealPosition = false;
            m_nBay = 0;
            m_nLane = 0;
            m_nTier = 0;
            m_nGantryPos = 0;
            m_nTrolleyPos = 0;
            m_nHoistPos = 0;
            m_nBayType = 0;
            m_strBlockName = "";
        }

        public Position(int nStack, int nLane, int nTier)
        {
            m_nBay = nStack;
            m_nLane = nLane;
            m_nTier = nTier;
        }

        public Position(Position pos)
        {
            if (pos != null)
            {
                m_bRealPosition = pos.m_bRealPosition;
                if (pos.Bay > 0)
                    m_nBay = pos.Bay;
                if (pos.Lane > 0)
                    m_nLane = pos.Lane;
                if (pos.Tier > 0)
                    m_nTier = pos.Tier;
                if (pos.BayType > 0)
                    m_nBayType = pos.BayType;

                m_nGantryPos = pos.GantryPos;
                m_nTrolleyPos = pos.TrolleyPos;
                m_nHoistPos = pos.HoistPos;

                m_strBlockName = pos.BlockName;
            }
        }

        /// <summary>
        /// 所在的堆垛名称
        /// </summary>
        public String BlockName
        {
            get { return m_strBlockName; }
            set { m_strBlockName = value; }
        }

        /// <summary>
        /// 所在的贝位
        /// </summary>
        public int Bay
        {
            get { return m_nBay; }
            set { m_nBay = value; }
        }

        public int BayType
        {
            get { return m_nBayType; }
            set { m_nBayType = value; }
        }

        /// <summary>
        /// 所在的列
        /// </summary>
        public int Lane
        {
            get { return m_nLane; }
            set { m_nLane = value; }
        }

        /// <summary>
        /// 所在的层
        /// </summary>
        public int Tier
        {
            get { return m_nTier; }
            set { m_nTier = value; }
        }

        /// <summary>
        /// 大车的具体位置，单位mm
        /// </summary>
        public int GantryPos
        {
            get { return m_nGantryPos; }
            set { m_nGantryPos = value; }
        }

        /// <summary>
        /// 小车的具体位置，单位mm
        /// </summary>
        public int TrolleyPos
        {
            get { return m_nTrolleyPos; }
            set { m_nTrolleyPos = value; }
        }

        /// <summary>
        /// 层的高度，单位mm
        /// </summary>
        public int HoistPos
        {
            get { return m_nHoistPos; }
            set { m_nHoistPos = value; }
        }

        /// <summary>
        /// Position指定的位置是否是实际位置，还是贝、列、层信息
        /// true的话则应该使用GantryPos、TrolleyPos、HoistPos这三个属性
        /// false的话则应该使用Bay、Lane、Tier三个属性
        /// </summary>
        public bool IsRealPosition
        {
            get { return m_bRealPosition; }
            set { m_bRealPosition = value; }
        }

        int m_nBayType;
        int m_nBay;
        int m_nLane;
        int m_nTier;

        int m_nGantryPos;
        int m_nTrolleyPos;
        int m_nHoistPos;

        bool m_bRealPosition;

        String m_strBlockName;

        public override string ToString()
        {
            return BlockName + "-" + Bay + "-" + Lane + "-" + Tier;
        }
    }

    [Serializable]
    public class ContainerInfo
    {
        public ContainerInfo(String containerID, String iso, int length, int height, int weight, 
                             int doorDirection, int color, int bay, int lane, int tier, ContainerMask mask)
        {
            m_strContainerID = containerID;
            m_strISO = iso;
            m_Position = new Position(bay, lane, tier);
            Size = new ContainerSize(length, height);
            m_nWeight = weight;
            m_DoorDirection = (ContainerDoorDirection)doorDirection;
            Color = color;
            Mask.Copy(mask);
        }
        public String ContainerID
        {
            get { return m_strContainerID; }
            set { m_strContainerID = value; }
        }
        public String ISO
        {
            get { return m_strISO; }
            set { m_strISO = value; }
        }

        public Position Location
        {
            get { return m_Position; }
            set { m_Position = value; }
        }
        public ContainerSize Size
        {
            get { return m_size; }
            set { m_size = value; }
        }

        public int Weight
        {
            get { return m_nWeight; }
            set { m_nWeight = value; }
        }

        public ContainerDoorDirection DoorDirection
        {
            get { return m_DoorDirection; }
            set { m_DoorDirection = value; }
        }

        public int Color
        {
            get { return m_nColor; }
            set { m_nColor = value; }
        }

        public ContainerMask Mask
        {
            get { return m_mask; }
            set { m_mask = value; }
        }
        String m_strContainerID;
        Position m_Position;
        ContainerSize m_size;
        int m_nWeight;
        String m_strISO;
        int m_nColor;
        ContainerMask m_mask = new ContainerMask();

        ContainerDoorDirection m_DoorDirection;
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
}
