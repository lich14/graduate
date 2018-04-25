using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.YardMap;
using ZECS.Schedule.DBDefine;
using SSWPF.Define;

namespace SSWPF.SimManagers
{
    // 管场地的。Pile 的申请(Reclaim)和释放(Release)。不涉及投影的事情
    public class SimYardManager
    {
        public event EventHandler<ProjectToViewFrameEventArgs> ProjectToViewFrameEvent;

        public SimDataStore oSimDataStore;

        public SimYardManager()
        {
        }

        public SimYardManager(SimDataStore oSDS)
            : this()
        {
            this.oSimDataStore = oSDS;
        }

        public bool Init()
        {
            if (this.oSimDataStore == null)
            {
                Logger.Simulate.Error("ExcelInputter: Null SimDataStore!");
                return false;
            }

            if (this.ProjectToViewFrameEvent == null)
            {
                Logger.Simulate.Error("ExcelInputter: Null Event Listener!");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 申请新建或者释放 Pile 时触发。
        /// </summary>
        /// <param name="sender">导致场内箱变动的对象。可能是读入接口、卸船分配或者装船提箱</param>
        /// <param name="e"></param>
        public void OnPileRecliamAndRelease(object sender, PilesReclaimAndReleaseEventArgs e)
        {
            if (e == null)
                return;

            if (e.lReleasePileNames != null && e.lReleasePileNames.Count > 0)
                if (!this.ReleasePiles(e.lReleasePileNames))
                {
                    e.IsSucc = false;
                    return;
                }

            if (e.lReclaimMsgs != null && e.lReclaimMsgs.Count > 0)
                e.IsSucc = this.ReclaimPiles(e.lReclaimMsgs);
            else
                e.IsSucc = true;
        }

        /// <summary>
        /// 根据可能移除的 Pile 列表，删去无占用的 Pile
        /// </summary>
        /// <param name="lPiles">可能移除的 Pile 列表</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool ReleasePiles(List<string> lPileNames)
        {
            foreach (string Name in lPileNames)
            {
                if (this.oSimDataStore.dPiles.ContainsKey(Name))
                {
                    if (this.oSimDataStore.dPiles[Name].lUnits.All(u => u.eContStoreStage == StatusEnums.StoreStage.None))
                    {
                        if (this.oSimDataStore.dPiles[Name].Slot1 != null)
                            this.oSimDataStore.dPiles[Name].Slot1.PileNameOnSlot = "";
                        if (this.oSimDataStore.dPiles[Name].Slot2 != null)
                            this.oSimDataStore.dPiles[Name].Slot2.PileNameOnSlot = "";
                        this.oSimDataStore.dPiles.Remove(Name);
                    }
                    else
                    {
                        Logger.Simulate.Error("Pile : " + Name + " Not Empty");
                        return false;
                    }
                }

            }

            return true;
        }

        /// <summary>
        /// 根据新增的箱号列表，判断是否并能否新建Pile，并新建
        /// </summary>
        /// <param name="lConts">箱号列表</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool ReclaimPiles(List<PileReclaimMsg> lPRMsgs)
        {
            string BlockName, SlotName1, SlotName2;
            uint Bay, Row;
            Pile oP;
            List<Pile> lTempPiles;

            lTempPiles = new List<Pile>();
            foreach (PileReclaimMsg oMsg in lPRMsgs)
            {
                if (!this.oSimDataStore.dPiles.ContainsKey(oMsg.PileName))
                {
                    SlotName1 = "";
                    SlotName2 = "";
                    if (this.CreateNewPileWithoutSlot(oMsg, out oP))
                    {
                        lTempPiles.Add(oP);
                        switch (oMsg.oPileType.eContSize)
                        {
                            case StatusEnums.ContSize.TEU:
                                SlotName1 = oMsg.PileName;
                                if (!this.oSimDataStore.dYardSlots.ContainsKey(SlotName1))
                                {
                                    Logger.Simulate.Error("No Slot Found Named As : " + SlotName1);
                                    return false;
                                }
                                this.oSimDataStore.dYardSlots[SlotName1].PileNameOnSlot = oP.Name;
                                oP.Slot1 = this.oSimDataStore.dYardSlots[SlotName1];
                                break;
                            case StatusEnums.ContSize.FEU:
                                BlockName = oMsg.PileName.Substring(0, 3);
                                Bay = Convert.ToUInt32(oMsg.PileName.Substring(3, 2));
                                Row = Convert.ToUInt32(oMsg.PileName.Substring(5, 2));
                                SlotName1 = BlockName + (Bay - 1).ToString().PadLeft(2, '0') + Row.ToString().PadLeft(2, '0');
                                if (!this.oSimDataStore.dYardSlots.ContainsKey(SlotName1))
                                {
                                    Logger.Simulate.Error("No Slot Found Named As : " + SlotName1);
                                    return false;
                                }
                                SlotName2 = BlockName + (Bay + 1).ToString().PadLeft(2, '0') + Row.ToString().PadLeft(2, '0');
                                if (!this.oSimDataStore.dYardSlots.ContainsKey(SlotName2))
                                {
                                    Logger.Simulate.Error("No Slot Found Named As : " + SlotName2);
                                    return false;
                                }
                                this.oSimDataStore.dYardSlots[SlotName1].PileNameOnSlot = oP.Name;
                                this.oSimDataStore.dYardSlots[SlotName1].PileNameOnSlot = oP.Name;
                                oP.Slot1 = this.oSimDataStore.dYardSlots[SlotName1];
                                oP.Slot2 = this.oSimDataStore.dYardSlots[SlotName2];
                                break;
                            case StatusEnums.ContSize.FFEU:
                                BlockName = oMsg.PileName.Substring(0, 3);
                                Bay = Convert.ToUInt32(oMsg.PileName.Substring(3, 2));
                                Row = Convert.ToUInt32(oMsg.PileName.Substring(5, 2));
                                SlotName1 = BlockName + (Bay - 1).ToString().PadLeft(2, '0') + Row.ToString().PadLeft(2, '0');
                                if (!this.oSimDataStore.dYardSlots.ContainsKey(SlotName1))
                                {
                                    Logger.Simulate.Error("No Slot Found Named As : " + SlotName1);
                                    return false;
                                }
                                if (!this.oSimDataStore.dYardSlots[SlotName1].Cont45Permitted)
                                {
                                    Logger.Simulate.Error("45 Conts Unable To Store In Slot : " + SlotName1);
                                    return false;
                                }
                                SlotName2 = BlockName + (Bay + 1).ToString().PadLeft(2, '0') + Row.ToString().PadLeft(2, '0');
                                if (!this.oSimDataStore.dYardSlots.ContainsKey(SlotName2))
                                {
                                    Logger.Simulate.Error("No Slot Found Named As : " + SlotName2);
                                    return false;
                                }
                                if (!this.oSimDataStore.dYardSlots[SlotName2].Cont45Permitted)
                                {
                                    Logger.Simulate.Error("45 Conts Unable To Store In Slot : " + SlotName2);
                                    return false;
                                }
                                this.oSimDataStore.dYardSlots[SlotName1].PileNameOnSlot = oP.Name;
                                this.oSimDataStore.dYardSlots[SlotName1].PileNameOnSlot = oP.Name;
                                oP.Slot1 = this.oSimDataStore.dYardSlots[SlotName1];
                                oP.Slot2 = this.oSimDataStore.dYardSlots[SlotName2];
                                break;
                            default:
                                break;
                        }
                    }
                    else
                        return false;
                }
            }

            // 有了Slot才能定位
            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
            {
                eProjectType = StatusEnums.ProjectType.Create,
                oPPTViewFrame = new ProjectPackageToViewFrame()
                {
                    lPiles = lTempPiles
                }
            });

            return true;
        }

        /// <summary>
        /// 新建空的Pile，与Slot无关
        /// </summary>
        /// <param name="oMsg">申请Pile的消息</param>
        /// <param name="oP">out出Pile对象</param>
        /// <returns>成功返回true，失败返回false</returns>
        private bool CreateNewPileWithoutSlot(PileReclaimMsg oMsg, out Pile oP)
        {
            string BlockName;
            uint Bay, Row;

            BlockName = oMsg.PileName.Substring(0, 3);
            Bay = Convert.ToUInt32(oMsg.PileName.Substring(3, 2));
            Row = Convert.ToUInt32(oMsg.PileName.Substring(5, 2));

            oP = new Pile()
            {
                Name = oMsg.PileName,
                BlockName = BlockName,
                oType = oMsg.oPileType,
                Bay = Bay,
                Row = Row
            };

            for (int i = 0; i < oP.oType.MaxStackNum; i++)
                oP.lUnits.Add(new SingleRigidStoreUnit());

            if (!oP.CheckDefinitionCompleteness())
            {
                Logger.Simulate.Error("Pile Definition Completeness Check Failed : " + oP.Name);
                return false;
            }
            else
            {
                this.oSimDataStore.dPiles.Add(oP.Name, oP);
                return true;
            }
        }

        // 仿真开始前 Pile 占据 YardSlot
        public bool PileOccupyYardSlotsBeforeSim(List<Pile> lPs)
        {
            bool bRet = true;

            for (int i = 0; i < lPs.Count; i++)
            {
                if (!this.PileOccupyYardSlot(lPs[i].Name))
                {
                    bRet = false;
                    break;
                }
            }

            return bRet;
        }

        // Pile 占用 YardSlot
        public bool PileOccupyYardSlot(string PileName)
        {
            bool bRet = true;
            Pile oP = oSimDataStore.dPiles[PileName];
            string SlotStr1;
            string SlotStr2;

            if (oP.Bay % 2 == 0)
            {
                SlotStr1 = oP.BlockName + (oP.Bay - 1).ToString().PadLeft(2, '0') + oP.Row.ToString().PadLeft(2, '0');
                SlotStr2 = oP.BlockName + (oP.Bay + 1).ToString().PadLeft(2, '0') + oP.Row.ToString().PadLeft(2, '0');

                if ((this.oSimDataStore.dYardSlots.ContainsKey(SlotStr1) && this.oSimDataStore.dYardSlots[SlotStr1].PileNameOnSlot.Length == 0
                    && (oP.oType.eContSize == StatusEnums.ContSize.FEU || this.oSimDataStore.dYardSlots[SlotStr1].Cont45Permitted))
                    && (this.oSimDataStore.dYardSlots.ContainsKey(SlotStr2) && this.oSimDataStore.dYardSlots[SlotStr2].PileNameOnSlot.Length == 0
                    && (oP.oType.eContSize == StatusEnums.ContSize.FEU || this.oSimDataStore.dYardSlots[SlotStr2].Cont45Permitted)))
                {
                    this.oSimDataStore.dYardSlots[SlotStr1].PileNameOnSlot = oP.Name;
                    this.oSimDataStore.dYardSlots[SlotStr2].PileNameOnSlot = oP.Name;
                    oP.Slot1 = this.oSimDataStore.dYardSlots[SlotStr1];
                    oP.Slot2 = this.oSimDataStore.dYardSlots[SlotStr2];
                }
                else
                {
                    bRet = false;
                    Logger.Simulate.Error("Pile : " + oP.Name + " Reserve Yard Slot : " + SlotStr1 + " and Slot : " +  SlotStr2 + " Failed");
                }
            }
            else
            {
                SlotStr1 = oP.BlockName + oP.Bay.ToString().PadLeft(2, '0') + oP.Row.ToString().PadLeft(2, '0');
                if (this.oSimDataStore.dYardSlots.ContainsKey(SlotStr1) && this.oSimDataStore.dYardSlots[SlotStr1].PileNameOnSlot.Length == 0)
                {
                    this.oSimDataStore.dYardSlots[SlotStr1].PileNameOnSlot = oP.Name;
                    oP.Slot1 = this.oSimDataStore.dYardSlots[SlotStr1];
                }
                else
                {
                    bRet = false;
                    Logger.Simulate.Error("Pile : " + oP.Name + " Reserve Yard Slot : " + SlotStr1 + " Failed");
                }
                SlotStr2 = "";

            }
            return bRet;
        }

        // Pile 清除
        public bool PileReleaseYardSlot(string PileName)
        {
            bool bRet = true;
            Pile oP = this.oSimDataStore.dPiles[PileName];

            oP.Slot1.PileNameOnSlot = "";
            oP.Slot2.PileNameOnSlot = "";
            this.oSimDataStore.dPiles.Remove(PileName);

            return bRet;
        }

        /// <summary>
        /// 新建Pile
        /// </summary>
        /// <param name="sender">卸船箱新建Pile对象的触发事件，一般为SimTosManager</param>
        /// <param name="e">SimAlterPileEventArgs</param>
        public void CreatePileForInboundContainer(object sender, PilesReclaimAndReleaseEventArgs e)
        {
            //this.CreatePile(e.BlockName, e.Bay, e.Row, e.eContSize);
        }

        public bool CreatePile(string BlockName, uint Bay, uint Row, StatusEnums.ContSize eContSize)
        {
            YardSlot oYS1, oYS2;
            Pile oPile;

            if (string.IsNullOrWhiteSpace(BlockName) || Bay == 0 || Row == 0 || eContSize == StatusEnums.ContSize.Unknown)
                return false;

            if (Bay % 2 == 1)
            {
                oYS1 = this.oSimDataStore.dYardSlots.Values.FirstOrDefault<YardSlot>(u => u.BlockName == BlockName && u.Bay == Bay && u.Row == Row);
                if (oYS1 == null || !string.IsNullOrWhiteSpace(oYS1.PileNameOnSlot))
                    return false;

                oPile = new Pile();
                oPile.oType = this.oSimDataStore.dPileTypes[eContSize];
                oPile.BlockName = BlockName;
                oPile.Bay = Bay;
                oPile.Row = Row;
                oPile.Slot1 = oYS1;
                oPile.Name = BlockName.Substring(0, 7);
                oYS1.PileNameOnSlot = oPile.Name;
            }
            else
            {
                oYS1 = this.oSimDataStore.dYardSlots.Values.FirstOrDefault<YardSlot>(u => u.BlockName == BlockName && u.Bay == Bay - 1 && u.Row == Row);
                oYS2 = this.oSimDataStore.dYardSlots.Values.FirstOrDefault<YardSlot>(u => u.BlockName == BlockName && u.Bay == Bay + 1 && u.Row == Row);
                if (oYS1 == null || oYS2 == null || !string.IsNullOrWhiteSpace(oYS1.PileNameOnSlot) || !string.IsNullOrWhiteSpace(oYS2.PileNameOnSlot) 
                    || (!oYS1.Cont45Permitted && eContSize == StatusEnums.ContSize.FFEU))
                    return false;

                oPile = new Pile();
                oPile.oType = this.oSimDataStore.dPileTypes[eContSize];
                oPile.BlockName = BlockName;
                oPile.Bay = Bay;
                oPile.Row = Row;
                oPile.Slot1 = oYS1;
                oPile.Slot2 = oYS2;
                oPile.Name = BlockName.Substring(0, 7);
                oYS1.PileNameOnSlot = oPile.Name;
                oYS2.PileNameOnSlot = oPile.Name;
            }

            this.oSimDataStore.dPiles.Add(oPile.Name, oPile);

            return true;
        }

    }
}
