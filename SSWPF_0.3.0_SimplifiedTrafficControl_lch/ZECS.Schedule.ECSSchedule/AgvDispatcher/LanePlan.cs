using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using ZECS.Schedule.Algorithm.Utilities;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.DBDefine.YardMap;
using ZECS.Schedule.Define;
using ZECS.Schedule.DB;

namespace ZECS.Schedule.ECSSchedule
{
    public enum ReserveLaneKind
    {
        None,
        Any,
        Mate,
        NonMate,
    }

    public class LanePlan
    {
        private readonly List<LaneInfoEx> m_listLane = new List<LaneInfoEx>();
        private readonly Dictionary<LaneInfoEx, List<string>> m_dictLane2AgvOrderId = new Dictionary<LaneInfoEx, List<string>>();

        /// <summary>
        /// 初始化车道信息
        /// </summary>
        public void Init()
        {
            Hashtable htLane = null;
            try
            {
                htLane = DataAccess.LoadLane();
            }
            catch (Exception ex)
            {
                Logger.ECSSchedule.Error("[Lane] DataAccess.LoadLane: ", ex);
                throw new Exception("DataAccess.LoadLane: ", ex);
            }

            m_listLane.Clear();
            ClearLaneInUsing();

            if (htLane != null)
            {
                foreach (DictionaryEntry de in htLane)
                {
                    LaneInfo li = (DBDefine.YardMap.LaneInfo)de.Value;
                    LaneInfoEx liEx = new LaneInfoEx(li);
                    m_listLane.Add(liEx);
                }
            }

            // log
            //var listBlockLanes = m_listLane.FindAll(x => x.LaneType == LANE_TYPE.LT_BLOCK_EXCHANGE);
            //for (int i = 0; i < listBlockLanes.Count; ++i)
            //{
            //    Logger.ECSScheduleDebug.Debug(string.Format("[Lane] Load Block Lane {0}: {1}", i, listBlockLanes[i]));
            //}
        }

        /// <summary>
        /// 根据ID获取Lane
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public LaneInfoEx GetLane(ushort ID)
        {
            return m_listLane.Find(x => x.ID == ID);
        }

        /// <summary>
        /// GetAgvOccupiedLane
        /// </summary>
        /// <param name="strAgvId"></param>
        /// <returns></returns>
        public LaneInfoEx GetAgvOccupiedLane(string strAgvId)
        {
            var li = m_listLane.Find(x =>
                        x.OccupyAgvId.ToString() == strAgvId
                        && x.LaneStatus == LaneStatus.OCCUPIED);

            return li;
        }

        /// <summary>
        /// 查找车道
        /// </summary>
        /// <param name="block"></param>
        /// <param name="bayType"></param>
        /// <param name="laneNo"></param>
        /// <returns></returns>
        public LaneInfoEx GetLane(string block, string bayType, string laneNo)
        {
            LANE_TYPE? lt = BayType2LaneType(bayType);
            if (lt == null)
                return null;

            var li = m_listLane.FirstOrDefault(
                        x => x.BlockOrQcId == block
                          && x.LaneType == lt
                          && x.GetLaneNo().ToString() == laneNo);

            return li;
        }

        /// <summary>
        /// 查找车道列表
        /// </summary>
        /// <param name="blockOrQcId"></param>
        /// <param name="bayType"></param>
        /// <returns></returns>
        public List<LaneInfoEx> GetTpLanes(string blockOrQcId, string bayType)
        {
            LANE_TYPE? lt = BayType2LaneType(bayType);
            if (lt == null)
                return new List<LaneInfoEx>();

            return m_listLane.FindAll(x => x.BlockOrQcId == blockOrQcId && x.LaneType == lt);
        }

        /// <summary>
        /// 查找PB车道
        /// </summary>
        /// <param name="strQcId"></param>
        /// <param name="isIn"></param>
        /// <returns></returns>
        private List<LaneInfoEx> GetPbLanes(string strQcId, bool isIn)
        {
            var listPbNo = VmsAlgorithm.Instance.GetPBList(Utility.GetNumberFromString(strQcId), isIn);
            if (listPbNo == null)
                return null;

            return listPbNo.Select(GetLane).Where(li => li != null).ToList();
        }

