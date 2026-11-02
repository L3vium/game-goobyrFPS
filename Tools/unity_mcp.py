# Author : Baptiste Vu Liem
"""Tiny client for MCP for Unity over streamable HTTP.

    python Tools/unity_mcp.py list
    python Tools/unity_mcp.py <tool> '<json args>'
    python Tools/unity_mcp.py <tool> @args.json

The editor's MCP port is read from UNITY_MCP_PORT (default 8084).

Several agents share one editor. Each one sets UNITY_MCP_AGENT=<name> (its own MCP session, so
replies never cross) and holds the editor lock for anything that changes the editor's state:

    python Tools/unity_mcp.py lock <minutes> [what]   take or renew the lock (exit 3 if held)
    python Tools/unity_mcp.py unlock                  release it
    python Tools/unity_mcp.py who                     who holds it, until when

Play mode, opening/saving scenes, builders and menu items, refresh/compile, tests and builds
refuse to run without the lock (the HEAVY tools below); reads (console, execute_code queries)
don't need it.
"""
import json
import os
import sys
import time
import urllib.request

PORT = int(os.environ.get("UNITY_MCP_PORT", "8084"))
URL = f"http://localhost:{PORT}/mcp"
AGENT = os.environ.get("UNITY_MCP_AGENT", "main")
HERE = os.path.dirname(os.path.abspath(__file__))
SESSION_FILE = os.path.join(HERE, ".unity_mcp_session." + AGENT)
LOCK_FILE = os.path.join(HERE, ".unity_editor_lock")
HEAVY = {"refresh_unity", "run_tests", "manage_scene", "execute_menu_item", "manage_editor", "manage_asset", "manage_prefabs"}


def _lock_state():
    """(owner, until, what) of the editor lock, or None when free or expired."""
    try:
        with open(LOCK_FILE, encoding="utf-8") as f:
            data = json.load(f)
    except (OSError, ValueError):
        return None
    if data.get("until", 0) < time.time():
        return None
    return data.get("owner"), data["until"], data.get("what", "")


def lock(minutes, what=""):
    state = _lock_state()
    if state and state[0] != AGENT:
        print(f"LOCKED by {state[0]} until {time.strftime('%H:%M:%S', time.localtime(state[1]))} ({state[2]})")
        sys.exit(3)
    tmp = LOCK_FILE + "." + AGENT
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump({"owner": AGENT, "until": time.time() + minutes * 60, "what": what}, f)
    os.replace(tmp, LOCK_FILE)
    state = _lock_state()
    if not state or state[0] != AGENT:
        print("lost the race for the lock, try again")
        sys.exit(3)
    print(f"locked by {AGENT} for {minutes} min ({what})")


def unlock():
    state = _lock_state()
    if state and state[0] == AGENT:
        os.remove(LOCK_FILE)
    print("unlocked")


def _require_lock(tool):
    state = _lock_state()
    if state is None or state[0] != AGENT:
        holder = f"held by {state[0]} ({state[2]})" if state else "free"
        print(f"'{tool}' changes the editor: take the lock first (python Tools/unity_mcp.py lock <minutes> <what>); it is {holder}")
        sys.exit(3)


def _post(payload, session=None):
    data = json.dumps(payload).encode()
    req = urllib.request.Request(URL, data=data, method="POST")
    req.add_header("Content-Type", "application/json")
    req.add_header("Accept", "application/json, text/event-stream")
    if session:
        req.add_header("Mcp-Session-Id", session)
    with urllib.request.urlopen(req, timeout=float(os.environ.get("UNITY_MCP_TIMEOUT", "600"))) as res:
        sid = res.headers.get("Mcp-Session-Id") or session
        body = res.read().decode("utf-8", "replace")
    if body.lstrip().startswith("{"):
        return sid, json.loads(body) if body.strip() else None
    # server-sent events: keep the last data line that parses
    result = None
    for line in body.splitlines():
        if line.startswith("data:"):
            try:
                result = json.loads(line[5:].strip())
            except ValueError:
                pass
    return sid, result


def _session():
    if os.path.exists(SESSION_FILE):
        with open(SESSION_FILE) as f:
            return f.read().strip()
    return None


