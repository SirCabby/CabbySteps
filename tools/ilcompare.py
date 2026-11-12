#!/usr/bin/env python3
"""Compare the IL of two .NET assemblies, type by type.

    tools/ilcompare.py A.dll B.dll [-r REFDIR ...] [-q] [--keep DIR]

Both assemblies are disassembled with ilspycmd (`dotnet tool install -g
ilspycmd`). Things that only reflect layout are normalised away: method RVAs
and the labels of static-data blobs. The rest (signatures, locals, every IL
instruction, constants, attributes) must match. Types are compared by name, so
the order of source files doesn't matter; member order inside a type does.

Exit status: 0 = identical, 1 = differences (printed as a unified diff),
2 = usage or tool error.
"""
import argparse
import difflib
import os
import re
import shutil
import subprocess
import sys

RVA_LINE = re.compile(r"^\s*// Method begins at RVA 0x[0-9a-fA-F]+\s*$")
DATA_LABEL = re.compile(r"\bI_[0-9A-F]{8}\b")
CLASS_START = re.compile(r"^\.class\s.*?(\S+)\s*$")
CLASS_END = re.compile(r"^\} // end of class ")


def find_ilspycmd():
    exe = shutil.which("ilspycmd")
    if exe:
        return exe
    home_tool = os.path.expanduser("~/.dotnet/tools/ilspycmd")
    if os.path.exists(home_tool):
        return home_tool
    sys.exit("ilcompare: ilspycmd not found (dotnet tool install -g ilspycmd)")


def disassemble(ilspy, dll, refs):
    cmd = [ilspy, "-il", dll]
    for r in refs:
        cmd += ["-r", r]
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode != 0:
        sys.stderr.write(res.stderr)
        sys.exit(f"ilcompare: ilspycmd failed on {dll}")
    return res.stdout


def split_types(il_text):
    """Return {type name: normalised IL lines} for each top-level type."""
    types, name, block = {}, None, []
    for line in il_text.splitlines():
        if RVA_LINE.match(line):
            continue
        line = DATA_LABEL.sub("I_xxxxxxxx", line.rstrip())
        if name is None:
            m = CLASS_START.match(line)
            if m:
                name, block = m.group(1), [line]
            continue
        block.append(line)
        if CLASS_END.match(line):
            types[name] = block
            name = None
    return types


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("a")
    ap.add_argument("b")
    ap.add_argument("-r", "--ref", action="append", default=[], help="reference assembly directory")
    ap.add_argument("-q", "--quiet", action="store_true", help="summary only, no diff")
    ap.add_argument("--keep", metavar="DIR", help="also write the normalised IL of both sides to DIR")
    args = ap.parse_args()

    ilspy = find_ilspycmd()
    a = split_types(disassemble(ilspy, args.a, args.ref))
    b = split_types(disassemble(ilspy, args.b, args.ref))

    if args.keep:
        os.makedirs(args.keep, exist_ok=True)
        for tag, types in (("a", a), ("b", b)):
            with open(os.path.join(args.keep, f"{tag}.il"), "w") as f:
                for t in sorted(types):
                    f.write("\n".join(types[t]) + "\n\n")

    only_a = sorted(set(a) - set(b))
    only_b = sorted(set(b) - set(a))
    changed = sorted(t for t in set(a) & set(b) if a[t] != b[t])

    for t in only_a:
        print(f"only in {args.a}: {t}")
    for t in only_b:
        print(f"only in {args.b}: {t}")
    for t in changed:
        print(f"differs: {t}")
        if not args.quiet:
            sys.stdout.writelines(
                l + "\n" for l in difflib.unified_diff(a[t], b[t], f"a/{t}", f"b/{t}", n=3, lineterm="")
            )

    same = len(set(a) & set(b)) - len(changed)
    if only_a or only_b or changed:
        print(f"ilcompare: {len(changed)} type(s) differ, {len(only_a) + len(only_b)} unmatched, {same} identical")
        return 1
    print(f"ilcompare: IL identical ({same} types)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
