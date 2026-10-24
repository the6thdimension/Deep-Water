"""Run a Python file inside the open Blender through the local MCP add-on (HTTP on 127.0.0.1:9876).

usage: python -I bl.py <script.py>
The script runs with `bpy` available; whatever it assigns to `result` comes back as JSON.
"""
import json, sys, urllib.request

user = open(sys.argv[1], encoding="utf-8").read()
# The add-on execs with separate globals/locals, which hides a script's module-level names from its own
# functions. Run the script inside one namespace instead and hand back its `result`.
code = ("G = {'bpy': bpy, '__name__': '__gq__'}\n"
        f"exec(compile({user!r}, {sys.argv[1]!r}, 'exec'), G)\n"
        "result = G.get('result')\n")
req = urllib.request.Request("http://127.0.0.1:9876", data=json.dumps({"id": sys.argv[1], "type": "execute_python", "code": code}).encode(),
                             headers={"Content-Type": "application/json"}, method="POST")
with urllib.request.urlopen(req, timeout=120) as r:
    resp = json.loads(r.read())
if resp.get("status") != "success":
    print("ERROR:", resp.get("error"))
    print(resp.get("traceback", ""))
    sys.exit(1)
print(json.dumps(resp.get("result"), indent=1, ensure_ascii=False))
