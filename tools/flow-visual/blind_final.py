"""为终稿 tiles 生成按属性拆分的盲测表（final/blind/）。"""
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
FINAL = ROOT / "docs/flow-visual/final"
TILES = FINAL / "tiles"
OUT = FINAL / "blind"

LABELS = {
    "comp": ["water", "poison", "oil", "blood", "acid", "energy"],
    "temp": ["cold", "ambient", "hot"],
    "content": ["empty", "small", "mid", "large"],
    "range": ["long", "mid", "short", "3", "2", "1"],
    "dir": ["up", "right", "down", "left"],
    "state": ["flow", "blocked", "split", "merge", "collect"],
}


def main():
    manifest = json.loads((TILES / "manifest.json").read_text(encoding="utf-8"))
    items = manifest["tiles"] if isinstance(manifest, dict) else manifest
    OUT.mkdir(parents=True, exist_ok=True)
    groups = {}
    for it in items:
        p = it.get("path") or it.get("file")
        groups.setdefault(it.get("group", "misc"), []).append({
            "path": str(TILES / p),
            "group": it.get("group", "misc"),
            "answer": it.get("answer"),
            "meta": json.dumps(it.get("meta", {}), ensure_ascii=False),
        })
    for g, arr in groups.items():
        m = OUT / f"manifest-{g}.json"
        m.write_text(json.dumps(arr, ensure_ascii=False, indent=2), encoding="utf-8")
        subprocess.run([sys.executable, str(HERE / "qa.py"), "blind",
                        "--manifest", str(m), "--out", str(OUT / f"blind-{g}.png"),
                        "--key", str(OUT / f"key-{g}.json"),
                        "--cols", str(min(6, len(arr))), "--scale", "8"], check=True)
    print({g: len(v) for g, v in groups.items()})


if __name__ == "__main__":
    main()
