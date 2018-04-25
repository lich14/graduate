using System;
using System.Collections.Generic;
using System.Linq;
using SSWPF.Define;
using ZECS.Schedule.DBDefine.Schedule;
using SharpSim;

namespace SSWPF.SimManagers
{
    public class SimOrderCommUnifier
    {
        private SimDataStore oSimDataStore;


        public SimOrderCommUnifier()
        {
        }

        public SimOrderCommUnifier(SimDataStore oSimDataStore)
            : this()
        {
            this.oSimDataStore = oSimDataStore;
        }

        /// <summary>
        /// 统一化 Order 和 Command
        /// </summary>
        /// <param name="IsSTSUnify">是否统一 STS_Order 和 STS_Command</param>
        /// <param name="IsASCUnify">是否统一 ASC_Order 和 ASC_Command</param>
        /// <param name="IsAGVUnify">是否统一 AGV_Order 和 AGV_Command</param>
        public void OrderCommUnify(bool IsSTSUnify = true, bool IsASCUnify = true, bool IsAGVUnify = true)
        {
            int OrdVersionOfOrd, OrdVersionOfComm, CommVersionOfOrd, CommVersionOfComm;
            List<string> lIDs;
            STS_Order oSTSOrd;
            STS_Command oSTSComm;
            ASC_Order oASCOrd;
            ASC_Command oASCComm;
            AGV_Order oAGVOrd;
            AGV_Command oAGVComm;

            if (IsSTSUnify)
            {
                lIDs = this.oSimDataStore.dSTSOrders.Keys.Intersect(this.oSimDataStore.dSTSCommands.Keys).ToList();

                foreach (string ID in lIDs)
                {
                    oSTSOrd = this.oSimDataStore.dSTSOrders[ID];
                    oSTSComm = this.oSimDataStore.dSTSCommands[ID];

                    if (oSTSOrd.ORDER_VERSION == null)
                        OrdVersionOfOrd = 0;
                    else
                        OrdVersionOfOrd = Convert.ToInt32(oSTSOrd.ORDER_VERSION);

                    if (oSTSOrd.COMMAND_VERSION == null)
                        CommVersionOfOrd = 0;
                    else
                        CommVersionOfOrd = Convert.ToInt32(oSTSOrd.COMMAND_VERSION);

                    if (oSTSComm.ORDER_VERSION == null)
                        OrdVersionOfComm = 0;
                    else
                        OrdVersionOfComm = Convert.ToInt32(oSTSComm.ORDER_VERSION);

                    if (oSTSComm.COMMAND_VERSION == null)
                        CommVersionOfComm = 0;
                    else
                        CommVersionOfComm = Convert.ToInt32(oSTSComm.COMMAND_VERSION);

                    if (CommVersionOfComm > CommVersionOfOrd)
                    {
                        if (OrdVersionOfComm < CommVersionOfComm)
                            oSTSComm.ORDER_VERSION = oSTSComm.COMMAND_VERSION;
                        this.UnifySTSOrderFromCommand(ID);
                    }
                    else if (OrdVersionOfOrd > OrdVersionOfComm)
                    {
                        if (CommVersionOfOrd < OrdVersionOfOrd)
                            oSTSOrd.COMMAND_VERSION = oSTSOrd.ORDER_VERSION;
                        this.UnifySTSCommandFromOrder(ID);
                    }
                }
            }

            if (IsASCUnify)
            {
                lIDs = this.oSimDataStore.dASCOrders.Keys.Intersect(this.oSimDataStore.dASCCommands.Keys).ToList();

                foreach (string ID in lIDs)
                {
                    oASCOrd = this.oSimDataStore.dASCOrders[ID];
                    oASCComm = this.oSimDataStore.dASCCommands[ID];

                    if (oASCOrd.ORDER_VERSION == null)
                        OrdVersionOfOrd = 0;
                    else
                        OrdVersionOfOrd = Convert.ToInt32(oASCOrd.ORDER_VERSION);

                    if (oASCOrd.COMMAND_VERSION == null)
                        CommVersionOfOrd = 0;
                    else
                        CommVersionOfOrd = Convert.ToInt32(oASCOrd.COMMAND_VERSION);

                    if (oASCComm.ORDER_VERSION == null)
                        OrdVersionOfComm = 0;
                    else
                        OrdVersionOfComm = Convert.ToInt32(oASCComm.ORDER_VERSION);

                    if (oASCComm.COMMAND_VERSION == null)
                        CommVersionOfComm = 0;
                    else
                        CommVersionOfComm = Convert.ToInt32(oASCComm.COMMAND_VERSION);

                    if (CommVersionOfComm > CommVersionOfOrd)
                    {
                        if (OrdVersionOfComm < CommVersionOfComm)
                            oASCComm.ORDER_VERSION = oASCComm.COMMAND_VERSION;
                        this.UnifySTSOrderFromCommand(ID);
                    }
                    else if (OrdVersionOfOrd > OrdVersionOfComm)
                    {
                        if (CommVersionOfOrd < OrdVersionOfOrd)
                            oASCOrd.COMMAND_VERSION = oASCOrd.ORDER_VERSION;
                        this.UnifySTSCommandFromOrder(ID);
                    }
                }
            }

            if (IsAGVUnify)
            {
                lIDs = this.oSimDataStore.dAGVOrders.Keys.Intersect(this.oSimDataStore.dAGVCommands.Keys).ToList();

                foreach (string ID in lIDs)
                {
                    oAGVOrd = this.oSimDataStore.dAGVOrders[ID];
                    oAGVComm = this.oSimDataStore.dAGVCommands[ID];

                    if (oAGVOrd.ORDER_VERSION == null)
                        OrdVersionOfOrd = 0;
                    else
                        OrdVersionOfOrd = Convert.ToInt32(oAGVOrd.ORDER_VERSION);

                    if (oAGVOrd.COMMAND_VERSION == null)
                        CommVersionOfOrd = 0;
                    else
                        CommVersionOfOrd = Convert.ToInt32(oAGVOrd.COMMAND_VERSION);

                    if (oAGVComm.ORDER_VERSION == null)
                        OrdVersionOfComm = 0;
                    else
                        OrdVersionOfComm = Convert.ToInt32(oAGVComm.ORDER_VERSION);

                    if (oAGVComm.COMMAND_VERSION == null)
                        CommVersionOfComm = 0;
                    else
                        CommVersionOfComm = Convert.ToInt32(oAGVComm.COMMAND_VERSION);

                    if (CommVersionOfComm > CommVersionOfOrd)
                    {
                        if (OrdVersionOfComm < CommVersionOfComm)
                            oAGVComm.ORDER_VERSION = oAGVComm.COMMAND_VERSION;
                        this.UnifyAGVOrderFromCommand(ID);
                    }
                    else if (OrdVersionOfOrd > OrdVersionOfComm)
                    {
                        if (CommVersionOfOrd < OrdVersionOfOrd)
                            oAGVOrd.COMMAND_VERSION = oAGVOrd.ORDER_VERSION;
                        this.UnifyAGVCommandFromOrder(ID);
                    }
                }
            }

        }