        /// <summary>
        /// 查找空闲PB车道
        /// </summary>
        /// <param name="strPrefferQcId"></param>
        /// <param name="isIn"></param>
        /// <returns></returns>
        public LaneInfoEx GetAnyIdlePbLanes(string strPrefferQcId, bool isIn)
        {
            if (!string.IsNullOrWhiteSpace(strPrefferQcId))
            {
                var listPbLane = GetPbLanes(strPrefferQcId, isIn);
                if (listPbLane != null)
                {
                    var listIdlePbLane = GetIdleLaneList(listPbLane);

                    return listIdlePbLane.Count > 0 ? listIdlePbLane[0] : null;
                }
            }

            LaneInfoEx li;

            var listIdleLane = GetIdleLaneList(m_listLane);
            li = listIdleLane.FirstOrDefault(x =>
                    x.LaneType == LANE_TYPE.LT_QC_BUFFER
                    && (isIn ? x.Attr == LaneAttribute.STS_PB_ONLY_IN : x.Attr == LaneAttribute.STS_PB_ONLY_OUT));

            if (li != null)
                return li;

            li = listIdleLane.FirstOrDefault(x =>
                    x.LaneType == LANE_TYPE.LT_QC_BUFFER
                    && x.Attr == LaneAttribute.STS_PB_IN_OUT
                );

            return li;
        }

        /// <summary>
        /// 查找车道
        /// </summary>
        /// <param name="blockOrQcId"></param>
        /// <param name="bayType"></param>
        /// <returns></returns>
        public LaneInfoEx GetOneTpLane(string blockOrQcId, string bayType)
        {
            var listLaneInfo = GetTpLanes(blockOrQcId, bayType);
            return listLaneInfo != null && listLaneInfo.Count > 0 ? listLaneInfo[0] : null;
        }

        /// <summary>
        /// 为去堆场的任务指派车道
        /// </summary>
        /// <param name="agvStatus"></param>
        /// <param name="blockOrQcId"></param>
        /// <param name="bayType"></param>
        /// <param name="canAssignToMateLane"></param>
        /// <returns></returns>
        public LaneInfoEx PreAssignLane(AGV_STATUS agvStatus, string blockOrQcId, string bayType,
            bool canAssignToMateLane, ReserveLaneKind reserveLaneKind = ReserveLaneKind.None)
        {
            if (agvStatus == null)
            {
                return null;
            }

            Logger.ECSScheduleDebug.Debug(string.Format("[Lane] PreAssignLane Start: agv={0}, block={1}, bay type={2}, canAssignToMateLane={3}, reserveLaneKind={4}",
                      agvStatus.CHE_ID, blockOrQcId, bayType, canAssignToMateLane, reserveLaneKind));

            LaneInfoEx li = GetPreAssignLane(agvStatus, blockOrQcId, bayType, canAssignToMateLane, reserveLaneKind);

            Logger.ECSScheduleDebug.Debug(string.Format("[Lane] PreAssignLane {0}: agv={1}, block={2}, bay type={3}, lane={4}",
                      li != null ? "Success" : "Fail", agvStatus.CHE_ID, blockOrQcId, bayType, li == null ? "null" : li.ToString()));

            return li;
        }

        private LaneInfoEx GetPreAssignLane(AGV_STATUS agvStatus, string blockOrQcId, string bayType,
            bool canAssignToMateLane, ReserveLaneKind reserveLaneKind = ReserveLaneKind.None)
        {
            if (Helper.GetEnum(bayType, BayType.UnKnown) != BayType.WS)
            {
                return null;
            }

            var listBlockLanes = GetTpLanes(blockOrQcId, bayType);

            if (listBlockLanes == null || listBlockLanes.Count <= 0)
            {
                return null;
            }

            LogLanes(listBlockLanes);

            var listBlockLanesNotReserved = GetBlockLanesNotReserved(listBlockLanes, reserveLaneKind);

            LaneInfoEx liPreAssign = null;

            // 如果AGV在车道上，优先分配当前所占车道
            liPreAssign = listBlockLanesNotReserved.Find(x =>
                x.OccupyAgvId.ToString() == agvStatus.CHE_ID
                && x.LaneStatus == LaneStatus.OCCUPIED
                && x.LaneType == LANE_TYPE.LT_BLOCK_EXCHANGE
                && !IsLaneInUsing(x)
                && (canAssignToMateLane || !x.IsMateLane()));

            if (liPreAssign != null)
            {
                return liPreAssign;
            }

            List<LaneInfoEx> listIdleBlockLanes = GetIdleLaneList(listBlockLanesNotReserved);

            var listAssignableLanes = listIdleBlockLanes.FindAll(x =>canAssignToMateLane || !x.IsMateLane());
            if (listAssignableLanes.Count <= 0)
            {
                return null;
            }

            bool isAgvNeedCharge = (agvStatus.BATTERY_STATE != Battery_State.GREEN);
            bool isMatePreferred = true;

            if (isMatePreferred
                && (liPreAssign = listAssignableLanes.Find(x => x.IsMateLane())) != null)
            {
                return liPreAssign;
            }

            if (isAgvNeedCharge)
            {
                liPreAssign = listAssignableLanes.Find(x => x.IsChargerLane());

                if (liPreAssign != null)
                    return liPreAssign;
            }

            return listAssignableLanes[0];
        }

