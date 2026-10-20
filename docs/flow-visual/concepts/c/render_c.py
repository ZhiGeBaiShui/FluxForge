#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
concept-c renderer — 符号化流源（像素符文 / 电路节点 / 仪表）
全部图形在本文件内程序生成，无外部贴图。

用法:
  python render_c.py            # 生成全部 PNG + ASCII 构造图
  python render_c.py ascii      # 只打印 ASCII 构造图

坐标: 16x16 单元格, (0,0) 左上, x 右 y 下. 几何中心 = 像素 (7,7).
层:  图标占位 12x12 @2..13  <  刻度环 14x14  <  环 9x9  <  温度点  <  核心 5x5  <  喷口  <  状态
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.dirname(os.path.abspath(__file__))
FONT_PATH = r"C:\Windows\Fonts\msyh.ttc"

# ---------------------------------------------------------------- 调色板
BG        = (10, 14, 19, 255)
SHEET_BG  = (6, 9, 13, 255)
BORDER    = (30, 40, 54, 255)
# 图标占位（中性灰，无彩度，避免与成分色相互抢）
ICON_CAP  = (40, 48, 61, 255)
ICON_NECK = (35, 42, 53, 255)
ICON_EDGE = (38, 46, 56, 255)
ICON_FILL = (23, 28, 36, 255)
ICON_HL   = (31, 39, 49, 255)

SCALE_DIM = (74, 85, 104, 150)
NEEDLE    = (240, 246, 255, 255)
RING_OFF  = (43, 52, 65, 255)
SPARK     = (234, 246, 255, 255)
TRAIL     = (110, 127, 148, 170)
WALL      = (244, 248, 255, 255)
TEMP_COLD = (159, 232, 255, 255)
TEMP_HOT  = (255, 162, 58, 255)
TINT_NEUT = (159, 182, 207, 255)
TINT_COLD = (143, 223, 255, 255)
TINT_HOT  = (255, 176, 120, 255)

# ---------------------------------------------------------------- 成分六族
# 5x5, '#' = 实心. 每族顶部中心 (2,0) 为空 -> 旋转后成为"流口"。
FAMS = {
    "water":  dict(cn="水",  hue=(59, 158, 255),
                   pat=[".#.#.",
                        "#.#.#",
                        ".....",
                        "#.#.#",
                        ".#.#."]),
    "poison": dict(cn="毒",  hue=(168, 85, 247),
                   pat=[".#.#.",
                        "#...#",
                        "#.#.#",
                        "#...#",
                        ".#.#."]),
    "oil":    dict(cn="油",  hue=(232, 166, 43),
                   pat=["..#..",
                        ".###.",
                        "#####",
                        ".###.",
                        "..#.."]),
    "blood":  dict(cn="血",  hue=(214, 40, 57),
                   pat=["..#..",
                        "..#..",
                        "#####",
                        "..#..",
                        "..#.."]),
    "acid":   dict(cn="酸",  hue=(208, 245, 66),
                   pat=["####.",
                        "...#.",
                        "..##.",
                        ".#...",
                        "####."]),
    "energy": dict(cn="能量", hue=(98, 241, 214),
                   pat=[".###.",
                        "#####",
                        "##.##",
                        "#####",
                        ".###."]),
}

DIRS = {
    "U": dict(cn="上", spout=[(7, 4), (7, 3), (7, 2)],
              wall=[(6, 2), (7, 2), (8, 2)],
              split=[(5, 2), (9, 2)],
              rot=0),
    "R": dict(cn="右", spout=[(10, 7), (11, 7), (12, 7)],
              wall=[(12, 6), (12, 7), (12, 8)],
              split=[(12, 5), (12, 9)],
              rot=1),
    "D": dict(cn="下", spout=[(7, 10), (7, 11), (7, 12)],
              wall=[(6, 12), (7, 12), (8, 12)],
              split=[(5, 12), (9, 12)],
              rot=2),
    "L": dict(cn="左", spout=[(4, 7), (3, 7), (2, 7)],
              wall=[(2, 6), (2, 7), (2, 8)],
              split=[(2, 5), (2, 9)],
              rot=3),
}

