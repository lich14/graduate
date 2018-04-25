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
    /// 箱区类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0
    /// </summary>
    public class YardBlockEntity
    {
        /// <summary>
        /// 箱区索引
        /// </summary>
        public int BlockIndex;

        //包含的bay位列表
        //public List<YardBayEntity> BayList;
    }
}
