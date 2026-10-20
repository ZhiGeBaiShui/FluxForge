# -*- coding: utf-8 -*-
"""
W3 「Motion & State」渲染器 —— 只做时间维度，不重画静态形状。

形状来源：concepts/b/render_b.py 的 TIERS["large"] 行表 + 描边/明暗/高光三阶
（只读复用其数值，本文件不 import、不修改 B 的任何文件）。

产出（png/）：
  loop-1x.png / loop-4x.png            12 帧循环（常温 + 冷/热 + 含量少 相位采样）
  states-1x.png / states-4x.png        6 个状态代表帧 + 「经过格子」2 帧边框
  transition-1x.png / transition-4x.png 流动→受阻 6 帧转换

运行：
  python render_w3.py          仅出图 + 打印统计表
  python render_w3.py --stats  同上（统计表始终打印）
"""
import os
import math
import sys
from PIL import Image, ImageDraw, ImageFont

W = 16
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "png")
FONT_PATH = "C:/Windows/Fonts/msyh.ttc"

PAPER = (10, 11, 14)
BG = (14, 16, 21)
GRID = (30, 34, 42)
ICON = (78, 86, 106)
TEXT = (214, 220, 230)
DIM = (122, 130, 146)
ACCENT = (255, 206, 92)
WARN = (255, 138, 110)
OK = (126, 226, 160)

BASE = (39, 199, 230)              # water
TEMP_TINT = {"cold": (214, 236, 255), "ambient": None, "hot": (255, 150, 78)}
TEMP_CN = {"cold": "冷 -10℃ ×0.7", "ambient": "常温 21℃ ×1.0", "hot": "热 78℃ ×1.4"}

# B 的 large 档行表（右向：头在右、尾在左）。包围盒 x=3..12 / y=4..11
ROWS_L = {4: (8, 11), 5: (6, 12), 6: (4, 12), 7: (3, 12),
          8: (4, 12), 9: (5, 12), 10: (6, 12), 11: (8, 11)}
# 子滴 6x5（= large 的 60%）：行表为局部坐标
ROWS_S = {0: (3, 5), 1: (1, 5), 2: (0, 5), 3: (1, 5), 4: (3, 5)}


# ------------------------------------------------------------------ 工具
def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def shade(c, f):
    if f >= 0:
        return tuple(int(round(v + (255 - v) * f)) for v in c)
    return tuple(int(round(v * (1 + f))) for v in c)


def rows_mask(rows, ox=0, oy=0):
    m = set()
    for y, (a, b) in rows.items():
        for x in range(a, b + 1):
            m.add((x + ox, y + oy))
    return m


def rim_of(mask):
    out = set()
    for (x, y) in mask:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in mask:
                out.add((x, y))
                break
    return out


def shift_row(mask, yy, dx):
    if dx == 0:
        return set(mask)
    return {(x + dx, y) if y == yy else (x, y) for (x, y) in mask}


def clip_maxx(mask, k):
    return {p for p in mask if p[0] <= k}


MASK13 = rows_mask(ROWS_L) | {(13, y) for y in range(5, 11)}   # 允许伸展到 13


# ------------------------------------------------------------------ 绘制
def blend(bg, col, a):
    f = a / 255.0
    return (int(col[0] * f + bg[0] * (1 - f)),
            int(col[1] * f + bg[1] * (1 - f)),
            int(col[2] * f + bg[2] * (1 - f)))


def rim_color(temp):
    c = shade(BASE, -0.52)
    t = temp
    if t == "cold":
        return mix(c, TEMP_TINT["cold"], 0.88)
    if t == "hot":
        return mix(c, TEMP_TINT["hot"], 0.86)
    return c


def hi_color(temp):
    if temp == "cold":
        return mix(BASE, (255, 255, 255), 0.60)     # 冷：高光更暗
    if temp == "hot":
        return mix(BASE, (255, 255, 255), 0.90)     # 热：高光更亮
    return mix(BASE, (255, 255, 255), 0.74)


