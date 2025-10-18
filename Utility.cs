using System.IO;
using System.Collections.Generic;
using System.Globalization;

namespace MS2IPL
{
	//Bunch of string utilites that are often used in Lexer
	public static class StringUtility
	{
		public static readonly CultureInfo s_ConfigDecoding = GetConfigDecoding();

		private static CultureInfo GetConfigDecoding()
		{
			var dec = new CultureInfo("en-us", true);
			dec.NumberFormat.NumberDecimalSeparator = ".";
			return dec;
		}

		public static string BeforeSeparator(this string s, char sep = ' ')
		{
			string output = "";
			for (int i = 0; i < s.Length && s[i] != sep; i++)
				output += s[i];
			return output;
		}

		public static string AfterSeparator(this string s, char sep = ' ')
		{
			string output = "";
			int start = 0;
			while (start < s.Length && s[start] != sep)
				start++;
			start++;
			for (int i = start; i < s.Length; i++)
				output += s[i];
			return output;
		}

		public static string Cut(this string s, ref int start, int end)
		{
			string result = "";
			for (int i = start; i < end; i++)
				result += s[i];
			return result;
		}

		public static bool IsSpace(this char s) => s == ' ' || s == '\t' || s == '\n';

		public static string MultiplyString(string s, long l)
		{
			if (l <= 0)
				return "";

			var ret = s;
			for (int i = 1; i < l; i++)
				ret += s;
			return ret;
		}

		public static bool IsString(string token) => token != null && token.Length >= 2 && token[0] == '\"' &&
			token[^1] == '\"' && (token.Length == 2 || token[^2] != '\\' || token[^3] == '\\');

		public static string ParseString(string s)
		{
			if (!IsString(s))
				throw new System.ArgumentException();
			var ret = "";
			var esc = false;

			for (int i = 1; i < s.Length - 1; i++)
			{
				if (esc)
				{
					ret += s[i] switch
					{
						'\\' => '\\',
						'\"' => '\"',
						'n' => '\n',
						't' => '\t',
						_ => throw new System.Exception($"Invalid escape-sequence \"\\{s[i]}\" in the string {s}")
					};
					esc = false;
					continue;
				}

				if (s[i] == '\\')
					esc = true;
				else
					ret += s[i];
			}

			return ret;
		}
	}

	public static class Logger
	{
		private static readonly string s_logPath = "console.txt";
		private static Dictionary<MessageType, int> s_last = new Dictionary<MessageType, int>();
		private static Dictionary<MessageType, int> s_current = new Dictionary<MessageType, int>();
		private static Dictionary<MessageType, List<string>> s_logs = new Dictionary<MessageType, List<string>>();

		public static void ClearConsole() => File.WriteAllText(s_logPath, "");

		public static void AddMessage(object message, MessageType t)
		{
			if (!s_logs.ContainsKey(t))
			{
				s_logs.Add(t, new List<string>());
				s_last.Add(t, 0);
				s_current.Add(t, 0);
			}
			s_current[t]++;
			s_logs[t].Add(message?.ToString());
		}

		public static void Log()
		{
			Log(MessageType.Debug);
			Log(MessageType.LexycalError);
			Log(MessageType.SyntaxError);
			Log(MessageType.Warning);
			Log(MessageType.RuntimeError);
			Log(MessageType.UserOutput);
			Log(MessageType.cow);
		}

		private static void Log(MessageType t)
		{
			if (!s_logs.ContainsKey(t))
				return;
			for (int i = s_last[t]; i < s_current[t]; i++)
				Log(s_logs[t][i], t);
			s_last = s_current;
		}

		public static void LogAll()
		{
			LogAll(MessageType.Debug);
			LogAll(MessageType.LexycalError);
			LogAll(MessageType.SyntaxError);
			LogAll(MessageType.Warning);
			LogAll(MessageType.RuntimeError);
			LogAll(MessageType.UserOutput);
			LogAll(MessageType.cow);
		}

		private static void LogAll(MessageType t)
		{
			if (!s_logs.ContainsKey(t))
				return;
			for (int i = 0; i < s_logs[t].Count; i++)
				Log(s_logs[t][i], t);
		}

		private static void Log(string message, MessageType t)
		{
			File.AppendAllText(s_logPath, Format(message, t));
		}

		private static string Format(string message, MessageType t)
		{
			return t switch
			{
				MessageType.Debug => "debug information",
				MessageType.LexycalError => "lexycal error",
				MessageType.SyntaxError => "syntax error",
				MessageType.Warning => "warning",
				MessageType.RuntimeError => "runtime error",
				MessageType.UserOutput => "user output",
				MessageType.cow => "the cow statue says",
				_ => "unknown message type"
			} + ":\n{\n" + message + "\n}\n\n";
		}

		public enum MessageType : byte
		{
			Debug,
			LexycalError,
			SyntaxError,
			Warning,
			RuntimeError,
			UserOutput,
			cow
		}
	}
}
