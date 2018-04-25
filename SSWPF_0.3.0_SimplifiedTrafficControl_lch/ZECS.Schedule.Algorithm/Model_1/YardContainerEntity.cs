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
    /// 堆场箱位类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0
    /// </summary>
    public class YardContainerEntity: ContainerEntity
    {
        #region [ 属性 ]
        
        /// <summary>
        /// 出口航次ID
        /// </summary>
        public long EVOYID;
        
        /// <summary>
        /// 进口航次ID
        /// </summary>
        public long IVOYID;
                
        /// <summary>
        /// 场箱位 ,6位数字
        /// </summary>
        public string YLocation;
        
        /// <summary>
        /// 装船放行标志
        /// </summary>
        public string PassFlag;
        
        /// <summary>
        /// 配载标志
        /// </summary>
        public string StowageFG;
        
        /// <summary>
        /// 所处箱区索引
        /// </summary>
        public int YardBlockIndex;
        
        /// <summary>
        /// 场倍位号
        /// </summary>
        public int YardBayIndex;
        
        /// <summary>
        /// 场排号
        /// </summary>
        public int YardBaySlotIndex;
        
        /// <summary>
        /// 场层号
        /// </summary>
        public int YardBayTierIndex;
        
        /// <summary>
        /// 作业类型：YV(出口）,VY（进口） and 空
        /// </summary>
        public string Opprc;
        
        /// <summary>
        /// 压箱数量
        /// </summary>
        //public int YYNum;

        #endregion

        
    }
}
