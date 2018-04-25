using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SSWPF.Define;
using ZECS.Schedule.DBDefine.Schedule;
using ZECS.Schedule.Define;

namespace SSWPF.SimManagers
{
    public class SimASCManager
    {
        public event EventHandler<ProjectToViewFrameEventArgs> ProjectToViewFrameEvent;
        public event EventHandler<ProjectToInfoFrameEventArgs> ProjectToInfoFrameEvent;

        private SimDataStore oSimDataStore;
        public bool IsInited;

        public SimASCManager()
        {
        }

        public SimASCManager(SimDataStore oSimDataStore)
            : this()
        {
            this.oSimDataStore = oSimDataStore;
        }

        public void StartASCs()
        {
            // List<ASC_Order> lNewASCOrds = this.oSimDataStore.dASCOrders.Values.Where(u => this.oSimDataStore.dASCCommands.Values.ToList().Exists(v => ));
        }

        public bool Init()
        {
            if (this.oSimDataStore == null)
            {
                Logger.Simulate.Error("SimASCManager: Null SimDataStore!");
                return false;
            }

            if (this.ProjectToViewFrameEvent == null || this.ProjectToInfoFrameEvent == null)
            {
                Logger.Simulate.Error("SimASCManager: Null Event Listener!");
                return false;
            }

            if (this.oSimDataStore.dASCs == null || this.oSimDataStore.dASCs.Count == 0)
            {
                Logger.Simulate.Error("SimASCManager: No ASC Existed!");
                return false;
            }

            this.IsInited = true;
            return true;
        }



        public void MoveASCsInStep(int intv)
        {


        }

        public bool LockWorkPoints()
        {

            return false;
        }



    }
}
