"""从 blind-b 的 22 个 tile 生成按属性拆分的盲测表（temp/content/dir/state）。"""
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
BLIND = ROOT / "docs/flow-visual/round2/blind-b"
PY = sys.executable

items = json.loads((BLIND / "manifest.json").read_text(encoding="utf-8"))
groups = {}
for it in items:
    groups.setdefault(it["group"], []).append(it)

for g, arr in groups.items():
    m = BLIND / f"manifest-{g}.json"
    m.write_text(json.dumps(arr, ensure_ascii=False, indent=2), encoding="utf-8")
    subprocess.run([PY, str(HERE / "qa.py"), "blind",
                    "--manifest", str(m),
                    "--out", str(BLIND / f"blind-{g}.png"),
                    "--key", str(BLIND / f"key-{g}.json"),
                    "--cols", "6", "--scale", "4"], check=True)
    print(g, len(arr))
