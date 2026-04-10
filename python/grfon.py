"""
GRFON – General Recursive Format Object Notation

A simpler, gentler file format designed to be especially human-editable.
See https://github.com/JoeStrout/grfon for the full specification.

Public API
----------
parse(text)      Parse a GRFON string (or list of lines) into Python objects.
to_grfon(value)  Serialize a Python object as a GRFON string.
escape(s)        Escape special GRFON characters in a plain string.
unescape(s)      Process backslash-escape sequences in a GRFON string.

Configuration flags
-------------------
interpret_true_and_false : bool (default False)
    When True, the bare values "true" and "false" are returned as Python
    booleans rather than strings.

interpret_null : bool (default True)
    When True, the bare value "null" is returned as Python None.
"""

from __future__ import annotations

__all__ = [
    "parse",
    "to_grfon",
    "escape",
    "unescape",
    "interpret_true_and_false",
    "interpret_null",
]

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

#: When True, bare "true" / "false" values are returned as Python booleans.
interpret_true_and_false: bool = False

#: When True, the bare value "null" is returned as Python None.
interpret_null: bool = True

# ---------------------------------------------------------------------------
# Public helpers: escape / unescape
# ---------------------------------------------------------------------------

def escape(s: str) -> str:
    """Return *s* with all GRFON-special characters backslash-escaped.

    Special characters are ``\\ ; : { } \\b \\t \\n \\f \\r`` and ``//``.
    The sequence ``://`` is left unescaped so that URLs require no special
    treatment (per the GRFON spec).
    """
    # Backslash must come first so later substitutions don't double-escape it.
    s = s.replace("\\", "\\\\")
    s = s.replace(":", "\\:")
    s = s.replace(";", "\\;")
    s = s.replace("{", "\\{")
    s = s.replace("}", "\\}")
    s = s.replace("\b", "\\b")
    s = s.replace("\t", "\\t")
    s = s.replace("\n", "\\n")
    s = s.replace("\f", "\\f")
    s = s.replace("\r", "\\r")
    # Escape "//" but then restore "://" so URLs pass through unmodified.
    # After the colon-escape step above, "://" became "\://"; after escaping
    # "//" it becomes "\:\/\/".  We restore that 6-char sequence to "://".
    s = s.replace("//", "\\/\\/")
    s = s.replace("\\:\\/\\/", "://")
    return s


def unescape(s: str) -> str:
    """Resolve backslash-escape sequences in *s* and return the result.

    Recognised sequences: ``\\\\`` ``\\b`` ``\\t`` ``\\n`` ``\\f`` ``\\r``
    ``\\uXXXX`` (4 hex digits).  Any other ``\\x`` sequence is replaced
    by ``x`` alone (the backslash is consumed).
    """
    if "\\" not in s:
        return s  # fast path: nothing to do

    parts: list[str] = []
    i = 0
    n = len(s)
    while i < n:
        c = s[i]
        if c != "\\":
            parts.append(c)
            i += 1
            continue

        if i + 1 >= n:
            parts.append("\\")  # lone trailing backslash
            break

        nc = s[i + 1]
        if nc == "b":
            parts.append("\b"); i += 2
        elif nc == "t":
            parts.append("\t"); i += 2
        elif nc == "n":
            parts.append("\n"); i += 2
        elif nc == "f":
            parts.append("\f"); i += 2
        elif nc == "r":
            parts.append("\r"); i += 2
        elif nc == "u" and i + 5 <= n:
            hex_str = s[i + 2 : i + 6]
            try:
                parts.append(chr(int(hex_str, 16)))
                i += 6
            except ValueError:
                # Not a valid 4-hex-digit sequence — treat \u literally.
                parts.append(nc); i += 2
        else:
            # Any other character: backslash is consumed, character passes through.
            parts.append(nc); i += 2

    return "".join(parts)


# ---------------------------------------------------------------------------
# Public entry points
# ---------------------------------------------------------------------------

def parse(text: str | list[str]) -> object:
    """Parse *text* (a GRFON string or list of lines) into Python objects.

    Return type depends on content:

    * Scalar value → ``str``, ``int``, ``float``, ``bool``, or ``None``
    * All-unkeyed collection → ``list``
    * All-keyed collection → ``dict``
    * Mixed collection → ``dict`` with unkeyed entries stored under ``"_"``
    """
    if isinstance(text, list):
        text = "\n".join(text)
    return _Parser(text).parse()


