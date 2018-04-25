using NExcel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace SharpSim
{
	public class Simulation
	{
		public static List<Event> fel = new List<Event>();

		public static double clock;

		public static int replication;

		public List<Event> felStart = new List<Event>();

		private RandomGenerate rndgnrt;

		private int seedNo = 0;

		private bool randomizeSeed;

		public static int replicationNow = 0;

		public Dictionary<string, Event> events = new Dictionary<string, Event>();

		public Dictionary<string, Edge> edges = new Dictionary<string, Edge>();

		//public static SimForm form1;

		public static Thread tSim;

		//internal static BackgroundWorker tOutput;

		public static readonly object threadLock = new object();

		public static bool trackList = true;

		public Simulation(bool trackList, int replication, bool randomizeSeed)
		{
			Simulation.replication = replication;
			this.randomizeSeed = randomizeSeed;
			Simulation.tSim = new Thread(new ThreadStart(this.Run));
			//Simulation.tOutput = new BackgroundWorker();
			//Simulation.tOutput.DoWork += new DoWorkEventHandler(this.Write);
			Simulation.trackList = trackList;
			//Simulation.form1 = new SimForm();
            //if (trackList)
            //{
            //    Simulation.form1.tSimForm.DoWork += new DoWorkEventHandler(Simulation.form1.Write);
            //    Simulation.form1.tSimForm.RunWorkerAsync();
            //}
		}

		public Simulation(bool trackList, int replication, bool randomizeSeed, int seedNo)
		{
			Simulation.replication = replication;
			this.randomizeSeed = randomizeSeed;
			this.seedNo = seedNo;
			Simulation.tSim = new Thread(new ThreadStart(this.Run));
			//Simulation.tOutput = new BackgroundWorker();
			//Simulation.tOutput.DoWork += new DoWorkEventHandler(this.Write);
			Simulation.trackList = trackList;
            //Simulation.form1 = new SimForm();
            //if (trackList)
            //{
            //    Simulation.form1.tSimForm.DoWork += new DoWorkEventHandler(this.Write);
            //    Simulation.form1.tSimForm.RunWorkerAsync();
            //}
		}

		public void ResetSimulation()
		{
			foreach (Event current in this.events.Values)
			{
				Edge.SetAllEdgeConditionsToTrue();
				current.queue.Clear();
			}
		}

		public void CreateEvents(Sheet sheet)
		{
			for (int i = 1; i < sheet.Rows; i++)
			{
				if (sheet.getCell(3, i).Contents == "" || sheet.getCell(3, i).Contents == null)
				{
					Event @event = new Event(sheet.getCell(0, i).Contents, sheet.getCell(1, i).Contents, int.Parse(sheet.getCell(2, i).Contents));
					this.events.Add(@event.no, @event);
				}
				else
				{
					Event @event = new Event(sheet.getCell(0, i).Contents, sheet.getCell(1, i).Contents, int.Parse(sheet.getCell(2, i).Contents), double.Parse(sheet.getCell(3, i).Contents));
					this.events.Add(@event.no, @event);
				}
			}
		}

		public void CreateEvents(DataTable table)
		{
			foreach (DataRow dataRow in table.Rows)
			{
				if (dataRow["ExecutionTime"].ToString() != "")
				{
					Event @event = new Event(dataRow["No"].ToString(), dataRow["Name"].ToString(), int.Parse(dataRow["Priority"].ToString()), double.Parse(dataRow["ExecutionTime"].ToString()));
					this.events.Add(@event.no, @event);
				}
				else
				{
					Event @event = new Event(dataRow["No"].ToString(), dataRow["Name"].ToString(), int.Parse(dataRow["Priority"].ToString()));
					this.events.Add(@event.no, @event);
				}
			}
		}

		public void CreateEdges(Sheet sheet)
		{
			for (int i = 1; i < sheet.Rows; i++)
			{
				Edge edge = new Edge(sheet.getCell(0, i).Contents, this.events[sheet.getCell(8, i).Contents], this.events[sheet.getCell(9, i).Contents]);
				this.edges.Add(edge.name, edge);
				string contents = sheet.getCell(3, i).Contents;
				if (contents == "yes")
				{
					edge.multiplyingEdge = true;
				}
				contents = sheet.getCell(4, i).Contents;
				if (contents != null && contents != "")
				{
					edge.interEventTime = double.Parse(contents);
				}
				contents = sheet.getCell(5, i).Contents;
				if (contents != null && contents != "")
				{
					edge.dist = contents;
				}
				contents = sheet.getCell(6, i).Contents;
				if (contents != null && contents != "")
				{
					edge.param1 = double.Parse(contents);
				}
				contents = sheet.getCell(7, i).Contents;
				if (contents != null && contents != "")
				{
					edge.param2 = double.Parse(contents);
				}
				contents = sheet.getCell(10, i).Contents;
				if (contents == "yes")
				{
					edge.cancellingEdge = true;
				}
			}
		}

		public void CreateEdges(DataTable table)
		{
			foreach (DataRow dataRow in table.Rows)
			{
				Edge edge = new Edge(dataRow["Name"].ToString(), this.events[dataRow["SourceEvent"].ToString()], this.events[dataRow["TargetEvent"].ToString()]);
				this.edges.Add(edge.name, edge);
				if (dataRow["MultiplyingEdge"].ToString() == "yes")
				{
					edge.multiplyingEdge = true;
				}
				if (dataRow["InterEventTime"].ToString() != "")
				{
					edge.interEventTime = double.Parse(dataRow["InterEventTime"].ToString());
				}
				if (dataRow["Distribution"].ToString() != "")
				{
					edge.dist = dataRow["Distribution"].ToString();
				}
				if (dataRow["Param1"].ToString() != "")
				{
					edge.param1 = double.Parse(dataRow["Param1"].ToString());
				}
				if (dataRow["Param2"].ToString() != "")
				{
					edge.param2 = double.Parse(dataRow["Param2"].ToString());
				}
				if (dataRow["CancellingEdge"].ToString() == "yes")
				{
					edge.cancellingEdge = true;
				}
			}
		}

		public void CreateStats(Sheet sheet)
		{
			for (int i = 0; i < sheet.Rows; i++)
			{
				string contents = sheet.getCell(0, i).Contents;
				List<double> value = new List<double>();
				Stats.globalDictionary.Add(contents, value);
				BasicStats value2 = new BasicStats();
				Stats.Dictionary.Add(contents, value2);
			}
		}

		public void CreateStats(DataTable table)
		{
			foreach (DataRow dataRow in table.Rows)
			{
				List<double> value = new List<double>();
				Stats.globalDictionary.Add(dataRow["Name"].ToString(), value);
				BasicStats value2 = new BasicStats();
				Stats.Dictionary.Add(dataRow["Name"].ToString(), value2);
			}
		}

		public void CreateStats(string data)
		{
			List<double> value = new List<double>();
			Stats.globalDictionary.Add(data, value);
			BasicStats value2 = new BasicStats();
			Stats.Dictionary.Add(data, value2);
		}

		public void StartSimulationThread()
		{
			Simulation.tSim.Start();
		}

		public void Run()
		{
			Stats.stats.Clear();
			Simulation.replicationNow = 0;
			for (int i = 0; i < Simulation.fel.Count<Event>(); i++)
			{
				this.felStart.Add(Simulation.fel[i]);
			}
			for (int i = 0; i < Simulation.replication; i++)
			{
				if (i == 0)
				{
					if (this.randomizeSeed)
					{
						this.rndgnrt = new RandomGenerate();
					}
					else
					{
						this.rndgnrt = new RandomGenerate(this.seedNo);
					}
				}
				else if (this.randomizeSeed)
				{
					this.rndgnrt = new RandomGenerate();
				}
				Simulation.replicationNow++;
				this.ResetSimulation();
				Simulation.clock = 0.0;
				this.RunEngine();
				Thread.Sleep(0);
			}
			if (Simulation.trackList)
			{
				//Simulation.form1.terminated = true;
			}
			Stats.StatsComputations();
			//Simulation.tOutput.RunWorkerAsync();
			this.felStart.Clear();
		}

		private void RunEngine()
		{
			while (Simulation.fel.Count<Event>() > 0)
			{
				lock (Simulation.threadLock)
				{
					if (Simulation.fel[0].name == "Terminate" || Simulation.fel[0].name == "terminate")
					{
						this.ExecuteEvent(Simulation.fel[0]);
						Simulation.fel.Clear();
						for (int i = 0; i < this.felStart.Count<Event>(); i++)
						{
							Simulation.fel.Add(this.felStart[i]);
						}
						foreach (Event current in Event.events)
						{
							current.queue.Clear();
						}
						break;
					}
					Simulation.clock = Simulation.fel[0].executionTime;
					this.ExecuteEvent(Simulation.fel[0]);
					Simulation.fel.RemoveAt(0);
				}
				Thread.Sleep(0);
			}
			Stats.ResetDictionary();
		}

        //public void Write(object sender, DoWorkEventArgs e)
        //{
        //    int counter = 0;
        //    MethodInvoker method = delegate
        //    {
        //        RichTextBox expr_0B = Simulation.form1.richTextBox2;
        //        expr_0B.Text = expr_0B.Text + DateTime.Now.TimeOfDay + " ";
        //        RichTextBox expr_3D = Simulation.form1.richTextBox2;
        //        expr_3D.Text += "simulation completed.\n";
        //        RichTextBox expr_5D = Simulation.form1.richTextBox2;
        //        expr_5D.Text += "statistics    : Mean :  Conf.Interval: \n";
        //        RichTextBox expr_7D = Simulation.form1.richTextBox2;
        //        expr_7D.Text += "--------------------- ------------  ---------------- \n";
        //        foreach (KeyValuePair<string, BasicStats> current in Stats.stats)
        //        {
        //            RichTextBox expr_B7 = Simulation.form1.richTextBox2;
        //            string text = expr_B7.Text;
        //            expr_B7.Text = string.Concat(new string[]
        //            {
        //                text,
        //                current.Key.ToString(),
        //                "  ",
        //                string.Format("{0: 0.####}", current.Value.mean),
        //                "  ",
        //                string.Format("{0: 0.####}", current.Value.confidenceInterval),
        //                "\n"
        //            });
        //            counter++;
        //        }
        //    };
        //    Simulation.form1.BeginInvoke(method);
        //    Thread.Sleep(100);
        //    MessageBox.Show("This is the end of simulation.\nCheck results!!!", "Simulation ended.", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
        //}

		public void ExecuteEvent(Event evnt)
		{
			EventInfoArgs e = new EventInfoArgs(evnt);
			if (evnt.parameter != null)
			{
				Entity parameter = evnt.parameter;
				if (parameter.history.ContainsKey(evnt.no))
				{
					parameter.history.Remove(evnt.no);
					parameter.history.Add(evnt.no, evnt.executionTime);
				}
				else
				{
					parameter.history.Add(evnt.no, evnt.executionTime);
				}
				if (parameter.eventHistory.ContainsKey(evnt.no))
				{
					List<double> list = parameter.eventHistory[evnt.no];
					list.Add(evnt.executionTime);
					parameter.eventHistory[evnt.no] = list;
				}
				else
				{
					List<double> list = new List<double>();
					list.Add(evnt.executionTime);
					parameter.eventHistory.Add(evnt.no, list);
				}
			}
			evnt.OnEventExecution(e);
		}

		public static int PutInFel(Event evnt)
		{
			int result = Simulation.fel.Count<Event>();
			for (int i = 1; i < Simulation.fel.Count<Event>(); i++)
			{
				if (evnt.executionTime < Simulation.fel[i].executionTime)
				{
					result = i;
					break;
				}
				if (evnt.executionTime == Simulation.fel[i].executionTime)
				{
					if (evnt.priority < Simulation.fel[i].priority)
					{
						result = i;
						break;
					}
				}
			}
			return result;
		}
	}
}