def paint(mask, tail=(), hi=None, temp="ambient", alpha=208, ascale=1.0, tex=True):
    """mask: 身体像素；tail: 尾脊像素；hi: (x,y,alpha)"""
    rc = rim_color(temp)
    rim = rim_of(mask)
    inner = mask - rim
    cell = {}
    for p in mask:
        if p in rim:
            # 上亮下暗 1px 明度带（B 的三阶明暗）
            cell[p] = (shade(rc, 0.10 if p[1] <= 7 else -0.08), int(255 * ascale), "rim")
        else:
            cell[p] = (BASE, int(alpha * ascale), "body")
    if tex:
        # 静态内部纹理 1px（B 的水=caustic，2 点；不参与动画，不编码属性）
        tc = mix(BASE, (255, 255, 255), 0.62)
        for p in [(7, 5), (6, 9)]:
            if p in inner:
                cell[p] = (tc, min(255, int(alpha * ascale)), "tex")
    for p in tail:
        cell[p] = (shade(BASE, -0.30), min(255, int((alpha + 16) * ascale)), "tail")
    if hi:
        hx, hy, ha = hi
        cell[(hx, hy)] = (hi_color(temp), min(255, int(ha * ascale)), "hi")
    return cell


def tail_px(L):
    """尾脊：第 7 行 x=5-L..4 全长，L>=3 时第 8 行再短 1px（锥形液丝）。
    L∈2..4 → 外伸 (L-2)px，最远 x=1，与格边框 x=0 恒留 1px。"""
    out = [(x, 7) for x in range(5 - L, 5)]
    if L >= 3:
        out += [(x, 8) for x in range(6 - L, 5)]
    return out


def snap_hi(mask, tail, hi):
    if hi is None:
        return None
    x, y, a = hi
    if (x, y) in mask or (x, y) in tail:
        return hi
    best, bd = None, 1e9
    for (px, py) in list(mask) + list(tail):
        d = (px - x) ** 2 + (py - y) ** 2
        if d < bd:
            best, bd = (px, py), d
    return (best[0], best[1], a) if best else None


def cell_image(cell, icon=True, border=None, bgcol=None):
    bg = bgcol or BG
    px = [[bg + (255,) for _ in range(W)] for _ in range(W)]
    if icon:
        draw_icon(px)
    if border:
        bc, ba = border
        c = blend(bg, bc, ba)
        for x in range(W):
            px[0][x] = c + (255,)
            px[W - 1][x] = c + (255,)
        for y in range(1, W - 1):
            px[y][0] = c + (255,)
            px[y][W - 1] = c + (255,)
    for (x, y), (col, a, _k) in cell.items():
        if not (0 <= x < W and 0 <= y < W):
            continue
        b = px[y][x]
        px[y][x] = blend(b, col, a) + (255,)
    img = Image.new("RGB", (W, W))
    img.putdata([px[y][x] for y in range(W) for x in range(W)])
    return img


def draw_icon(px):
    """B 的 12x12 图标占位（瓶子），(2,2)-(11,12)。"""
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


def quads(cell):
    """每行同 (rgb, alpha) 的最大连续段 = 1 quad。"""
    rows = {}
    for (x, y), (c, a, k) in cell.items():
        rows.setdefault(y, []).append((x, c, a))
    n = 0
    for y, items in rows.items():
        items.sort()
        last = None
        for (x, c, a) in items:
            if last != (c, a):
                n += 1
                last = (c, a)
    return n


# ------------------------------------------------------------------ 时间模型
TAIL_T = [2, 3, 3, 4, 4, 4, 4, 4, 4, 4, 3, 2]        # F0..F11 尾长 px
# 高光 1px：(x, y, alpha)
HI_T = [(10, 4, 255), (10, 5, 255), (11, 5, 255), (11, 6, 255),
        (11, 7, 255), (11, 8, 255), (10, 9, 255), (9, 10, 255),
        (7, 10, 255), (6, 9, 235), (5, 8, 180), (3, 7, 130)]


