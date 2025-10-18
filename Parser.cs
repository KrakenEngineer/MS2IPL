using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MS2IPL
{
	//Converts tokens into program execution tree nodes (from CodeNodes.cs)
	public static class Parser
	{
		private static Script s_currentScript;
		private static string[] s_lines;
		private static Token[][] s_tokens;
		private static int s_lineIndex;
		private static string s_line => s_lines[s_lineIndex];
		private static bool s_declaringLValue = false;
		private static Stack<Statement> s_statements = new Stack<Statement>();
		private static StatementType s_statementType => s_statements.TryPeek(out Statement s) ? s.StatementType : StatementType.none;

		public static StatementList Parse(Script s, string[] lines, Token[][] tokens)
		{
			s_currentScript = s;
			s_lines = lines;
			s_tokens = tokens;
			var statements = new StatementList();
			s_lineIndex = 0;

			while (s_lineIndex < s_lines.Length)
			{
				//Console.WriteLine($"{_currentLine} {_lines[_currentLine]}");
				if (s_tokens[s_lineIndex] == null || s_tokens[s_lineIndex].Length == 0)
				{
					s_lineIndex++;
					continue;
				}
				CodeNode node = ParseLine(s_tokens[s_lineIndex], s_lines[s_lineIndex]);
				if (node != null && node != statements.Last && node != SingletoneStatement.CLS)
					statements.Add(node);
				s_lineIndex++;
			}

			statements.Complete(s_currentScript);
			return statements;
		}

		private static CodeNode ParseLine(Token[] tokens, string original)
		{
			if (tokens == null || tokens.Length == 0)
				return null;

			if (s_statementType == StatementType.@switch && (tokens[0] is not StatementToken s ||
				(s.Type != StatementType.@case && s.Type != StatementType.@default && s.Type != StatementType.cls)))
				return Error<CodeNode>($"Invalid token {tokens[0]} at the line\n{s_line}\nnumber{s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseLine)}");
			if (s_statementType == StatementType.ifelse && (tokens[0] is not StatementToken s1 ||
				(s1.Type != StatementType.elif && s1.Type != StatementType.@else)))
				((IfElseChain)s_statements.Pop()).Complete(s_currentScript);

			switch (tokens[0].TokenType)
			{
				case TokenType.Statement:
					return ParseStatement(tokens, 0, tokens.Length - 1);
				case TokenType.Type:
					return ParseDeclaration(tokens, 0, tokens.Length - 1);
				case TokenType.Variable:
					if (FindAssignmentOperator(tokens, 0, tokens.Length - 1).Item2 != -1)
						return ParseAssignment(tokens, 0, tokens.Length - 1);
					return ParseExpression(tokens, 0, tokens.Length - 1);
				default:
					return Error<CodeNode>($"The line\n{s_line}\nnumber {s_lineIndex} cannot start with {tokens[0]} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseLine)}");
			}
		}

		private static CodeNode ParseStatement(Token[] tokens, int start, int end)
		{
			var statement = tokens[start] as StatementToken;
			switch (statement.Type)
			{
				case StatementType.PRINT:
					ExpressionNode expression = ParseExpression(tokens, start + 1, end);
					if (expression == null)
						return Error<CodeNode>($"Invalid expression at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseStatement)}");
					return new PRINT(expression);

				case StatementType.@if:
					var ch = new IfElseChain(s_currentScript);
					s_statements.Push(ch);
					ParseCondition(tokens, ch);
					if (s_lineIndex >= s_lines.Length - 1)
						ch.Complete(s_currentScript);
					return ch;

				case StatementType.@elif:
					if (!s_statements.TryPeek(out Statement s) || s is not IfElseChain ch1 || ch1.Completed)
						return Error<CodeNode>($"Cannot add elif at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseStatement)}");
					
					ParseCondition(tokens, ch1);
					if (s_lineIndex >= s_lines.Length - 1)
						ch1.Complete(s_currentScript);
					return ch1;

				case StatementType.@else:
					if (!s_statements.TryPeek(out s) || s is not IfElseChain ch2 || ch2.Completed)
						return Error<CodeNode>($"Cannot add else at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseStatement)}");
					
					var @else = new Condition(s_currentScript);
					s_statements.Push(@else);
					StatementList body = ParseBody();
					if (body != null)
						@else.TryGiveStatements(body);

					ch2.Add(@else);
					ch2.Complete(s_currentScript);
					s_statements.Pop();
					return ch2;

				case StatementType.@switch:
					return ParseSwitch(tokens);

				case StatementType.@while:
				case StatementType.always:
					ExpressionNode condition = null;
					if (statement.Type == StatementType.@while)
						condition = ParseExpression(tokens, 1, tokens.Length - 1);

					var @while = new While(s_currentScript, condition);
					s_statements.Push(@while);
					body = ParseBody();
					s_statements.Pop();
					@while.TryGiveStatements(body);
					return @while;

				case StatementType.@for:
					int[] semicolons = FindOptokens(tokens, start, end, OperatorType.Sep);
					if (semicolons.Length == 0 || semicolons.Length > 2)
						return Error<CodeNode>($"for in the line\n{s_line}\nnumber {s_lineIndex} cannot have {semicolons.Length} semicolons at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseStatement)}");
					
					if (semicolons.Length == 1)
					{
						condition = ParseExpression(tokens, 1, semicolons[0] - 1);
						Assignment end_assign = ParseAssignment(tokens, semicolons[0] + 1, end);
						var _for = new For(s_currentScript, condition, end_assign);
						s_statements.Push(_for);
						body = ParseBody();
						s_statements.Pop();
						_for.TryGiveStatements(body);
						return _for;
					}

					//Console.WriteLine($"{semicolons[0]} {semicolons[1]}");
					Assignment start_assign = ParseAssignment(tokens, 1, semicolons[0] - 1);
					condition = ParseExpression(tokens, semicolons[0] + 1, semicolons[1] - 1);
					Assignment endAssign = ParseAssignment(tokens, semicolons[1] + 1, end);
					var @for = new For(s_currentScript, condition, endAssign, start_assign);
					s_statements.Push(@for);
					body = ParseBody();
					s_statements.Pop();
					@for.TryGiveStatements(body);
					return @for;

				case StatementType.@break:
				case StatementType.@continue:
				case StatementType.cls:
					if (tokens.Length != 1)
						return Error<CodeNode>($"{statement.Type} must be the only token in the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseStatement)}");
					
					return statement.Type switch
					{
						StatementType.cls => SingletoneStatement.CLS,
						StatementType.@break => SingletoneStatement.Break,
						StatementType.@continue => SingletoneStatement.Continue
					};

				default:
					return Error<CodeNode>($"Unknown statement type for token {statement} for line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseStatement)}");
			}
		}

		private static Switch ParseSwitch(Token[] tokens)
		{
			//Console.WriteLine("switch enter " + s_lineIndex);
			ExpressionNode cond = ParseExpression(tokens, 1, tokens.Length - 1);
			TypeNode type = cond.ReturnType;
			var values = new Dictionary<int, object>();
			var @switch = new Switch(s_currentScript, cond);
			s_statements.Push(@switch);
			(int, string) cur_line = (s_lineIndex, s_line);
			s_lineIndex++;
			bool cls = false;

			while (s_lineIndex < s_lines.Length && !cls)
			{
				tokens = s_tokens[s_lineIndex];
				if (tokens == null || tokens.Length == 0)
					continue;

				Case node;
				if (tokens[0] is not StatementToken st || (st.Type != StatementType.@case && st.Type != StatementType.@default))
					return Error<Switch>($"Invalid token {tokens[0]} at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseSwitch)}");
				else node = ParseCase(tokens, values, type);

				if (node != null)
				{
					@switch.Add(node);
					if (node.IsDefault)
					{
						cls = true;
						break;
					}
				}
				s_lineIndex++;
			}

			if (cls)
			{
				@switch.Complete(s_currentScript);
				return @switch;
			}

			return Error<Switch>($"switch at the line\n{cur_line.Item2}\n number {cur_line.Item1} is not closed at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseSwitch)}");
		}

		private static Case ParseCase(Token[] tokens, Dictionary<int, object> values, TypeNode type)
		{
			var node = new Case(s_currentScript, type);
			var st = tokens[0] as StatementToken;

			if (st.Type == StatementType.@case)
			{
				var caseValues = new Value[tokens.Length - 1];
				for (int i = 1; i < tokens.Length; i++)
				{
					if (tokens[i] is VariableToken v)
					{
						if (v.Variable.ReturnType == null)
							return Error<Case>($"Variable {v.Variable} in the line\n{s_line}\nnumber {s_lineIndex} is not defined at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseCase)}");
						if (v.Variable.ReturnType != type)
							Warn($"Variable {v.Variable} in the line\n{s_line}\nnumber {s_lineIndex} has invalid type. The variable will be ignored at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseCase)}");
						else
							Warn($"Variable {v.Variable} in the line\n{s_line}\nnumber {s_lineIndex} might have the same value as ones in different cases. If it does, only the first case with this value will matter at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseCase)}");
						caseValues[i - 1] = v.Variable;
						continue;
					}
					else if (tokens[i] is ValueToken val)
					{
						if (TypeSystem.TypeOf(val.Value) != type)
							Warn($"Value {val.Value} in the line\n{s_line}\nnumber {s_lineIndex} has invalid type. The value will be ignored at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseCase)}");
						else if (values.ContainsValue(val.Value))
							Warn($"Value {val.Value} in the line\n{s_line}\nnumber {s_lineIndex} has the same value as ones in different cases. Only the first case with this value will matter at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseCase)}");
						caseValues[i - 1] = new ConstantValue(s_currentScript, val.Value);
						values.Add(values.Count, val.Value);
						continue;
					}
					return Error<Case>($"Token {tokens[i]} in the line\n{s_line}\nnumber {s_lineIndex} is not a value or a variable at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseCase)}");
				}
				node = new Case(s_currentScript, type, caseValues);
			}

			s_statements.Push(node);
			StatementList body = ParseBody();
			node.TryGiveStatements(body == null ? new StatementList() : body);
			s_statements.Pop();
			if (node.IsDefault)
			{
				s_statements.Pop();
				//Console.WriteLine("switch exit " + s_lineIndex + ' ' + s_statementType);
			}
			return body == null ? null : node;
		}

		private static Condition ParseCondition(Token[] tokens, IfElseChain ch)
		{
			ExpressionNode cond = ParseExpression(tokens, 1, tokens.Length - 1);
			var node = new Condition(s_currentScript, cond);
			s_statements.Push(node);
			StatementList body = ParseBody();
			CodeNode c = s_statements.Pop();

			if (c is IfElseChain ch1)
			{
				ch1.Complete(s_currentScript);
				s_statements.Pop();
			}

			node.TryGiveStatements(body == null ? new StatementList() : body);
			ch.Add(node);
			return body == null ? null : node;
		}

		private static StatementList ParseBody()
		{
			Token[] tokens;
			var statements = new StatementList();
			(int, string) cur_line = (s_lineIndex, s_line);
			s_lineIndex++;
			bool cls = false;

			while (s_lineIndex < s_lines.Length && !cls)
			{
				tokens = s_tokens[s_lineIndex];
				if (tokens == null || tokens.Length == 0)
				{
					s_lineIndex++;
					continue;
				}
				CodeNode node = ParseLine(tokens, s_lines[s_lineIndex]);
				if (node == SingletoneStatement.CLS)
				{
					cls = true;
					break;
				}
				if (node != null && node != statements.Last)
					statements.Add(node);
				s_lineIndex++;
			}

			if (cls)
			{
				statements.Complete(s_currentScript);
				return statements;
			}

			return Error<StatementList>($"{s_statementType} at the line\n{cur_line.Item2}\nnumber {cur_line.Item1} is not closed at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseBody)}");
		}

		private static Assignment ParseDeclaration(Token[] tokens, int start, int end)
		{
			var type = tokens[start] as TypeToken;
			int assign_pos = FindAssignmentOperator(tokens, start, end).Item2;
			if (assign_pos == -1)
				assign_pos += tokens.Length + 1;

			if (start == end || tokens[assign_pos - 1] is not VariableToken varToken || type == null)
				return Error<Assignment>($"Invalid variable declaration at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseDeclaration)}");
			if (varToken.Variable.ReturnType != null)
				return Error<Assignment>($"Variable from the token {varToken} at the line\n{s_line}\nnumber {s_lineIndex} already exists at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseDeclaration)}");

			s_declaringLValue = true;
			Assignment ret;
			if (assign_pos == tokens.Length)
			{
				var rvalue = new ConstantValue(s_currentScript, type.Type.DefaultValue);
				ret = new Assignment(s_currentScript, varToken.Variable, rvalue, OperatorType.Assign);
				varToken.Variable.SetType(type.Type);
				return ret;
			}
			ret = ParseAssignment(tokens, assign_pos - 1, end, varToken);
			varToken.Variable.SetType(type.Type);
			return ret;
		}

		private static Assignment ParseAssignment(Token[] tokens, int start, int end, VariableToken declaring = null)
		{
			//Console.WriteLine($"assign {s_lineIndex} {start} {end}");
			(OperatorToken, int) op = FindAssignmentOperator(tokens, start, end);
			if (start >= op.Item2 || op.Item2 >= end)
				return Error<Assignment>($"Invalid assignment at the line \n{s_line}\n number {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseAssignment)}");

			ExpressionNode lvalue = ParseExpression(tokens, start, op.Item2 - 1);
			if (lvalue == null)
				return null;

			if (lvalue is not Variable var)
				return Error<Assignment>($"Invalid lvalue at the line \n{s_line}\n number {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseAssignment)}");

			if (declaring != null)
			{
				if (var != declaring.Variable)
					return Error<Assignment>($"Invalid declaration of variable from the token {declaring} at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseAssignment)}");
				if (op.Item1.Type != OperatorType.Assign)
					return Error<Assignment>($"Cannot use variable {var} before initialized ath the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseAssignment)}");
			}

			s_declaringLValue = false;
			ExpressionNode rvalue = ParseExpression(tokens, op.Item2 + 1, end);
			if (rvalue == null)
				return null;

			return new Assignment(s_currentScript, var, rvalue, op.Item1.Type);
		}

		private static (OperatorToken, int) FindAssignmentOperator(Token[] tokens, int start, int end)
		{
			for (int i = start; i <= end; i++)
				if (tokens[i] is OperatorToken o && o.IsAssignment)
					return (o, i);
			return (null, -1);
		}

		private static ExpressionNode ParseExpression(Token[] tokens, int start, int end)
		{
			//Console.WriteLine($"exp {s_lineIndex} {start} {end}");
			if (HasNonExpressionTokens(tokens, start, end, out Token token))
				return Error<ExpressionNode>($"Invalid token {token} in expression at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseExpression)}");
			if (InvalidBrckets(tokens, start, end))
				return Error<ExpressionNode>($"Invalid brackets in expression in the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseExpression)}");
			return ParseExpression_(tokens, start, end);
		}

		private static bool HasNonExpressionTokens(Token[] tokens, int start, int end, out Token token)
		{
			token = null;
			for (int i = start; i <= end; i++)
				if ((tokens[i] is OperatorToken op && (op.IsAssignment || op.Type == OperatorType.Sep)) || tokens[i] is not ExpressionToken)
				{
					token = tokens[i];
					return true;
				}
			return false;
		}

		private static bool InvalidBrckets(Token[] tokens, int start, int end)
		{
			var brackets = new Stack<BracketType>();

			for (int i = start; i <= end; i++)
			{
				if (tokens[i] is not BracketToken bracket)
					continue;

				if (!bracket.Closing)
				{
					brackets.Push(bracket.Type);
					continue;
				}

				if (brackets.Count == 0 || bracket.Type != brackets.Peek())
					return true;

				brackets.Pop();
			}

			return brackets.Count != 0;
		}

		private static ExpressionNode ParseExpression_(Token[] tokens, int start, int end)
		{
			//Logger.AddMessage($"Expression parsing attempt between {tokens[start]} and {tokens[end]}\n{s_original}", Logger.MessageType.Debug);
			if (end - start < 0)
				return Error<ExpressionNode>($"Empty expression at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseExpression_)}");

			if (end == start)
			{
				Token token = tokens[start];
				if (token is VariableToken variable)
				{
					if (variable.Variable.ReturnType == null && !s_declaringLValue)
						return Error<ExpressionNode>($"Cannot use variable {variable.Variable} before declared at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseExpression_)}");
					return variable.Variable;
				}
				if (token is ValueToken value)
					return new ConstantValue(s_currentScript, value.Value);
				return Error<ExpressionNode>($"Cannot create expression with {token.TokenType} at the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseExpression_)}");
			}

			(OperatorToken op, int index) = FindOperator(tokens, start, end, out bool error);
			if (op == null)
			{
				if (error)
					return Error<ExpressionNode>($"Something is wrong with the expression between {tokens[start]} and {tokens[end]} at the line\n{s_line}\nnumber {s_lineIndex} idk how to explain at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseExpression_)}");
				//Debug.Log($"{start + 1} {end + 1} {tokens[start + 1]} {tokens[end - 1]}");
				ExpressionNode node = ParseExpression_(tokens, start + 1, end - 1);
				if (node == null)
					return null;
	            //Logger.AddMessage($"Success between {tokens[start]} and {tokens[end]}\n{s_original}", Logger.MessageType.Debug);
				return node;
			}

			//Debug.Log(op);
			if (Operator.IsUnary(op.Type))
			{
				ExpressionNode arg = ParseExpression_(tokens, index + 1, end);
				if (arg == null)
					return null;
				//Logger.AddMessage($"Success between {tokens[start]} and {tokens[end]}\n{s_original}", Logger.MessageType.Debug);
				return new UnaryOperator(s_currentScript, arg, op.Type);
			}
			if (Operator.IsBinary(op.Type))
			{
				ExpressionNode left = ParseExpression_(tokens, start, index - 1);
				if (left == null)
					return null;
				if (op.Type == OperatorType.Dot)
					return ParseDotOperator(tokens, index + 1, end, left);
				ExpressionNode right = ParseExpression_(tokens, index + 1, end);
                if (right == null)
					return null;
				//Logger.AddMessage($"Success between {tokens[start]} and {tokens[end]}\n{s_original}", Logger.MessageType.Debug);
				return new BinaryOperator(s_currentScript, left, right, op.Type);
			}
			if (Operator.IsTernary(op.Type))
			{
				ExpressionNode left = ParseExpression_(tokens, start, index - 1);
				if (left == null)
					return null;
				ExpressionNode right = ParseExpression_(tokens, index + 1, end);
				if (right == null || right is not BinaryOperator ter2 || ter2.Operation != OperatorType.Ter2)
					return null;
				return new TernaryOperator(s_currentScript, left, ter2.Left, ter2.Right);
			}

			throw new NotImplementedException($"{start} {end}");
		}

		private static ExpressionNode ParseDotOperator(Token[] tokens, int start, int end, ExpressionNode owner)
		{
			if (tokens[start] is not MemberToken m)
				return Error<ExpressionNode>($"Token {tokens[start]} after a dot operator is not a MemberToken in the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseDotOperator)}");
			if (start == end)
			{
				if (!MemberCollection.TryFindMember(owner.ReturnType, m.Name, out Member mem) || mem is not Property p)
					return Error<ExpressionNode>($"Property {m.Name} doesn't exist in the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseDotOperator)}");
				return new PropertyNode(s_currentScript, owner, p);
			}
			return ParseFunction(tokens, start, end, m, owner);
		}

		private static ExpressionNode ParseFunction(Token[] tokens, int start, int end, MemberToken m, ExpressionNode owner = null)
		{
			(int, int) brackets = FindBrackets(tokens, start, end, BracketType.Regular);
			if (brackets.Item1 == start || brackets.Item2 != end)
				return Error<ExpressionNode>($"Invalid brackets for a {(owner == null ? "function" : "method")} in the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseFunction)}");
			ExpressionNode[] parameters;
			if (brackets.Item1 + 1 == brackets.Item2)
				parameters = Array.Empty<ExpressionNode>();
			if (!MemberCollection.TryFindMember(owner.ReturnType, m.Name, out Member mem) || mem is not IFunction f)
				return Error<ExpressionNode>($"IFunction {m.Name} doesn't exist in the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseFunction)}");
			else
			{
				int[] commas = FindOptokens(tokens, start + 1, end - 1, OperatorType.Sep);
				if (commas.Length == 0)
					parameters = new ExpressionNode[1] { ParseExpression_(tokens, brackets.Item1 + 1, end - 1) };
				else
				{
					parameters = new ExpressionNode[commas.Length];
					parameters[0] = ParseExpression_(tokens, brackets.Item1 + 1, commas[0] - 1);
					parameters[^1] = ParseExpression_(tokens, commas[^1] + 1, end - 1);
					for (int i = 0; i < commas.Length - 1; i++)
						parameters[i] = ParseExpression_(tokens, commas[i] + 1, commas[i + 1] - 1);
				}
				if (InvalidParameters(f, parameters, out string error))
					return Error<ExpressionNode>(error);
			}

			if (owner == null)
				throw new NotImplementedException();
			if (f is not Method mt)
				return Error<ExpressionNode>($"Method {m.Name} doesn't exist in the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(ParseFunction)}");
			return new MethodNode(s_currentScript, owner, parameters, mt);
		}

		private static bool InvalidParameters(IFunction f, ExpressionNode[] pars, out string error)
		{
			Parameter[] parameters = f.Parameters;
			if (parameters.Length != pars.Length)
			{
				error = $"Invalid parameter count {pars.Length} instead of {parameters.Length} in the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(InvalidParameters)}";
				return true;
			}

			for (int i = 0; i < pars.Length; i++)
			{
				if (parameters[i].Type != TypeNode.Object && parameters[i].Type != pars[i].ReturnType)
				{
					error = $"Invalid parameter type of {parameters[i]}: {pars[i].ReturnType} instead of {parameters[i].Type} in the line\n{s_line}\nnumber {s_lineIndex} at {nameof(MS2IPL)}.{nameof(Parser)}.{nameof(InvalidParameters)}";
					return true;
				}
			}
			error = "";
			return false;
		}

		private static (int, int) FindBrackets(Token[] tokens, int start, int end, BracketType type)
		{
			int a = -1, b = -1;
			int brackets = 0;

			for (int i = start; i <= end; i++)
			{
				if (tokens[i] is not BracketToken bracket)
					continue;

				if (bracket.Closing)
				{
					brackets--;
					if (brackets == 0 && bracket.Type == type && b == -1)
						b = i;
					continue;
				}

				if (brackets == 0 && bracket.Type == type && a == -1)
					a = i;
				brackets++;
			}

			return (a, b);
		}

		private static int[] FindOptokens(Token[] tokens, int start, int end, OperatorType t)
		{
			int brackets = 0;
			var ret = new List<int>();
			for (int i = start; i < end + 1; i++)
			{
				if (tokens[i] is BracketToken b)
					brackets += b.Closing ? -1 : 1;
				else if (brackets == 0 && tokens[i] is OperatorToken op && op.Type == t)
					ret.Add(i);
			}
			return ret.ToArray();
		}

		private static (OperatorToken, int) FindOperator(Token[] tokens, int start, int end, out bool error)
		{
			OperatorToken ret = null;
			int index = -1;
			int brackets = 0;
			error = false;

			for (int i = end; i >= start; i--)
			{
				if (tokens[i] is BracketToken bracket)
				{
					brackets += bracket.Closing ? 1 : -1;
					continue;
				}
				if (brackets != 0)
					continue;

				error = true;
				if (tokens[i] is not OperatorToken op)
					continue;

				if (ret == null || op.Priority <= ret.Priority)
				{
					ret = op;
					index = i;
					continue;
				}
			}

			error &= ret == null;
			return (ret, index);
		}

		private static T Error<T>(object exception) where T : CodeNode
		{
			s_currentScript.AddError();
			Logger.AddMessage(exception, Logger.MessageType.SyntaxError);
			return null;
		}

		private static void Warn(object message) => Logger.AddMessage(message, Logger.MessageType.Warning);
	}
}