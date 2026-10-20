"""把 atlas.json 转成浏览器可直接 <script> 引入的 atlas.js。

支持三种 atlas.json：
  1) {"water|cold|large|right|flow": [x, y], ...}
  2) {"tiles": [{"key": "...", "x": 0, "y": 0}, ...]}
  3) [{"key": "...", "x": 0, "y": 0}, ...]

输出：final/atlas.js
    window.FLOW_ATLAS = { "key": [x, y], ... };
    window.FLOW_ATLAS_SIZE = 16;
"""
import json
import sys
from pathlib import Path


def normalize(data):
    out = {}
    if isinstance(data, dict) and "tiles" in data and isinstance(data["tiles"], list):
        for t in data["tiles"]:
            out[str(t["key"])] = [int(t["x"]), int(t["y"])]
        return out
    if isinstance(data, list):
        for t in data:
            out[str(t["key"])] = [int(t["x"]), int(t["y"])]
        return out
    if isinstance(data, dict):
        for k, v in data.items():
            if isinstance(v, (list, tuple)) and len(v) >= 2:
                out[str(k)] = [int(v[0]), int(v[1])]
            elif isinstance(v, dict) and "x" in v and "y" in v:
                out[str(k)] = [int(v["x"]), int(v["y"])]
        return out
    raise ValueError("无法识别的 atlas 格式")


def main():
    src = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("docs/flow-visual/final/atlas.json")
    dst = src.with_suffix(".js")
    data = json.loads(src.read_text(encoding="utf-8"))
    atlas = normalize(data)
    dst.write_text("window.FLOW_ATLAS = " + json.dumps(atlas, ensure_ascii=False, separators=(",", ":"))
                   + ";\nwindow.FLOW_ATLAS_SIZE = 16;\n", encoding="utf-8")
    print(f"{dst}  ({len(atlas)} keys)")


if __name__ == "__main__":
    main()
