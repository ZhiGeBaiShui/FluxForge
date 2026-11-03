import os
from PIL import Image

GEN = "E:/code/FluxForge/generated-images/"

# 检查 shape 掩码与管道几何是否对齐
CASES = [
    ("route_l_turn", "1110", 2),
    ("route_t_junction", "010111", 3),
    ("route_cross", "010111010", 3),
    ("route_double_emit", "11", 1),
    ("chem_reactor_base", "010111010", 3),
]

for name, shape, sw in CASES:
    p = GEN + name + ".png"
    if not os.path.exists(p):
        continue
    im = Image.open(p).convert("RGBA")
    w, h = im.size
    px = im.load()
    rows = len(shape) // sw
    cell_w, cell_h = w // sw, h // rows
    print("=" * 70)
    print(f"{name} [{w}x{h}] shape={shape} cell={cell_w}x{cell_h}")
    for r in range(rows):
        line = []
        for c in range(sw):
            # 每格中心
            cx, cy = c * cell_w + cell_w // 2, r * cell_h + cell_h // 2
            a = px[cx, cy][3]
            line.append("■" if a in (1, 255) else "·")
        print(f"   row{r}: " + "".join(line) + f"   ({''.join(shape[r*sw:(r+1)*sw])})")

# 关键: 检查 route_l_turn 的 shape "1110" 是否与几何一致
print("\n" + "=" * 70)
print("几何/掩码一致性详查")
im = Image.open(GEN + "route_l_turn.png").convert("RGBA")
px = im.load()
print("route_l_turn 关键点 (0=透明,1=命中黑,255=实色):")
for y in [4, 12, 16, 24, 28]:
    row = ""
    for x in range(0, 32, 2):
        a = px[x, y][3]
        row += "." if a == 0 else ("#" if a == 1 else "O")
    print(f"  y={y:2d}: {row}")
print("  (x 每列代表 2px, 索引 0..15)")