def squeeze(f, temp="ambient", content="large"):
    """返回 [(行, dx)]：行 y=4 与 y=11 反向平移 ±1px（面积/包围盒守恒）"""
    k = {"cold": 0.7, "ambient": 1.0, "hot": 1.4}[temp]
    phi = 2 * math.pi * f * k / 6.0
    s = math.sin(phi)
    sign = 0 if abs(s) < 0.5 else (1 if s > 0 else -1)
    if content != "large" and sign != 0 and f % 2 == 0:
        sign = 0                                   # 0.5px = 1px × 50% 占空比（只在奇数帧施加）
    if sign == 0:
        return [], phi
    return [(4, sign), (11, -sign)], phi


def flow(f, temp="ambient", content="large", border=None):
    mask = rows_mask(ROWS_L)
    sq, phi = squeeze(f, temp, content)
    for (y, dx) in sq:
        mask = shift_row(mask, y, dx)
    if content == "large":
        L = TAIL_T[f]
    else:
        L = 2 if f in (0, 11) else TAIL_T[f]
    tail = tail_px(L)
    hi = snap_hi(mask, tail, HI_T[f] if f != 11 else (3, 7, HI_T[11][2]))
    return paint(mask, tail, hi, temp)


# --- 受阻 -----------------------------------------------------------------
def blocked(maxx, tailL=2, hi=(11, 8, 130), temp="ambient"):
    mask = clip_maxx(rows_mask(ROWS_L), maxx)
    tail = tail_px(tailL)
    return paint(mask, tail, snap_hi(mask, tail, hi), temp)


# --- 受阻 → 流动（6 帧 = 0.5s）--------------------------------------------
RECOVER = [(11, 2), (12, 3), (13, 3), (13, 4), (12, 3), (12, 2)]


def recover(g):
    maxx, tl = RECOVER[g]
    mask = clip_maxx(MASK13, maxx)
    tail = tail_px(tl)
    hi = HI_T[0] if g == 5 else (11, 8, min(255, 130 + g * 25))
    return paint(mask, tail, snap_hi(mask, tail, hi))


# --- 发身（1px 亮点 3 帧长成完整液滴）--------------------------------------
def spawn(s):
    if s == 0:
        return {(11, 7): (mix(BASE, (255, 255, 255), 0.50), 255, "spark")}
    if s == 1:
        return {(x, 7): (mix(BASE, (255, 255, 255), 0.30), 210, "body") for x in range(10, 13)}
    if s == 2:
        m = {(x, y) for x in range(9, 13) for y in range(6, 9)}
        return paint(m, (), (10, 6, 235))
    return flow(0)


# --- 分流 / 汇聚 -----------------------------------------------------------
def waist(mask, cols=(7,)):
    """中段纵向收腰：只在中列去掉上下各 2 行，保留 2px 高的颈部"""
    return {(x, y) for (x, y) in mask if not (x in cols and y not in (7, 8))}


def split(p):
    if p == 0:
        return flow(0)
    base = rows_mask(ROWS_L)
    if p == 1:                                        # P1 沿轴拉长：头 +1 / 尾 −1（面积守恒）
        m = set(base) | {(13, y) for y in range(5, 11)} | {(2, y) for y in (6, 7, 8)}
        return paint(m, tail_px(4), (12, 7, 255))
    if p == 2:                                        # P2 收腰：中列只剩 2px 高颈部
        m = waist(clip_maxx(MASK13, 13))
        return paint(m, tail_px(4), (12, 7, 255))
    if p == 3:                                        # P3 断裂：中列 1px 全断
        m = {q for q in waist(clip_maxx(MASK13, 13)) if q[0] != 7}
        return paint(m, tail_px(3), (12, 7, 255))
    left = paint(rows_mask(ROWS_S, 1, 5), (), (6, 7, 255))       # 子滴 60%：6x5
    right = paint(rows_mask(ROWS_S, 9, 5), (), (14, 7, 255))
    left.update(right)
    return left


def merged_child(ox, oy=5):
    return rows_mask(ROWS_S, ox, oy)


