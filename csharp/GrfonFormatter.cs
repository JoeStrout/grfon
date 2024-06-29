//GRFON : General Recursive Format Object Notation
//
//	GRFON is a simpler, gentler file format designed to be
//	especially human-editable.  See license at end of file.
//
//	This module (GrfonFormatter) adds optional support for
//	serializing and deserializing C# classes containing
//	simple data types, and marked with [System.Serializable],
//	via the standard System.Runtime.Serialization mechanims.
//	If you don't want to do that, then you can ignore this file.

using System;
using System.Collections;
using System.Reflection;
using System.IO;
using System.Runtime.Serialization;
 
namespace GRFON {
 
	public class GrfonFormatter : IFormatter {
		
		#region Fields
		
		private const string KEY_TYPE = "*type";
		private const string KEY_FULLTYPE = "*assemb-type";
		private const string KEY_VALUE = "value";
		
		private IFormatterConverter _converter = new GrfonFormatterConverter();
		
		#endregion
		
		#region Properties
		
		public bool UseAssemblyQualifiedNames { get; set; }
		public SerializationBinder Binder { get; set; }
		public StreamingContext Context { get; set; }
		public ISurrogateSelector SurrogateSelector { get; set; }
		
		#endregion
		
		#region Methods
		
		public T Deserialize<T>(Stream serializationStream) {
			var result = this.Deserialize(serializationStream);
			try {
				return (T)_converter.Convert(result, typeof(T));
			} catch {
				return default(T);
			}
		}
		
		private object ReadObject(GrfonCollection data) {
			if (data == null) return null;
			
			Type tp = null;
			if (data.ContainsKey(KEY_FULLTYPE)) {
				tp = Type.GetType(data.GetString(KEY_FULLTYPE));
			} else if (data.ContainsKey(KEY_TYPE)) {
				tp = TypeUtil.FindType(data.GetString(KEY_TYPE), true, false);
			}
			
			if (data.ListCount > 0) {
				//we're a list
				if (tp == null) tp = typeof(object);
				Array arr = System.Array.CreateInstance(tp, data.ListCount);
				for(int i = 0; i < arr.Length; i++) {
					arr.SetValue(FromNode(data[i], tp), i);
				}
				return arr;
			} else if (IsDirectGrfonType(tp)) {
				return FromNode(data[KEY_VALUE], tp);
			} else if (tp != null) {
				var surrogate = this.SelectSurrogate(tp);
				var info = new SerializationInfo(tp, _converter);
				
				var e = data.Keys.GetEnumerator();
				while (e.MoveNext()) {
					info.AddValue(e.Current, FromNode(data[e.Current], null));
				}
				
				var obj = FormatterServices.GetUninitializedObject(tp);
				obj = surrogate.SetObjectData(obj, info, this.Context, this.SurrogateSelector);
				return obj;
			} else {
				return null;
			}
		}
		
		private object FromNode(GrfonNode node, System.Type tp) {
			if (node == null) return null;
			
			if (node is GrfonCollection) {
				return this.ReadObject(node as GrfonCollection);
			} else if (node is GrfonValue) {
				return node.ToString();
			}
			
			return null;
		}
		
		private void WriteObject(object graph, GrfonCollection data) {
			var tp = graph.GetType();
			
			if (TypeUtil.IsListType(tp, true)) {
				var lst = graph as IList;
				for(int i = 0; i < lst.Count; i++) {
					data.Add(ToNode(lst[i]));
				}
			} else if (IsDirectGrfonType(tp)) {
				if (this.UseAssemblyQualifiedNames) {
					data[KEY_FULLTYPE] = tp.AssemblyQualifiedName;
				} else {
					data[KEY_TYPE] = tp.FullName;
				}
				data[KEY_VALUE] = ToNode(graph);
			} else {
				if (this.UseAssemblyQualifiedNames) {
					data[KEY_FULLTYPE] = tp.AssemblyQualifiedName;
				} else {
					data[KEY_TYPE] = tp.FullName;
				}
				
				var surrogate = this.SelectSurrogate(tp);
				var info = new SerializationInfo(tp, _converter);
				
				surrogate.GetObjectData(graph, info, this.Context);
				
				var e = info.GetEnumerator();
				while (e.MoveNext()) {
					data[e.Current.Name] = ToNode(e.Current.Value);
				}
			}
		}
		
		private GrfonNode ToNode(object value) {
			//because null nodes aren't even added to grfoncollection, we forcefully add an empty GrfonValue
			if (value == null) return new GrfonValue(string.Empty);
			
			var tp = value.GetType();
			if (IsDirectGrfonType(tp)) {
				return System.Convert.ToString(value);
			} else {
				var subdata = new GrfonCollection();
				this.WriteObject(value, subdata);
				return subdata;
			}
		}
				
		private ISerializationSurrogate SelectSurrogate(System.Type tp) {
			ISerializationSurrogate surrogate = null;
			if (this.SurrogateSelector != null) {
				ISurrogateSelector selector;
				surrogate = this.SurrogateSelector.GetSurrogate(tp, this.Context, out selector);
			}
			if (surrogate == null) surrogate = DefaultSurrogate.Default;
			
			return surrogate;
		}
		
