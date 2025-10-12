namespace MS2IPL
{
	public abstract class Library
	{ }

	public sealed class STD : Library
	{
		private STD() { }
		public static readonly STD Instance = new STD();

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
	}
}