def to_grfon(
    value: object,
    *,
    compact: bool = False,
    indent: int = 0,
    top_level: bool = True,
) -> str:
    """Serialize *value* as a GRFON string.

    Parameters
    ----------
    compact:
        Use semicolons instead of newlines to separate collection entries.
    indent:
        Current indentation depth (used automatically in recursive calls).
    top_level:
        When ``True`` (the default), omit the surrounding ``{ }`` braces,
        treating the value as a top-level GRFON document.
    """
    if callable(value):
        return "<function>"
    if value is None:
        return "null"
    # bool must be checked before int because bool is a subclass of int.
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int):
        return str(value)
    if isinstance(value, float):
        return _format_float(value)
    if isinstance(value, str):
        return escape(value)
    if isinstance(value, (list, tuple)):
        return _list_to_grfon(list(value), compact=compact, indent=indent, top_level=top_level)
    if isinstance(value, dict):
        return _map_to_grfon(value, compact=compact, indent=indent, top_level=top_level)
    # Fallback for other types: stringify and escape.
    return escape(str(value))


# ---------------------------------------------------------------------------
# Internal serialisation helpers
# ---------------------------------------------------------------------------

_EOL = "\n"


def _format_float(value: float) -> str:
    """Format a float, dropping the decimal point for whole numbers."""
    try:
        i = int(value)
    except (OverflowError, ValueError):
        return repr(value)
    if value == i:
        return str(i)
    return repr(value)


def _list_to_grfon(lst: list, *, compact: bool, indent: int, top_level: bool) -> str:
    if not lst:
        return "{}" if not top_level else ""

    if compact:
        sep = "; "
    else:
        inner_prefix = "  " * (indent + (0 if top_level else 1))
        sep = _EOL + inner_prefix

    next_indent = indent + (0 if top_level else 1)
    items = [to_grfon(v, compact=compact, indent=next_indent, top_level=False) for v in lst]
    body = sep.join(items)

    if top_level:
        return body
    if compact:
        return "{" + body + "}"
    return "{" + sep + body + _EOL + "  " * indent + "}"


def _map_to_grfon(m: dict, *, compact: bool, indent: int, top_level: bool) -> str:
    if not m:
        return "{}" if not top_level else ""

    if compact:
        sep = "; "
    else:
        inner_prefix = "  " * (indent + (0 if top_level else 1))
        sep = _EOL + inner_prefix

    next_indent = indent + (0 if top_level else 1)
    pairs = [
        to_grfon(k, compact=True, indent=next_indent, top_level=False)
        + ": "
        + to_grfon(v, compact=compact, indent=next_indent, top_level=False)
        for k, v in m.items()
    ]
    body = sep.join(pairs)

    if top_level:
        return body
    if compact:
        return "{" + body + "}"
    return "{" + sep + body + _EOL + "  " * indent + "}"


# ---------------------------------------------------------------------------
# Internal parser
# ---------------------------------------------------------------------------

_WHITESPACE = frozenset(" \t\r\n")
_DELIMITERS = frozenset(":;}\r\n")


