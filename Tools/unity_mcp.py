# Author : Baptiste Vu Liem
"""Tiny client for MCP for Unity over streamable HTTP.

    python Tools/unity_mcp.py list
    python Tools/unity_mcp.py <tool> '<json args>'
    python Tools/unity_mcp.py <tool> @args.json

The editor's MCP port is read from UNITY_MCP_PORT (default 8082).
"""
import json
import os
import sys
import urllib.request

PORT = int(os.environ.get("UNITY_MCP_PORT", "8082"))
URL = f"http://localhost:{PORT}/mcp"
SESSION_FILE = os.path.join(os.path.dirname(__file__), ".unity_mcp_session")


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
    if sys.argv[1] == "list":
        res = call("tools/list", {})
        for t in res["result"]["tools"]:
            print(t["name"], "-", (t.get("description") or "").split("\n")[0][:110])
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
