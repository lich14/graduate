using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;

namespace ZECS.Schedule.Define
{
    public class ThreadBase
    {

        protected Thread m_thread = null;
        protected bool m_bStarted = false;
        protected int m_nInterval = 200;

        public int Interval
        {
            get { return m_nInterval; }
            set { m_nInterval = value; }
        }
        public virtual bool Start(Object param)
        {
            if (m_bStarted)
                return true;
            m_thread = new Thread(new ParameterizedThreadStart(WorkFunc));
            m_bStarted = true;
            m_thread.Start(param);

            return true;

        }

        public virtual void Stop()
        {
            if (!m_bStarted)
                return;

            m_bStarted = false;
            if (m_thread!=null)
                m_thread.Join();
        }

        public virtual bool Pause()
        {
            m_bStarted = false;
            return true;
        }

        public virtual bool Resume()
        {
            Start(null);
            return true;
        }

        public virtual void WorkFunc(Object param)
        {
            while (m_bStarted)
            {

                //try
                {
                    ThreadDeal(param);
                    Thread.Sleep(m_nInterval);
                }
                //catch (Exception ex)
                //{
                //    string msg = "Message:" + ex.Message + " TargetSite:" + ex.TargetSite + " Source:" + ex.Source + " StackTrace:" + ex.StackTrace;

                //}
            }
        }

        public virtual void ThreadDeal(Object param)
        {
 
        }

        public virtual bool IsAlive()
        {
            if (m_thread == null) return false;

            return m_thread.IsAlive;
        }
    }
}