class _Parser:
    """Recursive-descent GRFON parser."""

    __slots__ = ("_src", "_len", "_pos")

    def __init__(self, source: str) -> None:
        self._src = source
        self._len = len(source)
        self._pos = 0

    def parse(self) -> object:
        self._pos = 0
        return self._parse_element()

    # ------------------------------------------------------------------
    # Whitespace / comment handling
    # ------------------------------------------------------------------

    def _skip_whitespace(self) -> None:
        src = self._src
        n = self._len
        while self._pos < n:
            if src[self._pos : self._pos + 2] == "//":
                self._skip_to_eol()
            elif src[self._pos] in _WHITESPACE:
                self._pos += 1
            else:
                break

    def _skip_to_eol(self) -> None:
        src = self._src
        n = self._len
        while self._pos < n:
            c = src[self._pos]
            self._pos += 1
            if c in "\r\n":
                break

    # ------------------------------------------------------------------
    # Element parsing
    # ------------------------------------------------------------------

    def _parse_element(self, *, as_value: bool = False) -> object:
        """Parse one element, which may be a scalar or a collection.

        *as_value* is True when we're parsing the value part of a key:value
        pair; in that case we return after the first complete token.
        """
        result: object = None
        result_is_keyed_map = False  # True only when result was built by the ':' branch

        while True:
            self._skip_whitespace()

            if self._pos >= self._len:
                return result

            c = self._src[self._pos]

            if c == "}":
                if not as_value:
                    self._pos += 1  # consume the closing brace
                return result

            if c == "{":
                self._pos += 1  # consume the opening brace
                tok = self._parse_element()
                if tok is None:
                    tok = {}  # {} → empty map
            else:
                tok = self._parse_value()

            if as_value:
                return tok
            if tok is None:
                return result

            self._skip_whitespace()
            nxt = self._src[self._pos] if self._pos < self._len else ""

            if nxt == ":":
                # ---------------------------------------------------------
                # Key / value pair
                # ---------------------------------------------------------
                self._pos += 1  # consume ':'
                val = self._parse_element(as_value=True)
                if result is None:
                    result = {}
                    result_is_keyed_map = True
                elif isinstance(result, list):
                    result = {"_": result}
                    result_is_keyed_map = True
                result[tok] = val  # type: ignore[index]
                # An optional semicolon may follow a key:value pair.
                self._skip_whitespace()
                if self._pos < self._len and self._src[self._pos] == ";":
                    self._pos += 1

            elif nxt == ";":
                # ---------------------------------------------------------
                # Unkeyed element, semicolon-separated
                # ---------------------------------------------------------
                if result is None:
                    result = [tok]
                elif isinstance(result, list):
                    result.append(tok)  # type: ignore[union-attr]
                else:
                    # Mixed collection: add unkeyed item to the "_" list.
                    result.setdefault("_", []).append(tok)  # type: ignore[union-attr]
                self._pos += 1  # consume ';'

            else:
                # ---------------------------------------------------------
                # Unkeyed element, newline-separated (or end of input)
                # ---------------------------------------------------------
                if result is None:
                    result = tok
                elif isinstance(result, list):
                    result.append(tok)  # type: ignore[union-attr]
                elif isinstance(result, dict) and result_is_keyed_map:
                    # Mixed collection: add unkeyed item to the "_" list.
                    result.setdefault("_", []).append(tok)  # type: ignore[union-attr]
                else:
                    # result is a primitive or an unkeyed sub-collection;
                    # promote to a list rather than merging into it.
                    result = [result, tok]

                if nxt == "":
                    return result

    def _parse_value(self) -> object:
        """Parse and return a single scalar token."""
        self._skip_whitespace()
        if self._pos >= self._len or self._src[self._pos] == "}":
            return None

        raw, any_escape = self._get_string_and_esc()
        raw = raw.rstrip(" \t")  # trim trailing whitespace per spec

        # Special bare values are checked on the raw (pre-unescape) string.
        if interpret_true_and_false:
            if raw == "true":
                return True
            if raw == "false":
                return False
        if interpret_null and raw == "null":
            return None

        # If any backslash escapes were present, skip numeric coercion and
        # return as a string (this is how \42 forces "42" to stay a string).
        if any_escape:
            return unescape(raw)

        n = _try_parse_number(raw)
        if n is not None:
            return n

        return raw

    def _get_string_and_esc(self) -> tuple[str, bool]:
        """Read a raw token; return ``(raw_string, any_escape_present)``."""
        start = self._pos
        any_escape = False
        src = self._src
        n = self._len

        while self._pos < n:
            c = src[self._pos]

            # The :// sequence is always literal (URL exception).
            if c == ":" and src[self._pos : self._pos + 3] == "://":
                self._pos += 3
                continue

            if c in _DELIMITERS:
                break

            # "//" starts a comment; stop here.
            if c == "/" and src[self._pos : self._pos + 2] == "//":
                break

            self._pos += 1

            if c == "\\":
                any_escape = True
                if self._pos < n:
                    self._pos += 1  # skip the escaped character

        return src[start : self._pos], any_escape


# ---------------------------------------------------------------------------
# Numeric coercion helper
# ---------------------------------------------------------------------------

def _try_parse_number(s: str) -> int | float | None:
    """Try to interpret *s* as a number; return ``None`` if it is neither."""
    if not s:
        return None
    try:
        return int(s)
    except ValueError:
        pass
    try:
        f = float(s)
        # Return whole-number floats as int so that "42.000" → 42, not 42.0.
        try:
            i = int(f)
            if f == i:
                return i
        except (OverflowError, ValueError):
            pass
        return f
    except ValueError:
        pass
    return None
