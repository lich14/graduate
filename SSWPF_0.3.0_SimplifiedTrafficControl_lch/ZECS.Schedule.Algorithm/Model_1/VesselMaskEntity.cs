using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Algorithm;
using ZECS.Schedule.Define.DBDefine.Schedule;


namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// Mask属性类（StowFactor)
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0
    /// </summary>
    public class VesselMaskEntity
    {

        /// <summary>
        /// MSAK ID
        /// </summary>
        public long MaskID;

        /// <summary>
        /// Mask索引
        /// </summary>
        public int MaskIndex;
        
        /// <summary>
        /// 箱型
        /// </summary>
        public string Cntrtype;

        /// <summary>
        /// 危险品标志，危品标志
        /// </summary>
        public bool Dgflag;

        /// <summary>
        /// 高箱标志
        /// </summary>
        public bool CHeightFG;

        /// <summary>
        /// 超限标志
        /// </summary>
        public bool Ovlmtflag;

        /// <summary>
        /// 特种箱标志
        /// </summary>
        public bool CTypeFG;

        /// <summary>
        /// 冷藏箱标志
        /// </summary>
        public bool RFCFG;

        /// <summary>
        /// 箱ISO代码（可包含多种ISO代码）
        /// </summary>
        public string Cisocd;

        /// <summary>
        /// 箱尺寸(可包含多种箱尺寸）
        /// </summary>
        public string CSize;

        /// <summary>
        /// 卸货港
        /// </summary>
        public string UNLDPort;

        /// <summary>
        /// 重量等级区间，首项恒为0
        /// </summary>
        public List<double> WeightInterval;

        /// <summary>
        /// 该Mask的所有不同重量要求以及相应的船箱位数量
        /// </summary>
        public List<SlotNumOfWeightIntervalOfVesselCntrGroup> SlotNumOfWeightParametreList = new List<SlotNumOfWeightIntervalOfVesselCntrGroup>();

        
        /// <summary>
        /// 船箱位对应的Mask以及重量等级类
        /// </summary>
        public class SlotNumOfWeightIntervalOfVesselCntrGroup
        {
            /// <summary>
            /// 2元数组，对应WeightInterval,记录重量等级索引范围,首元素为起始重量索引，末元素位截止重量索引
            /// </summary>
            public int[] WeightRange;


            /// <summary>
            /// 出现的Bay位作业块
            /// </summary>
            public VesselContainerGroupEntity BelongToCntrGroup;

            /// <summary>
            /// 所属的Mask
            /// </summary>
            public VesselMaskEntity BelongToMask;

            /// <summary>
            /// 相应船箱位数量
            /// </summary>
            public int SlotNum;

        }


        /// <summary>
        /// 属于同一Mask的在场箱类
        /// </summary>
        public class SubYardContainerEntityofMask
        {
            /// <summary>
            /// 箱区编号
            /// </summary>
            public string yardno;

            /// <summary>
            /// 箱区索引
            /// </summary>
            public int YardIndex;

            /// <summary>
            /// 在场箱子某mask箱区子队列
            /// </summary>
            public List<YardContainerEntity> subyardconlist = new List<YardContainerEntity>();
        }

        /// <summary>
        /// 引入重量等级的该Mask在场箱分布类
        /// </summary>
        public class SubYardContainerByWeightEntityofMask
        {

            /// <summary>
            /// 2元数组，记录重量等级范围
            /// </summary>
            public int[] WeightRange;

            /// <summary>
            /// 对应该重量等级区间Mask在各箱区的在场箱分布
            /// </summary>
            public List<SubYardContainerEntityofMask> WeightIntervalofSubYardConList = new List<SubYardContainerEntityofMask>();

        }


        /// <summary>
        /// 在场箱分箱区分重量区间子队列
        /// </summary>
        public List<SubYardContainerByWeightEntityofMask> SubYardCntrByWeightIntervalList = new List<SubYardContainerByWeightEntityofMask>();

        
    }
}
