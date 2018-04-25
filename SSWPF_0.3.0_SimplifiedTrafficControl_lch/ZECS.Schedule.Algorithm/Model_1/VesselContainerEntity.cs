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
    /// 船图箱(卸船箱）实体类(暂不关心卸箱作业次序）
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0
    /// </summary>
    public class VesselContainerEntity : ContainerEntity
    {
        #region [ 属性 ]

        /// <summary>
        /// 船图箱ID
        /// </summary>
        public int ContainerID;

        /// <summary>
        /// 翻舱作业类型
        /// </summary>
        public string RestowType;

        /// <summary>
        /// 所属卸箱作业块
        /// </summary>
        public VesselContainerGroupEntity CntrGroup;

        /// <summary>
        /// 所在船箱位
        /// </summary>
        public string BaySlot;

        /// <summary>
        /// 所在Bay位层索引
        /// </summary>
        public int BaytierId;

        /// <summary>
        /// 所在Bay位列索引
        /// </summary>
        public int ColumnId;

        /// <summary>
        /// 甲板or舱内
        /// D:甲板上
        /// H:舱内
        /// </summary>
        public bool HDflag;

        /// <summary>
        /// 作业状态
        /// </summary>
        public string Workstatus;

        /// <summary>
        /// 卸箱目的箱位
        /// </summary>
        //public string Ylocation;

        /// <summary>
        /// 预分配的卸箱目的箱区
        /// </summary>
        public YardBlockEntity GoalYardBlock;

        /// <summary>
        /// 卸箱目的倍位
        /// </summary>
        public int YardBayNo;

        /// <summary>
        /// 卸箱目的排
        /// </summary>
        public int YardBayStack;

        /// <summary>
        /// 卸箱目的层
        /// </summary>
        public int YardBayTier;
     
        #endregion


    }
}
