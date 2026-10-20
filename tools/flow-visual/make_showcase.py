"""用终稿渲染器生成一张大尺寸展示图（8×4 格 @8x），作为交付门面。"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

FINAL = Path(__file__).resolve().parent.parent.parent / "docs/flow-visual/final"
sys.path.insert(0, str(FINAL))
import render_final as rf  # noqa: E402

OUT = FINAL / "png"
CELL = 16
S = 8
COLS, ROWS = 8, 4
W, H = COLS * CELL * S, ROWS * CELL * S
GRID_A = (30, 25, 23)
GRID_B = (27, 23, 21)
LINE = (46, 39, 36)
TEXT = (235, 225, 215)
DIM = (150, 140, 130)
FONT = r"C:\Windows\Fonts\msyh.ttc"


def font(sz):
    try:
        return ImageFont.truetype(FONT, sz)
    except Exception:
        return ImageFont.load_default()


SHOW = [
    # x, y, comp, temp, content, state, dir, icon
    (1, 1, "water", "cold", "mid", "flow", "right", True),
    (3, 2, "oil", "hot", "large", "flow", "up", False),
    (5, 1, "blood", "normal", "small", "flow", "left", False),
    (6, 3, "acid", "cold", "large", "blocked", "down", False),
]


def main():
    top, bottom = 52, 66
    img = Image.new("RGB", (W + 24, top + H + bottom), (22, 19, 18))
    d = ImageDraw.Draw(img)
    d.text((12, 8), "流源 · 终稿视觉（8×4 格，8× 最近邻）", fill=TEXT, font=font(20))
    d.text((12, 32), "成分 = 色相 + 内部纹理 · 温度 = 边缘色 + 霜/汽 · 含量 = 体积 · 射程 = 底部三点 · 状态 = 物理直喻",
           fill=DIM, font=font(12))

    ox, oy = 12, top
    for r in range(ROWS):
        for c in range(COLS):
            fill = GRID_A if (r + c) & 1 else GRID_B
            d.rectangle([ox + c * CELL * S, oy + r * CELL * S,
                         ox + (c + 1) * CELL * S - 1, oy + (r + 1) * CELL * S - 1], fill=fill)
    for c in range(COLS + 1):
        d.rectangle([ox + c * CELL * S, oy, ox + c * CELL * S + 2, oy + H], fill=LINE)
    for r in range(ROWS + 1):
        d.rectangle([ox, oy + r * CELL * S, ox + W, oy + r * CELL * S + 2], fill=LINE)

    for (x, y, comp, temp, content, state, dirn, icon) in SHOW:
        out, lay, bbox, g = rf.render(comp=comp, temp=temp, content=content, state=state,
                                      dirn=dirn, rng=3, icon=icon)
        tile = out.convert("RGBA")
        big = tile.resize((CELL * S, CELL * S), Image.NEAREST)
        img.paste(big, (ox + x * CELL * S, oy + y * CELL * S), big)

    d.text((12, top + H + 10),
           "示例：水/冷/中（左上，叠在物品图标上） · 油/热/多（中） · 血/常温/少（右上） · 酸/冷/受阻（右下，墙在运动方向前方）",
           fill=(200, 190, 180), font=font(12))
    d.text((12, top + H + 32),
           "1 格 = 16px；实机 1 格 ≈ 113 屏幕像素（≈7×）。射程点画在最上层，任何状态都不遮挡。",
           fill=DIM, font=font(11))
    out = OUT / "showcase-8x.png"
    img.save(out)
    print(out, img.size)


if __name__ == "__main__":
    main()
