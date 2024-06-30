This page describe how to use GRFON from the perspective of an end-user who wants to edit GRFON files.

Fortunately, GRFON is very easy to read and write.  You'll probably grasp everything you need to know just from looking at a few [GRFON samples](../examples/README.md).

GRFON files are made up of two basic kinds of data: **values** and **collections**.

A **value** is simply some text or a number.  GRFON does not distinguish between these, and no quotation marks are needed around text strings.  

> ### Special Characters
> The only characters to be careful of are semicolon `;`, colon `:`, curly brace `{`, backslash `\`, and two forward slashes `//`, all of which have special meaning in GRFON.  When you want one of those to just be the character itself, you can avoid the special meaning by preceding it with a backslash.
>
> The exception to this rule is `://`, which is always interpreted as a simple string (because otherwise, URLs would be annoyingly hard to write correctly).


So, all of the following are valid GRFON values.

```
42
The Ultimate Answer to Life, The Universe, and Everything
3.14157
Spam and Eggs
That was then\; this is now.
https://miniscript.org
```

> ### Backslash-Escaped Special Characters
> Certain characters following a backslash have special meaning.  The official set of these supported by GRFON is shown in this table:
>
> | Sequence | Unicode (Meaning) |
> |----------| ----------------- |
> | \b | 8 (backspace) |
> | \t | 9 (tab) |
> | \n | 10 (newline) |
> | \f | 12 (page break) |
> | \r | 13 (carriage return |
>
> A backslash followed by any other character is parsed as just that other character, e.g., `\x` comes through as simply `x`.

A **collection** is a bunch of values grouped together, some of which may be identified by unique string keys, and others which are only identified by their order in the collection.  These are called "key/value pairs" and "unkeyed values" respectively.  One collection may have any number of key/value pairs and unkeyed values.  However, among the key/value pairs, each key must be unique.

A key/value pair is given with the key, followed by a colon, and then the value.  Key/value pairs, as well as unkeyed values, are separated within a collection by either a semicolon (;) or a line break.  Finally, a collection is always wrapped in curly braces, except for the very top-level collection that represents a GRFON document; curly braces aren't needed in that case.

The following is an example of a simple collection containing three key/value pairs.

```
{
    name: Bob
    occupation: Builder
    motto: Can we fix it? Yes we can!
}
```

And here's the very same collection, written all on one line.

```
{ name: Bob; occupation: Builder; motto: Can we fix it? Yes we can! }
```

You can mix and match semicolons and line breaks however you please; for example, you could put name and occupation on one line, but leave motto on a line by itself.  It's all the same to GRFON.

Here's another example of a collection containing only unkeyed values:

```
{ Washington; Adams; Jefferson; Madison; Monroe }
```

And here's an example that mixes the two; it has both keyed and unkeyed values.  (It is somewhat customary to put all the key/value pairs at the top, followed by the unkeyed values, but this is not necessary; the order and position of key/value pairs is completely irrelevant.)

```
{
    author: Bill T. Cat
    lastEdit: 24-Dec-2015
    note: I'm not sure about Monroe.  Somebody better look that up.

    Washington; Adams; Jefferson
    Madison
    Monroe
}
```

Note how, just for fun, we combined three of the unkeyed values onto one line using semicolons.  It makes no difference whatsoever to the data represented, so you should format your GRFON files in whatever way is easiest for you.

Finally, a collection may itself be used anywhere a value is expected.  That is, you can have a collection as part of a key/value pair, or as an unkeyed value, in another collection.  These may be nested to arbitrary depth.  Here's a simple example with some nested data.

```
// This line is a comment, ignored by GRFON.
// The collection below is a list of collections (plus one key/value pair).
 {
    metaData: {
        author: unknown
        lastRev: 4000 BCE
        printColor: gold
        url: http://example.com
    }
    { name: Adam; job: gardener; age: 26 }
    { name: Beth; job: programmer; age: 24 }
    { name: Charlie; job: codebreaker; age: 30 }
    { name: Dave; job: digger; age: 22 }
    { name: Eve; job: temptress; age: 26 }
}
```

The above example also illustrates comments, which can be added to GRFON files for the benefit of human readers.  These comments start with a double forward slash `//` and continue to the end of the line, and are ignored by any computer program that reads the data.

And about that's all there is to GRFON.  It does not define a standard format for dates, 3D vectors, or other complex data types, but the host app (whatever is reading your GRFON data) will probably define formats for those as needed.

Finally, note that GRFON is fully Unicode-savvy, and a GRFON file should be stored in UTF-8 encoding.

Check out the [examples directory](../examples/README.md) for more examples of valid GRFON files.
