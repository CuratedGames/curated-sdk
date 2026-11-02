#!/usr/bin/env python3
"""Extract the public API surface of every Curated SDK binding from source.

Used by scripts/check-public-api.py (drift check against contracts/public-api.v1.json) and by
scripts/docs_gen.py (API reference pages). No engine install is needed: the parsers are small,
line based, and only understand the shapes used in this repository.

Each binding yields an ordered list of Member(name, kind, signature, doc, group).
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


@dataclass
class Member:
    name: str
    kind: str
    group: str
    signatures: list[str] = field(default_factory=list)
    doc: str = ""


def _strip(text: str, *, hash_comments: bool = False, backticks: bool = False) -> str:
    """Blank out comments and string contents, keeping line breaks and quote characters."""
    out: list[str] = []
    i = 0
    n = len(text)
    while i < n:
        c = text[i]
        two = text[i : i + 2]
        if hash_comments and c == "#":
            while i < n and text[i] != "\n":
                i += 1
            continue
        if not hash_comments and two == "//":
            while i < n and text[i] != "\n":
                i += 1
            continue
        if not hash_comments and two == "/*":
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append("\n" * text.count("\n", i, j))
            i = j
            continue
        if c in "\"'" or (backticks and c == "`"):
            quote = c
            verbatim = i > 0 and text[i - 1] == "@"
            out.append(quote)
            i += 1
            while i < n:
                ch = text[i]
                if ch == "\\" and not verbatim:
                    i += 2
                    continue
                if ch == "\n":
                    out.append("\n")
                    if quote != "`" and not verbatim:
                        break
                if ch == quote:
                    break
                i += 1
            out.append(quote)
            i += 1
            continue
        out.append(c)
        i += 1
    return "".join(out)


def _clean_doc(lines: list[str]) -> str:
    text = " ".join(l.strip() for l in lines if l.strip())
    text = re.sub(r"<see cref=\"([^\"]+)\"\s*/>", lambda m: "`" + m.group(1).split("(")[0].replace("T:", "") + "`", text)
    text = re.sub(r"<paramref name=\"([^\"]+)\"\s*/>", r"`\1`", text)
    text = re.sub(r"<c>(.*?)</c>", r"`\1`", text)
    text = re.sub(r"</?(summary|para|remarks)>", " ", text)
    text = re.sub(r"<[^>]+>", "", text)
    return re.sub(r"\s+", " ", text).strip()


# ---------------------------------------------------------------------------
# C#
# ---------------------------------------------------------------------------

_TYPE_DECL = re.compile(r"\b(class|struct|enum|interface|record)\s+(\w+)")
_MODIFIERS = re.compile(r"\b(public|private|protected|internal|static|async|override|virtual|sealed|abstract|readonly|new|partial|extern|unsafe|volatile)\s+")


def _csharp_doc_above(orig_lines: list[str], index: int) -> str:
    docs: list[str] = []
    j = index - 1
    while j >= 0:
        s = orig_lines[j].strip()
        if s.startswith("///"):
            docs.insert(0, s[3:])
        elif s.startswith("[") and s.endswith("]"):
            pass
        else:
            break
        j -= 1
    summary = " ".join(docs)
    m = re.search(r"<summary>(.*?)</summary>", summary, re.S)
    return _clean_doc([m.group(1)] if m else [])


def _csharp_attr(orig_lines: list[str], index: int, attr: str) -> str:
    j = index - 1
    while j >= 0 and orig_lines[j].strip().startswith("["):
        m = re.match(r"\s*\[" + attr + r"\(\"(.*)\"\)\]", orig_lines[j])
        if m:
            return m.group(1)
        j -= 1
    return ""


def extract_csharp(paths: list[Path], group_prefix: str = "") -> list[Member]:
    members: list[Member] = []
    by_name: dict[str, Member] = {}

    def add(name: str, kind: str, group: str, signature: str, doc: str) -> None:
        if name in by_name:
            if signature and signature not in by_name[name].signatures:
                by_name[name].signatures.append(signature)
            return
        m = Member(name, kind, group, [signature] if signature else [], doc)
        by_name[name] = m
        members.append(m)

    for path in paths:
        orig = path.read_text(encoding="utf-8")
        orig_lines = orig.split("\n")
        lines = _strip(orig).split("\n")
        depth = 0
        stack: list[tuple[str, str, int, bool]] = []  # (kind, qualified name, body depth, public chain)
        pending: tuple[str, str, bool] | None = None
        i = 0
        while i < len(lines):
            line = lines[i]
            code = re.sub(r"^\s*\[[^\]]*\]\s*", "", line).strip()
            top = stack[-1] if stack else None
            in_body = top is not None and depth == top[2]
            public_chain = top[3] if top else True
            tm = _TYPE_DECL.search(code)
            if tm and (top is None or in_body) and not code.startswith("return"):
                is_public = code.startswith("public")
                qual = f"{top[1]}.{tm.group(2)}" if top else tm.group(2)
                chain = public_chain and is_public
                pending = (tm.group(1), qual, chain)
                if chain:
                    add(qual, "enum" if tm.group(1) == "enum" else "type", qual, "", _csharp_doc_above(orig_lines, i))
            elif in_body and public_chain and top[0] == "enum":
                for value in re.findall(r"(?:^|,)\s*([A-Za-z_]\w*)\s*(?:=[^,]*)?(?=,|$)", code):
                    add(f"{top[1]}.{value}", "enum-value", top[1], "", _csharp_doc_above(orig_lines, i))
            elif in_body and public_chain and code.startswith("public "):
                rest = code[len("public ") :]
                idx = min([p for p in (rest.find(ch) for ch in "({=;") if p >= 0] or [len(rest)])
                pre = rest[:idx].strip()
                stop = rest[idx] if idx < len(rest) else ""
                words = pre.split()
                if words:
                    name = words[-1]
                    short_type = top[1].split(".")[-1]
                    doc = _csharp_doc_above(orig_lines, i)
                    if stop == "(":
                        sig_lines = [code]
                        balance = code.count("(") - code.count(")")
                        k = i
                        while balance > 0 and k + 1 < len(lines):
                            k += 1
                            nxt = lines[k].strip()
                            sig_lines.append(nxt)
                            balance += nxt.count("(") - nxt.count(")")
                        sig = " ".join(sig_lines)
                        sig = re.split(r"\s*(=>|\{)\s*", sig)[0].rstrip(";").strip()
                        sig = _MODIFIERS.sub(lambda m: "static " if m.group(1) == "static" else "", sig)
                        sig = re.sub(r"\(\s+", "(", re.sub(r"\s+\)", ")", re.sub(r"\s+", " ", sig)))
                        kind = "constructor" if name == short_type and len(words) == 1 else "method"
                        add(f"{top[1]}.{name}", kind, top[1], sig.strip(), doc)
                    else:
                        if " const " in f" {pre} ":
                            kind = "constant"
                        elif " readonly " in f" {pre} ":
                            kind = "field"
                        else:
                            kind = "property"
                        sig = _MODIFIERS.sub(lambda m: "static " if m.group(1) == "static" else "", pre)
                        add(f"{top[1]}.{name}", kind, top[1], sig.strip(), doc)
            elif in_body and public_chain and re.search(r"\[SerializeField\]", "\n".join(orig_lines[max(0, i - 3) : i])):
                fm = re.match(r"([\w<>\[\],.]+)\s+(\w+)\s*(=|;)", code)
                if fm and not code.startswith(("public", "private", "protected", "internal", "static", "readonly")):
                    add(f"{top[1]}.{fm.group(2)}", "inspector", top[1], f"{fm.group(1)} {fm.group(2)}", _csharp_attr(orig_lines, i, "Tooltip"))
            for ch in line:
                if ch == "{":
                    depth += 1
                    if pending is not None:
                        stack.append((pending[0], pending[1], depth, pending[2]))
                        pending = None
                elif ch == "}":
                    if stack and stack[-1][2] == depth:
                        stack.pop()
                    depth -= 1
            i += 1
    return members


# ---------------------------------------------------------------------------
# GDScript (Godot autoload)
# ---------------------------------------------------------------------------


def extract_gdscript(path: Path, owner: str) -> list[Member]:
    orig_lines = path.read_text(encoding="utf-8").split("\n")
    members: list[Member] = [Member(owner, "type", owner, [], "")]

    def doc_above(i: int) -> str:
        docs: list[str] = []
        j = i - 1
        while j >= 0 and orig_lines[j].startswith("##"):
            docs.insert(0, orig_lines[j][2:])
            j -= 1
        return _clean_doc(docs)

    for i, line in enumerate(orig_lines):
        if m := re.match(r"(static\s+)?func\s+([a-z]\w*)\s*\((.*)", line):
            sig = line.rstrip(":").strip()
            members.append(Member(f"{owner}.{m.group(2)}", "method", owner, [sig], doc_above(i)))
        elif m := re.match(r"signal\s+([a-z]\w*)", line):
            members.append(Member(f"{owner}.{m.group(1)}", "signal", owner, [line.strip()], doc_above(i)))
        elif m := re.match(r"var\s+([a-z]\w*)", line):
            members.append(Member(f"{owner}.{m.group(1)}", "property", owner, [line.split("=")[0].strip()], doc_above(i)))
    return members


# ---------------------------------------------------------------------------
# Unreal C++ headers
# ---------------------------------------------------------------------------


def _cpp_doc_above(orig_lines: list[str], i: int) -> str:
    j = i - 1
    while j >= 0 and re.match(r"\s*(UFUNCTION|UPROPERTY|UENUM|USTRUCT|UCLASS)\(", orig_lines[j]):
        j -= 1
    s = orig_lines[j].strip() if j >= 0 else ""
    if s.startswith("/**") and s.endswith("*/"):
        return _clean_doc([s[3:-2]])
    if s.endswith("*/"):
        docs: list[str] = []
        while j >= 0:
            t = orig_lines[j].strip()
            docs.insert(0, t.lstrip("/*").rstrip("*/").strip())
            if t.startswith("/**"):
                break
            j -= 1
        return _clean_doc(docs)
    return ""


def extract_unreal(paths: list[Path]) -> list[Member]:
    members: list[Member] = []
    for path in paths:
        orig = path.read_text(encoding="utf-8")
        orig_lines = orig.split("\n")
        lines = _strip(orig).split("\n")
        current: tuple[str, str] | None = None  # (kind, name)
        section = "private"
        depth = 0
        body_depth = -1
        macro = ""
        for i, raw in enumerate(lines):
            line = raw.strip()
            if m := re.match(r"DECLARE_DYNAMIC_(?:MULTICAST_)?DELEGATE\w*\((\w+)(.*)\);", line):
                members.append(Member(m.group(1), "delegate", m.group(1), [line], _cpp_doc_above(orig_lines, i)))
            elif m := re.match(r"enum class (\w+)", line):
                current = ("enum", m.group(1))
                members.append(Member(m.group(1), "enum", m.group(1), [], _cpp_doc_above(orig_lines, i)))
            elif m := re.match(r"struct CURATEDSDK_API (\w+)", line):
                if re.match(r"\s*USTRUCT\(", orig_lines[i - 1] if i else ""):
                    current = ("struct", m.group(1))
                    members.append(Member(m.group(1), "type", m.group(1), [], _cpp_doc_above(orig_lines, i)))
                else:
                    current = ("skip", m.group(1))
            elif m := re.match(r"class CURATEDSDK_API (\w+)", line):
                current = ("class", m.group(1))
                section = "private"
                members.append(Member(m.group(1), "type", m.group(1), [], _cpp_doc_above(orig_lines, i)))
            elif m := re.match(r"namespace (\w+)", line):
                current = ("namespace", m.group(1))
            elif current and depth == body_depth:
                kind, owner = current
                if line in ("public:", "private:", "protected:"):
                    section = line[:-1]
                elif line.startswith(("UFUNCTION(", "UPROPERTY(")):
                    macro = line
                elif kind == "enum" and (vm := re.match(r"(\w+)\s*(=[^,]*)?,?$", line)):
                    members.append(Member(f"{owner}::{vm.group(1)}", "enum-value", owner, [], ""))
                elif kind == "struct" and macro.startswith("UPROPERTY(") and (fm := re.match(r"([\w<>:]+)\s+(\w+)\s*(=|;)", line)):
                    members.append(Member(f"{owner}.{fm.group(2)}", "property", owner, [f"{fm.group(1)} {fm.group(2)}"], _cpp_doc_above(orig_lines, i)))
                    macro = ""
                elif kind == "class" and section == "public" and macro:
                    if macro.startswith("UFUNCTION(") and (fm := re.match(r"(.*?)(\w+)\s*\((.*)", line)):
                        sig = re.split(r"\s*\{", line)[0].rstrip(";").strip()
                        members.append(Member(f"{owner}::{fm.group(2)}", "method", owner, [sig], _cpp_doc_above(orig_lines, i)))
                    elif "BlueprintAssignable" in macro and (fm := re.match(r"(\w+)\s+(\w+);", line)):
                        members.append(Member(f"{owner}::{fm.group(2)}", "event", owner, [line.rstrip(";")], _cpp_doc_above(orig_lines, i)))
                    macro = ""
                elif kind == "namespace" and line.startswith("CURATEDSDK_API") and (fm := re.match(r"CURATEDSDK_API\s+(.*?)(\w+)\s*\(", line)):
                    sig = line.replace("CURATEDSDK_API ", "").rstrip(";")
                    members.append(Member(f"{owner}::{fm.group(2)}", "function", owner, [sig], _cpp_doc_above(orig_lines, i)))
            for ch in raw:
                if ch == "{":
                    depth += 1
                    if current and body_depth < 0:
                        body_depth = depth
                elif ch == "}":
                    if depth == body_depth:
                        current = None
                        body_depth = -1
                    depth -= 1
    return members


# ---------------------------------------------------------------------------
# RPG Maker plugin (JavaScript)
# ---------------------------------------------------------------------------


def extract_rpgmaker(path: Path) -> list[Member]:
    orig = path.read_text(encoding="utf-8")
    orig_lines = orig.split("\n")
    lines = _strip(orig, backticks=True).split("\n")
    members: list[Member] = [Member("Curated", "type", "Curated", [], "")]

    header = orig.split("*/", 1)[0]
    current_param = None
    for raw in header.split("\n"):
        s = raw.strip().lstrip("*").strip()
        if m := re.match(r"@param (\w+)", s):
            current_param = Member(f"@param {m.group(1)}", "plugin-parameter", "Plugin parameters", [], "")
            members.append(current_param)
        elif m := re.match(r"@command (\w+)", s):
            current_param = Member(f"@command {m.group(1)}", "plugin-command", "Plugin parameters", [], "")
            members.append(current_param)
        elif current_param and (m := re.match(r"@(text|desc) (.*)", s)):
            current_param.doc = (current_param.doc + " " + m.group(2)).strip() if m.group(1) == "desc" else current_param.doc
            if m.group(1) == "text":
                current_param.signatures = [m.group(2)]
        elif s.startswith("@arg") or s.startswith("@help"):
            current_param = None

    top_functions: dict[str, tuple[str, int]] = {}
    for idx, l in enumerate(lines):
        if fm := re.match(r"\s*function (\w+)\s*\(.*$", l):
            top_functions[fm.group(1)] = (l.strip().rstrip("{").strip(), idx)
    start = next(i for i, l in enumerate(lines) if re.match(r"\s*const Curated = \{", l))
    depth = 0
    for i in range(start, len(lines)):
        line = lines[i]
        if depth == 1:
            s = line.strip()

            def doc(at: int | None = None) -> str:
                j = (i if at is None else at) - 1
                if orig_lines[j].strip().endswith("*/"):
                    docs: list[str] = []
                    while j >= 0:
                        t = orig_lines[j].strip()
                        docs.insert(0, t.lstrip("/*").rstrip("*/").strip())
                        if t.startswith("/**"):
                            break
                        j -= 1
                    return _clean_doc(docs)
                return ""

            if m := re.match(r"(async\s+)?([a-zA-Z]\w*)\s*\((.*)\)\s*\{", s):
                sig = f"{'async ' if m.group(1) else ''}Curated.{m.group(2)}({m.group(3)})"
                members.append(Member(f"Curated.{m.group(2)}", "method", "Curated", [sig], doc()))
            elif m := re.match(r"([A-Za-z]\w*),$", s):
                name = m.group(1)
                if name in top_functions:
                    text, at = top_functions[name]
                    sig = text.replace(f"function {name}", f"Curated.{name}")
                    members.append(Member(f"Curated.{name}", "method", "Curated", [sig], doc(at)))
                else:
                    members.append(Member(f"Curated.{name}", "constant", "Curated", [], doc()))
            elif m := re.match(r"([a-zA-Z]\w*):", s):
                members.append(Member(f"Curated.{m.group(1)}", "property", "Curated", [], doc()))
        depth += line.count("{") - line.count("}")
        if depth == 0 and i > start:
            break
    return members


# ---------------------------------------------------------------------------
# Bindings
# ---------------------------------------------------------------------------

BINDINGS: dict[str, dict] = {
    "dotnet": {"title": ".NET (Curated.Sdk)", "patterns": ["Curated.Sdk/*.cs"]},
    "unity": {
        "title": "Unity (Curated.Unity)",
        "patterns": ["engines/unity/com.curatedgames.sdk/Runtime/*.cs"],
    },
    "godot": {"title": "Godot 4 (Curated autoload)", "patterns": ["engines/godot/addons/curated/curated.gd"]},
    "unreal": {
        "title": "Unreal Engine 5 (CuratedSDK plugin)",
        "patterns": [
            "engines/unreal/CuratedSDK/Source/CuratedSDK/Public/CuratedLaunchContract.h",
            "engines/unreal/CuratedSDK/Source/CuratedSDK/Public/CuratedSubsystem.h",
        ],
    },
    "rpgmaker": {"title": "RPG Maker MZ / MV (CuratedSDK.js)", "patterns": ["engines/rpgmaker/CuratedSDK.js"]},
}


def sources(binding: str, root: Path = ROOT) -> list[str]:
    """Source files (relative paths) that define a binding's public surface."""
    out: list[str] = []
    for pattern in BINDINGS[binding]["patterns"]:
        if "*" in pattern:
            out += sorted(p.relative_to(root).as_posix() for p in root.glob(pattern))
        elif (root / pattern).exists():
            out.append(pattern)
    return out


def extract(binding: str, root: Path = ROOT) -> list[Member]:
    paths = [root / s for s in sources(binding, root)]
    if not paths:
        return []
    if binding in ("dotnet", "unity"):
        return extract_csharp(paths)
    if binding == "godot":
        return extract_gdscript(paths[0], "Curated")
    if binding == "unreal":
        return extract_unreal(paths)
    if binding == "rpgmaker":
        return extract_rpgmaker(paths[0])
    raise KeyError(binding)


if __name__ == "__main__":
    import sys

    for name in sys.argv[1:] or BINDINGS:
        print(f"== {name}")
        for m in extract(name):
            print(f"  {m.kind:16} {m.name:60} {' | '.join(m.signatures)[:90]}")