# 环: 8 枚 2px 灯段 (每边 2 枚), 四正向留 3px 门.
# 填充顺序 = 从下左开始顺时针 (含量弧): 底左/左/顶左/顶右/右/底右
PIP_SEG = [
    [(4, 11), (5, 11)],    # 底左
    [(3, 9), (3, 10)],     # 左下
    [(3, 4), (3, 5)],      # 左上
    [(4, 3), (5, 3)],      # 顶左
    [(9, 3), (10, 3)],     # 顶右
    [(11, 4), (11, 5)],    # 右上
    [(11, 9), (11, 10)],   # 右下
    [(9, 11), (10, 11)],   # 底右
]
PIP_ORDER = [i for i in range(8)]
# 受阻时前侧两段内缩 1px
CRUSH_IDX = {"U": {3, 4}, "R": {5, 6}, "D": {0, 7}, "L": {1, 2}}
CRUSH_VEC = {"U": (0, 1), "R": (-1, 0), "D": (0, -1), "L": (1, 0)}

# 温度: 内侧四角 (4,4)(10,4)(4,10)(10,10)
TEMP_COLD_PIPS = [(4, 10), (10, 10)]
TEMP_HOT_PIPS = [(4, 4), (10, 4)]

# 刻度: 14x14 环, 8 个八分位
SCALE = [(7, 1), (14, 1), (14, 7), (14, 14), (7, 14), (1, 14), (1, 7), (1, 1)]

# 状态火花轨道: 11x11 环, 顺时针, 起点 (7,2), 共 40 px
def orbit_path():
    p = []
    for x in range(2, 13):
        p.append((x, 2))
    for y in range(3, 13):
        p.append((12, y))
    for x in range(11, 1, -1):
        p.append((x, 12))
    for y in range(11, 2, -1):
        p.append((2, y))
    assert len(p) == 40, len(p)
    i = p.index((7, 2))
    return p[i:] + p[:i]

ORBIT = orbit_path()


# ---------------------------------------------------------------- 工具
def rot90(pat):
    """90° 顺时针旋转 5x5 图案。"""
    return ["".join(pat[4 - c][r] for c in range(5)) for r in range(5)]


def mul(c, k):
    return (max(0, min(255, int(c[0] * k))),
            max(0, min(255, int(c[1] * k))),
            max(0, min(255, int(c[2] * k))),
            c[3] if len(c) > 3 else 255)


def blend(a, b, f):
    return tuple(int(a[i] * (1 - f) + b[i] * f) for i in range(4))


class Tile:
    """16x16 画布 + 层标签。"""

    def __init__(self):
        self.img = Image.new("RGBA", (16, 16), BG)
        self.tag = [["." for _ in range(16)] for _ in range(16)]

    def put(self, x, y, color, tag=None):
        if 0 <= x < 16 and 0 <= y < 16:
            self.img.putpixel((x, y), color)
            if tag:
                self.tag[y][x] = tag

    def icon(self):
        # 12x12 @ (2,2)..(13,13)：中性小药瓶占位
        for x in range(5, 11):
            for y in range(2, 4):
                self.put(x, y, ICON_CAP, "i")
        for x in range(6, 10):
            self.put(x, 4, ICON_NECK, "i")
        for x in range(4, 12):
            for y in range(5, 13):
                edge = x in (4, 11) or y in (5, 12)
                self.put(x, y, ICON_EDGE if edge else ICON_FILL, "i")
        for y in range(6, 11):
            self.put(5, y, ICON_HL, "i")
        self.put(6, 10, ICON_HL, "i")
        self.put(7, 10, ICON_HL, "i")

    def dump(self):
        return "\n".join("".join(r) for r in self.tag)


