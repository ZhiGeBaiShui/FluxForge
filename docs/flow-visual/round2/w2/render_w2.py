#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
W2「B-core · 内核 + 环」渲染器
================================
在 concepts/b 的液滴语言之上，加入两个**结构件**：
  1) 常驻亮核（1x1 / 2x2 / 4x2，随温度变形，随成分子纹理）
  2) 头部外侧 1px 弧环（11px，分 3 段，亮段数 = 射程）
只写 docs/flow-visual/round2/w2/ 下的东西，不读写任何游戏资源。

坐标：格内 16x16 像素，(0,0) 左上；基准方向 = 右。
用法：
    python render_w2.py            # 出全部 PNG + tiles
    python render_w2.py --dump     # 只打印字符网格 / quads / 时间轴
"""

import os
import sys
import json
from collections import defaultdict
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "png")
TILDIR = os.path.join(HERE, "tiles")
W = 16
FONT_PATH = "C:/Windows/Fonts/msyh.ttc"

# ---------------------------------------------------------------- 基色
PAPER = (10, 11, 14)
BG = (14, 16, 21)
GRID = (30, 34, 42)
ICON = (78, 86, 106)
TEXT = (214, 220, 230)
DIM = (122, 130, 146)
ACCENT = (255, 206, 92)

COMP = {
    "water":  (39, 199, 230),
    "poison": (155, 92, 255),
    "oil":    (240, 179, 43),
    "blood":  (224, 58, 86),
    "acid":   (127, 209, 43),
    "energy": (233, 242, 255),
}
COMP_CN = {"water": "水", "poison": "毒", "oil": "油",
           "blood": "血", "acid": "酸", "energy": "能量"}
COMP_ORDER = ["water", "poison", "oil", "blood", "acid", "energy"]

TEMP_CN = {"cold": "冷 -10℃", "ambient": "常温 21℃", "hot": "热 78℃"}
TIER_CN = {"empty": "空", "small": "少", "mid": "中", "large": "多"}
TIER_ML = {"empty": "0", "small": "<0.5L", "mid": "0.5–10L", "large": "≥10L"}
STATE_CN = {"flow": "流动", "blocked": "受阻", "split": "分流",
            "merge": "汇聚", "collect": "被收集"}
DIR_CN = {"right": "右", "down": "下", "left": "左", "up": "上"}

# ================================================================ 几何常量
LEAD = 13          # 身体前沿列（右向）
CY = 7             # 垂直中心行
RING_X = 14        # 弧环列：身体外 1px，距格边 1px
RING_SEGS = [(2, 4), (6, 8), (10, 12)]   # 三段，各 3px，间隙 1px（y=5 / y=9）
RANGE_N = {"long": 3, "mid": 2, "short": 1}
RANGE_CN = {"long": "长 ≥8 格", "mid": "中 3–7 格", "short": "短 0–2 格"}

# 四档体积：包围盒 6x1 / 6x5 / 9x7 / 12x9（右向行表；头恒在 x=13，尾向后伸）
TIERS = {
    "large": {3: (9, 12), 4: (7, 13), 5: (5, 13), 6: (3, 13), 7: (2, 13),
              8: (3, 13), 9: (5, 13), 10: (7, 13), 11: (9, 12)},
    "mid":   {4: (11, 12), 5: (9, 13), 6: (7, 13), 7: (5, 13),
              8: (7, 13), 9: (9, 13), 10: (11, 12)},
    "small": {5: (11, 12), 6: (9, 13), 7: (8, 13), 8: (9, 13), 9: (11, 12)},
    "empty": {7: (8, 13)},
}

# 核：三档形态（格内绝对坐标；front edge 见 spec）
CORE = {
    "cold":    [(9, 7)],
    "ambient": [(10, 6), (11, 6), (10, 7), (11, 7)],
    "hot":     [(9, 6), (10, 6), (11, 6), (12, 6),
                (9, 7), (10, 7), (11, 7), (12, 7)],
}

# 核内 1px 纹理（成分子通道）；("set", 额外像素, 种类) / ("ov", 覆盖, 种类)
CORE_TEX = {
    "water":  [],
    "poison": [((11, 6), "dot")],
    "oil":    [((10, 7), "oil"), ((11, 7), "oil")],
    "blood":  [((10, 6), "dark")],
    "acid":   [((10, 6), "dot"), ((11, 6), "acid_lo")],
    "energy": [((10, 5), "arm"), ((9, 6), "arm"),
               ((12, 7), "arm"), ((11, 8), "arm")],
}

# 轮廓微差（酸=减法：上缘咬掉 2 px；毒=加法：头右上凸 1 px）
ACID_NOTCH = {"large": [(9, 3), (7, 4)], "mid": [(11, 4), (9, 5)],
              "small": [(11, 5), (8, 7)]}
POISON_BUMP = {"large": (13, 3), "mid": (13, 4), "small": (13, 5)}

# 温度静态轮廓线索（蒸汽孔只挖内区；缺口在头部下前角，避开毒凸泡）
HOT_PORES = {"large": [(10, 4), (12, 4)], "mid": [(11, 5)], "small": []}
HOT_NOTCH = {"large": (13, 10), "mid": (13, 9), "small": (13, 8)}

SPLIT_COL = {"large": 7, "mid": 8, "small": 9}
MERGE_CUT = {"large": [(2, 7), (3, 7), (4, 7)],
             "mid": [(5, 7), (6, 7), (7, 7)],
             "small": [(8, 7), (9, 7)]}
BLOCK_SHAKE = {1, 2, 4, 5}      # 12fps 下 0.5s = 6 帧内的回拉相位


# ================================================================ 基础工具
def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def shade(c, f):
    if f >= 0:
        return tuple(min(255, int(round(v + (255 - v) * f))) for v in c)
    return tuple(max(0, int(round(v * (1 + f)))) for v in c)


def rows_mask(rows):
    m = set()
    for y, (x0, x1) in rows.items():
        for x in range(x0, x1 + 1):
            m.add((x, y))
    return m


def bbox(m):
    xs = [p[0] for p in m]
    ys = [p[1] for p in m]
    return min(xs), min(ys), max(xs), max(ys)


def rim_of(mask):
    out = set()
    for (x, y) in mask:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in mask:
                out.add((x, y))
                break
    return out


def rot(direction):
    n = {"right": 0, "down": 1, "left": 2, "up": 3}[direction]

    def f(p):
        x, y = p
        for _ in range(n):
            x, y = W - 1 - y, x
        return (x, y)
    return f


# ================================================================ 形变
def state_mask(m, tier, state):
    m = set(m)
    if state == "flow":
        return m
    if state == "blocked":
        # 尾尖回缩 1px：顶住了，尾巴被拽回去；剪影其余不动
        x0, y0, x1, y1 = bbox(m)
        row = [p for p in m if p[1] == CY and p[0] == x0]
        return m - set(row)
    if state == "split":
        gap = SPLIT_COL[tier]
        return {(x, y) for (x, y) in m if not (x == gap and 5 <= y <= 9)}
    if state == "merge":
        # 尾端 V 谷：两股在后端还没合拢
        return m - set(MERGE_CUT[tier])
    if state == "collect":
        # 前沿中段开口 3px + 上下各 1px 亮唇
        m = m - {(LEAD, CY - 1), (LEAD, CY), (LEAD, CY + 1)}
        return m
    return m


def shear_rows(m, tier, frame):
    """整行 ±1px 剪切；永不动核所在行 6/7，永不碰弧环列 14。"""
    if frame == 0:
        return m
    x0, y0, x1, y1 = bbox(m)
    rows = defaultdict(list)
    for (x, y) in m:
        rows[y].append(x)
    back = sorted([y for y in rows if y not in (6, 7) and min(rows[y]) > x0])
    fwd = sorted([y for y in rows if y not in (6, 7) and max(rows[y]) <= 12])
    if not back:
        return m
    n = len(back)
    k = (frame * n) // 12
    yb = back[k % n]
    shift = {yb: -1}
    if fwd:
        yf = fwd[(k + 1) % len(fwd)]
        shift[yf] = 1
    return {(x + shift.get(y, 0), y) for (x, y) in m}


# ================================================================ 构建
def build(comp="water", tier="large", temp="ambient", state="flow",
          direction="right", frame=0, rng="long"):
    """返回 {(x,y): (part, sub)}，part 用于 quads 归因与着色。"""
    base = rows_mask(TIERS[tier])
    if tier == "empty":
        roles = {}
        for x in range(8, 14):
            roles[(x, CY)] = ("stain", 2 if x in (10, 11) else 1)
        return {rot(direction)(p): r for p, r in roles.items()}

    m = state_mask(base, tier, state)
    if comp == "acid":
        m -= set(ACID_NOTCH[tier])
    if comp == "poison":
        m |= {POISON_BUMP[tier]}
    if temp == "hot":
        m -= set(HOT_PORES[tier]) | {HOT_NOTCH[tier]}
    if frame and state == "flow":
        m = shear_rows(m, tier, frame)

    rim = rim_of(m)
    roles = {}
    for p in m:
        roles[p] = ("rim", 0) if p in rim else ("body", 0)

    # --- 核（温度通道；含量无关，恒 2x2@常温）
    for p in CORE[temp]:
        if p in roles:
            roles[p] = ("core", 0)
    for p, kind in CORE_TEX[comp]:
        if p in roles:
            roles[p] = ("ctex", kind)

    # --- 弧环（射程通道；几何恒定，只有亮段数变）
    nlit = RANGE_N[rng]
    for i, (a, b) in enumerate(RING_SEGS):
        for y in range(a, b + 1):
            roles[(RING_X, y)] = ("ring", "lit" if i < nlit else "dim")

    # --- 冷：尾部 1px 霜点 x2（在包围盒内，不改总体尺寸）
    if temp == "cold":
        x0 = min(p[0] for p in m)
        for dy in (-1, 1):
            roles[(x0, CY + dy)] = ("frost", 0)

    # --- 状态附加
    if state == "blocked":
        for x in (SPLIT_COL[tier] - 1, SPLIT_COL[tier] + 1):
            for y in (CY - 1, CY, CY + 1):
                if (x, y) in roles and roles[(x, y)][0] in ("body", "rim"):
                    roles[(x, y)] = ("rib", 0)
    if state == "collect":
        for y in (CY - 2, CY + 2):
            if (LEAD, y) in roles and roles[(LEAD, y)][0] == "rim":
                roles[(LEAD, y)] = ("lip", 0)

    # --- 刚体旋转
    f = rot(direction)
    roles = {f(p): r for p, r in roles.items()}

    # --- 受阻回拉：整体（除环）沿反运动方向 1px
    if state == "blocked" and frame in BLOCK_SHAKE:
        dv = {"right": (-1, 0), "down": (0, -1),
              "left": (1, 0), "up": (0, 1)}[direction]
        roles = {(p[0] + dv[0], p[1] + dv[1]): r for p, r in roles.items()
                 if r[0] != "ring"}
    return roles


# ================================================================ 着色
def colorize(roles, comp, temp):
    base = COMP[comp]
    rim_c = shade(base, -0.55)
    if comp == "energy":
        rim_c = (143, 227, 255)
    if temp == "cold":
        rim_c = mix(rim_c, (206, 232, 255), 0.45)
    elif temp == "hot":
        rim_c = mix(rim_c, (255, 150, 86), 0.45)

    core_c = shade(base, 0.55)
    if comp == "energy":
        core_c = (246, 250, 255)          # 提亮后仍非纯白
    ring_lit = shade(base, 0.70)
    ring_dim = shade(base, 0.36)
    if comp == "energy":
        ring_lit = (196, 245, 255)
        ring_dim = (92, 150, 172)

    out = {}
    for p, r in roles.items():
        k, sub = r
        if k == "body":
            out[p] = (base, 208)
        elif k == "rim":
            out[p] = (rim_c, 255)
        elif k == "core":
            out[p] = (core_c, 255)
        elif k == "ctex":
            if sub == "dot":
                out[p] = (shade(base, 0.84), 255)
            elif sub == "oil":
                out[p] = (shade(base, 0.80), 255)
            elif sub == "dark":
                out[p] = (shade(base, -0.62), 255)
            elif sub == "acid_lo":
                out[p] = (shade(base, -0.42), 255)
            elif sub == "arm":
                out[p] = ((86, 214, 255) if comp == "energy"
                          else shade(base, -0.58), 255)
        elif k == "ring":
            out[p] = (ring_lit, 255) if sub == "lit" else (ring_dim, 64)
        elif k == "frost":
            out[p] = ((223, 241, 255), 255)
        elif k == "rib":
            out[p] = (shade(base, -0.62), 255)
        elif k == "lip":
            out[p] = (shade(base, 0.74), 255)
        elif k == "stain":
            out[p] = (mix(BG, base, 0.30 if sub == 1 else 0.50), 215)
    return out


# ---------------------------------------------------------------- 像素图
def draw_icon(px, kind="bottle"):
    """12x12 图标占位，落在 (2,2)-(13,13)。kind: bottle / bright / busy"""
    ox, oy = 2, 2
    if kind == "bright":
        for y in range(12):
            for x in range(12):
                px[oy + y][ox + x] = (196, 190, 168, 255)
        return
    if kind == "busy":
        for y in range(12):
            for x in range(12):
                v = 210 if (x // 2 + y // 2) % 2 else 96
                px[oy + y][ox + x] = (v, v, v, 255)
        return
    pts = [(x, 0) for x in range(4, 7)]
    for y in range(1, 3):
        pts += [(4, y), (6, y)]
    pts += [(2, 3), (9, 3)]
    for y in range(4, 10):
        pts += [(2, y), (9, y)]
    pts += [(x, 10) for x in range(2, 10)]
    for (x, y) in pts:
        xx, yy = ox + x, oy + y
        if 0 <= xx < W and 0 <= yy < W:
            px[yy][xx] = ICON + (168,)


def cell_image(layers, icon=None, gray=False, alpha_icon=255):
    px = [[BG + (255,) for _ in range(W)] for _ in range(W)]
    if icon:
        draw_icon(px, icon)
    for (x, y), (c, a) in layers.items():
        if not (0 <= x < W and 0 <= y < W):
            continue
        bg = px[y][x]
        af = a / 255.0
        px[y][x] = (int(c[0] * af + bg[0] * (1 - af)),
                    int(c[1] * af + bg[1] * (1 - af)),
                    int(c[2] * af + bg[2] * (1 - af)), 255)
    img = Image.new("RGB", (W, W))
    img.putdata([px[y][x] for y in range(W) for x in range(W)])
    if gray:
        img = img.convert("L")
        luma = img.copy()
        img = img.convert("RGB")
    return img


def luma(img_rgb):
    return None


def cell_rgba(layers):
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    for (x, y), (c, a) in layers.items():
        if 0 <= x < W and 0 <= y < W:
            img.putpixel((x, y), (c[0], c[1], c[2], a))
    return img


def cell_for(comp="water", tier="large", temp="ambient", state="flow",
             direction="right", frame=0, rng="long", icon=None, gray=False):
    layers = colorize(build(comp, tier, temp, state, direction, frame, rng),
                      comp, temp)
    return cell_image(layers, icon=icon, gray=gray)


# ================================================================ quads
def greedy_rects(cells):
    """cells: {(x,y): key} -> {key: 矩形数}；每块同色同 alpha 最大矩形 = 1 quad。"""
    groups = defaultdict(set)
    for p, k in cells.items():
        groups[k].add(p)
    out = {}
    for k, ps in groups.items():
        rem = set(ps)
        n = 0
        while rem:
            n += 1
            x0, y0 = min(rem, key=lambda p: (p[1], p[0]))
            w = 1
            while (x0 + w, y0) in rem:
                w += 1
            h = 1
            while all((x0 + i, y0 + h) in rem for i in range(w)):
                h += 1
            rem -= {(x0 + i, y0 + j) for i in range(w) for j in range(h)}
        out[k] = n
    return out


def quads_report(comp, tier, temp, state, direction, frame=0, rng="long"):
    roles = build(comp, tier, temp, state, direction, frame, rng)
    layers = colorize(roles, comp, temp)
    cells = {p: (layers[p][0], layers[p][1]) for p in layers}
    total = sum(greedy_rects(cells).values())
    # 按 part 归因
    by_part = {}
    for p, r in roles.items():
        by_part.setdefault(r[0], {})[p] = r
    partq = {}
    for part, rs in by_part.items():
        sub = {}
        for p, r in rs.items():
            sub[p] = (layers[p][0], layers[p][1])
        partq[part] = sum(greedy_rects(sub).values())
    return total, partq, len(roles)


# ================================================================ 字体/板
def font(sz):
    try:
        return ImageFont.truetype(FONT_PATH, sz)
    except Exception:
        return ImageFont.load_default()


F_T = font(21)
F_H = font(14)
F_B = font(12)
F_S = font(10)


def sheet(w, h, title, sub=""):
    img = Image.new("RGB", (w, h), PAPER)
    d = ImageDraw.Draw(img)
    d.text((18, 12), title, font=F_T, fill=TEXT)
    if sub:
        d.text((18, 39), sub, font=F_B, fill=DIM)
    d.line((18, 58, w - 18, 58), fill=(40, 44, 54))
    return img, d


def paste(img, x, y, im, scale):
    if scale != 1:
        im = im.resize((W * scale, W * scale), Image.NEAREST)
    img.paste(im, (x, y))
    return im


def frame_box(d, x, y, scale, col=GRID):
    d.rectangle((x - 1, y - 1, x + W * scale, y + W * scale), outline=col)


# ---------------------------------------------------------------- 分区图版
def render_sections(path, title, sub, sections, scale=4, gray=False,
                    pitch=None, note_lines=()):
    cell = W * scale
    gap = 16 if scale >= 4 else 8
    maxn = max(len(s[2]) for s in sections)
    left = 22
    width = max(left * 2 + maxn * (cell + gap) - gap, 620 if scale >= 4 else 340)
    row_h = 20 + 16 + cell + 14 + (W + 18 if scale >= 4 else W + 18) + 18
    height = 74 + len(sections) * row_h + 16 + 22 * len(note_lines)
    img, d = sheet(width, height, title, sub)

    y = 74
    for name, note, items in sections:
        d.text((left, y), name, font=F_H, fill=ACCENT)
        d.text((left + 96, y + 2), note, font=F_S, fill=DIM)
        yy = y + 22
        for i, (lab, layer_fn) in enumerate(items):
            x = left + i * (cell + gap)
            im = layer_fn()
            paste(img, x, yy, im, scale)
            frame_box(d, x, yy, scale)
            d.text((x + cell // 2, yy + cell + 3), lab, font=F_S,
                   fill=TEXT, anchor="ma")
            if scale >= 4:
                paste(img, x + (cell - W) // 2, yy + cell + 18, im, 1)
                frame_box(d, x + (cell - W) // 2, yy + cell + 18, 1,
                          (26, 30, 38))
        if scale >= 4:
            d.text((left, yy + cell + 34), "1×", font=F_S, fill=DIM)
            y = yy + cell + 34 + W + 12
        else:
            y = yy + cell + 16

    ty = height - 16 - 22 * len(note_lines)
    for i, t in enumerate(note_lines):
        d.text((left, ty + i * 22), t, font=F_S,
               fill=TEXT if i == 0 else DIM)
    img.save(path)
    return img


# ================================================================ 主图
def render_main(scale):
    path = os.path.join(OUT, "main-%dx.png" % scale)
    cell = W * scale
    gap = 14 if scale >= 4 else 6
    items = [
        ("水", dict(comp="water")),
        ("毒", dict(comp="poison")),
        ("油", dict(comp="oil")),
        ("血", dict(comp="blood")),
        ("酸", dict(comp="acid")),
        ("能量", dict(comp="energy")),
        ("冷", dict(temp="cold")),
        ("热", dict(temp="hot")),
        ("空", dict(tier="empty")),
        ("少", dict(tier="small")),
        ("中", dict(tier="mid")),
        ("射程 1 段", dict(rng="short")),
        ("射程 2 段", dict(rng="mid")),
        ("分流", dict(state="split")),
        ("汇聚", dict(state="merge")),
        ("受阻", dict(state="blocked")),
    ]
    cols = 8
    rows = (len(items) + cols - 1) // cols
    left, top = 22, 76
    width = left * 2 + cols * (cell + gap) - gap
    height = top + rows * (cell + 18) + 60
    title = ("W2「B-core」— 液滴 + 亮核 + 头部弧环 · %d×" %
             scale) + (" NEAREST" if scale >= 4 else " 原尺寸")
    img, d = sheet(width, height, title,
                   "核=成分色+55%（非纯白）；环=头部外 1px、3 段、亮段数=射程；"
                   "温度改核形态，不改包围盒")
    for i, (lab, kw) in enumerate(items):
        r, c = divmod(i, cols)
        x = left + c * (cell + gap)
        y = top + r * (cell + 18)
        im = cell_for(**kw)
        paste(img, x, y, im, scale)
        frame_box(d, x, y, scale)
        if scale >= 4:
            d.text((x + cell // 2, y + cell + 2), lab, font=F_S, fill=TEXT,
                   anchor="ma")
    d.text((left, height - 44),
           "1× 判读：核在哪、核多宽、环几段亮 —— 这三件事就是温度、温度、射程。",
           font=F_S, fill=DIM)
    d.text((left, height - 26),
           "成分靠色相 + 核内 1px 纹理 + 轮廓微差（酸上缘缺 2 px / 毒右上凸 1 px）。",
           font=F_S, fill=DIM)
    img.save(path)


# ================================================================ attrs
def _sections_all(gray=False):
    S = []
    S.append(("I1 成分", "色相 + 核内 1px 纹理 + 轮廓微差；灰度下仍分 6 组",
              [(COMP_CN[c], (lambda c=c: cell_for(comp=c, gray=gray)))
               for c in COMP_ORDER]))
    S.append(("I2 温度", "核 1×1 后移 / 2×2 / 4×2 前移 1px；外加霜点 / 蒸汽孔 + 缺口",
              [(TEMP_CN[t].split()[0],
                (lambda t=t: cell_for(temp=t, gray=gray)))
               for t in ("cold", "ambient", "hot")]))
    S.append(("I3 含量", "包围盒 6×1 / 6×5 / 9×7 / 12×9；核大小恒 2×2，不随含量变",
              [(TIER_CN[t], (lambda t=t: cell_for(tier=t, gray=gray)))
               for t in ("empty", "small", "mid", "large")]))
    S.append(("I4 射程", "头部外侧 1px 弧环 3 段，亮段数 3/2/1；几何位置恒定",
              [(RANGE_CN[r].split()[0],
                (lambda r=r: cell_for(rng=r, gray=gray)))
               for r in ("short", "mid", "long")]))
    S.append(("I5 状态", "只改拓扑：缝 / 谷 / 口 / 肋 + 1px 回拉",
              [(STATE_CN[s], (lambda s=s: cell_for(state=s, gray=gray)))
               for s in ("flow", "blocked", "split", "merge", "collect")]))
    S.append(("I6 方向", "同一掩膜绕格心刚性旋转；头距格边恒 2px、环恒 1px",
              [(DIR_CN[dr], (lambda dr=dr: cell_for(direction=dr, gray=gray)))
               for dr in ("right", "down", "left", "up")]))
    return S


def render_attrs(scale, gray=False):
    name = "attrs-gray-%dx.png" % scale if gray else "attrs-%dx.png" % scale
    path = os.path.join(OUT, name)
    title = ("W2「B-core」— 属性通道（%s · %d×）" %
             ("灰度" if gray else "彩色", scale))
    sub = ("左：4× 构造；每格下方：1× 原尺寸。灰度 = 逐像素亮度，最严苛的色盲代理"
           if scale >= 4 else "1× 原尺寸；验收「一眼可分类」")
    notes = ()
    if not gray:
        notes = ("通道分工：核=温度（形态）/ 环=射程（亮段数）/ 面积=含量 / "
                 "核内纹理+轮廓=成分 / 拓扑=状态 / 旋转=方向。",
                 "同一像素不承担两个属性：环在 x=14，核在 x=9..12，轮廓微差在 y=3/4 与 x=13。")
    return render_sections(path, title, sub, _sections_all(gray),
                           scale=scale, gray=gray, note_lines=notes)


# ================================================================ temp
def render_temp(scale):
    path = os.path.join(OUT, "temp-%dx.png" % scale)
    S = [
        ("水", "同一成分，只换温度：核形态 + 霜点/蒸汽孔/缺口 + 环恒 3 段",
         [(TEMP_CN[t].split()[0], (lambda t=t: cell_for(comp="water", temp=t)))
          for t in ("cold", "ambient", "hot")]),
        ("酸", "酸本身有上缘缺口，仍不与温度线索混：温度线索只在核 / 头部前角",
         [(TEMP_CN[t].split()[0], (lambda t=t: cell_for(comp="acid", temp=t)))
          for t in ("cold", "ambient", "hot")]),
        ("能量·小含量", "小含量档的退化：蒸汽孔 0 个（核已占满顶排），缺口仍在",
         [(TEMP_CN[t].split()[0],
           (lambda t=t: cell_for(comp="energy", temp=t, tier="small")))
          for t in ("cold", "ambient", "hot")]),
    ]
    notes = ("三档总体包围盒逐像素相同（含环 13×11，x 2..14 / y 2..12）："
             "冷加霜点、热挖孔，都在盒内。",
             "核面积：冷 1 px / 常温 4 px / 热 8 px（4× 跨度），单帧可判，不依赖动画。")
    return render_sections(path, "W2「B-core」— P2 温度（静态单帧）",
                           "核=温度主通道；霜点 / 蒸汽孔+缺口=静态外轮廓副通道",
                           S, scale=scale, note_lines=notes)


# ================================================================ content
def render_content(scale):
    path = os.path.join(OUT, "content-%dx.png" % scale)
    S = [("体积四档", "头恒在 x=13，只有尾巴向后伸；核恒 2×2 停在 x=10..11",
          [(TIER_CN[t], (lambda t=t: cell_for(tier=t)))
           for t in ("empty", "small", "mid", "large")])]
    S.append(("同四档 · 灰度", "灰度下仍靠面积（6 / 22 / 43 / 80 px）分档，核不变",
              [(TIER_CN[t], (lambda t=t: cell_for(tier=t, gray=True)))
               for t in ("empty", "small", "mid", "large")]))
    notes = ("「少」= 小、整、贴着头；不是散点、不是暗。四档面积 6 / 22 / 43 / 80 px，"
             "相邻档 ≥1.8×（要求 ≥1.3×）。",
             "核的像素数在四档里恒为 4（常温 2×2）—— 含量不可能被误读成温度。")
    return render_sections(path, "W2「B-core」— P4 含量（体积，不是密度）",
                           "面积是含量通道；核与环的像素数不参与", S, scale=scale,
                           note_lines=notes)


# ================================================================ range
def render_range(scale):
    path = os.path.join(OUT, "range-%dx.png" % scale)
    S = [("射程三档", "环分 3 段（各 3px、间隙 1px）；亮段数 = 3 / 2 / 1",
          [(RANGE_CN[r].split()[0], (lambda r=r: cell_for(rng=r)))
           for r in ("long", "mid", "short")])]
    S.append(("含量 × 射程", "9 种组合：环的位置与长度逐像素不变，只有亮段数变",
              [("%s·%s" % (TIER_CN[t], RANGE_CN[r].split()[0]),
                (lambda t=t, r=r: cell_for(tier=t, rng=r)))
               for t in ("small", "mid", "large") for r in ("long", "short")]))
    notes = ("环几何恒定：x=14、y=2..12、段 [2-4][6-8][10-12]，与含量无关；"
             "因此「环少一段」永远只可能是射程。",
             "未亮的段保留 alpha 64（可见但暗），环的整体长度不随射程变 —— "
             "避免「短射程」被读成「含量少」。")
    return render_sections(path, "W2「B-core」— P3 射程（头部弧环）",
                           "与含量同时变化时：面积变、环段数不变 / 环段数变、面积不变",
                           S, scale=scale, note_lines=notes)


# ================================================================ dirs
def render_dirs(scale):
    path = os.path.join(OUT, "dirs-%dx.png" % scale)
    ARROW = {"right": "右 →", "down": "下 ↓", "left": "左 ←", "up": "上 ↑"}
    S = [("四向", "三条同向线索：弧环在头侧 / 核偏前(距头 2px) / 前端 7 行钝口",
          [(ARROW[d], (lambda d=d: cell_for(direction=d)))
           for d in ("right", "down", "left", "up")])]
    S.append(("受阻四向", "回拉方向永远朝尾；环不动 → 头与环的 1px 间隙读作「顶住了」",
              [(ARROW[d],
                (lambda d=d: cell_for(direction=d, state="blocked", frame=1)))
               for d in ("right", "down", "left", "up")]))
    notes = ("方向主线索不是「尾巴细」（B 稿盲测 4/4 读反，1px 尖尾被当成头），"
             "而是：环在头侧、核贴头、前端钝口宽 7 行。尾巴只作次线索。",
             "沿运动轴：头（含壳）距格边 2px、环距格边 1px、尾距格边 2px —— 四向逐像素相同；"
             "垂直轴 ±0.5px 来自 9 行的奇数高度，是形状自身的偏，不是对齐误差。")
    return render_sections(path, "W2「B-core」— P5 方向（不靠猜）",
                           "围盒与头/尾到格边的距离表见 spec §P5", S,
                           scale=scale, note_lines=notes)


# ================================================================ states
def render_states(scale):
    path = os.path.join(OUT, "states-%dx.png" % scale)
    S = [("五态", "只改拓扑，不改宽度/核大小/环段数（射程仍 3 段）",
          [(STATE_CN[s], (lambda s=s: cell_for(state=s, frame=1 if s == "blocked" else 0)))
           for s in ("flow", "blocked", "split", "merge", "collect")])]
    S.append(("受阻抖动 6 帧", "12fps × 6 = 0.5s；整体回拉 1px，环不动",
              [("f%d" % f, (lambda f=f: cell_for(state="blocked", frame=f)))
               for f in range(6)]))
    notes = ("受阻：f0 无回拉，f1/f2 回拉 1px，f3 无，f4/f5 回拉 —— 0.5s 一个循环，"
             "几何只沿 −运动方向，不会读成位置漂移。",
             "五态的区分件：肋（受阻）/ 缝（分流）/ 尾谷（汇聚）/ 口+唇（收集）/ 无（流动）。")
    return render_sections(path, "W2「B-core」— P6 状态",
                           "静止画面即可分出 5 种", S, scale=scale,
                           note_lines=notes)


# ================================================================ occlusion
def render_occlusion(scale):
    path = os.path.join(OUT, "occlusion-%dx.png" % scale)
    cell = W * scale
    gap = 18
    left, top = 22, 76
    icons = [("深色瓶", "bottle"), ("亮底图标", "bright"), ("棋盘图标", "busy")]
    comps = [("水", "water"), ("酸", "acid"), ("能量", "energy")]
    width = left * 2 + len(icons) * (cell + gap) - gap
    height = top + len(comps) * (cell + 20) + 74
    img, d = sheet(width, height,
                   "W2「B-core」— P8 遮挡（叠在 12×12 图标之上）",
                   "流源 = 壳 alpha 208 + 核/环 alpha 255；三层叠加，图标始终可透出")
    for r, (cn, ck) in enumerate(comps):
        for c, (lab, ic) in enumerate(icons):
            x = left + c * (cell + gap)
            y = top + r * (cell + 20)
            im = cell_for(comp=ck, icon=ic)
            paste(img, x, y, im, scale)
            frame_box(d, x, y, scale)
            if r == 0:
                d.text((x + cell // 2, y - 14), lab, font=F_S, fill=DIM,
                       anchor="ma")
        d.text((left - 2, top + r * (cell + 20) + cell // 2), cn, font=F_B,
               fill=ACCENT, anchor="rm")
    d.text((left, height - 52),
           "最坏情况：亮底图标（L≈196）上，壳 alpha 208 → 合成后仍比图标暗 ≥40 级，剪影不断。",
           font=F_S, fill=TEXT)
    d.text((left, height - 34),
           "核 alpha 255 是全场最亮结构，但核只占 4 px、且不贴边；环在图标外 1px 的格边侧。",
           font=F_S, fill=DIM)
    img.save(path)


# ================================================================ tiles
def export_tiles():
    os.makedirs(TILDIR, exist_ok=True)
    man = {"note": "盲测用：文件名不含答案，answer 只在 manifest 里",
           "grid": 16, "bg": "transparent", "pivot": "格心 (7.5,7.5)",
           "tiles": []}
    n = 0
    stmt = []
    for c in COMP_ORDER:
        stmt.append(("comp", c, dict(comp=c)))
    for t in ("cold", "ambient", "hot"):
        stmt.append(("temp", t, dict(temp=t)))
    for t in ("empty", "small", "mid", "large"):
        stmt.append(("content", t, dict(tier=t)))
    for r in ("short", "mid", "long"):
        stmt.append(("range", r, dict(rng=r)))
    for d in ("right", "down", "left", "up"):
        stmt.append(("dir", d, dict(direction=d)))
    for s in ("flow", "blocked", "split", "merge", "collect"):
        stmt.append(("state", s, dict(state=s)))
    for grp, ans, kw in stmt:
        n += 1
        fn = "t%02d.png" % n
        roles = build(**kw)
        layers = colorize(roles, kw.get("comp", "water"), kw.get("temp", "ambient"))
        im = cell_rgba(layers)
        im.save(os.path.join(TILDIR, fn))
        q, partq, npx = quads_report(kw.get("comp", "water"), kw.get("tier", "large"),
                                     kw.get("temp", "ambient"),
                                     kw.get("state", "flow"),
                                     kw.get("direction", "right"), 0,
                                     kw.get("rng", "long"))
        man["tiles"].append({"file": fn, "group": grp, "answer": ans,
                             "quads": q, "px": npx,
                             "meta": {k: v for k, v in kw.items()}})

    # 结构件 tile：核（6 成分 x 3 温度）+ 环（3 档）
    atlas = {"core": [], "ring": [], "body": []}
    n2 = n
    for c in COMP_ORDER:
        for t in ("cold", "ambient", "hot"):
            n2 += 1
            fn = "t%02d.png" % n2
            roles = {}
            for p in CORE[t]:
                roles[p] = ("core", 0)
            for p, kind in CORE_TEX[c]:
                if p in roles:
                    roles[p] = ("ctex", kind)
            layers = colorize(roles, c, t)
            cell_rgba(layers).save(os.path.join(TILDIR, fn))
            atlas["core"].append({"file": fn, "comp": c, "temp": t,
                                  "rect": "4x2 上限", "quads": 1})
    for r in ("short", "mid", "long"):
        n2 += 1
        fn = "t%02d.png" % n2
        roles = {}
        for i, (a, b) in enumerate(RING_SEGS):
            for y in range(a, b + 1):
                roles[(RING_X, y)] = ("ring", "lit" if i < RANGE_N[r] else "dim")
        layers = colorize(roles, "water", "ambient")
        cell_rgba(layers).save(os.path.join(TILDIR, fn))
        atlas["ring"].append({"file": fn, "range": r, "rect": "1x11", "quads": 1})
    man["atlas"] = atlas
    man["image_fallback"] = {
        "layers": 3,
        "quads": 3,
        "tiles": "body(16x16) + core(1 tile) + ring(1 tile) = 1 贴图 tile 各 1 张",
        "atlas_size": "512x512（32x32 槽）= 60 body + 18 core + 3 ring = 81 槽"}
    with open(os.path.join(TILDIR, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(man, f, ensure_ascii=False, indent=1)
    return man


# ================================================================ dump
SYM = {"body": "#", "rim": "@", "core": "C", "ring": "=", "frost": "*",
       "rib": "|", "lip": "^", "stain": "~"}
TSYM = {"dot": "c", "oil": "o", "dark": "d", "acid_lo": "a", "arm": "+"}


def grid_str(comp, tier, temp, state, direction, frame=0, rng="long"):
    roles = build(comp, tier, temp, state, direction, frame, rng)
    lines = []
    for y in range(W):
        s = ""
        for x in range(W):
            r = roles.get((x, y))
            if not r:
                s += "."
            elif r[0] == "ctex":
                s += TSYM.get(r[1], "?")
            elif r[0] == "ring":
                s += "=" if r[1] == "lit" else "-"
            else:
                s += SYM.get(r[0], "?")
        lines.append("%2d %s" % (y, s))
    lines.append("   " + "".join(str(x % 10) for x in range(W)))
    return "\n".join(lines)


def dump():
    cases = [
        ("右/水/多/常温/流动/long", dict(comp="water", direction="right")),
        ("上/水/多/常温/流动/long", dict(comp="water", direction="up")),
        ("右/毒/多/常温/流动/long", dict(comp="poison")),
        ("右/酸/多/常温/流动/long", dict(comp="acid")),
        ("右/能量/多/常温/流动/long", dict(comp="energy")),
        ("右/水/多/冷/流动/long", dict(temp="cold")),
        ("右/水/多/热/流动/long", dict(temp="hot")),
        ("右/水/少/常温/流动/long", dict(tier="small")),
        ("右/水/中/常温/流动/long", dict(tier="mid")),
        ("右/水/空/常温/流动/long", dict(tier="empty")),
        ("右/水/多/常温/受阻/1", dict(state="blocked", frame=1)),
        ("右/水/多/常温/分流", dict(state="split")),
        ("右/水/多/常温/汇聚", dict(state="merge")),
        ("右/水/多/常温/收集", dict(state="collect")),
    ]
    for name, kw in cases:
        print("\n=== %s ===" % name)
        print(grid_str(kw.get("comp", "water"), kw.get("tier", "large"),
                       kw.get("temp", "ambient"), kw.get("state", "flow"),
                       kw.get("direction", "right"), kw.get("frame", 0),
                       kw.get("rng", "long")))

    print("\n=== quads ===")
    qcases = [
        ("水/多/常温/流动", "water", "large", "ambient", "flow", "right", 0, "long"),
        ("水/多/冷/流动", "water", "large", "cold", "flow", "right", 0, "long"),
        ("水/多/热/流动", "water", "large", "hot", "flow", "right", 0, "long"),
        ("毒/多/常温", "poison", "large", "ambient", "flow", "right", 0, "long"),
        ("油/多/常温", "oil", "large", "ambient", "flow", "right", 0, "long"),
        ("血/多/常温", "blood", "large", "ambient", "flow", "right", 0, "long"),
        ("酸/多/常温", "acid", "large", "ambient", "flow", "right", 0, "long"),
        ("能量/多/常温", "energy", "large", "ambient", "flow", "right", 0, "long"),
        ("能量/多/热", "energy", "large", "hot", "flow", "right", 0, "long"),
        ("水/多/常温/受阻", "water", "large", "ambient", "blocked", "right", 1, "long"),
        ("水/多/常温/分流", "water", "large", "ambient", "split", "right", 0, "long"),
        ("水/多/常温/汇聚", "water", "large", "ambient", "merge", "right", 0, "long"),
        ("水/多/常温/收集", "water", "large", "ambient", "collect", "right", 0, "long"),
        ("水/多/常温/上", "water", "large", "ambient", "flow", "up", 0, "long"),
        ("水/中/常温", "water", "mid", "ambient", "flow", "right", 0, "long"),
        ("水/少/常温", "water", "small", "ambient", "flow", "right", 0, "long"),
        ("水/空/常温", "water", "empty", "ambient", "flow", "right", 0, "long"),
        ("水/多/常温/f5", "water", "large", "ambient", "flow", "right", 5, "long"),
    ]
    print("%-20s %5s  %s" % ("case", "total", "by part"))
    worst = 0
    for name, c, t, tp, st, di, fr, rg in qcases:
        q, partq, npx = quads_report(c, t, tp, st, di, fr, rg)
        worst = max(worst, q)
        print("%-20s %5d  %s  px=%d" % (name, q,
              " ".join("%s:%d" % (k, v) for k, v in sorted(partq.items())), npx))
    print("worst =", worst)

    print("\n=== bbox / 边缘距离（多） ===")
    for di in ("right", "down", "left", "up"):
        m = {p for p, r in build(direction=di).items() if r[0] != "ring"}
        ring = {p for p, r in build(direction=di).items() if r[0] == "ring"}
        bx = bbox(m)
        rx = bbox(ring)
        allp = bbox(m | ring)
        print("%-6s body x%2d..%2d y%2d..%2d  ring x%2d..%2d y%2d..%2d  all %2dx%2d  "
              "body中心(%.1f,%.1f)" %
              (di, bx[0], bx[2], bx[1], bx[3], rx[0], rx[2], rx[1], rx[3],
               allp[2] - allp[0] + 1, allp[3] - allp[1] + 1,
               (bx[0] + bx[2]) / 2, (bx[1] + bx[3]) / 2))

    print("\n=== 含量面积 ===")
    for t in ("empty", "small", "mid", "large"):
        m = rows_mask(TIERS[t])
        b = bbox(m)
        print("%-6s bbox %dx%d  area %d" % (t, b[2] - b[0] + 1,
                                            b[3] - b[1] + 1, len(m)))

    print("\n=== 时间轴 f0..f11（水/多，行剪切） ===")
    for f in range(12):
        m0 = rows_mask(TIERS["large"])
        m1 = shear_rows(m0, "large", f)
        rows0 = defaultdict(list)
        rows1 = defaultdict(list)
        for (x, y) in m0:
            rows0[y].append(x)
        for (x, y) in m1:
            rows1[y].append(x)
        d = {}
        for y in sorted(rows0):
            a, b = min(rows0[y]), min(rows1.get(y, [99]))
            if a != b:
                d[y] = b - a
        b = bbox(m1)
        print("f%-2d 行位移 %-28s bbox %dx%d area %d" %
              (f, str(d), b[2] - b[0] + 1, b[3] - b[1] + 1, len(m1)))

    print("\n=== 灰度亮度（body / core / rim） ===")
    for c in COMP_ORDER:
        b = COMP[c]
        core = shade(b, 0.55)
        rim = shade(b, -0.55)
        def L(x):
            return int(round(0.299 * x[0] + 0.587 * x[1] + 0.114 * x[2]))
        print("%-7s body #%02X%02X%02X L=%3d   core #%02X%02X%02X L=%3d   "
              "rim L=%3d" % (c, b[0], b[1], b[2], L(b),
                             core[0], core[1], core[2], L(core), L(rim)))

    print("\n=== 环坐标表 ===")
    for i, (a, b) in enumerate(RING_SEGS):
        print("S%d: x=14, y=%d..%d (%d px)" % (i + 1, a, b, b - a + 1))
    print("gap: y=5, y=9")


# ================================================================ main
def main():
    os.makedirs(OUT, exist_ok=True)
    if "--dump" in sys.argv:
        dump()
        return
    render_main(1)
    render_main(4)
    for sc in (1, 4):
        render_attrs(sc, gray=False)
        render_attrs(sc, gray=True)
        render_temp(sc)
        render_content(sc)
        render_range(sc)
        render_dirs(sc)
        render_states(sc)
        render_occlusion(sc)
    export_tiles()
    print("done ->", OUT, "|", TILDIR)


if __name__ == "__main__":
    main()
