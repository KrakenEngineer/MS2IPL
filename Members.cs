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
			var sets1 = typeof(MemberCollection).Assembly.GetTypes();
			var sets2 = sets1.Select(t => t.GetMethods().Where(m => m.IsDefined(typeof(BringToPL))));
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

		public static bool TryFindMember(TypeNode ownerType, string name, out Member m) => _sets[ownerType].FindMember(name, out m);

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

		public void Add(Member m) => _members.Add(m.Method.Name, m);

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
		public abstract MethodInfo Method { get; }

		public static Member Create(MethodInfo m)
		{
			var attribute = m.GetCustomAttribute<BringToPL>();
			if (attribute.IsMethod)
				throw new NotImplementedException();
			return new Property(TypeSystem.TypeOf(m.GetParameters()[0].ParameterType), m);
		}

		protected abstract TypeNode GetReturnType(TypeNode ownerType);

		public abstract override string ToString();
	}

	public sealed class Property : Member
	{
		public override TypeNode OwnerType => _ownerType;
		public override MethodInfo Method => _method;
		public override TypeNode ReturnType => _returnType;


		private readonly TypeNode _ownerType;
		private readonly MethodInfo _method;
		private readonly TypeNode _returnType;

		public Property(TypeNode ownerType, MethodInfo method)
		{
			_ownerType = ownerType;
			_method = method;
			_returnType = GetReturnType(ownerType);
		}

		protected override TypeNode GetReturnType(TypeNode ownerType)
		{
			return TypeSystem.TypeOf(_method.ReturnType);
		}

		public override string ToString() => $"property (for: {OwnerType} method: {Method})";
	}

	public static class BIMethods
	{
		[BringToPL(false)]
		public static long len(string s) => s.Length;
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class BringToPL : Attribute
	{
		public bool IsMethod;

		public BringToPL(bool isMethod)
		{
			IsMethod = isMethod;
		}
	}
}