		private static bool IsDirectGrfonType(System.Type tp) {
			return tp != null && (tp == typeof(bool) || tp == typeof(String) || Type.GetTypeCode(tp) >= TypeCode.Boolean);
		}
		
		#endregion
		
		#region IFormatter Interface
		
		public object Deserialize(Stream serializationStream) {
			using (var inp = new GrfonStreamInput(serializationStream)) {
				var des = new GrfonDeserializer(inp);
				var data = des.Parse();
				if (data is GrfonValue) {
					return data.ToString();
				} else if (data is GrfonCollection) {
					return this.ReadObject(data as GrfonCollection);
				} else {
					return null;
				}
			}
		}
		
		public void Serialize(Stream serializationStream, object graph) {
			if (graph == null) throw new System.ArgumentNullException("graph");
			
			var data = new GrfonCollection();
			this.WriteObject(graph, data);
			
			using (var outp = new GrfonStreamOutput(serializationStream)) {
				data.SerializeTo(outp);
			}
		}
		
		#endregion
		
		#region Special Types
		
		private class DefaultSurrogate : ISerializationSurrogate {
			
			private static DefaultSurrogate _default;
			public static DefaultSurrogate Default {
				get {
					if (_default == null) _default = new DefaultSurrogate();
					return _default;
				}
			}
			
			
			public void GetObjectData(object obj, SerializationInfo info, StreamingContext context) {
				if (obj == null) return;
				
				if (obj is ISerializable) {
					(obj as ISerializable).GetObjectData(info, context);
				} else {
					var members = FormatterServices.GetSerializableMembers(obj.GetType(), context);
					var objs = FormatterServices.GetObjectData(obj, members);
					
					for (int i = 0; i < objs.Length; i++) {
						info.AddValue(members[i].Name, objs[i]);
					}
				}
			}
			
