using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.RegularExpressions;

namespace MS2IPL
{
	//Converts raw code into tokens (from Tokens.cs)
	public static class Lexer
	{
		private static Script s_currentScript;
		private static int s_lineIndex;
		private static string s_line;
		private static readonly Dictionary<string, OperatorType> s_stringToOperator = new Dictionary<string, OperatorType>
		{
			{ "?", OperatorType.Ter1 },
			{ ":", OperatorType.Ter2 },
			{ "+", OperatorType.Add },
			{ "-", OperatorType.Sub },
			{ "*", OperatorType.Mul },
			{ "/", OperatorType.Div },
			{ "//", OperatorType.DivInt },
			{ "%", OperatorType.Mod },
			{ "**", OperatorType.Pow },
			{ "!", OperatorType.Not },
			{ "|", OperatorType.Or },
			{ "||", OperatorType.Or2 },
			{ "&", OperatorType.And },
			{ "&&", OperatorType.And2 },
			{ "^", OperatorType.Xor },
			{ "<", OperatorType.Less },
			{ ">", OperatorType.Greater },
			{ "==", OperatorType.Eq },
			{ "$", OperatorType.Char },
			{ "=", OperatorType.Assign },
			{ ";", OperatorType.Sep }
		};

		#region Regular expression patterns
		private static readonly string s_bracket = "[(){}\\[\\]]";
		private static readonly string[] s_operators = OperatorCollection.Regexps;
		private static readonly string s_validVariableName = "[a-zA-Z_]*[a-zA-Z][\\da-zA-Z_]*";
		#endregion

		public static Token[] Analyse(Script s, string line, int index, VariableTable variables)
		{
			s_currentScript = s;
			s_line = line;
			s_lineIndex = index;
			line = line.BeforeSeparator('#');
			if (string.IsNullOrEmpty(line))
				return Array.Empty<Token>();
			List<RawToken> rawTokensList = GetStrings(line, out List<RawToken> substrings, out bool incompleteString);

			//for (int i = 0; i < rawTokensList.Count; i++)
			//	Console.WriteLine(rawTokensList[i]);
			//for (int i = 0; i < substrings.Count; i++)
			//	Console.WriteLine(substrings[i]);

			if (incompleteString)
			{
				Logger.AddMessage($"String in the line\n{s_line}\nnumber {s_lineIndex} is not completed at {nameof(MS2IPL)}.{nameof(Lexer)}.{nameof(Analyse)}", Logger.MessageType.LexycalError);
				return null;
			}

			for (int i = 0; i < substrings.Count; i++)
			{
				List<RawToken> newRawTokens = AnalyseSubstring(substrings[i].Content, substrings[i].Index, variables);
				for (int j = 0; j < newRawTokens.Count; j++)
					rawTokensList.Add(newRawTokens[j]);
			}
			RawToken[] rawTokens = rawTokensList.OrderBy(t => t.Index).ToArray();
			if (rawTokens == null || rawTokens.Length == 0)
				return Array.Empty<Token>();

			//for (int i = 0; i < rawTokens.Length; i++)
			//	Console.Write(rawTokens[i]+" ");
			//Console.WriteLine();

			var tokens = new List<Token>();
			Token curToken = GenerateToken(null, rawTokens[0], variables);
			if (curToken == null)
				return null;
			tokens.Add(curToken);

			for (int i = 1; i < rawTokens.Length; i++)
			{
				curToken = GenerateToken(tokens[^1], rawTokens[i], variables);
				if (curToken == null)
					return null;
				if (curToken == tokens[^1])
					continue;
				tokens.Add(curToken);
			}
			return tokens.ToArray();
		}

		private static List<RawToken> GetStrings(string line, out List<RawToken> substrings, out bool inclompleteString)
		{
			var strings = new List<RawToken>();
			substrings = line[0] == '\"' ? new List<RawToken>() : new List<RawToken>() { new RawToken("", 0, TokenType.None) };
			var inString = false;
			var esc = false;

			for (int i = 0; i < line.Length; i++)
			{
				if (!inString)
				{
					if (line[i] == '\"')
					{
						inString = true;
						strings.Add(new RawToken("\"", i, TokenType.Value));
						continue;
					}
					substrings[^1].Content += line[i];
					continue;
				}

				if (line[i] != '\"' || esc)
				{
					strings[^1].Content += line[i];
					esc = esc ? false : line[i] == '\\';
					continue;
				}

				strings[^1].Content += '\"';
				inString = false;
				substrings.Add(new RawToken("", i + 1, TokenType.None));
			}

			inclompleteString = inString;
			return strings;
		}

