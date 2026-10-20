#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
W1「B-solid · 剪影优先」渲染器（设计文档见同目录 spec.md）
只写 docs/flow-visual/round2/w1/ 下的文件；不读写任何游戏资源。

用法:
    python render_w1.py --dump   # 字符网格 / 面积 / quads / 正交性表
    python render_w1.py          # png/*.png + tiles/**
"""

import os
import sys
import json
from PIL import Image, ImageDraw, ImageFont

W = 16
HERE = os.path.dirname(os.path.abspath(__file__))
PNG_DIR = os.path.join(HERE, "png")
TILE_DIR = os.path.join(HERE, "tiles")
FONT_PATH = "C:/Windows/Fonts/msyh.ttc"

PAPER = (10, 11, 14)
BG = (14, 16, 21)
GRID_LINE = (32, 36, 45)
ICON = (92, 100, 118)
TEXT = (214, 220, 230)
DIM = (122, 130, 146)
ACCENT = (255, 206, 92)
WARN = (255, 110, 110)
RIM = (0x24, 0x1E, 0x2E)
ALPHA_BODY = 208

COMP = {"water": (39, 199, 230), "poison": (155, 92, 255), "oil": (240, 179, 43),
        "blood": (224, 58, 86), "acid": (127, 209, 43), "energy": (233, 242, 255)}
COMP_CN = {"water": "水", "poison": "毒", "oil": "油",
           "blood": "血", "acid": "酸", "energy": "能量"}
TEMP_CN = {"cold": "冷 -10C", "ambient": "常温 21C", "hot": "热 78C"}
TIER_CN = {"empty": "空", "small": "少", "mid": "中", "large": "多"}
TIER_ML = {"empty": "0 uL", "small": "<0.5 L", "mid": "0.5-10 L", "large": ">=10 L"}
STATE_CN = {"flow": "流动", "blocked": "受阻", "split": "分流",
            "merge": "汇聚", "collect": "被收集"}
DIR_CN = {"right": "右", "down": "下", "left": "左", "up": "上"}

# 局部坐标（右向）。mid = 运动轴所在行；rows = 每行 x 区间
TIERS = {
    "large": dict(w=12, h=9, mid=4,
                  rows={0: (6, 9), 1: (4, 10), 2: (3, 11), 3: (2, 11), 4: (0, 11),
                        5: (2, 11), 6: (3, 11), 7: (4, 10), 8: (6, 9)}),
    "mid":   dict(w=9, h=7, mid=3,
                  rows={0: (4, 7), 1: (3, 8), 2: (2, 8), 3: (0, 8),
                        4: (2, 8), 5: (3, 8), 6: (4, 7)}),
    "small": dict(w=6, h=5, mid=2,
                  rows={0: (2, 4), 1: (1, 5), 2: (0, 5), 3: (1, 5), 4: (2, 4)}),
    "empty": dict(w=6, h=1, mid=0, rows={0: (0, 5)}),
}


def o(e):
    return (16 - e + 1) // 2


PLACE = {
    "right": lambda t, p: (o(t["w"]) + p[0], o(t["h"]) + p[1]),
    "left":  lambda t, p: (o(t["w"]) + (t["w"] - 1 - p[0]), o(t["h"]) + (t["h"] - 1 - p[1])),
    "down":  lambda t, p: (o(t["h"]) + (t["h"] - 1 - p[1]), o(t["w"]) + p[0]),
    "up":    lambda t, p: (o(t["h"]) + p[1], o(t["w"]) + (t["w"] - 1 - p[0])),
}

ICON12 = ["....####....", "....#..#....", "....#..#....", "....#..#....",
          "...#....#...", "...#....#...", "..#......#..", "..#..##..#..",
          "..#..##..#..", "..#......#..", "...######...", "............"]
ICON12_BUSY = [("#.#.#.#.#.#." if r % 2 == 0 else ".#.#.#.#.#.#") for r in range(12)]


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def shade(c, f):
    if f >= 0:
        return tuple(int(round(v + (255 - v) * f)) for v in c)
    return tuple(int(round(v * (1 + f))) for v in c)


def desat(c, t):
    L = 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]
    return mix(mix(c, (L, L, L), t), (188, 214, 236), 0.14)


def rim_of(mask):
    out = set()
    for (x, y) in mask:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in mask:
                out.add((x, y))
                break
    return out


def bbox(mask):
    xs = [p[0] for p in mask]
    ys = [p[1] for p in mask]
    return min(xs), min(ys), max(xs), max(ys)


def touches(mask, p):
    x, y = p
    return any((x + dx, y + dy) in mask for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))


def pick_near(pool, target, used=()):
    best, bd = None, 10 ** 9
    for c in sorted(pool):
        if c in used:
            continue
        d = (c[0] - target[0]) ** 2 + (c[1] - target[1]) ** 2
        if d < bd:
            best, bd = c, d
    return best


def row_span(mask, y):
    xs = [x for (x, yy) in mask if yy == y]
    return (min(xs), max(xs)) if xs else None


def row_left(mask, y):
    sp = row_span(mask, y)
    return sp[0] if sp else None


def tier_mask(t):
    m = set()
    for y, (a, b) in t["rows"].items():
        for x in range(a, b + 1):
            m.add((x, y))
    return m


def rear_cap(t):
    """P3 三个槽位（尾尖内侧 1px / 上帽 / 下帽）与尾尖。尾尖永远不亮。"""
    m = tier_mask(t)
    xmin = min(x for x, _ in m)
    tip = sorted([p for p in m if p[0] == xmin], key=lambda p: abs(p[1] - t["mid"]))[0]
    up = row_left(m, tip[1] - 1)
    dn = row_left(m, tip[1] + 1)
    slots = [(tip[0] + 1, tip[1])]
    if up is not None:
        slots.append((up, tip[1] - 1))
    if dn is not None:
        slots.append((dn, tip[1] + 1))
    if len(slots) < 3:
        slots.append((xmin + 2, t["mid"]))
    return slots[:3], tip


def apply_comp(mask, comp, t):
    m = set(mask)
    extra = {}
    if comp == "water":
        return m, extra
    xr, xhe, mid = 0, t["w"] - 1, t["mid"]
    ys, ye = 0, t["h"] - 1

    if comp == "acid":
        x0, x1 = row_span(m, ys)
        keep = [x for x in [x0 + 2 * i for i in range(3)]
                if (x, ys + 1) in m or (x, ys) in m]
        for x in range(x0, x1 + 1):
            if x not in keep:
                m.discard((x, ys))
        for x in keep:
            if (x, ys) not in m:
                m.add((x, ys))

    elif comp == "poison":
        for p in ((xr + 1, mid + 1), (xr + 2, mid + 2)):
            if p not in m and touches(m, p):
                m.add(p)

    elif comp == "oil":
        m.discard((xr, mid))
        p = (xr + 1, mid - 1)
        if p not in m and touches(m, p):
            m.add(p)
        m.discard((xhe, mid - 2))
        m.discard((xhe, mid + 2))

    elif comp == "blood":
        for p in ((xr + 4, mid + 2), (xr + 7, mid + 3)):
            if p in m and p not in rim_of(m):
                extra[p] = ("dot",)

    elif comp == "energy":
        for y in (ys, ye):
            sp = row_span(m, y)
            if not sp:
                continue
            a, b = sp
            cx = (a + b) // 2
            keep = {cx - 1, cx} if b - a + 1 >= 2 else {cx}
            for x in range(a, b + 1):
                if x not in keep:
                    m.discard((x, y))
        m.discard((xhe, mid - 2))
        m.discard((xhe, mid + 2))
    return m, extra


def apply_state(mask, state, t):
    m = set(mask)
    extra = {}
    if state == "flow":
        return m, extra
    xr, xhe, mid = 0, t["w"] - 1, t["mid"]

    if state == "blocked":
        for p in ((xhe, mid - 3), (xhe, mid + 3), (xr, mid - 1), (xr, mid + 1)):
            if p not in m and touches(m, p):
                m.add(p)
        # 停滞肋（1px 深色竖肋）是可选表达：全组合极值超顶时先砍它，见 spec §6.2

    elif state == "split":
        for dx in (0,):
            m.discard((xhe - dx, mid))

    elif state == "merge":
        for p in ((xr + 1, mid - 1), (xr, mid - 1), (xr + 1, mid + 1), (xr, mid + 1)):
            if p not in m and touches(m, p):
                m.add(p)

    elif state == "collect":
        for p in ((xhe, mid - 2), (xhe, mid + 2)):
            m.discard(p)
        for p in ((xhe + 1, mid - 1), (xhe + 1, mid)):
            if p not in m and touches(m, p):
                m.add(p)
    return m, extra


def apply_temp(mask, temp, t, pips=()):
    m = set(mask)
    extra = {}
    if temp == "ambient":
        return m, extra
    mid, xhe = t["mid"], t["w"] - 1
    ys = min(y for _, y in m)
    rim = rim_of(m)
    inner = m - rim

    if temp == "cold":
        want = []
        sp = row_span(m, ys)
        if sp:
            want.append(((sp[0] + sp[1]) // 2, ys))
        if row_span(m, ys + 1):
            want.append((row_left(m, ys + 1), ys + 1))
        if row_span(m, mid + 2):
            want.append((row_left(m, mid + 2), mid + 2))
        used = set(pips)
        for target in want:
            p = pick_near([q for q in rim if q not in used], target, used)
            if p:
                extra[p] = ("frost",)
                used.add(p)

    elif temp == "hot":
        used = set(pips)
        for target in ((xhe - 3, mid - 3), (xhe - 1, mid - 2)):
            p = pick_near([q for q in inner if q not in used], target, used)
            if p:
                extra[p] = ("vent",)
                used.add(p)
        if row_span(m, mid + 2):
            p = pick_near([q for q in rim if q not in used],
                          (row_left(m, mid + 2), mid + 2), used)
            if p:
                extra[p] = ("notch",)
    return m, extra


SHEAR = [0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2, 1]


def apply_motion(mask, frame, state, t):
    m = set(mask)
    if not m:
        return m
    mid = t["mid"]
    if state == "blocked":
        k = frame % 6
        if k in (3, 4, 5):
            xhe = max(x for x, _ in m)
            for y in (mid - 3, mid + 3):
                m.discard((xhe, y))
        return m
    if state in ("split", "merge", "collect"):
        return m
    ys, ye = min(y for _, y in m), max(y for _, y in m)
    xmin, xmax = min(x for x, _ in m), max(x for x, _ in m)
    inner_rows = [y for y in range(ys + 1, ye)
                  if y != mid and row_span(m, y)
                  and row_span(m, y)[0] > xmin and row_span(m, y)[1] < xmax]
    k = SHEAR[frame % 12]
    if k and len(inner_rows) >= 3:
        y1 = inner_rows[min(k, len(inner_rows) - 1)]
        y2 = inner_rows[max(len(inner_rows) - 1 - k, 0)]
        if y1 != y2:
            out = set()
            for (x, y) in m:
                if y == y1:
                    out.add((x + 1, y))
                elif y == y2:
                    out.add((x - 1, y))
                else:
                    out.add((x, y))
            if bbox(out) == bbox(m) and len(out) == len(m):
                m = out
    return m


def build(comp="water", tier="large", temp="ambient", state="flow",
          direction="right", frame=0, rng=3):
    t = TIERS[tier]
    slots, tip = rear_cap(t)

    if tier == "empty":
        roles = {}
        for x in range(6):
            if x in (0, 2, 4):
                roles[(x, 0)] = ("dash", "lit" if x // 2 < rng else "dim")
            else:
                roles[(x, 0)] = ("stain",)
        return {PLACE[direction](t, p): r for p, r in roles.items()}

    mask = tier_mask(t)
    mask, extra_c = apply_comp(mask, comp, t)
    mask, extra_s = apply_state(mask, state, t)
    mask = apply_motion(mask, frame, state, t)

    rim = rim_of(mask)
    ys, ye = min(y for _, y in mask), max(y for _, y in mask)
    xhe, mid = t["w"] - 1, t["mid"]
    roles = {}
    for p in mask:
        y = p[1]
        band = "hi" if y - ys <= 1 else ("lo" if ye - y <= 1 else "mid")
        roles[p] = ("body", band)
    for p in rim:
        roles[p] = ("rim",)

    live = [p for p in slots if p in mask]
    for i, p in enumerate(live):
        roles[p] = ("dash", "lit" if i < rng else "dim")
    if tip in roles:
        roles[tip] = ("tip",)

    inner = mask - rim
    used = set()
    for i, target in enumerate([(xhe - 2, mid + 1), (xhe - 1, mid + 1),
                                (xhe - 2, mid + 2), (xhe - 1, mid + 2)]):
        p = pick_near([q for q in inner if q not in used], target, used)
        if p:
            roles[p] = ("pupil",) if i == 0 else ("core",)
            used.add(p)

    for p, r in extra_c.items():
        if p in roles and roles[p][0] != "core":
            roles[p] = r
    for p, r in extra_s.items():
        if p in roles and roles[p][0] != "core":
            roles[p] = r

    _, extra_t = apply_temp(mask, temp, t, live)
    for p, r in extra_t.items():
        if r[0] in ("vent", "notch"):
            roles.pop(p, None)
        elif p in roles:
            roles[p] = r

    return {PLACE[direction](t, p): r for p, r in roles.items()}


def colorize(roles, comp, temp="ambient"):
    base = COMP[comp]
    out = {}
    for p, r in roles.items():
        k = r[0]
        if k == "body":
            band = r[1]
            c = mix(base, (255, 255, 255), 0.26) if band == "hi" else (
                shade(base, -0.24) if band == "lo" else base)
            if temp == "cold":
                c = desat(c, 0.55)
            elif temp == "hot":
                c = mix(c, (255, 150, 78), 0.16)
            out[p] = (c, ALPHA_BODY)
        elif k == "rim":
            out[p] = (RIM, 255)
        elif k == "dash":
            out[p] = (mix(base, (255, 255, 255), 0.88), 255) if r[1] == "lit" \
                else (mix(base, RIM, 0.55), 255)
        elif k == "core":
            out[p] = (mix(base, (255, 255, 255), 0.84), 255)
        elif k == "pupil":
            out[p] = (mix(base, RIM, 0.80), 255)
        elif k == "tip":
            out[p] = (RIM, 150)
        elif k == "dot":
            out[p] = (mix(base, (26, 6, 12), 0.74), 255)
        elif k == "frost":
            out[p] = ((246, 251, 255), 255)
        elif k == "rib":
            out[p] = (mix(base, RIM, 0.45), 255)
        elif k == "stain":
            out[p] = (mix(BG, base, 0.30), 220)
    return out


def draw_icon(px, glyph, col, a):
    for gy in range(12):
        for gx in range(12):
            if glyph[gy][gx] == "#":
                xx, yy = 2 + gx, 2 + gy
                bg = px[yy][xx]
                q = a / 255.0
                px[yy][xx] = (int(col[0] * q + bg[0] * (1 - q)),
                              int(col[1] * q + bg[1] * (1 - q)),
                              int(col[2] * q + bg[2] * (1 - q)), 255)


def cell_image(colors, icon=True, grid=True, glyph=ICON12, icon_col=ICON, icon_a=205,
               gray=False):
    px = [[BG + (255,) for _ in range(W)] for _ in range(W)]
    if grid:
        for i in range(W):
            for (xx, yy) in ((i, 0), (i, W - 1), (0, i), (W - 1, i)):
                px[yy][xx] = GRID_LINE + (255,)
    if icon:
        draw_icon(px, glyph, icon_col, icon_a)
    for (x, y), (c, a) in colors.items():
        if not (0 <= x < W and 0 <= y < W):
            continue
        bg = px[y][x]
        q = a / 255.0
        px[y][x] = (int(c[0] * q + bg[0] * (1 - q)),
                    int(c[1] * q + bg[1] * (1 - q)),
                    int(c[2] * q + bg[2] * (1 - q)), 255)
    img = Image.new("RGB", (W, W))
    img.putdata([px[y][x][:3] for y in range(W) for x in range(W)])
    return img.convert("L").convert("RGB") if gray else img


def cell_at(comp="water", tier="large", temp="ambient", state="flow",
            direction="right", frame=0, rng=3, **kw):
    return cell_image(colorize(build(comp, tier, temp, state, direction, frame, rng),
                               comp, temp), **kw)


def quads(colors):
    rows = {}
    for (x, y), (c, a) in colors.items():
        rows.setdefault(y, []).append((x, c, a))
    total = 0
    for y, items in rows.items():
        items.sort()
        last = None
        for (x, c, a) in items:
            if last != (c, a):
                total += 1
                last = (c, a)
    return total


def rects(colors):
    """贪心矩形覆盖：同色同 x 区间相邻行合并 = 1 quad（可直接写顶点）。"""
    rows = {}
    for (x, y), (c, a) in colors.items():
        rows.setdefault(y, []).append((x, c, a))
    span = {}
    for y, items in rows.items():
        items.sort()
        out = []
        x0 = x1 = items[0][0]
        col = (items[0][1], items[0][2])
        for (x, c, a) in items[1:]:
            if (c, a) == col and x == x1 + 1:
                x1 = x
            else:
                out.append((x0, x1, col))
                x0 = x1 = x
                col = (c, a)
        out.append((x0, x1, col))
        span[y] = out
    total, prev = 0, {}
    for y in sorted(span):
        cur = {}
        for r in span[y]:
            cur[r] = y
            if prev.get(r) != y - 1:
                total += 1
        prev = cur
    return total


def quads_canonical(comp="water", tier="large", temp="ambient", state="flow",
                    frame=0, rng=3):
    return quads(colorize(build(comp, tier, temp, state, "right", frame, rng), comp, temp))


def rects_canonical(comp="water", tier="large", temp="ambient", state="flow",
                    frame=0, rng=3):
    return rects(colorize(build(comp, tier, temp, state, "right", frame, rng), comp, temp))


F = {}


def f(sz):
    if sz not in F:
        try:
            F[sz] = ImageFont.truetype(FONT_PATH, sz)
        except Exception:
            F[sz] = ImageFont.load_default()
    return F[sz]


def sheet(w, h, title, sub, ts=22, bs=12):
    img = Image.new("RGB", (w, h), PAPER)
    d = ImageDraw.Draw(img)
    d.text((16, 10), title, font=f(ts), fill=TEXT)
    if sub:
        d.text((16, 12 + ts + 4), sub, font=f(bs), fill=DIM)
    y = 12 + ts + (bs + 10 if sub else 0)
    d.line((16, y, w - 16, y), fill=(40, 44, 54))
    return img, d, y + 10


def paste(img, im16, x, y, s, frame_col=None):
    img.paste(im16.resize((W * s, W * s), Image.Resampling.NEAREST), (x, y))
    if frame_col:
        ImageDraw.Draw(img).rectangle((x - 1, y - 1, x + W * s, y + W * s), outline=frame_col)


def render_main(scale, path):
    cell = W * scale
    gap = 6 * (scale // 2) if scale >= 4 else 4
    cols, rows, left = 6, 2, 18
    width = max(left * 2 + cols * (cell + gap) - gap, 360 if scale == 1 else 700)
    ts = 22 if scale >= 4 else 14
    bs = 12 if scale >= 4 else 8
    img, d, y = sheet(width, 1600, "W1 B-solid 剪影优先 %dx%s" % (scale, "" if scale == 1 else " NEAREST"),
                      "12x9 包围盒 / 1px #241E2E / 头重尾轻 / 3 阶明暗 / 叠 12x12 灰色图标占位 + 深色网格",
                      ts=ts, bs=bs)
    top = y + 2
    lab = 9 if scale >= 4 else 6
    for r in range(rows):
        for c in range(cols):
            if r == 0:
                im = cell_at("water", "large", "ambient", "flow", "right", c)
                cap = "f%d" % c
            else:
                comp = list(COMP)[c % 6]
                tier = "large" if c < 4 else ("mid" if c == 4 else "small")
                im = cell_at(comp, tier, "ambient", "flow", "right", 0)
                cap = COMP_CN[comp] + TIER_CN[tier]
            x = left + c * (cell + gap)
            yy = top + r * (cell + gap + (14 if scale >= 4 else 10))
            paste(img, im, x, yy, scale, (44, 48, 60))
            d.text((x + cell // 2, yy + cell + 1), cap, font=f(lab), fill=DIM, anchor="ma")
    ny = top + rows * (cell + gap + 14) + 6
    if scale >= 4:
        d.text((18, ny), "上排 f0-f5：12fps 的一个周期（发身到稳定）。下排：6 成分 + 中 + 少。", font=f(12), fill=TEXT)
        d.text((18, ny + 20), "格 16x16 px；图标占位 12x12 居中 (2,2)-(13,13)；流源画在图标之上。", font=f(11), fill=DIM)
        d.text((18, ny + 40), "1x 行是真机密度（1 格约 113 屏幕像素）；4x 只用于看 1px 构造。", font=f(11), fill=DIM)
    else:
        d.text((18, ny), "1x 原尺寸：格 16px，图标 12x12。", font=f(9), fill=DIM)
    img = img.crop((0, 0, width, ny + (70 if scale >= 4 else 30)))
    img.save(path)


SIG = {"water": "最平滑 无附加", "poison": "下侧 2 个 1px 凸泡", "oil": "尾短 1px 端钝",
       "blood": "体内 2 个暗点", "acid": "上缘 3 处 1px 锯齿", "energy": "4 向 1px 星芒"}


def render_attrs(scale, path):
    comps = list(COMP)
    cell = W * scale
    gap = 8 if scale >= 4 else 4
    left = 22
    width = max(left * 2 + 6 * (cell + gap), 620 if scale >= 4 else 340)
    ts = 22 if scale >= 4 else 14
    bs = 12 if scale >= 4 else 8
    img, d, y = sheet(width, 1600, "W1 P1 成分剪影 %dx" % scale,
                      "轮廓 + 内部纹理承担灰度可分；色相只是第二通道。上行彩色 / 下行灰度",
                      ts=ts, bs=bs)
    y += 4
    for gi, gray in enumerate((False, True)):
        yy = y + gi * (cell + 44)
        d.text((16, yy - 14), "灰度" if gray else "彩色", font=f(11), fill=ACCENT)
        for i, c in enumerate(comps):
            x = left + i * (cell + gap)
            paste(img, cell_at(c, "large", "ambient", "flow", "right", 0, gray=gray),
                  x, yy, scale, (44, 48, 60))
            d.text((x + cell // 2, yy + cell + 2), COMP_CN[c], font=f(11 if scale >= 4 else 8),
                   fill=TEXT if scale >= 4 else DIM, anchor="ma")
            if scale >= 4:
                d.text((x + cell // 2, yy + cell + 17), SIG[c], font=f(9), fill=DIM, anchor="ma")
    y1 = y + 2 * (cell + 44) + 4
    d.text((16, y1), "1x 彩", font=f(9), fill=DIM)
    for i, c in enumerate(comps):
        paste(img, cell_at(c, "large", "ambient", "flow", "right", 0),
              left + i * (W + 4), y1 + 12, 1, (44, 48, 60))
    d.text((16, y1 + 34), "1x 灰", font=f(9), fill=DIM)
    for i, c in enumerate(comps):
        paste(img, cell_at(c, "large", "ambient", "flow", "right", 0, gray=True),
              left + i * (W + 4), y1 + 46, 1, (44, 48, 60))
    if scale >= 4:
        d.text((16, y1 + 78), "灰度判据：平滑 / 锯齿 / 凸泡 / 钝短尾 / 暗点 / 星芒，六者靠轮廓即可分组。",
               font=f(11), fill=TEXT)
        d.text((16, y1 + 98), "面积扰动：水 72 / 酸 71 / 毒 74 / 油 70 / 血 72 / 能量 66（不到 9%，远小于含量阶梯 30%）。",
               font=f(10), fill=DIM)
    img = img.crop((0, 0, width, y1 + 122))
    img.save(path)


def render_temp(scale, path):
    cell = W * scale
    gap = 14 if scale >= 4 else 5
    temps = ["cold", "ambient", "hot"]
    left = 110 if scale >= 4 else 52
    width = max(left + 3 * (cell + gap) + 280, 660 if scale >= 4 else 340)
    ts = 22 if scale >= 4 else 14
    bs = 12 if scale >= 4 else 8
    img, d, y = sheet(width, 1600, "W1 P2 温度（静态帧可读）%dx" % scale,
                      "冷 = 霜点 + 去饱和 / 常温 = 干净 / 热 = 2 蒸汽孔 + 1 蒸发缺口；包围盒、面积、3 个尾帽槽位不动",
                      ts=ts, bs=bs)
    y += 8
    NOTES = {"cold": "上缘 2 + 尾部 1 = 3 个 1px 纯白霜点；体内去饱和 55%（主载体）",
             "ambient": "无附加像素（基线）",
             "hot": "头顶 2 个 1px 挖空蒸汽孔 + 尾下缘 1px 蒸发缺口"}
    for i, t in enumerate(temps):
        yy = y + i * (cell + 34)
        d.text((16, yy + cell // 2 - 8), TEMP_CN[t], font=f(12 if scale >= 4 else 9), fill=TEXT)
        im = cell_at("water", "large", t, "flow", "right", 0)
        paste(img, im, left, yy, scale, (44, 48, 60))
        if scale >= 4:
            d.text((left + cell + gap, yy + cell // 2 - 8), NOTES[t], font=f(11), fill=DIM)
    y2 = y + 3 * (cell + 34) + 2
    d.text((16, y2), "1x", font=f(9), fill=DIM)
    for i, t in enumerate(temps):
        paste(img, cell_at("water", "large", t, "flow", "right", 0),
              left + i * (W + 5), y2 + 12, 1, (44, 48, 60))
    if scale >= 4:
        d.text((16, y2 + 48), "灰度极性相反：冷 = 亮霜点（246,251,255）加整体变浅；热 = 暗孔（挖空后露出图标/背景）。",
               font=f(11), fill=TEXT)
        d.text((16, y2 + 68), "禁区：温度不借宽度、不借面积、不借包围盒、不借尾帽槽位（P3 通道完全不动）。",
               font=f(10), fill=DIM)
    img = img.crop((0, 0, width, y2 + 94))
    img.save(path)


def render_content(scale, path):
    cell = W * scale
    tiers = ["empty", "small", "mid", "large"]
    gap = 22 if scale >= 4 else 8
    left = 24
    width = max(left * 2 + 4 * (cell + gap), 680 if scale >= 4 else 340)
    ts = 22 if scale >= 4 else 14
    bs = 12 if scale >= 4 else 8
    img, d, y = sheet(width, 1600, "W1 P4 含量（体积，不是密度）%dx" % scale,
                      "包围盒 6x1 / 6x5 / 9x7 / 12x9，面积 6 / 22 / 43 / 72，相邻档 +67% 以上；少 = 小、圆、亮，不是散",
                      ts=ts, bs=bs)
    y += 10
    BOX = ["6x1 面积 6", "6x5 面积 22", "9x7 面积 43", "12x9 面积 72"]
    for i, tr in enumerate(tiers):
        x = left + i * (cell + gap)
        paste(img, cell_at("water", tr, "ambient", "flow", "right", 0), x, y, scale, (44, 48, 60))
        d.text((x + cell // 2, y + cell + 3), TIER_CN[tr], font=f(13 if scale >= 4 else 9),
               fill=TEXT if scale >= 4 else DIM, anchor="ma")
        if scale >= 4:
            d.text((x + cell // 2, y + cell + 21), TIER_ML[tr], font=f(9), fill=DIM, anchor="ma")
            d.text((x + cell // 2, y + cell + 35), BOX[i], font=f(9), fill=DIM, anchor="ma")
    y2 = y + cell + (56 if scale >= 4 else 18)
    d.text((24, y2), "1x", font=f(9), fill=DIM)
    for i, tr in enumerate(tiers):
        paste(img, cell_at("water", tr, "ambient", "flow", "right", 0),
              left + i * (W + 22), y2 + 12, 1, (44, 48, 60))
    if scale >= 4:
        d.text((24, y2 + 46), "少 = 整块更小 + 仍闭合 + 高光占比更高（22px 里 2x2 亮核 = 18%；72px 里 = 5.6%）。越少越紧、越亮。",
               font=f(11), fill=TEXT)
        d.text((24, y2 + 66), "空 = 6x1 干涸痕（低对比、不断开）+ 3 个尾帽槽位；玩家知道这里有个流源，但没料了。",
               font=f(10), fill=DIM)
    img = img.crop((0, 0, width, y2 + 92))
    img.save(path)


def render_range(scale, path):
    cell = W * scale
    gap = 16 if scale >= 4 else 6
    left = 96 if scale >= 4 else 40
    width = max(left + 3 * (cell + gap) + 200, 720 if scale >= 4 else 350)
    ts = 22 if scale >= 4 else 14
    bs = 12 if scale >= 4 else 8
    img, d, y = sheet(width, 1600, "W1 P3 射程（尾帽 3 段）%dx" % scale,
                      "尾帽 3 个固定槽位（尾尖内侧 / 上帽 / 下帽），亮段数 = 剩余射程；槽位永不移位，只有亮灭",
                      ts=ts, bs=bs)
    y += 10
    for r in (3, 2, 1):
        d.text((16, y + cell // 2 - 8), "射程 %d" % r, font=f(12 if scale >= 4 else 9), fill=TEXT)
        for j, tr in enumerate(("large", "mid", "small")):
            x = left + j * (cell + gap)
            im = cell_at("water", tr, "ambient", "flow", "right", 0, rng=r)
            paste(img, im, x, y, scale, (44, 48, 60))
            paste(img, im, x + cell - W, y + cell + 4, 1, (44, 48, 60))
        y += cell + (22 if scale >= 4 else 18)
    if scale >= 4:
        d.text((left, y - 4), "列：多 / 中 / 少（含量同帧变化）—— 亮段数始终读得出 3 / 2 / 1。", font=f(10), fill=DIM)
        d.text((16, y + 22), "亮段 = 成分色提亮 88%（alpha 255）；灭段 = 混描边 55%（暗紫，读作空槽）。间隔 1-2px，1x 下是三个亮点。",
               font=f(11), fill=TEXT)
        d.text((16, y + 42), "正交性：含量只改包围盒，槽位锚在尾帽上（随包围盒走）；射程只改亮灭，不动形状。",
               font=f(10), fill=DIM)
        d.text((16, y + 62), "尾尖本身永远不发亮（alpha 150 的散逸点），否则尾端会被读成头部。",
               font=f(10), fill=DIM)
    img = img.crop((0, 0, width, y + 90))
    img.save(path)


def render_dirs(scale, path):
    cell = W * scale
    gap = 14 if scale >= 4 else 5
    dirs = ["right", "down", "left", "up"]
    left = 84 if scale >= 4 else 36
    width = max(left + 4 * (cell + gap) + 120, 740 if scale >= 4 else 360)
    ts = 22 if scale >= 4 else 14
    bs = 12 if scale >= 4 else 8
    img, d, y = sheet(width, 1600, "W1 P5 方向（刚体旋转 + 显式头部）%dx" % scale,
                      "头部 = 2x2 亮核 + 1px 深瞳 + 5px 平口前脸；尾部 = 1px 尖且最暗。四向同一锚点函数",
                      ts=ts, bs=bs)
    y += 16
    for st in ("flow", "blocked"):
        d.text((16, y + cell // 2 - 8), STATE_CN[st], font=f(12 if scale >= 4 else 9), fill=ACCENT)
        for i, di in enumerate(dirs):
            x = left + i * (cell + gap)
            im = cell_at("water", "large", "ambient", st, di, 0)
            paste(img, im, x, y, scale, (44, 48, 60))
            d.text((x + cell // 2, y - 14), DIR_CN[di], font=f(10), fill=DIM, anchor="ma")
            paste(img, im, x + cell - W, y + cell + 4, 1, (44, 48, 60))
        y += cell + (30 if scale >= 4 else 24)
    d.text((16, y + 6), "L 型路径", font=f(12 if scale >= 4 else 9), fill=ACCENT)
    if scale >= 4:
        d.text((110, y + 8), "每格独立绘制、位置由逻辑层给；转向不做形状插值，只让尾丝滞后 1px（拐角格取 f1）",
               font=f(9), fill=DIM)
    for i, (di, fr) in enumerate([("right", 0), ("right", 0), ("down", 1), ("down", 0)]):
        paste(img, cell_at("water", "large", "ambient", "flow", di, fr),
              left + i * (cell + gap), y + 26, scale, (44, 48, 60))
    if scale >= 4:
        d.text((left, y + 26 + cell + 4), "格1        格2        拐角格（f1 尾丝滞后）      格4", font=f(9), fill=DIM)
        d.text((16, y + 26 + cell + 30), "拐角不插值：转向帧只是换一张同包围盒的刚体帧，因此不出现半像素错台；唯一时间差是尾丝 1px 滞后。",
               font=f(10), fill=TEXT)
        d.text((16, y + 26 + cell + 50), "奇偶事实：12 偶 9 奇，16 格上不存在 12x9 的 90 度自对称解。本方案让运动轴与横截轴共用同一锚点函数 o(e)，四向包围盒逐像素一致。",
               font=f(9), fill=WARN)
    img = img.crop((0, 0, width, y + 26 + cell + 84))
    img.save(path)


def render_states(scale, path):
    cell = W * scale
    sts = ["flow", "blocked", "split", "merge", "collect"]
    gap = 12 if scale >= 4 else 4
    left = 24
    width = max(left * 2 + 5 * (cell + gap), 800 if scale >= 4 else 370)
    ts = 22 if scale >= 4 else 14
    bs = 12 if scale >= 4 else 8
    img, d, y = sheet(width, 1600, "W1 P6 状态（静态帧可读）%dx" % scale,
                      "只改拓扑与 1px 记号：不改色相、不改温度、不改尾帽亮段数",
                      ts=ts, bs=bs)
    y += 12
    NOTE = {"flow": "圆头 单尾 平滑", "blocked": "头前脸压平 7px + 尾帽鼓 3px + 1 肋",
            "split": "头前 1px 纵缝 2px 深", "merge": "尾后三叉（上下各 1 根 1px 尾丝）",
            "collect": "头收 2px 颈 + 1px 吸管"}
    for i, st in enumerate(sts):
        x = left + i * (cell + gap)
        paste(img, cell_at("water", "large", "ambient", st, "right", 0), x, y, scale, (44, 48, 60))
        d.text((x + cell // 2, y + cell + 3), STATE_CN[st], font=f(13 if scale >= 4 else 9),
               fill=TEXT if scale >= 4 else DIM, anchor="ma")
        if scale >= 4:
            d.text((x + cell // 2, y + cell + 21), NOTE[st], font=f(9), fill=DIM, anchor="ma")
    y2 = y + cell + (44 if scale >= 4 else 18)
    d.text((24, y2), "1x", font=f(9), fill=DIM)
    for i, st in enumerate(sts):
        paste(img, cell_at("water", "large", "ambient", st, "right", 0),
              left + i * (W + 12), y2 + 12, 1, (44, 48, 60))
    if scale >= 4:
        d.text((24, y2 + 46), "非作者盲测：5 张里应能分出 4 种以上（正常 / 顶住了 / 分开了 / 合上了 / 被吸走）。",
               font=f(11), fill=TEXT)
        d.text((24, y2 + 66), "已知弱点：受阻与被收集都动了头前脸；靠尾帽鼓包加肋 vs 2px 颈加倍越界吸管区分（spec F3）。",
               font=f(10), fill=WARN)
    img = img.crop((0, 0, width, y2 + 92))
    img.save(path)


def render_occlusion(scale, path):
    cell = W * scale
    gap = 14 if scale >= 4 else 5
    rows = [("暗图标 灰(92,100,118) a205", ICON12, ICON, 205),
            ("亮图标 近白 a255", ICON12, (235, 238, 245), 255),
            ("花图标 12x12 棋盘 a255", ICON12_BUSY, (200, 206, 220), 255)]
    comps = ["water", "energy", "blood"]
    left = 200 if scale >= 4 else 116
    width = max(left + 3 * (cell + gap) + 24, 760 if scale >= 4 else 380)
    ts = 22 if scale >= 4 else 14
    bs = 12 if scale >= 4 else 8
    img, d, y = sheet(width, 1600, "W1 P8 遮挡（叠 12x12 物品图标）%dx" % scale,
                      "流源画在图标之上；体内 alpha 208 让图标透出。判据：三种图标下剪影与尾帽仍可读",
                      ts=ts, bs=bs)
    y += 10
    for rn, glyph, col, a in rows:
        d.text((16, y + cell // 2 - 8), rn, font=f(10), fill=TEXT if scale >= 4 else DIM)
        for j, c in enumerate(comps):
            x = left + j * (cell + gap)
            im = cell_image(colorize(build(c, "large", "ambient", "flow", "right", 0), c),
                            icon=True, glyph=glyph, icon_col=col, icon_a=a)
            paste(img, im, x, y, scale, (44, 48, 60))
        y += cell + (10 if scale >= 4 else 8)
    if scale >= 4:
        d.text((16, y + 8), "结论：1px #241E2E 描边在亮/花图标上依然闭合；alpha 208 的体内色不被图标吃掉（局部对比度约 3:1）。",
               font=f(11), fill=TEXT)
        d.text((16, y + 28), "最坏情况（1x1 黑白棋盘）：描边仍闭合，但血 2 暗点对比度掉到约 2:1，见 spec F4。",
               font=f(10), fill=WARN)
        d.text((16, y + 48), "真机 1 格约 113 屏幕像素，本图 1x 行等价于玩家实际看到的密度。",
               font=f(10), fill=DIM)
    img = img.crop((0, 0, width, y + 76))
    img.save(path)


def render_faces(scale, path):
    cell = W * scale
    comps = list(COMP)
    gap = 10 if scale >= 4 else 5
    left = 24
    width = max(left * 2 + 6 * (cell + gap), 640 if scale >= 4 else 340)
    ts = 22 if scale >= 4 else 14
    bs = 12 if scale >= 4 else 8
    img, d, y = sheet(width, 1600, "W1 灰度量表 faces %dx" % scale,
                      "同一批帧转 8-bit 灰度，只留明度：验证去掉色相后还剩几组",
                      ts=ts, bs=bs)
    y += 8
    for i, c in enumerate(comps):
        x = left + i * (cell + gap)
        im = cell_at(c, "large", "ambient", "flow", "right", 0, gray=True)
        paste(img, im, x, y, scale, (44, 48, 60))
        d.text((x + cell // 2, y + cell + 2), COMP_CN[c], font=f(11 if scale >= 4 else 8),
               fill=DIM, anchor="ma")
        paste(img, im, x + cell - W, y + cell + (18 if scale >= 4 else 12), 1, (44, 48, 60))
    y2 = y + cell + (42 if scale >= 4 else 26)
    d.text((24, y2), "灰度均值（去底色后）", font=f(10), fill=ACCENT)
    xs = 24
    for c in comps:
        im = cell_at(c, "large", "ambient", "flow", "right", 0, gray=True)
        vals = [v for v in list(im.convert("L").getdata()) if v > 30]
        m = sum(vals) / max(1, len(vals))
        d.text((xs, y2 + 18), "%s %.0f" % (COMP_CN[c], m), font=f(10), fill=TEXT)
        xs += 70 if scale >= 4 else 52
    if scale >= 4:
        d.text((24, y2 + 44), "明度只做辅助：真正分组靠剪影。单项失效时另一项仍能兜住。", font=f(10), fill=DIM)
    img = img.crop((0, 0, width, y2 + 70))
    img.save(path)


def render_tiles():
    os.makedirs(TILE_DIR, exist_ok=True)
    for old in os.listdir(TILE_DIR):
        if old.endswith(".png"):
            os.remove(os.path.join(TILE_DIR, old))
    entries = []
    n = [0]

    def add(group, answer, meta, colors, gray=False):
        n[0] += 1
        name = "t%03d_%s.png" % (n[0], answer)
        cell_image(colors, icon=True, gray=gray).save(os.path.join(TILE_DIR, name))
        entries.append({"path": "tiles/" + name, "group": group, "answer": answer,
                        "meta": (meta + (" gray=1" if gray else " gray=0")).strip()})

    for tier, temp, rng in [("large", "ambient", 3), ("mid", "hot", 2), ("small", "cold", 3)]:
        meta = "tier=%s temp=%s range=%d" % (tier, temp, rng)
        for c in COMP:
            add("comp", c, meta, colorize(build(c, tier, temp, "flow", "right", 0, rng), c, temp))
        for c in COMP:
            add("comp", c, meta, colorize(build(c, tier, temp, "flow", "right", 0, rng), c, temp),
                gray=True)
    for comp in ("water", "poison", "energy"):
        for temp in ("cold", "ambient", "hot"):
            add("temp", temp, "comp=%s tier=large range=3" % comp,
                colorize(build(comp, "large", temp, "flow", "right", 0, 3), comp, temp))
    for tier in TIERS:
        for comp, temp in (("water", "ambient"), ("oil", "ambient"),
                           ("acid", "cold"), ("blood", "ambient")):
            add("content", tier, "comp=%s temp=%s range=3" % (comp, temp),
                colorize(build(comp, tier, temp, "flow", "right", 0, 3), comp, temp))
    for rng in (3, 2, 1):
        for tier in ("large", "mid", "small", "empty"):
            add("range", "range%d" % rng, "tier=%s range=%d" % (tier, rng),
                colorize(build("water", tier, "ambient", "flow", "right", 0, rng), "water", "ambient"))
    for st in STATE_CN:
        for comp in ("water", "poison", "energy"):
            add("state", st, "comp=%s tier=large range=3" % comp,
                colorize(build(comp, "large", "ambient", st, "right", 0, 3), comp, "ambient"))
    for di in DIR_CN:
        for comp in ("water", "oil"):
            add("dir", di, "comp=%s tier=large" % comp,
                colorize(build(comp, "large", "ambient", "flow", di, 0, 3), comp, "ambient"))
    manifest = {
        "generator": "render_w1.py",
        "tile_size": 16,
        "note": "每个 tile 是 16x16 真机密度的单格（含 12x12 灰色图标占位；流源在图标之上）。answer 是正确判读；group 是这一批只变的那一个量，其余量写在 meta。",
        "groups": {"comp": "6 种成分（含 gray=1 灰度子集）", "temp": "冷/常温/热",
                   "content": "空/少/中/多", "range": "range1/range2/range3",
                   "state": "流动/受阻/分流/汇聚/被收集", "dir": "右/下/左/上"},
        "tiles": entries,
    }
    with open(os.path.join(TILE_DIR, "manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, ensure_ascii=False, indent=2)
    return len(entries)


SYM = {"body": "#", "rim": "@", "core": "C", "pupil": "P", "tip": "v", "dot": "x",
       "frost": "+", "rib": "|", "stain": "."}


def grid_str(roles):
    lines = []
    for y in range(W):
        s = ""
        for x in range(W):
            r = roles.get((x, y))
            if r is None:
                s += "."
            elif r[0] == "dash":
                s += "*" if r[1] == "lit" else "o"
            else:
                s += SYM.get(r[0], "?")
        lines.append("%2d %s" % (y, s))
    lines.append("   " + "".join(str(x % 10) for x in range(W)))
    return "\n".join(lines)


def dump():
    def box(roles):
        xs = [p[0] for p in roles]
        ys = [p[1] for p in roles]
        return min(xs), max(xs), min(ys), max(ys), max(xs) - min(xs) + 1, max(ys) - min(ys) + 1

    print("### tiers")
    for tier in TIERS:
        r = build("water", tier)
        x0, x1, y0, y1, w, h = box(r)
        print("  %-6s bbox %2dx%-2d x%2d..%2d y%2d..%2d px=%3d run=%2d rect=%2d"
              % (tier, w, h, x0, x1, y0, y1, len(r), quads_canonical("water", tier),
                 rects_canonical("water", tier)))
    for di in DIR_CN:
        print("\n### dir %s" % DIR_CN[di])
        print(grid_str(build("water", "large", "ambient", "flow", di, 0)))
    for c in COMP:
        print("\n### comp %s" % COMP_CN[c])
        print(grid_str(build(c, "large", "ambient", "flow", "right", 0)))
    for temp in ("cold", "hot"):
        print("\n### temp %s" % temp)
        print(grid_str(build("water", "large", temp, "flow", "right", 0)))
    for st in STATE_CN:
        print("\n### state %s" % STATE_CN[st])
        print(grid_str(build("water", "large", "ambient", st, "right", 0)))
    print()
    for tier in TIERS:
        for rng in (3, 2, 1):
            r = build("water", tier, rng=rng)
            lit = sorted(p for p, v in r.items() if v[0] == "dash" and v[1] == "lit")
            print("  %-6s range%d lit=%d %s" % (tier, rng, len(lit), lit))
    print("\n### budget")
    for name, c, tier, tp, st in [("water", "water", "large", "ambient", "flow"),
                                  ("cold", "water", "large", "cold", "flow"),
                                  ("hot", "water", "large", "hot", "flow"),
                                  ("acid", "acid", "large", "ambient", "flow"),
                                  ("poison", "poison", "large", "ambient", "flow"),
                                  ("oil", "oil", "large", "ambient", "flow"),
                                  ("blood", "blood", "large", "ambient", "flow"),
                                  ("energy", "energy", "large", "ambient", "flow"),
                                  ("blocked", "water", "large", "ambient", "blocked"),
                                  ("split", "water", "large", "ambient", "split"),
                                  ("merge", "water", "large", "ambient", "merge"),
                                  ("collect", "water", "large", "ambient", "collect"),
                                  ("mid", "water", "mid", "ambient", "flow"),
                                  ("small", "water", "small", "ambient", "flow"),
                                  ("empty", "water", "empty", "ambient", "flow")]:
        print("  %-8s run=%2d rect=%2d" % (name, quads_canonical(c, tier, tp, st),
                                           rects_canonical(c, tier, tp, st)))
    worst = (0, None)
    for c in COMP:
        for tp in ("cold", "ambient", "hot"):
            for st in STATE_CN:
                for tier in ("large", "mid"):
                    for rng in (3, 2, 1):
                        rr = rects_canonical(c, tier, tp, st, 0, rng)
                        if rr > worst[0]:
                            worst = (rr, (c, tp, st, tier, rng))
    print("  MAX(rect) all = %d @ %s" % worst)
    for fr in range(12):
        print("  f%-2d px=%d rect=%d"
              % (fr, len(build("water", "large", "ambient", "flow", "right", fr)),
                 rects_canonical("water", "large", "ambient", "flow", fr)))


def main():
    if "--dump" in sys.argv:
        dump()
        return
    os.makedirs(PNG_DIR, exist_ok=True)
    for name, fn in [("main-1x.png", lambda p: render_main(1, p)),
                     ("main-4x.png", lambda p: render_main(4, p)),
                     ("attrs-1x.png", lambda p: render_attrs(1, p)),
                     ("attrs-4x.png", lambda p: render_attrs(4, p)),
                     ("temp-1x.png", lambda p: render_temp(1, p)),
                     ("temp-4x.png", lambda p: render_temp(4, p)),
                     ("content-1x.png", lambda p: render_content(1, p)),
                     ("content-4x.png", lambda p: render_content(4, p)),
                     ("range-1x.png", lambda p: render_range(1, p)),
                     ("range-4x.png", lambda p: render_range(4, p)),
                     ("dirs-1x.png", lambda p: render_dirs(1, p)),
                     ("dirs-4x.png", lambda p: render_dirs(4, p)),
                     ("states-1x.png", lambda p: render_states(1, p)),
                     ("states-4x.png", lambda p: render_states(4, p)),
                     ("occlusion-1x.png", lambda p: render_occlusion(1, p)),
                     ("occlusion-4x.png", lambda p: render_occlusion(4, p)),
                     ("faces-1x.png", lambda p: render_faces(1, p)),
                     ("faces-4x.png", lambda p: render_faces(4, p))]:
        fn(os.path.join(PNG_DIR, name))
        print("ok", name)
    print("tiles", render_tiles())


if __name__ == "__main__":
    main()
