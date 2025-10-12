using System.Collections;
using System.Collections.Generic;

namespace MS2IPL
{
    //Allows to add/get/set variables and get their properties
    public sealed class VariableTable : IEnumerable<KeyValuePair<string, Variable>>
    {
        public readonly int _maxCount;
        public readonly Dictionary<string, Variable> s_Variables =
            new Dictionary<string, Variable>();

        public VariableTable(int maxCount = int.MaxValue)
        {
            if (maxCount <= 0)
                Logger.AddMessage($"Invalid max variables count at constructor of {nameof(MS2IPL)}.{nameof(VariableTable)}", Logger.MessageType.Debug);
            _maxCount = maxCount;
			TryAdd(Variable.STD);
        }

		public VariableTable(VariableTable variables)
		{
			_maxCount = variables._maxCount;
			foreach (var v in variables)
				TryAdd(new Variable(v.Value.Script, v.Value));
		}

        public bool Exists(string name) => s_Variables.ContainsKey(name);
        public int Count => s_Variables.Count;
		public bool Empty => Count == 0;

		public object GetValue(string name)
        {
            if (Exists(name))
                return s_Variables[name].Get();
            Logger.AddMessage($"Variable {name} doesn't exist at {nameof(MS2IPL)}.{nameof(VariableTable)}.{nameof(GetValue)}", Logger.MessageType.RuntimeError);
            return null;
        }

        public Variable GetPointer(string name)
        {
            if (Exists(name))
                return s_Variables[name];
            Logger.AddMessage($"Variable {name} doesn't exist at {nameof(MS2IPL)}.{nameof(VariableTable)}.{nameof(GetPointer)}", Logger.MessageType.RuntimeError);
            return null;
        }

        public void SetValue(string name, object value)
        {
            if (!Exists(name))
                Logger.AddMessage($"Variable {name} doesn't exist at {nameof(MS2IPL)}.{nameof(VariableTable)}.{nameof(SetValue)}", Logger.MessageType.RuntimeError);
            else
                s_Variables[name].Set(value);
        }

        public bool TryAdd(Variable variable)
        {
            if (variable == null || Exists(variable.Name))
                return false;

            if (Count < _maxCount)
				s_Variables.Add(variable.Name, variable);
            return Count <= _maxCount;
        }

        public override string ToString()
        {
			if (Empty)
				return "No variables";

            string output = "";
            foreach (var item in s_Variables)
                output += item.Value.ToString() + '\n';

            return output;
        }

		public IEnumerator<KeyValuePair<string, Variable>> GetEnumerator() => s_Variables.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => s_Variables.GetEnumerator();
	}

    public sealed class Variable : Value
    {
        private readonly string _name;
        private object _value;

        public Variable(Script s, Variable var)
        {
            _name = var._name;
            _value = var._value;
			_returnType = GetReturnType();
        }

        private Variable(Script s, string name, object value)
        {
            _name = name;
            _value = value;
			_returnType = GetReturnType();
        }

        //public static Variable Component(string name, Component value) =>
        //    new Variable(name, value);

		/// <summary>
		/// ONLY FOR Lexer.cs
		/// </summary>
        public static Variable Untyped(Script s, string name) => new Variable(s, name, null);

		public void SetType(TypeNode type)
		{
			_value = type.DefaultValue;
			CompleteConstruction(_script);
		}

        public override object Get() => _value;
        public void Set(object value)
        {
            if (TypeSystem.TypeOf(value) != _returnType)
				Error($"Invalid value at {nameof(MS2IPL)}.{nameof(VariableTable)}.{nameof(Set)}");
            _value = value;
        }

        /// <summary>
		/// WARNING: USE THIS METHOD ONLY IF YOU ARE SURE THAT YOU NEED IT
		/// </summary>
        public void Clear()
        {
            //if (_value is LogicPart p) p.RemoveUsing();
            //if (_value is Component c) c.RemoveUsing();
            _value = null;
        }

        public string Name => _name;

		private static Variable _std = new Variable(null, "std", MS2IPL.STD.Instance);
		public static Variable STD => _std;

		//public bool IsNull => _value == null || (_value is LogicPart p && p.Part == null) ||
		//     (_value is Component c && (c.Part == null || c.Get() == null || c.Part == null || c.Part.Part == null));

		#region expression node
		public override string ToString() => $"variable (type: {_returnType} name: {_name} value: {_value})";
		protected override void CompleteConstruction(Script s)
		{
			base.CompleteConstruction(s);
			_returnType = GetReturnType();
		}
		public override object Execute(out ReturnCode ret) { ret = ReturnCode.Success; return Get();}
		protected override float GetComplexity() => 0;
		public override TypeNode GetReturnType() => TypeSystem.TypeOf(_value);
		public override bool TryPreEvaluate(out ExpressionNode result, out ReturnCode ret)
		{
			ret = ReturnCode.Success;
			result = this;
			return false;
		}
		#endregion
	}
}