		private static List<RawToken> AnalyseSubstring(string substring, int offset, VariableTable variables)
		{
			var rawTokensList = new List<RawToken>();
			MatchCollection temp = Regex.Matches(substring, s_bracket);
			foreach (Match m in temp)
				rawTokensList.Add(new RawToken(m.Value, m.Index + offset, TokenType.Bracket));
			//for (int i = 0; i < rawTokensList.Count; i++)
			//	Console.Write(rawTokensList[i] + " ");
			//Console.WriteLine();

			for (int i = 0; i < s_operators.Length; i++)
			{
				temp = Regex.Matches(substring, s_operators[i]);
				foreach (Match m in temp)
				{
					//Console.WriteLine($"{s_operators[i]} token {m.Value}");
					rawTokensList.Add(new RawToken(m.Value, m.Index + offset, TokenType.Operator));
					if (OperatorCollection.IsOperator(rawTokensList[^1].Content, OperatorType.None))
					{
						Logger.AddMessage($"Invalid operator {rawTokensList[^1]} at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Lexer)}.{nameof(AnalyseSubstring)}", Logger.MessageType.LexycalError);
						return null;
					}
				}
				//Console.WriteLine(s_operators[i]);
			}

			//for (int i = 0; i < rawTokensList.Count; i++)
			//	Console.Write(rawTokensList[i] + " ");
			//Console.WriteLine();

			return rawTokensList.InsertTextTokens(offset, substring);
		}

		private static List<RawToken> InsertTextTokens(this List<RawToken> tokens, int offset, string s)
		{
			if (tokens.Count == 0)
				return s.SplitBySpaces();

			tokens = tokens.OrderBy(t => t.Index).ToList();
			int lastToken = tokens.Count - 1;

			int pos = 0;
			List<RawToken> newTokens = s.Cut(ref pos, tokens[0].Index - offset).SplitBySpaces();
			//Console.WriteLine($"{s} {newTokens.Count}");
			foreach (var token in newTokens)
			{
				token.Index += pos + offset;
				tokens.Add(token);
			}

			for (int i = 0; i < lastToken; i++)
			{
				pos = tokens[i].Index + tokens[i].Content.Length;
				newTokens = s.Cut(ref pos, tokens[i + 1].Index - offset).SplitBySpaces();
				//Console.WriteLine($"{s} {newTokens.Count}");
				foreach (var token in newTokens)
				{
					token.Index += pos + offset;
					tokens.Add(token);
				}
			}

			pos = tokens[lastToken].Index + tokens[lastToken].Content.Length - offset;
			newTokens = s.Cut(ref pos, s.Length).SplitBySpaces();
			//Console.WriteLine($"{s} {newTokens[0]} {pos}");
			foreach (var token in newTokens)
			{
				token.Index += pos + offset;
				tokens.Add(token);
			}

			return tokens.OrderBy(t => t.Index).ToList();
		}

		private static List<RawToken> SplitBySpaces(this string s)
		{
			var tokens = new List<RawToken>();
			if (string.IsNullOrEmpty(s))
				return tokens;

			if (!s[0].IsSpace())
				tokens.Add(new RawToken("" + s[0], 0, TokenType.None));

			for (int i = 1; i < s.Length; i++)
			{
				if (s[i].IsSpace())
					continue;

				if (s[i - 1].IsSpace())
					tokens.Add(new RawToken("" + s[i], i, TokenType.None));
				else
					tokens[^1].Content += s[i];
			}

			return tokens;
		}

		private static Token GenerateToken(Token prev, RawToken current, VariableTable variables)
		{
			if (TryGenerateTextToken(current, out Token t))
				return t;
			if (current.Type == TokenType.None)
				current.Type = RecognizeTokenType(prev, current.Content, variables);
			if (current.Type == TokenType.None)
			{
				if (!IsVariableNameValid(current.Content))
					return null;
				current.Type = TokenType.Variable;
				return GenerateNewVariable(current, variables);
			}

			return current.Type switch
			{
				TokenType.Value => GenerateValue(current),
				TokenType.Type => GenerateType(current),
				TokenType.Operator => GenerateOperator(prev, current),
				TokenType.Bracket => GenerateBracket(current),
				TokenType.Variable => GenerateVariable(current, variables),
				_ => throw new Exception($"Invalid token type {current.Type}")
			};
		}

		private static TokenType RecognizeTokenType(Token prev, string token, VariableTable variables)
		{
			if (decimal.TryParse(token, System.Globalization.NumberStyles.Float, StringUtility.s_ConfigDecoding, out decimal d))
				return TokenType.Value;
			if (variables.Exists(token))
				return TokenType.Variable;
			if (TypeSystem.TypeOf(token) != null)
				return TokenType.Type;
			if (s_stringToOperator.ContainsKey(token))
				return TokenType.Operator;
			if (Regex.IsMatch(token, s_bracket))
				return TokenType.Bracket;
			return TokenType.None;
		}

