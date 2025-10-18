using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MS2IPL
{
	public static class OperatorCollection
	{
		private static readonly Dictionary<OperatorType, OperatorData> _operators = Generate();

		private static Dictionary<OperatorType, OperatorData> Generate()
		{
			var ret = new Dictionary<OperatorType, OperatorData>()
			{
				{ OperatorType.Ter1, new OperatorData("\\?", OperatorType.Ter1, false) },
				{ OperatorType.Ter2, new OperatorData("\\:", OperatorType.Ter2, false) },
				{ OperatorType.Dot, new OperatorData("\\.", OperatorType.Dot, false) },

				{ OperatorType.Add, new OperatorData("\\+", OperatorType.Add) },
				{ OperatorType.Sub, new OperatorData("-", OperatorType.Sub) },
				{ OperatorType.Mul, new OperatorData("\\*{1,2}", OperatorType.Mul) },
				{ OperatorType.Pow, new OperatorData("\\*{2}", OperatorType.Pow, true, true) },
				{ OperatorType.Div, new OperatorData("\\/{1,2}", OperatorType.Div) },
				{ OperatorType.DivInt, new OperatorData("\\/{2}", OperatorType.DivInt, true, true) },
				{ OperatorType.Mod, new OperatorData("%", OperatorType.Mod) },
				{ OperatorType.Neg, new OperatorData("-", OperatorType.Neg, false, true) },

				{ OperatorType.Not, new OperatorData("!", OperatorType.Not) },
				{ OperatorType.Or, new OperatorData("\\|{1,2}", OperatorType.Or) },
				{ OperatorType.Or2, new OperatorData("\\|{2}", OperatorType.Or2, true, true) },
				{ OperatorType.And, new OperatorData("\\&{1,2}", OperatorType.And) },
				{ OperatorType.And2, new OperatorData("\\&{2}", OperatorType.And2, true, true) },
				{ OperatorType.Xor, new OperatorData("\\^", OperatorType.Xor) },

				{ OperatorType.Less, new OperatorData("<", OperatorType.Less) },
				{ OperatorType.Greater, new OperatorData(">", OperatorType.Greater) },
				{ OperatorType.Eq, new OperatorData("==", OperatorType.Eq, false, true) },

				{ OperatorType.Char, new OperatorData("\\$", OperatorType.Char, false) },
				{ OperatorType.Assign, new OperatorData("={1,2}", OperatorType.Assign, false) },
				{ OperatorType.Sep, new OperatorData(",", OperatorType.Sep, false) },
				{ OperatorType.None, new OperatorData("(\\*{3,})|(\\/{3,})|(\\|{3,})|(\\&{3,})|(={3,})", OperatorType.None, false) }
			};

			return ret;
		}

		public static OperatorData Get(OperatorType t) => _operators[t];
		public static string Regexp(OperatorType t) => Get(t).Regexp;
		public static bool IsUsableWithAssignment(OperatorType t) => Get(t).UsableWithAssignment;
		public static bool IsOperator(string s, OperatorType t) => Regex.IsMatch(s, Regexp(t));
		public static string[] Regexps =>
			_operators.Values.Where(x => !string.IsNullOrEmpty(x.Regexp) && !x.Hidden).Select(x => x.Regexp).ToArray();
	}

	public class OperatorData
	{
		public readonly string Regexp;
		public readonly OperatorType Type;
		public readonly bool UsableWithAssignment;
		public readonly bool Hidden;

		internal OperatorData(string regexp, OperatorType type, bool usableWithAssignment = true, bool hidden = false)
		{
			Regexp = regexp;
			Type = type;
			UsableWithAssignment = usableWithAssignment;
			Hidden = hidden;
		}
	}

	public enum OperatorType : byte
	{
		None, Ter1, Ter2, Dot,
		Add, Sub, Neg,
		Mul, Pow, Div, DivInt, Mod,
		Not, Or, Or2, And, And2, Xor,
		Less, LessEq, Greater, GreaterEq, Eq, NotEq,
		Concat, StrMul, Char, ChCode,
		Assign,
		Sep
	}
}
