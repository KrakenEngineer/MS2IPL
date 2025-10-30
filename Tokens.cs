using System;

namespace MS2IPL
{
	public abstract class Token
	{
		public abstract TokenType TokenType { get; }
		public int Index = -1;

		public abstract Token Copy(Script s);

		public abstract override string ToString();
	}

	public abstract class ExpressionToken : Token { }

	public class ValueToken : ExpressionToken
	{
		public object Value;
		public override TokenType TokenType => TokenType.Value;

		public ValueToken(object value, int index = -1)
		{
			Value = value;
			Index = index;
		}

		public override ValueToken Copy(Script s) => new ValueToken(Value, Index);

		public override string ToString() => $"ValueToken (value: {Value} at: {Index})";
	}

	public class VariableToken : ExpressionToken
	{
		public override TokenType TokenType => TokenType.Variable;

		public Variable Variable;

		public VariableToken(Variable variable, int index = -1)
		{
			Variable = variable;
			Index = index;
		}

		public override VariableToken Copy(Script s) => new VariableToken(s.Variables.GetPointer(Variable.Name), Index);

		public override string ToString() => $"VariableToken ({Variable.ToString()} at: {Index})";
	}

	public class OperatorToken : ExpressionToken
	{
		private OperatorType _type;
		public OperatorType Type
		{
			get => _type;
			set
			{
				_type = value;
				Priority = GetPriority(_type);
			}
		}

		public int Priority { get; private set; }
		public bool IsAssignment = false;

		public OperatorToken(OperatorType type, int index = -1, bool assign = false)
		{
			Type = type;
			Index = index;
			IsAssignment = assign;
		}

		public override OperatorToken Copy(Script s) => new OperatorToken(_type, Index, IsAssignment);

		//Copied from C# documentation https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/
		private static int GetPriority(OperatorType t) => t switch
		{
			//Priority of ? is lower than priority of : because of how parser works
			OperatorType.Ter1 => 1,
			OperatorType.Ter2 => 2,
			OperatorType.Or2 => 3,
			OperatorType.And2 => 4,
			OperatorType.Or => 5,
			OperatorType.Xor => 6,
			OperatorType.And => 7,
			OperatorType.Eq or OperatorType.NotEq => 8,
			OperatorType.Less or OperatorType.LessEq or OperatorType.Greater or OperatorType.GreaterEq => 9,
			OperatorType.Add or OperatorType.Sub or OperatorType.Concat => 10,
			OperatorType.Mul or OperatorType.Div or OperatorType.DivInt or OperatorType.Mod or OperatorType.StrMul => 11,
			OperatorType.Pow => 12,
			OperatorType.Neg or OperatorType.Not or OperatorType.Char or OperatorType.ChCode => 13,
			OperatorType.Dot => 14,
			_ => -1,
		};

		public override TokenType TokenType => TokenType.Operator;

		public override string ToString() => $"OperatorToken (type: {Type} at: {Index} is_assignment: {IsAssignment})";
	}

	public class BracketToken : ExpressionToken
	{
		public BracketType Type = BracketType.Regular;
		public bool Closing = false;

		public BracketToken(bool closing, BracketType type, int index = -1)
		{
			Closing = closing;
			Type = type;
			Index = index;
		}

		public override BracketToken Copy(Script s) => new BracketToken(Closing, Type, Index);

		public override TokenType TokenType => TokenType.Bracket;

		public override string ToString() => $"BracketToken (type: {Type} at: {Index} closing: {Closing})";
	}

	public enum BracketType : byte
	{
		Regular,
		Square,
		Triangle
	}

	public class TypeToken : Token
	{
		public TypeNode Type;

		public override TokenType TokenType => TokenType.Type;

		public TypeToken(TypeNode type, int index = -1)
		{
			Type = type;
			Index = index;
		}

		public override TypeToken Copy(Script s) => new TypeToken(Type, Index);

		public override string ToString() => $"TypeToken (type: {Type} at: {Index})";
	}

	public class StatementToken : Token
	{
		public override TokenType TokenType => TokenType.Statement;
		public StatementType Type;

		public StatementToken(StatementType type, int index = -1)
		{
			Type = type;
			Index = index;
		}

		public override StatementToken Copy(Script s) => new StatementToken(Type, Index);

		public override string ToString() => $"StatementToken (type: {Type} at: {Index})";
	}

	public enum StatementType : byte
	{
		none, cls,
		ifelse, @if, elif, @else,
		@switch, @case, @default,
		@while, always, @for, @break, @continue
	}

	public class MemberToken : ExpressionToken
	{
		public override TokenType TokenType => TokenType.Member;
		public string Name;

		public MemberToken(string name, int index)
		{
			Name = name;
			Index = index;
		}

		public override MemberToken Copy(Script s) => new MemberToken(Name, Index);

		public override string ToString() => $"MemberToken (name: {Name} at: {Index})";
	}

	public class RawToken
	{
		public string Content;
		public int Index;
		public TokenType Type;

		public RawToken(string content, int index, TokenType type)
		{
			Content = content;
			Index = index;
			Type = type;
		}

		public override string ToString() => $"RawToken (content: {Content} at: {Index} type: {Type})";
	}

	public enum TokenType : byte
	{
		None,
		Value,
		Variable,
		Operator,
		Bracket,
		Type,
		Statement,
		Member
	}
}