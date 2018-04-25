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
    /// 配载后的出口船箱位类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0 
    /// </summary>
    public class StowageSlotLocationEntity 
    {
        #region [ 属性 ]

        /// <summary>
        /// 船箱位索引
        /// </summary>
        public int VLocationIndex;

        /// <summary>
        /// 船倍位号
        /// </summary>
        public int BayNo;
        
        /// <summary>
        /// 船排号
        /// </summary>
        public int BaySlot;
        
        /// <summary>
        /// 船层号
        /// </summary>
        public int BayTier;

        /// <summary>
        /// 舱内/甲板标记
        /// </summary>
        public bool HDflag;

        /// <summary>
        /// 该船箱位对应的Mask
        /// </summary>
        public VesselMaskEntity Mask;

        /// <summary>
        /// 该船箱位对应的重量区间
        /// </summary>
        public int[] WeightInterval;
        
        /// <summary>
        /// CWP计划的作业序值
        /// </summary>
        public int CWPWKSeq;
        
        /// <summary>
        /// 建议作业工艺
        /// </summary>
        public string WorkFlow;

        /// <summary>
        /// 当前配载的在场箱
        /// </summary>
        public YardContainerEntity StowedContainer;

        /// <summary>
        /// 分箱区按Mask可换箱以及重量要求的所有可装在场箱列表
        /// </summary>
        public List<List<YardContainerEntity>> YardContainerListOfEachYardBlock;

        /// <summary>
        /// 前驱作业Slot列表(暂不考虑卸箱）
        /// </summary>
        public List<StowageSlotLocationEntity> PreOperationSlotList = new List<StowageSlotLocationEntity>();

        /// <summary>
        /// 紧接该Slot后可作业Slot列表
        /// </summary>
        public List<StowageSlotLocationEntity> ProOperationSlotList = new List<StowageSlotLocationEntity>();

        
        #endregion

        
    }
}