def merge(m):
    if m == 0:
        a = paint(merged_child(1), (), (6, 7, 255))
        a.update(paint(merged_child(9), (), (14, 7, 255)))
        return a
    if m == 1:
        a = paint(merged_child(2), (), (7, 7, 255))
        a.update(paint(merged_child(8), (), (13, 7, 255)))
        return a
    if m == 2:
        msk = merged_child(3) | merged_child(7)
        return paint(msk, (), (8, 7, 255))
    if m == 3:                                        # 接缝 1px 闪光
        msk = rows_mask(ROWS_L)
        c = paint(msk, tail_px(3), None)
        c[(8, 7)] = (mix(BASE, (255, 255, 255), 0.55), 255, "spark")
        return c
    return flow(0)


# --- 被收集（6 帧 alpha 递减 + 尾部拉长）-----------------------------------
COLLECT = [(3, 2, 208), (4, 3, 200), (5, 4, 170), (6, 6, 130), (8, 8, 84), (10, 10, 40)]


def collect(c):
    if c >= len(COLLECT):
        return {}
    rear, L, a = COLLECT[c]
    body = {p for p in rows_mask(ROWS_L) if p[0] >= rear}
    tail = [(x, 7) for x in range(max(0, rear - L), rear)]
    return paint(body, tail, None, alpha=a, ascale=a / 208.0)


# ------------------------------------------------------------------ 图版
def font(sz):
    try:
        return ImageFont.truetype(FONT_PATH, sz)
    except Exception:
        return ImageFont.load_default()


class Sheet(object):
    def __init__(self, w, h, title, sub=""):
        self.w, self.h = w, h
        self.img = Image.new("RGB", (w, h), PAPER)
        self.d = ImageDraw.Draw(self.img)
        self.d.text((18, 12), title, font=font(21), fill=TEXT)
        if sub:
            self.d.text((18, 40), sub, font=font(12), fill=DIM)
        self.d.line((18, 60, w - 18, 60), fill=(40, 44, 54))

    def cell(self, x, y, cell, scale, icon=True, border=None, bg=None):
        im = cell_image(cell, icon, border, bg)
        if scale != 1:
            im = im.resize((W * scale, W * scale), Image.NEAREST)
        self.img.paste(im, (x, y))
        self.d.rectangle((x - 1, y - 1, x + W * scale, y + W * scale), outline=GRID)

    def text(self, x, y, s, f=10, col=DIM, anchor=None):
        self.d.text((x, y), s, font=font(f), fill=col, anchor=anchor)

    def save(self, name, trim=None):
        p = os.path.join(OUT, name)
        img = self.img
        if trim and trim < img.size[1]:
            img = img.crop((0, 0, img.size[0], int(trim) + 10))
        img.save(p)
        return p


