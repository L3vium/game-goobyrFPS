# Author : Baptiste Vu Liem
"""Commit and push one change from the shared working tree.

    python Tools/commit.py "Subject line" [-m "body paragraph"] path [path ...]

Several agents share this checkout and its git index. This takes a short mutex, stages only the
given paths (with their .meta files, deletions included), commits only those paths
(git commit --only), appends the co-author line and pushes dev. In this checkout never use
git add -A / commit -a / stash / reset / checkout / pull: they take other agents' work with them.
"""
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
MUTEX = os.path.join(HERE, ".git_commit_lock")
TRAILER = "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")


def acquire():
    for _ in range(240):
        try:
            fd = os.open(MUTEX, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            os.write(fd, str(os.getpid()).encode())
            os.close(fd)
            return
        except FileExistsError:
            try:
                if time.time() - os.path.getmtime(MUTEX) > 300:
                    os.remove(MUTEX)
                    continue
            except OSError:
                pass
            time.sleep(1)
    sys.exit("commit mutex busy for 4 minutes")


def main():
    args = sys.argv[1:]
    if len(args) < 2:
        print(__doc__)
        sys.exit(1)
    subject = args.pop(0)
    body = []
    while len(args) >= 2 and args[0] == "-m":
        body.append(args[1])
        args = args[2:]
    paths = []
    for path in args:
        paths.append(path)
        if not path.endswith(".meta") and os.path.exists(os.path.join(ROOT, path + ".meta")):
            paths.append(path + ".meta")
    message = "\n\n".join([subject, *body, TRAILER])

    acquire()
    try:
        res = git("add", "-A", "--", *paths)
        if res.returncode:
            sys.exit(res.stderr)
        res = git("commit", "--only", "-m", message, "--", *paths)
        if res.returncode:
            sys.exit(res.stdout + res.stderr)
        print(git("log", "--oneline", "-1").stdout.strip())
        for attempt in range(3):
            res = git("push", "-q", "origin", "dev")
            if res.returncode == 0:
                print("pushed")
                break
            time.sleep(5)
        else:
            sys.exit("push failed: " + res.stderr)
    finally:
        os.remove(MUTEX)


if __name__ == "__main__":
    main()
