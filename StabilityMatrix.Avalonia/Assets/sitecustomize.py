"""
Startup site customization for Stability Matrix.

Currently this installs an audit hook to notify the parent process when input() is called,
so we can prompt the user to enter something.
"""

import sys
import json

# Application Program Command escape sequence
# This wraps messages sent to the parent process.
esc_apc = "\x9F"
esc_prefix = "[SM;"
esc_st = "\x9C"


def send_apc(msg: str):
    """Send an Application Program Command to the parent process."""
    sys.stdout.flush()
    sys.stdout.write(esc_apc + esc_prefix + msg + esc_st)
    sys.stdout.flush()

def send_apc_json(type: str, data: str):
    """Send an APC Json message."""
    send_apc(json.dumps({"type": type, "data": data}))

def send_apc_input(prompt: str):
    """Apc message for input() prompt."""
    send_apc_json("input", prompt)

def audit(event: str, *args):
    """Main audit hook function."""
    # https://docs.python.org/3/library/functions.html#input
    # input() raises audit event `builtins.input` with args (prompt: str) *before* reading from stdin.
    # `builtins.input/result` raised after reading from stdin.

    if event == "builtins.input":
        try:
            prompts = args[0] if args else ()
            prompt = "".join(prompts)
            send_apc_input(prompt)
        except Exception:
            pass


# Reconfigure stdout to UTF-8
# noinspection PyUnresolvedReferences
sys.stdin.reconfigure(encoding="utf-8")
sys.stdout.reconfigure(encoding="utf-8")
sys.stderr.reconfigure(encoding="utf-8")

# Install the audit hook
sys.addaudithook(audit)
