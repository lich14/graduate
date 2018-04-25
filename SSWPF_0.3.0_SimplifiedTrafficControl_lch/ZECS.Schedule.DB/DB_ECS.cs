using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using SharpSim;
using SSWPF.Define;
using ZECS.Schedule.Define.DBDefine.Schedule;

namespace ZECS.Schedule.DB
{
    public class DB_ECS
    {
        private static DB_ECS s_instance;
        public static DB_ECS Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new DB_ECS();
                }
                return s_instance;
            }
        }
        public SimDataStore oSimDataStore;

        public void Start(DatabaseConfig Database_ECS_STSMS, DatabaseConfig Database_ECS_VMS, DatabaseConfig Database_ECS_BMS)
        {
            //Logger.JobManager_ECS.Info("DB_ECS Started");
        }

        public bool Stop()
        {
            return false;
        }

        public bool Insert_STS_Order(STS_Order order)
        {
            order.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            oSimDataStore.dSTSOrders.Add(order.ORDER_ID, order);
            return true;
        }

        public bool Insert_STS_Command(STS_Command cmd)
        {
            cmd.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            oSimDataStore.dSTSCommands.Add(cmd.COMMAND_ID, cmd);
            return true;
        }

        public bool Insert_AGV_Order(AGV_Order order)
        {
            order.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            oSimDataStore.dAGVOrders.Add(order.ORDER_ID, order);
            return true;
        }

        public bool Insert_AGV_Command(AGV_Command cmd)
        {
            cmd.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            oSimDataStore.dAGVCommands.Add(cmd.COMMAND_ID, cmd);
            return true;
        }

        public bool Insert_ASC_Order(ASC_Order order)
        {
            order.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            oSimDataStore.dASCOrders.Add(order.ORDER_ID, order);
            return true;
        }

        public bool Insert_ASC_Command(ASC_Command cmd)
        {
            cmd.DATETIME = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
            oSimDataStore.dASCCommands.Add(cmd.COMMAND_ID, cmd);
            return true;
        }

        public List<STS_Order> GetList_STS_Order()
        {
            STS_Order oSO;
            List<STS_Order> lSOs = new List<STS_Order>();

            foreach (STS_Order obj in oSimDataStore.dSTSOrders.Values)
            {
                oSO = Helper.Clone<STS_Order>(obj);
                lSOs.Add(oSO);
            }

            return lSOs;
        }

        public bool Update_STS_Order(STS_Order order)
        {
            bool bUpdateResult = false;

            if (oSimDataStore.dSTSOrders.ContainsKey(order.ORDER_ID))
            {
                if (Convert.ToInt32(oSimDataStore.dSTSOrders[order.ORDER_ID].ORDER_VERSION) < Convert.ToInt32(order.ORDER_VERSION))
                {
                    oSimDataStore.dSTSOrders[order.ORDER_ID] = order;
                    bUpdateResult = true;
                }
            }

            return bUpdateResult;
        }

        public List<STS_Command> GetList_STS_Command()
        {
            STS_Command oSC;
            List<STS_Command> lSCs = new List<STS_Command>();

            foreach (STS_Command obj in oSimDataStore.dSTSCommands.Values)
            {
                oSC = Helper.Clone<STS_Command>(obj);
                lSCs.Add(oSC);
            }

            return lSCs;
        }

        public bool Update_STS_Command(STS_Command cmd)
        {
            bool bUpdateResult = false;

            if (oSimDataStore.dSTSCommands.ContainsKey(cmd.COMMAND_ID))
            {
                if (Convert.ToInt32(oSimDataStore.dSTSCommands[cmd.COMMAND_ID].COMMAND_VERSION) < Convert.ToInt32(cmd.COMMAND_VERSION))
                {
                    oSimDataStore.dSTSCommands[cmd.COMMAND_ID] = cmd;
                    bUpdateResult = true;
                }
            }

            return bUpdateResult;
        }

        public bool Update_STS_STATUS(STS_STATUS status)
        {
            bool bUpdateResult = false;

            if (oSimDataStore.dSTSStatus.ContainsKey(status.QC_ID))
            {
                oSimDataStore.dSTSStatus[status.QC_ID] = status;
                bUpdateResult = true;
            }

            return bUpdateResult;
        }

        public List<AGV_Order> GetList_AGV_Order()
        {
            AGV_Order oAO;
            List<AGV_Order> lAOs = new List<AGV_Order>();
            foreach (AGV_Order obj in oSimDataStore.dAGVOrders.Values)
            {
                oAO = Helper.Clone<AGV_Order>(obj);
                lAOs.Add(oAO);
            }
            return lAOs;
        }

        public bool Update_AGV_Order(AGV_Order order)
        {
            bool bUpdateResult = false;

            if (oSimDataStore.dAGVOrders.ContainsKey(order.ORDER_ID))
            {
                if (Convert.ToInt32(oSimDataStore.dAGVOrders[order.ORDER_ID].ORDER_VERSION) < Convert.ToInt32(order.ORDER_VERSION))
                {
                    oSimDataStore.dAGVOrders[order.ORDER_ID] = order;
                    bUpdateResult = true;
                }
            }

            return bUpdateResult;
        }

        public List<AGV_Command> GetList_AGV_Command()
        {
            AGV_Command oAC;
            List<AGV_Command> lACs = new List<AGV_Command>();

            foreach (AGV_Command obj in oSimDataStore.dAGVCommands.Values)
            {
                oAC = Helper.Clone<AGV_Command>(obj);
                lACs.Add(oAC);
            }

            return lACs;
        }

        public bool Update_AGV_Command(AGV_Command cmd)
        {
            bool bUpdateResult = false;

            if (oSimDataStore.dAGVCommands.ContainsKey(cmd.CHE_ID))
            {
                if (Convert.ToInt32(oSimDataStore.dAGVCommands[cmd.CHE_ID].COMMAND_VERSION) < Convert.ToInt32(cmd.COMMAND_VERSION))
                {
                    oSimDataStore.dAGVCommands[cmd.CHE_ID] = cmd;
                    bUpdateResult = true;
                }
            }

            return bUpdateResult;
        }

        public bool Update_AGV_STATUS(AGV_STATUS status)
        {
            bool bUpdateResult = false;

            if (oSimDataStore.dAGVStatus.ContainsKey(status.CHE_ID))
            {
                oSimDataStore.dAGVStatus[status.CHE_ID] = status;
                bUpdateResult = true;
            }

            return bUpdateResult;
        }

        public List<ASC_Order> GetList_ASC_Order()
        {
            ASC_Order oAO;
            List<ASC_Order> lAOs = new List<ASC_Order>();

            foreach (ASC_Order obj in oSimDataStore.dASCOrders.Values)
            {
                oAO = Helper.Clone<ASC_Order>(obj);
                lAOs.Add(oAO);
            }

            return lAOs;
        }

        public bool Update_ASC_Order(ASC_Order order)
        {
            bool bUpdateResult = false;

            if (oSimDataStore.dASCOrders.ContainsKey(order.CHE_ID))
            {
                if (Convert.ToInt32(oSimDataStore.dASCOrders[order.CHE_ID].ORDER_VERSION) < Convert.ToInt32(order.ORDER_VERSION))
                {
                    oSimDataStore.dASCOrders[order.CHE_ID] = order;
                    bUpdateResult = true;
                }
            }

            return bUpdateResult;
        }

        public List<ASC_Command> GetList_ASC_Command()
        {
            ASC_Command oAC;
            List<ASC_Command> lACs = new List<ASC_Command>();

            foreach (ASC_Command obj in oSimDataStore.dASCCommands.Values)
            {
                oAC = Helper.Clone<ASC_Command>(obj);
                lACs.Add(oAC);
            }

            return lACs;
        }

        public bool Update_ASC_Command(ASC_Command cmd)
        {
            bool bUpdateResult = false;

            if (oSimDataStore.dASCCommands.ContainsKey(cmd.COMMAND_ID))
            {
                if (Convert.ToInt32(oSimDataStore.dASCCommands[cmd.COMMAND_ID].COMMAND_VERSION) < Convert.ToInt32(cmd.COMMAND_VERSION))
                {
                    oSimDataStore.dASCCommands[cmd.COMMAND_ID] = cmd;
                    bUpdateResult = true;
                }
            }

            return bUpdateResult;
        }

        public bool Update_ASC_STATUS(ASC_STATUS status)
        {
            bool bUpdateResult = false;

            if (oSimDataStore.dASCStatus.ContainsKey(status.CHE_ID))
            {
                oSimDataStore.dASCStatus[status.CHE_ID] = status;
                bUpdateResult = true;
            }

            return bUpdateResult;
        }

        public List<STS_STATUS> GetList_STS_STATUS()
        {
            STS_STATUS oSS;
            List<STS_STATUS> lSSs = new List<STS_STATUS>();

            foreach (STS_STATUS obj in oSimDataStore.dSTSStatus.Values)
            {
                oSS = Helper.Clone<STS_STATUS>(obj);
                lSSs.Add(oSS);
            }

            return lSSs;
        }

        public List<AGV_STATUS> GetList_AGV_STATUS()
        {
            AGV_STATUS oAS;
            List<AGV_STATUS> lASs = new List<AGV_STATUS>();

            foreach (AGV_STATUS obj in oSimDataStore.dAGVStatus.Values)
            {
                oAS = Helper.Clone<AGV_STATUS>(obj);
                lASs.Add(oAS);
            }

            return lASs;
        }

        public List<ASC_STATUS> GetList_ASC_STATUS()
        {
            ASC_STATUS oAS;
            List<ASC_STATUS> lASs = new List<ASC_STATUS>();

            foreach (ASC_STATUS obj in oSimDataStore.dASCStatus.Values)
            {
                oAS = Helper.Clone<ASC_STATUS>(obj);
                lASs.Add(oAS);
            }

            return lASs;
        }

        public Block_Container Get_Block_Container(string ContainerId)
        {
            Block_Container BC = new Block_Container();

            if (oSimDataStore.dSimContainerInfos.ContainsKey(ContainerId))
            {
                BC.CONTAINER_ID = ContainerId;
                BC.BLOCK_NO = oSimDataStore.dSimContainerInfos[ContainerId].YardBlock;
                BC.BAY = oSimDataStore.dSimContainerInfos[ContainerId].YardBay;
                BC.LANE = oSimDataStore.dSimContainerInfos[ContainerId].YardRow;
                BC.TIER = oSimDataStore.dSimContainerInfos[ContainerId].YardTier;
            }

            return BC;
        }

        public List<Block_Container> Get_Block_Container(string Block_NO, string Bay, string Lane = null, string Tier = null)
        {
            Block_Container oBC;
            List<Block_Container> lBCs = new List<Block_Container>();

            foreach(SimContainerInfo oSCI in oSimDataStore.dSimContainerInfos.Values)
            {
                if (oSCI.YardBlock == Block_NO && oSCI.YardBay == Convert.ToInt32(Bay))
                {
                    oBC = new Block_Container();
                    oBC.CONTAINER_ID = oSCI.ContainerID;
                    oBC.BLOCK_NO = oSCI.YardBlock;
                    oBC.BAY = oSCI.YardBay;
                    oBC.LANE = oSCI.YardRow;
                    oBC.TIER = oSCI.YardTier;
                    lBCs.Add(oBC);
                }
            }

            return lBCs;
        }

        /// <summary>
        /// 获取在场箱列表
        /// </summary>
        /// <returns></returns>
        public List<Block_Container> Get_All_Block_Container()
        {
            Block_Container BC = new Block_Container();
            List<Block_Container> objList;
            Block_Container oBC;

            objList = new List<Block_Container>();
            foreach (SimContainerInfo oContInfo in this.oSimDataStore.dSimContainerInfos.Values)
            {
                if (oContInfo.YardLoc != null && oContInfo.YardLoc.Length > 0)
                {
                    oBC = new Block_Container();
                    oBC.BLOCK_NO = oContInfo.YardBlock;
                    oBC.BAY = oContInfo.YardBay;
                    oBC.LANE = oContInfo.YardRow;
                    oBC.TIER = oContInfo.YardTier;
                    oBC.CONTAINER_ID = oContInfo.ContainerID;
                    objList.Add(oBC);
                }
            }

            return objList;
        }

        /// <summary>
        /// 修改 AGV_STATUS 与移动有关的部分
        /// </summary>
        /// <param name="lAGVs">要修改位置的AGV列表</param>
        public void RenewAGVStatusForMoving(List<AGV> lAGVs)
        {
            AGV_STATUS oAS;
            bool bRenewed;

            foreach(AGV oA in lAGVs)
            {
                if (this.oSimDataStore.dAGVStatus.ContainsKey(oA.ID.ToString()))
                {
                    bRenewed = false;
                    oAS = this.oSimDataStore.dAGVStatus[oA.ID.ToString()];
                    if (oAS.LOCATION != oA.CurrLaneID.ToString())
                    {
                        oAS.LOCATION = oA.CurrLaneID.ToString();
                        bRenewed = true;
                    }
                    if (oAS.LOCATION_X != oA.MidPoint.X)
                    {
                        oAS.LOCATION_X = (int)oA.MidPoint.X;
                        bRenewed = true;
                    }
                    if (oAS.LOCATION_Y != oA.MidPoint.Y)
                    {
                        oAS.LOCATION_Y = (int)oA.MidPoint.Y;
                        bRenewed = true;
                    }
                    if (oAS.NEXT_LOCATION != oA.NextLaneID.ToString())
                    {
                        oAS.NEXT_LOCATION = oA.NextLaneID.ToString();
                        bRenewed = true;
                    }
                    if (oAS.ORIENTATION != oA.RotateAngle)
                    {
                        oAS.ORIENTATION = (short)oA.RotateAngle;
                        bRenewed = true;
                    }
                    if (bRenewed)
                        oAS.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                }
            }
        }

        /// <summary>
        /// 修改 QC_STATUS 与移动有关的部分
        /// </summary>
        /// <param name="lQCs">要修改位置的QC列表</param>
        public void RenewQCStatusForMoving(List<QCDT> lQCs)
        {
            STS_STATUS oSS;
            bool bRenewed;

            foreach (QCDT oQC in lQCs)
            {
                if (this.oSimDataStore.dSTSStatus.ContainsKey(oQC.ID.ToString()))
                {
                    bRenewed = false;
                    oSS = this.oSimDataStore.dSTSStatus[oQC.ID.ToString()];
                    if (oSS.nQCPosition != Convert.ToInt32(oQC.BasePoint.X))
                    {
                        oSS.nQCPosition = Convert.ToInt32(oQC.BasePoint.X);
                        bRenewed = true;
                    }
                    if (bRenewed)
                        oSS.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                }
            }
        }

        /// <summary>
        /// 修改 ASC_STATUS 与移动有关的部分
        /// </summary>
        /// <param name="lASCs">要修改位置的ASC列表</param>
        public void RenewASCStatusForMoving(List<ASC> lASCs)
        {
            ASC_STATUS oAS;
            bool bRenewed;

            foreach (ASC oASC in lASCs)
            {
                if (this.oSimDataStore.dASCStatus.ContainsKey(oASC.ID.ToString()))
                {
                    bRenewed = false;
                    oAS = this.oSimDataStore.dASCStatus[oASC.ID.ToString()];
                    if (oAS.LOCATION != oASC.CurrBay)
                    {
                        oAS.LOCATION = oASC.CurrBay;
                        bRenewed = true;
                    }
                    if (bRenewed)
                        oAS.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                }
            }
        }

        /// <summary>
        /// 向数据源更新偏序表和检查后的排序结果
        /// </summary>
        /// <param name="oODT">偏序表</param>
        /// <param name="lSortedWIList">排序结果</param>
        public void UpdateWIListAndOrderTable(OrderedDecisionTable oODT, List<WORK_INSTRUCTION_STATUS> lSortedWIList)
        {
            int Seq1, Seq2;

            this.oSimDataStore.lWIContIDs.Clear();
            this.oSimDataStore.lWIContIDs = new List<string>(oODT.WIList.Select(u => u.CONTAINER_ID).ToList());
            this.oSimDataStore.mOrderTable = new int[oODT.WIList.Count, oODT.WIList.Count];
            this.oSimDataStore.mOrderTable = oODT.DecisionTable;

            for (int i = 0; i < oODT.WIList.Count; i++)
            {
                this.oSimDataStore.lSortedOrderChecks.Add(new OrderCheck()
                {
                    ContID = oODT.WIList[i].CONTAINER_ID,
                    OriSeq = i + 1,
                    OrderSeq = lSortedWIList.IndexOf(oODT.WIList[i]) + 1,
                    IfError = false
                });
            }

            // 检查. m[i,j] > 0, i在j后；m[i,j] < 0, i在j前 
            for (int i = 0; i < oODT.WIList.Count; i++)
            {
                for (int j = 0; j < oODT.WIList.Count; j++)
                {
                    if (oODT.DecisionTable[i, j] != 0)
                    {
                        Seq1 = lSortedWIList.IndexOf(oODT.WIList[i]);
                        Seq2 = lSortedWIList.IndexOf(oODT.WIList[j]);
                        if ((oODT.DecisionTable[i, j] > 0 && Seq1 < Seq2) || (oODT.DecisionTable[i, j] < 0 && Seq1 > Seq2))
                        {
                            this.oSimDataStore.lSortedOrderChecks[Seq2].IfError = true;
                            break;
                        }
                    }
                }
            }

        }

        /// <summary>
        /// 返回新的ID，可能是JobID，TaskID，OrderID或者CommID
        /// </summary>
        /// <param name="sIndex">ID类型</param>
        /// <returns>ID号</returns>
        public long GetNewIndexNum(StatusEnums.IndexType eIndexType)
        {
            long Index = 0;

            if (oSimDataStore.dIndexNums.ContainsKey(eIndexType))
            {
                oSimDataStore.dIndexNums[eIndexType] = oSimDataStore.dIndexNums[eIndexType] + 1;
                Index = oSimDataStore.dIndexNums[eIndexType];
            }

            return Index;
        }

        // Abandoned

        //private bool InsertTable(DBClass_BASE obj, string tableName)
        //{
        //    if (obj == null)
        //        return false;
        //    bool bInsertResult = false;

        //    Hashtable ht = obj.GetHashtableValue();

        //    int iCount = m_conn.Insert(tableName, ht);

        //    if (iCount > 0)
        //        bInsertResult = true;
        //    else
        //        bInsertResult = false;

        //    return bInsertResult;

        //}

        //    private bool UpdateCommand(DBClass_BASE obj, string commandID, string tableName)
        //    {
        //        if (obj == null)
        //            return false;
        //        bool bUpdate = false;
        //        Hashtable htUpdate = obj.GetHashtableValue();

        //        string ht_Where = "COMMAND_ID='" + commandID + "'";
        //        int n = m_conn.Update(tableName, ht_Where, htUpdate);

        //        if (n > 0)
        //            bUpdate = true;
        //        else
        //            bUpdate = false;

        //        return bUpdate;
        //    }

        //    private bool UpdateOrder(DBClass_BASE obj, string orderID, string tableName)
        //    {
        //        if (obj == null)
        //            return false;
        //        bool bUpdate = false;
        //        Hashtable htUpdate = obj.GetHashtableValue();

        //        string ht_Where = "ORDER_ID='" + orderID + "'";
        //        int n = m_conn.Update(tableName, ht_Where, htUpdate);

        //        if (n > 0)
        //            bUpdate = true;
        //        else
        //            bUpdate = false;

        //        return bUpdate;
        //    }

        //    private bool UpdateStatus(CHESTATUS_BASE obj, string ht_Where, string tableName)
        //    {
        //        if (obj == null)
        //            return false;
        //        bool bUpdate = false;

        //        Hashtable htUpdate = obj.GetHashtableValue();
        //        htUpdate.Add("TECHNICAL_STATUS", obj.TECHNICAL_STATUS);
        //        htUpdate.Add("UPDATED", DateTime.Now);

        //        int n = m_conn.Update(tableName, ht_Where, htUpdate);

        //        if (n > 0)
        //            bUpdate = true;
        //        else
        //            bUpdate = false;

        //        return bUpdate;
        //    }
        //}

    }
}
