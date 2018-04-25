using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZECS.Schedule.Algorithm
{
    /// <summary>
    /// 箱类
    /// 作者     修改        日期            版本号
    /// 董琳     初始化      2016-03-28       ver1.0
    /// </summary>
    public class ContainerEntity
    {
                
        /// <summary>
        /// 箱号
        /// </summary>
        public string ContainerID;

        /// <summary>
        /// 航次ID
        /// </summary>
        public long VOY_CURID;

        /// <summary>
        /// 箱尺寸：20,40,45尺
        /// </summary>
        public string CSize;

        /// <summary>
        /// 箱型:GP等
        /// </summary>
        public string Cntrtype;

        /// <summary>
        /// 箱高：箱高代码PQ等
        /// </summary>
        public string CHeightCD;

        /// <summary>
        /// 箱ISO代码
        /// </summary>
        public string Cisocd;

        /// <summary>
        /// 箱重
        /// </summary>
        public double Cweight;

        /// <summary>
        /// 箱状态
        /// </summary>
        public string Cstatuscd;

        /// <summary>
        /// 持箱人
        /// </summary>
        public string COperCD;

        /// <summary>
        /// 卸货港
        /// </summary>
        public string UNLDPort;

        /// <summary>
        /// 冷藏箱状态
        /// </summary>
        public string RefStatus;

        /// <summary>
        /// 危险品代码
        /// </summary>
        public string DnggCD;

        /// <summary>
        /// 超限标志代码
        /// </summary>
        public string OvlmtCD;

        /// <summary>
        /// 空重标记
        /// </summary>
        public bool EFFG;
        
        /// <summary>
        /// 中转箱标志
        /// </summary>
        public bool TranFG;

        /// <summary>
        /// 过境箱标记
        /// </summary>  
        public bool ThrgcFG;
        
        /// <summary>
        /// 危险品标志，危品标志，Y或N
        /// </summary>
        public bool Dgflag;

        /// <summary>
        /// 高箱标志， Y,N
        /// </summary>
        public bool CHeightFG;

        /// <summary>
        /// 超限标志:Y, N
        /// </summary>
        public bool Ovlmtflag;

        /// <summary>
        /// 特种箱标志:Y, N
        /// </summary>
        public bool CTypeFG;

        /// <summary>
        /// 冷藏箱标志:Y, N
        /// </summary>
        public bool RFCFG;

        /// <summary>
        /// 船上箱门方向
        /// </summary>
        public int DoorDirectionAtVessel;

        /// <summary>
        /// 场地箱门方向
        /// </summary>
        public int DoorDirectionAtYard;


    }
}
