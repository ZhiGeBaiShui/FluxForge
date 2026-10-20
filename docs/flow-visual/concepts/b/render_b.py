#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
概念 B「流体：表面张力液滴」静态渲染器
=====================================
只生成 docs/flow-visual/concepts/b/ 下的 PNG，不读写任何游戏资源。

管线：
    行表 -> 掩膜 -> 状态形变 -> 呼吸(行剪切) -> 角色标注 -> 四向旋转 -> 行游程量化(quads)
所有坐标是格内 16x16 像素坐标，(0,0) 左上；方向基准为「右」。

用法：
    python render_b.py            # 生成全部 PNG
    python render_b.py --dump     # 只打印逐像素字符网格 + quads 预算
"""

import os
import sys
from PIL import Image, ImageDraw, ImageFont

W = 16
OUT = os.path.dirname(os.path.abspath(__file__))
FONT_PATH = "C:/Windows/Fonts/msyh.ttc"

# ---------------------------------------------------------------- 基色
PAPER = (10, 11, 14)
PANEL = (16, 18, 23)
BG = (14, 16, 21)          # 格内背景（与图标占位同底色）
GRID = (30, 34, 42)        # 格线
ICON = (78, 86, 106)       # 12x12 图标占位描边
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
TEX = {"water": "caustic", "poison": "bubble", "oil": "irid",
       "blood": "grain", "acid": "foam", "energy": "spark"}

TEMP_TINT = {"cold": (214, 236, 255), "ambient": None, "hot": (255, 150, 78)}
TEMP_CN = {"cold": "冷 -10℃", "ambient": "常温 21℃", "hot": "热 78℃"}

# ---------------------------------------------------------------- 形状行表
# 大/中/小 三档：右向、上下大致对称，尾部在左、圆头在右。
TIERS = {
    # 全部偶数包围盒、中心恒为 (7.5,7.5)：旋转 90° 后包围盒不变 => 拐弯不偏 1px
    "large": {4: (8, 11), 5: (6, 12), 6: (4, 12), 7: (3, 12),
              8: (4, 12), 9: (5, 12), 10: (6, 12), 11: (8, 11)},
    "mid":   {5: (8, 10), 6: (6, 11), 7: (5, 11), 8: (4, 11),
              9: (5, 11), 10: (7, 10)},
    "small": {5: (7, 9), 6: (6, 10), 7: (6, 10), 8: (5, 10),
              9: (6, 10), 10: (7, 9)},
}
TIER_CN = {"large": "多", "mid": "中", "small": "少", "empty": "空"}
TIER_ML = {"large": "≥10L", "mid": "0.5–10L", "small": "<0.5L", "empty": "0"}


# ---------------------------------------------------------------- 基础工具
def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def shade(c, f):
    if f >= 0:
        return tuple(int(round(v + (255 - v) * f)) for v in c)
    return tuple(int(round(v * (1 + f))) for v in c)


def rows_mask(rows):
    m = set()
    for y, (x0, x1) in rows.items():
        for x in range(x0, x1 + 1):
            m.add((x, y))
    return m


def mask_rows(mask):
    rows = {}
    for (x, y) in mask:
        if y not in rows:
            rows[y] = [x, x]
        else:
            rows[y][0] = min(rows[y][0], x)
            rows[y][1] = max(rows[y][1], x)
    return {y: (a, b) for y, (a, b) in rows.items()}


def bbox(mask):
    xs = [p[0] for p in mask]
    ys = [p[1] for p in mask]
    return min(xs), min(ys), max(xs), max(ys)


def rotate_mask(mask, direction):
    """右 -> 下 -> 左 -> 上（屏幕坐标，y 向下，顺时针）。"""
    n = {"right": 0, "down": 1, "left": 2, "up": 3}[direction]
    m = set(mask)
    for _ in range(n):
        m = {(W - 1 - y, x) for (x, y) in m}
    return m


def rot(direction):
    n = {"right": 0, "down": 1, "left": 2, "up": 3}[direction]
    def f(p):
        x, y = p
        for _ in range(n):
            x, y = W - 1 - y, x
        return (x, y)
    return f


# ---------------------------------------------------------------- 状态形变
def deform(mask, state):
    """在「右向」基准帧上做形变，之后再旋转。"""
    m = set(mask)
    if not m:
        return m
    x0, y0, x1, y1 = bbox(m)
    cy = (y0 + y1) // 2

    if state == "flow":
        return m

    if state == "blocked":
        # 压扁 = 沿运动轴两端各收 1px + 上下各削 1 行：包围盒仍是偶数、中心不变
        m = {(x, y) for (x, y) in m if y0 < y < y1 and x0 < x < x1}
        return m

    if state == "split":
        # 沿运动轴切 1px 缝，前端再加宽 1px 成楔形开口
        gap = (x0 + x1) // 2
        m = {(x, y) for (x, y) in m if x != gap}
        m = {(x, y) for (x, y) in m if not (x == gap + 1 and cy - 1 <= y <= cy + 1)}
        return m

    if state == "merge":
        # 前缘 V 形缺口：两股汇合处的凹谷
        m.discard((x1, cy))
        m.discard((x1, cy - 1))
        m.discard((x1 - 1, cy))
        return m

    if state == "collect":
        # 头部鼓包削平，向前伸出 1px 抽吸管（2px 长）
        m = {(x, y) for (x, y) in m if x <= x1 - 1}
        for dx in (1, 2):
            m.add((x1 + dx, cy))
        return m

    return m


def slosh(mask, frame, big=True):
    """
    整数像素呼吸：整行 ±1px 平移（面积守恒、包围盒不变）。
    只选不触及全局左右极值的行，保证 bbox 不跳。
    """
    if not mask or frame == 0:
        return mask
    x0, y0, x1, y1 = bbox(mask)
    rows = {}
    for (x, y) in mask:
        rows.setdefault(y, []).append(x)
    ys = sorted(rows)
    inner = [y for y in ys
             if rows[y] and min(rows[y]) > x0 and max(rows[y]) < x1]
    if len(inner) < 2:
        return mask
    n = len(inner)
    k = (frame * n) // 12
    yb = inner[k % n]                     # 这行向头部 +1
    yr = inner[(k + n // 2) % n]          # 这行向尾部 -1
    m = set()
    for (x, y) in mask:
        if y == yb:
            m.add((x + 1, y))
        elif y == yr:
            m.add((x - 1, y))
        else:
            m.add((x, y))
    return m


# ---------------------------------------------------------------- 角色标注
def rim_of(mask):
    out = set()
    for (x, y) in mask:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in mask:
                out.add((x, y))
                break
    return out


def pick(points, cand, used):
    """在候选里找离目标最近且未用的像素。"""
    for p in points:
        best, bd = None, 1e9
        for c in cand:
            if c in used:
                continue
            d = (c[0] - p[0]) ** 2 + (c[1] - p[1]) ** 2
            if d < bd:
                best, bd = c, d
        if best is not None:
            return best
    return None


def build(comp="water", tier="large", temp="ambient", state="flow",
          direction="right", frame=0, tex_on=True):
    """返回 {(x,y): role}，role 是元组，颜色在 colorize 里解。"""
    base = COMP[comp]

    if tier == "empty":
        roles = {}
        if state in ("flow", "blocked"):
            for x in range(5, 11):
                roles[(x, 10)] = ("stain", 0 if x in (5, 10) else 1)
            roles[(7, 10)] = ("stain", 2)
            roles[(8, 10)] = ("stain", 2)
        return {rot(direction)(p): r for p, r in roles.items()}

    mask = rows_mask(TIERS[tier])
    mask = deform(mask, state)
    mask = slosh(mask, frame)
    rim = rim_of(mask)
    inner = mask - rim

    roles = {p: ("body",) for p in mask}
    for p in rim:
        roles[p] = ("rim", 0)

    x0, y0, x1, y1 = bbox(mask)
    cy = (y0 + y1) // 2
    cx_head = x1

    # --- 高光：头部上-外侧 2px（1x2 或 2x1，按行取；不会落在尾巴上）
    inner_sorted = sorted(inner, key=lambda p: (p[1], p[0]))
    hi_targets = [(cx_head - 3, y0 + 1), (cx_head - 2, y0 + 1)]
    used = set()
    hi = []
    for t in hi_targets:
        p = pick([t], [q for q in inner_sorted if q not in used], used)
        if p:
            hi.append(p)
            used.add(p)
    for p in hi:
        roles[p] = ("hi",)

    # --- 成分内部纹理
    cand = [q for q in inner_sorted if q not in used]
    plan = {
        "caustic": [(cx_head - 5, cy + 3)],
        "bubble": [(x0 + 3, cy + 2), (x0 + 4, cy), (x0 + 2, cy - 1)],
        "irid":   [(x0 + 3, cy + 2), (x0 + 4, cy + 1), (x0 + 5, cy)],
        "grain":  [(x0 + 2, cy + 2), (x0 + 3, cy + 3), (x0 + 4, cy + 2)],
        "foam":   [],
        "spark":  [(x0 + 3, cy - 1), (x0 + 4, cy), (x0 + 3, cy + 1)],
    }[TEX[comp]]
    tex_px = []
    if tex_on:
        for t in plan:
            p = pick([t], cand, used)
            if p:
                tex_px.append(p)
                used.add(p)
                cand.remove(p)
    for i, p in enumerate(tex_px):
        roles[p] = ("tex", TEX[comp], i)

    # 酸：泡沫在顶部边缘（替换描边像素，形成白沫冠）
    if tex_on and TEX[comp] == "foam":
        top = sorted([p for p in rim if p[1] <= y0 + 2], key=lambda p: (p[1], p[0]))
        for i, p in enumerate(top[:3]):
            roles[p] = ("foam", i)

    # 能量：外壳改用青蓝描边（近白身体需要边界）
    # （在 colorize 里按 comp 处理）

    # --- 温度（只动边缘带）
    if temp == "cold":
        frost_pool = sorted([p for p in rim if p[1] <= y0 + 3], key=lambda p: (p[1], p[0]))
        for i, p in enumerate(frost_pool[:3]):
            roles[p] = ("frost", i % 2)
    elif temp == "hot":
        # 蒸汽孔：贴近描边的内部像素挖空（露出背景/图标）
        pore_c = [(cx_head - 2, y0 + 2), (cx_head - 3, y1 - 1)]
        for t in pore_c:
            p = pick([t], [q for q in inner if q not in used], used)
            if p:
                del roles[p]
                used.add(p)
        # 沸腾缺口：头部上下各咬掉 1 个描边像素
        for t in [(x1, cy - 2), (x1, cy + 2)]:
            p = pick([t], [q for q in rim if q in roles], used)
            if p:
                del roles[p]
                used.add(p)

    # --- 状态附加
    if state == "blocked":
        # 停滞纹：前缘后方 2 条 1px 竖肋
        for i, (dx, half) in enumerate(((2, 1), (4, 1))):
            for dy in range(-half, half + 1):
                p = (x1 - dx, cy + dy)
                if p in roles:
                    roles[p] = ("rib", i)

    if state == "merge":
        p = (x1 - 2, cy)
        if p in roles:
            roles[p] = ("seam",)

    # --- 射程残留（尾部外侧，离体 1px，暗淡）
    if state in ("flow", "collect"):
        res_n = {"long": 0, "mid": 1, "short": 2}.get(_RANGE[0], 0)
        rear = min(x for (x, y) in mask)
        spots = [(rear - 2, cy - 2), (rear - 2, cy + 2)]
        for i in range(res_n):
            p = spots[i]
            roles[p] = ("res",)

    # --- 四向旋转（整体刚性旋转：形状/高光/纹理/残留一起转）
    f = rot(direction)
    return {f(p): r for p, r in roles.items()}


# 射程档位用一个可变全局，方便渲染不同档（默认长）
_RANGE = ["long"]


# ---------------------------------------------------------------- 着色
def colorize(roles, comp, temp):
    base = COMP[comp]
    rim_base = shade(base, -0.52)
    if comp == "energy":
        rim_base = (143, 227, 255)
    if temp == "cold":
        rim_base = mix(rim_base, TEMP_TINT["cold"], 0.88)
    elif temp == "hot":
        rim_base = mix(rim_base, TEMP_TINT["hot"], 0.86)

    tex_col = {
        "water":   mix(base, (255, 255, 255), 0.62),
        "poison":  mix(base, (255, 255, 255), 0.55),
        "oil":     mix(base, (255, 255, 255), 0.55),
        "blood":   shade(base, -0.55),
        "acid":    (236, 255, 214),
        "energy":  (111, 226, 255),
    }
    out = {}
    for p, r in roles.items():
        k = r[0]
        if k == "body":
            out[p] = (base, 208, "body")
        elif k == "rim":
            # 上亮下暗的 1px 明度带（±12%）
            y = p[1]
            out[p] = (rim_base, 255, "rim")
        elif k == "hi":
            out[p] = (mix(base, (255, 255, 255), 0.74), 255, "hi")
        elif k == "tex":
            kind = r[1]
            if kind == "irid":
                cols = [mix(base, (110, 240, 220), 0.55),
                        mix(base, (255, 120, 214), 0.55),
                        mix(base, (255, 245, 190), 0.55)]
                out[p] = (cols[r[2] % 3], 255, "tex")
            elif kind == "foam":
                out[p] = ((238, 255, 220), 255, "foam")
            else:
                out[p] = (tex_col[comp], 255, "tex")
        elif k == "foam":
            out[p] = ((238, 255, 220), 255, "foam")
        elif k == "frost":
            out[p] = (mix(rim_base, (240, 250, 255), 0.75), 255, "frost")
        elif k == "rib":
            out[p] = (mix(rim_base, (255, 255, 255), 0.10), 255, "rib")
        elif k == "seam":
            out[p] = ((245, 255, 255), 255, "seam")
        elif k == "res":
            out[p] = (mix(base, BG, 0.66), 176, "res")
        elif k == "stain":
            lvl = r[1]
            c = mix(BG, base, 0.28) if lvl != 2 else mix(BG, base, 0.45)
            out[p] = (c, 220, "stain")
    return out


# ---------------------------------------------------------------- 游程量化
def quads(cell):
    """每行同 (rgb, alpha) 的最大连续段 = 1 个 quad。"""
    rows = {}
    for (x, y), (c, a, k) in cell.items():
        rows.setdefault(y, []).append((x, c, a, k))
    n = 0
    detail = {}
    for y, items in rows.items():
        items.sort()
        last = None
        for (x, c, a, k) in items:
            if last != (c, a):
                n += 1
                detail[k] = detail.get(k, 0) + 1
                last = (c, a)
    return n, detail


# ---------------------------------------------------------------- 格图
def draw_icon(px):
    """12x12 图标占位：瓶子轮廓，位于 (2,2)-(13,13)。"""
    ox, oy = 2, 2
    pts = []
    for x in range(4, 7):
        pts += [(x, 0)]
    for y in range(1, 3):
        pts += [(4, y), (6, y)]
    pts += [(2, 3), (9, 3)]
    for y in range(4, 10):
        pts += [(2, y), (9, y)]
    for x in range(2, 10):
        pts += [(x, 10)]
    for (x, y) in pts:
        xx, yy = ox + x, oy + y
        if 0 <= xx < W and 0 <= yy < W:
            px[yy][xx] = ICON + (168,)


def cell_image(cell, icon=True):
    px = [[BG + (255,) for _ in range(W)] for _ in range(W)]
    if icon:
        draw_icon(px)
    for (x, y), (c, a, k) in cell.items():
        if not (0 <= x < W and 0 <= y < W):
            continue
        bg = px[y][x]
        af = a / 255.0
        px[y][x] = (int(c[0] * af + bg[0] * (1 - af)),
                    int(c[1] * af + bg[1] * (1 - af)),
                    int(c[2] * af + bg[2] * (1 - af)), 255)
    img = Image.new("RGB", (W, W))
    img.putdata([px[y][x] for y in range(W) for x in range(W)])
    return img


def cell_at(comp="water", tier="large", temp="ambient", state="flow",
            direction="right", frame=0, icon=True):
    roles = build(comp, tier, temp, state, direction, frame)
    return cell_image(colorize(roles, comp, temp), icon=icon)


# ---------------------------------------------------------------- 字体/图版
def font(sz):
    try:
        return ImageFont.truetype(FONT_PATH, sz)
    except Exception:
        return ImageFont.load_default()


F_T = font(22)
F_H = font(15)
F_B = font(12)
F_S = font(10)


def sheet(w, h, title, sub="", tf=None):
    img = Image.new("RGB", (w, h), PAPER)
    d = ImageDraw.Draw(img)
    d.text((18, 12), title, font=tf or F_T, fill=TEXT)
    if sub:
        d.text((18, 40 if (tf is None or tf.size > 15) else 34), sub, font=F_B, fill=DIM)
    d.line((18, 60 if (tf is None or tf.size > 15) else 54, w - 18,
            60 if (tf is None or tf.size > 15) else 54), fill=(40, 44, 54))
    return img, d


def paste_cell(img, x, y, scale, cell, icon=True):
    im = cell_image(cell, icon) if isinstance(cell, dict) else cell
    im = im.resize((W * scale, W * scale), Image.NEAREST)
    img.paste(im, (x, y))
    return im


def frame_box(d, x, y, scale, col=GRID):
    d.rectangle((x - 1, y - 1, x + W * scale, y + W * scale), outline=col)


def caption(d, x, y, s, f=F_S, col=DIM, anchor=None):
    d.text((x, y), s, font=f, fill=col, anchor=anchor)


# ================================================================ 图 1/2
def render_main(scale, path, title, sub):
    cell = W * scale
    gap = 6 * max(1, scale // 2)
    cols = 8
    rows = 2
    left = 20
    top = 78
    grid_w = cols * (cell + gap) - gap
    grid_h = rows * (cell + gap + 14) - 14
    width = max(left * 2 + grid_w, 760 if scale >= 4 else 340)
    height = top + grid_h + (96 + 110 if scale >= 4 else 40)

    img, d = sheet(width, height, title, sub, tf=None if scale >= 4 else F_H)
    y = top
    labels = []
    for r in range(rows):
        for c in range(cols):
            if r == 0:
                fr = c
                g = build("water", "large", "ambient", "flow", "right", fr)
                cap = "f%d" % fr if scale >= 4 else ""
            else:
                comp = list(COMP)[c % 6]
                cur = "mid" if c == 6 else ("small" if c == 7 else "large")
                g = build(comp, cur, "ambient", "flow", "right", 0)
                cap = COMP_CN[comp] if scale >= 4 else ""
            x = left + c * (cell + gap)
            yy = y + r * (cell + gap + 14)
            paste_cell(img, x, yy, scale, colorize(g, "water" if r == 0 else list(COMP)[c % 6],
                                                   "ambient"))
            if scale >= 4:
                frame_box(d, x, yy, scale)
                caption(d, x + cell // 2, yy + cell + 2, cap, F_S, DIM, anchor="ma")
            else:
                frame_box(d, x, yy, 1, (24, 27, 34))

    if scale >= 4:
        ytxt = height - 92
        caption(d, 20, ytxt, "上排：12fps 的 f0–f7，水·大含量·右向·1 格（每格 83ms）。", F_B, TEXT)
        caption(d, 20, ytxt + 20, "下排：六种成分 + 中/小含量（最右 2 格）。内部纹理为 1px 级，4× 下可数。", F_B, DIM)
        caption(d, 20, ytxt + 40, "格子 = 16×16 px，图标占位 = 12×12 px、居中 (2,2)-(13,13)；流源绘制在图标之上。", F_S, DIM)
    else:
        caption(d, 20, height - 30, "1× 原尺寸；格 16px，图标占位 12×12。", F_S, DIM)

    img.save(path)
    return img


# ================================================================ 构造大图
def render_construction():
    S = 16
    cell = W * S
    width, height = 900, 420
    img, d = sheet(width, height, "B·流体 — 逐像素构造（16× 放大，仅标注用；验收看 4×）",
                   "水 / 大含量 / 常温 / 流动 / 右向 / f0")
    g = build("water", "large", "ambient", "flow", "right", 0)
    col = colorize(g, "water", "ambient")
    x0, y0 = 60, 96
    paste_cell(img, x0, y0, S, col)
    # 网格线
    for i in range(W + 1):
        d.line((x0 + i * S, y0, x0 + i * S, y0 + cell), fill=(26, 30, 36))
        d.line((x0, y0 + i * S, x0 + cell, y0 + i * S), fill=(26, 30, 36))

    # 标注点
    marks = [
        ("1", (9, 5), "高光：头部上-外侧 2px，成分色提亮 74%，永不纯白"),
        ("2", (5, 4), "表面张力边：1px 闭合环，上亮下暗 ±12%，四角削圆"),
        ("3", (7, 9), "内部纹理：气泡/虹彩/颗粒… 1–3px，随成分换"),
        ("4", (3, 7), "拖尾：1px 尾尖 + 3px 细颈，长度固定（不代表射程）"),
        ("5", (12, 7), "头部：6px 竖边圆鼓，与尾部 5:1 质量比 = 方向"),
    ]
    for num, (gx, gy), txt in marks:
        cx = x0 + gx * S + S // 2
        cy = y0 + gy * S + S // 2
        d.ellipse((cx - 4, cy - 4, cx + 4, cy + 4), outline=ACCENT, width=1)
        d.text((cx, cy), num, font=F_S, fill=ACCENT, anchor="mm")
    lx = x0 + cell + 36
    ly = 110
    for num, _, txt in marks:
        d.text((lx, ly), num, font=F_H, fill=ACCENT)
        d.text((lx + 18, ly + 1), txt, font=F_B, fill=TEXT)
        ly += 34
    d.text((lx, ly + 12),
           "面积 %dpx / 包围盒 %d×%d / quads=%d" % (
               len(col), bbox(set(col))[2] - bbox(set(col))[0] + 1,
               bbox(set(col))[3] - bbox(set(col))[1] + 1, quads(col)[0]),
           font=F_H, fill=ACCENT)
    d.text((lx, ly + 38), "内部 alpha 208（图标可透出），描边 alpha 255。", font=F_S, fill=DIM)
    d.text((60, height - 34), "整数像素，无抗锯齿；所有坐标可 1:1 写进顶点数组。", font=F_S, fill=DIM)
    img.save(os.path.join(OUT, "construction.png"))


# ================================================================ attrs
def render_attrs():
    S = 4
    cell = W * S
    gap = 10
    width, height = 940, 860
    img, d = sheet(width, height, "B·流体 — 属性通道（I1/I4/I5/I6）",
                   "上=4× NEAREST，下=1× 原尺寸；全部静态单帧，不依赖动画")

    def block(y, name, items, note):
        d.text((20, y), name, font=F_H, fill=ACCENT)
        d.text((20, y + 19), note, font=F_S, fill=DIM)
        yy = y + 40
        for i, (lab, g, sub) in enumerate(items):
            x = 24 + i * (cell + gap)
            paste_cell(img, x, yy, S, colorize(g, _cur_comp[i], _cur_temp[i]))
            frame_box(d, x, yy, S)
            caption(d, x + cell // 2, yy + cell + 3, lab, F_S, TEXT, anchor="ma")
            if sub:
                caption(d, x + cell // 2, yy + cell + 15, sub, F_S, DIM, anchor="ma")
        # 1x 行
        yy1 = yy + cell + 30
        for i, (lab, g, sub) in enumerate(items):
            x = 24 + i * (cell + gap) + (cell - W) // 2
            paste_cell(img, x, yy1, 1, colorize(g, _cur_comp[i], _cur_temp[i]))
            frame_box(d, x, yy1, 1, (26, 30, 38))
        caption(d, 20, yy1 + 20, "1×", F_S, DIM)
        return yy1 + 40

    global _cur_comp, _cur_temp
    y = 74

    comps = list(COMP)
    _cur_comp = comps
    _cur_temp = ["ambient"] * 6
    items = [(COMP_CN[c], build(c, "large", "ambient", "flow", "right", 0),
              {"water": "清澈", "poison": "气泡", "oil": "虹彩",
               "blood": "颗粒", "acid": "泡沫", "energy": "电点"}[c]) for c in comps]
    y = block(y, "I1 成分", items, "色相（6 色）+ 内部纹理（1–3px）；纹理只换内部像素，不改外形")

    _cur_comp = ["water"] * 5
    _cur_temp = ["ambient"] * 5
    sts = [("flow", "流动", "单尾"), ("blocked", "受阻", "压扁+停滞肋"),
           ("split", "分流", "1px 缝"), ("merge", "汇聚", "V 形谷"),
           ("collect", "被收集", "2px 抽吸管")]
    items = [(lab, build("water", "large", "ambient", st, "right", 0), sub)
             for st, lab, sub in sts]
    y = block(y, "I5 状态", items, "只改拓扑（连通/压平/缝/谷/管），不改包围盒宽度")

    _cur_comp = ["water"] * 3
    _cur_temp = ["ambient"] * 3
    items = []
    for rng, lab, n in (("long", "长 ≥8 格", 0), ("mid", "中 3–7 格", 1), ("short", "短 0–2 格", 2)):
        _RANGE[0] = rng
        items.append((lab, build("water", "large", "ambient", "flow", "right", 0),
                      "%d 残滴" % n))
    _RANGE[0] = "long"
    y = block(y, "I4 射程", items, "尾部外侧 1px 残滴，暗淡(alpha176)+离体；与 I3 的亮而连通相反")

    _cur_comp = ["water"] * 4
    _cur_temp = ["ambient"] * 4
    items = [(lab, build("water", "large", "ambient", "flow", di, 0), "")
             for di, lab in (("right", "右"), ("down", "下"), ("left", "左"), ("up", "上"))]
    y = block(y, "I6 方向", items, "同一掩膜整体刚性旋转，头部质量 5:1；4 向中心不偏移")

    caption(d, 20, height - 26,
            "注：1× 行是真实 16px 尺寸，用来验收「一眼可分类」；4× 行只用于看构造。", F_S, DIM)
    img.save(os.path.join(OUT, "attrs.png"))


# ================================================================ temp
def render_temp():
    S = 4
    cell = W * S
    gap = 12
    width, height = 940, 480
    img, d = sheet(width, height, "B·流体 — I2 温度（静态帧可读）",
                   "冷=霜壳(白蓝描边+结晶点) / 常温=成分暗描边 / 热=橙描边+蒸汽孔+沸腾缺口；三者尺寸完全一致")
    comps = ["water", "poison", "oil"]
    temps = ["cold", "ambient", "hot"]
    y = 86
    for r, t in enumerate(temps):
        d.text((20, y + 4), TEMP_CN[t], font=F_B, fill=TEXT)
        for c, comp in enumerate(comps):
            x = 150 + c * (cell + gap)
            g = build(comp, "large", t, "flow", "right", 0)
            paste_cell(img, x, y, S, colorize(g, comp, t))
            frame_box(d, x, y, S)
            caption(d, x + cell // 2, y - 16, COMP_CN[comp] if r == 0 else "", F_S, DIM, anchor="ma")
        # 1x 行
        for c, comp in enumerate(comps):
            x = 150 + c * (cell + gap) + (cell - W) // 2
            paste_cell(img, x, y + cell + 6, 1, colorize(build(comp, "large", t, "flow", "right", 0), comp, t))
            frame_box(d, x, y + cell + 6, 1, (26, 30, 38))
        y += cell + 34

    d.text((150, y + 4), "1× 原尺寸（上三组同列）", font=F_S, fill=DIM)
    caption(d, 20, height - 62,
            "冷：#D6ECFF 描边全覆盖 + 顶部 3 个更亮结晶点；热：#FF9650 描边 + 2 个蒸汽孔(挖空) + 头上下各 1 个沸腾缺口。", F_S, TEXT)
    caption(d, 20, height - 42,
            "尺寸、面积、内部纹理密度三项在三种温度下逐像素相同 —— 温度不借用量/大小通道。", F_S, DIM)
    img.save(os.path.join(OUT, "temp.png"))


# ================================================================ content
def render_content():
    S = 4
    cell = W * S
    width, height = 900, 360
    img, d = sheet(width, height, "B·流体 — I3 含量（体积，不是密度）",
                   "「少」= 更小、更圆、更亮，不是更散；三档包围盒 10×8 / 8×6 / 6×6，远超 1px 呼吸幅度")
    tiers = ["empty", "small", "mid", "large"]
    y = 92
    for i, t in enumerate(tiers):
        x = 24 + i * 140
        g = build("water", t, "ambient", "flow", "right", 0)
        paste_cell(img, x, y, S, colorize(g, "water", "ambient"))
        frame_box(d, x, y, S)
        caption(d, x + cell // 2, y + cell + 4, TIER_CN[t], F_B, TEXT, anchor="ma")
        caption(d, x + cell // 2, y + cell + 20, TIER_ML[t], F_S, DIM, anchor="ma")
        # 1x
        paste_cell(img, x + (cell - W) // 2, y + cell + 40, 1, colorize(g, "water", "ambient"))
        frame_box(d, x + (cell - W) // 2, y + cell + 40, 1, (26, 30, 38))

    caption(d, 24, y + cell + 66, "1× 原尺寸", F_S, DIM)
    caption(d, 24, height - 56,
            "空：只剩 6×1 连续干涸痕（低对比、不断开），玩家知道「这里有个流源但空了」。", F_S, TEXT)
    caption(d, 24, height - 36,
            "少/中/多：包围盒 6×6 / 8×6 / 10×8，面积 27 / 35 / 58 px；高光恒 2px，所以越小高光占比越高 = 越「紧」。", F_S, DIM)
    img.save(os.path.join(OUT, "content.png"))


# ================================================================ dirs
def render_dirs():
    S = 4
    cell = W * S
    width, height = 940, 560
    img, d = sheet(width, height, "B·流体 — I6 方向 & L 型路径",
                   "同一掩膜刚性旋转；包围盒中心恒为 (8,8)，拐弯不出现半像素错台")
    dirs = [("right", "右"), ("down", "下"), ("left", "左"), ("up", "上")]
    y = 88
    for st, lab in (("flow", "流动"), ("blocked", "受阻")):
        d.text((20, y + 24), lab, font=F_H, fill=ACCENT)
        for i, (di, dl) in enumerate(dirs):
            x = 90 + i * (cell + 16)
            g = build("water", "large", "ambient", st, di, 0)
            paste_cell(img, x, y, S, colorize(g, "water", "ambient"))
            frame_box(d, x, y, S)
            caption(d, x + cell // 2, y - 16, dl, F_S, DIM, anchor="ma")
            paste_cell(img, x + (cell - W) // 2, y + cell + 6, 1, colorize(g, "water", "ambient"))
            frame_box(d, x + (cell - W) // 2, y + cell + 6, 1, (26, 30, 38))
        y += cell + 40

    # L 路径：3 格向右 + 1 格向下
    d.text((20, y + 6), "L 型路径", font=F_H, fill=ACCENT)
    d.text((110, y + 9), "每格 16px 独立绘制，位置由逻辑层给；转向帧不做形状插值，只做尾丝 1px 滞后", font=F_S, fill=DIM)
    path = [("right", 0), ("right", 0), ("down", 3), ("down", 0)]
    for i, (di, fr) in enumerate(path):
        x = 90 + i * (cell + 16)
        yy = y + 34
        g = build("water", "large", "ambient", "flow", di, fr)
        paste_cell(img, x, yy, S, colorize(g, "water", "ambient"))
        frame_box(d, x, yy, S)
    caption(d, 90, y + 34 + cell + 6,
            "格 1        格 2        拐角格(f3 尾丝滞后)   格 4", F_S, DIM)
    img.save(os.path.join(OUT, "dirs.png"))


# ================================================================ motion
def render_motion():
    S = 4
    cell = W * S
    width, height = 940, 500
    img, d = sheet(width, height, "B·流体 — 0.0–1.0s 稳态波动（12fps / 12 帧）",
                   "波动 = 整行 ±1px 剪切（面积守恒、包围盒不变）；纹理 1px/4帧 向头部漂移；高光 1px 抖动")
    y = 88
    for r in range(2):
        for c in range(6):
            f = r * 6 + c
            x = 24 + c * (cell + 8)
            g = build("water", "large", "ambient", "flow", "right", f)
            paste_cell(img, x, y, S, colorize(g, "water", "ambient"))
            frame_box(d, x, y, S)
            caption(d, x + cell // 2, y + cell + 3, "%dms" % round(f * 1000 / 12), F_S, DIM, anchor="ma")
        y += cell + 30

    for f in range(12):
        x = 24 + f * (W + 3)
        paste_cell(img, x, y + 6, 1, colorize(build("water", "large", "ambient", "flow", "right", f), "water", "ambient"))
        frame_box(d, x, y + 6, 1, (26, 30, 38))
    caption(d, 24, y + 26, "1× 原尺寸 12 帧连续", F_S, DIM)

    caption(d, 24, height - 72,
            "f0–f3 发射：尾部两滴合并（不缩放，缩放会改含量通道）；f4–f7 稳定波动；f8–f11 尾部回卷 + 高光回摆。", F_S, TEXT)
    caption(d, 24, height - 52,
            "受阻：f0/f2/f4 停滞肋相位 0/1/1，停滞肋不位移；0.5s 周期，与波动 2Hz 错开，读作「顶住了」而不是「卡帧」。", F_S, DIM)
    caption(d, 24, height - 32,
            "单帧变形幅度 ≤1px、单帧面积恒定 —— 不会把「动」读成「含量变化」。", F_S, DIM)
    img.save(os.path.join(OUT, "motion.png"))


# ================================================================ dump
def dump():
    print("=== 右向 / 水 / 大 / 常温 / 流动 / f0 ===")
    g = build("water", "large", "ambient", "flow", "right", 0)
    col = colorize(g, "water", "ambient")
    sym = {"body": "#", "rim": "@", "hi": "O", "tex": "*", "foam": "o",
           "frost": "+", "rib": "|", "seam": "s", "res": "~", "stain": "."}
    for y in range(W):
        line = ""
        for x in range(W):
            k = col.get((x, y), (None, 0, None))[2]
            line += sym.get(k, ".") if k else "."
        print("%2d %s" % (y, line))
    print("   " + "".join(str(x % 10) for x in range(W)))
    q, det = quads(col)
    print("quads =", q, det)

    print("\n=== 上向 / 水 / 大 / 常温 / 流动 / f0 ===")
    g = build("water", "large", "ambient", "flow", "up", 0)
    col = colorize(g, "water", "ambient")
    for y in range(W):
        print("%2d %s" % (y, "".join(sym.get(col[(x, y)][2], ".") if (x, y) in col else "." for x in range(W))))

    print("\n=== quads 预算 ===")
    cases = [
        ("水/大/常温/流动/右", "water", "large", "ambient", "flow", "right"),
        ("水/大/冷/流动/右", "water", "large", "cold", "flow", "right"),
        ("水/大/热/流动/右", "water", "large", "hot", "flow", "right"),
        ("酸/大/常温/流动/右", "acid", "large", "ambient", "flow", "right"),
        ("水/大/常温/受阻/右", "water", "large", "ambient", "blocked", "right"),
        ("水/大/常温/分流/右", "water", "large", "ambient", "split", "right"),
        ("水/大/常温/汇聚/右", "water", "large", "ambient", "merge", "right"),
        ("水/大/常温/收集/右", "water", "large", "ambient", "collect", "right"),
        ("水/大/常温/流动/上", "water", "large", "ambient", "flow", "up"),
        ("水/中/常温/流动/右", "water", "mid", "ambient", "flow", "right"),
        ("水/小/常温/流动/右", "water", "small", "ambient", "flow", "right"),
    ]
    for name, c, t, tp, st, di in cases:
        col = colorize(build(c, t, tp, st, di, 0), c, tp)
        q, det = quads(col)
        print("%-18s %3d  %s" % (name, q, det))


def main():
    os.makedirs(OUT, exist_ok=True)
    if "--dump" in sys.argv:
        dump()
        return
    render_main(1, os.path.join(OUT, "main-1x.png"),
                "B·流体（表面张力液滴）— 1× 原尺寸", "16px 格 / 12×12 图标占位 / 流源画在图标之上")
    render_main(4, os.path.join(OUT, "main-4x.png"),
                "B·流体（表面张力液滴）— 4× NEAREST", "一格 = 16px；液滴 1 格，有中心、有方向、无固定轮廓")
    render_construction()
    render_attrs()
    render_temp()
    render_content()
    render_dirs()
    render_motion()
    print("done ->", OUT)


if __name__ == "__main__":
    main()
