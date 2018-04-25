using System;
using System.Collections.Generic;

namespace SharpSim
{
	public abstract class Entity
	{
		public object identifier;

		public Dictionary<string, double> history = new Dictionary<string, double>();

		public Dictionary<string, List<double>> eventHistory = new Dictionary<string, List<double>>();

		public Dictionary<string, List<double>> scheduledEventHistory = new Dictionary<string, List<double>>();

		public Entity()
		{
		}

		public Entity(object identifier)
		{
		}

		public double ReturnInterval(string no1, string no2)
		{
			double num = 0.0;
			double num2 = 0.0;
			if (this.history.ContainsKey(no1))
			{
				num = this.history[no1];
			}
			if (this.history.ContainsKey(no2))
			{
				num2 = this.history[no2];
			}
			return num2 - num;
		}

		public double ReturnIntervalEventHistory(string no1, string no2)
		{
			List<double> list = new List<double>();
			List<double> list2 = new List<double>();
			double num = 0.0;
			double num2 = 0.0;
			if (this.eventHistory.ContainsKey(no1))
			{
				list = this.eventHistory[no1];
				num = list[list.Count - 1];
			}
			if (this.eventHistory.ContainsKey(no2))
			{
				list2 = this.eventHistory[no2];
				num2 = list2[list2.Count - 1];
			}
			return num2 - num;
		}
	}
}
