// GRFON : General Recursive Format Object Notation
//
//	GRFON is a simpler, gentler file format designed to be
//	especially human-editable.  See license at end of file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GRFON {
	
	//======================================================================
	#region GrfonOutput
	
	/// <summary>
	/// GrfonOutput is an abstract class used to output GRFON data
	/// to some arbitrary output stream.
	/// </summary>
	public abstract class GrfonOutput {
		
		protected bool lastLineTerminated = true;
		protected int indentLevel = 0;
		protected string indentStr = "";
		
		/// <summary>
		/// Get/set the indentation level.
		/// </summary>
		public int indent {
			get {
				return indentLevel;
			}
			set {
				indentLevel = value;
				indentStr = new string('\t', indentLevel);
			}
		}
		
		/// <summary>
		/// Print the specified line to the output, and optionally terminate the line
		/// (so that the next Print starts on a new, indented line).
		/// </summary>
		/// <param name="line">Text to print.</param>
		/// <param name="terminateLine">If set to <c>true</c> (the default), terminate the line.</param>
		/// <returns>Line number of the line printed.</returns>
		public abstract int Print(string line, bool terminateLine=true);

		/// <summary>
		/// Get the line number that would be used for the next line to be printed.
		/// </summary>
		/// <returns>The line number.</returns>
		public abstract int NextLineNum();

		/// <summary>
		/// Escape any special characters in the given string by preceeding
		/// them with backslashes.  In addition: a newline becomes \n; a
		/// carriage return becomes \r; and a tab becomes \t.
		/// </summary>
		/// <param name="str">String.</param>
		public static string Escape(string str) {
			if (str == null) return null;
			// Start by counting how many characters we need to escape.
			int escapeCount = 0;
			foreach (char c in str) {
				if (c == ';' || c == '\\' || c == '\n' || c == '\r' || c == '\t') escapeCount++;
			}
			if (escapeCount == 0) return str;

			// Now, allocate a buffer big enough for all the escaping, and loop again,
			// escaping as we go.
			char[] buffer = new char[str.Length + escapeCount];
			int idx = 0;
			foreach (char c in str) {
				switch (c) {
				case ';':
				case '\\':
					buffer[idx++] = '\\';
					break;
				case '\n':
					buffer[idx++] = '\\';
					buffer[idx++] = 'n';
					continue;
				case '\r':
					buffer[idx++] = '\\';
					buffer[idx++] = 'r';
					continue;
				case '\t':
					buffer[idx++] = '\\';
					buffer[idx++] = 't';
					continue;
				}
				buffer[idx++] = c;
			}
			return new string(buffer, 0, idx);
		}
	}

	/// <summary>
	/// This is a GRFON output class that writes to a List<string> in memory,
	/// and then provides ways to retrieve or print that list, or even pipe
	/// it directly to a GrfonInput for parsing.
	/// </summary>
	public class GrfonBufferOutput : GrfonOutput {
		List<string> lines = new List<string>();
		
		/// <summary>
		/// Get the list of strings that contain the current output.
		/// </summary>
		public List<string> Lines {
			get { return lines; }
		}
		
		/// <summary>
		/// Get the current output as a single string, by joining lines
		/// with the given delimiter.
		/// </summary>
		/// <param name="lineDelimiter">Line separator to use.</param>
		/// <returns>Current output, as one big string.</returns>
		public string Text(string lineDelimiter="\n") {
			return string.Join(lineDelimiter, lines.ToArray());
		}
		
		/// <summary>
		/// Print the specified line to the output, and optionally terminate the line
		/// (so that the next Print starts on a new, indented line).
		/// </summary>
		/// <param name="line">Text to print.</param>
		/// <param name="terminateLine">If set to <c>true</c> (the default), terminate the line.</param>
		/// <returns>Line number of the line printed.</returns>
		public override int Print(string line, bool terminateLine=true) {
			if (lastLineTerminated) {
				lines.Add(indentStr + line);
			} else {
				lines[lines.Count-1] += line;
			}
			lastLineTerminated = terminateLine;
			return lines.Count - 1;
		}

		/// <summary>
		/// Get the line number that would be used for the next line to be printed.
		/// </summary>
		/// <returns>The line number.</returns>
		public override int NextLineNum() {
			return lines.Count;
		}
		
		/// <summary>
		/// Dump our output to System.Console.WriteLine, for debugging purposes.
		/// </summary>
		/// <param name="includeLineNumbers">whether to prefix each line with a line number</param>
		public void DumpToConsole(bool includeLineNumbers = false) {
			int lineNum = 0;
			foreach (string line in lines) {
				if (includeLineNumbers) Console.Write("{0:000}  ", lineNum);
				Console.WriteLine(line);
				lineNum++;
			}
		}
		
		/// <summary>
		/// Return a new GrfonInput with the lines of our current output.
		/// This is handy in unit testing.
		/// </summary>
		/// <returns></returns>
		public GrfonInput DumpToInput() {
			return new GrfonStringInput(lines);
		}
	}
	
	/// <summary>
	/// This is a concrete GrfonOutput subclass that writes to an IO stream.
	/// It's the primary way most apps will write GRFON files.
	/// </summary>
	public class GrfonStreamOutput : GrfonOutput, IDisposable {
		System.IO.StreamWriter stream;
		bool ownsStream;
		int lineNum = 0;
		
		/// <summary>
		/// Construct a GrfonStreamOutput with an IO.StreamWriter.
		/// In this case, the GrfonStreamOutput does NOT own the stream,
		/// and will not dispose of it.  That's your responsibility.
		/// </summary>
		/// <param name="stream">StreamWriter to write to.</param>
		/// <returns></returns>
		public GrfonStreamOutput(System.IO.StreamWriter stream) {
			this.stream = stream;
			ownsStream = false;
		}
		
		/// <summary>
		/// Construct a GrfonStreamOutput with an IO.Stream.  In this case,
		/// the GrfonStreamOutput owns the given stream, and will dispose
		/// of it automatically when the GrfonStreamOutput is disposed.
		/// </summary>
		/// <param name="stream"></param>
		/// <returns></returns>
		public GrfonStreamOutput(System.IO.Stream stream) {
			// Careful!  If you pass in a stream, then this takes ownership
			// of it.  This is, sadly, unavoidable in Unity, though if we
			// were using .NET 4.5 or later, we could use the new leaveOpen
			// parameter to StreamWriter to control this behavior.
			this.stream = new System.IO.StreamWriter(stream,
			                                         System.Text.Encoding.UTF8);
			ownsStream = true;
		}
		
		/// <summary>
		/// Print the specified line to the output, and optionally terminate the line
		/// (so that the next Print starts on a new, indented line).
		/// </summary>
		/// <param name="line">Text to print.</param>
		/// <param name="terminateLine">If set to <c>true</c> (the default), terminate the line.</param>
		/// <returns>Line number of the line printed.</returns>
		public override int Print(string line, bool terminateLine=true) {
			if (lastLineTerminated) stream.Write(indentStr);
			if (terminateLine) {
				stream.WriteLine(line);
				lastLineTerminated = true;
				return lineNum++;
			}
			stream.Write(line);
			lastLineTerminated = false;
			return lineNum;
		}

		/// <summary>
		/// Get the line number that would be used for the next line to be printed.
		/// </summary>
		/// <returns>The line number.</returns>
		public override int NextLineNum() {
			return lineNum;
		}
		
		/// <summary>
		/// Dispose of our stream, if we own it.  (That depends on how this
		/// object was constructed; see the constructors above.)
		/// </summary>
		public void Dispose() {
			if (stream != null && ownsStream) {
				stream.Dispose();
				stream = null;
			}
		}
	}
	
	#endregion
	//======================================================================
	#region GrfonInput
	
	/// <summary>
	/// GrfonInput represents a source of GRFON data.  This is an
	/// abstract base class.
	/// </summary>
	public abstract class GrfonInput {
		
		/// <summary>
		/// Return whether we are at the end of our input stream.
		/// </summary>
		public abstract bool EOF { get; }
		
		/// <summary>
		/// Get the current line number within our input.
		/// </summary>
		public abstract int LineNum { get; }
		
		/// <summary>
		/// Read and return the next line of input.
		/// </summary>
		/// <returns></returns>
		public abstract string ReadLine();
	}
	
	/// <summary>
	/// Concrete GrfonInput subclass that reads a list of strings
	/// (or one big string, which is split into a list internally).
	/// </summary>
	public class GrfonStringInput : GrfonInput {
		List<string> lines;
		int lineNum;
		
		public GrfonStringInput(List<string> lines) {
			this.lines = lines;
		}
		
		public GrfonStringInput(string text) {
			this.lines = new List<string>(
				text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None));
		}
		
		public GrfonStringInput(System.IO.Stream stream) {
			lines = new List<string>();
			if (stream != null) {
				using (System.IO.StreamReader reader = new System.IO.StreamReader(stream)) {
					while (!reader.EndOfStream) {
						lines.Add(reader.ReadLine());
					}
				}
			}
		}
		
		/// <summary>
		/// Return whether we are at the end of our input stream.
		/// </summary>
		public override bool EOF {
			get {
				return lineNum >= lines.Count;
			}
		}
		
		/// <summary>
		/// Get the current line number within our input.
		/// </summary>
		public override int LineNum {
			get { return lineNum; }
		}
		
		/// <summary>
		/// Read and return the next line of input.
		/// </summary>
		/// <returns></returns>
		public override string ReadLine() {
			return lines[lineNum++];
		}
	}
	
	/// <summary>
	/// Concrete GrfonInput subclass that reads a System.IO.Stream.
	/// (Unlike GrfonStringInput, which if given a stream reads the 
	/// entire contents into memory right away, this one reads from
	/// the stream as it goes, and so may be better for large files.)
	/// </summary>
	public class GrfonStreamInput : GrfonInput, IDisposable {
		System.IO.StreamReader reader;
		int lineNum;
		
		/// <summary>
		/// Construct a GrfonStreamInput with an IO.Stream (which we adopt and
		/// dispose of).  Note that the argument may be null, in which case we
		/// simply report EOF right away.
		/// </summary>
		/// <param name="stream">stream to read from</param>
		public GrfonStreamInput(System.IO.Stream stream) {
			reader = stream == null ? null : new System.IO.StreamReader(stream);
		}
		
		/// <summary>
		/// Return whether we are at the end of our input stream.
		/// </summary>
		public override bool EOF {
			get {
				return reader == null || reader.EndOfStream;
			}
		}
		
		/// <summary>
		/// Get the current line number within our input.
		/// </summary>
		public override int LineNum {
			get { return lineNum; }
		}
		
		/// <summary>
		/// Read and return the next line of input.
		/// </summary>
		/// <returns></returns>
		public override string ReadLine() {
			lineNum++;
			return reader.ReadLine();
		}
		
		/// <summary>
		/// Dispose of our stream reader.
		/// </summary>
		public void Dispose() {
			if (reader != null) {
				reader.Dispose();
				reader = null;
			}
		}
	}
	
	#endregion
	//======================================================================
	#region GrfonNode and subclasses
	
	/// <summary>
	/// GrfonNode is an abstract class for anything that can be part
	/// of a GRFON data hierarchy -- i.e., a value or collection.
	/// </summary>
	public abstract class GrfonNode {
		/// <summary>
		/// Keeps track of which line in the source file this node started on.
		/// (You do not need to worry about this when writing files.)
		/// </summary>
		public int lineNum;

		/// <summary>
		/// Factory method that makes it really easy to parse GRFON data in a string
		/// into a GRFON node (usually, a GrfonCollection, though it could be just a value).
		/// To use, simply do:
		/// 		GrfonNode result = GrfonNode.FromString(someData);
		/// </summary>
		/// <param name="grfonData">GRFON data, in string form.</param>
		/// <returns>The GrfonCollection or other node parsed from the given data.</returns>
		public static GrfonNode FromString(string grfonData) {
			GrfonDeserializer des = new GrfonDeserializer(new GrfonStringInput(grfonData));
			return des.Parse();
		}

		/// <summary>
		/// Serialize this node to the given output.  This is the primary
		/// output method used for writing GRFON data.  Optional parameters
		/// let you specify whether this is the top-level (document) node,
		/// and whether you want a more compact format (where applicable).
		/// </summary>
		/// <param name="output">GrfonOutput to write to</param>
		/// <param name="isDocument">whether this is the top-level (document) node</param>
		/// <param name="compact">whether you want a more compact format if possible</param>
		public abstract void SerializeTo(GrfonOutput output, bool isDocument=true, bool compact=false);
		
		/// <summary>
		/// Implicit conversion of a string to a GrfonNode.
		/// </summary>
		public static implicit operator GrfonNode(string value) {
			return new GrfonValue(value);
		}
		
		/// <summary>
		/// Implicit conversion of an int to a GrfonNode.
		/// </summary>
		public static implicit operator GrfonNode(int value) {
			return new GrfonValue(value.ToString(CultureInfo.InvariantCulture));
		}
		
		/// <summary>
		/// Implicit conversion of a bool to a GrfonNode.
		/// </summary>
		public static implicit operator GrfonNode(bool value) {
			return new GrfonValue(value ? "true" : "false");
		}
		
		
		/// <summary>
		/// Implicit conversion of a float to a GrfonNode.
		/// </summary>
		public static implicit operator GrfonNode(float value) {
			return new GrfonValue(value.ToString(CultureInfo.InvariantCulture));
		}

		/// <summary>
		/// Explicit conversion to an int; i.e., get the int value of this node.
		/// Subclasses will override this to provide a more useful value.
		/// </summary>
		/// <returns>int value of this node</returns>
		public virtual int ToInt(int defaultValue = 0) {
			return 0;
		}
		
		/// <summary>
		/// Explicit conversion to a bool; i.e., get the bool value of this node.
		/// Subclasses will override this to provide a more useful value.
		/// </summary>
		/// <returns>int value of this node</returns>
		public virtual bool ToBool() {
			return false;
		}
		
		/// <summary>
		/// Explicit conversion to a float; i.e., get the float value of this node.
		/// Subclasses will override this to provide a more useful value.
		/// </summary>
		/// <returns>float value of this node</returns>
		public virtual float ToFloat() {
			return 0f;
		}
	}
	
	/// <summary>
	/// GrfonValue represents a leaf node, that is, an atomic bit of data.
	/// The value is in fact always stored as a string, though we have 
	/// convenience methods to convert to/from ints and floats.
	/// </summary>
	public class GrfonValue : GrfonNode {
		/// <summary>
		/// The actual string value of this node.
		/// </summary>
		public string value;
		
		/// <summary>
		/// Construct a GrfonValue from a string.
		/// </summary>
		/// <param name="value">value to store</param>
		public GrfonValue(string value) {
			this.value = value;
		}
		
		/// <summary>
		/// Construct a GrfonValue from a float (or int, via implicit conversion).
		/// </summary>
		/// <param name="number">value to store</param>
		/// <param name="format">format specifier used to convert number to a string</param>
		public GrfonValue(float number, string format="G") {
			this.value = number.ToString(format, CultureInfo.InvariantCulture);
		}
		
		/// <summary>
		/// Construct a GrfonValue from a list of numbers, by joining them with a space.
		/// (This is just a convenience; you can convert your lists to a string in other
		/// ways if you prefer.)
		/// </summary>
		/// <param name="numbers">list of numbers to store</param>
		/// <param name="format">format specifier to use on each number</param>
		public GrfonValue(List<float> numbers, string format="G") {
			this.value = string.Join(" ", numbers.Select(f => f.ToString(format, CultureInfo.InvariantCulture)).ToArray());
		}
		
		/// <summary>
		/// Serialize this value to an output stream.  Note that values
		/// are already compact, but if 'compact' is true, we append
		/// a semicolon and no line break, whereas when compact=false,
		/// we instead append a line break.
		/// </summary>
		/// <param name="output">GrfonOutput to write to</param>
		/// <param name="isDocument">whether this value is the entire document</param>
		/// <param name="compact">whether to output in compact form</param>
		public override void SerializeTo(GrfonOutput output, bool isDocument=true, bool compact=false) {
			if (compact) {
				lineNum = output.Print(GrfonOutput.Escape(value) + ";", false);
			} else {
				lineNum = output.Print(GrfonOutput.Escape(value));
			}
		}
		
		/// <summary>
		/// Override standard .NET ToString() to just return our value.
		/// </summary>
		/// <returns></returns>
		public override string ToString() {
			return value;
		}
		
		/// <summary>
		/// Explicitly convert this node to an int.
		/// </summary>
		/// <returns></returns>
		public override int ToInt(int defaultValue = 0) {
			if (value.Contains("E")) {
				if (Double.TryParse(value, out var result)) return (int)Convert.ToDouble(value, CultureInfo.InvariantCulture);
			} else {
				if (Int32.TryParse(value, out var result)) return (int)Convert.ToInt32(value, CultureInfo.InvariantCulture);
			}

			return defaultValue;
		}
		
		/// <summary>
		/// Explicitly convert this node to a bool.
		/// </summary>
		/// <returns></returns>
		public override bool ToBool() {
			if (string.IsNullOrEmpty(value)) return false;
			char c = value[0];
			return (c == '1' || c == 'y' || c == 'Y' || c == 't' || c == 'T');
		}
		
		/// <summary>
		/// Explicitly convert this node to a float.
		/// </summary>
		/// <returns></returns>
		public override float ToFloat() {
			return Convert.ToSingle(value, CultureInfo.InvariantCulture);
		}
	}
	
	/// <summary>
	/// GrfonCollection represents a collection of GRFON nodes.  This
	/// includes both key/value pairs (where keys are strings and values
	/// are GrfonNodes), and ordered (unkeyed) nodes.
	/// </summary>
	public class GrfonCollection : GrfonNode, IEnumerable {
		/// <summary>
		/// Whether to prefer compact output when serializing this collection.
		/// (Often used for small collections, e.g. that represent a 3D vector, etc.).
		/// </summary>
		public bool preferCompactOutput = false;
		
		// Private data containing the actual elements of our collection.
		Dictionary<string, GrfonNode> keyedEntries;
		List<GrfonNode> listEntries;

		/// <summary>
		/// Factory method that makes it really easy to parse GRFON data in a string
		/// into a GrfonCollection.  (In the event that the data contains a simple
		/// value, we'll return a 1-element collection containing that value.)
		/// To use, simply do:
		/// 		GrfonCollection result = GrfonCollection.FromString(someData);
		/// </summary>
		/// <param name="grfonData">GRFON data, in string form.</param>
		/// <returns>The GrfonCollection parsed from the given data.</returns>
		public static new GrfonCollection FromString(string grfonData) {
			GrfonNode result = GrfonNode.FromString(grfonData);
			if (result is GrfonCollection) return (GrfonCollection)result;
			GrfonCollection coll = new GrfonCollection();
			coll.Add(result);
			return coll;
		}


		// Name-oriented accessors
		
		/// <summary>
		/// Get the number of key/value pairs in this collection.
		/// </summary>
		public int KeyCount {
			get {
				return keyedEntries == null ? 0 : keyedEntries.Count;
			}
		}
		
		/// <summary>
		/// Get the collection of keys from our key/value pairs.
		/// </summary>
		public Dictionary<string, GrfonNode>.KeyCollection Keys {
			get {
				EnsureKeyedEntries();
				return keyedEntries.Keys;
			}
		}
		
		/// <summary>
		/// Return whether this collection contains the given string key.
		/// </summary>
		/// <param name="key">key to look for</param>
		/// <returns>true if we have such a key; false otherwise</returns>
		public bool ContainsKey(string key) {
			return keyedEntries == null ? false : keyedEntries.ContainsKey(key);
		}
		
		/// <summary>
		/// Set the value associated with a given key (overwriting any 
		/// previous value for that key).
		/// </summary>
		/// <param name="name">key</param>
		/// <param name="node">value</param>
		public void SetChild(string name, GrfonNode node) {
			EnsureKeyedEntries();
			keyedEntries[name] = node;
		}

		public void Remove(string name) {
			EnsureKeyedEntries();
			keyedEntries.Remove(name);
		}
		
		/// <summary>
		/// []-syntax for getting and setting keyed entries.  So, for example,
		/// if you have a GrfonCollection called gc, you can do
		/// 	gc["foo"] = bar
		/// </summary>
		/// <param name="key">string key</param>
		/// <returns></returns>
		public GrfonNode this[string key] {
		get {
			EnsureKeyedEntries();
			return keyedEntries[key];
		}
		set {
			EnsureKeyedEntries();
			keyedEntries[key] = value;
		}
		}
		
		/// <summary>
		/// Convenience method to look up a keyed value and convert it to a string.
		/// </summary>
		/// <param name="key">key to look up</param>
		/// <param name="defaultValue">value to return if key is not found</param>
		/// <returns>value found, or defaultValue</returns>
		public string GetString(string key, string defaultValue="") {
			GrfonNode result = null;
			if (keyedEntries != null) keyedEntries.TryGetValue(key, out result);
			if (result == null) return defaultValue;
			return result.ToString();
		}
		
		/// <summary>
		/// Convenience method to look up a keyed value and convert it to an int.
		/// </summary>
		/// <param name="key">key to look up</param>
		/// <param name="defaultValue">value to return if key is not found</param>
		/// <returns>value found, or defaultValue</returns>
		public int GetInt(string key, int defaultValue=0) {
			GrfonNode result = null;
			if (keyedEntries != null) keyedEntries.TryGetValue(key, out result);
			if (result == null) return defaultValue;
			return result.ToInt(defaultValue);
		}
		
		/// <summary>
		/// Convenience method to look up a keyed value and convert it to a bool.
		/// </summary>
		/// <param name="key">key to look up</param>
		/// <param name="defaultValue">value to return if key is not found</param>
		/// <returns>value found, or defaultValue</returns>
		public bool GetBool(string key, bool defaultValue=false) {
			GrfonNode result = null;
			if (keyedEntries != null) keyedEntries.TryGetValue(key, out result);
			if (result == null) return defaultValue;
			return result.ToBool();
		}
		
		
		
		/// <summary>
		/// Convenience method to look up a keyed value and convert it to a float.
		/// </summary>
		/// <param name="key">key to look up</param>
		/// <param name="defaultValue">value to return if key is not found</param>
		/// <returns>value found, or defaultValue</returns>
		public float GetFloat(string key, float defaultValue=0) {
			GrfonNode result = null;
			if (keyedEntries != null) keyedEntries.TryGetValue(key, out result);
			if (result == null) return defaultValue;
			return result.ToFloat();
		}
		
		/// <summary>
		/// Convenience method to look up a keyed value as a GrfonCollection.
		/// </summary>
		/// <param name="key">key to look up</param>
		/// <param name="defaultToEmpty">if true, return an empty collection if key is not found; otherwise return null</param>
		/// <returns>collection found, empty collection, or null</returns>
		public GrfonCollection GetCollection(string key, bool defaultToEmpty=true) {
			GrfonNode result = null;
			if (keyedEntries != null) keyedEntries.TryGetValue(key, out result);
			if (result is GrfonCollection) return (GrfonCollection)result;
			if (defaultToEmpty) return new GrfonCollection();
			return null;
		}
		
		/// <summary>
		/// Get the given node as a list of strings.  If the data for this node
		/// is a collection, we'll break it out and convert each one to a string
		/// for the result.  If it's not a collection, we'll return it as a one-
		/// element list.  If there is no such node, we can return null or an 
		/// empty list, depending on the defaultToEmpty parameter.
		/// </summary>
		public List<string> GetStringList(string key, bool defaultToEmpty=true) {
			List<string> result = null;
			GrfonNode node = null;
			if (keyedEntries != null) keyedEntries.TryGetValue(key, out node);
			if (node == null && !defaultToEmpty) return null;
			result = new List<string>();			
			if (node is GrfonCollection) {
				foreach (GrfonNode item in ((GrfonCollection)node)) result.Add(item.ToString());
			} else if (node != null) {
				result.Add(node.ToString());
			}
			return result;
		}
		
		// List-oriented accessors
		
		/// <summary>
		/// Get the number of unkeyed elements in this collection.
		/// </summary>
		public int ListCount {
			get {
				return listEntries == null ? 0 : listEntries.Count;
			}
		}
		
		/// <summary>
		/// Add a new unkeyed element to the end of our list.
		/// </summary>
		/// <param name="node"></param>
		public void Add(GrfonNode node) {
			EnsureListEntries();
			listEntries.Add(node);
		}

		/// <summary>
		/// Remove an unkeyed element from our list.
		/// </summary>
		/// <param name="index"></param>
		public void Remove(int index) {
			EnsureListEntries();
			listEntries.RemoveAt(index);
		}
		
		/// <summary>
		/// []-syntax for accessing the unkeyed elements by numeric index.
		/// For example, if you gave a GrfonCollection named gc, you can do:
		/// 	gc[0] = "Foo!";
		/// </summary>
		/// <param name="listIndex">index of unkeyed element to get/set</param>
		public GrfonNode this[int listIndex] {
		get {
			EnsureListEntries();
			return listEntries[listIndex];
		}
		set {
			EnsureListEntries();
			listEntries[listIndex] = value;
		}
		}
		
		/// <summary>
		/// Get an enumerator over the unkeyed entries.  This lets you use
		/// "for each" with a GrfonCollection to iterate over these entries.
		/// </summary>
		/// <returns></returns>
		IEnumerator IEnumerable.GetEnumerator() {
			return (IEnumerator)GetEnumerator();
		}
		
		/// <summary>
		/// Get an enumerator over the unkeyed entries.  This lets you use
		/// "for each" with a GrfonCollection to iterate over these entries.
		/// </summary>
		/// <returns></returns>
		public GrfonListEnumerator GetEnumerator() {
			return new GrfonListEnumerator(this);
		}
		
		// Serializing (conversion to string)
		
		/// <summary>
		/// Serialize this collection to the given output.
		/// </summary>
		/// <param name="output">GrfonOutput to write to</param>
		/// <param name="isDocument">whether this is the top-level (document) node</param>
		/// <param name="compact">whether compact output is preferred</param>
		public override void SerializeTo(GrfonOutput output, bool isDocument=true, bool compact=false) {
			if (preferCompactOutput) compact = true;
			
			// Skip printing enclosing braces at the top (document) level
			if (isDocument) {
				lineNum = output.NextLineNum();
			} else {
				if (compact) {
					lineNum = output.Print("{ ", false);
				} else {
					lineNum = output.Print("{");
				}
				output.indent++;
			}
			
			// Print all the keyed entries first, in alphabetical order...
			if (keyedEntries != null) {
				List<string> keys = keyedEntries.Keys.ToList();
				keys.Sort();
				foreach (string key in keys) {
					GrfonNode entry = keyedEntries[key];
					if (entry == null) continue;
					output.Print(key + ": ", false);
					entry.SerializeTo(output, false, compact);
				}
			}
			
			// ...Followed by the list entries
			if (listEntries != null) {
				foreach (GrfonNode entry in listEntries) {
					if (entry != null) entry.SerializeTo(output, false, compact);
				}
			}
			
			if (!isDocument) {
				output.indent--;
				output.Print("}");
			}
		}
		
		// Private helper methods
		
		void EnsureKeyedEntries() {
			if (keyedEntries == null) keyedEntries = new Dictionary<string, GrfonNode>();
		}
		
		void EnsureListEntries() {
			if (listEntries == null) listEntries = new List<GrfonNode>();
		}
	}
	
	/// <summary>
	/// This is an IEnumerator implementation that iterates over the
	/// unkeyed entries of a GrfonCollection.  This is primarily to
	/// enable the use of "for each" to iterate over a GrfonCollection.
	/// </summary>
	public struct GrfonListEnumerator : IEnumerator {
		GrfonCollection collection;
		int position;
		
		public GrfonListEnumerator(GrfonCollection collection) {
			this.collection = collection;
			position = -1;
		}
		
		public bool MoveNext() {
			position++;
			return (position < collection.ListCount);
		}
		
		public void Reset() {
			position = -1;
		}
		
		object IEnumerator.Current {
			get { return Current;
			}
		}
		
		public GrfonNode Current {
			get {
				try {
					return collection[position];
				} catch (IndexOutOfRangeException) {
					throw new InvalidOperationException();
				}
			}
		}
	}
	
	#endregion
	//======================================================================
	#region GrfonDeserializer
	
	/// <summary>
	/// GrfonDeserializer parses input into a GrfonNode (which is, in most
	/// cases, a GrfonCollection) representing the contents of a document.
	/// This is how you read a file (or other buffer) of GRFON data.
	/// </summary>
	public class GrfonDeserializer {
		
		// Private data used to maintain our parsing state.
		
		enum TokenType {
			Comment,
			CollStart,
			CollEnd,
			Keyword,
			Colon,
			Value
		}

		GrfonInput input;
		List<GrfonCollection> stack;
		string curLine = "";	// line we're currently parsing
		int curPos;				// position within that line (to be read next)
		int curLineNum = -1;	// number of that line in the file
		bool keyPossible;		// true in a context where a key (of a key:value pair) could occur
		string token;			// text of the last-read token
		TokenType tokType;		// type of the last-read token
		string pendingKeyword = "";		// keyword we're waiting for a value for
		
		/// <summary>
		/// Construct a GrfonDeserializer around a GrfonInput.
		/// </summary>
		/// <param name="input">GrfonInput to read</param>
		public GrfonDeserializer(GrfonInput input) {
			this.input = input;
		}

		bool NextToken() {
			while (true) {
				// Grab the next line, if needed.
				if (curPos >= curLine.Length) {
					if (input.EOF) return false;
					curLine = input.ReadLine();
					curLineNum++;
					curPos = 0;
					keyPossible = true;
				}

				// Advance past initial whitespace and semicolons.
				while (curPos < curLine.Length &&
				       (curLine[curPos] == ' ' || curLine[curPos] == '\t'
						|| curLine[curPos] == ';')) {
					if (curLine[curPos] == ';') keyPossible = true;	// semicolon's as good as a line break!
					curPos++;
				}

				// If we got nothing but whitespace, this was a blank line;
				// skip it and continue.
				if (curPos >= curLine.Length) continue;

				// OK, so what do we have here?  Check for easy cases first.
				char c = curLine[curPos];
				if (c == '{' || c == '}' || c == ':') {
					// Single-character tokens.
					token = new string(c, 1);
					if (c == '{') tokType = TokenType.CollStart;
					else if (c == '}') tokType = TokenType.CollEnd;
					else if (c == ':') tokType = TokenType.Colon;
					curPos++;
					keyPossible = (c != ':');
					return true;
				}

				if (c == '/' && curPos + 1 < curLine.Length && curLine[curPos++] == '/') {
					// Start of a comment (//), which extends to end of line.
					token = curLine.Substring(curPos-1, curLine.Length - curPos + 1);
					tokType = TokenType.Comment;
					curPos = curLine.Length;
					keyPossible = false;
					return true;
				}

				// If it's none of the above, and we don't have a fresh line,
				// then we have a value, which extends to EOL, ;, or //.
				if (!keyPossible) {
					int endPos = IndexOfEndOfValue(curLine, curPos);
					token = Unescape(curLine.Substring(curPos, endPos - curPos));
					tokType = TokenType.Value;
					curPos = endPos;
					keyPossible = false;
					return true;
				}

				// If we do have a fresh line, then it's trickier.  We either
				// have a key, which will be terminated by a colon, or a value.
				int colonPos = IndexOfKeyValueSeparator(curLine, curPos);
				if (colonPos >= curPos) {
					// Found a key.
					token = curLine.Substring(curPos, colonPos - curPos);
					tokType = TokenType.Keyword;
					curPos = colonPos;
				} else {
					// No key found, so we just have a value.
					int endPos = IndexOfEndOfValue(curLine, curPos);
					token = Unescape(curLine.Substring(curPos, endPos - curPos));
					tokType = TokenType.Value;
					curPos = endPos;
				}
				keyPossible = false;
				return true;
			}
		}

		/// <summary>
		/// Unescape the specified string.  This means replacing a backslash
		/// with whatever character follows, with these exceptions: \n becomes
		/// a newline, \r becomes a carriage return, and \t becomes a tab.
		/// </summary>
		/// <returns>Unescaped string.</returns>
		/// <param name="str">String to unescape.</param>
		public static string Unescape(string str) {
			// bail out for strings too short to have any escapes
			if (str.Length <= 1) return str;

			// and, bail out if our string doesn't contain any backslashes at all
			if (!str.Contains('\\')) return str;

			// now loop over characters, copying into a buffer, unescaping as we go
			char[] buffer = new char[str.Length];
			int outIdx = 0;
			for (int i = 0; i < str.Length; i++) {
				char c = str[i];
				if (c == '\\' && i + 1 < str.Length) {
					switch (str[i + 1]) {
					case 'n':
						c = '\n';
						i++;
						break;
					case 'r':
						c = '\r';
						i++;
						break;
					case 't':
						c = '\t';
						i++;
						break;
					default:
						c = str[i + 1];
						i++;
						break;
					}
				}

				buffer[outIdx++] = c;
			}

			return new String(buffer, 0, outIdx);
		}

		protected virtual void ReportError(string msg) {
			Console.WriteLine("Error on line {0}: {1}", curLineNum, msg);
		}

		void Store(GrfonNode node) {
			node.lineNum = curLineNum;
			if (pendingKeyword == "") {
				OpenCollection().Add(node);
			} else {
				OpenCollection().SetChild(pendingKeyword, node);
				pendingKeyword = "";
			}
		}
		
		/// <summary>
		/// Parse our input and return its data as a GrfonNode.  This could
		/// be a GrfonValue, if that's all the input contains, but more 
		/// typically it will be a GrfonCollection containing possibly many
		/// other nodes.
		/// </summary>
		/// <returns></returns>
		public GrfonNode Parse() {

			while (true) {
				// Grab the next token, or if at end-of-file, bail out.
				if (!NextToken()) break;

				switch (tokType) {

				case TokenType.CollStart:
					GrfonCollection coll = new GrfonCollection();
					Store(coll);
					Push(coll);
					break;

				case TokenType.CollEnd:
					Pop();
					break;

				case TokenType.Value:
					Store(new GrfonValue(token));
					break;
				
				case TokenType.Keyword:
					pendingKeyword = token;
					// Next token should be the colon; just verify...
					NextToken();
					if (tokType != TokenType.Colon) {
						ReportError("Colon not found after keyword");
					}
					break;

				case TokenType.Colon:
				case TokenType.Comment:
					// Ignore these, for now
					break;
				
				}
			}
			if (stack == null) return null;
			return stack[0];
		}

		void Push(GrfonCollection coll) {
			if (stack == null) stack = new List<GrfonCollection>();
			stack.Add(coll);
		}

		void Pop() {
			if (stack == null || stack.Count == 0) {
				Console.WriteLine("Error on line {0}: unmatched '}}'", input.LineNum - 1);
			} else {
				if (curLineNum == stack.Last().lineNum) {
					// This GrfonCollection was all on one line.  Looks compact to me.
					stack.Last().preferCompactOutput = true;
				}
				stack.RemoveAt(stack.Count - 1);
			}
		}

		GrfonCollection OpenCollection() {
			if (stack == null) stack = new List<GrfonCollection>();
			if (stack.Count == 0) {
				GrfonCollection doc = new GrfonCollection();
				doc.lineNum = 0;
				Push(doc);
			}
			return stack[stack.Count - 1];
		}
			
		static int IndexOfKeyValueSeparator(string line, int startingAt=0) {
			// TODO: watch out for and skip escaped colons.   Ignoring that for now.
			return line.IndexOf(':', startingAt);
		}

		/// <summary>
		/// Finds the end of a value string with the given starting point.
		/// This is either the end of the line, or the position of an 
		/// the run of whitespace before an unescaped "//" (comment),
		/// or the position of a semicolon or } character.
		/// </summary>
		/// <returns>Position of the end of the value within the line.</returns>
		static int IndexOfEndOfValue(string line, int startingAt=0) {
			int endPos = startingAt;
			int lastNonWhitespace = startingAt;
			while (true) {
				if (endPos < line.Length && line[endPos] == '\\') {
					// backslash escape; ignore the next character
					lastNonWhitespace = endPos;
					endPos += 2;
					continue;
				}
				if (IsValueDelimiter(line, endPos)) {
					return lastNonWhitespace + 1;
				}
				if (endPos < line.Length && line[endPos] != ' ' && line[endPos] != '\t') {
					lastNonWhitespace = endPos;
				}
				endPos++;
			}
		}

		static bool IsValueDelimiter(string line, int index) {
			if (index >= line.Length) return true;
			char c = line[index];
			if (c == ';' || c == '}') return true;
			if (c == '/' && index + 1 < line.Length && line[index + 1] == '/') return true;
			return false;
		}
	}

	#endregion
	//======================================================================
	#region Test Code
	
	/// <summary>
	/// This is a little test class that exercises Grfon, and also serves
	/// as a few examples of how Grfon can be used.  Output is written 
	/// to the Console.
	/// </summary>
	public static class GrfonStaticTests {
		public static void Run() {
			Console.WriteLine("GRFON");

			System.Diagnostics.Debug.Assert(GrfonOutput.Escape("foo bar") == @"foo bar");
			System.Diagnostics.Debug.Assert(GrfonOutput.Escape("foo;bar") == @"foo\;bar");
			System.Diagnostics.Debug.Assert(GrfonOutput.Escape("foo\nbar\\baz") == @"foo\nbar\\baz");

			System.Diagnostics.Debug.Assert(GrfonDeserializer.Unescape(@"foo bar") == "foo bar");
			System.Diagnostics.Debug.Assert(GrfonDeserializer.Unescape(@"foo\;bar") == "foo;bar");
			System.Diagnostics.Debug.Assert(GrfonDeserializer.Unescape(@"foo\nbar\\baz") == "foo\nbar\\baz");

			GrfonCollection doc = new GrfonCollection();
			doc["foo"] = new GrfonValue("bar");
			doc["baz"] = new GrfonValue("bamf");
			doc.Add(new GrfonValue("list zero"));
			doc.Add(new GrfonValue("list one"));
			doc.Add(new GrfonValue("list two"));
			
			// Include a sub-collection, with values of its own.
			GrfonCollection sub = new GrfonCollection();
			sub["hair"] = new GrfonValue("brown");
			sub["eyes"] = new GrfonValue("blue");
			sub["height"] = new GrfonValue(156);
			sub["measurements"] = new GrfonValue(new List<float>() { 36, 24, 35 });
			doc["attributes"] = sub;

			GrfonBufferOutput output = new GrfonBufferOutput();
			doc.SerializeTo(output);
			output.DumpToConsole(true);
			Console.WriteLine("baz line number: {0}", doc["baz"].lineNum);
			Console.WriteLine("list(1) number: {0}", doc[1].lineNum);
			Console.WriteLine("--------");
			
			// Read in some GRFON data, and dump it to the console.
			string test = @"
				42

				// Blank lines and comments, no problem!
				test1: foo  // end-of-line comment
				test2: {
					bar
					baz
				}
				test3: {apple; strawberry; banana}";
			GrfonDeserializer des = new GrfonDeserializer(new GrfonStringInput(test));
			GrfonNode result = des.Parse();
			output = new GrfonBufferOutput();
			result.SerializeTo(output);
			output.DumpToConsole(true);

			test = @"foo: semi\;colons; bar:line\nbreak";
			doc = GrfonCollection.FromString(test);
			System.Diagnostics.Debug.Assert(doc["foo"].ToString() == "semi;colons");
			System.Diagnostics.Debug.Assert(doc["bar"].ToString() == "line\nbreak");

			output = new GrfonBufferOutput();
			doc.SerializeTo(output);
			output.DumpToConsole(true);
		}
	}
	
	#endregion
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
