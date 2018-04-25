using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSim
{
	public class Edge
	{
		public string name;

		public Event sourceEvent;

		public Event targetEvent;

		public bool multiplyingEdge;

		public double interEventTime;

		public List<object> distribution;

		public string dist;

		public RandomGenerate.dist distEnum;

		public double param1;

		public double param2;

		public Entity attribute;

		public List<Entity> attributeList;

		public bool condition = true;

		public static List<Edge> edgeList = new List<Edge>();

		public bool cancellingEdge = false;

		public bool cancelAllEvents = false;

		public Edge(string name, Event sourceEvent, Event targetEvent)
		{
			this.name = name;
			this.sourceEvent = sourceEvent;
			this.targetEvent = targetEvent;
			targetEvent.SubscribeEventExecutionHandler(sourceEvent);
			sourceEvent.exitingEdge.Add(targetEvent, this);
			Edge.edgeList.Add(this);
		}

		public Edge(string name, Event sourceEvent, Event targetEvent, bool cancelling)
		{
			this.name = name;
			this.sourceEvent = sourceEvent;
			this.targetEvent = targetEvent;
			targetEvent.SubscribeEventExecutionHandler(sourceEvent);
			sourceEvent.exitingEdge.Add(targetEvent, this);
			Edge.edgeList.Add(this);
			if (cancelling)
			{
				this.cancellingEdge = true;
			}
		}

		public static void SetAllEdgeConditionsToTrue()
		{
			for (int i = 0; i < Edge.edgeList.Count<Edge>(); i++)
			{
				Edge.edgeList[i].condition = true;
			}
		}
	}
}
