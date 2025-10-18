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
				Where(m => m.IsDefined(typeof(BringToPL)) && m.IsStatic && m.GetParameters().Length > 0));
			var sets3 = sets2.Where(m => m.Count() > 0);
			var sets4 = sets3.Select(m1 => m1.Select(m => Member.Create(m)));

			var dict = new Dictionary<TypeNode, MemberSet>();
			foreach (var set in sets4)
			{
				foreach (var member in set)
				{
					if (!dict.ContainsKey(member.OwnerType))
						dict.Add(member.OwnerType, new MemberSet());
					dict[member.OwnerType].Add(member);
				}
			}
			return dict;
		}

		public static bool TryFindMember(TypeNode ownerType, string name, out Member m)
		{
			if (_sets.ContainsKey(ownerType))
				return _sets[ownerType].FindMember(name, out m);
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
		private readonly Dictionary<string, Member> _members = new Dictionary<string, Member>();

		public MemberSet() { }

		public MemberSet(IEnumerable<Member> members)
		{
			foreach (var member in members)
				Add(member);
		}

		public bool Empty => _members.Count == 0;

		public void Add(Member m) => _members.Add(m.RealMethod.Name, m);

		public bool FindMember(string name, out Member m)
		{
			m = _members?[name];
			return m != null;
		}

		public override string ToString()
		{
			string s = "member set [\n";
			foreach (var members in _members)
				s += members.ToString() + '\n';
			return s + "]";
		}
	}

	public abstract class Member
	{
		public abstract TypeNode OwnerType { get; }
		public abstract TypeNode ReturnType { get; }
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

		public abstract override string ToString();
	}

	public sealed class Property : Member
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

		public override string ToString() => $"property (for: {OwnerType} real method: {RealMethod} returns {ReturnType})";
	}

	public interface IFunction
	{
		public abstract TypeNode ReturnType { get; }
		public abstract MethodInfo RealMethod { get; }
		public Parameter[] Parameters { get; }

		protected TypeNode GetReturnType(TypeNode ownerType) => TypeSystem.TypeOf(RealMethod.ReturnType);
	}

	public sealed class Method : Member, IFunction
	{
		public override TypeNode OwnerType => _ownerType;
		public override MethodInfo RealMethod => _method;
		public override TypeNode ReturnType => _returnType;
		public Parameter[] Parameters => _parameters;

		private readonly TypeNode _ownerType;
		private readonly MethodInfo _method;
		private readonly TypeNode _returnType;
		private readonly Parameter[] _parameters;

		internal Method(TypeNode ownerType, MethodInfo method, Parameter[] parameters, TypeNode returnType)
		{
			_ownerType = ownerType;
			_method = method;
			_returnType = returnType == null ? GetReturnType() : returnType;
			_parameters = parameters;
		}

		public override string ToString()
		{
			string s = $"method (for: {OwnerType} real method: {RealMethod} returns {ReturnType}\nparameters:";
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

	[AttributeUsage(AttributeTargets.Class)]
	public class PLIgnore : Attribute { }
}
