"""Run Python in the open Unreal editor (Python remote execution must be enabled).

Usage:
    python Tools/Editor/ue_remote.py "print(unreal.SystemLibrary.get_engine_version())"
    python Tools/Editor/ue_remote.py --file Tools/Editor/some_script.py
"""
import argparse
import sys
import time

sys.path.append(r"C:\Program Files\Epic Games\UE_5.7\Engine\Plugins\Experimental\PythonScriptPlugin\Content\Python")
import remote_execution  # noqa: E402


def run(command, timeout=10.0, exec_mode=remote_execution.MODE_EXEC_FILE):
    remote = remote_execution.RemoteExecution()
    remote.start()
    try:
        deadline = time.time() + timeout
        while not remote.remote_nodes and time.time() < deadline:
            time.sleep(0.2)
        time.sleep(0.5)  # let every open editor answer the discovery ping
        # Prefer the Hitmen editor when several projects are open.
        nodes = [n for n in remote.remote_nodes if n.get("project_name") == "Hitmen"] or remote.remote_nodes
        if not nodes:
            print("NO_EDITOR: no Unreal editor answered")
            return 2
        remote.open_command_connection(nodes[0])
        result = remote.run_command(command, unattended=True, exec_mode=exec_mode)
        for entry in result.get("output", []):
            print(entry.get("output", "").rstrip())
        if not result.get("success", False):
            print("FAILED:", result.get("result"))
            return 1
        return 0
    finally:
        remote.stop()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("command", nargs="?")
    parser.add_argument("--file")
    args = parser.parse_args()
    code = open(args.file, encoding="utf-8").read() if args.file else args.command
    sys.exit(run(code))