def strip(sheet, y, frames, scale, labels, gap=None, fsz=None, lsz=None):
    gap = gap if gap is not None else (6 if scale == 1 else 12)
    cellw = W * scale
    for i, (cellimg, lab) in enumerate(zip(frames, labels)):
        x = 20 + i * (cellw + gap)
        sheet.cell(x, y, cellimg, scale)
        if lab:
            f = fsz or (8 if scale == 1 else 11)
            sheet.text(x + cellw // 2, y + cellw + 2, lab, f, DIM, anchor="ma")
# ------------------------------------------------------------------ 图 1/2
def render_loop(scale, name):
    n = 12
    gap = 6 if scale == 1 else 12
    cellw = W * scale
    width = 20 * 2 + n * (cellw + gap) - gap
    top = 78
    tp = 16 if scale > 1 else 11          # 温度小标题行高
    if scale == 1:
        sh = Sheet(max(width, 372), 380, "B·流体 — 12fps 12 帧循环（1.0s）",
                   "尾 2→4px · 挤压 ±1px · 高光 1px · 正弦 0.5s")
    else:
        sh = Sheet(max(width, 940), 640,
                   "B·流体 — 12fps / 12 帧循环（常温 = 1.0s）",
                   "尾长 2→4px · ±1px 挤压（面积/包围盒守恒）· 高光 1px · 挤压 0.5s 正弦（每循环 2 周期）")

    # --- 主循环：常温 12 帧
    labs = ["F%d\n%dms" % (f, int(round(f * 1000 / 12.0))) if scale > 1 else "%d" % f
            for f in range(12)]
    strip(sh, top, [flow(f) for f in range(12)], scale, labs, gap)
    y = top + cellw + (40 if scale > 1 else 18)

    # --- 温度耦合：同一 12 帧包络下的相位采样
    for temp in ("cold", "ambient", "hot"):
        sh.text(20, y, TEMP_CN[temp], 10 if scale > 1 else 8,
                {"cold": (150, 200, 240), "ambient": DIM, "hot": (255, 170, 120)}[temp])
        y += tp
        strip(sh, y, [flow(f, temp) for f in range(12)], scale, [""] * 12, gap)
        y += cellw + (8 if scale > 1 else 5)

    # --- 含量：少 = 0.5px 占空比
    y += 2
    sh.text(20, y, "常温·含量少：幅度 0.5px（1px × 50% 占空比）", 10 if scale > 1 else 8, OK)
    y += tp
    strip(sh, y, [flow(f, "ambient", "small") for f in range(12)], scale, [""] * 12, gap)
    y += cellw + (18 if scale > 1 else 14)

    if scale == 1:
        notes = ["φ=2πfk/6：常温 k=1（每循环 2 周期）冷 .7 热 1.4",
                 "挤压：y4 +1 向头 / y11 −1 向尾，像素数不变",
                 "尾：F0 2 F3 4 F4-9 4 F10 3 F11 2；尖端 x=1"]
    else:
        notes = [
            "相位 φ(f)=2π·f·k/6：常温 k=1 → 每循环 2 个正弦周期（0.5s/周期）；冷 k=0.7、热 k=1.4（相位跨循环累加，不锁 12 帧）",
            "挤压：第 4 行 +1px 向头 / 第 11 行 −1px 向尾（互为反向），逐帧像素数不变 → 不会被读成「含量变化」",
            "尾长：F0 2px → F3 4px（发身）· F4–F9 4px（稳定）· F10 3px、F11 2px（回卷）· 尾尖最远 x=1，与格边框 x=0 恒留 1px",
        ]
    for i, s in enumerate(notes):
        sh.text(20, y + i * (15 if scale > 1 else 10), s, 10 if scale > 1 else 7, DIM)
    return sh.save(name, y + len(notes) * (15 if scale > 1 else 10))


# ------------------------------------------------------------------ 图 3/4
def cells_row(sh, y, items, scale, gap, fsz, subfsz):
    cellw = W * scale
    for i, (cell, name, sub) in enumerate(items):
        x = 20 + i * (cellw + gap)
        sh.cell(x, y, cell, scale)
        sh.text(x + cellw // 2, y + cellw + 3, name, fsz, TEXT, anchor="ma")
        for j, line in enumerate(sub if scale > 1 else []):
            sh.text(x + cellw // 2, y + cellw + 3 + (15 if scale > 1 else 10) * (j + 1),
                    line, subfsz, DIM, anchor="ma")


def render_states(scale, name):
    cellw = W * scale
    gap = 30 if scale == 1 else 34
    width = max(20 * 2 + 6 * (cellw + gap) - gap, 660)
    sh = Sheet(width, 520, "B·流体 — 6 个状态代表帧（静态可读性）",
               "同一包围盒：状态差异靠「轴向长度 / 中间是否有缝 / 是否有两条尾巴 / 整体变淡」，不靠颜色")

    items = [
        (flow(5), "流动", ["F5 · 尾 4px", "挤压 y4:+1"]),
        (blocked(11, 2, (11, 8, 130)), "受阻", ["maxx=11 · 尾 2px", "T7 起静止"]),
        (spawn(1), "发身", ["S1 · 3px 条", "面积 3px²"]),
        (split(2), "分流", ["P2 · 收腰", "颈部 1×2px"]),
        (merge(3), "汇聚", ["M3 · 闪光 1px", "(8,7) α255"]),
        (collect(3), "被收集", ["C3 · 尾 6px", "α=130"]),
    ]
    cells_row(sh, 78, items, scale, gap, 12 if scale > 1 else 8, 9 if scale > 1 else 7)
    y = 78 + cellw + (68 if scale > 1 else 46)

    # --- 经过格子的 2 帧边框
    sh.d.line((18, y - 12, width - 18, y - 12), fill=(40, 44, 54))
    sh.text(20, y, "「经过格子」反馈：进入新格的前 2 帧，该格画 1px 边框（不填满格）", 12 if scale > 1 else 8, ACCENT)
    y += 20 if scale > 1 else 14

    bc = mix(BASE, (255, 255, 255), 0.35)
    bframes = [(flow(0), "t+0ms", ["α=0.35"]),
               (flow(0), "t+83ms", ["α=0.17"]),
               (flow(0), "t+166ms", ["α=0"])]
    borders = [(bc, 89), (bc, 45), None]
    for i, (cell, lab, sub) in enumerate(bframes):
        x = 20 + i * (cellw + gap)
        sh.cell(x, y, cell, scale, border=borders[i])
        sh.text(x + cellw // 2, y + cellw + 3, lab, 11 if scale > 1 else 8, TEXT, anchor="ma")
        if scale > 1:
            sh.text(x + cellw // 2, y + cellw + 18, sub[0], 9, DIM, anchor="ma")
    tx = 20 + 3 * (cellw + gap) + 4
    for j, line in enumerate([
        "边框 = 16×16 格最外 1px 环（上下各 1 个 16×1 quad +",
        "左右各 1 个 1×14 quad，共 4 quads，不填格）",
        "颜色 = mix(成分色, #FFF, 0.35)；α 89 → 45 → 0",
        "叠加顺序：背景 → 图标(12×12) → 边框 → 液滴",
        "三者几何不重叠（图标 2..11，边框 0/15，液滴 1..14）",
    ]):
        sh.text(tx, y + j * (15 if scale > 1 else 10), line, 10 if scale > 1 else 7, DIM)
    return sh.save(name, y + cellw + (80 if scale > 1 else 56))


# ------------------------------------------------------------------ 图 5/6
def render_transition(scale, name):
    cellw = W * scale
    gap = 30 if scale == 1 else 34
    width = max(20 * 2 + 6 * (cellw + gap) - gap, 700)
    sh = Sheet(width, 460, "B·流体 — 流动 → 受阻：预压缩 2 帧 + 撞击 + 3 帧回弹 + 2 次微抖",
               "全部位移都在运动轴上（无垂直位移）· 除撞击帧外单帧变化 ≤1px · 0.5s 后几何完全静止")

    items = [
        (blocked(11, 3, (12, 8, 200)), "T−2", ["预压缩 −1", "maxx=11 尾 3"]),
        (blocked(11, 3, (11, 8, 180)), "T−1", ["顶住 · 高光 180", "maxx=11 尾 3"]),
        (blocked(10, 2, (11, 8, 255)), "T0", ["撞击 −2", "maxx=10 α255"]),
        (blocked(11, 2, (11, 8, 255)), "T1", ["回弹 −1", "maxx=11"]),
        (blocked(12, 2, (11, 8, 235)), "T2", ["过冲 +1（微抖 1）", "maxx=12"]),
        (blocked(11, 2, (11, 8, 130)), "T7+", ["静止 · 不再抖", "maxx=11 恒定"]),
    ]
    cells_row(sh, 78, items, scale, gap, 12 if scale > 1 else 8, 9 if scale > 1 else 7)

    y = 78 + cellw + (94 if scale > 1 else 66)
    for i, s in enumerate([
        "T−2/T−1 预压缩：头内收 1px、尾部仍 3px（身体先缩短，还没撞上）；高光 α 255→200→180",
        "T0 撞击：头再内收 1px（maxx=10），高光 α255 仅 1 帧；面积 −15%，低于含量 4 档的 30% 阈值",
        "T1–T3 回弹：maxx 10→11→12→11（每帧 1px，第 3 帧过冲 +1）；T4/T6 两次微抖（maxx=12）",
        "T7 起 maxx=11 恒定，逐帧像素 100% 相同（0 抖动）；回弹+微抖共 7 帧 583ms，微抖落在 333ms / 500ms",
    ]):
        sh.text(20, y + i * (16 if scale > 1 else 11), s, 10 if scale > 1 else 7, DIM)
    return sh.save(name, y + 4 * (16 if scale > 1 else 11))


# ------------------------------------------------------------------ 统计
def stats():
    print("## 逐帧参数（常温 / 含量多）")
    print("| 帧 | ms | 尾长 px | 挤压（行:dx） | 高光 (x,y) α | 像素 | quads |")
    print("|---|---|---|---|---|---|---|")
    for f in range(12):
        sq, _ = squeeze(f)
        c = flow(f)
        s = " ".join("y%d:%+d" % (y, dx) for (y, dx) in sq) or "—"
        hx, hy, ha = HI_T[f]
        print("| F%d | %d | %d | %s | (%d,%d) %d | %d | %d |"
              % (f, int(round(f * 1000 / 12.0)), TAIL_T[f], s, hx, hy, ha, len(c), quads(c)))
    print()
    print("## 状态代表帧")
    for nm, c in [("流动 F5", flow(5)), ("受阻稳态", blocked(11, 2, (11, 8, 130))),
                  ("发身 S1", spawn(1)), ("发身 S2", spawn(2)), ("分流 P2", split(2)),
                  ("分流 P4", split(4)), ("汇聚 M3", merge(3)), ("被收集 C3", collect(3)),
                  ("被收集 C5", collect(5))]:
        print("- %-8s 像素 %3d  quads %2d" % (nm, len(c), quads(c)))
    print()
    print("## 逐帧差异（相邻帧 + 循环接缝）")
    for f in range(12):
        g = (f + 1) % 12
        a, b = flow(f), flow(g)
        diff = len({p for p in a if a[p][:2] != b.get(p, (None, None))[:2]} |
                   {p for p in b if a.get(p, (None, None))[:2] != b[p][:2]})
        print("- F%d→F%d 变化像素 %d" % (f, g, diff))
    print()
    print("## 受阻时间轴：与稳态的差")
    ref = blocked(11, 2, (11, 8, 130))
    for lab, c in [("T-2", blocked(11, 3, (12, 8, 200))), ("T-1", blocked(11, 3, (11, 8, 180))),
                   ("T0", blocked(10, 2, (11, 8, 255))), ("T1", blocked(11, 2, (11, 8, 255))),
                   ("T2", blocked(12, 2, (11, 8, 235))), ("T7", blocked(11, 2, (11, 8, 130)))]:
        diff = len({p for p in ref if ref[p][:2] != c.get(p, (None, None))[:2]} |
                   {p for p in c if ref.get(p, (None, None))[:2] != c[p][:2]})
        print("- %-4s 与稳态差 %2d px  面积 %+3.0f%%" % (lab, diff, 100.0 * (len(c) - len(ref)) / max(1, len(ref))))
    print()
    print("## 恢复（受阻→流动）")
    for g in range(6):
        c = recover(g)
        print("- G%d maxx=%d 尾 %d 像素 %d" % (g, RECOVER[g][0], RECOVER[g][1], len(c)))
    print()
    print("## 发身 / 分流 / 汇聚 / 收集 面积序列")
    for nm, seq in [("发身", [spawn(s) for s in range(4)]),
                    ("分流", [split(p) for p in range(5)]),
                    ("汇聚", [merge(m) for m in range(5)]),
                    ("收集", [collect(c) for c in range(7)])]:
        print("- %s: %s" % (nm, " → ".join(str(len(c)) for c in seq)))


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    ps = []
    ps.append(render_loop(1, "loop-1x.png"))
    ps.append(render_loop(4, "loop-4x.png"))
    ps.append(render_states(1, "states-1x.png"))
    ps.append(render_states(4, "states-4x.png"))
    ps.append(render_transition(1, "transition-1x.png"))
    ps.append(render_transition(4, "transition-4x.png"))
    for p in ps:
        im = Image.open(p)
        print("%-24s %dx%d" % (os.path.basename(p), im.size[0], im.size[1]))
    stats()


if __name__ == "__main__":
    main()
