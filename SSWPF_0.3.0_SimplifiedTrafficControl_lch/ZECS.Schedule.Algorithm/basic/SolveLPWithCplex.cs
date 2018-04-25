using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ILOG.Concert;
using ILOG.CPLEX;

namespace ZECS.Schedule.Algorithm
{
    class SolveLPWithCplex
    {
        #region [ 属性 ]

        /// <summary>
        /// 目标函数系数
        /// </summary>
        private List<double> _objFuncCoeffi;

        /// <summary>
        /// 目标函数最优值此处为最小值
        /// </summary>
        private double _optionalValue;

        public double OptionalValue
        {
            get { return _optionalValue; }
            set { _optionalValue = value; }
        }

        /// <summary>
        /// 得到的最优解
        /// </summary>
        private List<double> _optSolution;

        public List<double> OptSolution
        {
            get { return _optSolution; }
            set { _optSolution = value; }
        }

        /// <summary>
        /// 所得解的类型，（无可行解，得到最优解等）
        /// 为1时为，得到最优解
        /// </summary>
        private int _flag;

        public int Flag
        {
            get { return _flag; }
            set { _flag = value; }
        }

        /// <summary>
        /// 等号约束系数矩阵
        /// </summary>
        private List<List<double>> _eqConstraintCoeffiMatrix;

        /// <summary>
        /// 等号约束中等式右边的值
        /// </summary>
        private List<double> _eqConstraintValue;

        /// <summary>
        /// 小于等于不等式约束的系数矩阵（变量恒非负）
        /// </summary>
        private List<List<double>> _lsneqConstraintCoeffiMatrix;

        /// <summary>
        /// 小于等于不等式约束中不等式右边的值
        /// </summary>
        private List<double> _lsneqConstraintValue;

        /// <summary>
        /// 具有上界约束的变量索引列表
        /// </summary>
        private List<int> _upBoundIndexList;

        /// <summary>
        /// 上界约束列表
        /// </summary>
        private List<double> _upBoundList;

        /// <summary>
        /// 具有下界约束的变量索引列表
        /// </summary>
        private List<int> _lowBoundIndexList;

        /// <summary>
        /// 下界约束列表
        /// </summary>
        private List<double> _lowBoundList;

        /// <summary>
        /// 若无可行解，返回违反的约束编号
        /// </summary>
        public List<int> ConflictConstraintNo;

        /// <summary>
        /// 若无可行解，返回违反的变量编号
        /// </summary>
        public List<int> ConflictVarNo;

        #endregion

        #region [ 方法 ]

        #region [ 构造函数 ]
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="objFuncCoeffi">目标函数系数</param>
        /// <param name="eqConstraintCoeffi">等式约束系数矩阵</param>
        /// <param name="eqConstraintValue">等式约束值</param>
        /// <param name="lsneqConstraintCoeffi">小于等于约束系数矩阵</param>
        /// <param name="lsneqConstraintValue">小于等于约束</param>
        /// <param name="upBoundIndexList">上界变量索引</param>
        /// <param name="upBoundList">对应上界</param>
        /// <param name="lowBoundIndexList">下界变量索引</param>
        /// <param name="lowBoundList">对应下界</param>
        public SolveLPWithCplex(List<double> objFuncCoeffi, List<List<double>> eqConstraintCoeffi, List<double> eqConstraintValue, List<List<double>> lsneqConstraintCoeffi, List<double> lsneqConstraintValue, List<int> upBoundIndexList, List<double> upBoundList, List<int> lowBoundIndexList, List<double> lowBoundList)
        {
            this._objFuncCoeffi = objFuncCoeffi;
            this._eqConstraintCoeffiMatrix = eqConstraintCoeffi;
            this._eqConstraintValue = eqConstraintValue;
            this._lsneqConstraintCoeffiMatrix = lsneqConstraintCoeffi;
            this._lsneqConstraintValue = lsneqConstraintValue;
            this._upBoundIndexList = upBoundIndexList;
            this._upBoundList = upBoundList;
            this._lowBoundIndexList = lowBoundIndexList;
            this._lowBoundList = lowBoundList;

            this._optSolution = new List<double>();
            this.ConflictConstraintNo = new List<int>();
            this.ConflictVarNo = new List<int>();
        }

