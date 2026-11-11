"""Summarize and symbolize Hitmen crash reports (Windows, packaged builds).

Reads every Saved/Crashes/*/CrashContext.runtime-xml under a build, checks that the crashed exe is the
one next to the PDB (PE timestamp vs minidump module timestamp), and resolves the Hitmen.exe frames
with the system dbghelp.dll. Identical stacks are grouped.

    python Tools/Debug/hitmen_crash_report.py                       # Builds/Windows Dev
    python Tools/Debug/hitmen_crash_report.py "Builds/Windows" -n 30 # other build, 30 frames
    python Tools/Debug/hitmen_crash_report.py --crashes D:/copy/Saved/Crashes --binaries "Builds/Windows Dev/Hitmen/Binaries/Win64"

The PDB must come from the same build as the crashed exe (player builds are versioned in Git LFS:
check out the release that crashed if needed).
"""
import argparse
import collections
import ctypes
import ctypes.wintypes as wt
import glob
import os
import re
import struct
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def pe_timestamp(exe):
    with open(exe, "rb") as f:
        head = f.read(4096)
    lfanew = struct.unpack_from("<I", head, 0x3C)[0]
    return struct.unpack_from("<I", head, lfanew + 8)[0]


def dump_exe_timestamp(dmp):
    """TimeDateStamp of the Hitmen*.exe module recorded in a minidump, or None."""
    try:
        data = open(dmp, "rb").read()
    except OSError:
        return None
    count, rva = struct.unpack_from("<II", data, 8)
    for i in range(count):
        stream_type, _, stream_rva = struct.unpack_from("<III", data, rva + 12 * i)
        if stream_type != 4:  # ModuleListStream
            continue
        modules = struct.unpack_from("<I", data, stream_rva)[0]
        for m in range(modules):
            offset = stream_rva + 4 + 108 * m
            _, _, _, stamp, name_rva = struct.unpack_from("<QIIII", data, offset)
            length = struct.unpack_from("<I", data, name_rva)[0]
            name = data[name_rva + 4:name_rva + 4 + length].decode("utf-16le", "replace")
            if os.path.basename(name).lower().startswith("hitmen") and name.lower().endswith(".exe"):
                return stamp
    return None


class Symbolizer:
    BASE = 0x140000000

    class SymbolInfo(ctypes.Structure):
        _fields_ = [("SizeOfStruct", wt.ULONG), ("TypeIndex", wt.ULONG), ("Reserved", ctypes.c_uint64 * 2),
                    ("Index", wt.ULONG), ("Size", wt.ULONG), ("ModBase", ctypes.c_uint64), ("Flags", wt.ULONG),
                    ("Value", ctypes.c_uint64), ("Address", ctypes.c_uint64), ("Register", wt.ULONG),
                    ("Scope", wt.ULONG), ("Tag", wt.ULONG), ("NameLen", wt.ULONG), ("MaxNameLen", wt.ULONG),
                    ("Name", ctypes.c_wchar * 1024)]

    class Line(ctypes.Structure):
        _fields_ = [("SizeOfStruct", wt.DWORD), ("Key", ctypes.c_void_p), ("LineNumber", wt.DWORD),
                    ("FileName", ctypes.c_wchar_p), ("Address", ctypes.c_uint64)]

    def __init__(self, exe):
        self.dbg = ctypes.WinDLL(os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "System32", "dbghelp.dll"))
        self.handle = ctypes.c_void_p(0x4854)
        self.dbg.SymSetOptions(0x2 | 0x10 | 0x4)  # undecorate, load lines, deferred loads
        if not self.dbg.SymInitializeW(self.handle, ctypes.c_wchar_p(os.path.dirname(exe)), False):
            raise RuntimeError("SymInitialize failed")
        self.dbg.SymLoadModuleExW.restype = ctypes.c_uint64
        self.dbg.SymLoadModuleExW.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_wchar_p, ctypes.c_wchar_p,
                                              ctypes.c_uint64, wt.DWORD, ctypes.c_void_p, wt.DWORD]
        self.dbg.SymFromAddrW.argtypes = [ctypes.c_void_p, ctypes.c_uint64, ctypes.POINTER(ctypes.c_uint64),
                                          ctypes.POINTER(self.SymbolInfo)]
        self.dbg.SymGetLineFromAddrW64.argtypes = [ctypes.c_void_p, ctypes.c_uint64, ctypes.POINTER(wt.DWORD),
                                                   ctypes.POINTER(self.Line)]
        if not self.dbg.SymLoadModuleExW(self.handle, None, exe, None, self.BASE, 0x40000000, None, 0):
            raise RuntimeError("SymLoadModuleEx failed for " + exe)

    def resolve(self, offset):
        info = self.SymbolInfo()
        info.SizeOfStruct = 88
        info.MaxNameLen = 1024
        displacement = ctypes.c_uint64()
        if not self.dbg.SymFromAddrW(self.handle, self.BASE + offset, ctypes.byref(displacement), ctypes.byref(info)):
            return "?"
        name = info.Name[:info.NameLen]
        line = self.Line()
        line.SizeOfStruct = ctypes.sizeof(self.Line)
        line_disp = wt.DWORD()
        if self.dbg.SymGetLineFromAddrW64(self.handle, self.BASE + offset, ctypes.byref(line_disp), ctypes.byref(line)):
            name += "  [%s:%d]" % (os.path.basename(line.FileName), line.LineNumber)
        return name


