"""Unit tests for grfon.py, mirroring the MiniScript test suite."""

import unittest
import sys
import os

sys.path.insert(0, os.path.dirname(__file__))
import grfon


class TestEscape(unittest.TestCase):

    def test_tab(self):
        self.assertEqual(grfon.escape("foo\tbar"), "foo\\tbar")

    def test_newline(self):
        self.assertEqual(grfon.escape("a\nb"), "a\\nb")

    def test_url_preserved(self):
        # :// is never escaped — URLs must pass through unchanged.
        self.assertEqual(grfon.escape("http://foo"), "http://foo")
        self.assertEqual(grfon.escape("https://example.com/path?a=1"), "https://example.com/path?a=1")

    def test_colon_slash_slash(self):
        # A colon *not* followed by // must be escaped; // alone must be escaped.
        self.assertEqual(grfon.escape("foo: //bar"), "foo\\: \\/\\/bar")

    def test_backslash(self):
        # "C:\Users\Bob" → backslashes doubled, colon also escaped
        self.assertEqual(grfon.escape("C:\\Users\\Bob"), "C\\:\\\\Users\\\\Bob")

    def test_braces(self):
        self.assertEqual(grfon.escape("a {b} c"), "a \\{b\\} c")

    def test_semicolon(self):
        self.assertEqual(grfon.escape("then; now"), "then\\; now")


class TestUnescape(unittest.TestCase):

    def test_tab(self):
        self.assertEqual(grfon.unescape("foo\\tbar"), "foo\tbar")

    def test_newline(self):
        self.assertEqual(grfon.unescape("line1\\nline2"), "line1\nline2")

    def test_url(self):
        # \:\/ \/ → ://
        self.assertEqual(grfon.unescape("http\\:\\/\\/foo"), "http://foo")

    def test_arbitrary_char(self):
        # Backslash before a non-special char → that char alone.
        self.assertEqual(grfon.unescape("\\x"), "x")
        self.assertEqual(grfon.unescape("\\4"), "4")

    def test_unicode(self):
        self.assertEqual(grfon.unescape("caf\\u00e9"), "café")
        self.assertEqual(grfon.unescape("\\u263A"), "\u263A")

    def test_backslash_backslash(self):
        self.assertEqual(grfon.unescape("\\\\"), "\\")

    def test_no_escapes_fast_path(self):
        self.assertEqual(grfon.unescape("plain text"), "plain text")


