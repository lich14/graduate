using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SSWPF.Define.SimClasses;

namespace SolverTester
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Dictionary<uint, List<uint>> dMapInput;
            
            InputGenerator(out dMapInput);
            
            OutputDirMap(dMapInput);

            Console.ReadKey();

            // 强连通分量求解器，全强连通分量测试
            //TestStrongConnComponentSolver(dMapInput);

            // 强连通分量求解器，给定点集的强连通分量测试
            TestStrongConnComponentSolver(dMapInput, new List<uint>() { 58 });

            // 全基本环提取器测试
            //TestElementCyclesSolvor(dMapInput);

            // 给定点集的全基本环提取器测试
            // TestElementCyclesSolver(dMapInput, new List<uint>() { 0, 4, 3 });

        }

        /// <summary>
        /// 强连通分量求解器测试程序之一，返回全部连通分量
        /// </summary>
        /// <param name="dMapInput">输入图</param>
        private static void TestStrongConnComponentSolver(Dictionary<uint, List<uint>> dMapInput)
        {
            StrongConnectedComponentsSolver oSCCSolver;
            List<Dictionary<uint, List<uint>>> lStrongConnComponents;

            oSCCSolver = new StrongConnectedComponentsSolver(dMapInput);
            oSCCSolver.Solve();
            oSCCSolver.GetResults(out lStrongConnComponents);

            for (int i = 0; i < 20; i++)
                Console.Write("-");
            Console.WriteLine("\r\nStrong Connect Component Solver Test\r\n");
            foreach (Dictionary<uint, List<uint>> oMap in lStrongConnComponents)
                OutputDirMap(oMap);
            Console.ReadKey();
        }

        /// <summary>
        /// 强连通分量求解器测试程序之二，仅返回包含指定点的全连通分量
        /// </summary>
        /// <param name="dMapInput">输入图</param>
        /// <param name="lNodeIDs">给定点编号</param>
        private static void TestStrongConnComponentSolver(Dictionary<uint, List<uint>> dMapInput, List<uint> lNodeIDs)
        {
            StrongConnectedComponentsSolver oSCCSolver;
            List<Dictionary<uint, List<uint>>> lStrongConnComponents;
            DateTime dTemp;

            dTemp = DateTime.Now;
            oSCCSolver = new StrongConnectedComponentsSolver(dMapInput);
            oSCCSolver.Solve(lNodeIDs);
            oSCCSolver.GetResults(out lStrongConnComponents);
            Console.WriteLine("Time Used : " + new TimeSpan(DateTime.Now.Ticks).Subtract(new TimeSpan(dTemp.Ticks)).TotalSeconds.ToString());

            for (int i = 0; i < 20; i++)
                Console.Write("-");
            Console.Write("\r\nStrong Connect Component Solver Test With Given Nodes :");
            foreach (uint i in lNodeIDs)
                Console.Write("\t" + i.ToString());
            Console.Write("\r\n");
            foreach (Dictionary<uint, List<uint>> oMap in lStrongConnComponents)
                OutputDirMap(oMap);
            Console.ReadKey();
        }

        /// <summary>
        /// 全基本环提取器测试程序
        /// </summary>
        /// <param name="dMapInput">输入图</param>
        private static void TestElementCyclesSolver(Dictionary<uint, List<uint>> dMapInput)
        {
            ElementCyclesSolver oECS;
            List<Dictionary<uint, uint>> lElementCycles;

            oECS = new ElementCyclesSolver(dMapInput);
            oECS.Solve();
            oECS.GetResults(out lElementCycles);

            for (int i = 0; i < 20; i++)
                Console.Write("-");
            Console.WriteLine("\r\nElement Cycles Solver Test\r\n");
            foreach (Dictionary<uint, uint> Cycle in lElementCycles)
                OutPutDirCycle(Cycle);
            Console.ReadKey();
        }

        /// <summary>
        /// 给定点闭环提取测试程序
        /// </summary>
        /// <param name="dMapInput">输入图</param>
        /// <param name="lNodeIDs">给定点编号</param>
        private static void TestElementCyclesSolver(Dictionary<uint, List<uint>> dMapInput, List<uint> lNodeIDs)
        {
            ElementCyclesSolver oECS;
            List<Dictionary<uint, uint>> lElementCycles;

            oECS = new ElementCyclesSolver(dMapInput);
            oECS.Solve(lNodeIDs);
            oECS.GetResults(out lElementCycles);

            for (int i = 0; i < 20; i++)
                Console.Write("-");
            Console.Write("\r\nElement Cycles Solver Test With Given Nodes :");
            foreach (uint i in lNodeIDs)
                Console.Write("\t" + i.ToString());
            Console.Write("\r\n");
            foreach (Dictionary<uint, uint> Cycle in lElementCycles)
                OutPutDirCycle(Cycle);
            Console.ReadKey();
        }

        /// <summary>
        /// 图输出
        /// </summary>
        /// <param name="dMapInput">图</param>
        private static void OutputDirMap(Dictionary<uint, List<uint>> dMapInput)
        {
            Console.WriteLine("Map in Nodes And Arcs : ");

            Console.Write("\r\n");

            Console.Write("Node List: ");

            foreach (uint FromNode in dMapInput.Keys)
            {
                Console.Write("\t" + FromNode);
            }

            Console.Write("\r\n\r\n");

            Console.WriteLine("Arc List: ");

            Console.Write("\r\n");

            foreach (uint FromNode in dMapInput.Keys)
            {
                foreach (uint ToNode in dMapInput[FromNode])
                {
                    Console.WriteLine("Node : " + FromNode.ToString() + " --> Node : " + ToNode.ToString());
                }
            }

            Console.Write("\r\n\r\n");
        }

        /// <summary>
        /// 环输出
        /// </summary>
        /// <param name="dMapInput"></param>
        private static void OutPutDirCycle(Dictionary<uint, uint> dCycleInput)
        {
            uint NodeID;

            Console.WriteLine("Cycle Nodes In Sequence: ");
            Console.Write("\r\n");

            NodeID = dCycleInput.Keys.First();
            Console.Write(NodeID.ToString());
            do
            {
                Console.Write("\t-->\t" + dCycleInput[NodeID].ToString());
                NodeID = dCycleInput[NodeID];
            }
            while (NodeID != dCycleInput.Keys.First());

            Console.Write("\r\n\r\n");
        }


        private static void InputGenerator(out Dictionary<uint, List<uint>> dMapInput)
        {
            dMapInput = new Dictionary<uint, List<uint>>() { 
            { 0, new List<uint>() { 1 } }, { 1, new List<uint>() { 2 } }, { 2, new List<uint>() { 3 } }, { 3, new List<uint>() { 4 } }, { 4, new List<uint>() { 16, 17 } },
            { 5, new List<uint>() }, { 6, new List<uint>() { 19, 20 } }, { 7, new List<uint>() { 5, 6 } }, { 8, new List<uint>() { 7 } }, { 9, new List<uint>() { 8 } },
            { 10, new List<uint>() { 9 } }, { 11, new List<uint>() { 0 } }, { 12, new List<uint>() { 2 } }, { 13, new List<uint>() { 10, 11 } }, { 14, new List<uint>() { 3 } },
            { 15, new List<uint>() { 12, 13 } }, { 16, new List<uint>() { 14, 15 } }, { 17, new List<uint>() { 53, 54 } }, { 18, new List<uint>() { 16, 17 } }, { 19, new List<uint>() { 7 } },
            { 20, new List<uint>() { 21, 22 } }, { 21, new List<uint>() { 31, 32 } }, { 22, new List<uint>() { 23, 24 } }, { 23, new List<uint>() { 8 } }, { 24, new List<uint>() { 25, 26 } },
            { 25, new List<uint>() { 9 } }, { 26, new List<uint>() { 27, 28 } }, { 27, new List<uint>() { 29, 30 } }, { 28, new List<uint>() { 10, 11 } }, { 29, new List<uint>() { 53, 54 } },
            { 30, new List<uint>() { 14, 15 } }, { 31, new List<uint>() { 19, 20 } }, { 32, new List<uint>() { 39, 40 } }, { 33, new List<uint>() { 23, 24 } }, { 34, new List<uint>() { 31, 32 } },
            { 35, new List<uint>() { 33, 34 } }, { 36, new List<uint>() { 33, 34 } }, { 37, new List<uint>() { 35, 36 } }, { 38, new List<uint>() { 27, 28 } }, { 39, new List<uint>() { 41, 42 } },
            { 40, new List<uint>() { 37, 38 } }, { 41, new List<uint>() { 37, 38 } }, { 42, new List<uint>() { 43, 44 } }, { 43, new List<uint>() { 29, 30 } }, { 44, new List<uint>() { 51, 52 } },
            { 45, new List<uint>() { 43, 44 } }, { 46, new List<uint>() { 39, 40 } }, { 47, new List<uint>() { 41, 42 } }, { 48, new List<uint>() { 46 } }, { 49, new List<uint>() { 45 } },
            { 50, new List<uint>() { 47, 48 } }, { 51, new List<uint>() { 49, 50 } }, { 52, new List<uint>() { 56 } }, { 53, new List<uint>() { 57, 58 } }, { 54, new List<uint>() { 51, 52 } },
            { 55, new List<uint>() { 49, 50 } }, { 56, new List<uint>() { 68, 69 } }, { 57, new List<uint>() { 70, 71 } }, { 58, new List<uint>() { 56 } }, { 59, new List<uint>() { 46 } },
            { 60, new List<uint>() { 59 } }, { 61, new List<uint>() { 47, 48 } }, { 62, new List<uint>() { 60, 61 } }, { 63, new List<uint>() { 55 } }, { 64, new List<uint>() { 59 } },
            { 65, new List<uint>() { 60, 61 } }, { 66, new List<uint>() { 64 } }, { 67, new List<uint>() { 62, 63 } }, { 68, new List<uint>() { 67 } }, { 69, new List<uint>() { 75, 76 } },
            { 70, new List<uint>() { 77, 78 } }, { 71, new List<uint>() { 68, 69 } }, { 72, new List<uint>() { 65, 66 } }, { 73, new List<uint>() { 67 } }, { 74, new List<uint>() { 72 } },
            { 75, new List<uint>() { 80, 81 } }, { 76, new List<uint>() { 73, 74 } }, { 77, new List<uint>() { 82, 83 } }, { 78, new List<uint>() { 75, 76 } }, { 79, new List<uint>() { 64 } },
            { 80, new List<uint>() { 79 } }, { 81, new List<uint>() }, { 82, new List<uint>() { 80, 81 } }, { 83, new List<uint>() }
            };


            // 算例
            //dMapInput = new Dictionary<uint, List<uint>>() { 
            //    { 2015, new List<uint>() { 2023, 2545 } },
            //    { 2023, new List<uint>() { 2031 } },
            //    { 2031, new List<uint>() { 2119 } },
            //    { 2119, new List<uint>() { 5255 } },
            //    { 2288, new List<uint>() { 2031, 2023 } },
            //    { 2376, new List<uint>() { 2119, 2288 } },
            //    { 2545, new List<uint>() { 2288, 2633 } },
            //    { 2633, new List<uint>() { 2376, 2645 } },
            //    { 2645, new List<uint>() { 2376 } },
            //    { 3742, new List<uint>() { 2645 } },
            //    { 4289, new List<uint>() { 3742, 3742 } },
            //    { 5255, new List<uint>() { 4289 } } };

            //dMapInput = new Dictionary<uint, List<uint>>() { 
            //{ 0, new List<uint>() { 1 } }, 
            //{ 1, new List<uint>() { 2 } }, 
            //{ 2, new List<uint>() { 0, 6 } },
            //{ 3, new List<uint>() { 4 } },
            //{ 4, new List<uint>() { 5, 6 } },
            //{ 5, new List<uint>() { 3 } },
            //{ 6, new List<uint>() { 1, 7 } },
            //{ 7, new List<uint>() { 8 } },
            //{ 8, new List<uint>() { 6 } } };

            //dMapInput = new Dictionary<uint, List<uint>>() { 
            //{ 0, new List<uint>() { 1 } }, 
            //{ 1, new List<uint>() { 2 } }, 
            //{ 2, new List<uint>() { 0, 6 } },
            //{ 3, new List<uint>() { 4 } },
            //{ 4, new List<uint>() { 5, 6 } },
            //{ 5, new List<uint>() { 3 } },
            //{ 6, new List<uint>() { 1 } }
            //};

            //dMapInput = new Dictionary<uint, List<uint>>() { 
            //    { 2256, new List<uint>() },
            //    { 2276, new List<uint>() { 2256, 2557 } },
            //    { 2300, new List<uint>() { 2276 } },
            //    { 2557, new List<uint>() { 2625, 2300 } },
            //    { 2625, new List<uint>() { 2917 } },
            //    { 2882, new List<uint>() { 2625, 2557 } },
            //    { 2917, new List<uint>() { 2882 } }
            //    };

        }
    }
}
