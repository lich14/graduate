using System;

namespace SharpSim
{
	public class EventInfoArgs : EventArgs
	{
		public Event evnt;

		public EventInfoArgs(Event evnt)
		{
			this.evnt = evnt;
		}
	}
}
