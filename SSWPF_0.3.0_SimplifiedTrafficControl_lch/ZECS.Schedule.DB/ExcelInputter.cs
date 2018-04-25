using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NExcel;
using System.Data;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Shapes;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.YardMap;
using SSWPF.Define;
using SharpSim;


namespace ZECS.Schedule.DB
{
    // 只在仿真开始前执行，读入数据到 SimDataStore 并投影，不做任何其他处理。
    public class ExcelInputter
    {
        public SimDataStore oSimDataStore;
        public string ProjectDirectory;
        public bool bInited;
        Workbook WkBExtraCase;
        Workbook WkBCase;

        public event EventHandler<ProjectToViewFrameEventArgs> ProjectToViewFrameEvent;
        public event EventHandler<ProjectToInfoFrameEventArgs> ProjectToInfoFrameEvent;
        public event EventHandler<PilesReclaimAndReleaseEventArgs> PilesReclaimAndReleaseEvent;

        public ExcelInputter()
        {
        }

        public ExcelInputter(SimDataStore oSimDataStore, string ProjectDirectory)
            : this()
        {
            this.oSimDataStore = oSimDataStore;
            this.ProjectDirectory = ProjectDirectory;
        }

        /// <summary>
        /// ImputFromExcel 初始化，拿到对应 Excel文件的句柄
        /// </summary>
        /// <returns></returns>
        public bool Init()
        {
            // 来自Excel：箱区、磁钉、车道（Graphic）、支架（Label）、AGV、ASC、QC和PileType，以及各种状态对象的颜色
            string FileName;

            FileName = this.ProjectDirectory + "\\SimInput.xls";
            this.WkBExtraCase = Workbook.getWorkbook(FileName);
            if (this.WkBExtraCase == null)
            {
                Logger.Simulate.Error("ExcelInputter: Load SimInput Failed!");
                return false;
            }

            // 应来自Oracle，实际来自Excel：船和箱堆Pile，以及TOS数据，包括BERTH_STATUS，WQ，WI（还应该有进口箱区策划和出口配载方案）。
            FileName = this.ProjectDirectory + "\\TOSExp.xls";
            this.WkBCase = Workbook.getWorkbook(FileName);
            if (this.WkBExtraCase == null)
            {
                Logger.Simulate.Error("ExcelInputter: Load Example Failed!");
                return false;
            }

            if (this.oSimDataStore == null)
            {
                Logger.Simulate.Error("ExcelInputter: Null SimDataStore!");
                return false;
            }

            if (this.ProjectToViewFrameEvent == null || this.ProjectToInfoFrameEvent == null || this.PilesReclaimAndReleaseEvent == null)
            {
                Logger.Simulate.Error("ExcelInputter: Null Event Listener!");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 为SimFrame准备投射包用于对象初始化
        /// </summary>
        /// <param name="oPPTSimFrame">SimFrame投射包</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool LoadTerminalAndPlan()
        {
            ProjectPackageToViewFrame oPPTViewFrame;

            oPPTViewFrame = new ProjectPackageToViewFrame();

            // 里Create，外Renew

            // 码头区域。
            if (!this.LoadTerminalRegion("TerminalRegions"))
                return false;

            // 磁钉和AGVLine，注意磁钉在 AGVLine 之前生成，但可能更新
            if (!this.LoadTransponders("Transponders", ref oPPTViewFrame.lTPs))
                return false;
            if (!this.LoadAGVLines("AGVLines"))
                return false;

            // BlockDiv、BlockMargin 和 Block，并生成 YardSlot
            if (!this.LoadBlockDivs("BlockDivs"))
                return false;
            if (!this.LoadBlockDivsTypes("BlockDivTypes"))
                return false;
            if (!this.LoadBlocks("Blocks"))
                return false;
            if (!this.GenerateYardSlots())
                return false;

            // Lane 及其颜色 LaneStatus 和 LaneAttribute
            if (!this.LoadColors("LaneStatus"))
                return false;
            if (!this.LoadColors("LaneAttributes"))
                return false;
            if (!this.LoadLanes("Lanes", ref oPPTViewFrame.lLanes))
                return false;

            // Mate 及其颜色
            if (!this.LoadColors("StoreTypes"))
                return false;
            if (!this.LoadMates("Mates", ref oPPTViewFrame.lMates))
                return false;

            // 计算 Block 和 Lane 以及 Mate 的 ASC 作业位置 WorkPoint
            if (!this.GenerateASCWorkPoints())
                return false;

            // 载入 AGVType 和 AGV。注意 AGV 需占据车道
            if (!this.LoadAGVTypes("AGVTypes"))
                return false;
            if (!this.LoadAGVs("AGVs"))
                return false;

            // 载入 TravelStatus 和 MoveKind 相关的颜色
            if (!this.LoadColors("MotionStatus"))
                return false;
            if (!this.LoadColors("MoveKinds"))
                return false;

            // 载入岸桥，包括小车时间统计(单项和全小车项)、类型和 QC 主体
            if (!this.LoadQCActionStats("QCActionStats"))
                return false;
            if (!this.LoadQCTrolleyStats("QCTrolleyStats"))
                return false;
            if (!this.LoadQCTypes("QCTypes"))
                return false;
            if (!this.LoadQCs("QCs"))
                return false;

            // 载入场桥，包括类型和 ASC 主体
            if (!this.LoadASCTypes("ASCTypes"))
                return false;
            if (!this.LoadASCs("ASCs"))
                return false;

            // 载入 TierColor 和 PileType
            if (!this.LoadColors("TierColors"))
                return false;
            if (!this.LoadPileTypes("PileTypes"))
                return false;

            // 载入船型信息
            if (!this.LoadVesselTypes("VesselTypes"))
                return false;

            // 载入 ISOReference
            if (!this.LoadISORefs("ISORefs"))
                return false;

            // 载入 AGV 开行时间预估
            if (!this.LoadVMSExpectTime("T_VMS_EXPECTTIME"))
                return false;

            // 载入 Vessel 和 Voyage，必须在 ContainerInfo 之前
            if (!this.LoadVessels("SHIP_VOYAGE"))
                return false;
            if (!this.LoadVoyages("SHIP"))
                return false;

            // 载入装卸箱的 ContainerInfo 和在场箱 Pile，注意首先令 Pile 占据 YardSlot，否则没有位置
            if (!this.LoadSimContainerInfos("SHIP_NCL"))
                return false;
            if (!this.LoadSimContainerInfos("SHIP_BAPLIE"))
                return false;
            if (!this.LoadSimContainerInfos("PORT_CNTR"))
                return false;
            if (!this.LoadPiles())
                return false;

            // 载入 WorkQueue 和 WorkInstruction，案例中的所有数据
            // 注意 WorkInstruction 里面还包含了出口箱的配载位置，需向ContainerInfo中补充
            if (!this.LoadWorkQueues("SHIP_PLAN"))
                return false;
            if (!this.LoadWorkInstructions("WORK_QUEUE"))
                return false;

            // 标注空 WQ。数据源中有些 WQ 没有 WI
            this.SignEmptyWorkQueues();

            // 给 WI 补充 CONTAINER_STOW_FACTOR 和 CONTAINER_WEIGHT_MARGIN
            this.SupplementWI();

            // 载入进箱计划 PlanGroup，PlanRange 和 PlanPlac。亦属于数据库部分
            if (!this.LoadPlanGroups("PLAN_GROUP"))
                return false;
            if (!this.LoadPlanRanges("PLAN_RANGE"))
                return false;
            if (!this.LoadPlanPlacs("PLAN_PLAC"))
                return false;

            // 载入 Index 编号方案
            this.LoadIndexNums();

            this.ProjectToViewFrameEvent(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTViewFrame = oPPTViewFrame
                });

            // Log 成功记录
            Logger.Simulate.Info("Loading From Excel Succesfully Finished");
            return true;
        }

        
        // 海域、陆域 TreminalRegion。
        private bool LoadTerminalRegion(string SheetName)
        {
            Sheet Sht;
            TerminalRegion oTR;
            bool bRet = false;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            oTR = new TerminalRegion();
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "RegionXMeter":
                                oTR.Width = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "RegionYMeter":
                                oTR.LandHeight = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "VesselOutReach":
                                oTR.WaterHeight = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                        }
                    }
                    if (!oTR.CheckDefinitionCompleteness())
                        continue;
                    else
                    {
                        bRet = true;
                        break;
                    }
                }
            }
            if (!bRet)
                Logger.Simulate.Error("No Terminal Region Loaded");
            else
            {
                this.oSimDataStore.oTerminalRegion = oTR;
                this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                    {
                        eProjectType = StatusEnums.ProjectType.Create,
                        oPPTViewFrame = new ProjectPackageToViewFrame()
                        {
                            oTR = oTR
                        }
                    });
            }

            return bRet;
        }

        // 磁钉 SimTransponder 
        private bool LoadTransponders(string SheetName, ref List<SimTransponder> lTPs)
        {
            Sheet Sht;
            SimTransponder oSTP;
            bool bRet = true;
            int IndexLength;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            lTPs.Clear();

            IndexLength = Sht.Rows.ToString().Length;
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oSTP = new SimTransponder();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ID":
                                oSTP.ID = Convert.ToUInt16(Sht.getCell(j, i).Value);
                                oSTP.Name = "Transponder" + oSTP.ID.ToString().PadLeft(IndexLength, '0');
                                break;
                            case "X":
                                oSTP.PhysicalPosX = Convert.ToSingle(Sht.getCell(j, i).Value);
                                oSTP.LogicPosX = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "Y":
                                oSTP.PhysicalPosY = Convert.ToSingle(Sht.getCell(j, i).Value);
                                oSTP.LogicPosY = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "HORIZONTAL_LINE_ID":
                                oSTP.HorizontalLineID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "VERTICAL_LINE_ID":
                                oSTP.VerticalLineID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "AREA_TYPE":
                                oSTP.AreaType = (AreaType)Enum.Parse(typeof(AreaType), Sht.getCell(j, i).Value.ToString());
                                break;
                            case "AREA_NO":
                                oSTP.AreaNo = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "AREA_LANE_ID":
                                oSTP.LaneNo = Sht.getCell(j, i).Value.ToString();
                                if (oSTP.LaneNo == null || oSTP.LaneNo.Length == 0)
                                    oSTP.LaneNo = "0";
                                break;
                            case "ENABLED":
                                if (Convert.ToInt32(Sht.getCell(j, i).Value) == 1)
                                    oSTP.Enabled = true;
                                else 
                                    oSTP.Enabled = false;
                                break;
                            case "NOSTOP":
                                oSTP.NoStop = Convert.ToUInt16(Sht.getCell(j, i).Value);
                                break;
                        }
                    }
                    if (!oSTP.CheckDefinitionCompleteness())
                    {
                        Logger.Simulate.Error("Load Transponder in Row " + i.ToString() + " Failed");
                        bRet = false;
                        break;
                    }
                    else
                        lTPs.Add(oSTP);
                }
            }
            if (bRet)
            {
                if (lTPs.Count > 0)
                {
                    foreach (SimTransponder oST in lTPs)
                        this.oSimDataStore.dTransponders.Add(oST.ID, oST);
                    this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                        {
                            eProjectType = StatusEnums.ProjectType.Create,
                            oPPTViewFrame = new ProjectPackageToViewFrame()
                            {
                                lTPs = lTPs
                            }
                        });
                }
                else
                {
                    Logger.Simulate.Error("No Transponder Loaded");
                    bRet = false;
                }
            }
            return bRet;
        }

        // AGV 行驶线 AGVLine，路径相关
        private bool LoadAGVLines(string SheetName)
        {
            Sheet Sht;
            string CompleteDir;
            List<AGVLine> lALs;
            bool bRet = true;
            AGVLine oAL;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            lALs = new List<AGVLine>();
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oAL = new AGVLine();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ID":
                                oAL.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "DIRECTION":
                                bRet = Enum.TryParse<CoordinateDirection>(Sht.getCell(j, i).Contents, out oAL.eFlowDir);
                                break;
                            case "MoveDir":
                                CompleteDir = Sht.getCell(j, i).Contents;
                                if (CompleteDir.Length > 0)
                                {
                                    if (CompleteDir.Length == 1)
                                    {
                                        switch (CompleteDir)
                                        {
                                            case "N":
                                                CompleteDir = "North";
                                                break;
                                            case "S":
                                                CompleteDir = "South";
                                                break;
                                            case "W":
                                                CompleteDir = "West";
                                                break;
                                            case "E":
                                                CompleteDir = "East";
                                                break;
                                            default:
                                                break;
                                        }
                                    }
                                    Enum.TryParse<CHE_Direction>(CompleteDir, out oAL.eMoveDir);
                                }
                                break;
                        }
                    }
                    if (bRet) 
                        bRet = this.LinkAGVLinesAndTransponders(oAL);
                    if (!bRet || !oAL.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load AGV Line in Row " + i.ToString() + " Failed");
                        break;
                    }
                    if (bRet)
                        lALs.Add(oAL);
                }
            }
            if (bRet)
            {
                if (lALs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No AGV Line Loaded");
                }
                else
                {
                    foreach (AGVLine obj in lALs)
                        this.oSimDataStore.dAGVLines.Add(obj.ID, obj);
                    
                    this.ProjectToViewFrameEvent(this, new ProjectToViewFrameEventArgs()
                        {
                            eProjectType = StatusEnums.ProjectType.Create,
                            oPPTViewFrame = new ProjectPackageToViewFrame()
                            {
                                lAGVLines = lALs
                            }
                        });
                }
            }

            return bRet;
        }

        // 联系 AGVLine 和 TransponderID
        private bool LinkAGVLinesAndTransponders(AGVLine oAL)
        {
            bool bRet = true;

            oAL.lTPIDs = this.oSimDataStore.dTransponders.Values
                .Where(u => u.HorizontalLineID == oAL.ID || u.VerticalLineID == oAL.ID)
                .Select(u => u.ID).Where(u => u > 0).ToList();

            switch (oAL.eFlowDir)
            {
                case CoordinateDirection.X_POSITIVE:
                    oAL.lTPIDs.Sort((u, v) => this.oSimDataStore.dTransponders[u].LogicPosX.CompareTo(this.oSimDataStore.dTransponders[v].LogicPosX));
                    oAL.FeaturePosition = this.oSimDataStore.dTransponders[oAL.lTPIDs[0]].LogicPosY;
                    break;
                case CoordinateDirection.X_NEGATIVE:
                    oAL.lTPIDs.Sort((u, v) => this.oSimDataStore.dTransponders[v].LogicPosX.CompareTo(this.oSimDataStore.dTransponders[u].LogicPosX));
                    oAL.FeaturePosition = this.oSimDataStore.dTransponders[oAL.lTPIDs[0]].LogicPosY;
                    break;
                case CoordinateDirection.Y_POSITIVE:
                    oAL.lTPIDs.Sort((u, v) => this.oSimDataStore.dTransponders[u].LogicPosY.CompareTo(this.oSimDataStore.dTransponders[v].LogicPosY));
                    oAL.FeaturePosition = this.oSimDataStore.dTransponders[oAL.lTPIDs[0]].LogicPosX;
                    break;
                case CoordinateDirection.Y_NEGATIVE:
                    oAL.lTPIDs.Sort((u, v) => this.oSimDataStore.dTransponders[v].LogicPosY.CompareTo(this.oSimDataStore.dTransponders[u].LogicPosY));
                    oAL.FeaturePosition = this.oSimDataStore.dTransponders[oAL.lTPIDs[0]].LogicPosX;
                    break;
            }

            if (oAL.lTPIDs.Count == 0)
                bRet = false;

            return bRet;
        }

        // 箱区划分 BlockDiv。
        private bool LoadBlockDivs(string SheetName)
        {
            Sheet Sht;
            BlockDiv oBD;
            List<BlockDiv> lBDs = new List<BlockDiv>();
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            lBDs.Clear();
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oBD = new BlockDiv();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ID":
                                oBD.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "Start":
                                oBD.StartPos = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "Cont45Permitted":
                                if (Sht.getCell(j, i).Type != CellType.EMPTY) oBD.Cont45Permitted = Convert.ToInt32(Sht.getCell(j, i).Value);
                                else oBD.Cont45Permitted = 0;
                                break;
                        }
                    }
                    if (!oBD.CheckDefinitionCompleteness())
                    {
                        Logger.Simulate.Error("Load Block Division in Row " + i.ToString() + " Failed");
                        bRet = false;
                        break;
                    }
                    else
                        lBDs.Add(oBD);
                }
            }
            if (bRet)
            {
                if (lBDs.Count > 0)
                    foreach (BlockDiv obj in lBDs)
                        this.oSimDataStore.dBlockDivs.Add(obj.ID, obj);
                else
                {
                    Logger.Simulate.Error("No Block Division Loaded");
                    bRet = false;
                }
            }
            return bRet;
        }

        // 箱区类型 BlockDivsType。
        private bool LoadBlockDivsTypes(string SheetName)
        {
            Sheet Sht;
            BlockDivsType oBDT;
            List<BlockDivsType> lBDTs = new List<BlockDivsType>();
            uint DivID;
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            lBDTs.Clear();
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oBDT = new BlockDivsType();
                    oBDT.ID = Convert.ToUInt32(Sht.getCell(0, i).Contents);
                    for (int j = 1; j < Sht.Columns; j++)
                    {
                        if (Sht.getCell(j, i).Type != CellType.EMPTY)
                        {
                            DivID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                            if (this.oSimDataStore.dBlockDivs.ContainsKey(DivID))
                            {
                                oBDT.lBlockDivs.Add(this.oSimDataStore.dBlockDivs[DivID]);
                            }
                        }
                        else break;
                    }
                    if (!oBDT.CheckDefinitionCompleteness())
                    {
                        Logger.Simulate.Error("Load Block Divs Type in Row " + i.ToString() + " Failed");
                        break;
                    }
                    else
                        lBDTs.Add(oBDT);
                }
            }
            if (bRet)
            {
                if (lBDTs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Block Divs Type Loaded");
                }
                else
                    foreach (BlockDivsType obj in lBDTs)
                        this.oSimDataStore.dBlockDivsTypes.Add(obj.ID, obj);
            }
            return bRet;
        }

        // 箱区 Block。
        private bool LoadBlocks(string SheetName)
        {
            Sheet Sht;
            Block oB;
            List<Block> lBs;
            uint DivsTypeInd;
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            lBs = new List<Block>();
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oB = new Block();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "BlockInd":
                                oB.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "BlockName":
                                oB.Name = Sht.getCell(j, i).Contents;
                                break;
                            case "posXUL":
                                oB.X = Convert.ToSingle(Sht.getCell(j, i).Value);
                                break;
                            case "posYUL":
                                oB.Y = Convert.ToSingle(Sht.getCell(j, i).Value);
                                break;
                            case "MarginX":
                                oB.MarginX = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "MarginY":
                                oB.MarginY = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "BayVaryDir":
                                oB.BayVaryDir = Sht.getCell(j, i).Contents;
                                break;
                            case "DivTypeIndX":
                                DivsTypeInd = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dBlockDivsTypes.ContainsKey(DivsTypeInd)) oB.lBlockDivsX = this.oSimDataStore.dBlockDivsTypes[DivsTypeInd].lBlockDivs;
                                break;
                            case "DivTypeIndY":
                                DivsTypeInd = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dBlockDivsTypes.ContainsKey(DivsTypeInd)) oB.lBlockDivsY = this.oSimDataStore.dBlockDivsTypes[DivsTypeInd].lBlockDivs;
                                break;
                        }
                    }
                    if (!oB.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load Block in Row " + i.ToString() + " Failed");
                        break;
                    }
                    else
                        lBs.Add(oB);
                }
            }

            if (bRet)
            {
                if (lBs.Count > 0)
                {
                    foreach (Block obj in lBs)
                        this.oSimDataStore.dBlocks.Add(obj.Name, obj);

                    this.ProjectToViewFrameEvent(this, new ProjectToViewFrameEventArgs()
                        {
                            eProjectType = StatusEnums.ProjectType.Create,
                            oPPTViewFrame = new ProjectPackageToViewFrame()
                            {
                                lBlocks = lBs
                            }
                        });
                }
                else
                {
                    Logger.Simulate.Error("No Block Loaded");
                    bRet = false;
                }
            }
            return bRet;
        }

        // 箱区串 YardSlot。
        private bool GenerateYardSlots()
        {
            bool bRet = true;
            YardSlot oYS;
            List<YardSlot> lYSs = new List<YardSlot>();

            foreach (Block oB in this.oSimDataStore.dBlocks.Values)
            {
                for (int i = 0; i < oB.lBlockDivsX.Count; i++)
                {
                    for (int j = 0; j < oB.lBlockDivsY.Count; j++)
                    {
                        oYS = new YardSlot();
                        if (oB.BayVaryDir == "X")
                        {
                            oYS.Bay = 2 * i + 1;
                            oYS.Row = j + 1;
                        }
                        else
                        {
                            oYS.Bay = 2 * j + 1;
                            oYS.Row = i + 1;
                        }
                        oYS.BlockName = oB.Name;
                        oYS.Name = oB.Name + oYS.Bay.ToString().PadLeft(2, '0') + oYS.Row.ToString().PadLeft(2, '0');
                        oYS.BasePointUL.X = oB.X + oB.lBlockDivsX[i].StartPos;
                        oYS.BasePointUL.Y = oB.Y + oB.lBlockDivsY[j].StartPos;
                        if ((oB.lBlockDivsX[i].Cont45Permitted > 0 && oB.lBlockDivsY[j].Cont45Permitted >= 0)
                            || (oB.lBlockDivsX[i].Cont45Permitted >= 0 && oB.lBlockDivsY[j].Cont45Permitted > 0)) oYS.Cont45Permitted = true;
                        else oYS.Cont45Permitted = false;

                        if (!oYS.CheckDefinitionCompleteness())
                        {
                            bRet = false;
                            Logger.Simulate.Error("Load Yard Slot in Block " + oB.Name + " DivX " + i.ToString() + " DivY " + j.ToString() + " Failed");
                            break;
                        }
                        else
                            lYSs.Add(oYS);
                    }
                    if (!bRet) break;
                }
                if (!bRet) break;
            }

            if (bRet)
            {
                if (lYSs.Count == 0)
                {
                    Logger.Simulate.Error("No Yard Slot Loaded");
                    bRet = false;
                }
                else
                    foreach (YardSlot obj in lYSs)
                        this.oSimDataStore.dYardSlots.Add(obj.Name, obj);
            }

            return bRet;
        }

        // 颜色，可能有多种对象的状态颜色，用统一的加载函数。
        private bool LoadColors(string SheetName)
        {
            Sheet Sht;
            bool bRet = true;
            Color c;
            string Case = "";
            long c_int;

            Dictionary<string, Dictionary<string, Color>> ddColors;
            Dictionary<string, Color> dColors;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            ddColors = new Dictionary<string, Dictionary<string, Color>>();
            dColors = new Dictionary<string, Color>();
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    c = new Color();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "Case":
                                if (Sht.getCell(j, i).Type == CellType.LABEL)
                                    Case = Sht.getCell(j, i).Contents;
                                else if (Sht.getCell(j, i).Type == CellType.NUMBER)
                                    Case = Convert.ToString(Sht.getCell(j, i).Value);
                                break;
                            case "A":
                                if (Sht.getCell(j, i).Type == CellType.EMPTY) c.A = 0;
                                else
                                {
                                    c_int = Convert.ToInt64(Sht.getCell(j, i).Value);
                                    c_int = Math.Max(0, c_int);
                                    c_int = Math.Min(c_int, 255);
                                    c.A = Convert.ToByte(c_int);
                                }
                                break;
                            case "R":
                                if (Sht.getCell(j, i).Type == CellType.EMPTY) c.R = 0;
                                else
                                {
                                    c_int = Convert.ToInt64(Sht.getCell(j, i).Value);
                                    c_int = Math.Max(0, c_int);
                                    c_int = Math.Min(c_int, 255);
                                    c.R = Convert.ToByte(c_int);
                                }
                                break;
                            case "G":
                                if (Sht.getCell(j, i).Type == CellType.EMPTY) c.G = 0;
                                else
                                {
                                    c_int = Convert.ToInt64(Sht.getCell(j, i).Value);
                                    c_int = Math.Max(0, c_int);
                                    c_int = Math.Min(c_int, 255);
                                    c.G = Convert.ToByte(c_int);
                                }
                                break;
                            case "B":
                                if (Sht.getCell(j, i).Type == CellType.EMPTY) c.B = 0;
                                else
                                {
                                    c_int = Convert.ToInt64(Sht.getCell(j, i).Value);
                                    c_int = Math.Max(0, c_int);
                                    c_int = Math.Min(c_int, 255);
                                    c.B = Convert.ToByte(c_int);
                                }
                                break;
                        }
                    }
                    dColors.Add(Case, c);
                }
            }
            if (dColors.Count == 0)
                bRet = false;

            switch (Sht.Name)
            {
                case "StoreTypes":
                    if (bRet)
                    {
                        this.oSimDataStore.dCarryColors = dColors;
                        if (!ddColors.ContainsKey("StoreTypes"))
                            ddColors.Add("StoreTypes", dColors);
                        else
                            ddColors["StoreTypes"] = dColors;
                    }
                    else 
                        Logger.Simulate.Error("No Carry Color Loaded");
                    break;
                case "LaneStatus":
                    if (bRet)
                    {
                        this.oSimDataStore.dLaneStatusColors = dColors;
                        if (!ddColors.ContainsKey("LaneStatus"))
                            ddColors.Add("LaneStatus", dColors);
                        else
                            ddColors["LaneStatus"] = dColors;
                    }
                    else 
                        Logger.Simulate.Error("No Lane Status Color Loaded");
                    break;
                case "TierColors":
                    if (bRet)
                    {
                        this.oSimDataStore.dTierColors = dColors;
                        if (!ddColors.ContainsKey("TierColors"))
                            ddColors.Add("TierColors", dColors);
                        else
                            ddColors["TierColors"] = dColors;
                    }
                    else 
                        Logger.Simulate.Error("No Tier Color Loaded");
                    break;
                case "LaneAttributes":
                    if (bRet)
                    {
                        this.oSimDataStore.dLaneAttrColors = dColors;
                        if (!ddColors.ContainsKey("LaneAttrs"))
                            ddColors.Add("LaneAttrs", dColors);
                        else
                            ddColors["LaneAttrs"] = dColors;
                    }
                    else 
                        Logger.Simulate.Error("No Lane Attribute Color Loaded");
                    break;
                case "MoveKinds":
                    if (bRet)
                    {
                        this.oSimDataStore.dMoveKindColors = dColors;
                        if (!ddColors.ContainsKey("MoveKinds"))
                            ddColors.Add("MoveKinds", dColors);
                        else
                            ddColors["MoveKinds"] = dColors;
                    }
                    else 
                        Logger.Simulate.Error("No MoveKind Colors Loaded");
                    break;
                case "MotionStatus":
                    if (bRet)
                    {
                        this.oSimDataStore.dStepTravelStatusColors = dColors;
                        if (!ddColors.ContainsKey("MotionStatus"))
                            ddColors.Add("MotionStatus", dColors);
                        else
                            ddColors["MotionStatus"] = dColors;
                    }
                    else 
                        Logger.Simulate.Error("No TravelStatus Colors Loaded");
                    break;
            }

            if (bRet)
                this.ProjectToViewFrameEvent(this, new ProjectToViewFrameEventArgs()
                    {
                        eProjectType = StatusEnums.ProjectType.Create,
                        oPPTViewFrame = new ProjectPackageToViewFrame()
                        {
                            dColorDics = ddColors
                        }
                    });

            return bRet;
        }

        // 车道 Lane。
        private bool LoadLanes(string SheetName, ref List<Lane> lLanes)
        {
            Sheet Sht;
            bool bRet = true;
            Lane oLane;
            uint TPID1 = 0;
            uint TPID2 = 0 ;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            lLanes.Clear();
            for (int i = 1; i < Sht.Rows; i++)
            {
                TPID1 = 0;
                TPID2 = 0;
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oLane = new Lane();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ID":
                                oLane.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "LINE_ID":
                                oLane.LineID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "TRANSPONDER_START":
                                TPID1 = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "TRANSPONDER_END":
                                TPID2 = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "TYPE":
                                oLane.eType = (AreaType)Enum.Parse(typeof(AreaType), Sht.getCell(j, i).Contents);
                                break;
                            case "CHE_NO":
                                oLane.CheNo = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "AREA_LANE_ID":
                                if (Sht.getCell(j, i).Type == CellType.NUMBER)
                                    oLane.AreaLaneID = Sht.getCell(j, i).Value.ToString();
                                else if (Sht.getCell(j, i).Type == CellType.LABEL)
                                    oLane.AreaLaneID = Sht.getCell(j, i).Contents;
                                break;
                            case "ATTR":
                                oLane.eAttr = (LaneAttribute)Enum.Parse(typeof(LaneAttribute), Sht.getCell(j, i).Value.ToString());
                                break;
                        }
                    }

                    // 调整 Start TP 和 End TP 的相对位置，以保证总是位置上从小到大
                    if (TPID1 > 0 && this.oSimDataStore.dTransponders.ContainsKey(TPID1) && TPID2 > 0 && this.oSimDataStore.dTransponders.ContainsKey(TPID2))
                    {
                        if (Math.Abs(this.oSimDataStore.dTransponders[TPID1].LogicPosX - this.oSimDataStore.dTransponders[TPID2].LogicPosX) < 0.01)
                        {
                            if (this.oSimDataStore.dTransponders[TPID1].LogicPosY > this.oSimDataStore.dTransponders[TPID2].LogicPosY)
                            {
                                oLane.TPIDStart = TPID2;
                                oLane.TPIDEnd = TPID1;
                            }
                            else
                            {
                                oLane.TPIDStart = TPID1;
                                oLane.TPIDEnd = TPID2;
                            }
                            oLane.RotateAngle = 90;
                        }
                        else if (Math.Abs(this.oSimDataStore.dTransponders[TPID1].LogicPosY - this.oSimDataStore.dTransponders[TPID2].LogicPosY) < 0.01)
                        {
                            if (this.oSimDataStore.dTransponders[TPID1].LogicPosX > this.oSimDataStore.dTransponders[TPID2].LogicPosX)
                            {
                                oLane.TPIDStart = TPID2;
                                oLane.TPIDEnd = TPID1;
                            }
                            else
                            {
                                oLane.TPIDStart = TPID1;
                                oLane.TPIDEnd = TPID2;
                            }
                            oLane.RotateAngle = 0;
                        }
                    }
                    else bRet = false;

                    // 补充定义
                    if (bRet)
                    {
                        oLane.Name = "Lane" + oLane.ID.ToString().PadLeft(4, '0');
                        oLane.InitLen = (this.oSimDataStore.dTransponders[oLane.TPIDEnd].LogicPosX - this.oSimDataStore.dTransponders[oLane.TPIDStart].LogicPosX)
                            + (this.oSimDataStore.dTransponders[oLane.TPIDEnd].LogicPosY - this.oSimDataStore.dTransponders[oLane.TPIDStart].LogicPosY);
                        if (oLane.eType == AreaType.STS_PB)
                        {
                            oLane.oDirSign = new DirSign();
                            oLane.oDirSign.Name = "LaneDirSign" + oLane.ID.ToString().PadLeft(4, '0');
                        }
                        oLane.pMid.X = (this.oSimDataStore.dTransponders[oLane.TPIDEnd].LogicPosX + this.oSimDataStore.dTransponders[oLane.TPIDStart].LogicPosX) / 2;
                        oLane.pMid.Y = (this.oSimDataStore.dTransponders[oLane.TPIDEnd].LogicPosY + this.oSimDataStore.dTransponders[oLane.TPIDStart].LogicPosY) / 2;
                        oLane.pWork = oLane.pMid;   // 防止不用支架后无值
                        oLane.Width = 4;

                        // 关联 AGVLine 和 Transponder
                        this.oSimDataStore.dAGVLines[oLane.LineID].lLaneIDs.Add(oLane.ID);
                        this.oSimDataStore.dTransponders[oLane.TPIDStart].LaneID = oLane.ID;
                        this.oSimDataStore.dTransponders[oLane.TPIDEnd].LaneID = oLane.ID;
                    }
                    if (!bRet || !oLane.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load Lane in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    if (bRet)
                        lLanes.Add(oLane);
                }
            }
            if (bRet)
            {
                if (lLanes.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Lane Loaded");
                }
                else
                {
                    foreach (Lane obj in lLanes)
                        this.oSimDataStore.dLanes.Add(obj.ID, obj);

                    this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                        {
                            eProjectType = StatusEnums.ProjectType.Create,
                            oPPTViewFrame = new ProjectPackageToViewFrame()
                            {
                                lLanes = this.oSimDataStore.dLanes.Values.ToList()
                            }
                        });
                }
            }
            return bRet;
        }

        // 支架(伴侣) Mate。
        private bool LoadMates(string SheetName, ref List<Mate> lMs)
        {
            Sheet Sht;
            bool bRet = true;
            Mate oM;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            lMs.Clear();
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oM = new Mate();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "MateInd":
                                oM.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "BlockName":
                                oM.BlockName = Sht.getCell(j, i).Contents;
                                break;
                            case "TPStart":
                                oM.TPIDStart = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "TPEnd":
                                oM.TPIDEnd = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "AreaLaneID":
                                oM.AreaLaneID = Sht.getCell(j, i).Contents;
                                break;
                        }
                    }

                    // 补充定义，调整同箱区车道的停车位置
                    foreach (Lane oL in this.oSimDataStore.dLanes.Values)
                    {
                        if (oL.eType == AreaType.WS_TP && oL.CheNo == Convert.ToUInt32(oM.BlockName.Substring(oM.BlockName.Length - 1, 1)))
                        {
                            oL.pWork.X = (this.oSimDataStore.dTransponders[oM.TPIDStart].LogicPosX + this.oSimDataStore.dTransponders[oM.TPIDEnd].LogicPosX) / 2;
                            if (oL.AreaLaneID == oM.AreaLaneID)
                            {
                                oM.LaneID = oL.ID;
                                oL.MateID = oM.ID;
                                oM.Name = "Mate" + oM.BlockName + oM.AreaLaneID;
                            }
                        }
                    }
                    oM.Width = 3;
                    if (oM.LaneID == 0) 
                        bRet = false;
                    
                    if (bRet)
                    {
                        if (oM.TPIDStart > 0 && this.oSimDataStore.dTransponders.ContainsKey(oM.TPIDStart))
                            this.oSimDataStore.dTransponders[oM.TPIDStart].MateID = oM.ID;
                        else 
                            bRet = false;
                        if (oM.TPIDEnd > 0 && this.oSimDataStore.dTransponders.ContainsKey(oM.TPIDEnd))
                            this.oSimDataStore.dTransponders[oM.TPIDEnd].MateID = oM.ID;
                        else 
                            bRet = false;
                    }

                    if (!bRet || !oM.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load Mate in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    
                    if (bRet) 
                        lMs.Add(oM);
                }
            }
            if (bRet)
            {
                if (lMs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Mate Loaded");
                }
                else
                {
                    foreach (Mate obj in lMs)
                        this.oSimDataStore.dMates.Add(obj.ID, obj);
                    this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                        {
                            eProjectType = StatusEnums.ProjectType.Create,
                            oPPTViewFrame = new ProjectPackageToViewFrame()
                            {
                                lMates = lMs
                            }
                        });
                }
            }
            return bRet;
        }

        // ASC 的 WorkPoint 作业位置。
        private bool GenerateASCWorkPoints()
        {
            bool bRet = true;
            ASCWorkPoint oAWP;
            List<ASCWorkPoint> lCWPs = new List<ASCWorkPoint>();
            string IndStr;

            // 部分来自箱区倍
            foreach (Block oB in this.oSimDataStore.dBlocks.Values)
            {
                if (oB.BayVaryDir == "X")
                {
                    for (int i = 0; i < oB.lBlockDivsX.Count; i++)
                    {
                        oAWP = new ASCWorkPoint();
                        oAWP.BlockName = oB.Name;
                        oAWP.Bay = Convert.ToUInt32(2 * i + 1);
                        oAWP.IndStr = "YARD." + oAWP.BlockName + "." + oAWP.Bay.ToString();
                        oAWP.BasePoint.Y = oB.Y - 2;
                        if (i < oB.lBlockDivsX.Count - 1) oAWP.BasePoint.X = oB.X + (oB.lBlockDivsX[i].StartPos + oB.lBlockDivsX[i + 1].StartPos) / 2;
                        else oAWP.BasePoint.X = oB.X + (oB.lBlockDivsX[i].StartPos + oB.MarginX) / 2;
                        if (!oAWP.CheckDefinitionCompleteness())
                        {
                            bRet = false;
                            Logger.Simulate.Error("Generate ASC Work Point Error For Block : " + oB.Name + " Row : " + Convert.ToUInt32(2 * i + 1));
                            break;
                        }
                        else lCWPs.Add(oAWP);
                        if (i < oB.lBlockDivsX.Count - 1)
                        {
                            oAWP = new ASCWorkPoint();
                            oAWP.BlockName = oB.Name;
                            oAWP.Bay = Convert.ToUInt32(2 * i + 2);
                            oAWP.IndStr = "YARD." + oAWP.BlockName + "." + oAWP.Bay.ToString();
                            oAWP.BasePoint.Y = oB.Y - 2;
                            oAWP.BasePoint.X = oB.X + (oB.lBlockDivsX[i].StartPos + oB.lBlockDivsX[i + 1].StartPos) / 2;
                            if (!oAWP.CheckDefinitionCompleteness())
                            {
                                bRet = false;
                                Logger.Simulate.Error("Generate ASC Work Point Error For Block : " + oB.Name + " Row : " + Convert.ToUInt32(2 * i + 2));
                                break;
                            }
                            else 
                                lCWPs.Add(oAWP);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < oB.lBlockDivsY.Count; i++)
                    {
                        oAWP = new ASCWorkPoint();
                        oAWP.BlockName = oB.Name;
                        oAWP.Bay = Convert.ToUInt32(2 * i + 1);
                        oAWP.IndStr = "YARD." + oAWP.BlockName + "." + oAWP.Bay.ToString();
                        oAWP.BasePoint.X = oB.X - 2;
                        if (i < oB.lBlockDivsY.Count - 1) oAWP.BasePoint.Y = oB.X + (oB.lBlockDivsY[i].StartPos + oB.lBlockDivsY[i + 1].StartPos) / 2;
                        else oAWP.BasePoint.Y = oB.Y + (oB.lBlockDivsY[i].StartPos + oB.MarginY) / 2;
                        if (!oAWP.CheckDefinitionCompleteness())
                        {
                            bRet = false;
                            Logger.Simulate.Error("Generate ASC Work Point Error For Block : " + oB.Name + " Row : " + Convert.ToUInt32(2 * i + 1));
                            break;
                        }
                        else lCWPs.Add(oAWP);
                        if (i < oB.lBlockDivsY.Count - 1)
                        {
                            oAWP = new ASCWorkPoint();
                            oAWP.BlockName = oB.Name;
                            oAWP.Bay = Convert.ToUInt32(2 * i + 2);
                            oAWP.IndStr = "YARD." + oAWP.BlockName + "." + oAWP.Bay.ToString();
                            oAWP.BasePoint.X = oB.X - 2;
                            oAWP.BasePoint.Y = oB.Y + (oB.lBlockDivsY[i].StartPos + oB.lBlockDivsY[i + 1].StartPos) / 2;
                            if (!oAWP.CheckDefinitionCompleteness())
                            {
                                bRet = false;
                                Logger.Simulate.Error("Generate ASC Work Point Error For Block : " + oB.Name + " Row : " + Convert.ToUInt32(2 * i + 2));
                                break;
                            }
                            else
                                lCWPs.Add(oAWP);
                        }
                    }
                }
                if (!bRet) 
                    break;
            }

            // 根据 Lane.pWork 确定海测作业位置。 若存在支架，则该位置会受支架调整
            // pWork 为双箱作业位，到两个单箱作业位的距离各自为20尺箱的一半长度
            foreach (Lane oL in this.oSimDataStore.dLanes.Values)
            {
                if (oL.eType == AreaType.WS_TP)
                {
                    for (int i = 1; i <= 3; i++)
                    {
                        IndStr = "WSTP.A0" + oL.CheNo + "." + i.ToString();
                        if (lCWPs.Find(u => u.IndStr == IndStr) == null)
                        {
                            oAWP = new ASCWorkPoint();
                            oAWP.BlockName = "A0" + oL.CheNo.ToString();
                            oAWP.Bay = 1;
                            oAWP.IndStr = IndStr;
                            if (this.oSimDataStore.dBlocks.ContainsKey(oAWP.BlockName))
                            {
                                if (this.oSimDataStore.dBlocks[oAWP.BlockName].BayVaryDir == "X")
                                {
                                    oAWP.BasePoint.X = oL.pWork.X + 3 * (i - 1);
                                    oAWP.BasePoint.Y = this.oSimDataStore.dBlocks[oAWP.BlockName].Y - 2;
                                }
                                else if (this.oSimDataStore.dBlocks[oAWP.BlockName].BayVaryDir == "Y")
                                {
                                    oAWP.BasePoint.X = this.oSimDataStore.dBlocks[oAWP.BlockName].X - 2;
                                    oAWP.BasePoint.Y = oL.pWork.Y + 3 * (i - 1);
                                }
                                else
                                {
                                    bRet = false;
                                    Logger.Simulate.Error("Unexpected BayVaryDir : " + this.oSimDataStore.dBlocks[oAWP.BlockName].BayVaryDir + " When Caculating ASC Work Point : " + IndStr);
                                    break;
                                }
                                if (bRet)
                                {
                                    if (!oAWP.CheckDefinitionCompleteness())
                                    {
                                        bRet = false;
                                        Logger.Simulate.Error("Invalid ASC WorkPoint : " + IndStr);
                                        break;
                                    }
                                    else
                                        lCWPs.Add(oAWP);
                                }
                            }
                            else
                            {
                                bRet = false;
                                Logger.Simulate.Error("Combine Block Name Failed for Lane : " + oL.ID.ToString() + " with CheNo : " + oL.CheNo.ToString());
                                break;
                            }
                        }
                        if (!bRet)
                            break;
                    }
                }
                if (!bRet)
                    break;
            }

            if (bRet)
            {
                if (lCWPs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No ASC Work Point Generated");
                }
                else
                    foreach (ASCWorkPoint obj in lCWPs)
                        this.oSimDataStore.dASCWorkPoints.Add(obj.IndStr, obj);
            }
            return bRet;
        }

        // AGVType AGV 车型。
        private bool LoadAGVTypes(string SheetName)
        {
            Sheet Sht;
            AGVType oAT;
            List<AGVType> lATs = new List<AGVType>();
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            lATs.Clear();
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oAT = new AGVType();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ID":
                                oAT.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "Length":
                                oAT.Length = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "Width":
                                oAT.Width = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "VeloFullUpper":
                                oAT.VeloFullUpper = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "VeloEmptyUpper":
                                oAT.VeloEmptyUpper = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "VeloTurnUpper":
                                oAT.VeloTurnUpper = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "Acceleration":
                                oAT.Acceleration = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "TurnRadius":
                                oAT.TurnRadius = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                        }
                    }
                    if (!oAT.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load AGVType in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                        lATs.Add(oAT);
                }
            }
            if (bRet)
            {
                if (lATs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No AGVType Loaded");
                }
                else
                    foreach (AGVType obj in lATs)
                        this.oSimDataStore.dAGVTypes.Add(obj.ID, obj);
            }
            return bRet;
        }

        // AGV。
        private bool LoadAGVs(string SheetName)
        {
            Sheet Sht;
            AGV oAGV;
            List<AGV> lAGVs;
            List<AGV_STATUS> lAgvStatus;
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            lAGVs = new List<AGV>();
            lAgvStatus = new List<AGV_STATUS>();
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            lAGVs.Clear();
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oAGV = new AGV();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "CHE_ID":
                                oAGV.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "Type":
                                oAGV.oType = this.oSimDataStore.dAGVTypes[Convert.ToUInt32(Sht.getCell(j, i).Value)];
                                break;
                            case "LaneID":
                                oAGV.CurrLaneID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                        }
                    }

                    // 补充定义
                    oAGV.Name = "AGV" + oAGV.ID.ToString();

                    if (!oAGV.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load AGV in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                        lAGVs.Add(oAGV);
                }
            }
            if (bRet)
            {
                if (lAGVs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No AGV Loaded");
                }
                else if (lAGVs.Select(u => u.CurrLaneID).Distinct().Count() < lAGVs.Count)
                {
                    bRet = false;
                    Logger.Simulate.Error("Multiple AGV Allocated To A Same Lane");
                }
                else
                    // 注册
                    foreach (AGV obj in lAGVs)
                    {
                        this.oSimDataStore.dAGVs.Add(obj.ID, obj);
                        this.oSimDataStore.dLanes[obj.CurrLaneID].AGVNo = obj.ID;
                        this.oSimDataStore.dLanes[obj.CurrLaneID].eStatus = LaneStatus.OCCUPIED;
                        obj.eMotionStatus = StatusEnums.MotionStatus.Free;
                        obj.MidPoint = this.oSimDataStore.dLanes[obj.CurrLaneID].pWork;
                        obj.RotateAngle = this.oSimDataStore.dLanes[obj.CurrLaneID].RotateAngle;
                    }

                this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                    {
                        eProjectType = StatusEnums.ProjectType.Create,
                        oPPTViewFrame = new ProjectPackageToViewFrame()
                        {
                            lAGVs = lAGVs
                        }
                    });

                this.GenerateAGVStatus(lAGVs);
            }

            return bRet;
        }

        // TrolleyProcStats
        private bool LoadQCActionStats(string SheetName)
        {
            Sheet Sht;
            QCActionTimeStat oTPS;
            List<QCActionTimeStat> lTPSs = new List<QCActionTimeStat>();
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oTPS = new QCActionTimeStat();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ID":
                                oTPS.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "Min":
                                oTPS.Min = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "Max":
                                oTPS.Max = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "Avg":
                                oTPS.Avg = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                        }
                    }
                    if (!oTPS.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load QC Action Statistic in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                    {
                        lTPSs.Add(oTPS);
                    }
                }
            }
            if (bRet)
            {
                if (lTPSs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Trolley Proc Statistic Loaded");
                }
                else
                {
                    foreach (QCActionTimeStat obj in lTPSs)
                    {
                        this.oSimDataStore.dQCActionStats.Add(obj.ID, obj);
                    }
                }
            }
            return bRet;
        }

        // PlatformSlot
        private bool LoadQCPlatformSlots(string SheetName)
        {
            Sheet Sht;
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }



            return bRet;
        }

        // TrolleyType
        private bool LoadQCTrolleyStats(string SheetName)
        {
            Sheet Sht;
            QCTrolleyTimeStat oTT;
            List<QCTrolleyTimeStat> lTTs = new List<QCTrolleyTimeStat>();
            uint AimID;
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oTT = new QCTrolleyTimeStat();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ID":
                                oTT.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "WSRise":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCActionStats.ContainsKey(AimID)) 
                                    oTT.oStatWSRise = this.oSimDataStore.dQCActionStats[AimID];
                                break;
                            case "WSFall":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCActionStats.ContainsKey(AimID)) 
                                    oTT.oStatWSFall = this.oSimDataStore.dQCActionStats[AimID];
                                break;
                            case "WSToB":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCActionStats.ContainsKey(AimID)) 
                                    oTT.oStatWSToB = this.oSimDataStore.dQCActionStats[AimID];
                                break;
                            case "BToWS":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCActionStats.ContainsKey(AimID)) 
                                    oTT.oStatBToWS = this.oSimDataStore.dQCActionStats[AimID];
                                break;
                            case "BFall":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCActionStats.ContainsKey(AimID)) 
                                    oTT.oStatBFall = this.oSimDataStore.dQCActionStats[AimID];
                                break;
                            case "BRise":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCActionStats.ContainsKey(AimID)) 
                                    oTT.oStatBRise = this.oSimDataStore.dQCActionStats[AimID];
                                break;
                            case "BToLS":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCActionStats.ContainsKey(AimID))
                                    oTT.oStatBToLS = this.oSimDataStore.dQCActionStats[AimID];
                                break;
                            case "LSToB":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCActionStats.ContainsKey(AimID))
                                    oTT.oStatLSToB = this.oSimDataStore.dQCActionStats[AimID];
                                break;
                            case "LSRise":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCActionStats.ContainsKey(AimID))
                                    oTT.oStatLSRise = this.oSimDataStore.dQCActionStats[AimID];
                                break;
                            case "LSFall":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCActionStats.ContainsKey(AimID))
                                    oTT.oStatLSFall = this.oSimDataStore.dQCActionStats[AimID];
                                break;
                        }
                    }
                    if (!oTT.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load QC Trolley Type in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                    {
                        lTTs.Add(oTT);
                    }
                }
            }
            if (bRet)
            {
                if (lTTs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Trolley Type Loaded");
                }
                else
                {
                    foreach (QCTrolleyTimeStat obj in lTTs)
                    {
                        this.oSimDataStore.dQCTrolleyTimeStats.Add(obj.ID, obj);
                    }
                }
            }
            return bRet;
        }

        // QCType
        private bool LoadQCTypes(string SheetName)
        {
            Sheet Sht;
            bool bRet = true; ;
            QCType oQT;
            List<QCType> lQTs = new List<QCType>();

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oQT = new QCType();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ID":
                                oQT.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "TrackGauge":
                                oQT.TrackGauge = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "BaseGauge":
                                oQT.BaseGauge = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "FrontReach":
                                oQT.FrontReach = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "BackReach":
                                oQT.BackReach = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "CantileverWidth":
                                oQT.CantiWidth = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "Thickness":
                                oQT.Thickness = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "TravelSpeed":
                                oQT.TravelSpeed = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "PFSlotNum":
                                oQT.PlatformSlotNum = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                        }
                    }
                    if (!oQT.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load QC Type in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                    {
                        lQTs.Add(oQT);
                    }
                }
            }
            if (bRet)
            {
                if (lQTs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No QC Type Loaded");
                }
                else
                {
                    foreach (QCType obj in lQTs)
                    {
                        this.oSimDataStore.dQCTypes.Add(obj.ID, obj);
                    }

                }
            }
            return bRet;
        }

        // QC
        private bool LoadQCs(string SheetName)
        {
            Sheet Sht;
            QCDT oQC;
            List<QCDT> lQCs;
            List<STS_STATUS> lStsStatuses;
            QCPlatformSlot oSlot;
            uint AimID, PlatformStatID;
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            lQCs = new List<QCDT>();
            lStsStatuses = new List<STS_STATUS>();
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            lQCs.Clear();
            PlatformStatID = 0;
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oQC = new QCDT();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ID":
                                oQC.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "QcType":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCTypes.ContainsKey(AimID)) 
                                    oQC.oType = this.oSimDataStore.dQCTypes[AimID];
                                break;
                            case "X":
                                oQC.BasePoint.X = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "Y":
                                oQC.BasePoint.Y = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "MainTroStatID":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCTrolleyTimeStats.ContainsKey(AimID)) 
                                    oQC.MainTrolley.oQCTrolleyTimeStat = this.oSimDataStore.dQCTrolleyTimeStats[AimID];
                                break;
                            case "ViceTroStatID":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dQCTrolleyTimeStats.ContainsKey(AimID)) 
                                    oQC.ViceTrolley.oQCTrolleyTimeStat = this.oSimDataStore.dQCTrolleyTimeStats[AimID];
                                break;
                            case "PlatformActionStatID":
                                PlatformStatID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "nAgvCountMin":
                                oQC.nAGVCountMin = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "nAgvCountMax":
                                oQC.nAGVCountMax = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                        }
                    }

                    // 补充定义
                    oQC.Name = "QC" + oQC.ID.ToString();
                    for (int j = 0; j < oQC.oType.PlatformSlotNum; j++)
                    {
                        oSlot = new QCPlatformSlot(oQC) { ID = Convert.ToUInt32(j + 1) };
                        if (this.oSimDataStore.dQCActionStats.ContainsKey(PlatformStatID))
                            oSlot.oConfirmStat = this.oSimDataStore.dQCActionStats[PlatformStatID];
                        oQC.Platform.Add(oSlot);
                    }

                    if (!oQC.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load QC in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                    {
                        lQCs.Add(oQC);
                    }
                }
            }
            if (bRet)
            {
                if (lQCs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No QC Loaded");
                }
                else
                {
                    foreach (QCDT obj in lQCs)
                        this.oSimDataStore.dQCs.Add(obj.ID, obj);

                    this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                        {
                            eProjectType = StatusEnums.ProjectType.Create,
                            oPPTViewFrame = new ProjectPackageToViewFrame()
                            {
                                lQCs = lQCs
                            }
                        });

                    this.GenerateQCStatus(lQCs);
                }
            }

            return bRet;
        }

        // ASCType
        private bool LoadASCTypes(string SheetName)
        {
            Sheet Sht;
            ASCType oAT;
            List<ASCType> lATs = new List<ASCType>();
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oAT = new ASCType();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ID":
                                oAT.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "BaseGauge":
                                oAT.BaseGauge = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "TrackGauge":
                                oAT.TrackGauge = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "Thickness":
                                oAT.Thickness = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "TravelSpeed":
                                oAT.TravelSpeed = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "TrolleySpeed":
                                oAT.TrolleySpeed = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "FullLiftSpeed":
                                oAT.FullLiftSpeed = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "EmptyLiftSpeed":
                                oAT.EmptyLiftSpeed = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "MaxLiftHeight":
                                oAT.MaxLiftHeight = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "GanTroSimulMove":
                                oAT.bGanTroSimulMove = Convert.ToBoolean(Sht.getCell(j, i).Contents);
                                break;
                        }
                    }
                    if (!oAT.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load ASC Type in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                    {
                        lATs.Add(oAT);
                    }
                }
            }
            if (bRet)
            {
                if (lATs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No ASC Type Loaded");
                }
                else
                {
                    foreach (ASCType obj in lATs)
                    {
                        this.oSimDataStore.dASCTypes.Add(obj.ID, obj);
                    }

                }
            }
            return bRet;
        }

        // ASC
        private bool LoadASCs(string SheetName)
        {
            Sheet Sht;
            ASC oASC;
            List<ASC> lASCs;
            List<ASC_STATUS> lAscStatuses;
            uint AimID;
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            lAscStatuses = new List<ASC_STATUS>();
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            lASCs = new List<ASC>();
            lAscStatuses = new List<ASC_STATUS>();
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oASC = new ASC();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "CHE_ID":
                                oASC.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "BlockName":
                                oASC.BlockName = Sht.getCell(j, i).Contents;
                                if (this.oSimDataStore.dBlocks.ContainsKey(oASC.BlockName)) oASC.BasePoint.Y = this.oSimDataStore.dBlocks[oASC.BlockName].Y - 2;
                                break;
                            case "TypeID":
                                AimID = Convert.ToUInt32(Sht.getCell(j, i).Contents);
                                if (this.oSimDataStore.dASCTypes.ContainsKey(AimID)) oASC.oType = this.oSimDataStore.dASCTypes[AimID];
                                break;
                            case "Side":
                                Enum.TryParse<StatusEnums.ASCSide>(Sht.getCell(j, i).Contents, out oASC.eSide);
                                break;
                            case "X":
                                oASC.BasePoint.X = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                        }
                    }

                    // 补充定义
                    oASC.Name = "ASC" + oASC.ID.ToString();
                    if (this.oSimDataStore.dBlocks.ContainsKey(oASC.BlockName))
                    {
                        if (oASC.eSide == StatusEnums.ASCSide.WS)
                            this.oSimDataStore.dBlocks[oASC.BlockName].WSASC = oASC.ID;
                        else if (oASC.eSide == StatusEnums.ASCSide.LS)
                            this.oSimDataStore.dBlocks[oASC.BlockName].LSASC = oASC.ID;
                    }

                    if (!oASC.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load ASC in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                        lASCs.Add(oASC);
                }
            }
            if (bRet)
            {
                if (lASCs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No ASC Loaded");
                }
                else
                {
                    foreach (ASC obj in lASCs)
                        this.oSimDataStore.dASCs.Add(obj.ID, obj);

                    this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                        {
                            eProjectType = StatusEnums.ProjectType.Create,
                            oPPTViewFrame = new ProjectPackageToViewFrame()
                            {
                                lASCs = lASCs
                            }
                        });

                    this.GenerateASCStatus(lASCs);
                }
            }

            return bRet;
        }

        // PileType
        private bool LoadPileTypes(string SheetName)
        {
            Sheet Sht;
            PileType oPT;
            List<PileType> lPTs = new List<PileType>();
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oPT = new PileType();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ContSize":
                                if (Sht.getCell(j, i).Type == CellType.EMPTY) 
                                    oPT.eContSize = StatusEnums.ContSize.Unknown;
                                else if (Sht.getCell(j, i).Contents == "20")
                                    oPT.eContSize = StatusEnums.ContSize.TEU;
                                else if (Sht.getCell(j, i).Contents == "40")
                                    oPT.eContSize = StatusEnums.ContSize.FEU;
                                else if (Sht.getCell(j, i).Contents == "45")
                                    oPT.eContSize = StatusEnums.ContSize.FFEU;
                                else oPT.eContSize = StatusEnums.ContSize.Unknown;
                                break;
                            case "Length":
                                oPT.Length = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "Width":
                                oPT.Width = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                        }
                    }
                    if (!oPT.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load Pile Type in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                    {
                        lPTs.Add(oPT);
                    }
                }
            }
            if (bRet)
            {
                if (lPTs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Pile Type Loaded");
                }
                else
                {
                    foreach (PileType obj in lPTs)
                    {
                        this.oSimDataStore.dPileTypes.Add(obj.eContSize, obj);
                    }
                }
            }
            return bRet;
        }

        // VesselType
        private bool LoadVesselTypes(string SheetName)
        {
            Sheet Sht;
            bool bRet = true;
            VesselType oVT;
            List<VesselType> lVTs = new List<VesselType>();

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oVT = new VesselType();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "VesNo":
                                oVT.VesNo = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "Length":
                                oVT.Length = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "Width":
                                oVT.Width = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "BowSpaceLen":
                                oVT.BowSpaceLen = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "SternSpaceLen":
                                oVT.SternSpaceLen = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "CabinNum":
                                oVT.CabinNum = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                        }
                    }

                    // 补充定义
                    if (oVT.CabinNum > 0)
                    {
                        oVT.CabinRange = oVT.Length - oVT.BowSpaceLen - oVT.SternSpaceLen;
                        oVT.SingleCabinLength = oVT.CabinRange / oVT.CabinNum;
                    }

                    if (!oVT.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load Vessel Type in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                    {
                        lVTs.Add(oVT);
                    }
                }
            }
            if (bRet)
            {
                if (lVTs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Vessel Type Loaded");
                }
                else
                {
                    foreach (VesselType obj in lVTs)
                        this.oSimDataStore.dVesselTypes.Add(obj.VesNo, obj);
                }
            }
            return bRet;
        }

        // ISOReference
        private bool LoadISORefs(string SheetName)
        {
            Sheet Sht;
            ISORef oISO;
            List<ISORef> lISOs = new List<ISORef>();
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oISO = new ISORef();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "ISO":
                                oISO.ISO = Sht.getCell(j, i).Contents;
                                break;
                            case "Size":
                                Enum.TryParse<StatusEnums.ContSize>(Sht.getCell(j, i).Contents, out oISO.eContSize);
                                break;
                            case "Type":
                                Enum.TryParse<StatusEnums.ContType>(Sht.getCell(j, i).Contents, out oISO.eContType);
                                break;
                            case "Length":
                                oISO.ContainerLengthCM = (short)(Convert.ToInt32(Sht.getCell(j, i).Value) / 100);
                                break;
                            case "Height":
                                oISO.ContainerHeightCM = (short)(Convert.ToInt32(Sht.getCell(j, i).Value) / 100);
                                break;
                        }
                    }
                    if (!oISO.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load ISO in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                    {
                        lISOs.Add(oISO);
                    }
                }
            }
            if (bRet)
            {
                if (lISOs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No ISOReference Loaded");
                }
                else
                {
                    foreach (ISORef obj in lISOs)
                    {
                        this.oSimDataStore.dISORefs.Add(obj.ISO, obj);
                    }
                }
            }
            return bRet;
        }

        // VMS_EXPECT_TIME
        private bool LoadVMSExpectTime(string SheetName)
        {
            Sheet Sht;
            SimExpectTime oET;
            List<SimExpectTime> lETs = new List<SimExpectTime>();
            bool bRet = true;

            Sht = this.WkBExtraCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : " + SheetName);
                return false;
            }

            for (int i = 1; i < Sht.Rows; i++)
            {
                oET = new SimExpectTime();
                for (int j = 0; j < Sht.Columns; j++)
                {
                    switch (Sht.getCell(j, 0).Contents)
                    {
                        case "ID":
                            oET.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                            break;
                        case "TYPE":
                            oET.l2LType = (LaneToLaneType)Enum.Parse(typeof(LaneToLaneType), Sht.getCell(j, i).Contents);
                            break;
                        case "FROMID":
                            oET.fromID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                            break;
                        case "TOID":
                            oET.toID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                            break;
                        case "EXPECTTIME":
                            oET.expectTime = Convert.ToInt32(Sht.getCell(j, i).Value);
                            break;
                    }
                }
                oET.DependentDefinition();
                if (!oET.CheckDefinitionCompleteness())
                {
                    bRet = false;
                    Logger.Simulate.Error("Load VMS Expect Time in Row : " + i.ToString() + " Failed");
                    break;
                }
                else
                {
                    lETs.Add(oET);
                }
            }
            if (bRet)
            {
                if (lETs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No VMS Expect Time Loaded");
                }
                else
                {
                    this.oSimDataStore.lVMSExpectTimes = lETs;
                }
            }
            return bRet;
        }

        // SimContainerInfo
        private bool LoadSimContainerInfos(string SheetName)
        {
            Sheet Sht;
            SimContainerInfo oSCI;
            List<SimContainerInfo> lSCIs = new List<SimContainerInfo>();
            string ClassInd, TempStr;
            bool bRet = true;

            Sht = this.WkBCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Case Excel Named : " + SheetName);
                return false;
            }

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oSCI = new SimContainerInfo();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "CNTR":
                                oSCI.ContainerID = Sht.getCell(j, i).Contents;
                                break;
                            case "SHIP_NO":
                                oSCI.VoyageNo = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "CNTR_CLASS":
                                ClassInd = Sht.getCell(j, i).Contents;
                                if (ClassInd == "I") 
                                    oSCI.MoveKind = Move_Kind.DSCH;
                                else if (ClassInd == "E") 
                                    oSCI.MoveKind = Move_Kind.LOAD;
                                break;
                            case "CNTR_ISO":
                                oSCI.ISO = Sht.getCell(j, i).Contents;
                                break;
                            case "CNTR_TARE_WGT":
                                oSCI.TareWeight = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "CNTR_GROSS_WGT":
                                oSCI.GrossWeight = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "E_F_ID":
                                Enum.TryParse<StatusEnums.EF>(Sht.getCell(j, i).Contents, out oSCI.eEF);
                                break;
                            case "CNTR_TYP_COD":
                                Enum.TryParse<StatusEnums.ContType>(Sht.getCell(j, i).Contents, out oSCI.eType);
                                break;
                            case "CNTR_SIZ_COD":
                                oSCI.eSize = StatusEnums.GetContSize(Sht.getCell(j, i).Contents);
                                break;
                            case "CNTR_PLAC":
                                oSCI.YardLoc = Sht.getCell(j, i).Contents;
                                break;
                            case "BAY_NO":
                                oSCI.VesLoc = Sht.getCell(j, i).Contents.PadLeft(7, '0');
                                break;
                            case "LOAD_PORT_COD":
                                oSCI.PortOfLoad = Sht.getCell(j, i).Contents;
                                break;
                            case "TRANS_PORT_COD":
                                oSCI.PortOfTrans = Sht.getCell(j, i).Contents;
                                break;
                            case "DISC_PORT_COD":
                                oSCI.PortOfDisc = Sht.getCell(j, i).Contents;
                                break;
                        }
                    }

                    if (this.oSimDataStore.dVoyages.ContainsKey(oSCI.VoyageNo))
                        oSCI.VesNo = this.oSimDataStore.dVoyages[oSCI.VoyageNo].VesID;

                    if (oSCI.MoveKind == Move_Kind.LOAD)
                    {
                        oSCI.DoorDirection = 2;
                    }
                    else if (oSCI.MoveKind == Move_Kind.DSCH)
                    {
                        oSCI.DoorDirection = 1;
                    }
                    if (oSCI.YardLoc.Length > 0)
                    {
                        oSCI.YardBlock = oSCI.YardLoc.Substring(0, 3);
                        oSCI.YardBay = Convert.ToInt32(oSCI.YardLoc.Substring(3, 2));
                        oSCI.YardRow = Convert.ToInt32(oSCI.YardLoc.Substring(5, 2));
                        oSCI.YardTier = Convert.ToInt32(oSCI.YardLoc.Substring(7, 1));
                    }
                    if (oSCI.VesLoc.Length > 0)
                    {
                        oSCI.VesBay = Convert.ToInt32(oSCI.VesLoc.Substring(0, 3));
                        oSCI.VesRow = Convert.ToInt32(oSCI.VesLoc.Substring(3, 2));
                        oSCI.VesTier = Convert.ToInt32(oSCI.VesLoc.Substring(5, 2));
                    }
                    if (oSCI.PlanLoc.Length > 0)
                    {
                        oSCI.PlanBlock = oSCI.PlanLoc.Substring(0, 3);
                        oSCI.PlanBay = Convert.ToInt32(oSCI.PlanLoc.Substring(3, 2));
                        oSCI.PlanRow = Convert.ToInt32(oSCI.PlanLoc.Substring(5, 2));
                        oSCI.PlanTier = Convert.ToInt32(oSCI.PlanLoc.Substring(7, 1));
                    }

                    if (this.oSimDataStore.dWorkInstructions.ContainsKey(oSCI.ContainerID)
                        && this.oSimDataStore.dWorkInstructions[oSCI.ContainerID].MOVE_KIND == Move_Kind.LOAD
                        && this.oSimDataStore.dWorkInstructions[oSCI.ContainerID].DESTINATION_CARRIER_SLOT != null
                        && this.oSimDataStore.dWorkInstructions[oSCI.ContainerID].DESTINATION_CARRIER_SLOT.Length > 0)
                    {
                        TempStr = this.oSimDataStore.dWorkInstructions[oSCI.ContainerID].DESTINATION_CARRIER_SLOT;
                        oSCI.StowLoc = TempStr;
                        oSCI.StowBay = Convert.ToInt32(TempStr.Substring(0, 3));
                        oSCI.StowRow = Convert.ToInt32(TempStr.Substring(3, 2));
                        oSCI.StowTier = Convert.ToInt32(TempStr.Substring(5, 2));
                    }

                    if (!oSCI.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load Container Info in Sheet : " + SheetName + " Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                        lSCIs.Add(oSCI);
                }
            }
            if (bRet)
            {
                if (lSCIs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Container Info in Sheet : " + SheetName + " Loaded");
                }
                else
                    foreach (SimContainerInfo obj in lSCIs)
                        if (!this.oSimDataStore.dSimContainerInfos.ContainsKey(obj.ContainerID))
                            this.oSimDataStore.dSimContainerInfos.Add(obj.ContainerID, obj);
            }
            return bRet;
        }

        // 根据 ContInfo 得到需要的 Pile，如果有或者能够生成，则占据该占的位置
        private bool LoadPiles()
        {
            // 在场箱和预约箱
            List<SimContainerInfo> lTempContInfos;
            string PileName;
            int Tier;
            Pile oP;
            PilesReclaimAndReleaseEventArgs e;

            lTempContInfos = this.oSimDataStore.dSimContainerInfos.Values.Where(u => !string.IsNullOrWhiteSpace(u.YardLoc) || !string.IsNullOrWhiteSpace(u.PlanLoc)).ToList();

            foreach (SimContainerInfo oSCI in lTempContInfos)
            {
                if (!string.IsNullOrWhiteSpace(oSCI.YardLoc))
                {
                    PileName = oSCI.YardLoc.Substring(0, 7);
                    Tier = oSCI.YardTier;
                }
                else
                {
                    PileName = oSCI.PlanLoc.Substring(0, 7);
                    Tier = oSCI.PlanTier;
                }
                if (!this.oSimDataStore.dPiles.ContainsKey(PileName))
                {
                    e = new PilesReclaimAndReleaseEventArgs();
                    e.lReclaimMsgs.Add(new PileReclaimMsg() 
                        { 
                            PileName = PileName, 
                            oPileType = this.oSimDataStore.dPileTypes[oSCI.eSize] 
                        });
                    this.PilesReclaimAndReleaseEvent.Invoke(this, e);
                    if (!e.IsSucc)
                    {
                        Logger.Simulate.Error("No Such Pile : " + PileName + " For Cont : " + oSCI.ContainerID);
                        return false;
                    }
                }
                oP = this.oSimDataStore.dPiles[PileName];
                if (oP.oType.eContSize != oSCI.eSize)
                {
                    Logger.Simulate.Error("Pile : " + PileName + " Is For Conts Of Size : " + oP.oType.eContSize.ToString() 
                        + " While Cont : " + oSCI.ContainerID + "Is Of Size : " + oSCI.eSize.ToString());
                    return false;
                }
                oP.lUnits[Tier - 1].ContReserve(oSCI.ContainerID, oSCI.eSize.ToString());
                if (!string.IsNullOrWhiteSpace(oSCI.YardLoc))
                    oP.lUnits[Tier - 1].ContOccupy(oSCI.ContainerID);
            }

            // 检查下lPs是否有悬空
            foreach (Pile obj in this.oSimDataStore.dPiles.Values)
            {
                for (int i = 1; i < obj.lUnits.Count; i++)
                {
                    if (obj.lUnits[i].eContStoreStage == StatusEnums.StoreStage.Stored
                        && obj.lUnits[i - 1].eContStoreStage != StatusEnums.StoreStage.Stored)
                    {
                        Logger.Simulate.Error("Tier : " + (i + 1).ToString() + " hollowed in Pile :" + obj.Name);
                        return false;
                    }
                }
                obj.RenewNums();
            }

            this.ProjectToViewFrameEvent.Invoke(this, new ProjectToViewFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Renew,
                    oPPTViewFrame = new ProjectPackageToViewFrame()
                    {
                        lPiles = this.oSimDataStore.dPiles.Values.ToList()
                    }
                });

            return true;
        }

        // Vessel，进字典，有图像
        private bool LoadVessels(string SheetName)
        {
            bool bRet = true;
            Sheet Sht;
            Vessel oVes;
            String Str;
            List<Vessel> lVs;

            Sht = this.WkBCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Case Excel Named : " + SheetName);
                return false;
            }

            lVs = new List<Vessel>();
            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oVes = new Vessel();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents.Trim())
                        {
                            case "VOYAGE_NO":
                                oVes.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                if (this.oSimDataStore.dVesselTypes.ContainsKey(oVes.ID)) 
                                    oVes.oType = this.oSimDataStore.dVesselTypes[oVes.ID];
                                break;
                            case "BERTH_WAY":
                                Enum.TryParse<StatusEnums.BerthWay>(Sht.getCell(j, i).Contents.Trim(), out oVes.eBerthWay);
                                break;
                            case "BEG_METER":
                                oVes.ExpBeginMeter = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "END_METER":
                                oVes.ExpEndMeter = Convert.ToDouble(Sht.getCell(j, i).Value);
                                break;
                            case "SHIP_NAM":
                                oVes.ShipName = Sht.getCell(j, i).Contents;
                                break;
                            case "SHIP_COD":
                                oVes.ShipCode = Sht.getCell(j, i).Contents;
                                break;
                            case "UPD_TIM":
                                Str = Sht.getCell(j, i).Contents;
                                Str = Str.Insert(Str.LastIndexOf("/") + 1, "20");
                                oVes.Updated = Convert.ToDateTime(Str);
                                break;
                        }
                    }
                    oVes.Name = "Vessel" + oVes.ID.ToString();
                    oVes.eVesselVisitPhrase = StatusEnums.VesselVisitPhrase.InPortArriving;
                    oVes.YAppend = this.oSimDataStore.oTerminalRegion.LandHeight + 2;
                    oVes.BeginMeter = 348 - oVes.ExpBeginMeter + 100;
                    oVes.EndMeter = 348 - oVes.ExpEndMeter + 100;
                    if (!oVes.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load Vessel in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                    {
                        lVs.Add(oVes);
                    }
                }
            }
            if (bRet)
            {
                if (lVs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Vessel Loaded");
                }
                else
                {
                    foreach (Vessel obj in lVs)
                    {
                        this.oSimDataStore.dVessels.Add(obj.ID, obj);

                    }
                }
            }
            return bRet;
        }

        // Voyage，进字典，无图像
        private bool LoadVoyages(string SheetName)
        {
            bool bRet = true;
            Sheet Sht;
            Voyage oV;
            List<Voyage> lVs = new List<Voyage>();

            Sht = this.WkBCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Case Excel Named : " + SheetName);
                return false;
            }

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(0, i).Type != CellType.EMPTY)
                {
                    oV = new Voyage();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "SHIP_NO":
                                oV.ID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "VOYAGE_NO":
                                oV.VesID = Convert.ToUInt32(Sht.getCell(j, i).Value);
                                break;
                            case "VOYAGE":
                                oV.Name = Sht.getCell(j, i).Contents;
                                break;
                            case "I_E_ID":
                                oV.IE = Sht.getCell(j, i).Contents;
                                break;
                        }
                    }
                    if (!oV.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load Voyage in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                    {
                        lVs.Add(oV);
                    }
                }
            }
            if (bRet)
            {
                if (lVs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Voyage Loaded");
                }
                else
                {
                    foreach (Voyage obj in lVs)
                    {
                        this.oSimDataStore.dVoyages.Add(obj.ID, obj);
                    }
                }
            }
            return bRet;
        }

        // WorkQueues，进字典，无图像
        private bool LoadWorkQueues(string SheetName)
        {
            Sheet Sht;
            STS_WORK_QUEUE_STATUS oWQ;
            List<STS_WORK_QUEUE_STATUS> lWQs;
            int IndCol;
            string Str;
            bool bRet = true;

            Sht = this.WkBCase.getSheet(SheetName);
            lWQs = new List<STS_WORK_QUEUE_STATUS>();
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Case Excel Named : " + SheetName);
                return false;
            }

            IndCol = Sht.findCell("WORK_QUEUE_NO").Column;

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(IndCol, i).Type != CellType.EMPTY)
                {
                    oWQ = new STS_WORK_QUEUE_STATUS();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "WORK_QUEUE_NO":
                                oWQ.WORK_QUEUE = Sht.getCell(j, i).Contents;
                                break;
                            case "QUEUE_TYP":
                                Str = Sht.getCell(j, i).Contents;
                                if (Str == "SO") oWQ.MOVE_KIND = Move_Kind.LOAD;
                                else if (Str == "SI") oWQ.MOVE_KIND = Move_Kind.DSCH;
                                break;
                            case "DECK_ID":
                                Str = Sht.getCell(j, i).Contents.Trim();
                                if (Str == "U") oWQ.ABOVE_BELOW = Above_Below.A;
                                else if (Str == "D") oWQ.ABOVE_BELOW = Above_Below.B;
                                break;
                            case "SHIP_MACH_NO":
                                oWQ.QC_ID = Sht.getCell(j, i).Contents;
                                break;
                            case "SHIP_NO":
                                oWQ.SHIP_NO = Sht.getCell(j, i).Contents;
                                break;
                            case "BAY_NO":
                                oWQ.VESSEL_BAY = Sht.getCell(j, i).Contents;
                                if (oWQ.VESSEL_BAY.IndexOf("/") > 0)
                                {
                                    Str = oWQ.VESSEL_BAY.Substring(0, oWQ.VESSEL_BAY.IndexOf("/"));
                                    oWQ.VESSEL_BAY = (Convert.ToInt32(Str) + 1).ToString();
                                }
                                break;
                            case "SHIP_WORK_SEQ":
                                oWQ.WQ_SEQ = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                        }
                    }
                    oWQ.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                    lWQs.Add(oWQ);
                }
            }

            if (bRet)
            {
                if (lWQs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Work Queue Loaded");
                }
                else
                {
                    foreach (STS_WORK_QUEUE_STATUS obj in lWQs)
                    {
                        this.oSimDataStore.dWorkQueues.Add(obj.WORK_QUEUE, obj);
                        oWQ = new STS_WORK_QUEUE_STATUS();
                        oWQ.Copy(obj);
                        oWQ.CONFIGURATION = Configuration.CONFIGURED;
                        this.oSimDataStore.dViewWorkQueues.Add(oWQ.WORK_QUEUE, oWQ);
                    }

                    this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
                        {
                            eProjectType = StatusEnums.ProjectType.Create,
                            oPPTInfoFrame = new ProjectPackageToInfoFrame()
                            {
                                lWQs = this.oSimDataStore.dViewWorkQueues.Values.ToList()
                            }
                        });
                }
            }

            return bRet;
        }

        // WorkInstruction，进字典，无图像
        private bool LoadWorkInstructions(string SheetName)
        {
            bool bRet = true;
            Sheet Sht;
            WORK_INSTRUCTION_STATUS oWI;
            List<WORK_INSTRUCTION_STATUS> lWIs;
            int IndexCol;
            string FuturePlac = "", QueueType = "";
            string Str = "";

            Sht = this.WkBCase.getSheet(SheetName);
            lWIs = new List<WORK_INSTRUCTION_STATUS>();
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Case Excel Named : " + SheetName);
                return false;
            }

            IndexCol = Sht.findCell("CNTR").Column;

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(IndexCol, i).Type != CellType.EMPTY)
                {
                    oWI = new WORK_INSTRUCTION_STATUS();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "CNTR":
                                oWI.CONTAINER_ID = Sht.getCell(j, i).Contents;
                                break;
                            case "WORK_QUEUE_NO":
                                oWI.WORK_QUEUE = Sht.getCell(j, i).Contents;
                                break;
                            case "SEQ_NO":
                                oWI.ORDER_SEQ = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "TOOL_NO":
                                oWI.VESSEL_ID = Sht.getCell(j, i).Contents;
                                break;
                            case "OPP_CNTR":
                                oWI.LIFT_REFERENCE = Sht.getCell(j, i).Contents;
                                break;
                            case "QUEUE_TYP":
                                QueueType = Sht.getCell(j, i).Contents;
                                break;
                            case "FUTURE_PLAC":
                                FuturePlac = Sht.getCell(j, i).Contents.PadLeft(7, '0');
                                break;
                            case "REC_TIM":
                                Str = Sht.getCell(j, i).Contents;
                                Str = Str.Insert(Str.LastIndexOf("/") + 1, "20");
                                oWI.UPDATED = Convert.ToDateTime(Str);
                                break;
                        }
                    }

                    if (QueueType == "SO")
                    {
                        this.oSimDataStore.dSimContainerInfos[oWI.CONTAINER_ID].StowLoc = FuturePlac;
                        this.oSimDataStore.dSimContainerInfos[oWI.CONTAINER_ID].StowBay = Convert.ToInt32(FuturePlac.Substring(0, 3));
                        this.oSimDataStore.dSimContainerInfos[oWI.CONTAINER_ID].StowRow = Convert.ToInt32(FuturePlac.Substring(3, 2));
                        this.oSimDataStore.dSimContainerInfos[oWI.CONTAINER_ID].StowTier = Convert.ToInt32(FuturePlac.Substring(5, 2));
                    }

                    oWI.CONTAINER_LENGTH_CM = 0;
                    oWI.CONTAINER_HEIGHT_CM = 0;
                    oWI.CONTAINER_QC_LOC_TYPE = Container_STS_LOC_Type.NULL;
                    oWI.CONTAINER_NEXT_QC_LOC_TYPE = Container_STS_LOC_Type.NULL;
                    if (oWI.MOVE_KIND == Move_Kind.LOAD) 
                        oWI.DESTINATION_CARRIER_SLOT = FuturePlac;
                    if (this.oSimDataStore.dSimContainerInfos.ContainsKey(oWI.CONTAINER_ID))
                    {
                        oWI.CONTAINER_ISO = this.oSimDataStore.dSimContainerInfos[oWI.CONTAINER_ID].ISO;
                        oWI.CONTAINER_LENGTH_CM = this.oSimDataStore.dISORefs[oWI.CONTAINER_ISO].ContainerLengthCM;
                        oWI.CONTAINER_HEIGHT_CM = this.oSimDataStore.dISORefs[oWI.CONTAINER_ISO].ContainerHeightCM;
                        oWI.CONTAINER_WEIGHT_KG = this.oSimDataStore.dSimContainerInfos[oWI.CONTAINER_ID].GrossWeight;
                        oWI.MOVE_KIND = this.oSimDataStore.dSimContainerInfos[oWI.CONTAINER_ID].MoveKind;
                        if (this.oSimDataStore.dSimContainerInfos[oWI.CONTAINER_ID].MoveKind == Move_Kind.LOAD)
                        {
                            oWI.CONTAINER_QC_LOC_TYPE = Container_STS_LOC_Type.YARD;
                            oWI.CONTAINER_NEXT_QC_LOC_TYPE = Container_STS_LOC_Type.AGV;
                        }
                        else if (this.oSimDataStore.dSimContainerInfos[oWI.CONTAINER_ID].MoveKind == Move_Kind.DSCH)
                        {
                            oWI.CONTAINER_QC_LOC_TYPE = Container_STS_LOC_Type.VESSEL;
                            oWI.CONTAINER_NEXT_QC_LOC_TYPE = Container_STS_LOC_Type.AGV;
                        }
                    }
                    oWI.IS_TANK = Is_Tank.NO;
                    oWI.RACK_SUITABLE = Rack_Suitable.YES;
                    oWI.HOLD = Hold.NONE;
                    oWI.POINT_OF_WORK = this.oSimDataStore.dWorkQueues[oWI.WORK_QUEUE].QC_ID;
                    oWI.MOVE_STAGE = Move_Stage.NULL;              // 发出后改为PLANNED
                    oWI.CONTAINER_STOW_FACTOR = "";
                    oWI.CONTAINER_WEIGHT_MARGIN_KG = 0;
                    oWI.CARRY_REFERENCE = 0;
                    oWI.RELATIVE_POS_ON_CARRIER = "";
                    oWI.ORIGIN_CARRIER_SLOT = "";
                    oWI.DESTINATION_CARRIER_SLOT = "";
                    //oWI.DOOR_DIRECTION = ZECS.Schedule.DBDefine.Schedule.Orientation.NULL;
                    oWI.OFFSET_TO_BAY_CENTER_CM = 0;
                    oWI.HAS_TOP_RAILS = Has.NULL;
                    oWI.HAS_BOTTOM_RAILS = Has.NULL;
                    oWI.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);

                    //问题儿童
                    oWI.PHYSICAL_PREDECESSOR = "";
                    oWI.LOGICAL_PREDECESSOR = "";

                    lWIs.Add(oWI);
                }
            }

            if (bRet)
            {
                if (lWIs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Work Instruction Loaded");
                }
                else
                {
                    foreach (WORK_INSTRUCTION_STATUS obj in lWIs)
                    {
                        this.oSimDataStore.dWorkInstructions.Add(obj.CONTAINER_ID, obj);
                        oWI = new WORK_INSTRUCTION_STATUS();
                        oWI.Copy(obj);
                        this.oSimDataStore.dViewWorkInstructions.Add(oWI.CONTAINER_ID, oWI);
                    }

                    this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
                        {
                            eProjectType = StatusEnums.ProjectType.Create,
                            oPPTInfoFrame = new ProjectPackageToInfoFrame()
                            {
                                lWIs = this.oSimDataStore.dViewWorkInstructions.Values.ToList()
                            }
                        });
                }
            }

            return bRet;
        }

        // 标记空 WQ
        private void SignEmptyWorkQueues()
        {
            foreach (string sWQ in this.oSimDataStore.dWorkQueues.Keys)
            {
                if (this.oSimDataStore.dWorkInstructions.Values.Count(u => u.WORK_QUEUE == sWQ) == 0) this.oSimDataStore.dWorkQueues[sWQ].WQ_STATUS = WQ_Status.EMPTY;
            }
        }

        // 加载计划组 PlanGroup，进字典，无图像
        private bool LoadPlanGroups(string SheetName)
        {
            bool bRet = true;
            Sheet Sht;
            PlanGroup oPG;
            List<PlanGroup> lPGs = new List<PlanGroup>();
            ISORef oISORef;
            int IndexCol;
            string Str;

            Sht = this.WkBCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Case Excel Named : " + SheetName);
                return false;
            }

            IndexCol = Sht.findCell("PLAN_GROUP_NO").Column;

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(IndexCol, i).Type != CellType.EMPTY)
                {
                    oPG = new PlanGroup();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "PLAN_GROUP_NO":
                                oPG.Name = Sht.getCell(j, i).Contents;
                                break;
                            case "CNTR_CLASS":
                                Str = Sht.getCell(j, i).Contents;
                                if (Str == "I")
                                    oPG.MoveKind = Move_Kind.DSCH;
                                break;
                            case "CNTR_SIZ_COD":
                                Enum.TryParse<StatusEnums.ContSize>(Sht.getCell(j, i).Contents.Trim(), out oPG.eContSize);
                                break;
                            case "CNTR_ISO":
                                Enum.TryParse<StatusEnums.ContType>(Sht.getCell(j, i).Contents.Trim(), out oPG.eContType);
                                break;
                            case "TOTAL_NUM":
                                oPG.TotalNum = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                        }
                    }

                    oISORef = this.oSimDataStore.dISORefs.Values.FirstOrDefault(u => u.eContSize == oPG.eContSize && u.eContType == oPG.eContType);

                    if (oISORef != null)
                        oPG.ISO = oISORef.ISO;

                    if (!oPG.CheckDefinitionCompleteness())
                    {
                        bRet = false;
                        Logger.Simulate.Error("Load PlanGroup in Row : " + i.ToString() + " Failed");
                        break;
                    }
                    else
                        lPGs.Add(oPG);
                }
            }

            if (bRet)
            {
                if (lPGs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Plan Group Loaded");
                }
                else
                {
                    foreach (PlanGroup obj in lPGs)
                    {
                        this.oSimDataStore.dPlanGroups.Add(obj.Name, obj);
                    }
                }
            }

            return bRet;
        }

        // 加载计划范围 PlanRange，进字典，无图像
        private bool LoadPlanRanges(string SheetName)
        {
            bool bRet = true;
            Sheet Sht;
            PlanRange oPR;
            PlanGroup oPG;
            List<PlanRange> lPRs = new List<PlanRange>();
            int IndexCol;

            Sht = this.WkBCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Case Excel Named : " + SheetName);
                return false;
            }

            IndexCol = Sht.findCell("PLAN_GROUP_NO").Column;

            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(IndexCol, i).Type != CellType.EMPTY)
                {
                    oPR = new PlanRange();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "PLAN_GROUP_NO":
                                oPR.PlanGroupName = Sht.getCell(j, i).Contents;
                                break;
                            case "SEQ_NO":
                                oPR.SeqNo = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "BEG_PLAC":
                                oPR.BeginPlac = Sht.getCell(j, i).Contents;
                                break;
                            case "END_PLAC":
                                oPR.EndPlac = Sht.getCell(j, i).Contents;
                                break;
                            case "AREA_NO":
                                oPR.BlockName = Sht.getCell(j, i).Contents;
                                break;
                            case "BEG_BAY_NO":
                                oPR.BeginBay = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "END_BAY_NO":
                                oPR.EndBay = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "BEG_ROW_NO":
                                oPR.BeginRow = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "END_ROW_NO":
                                oPR.EndRow = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "BEG_TIER_NO":
                                oPR.BeginTier = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "END_TIER_NO":
                                oPR.EndTier = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "PRI_NO":
                                oPR.PriNo = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "TOTAL_NUM":
                                oPR.TotalNum = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                        }
                    }
                    oPR.Name = oPR.PlanGroupName + "_" + oPR.SeqNo.ToString();
                    lPRs.Add(oPR);
                }
            }

            if (bRet)
            {
                if (lPRs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Plan Range Loaded");
                }
                else
                {
                    foreach (PlanRange obj in lPRs)
                    {
                        oPG = this.oSimDataStore.dPlanGroups.Values.FirstOrDefault<PlanGroup>(u => u.Name == obj.PlanGroupName);
                        if (oPG != null && !oPG.lPlanRangeNames.Contains(obj.Name))
                            oPG.lPlanRangeNames.Add(obj.Name);
                        this.oSimDataStore.dPlanRanges.Add(obj.Name, obj);
                    }
                }
            }

            return bRet;
        }

        // 加载计划位置，进字典，无图像
        private bool LoadPlanPlacs(string SheetName)
        {
            bool bRet = true;
            Sheet Sht;
            PlanGroup oPG;
            PlanRange oPR;
            PlanPlac oPP;
            List<PlanPlac> lPPs = new List<PlanPlac>();
            int IndexCol;

            Sht = this.WkBCase.getSheet(SheetName);
            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Case Excel Named : " + SheetName);
                return false;
            }

            IndexCol = Sht.findCell("PLAN_GROUP_NO").Column;


            for (int i = 1; i < Sht.Rows; i++)
            {
                if (Sht.getCell(IndexCol, i).Type != CellType.EMPTY)
                {
                    oPP = new PlanPlac();
                    for (int j = 0; j < Sht.Columns; j++)
                    {
                        switch (Sht.getCell(j, 0).Contents)
                        {
                            case "PLAN_GROUP_NO":
                                oPP.PlanGroupName = Sht.getCell(j, i).Contents;
                                break;
                            case "SEQ_NO":
                                oPP.SeqNo = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "ORDER_ID":
                                oPP.OrderID = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "CNTR_PLAC":
                                oPP.Plac = Sht.getCell(j, i).Contents;
                                break;
                            case "CY_AREA_NO":
                                oPP.BlockName = Sht.getCell(j, i).Contents;
                                break;
                            case "CY_BAY_NO":
                                oPP.Bay = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "CY_ROW_NO":
                                oPP.Row = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                            case "CY_TIER_NO":
                                oPP.Tier = Convert.ToInt32(Sht.getCell(j, i).Value);
                                break;
                        }
                    }
                    oPP.Name = oPP.PlanGroupName + "_" + oPP.SeqNo.ToString() + "_" + oPP.OrderID.ToString();
                    oPP.PlanRangeName = oPP.PlanGroupName + "_" + oPP.SeqNo.ToString();
                    lPPs.Add(oPP);
                }
            }

            if (bRet)
            {
                if (lPPs.Count == 0)
                {
                    bRet = false;
                    Logger.Simulate.Error("No Plan Plac Loaded");
                }
                else
                {
                    foreach (PlanPlac obj in lPPs)
                    {
                        oPR = this.oSimDataStore.dPlanRanges.Values.FirstOrDefault<PlanRange>(u => u.Name == obj.PlanRangeName);
                        if (oPR != null && !oPR.lPlanPlacNames.Contains(obj.Name))
                        {
                            oPR.lPlanPlacNames.Add(obj.Name);
                            oPR.TotalNum++;
                        }
                        oPG = this.oSimDataStore.dPlanGroups.Values.FirstOrDefault<PlanGroup>(u => u.Name == obj.PlanGroupName);
                        if (oPG != null && !oPG.lPlanPlacNames.Contains(obj.Name))
                        {
                            oPG.lPlanPlacNames.Add(obj.Name);
                            oPG.TotalNum++;
                        }
                        this.oSimDataStore.dPlanPlacs.Add(obj.Name, obj);
                    }
                }
            }

            return bRet;
        }

        // 加载 Index 初始值
        private bool LoadIndexNums()
        {
            StatusEnums.IndexType eIndexType;

            this.oSimDataStore.dIndexNums.Clear();
            foreach (string sE in Enum.GetNames(typeof(StatusEnums.IndexType)))
            {
                if (Enum.TryParse<StatusEnums.IndexType>(sE, out eIndexType))
                {
                    switch (eIndexType)
                    {
                        case StatusEnums.IndexType.JobID:
                            this.oSimDataStore.dIndexNums.Add(eIndexType, 100000);
                            break;
                        case StatusEnums.IndexType.TaskID:
                            this.oSimDataStore.dIndexNums.Add(eIndexType, 6900000);
                            break;
                        case StatusEnums.IndexType.STSOrdComm:
                        case StatusEnums.IndexType.ASCOrdComm:
                        case StatusEnums.IndexType.AGVOrdComm:
                            this.oSimDataStore.dIndexNums.Add(eIndexType, 0);
                            break;
                        default:
                            break;
                    }

                } 
            }

            return true;
        }

        // 初始化 QC_STATUS
        private void GenerateQCStatus(List<QCDT> lQCs)
        {
            STS_STATUS oSS;
            foreach(QCDT oQC in oSimDataStore.dQCs.Values)
            {
                oSS = new STS_STATUS();
                oSS.nAGVCountMin = oQC.nAGVCountMin;
                oSS.nAGVCountMax = oQC.nAGVCountMax;
                oSS.QC_ID = oQC.ID.ToString();
                oSS.nQCPosition = Convert.ToInt32(oQC.BasePoint.X);
                oSS.TECHNICAL_STATUS = Technical_Status.GREEN;
                oSS.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                oSimDataStore.dSTSStatus.Add(oSS.QC_ID, oSS);
            }

            this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Create,
                    oPPTInfoFrame = new ProjectPackageToInfoFrame()
                    {
                        lSTSStatuses = this.oSimDataStore.dSTSStatus.Values.ToList()
                    }
                });
        }

        // 初始化 ASC_STATUS
        private void GenerateASCStatus(List<ASC> lASCs)
        {
            ASC_STATUS oAS;
            foreach (ASC obj in lASCs)
            {
                oAS = new ASC_STATUS();
                oAS.CHE_ID = obj.ID.ToString();
                oAS.TECHNICAL_DETAILS = "";
                oAS.CONTAINER_ID = "";
                oAS.OPERATIONAL_STATUS = Operational_Status.AUTOMATIC;
                oAS.LOCATION = "";
                oAS.TECHNICAL_STATUS = Technical_Status.GREEN;
                oAS.WORK_STATUS = Work_Status.IDLE;
                oAS.ORDER_GKEY = 0;
                oAS.COMMAND_GKEY = 0;
                oAS.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                oSimDataStore.dASCStatus.Add(oAS.CHE_ID, oAS);
            }

            this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Create,
                    oPPTInfoFrame = new ProjectPackageToInfoFrame()
                    {
                        lASCStatuses = this.oSimDataStore.dASCStatus.Values.ToList()
                    }
                });
        }

        // 初始化 AGV_STATUS
        private void GenerateAGVStatus(List<AGV> lAGVs)
        {
            AGV_STATUS oAS;
            foreach (AGV obj in lAGVs)
            {
                oAS = new AGV_STATUS();
                oAS.CHE_ID = obj.ID.ToString();
                oAS.BATTERY_STATE = Battery_State.GREEN;
                oAS.FUEL_TYPE = Fuel_Type.BATTERY;
                oAS.LOCATION_X = Convert.ToInt32(obj.MidPoint.X);
                oAS.LOCATION_Y = Convert.ToInt32(obj.MidPoint.Y);
                oAS.OPERATIONAL_STATUS = Operational_Status.AUTOMATIC;
                oAS.ORIENTATION = 0;
                oAS.RUNNING_HOURS = 0;
                oAS.TECHNICAL_STATUS = Technical_Status.GREEN;
                oAS.TECHNICAL_DETAILS = "";
                oAS.LIFT_CAPABILITY = "YES";
                oAS.LOCATION = obj.CurrLaneID.ToString();
                oAS.NEXT_LOCATION = "";
                oAS.REFERENCE_ID_1 = "";
                oAS.REFERENCE_ID_2 = "";
                oAS.CONTAINER_ID_1 = "";
                oAS.CONTAINER_ID_2 = "";
                oAS.ORDER_GKEY = 0;
                oAS.COMMAND_GKEY = 0;
                oAS.UPDATED = SimStaticParas.SimDtStart.AddSeconds(Simulation.clock);
                this.oSimDataStore.dAGVStatus.Add(oAS.CHE_ID, oAS);
            }

            this.ProjectToInfoFrameEvent.Invoke(this, new ProjectToInfoFrameEventArgs()
                {
                    eProjectType = StatusEnums.ProjectType.Create,
                    oPPTInfoFrame = new ProjectPackageToInfoFrame()
                    {
                        lAGVStatuses = this.oSimDataStore.dAGVStatus.Values.ToList()
                    }
                });
        }

        // 向 WI 补充，包括 CONTAINER_STOW_FACTOR 和 CONTAINER_WEIGHT_MARGIN_KG
        private bool SupplementWI()
        {
            bool bRet = true;

            foreach (string sKey in oSimDataStore.dWorkInstructions.Keys)
            {
                if (oSimDataStore.dWorkInstructions[sKey].MOVE_KIND == Move_Kind.LOAD)
                {
                    if (!oSimDataStore.dSimContainerInfos.ContainsKey(sKey))
                    {
                        bRet = false;
                        break;
                    }
                    oSimDataStore.dWorkInstructions[sKey].CONTAINER_STOW_FACTOR = 
                        oSimDataStore.dSimContainerInfos[sKey].ISO + "/" + oSimDataStore.dSimContainerInfos[sKey].PortOfDisc;
                    oSimDataStore.dWorkInstructions[sKey].CONTAINER_WEIGHT_MARGIN_KG = oSimDataStore.dSimContainerInfos[sKey].GrossWeight;
                }
            }


            return bRet;
        }


        //读取Excel中Task的AGVID
        public uint LoadTaskAGVID(int iRowNum)
        {
            Sheet Sht;
            uint uAGVID;
            Sht = this.WkBExtraCase.getSheet("Task");

            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : Task");
                return 0;
            }


            int iColNum = 0;
            
            uAGVID = Convert.ToUInt32(Sht.getCell(iColNum, iRowNum).Value);

            return uAGVID;
        }


        //读取Excel中Task的LaneID
        public uint LoadTaskLaneID(int iRowNum)
        {

            Sheet Sht;
            uint uLaneID;
            Sht = this.WkBExtraCase.getSheet("Task");

            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : Task");
                return 0;
            }


            int iColNum = 1;

            uLaneID = Convert.ToUInt32(Sht.getCell(iColNum, iRowNum).Value);

            return uLaneID;
        }


        //读取Excel中Task的发生时刻
        public uint LoadTaskTime(int iRowNum)
        {

            Sheet Sht;
            uint uTaskTime;
            Sht = this.WkBExtraCase.getSheet("Task");

            if (Sht == null)
            {
                Logger.Simulate.Error("Cannot Find Sheet in Definition Excel Named : Task");
                return 0;
            }


            int iColNum = 2;

            uTaskTime = Convert.ToUInt32(Sht.getCell(iColNum, iRowNum).Value);

            return uTaskTime;
        }

        //判断读取Excel中Task是否到最后一行
        public bool IsLoadingTaskEnd(int iRowNum)
        {
            Sheet Sht;
            Sht = this.WkBExtraCase.getSheet("Task");

            return iRowNum == Sht.Rows || Sht.getCell(0, iRowNum).Type == CellType.EMPTY;

        }


    }
}