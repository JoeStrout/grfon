This directory contains C# classes for reading/writing GRFON data, as well as a GrfonFormatter class which can automatically serialize and deserialize classes via introspection.

# Using GRFON.cs for manual serialization

## Adding GRFON to Your Project

You can download the latest GRFON code and documentation from the GitHub repo at  https://github.com/JoeStrout/grfon/.

GRFON is implemented in a single C# source file, GRFON.cs, to make it trivial to add to a project.  Just add it to your project and it's done.

Note that all the GRFON classes are within a "GRFON" namespace.  So you will need to either have a "using GRFON;" line at the top of your file, or use a GRFON prefix on all GRFON identifiers.  The examples below assume the "using GRFON" approach.

## Writing GRFON Data

You create a GRFON file (or string — for example, to send over a network connection) by first creating a GrfonCollection that represents the data, and then serializing that to a GrfonOutput.

Here's an example (assuming the GRFON and System.IO namespaces).

```
    public static void Save() {
        GrfonCollection data = new GrfonCollection();
        data["gameMode"] = gameMode.ToString();
        data["spentXP"] = spentXP;
        data["unspentXP"] = unspentXP;
        data["projects"] = ToCollection(projectsDone);
        
        Stream stream = new FileStream("data.grfon", FileMode.Create);
        using (GrfonStreamOutput outp = new GrfonStreamOutput(stream)) {
            data.SerializeTo(outp);
        }   
    }

    static GrfonCollection ToCollection(List<string> strings) {
        GrfonCollection coll = new GrfonCollection();
        foreach (string s in strings) coll.Add(new GrfonValue(s));
        return coll;
    }
```

This example writes to a file stream, using GrfonStreamOutput.  If you wanted to get the GRFON data as a string, you would instead serialize to a GrfonBufferOutput.

## Reading GRFON Data

Reading GRFON data is essentially the same process in reverse.  Build a GrfonInput around your string data or IO stream.  Then create a GrfonDeserializer with that GrfonInput, and call its Parse method.  The result is a GrfonCollection, which you can then inspect for your data.  GrfonCollection has a bunch of methods to make it easy to get data as strings, integers, floats, etc.  In keeping with GRFON philosophy, missing data should not be an error; instead you specify a default value you want to assume if a given key is not found.  (But you can call ContainsKey when you really need to know.)

Here's sample code to load the data that was saved above.

```
    public static void Load() {
        using (Stream stream = new FileStream("data.grfon", FileMode.Open)) {
            GrfonDeserializer des = new GrfonDeserializer(new GrfonStreamInput(stream));
            GrfonCollection data = des.Parse() as GrfonCollection;
            if (data == null) data = new GrfonCollection();
        
            gameMode = (GameMode)System.Enum.Parse(typeof(GameMode), 
                    data.GetString("gameMode", "Sandbox"), true);        
            spentXP = data.GetInt("spentXP");
            unspentXP = data.GetInt("unspentXP");
            projectsDone = data.GetStringList("projects");   
        }     
    }
```

Note that GrfonDeserializer.Parse _can_ return null, in cases where the input is completely empty.  So it's usually a good idea to check for this.  The example above handles it by simply creating a new empty GrfonCollection, which will then return all default values.  (Methods like GetInt and GetFloat default to zero, and GetString defaults to the null string, but you can specify a different default with the optional second parameter.)

If you have GRFON data in a string, you can do the same as the above, but using a GrfonStringInput instead of a GrfonStreamInput...

```
            GrfonDeserializer des = new GrfonDeserializer(new GrfonStringInput(grfonDataString));
            GrfonCollection data = des.Parse() as GrfonCollection;
            // ...continue as above
```

But there's an even easier way: the GrfonCollection.FromString method, which handles all those details for you:


```
            GrfonCollection data = GrfonCollection.FromString(grfonDataString);
            // ...continue as above
```

This makes it really easy to convert GRFON data back into objects you can manipulate in your code.

# Using GrfonFormatter for auto-serialization

The "normal" way of writing GRFON data is to create a GrfonCollection, stuff data into it, and then write it out, as shown above.  And then when reading, you manually do the reverse: parse your data into a GrfonCollection, and pull the data you need back out.

However, in cases where you have simple classes and you just want your GRFON file to mirror the structure of those classes, there is an easier way.  Using the (optional) GrfonFormatter class, you can just mark your own classes with `[System.Serializable]`, and then use the standard C# serialization support (i.e. System.Runtime.Serialization) to convert them to/from GRFON.

For example, here's a simple class with several data members, including a list of strings.  Note the [System.Serializable] tag.

```
[System.Serializable]
public class TestClass {
    public string foo = "bar";
    public int meaningOfLife = 42;
    public List<string> fruits = new List<string>() { "apple", "banana", "cherry" };
}
```

Now you can write this out in GRFON form using standard C# techniques.  For example, assuming we have a testInstance of type TestClass, here's code that serializes this (in GRFON format) to a memory stream, and then returns that as a string.

```
        var formatter = new GrfonFormatter();
        using(var memStream = new System.IO.MemoryStream()) {
            formatter.Serialize(memStream, testInstance);
            return System.Text.Encoding.UTF8.GetString(memStream.ToArray());
        }
```

The string returned looks like this:

```
*type: TestClass
foo: bar
fruits: {
    apple
    banana
    cherry
}
meaningOfLife: 42
```

For the most part this is just standard GRFON; the only unusual thing is the *type key, which is used to keep track of what sort of object was serialized, so that we can create that same type at deserialization time.

To convert such data back into an object using standard C# techniques would look something like this:

```
        byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(grfonData);
        var stream = new System.IO.MemoryStream(byteArray, false);
        
        var formatter = new GrfonFormatter();
        return formatter.Deserialize<TestClass>(stream);
```

Here we built a MemoryStream around our string data; of course any System.IO.Stream would also work (e.g. a file stream).  Then you simply create a GrfonFormatter, and call Deserialize on it, passing in the stream.  In this example we used the generic form of Deserialize to specify the type of object we expect; you can also call the untyped version, which returns type object, and then see what type of object you got.

This System.Runtime.Serialization support is optional, and provided in a separate source file (GrfonFormatter.cs).  If it fits your workflow, use it!  If not, you can leave this file out of your project, and work with GRFON in the other way.