def _open():
    sid, _ = _post({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
        "protocolVersion": "2025-03-26", "capabilities": {},
        "clientInfo": {"name": "goobyr-cli", "version": "1"}}})
    _post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    with open(SESSION_FILE, "w") as f:
        f.write(sid or "")
    return sid


def call(method, params):
    sid = _session() or _open()
    try:
        _, res = _post({"jsonrpc": "2.0", "id": 2, "method": method, "params": params}, sid)
    except urllib.error.HTTPError as e:
        if e.code in (400, 404):
            sid = _open()
            _, res = _post({"jsonrpc": "2.0", "id": 2, "method": method, "params": params}, sid)
        else:
            raise
    if res and "error" in res and "session" in json.dumps(res["error"]).lower():
        sid = _open()
        _, res = _post({"jsonrpc": "2.0", "id": 2, "method": method, "params": params}, sid)
    return res


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) < 2:
        print(__doc__)
        return
    if sys.argv[1] == "lock":
        lock(float(sys.argv[2]) if len(sys.argv) > 2 else 15, " ".join(sys.argv[3:]))
        return
    if sys.argv[1] == "unlock":
        unlock()
        return
    if sys.argv[1] == "who":
        state = _lock_state()
        print("free" if state is None else f"{state[0]} until {time.strftime('%H:%M:%S', time.localtime(state[1]))} ({state[2]})")
        return
    if sys.argv[1] in ("compile", "tests") or sys.argv[1] in HEAVY:
        _require_lock(sys.argv[1])
    if sys.argv[1] == "list":
        res = call("tools/list", {})
        for t in res["result"]["tools"]:
            print(t["name"], "-", (t.get("description") or "").split("\n")[0][:110])
        return
    if sys.argv[1] in ("compile", "wait"):
        # refresh + compile, wait until the editor is idle, then print the console errors
        import time
        if sys.argv[1] == "compile":
            call("tools/call", {"name": "refresh_unity", "arguments": {"compile": "request"}})
        time.sleep(3)
        busy_code = "return UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating;"
        for _ in range(240):
            try:
                res = call("tools/call", {"name": "execute_code", "arguments": {"action": "execute", "code": busy_code}})
                text = json.dumps(res)
                if '"result": false' in text.replace('\\"', '"') or "False" in text or '\\"result\\":false' in text:
                    break
            except Exception:
                pass
            time.sleep(2)
        res = call("tools/call", {"name": "read_console", "arguments": {"types": ["error"], "count": 30}})
        for c in res.get("result", {}).get("content", []):
            print(c.get("text", c)[:6000])
        return
    if sys.argv[1] == "tests":
        # run the EditMode tests, wait, print the summary and the failures
        import time
        res = call("tools/call", {"name": "run_tests", "arguments": {"mode": "EditMode"}})
        job = json.loads(res["result"]["content"][0]["text"])["data"]["job_id"]
        for _ in range(200):
            time.sleep(3)
            try:
                res = call("tools/call", {"name": "get_test_job", "arguments": {"job_id": job, "include_failed_tests": True}})
                data = json.loads(res["result"]["content"][0]["text"])["data"]
            except Exception:
                data = None
            if not data:
                # the editor is busy (domain reload while the run starts): ask again
                continue
            if data["status"] != "running":
                summary = (data.get("result") or {}).get("summary")
                print(data["status"], json.dumps(summary))
                for f in (data.get("result") or {}).get("results", []) or data["progress"].get("failures_so_far", []):
                    print(json.dumps(f)[:800])
                return
        print("timeout")
        return
    if sys.argv[1] == "schema":
        res = call("tools/list", {})
        for t in res["result"]["tools"]:
            if t["name"] == sys.argv[2]:
                print(json.dumps(t, indent=1))
        return
    args = {}
    if len(sys.argv) > 2:
        raw = sys.argv[2]
        if raw.startswith("@"):
            with open(raw[1:], encoding="utf-8") as f:
                raw = f.read()
        args = json.loads(raw)
    res = call("tools/call", {"name": sys.argv[1], "arguments": args})
    if res is None:
        print("(no response)")
        return
    if "error" in res:
        print("ERROR", json.dumps(res["error"], indent=1))
        sys.exit(1)
    for c in res.get("result", {}).get("content", []):
        print(c.get("text", c))


if __name__ == "__main__":
    main()
