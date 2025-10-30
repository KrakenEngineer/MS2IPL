using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MS2IPL
{
	public abstract class CodeNode
	{
		public float Complexity => _complexity;
		private float _complexity;
		public Script Script => _script;
		protected Script _script;

		public abstract object Execute(out ReturnCode ret);

		protected static object Error(object exception)
		{
			Logger.AddMessage(exception, Logger.MessageType.RuntimeError);
			return null;
		}

		protected virtual void CompleteConstruction(Script s)
		{
			_script = s;
			_complexity = GetComplexity();
		}

		protected async void AcceptExecution()
		{
			_script.AcceptExecution(this);
			await _script.WaitForContinue();
		}

		protected abstract float GetComplexity();

		public abstract override string ToString();
		public static bool Any(ReturnCode ret) => ((int)ret) > 1;
		public static bool Any(params ReturnCode[] rets) => rets.Where(r => Any(r)).Any();
	}

	public abstract class Statement : CodeNode
	{
		public abstract StatementType StatementType { get; }
	}

	public abstract class CodeBlock : Statement
	{
		protected StatementList _statements { get; private set; }

		public bool TryGiveStatements(StatementList list)
		{
			if (_statements != null)
				return false;
			if (list == null)
				throw null;
			_statements = list;
			return true;
		}
	}

	public abstract class NodeList<T> : Statement where T : CodeNode
	{
		protected List<T> _temp = new List<T>();
		protected T[] _completed;
		public bool Completed { get; protected set; }

		public virtual void Add(T node)
		{
			if (_temp == null)
				throw new Exception($"This {GetType().FullName} {ToString()} is already completed");
			if (node == null)
				throw new ArgumentNullException();
			_temp.Add(node);
		}

		public void Complete(Script s) => CompleteConstruction(s);
		protected override void CompleteConstruction(Script s)
		{
			base.CompleteConstruction(s);
			if (Completed)
				return;
			_completed = _temp.ToArray();
			_temp = null;
			Completed = true;
		}

		public override string ToString() => _temp == null ? CompletedToString() : TempToString();
		protected abstract string CompletedToString();
		protected abstract string TempToString();
	}

	public abstract class ExpressionNode : CodeNode
	{
		protected bool _isPreEvaluating = false;
		protected TypeNode _returnType;
		public TypeNode ReturnType => _returnType;

		public abstract bool TryPreEvaluate(out ExpressionNode result, out ReturnCode ret);

		public abstract TypeNode GetReturnType();
	}

	public sealed class StatementList : NodeList<CodeNode>
	{
		public override StatementType StatementType => StatementType.none;

		public override object Execute(out ReturnCode ret)
		{
			AcceptExecution();
			if (_completed == null)
				throw new Exception("Cannot evaluate incomplete statement list");
			for (int i = 0; i < Length; i++)
			{
				_completed[i].Execute(out ReturnCode ret_i);
				if (Any(ret_i))
				{
					ret = ret_i;
					return null;
				}
			}
			ret = ReturnCode.Success;
			return null;
		}

		public CodeNode this[int i] => _temp == null ? _completed[i] : _temp[i];
		public int Length => _temp == null ? _completed.Length : _temp.Count;
		public CodeNode Last => Length == 0 ? null : this[Length - 1];
		protected override float GetComplexity() => 0;

		protected override string CompletedToString()
		{
			string output = "";
			for (int i = 0; i < Length; i++)
				output += _completed[i].ToString() + '\n';
			return output;
		}

		protected override string TempToString()
		{
			string output = "temp\n";
			for (int i = 0; i < Length; i++)
				output += _temp[i].ToString() + '\n';
			return output;
		}
	}

	public abstract class Value : ExpressionNode
	{
		public abstract object Get();
	}

	public sealed class ConstantValue : Value
	{
		protected object _value;
		public override object Get() => _value;

		public ConstantValue(Script s, object value)
		{
			if (value == null)
				throw new ArgumentNullException();
			_value = value;
			CompleteConstruction(s);
		}

		protected override void CompleteConstruction(Script s)
		{
			base.CompleteConstruction(s);
			_returnType = GetReturnType();
			if (_returnType == null)
				throw new Exception($"Invalid type of value {_value.GetType().FullName}");
		}

		public override object Execute(out ReturnCode ret)
		{
			ret = ReturnCode.Success;
			return Get();
		}

		public override bool TryPreEvaluate(out ExpressionNode result, out ReturnCode ret)
		{
			ret = ReturnCode.Success;
			result = this;
			return true;
		}

		public override string ToString() => $"value (value: {_value.ToString()} type: {_returnType})";

		public override TypeNode GetReturnType() => TypeSystem.TypeOf(_value);

		protected override float GetComplexity() => 0;
	}

	public abstract class Operator : ExpressionNode
	{
		protected OperatorType _operation;
		public OperatorType Operation => _operation;

		public static bool IsUnary(OperatorType t) => t == OperatorType.Neg || t == OperatorType.Not || t == OperatorType.Char ||
			t == OperatorType.ChCode || t == OperatorType.Vneg;
		public static bool IsBinary(OperatorType t) => t != OperatorType.None && t != OperatorType.Sep &&
			!IsUnary(t) && !IsTernary(t);
		public static bool IsTernary(OperatorType t) => t == OperatorType.Ter1;

		public static bool IsArithmetic(OperatorType t) => OperatorType.Add <= t && t <= OperatorType.Mod;
		public static bool IsLogical(OperatorType t) => OperatorType.Not <= t && t <= OperatorType.Xor;
		public static bool IsRelational(OperatorType t) => OperatorType.Less <= t && t <= OperatorType.NotEq;
		public static bool IsNumberRelational(OperatorType t) => OperatorType.Less <= t && t <= OperatorType.GreaterEq;
		public static bool IsStringy(OperatorType t) => OperatorType.Concat <= t && t <= OperatorType.ChCode;
		public static bool IsVectorish(OperatorType t) => OperatorType.Vadd <= t && t <= OperatorType.DotProduct;
	}

	public sealed class UnaryOperator : Operator
	{
		private ExpressionNode _arg;
		public ExpressionNode Arg => _arg;

		public UnaryOperator(Script s, OperatorType operation)
		{
			if (!IsUnary(operation))
				throw new Exception($"Invalid operation {operation}");
			_operation = operation;
			_script = s;
		}

		public UnaryOperator(Script s, ExpressionNode arg, OperatorType operation) : this(s, operation)
		{
			SetArg(arg);
		}

		public override object Execute(out ReturnCode ret)
		{
			if (!_isPreEvaluating)
				AcceptExecution();
			object arg = _arg.Execute(out ret);
			if (Any(ret))
				return null;

			if (_operation == OperatorType.ChCode)
			{
				var s = (string)arg;
				return string.IsNullOrEmpty(s) ? 0 : (long)s[0];
			}
			return _operation switch
			{
				OperatorType.Not => !(bool)arg,
				OperatorType.Neg => _arg.ReturnType == TypeNode.Int ? -(long)arg : -(decimal)arg,
				OperatorType.Char => $"{(char)(long)arg}",
				OperatorType.Vneg => -(Vector2)arg,
				_ => null
			};
		}

		public override bool TryPreEvaluate(out ExpressionNode result, out ReturnCode ret)
		{
			_isPreEvaluating = true;
			ret = ReturnCode.Success;
			result = this;

			if (_arg.TryPreEvaluate(out _arg, out ReturnCode ret_a))
			{
				object res = Execute(out ret);
				if (Any(ret))
					return false;
				result = new ConstantValue(_script, res);
				return true;
			}
			ret = ret_a;
			_isPreEvaluating = false;
			return Any(ret);
		}

		private void SetArg(ExpressionNode arg)
		{
			if (_arg != null)
				throw new Exception("Unable to reset the argument");
			if (arg == null)
				throw new ArgumentNullException();
			_arg = arg;
			CompleteConstruction(_script);
		}

		protected override void CompleteConstruction(Script s)
		{
			base.CompleteConstruction(s);
			HandleTypeDepended(_arg.ReturnType, ref _operation);
			_returnType = GetReturnType();
			if (_returnType == null)
				throw new Exception($"Invalid argument {_arg.ToString()}" +
					$" of unary operator {_operation} with type {Arg.ReturnType.Type}");
		}

		public override TypeNode GetReturnType()
		{
			if (_operation == OperatorType.Not)
				return _arg.ReturnType == TypeNode.Bool ? TypeNode.Bool : null;
			else if (_operation == OperatorType.Neg)
				return _arg.ReturnType.IsNum ? _arg.ReturnType : null;
			else if (_operation == OperatorType.Char)
				return _arg.ReturnType == TypeNode.Int ? TypeNode.String : null;
			else if (_operation == OperatorType.ChCode)
				return _arg.ReturnType == TypeNode.String ? TypeNode.Int : null;
			else if (_operation == OperatorType.Vneg)
				return _arg.ReturnType == TypeNode.Vector2 ? TypeNode.Vector2 : null;
			return null;
		}

		protected override float GetComplexity() => 0;

		public override string ToString() => $"unary (type: {_operation} arg: {_arg})";

		public static void HandleTypeDepended(TypeNode argtype, ref OperatorType operation)
		{
			if (operation == OperatorType.Char && argtype == TypeNode.String)
				operation =  OperatorType.ChCode;
			else if (operation == OperatorType.Neg &&  argtype == TypeNode.Vector2)
				operation = OperatorType.Vneg;
		}
	}

	public sealed class BinaryOperator : Operator
	{
		private ExpressionNode _left;
		public ExpressionNode Left => _left;
		private ExpressionNode _right;
		public ExpressionNode Right => _right;

		public BinaryOperator(Script s, OperatorType operation)
		{
			if (!IsBinary(operation))
				throw new Exception($"Invalid operation {operation}");
			_operation = operation;
			_script = s;
		}

		public BinaryOperator(Script s, ExpressionNode left, ExpressionNode right, OperatorType operation) : this(s, operation)
		{
			SetLeft(left);
			SetRight(right);
		}

		public override object Execute(out ReturnCode ret)
		{
			if (!_isPreEvaluating)
				AcceptExecution();
			object left = _left.Execute(out ReturnCode ret_l);
			object right = _right.Execute(out ReturnCode ret_r);
			if (Any(ret_l, ret_l))
			{
				ret = ret_l | ret_r;
				return null;
			}
			ret = ReturnCode.Success;

			if (IsArithmetic(_operation))
				return EvaluateArithmetic(left, right, _operation, ReturnType == TypeNode.Int, _script.Line, out ret);
			else if (IsLogical(_operation))
				return EvaluateLogical((bool)left, (bool)right, _operation);
			else if (IsRelational(_operation))
				return EvaluateRelational(left, right, _operation);
			else if (IsStringy(_operation))
				return EvaluateStringy(left, right, _operation);
			else if (IsVectorish(_operation))
				return EvaluateVectorish(left, right, _script.Line, _operation);
			return null;
		}

		public override bool TryPreEvaluate(out ExpressionNode result, out ReturnCode ret)
		{
			_isPreEvaluating = true;
			result = this;
			if (_left.TryPreEvaluate(out _left, out ReturnCode ret_l) & _right.TryPreEvaluate(out _right, out ReturnCode ret_r))
			{
				object res = Execute(out ret);
				if (Any(ret))
					return false;
				result = new ConstantValue(_script, res);
				return true;
			}
			ret = ret_l | ret_r;
			_isPreEvaluating = false;
			return Any(ret);
		}

		public static object EvaluateArithmetic(object left, object right, OperatorType opcode, bool retint, int curline, out ReturnCode ret)
		{
			decimal leftf, rightf;
			if (left is long lefti) leftf = lefti;
			else leftf = (decimal)left;
			if (right is long righti) rightf = righti;
			else rightf = (decimal)right;

			if (((opcode == OperatorType.Div || opcode == OperatorType.DivInt) && rightf == 0) ||
				(opcode == OperatorType.Pow && ((leftf < 0 && right is not int) || (leftf == 0 && rightf <= 0))))
			{
				ret = ReturnCode.Error;
				return Error($"Arithmetic error for the operation {left} {opcode} {right} in the line number {curline} in {nameof(MS2IPL)}.{nameof(BinaryOperator)}.{nameof(EvaluateArithmetic)}");
			}
			ret = ReturnCode.Success;

			object result = opcode switch
			{
				OperatorType.Add => leftf + rightf,
				OperatorType.Sub => leftf - rightf,
				OperatorType.Mul => leftf * rightf,
				OperatorType.Div => leftf / rightf,
				OperatorType.DivInt => (leftf < 0 ^ rightf < 0) ? Math.Ceiling(leftf / rightf) : Math.Floor(leftf / rightf),
				OperatorType.Mod => leftf % rightf,
				OperatorType.Pow => (decimal)Math.Pow((double)leftf, (double)rightf),
				_ => 0
			};

			if (retint)
				result = (long)(decimal)result;
			return result;
		}

		public static bool EvaluateLogical(bool left, bool right, OperatorType opcode)
		{
			return opcode switch
			{
				OperatorType.Or => left | right,
				OperatorType.Or2 => left || right,
				OperatorType.And => left & right,
				OperatorType.And2 => left && right,
				OperatorType.Xor => left ^ right,
				_ => false
			};
		}

		public static bool EvaluateRelational(object left, object right, OperatorType opcode)
		{
			if (!IsNumberRelational(opcode) && !TypeSystem.TypeOf(left).IsNum)
				return object.Equals(left, right) ^ opcode == OperatorType.NotEq;
			decimal leftf, rightf;
			if (left is long lefti) leftf = lefti;
			else leftf = (decimal)left;
			if (right is long righti) rightf = righti;
			else rightf = (decimal)right;
			return opcode switch
			{
				OperatorType.Less => leftf < rightf,
				OperatorType.LessEq => leftf <= rightf,
				OperatorType.Greater => leftf > rightf,
				OperatorType.GreaterEq => leftf >= rightf,
				OperatorType.Eq => leftf == rightf,
				OperatorType.NotEq => leftf != rightf,
				_ => false
			};
		}

		public static string EvaluateStringy(object left, object right, OperatorType opcode)
		{
			return opcode switch
			{
				OperatorType.Concat => TypeSystem.ToString(left) + TypeSystem.ToString(right),
				OperatorType.StrMul => StringUtility.MultiplyString((string)left, (long)right),
				_ => null
			};
		}

		public static object EvaluateVectorish(object left, object right, int curline, OperatorType opcode)
		{
			var leftv = (Vector2)left;
			if (opcode == OperatorType.Vdiv)
			{
				float rightf = right is long righti ? righti : (float)right;
				if (rightf == 0)
					return Error($"Cannot divide vector {leftv} by 0 in the line number {curline} in {nameof(MS2IPL)}.{nameof(BinaryOperator)}.{nameof(EvaluateVectorish)}");
				return leftv / (float)rightf;
			}
			else if (opcode == OperatorType.Vmul)
			{
				float rightf = right is long righti ? righti : (float)right;
				return leftv * (float)rightf;
			}
			var rightv = (Vector2)right;
			return opcode switch
			{
				OperatorType.Vadd => leftv + rightv,
				OperatorType.Vsub => leftv - rightv,
				OperatorType.DotProduct => leftv * rightv,
				_ => new Vector2()
			};
		}

		protected override void CompleteConstruction(Script s)
		{
			base.CompleteConstruction(s);
			HandleTypeDepended(_left.ReturnType, _right.ReturnType, ref _operation);
			_returnType = GetReturnType();
			if (_returnType == null)
				throw new Exception($"Invalid arguments {_left.ToString()} {_right.ToString()} " +
					$"of binary operator {_operation} with types {_left.ReturnType.Type} and {_right.ReturnType.Type}");
		}

		private void SetLeft(ExpressionNode left)
		{
			if (_left != null)
				throw new Exception("Unable to reset left argument");
			if (left == null)
				throw new ArgumentNullException();
			_left = left;
			if (_right != null)
				CompleteConstruction(_script);
		}

		private void SetRight(ExpressionNode right)
		{
			if (_right != null)
				throw new Exception("Unable to reset right argument");
			if (right == null)
				throw new ArgumentNullException();
			_right = right;
			if (_left != null)
				CompleteConstruction(_script);
		}

		public override TypeNode GetReturnType()
		{
			if (IsArithmetic(_operation))
			{
				if (!(_left.ReturnType.IsNum && _right.ReturnType.IsNum))
					return null;
				if (_operation == OperatorType.Div)
					return TypeNode.Float;
				if (_operation == OperatorType.DivInt)
					return TypeNode.Int;
				return _left.ReturnType == TypeNode.Float || _right.ReturnType == TypeNode.Float ? TypeNode.Float : TypeNode.Int;
			}
			else if (IsLogical(_operation))
				return _left.ReturnType == TypeNode.Bool && _right.ReturnType == TypeNode.Bool ? TypeNode.Bool : null;
			else if (IsRelational(_operation))
			{
				if (IsNumberRelational(_operation))
					return _left.ReturnType.IsNum && _right.ReturnType.IsNum ? TypeNode.Bool : null;
				return _left.ReturnType == _right.ReturnType || (_left.ReturnType.IsNum && _right.ReturnType.IsNum) ?
					TypeNode.Bool : null;
			}
			else if (IsStringy(_operation)) {
				return _operation == OperatorType.Concat ?
					(_left.ReturnType == TypeNode.String || _right.ReturnType == TypeNode.String ? TypeNode.String : null) :
					_operation == OperatorType.StrMul ?
					(_left.ReturnType == TypeNode.String && _right.ReturnType == TypeNode.Int ?
					TypeNode.String : null) : null; }
			else if (IsVectorish(_operation)) {
				if (_left.ReturnType != TypeNode.Vector2)
					return null;
				else if (_operation == OperatorType.Vmul || _operation == OperatorType.Vdiv)
					return _right.ReturnType.IsNum ? TypeNode.Vector2 : null;
				else return _right.ReturnType == TypeNode.Vector2 ? TypeNode.Vector2 : null;
			}
			else if (_operation == OperatorType.Ter2)
				return _left.ReturnType == _right.ReturnType ? _left.ReturnType : null;
			return null;
		}

		protected override float GetComplexity() => 0;

		public override string ToString() => $"binary (type: {_operation} left: {_left} right: {_right})";

		public static void HandleTypeDepended(TypeNode lt, TypeNode rt, ref OperatorType operation)
		{
			if (operation == OperatorType.Mul && lt == TypeNode.String)
				operation = OperatorType.StrMul;
			else if (operation == OperatorType.Add && (lt == TypeNode.String || rt == TypeNode.String))
				operation = OperatorType.Concat;
			else if (lt == TypeNode.Vector2)
			{
				if (operation == OperatorType.Mul && rt == TypeNode.Vector2)
					operation = OperatorType.DotProduct;
				else operation = operation switch
				{
					OperatorType.Add => OperatorType.Vadd,
					OperatorType.Sub => OperatorType.Vsub,
					OperatorType.Mul => OperatorType.Vmul,
					OperatorType.Div => OperatorType.Vdiv,
				};
			}
		}
	}

	public sealed class TernaryOperator : Operator
	{
		private ExpressionNode _condition;
		private ExpressionNode _true;
		private ExpressionNode _false;

		public TernaryOperator(Script s)
		{
			_operation = OperatorType.Ter1;
			_script = s;
		}

		public TernaryOperator(Script s, ExpressionNode condition, ExpressionNode @true, ExpressionNode @false) : this(s)
		{
			SetCond(condition);
			SetTrue(@true);
			SetFalse(@false);
		}

		public override object Execute(out ReturnCode ret)
		{
			if (!_isPreEvaluating)
				AcceptExecution();
			var cond = (bool)_condition.Execute(out ret);
			if (Any(ret))
				return null;
			if (cond)
				return _true.Execute(out ret);
			return _false.Execute(out ret);
		}

		public override bool TryPreEvaluate(out ExpressionNode result, out ReturnCode ret)
		{
			_isPreEvaluating = true;
			result = this;
			bool prevCond = _condition.TryPreEvaluate(out _condition, out ret);
			if (Any(ret))
				return false;
			if (!prevCond)
			{
				result = (bool)_condition.Execute(out ret) ? _true : _false;
				return result.TryPreEvaluate(out result, out ret);
			}
			result = this;
			_isPreEvaluating = false;
			return false;
		}

		protected override void CompleteConstruction(Script s)
		{
			base.CompleteConstruction(s);
			_returnType = GetReturnType();
			if (_returnType == null)
				throw new Exception($"Invalid arguments of ternary operator " +
					$"{_condition.ToString()} ? {_true.ToString()} : {_false.ToString()} " +
					$"with types {_condition.ReturnType.Type} ? {_true.ReturnType.Type} : {_false.ReturnType.Type}");
		}

		public void SetCond(ExpressionNode cond)
		{
			if (_condition != null)
				throw new Exception("Unable to reset condition");
			if (cond == null)
				throw new ArgumentNullException();
			_condition = cond;
			if (_true != null && _false != null)
				CompleteConstruction(_script);
		}

		public void SetTrue(ExpressionNode t)
		{
			if (_true != null)
				throw new Exception("Unable to reset true case");
			if (t == null)
				throw new ArgumentNullException();
			_true = t;
			if (_condition != null && _false != null)
				CompleteConstruction(_script);
		}

		public void SetFalse(ExpressionNode f)
		{
			if (_false != null)
				throw new Exception("Unable to reset false case");
			if (f == null)
				throw new ArgumentNullException();
			_false = f;
			if (_true != null && _condition != null)
				CompleteConstruction(_script);
		}

		public override TypeNode GetReturnType() =>
			_condition.ReturnType == TypeNode.Bool && _true.ReturnType == _false.ReturnType ? _true.ReturnType : null;

		protected override float GetComplexity() => 0;

		public override string ToString() => $"ternary ({_condition} ? {_true} : {_false})";
	}

	public abstract class MemberNode<T> : ExpressionNode where T : Member
	{
		protected ExpressionNode _owner;
		protected T _member;

		public override TypeNode GetReturnType() => _member.ReturnType;
	}

	public sealed class ConstructorNode : ExpressionNode
	{
		private readonly Constructor _constructor;
		private readonly ExpressionNode[] _parameters;

		public ConstructorNode(Script s, Constructor constructor, ExpressionNode[] parameters)
		{
			_constructor = constructor;
			_parameters = parameters;
			_returnType = GetReturnType();
			base.CompleteConstruction(s);
		}

		public override object Execute(out ReturnCode ret)
		{
			if (!_isPreEvaluating)
				AcceptExecution();
			ret = ReturnCode.Success;
			var args = new object[_parameters.Length];

			for (int i = 0; i < _parameters.Length; i++)
			{
				args[i] = _parameters[i].Execute(out ret);
				if (args[i] is ReturnCode r1 && r1 == ReturnCode.Error)
					ret |= ReturnCode.Error;
				if (Any(ret))
					return null;
			}
			CheckTypes(args);

			object result = _constructor.RealConstructor.Invoke(args);
			if (result is ReturnCode r && r == ReturnCode.Error)
				ret |= ReturnCode.Error;
			//Console.WriteLine(result);
			return result;
		}

		public override bool TryPreEvaluate(out ExpressionNode result, out ReturnCode ret)
		{
			_isPreEvaluating = true;
			result = this;
			bool preev = true;
			ret = ReturnCode.Success;

			for (int i = 0; i < _parameters.Length; i++)
			{
				preev &= _parameters[i].TryPreEvaluate(out _parameters[i], out ret);
				preev &= !Any(ret);
			}

			_isPreEvaluating = false;
			return !Any(ret);
		}

		private void CheckTypes(object[] args)
		{
			ParameterInfo[] realArgs = _constructor.RealConstructor.GetParameters();
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] is decimal d && realArgs[i].ParameterType == typeof(float))
					args[i] = Convert.ToSingle(d);
				if (args[i] is long l && realArgs[i].ParameterType == typeof(int))
					args[i] = Convert.ToInt32(i);
				//Console.WriteLine(args[i]);
			}
		}

		protected override float GetComplexity() => 0;

		public override TypeNode GetReturnType() => _constructor.ReturnType;

		public override string ToString()
		{
			string s = $"constructor ({_constructor})\nparameters{{";
			for (int i = 0; i < _parameters.Length; i++)
				s += _parameters[i].ToString() + " ";
			return s + " })";
		}
	}

	public sealed class PropertyNode : MemberNode<Property>
	{
		public PropertyNode(Script s, ExpressionNode owner, Property prop)
		{
			base.CompleteConstruction(s);
			_owner = owner;
			_member = prop;
			_returnType = GetReturnType();
		}

		public override object Execute(out ReturnCode ret)
		{
			if (!_isPreEvaluating)
				AcceptExecution();
			object owner = _owner.Execute(out ret);
			if (Any(ret))
				return null;
			var args = new object[] { owner };
			object result = _member.RealMethod.Invoke(null, args);
			if (result is ReturnCode r && r == ReturnCode.Error)
				ret |= ReturnCode.Error;
			return result;
		}

		public override bool TryPreEvaluate(out ExpressionNode result, out ReturnCode ret)
		{
			_isPreEvaluating = true;
			ret = ReturnCode.Success;
			result = this;

			if (_owner.TryPreEvaluate(out _owner, out ReturnCode ret_a))
			{
				object res = Execute(out ret);
				if (Any(ret))
					return false;
				result = new ConstantValue(_script, res);
				return true;
			}
			ret = ret_a;
			_isPreEvaluating = false;
			return Any(ret);
		}

		protected override float GetComplexity() => 0;

		public override string ToString() => $"property (({_owner}) . ({_member}))";
	}

	public sealed class MethodNode : MemberNode<Method>
	{
		private readonly ExpressionNode[] _parameters;

		public MethodNode(Script s, ExpressionNode owner, ExpressionNode[] parameters, Method m)
		{
			base.CompleteConstruction(s);
			_owner = owner;
			_parameters = parameters;
			_member = m;
			_returnType = GetReturnType();
		}

		public override object Execute(out ReturnCode ret)
		{
			if (!_isPreEvaluating)
				AcceptExecution();

			object owner = _owner.Execute(out ret);
			if (Any(ret))
				return null;

			var args = new object[_parameters.Length + 1];
			args[0] = owner;
			for (int i = 1; i <= _parameters.Length; i++)
			{
				args[i] = _parameters[i - 1].Execute(out ret);
				if (args[i] is ReturnCode r1 && r1 == ReturnCode.Error)
					ret |= ReturnCode.Error;
				if (Any(ret))
					return null;
			}

			object result = _member.RealMethod.Invoke(null, args);
			if (result is ReturnCode r && r == ReturnCode.Error)
				ret |= ReturnCode.Error;
			return result;
		}

		public override bool TryPreEvaluate(out ExpressionNode result, out ReturnCode ret)
		{
			_isPreEvaluating = true;
			ret = ReturnCode.Success;
			result = this;

			bool preev = _owner.TryPreEvaluate(out _owner, out ret);
			preev &= !Any(ret);
			var args = new object[_parameters.Length + 1];
			if (!Any(ret))
				args[0] = _owner.Execute(out ret);

			for (int i = 0; i < _parameters.Length; i++)
			{
				preev &= _parameters[i].TryPreEvaluate(out _parameters[i], out ret);
				preev &= !Any(ret);
				if (!Any(ret))
					args[i + 1] = _parameters[i].Execute(out ret);
			}

			if (preev)
			{
				object res = Execute(out ret);
				if (Any(ret))
					return false;
				result = new ConstantValue(_script, res);
				return true;
			}

			_isPreEvaluating = false;
			return Any(ret);
		}

		protected override float GetComplexity() => 0;

		public override string ToString()
		{
			string s = $"method (({_owner}) . ({_member})\nparameters{{";
			for (int i = 0; i<_parameters.Length; i++)
				s += _parameters[i].ToString() + " ";
			return s + " })";
		}
	}

	public sealed class Assignment : CodeNode
	{
		private OperatorType _operation;

		private Variable _lvalue;
		public Variable Lvalue => _lvalue;
		private ExpressionNode _rvalue;
		public ExpressionNode RValue => _rvalue;

		private Assignment(Script s, OperatorType operation)
		{
			if (!Operator.IsBinary(operation))
				throw new Exception($"Invalid operation {operation}");
			_operation = operation;
			base.CompleteConstruction(s);
		}

		public Assignment(Script s, Variable lvalue, ExpressionNode rvalue, OperatorType operation) : this(s, operation)
		{
			SetLValue(lvalue);
			SetRValue(rvalue);
		}

		public override object Execute(out ReturnCode ret)
		{
			AcceptExecution();
			object left = _lvalue.Execute(out ReturnCode ret_l);
			object right = _rvalue.Execute(out ReturnCode ret_r);
			if (Any(ret_l, ret_r))
			{
				ret = ret_l | ret_r;
				return null;
			}
			ret = ReturnCode.Success;

			if (Operator.IsArithmetic(_operation))
				right = BinaryOperator.EvaluateArithmetic(left, right, _operation, _lvalue.ReturnType == TypeNode.Int, _script.Line, out ret);
			else if (Operator.IsLogical(_operation))
				right = BinaryOperator.EvaluateLogical((bool)left, (bool)right, _operation);
			else if (Operator.IsVectorish(_operation))
				right = BinaryOperator.EvaluateVectorish(left, right, _script.Line, _operation);
			else if (right is decimal && _lvalue.ReturnType == TypeNode.Int)
				right = (long)(decimal)right;
			else if (right is long && _lvalue.ReturnType == TypeNode.Float)
				right = (decimal)(long)right;

			if (Any(ret))
				return null;
			_lvalue.Set(right);
			return right;
		}

		private void SetLValue(Variable lvalue)
		{
			if (_lvalue != null)
				throw new Exception("Unable to reset lvalue");
			if (lvalue == null)
				throw new ArgumentNullException();
			_lvalue = lvalue;
			if (_rvalue != null)
				CompleteConstruction(_script);
		}

		private void SetRValue(ExpressionNode rvalue)
		{
			if (_rvalue != null)
				throw new Exception("Unable to reset rvalue");
			if (rvalue == null)
				throw new ArgumentNullException();
			_rvalue = rvalue;
			if (_lvalue != null)
				CompleteConstruction(_script);
		}

		protected override void CompleteConstruction(Script s)
		{
			base.CompleteConstruction(s);
			BinaryOperator.HandleTypeDepended(_lvalue.ReturnType, _rvalue.ReturnType, ref _operation);
			TypeNode returnType = GetReturnType();
			if (returnType == null)
				throw new Exception($"Invalid arguments {_lvalue.ToString()} {_rvalue.ToString()} " +
					$"of assignment operator {_operation} with types {_lvalue.ReturnType.Type} and {_rvalue.ReturnType.Type}");
			_rvalue.TryPreEvaluate(out _rvalue, out ReturnCode ret);
		}

		private TypeNode GetReturnType()
		{
			if (_operation == OperatorType.Assign)
			{
				if (_lvalue.ReturnType == TypeNode.Int)
					return _rvalue.ReturnType == TypeNode.Int || _rvalue.ReturnType == TypeNode.Float ? TypeNode.Int : null;
				return _lvalue.ReturnType == _rvalue.ReturnType ? _lvalue.ReturnType : _rvalue.ReturnType;
			}
			else if (Operator.IsArithmetic(_operation))
			{
				if (!(_lvalue.ReturnType.IsNum && _rvalue.ReturnType.IsNum))
					return null;
				if (_operation == OperatorType.Div)
					return TypeNode.Float;
				if (_operation == OperatorType.DivInt)
					return TypeNode.Int;
				return _lvalue.ReturnType == TypeNode.Float || _rvalue.ReturnType == TypeNode.Float ? TypeNode.Float : TypeNode.Int;
			}
			else if (Operator.IsLogical(_operation))
				return _lvalue.ReturnType == TypeNode.Bool && _rvalue.ReturnType == TypeNode.Bool ? TypeNode.Bool : null;
			else if (Operator.IsVectorish(_operation))
			{
				if (_lvalue.ReturnType != TypeNode.Vector2)
					return null;
				else if (_operation == OperatorType.Vmul || _operation == OperatorType.Vdiv)
					return _rvalue.ReturnType.IsNum ? TypeNode.Vector2 : null;
				else return _rvalue.ReturnType == TypeNode.Vector2 ? TypeNode.Vector2 : null;
			}
			return null;
		}

		protected override float GetComplexity() => 0;

		public override string ToString() => $"Assignment (lvalue: {_lvalue} operation: {_operation} rvalue: {_rvalue})";
	}

	public sealed class SingletoneStatement : Statement
	{
		private StatementType _statementType;
		public override StatementType StatementType => _statementType;
		private SingletoneStatement(StatementType type) { _statementType = type; }

		private static SingletoneStatement _cls = new SingletoneStatement(StatementType.cls);
		private static SingletoneStatement _break = new SingletoneStatement(StatementType.@break);
		private static SingletoneStatement _continue = new SingletoneStatement(StatementType.@continue);

		public static SingletoneStatement CLS => _cls;
		public static SingletoneStatement Break => _break;
		public static SingletoneStatement Continue => _continue;

		public override object Execute(out ReturnCode ret)
		{
			if (_statementType == StatementType.cls)
			{
				ret = ReturnCode.Error;
				return Error("cls evaluated wtf");
			}
			ret = _statementType == StatementType.@break ? ReturnCode.Break : ReturnCode.Continue;
			return null;
		}

		protected override float GetComplexity() => 0;
		public override string ToString() => StatementType.ToString();
	}

	public sealed class IfElseChain : NodeList<Condition>
	{
		public override StatementType StatementType => StatementType.ifelse;
		public bool HasElse { get; private set; }

		public IfElseChain(Script s)
		{
			_script = s;
		}

		public override void Add(Condition c)
		{
			base.Add(c);
			if (c.IsElse)
			{
				HasElse = true;
				CompleteConstruction(_script);
			}
		}

		public override object Execute(out ReturnCode ret)
		{
			//Console.WriteLine(this);
			AcceptExecution();
			for (int i = 0; i < _completed.Length; i++)
				if ((bool)_completed[i].Execute(out ret) || Any(ret))
					return null;
			ret = ReturnCode.Success;
			return null;
		}

		protected override float GetComplexity() => 0;

		protected override string CompletedToString()
		{
			string output = "IfElseChain [\n";
			for (int i = 0; i < _completed.Length; i++)
				output += _completed[i].ToString() + '\n';
			output += "] endchain";
			return output;
		}

		protected override string TempToString()
		{
			string output = "IfElseChain temp [\n";
			for (int i = 0; i < _temp.Count; i++)
				output += _temp[i].ToString() + '\n';
			output += "] endchain";
			return output;
		}
	}

	public sealed class Condition : CodeBlock
	{
		private readonly ExpressionNode _condition;
		public override StatementType StatementType => IsElse ? StatementType.@else : StatementType.@if;

		public Condition(Script s, ExpressionNode condition = null)
		{
			base.CompleteConstruction(s);
			_condition = condition == null ? new ConstantValue(s, true) : condition;
			IsElse = condition == null;
		}

		public override object Execute(out ReturnCode ret)
		{
			AcceptExecution();
			object cond = _condition.Execute(out ret);
			if (Any(ret))
				return null;
			if (!object.Equals(cond, _condition.ReturnType.DefaultValue))
			{
				_statements.Execute(out ret);
				return !Any(ret);
			}
			return false;
		}

		protected override float GetComplexity() => 0;

		public override string ToString() =>
			(IsElse ? "else [" : $"if [({_condition}) then") + $" \n{_statements}\n] cls";

		public bool IsElse { get; private set; }
	}

	public sealed class Switch : NodeList<Case>
	{
		public static object CurrentValue { get; private set; }
		public override StatementType StatementType => StatementType.@switch;
		private ExpressionNode _condition;

		public Switch(Script s, ExpressionNode cond)
		{
			if (cond == null)
				throw null;
			if (!IsSwitchable(cond.ReturnType))
				throw new Exception($"{cond} is not switchable");
			_condition = cond;
			_script = s;
		}

		public override void Add(Case c)
		{
			base.Add(c);
			if (c.IsDefault)
				CompleteConstruction(_script);
		}

		public override object Execute(out ReturnCode ret)
		{
			//Console.WriteLine(this);
			AcceptExecution();
			ret = ReturnCode.Success;
			CurrentValue = _condition?.Execute(out ret);
			if (Any(ret))
				return null;
			for (int i = 0; i < _completed.Length; i++)
				if ((bool)_completed[i].Execute(out ret) || Any(ret))
					return null;
			return null;
		}

		protected override float GetComplexity() => 0;

		protected override string CompletedToString()
		{
			CurrentValue = _condition;
			string output = "switch [\n";
			for (int i = 0; i < _completed.Length; i++)
				output += _completed[i].ToString() + '\n';
			output += "] endswitch";
			return output;
		}

		protected override string TempToString()
		{
			CurrentValue = _condition;
			string output = "switch temp [\n";
			for (int i = 0; i < _temp.Count; i++)
				output += _temp[i].ToString() + '\n';
			output += "] endswitch";
			return output;
		}

		public bool IsSwitchable(TypeNode type) => type.IsNum;
	}

	public sealed class Case : CodeBlock
	{
		private readonly Value[] _condition;
		public override StatementType StatementType => IsDefault ? StatementType.@default : StatementType.@case;

		public Case(Script s, TypeNode type, Value[] condition = null)
		{
			_condition = condition;
			if (_condition != null && type != GetType(condition))
				throw new Exception($"Invalid condition {_condition}");
			base.CompleteConstruction(s);
		}

		private TypeNode GetType(Value[] condition)
		{
			TypeNode type = condition[0].ReturnType;
			for (int i = 1; i < _condition.Length; i++)
				if (type != _condition[i].ReturnType)
					return null;
			return type;
		}

		public override object Execute(out ReturnCode ret)
		{
			AcceptExecution();
			if (EvaluateCondition(Switch.CurrentValue))
			{
				_statements.Execute(out ret);
				return !Any(ret);
			}
			ret = ReturnCode.Success;
			return false;
		}

		private bool EvaluateCondition(object val)
		{
			if (_condition == null)
				return true;
			foreach (var c in _condition)
				if (BinaryOperator.EvaluateRelational(val, c.Get(), OperatorType.Eq))
					return true;
			return false;
		}

		protected override float GetComplexity() => 0;

		public override string ToString() => $"case [({(IsDefault ? ("default") : ConditionToString())}) then\n{_statements}\n] cls";

		private string ConditionToString()
		{
			string result = Switch.CurrentValue.ToString();
			for (int i = 0; i < _condition.Length; i++)
				result += " " + _condition[i].ToString();
			return result;
		}

		public bool IsDefault => _condition == null;
	}

	public sealed class While : CodeBlock
	{
		private readonly ExpressionNode _condition;
		public override StatementType StatementType => IsAlways ? StatementType.always : StatementType.@while;

		public While(Script s, ExpressionNode condition = null)
		{
			base.CompleteConstruction(s);
			_condition = condition == null ? new ConstantValue(s, true) : condition;
			IsAlways = condition == null;
		}

		public override object Execute(out ReturnCode ret)
		{
			AcceptExecution();
			object cond = _condition.Execute(out ret);
			if (Any(ret))
				return null;
			object defval = _condition.ReturnType.DefaultValue;
			while (!object.Equals(cond, defval))
			{
				_statements.Execute(out ret);
				ret &= (ReturnCode)(255 - (byte)ReturnCode.Continue);
				if (Any(ret))
					return null;
				cond = _condition.Execute(out ret);
				if (Any(ret))
					return null;
			}
			return null;
		}

		protected override float GetComplexity() => 0;

		public override string ToString() =>
			(IsAlways ? "always [" : $"while [({_condition}) then") + $" \n{_statements}\n] cls";

		public bool IsAlways { get; private set; }
	}

	public sealed class For : CodeBlock
	{
		private readonly ExpressionNode _condition;
		private readonly Assignment _endAssignment;
		private readonly Assignment _startAssignment;
		public override StatementType StatementType => StatementType.@for;

		public For(Script s, ExpressionNode condition, Assignment endAssignment, Assignment startAssignment = null)
		{
			if (condition == null || endAssignment == null)
				throw null;
			base.CompleteConstruction(s);
			_condition = condition;
			_endAssignment = endAssignment;
			_startAssignment = startAssignment;
		}

		public override object Execute(out ReturnCode ret)
		{
			AcceptExecution();
			if (_startAssignment != null)
			{
				_startAssignment.Execute(out ret);
				if (Any(ret))
					return null;
			}

			object cond = _condition.Execute(out ret);
			if (Any(ret))
				return null;
			object defval = _condition.ReturnType.DefaultValue;

			while (!object.Equals(cond, defval))
			{
				_statements.Execute(out ret);
				ret &= (ReturnCode)(255 - (byte)ReturnCode.Continue);
				if (Any(ret))
					return null;
				
				_endAssignment.Execute(out ret);
				if (Any(ret))
					return null;

				cond = _condition.Execute(out ret);
				if (Any(ret))
					return null;
			}

			return null;
		}

		protected override float GetComplexity() => 0;

		public override string ToString() => $"for [({_condition}) then" + $" \n{_statements}\n] cls";
	}

	[System.Flags]
	public enum ReturnCode : byte
	{
		Success = 1,
		Error = 2,
		Break = 4,
		Continue = 8
	}
}
