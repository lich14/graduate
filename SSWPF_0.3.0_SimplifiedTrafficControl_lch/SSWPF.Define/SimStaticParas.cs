using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSWPF.Define
{
    public static class SimStaticParas
    {
        private static DateTime simDtStart = new DateTime(2016, 1, 1, 0, 0, 0);

        // 参数属性
        public static DateTime SimDtStart
        {
            get { return simDtStart; }
            set { }
        }
    }
}
