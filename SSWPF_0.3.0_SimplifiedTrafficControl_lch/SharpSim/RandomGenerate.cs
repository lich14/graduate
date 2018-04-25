using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSim
{
	public class RandomGenerate
	{
		public enum dist
		{
			none,
			exponential,
			normal,
			uniform,
			gamma,
			chiSquare,
			inverseGamma,
			weibull,
			cauchy,
			studentT,
			laplace,
			logNormal,
			beta
		}

		public static Random rnd;

		public RandomGenerate()
		{
			RandomGenerate.rnd = new Random();
		}

		public RandomGenerate(int seed)
		{
			RandomGenerate.rnd = new Random(seed);
		}

		public static double GetRandomDoubleNumber(double minimum, double maximum)
		{
			return RandomGenerate.rnd.NextDouble() * (maximum - minimum) + minimum;
		}

		public static double ComputeValue(List<object> simDist)
		{
			double result = 0.0;
			string dist = (string)simDist[0];
			int num = simDist.Count<object>() - 1;
			switch (num)
			{
			case 1:
				result = RandomGenerate.ComputeValue(dist, (double)simDist[num], 0.0);
				break;
			case 2:
				result = RandomGenerate.ComputeValue(dist, (double)simDist[num - 1], (double)simDist[num]);
				break;
			}
			return result;
		}

		public static double ComputeValue(string dist, double param1, double param2)
		{
			double result = 0.0;
			switch (dist)
			{
			case "exponential":
				result = RandomGenerate.GetExponential(param1);
				break;
			case "normal":
				result = RandomGenerate.GetNormal(param1, param2);
				break;
			case "uniform":
				result = RandomGenerate.GetUniform(param1, param2);
				break;
			case "gamma":
				result = RandomGenerate.GetGamma(param1, param2);
				break;
			case "chiSquare":
				result = RandomGenerate.GetChiSquare(param1);
				break;
			case "inverseGamma":
				result = RandomGenerate.GetInverseGamma(param1, param2);
				break;
			case "weibull":
				result = RandomGenerate.GetWeibull(param1, param2);
				break;
			case "cauchy":
				result = RandomGenerate.GetCauchy(param1, param2);
				break;
			case "studentT":
				result = RandomGenerate.GetStudentT(param1);
				break;
			case "laplace":
				result = RandomGenerate.GetLaplace(param1, param2);
				break;
			case "logNormal":
				result = RandomGenerate.GetLogNormal(param1, param2);
				break;
			case "beta":
				result = RandomGenerate.GetBeta(param1, param2);
				break;
			}
			return result;
		}

		public static double ComputeValue(RandomGenerate.dist distribution, double param1, double param2)
		{
			double result = 0.0;
			switch (distribution)
			{
			case RandomGenerate.dist.exponential:
				result = RandomGenerate.GetExponential(param1);
				break;
			case RandomGenerate.dist.normal:
				result = RandomGenerate.GetNormal(param1, param2);
				break;
			case RandomGenerate.dist.uniform:
				result = RandomGenerate.GetUniform(param1, param2);
				break;
			case RandomGenerate.dist.gamma:
				result = RandomGenerate.GetGamma(param1, param2);
				break;
			case RandomGenerate.dist.chiSquare:
				result = RandomGenerate.GetChiSquare(param1);
				break;
			case RandomGenerate.dist.inverseGamma:
				result = RandomGenerate.GetInverseGamma(param1, param2);
				break;
			case RandomGenerate.dist.weibull:
				result = RandomGenerate.GetWeibull(param1, param2);
				break;
			case RandomGenerate.dist.cauchy:
				result = RandomGenerate.GetCauchy(param1, param2);
				break;
			case RandomGenerate.dist.studentT:
				result = RandomGenerate.GetStudentT(param1);
				break;
			case RandomGenerate.dist.laplace:
				result = RandomGenerate.GetLaplace(param1, param2);
				break;
			case RandomGenerate.dist.logNormal:
				result = RandomGenerate.GetLogNormal(param1, param2);
				break;
			case RandomGenerate.dist.beta:
				result = RandomGenerate.GetBeta(param1, param2);
				break;
			}
			return result;
		}

		public static bool GenerateBool(double criticalValue)
		{
			bool result = true;
			if (RandomGenerate.rnd.NextDouble() < criticalValue)
			{
				result = false;
			}
			return result;
		}

		public static int GenerateInteger(int criticalValue)
		{
			return RandomGenerate.rnd.Next(criticalValue);
		}

		public static double GetUniform()
		{
			return RandomGenerate.rnd.NextDouble();
		}

		public static double GetUniform(double a, double b)
		{
			return a + RandomGenerate.rnd.NextDouble() * (b - a);
		}

		public static double GetNormal()
		{
			double uniform = RandomGenerate.GetUniform();
			double uniform2 = RandomGenerate.GetUniform();
			double num = Math.Sqrt(-2.0 * Math.Log(uniform));
			double a = 6.2831853071795862 * uniform2;
			return num * Math.Sin(a);
		}

		public static double GetNormal(double mean, double standardDeviation)
		{
			if (standardDeviation < 0.0)
			{
				string paramName = string.Format("Standard Deviation must be positive. Received {Negative}.", standardDeviation);
				throw new ArgumentOutOfRangeException(paramName);
			}
			return mean + standardDeviation * RandomGenerate.GetNormal();
		}

		public static double GetExponential()
		{
			return Math.Log(RandomGenerate.rnd.NextDouble());
		}

		public static double GetExponential(double mean)
		{
			return -mean * Math.Log(RandomGenerate.rnd.NextDouble());
		}

		public static double GetGamma(double shape, double scale)
		{
			double result;
			if (shape >= 1.0)
			{
				double num = shape - 0.33333333333333331;
				double num2 = 1.0 / Math.Sqrt(9.0 * num);
				double num3;
				double uniform;
				double num4;
				do
				{
					double normal;
					do
					{
						normal = RandomGenerate.GetNormal();
						num3 = 1.0 + num2 * normal;
					}
					while (num3 <= 0.0);
					num3 = num3 * num3 * num3;
					uniform = RandomGenerate.GetUniform();
					num4 = normal * normal;
				}
				while (uniform >= 1.0 - 0.0331 * num4 * num4 && Math.Log(uniform) >= 0.5 * num4 + num * (1.0 - num3 + Math.Log(num3)));
				result = scale * num * num3;
			}
			else
			{
				if (shape <= 0.0)
				{
					string paramName = string.Format("Shape must be positive. Received {0}.", shape);
					throw new ArgumentOutOfRangeException(paramName);
				}
				double gamma = RandomGenerate.GetGamma(shape + 1.0, 1.0);
				double uniform2 = RandomGenerate.GetUniform();
				result = scale * gamma * Math.Pow(uniform2, 1.0 / shape);
			}
			return result;
		}

		public static double GetChiSquare(double degreesOfFreedom)
		{
			return RandomGenerate.GetGamma(0.5 * degreesOfFreedom, 2.0);
		}

		public static double GetInverseGamma(double shape, double scale)
		{
			return 1.0 / RandomGenerate.GetGamma(shape, 1.0 / scale);
		}

		public static double GetWeibull(double shape, double scale)
		{
			if (shape <= 0.0 || scale <= 0.0)
			{
				string paramName = string.Format("Shape and scale parameters must be positive. Recieved shape {0} and scale{1}.", shape, scale);
				throw new ArgumentOutOfRangeException(paramName);
			}
			return scale * Math.Pow(-Math.Log(RandomGenerate.GetUniform()), 1.0 / shape);
		}

		public static double GetCauchy(double median, double scale)
		{
			if (scale <= 0.0)
			{
				string message = string.Format("Scale must be positive. Received {0}.", scale);
				throw new ArgumentException(message);
			}
			double uniform = RandomGenerate.GetUniform();
			return median + scale * Math.Tan(3.1415926535897931 * (uniform - 0.5));
		}

		public static double GetStudentT(double degreesOfFreedom)
		{
			if (degreesOfFreedom <= 0.0)
			{
				string message = string.Format("Degrees of freedom must be positive. Received {0}.", degreesOfFreedom);
				throw new ArgumentException(message);
			}
			double normal = RandomGenerate.GetNormal();
			double chiSquare = RandomGenerate.GetChiSquare(degreesOfFreedom);
			return normal / Math.Sqrt(chiSquare / degreesOfFreedom);
		}

		public static double GetLaplace(double mean, double scale)
		{
			double uniform = RandomGenerate.GetUniform();
			return (uniform < 0.5) ? (mean + scale * Math.Log(2.0 * uniform)) : (mean - scale * Math.Log(2.0 * (1.0 - uniform)));
		}

		public static double GetLogNormal(double mu, double sigma)
		{
			return Math.Exp(RandomGenerate.GetNormal(mu, sigma));
		}

		public static double GetBeta(double a, double b)
		{
			if (a <= 0.0 || b <= 0.0)
			{
				string paramName = string.Format("Beta parameters must be positive. Received {0} and {1}.", a, b);
				throw new ArgumentOutOfRangeException(paramName);
			}
			double gamma = RandomGenerate.GetGamma(a, 1.0);
			double gamma2 = RandomGenerate.GetGamma(b, 1.0);
			return gamma / (gamma + gamma2);
		}
	}
}
