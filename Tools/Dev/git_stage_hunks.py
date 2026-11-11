"""Stage only the hunks of a file whose changed lines match a regex (non-interactive `git add -p`).
    python Tools/Dev/git_stage_hunks.py <file> <regex> [--invert]
Used to commit separate changes that touch the same file one by one."""
import re
import subprocess
import sys
import tempfile

path, pattern = sys.argv[1], re.compile(sys.argv[2])
invert = "--invert" in sys.argv
diff = subprocess.run(["git", "diff", "--", path], capture_output=True, text=True, encoding="utf-8").stdout
if not diff:
    sys.exit("no unstaged change in " + path)
head, *rest = re.split(r"(?m)^(?=@@ )", diff)
chosen = []
for hunk in rest:
    changed = "\n".join(l for l in hunk.splitlines()[1:] if l[:1] in "+-")
    if bool(pattern.search(changed)) != invert:
        chosen.append(hunk)
if not chosen:
    sys.exit("no hunk matches")
with tempfile.NamedTemporaryFile("w", suffix=".patch", delete=False, encoding="utf-8", newline="\n") as f:
    f.write(head + "".join(chosen))
subprocess.run(["git", "apply", "--cached", "--recount", f.name], check=True)
print("staged %d of %d hunk(s) of %s" % (len(chosen), len(rest), path))