def read_crash(folder):
    text = open(os.path.join(folder, "CrashContext.runtime-xml"), encoding="utf-8", errors="replace").read()

    def tag(name):
        match = re.search(r"<%s>(.*?)</%s>" % (name, name), text, re.S)
        return match.group(1).strip() if match else ""

    stack = tag("PCallStack")
    offsets = [int(o, 16) for o in re.findall(r"^\s*Hitmen\S*\s+0x[0-9a-fA-F]+ \+ ([0-9a-fA-F]+)", stack, re.M)]
    return {
        "folder": os.path.basename(folder),
        "error": tag("ErrorMessage").replace("&gt;", ">").replace("&lt;", "<").splitlines()[0] if tag("ErrorMessage") else "",
        "seconds": tag("SecondsSinceStart"),
        "config": tag("BuildConfiguration"),
        "offsets": offsets,
        "stamp": dump_exe_timestamp(os.path.join(folder, "UEMinidump.dmp")),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("build", nargs="?", default=os.path.join(ROOT, "Builds", "Windows Dev"),
                        help="build folder holding Hitmen/Saved/Crashes and Hitmen/Binaries/Win64")
    parser.add_argument("--crashes", help="crash folder (default: <build>/Hitmen/Saved/Crashes)")
    parser.add_argument("--binaries", help="folder with Hitmen.exe and Hitmen.pdb (default: <build>/Hitmen/Binaries/Win64)")
    parser.add_argument("-n", "--frames", type=int, default=16, help="frames to print per stack")
    args = parser.parse_args()

    crashes = args.crashes or os.path.join(args.build, "Hitmen", "Saved", "Crashes")
    binaries = args.binaries or os.path.join(args.build, "Hitmen", "Binaries", "Win64")
    exes = [p for p in glob.glob(os.path.join(binaries, "Hitmen*.exe"))]
    if not exes:
        sys.exit("No Hitmen*.exe in " + binaries)
    exe = exes[0]
    folders = sorted(d for d in glob.glob(os.path.join(crashes, "*")) if os.path.isfile(os.path.join(d, "CrashContext.runtime-xml")))
    if not folders:
        print("No crash report in", crashes)
        return

    local_stamp = pe_timestamp(exe)
    symbolizer = Symbolizer(exe)
    groups = collections.OrderedDict()
    for folder in folders:
        crash = read_crash(folder)
        key = tuple(crash["offsets"][:8])
        groups.setdefault(key, []).append(crash)

    print("%d crash report(s), %d distinct stack(s). Symbols: %s (PE stamp 0x%08x)\n" % (len(folders), len(groups), exe, local_stamp))
    for key, items in sorted(groups.items(), key=lambda kv: -len(kv[1])):
        first = items[0]
        times = sorted(set(i["seconds"] for i in items))
        mismatched = [i for i in items if i["stamp"] is not None and i["stamp"] != local_stamp]
        print("=" * 100)
        print("x%d  %s  (%s, seconds since start: %s)" % (len(items), first["error"], first["config"], ", ".join(times)))
        if mismatched:
            print("  WARNING: %d report(s) come from another exe (stamp 0x%08x): names below may be wrong."
                  % (len(mismatched), mismatched[0]["stamp"]))
        for offset in first["offsets"][:args.frames]:
            print("  %8x  %s" % (offset, symbolizer.resolve(offset)))
        print("  reports: " + ", ".join(i["folder"][13:21] for i in items))
    print()


if __name__ == "__main__":
    main()
