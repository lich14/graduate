using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Define;

namespace ZECS.Schedule.ECSSchedule
{
    public class LaneCoordinator
    {
        private readonly Dictionary<string, string> m_dictPairedOrders = new Dictionary<string, string>();

        public void Clear()
        {
            m_dictPairedOrders.Clear();
        }

        public void PairOrder(string strAgvOrderId, string strAscOrderId)
        {
            m_dictPairedOrders[strAgvOrderId] = strAscOrderId;
        }

        public void UnpairOrder(string strAgvOrderId)
        {
            if (!string.IsNullOrWhiteSpace(strAgvOrderId) && m_dictPairedOrders.ContainsKey(strAgvOrderId))
            {
                m_dictPairedOrders.Remove(strAgvOrderId);
            }
        }

        public string FindPairedAscOrder(string strAgvOrderId)
        {
            if (m_dictPairedOrders.ContainsKey(strAgvOrderId))
            {
                var kvp = m_dictPairedOrders.FirstOrDefault(x => x.Key == strAgvOrderId);
                return kvp.Value;
            }

            return null;
        }

        public string FindPairedAgvOrder(string strAscOrderId)
        {
            if (m_dictPairedOrders.ContainsValue(strAscOrderId))
            {
                var kvp = m_dictPairedOrders.FirstOrDefault(x => x.Value == strAscOrderId);
                return kvp.Key;
            }

            return null;
        }

        public bool IsPaired(string strAgvOrderId)
        {
            return m_dictPairedOrders.ContainsKey(strAgvOrderId);
        }

        public void LogSnapshot(List<AGV_Order> listAgvOrder, List<ASC_Order> listAscOrder)
        {
            foreach (var kvp in m_dictPairedOrders)
            {
                string strAgvOrderId = kvp.Key;
                string strAscOrderId = kvp.Value;
                AGV_Order agvOrder = listAgvOrder.Find(x => x.ORDER_ID == strAgvOrderId);
                ASC_Order ascOrder = listAscOrder.Find(x => x.ORDER_ID == strAscOrderId);
                string log = string.Format("[ASC] Paired AGV-ASC Order: {0}-{1} ", strAgvOrderId, strAscOrderId);
                Logger.ScheduleSnapshot.Debug(log + "AGV: " + agvOrder);
                Logger.ScheduleSnapshot.Debug(log + "ASC: " + ascOrder);
            }
        }
    }
}