        #endregion

        /// <summary>
        /// 求解该线性规划
        /// </summary>  
        public void SolveLP()
        {

            try
            {
                Cplex cplexLP = new Cplex();

                //Cplex.RegisterLicense(

                INumVar[] opSolutionList = new INumVar[this._objFuncCoeffi.Count];

                double[] objFuncEffi = new double[this._objFuncCoeffi.Count];

                int i = 0;
                int currentUpIndex = 0;
                int currentLowIndex = 0;

                foreach (double a in this._objFuncCoeffi)
                {
                    double upBound = System.Double.MaxValue;

                    double lowBound = 0;

                    //变量
                    if (currentUpIndex < this._upBoundIndexList.Count && this._upBoundIndexList[currentUpIndex] == i)
                    {
                        upBound = this._upBoundList[currentUpIndex];
                        currentUpIndex = currentUpIndex + 1;
                    }

                    if (currentLowIndex < this._lowBoundIndexList.Count && this._lowBoundIndexList[currentLowIndex] == i)
                    {
                        lowBound = this._lowBoundList[currentLowIndex];
                        currentLowIndex = currentLowIndex + 1;
                    }

                    opSolutionList[i] = cplexLP.NumVar(lowBound, upBound, NumVarType.Float, i.ToString());

                    objFuncEffi[i] = a;

                    i = i + 1;
                }

                //目标函数
                cplexLP.AddMinimize(cplexLP.ScalProd(objFuncEffi, opSolutionList));

                //等式约束
                i = 0;
                foreach (List<double> a in this._eqConstraintCoeffiMatrix)
                {
                    double[] eqConstraint = new double[a.Count];
                    int j = 0;
                    foreach (double b in a)
                    {
                        eqConstraint[j] = b;
                        j = j + 1;
                    }

                    cplexLP.AddEq(cplexLP.ScalProd(eqConstraint, opSolutionList), this._eqConstraintValue[i], i.ToString());
                    i = i + 1;
                }

                int lsEqNum = i;
                //小于不等式约束
                i = 0;
                foreach (List<double> a in this._lsneqConstraintCoeffiMatrix)
                {
                    double[] lseqConstraint = new double[a.Count];
                    int j = 0;
                    foreach (double b in a)
                    {
                        lseqConstraint[j] = b;
                        j = j + 1;
                    }

                    cplexLP.AddLe(cplexLP.ScalProd(lseqConstraint, opSolutionList), this._lsneqConstraintValue[i], lsEqNum.ToString());
                    i = i + 1;
                    lsEqNum = lsEqNum + 1;
                }
                cplexLP.ExportModel("././././QTPlan.lp");

                if (cplexLP.Solve())
                {
                    if (cplexLP.ObjValue == 0)
                        return;

                    this.Flag = 1;

                    this._optionalValue = cplexLP.ObjValue;

                    double[] tmpOptSolution = cplexLP.GetValues(opSolutionList);

                    foreach (double a in tmpOptSolution)
                    {
                        this.OptSolution.Add(a);

                    }

                    //this._optSolution

                }
                else
                {
                    Cplex.IIS tmpIIs = cplexLP.GetIIS();

                    //记录导致无可行解的约束
                    foreach (IConstraint a in tmpIIs.Constraints)
                    {
                        this.ConflictConstraintNo.Add(Convert.ToInt32(a.Name));

                    }

                    foreach (INumVar a in tmpIIs.NumVars)
                    {
                        this.ConflictVarNo.Add(Convert.ToInt32(a.Name));

                    }
                }


            }

            catch (ILOG.Concert.Exception ex)
            {
                System.Console.WriteLine("Concert Error: " + ex);
            }



        }

        #endregion


    }
}
