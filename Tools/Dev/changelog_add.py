"""Add a bullet to docs/CHANGELOG.md under [Unreleased] / <section> (Added, Changed, Fixed, Removed).
    python Tools/Dev/changelog_add.py Changed "Text of the entry"
Long entries are wrapped at 118 columns with a two-space continuation indent."""
import sys
import textwrap

section, text = sys.argv[1], sys.argv[2]
path = "docs/CHANGELOG.md"
s = open(path, encoding="utf-8").read()
head, rest = s.split("## [Unreleased]\n", 1)
unreleased, tail = (rest.split("\n## [", 1) + [""])[:2]
order = ["Added", "Changed", "Fixed", "Removed"]
blocks = {}
current = None
for line in unreleased.strip("\n").splitlines():
    if line.startswith("### "):
        current = line[4:].strip()
        blocks.setdefault(current, [])
    elif current:
        blocks[current].append(line)
entry = textwrap.wrap(text, 116, initial_indent="- ", subsequent_indent="  ")
blocks.setdefault(section, [])
lines = [l for l in blocks[section] if l.strip()]
blocks[section] = lines + entry
out = []
for name in order + [k for k in blocks if k not in order]:
    if name in blocks and blocks[name]:
        out.append("### " + name)
        out.extend(blocks[name])
        out.append("")
new = head + "## [Unreleased]\n\n" + "\n".join(out) + ("\n## [" + tail if tail else "")
open(path, "w", encoding="utf-8", newline="").write(new)