		public static bool IsVariableNameValid(this string name)
		{
			if (string.IsNullOrEmpty(name))
				return false;

			return Regex.IsMatch(name, s_validVariableName);
		}

		private static ValueToken GenerateValue(RawToken token)
		{
			if (bool.TryParse(token.Content, out bool b))
				return new ValueToken(b, token.Index);
			if (long.TryParse(token.Content, out long l))
				return new ValueToken(l, token.Index);
			if (decimal.TryParse(token.Content, System.Globalization.NumberStyles.Float, StringUtility.s_ConfigDecoding, out decimal d))
				return new ValueToken(d, token.Index);
			if (StringUtility.IsString(token.Content))
				return new ValueToken(StringUtility.ParseString(token.Content), token.Index);
			throw new NotImplementedException($"Value type of {token.Content} isn't supported yet");
		}

		private static OperatorToken GenerateOperator(Token prev, RawToken token)
		{
			if (!s_stringToOperator.ContainsKey(token.Content))
				throw new NotImplementedException($"Operation {token} does not exist");

			if (token.Content == "==")
				return new OperatorToken(OperatorType.Eq, token.Index);
			bool set = OperatorCollection.IsOperator(token.Content, OperatorType.Assign);
			if (set)
			{
				if (prev is not OperatorToken op || op.IsAssignment)
					return new OperatorToken(OperatorType.Assign, token.Index, true);
				if (OperatorCollection.IsUsableWithAssignment(op.Type))
				{
					if (op.Type == OperatorType.Less)
						op.Type = OperatorType.LessEq;
					else if (op.Type == OperatorType.Greater)
						op.Type = OperatorType.GreaterEq;
					else if (op.Type == OperatorType.Not)
						op.Type = OperatorType.NotEq;
					else op.IsAssignment = true;
					return op;
				}
				Logger.AddMessage($"Operator {op} cannot be used with setter at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Lexer)}.{nameof(GenerateOperator)}", Logger.MessageType.SyntaxError);
				return null;
			}

			var ret = new OperatorToken(s_stringToOperator[token.Content], token.Index);
			if (ret.Type == OperatorType.Sub && !set && (prev == null ||
				prev is not ValueToken && prev is not VariableToken && (prev is not BracketToken b || !b.Closing)))
				ret.Type = OperatorType.Neg;

			if (set)
				ret.IsAssignment = true;
			return ret;
		}

		private static BracketToken GenerateBracket(RawToken token)
		{
			return token.Content[0] switch
			{
				'(' => new BracketToken(false, BracketType.Regular, token.Index),
				')' => new BracketToken(true, BracketType.Regular, token.Index),
				'[' => new BracketToken(false, BracketType.Square, token.Index),
				']' => new BracketToken(true, BracketType.Square, token.Index),
				'{' => new BracketToken(false, BracketType.Triangle, token.Index),
				'}' => new BracketToken(true, BracketType.Triangle, token.Index),
				_ => null
			};
		}

		private static VariableToken GenerateVariable(RawToken token, VariableTable variables) =>
			new VariableToken(variables.GetPointer(token.Content), token.Index);

		private static VariableToken GenerateNewVariable(RawToken token, VariableTable variables)
		{
			Variable v = Variable.Untyped(s_currentScript, token.Content);
			variables.TryAdd(v);
			return new VariableToken(v, token.Index);
		}

		private static TypeToken GenerateType(RawToken token)
		{
			TypeNode type = TypeSystem.TypeOf(token.Content);
			return new TypeToken(type, token.Index);
		}

		private static bool TryGenerateTextToken(RawToken token, out Token t)
		{
			t = token.Content switch
			{
				"True" => new ValueToken(true),
				"False" => new ValueToken(false),
				"PRINT" => new StatementToken(StatementType.PRINT),
				"cls" => new StatementToken(StatementType.cls),

				"if" => new StatementToken(StatementType.@if),
				"elif" => new StatementToken(StatementType.elif),
				"else" => new StatementToken(StatementType.@else),
				"switch" => new StatementToken(StatementType.@switch),
				"case" => new StatementToken(StatementType.@case),
				"default" => new StatementToken(StatementType.@default),

				"while" => new StatementToken(StatementType.@while),
				"always" => new StatementToken(StatementType.always),
				"break" => new StatementToken(StatementType.@break),
				"continue" => new StatementToken(StatementType.@continue),
				"for" => new StatementToken(StatementType.@for),

				_ => null
			};

			if (t == null)
				return false;
			t.Index = token.Index;
			return true;
		}

		public static void DebugTokens(string path, string line, Token[] tokens)
		{
			string s = $"Tokens of the line {line} of the script {path}:\n";
			for (int i = 0; i < tokens.Length; i++)
				s += tokens[i].ToString() + '\n';
			Logger.AddMessage(s, Logger.MessageType.Debug);
		}
	}
}
