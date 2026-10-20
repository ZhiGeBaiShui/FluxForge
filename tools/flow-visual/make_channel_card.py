"""生成「六通道总览卡」：一张图解释整套设计。"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

FINAL = Path(__file__).resolve().parent.parent.parent / "docs/flow-visual/final"
sys.path.insert(0, str(FINAL))
import render_final as rf  # noqa: E402

OUT = FINAL / "png" / "channel-card.png"
S = 6
CELL = 16 * S
PAD = 10
LEFT = 132
TOP = 78
ROW_H = CELL + 34
FONT = r"C:\Windows\Fonts\msyh.ttc"
BG = (24, 21, 19)
LINE = (52, 45, 40)
TEXT = (236, 226, 216)
DIM = (150, 140, 130)
ACCENT = (47, 224, 200)


def font(sz):
    try:
        return ImageFont.truetype(FONT, sz)
    except Exception:
        return ImageFont.load_default()


def tile(comp="water", temp="normal", content="large", state="flow", dirn="right", rng=3, icon=False):
    out, lay, bbox, g = rf.render(comp=comp, temp=temp, content=content, state=state,
                                  dirn=dirn, rng=rng, icon=icon)
    return out.convert("RGB").resize((CELL, CELL), Image.NEAREST)


ROWS = [
    ("I1 成分", "色相+纹理\n6 色可分\ndeutan ≥4",
     [("水", tile(comp="water")), ("毒", tile(comp="poison")), ("油", tile(comp="oil")),
      ("血", tile(comp="blood")), ("酸", tile(comp="acid")), ("能量", tile(comp="energy"))]),
    ("I2 温度", "边缘色\n+霜点/汽孔\n三档同盒",
     [("冷", tile(temp="cold")), ("常温", tile(temp="normal")), ("热", tile(temp="hot"))]),
    ("I3 含量", "体积\n6×1/6×6/8×8/12×8\n档差 ≥42%",
     [("空", tile(content="empty")), ("少", tile(content="small")),
      ("中", tile(content="mid")), ("多", tile(content="large"))]),
    ("I4 射程", "格底 3 点\n亮 3/2/1\n与含量正交",
     [("3 格", tile(rng=3)), ("2 格", tile(rng=2)), ("1 格", tile(rng=1))]),
    ("I5 状态", "墙 / 两行空\n亮缝 + V\n连续流",
     [("流动", tile(state="flow")), ("受阻", tile(state="blocked")), ("分流", tile(state="split")),
      ("汇聚", tile(state="merge")), ("被收集", tile(state="collect"))]),
    ("I6 方向", "钝头 + 高光\n亮核偏前\n无 1px 尖角",
     [("上", tile(dirn="up")), ("右", tile(dirn="right")), ("下", tile(dirn="down")), ("左", tile(dirn="left"))]),
]


def main():
    max_tiles = max(len(r[2]) for r in ROWS)
    W = LEFT + max_tiles * (CELL + PAD) + PAD
    H = TOP + len(ROWS) * ROW_H + 46
    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img)

    d.text((14, 12), "流源视觉 · 六通道总览", fill=TEXT, font=font(20))
    d.text((14, 40), "一个属性只占一条通道；宽度/粗细永不参与编码。调色后 23/25（调色前 25/25）：方向/温度/含量/射程/状态全对",
           fill=DIM, font=font(11))

    for i, (name, desc, items) in enumerate(ROWS):
        y = TOP + i * ROW_H
        d.rectangle([0, y - 6, W, y - 6 + 1], fill=LINE)
        d.text((14, y + 4), name, fill=ACCENT, font=font(15))
        for k, ln in enumerate(desc.split("\n")):
            d.text((14, y + 23 + k * 11), ln, fill=DIM, font=font(10))
        for j, (label, im) in enumerate(items):
            x = LEFT + j * (CELL + PAD)
            d.rectangle([x - 1, y - 1, x + CELL, y + CELL], outline=LINE)
            img.paste(im, (x, y))
            d.text((x + 2, y + CELL + 4), label, fill=(205, 195, 185), font=font(11))

    d.text((14, H - 32), "16px/格 · 设计空间；实机 1 格 ≈ 113 屏幕像素（≈7×）。复跑：final/render_final.py",
           fill=DIM, font=font(10))
    img.save(OUT)
    print(OUT, img.size)


if __name__ == "__main__":
    main()



