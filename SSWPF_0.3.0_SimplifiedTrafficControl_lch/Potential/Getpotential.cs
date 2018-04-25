using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Potential
{
    public class Getpotential
    {
        
        //用来判断是否变换车道
        //odistance两车之间的距离
        //ospeed1障碍物车的速度
        //ospeed2行驶车的速度
        //若两车相向行驶则障碍物车的速度为负,否则为正
        //只考虑在行驶车前方的障碍物车，距离都是正值
        public double changelinepotential(double odistance, double ospeed1, double ospeed2)
        {
            double result = 0;
            double c1 = 128;
            double c2 = 0.1;
            double c3 = 1;
            //double c4 = 1;

            result += c1 * Math.Exp(-c2 * odistance) / odistance;
            //result += c3 * Math.Exp(-c4 * (ospeed1 - ospeed2));

            if (odistance < 50)
            {
                result += c3 * (ospeed2 - ospeed1);
            }

            return result;
        }

        //用来判断选择哪种车道驶入
        //odistance两车之间的距离
        //ospeed障碍物车的速度
        //以将要行驶的方向为正方向
        public double chooselinepotential(double odistance, double ospeed)
        {
            double result = 0;
            double c1 = 1;
            double c2 = 1;
            double c3 = 1;
            double c4 = 1;
            double od = Math.Abs(odistance);

            result += c1 * Math.Exp(-c2 * od) / od;

            if (odistance > 0)
            {
                result += c3 * Math.Exp(-c4 * ospeed);
            }
            else
            {
                result += c3 * Math.Exp(c4 * ospeed);
            }

            return result;
        }
    }
}
