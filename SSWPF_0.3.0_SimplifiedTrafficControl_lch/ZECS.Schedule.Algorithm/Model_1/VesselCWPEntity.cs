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
    /// CWP 实体类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0
    /// </summary>
    
    public class VesselCWPEntity 
    {
        /// <summary>
        /// CWP索引
        /// </summary>
        public int CWPIndex;
                
        /// <summary>
        /// 桥吊作业开始时间
        /// </summary>
        public DateTime StQCSPTime;

        /// <summary>
        /// 桥机作业结束时间
        /// </summary>
        public DateTime EdQCSPTime;

        /// <summary>
        /// 所属船舶
        /// </summary>
        //public VesselEntity Vessel;
        
        /// <summary>
        ///  桥吊,TODO：目前不按每个桥吊精确地计算效率
        /// </summary>
        public QCEntity QC;
        
        /// <summary>
        /// 该CWP是否已完成配载
        /// </summary>
        //public bool IsStowed;
        
         /// <summary>
        ///  箱组队列(Bay位作业块）
        /// </summary>
        public List<VesselContainerGroupEntity> VesselContainerGroupList;

        
        /// <summary>
        /// 桥吊当前所在位置舱位编号
        /// </summary>
        public string Curvloction;
        
        /// <summary>
        /// 该作业路的分箱型结构的平均效率
        /// </summary>
        public EffStructureEntity MeanEfficiency;

    }
}
