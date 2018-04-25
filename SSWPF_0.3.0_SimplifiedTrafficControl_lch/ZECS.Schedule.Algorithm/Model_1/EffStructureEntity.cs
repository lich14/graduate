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
    /// 桥机平均效率类（效率比为1）
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0
    /// </summary>
    public class EffStructureEntity
    {
        #region [ 属性 ]
        /// <summary>
        /// 单箱装重箱效率（箱/分）
        /// </summary>
        public double LoadFEffSingle;

        /// <summary>
        /// 单箱装空箱效率（箱/分）
        /// </summary>
        public double LoadEEffSingle;

        /// <summary>
        /// 单箱卸重箱效率（箱/分）
        /// </summary>
        public double UnLoadFEffSingle;

        /// <summary>
        /// 单箱卸空箱效率（箱/分）
        /// </summary>
        public double UnLoadEEffSingle;

        /// <summary>
        /// 特殊箱作业效率（箱/分）
        /// </summary>
        public double SpecialEff;

        /// <summary>
        /// 涉及的其他时长,包括大件
        /// </summary>
        public double OtherWorkTime;
        
        #endregion

        #region [ 方法 ]

        #region [ 构造函数 ]

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="a"></param>
        public EffStructureEntity(EffStructureEntity a)
        {
            if (a != null)
            {
                this.LoadEEffSingle = a.LoadEEffSingle;
                this.LoadFEffSingle = a.LoadFEffSingle;
                this.SpecialEff = a.SpecialEff;
                this.UnLoadEEffSingle = a.UnLoadEEffSingle;
                this.UnLoadFEffSingle = a.UnLoadFEffSingle;
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public EffStructureEntity()
        {

        }

        #endregion
        /// <summary>
        /// 数乘
        /// </summary>
        /// <param name="a"></param>
        public void DotProduct(double a)
        {

            this.LoadEEffSingle = this.LoadEEffSingle * a;

            this.LoadFEffSingle = this.LoadFEffSingle * a;

            this.SpecialEff = this.SpecialEff * a;

            this.UnLoadEEffSingle = this.UnLoadEEffSingle * a;

            this.UnLoadFEffSingle = this.UnLoadFEffSingle * a;
        }

        /// <summary>
        /// 加法
        /// </summary>
        /// <param name="a"></param>
        public void Add(EffStructureEntity a)
        {
            this.LoadEEffSingle = this.LoadEEffSingle + a.LoadEEffSingle;
            this.LoadFEffSingle = this.LoadFEffSingle + a.LoadFEffSingle;
            this.SpecialEff = this.SpecialEff + a.SpecialEff;
            this.UnLoadEEffSingle = this.UnLoadEEffSingle + a.UnLoadEEffSingle;
            this.UnLoadFEffSingle = this.UnLoadFEffSingle + a.UnLoadFEffSingle;
        }

        /// <summary>
        /// 合计桥吊资源时间，单位分钟
        /// </summary>
        /// <param name="a"></param>
        public double GetTotalQcHours()
        {
            return (this.LoadEEffSingle + this.LoadFEffSingle + this.SpecialEff + this.UnLoadEEffSingle + this.UnLoadFEffSingle + this.OtherWorkTime);
        }

        /// <summary>
        /// 合计桥吊资源时间(去除大件），单位分钟
        /// </summary>
        /// <param name="a"></param>
        public double GetTotalQcHoursWithoutHugeCon()
        {
            return (this.LoadEEffSingle + this.LoadFEffSingle + this.SpecialEff + this.UnLoadEEffSingle + this.UnLoadFEffSingle);
        }
           
        #endregion

    }
}