# ---------------------------------------------------------------- 部件
def draw_scale(t, rng, dim=1.0):
    """I4: 8 刻度 + 针. rng: 剩余格数."""
    idx = max(0, min(7, int(rng) // 2))
    for i, (x, y) in enumerate(SCALE):
        if i == idx:
            t.put(x, y, NEEDLE, "N")
        else:
            t.put(x, y, mul(SCALE_DIM, dim), "S")


def draw_ring(t, content, tint, dim=1.0, status="flow", d="U"):
    n = 0 if content <= 0.001 else (2 if content <= 0.25 else
                                    4 if content <= 0.5 else
                                    6 if content <= 0.75 else 8)
    crush = CRUSH_IDX[d] if status == "blocked" else set()
    vx, vy = CRUSH_VEC[d]
    for i, seg in enumerate(PIP_SEG):
        on = i < n
        dx, dy = (vx, vy) if i in crush else (0, 0)
        col = mul(tint, dim) if on else RING_OFF
        for (x, y) in seg:
            t.put(x + dx, y + dy, col, "p" if on else "o")


def draw_temp(t, temp):
    """I2: 内侧角上的霜点/余烬点。冷=下, 热=上。"""
    if temp <= -10:
        pips, col = TEMP_COLD_PIPS, TEMP_COLD
    elif temp <= 5:
        pips, col = TEMP_COLD_PIPS[:1], TEMP_COLD
    elif temp < 55:
        pips, col = [], None
    elif temp < 85:
        pips, col = TEMP_HOT_PIPS[:1], TEMP_HOT
    else:
        pips, col = TEMP_HOT_PIPS, TEMP_HOT
    for (x, y) in pips:
        t.put(x, y, col, "t")


def draw_core(t, fam, d, k=1.0, mono=False):
    pat = FAMS[fam]["pat"]
    for _ in range(DIRS[d]["rot"]):
        pat = rot90(pat)
    col = (255, 255, 255, 255) if mono else mul(FAMS[fam]["hue"], k)
    mouth = {"U": (0, 2), "R": (2, 4), "D": (4, 2), "L": (2, 0)}[d]
    for r in range(5):
        for c in range(5):
            if pat[r][c] == "#" and (r, c) != mouth:
                t.put(5 + c, 5 + r, col, "c")
    mx, my = 5 + mouth[1], 5 + mouth[0]
    t.put(mx, my, t.img.getpixel((mx, my)), "m")


def draw_spout(t, d, k=1.0, blocked=False, hue=(255, 255, 255)):
    pts = DIRS[d]["spout"][:1] if blocked else DIRS[d]["spout"]
    col = mul((hue[0], hue[1], hue[2], 255), 0.80 + 0.20 * k)
    for i, (x, y) in enumerate(pts):
        t.put(x, y, col, "s")


def draw_status(t, status, d, tt, k=1.0, dim=1.0):
    if status == "flow":
        n = len(ORBIT)
        kk = int(round(tt * n)) % n
        t.put(*ORBIT[(kk - 1) % n][:2], TRAIL, "T")
        t.put(*ORBIT[kk], mul(SPARK, k), "k")
    elif status == "blocked":
        for (x, y) in DIRS[d]["wall"]:
            t.put(x, y, mul(WALL, dim), "w")
    elif status == "split":
        for (x, y) in DIRS[d]["split"]:
            t.put(x, y, mul(SPARK, k), "k")
    elif status == "merge":
        for (x, y) in [(7, 2), (12, 7), (7, 12), (2, 7)]:
            t.put(x, y, mul(SPARK, k), "k")
    elif status == "collected":
        pass


# ---------------------------------------------------------------- 组合
LAYER_ORDER = ("icon", "scale", "ring", "temp", "core", "spout", "status")


def compose(fam="water", temp=20, content=0.6, rng=6, d="U",
            status="flow", tt=0.0, layers=LAYER_ORDER, mono=False):
    t = Tile()
    dim_ring = 1.0
    if status == "blocked":
        dim_ring = 0.62
    elif status == "collected":
        dim_ring = 0.34
    elif status == "merge":
        dim_ring = 1.0
    tint = TINT_COLD if temp <= 5 else (TINT_HOT if temp >= 55 else TINT_NEUT)
    k = 0.90 + 0.10 * (0.5 + 0.5 * __import__("math").cos(2 * 3.1415926 * tt))

    if "icon" in layers:
        t.icon()
    if "scale" in layers:
        draw_scale(t, rng, 0.5 if status == "collected" else 1.0)
    if "ring" in layers:
        draw_ring(t, content, tint, dim_ring, status, d)
    if "temp" in layers:
        draw_temp(t, temp)
    if "core" in layers:
        draw_core(t, fam, d, 0.72 if status == "collected" else k, mono)
    if "spout" in layers:
        draw_spout(t, d, k, blocked=(status == "blocked"),
                   hue=(255, 255, 255) if mono else FAMS[fam]["hue"])
    if "status" in layers:
        draw_status(t, status, d, tt,
                    k=0.65 if status == "blocked" else 1.0,
                    dim=0.6 if status == "blocked" else 1.0)
    if mono:
        t = to_mono(t)
    return t


def to_mono(t):
    """去色：所有"信息性"像素变白/灰，检查形状能否独立成立。"""
    img = t.img.copy()
    for y in range(16):
        for x in range(16):
            c = img.getpixel((x, y))
            if c[:3] == BG[:3]:
                continue
            if c[:3] in (ICON_CAP[:3], ICON_NECK[:3], ICON_EDGE[:3],
                         ICON_FILL[:3], ICON_HL[:3]):
                img.putpixel((x, y), (26, 30, 36, 255))
                continue
            lum = 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]
            v = int(min(255, 40 + lum * 0.92))
            img.putpixel((x, y), (v, v, v, 255))
    t.img = img
    return t


# ---------------------------------------------------------------- 字与拼版
def font(sz=13):
    try:
        return ImageFont.truetype(FONT_PATH, sz)
    except Exception:
        return ImageFont.load_default()


def up(img, s):
    return img.resize((img.width * s, img.height * s), Image.NEAREST)


def sheet(items, cols, scale=4, title="", labelsize=13, pad=14, gap=14,
          sub_labels=True):
    """items: [(Tile, label)]"""
    tw = 16 * scale
    f = font(labelsize)
    th = 16 * scale
    lab_h = 20 if sub_labels else 0
    rows = (len(items) + cols - 1) // cols
    Wt = pad * 2 + cols * tw + (cols - 1) * gap
    title_h = 26 if title else 0
    Ht = pad * 2 + title_h + rows * (th + lab_h) + (rows - 1) * gap
    canvas = Image.new("RGB", (Wt, Ht), SHEET_BG)
    d = ImageDraw.Draw(canvas)
    if title:
        d.text((pad, pad - 2), title, font=font(15), fill=(210, 220, 232))
    for i, (tile, label) in enumerate(items):
        r, c = divmod(i, cols)
        x = pad + c * (tw + gap)
        y = pad + title_h + r * (th + lab_h + gap)
        d.rectangle([x - 1, y - 1, x + tw, y + th], outline=BORDER)
        canvas.paste(up(tile.img, scale), (x, y), up(tile.img, scale))
        if sub_labels:
            bbox = d.textbbox((0, 0), label, font=f)
            d.text((x + (tw - (bbox[2] - bbox[0])) // 2, y + th + 3),
                   label, font=f, fill=(168, 180, 196))
    return canvas


def strip(items, scale=1, gap=8, pad=10, border=True):
    tw = 16 * scale
    Wt = pad * 2 + len(items) * tw + (len(items) - 1) * gap
    Ht = pad * 2 + tw
    canvas = Image.new("RGB", (Wt, Ht), SHEET_BG)
    d = ImageDraw.Draw(canvas)
    for i, tile in enumerate(items):
        x = pad + i * (tw + gap)
        y = pad
        if border:
            d.rectangle([x - 1, y - 1, x + tw, y + tw], outline=BORDER)
        im = up(tile.img, scale)
        canvas.paste(im, (x, y), im)
    return canvas


# ---------------------------------------------------------------- 主流程
MIX = ["water", "poison", "oil", "blood", "acid", "energy"]
SAMPLE = [  # (fam, temp, content, rng, dir, status)
    ("water",  20, 0.62, 6,  "U", "flow"),
    ("poison", -12, 0.30, 3, "R", "flow"),
    ("oil",    78, 0.90, 12, "D", "flow"),
    ("blood",  42, 0.12, 1,  "L", "blocked"),
    ("acid",   96, 0.50, 9,  "U", "split"),
    ("energy", -4, 1.00, 15, "R", "merge"),
]


def tile_for(spec, tt=0.0, **kw):
    fam, temp, content, rng, d, status = spec
    return compose(fam=fam, temp=temp, content=content, rng=rng, d=d,
                   status=status, tt=tt, **kw)


def main():
    made = []

    def save(canvas, name):
        p = os.path.join(OUT, name)
        canvas.save(p)
        made.append(name)

    # 1) main-1x / main-4x
    save(strip([tile_for(s) for s in SAMPLE], 1), "main-1x.png")
    lab = [f'{FAMS[s[0]]["cn"]} {DIRS[s[4]]["cn"]} {s[5]}' for s in SAMPLE]
    save(sheet([(tile_for(s), lab[i]) for i, s in enumerate(SAMPLE)],
               cols=6, scale=4, title="C 符号化流源 · 4x 组装（同一背景 + 图标占位）"),
         "main-4x.png")

    # 2) attrs
    items = []
    for m in MIX:
        items.append((compose(fam=m, temp=20, content=0.75, rng=6, d="U"),
                      FAMS[m]["cn"] + " 形+色"))
    for m in MIX:
        items.append((compose(fam=m, temp=20, content=0.75, rng=6, d="U", mono=True),
                      FAMS[m]["cn"] + " 去色"))
    save(sheet(items, cols=6, scale=4,
               title="I1 成分：形状（去色行）与色相 双通道"),
         "attrs.png")

    # 3) temp
    temps = [(-20, "深冷 -20"), (-5, "冷 -5"), (20, "常温 20"), (60, "热 60"), (100, "灼热 100")]
    save(sheet([(compose(fam="water", temp=t, content=0.55, rng=6, d="U"), l)
                for t, l in temps], cols=5, scale=4,
               title="I2 温度：静态可读（霜点位置/数量 + 环染）"),
         "temp.png")

    # 4) content
    conts = [(0.0, "空"), (0.25, "少"), (0.5, "中"), (0.75, "多"), (1.0, "满")]
    save(sheet([(compose(fam="water", temp=20, content=c, rng=6, d="U"), l)
                for c, l in conts], cols=5, scale=4,
               title="I3 含量：环段数 0/2/4/6/8（自下左顺时针填充）"),
         "content.png")

    # 5) dirs
    save(sheet([(compose(fam="water", temp=20, content=0.6, rng=6, d=x,
                         status="flow", tt=0.55), "上" if x == "U" else
                "右" if x == "R" else "下" if x == "D" else "左")
                for x in "URDL"], cols=4, scale=5,
               title="I6 方向：喷口（3px）+ 字形旋转"),
         "dirs.png")

    # 6) status
    stats = [("flow", 0.55, "流动"), ("blocked", 0.0, "受阻"),
             ("split", 0.0, "分流"), ("merge", 0.0, "汇聚"),
             ("collected", 0.0, "被收集")]
    save(sheet([(compose(fam="water", temp=20, content=0.62, rng=6, d="U",
                         status=s, tt=tt), l) for s, tt, l in stats],
               cols=5, scale=4,
               title="I5 状态：火花数量/墙/环压缩/亮度"),
         "status.png")

    # 7) motion
    frames = [(i / 10.0) for i in range(11)]
    save(sheet([(compose(fam="water", temp=20, content=0.62, rng=6, d="U",
                         status="flow", tt=f), f"{f:.1f}s") for f in frames],
               cols=6, scale=4,
               title="运动时间轴 0.0-1.0s：火花绕行 1 圈 / 喷口 4Hz / 核心呼吸"),
         "motion.png")

    # 8) layers
    base = dict(fam="water", temp=-12, content=0.75, rng=6, d="U",
                status="split", tt=0.55)
    lay = [("核心 I1", ("core",)),
           ("环 I3", ("ring",)),
           ("温度 I2", ("temp",)),
           ("火花 I5", ("status",)),
           ("喷口 I6", ("spout",)),
           ("刻度 I4", ("scale",)),
           ("合成", LAYER_ORDER)]
    items = [(compose(layers=l, **base), n) for n, l in lay]
    save(sheet(items, cols=7, scale=4,
               title="分层构造（水位=水, -12C, 75%, 6 格, 上, 分流, t=0.55）"),
         "layers.png")

    # 9) rotation: 6 族 x 4 向, 去色
    items = []
    for m in MIX:
        for x in "URDL":
            items.append((compose(fam=m, temp=20, content=0.75, rng=6, d=x,
                                  mono=True), f'{FAMS[m]["cn"]}{DIRS[x]["cn"]}'))
    save(sheet(items, cols=12, scale=3,
               title="I1 x I6 正交：六族 x 四向（去色，验证字形可辨）"),
         "rotation.png")

    # 10) ASCII 构造图
    anchors = [
        ("锚 A: 水 / 上 / 75% / 6格 / 20C / 流动 t=0.55", compose(
            fam="water", temp=20, content=0.75, rng=6, d="U",
            status="flow", tt=0.55)),
        ("锚 B: 血 / 右 / 50% / 10格 / -12C / 受阻", compose(
            fam="blood", temp=-12, content=0.5, rng=10, d="R",
            status="blocked", tt=0.0)),
    ]
    with open(os.path.join(OUT, "ascii-anchors.txt"), "w", encoding="utf-8") as fh:
        for name, tile in anchors:
            fh.write(name + "\n")
            fh.write("   " + "".join(str(i % 10) for i in range(16)) + "\n")
            for y, line in enumerate(tile.tag):
                fh.write(f"{y:2d} " + "".join(line) + "\n")
            fh.write("\n")
    print("wrote", ", ".join(made), "ascii-anchors.txt")


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "ascii":
        a = compose(fam="water", temp=20, content=0.75, rng=6, d="U",
                    status="flow", tt=0.55)
        print("   " + "".join(str(i % 10) for i in range(16)))
        for y in range(16):
            print(f"{y:2d} " + a.dump()[y * 16:(y * 16 + 16)])
    else:
        main()
