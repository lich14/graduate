using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSWPF.Define
{
    /* 注意在本部分的求解器中，同起点、同终点的有向边是唯一的
     * 否则得不到结果 
     */

    /// <summary>
    /// 从有向图中提取强连通分量，形成集合输出
    /// </summary>
    public class StrongConnectedComponentsSolver
    {
        private Dictionary<uint, List<uint>> _dRawDirGraph;
        private List<Dictionary<uint, List<uint>>> _lStrongConnComponents;
        private List<uint> Stack;
        private Dictionary<uint, int> Dfn;
        private Dictionary<uint, int> Low;
        private SolverEnums.SolverStatus _eSolverStatus;
        private SolverEnums.SolutionType _eSolutionType;
        private List<uint> _lGivenNodeIDs;
        private int TimeStamp;

        public Dictionary<uint, List<uint>> dRawDirGraph
        {
            get { return this._dRawDirGraph; }
            set { }
        }
        public SolverEnums.SolverStatus eSolverStatus
        {
            get { return this._eSolverStatus; }
            set { }
        }
        public List<Dictionary<uint, List<uint>>> lStrongConnComponents
        {
            get { return this._lStrongConnComponents; }
            set { }
        }
        public SolverEnums.SolutionType eSolutionType
        {
            get { return this._eSolutionType; }
            set { }
        }
        public List<uint> lGivenNodeIDs
        {
            get { return this._lGivenNodeIDs; }
            set { }
        }

        /// <summary>
        /// 实例化
        /// </summary>
        public StrongConnectedComponentsSolver()
        {
            this._eSolverStatus = SolverEnums.SolverStatus.NoInput;
            this._eSolutionType = SolverEnums.SolutionType.Null;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="dDirGraphInput">图输入，键值为点，值为边终点列表。注意不要有重复边</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool Init(Dictionary<uint, List<uint>> dDirGraphInput)
        {
            this._dRawDirGraph = dDirGraphInput;
            if (this.NoEquivalentEdgeTest(dDirGraphInput))
            {
                this._eSolverStatus = SolverEnums.SolverStatus.Inited;
                return true;
            }
            else
            {
                this._eSolverStatus = SolverEnums.SolverStatus.InputCheckFailedForEquivalentEdges;
                return false;
            }
        }

        /// <summary>
        /// 带参初始化
        /// </summary>
        /// <param name="dDirGraphInput">图输入，键值为点，值为边终点列表。注意不要有重复边</param>
        public StrongConnectedComponentsSolver(Dictionary<uint, List<uint>> dDirGraphInput)
            : this()
        {
            this.Init(dDirGraphInput);
        }

        /// <summary>
        /// 求解全部强连通分量。注意先初始化
        /// </summary>
        /// <returns></returns>
        public bool Solve()
        {
            if (this._eSolverStatus != SolverEnums.SolverStatus.Inited) return false;

            // 只要被访问过就会有Dfn
            this.Dfn = new Dictionary<uint, int>();
            this.Low = new Dictionary<uint, int>();
            this._lStrongConnComponents = new List<Dictionary<uint, List<uint>>>();
            this.TimeStamp = 0;

            foreach (uint CurrNodeID in this._dRawDirGraph.Keys)
            {
                if (!this.Dfn.ContainsKey(CurrNodeID))
                {
                    this.Stack = new List<uint>();
                    this.Tarjan(CurrNodeID);
                }
            }

            this._eSolverStatus = SolverEnums.SolverStatus.StrongConnComponentsSolved;
            this._eSolutionType = SolverEnums.SolutionType.AllNodes;

            return true;
        }

        /// <summary>
        /// 求解包含点列表 lNodeIDs 中的所有点的强连通分量。注意先初始化
        /// </summary>
        /// <param name="NodeID"></param>
        /// <returns></returns>
        public bool Solve(List<uint> lNodeIDs)
        {
            if (this._eSolverStatus != SolverEnums.SolverStatus.Inited) return false;

            this.Dfn = new Dictionary<uint, int>();
            this.Low = new Dictionary<uint, int>();
            this._lStrongConnComponents = new List<Dictionary<uint, List<uint>>>();
            this.TimeStamp = 0;

            foreach (uint CurrNodeID in lNodeIDs)
            {
                if (this._dRawDirGraph.ContainsKey(CurrNodeID) && !this.Dfn.ContainsKey(CurrNodeID))
                {
                    this.Stack = new List<uint>();
                    this.Tarjan(CurrNodeID);
                }
            }

            this._lStrongConnComponents.RemoveAll(u => u.Keys.Intersect(lNodeIDs).Count() == 0);

            this._eSolverStatus = SolverEnums.SolverStatus.StrongConnComponentsSolved;
            this._eSolutionType = SolverEnums.SolutionType.WithGivenNodes;
            this._lGivenNodeIDs = lNodeIDs;

            return true;
        }

        // Tarjan 迭代
        private void Tarjan(uint CurrNodeID)
        {
            List<uint> lNodeIDs;
            this.TimeStamp++;
            this.Dfn.Add(CurrNodeID, TimeStamp);
            this.Low.Add(CurrNodeID, TimeStamp);
            this.Stack.Add(CurrNodeID);

            if (this._dRawDirGraph.ContainsKey(CurrNodeID))
            {
                foreach (uint NextNodeID in this._dRawDirGraph[CurrNodeID])
                {
                    if (!Dfn.ContainsKey(NextNodeID))
                    {
                        Tarjan(NextNodeID);
                        this.Low[CurrNodeID] = Math.Min(Low[NextNodeID], Low[CurrNodeID]);
                    }
                    else if (this.Stack.Contains(NextNodeID))
                        this.Low[CurrNodeID] = Math.Min(Low[CurrNodeID], Dfn[NextNodeID]);
                }
            }

            if (Dfn[CurrNodeID] == Low[CurrNodeID])
            {
                // 新建强连通分量结构
                this._lStrongConnComponents.Add(new Dictionary<uint, List<uint>>());
                lNodeIDs = new List<uint>();

                do
                {
                    this._lStrongConnComponents.Last().Add(this.Stack.Last(), new List<uint>());
                    lNodeIDs.Add(this.Stack.Last());
                    this.Stack.RemoveAt(this.Stack.Count - 1);
                }
                while (lNodeIDs.Last() != CurrNodeID);

                // 补全连通分量的边
                foreach (uint NodeID in lNodeIDs)
                {
                    if (this._dRawDirGraph.ContainsKey(NodeID))
                        this._lStrongConnComponents.Last()[NodeID] = this._dRawDirGraph[NodeID].Where(u => lNodeIDs.Contains(u)).ToList();
                }
            }
        }

        /// <summary>
        /// 强连通分量求解器输出
        /// </summary>
        /// <param name="lSCCOutPut">强连通分量图列表，列表元素的键表示点，值表示后续点列表。输入有误时返回null</param>
        public void GetResults(out List<Dictionary<uint, List<uint>>> lSCCOutPut)
        {
            lSCCOutPut = new List<Dictionary<uint, List<uint>>>();

            if (this._eSolverStatus != SolverEnums.SolverStatus.StrongConnComponentsSolved)
                return;

            lSCCOutPut = this._lStrongConnComponents;
        }

        /// <summary>
        /// 输入无等效边检测。同起点到同终点的边必须唯一。
        /// </summary>
        /// <param name="dDirGraphInput">图输入</param>
        /// <returns>无等效边返回true，有返回false</returns>
        private bool NoEquivalentEdgeTest(Dictionary<uint, List<uint>> dDirGraphInput)
        {
            bool bRet = true;

            foreach (List<uint> lNodeIDs in dDirGraphInput.Values)
            {
                if (lNodeIDs.Distinct().Count() < lNodeIDs.Count)
                {
                    bRet = false;
                    return bRet;
                }
            }

            return bRet;
        }
    }


    /// <summary>
    /// 从图中提取所有基本环。注意引用了 StrongConnectComponentSolver
    /// </summary>
    public class ElementCyclesSolver
    {
        private Dictionary<uint, List<uint>> _dRawDirGraph;
        public List<Dictionary<uint, List<uint>>> lStrongConnComponents;
        private Dictionary<uint, bool> dBlockNodeIDs;
        private Dictionary<uint, List<uint>> dBackBlockNodeIDs;
        private List<uint> Stack;
        private Dictionary<uint, List<uint>> oTempSCC;
        private List<Dictionary<uint, uint>> _lElementCycles;
        public StrongConnectedComponentsSolver oSCCSolver;
        private SolverEnums.SolverStatus _eSolverStatus;
        private SolverEnums.SolutionType _eSolutionType;
        private List<uint> _lGivenNodeIDs;

        public SolverEnums.SolverStatus eSolverStatus
        {
            get { return this._eSolverStatus; }
            set { }
        }
        public Dictionary<uint, List<uint>> dRawDirGraph
        {
            get { return this._dRawDirGraph; }
            set { }
        }
        public List<Dictionary<uint, uint>> lElementCycles
        {
            get { return this._lElementCycles; }
            set { }
        }
        public SolverEnums.SolutionType eSolutionType
        {
            get { return this._eSolutionType; }
            set { }
        }
        public List<uint> lGivenNodeIDs
        {
            get { return this._lGivenNodeIDs; }
            set { }
        }

        /// <summary>
        /// 实例化
        /// </summary>
        public ElementCyclesSolver()
        {
            this._eSolverStatus = SolverEnums.SolverStatus.NoInput;
            this._eSolutionType = SolverEnums.SolutionType.Null;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="dDirGraphInput">图输入。键值为点编号，值为边终点编号列表。注意不要有重复边</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool Init(Dictionary<uint, List<uint>> dDirGraphInput)
        {
            this._dRawDirGraph = dDirGraphInput;
            if (this.NoEquivalentEdgeTest(dDirGraphInput))
            {
                this.oSCCSolver = new StrongConnectedComponentsSolver(dDirGraphInput);
                this._eSolverStatus = SolverEnums.SolverStatus.Inited;
                return true;
            }
            else
            {
                this._eSolverStatus = SolverEnums.SolverStatus.InputCheckFailedForEquivalentEdges;
                return false;
            }
        }

        /// <summary>
        /// 带参初始化
        /// </summary>
        /// <param name="dDirGraphInput">图输入。键值为点编号，值为边终点编号列表。注意不要有重复边</param>
        public ElementCyclesSolver(Dictionary<uint, List<uint>> dDirGraphInput)
            :this()
        {
            this.Init(dDirGraphInput);
        }

        /// <summary>
        /// 从有向图到强连通分量
        /// </summary>
        /// <returns>成功返回true，失败返回false</returns>
        public bool SolveFromDirGraphToStrongConnComponents()
        {
            if (this._eSolverStatus != SolverEnums.SolverStatus.Inited) return false;

            this.oSCCSolver.Solve();
            this.oSCCSolver.GetResults(out this.lStrongConnComponents);
            this._eSolverStatus = SolverEnums.SolverStatus.StrongConnComponentsSolved;
            return true;
        }

        /// <summary>
        /// 从有向图到包含特定点的强连通分量
        /// </summary>
        /// <param name="lNodeIDs"></param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool SolveFromDirGraphToStrongConnComponents(List<uint> lNodeIDs)
        {
            if (this._eSolverStatus != SolverEnums.SolverStatus.Inited) return false;

            this.oSCCSolver.Solve(lNodeIDs);
            this.oSCCSolver.GetResults(out this.lStrongConnComponents);
            this._eSolverStatus = SolverEnums.SolverStatus.StrongConnComponentsSolved;
            return true;
        }

        /// <summary>
        /// 从强连通分量到全闭环
        /// </summary>
        /// <returns>成功返回true，失败返回false</returns>
        public bool SolveFromStrongConnComponentsToElementCycles()
        {
            uint TempNodeID;

            this._lElementCycles = new List<Dictionary<uint, uint>>();
            if (this._eSolverStatus != SolverEnums.SolverStatus.StrongConnComponentsSolved) return false;

            foreach (Dictionary<uint, List<uint>> oSCC in this.lStrongConnComponents)
            {
                if (oSCC.Count > 1)
                {
                    oTempSCC = new Dictionary<uint, List<uint>>(oSCC);

                    while (oTempSCC.Count > 0)
                    {
                        TempNodeID = oTempSCC.Keys.First();
                        this.dBlockNodeIDs = new Dictionary<uint, bool>();
                        this.dBackBlockNodeIDs = new Dictionary<uint, List<uint>>();
                        foreach (uint iKey in oTempSCC.Keys)
                        {
                            this.dBlockNodeIDs.Add(iKey, false);
                            this.dBackBlockNodeIDs.Add(iKey, new List<uint>());
                        }
                        this.Stack = new List<uint>();
                        this.LookForElementCycles(TempNodeID, TempNodeID, oTempSCC);
                        oTempSCC.Remove(TempNodeID);
                        foreach (uint iKey in oTempSCC.Keys)
                            oTempSCC[iKey].Remove(TempNodeID);
                    }
                }
            }

            this._eSolverStatus = SolverEnums.SolverStatus.ElementCyclesSolved;
            this._eSolutionType = SolverEnums.SolutionType.AllNodes;
            return true;
        }

        /// <summary>
        /// 从强连通分量到包含特定点的全闭环
        /// </summary>
        /// <param name="lNodeIDs">包含点列表</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool SolveFromStrongConnComponentsToElementCycles(List<uint> lNodeIDs)
        {
            uint TempNodeID;
            List<uint> lTempNodeIDs;

            this._lElementCycles = new List<Dictionary<uint, uint>>();
            this._lGivenNodeIDs = lNodeIDs;

            if (this._eSolverStatus != SolverEnums.SolverStatus.StrongConnComponentsSolved) return false;

            foreach (Dictionary<uint, List<uint>> oSCC in this.lStrongConnComponents)
            {
                if (oSCC.Count > 1)
                {
                    oTempSCC = new Dictionary<uint, List<uint>>(oSCC);
                    lTempNodeIDs = oTempSCC.Keys.Intersect(lNodeIDs).ToList();

                    while (lTempNodeIDs.Count > 0)
                    {
                        TempNodeID = lTempNodeIDs[0];
                        this.dBlockNodeIDs = new Dictionary<uint, bool>();
                        this.dBackBlockNodeIDs = new Dictionary<uint, List<uint>>();
                        foreach (uint iKey in oTempSCC.Keys)
                        {
                            this.dBlockNodeIDs.Add(iKey, false);
                            this.dBackBlockNodeIDs.Add(iKey, new List<uint>());
                        }
                        this.Stack = new List<uint>();
                        this.LookForElementCycles(TempNodeID, TempNodeID, oTempSCC);
                        lTempNodeIDs.Remove(TempNodeID);
                        oTempSCC.Remove(TempNodeID);
                        foreach (uint iKey in oTempSCC.Keys)
                            oTempSCC[iKey].Remove(TempNodeID);
                    }
                }
            }

            this._eSolverStatus = SolverEnums.SolverStatus.ElementCyclesSolved;
            this._eSolutionType = SolverEnums.SolutionType.WithGivenNodes;
            return true;
        }

        /// <summary>
        /// 求解全部基本环。注意先初始化
        /// </summary>
        /// <returns></returns>
        public bool Solve()
        {
            bool bRet = true;
            if (this._eSolverStatus != SolverEnums.SolverStatus.Inited)
                bRet = false;
            if (bRet)
                bRet = this.SolveFromDirGraphToStrongConnComponents();
            if (bRet)
                bRet = this.SolveFromStrongConnComponentsToElementCycles();
            return bRet;
        }

        /// <summary>
        /// 求解带有给定点的基本环。注意先初始化
        /// </summary>
        /// <param name="NodeID"></param>
        /// <returns></returns>
        public bool Solve(List<uint> lNodeIDs)
        {
            bool bRet = true;
            if (this._eSolverStatus != SolverEnums.SolverStatus.Inited)
                bRet = false;
            if (bRet)
                bRet = this.SolveFromDirGraphToStrongConnComponents(lNodeIDs);
            if (bRet)
                bRet = this.SolveFromStrongConnComponentsToElementCycles(lNodeIDs);
            return bRet;
        }

        private bool LookForElementCycles(uint CurrNodeID, uint StartNodeID, Dictionary<uint, List<uint>> oTempSCC)
        {
            Dictionary<uint, uint> Cycle;
            bool bRet = false;
            this.Stack.Add(CurrNodeID);
            this.dBlockNodeIDs[CurrNodeID] = true;

            foreach (uint NextNodeID in oTempSCC[CurrNodeID])
            {
                if (NextNodeID == StartNodeID)
                {
                    Cycle = new Dictionary<uint, uint>();
                    for (int i = 0; i < this.Stack.Count; i++)
                    {
                        if (i < this.Stack.Count - 1)
                            Cycle.Add(this.Stack[i], this.Stack[i + 1]);
                        else
                            Cycle.Add(this.Stack.Last(), this.Stack[0]);
                    }
                    this._lElementCycles.Add(Cycle);
                    bRet = true;
                }
                else if (!this.dBlockNodeIDs[NextNodeID])
                    if (this.LookForElementCycles(NextNodeID, StartNodeID, oTempSCC)) bRet = true;
            }

            if (bRet)
                this.Unblock(CurrNodeID);
            else
            {
                foreach (uint NextNodeID in oTempSCC[CurrNodeID])
                {
                    if (!this.dBackBlockNodeIDs[NextNodeID].Contains(CurrNodeID))
                        this.dBackBlockNodeIDs[NextNodeID].Add(CurrNodeID);
                }
            }

            this.Stack.Remove(CurrNodeID);

            return bRet;
        }

        private void Unblock(uint CurrNodeID)
        {
            List<uint> lTempBBNodeIDs;
            uint BBNodeID;

            this.dBlockNodeIDs[CurrNodeID] = false;

            lTempBBNodeIDs = this.dBackBlockNodeIDs[CurrNodeID];

            while (lTempBBNodeIDs.Count > 0)
            {
                BBNodeID = lTempBBNodeIDs[0];
                lTempBBNodeIDs.RemoveAt(0);
                if (this.dBlockNodeIDs[BBNodeID]) this.Unblock(BBNodeID);
            }
        }

        /// <summary>
        /// 输入无等效边检测。同起点到同终点的边必须唯一。
        /// </summary>
        /// <param name="dDirGraphInput">图输入</param>
        /// <returns>无等效边返回true，有返回false</returns>
        private bool NoEquivalentEdgeTest(Dictionary<uint, List<uint>> dDirGraphInput)
        {
            bool bRet = true;

            foreach (List<uint> lNodeIDs in dDirGraphInput.Values)
            {
                if (lNodeIDs.Distinct().Count() < lNodeIDs.Count)
                {
                    bRet = false;
                    return bRet;
                }
            }

            return bRet;
        }

        /// <summary>
        /// 取出结果，基本环集合
        /// </summary>
        /// <param name="lElementaryCyclesOutput"></param>
        public void GetResults(out List<Dictionary<uint, uint>> lElementaryCyclesOutput)
        {
            lElementaryCyclesOutput = new List<Dictionary<uint, uint>>();

            if (this.eSolverStatus != SolverEnums.SolverStatus.ElementCyclesSolved)
                return;

            lElementaryCyclesOutput = this._lElementCycles;
        }
    }

    public class SolverEnums
    {
        public enum SolverStatus : byte
        {
            NoInput = 0, InputCheckFailedForEquivalentEdges = 1, Inited = 2, StrongConnComponentsSolved = 3, ElementCyclesSolved = 4
        }

        public enum SolutionType : byte
        {
            Null = 0, AllNodes = 1, WithGivenNodes = 2
        }
    }

}
