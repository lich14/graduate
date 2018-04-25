using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.Algorithm
{

    /// <summary>
    /// 箱型结构类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0 
    /// </summary>
    public class CntrStructureEntity
    {

        #region [ 属性 ]

        /// <summary>
        /// 单箱作业装载重箱箱量
        /// </summary>
        public int LoadFCntrNumSingle;
        
        /// <summary>
        /// 单箱作业装载空箱箱量
        /// </summary>
        public int LoadECntrNumSingle;
        
        /*
        /// <summary>
        /// 双箱作业装载重箱箱量
        /// </summary>
        public int LoadHCntrNumDouble;
        

        /// <summary>
        /// 双箱作业装载空箱箱量
        /// </summary>
        public int LoadECntrNumDouble;
        
         
        /// <summary>
        /// 双箱作业卸载箱量
        /// </summary>
        public int UnloadHCntrNumDouble;
         
        */

        /// <summary>
        /// 单箱作业卸载重箱箱量
        /// </summary>
        public int UnLoadFCntrNumSingle;
        

        /// <summary>
        /// 单箱作业卸载空箱箱量
        /// </summary>
        public int UnLoadECntrNumSingle;
        
        /*
        /// <summary>
        /// 危险品箱量装卸箱量
        /// </summary>
        public int DangerCntrNum;
        
        /// <summary>
        /// 框架箱量装卸箱量
        /// </summary>
        public int FrameCntrNum;
        

        /// <summary>
        /// 冷冻箱箱量装卸箱量
        /// </summary>
        public int RefrCntrNum;
        
        */

        /// <summary>
        /// 由框架箱，危险品箱，冷冻箱等组成的特殊箱的箱量
        /// </summary>
        public int SpecialCntrNum;
        
        /// <summary>
        /// 大件的箱量
        /// </summary>
        public int HugeCntrNum;
        

        #endregion

        #region [ 方法 ]

        #region [ 构造函数 ]

        public CntrStructureEntity()
        {


        }


        public CntrStructureEntity(CntrStructureEntity a)
        {
            if (a != null)
            {
                this.LoadECntrNumSingle = a.LoadECntrNumSingle;
                this.LoadFCntrNumSingle = a.LoadFCntrNumSingle;
                this.UnLoadECntrNumSingle = a.UnLoadECntrNumSingle;
                this.UnLoadFCntrNumSingle = a.UnLoadFCntrNumSingle;
                this.SpecialCntrNum = a.SpecialCntrNum;
            }


        }

        #endregion

        /// <summary>
        /// 累加箱量
        /// </summary>
        /// <param name="a"></param>
        public void AddTo(CntrStructureEntity a)
        {
            if (a.LoadECntrNumSingle > 0)
                this.LoadECntrNumSingle = this.LoadECntrNumSingle + a.LoadECntrNumSingle;
            if (a.LoadFCntrNumSingle > 0)
                this.LoadFCntrNumSingle = this.LoadFCntrNumSingle + a.LoadFCntrNumSingle;
            if (a.UnLoadECntrNumSingle > 0)
                this.UnLoadECntrNumSingle = this.UnLoadECntrNumSingle + a.UnLoadECntrNumSingle;
            if (a.UnLoadFCntrNumSingle > 0)
                this.UnLoadFCntrNumSingle = this.UnLoadFCntrNumSingle + a.UnLoadFCntrNumSingle;
            if (a.SpecialCntrNum > 0)
                this.SpecialCntrNum = this.SpecialCntrNum + a.SpecialCntrNum;

        }

        /// <summary>
        /// 累减箱量
        /// </summary>
        /// <param name="a"></param>
        public void MinusTo(CntrStructureEntity a)
        {
            if (a.LoadECntrNumSingle > 0)
                this.LoadECntrNumSingle = this.LoadECntrNumSingle - a.LoadECntrNumSingle;
            if (a.LoadFCntrNumSingle > 0)
                this.LoadFCntrNumSingle = this.LoadFCntrNumSingle - a.LoadFCntrNumSingle;
            if (a.UnLoadECntrNumSingle > 0)
                this.UnLoadECntrNumSingle = this.UnLoadECntrNumSingle - a.UnLoadECntrNumSingle;
            if (a.UnLoadFCntrNumSingle > 0)
                this.UnLoadFCntrNumSingle = this.UnLoadFCntrNumSingle - a.UnLoadFCntrNumSingle;
            if (a.SpecialCntrNum > 0)
                this.SpecialCntrNum = this.SpecialCntrNum - a.SpecialCntrNum;

        }

        /// <summary>
        /// 数乘
        /// </summary>
        /// <param name="a"></param>
        public void DotProduct(double a)
        {

            this.LoadECntrNumSingle = (int)(this.LoadECntrNumSingle * a);

            this.LoadFCntrNumSingle = (int)(this.LoadFCntrNumSingle * a);

            this.SpecialCntrNum = (int)(this.SpecialCntrNum * a);

            this.UnLoadECntrNumSingle = (int)(this.UnLoadECntrNumSingle * a);

            this.UnLoadFCntrNumSingle = (int)(this.UnLoadFCntrNumSingle * a);
        }

        /// <summary>
        /// 计算给定效率下的作业总时间
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public double TotalTime(EffStructureEntity a)
        {

            return this.LoadECntrNumSingle * a.LoadEEffSingle + this.LoadFCntrNumSingle * a.LoadFEffSingle + this.SpecialCntrNum * a.SpecialEff + this.UnLoadECntrNumSingle * a.UnLoadEEffSingle + this.UnLoadFCntrNumSingle * a.UnLoadFEffSingle;

        }

        #endregion
    }

}
