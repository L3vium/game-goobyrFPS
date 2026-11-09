"""Call a UFUNCTION on the live (PIE) instance of a widget class, for UI tests driven from outside:
    python Tools/Editor/ue_remote.py "import sys; sys.argv=['', 'HitmenTitleScreenWidget', 'CreateRoom']; exec(open(r'Tools/Editor/ue_ui_call.py').read())"
Arguments: widget class name (without U), function name, optional args."""
import sys
import unreal

cls = getattr(unreal, sys.argv[1])
live = [w for w in unreal.ObjectIterator(cls) if "Transient" in w.get_path_name() or "PIE" in w.get_path_name()]
if not live:
    print("no live", sys.argv[1])
else:
    live[-1].call_method(sys.argv[2], tuple(sys.argv[3:]))
    print("called", sys.argv[2], "on", live[-1].get_path_name())
