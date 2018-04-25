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
    /// Bay位作业子块类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0
    /// </summary>
    public class VesselContainerGroupEntity
    {

        #region [ 属性 ]
        /// <summary>
        /// Bay位作业块索引
        /// </summary>
        public int CntrGroupIndex;
        
        /// <summary>
        /// 所属Bay位ID
        /// </summary>
        public long BayID;
        
        /// <summary>
        /// 桥吊作业开始时间，最小时间为分钟
        /// </summary>
        public DateTime StTime;
        

        /// <summary>
        /// 桥吊作业结束时间，最小时间为分钟
        /// </summary>
        public DateTime EdTime;
        
        /// <summary>
        /// 作业前QC的等待时间（避免冲突),单位为分钟
        /// </summary>
        public double WaitTime;
        
        /// <summary>
        /// 移动时间,单位为分钟
        /// </summary>
        //public double MoveTime;
        
        /// <summary>
        ///  执行桥吊
        /// </summary>
        public QCEntity QC;
        
        /// <summary>
        /// 箱组队列（进口）
        /// /// </summary>
        public List<VesselContainerEntity> VYVesselContaierList;
        

        /// <summary>
        /// 箱组队列（出口）
        /// </summary>
        public List<StowageSlotLocationEntity> YVVesselContaierList;

        /// <summary>
        /// 作业箱任务的偏序表
        /// </summary>
        public PartialOrderTable PartialOrder;

        /// <summary>
        /// 生成偏序表的装（卸）船规则
        /// </summary>
        public string RuleForPartialOrder;
        
        /// <summary>
        /// 箱组子队列，按堆场划块分配划分
        /// </summary>
        //public List<List<StowageSlotLocationEntity>> SubListVesselContaierList;
        

        /// <summary>
        /// 该Bay位作业块涉及的所有Mask列表
        /// </summary>
        public List<VesselMaskEntity> MaskList;
        
        /// <summary>
        /// 该Bay位作业块包含的箱型结构
        /// </summary>
        public CntrStructureEntity CntrStruct;

        /// <summary>
        /// 该Bay位作业块对应的计划作业效率
        /// </summary>
        public EffStructureEntity EffStruct;
        
        /// <summary>
        ///舱上舱下标记
        /// </summary>
        public bool HDFLAG;
       
        
        /// <summary>
        /// 前驱Bay位作业块
        /// </summary>
        public VesselContainerGroupEntity PreCntrGroup;

        /// <summary>
        /// 后续Bay位作业块
        /// </summary>
        public VesselContainerGroupEntity ProCntrGroup;

        #endregion

        #region [ 方法 ]

        /// <summary>
        /// 初始化装（卸）船
        /// </summary>
        public void IniPartialOrder()
        {

        }

        #endregion

    }
}
