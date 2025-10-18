using System;

namespace MS2IPL
{
	public abstract class Library
	{ }

	public sealed class STD : Library
	{
		private STD() { }
		private static STD _instance = new STD();
		public static STD Instance => _instance;
		private ReturnCode Error(string s)
		{
			Logger.AddMessage(s, Logger.MessageType.RuntimeError);
			return ReturnCode.Error;
		}

		#region constants
		[BringToPL(false)]
		public static long maxInt(STD s) => long.MaxValue;
		[BringToPL(false)]
		public static long minInt(STD s) => long.MinValue;
		
		[BringToPL(false)]
		public static decimal maxFloat(STD s) => decimal.MaxValue;
		[BringToPL(false)]
		public static decimal minFloat(STD s) => decimal.MinValue;

		[BringToPL(false)]
		public static decimal pi(STD s) => (decimal)double.Pi;
		[BringToPL(false)]
		public static decimal e(STD s) => (decimal)double.E;
		[BringToPL(false)]
		public static decimal rad2deg(STD s) => 180 / (decimal)double.Pi;
		[BringToPL(false)]
		public static decimal deg2rad(STD s) => (decimal)double.Pi / 180;
		#endregion

		#region convert
		[BringToPL(true, "int")]
		public static object @int(STD s, object o)
		{
			try { return TypeSystem.ConvertValue(o, TypeNode.Int); }
			catch { return s.Error($"Cannot convert {o} into int at {nameof(MS2IPL)}.{nameof(STD)}.{nameof(@int)}"); }
		}
		[BringToPL(true, "float")]
		public static object @float(STD s, object o)
		{
			try { return TypeSystem.ConvertValue(o, TypeNode.Float); }
			catch { return s.Error($"Cannot convert {o} into float at {nameof(MS2IPL)}.{nameof(STD)}.{nameof(@float)}"); }
		}
		[BringToPL(true, "bool")]
		public static object @bool(STD s, object o)
		{
			try { return TypeSystem.ConvertValue(o, TypeNode.Bool); }
			catch { return s.Error($"Cannot convert {o} into bool at {nameof(MS2IPL)}.{nameof(STD)}.{nameof(@bool)}"); }
		}
		[BringToPL(true)]
		public static string @string(STD s, object o) => TypeSystem.ToString(o);
		#endregion

		#region mathlab simulator
		#region trig
		[BringToPL(true)]
		public static decimal sin(STD s, decimal x) => (decimal)Math.Sin((double)x);
		[BringToPL(true)]
		public static decimal cos(STD s, decimal x) => (decimal)Math.Cos((double)x);
		[BringToPL(true)]
		public static decimal tan(STD s, decimal x) => (decimal)Math.Tan((double)x);
		[BringToPL(true)]
		public static decimal asin(STD s, decimal x) => (decimal)Math.Asin((double)x);
		[BringToPL(true)]
		public static decimal acos(STD s, decimal x) => (decimal)Math.Acos((double)x);
		[BringToPL(true)]
		public static decimal atan(STD s, decimal x) => (decimal)Math.Atan((double)x);
		#endregion

		#region hyp
		[BringToPL(true)]
		public static decimal sinh(STD s, decimal x) => (decimal)Math.Sinh((double)x);
		[BringToPL(true)]
		public static decimal cosh(STD s, decimal x) => (decimal)Math.Cosh((double)x);
		[BringToPL(true)]
		public static decimal tanh(STD s, decimal x) => (decimal)Math.Tanh((double)x);
		[BringToPL(true)]
		public static decimal asinh(STD s, decimal x) => (decimal)Math.Asinh((double)x);
		[BringToPL(true)]
		public static decimal acosh(STD s, decimal x) => (decimal)Math.Acosh((double)x);
		[BringToPL(true)]
		public static decimal atanh(STD s, decimal x) => (decimal)Math.Atanh((double)x);
		#endregion

		#region powers and logs
		[BringToPL(true)]
		public static decimal ln(STD S, decimal x) => (decimal)Math.Log((double)x);
		[BringToPL(true)]
		public static decimal lg(STD S, decimal x) => (decimal)Math.Log10((double)x);
		[BringToPL(true)]
		public static decimal log(STD S, decimal x, decimal y) => (decimal)Math.Log((double)x, (double)y);
		[BringToPL(true)]
		public static decimal sqrt(STD S, decimal x) => (decimal)Math.Sqrt((double)x);
		[BringToPL(true)]
		public static decimal cbrt(STD S, decimal x) => (decimal)Math.Cbrt((double)x);
		#endregion

		#region other
		[BringToPL(true)]
		public static long sgn(STD S, decimal x) => Math.Sign((double)x);
		[BringToPL(true)]
		public static decimal abs(STD S, decimal x) => (decimal)Math.Abs((double)x);
		[BringToPL(true)]
		public static long absi(STD S, long x) => Math.Abs(x);
		[BringToPL(true)]
		public static decimal round(STD S, decimal x) => (decimal)Math.Round((double)x);
		[BringToPL(true)]
		public static decimal ceil(STD S, decimal x) => (decimal)Math.Ceiling((double)x);
		[BringToPL(true)]
		public static decimal max(STD S, decimal x, decimal y) => Math.Max(x, y);
		[BringToPL(true)]
		public static long maxi(STD S, long x, long y) => Math.Max(x, y);
		[BringToPL(true)]
		public static decimal min(STD S, decimal x, decimal y) => Math.Min(x, y);
		[BringToPL(true)]
		public static long mini(STD S, long x, long y) => Math.Min(x, y);
		[BringToPL(true)]
		public static decimal clamp(STD S, decimal x, decimal a, decimal b) => Math.Clamp(x, a, b);
		[BringToPL(true)]
		public static long clampi(STD S, long x, long a, long b) => Math.Clamp(x, a, b);
		#endregion
		#endregion

		#region other
		[BringToPL(true)]
		public static void print(STD s, object message) => Logger.AddMessage(message, Logger.MessageType.UserOutput);

		[BringToPL(true)]
		public static void sqrt7(STD s) =>
			Logger.AddMessage("The creator of the Modular Spaceships 2 Interpreted Programming Language has been summoned. Donate him a cup of java", Logger.MessageType.Debug);
		[BringToPL(true)]
		public static void summonTheCow(STD s) =>
			Logger.AddMessage("moooooo", Logger.MessageType.cow);
		#endregion
	}
}
