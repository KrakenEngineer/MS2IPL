using System;
using System.Collections.Generic;

namespace MS2IPL
{
	//Responsible for type recognision & convertation. If you add custom type - don't forget to handle it here
	public static class TypeSystem
	{
		public static TypeNode TypeOf(string original)
		{
			return original switch
			{
				"int" => TypeNode.Int,
				"float" => TypeNode.Float,
				"bool" => TypeNode.Bool,
				"string" => TypeNode.String,
				"vector2" => TypeNode.Vector2,
				_ => null
			};
		}

		public static TypeNode TypeOf(object value)
		{
			if (value == null)
				return null;
			return TypeOf(value.GetType());
		}

		public static TypeNode TypeOf(Type type)
		{
			if (type == typeof(int) || type == typeof(long))
				return TypeNode.Int;
			if (type == typeof(float) || type == typeof(decimal))
				return TypeNode.Float;
			if (type == typeof(bool))
				return TypeNode.Bool;
			if (type == typeof(string))
				return TypeNode.String;
			if (type == typeof(Vector2))
				return TypeNode.Vector2;
			if (type == typeof(STD))
				return TypeNode.STD;
			//if (type == typeof(LogicPart) || type == typeof(Part) || type.IsSubclassOf(typeof(Part)))
			//	return TypeNode.Part;
			//if (type == typeof(Component))
			//	return TypeNode.Component;
			return TypeNode.Object;
		}

		public static object ConvertValue(object value, TypeNode t)
		{
			if (!t.IsValueType)
				Logger.AddMessage($"Invalid type {t} for convertation of the value {value}", Logger.MessageType.RuntimeError);
			return t.Type switch
			{
				DataType.@int => Convert.ToInt64(value),
				DataType.@float => Convert.ToDecimal(value),
				DataType.@bool => Convert.ToBoolean(value),
				DataType.@string => ToString(value),
				_ => null
			};
		}

		public static string ToString(object value) => value?.ToString();
	}

	public class TypeNode
	{
		private readonly DataType _type;
		public DataType Type => _type;

		public static Dictionary<string, TypeNode> s_Tree = PrimaryTypes;
		protected TypeNode(DataType t) => _type = t;

		private static Dictionary<string, TypeNode> PrimaryTypes => new Dictionary<string, TypeNode>
		{
			{ DataType.@object.ToString(), new TypeNode(DataType.@object) },
			{ DataType.@int.ToString(), new TypeNode(DataType.@int) },
			{ DataType.@float.ToString(), new TypeNode(DataType.@float) },
			{ DataType.@bool.ToString(), new TypeNode(DataType.@bool) },
			{ DataType.@string.ToString(), new TypeNode(DataType.@string) },
			{ DataType.vector2.ToString(), new TypeNode(DataType.@vector2) },
			{ DataType.std.ToString(), new TypeNode(DataType.std) }
		};

		public object DefaultValue => _type switch
		{
			DataType.@object => null,
			DataType.@int => 0L,
			DataType.@float => 0m,
			DataType.@bool => false,
			DataType.@string => "",
			DataType.vector2 => new Vector2(),
			DataType.std => MS2IPL.STD.Instance,
			_ => null
		};

		public override string ToString() => _type.ToString();

		public static TypeNode Object => s_Tree[DataType.@object.ToString()];
		public static TypeNode Int => s_Tree[DataType.@int.ToString()];
		public static TypeNode Float => s_Tree[DataType.@float.ToString()];
		public static TypeNode Bool => s_Tree[DataType.@bool.ToString()];
		public static TypeNode String => s_Tree[DataType.@string.ToString()];
		public static TypeNode Vector2 => s_Tree[DataType.vector2.ToString()];
		public static TypeNode STD => s_Tree[DataType.std.ToString()];
		public bool IsNum => this == Int || this == Float;
		public bool IsValueType => IsNum || this == Bool;
	}

	public class CompositeTypeNode : TypeNode
	{
		private readonly TypeNode[] _args;
		public TypeNode[] Args => _args.Clone() as TypeNode[];

		protected CompositeTypeNode(DataType t, TypeNode[] args) : base(t) => _args = args;
	}

	//Represents all MSIPL data types, including system ones
	public enum DataType : byte
	{
		@void, //AKA null
		@object, //system type for methods
		@int,
		@float,
		@bool,
		@string,
		vector2,
		std
	}
}