using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MS2IPL
{
	public static class MemberCollection
	{
		public static void Initialize() { _sets = CollectMembers(); }

		private static Dictionary<TypeNode, MemberSet> _sets;

		private static Dictionary<TypeNode, MemberSet> CollectMembers()
		{
			var sets1 = typeof(MemberCollection).Assembly.GetTypes().Where(t => !t.IsDefined(typeof(PLIgnore)));
			var sets2 = sets1.Select(t => t.GetMethods().
				Where(m => m.IsDefined(typeof(BringToPL)) && m.IsStatic && m.GetParameters().Length > 0)).
				Where(m => m.Any()).Select(m => m.Select(m => FunctionalMember.Create(m)));

			var dict = new Dictionary<TypeNode, MemberSet>();
			foreach (var set in sets2)
				foreach (var member in set)
				{
					if (!dict.ContainsKey(member.OwnerType))
						dict.Add(member.OwnerType, new MemberSet());
					dict[member.OwnerType].Add(member);
				}

			var sets3 = sets1.Select(t => t.GetConstructors().Where(c => c.IsDefined(typeof(PLConstructor)))).
				Where(c => c.Any()).Select(c => c.Select(c => new Constructor(c)));
			foreach (var set in sets3)
				foreach (var member in set)
				{
					if (!dict.ContainsKey(member.OwnerType))
						dict.Add(member.OwnerType, new MemberSet());
					dict[member.OwnerType].Add(member);
				}

			return dict;
		}

		public static bool TryFindConstructor(TypeNode ownerType, ExpressionNode[] args, out Constructor c)
		{
			if (_sets.ContainsKey(ownerType))
				return _sets[ownerType].TryFindConstructor(args, out c);
			c = null;
			return false;
		}

		public static bool TryFindMember(TypeNode ownerType, string name, out FunctionalMember m)
		{
			if (_sets.ContainsKey(ownerType))
				return _sets[ownerType].TryFindMember(name, out m);
			m = null;
			return false;
		}

		public static string ToString()
		{
			string s = "member collection [\n";
			foreach (var set in _sets)
				s += set.ToString() + '\n';
			return s + "]";
		}
	}

	public sealed class MemberSet
	{
		private readonly Dictionary<string, FunctionalMember> _members = new Dictionary<string, FunctionalMember>();
		private readonly List<Constructor> _constructors = new List<Constructor>();

		public MemberSet() { }

		public MemberSet(IEnumerable<Member> members)
		{
			foreach (var member in members)
				Add(member);
		}

		public bool Empty => _members.Count == 0;

		public void Add(Member m)
		{
			if (m is FunctionalMember f)
				_members.Add(f.RealMethod.Name, f);
			else _constructors.Add((Constructor)m);
		}

		public bool TryFindConstructor(ExpressionNode[] args, out Constructor ret)
		{
			var constructors = _constructors.Where(c => c.Parameters.Length == args.Length);
			foreach (var c in constructors)
			{
				bool match = true;
				for (int i = 0; i < args.Length; i++)
					if (args[i].ReturnType != c.Parameters[i].Type)
					{
						match = false;
						break;
					}
				if (match)
				{
					ret = c;
					return true;
				}
			}
			ret = null;
			return false;
		}

		public bool TryFindMember(string name, out FunctionalMember m)
		{
			m = _members?[name];
			return m != null;
		}

		public override string ToString()
		{
			string s = "member set [\n";
			foreach (var members in _members)
				s += members.ToString() + '\n';
			foreach (var member in _constructors)
				s += member.ToString() + "\n";
			return s + "]";
		}
	}

	public abstract class Member
	{
		public abstract TypeNode OwnerType { get; }
		public abstract TypeNode ReturnType { get; }
		public abstract override string ToString();
	}

	public sealed class Constructor : Member, IParameters
	{
		public override TypeNode OwnerType => _ownerType;
		public override TypeNode ReturnType => _ownerType;
		public ConstructorInfo RealConstructor => _realConstructor;
		public Parameter[] Parameters => _parameters;

		private readonly TypeNode _ownerType;
		private readonly ConstructorInfo _realConstructor;

		private readonly Parameter[] _parameters;

		internal Constructor(ConstructorInfo c)
		{
			_realConstructor = c;
			ParameterInfo[] realParams = c.GetParameters();
			_parameters = new Parameter[realParams.Length];
			for (int i = 0; i < realParams.Length; i++)
			{
				ParameterInfo p = realParams[i];
				var attributes = p.Attributes == ParameterAttributes.Out;
				_parameters[i] = new Parameter(TypeSystem.TypeOf(p.ParameterType), p.Name, attributes);
			}
			_ownerType = TypeSystem.TypeOf(c.DeclaringType);
		}

		private Constructor(TypeNode ownerType, ConstructorInfo realConstructor, Parameter[] parameters)
		{
			_ownerType = ownerType;
			_realConstructor = realConstructor;
			_parameters = parameters;
		}

		protected TypeNode GetReturnType() => OwnerType;

		public override string ToString()
		{
			string s = $"constructor (for: {_ownerType}\nparameters:";
			for (int i = 0; i < _parameters.Length; i++)
				s += "\n" + _parameters[i];
			return s + ')';
		}
	}

	public abstract class FunctionalMember : Member
	{
		public abstract MethodInfo RealMethod { get; }

		public static Member Create(MethodInfo m)
		{
			var attribute = m.GetCustomAttribute<BringToPL>();
			ParameterInfo[] realParams = m.GetParameters();
			TypeNode ownerType = TypeSystem.TypeOf(realParams[0].ParameterType);
			if (!attribute.IsMethod)
			{
				if (realParams.Length != 1)
					throw new Exception($"method {m.Name} doesn't describe a property");
				return new Property(ownerType, m, TypeSystem.TypeOf(attribute.ReturnType));
			}

			var parameters = new Parameter[realParams.Length - 1];
			for (int i = 1; i < realParams.Length; i++)
			{
				ParameterInfo p = realParams[i];
				var attributes = p.Attributes == ParameterAttributes.Out;
				parameters[i - 1] = new Parameter(TypeSystem.TypeOf(p.ParameterType), p.Name, attributes);
			}
			return new Method(ownerType, m, parameters, TypeSystem.TypeOf(attribute.ReturnType));
		}

		protected TypeNode GetReturnType() => TypeSystem.TypeOf(RealMethod.ReturnType);
	}

	public sealed class Property : FunctionalMember
	{
		public override TypeNode OwnerType => _ownerType;
		public override MethodInfo RealMethod => _realMethod;
		public override TypeNode ReturnType => _returnType;


		private readonly TypeNode _ownerType;
		private readonly MethodInfo _realMethod;
		private readonly TypeNode _returnType;

		internal Property(TypeNode ownerType, MethodInfo realMethod, TypeNode returnType)
		{
			_ownerType = ownerType;
			_realMethod = realMethod;
			_returnType = returnType == null ? GetReturnType() : returnType;
		}

		public override string ToString() => $"property (for: {_ownerType} real method: {_realMethod} returns {_returnType})";
	}

	public interface IParameters
	{
		public abstract TypeNode ReturnType { get; }
		public Parameter[] Parameters { get; }
	}

	public interface IFunction : IParameters
	{
		public abstract MethodInfo RealMethod { get; }
		protected TypeNode GetReturnType(TypeNode ownerType) => TypeSystem.TypeOf(RealMethod.ReturnType);
	}

	public sealed class Method : FunctionalMember, IFunction
	{
		public override TypeNode OwnerType => _ownerType;
		public override MethodInfo RealMethod => _realMethod;
		public override TypeNode ReturnType => _returnType;
		public Parameter[] Parameters => _parameters;

		private readonly TypeNode _ownerType;
		private readonly MethodInfo _realMethod;
		private readonly TypeNode _returnType;
		private readonly Parameter[] _parameters;

		internal Method(TypeNode ownerType, MethodInfo realMethod, Parameter[] parameters, TypeNode returnType)
		{
			_ownerType = ownerType;
			_realMethod = realMethod;
			_returnType = returnType == null ? GetReturnType() : returnType;
			_parameters = parameters;
		}

		public override string ToString()
		{
			string s = $"method (for: {_ownerType} real method: {_realMethod} returns {_returnType}\nparameters:";
			for (int i = 0; i < _parameters.Length; i++)
				s += "\n" + _parameters[i];
			return s + ')';
		}
	}

	public sealed class Parameter
	{
		public readonly TypeNode Type;
		public readonly string Name;
		public readonly bool IsOutput;

		public Parameter(TypeNode type, string name, bool isOutput = false)
		{
			Type = type;
			Name = name;
			IsOutput = isOutput;
		}

		public override string ToString() => $"parameter (type: {Type} name: {Name} is output: {IsOutput}";
	}

	public static class BIMethods
	{
		[BringToPL(false)]
		public static long len(string s) => s.Length;

		[BringToPL(true)]
		public static string get(string s, long i) => $"{s[(int)i]}";

		[BringToPL(false)]
		public static decimal sqrMagnitude(Vector2 v) => (decimal)v.sqrMagnitude;

		[BringToPL(false)]
		public static decimal magnitude(Vector2 v) => (decimal)v.magnitude;

		[BringToPL(false)]
		public static Vector2 normalized(Vector2 v) => v.normalized;

		[BringToPL(false)]
		public static Vector2 prependicular(Vector2 v) => v.perpendicular;
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class BringToPL : Attribute
	{
		public bool IsMethod;
		public string ReturnType;

		public BringToPL(bool isMethod)
		{
			IsMethod = isMethod;
			ReturnType = null;
		}

		public BringToPL(bool isMethod, string returnType)
		{
			IsMethod = isMethod;
			ReturnType = returnType;
		}
	}

	[AttributeUsage(AttributeTargets.Constructor)]
	public class PLConstructor : Attribute { }

	[AttributeUsage(AttributeTargets.Class)]
	public class PLIgnore : Attribute { }
}
