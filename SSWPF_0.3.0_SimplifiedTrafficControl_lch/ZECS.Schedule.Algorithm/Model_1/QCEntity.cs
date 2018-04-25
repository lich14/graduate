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
    /// QC实体类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0
    /// </summary>
    public class QCEntity
    {
        #region [ 属性 ]
        /// <summary>
        /// 桥机名称
        /// </summary>
        public string QTName { get; set; }
        
        /// <summary>
        /// 桥机索引
        /// </summary>
        public int QCIndex { get; set; }

        /// <summary>
        /// 桥机可能的最大效率比（具体效率结构应结合特定的各船舶）
        /// </summary>
        public double MaxEffRate;

        /// <summary>
        /// 该桥机额定效率比(该桥机为老式桥机的话，特别指定，否则取桥机预设效率）
        /// </summary>
        public double AvgEffRate;
        
        /// <summary>
        /// 桥机移动速度（米/分）
        /// </summary>
        public double MoveSpeed;

        /// <summary>
        /// 桥机大梁高度（米）
        /// </summary>
        public double CrossbeamHeight;

        /// <summary>
        /// 桥机停止作业移动后再次待命准备作业需要的时长（分）
        /// </summary>
        public double TimeOfStopAndReady;

        /// <summary>
        /// 装舱盖板所需时长（分）
        /// </summary>
        public double TimeOfLoadHatchCover;

        /// <summary>
        /// 卸舱盖板所需要时长（分）
        /// </summary>
        public double TimeOfDischargeHatchCover;

        /// <summary>
        /// QC穿过驾驶台所需要的时长（分）
        /// </summary>
        public double TimeOfCrossCage;

        /// <summary>
        /// 桥机一个Move所需要的时长（分）
        /// </summary>
        public double TimeOfOneMove;

        /// <summary>
        /// 更换吊具尺寸所需要的时长（分）
        /// </summary>
        public double TimeOfChangeHanger;
        
        /// <summary>
        /// 是否具有双吊具工艺
        /// </summary>
        public bool HasTwinHanger;
        
        #endregion

    }
}