			public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector) {
				if (obj is ISerializable) {
					try {
						var tp = obj.GetType();
						var constructor = tp.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(SerializationInfo), typeof(StreamingContext) }, null);
						constructor.Invoke(obj, new object[] { info, context });
					} catch {
					}
					return obj;
				} else {
					var members = FormatterServices.GetSerializableMembers(obj.GetType());
					object[] data = new object[members.Length];
					
					for(int i = 0; i < data.Length; i++) {
						try {
							var mtp = this.GetMemberType(members[i]);
							data[i] = info.GetValue(members[i].Name, mtp);
						} catch {
						}
					}
					
					FormatterServices.PopulateObjectMembers(obj, members, data);
					return obj;
				}
			}
			
			private System.Type GetMemberType(MemberInfo info) {
				if (info == null) return null;
				
				switch (info.MemberType) {
				case MemberTypes.Field:
					return (info as FieldInfo).FieldType;
				case MemberTypes.Property:
					return (info as PropertyInfo).PropertyType;
				case MemberTypes.Method:
					return (info as MethodInfo).ReturnType;
				}
				return null;
			}
			
		}
		
		private class GrfonFormatterConverter : IFormatterConverter {
			public object Convert(object value, TypeCode typeCode) {
				switch(typeCode) {
				case TypeCode.Empty:
					return null;
				case TypeCode.Object:
					return value;
				case TypeCode.DBNull:
					return null;
				case TypeCode.Boolean:
					return this.ToBoolean(value);
				case TypeCode.Char:
					return this.ToChar(value);
				case TypeCode.SByte:
					return this.ToSByte(value);
				case TypeCode.Byte:
					return this.ToByte(value);
				case TypeCode.Int16:
					return this.ToInt16(value);
				case TypeCode.UInt16:
					return this.ToUInt16(value);
				case TypeCode.Int32:
					return this.ToInt32(value);
				case TypeCode.UInt32:
					return this.ToUInt32(value);
				case TypeCode.Int64:
					return this.ToInt64(value);
				case TypeCode.UInt64:
					return this.ToUInt64(value);
				case TypeCode.Single:
					return this.ToSingle(value);
				case TypeCode.Double:
					return this.ToDouble(value);
				case TypeCode.Decimal:
					return this.ToDecimal(value);
				case TypeCode.DateTime:
					return this.ToDateTime(value);
				case TypeCode.String:
					return this.ToString(value);
				default:
					return null;
				}
			}
			
			public object Convert(object value, Type type) {
				if (type == null) return value;
				var tcode = Type.GetTypeCode(type);
				if (tcode != TypeCode.Object) return this.Convert(value, tcode);
				if (value == null) return null;
				
				if (type.IsAssignableFrom(value.GetType())) {
					return value;
				} else if (TypeUtil.IsListType(type, true)) {
					if (value is IList) {
						var etp = TypeUtil.GetElementTypeOfListType(type);
						var olst = value as IList;
						if (type.IsArray) {
							var arr = System.Array.CreateInstance(etp, olst.Count);
							for (int i = 0; i < arr.Length; i++) arr.SetValue(Convert(olst[i], etp), i);
							return arr;
						} else {
							var lst = CreateGenericList(etp);
							for (int i = 0; i < olst.Count; i++) lst.Add(Convert(olst[i], etp));
							return lst;
						}
					} else {
						return null;
					}
				} else {
					return null;
				}
			}
			
			public bool ToBoolean(object value) {
				if (value is bool) return (bool)value;
				if (value is string) {
					var str = (value as string).Trim();
					return !(string.IsNullOrEmpty(str) || str.Equals("false", System.StringComparison.OrdinalIgnoreCase) || str.Equals("0", System.StringComparison.OrdinalIgnoreCase) || str.Equals("off", System.StringComparison.OrdinalIgnoreCase));
				}
				return System.Convert.ToBoolean(value);
			}
			
			public byte ToByte(object value) {
				return System.Convert.ToByte(value);
			}
			
			public char ToChar(object value) {
				return System.Convert.ToChar(value);
			}
			
			public DateTime ToDateTime(object value) {
				return System.Convert.ToDateTime(value);
			}
			
			public decimal ToDecimal(object value) {
				return System.Convert.ToDecimal(value);
			}
			
			public double ToDouble(object value) {
				return System.Convert.ToDouble(value);
			}
			
			public short ToInt16(object value) {
				return System.Convert.ToInt16(value);
			}
			
			public int ToInt32(object value) {
				return System.Convert.ToInt32(value);
			}
			
			public long ToInt64(object value) {
				return System.Convert.ToInt64(value);
			}
			
			public sbyte ToSByte(object value) {
				return System.Convert.ToSByte(value);
			}
			
			public float ToSingle(object value) {
				return System.Convert.ToSingle(value);
			}
			
			public string ToString(object value) {
				return System.Convert.ToString(value);
			}
			
			public ushort ToUInt16(object value) {
				return System.Convert.ToUInt16(value);
			}
			
			public uint ToUInt32(object value) {
				return System.Convert.ToUInt32(value);
			}
			
			public ulong ToUInt64(object value) {
				return System.Convert.ToUInt64(value);
			}
			
			private static IList CreateGenericList(System.Type tp) {
				var listType = typeof(System.Collections.Generic.List<>);
				var constructedListType = listType.MakeGenericType(tp);
				
				return Activator.CreateInstance(constructedListType) as IList;
			}
			
		}
		
		private class TypeUtil {
			public static System.Type FindType(string typeName, bool useFullName = false, bool ignoreCase = false) {
				if (string.IsNullOrEmpty(typeName)) return null;
				
				StringComparison e = (ignoreCase) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				if (useFullName) {
					foreach (var assemb in System.AppDomain.CurrentDomain.GetAssemblies()) {
						foreach (var t in assemb.GetTypes()) {
							if (string.Equals(t.FullName, typeName, e)) return t;
						}
					}
				} else {
					foreach (var assemb in System.AppDomain.CurrentDomain.GetAssemblies()) {
						foreach (var t in assemb.GetTypes()) {
							if (string.Equals(t.Name, typeName, e) || string.Equals(t.FullName, typeName, e)) return t;
						}
					}
				}
				return null;
			}
			
			public static bool IsListType(System.Type tp, bool ignoreAsInterface) {
				if (tp == null) return false;
				
				if (tp.IsArray) return tp.GetArrayRank() == 1;
				
				if (ignoreAsInterface) {
					if (tp.IsGenericType && tp.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>)) return true;
				} else {
					var interfaces = tp.GetInterfaces();
					if (Array.IndexOf(interfaces, typeof(System.Collections.IList)) >= 0 || Array.IndexOf(interfaces, typeof(System.Collections.Generic.IList<>)) >= 0) {
						return true;
					}
				}
				
				return false;
			}
			
			public static System.Type GetElementTypeOfListType(System.Type tp) {
				if (tp == null) return null;
				
				if (tp.IsArray) return tp.GetElementType();
				
				var interfaces = tp.GetInterfaces();
				if (Array.IndexOf(interfaces, typeof(System.Collections.IList)) >= 0 || Array.IndexOf(interfaces, typeof(System.Collections.Generic.IList<>)) >= 0) {
					if (tp.IsGenericType) return tp.GetGenericArguments()[0];
					else return typeof(object);
				}
				
				return null;
			}
		}
		
		#endregion
		
	}
 
}

//	GRFON Source Code License
//
//	Copyright (c) 2015-2016 Joseph J. Strout
//
//	Permission is hereby granted, free of charge, to any person obtaining
//	a copy of this software and associated documentation files (the
//	"Software"), to deal in the Software without restriction, including
//	without limitation the rights to use, copy, modify, merge, publish,
//	distribute, sublicense, and/or sell copies of the Software, and to
//	permit persons to whom the Software is furnished to do so, subject to
//	the following conditions:
//
//	The above copyright notice and this permission notice shall be
//	included in all copies or substantial portions of the Software.
//
//	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//	EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//	MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//	IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//	CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//	TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//	SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
