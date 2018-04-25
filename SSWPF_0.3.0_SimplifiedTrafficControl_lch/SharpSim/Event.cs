using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSim
{
	public class Event
	{
		public delegate void EventExecutionHandler(object object1, EventInfoArgs e);

		public static List<Event> events = new List<Event>();

		public string no;

		public string name;

		public string subModelName;

		public bool alerterEvent = false;

		public bool watcherEvent = false;

		public Edge watcherEdge;

		public double watchDelayTime = 0.0;

		public double executionTime;

		public int priority;

		public Dictionary<Event, Edge> exitingEdge = new Dictionary<Event, Edge>();

		public Entity parameter;

		public List<Entity> parameterList;

		public List<Entity> queue = new List<Entity>();

		public Event triggering;

		public static int counter = 0;

		public Event.EventExecutionHandler EventExecuted;

		public Event(string no, string name, int priority)
		{
			this.no = no;
			this.name = name;
			this.priority = priority;
			if (Simulation.trackList)
			{
				//Simulation.form1.Subscribe(this);
			}
			Event.events.Add(this);
		}

		public Event(string no, string name, int priority, double executionTime)
		{
			this.no = no;
			this.name = name;
			this.priority = priority;
			this.executionTime = executionTime;
			Simulation.fel.Insert(Simulation.PutInFel(this), this);
			if (Simulation.trackList)
			{
				//Simulation.form1.Subscribe(this);
			}
			Event.events.Add(this);
		}

		public Event(string subModelName, string no, string name, int priority)
		{
			this.subModelName = subModelName;
			this.no = no;
			this.name = name;
			this.priority = priority;
			if (Simulation.trackList)
			{
				//Simulation.form1.Subscribe(this);
			}
			Event.events.Add(this);
		}

		public Event(string subModelName, string no, string name, int priority, double executionTime)
		{
			this.subModelName = subModelName;
			this.no = no;
			this.name = name;
			this.priority = priority;
			this.executionTime = executionTime;
			Simulation.fel.Insert(Simulation.PutInFel(this), this);
			if (Simulation.trackList)
			{
				//Simulation.form1.Subscribe(this);
			}
			Event.events.Add(this);
		}

		public Event(string subModelName, string no, string name, int priority, bool alerterEvent, bool watcherEvent)
		{
			this.subModelName = subModelName;
			this.no = no;
			this.name = name;
			this.priority = priority;
			this.alerterEvent = alerterEvent;
			this.watcherEvent = watcherEvent;
			if (Simulation.trackList)
			{
				//Simulation.form1.Subscribe(this);
			}
			Event.events.Add(this);
		}

		public Event(string subModelName, string no, string name, int priority, double executionTime, bool alerterEvent, bool watcherEvent)
		{
			this.subModelName = subModelName;
			this.no = no;
			this.name = name;
			this.priority = priority;
			this.executionTime = executionTime;
			this.alerterEvent = alerterEvent;
			this.watcherEvent = watcherEvent;
			Simulation.fel.Insert(Simulation.PutInFel(this), this);
			if (Simulation.trackList)
			{
				//Simulation.form1.Subscribe(this);
			}
			Event.events.Add(this);
		}

		public void OnEventExecution(EventInfoArgs e)
		{
			if (this.EventExecuted != null)
			{
				this.EventExecuted(this, e);
			}
		}

		public void SubscribeEventExecutionHandler(Event evnt)
		{
			evnt.EventExecuted = (Event.EventExecutionHandler)Delegate.Combine(evnt.EventExecuted, new Event.EventExecutionHandler(this.DelegateExecuteEventExecutionHandler));
		}

		public void Watch(Event evnt, double watchDelayTime)
		{
			evnt.watcherEdge = new Edge("", evnt, this);
			evnt.watchDelayTime = watchDelayTime;
		}

		public void Watch(Event evnt, RandomGenerate.dist rgd, double param1)
		{
			evnt.watcherEdge = new Edge("", evnt, this);
			evnt.watcherEdge.distEnum = rgd;
			evnt.watcherEdge.param1 = param1;
		}

		public void Watch(Event evnt, RandomGenerate.dist rgd, double param1, double param2)
		{
			evnt.watcherEdge = new Edge("", evnt, this);
			evnt.watcherEdge.distEnum = rgd;
			evnt.watcherEdge.param1 = param1;
			evnt.watcherEdge.param2 = param2;
		}

		private void DelegateExecuteEventExecutionHandler(object object1, EventInfoArgs e)
		{
			Edge edge = e.evnt.exitingEdge[this];
			if (this.watcherEvent && e.evnt.alerterEvent && this.alerterEvent != this.watcherEvent)
			{
				edge.attribute = e.evnt.watcherEdge.attribute;
				if (e.evnt.watcherEdge.distEnum != RandomGenerate.dist.none)
				{
					edge.distEnum = e.evnt.watcherEdge.distEnum;
					edge.param1 = e.evnt.watcherEdge.param1;
					double arg_AF_0 = e.evnt.watcherEdge.param2;
					bool flag = 1 == 0;
					edge.param2 = e.evnt.watcherEdge.param2;
				}
				else
				{
					edge.interEventTime = e.evnt.watchDelayTime;
				}
			}
			if (edge.condition)
			{
				if (!edge.cancellingEdge)
				{
					int num = 1;
					if (edge.multiplyingEdge)
					{
						num = edge.attributeList.Count<Entity>();
					}
					for (int i = 0; i < num; i++)
					{
						Event @event = this.DeepCopy(edge, i, e.evnt.executionTime);
						if (@event.parameter != null)
						{
							if (@event.parameter.scheduledEventHistory.ContainsKey(@event.no))
							{
								List<double> list = @event.parameter.scheduledEventHistory[@event.no];
								list.Add(Simulation.clock);
								@event.parameter.scheduledEventHistory[@event.no] = list;
							}
							else
							{
								List<double> list = new List<double>();
								list.Add(Simulation.clock);
								@event.parameter.scheduledEventHistory.Add(@event.no, list);
							}
							try
							{
								if (@event.parameter.history.ContainsKey(@event.no + "s" + Simulation.clock))
								{
									Event.counter++;
									@event.parameter.history.Remove(@event.no + "s" + Simulation.clock);
									@event.parameter.history.Add(string.Concat(new object[]
									{
										@event.no,
										Event.counter,
										"s",
										Simulation.clock
									}), Simulation.clock);
								}
								else
								{
									@event.parameter.history.Add(@event.no + "s" + Simulation.clock, Simulation.clock);
								}
							}
							catch (Exception ex)
							{
								Console.WriteLine(string.Concat(new object[]
								{
									"try failed : ",
									ex.Message,
									"  ",
									Simulation.clock
								}));
							}
						}
						Simulation.fel.Insert(Simulation.PutInFel(@event), @event);
					}
				}
				else
				{
					int num = 1;
					if (edge.multiplyingEdge)
					{
						num = edge.attributeList.Count<Entity>();
					}
					for (int i = 0; i < num; i++)
					{
						Event @event = this.DeepCopy(edge, i, e.evnt.executionTime);
						if (@event.parameter == null)
						{
							foreach (Event current in Simulation.fel)
							{
								if (current.name == this.name)
								{
									Simulation.fel.Remove(current);
									break;
								}
							}
						}
						else if (!edge.cancelAllEvents)
						{
							foreach (Event current in Simulation.fel)
							{
								if (current.parameter != null)
								{
									if (current.parameter.identifier == edge.attribute.identifier && current.name == this.name)
									{
										Simulation.fel.Remove(current);
										break;
									}
								}
							}
						}
						else
						{
							int num2 = 1;
							foreach (Event current in Simulation.fel)
							{
								if (current.parameter != null && current != Simulation.fel[0])
								{
									if (current.parameter.identifier == edge.attribute.identifier)
									{
										num2++;
									}
								}
							}
							for (int j = 0; j < num2; j++)
							{
								foreach (Event current in Simulation.fel)
								{
									if (current.parameter != null && current != Simulation.fel[0])
									{
										if (current.parameter.identifier == edge.attribute.identifier)
										{
											Simulation.fel.Remove(current);
											break;
										}
									}
								}
							}
						}
					}
				}
			}
		}

		public Event DeepCopy(Edge edge, int i, double triggeringEventTime)
		{
			Event @event = (Event)base.MemberwiseClone();
			if (!edge.multiplyingEdge)
			{
				@event.parameter = edge.attribute;
			}
			else
			{
				@event.parameter = edge.attributeList[i];
			}
			double num;
			if (edge.dist != null)
			{
				num = RandomGenerate.ComputeValue(edge.dist, edge.param1, edge.param2);
			}
			else if (edge.distribution != null)
			{
				num = RandomGenerate.ComputeValue(edge.distribution);
			}
			else if (edge.distEnum != RandomGenerate.dist.none)
			{
				num = RandomGenerate.ComputeValue(edge.distEnum, edge.param1, edge.param2);
			}
			else
			{
				num = edge.interEventTime;
			}
			@event.executionTime = Simulation.clock + num;
			@event.triggering = edge.sourceEvent;
			return @event;
		}
	}
}
