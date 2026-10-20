#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
W4「方向与状态修复」渲染器
=========================
只写 docs/flow-visual/round2/w4/。不读游戏资源，不改 src/** / assets/**。

继承概念 B 的液滴语言（12×8 包围盒、1px 张力边、alpha 208、温度/含量/呼吸一套不动），
只替换两件东西：
  I6 方向：显式头部线索（钝口 + 2×2 亮核 + 前脸 2px 高光 + 尾部渐隐）
  I5 状态：物理直喻（墙 / 两瓣分离 / 两瓣相撞 / 连续流进喇叭口）

用法：
  python render_w4.py           # 生成 png/ 与 tiles/
  python render_w4.py --dump    # 打印字符网格 + 度量（spec-w4.md 的表格来源）
"""

import os
import sys
import json
from PIL import Image, ImageDraw, ImageFont

W = 16
OUT = os.path.dirname(os.path.abspath(__file__))
PNG = os.path.join(OUT, "png")
TILES = os.path.join(OUT, "tiles")
FONT_PATH = "C:/Windows/Fonts/msyh.ttc"

# ---------------------------------------------------------------- 基色（B 原样）
PAPER = (10, 11, 14)
BG = (14, 16, 21)
GRID = (30, 34, 42)
TEXT = (214, 220, 230)
DIM = (122, 130, 146)
ACCENT = (255, 206, 92)
BASE = (39, 199, 230)            # 水 #27C7E6，B 的基色
WHITE = (255, 255, 255)

DIFF_ADD = (255, 92, 138)        # 只在本态存在
DIFF_DEL = (63, 169, 255)        # 只在 flow 存在
DIFF_CHG = (255, 209, 102)       # 两态都有但角色不同
DIFF_SAME = (32, 36, 44)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def shade(c, f):
    if f >= 0:
        return tuple(int(round(v + (255 - v) * f)) for v in c)
    return tuple(int(round(v * (1 + f))) for v in c)


RIM = shade(BASE, -0.52)                       # #13606E，B 的常温描边
WALL = (201, 194, 184)                         # #C9C2B8，中性障碍物

# 角色 -> (颜色, alpha)
ROLE = {
    "#": (BASE, 208),                          # 液体本体（图标可透出）
    "@": (RIM, 255),                           # 1px 表面张力边
    "C": (mix(BASE, WHITE, 0.38), 255),        # 2×2 亮核（方向：头）
    "G": (mix(BASE, WHITE, 0.62), 255),        # 前脸 2px 高光（方向：最前端）
    "T": (mix(BASE, BG, 0.66), 130),           # 尾部渐隐（方向：尾）
    "t": (mix(RIM, BG, 0.66), 255),            # 尾部渐隐的边
    "W": (WALL, 255),                          # 1×3 中性竖墙（受阻）
    "S": (mix(BASE, WHITE, 0.60), 255),        # 1px 接缝亮线（汇聚）
    "F": (RIM, 255),                           # 回卷的尾唇（受阻）
    "~": (mix(BASE, WHITE, 0.18), 235),        # 连续 1px 抽吸流（被收集）
    "M": (RIM, 255),                           # 喇叭口唇（被收集）
}

# ---------------------------------------------------------------- 形状数据
# 每态 = {y: [(x0,x1), ...]} + 特写像素 {char: [(x,y), ...]}，全部在「右向」基准帧。
# 包围盒统一 12 宽 × 8 高：x=2..13, y=4..11，中心 (7.5,7.5)。

FLOW_SPANS = {
    4: [(8, 11)], 5: [(6, 13)], 6: [(4, 13)],
    7: [(2, 13)], 8: [(2, 13)], 9: [(4, 13)],
    10: [(6, 13)], 11: [(8, 11)],
}
FLOW_SPECIAL = {
    # 2×2 亮核偏前（x=11..12，紧贴前脸内侧）
    "C": [(11, 7), (12, 7), (11, 8), (12, 8)],
    # 前脸 2px 高光（长在张力边上，环仍闭合）
    "G": [(13, 7), (13, 8)],
    # 尾部 2px 渐隐：最暗的两列
    "T": [(3, 7), (3, 8)],
    "t": [(2, 7), (2, 8)],
}

# 受阻：前方 1×3 竖墙；主体沿轴压 1px（后缘 2→3）；尾部回卷（x=4 一条 1px 暗唇）；
# 前脸高光撤掉（避免"撞击闪白"），亮核后移 1px。
BLOCKED_SPANS = {
    4: [(8, 12)], 5: [(6, 13)], 6: [(4, 13)],
    7: [(3, 13)], 8: [(3, 13)], 9: [(4, 13)],
    10: [(6, 13)], 11: [(8, 12)],
}
BLOCKED_SPECIAL = {
    "W": [(14, 6), (14, 7), (14, 8)],
    "C": [(10, 7), (11, 7), (10, 8), (11, 8)],
    "F": [(4, 7), (4, 8)],
    "T": [(5, 7), (5, 8)],
}

# 分流：上下两瓣，空 2px（y=7,8 全空），各自闭合边 + 各自亮核/前脸高光，质量 33:33
SPLIT_SPANS = {
    3: [(9, 12)], 4: [(4, 13)], 5: [(2, 13)], 6: [(7, 13)],
    9: [(7, 13)], 10: [(2, 13)], 11: [(4, 13)], 12: [(9, 12)],
}
SPLIT_SPECIAL = {
    "C": [(11, 4), (12, 4), (11, 5), (12, 5), (11, 10), (12, 10), (11, 11), (12, 11)],
    "G": [(13, 4), (13, 5), (13, 10), (13, 11)],
    "T": [(3, 5), (3, 10)],
    "t": [(2, 5), (2, 10)],
}

# 汇聚：两臂从后上/后下相撞，汇成单头；后缘 V 形开口（箭尖朝前）；接缝 1px 亮线在 V 尖内侧
MERGE_SPANS = {
    4: [(2, 5)], 5: [(2, 10)], 6: [(4, 13)], 7: [(8, 13)],
    8: [(8, 13)], 9: [(4, 13)], 10: [(2, 10)], 11: [(2, 5)],
}
MERGE_SPECIAL = {
    "S": [(9, 7), (9, 8)],
    "C": [(11, 7), (12, 7), (11, 8), (12, 8)],
    "G": [(13, 7), (13, 8)],
    "T": [(3, 5), (3, 10)],
    "t": [(2, 5), (2, 10)],
}

# 被收集：主体沿轴被拉（前缘 13→12），1px 连续流穿过张力边开口伸向收集器 2px，
# 末端上下各张开 1px 的喇叭口；无离体残滴。
COLLECT_SPANS = {
    4: [(8, 11)], 5: [(6, 12)], 6: [(4, 12)],
    7: [(2, 12)], 8: [(2, 12)], 9: [(4, 12)],
    10: [(6, 12)], 11: [(8, 11)],
}
COLLECT_SPECIAL = {
    # 1px 流：从皮肤上的 1px 开口 (12,7) 一直连到喇叭口喉部 (15,7)
    "~": [(12, 7), (13, 7), (14, 7), (15, 7)],
    "M": [(15, 6), (15, 8)],
    "C": [(10, 7), (11, 7), (10, 8), (11, 8)],
    "T": [(3, 7), (3, 8)],
    "t": [(2, 7), (2, 8)],
}

STATES = [
    ("flow", FLOW_SPANS, FLOW_SPECIAL, ["无附加物（基准）"]),
    ("blocked", BLOCKED_SPANS, BLOCKED_SPECIAL, ["前方 1×3 中性墙", "+ 轴向压 1px + 尾回卷"]),
    ("split", SPLIT_SPANS, SPLIT_SPECIAL, ["两瓣分离 2px", "各 33px，各带完整边"]),
    ("merge", MERGE_SPANS, MERGE_SPECIAL, ["两瓣相撞", "1px 亮接缝 + 后缘 V"]),
    ("collect", COLLECT_SPANS, COLLECT_SPECIAL, ["连续 1px 流", "末端 2px 喇叭口"]),
]
DIRS = [("right", "右"), ("down", "下"), ("left", "左"), ("up", "上")]


# ---------------------------------------------------------------- 构造
def mask_of(spans):
    m = set()
    for y, segs in spans.items():
        for x0, x1 in segs:
            for x in range(x0, x1 + 1):
                m.add((x, y))
    return m


def rim_of(mask):
    out = set()
    for (x, y) in mask:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in mask:
                out.add((x, y))
                break
    return out


def build(spans, special):
    mask = mask_of(spans)
    rim = rim_of(mask)
    roles = {p: ("@" if p in rim else "#") for p in mask}
    EXTERNAL = {"W", "~", "M"}   # 墙在主体外；1px 流与喇叭口是主体的外延（不参与张力边计算）
    for ch, pts in special.items():
        for p in pts:
            assert ch in EXTERNAL or p in mask, "特写像素 %s 落在主体外：%s" % (p, ch)
            roles[p] = ch
    return roles


def rot(roles, direction):
    n = {"right": 0, "down": 1, "left": 2, "up": 3}[direction]
    out = {}
    for (x, y), ch in roles.items():
        for _ in range(n):
            x, y = W - 1 - y, x
        out[(x, y)] = ch
    return out


def bbox(roles):
    xs = [p[0] for p in roles]
    ys = [p[1] for p in roles]
    return min(xs), min(ys), max(xs), max(ys)


def center(roles):
    x0, y0, x1, y1 = bbox(roles)
    return ((x0 + x1) / 2.0, (y0 + y1) / 2.0)


def px_color(ch):
    c, a = ROLE[ch]
    af = a / 255.0
    return (int(c[0] * af + BG[0] * (1 - af)),
            int(c[1] * af + BG[1] * (1 - af)),
            int(c[2] * af + BG[2] * (1 - af)))


def lum(c):
    """相对亮度 Y（sRGB 直算，仅用于比较前后端明暗）。"""
    return (0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]) / 255.0


def quads(roles):
    rows = {}
    for (x, y), ch in roles.items():
        rows.setdefault(y, []).append((x, px_color(ch)))
    n = 0
    for y, items in rows.items():
        items.sort()
        last = None
        for (x, c) in items:
            if last != c:
                n += 1
                last = c
    return n


def cell_image(roles, scale=1, border=False):
    px = [[BG + (255,) for _ in range(W)] for _ in range(W)]
    for (x, y), ch in roles.items():
        px[y][x] = px_color(ch) + (255,)
    img = Image.new("RGB", (W, W))
    img.putdata([px[y][x][:3] for y in range(W) for x in range(W)])
    if scale != 1:
        img = img.resize((W * scale, W * scale), Image.NEAREST)
    if border:
        d = ImageDraw.Draw(img)
        d.rectangle((0, 0, W * scale - 1, W * scale - 1), outline=(30, 34, 42))
    return img


# ---------------------------------------------------------------- 字体/图版
def font(sz):
    try:
        return ImageFont.truetype(FONT_PATH, sz)
    except Exception:
        return ImageFont.load_default()


F_T = font(21)
F_H = font(15)
F_B = font(12)
F_S = font(11)


def sheet(w, h, title, sub=""):
    img = Image.new("RGB", (w, h), PAPER)
    d = ImageDraw.Draw(img)
    d.text((18, 12), title, font=F_T, fill=TEXT)
    if sub:
        d.text((18, 39), sub, font=F_B, fill=DIM)
    d.line((18, 60, w - 18, 60), fill=(40, 44, 54))
    return img, d


def paste(img, x, y, roles, scale):
    img.paste(cell_image(roles, scale, border=True), (x, y))


def cap(d, x, y, s, f=F_S, col=DIM, anchor=None):
    d.text((x, y), s, font=f, fill=col, anchor=anchor)


# ---------------------------------------------------------------- 方向：4 向表
def render_dirs_4x():
    S = 4
    cell = W * S
    width, height = 900, 470
    img, d = sheet(width, height, "w4 — I6 方向（4 向 × 4×，下排 1×）",
                   "同一掩膜整数旋转；包围盒 12×8，中心恒 (7.5,7.5)；前端钝+亮，后端窄+暗")
    x0, y0 = 36, 96
    gap = 130
    for i, (di, lab) in enumerate(DIRS):
        g = rot(build(FLOW_SPANS, FLOW_SPECIAL), di)
        x = x0 + i * (cell + gap)
        paste(img, x, y0, g, S)
        cap(d, x + cell // 2, y0 - 20, "%s  %s" % (lab, di), F_B, TEXT, anchor="ma")
        # 1x 行
        paste(img, x + (cell - W) // 2, y0 + cell + 12, g, 1)
        c = center(g)
        bx = bbox(g)
        cap(d, x + cell // 2, y0 + cell + 38,
            "bbox %d×%d" % (bx[2] - bx[0] + 1, bx[3] - bx[1] + 1), F_S, DIM, anchor="ma")
        cap(d, x + cell // 2, y0 + cell + 52,
            "中心 (%.1f,%.1f)" % (c[0], c[1]), F_S, DIM, anchor="ma")
    cap(d, x0, y0 + cell + 78, "↑ 上方每列的第二行 = 1× 原尺寸（16px；实机 1 格≈113 屏幕 px，约 7× 于此）",
        F_S, ACCENT)
    cap(d, 18, height - 74,
        "前端：x=13 一条 6px 平口（y=5..10），最前 2px 为高光 G（Y=%.2f），内侧 2×2 亮核 C。" % lum(px_color("G")), F_B, TEXT)
    cap(d, 18, height - 54,
        "后端：x=2 一条 2px 平口（y=7..8），向背景压 66%%、alpha 130（Y=%.2f）—— 没有 1px 针尖，"
        "B 的「尖=前」格式塔无从触发。" % lum(px_color("T")), F_B, DIM)
    cap(d, 18, height - 32,
        "亮度比 前端/尾部 = %.1f:1，宽度比 6px:2px = 3:1，亮核偏前 2px —— 三条线索同向。" %
        (lum(px_color("G")) / max(1e-6, lum(px_color("T")))), F_S, DIM)
    img.save(os.path.join(PNG, "dirs-4x.png"))


def render_dirs_1x():
    width, height = 460, 190
    img, d = sheet(width, height, "w4 — I6 方向（1× 原尺寸）", "只看这一行判方向；实机上再放大约 7×")
    for i, (di, lab) in enumerate(DIRS):
        g = rot(build(FLOW_SPANS, FLOW_SPECIAL), di)
        x = 40 + i * 100
        paste(img, x, 100, g, 1)
        cap(d, x + W // 2, 122, lab, F_B, TEXT, anchor="ma")
        cap(d, x + W // 2, 140, di, F_S, DIM, anchor="ma")
    cap(d, 18, height - 26, "16px 格 / 无标注辅助线；本图不用于盲测，只用于确认 1× 下不糊。", F_S, DIM)
    img.save(os.path.join(PNG, "dirs-1x.png"))


# ---------------------------------------------------------------- 状态：5 态表
def render_states_4x():
    S = 4
    cell = W * S
    width, height = 1060, 540
    img, d = sheet(width, height, "w4 — I5 状态（5 态 × 4×，每格下方 1×）",
                   "物理直喻：墙 / 两瓣分离 / 两瓣相撞 / 连续流进喇叭口；全部静态单帧")
    x0, y0 = 40, 96
    gap = 126
    for i, (name, spans, sp, desc) in enumerate(STATES):
        g = build(spans, sp)
        x = x0 + i * (cell + gap)
        paste(img, x, y0, g, S)
        cap(d, x + cell // 2, y0 - 18, name, F_H, ACCENT, anchor="ma")
        # 1x
        paste(img, x + (cell - W) // 2, y0 + cell + 10, g, 1)
        cap(d, x + cell // 2, y0 + cell + 32, "1×", F_S, DIM, anchor="ma")
        # 描述：两行
        for k, line in enumerate(split_desc(desc)):
            cap(d, x + cell // 2, y0 + cell + 52 + k * 16, line, F_S, TEXT, anchor="ma")
    cap(d, 18, height - 76,
        "flow 无任何附加物；blocked 墙 #C9C2B8 落在 (14,6)-(14,8)，前脸高光撤掉（不做撞击闪白）；", F_B, TEXT)
    cap(d, 18, height - 56,
        "split 空 2px（y=7,8 全空）、两瓣各 33px；merge 两臂 29+29 撞成一体、接缝 S 在 (9,7)-(9,8)、后缘 V 开口；", F_B, DIM)
    cap(d, 18, height - 34,
        "collect 1px 流从皮肤开口 (12,7) 连到 (15,7)，末端 (15,6)/(15,8) 上下各张 1px = 喇叭口。", F_S, DIM)
    img.save(os.path.join(PNG, "states-4x.png"))


def split_desc(desc):
    """描述已经是行列表时原样返回（旧调用兼容）。"""
    if isinstance(desc, list):
        return desc
    if len(desc) <= 22:
        return [desc]
    k = len(desc) // 2
    for i in range(k, len(desc)):
        if desc[i] in "，、+":
            return [desc[:i + 1], desc[i + 1:]]
    return [desc[:22], desc[22:]]


def render_states_1x():
    width, height = 560, 190
    img, d = sheet(width, height, "w4 — I5 状态（1× 原尺寸）", "5 态并排；只看这一行判状态")
    for i, (name, spans, sp, desc) in enumerate(STATES):
        g = build(spans, sp)
        x = 26 + i * 104
        paste(img, x, 96, g, 1)
        cap(d, x + W // 2, 118, name, F_S, TEXT, anchor="ma")
    cap(d, 18, height - 26, "16px 格；状态只看拓扑（墙/空/接缝/流），不靠颜色明暗。", F_S, DIM)
    img.save(os.path.join(PNG, "states-1x.png"))


# ---------------------------------------------------------------- 状态差量
def render_state_diff():
    S = 4
    cell = W * S
    width, height = 1020, 520
    img, d = sheet(width, height, "w4 — 5 态 vs flow 的逐像素差（4×）",
                   "红=只在本态 蓝=只在 flow 黄=两态都有但角色不同 深灰=完全相同")
    x0, y0 = 30, 92
    gap = 20
    flow = build(FLOW_SPANS, FLOW_SPECIAL)
    for i, (name, spans, sp, desc) in enumerate(STATES):
        g = build(spans, sp)
        x = x0 + i * (cell + gap)
        paste(img, x, y0, g, S)
        cap(d, x + cell // 2, y0 - 18, name, F_H, ACCENT, anchor="ma")
        # 差量图
        px = [[DIFF_SAME for _ in range(W)] for _ in range(W)]
        for (xx, yy), ch in g.items():
            px[yy][xx] = DIFF_ADD
        for (xx, yy), ch in flow.items():
            if (xx, yy) in g:
                px[yy][xx] = DIFF_SAME if g[(xx, yy)] == ch else DIFF_CHG
            else:
                px[yy][xx] = DIFF_DEL
        im = Image.new("RGB", (W, W))
        im.putdata([px[yy][xx] for yy in range(W) for xx in range(W)])
        im = im.resize((cell, cell), Image.NEAREST)
        dd = ImageDraw.Draw(im)
        dd.rectangle((0, 0, cell - 1, cell - 1), outline=(44, 48, 58))
        img.paste(im, (x, y0 + cell + 34))
        nd = sum(1 for (xx, yy) in g if flow.get((xx, yy)) != g[(xx, yy)]) + \
            sum(1 for (xx, yy) in flow if (xx, yy) not in g)
        cap(d, x + cell // 2, y0 + cell + 12, "差 %d px" % nd, F_B, TEXT, anchor="ma")
    cap(d, x0, y0 + 2 * cell + 48, "上排 = 本态 4×；下排 = 与 flow 的差量掩膜（同一 16×16 网格，逐像素比较角色）。", F_B, TEXT)
    img.save(os.path.join(PNG, "state-diff-4x.png"))


# ---------------------------------------------------------------- 方向方案对照
DIR_OPTS = [
    ("A 钝口", "前端 1px 平口 + 后端尖", {
        4: [(8, 11)], 5: [(6, 13)], 6: [(4, 13)],
        7: [(2, 13)], 8: [(4, 13)], 9: [(4, 13)],
        10: [(6, 13)], 11: [(8, 11)],
    }, {}),
    ("B 亮核", "2×2 亮核偏前", FLOW_SPANS,
     {"C": [(11, 7), (12, 7), (11, 8), (12, 8)], "T": [(3, 7), (3, 8)], "t": [(2, 7), (2, 8)]}),
    ("C 眼点", "前端 1px 眼点", FLOW_SPANS,
     {"G": [(13, 7)], "T": [(3, 7), (3, 8)], "t": [(2, 7), (2, 8)]}),
    ("D 世界拖尾", "尾后 2px 拖尾点（在上一格）", FLOW_SPANS,
     {"C": [(11, 7), (12, 7), (11, 8), (12, 8)], "G": [(13, 7), (13, 8)],
      "T": [(3, 7), (3, 8)], "t": [(2, 7), (2, 8)],
      "W": []}),
    ("E 组合（交付）", "钝口 + 亮核 + 前脸高光 + 尾部渐隐", FLOW_SPANS, FLOW_SPECIAL),
]


def render_dir_options():
    S = 4
    cell = W * S
    width, height = 980, 470
    img, d = sheet(width, height, "w4 — I6 方向线索：4 个候选 + 交付组合（4× / 1×）",
                   "A 只有形状、B 只有亮核、C 只有 1px 眼点、D 世界拖尾（破坏包围盒中心）、E = 本方案")
    x0, y0 = 190, 92
    gap = 14
    for i, (name, note, spans, sp) in enumerate(DIR_OPTS):
        g = build(spans, sp)
        if name.startswith("D"):
            # 上一格的拖尾点：本格内最靠后的 2px（保守版本，真实方案还会再退一格）
            for xx in (0, 1):
                g[(xx, 7)] = "T"
            g[(1, 8)] = "T"
        x = x0 + i * (cell + gap)
        paste(img, x, y0, g, S)
        cap(d, 26, y0 + 14, name, F_B, ACCENT)
        cap(d, 26, y0 + 32, note, F_S, DIM)
        c = center(g)
        cap(d, x + cell // 2, y0 + cell + 8, "中心(%.1f,%.1f)" % c, F_S,
            TEXT if abs(c[0] - 7.5) < 1e-6 and abs(c[1] - 7.5) < 1e-6 else DIFF_ADD, anchor="ma")
        paste(img, x + (cell - W) // 2, y0 + cell + 28, g, 1)
    cap(d, 18, height - 78,
        "结论：A 单独用就是 B 的原失败形态（尖尾仍在最前读数）；B/C 单独用各只有 1 条亮度线索，"
        "被「尖=前」压过的概率未消除。", F_B, TEXT)
    cap(d, 18, height - 56,
        "D 把包围盒拉到 x=0..13 → 中心 6.5，违反「4 向中心恒 (7.5,7.5)」，且与 I4 的离体残滴抢像素、"
        "与 I3 的尺寸通道相邻。", F_B, DIM)
    cap(d, 18, height - 34,
        "E 同时给 4 条同向线索（宽度 3:1、亮度 4.6:1、2×2 亮核偏前、前脸 2px 高光），并彻底删掉针尖。", F_S, ACCENT)
    img.save(os.path.join(PNG, "dir-options-4x.png"))


# ---------------------------------------------------------------- tiles
def render_tiles():
    os.makedirs(TILES, exist_ok=True)
    man = []
    for di, lab in DIRS:
        g = rot(build(FLOW_SPANS, FLOW_SPECIAL), di)
        p = os.path.join(TILES, "dir-%s.png" % di)
        cell_image(g, 4, border=True).save(p)
        man.append({"path": "docs/flow-visual/round2/w4/tiles/dir-%s.png" % di,
                    "group": "dir", "answer": di,
                    "meta": json.dumps({"direction": di, "view": "4x", "comp": "water"})})
    for name, spans, sp, desc in STATES:
        g = build(spans, sp)
        p = os.path.join(TILES, "state-%s.png" % name)
        cell_image(g, 4, border=True).save(p)
        man.append({"path": "docs/flow-visual/round2/w4/tiles/state-%s.png" % name,
                    "group": "state", "answer": name,
                    "meta": json.dumps({"state": name, "view": "4x", "comp": "water"})})
    with open(os.path.join(TILES, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(man, f, ensure_ascii=False, indent=2)


# ---------------------------------------------------------------- dump
def dump():
    print("=== 角色图例 ===")
    for ch, (c, a) in ROLE.items():
        print(" %s  #%02X%02X%02X  alpha %3d  Y=%.3f  %s" %
              (ch, c[0], c[1], c[2], a, lum(px_color(ch)), "" if ch not in "#tT" else ""))
    print()
    flow = build(FLOW_SPANS, FLOW_SPECIAL)
    for name, spans, sp, desc in STATES:
        g = build(spans, sp)
        print("=== %s — %s ===" % (name, desc))
        for y in range(W):
            print(" %2d %s" % (y, "".join(g.get((x, y), ".") for x in range(W))))
        print("    " + "".join(str(x % 10) for x in range(W)))
        bx = bbox(g)
        c = center(g)
        nd = sum(1 for p in g if flow.get(p) != g[p]) + sum(1 for p in flow if p not in g)
        print("  面积 %d  bbox %d×%d (%d,%d)-(%d,%d) 中心(%.1f,%.1f) quads=%d 差flow %dpx"
              % (len(g), bx[2] - bx[0] + 1, bx[3] - bx[1] + 1, bx[0], bx[1], bx[2], bx[3],
                 c[0], c[1], quads(g), nd))
        print()

    print("=== 4 向 ===")
    for di, lab in DIRS:
        g = rot(flow, di)
        bx = bbox(g)
        c = center(g)
        print("  %-5s bbox %d×%d (%d,%d)-(%d,%d) 中心(%.1f,%.1f) 面积 %d quads %d"
              % (di, bx[2] - bx[0] + 1, bx[3] - bx[1] + 1, bx[0], bx[1], bx[2], bx[3],
                 c[0], c[1], len(g), quads(g)))

    print("\n=== 前后端度量（右向）===")
    front = [p for p in flow if p[0] == 13]
    rear = [p for p in flow if p[0] <= 3]
    print(" 前端 x=13 高 %dpx 像素 %s" % (len(front), sorted(front)))
    print("   最前 2px 颜色 #%02X%02X%02X Y=%.3f（G）" % (px_color("G") + (lum(px_color("G")),)))
    print(" 后端 x=2..3 高 %dpx 像素 %s" % (len(rear), sorted(rear)))
    print("   尾部本体色 #%02X%02X%02X Y=%.3f（T，alpha150）" % (px_color("T") + (lum(px_color("T")),)))
    print("   本体色     #%02X%02X%02X Y=%.3f（#，alpha208）" % (px_color("#") + (lum(px_color("#")),)))
    print(" 亮度比 前端/尾部 = %.2f ; 宽度比 = 6:2 = 3.0" %
          (lum(px_color("G")) / max(1e-6, lum(px_color("T")))))

    print("\n=== 状态质量守恒 ===")
    split = build(SPLIT_SPANS, SPLIT_SPECIAL)
    up = sum(1 for p in split if p[1] <= 6)
    dn = sum(1 for p in split if p[1] >= 9)
    print(" split 上瓣 %d / 下瓣 %d / 空行 y=7,8 %d px" % (up, dn, sum(1 for p in split if p[1] in (7, 8))))
    mg = build(MERGE_SPANS, MERGE_SPECIAL)
    a1 = sum(1 for p in mg if p[1] <= 7)
    a2 = sum(1 for p in mg if p[1] >= 8)
    print(" merge 上臂 %d / 下臂 %d / 合计 %d = 两臂之和 %s" % (a1, a2, len(mg), a1 + a2 == len(mg)))
    col = build(COLLECT_SPANS, COLLECT_SPECIAL)
    print(" collect 主体 %d + 流 %d + 喇叭口 %d" %
          (sum(1 for p in col if p[0] <= 11), sum(1 for p in col if p[0] in (12, 13, 14)),
           sum(1 for p in col if p[0] == 15)))


def main():
    os.makedirs(PNG, exist_ok=True)
    if "--dump" in sys.argv:
        dump()
        return
    render_dirs_1x()
    render_dirs_4x()
    render_states_1x()
    render_states_4x()
    render_state_diff()
    render_dir_options()
    render_tiles()
    print("done ->", PNG, TILES)


if __name__ == "__main__":
    main()
