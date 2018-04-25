using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSim
{
	public class Stats
	{
		public static Dictionary<string, BasicStats> Dictionary = new Dictionary<string, BasicStats>();

		public static Dictionary<string, List<double>> globalDictionary = new Dictionary<string, List<double>>();

		public static Dictionary<string, BasicStats> stats = new Dictionary<string, BasicStats>();

		public static void CollectStats(string key, double value)
		{
			if (!Stats.Dictionary.ContainsKey(key))
			{
				BasicStats basicStats = new BasicStats();
				basicStats.counter++;
				basicStats.total += value;
				basicStats.mean = (basicStats.mean * (double)(basicStats.counter - 1) + value) / (double)basicStats.counter;
				Stats.Dictionary.Add(key, basicStats);
			}
			else
			{
				Stats.Dictionary[key].counter++;
				Stats.Dictionary[key].total = Stats.Dictionary[key].total + value;
				Stats.Dictionary[key].mean = (Stats.Dictionary[key].mean * (double)(Stats.Dictionary[key].counter - 1) + value) / (double)Stats.Dictionary[key].counter;
			}
		}

		public static void CollectStats(string key, double[] values)
		{
			if (!Stats.Dictionary.ContainsKey(key))
			{
				BasicStats basicStats = new BasicStats();
				basicStats.counter++;
				basicStats.time = values[0];
				basicStats.value = values[1];
				basicStats.timeWeightedAverage = 0.0;
				Stats.Dictionary.Add(key, basicStats);
			}
			else
			{
				Stats.Dictionary[key].counter++;
				if (Stats.Dictionary[key].counter == 1)
				{
					Stats.Dictionary[key].time = values[0];
					Stats.Dictionary[key].value = values[1];
					Stats.Dictionary[key].timeWeightedAverage = 0.0;
				}
				else
				{
					double time = Stats.Dictionary[key].time;
					double value = Stats.Dictionary[key].value;
					double timeWeightedAverage = Stats.Dictionary[key].timeWeightedAverage;
					Stats.Dictionary[key].timeWeightedAverage = (timeWeightedAverage * time + (values[0] - time) * value) / values[0];
					Stats.Dictionary[key].time = values[0];
					Stats.Dictionary[key].value = values[1];
				}
			}
		}

		public static void CollectCounter(string key, bool pointer)
		{
			if (!Stats.Dictionary.ContainsKey(key))
			{
				BasicStats basicStats = new BasicStats();
				if (pointer)
				{
					basicStats.counter++;
				}
				else
				{
					basicStats.counter--;
				}
				Stats.Dictionary.Add(key, basicStats);
			}
			else if (pointer)
			{
				Stats.Dictionary[key].counter++;
			}
			else
			{
				Stats.Dictionary[key].counter--;
			}
		}

		public static void StatsComputations()
		{
			foreach (KeyValuePair<string, List<double>> current in Stats.globalDictionary)
			{
				BasicStats basicStats = new BasicStats();
				for (int i = 0; i < current.Value.Count<double>(); i++)
				{
					basicStats.counter++;
					basicStats.total += current.Value[i];
				}
				basicStats.mean = basicStats.total / (double)current.Value.Count<double>();
				for (int i = 0; i < current.Value.Count<double>(); i++)
				{
					basicStats.variation = (basicStats.mean - current.Value[i]) * (basicStats.mean - current.Value[i]);
				}
				basicStats.variation /= (double)current.Value.Count<double>();
				basicStats.standardDeviation = Math.Sqrt(basicStats.variation);
				basicStats.confidenceInterval = 1.96 * (basicStats.standardDeviation / Math.Sqrt((double)basicStats.counter));
				Stats.stats.Add(current.Key, basicStats);
			}
		}

		public static void AddDataToStatsGlobalDictionary(string key, double data)
		{
			if (Stats.globalDictionary.ContainsKey(key))
			{
				Stats.globalDictionary[key].Add(data);
			}
		}

		public static void ResetDictionary()
		{
			foreach (KeyValuePair<string, BasicStats> current in Stats.Dictionary)
			{
				current.Value.confidenceInterval = 0.0;
				current.Value.counter = 0;
				current.Value.mean = 0.0;
				current.Value.standardDeviation = 0.0;
				current.Value.total = 0.0;
				current.Value.variation = 0.0;
				current.Value.timeWeightedAverage = 0.0;
				current.Value.time = 0.0;
				current.Value.value = 0.0;
			}
		}
	}
}
