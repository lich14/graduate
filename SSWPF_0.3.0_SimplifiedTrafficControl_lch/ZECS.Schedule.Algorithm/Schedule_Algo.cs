using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZECS.Schedule.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Algorithm;
using ZECS.Schedule.Define.DBDefine.Schedule;

namespace ZECS.Schedule.Algorithm
{
    public class STSWorkBalanceData
    {
        public UInt32 m_nSTSID;
        public UInt32 m_nMinAGVCount;
        public UInt32 m_nMaxAGVCount;
        public UInt32 m_nNeedTaskCount;
    }


    public class Schedule_Algo
    {
        private static Schedule_Algo s_instance;
        public static Schedule_Algo Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new Schedule_Algo();
                }
                return s_instance;
            }
        }


        public bool GeneralSchedule(ref DBData_Schedule dbData_Schedule,
                                            ref PartialOrderGraph jobsOrderedDecisionTable)
        {
            bool bRet = false;

            bRet = Schedule_Model_2(ref dbData_Schedule, ref  jobsOrderedDecisionTable);

            //bRet = ScheduleModel2.Instance.Schedule_Mode2(dbData_Schedule, ref jobsOrderedDecisionTable);
            
            return bRet;
        }

        protected bool Schedule_Model_1(ref DBData_Schedule dbData_Schedule,
                                              ref OrderedDecisionTable jobsOrderedDecisionTable)
        {
            bool bRet = false;

            bRet = GeneralSchedule_Model_1.Instance.Schedule_Mode1(dbData_Schedule.m_DBData_TOS, ref jobsOrderedDecisionTable);
             
            return bRet;
        }

        protected bool Schedule_Model_2(ref DBData_Schedule dbData_Schedule,
                                         ref PartialOrderGraph jobsOrderedDecisionTable)
        {
            bool bRet = false;
            bRet = GeneralSchedule_Model_2.Instance.Schedule_Mode2(dbData_Schedule, ref jobsOrderedDecisionTable);
            //bRet = ScheduleModel2.Instance.Schedule_Mode2(dbData_Schedule, ref jobsOrderedDecisionTable);
            return bRet;
        }
    }
    
}
