# Author : Baptiste Vu Liem
"""Waits until the editor answers again (a builder or a build blocks it), then prints the console.

    python Tools/wait_editor.py [max_minutes] [log filter]

A long menu item times out on the MCP side while the editor keeps working: run it, then this. Prints
the errors and the log lines containing the filter (default "[Build]").
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import unity_mcp


def ask(tool, args):
    res = unity_mcp.call("tools/call", {"name": tool, "arguments": args})
    text = res["result"]["content"][0]["text"]
    return json.loads(text)


def main():
    minutes = float(sys.argv[1]) if len(sys.argv) > 1 else 30
    needle = sys.argv[2] if len(sys.argv) > 2 else "[Build]"
    start = time.time()
    time.sleep(20)
    while time.time() - start < minutes * 60:
        try:
            state = ask("execute_code", {"action": "execute", "code": "return UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating || UnityEditor.BuildPipeline.isBuildingPlayer ? \"busy\" : \"idle\";"})
            if state.get("success") and state["data"]["result"] == "idle":
                break
        except Exception:
            pass
        time.sleep(10)
    else:
        print("still busy after", minutes, "minutes")
        return
    print(f"idle after {time.time() - start:.0f} s")
    errors = ask("read_console", {"types": ["error"], "count": 12})
    for line in errors.get("data") or []:
        print("ERROR", str(line)[:700])
    logs = ask("read_console", {"types": ["log"], "count": 60, "filter_text": needle})
    for line in logs.get("data") or []:
        print(str(line)[:300])


if __name__ == "__main__":
    main()