class TestParse(unittest.TestCase):

    def test_plain_string(self):
        self.assertEqual(grfon.parse("abc"), "abc")

    def test_integer(self):
        self.assertEqual(grfon.parse("42"), 42)
        self.assertIsInstance(grfon.parse("42"), int)

    def test_negative_float(self):
        self.assertAlmostEqual(grfon.parse("-123.45"), -123.45)

    def test_leading_decimal(self):
        self.assertAlmostEqual(grfon.parse(".5"), 0.5)

    def test_trailing_comment(self):
        self.assertEqual(grfon.parse("12 // foo"), 12)

    def test_leading_comment(self):
        self.assertEqual(grfon.parse("// foo\n34"), 34)

    def test_cr_before_value(self):
        self.assertEqual(grfon.parse("\r42"), 42)

    def test_semicolon_list(self):
        self.assertEqual(grfon.parse("1; 2; foo"), [1, 2, "foo"])

    def test_key_value_pairs(self):
        self.assertEqual(grfon.parse("1:one; 2:two"), {1: "one", 2: "two"})

    def test_tab_and_quotes_in_value(self):
        # GRFON input "\\t..." has a backslash-t escape sequence → tab character.
        # The double-quote is not special in GRFON so it passes through unchanged.
        self.assertEqual(
            grfon.parse('\\tHello, "Bob".'),
            '\tHello, "Bob".',
        )

    def test_integer_with_trailing_zeros(self):
        # 42.000 should parse as the integer 42.
        self.assertEqual(grfon.parse("42.000"), 42)
        self.assertIsInstance(grfon.parse("42.000"), int)

    def test_backslash_forces_string(self):
        # A leading \ means "treat this as a string, not a number".
        self.assertEqual(grfon.parse("\\42.000"), "42.000")
        self.assertIsInstance(grfon.parse("\\42.000"), str)

    def test_nested_collection(self):
        self.assertEqual(
            grfon.parse("foo:{1; 2; 3}; bar:baz"),
            {"foo": [1, 2, 3], "bar": "baz"},
        )

    def test_empty_collection(self):
        self.assertEqual(grfon.parse("{}"), {})

    def test_list_with_empty_collection(self):
        self.assertEqual(grfon.parse("1; 2; {}; 4"), [1, 2, {}, 4])

    def test_mixed_keyed_and_unkeyed(self):
        d = grfon.parse(["foo: bar", "42", "61", "hike!"])
        self.assertEqual(d["foo"], "bar")
        self.assertEqual(d["_"], [42, 61, "hike!"])

    def test_url_value(self):
        self.assertEqual(grfon.parse("https://miniscript.org"), "https://miniscript.org")

    def test_url_in_collection(self):
        result = grfon.parse(["{", "   url: https://miniscript.org", "}"])
        self.assertEqual(result, {"url": "https://miniscript.org"})

    def test_multiple_comments(self):
        self.assertEqual(grfon.parse("//\nfoo:42"), {"foo": 42})
        self.assertEqual(grfon.parse("//\n//\nfoo:42"), {"foo": 42})
        self.assertEqual(grfon.parse("//\n//\n//\nfoo:42"), {"foo": 42})

    def test_comment_then_blank_then_brace(self):
        self.assertEqual(grfon.parse("//\n\n{foo:42}"), {"foo": 42})

    def test_malformed_missing_value(self):
        # Key:value pair where the value is a comment — value should be null/None.
        d = grfon.parse([
            "foo: bar",
            "baz: {",
            "    this is the bug: //right here",
            "}",
            "bamf: 42",
        ])
        self.assertEqual(d["foo"], "bar")
        self.assertEqual(d["bamf"], 42)
        self.assertIsNone(d["baz"]["this is the bug"])

    def test_multiple_newline_separated_sub_collections(self):
        # Sub-collections separated by newlines must form a list,
        # not merge the first one's keys into the parent.
        d = grfon.parse([
            "systems: {",
            "  {",
            "    name: Reactor",
            "    maxPower: 8",
            "  }",
            "  {",
            "    name: Doors",
            "    maxPower: 1",
            "  }",
            "}",
        ])
        self.assertIsInstance(d["systems"], list)
        self.assertEqual(len(d["systems"]), 2)
        self.assertEqual(d["systems"][0]["name"], "Reactor")
        self.assertEqual(d["systems"][1]["name"], "Doors")

    def test_braces_in_string_round_trip(self):
        s = grfon.to_grfon("a {b} c")
        self.assertEqual(s, "a \\{b\\} c")
        self.assertEqual(grfon.parse(s), "a {b} c")

    def test_multiline_input_list(self):
        result = grfon.parse(["name: Bob", "age: 30"])
        self.assertEqual(result, {"name": "Bob", "age": 30})


class TestToGrfon(unittest.TestCase):

    def test_list_compact(self):
        self.assertEqual(grfon.to_grfon([1, 2, "foo"], compact=True), "1; 2; foo")

    def test_list_multiline(self):
        self.assertEqual(grfon.to_grfon([1, 2, "foo"]), "1\n2\nfoo")

    def test_nested_lists(self):
        self.assertEqual(
            grfon.to_grfon([0, [1], [1, 2]]),
            "0\n{\n  1\n}\n{\n  1\n  2\n}",
        )

    def test_map_compact(self):
        s = grfon.to_grfon({"foo": [1, 2, 3], "bar": "baz"}, compact=True)
        self.assertIn("foo: {1; 2; 3}", s)
        self.assertIn("bar: baz", s)

    def test_none(self):
        self.assertEqual(grfon.to_grfon(None), "null")

    def test_bool_true(self):
        self.assertEqual(grfon.to_grfon(True), "true")

    def test_bool_false(self):
        self.assertEqual(grfon.to_grfon(False), "false")

    def test_integer(self):
        self.assertEqual(grfon.to_grfon(42), "42")

    def test_float(self):
        self.assertEqual(grfon.to_grfon(3.14), repr(3.14))

    def test_whole_float_as_int(self):
        self.assertEqual(grfon.to_grfon(42.0), "42")

    def test_string_with_special_chars(self):
        self.assertEqual(grfon.to_grfon("a:b"), "a\\:b")

    def test_empty_map(self):
        self.assertEqual(grfon.to_grfon({}, top_level=False), "{}")

    def test_round_trip_nested(self):
        original = {"name": "Bob", "scores": [10, 20, 30]}
        serialized = grfon.to_grfon(original)
        parsed = grfon.parse(serialized)
        self.assertEqual(parsed, original)

    def test_round_trip_url(self):
        original = {"url": "https://example.com/path?a=1&b=2"}
        serialized = grfon.to_grfon(original)
        parsed = grfon.parse(serialized)
        self.assertEqual(parsed, original)


if __name__ == "__main__":
    unittest.main(verbosity=2)