        private List<LaneInfoEx> GetBlockLanesNotReserved(List<LaneInfoEx> listBlockLanes, ReserveLaneKind reserveLaneKind)
        {
            List<LaneInfoEx> listBlockLanesNotReserved = new List<LaneInfoEx>();

            if (reserveLaneKind == ReserveLaneKind.None)
            {
                listBlockLanesNotReserved.AddRange(listBlockLanes);

                return listBlockLanesNotReserved;
            }

            bool bReserved = false;
            for (int i = 0; i < listBlockLanes.Count; ++i)
            {
                if (!bReserved)
                {
                    if (reserveLaneKind == ReserveLaneKind.Any && i == 0
                        || reserveLaneKind == ReserveLaneKind.NonMate && !listBlockLanes[i].IsMateLane()
                        || reserveLaneKind == ReserveLaneKind.Mate && listBlockLanes[i].IsMateLane())
                    {
                        bReserved = true;
                        continue;
                    }
                }

                listBlockLanesNotReserved.Add(listBlockLanes[i]);
            }

            return listBlockLanesNotReserved;
        }

        public void AddLaneInUsing(LaneInfoEx li, string strAgvOrderId)
        {
            if (li == null || string.IsNullOrWhiteSpace(strAgvOrderId))
                return;
            
            if (!m_dictLane2AgvOrderId.ContainsKey(li))
            {
                m_dictLane2AgvOrderId[li] = new List<string>();
            }
            m_dictLane2AgvOrderId[li].Add(strAgvOrderId);
        }

        public bool IsLaneInUsing(LaneInfoEx li)
        {
            return m_dictLane2AgvOrderId.Keys.FirstOrDefault(x => x.ID == li.ID) != null;
        }

        private void ClearLaneInUsing()
        {
            m_dictLane2AgvOrderId.Clear();
        }

        public List<string> GetOrderIdOfLaneInUsing(LaneInfoEx li)
        {
            return m_dictLane2AgvOrderId
                .Where(kvp => kvp.Key.ID == li.ID)
                .SelectMany(kvp => kvp.Value)
                .Distinct()
                .ToList();
        }

        public List<string> GetListOrderOfLaneInUsing(string block, string bayType)
        {
            LANE_TYPE? lt = BayType2LaneType(bayType);
            if (lt == null)
                return new List<string>();

            return m_dictLane2AgvOrderId
                .Where(kvp => kvp.Key.BlockOrQcId == block && kvp.Key.LaneType == lt && kvp.Key.LaneStatus != LaneStatus.DISABLED)
                .SelectMany(kvp => kvp.Value)
                .Distinct()
                .ToList();
        } 

        public static LANE_TYPE? BayType2LaneType(string bayType)
        {
            LANE_TYPE lt;
            if (bayType == "WS")
                lt = LANE_TYPE.LT_BLOCK_EXCHANGE;
            else if (bayType == "QC" || bayType == "STS")
                lt = LANE_TYPE.LT_QC_WORKLANE;
            else if (bayType == "PB")
                lt = LANE_TYPE.LT_QC_BUFFER;
            else
                return null;

            return lt;
        }

        public List<LaneInfoEx> GetIdleLaneList(List<LaneInfoEx> listLane)
        {
            List<LaneInfoEx> listIdleLanes = new List<LaneInfoEx>();

            foreach (var li in listLane)
            {
                if (li.LaneStatus == LaneStatus.IDLE
                    && !IsLaneInUsing(li))
                {
                    listIdleLanes.Add(li);
                }
            }

            return listIdleLanes;
        }

        /// <summary>
        /// Log
        /// </summary>
        /// <param name="listLane"></param>
        private void LogLanes(List<LaneInfoEx> listLane)
        {
            if (listLane == null)
                return;

            for (int i = 0; i < listLane.Count; ++i)
            {
                Logger.ECSScheduleDebug.Debug(string.Format("[Lane] lane {0} of block {1}: {2}, assigned={3}",
                    i, listLane[i].BlockOrQcId, listLane[i], IsLaneInUsing(listLane[i])));
            }
        }
    }
}