        /// <summary>
        /// 用特定编号的 STSCommand 更新对应的 STSOrder
        /// </summary>
        /// <param name="ID">STSCommand编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool UnifySTSOrderFromCommand(string ID)
        {
            STS_Command oComm;
            STS_Order oOrd;

            oComm = this.oSimDataStore.dSTSCommands[ID];
            oOrd = this.oSimDataStore.dSTSOrders[ID];

            oOrd.CopyCmd(oComm);

            oOrd.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

            return true;
        }

        /// <summary>
        /// 用特定编号的 STSOrder 更新对应的 STSCommand
        /// </summary>
        /// <param name="ID">STSOrder编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool UnifySTSCommandFromOrder(string ID)
        {
            STS_Order oOrd;
            STS_Command oComm;
            Order_Command_Base oCommBasic;

            oOrd = this.oSimDataStore.dSTSOrders[ID];
            oComm = this.oSimDataStore.dSTSCommands[ID];

            oCommBasic = new Order_Command_Base();
            oCommBasic.Copy(oOrd);
            oComm.Copy(oCommBasic);

            oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

            return true;
        }

        /// <summary>
        /// 用特定编号的 ASCCommand 更新对应的 STSOrder
        /// </summary>
        /// <param name="ID">ASCCommand编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool UnifyASCOrderFromCommand(string ID)
        {
            ASC_Order oOrd;
            ASC_Command oComm;

            oComm = this.oSimDataStore.dASCCommands[ID];
            oOrd = this.oSimDataStore.dASCOrders[ID];

            oOrd.CopyCmd(oComm);

            oOrd.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

            return true;
        }

        /// <summary>
        /// 用特定编号的 ASCOrder 更新对应的 ASCCommand
        /// </summary>
        /// <param name="ID">ASCOrder编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool UnifyASCCommandFromOrder(string ID)
        {
            ASC_Order oOrd;
            ASC_Command oComm;
            Order_Command_Base oCommBasic;

            oOrd = this.oSimDataStore.dASCOrders[ID];
            oComm = this.oSimDataStore.dASCCommands[ID];

            oCommBasic = new Order_Command_Base();
            oCommBasic.Copy(oOrd);
            oComm.Copy(oCommBasic);

            oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

            return true;
        }

        /// <summary>
        /// 用特定编号的 AGVCommand 更新对应的 AGVOrder
        /// </summary>
        /// <param name="ID">AGVCommand编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool UnifyAGVOrderFromCommand(string ID)
        {
            AGV_Order oOrd;
            AGV_Command oComm;

            // Command 的 ORDER_VERSION 和 COMMAND_VERSION 应一致，但是 Order 还没有对 Command 的更新做出反应
            oComm = this.oSimDataStore.dAGVCommands[ID];
            oOrd = this.oSimDataStore.dAGVOrders[ID];

            oOrd.CopyCmd(oComm);

            oOrd.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

            return true;
        }

        /// <summary>
        /// 用特定编号的 AGVOrder 更新对应的 AGVCommand
        /// </summary>
        /// <param name="ID">AGVOrder编号</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool UnifyAGVCommandFromOrder(string ID)
        {
            AGV_Order oOrd;
            AGV_Command oComm;
            Order_Command_Base oOrdCommBasic;

            // Order 的 ORDER_VERSION 和 COMMAND_VERSION 应一致，但是 Command 还没有对 Order 的更新做出反应
            oOrd = this.oSimDataStore.dAGVOrders[ID];
            oComm = this.oSimDataStore.dAGVCommands[ID];

            oOrdCommBasic = new Order_Command_Base();
            oOrdCommBasic.Copy(oOrd);
            oComm.Copy(oOrdCommBasic);

            oComm.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

            return true;
        }
    }
}
