#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""render_final.py — 终稿渲染器：钝头液滴 + 物理状态 + 底部射程点。

生成 docs/flow-visual/final/ 下全部图版、16×16 tile、atlas、盲测表。
只用 Pillow / numpy；全部图形自绘；不读 src/**、assets/**。

用法：
    python render_final.py            # 生成全部
    python render_final.py --dump     # 只打印 quads / 面积 / bbox / 自检
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
PNG = HERE / "png"
TILES = HERE / "tiles"
BLIND = HERE / "blind"

BG = (0x1B, 0x17, 0x15)
GRID = (0x2A, 0x23, 0x1F)
INK = (0xE8, 0xE2, 0xD8)
INK2 = (0xA8, 0xA0, 0x94)
ICON = (0x5A, 0x62, 0x70)
ICON_EDGE = (0x3E, 0x45, 0x52)
ICON_A = 150

# 成分色（B 原表）
COMPS = {
    "water": (0x27, 0xC7, 0xE6),
    "poison": (0x9B, 0x5C, 0xFF),
    "oil": (0xF0, 0xB3, 0x2B),
    "blood": (0xE0, 0x3A, 0x56),
    "acid": (0x7F, 0xD1, 0x2B),
    "energy": (0xE9, 0xF2, 0xFF),
}
CN = {"water": "水", "poison": "毒", "oil": "油", "blood": "血", "acid": "酸", "energy": "能量"}

TEMPS = ("cold", "normal", "hot")
TCN = {"cold": "冷 ≤5℃", "normal": "常温 21℃", "hot": "热 ≥60℃"}
CONTENT = ("empty", "small", "mid", "large")
CCN = {"empty": "空", "small": "少", "mid": "中", "large": "多"}
CBBOX = {"empty": (5, 7, 6, 1), "small": (4, 5, 8, 6), "mid": (4, 4, 8, 8), "large": (2, 4, 12, 8)}
STATES = ("flow", "blocked", "split", "merge", "collect")
SCN = {"flow": "流动", "blocked": "受阻", "split": "分流", "merge": "汇聚", "collect": "被收集"}
DIRS = ("right", "down", "left", "up")
DCN = {"right": "右", "down": "下", "left": "左", "up": "上"}
DIRV = {"right": (1, 0), "down": (0, 1), "left": (-1, 0), "up": (0, -1)}
DARK = (0x24, 0x1E, 0x2E)

FONT_R = "C:/Windows/Fonts/msyh.ttc"
FONT_B = "C:/Windows/Fonts/msyhbd.ttc"


# ---------------------------------------------------------------- 颜色工具
def mix(a, b, t):
    return tuple(int(round(a[i] * (1 - t) + b[i] * t)) for i in range(3))


def shade(c, f):
    return mix(c, (255, 255, 255), f) if f >= 0 else mix(c, (0, 0, 0), -f)


def edge_color(comp, temp):
    base = shade(comp, -0.52)
    if temp == "cold":
        return mix(base, (0xD6, 0xEC, 0xFF), 0.88)
    if temp == "hot":
        return mix(base, (0xFF, 0x96, 0x4E), 0.86)
    return base


def body_color(comp, y, bbox):
    y0, h = bbox[1], bbox[3]
    t = (y - y0) / max(1, h - 1)
    if t < 1.0 / 3.0:
        return shade(comp, 0.12)
    if t > 2.0 / 3.0:
        return shade(comp, -0.12)
    return comp


TEX = {
    "water": [(6, 9, "X")],
    "poison": [(5, 7, "X"), (7, 7, "X"), (6, 8, "X")],
    "oil": [(5, 7, "Y"), (7, 7, "Z"), (6, 8, "X")],
    "blood": [(5, 8, "K"), (6, 8, "K"), (7, 8, "K")],
    "energy": [(5, 7, "N"), (7, 7, "N"), (6, 8, "N")],
    "acid": [],
}


def tex_color(role, comp):
    if role == "X":
        return mix(comp, (255, 255, 255), 0.62)
    if role == "Y":
        return mix(comp, (0x6F, 0xE2, 0xFF), 0.55)
    if role == "Z":
        return mix(comp, (0xFF, 0x6F, 0xD8), 0.45)
    if role == "K":
        return mix(comp, (0, 0, 0), 0.55)
    if role == "N":
        return (0x6F, 0xE2, 0xFF)
    return (0xEE, 0xFF, 0xDC)


# ---------------------------------------------------------------- 形状表
def flow_rows(content):
    """右向基准的逐行游程 (y, [(x0,x1,role)])。

    流动 = 圆头水滴：头部满高、前端 8→6→4 两级收圆（约 2px 渐变）、后端 2px 细尾；
    亮核 C 离最外缘始终留 1px（最外缘是前脸高光 G / 张力边 E，不形成白墙）。
    """
    if content == "empty":
        return [(7, [(5, 10, "P")])]
    if content == "large":
        return [
            (4, [(8, 11, "E")]),
            (5, [(6, 7, "E"), (8, 11, "B"), (12, 12, "E")]),
            (6, [(4, 5, "E"), (6, 12, "B"), (13, 13, "E")]),
            (7, [(2, 2, "r"), (3, 3, "R"), (4, 10, "B"), (11, 12, "C"), (13, 13, "G")]),
            (8, [(2, 2, "r"), (3, 3, "R"), (4, 10, "B"), (11, 12, "C"), (13, 13, "G")]),
            (9, [(4, 5, "E"), (6, 12, "B"), (13, 13, "E")]),
            (10, [(6, 7, "E"), (8, 11, "B"), (12, 12, "E")]),
            (11, [(8, 11, "E")]),
        ]
    if content == "mid":
        return [
            (4, [(7, 9, "E")]),
            (5, [(6, 6, "E"), (7, 9, "B"), (10, 10, "E")]),
            (6, [(5, 5, "E"), (6, 10, "B"), (11, 11, "E")]),
            (7, [(4, 4, "r"), (5, 5, "R"), (6, 8, "B"), (9, 10, "C"), (11, 11, "G")]),
            (8, [(4, 4, "r"), (5, 5, "R"), (6, 8, "B"), (9, 10, "C"), (11, 11, "G")]),
            (9, [(5, 5, "E"), (6, 10, "B"), (11, 11, "E")]),
            (10, [(6, 6, "E"), (7, 9, "B"), (10, 10, "E")]),
            (11, [(7, 9, "E")]),
        ]
    return [  # small 8×6：x=4..11，y=5..10；6→4→2 三级收圆，前端只剩 2px 尖鼻
        (5,  [(7, 9, "E")]),
        (6,  [(6, 6, "E"), (7, 9, "B"), (10, 10, "E")]),
        (7,  [(4, 4, "r"), (5, 5, "R"), (6, 8, "B"), (9, 10, "C"), (11, 11, "G")]),
        (8,  [(4, 4, "r"), (5, 5, "R"), (6, 8, "B"), (9, 10, "C"), (11, 11, "G")]),
        (9,  [(6, 6, "E"), (7, 9, "B"), (10, 10, "E")]),
        (10, [(7, 9, "E")]),
    ]


def blocked_rows(content):
    """右向基准的受阻形状。

    少 / 中 / 空：冻结改前外观（与旧 flow 逐像素相同），流动形状改动不再带着它们变。
    多：12×7（flow 的 8 行压扁 1px）——前脸 6px 竖平口贴 6px 中性墙 W（x=14, y=5..10），
        x=13 前脸与墙齐平、中间不留空；后部圆弧鼓起（x=2 三行 / x=3 五行 / x=4 起满高），
        L 只作后缘尾唇（只在 x=2）；撤掉 C/G，压力只由前脸一圈 E 表达。
    """
    if content == "empty":
        return [(7, [(5, 10, "P")])]
    if content == "small":
        return [
            (5, [(8, 11, "E")]),
            (6, [(6, 6, "E"), (7, 10, "B"), (11, 11, "G")]),
            (7, [(4, 4, "r"), (5, 5, "R"), (6, 8, "B"), (9, 10, "C"), (11, 11, "G")]),
            (8, [(4, 4, "r"), (5, 5, "R"), (6, 8, "B"), (9, 10, "C"), (11, 11, "G")]),
            (9, [(6, 6, "E"), (7, 10, "B"), (11, 11, "G")]),
            (10, [(8, 11, "E")]),
        ]
    if content == "mid":
        return [
            (4, [(8, 10, "E")]),
            (5, [(6, 7, "E"), (8, 10, "B"), (11, 11, "E")]),
            (6, [(5, 5, "E"), (6, 10, "B"), (11, 11, "E")]),
            (7, [(4, 4, "r"), (5, 5, "R"), (6, 8, "B"), (9, 10, "C"), (11, 11, "G")]),
            (8, [(4, 4, "r"), (5, 5, "R"), (6, 8, "B"), (9, 10, "C"), (11, 11, "G")]),
            (9, [(5, 5, "E"), (6, 10, "B"), (11, 11, "E")]),
            (10, [(6, 7, "E"), (8, 10, "B"), (11, 11, "E")]),
            (11, [(8, 10, "E")]),
        ]
    return [  # large 12×7：前脸 x=13 六行竖平口与 x=14 的 W 六行严丝合缝；后缘 x=2 只作 L 尾唇
        (4, [(4, 12, "E")]),
        (5, [(3, 3, "E"), (4, 12, "B"), (13, 13, "E"), (14, 14, "W")]),
        (6, [(2, 2, "L"), (3, 12, "B"), (13, 13, "E"), (14, 14, "W")]),
        (7, [(2, 2, "L"), (3, 12, "B"), (13, 13, "E"), (14, 14, "W")]),
        (8, [(2, 2, "L"), (3, 12, "B"), (13, 13, "E"), (14, 14, "W")]),
        (9, [(3, 3, "E"), (4, 12, "B"), (13, 13, "E"), (14, 14, "W")]),
        (10, [(4, 13, "E"), (14, 14, "W")]),
    ]


def blank():
    return [["." for _ in range(16)] for _ in range(16)]


def put(g, x0, x1, y, role):
    for x in range(x0, x1 + 1):
        g[y][x] = role


def grid_from(rows):
    g = blank()
    for y, spans in rows:
        for x0, x1, r in spans:
            put(g, x0, x1, y, r)
    return g


def grid_state(state, content="large"):
    """流动 / 受阻按含量给形状；split/merge/collect 只在「多」档定义，其它档退回 flow。"""
    if state == "flow":
        return grid_from(flow_rows(content)), content == "large"
    if state == "blocked":
        return grid_from(blocked_rows(content)), content == "large"
    if content != "large":
        return grid_from(flow_rows(content)), False
    g = blank()
    if state == "split":
        rows = [
            (3, [(9, 12, "E")]),
            (4, [(4, 8, "E"), (9, 10, "B"), (11, 12, "C"), (13, 13, "G")]),
            (5, [(2, 2, "r"), (3, 3, "R"), (4, 6, "E"), (7, 10, "B"), (11, 12, "C"), (13, 13, "G")]),
            (6, [(7, 13, "E")]),
            (7, []),
            (8, []),
            (9, [(7, 13, "E")]),
            (10, [(2, 2, "r"), (3, 3, "R"), (4, 6, "E"), (7, 10, "B"), (11, 12, "C"), (13, 13, "G")]),
            (11, [(4, 8, "E"), (9, 10, "B"), (11, 12, "C"), (13, 13, "G")]),
            (12, [(9, 12, "E")]),
        ]
        g = grid_from(rows)
    elif state == "merge":
        rows = [
            (4, [(2, 5, "E")]),
            (5, [(2, 2, "r"), (3, 3, "R"), (4, 5, "B"), (6, 10, "E")]),
            (6, [(4, 7, "E"), (8, 10, "B"), (11, 13, "E")]),
            (7, [(8, 8, "E"), (9, 9, "S"), (10, 10, "B"), (11, 12, "C"), (13, 13, "G")]),
            (8, [(8, 8, "E"), (9, 9, "S"), (10, 10, "B"), (11, 12, "C"), (13, 13, "G")]),
            (9, [(4, 7, "E"), (8, 10, "B"), (11, 13, "E")]),
            (10, [(2, 2, "r"), (3, 3, "R"), (4, 5, "B"), (6, 10, "E")]),
            (11, [(2, 5, "E")]),
        ]
        g = grid_from(rows)
    elif state == "collect":
        rows = [
            (4, [(8, 11, "E")]),
            (5, [(6, 7, "E"), (8, 11, "B"), (12, 12, "E")]),
            (6, [(4, 5, "E"), (6, 11, "B"), (12, 12, "E")]),
            (7, [(2, 2, "r"), (3, 3, "R"), (4, 9, "B"), (10, 11, "C"), (12, 12, "E")]),
            (8, [(2, 2, "r"), (3, 3, "R"), (4, 9, "B"), (10, 11, "C"), (12, 12, "E")]),
            (9, [(4, 5, "E"), (6, 11, "B"), (12, 12, "E")]),
            (10, [(6, 7, "E"), (8, 11, "B"), (12, 12, "E")]),
            (11, [(8, 11, "E")]),
        ]
        g = grid_from(rows)
    return g, True


# ---------------------------------------------------------------- 变形
def rot(g, k):
    for _ in range(k % 4):
        ng = blank()
        for y in range(16):
            for x in range(16):
                ng[x][15 - y] = g[y][x]
        g = ng
    return g


def add_textures(g, comp, content):
    if content == "empty":
        return g
    x0, y0, w, h = CBBOX[content]
    for (tx, ty, role) in TEX.get(comp, []):
        x = x0 + int(round((tx - 2) * (w - 1) / 11.0))
        y = y0 + int(round((ty - 4) * (h - 1) / 7.0))
        if 0 <= x < 16 and 0 <= y < 16 and g[y][x] == "B":
            g[y][x] = role
        elif role == "F" and 0 <= x < 16 and 0 <= y < 16 and g[y][x] == "E":
            g[y][x] = "F"
    return g


def tail_fade(g, n):
    """n = 尾部渐隐列数 2..4（只改色/alpha，不改像素数）。"""
    if n <= 2:
        return g
    for y in range(16):
        for x in range(4, 2 + n):
            if g[y][x] == "B":
                g[y][x] = "u"
    return g


def squeeze(g, dtop, dbot, bbox):
    if dtop == 0 and dbot == 0:
        return g
    y0, y1 = bbox[1], bbox[1] + bbox[3] - 1
    for y, d in ((y0, dtop), (y1, dbot)):
        if d == 0:
            continue
        row = [(x, g[y][x]) for x in range(16) if g[y][x] != "."]
        for x, _ in row:
            g[y][x] = "."
        for x, r in row:
            nx = min(15, max(0, x + d))
            g[y][nx] = r
    return g


SHEEN = [(13, 9), (12, 10), (11, 11), (10, 11), (9, 11), (8, 11), (7, 10), (6, 10), (5, 9), (4, 9), (3, 8), (2, 8)]
FRAMES = {
    "tail": [2, 3, 3, 4, 4, 4, 4, 4, 4, 4, 3, 2],
    # 行 4 / 行 11 各自 ±1px（只动极值行内侧的 4px 弧，面积与 bbox 不变）
    "top": [0, 1, 1, 0, -1, -1, 0, 1, 1, 0, -1, 0],
    "bot": [0, -1, 0, -1, 0, 1, 1, 1, 0, 0, -1, -1],
}
SHEEN_A = [255, 255, 255, 255, 255, 255, 255, 255, 235, 200, 170, 130]


def apply_sheen(g, f, comp, content):
    if content != "large":
        return g
    idx = min(len(SHEEN) - 1, int(round(f * (len(SHEEN) - 1) / 11.0)))
    x, y = SHEEN[idx]
    if g[y][x] in ("E", "B", "r", "R", "u"):
        g[y][x] = "s"
    if idx > 0:
        xp, yp = SHEEN[idx - 1]
        if g[yp][xp] in ("E", "B", "r", "R", "u"):
            g[yp][xp] = "S2"
    return g


def top_foam(g, comp, content):
    """酸的白沫冠：旋转后贴世界上缘（泡沫上浮），只换边像素颜色。"""
    if comp != "acid" or content == "empty":
        return g
    for y in range(16):
        xs = [x for x in range(16) if g[y][x] == "E"]
        if xs:
            for x in xs[:3]:
                g[y][x] = "F"
            break
    return g


def apply_temp(g, temp, content, dirn):
    """温度在旋转后按世界方向施加：霜点在世上缘，热缺口在运动前端。"""
    if content == "empty" or temp == "normal":
        return g
    if temp == "cold":
        for y in range(16):
            xs = [x for x in range(16) if g[y][x] == "E"]
            if xs:
                for x in xs[:3]:
                    g[y][x] = "I"
                break
        return g
    # hot：咬掉紧贴前脸高光两侧的 2 个边像素
    gp = [(x, y) for y in range(16) for x in range(16) if g[y][x] == "G"]
    if gp:
        dx, dy = DIRV[dirn]
        bites = []
        for px, py in gp:
            for sgn in (1, -1):
                qx, qy = (px + sgn * dy, py + sgn * dx) if dx == 0 else (px + sgn * dy, py + sgn * dx)
                # 垂直轴 = (dy, dx) 的 90° 旋转
                qx, qy = px + sgn * (-dy), py + sgn * (-dx)
                if 0 <= qx < 16 and 0 <= qy < 16 and g[qy][qx] == "E":
                    bites.append((qx, qy))
        for (qx, qy) in sorted(set(bites), key=lambda p: (p[1], p[0]))[:2]:
            g[qy][qx] = "."
    # hot：顶行 2 个内部孔（该行 B 至少 4px 才挖，避免毁掉薄瓣）
    for y in range(16):
        xs = [x for x in range(16) if g[y][x] == "B"]
        if len(xs) >= 4:
            i1 = xs[len(xs) // 3]
            i2 = xs[2 * len(xs) // 3]
            g[y][i1] = "."
            g[y][i2] = "."
            break
        if xs:
            break
    return g


# ---------------------------------------------------------------- 绘制
def bbox_of(g, skip=("W", "~", "M")):
    xs, ys = [], []
    for y in range(16):
        for x in range(16):
            if g[y][x] != "." and g[y][x] not in skip:
                xs.append(x)
                ys.append(y)
    if not xs:
        return None
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    return (x0, y0, x1 - x0 + 1, y1 - y0 + 1)


def area_of(g, skip=()):
    n = 0
    for y in range(16):
        for x in range(16):
            if g[y][x] != "." and g[y][x] not in skip:
                n += 1
    return n


def role_rgba(role, x, y, comp, temp, bbox):
    e = edge_color(comp, temp)
    if role == "E":
        return e + (255,)
    if role == "B":
        return body_color(comp, y, bbox) + (208,)
    if role == "C":
        return mix(comp, (255, 255, 255), 0.62) + (255,)
    if role == "G":
        return mix(comp, (255, 255, 255), 0.72) + (255,)
    if role == "r":
        return mix(e, DARK, 0.75) + (130,)
    if role == "R":
        return mix(comp, DARK, 0.70) + (130,)
    if role == "u":
        return mix(comp, DARK, 0.35) + (170,)
    if role == "s":
        return mix(e, (255, 255, 255), 0.45) + (200,)
    if role == "S2":
        return mix(e, (255, 255, 255), 0.30) + (120,)
    if role == "P":
        return mix(e, BG, 0.30) + (200,)
    if role == "W":
        return (0xC9, 0xC2, 0xB8, 255)
    if role == "S":
        return mix(comp, (255, 255, 255), 0.60) + (255,)
    if role == "~":
        return mix(comp, (255, 255, 255), 0.55) + (255,)
    if role in ("M", "L"):
        return e + (255,)
    if role == "I":
        return (0xE8, 0xF6, 0xFF, 255)
    if role in ("X", "Y", "Z", "K", "N", "F"):
        return tex_color(role, comp) + (255,)
    return (255, 0, 255, 255)


def _bottle(x, y):
    """12×12 小瓶剪影：瓶颈 x=7..8 / y=2..4，瓶身 x=4..11 / y=5..13。"""
    if 7 <= x <= 8 and 2 <= y <= 4:
        return True
    if 4 <= x <= 11 and 5 <= y <= 13:
        return True
    return False


def icon_layer():
    lay = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    px = lay.load()
    for y in range(16):
        for x in range(16):
            if not _bottle(x, y):
                continue
            edge = not all(_bottle(x + dx, y + dy)
                           for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            r, gg, b = ICON_EDGE if edge else ICON
            px[x, y] = (r, gg, b, ICON_A)
    for x in range(6, 10):
        px[x, 6] = ICON_EDGE + (120,)
    return lay


ICON_L = icon_layer()


def build_grid(comp, content, state, dirn, temp, frame=None):
    g, is_large = grid_state(state, content)
    bbox = bbox_of(g)
    if frame is not None and state == "flow":
        # 小档 8×6 的极值行 y=5/10 只有 4px 宽（x=8..11）；squeeze 按 bbox 取极值行，
        # ±1px 会把前/后端推出 bbox（bbox 8→9、中心 7.5→8.0）。小档不做挤压（12 帧静态重复）。
        if content != "small":
            g = squeeze(g, FRAMES["top"][frame], FRAMES["bot"][frame], bbox)
        g = tail_fade(g, FRAMES["tail"][frame])
        g = add_textures(g, comp, content)
        g = apply_sheen(g, frame, comp, content)
    elif state == "flow":
        g = add_textures(g, comp, content)
    else:
        bbox = bbox_of(g) or CBBOX[content]
    g = rot(g, dirn and {"right": 0, "down": 1, "left": 2, "up": 3}[dirn] or 0)
    g = top_foam(g, comp, content)
    g = apply_temp(g, temp, content, dirn)
    return g


def range_dots(comp, n):
    """底部 y=14，x=4/6/8。亮 n 个（3/2/1），从左往右灭。"""
    out = []
    for i, x in enumerate((4, 6, 8)):
        lit = i >= 3 - n
        if lit:
            c = mix(comp, (255, 255, 255), 0.75)
            out.append((x, 14, c + (255,)))
        else:
            c = mix(comp, BG, 0.75)
            out.append((x, 14, c + (200,)))
    return out


def render(comp="water", temp="normal", content="large", state="flow", dirn="right",
           rng=3, frame=None, bg=BG, icon=True, ring_alpha=None):
    g = build_grid(comp, content, state, dirn, temp, frame)
    rgb = COMPS[comp]
    bbox = bbox_of(g) or CBBOX[content]
    base = Image.new("RGBA", (16, 16), bg + (255,))
    if icon:
        base = Image.alpha_composite(base, ICON_L)
    lay = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    px = lay.load()
    for y in range(16):
        for x in range(16):
            r = g[y][x]
            if r != ".":
                px[x, y] = role_rgba(r, x, y, rgb, temp, bbox)
    for (x, y, c) in range_dots(rgb, rng):
        px[x, y] = c
    out = Image.alpha_composite(base, lay)
    if ring_alpha:
        a = ring_alpha
        col = mix(rgb, (255, 255, 255), 0.35)
        ov = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        o = ov.load()
        for i in range(16):
            for (x, y) in ((i, 0), (i, 15), (0, i), (15, i)):
                o[x, y] = col + (a,)
        out = Image.alpha_composite(out, ov)
    return out, lay, bbox, g


# ---------------------------------------------------------------- quads
def runs_h(img):
    a = np.asarray(img)
    n = 0
    for y in range(a.shape[0]):
        prev = None
        for x in range(a.shape[1]):
            p = tuple(int(v) for v in a[y, x])
            if p[3] == 0:
                prev = None
                continue
            if p != prev:
                n += 1
                prev = p
    return n


def runs_v(img):
    a = np.asarray(img)
    n = 0
    for x in range(a.shape[1]):
        prev = None
        for y in range(a.shape[0]):
            p = tuple(int(v) for v in a[y, x])
            if p[3] == 0:
                prev = None
                continue
            if p != prev:
                n += 1
                prev = p
    return n


def cover_rects(img):
    """贪心最大同色矩形覆盖：即「一个顶点矩形」实现所需的 quad 上界。"""
    a = np.asarray(img).astype(np.int64)
    packed = (a[..., 0] << 24) | (a[..., 1] << 16) | (a[..., 2] << 8) | a[..., 3]
    rem = a[..., 3] > 0
    n = 0
    H, W = rem.shape
    while rem.any():
        best = None
        for y0 in range(H):
            if not rem[y0].any():
                continue
            for x0 in range(W):
                if not rem[y0, x0]:
                    continue
                ref = packed[y0, x0]
                x1 = x0
                while x1 + 1 < W and rem[y0, x1 + 1] and packed[y0, x1 + 1] == ref:
                    x1 += 1
                y1 = y0
                while y1 + 1 < H and np.all(rem[y1 + 1, x0:x1 + 1]) and np.all(packed[y1 + 1, x0:x1 + 1] == ref):
                    y1 += 1
                area = (x1 - x0 + 1) * (y1 - y0 + 1)
                if best is None or area > best[0]:
                    best = (area, x0, y0, x1, y1)
        _, x0, y0, x1, y1 = best
        rem[y0:y1 + 1, x0:x1 + 1] = False
        n += 1
    return n


def quads(lay):
    return cover_rects(lay)


# ---------------------------------------------------------------- 字体/拼图
_FC = {}


def F(size, bold=False):
    key = (size, bold)
    if key not in _FC:
        for p in ((FONT_B, FONT_R) if bold else (FONT_R,)):
            try:
                _FC[key] = ImageFont.truetype(p, size)
                break
            except Exception:
                continue
        else:
            _FC[key] = ImageFont.load_default()
    return _FC[key]


def up(img, s):
    return img.resize((img.width * s, img.height * s), Image.NEAREST)


def gray(img):
    return img.convert("RGB").convert("L").convert("RGBA")


DEUTAN = np.array([[0.367, 0.861, -0.228], [0.280, 0.673, 0.047], [-0.012, 0.043, 0.969]])


def deutan(img):
    a = np.asarray(img.convert("RGB")).astype(float)
    lin = np.where(a / 255 <= 0.04045, a / 255 / 12.92, ((a / 255 + 0.055) / 1.055) ** 2.4)
    flat = lin.reshape(-1, 3)
    out = np.clip((DEUTAN @ flat.T).T, 0, 1)
    srgb = np.where(out <= 0.0031308, out * 12.92, 1.055 * out ** (1 / 2.4) - 0.055) * 255
    return Image.fromarray(np.round(srgb).reshape(a.shape).astype(np.uint8), "RGB").convert("RGBA")


class Sheet:
    """4x 接触表：标题 / 每行 4x cells + 每格标签 / 底部 1x 原尺寸条。"""

    def __init__(self, title, subtitle="", scale=4, cell=16, gap=8, pad=18, rowlab=110):
        self.title, self.subtitle = title, subtitle
        self.scale, self.cell, self.gap, self.pad, self.rowlab = scale, cell, gap, pad, rowlab
        self.rows = []

    def add(self, label, tiles, labels=None, color=INK2):
        labels = labels or [""] * len(tiles)
        self.rows.append((label, list(zip(tiles, labels)), color))
        return self

    def build(self):
        cw = self.cell * self.scale
        n = max(len(r[1]) for r in self.rows)
        grid_w = self.rowlab + n * cw + (n - 1) * self.gap
        probe = ImageDraw.Draw(Image.new("RGBA", (4, 4)))
        tw = max(probe.textlength(self.title, font=F(20, True)),
                 probe.textlength(self.subtitle, font=F(13)))
        W = int(self.pad * 2 + max(grid_w, tw + 4))
        head = 34 + 22
        rowh = cw + 18 + 12
        H = self.pad + head + len(self.rows) * rowh + 26 + 52 + self.pad
        im = Image.new("RGBA", (W, H), BG + (255,))
        d = ImageDraw.Draw(im)
        d.text((self.pad, self.pad), self.title, font=F(20, True), fill=INK + (255,))
        d.text((self.pad, self.pad + 28), self.subtitle, font=F(13), fill=INK2 + (255,))
        y = self.pad + head
        for label, cells, color in self.rows:
            d.text((self.pad, y + 10), label, font=F(14, True), fill=color + (255,))
            x = self.pad + self.rowlab
            for tile, lab in cells:
                im.alpha_composite(up(tile, self.scale), (x, y))
                if lab:
                    d.text((x + cw / 2, y + cw + 3), lab, font=F(12), fill=INK2 + (255,), anchor="ma")
                x += cw + self.gap
            y += rowh
        # 1x 条（标题在上，条在下，互不压）
        d.line([(self.pad, y + 6), (W - self.pad, y + 6)], fill=GRID + (255,))
        d.text((self.pad, y + 10), "1x 原尺寸（NEAREST 放大前）", font=F(12), fill=INK2 + (255,))
        strip = strip_1x([c[0] for c in self.rows[0][1]])
        im.alpha_composite(strip, (self.pad, y + 28))
        return im


def strip_1x(tiles, gap=2, bg=BG):
    w = len(tiles) * 16 + (len(tiles) - 1) * gap
    im = Image.new("RGBA", (w, 16), bg + (255,))
    x = 0
    for t in tiles:
        im.alpha_composite(t, (x, 0))
        x += 16 + gap
    return im


def save(im, name, onex=None):
    im.convert("RGB").save(PNG / f"{name}-4x.png")
    if onex is not None:
        onex.convert("RGB").save(PNG / f"{name}-1x.png")


# ---------------------------------------------------------------- 图版
def cell(**kw):
    kw.setdefault("icon", False)
    return render(**kw)[0]


def make_comps():
    tiles = [cell(comp=c) for c in COMPS]
    s = Sheet("I1 成分 · 6 色 + 内部纹理 + 灰度行",
              "右向 / 多 / 常温 / 流动 / 射程3；纹理 1–3px 只落在体内，不压边、不压核。")
    s.add("色相", tiles, [CN[c] for c in COMPS])
    s.add("灰度", [gray(t) for t in tiles], [CN[c] for c in COMPS])
    s.add("deutan", [deutan(t) for t in tiles], [CN[c] for c in COMPS])
    im = s.build()
    save(im, "comps", strip_1x(tiles))


def make_temp():
    comps = ("water", "oil", "acid")
    s = Sheet("I2 温度 · 边缘带颜色 + 霜点/蒸汽孔",
              "三档包围盒与面积逐像素相同：只换 1px 边色，cold 加 3 个霜点，hot 咬 2 边 + 挖 2 孔。")
    for comp in comps:
        row = [cell(comp=comp, temp=t) for t in TEMPS]
        s.add(CN[comp], row, [TCN[t] for t in TEMPS])
    im = s.build()
    save(im, "temp", strip_1x([cell(comp="water", temp=t) for t in TEMPS]))


def make_content():
    s = Sheet("I3 含量 · 包围盒体积（空 6×1 / 少 8×6 / 中 8×8 / 多 12×8）",
              "高光：少 4px / 中·多 2px；边恒 1px；「少」= 更小更紧的珠子，不是更散。")
    tiles = [cell(content=c) for c in CONTENT]
    s.add("水/常温", tiles, [f"{CCN[c]} {CBBOX[c][2]}×{CBBOX[c][3]}" for c in CONTENT])
    im = s.build()
    save(im, "content", strip_1x(tiles))


def make_range():
    s = Sheet("I4 射程 · 底部 3 个 1px 点（y=14, x=4/6/8）",
              "亮 3/2/1 个，从左往右灭；点越少越接近走不动。点在外框，不随含量/温度变化。")
    rows = []
    for c in ("small", "mid", "large"):
        rows.append([cell(content=c, rng=n) for n in (3, 2, 1)])
    for i, c in enumerate(("small", "mid", "large")):
        s.add(f"含量 {CCN[c]}", rows[i], ["射程3", "射程2", "射程1"])
    im = s.build()
    save(im, "range", strip_1x(rows[2]))


def make_dirs():
    s = Sheet("I6 方向 · 钝口 + 前脸 2px 高光 + 2×2 亮核偏前 + 后端 2px 渐隐",
              "四向 = 同一掩膜整数旋转，bbox 中心恒 (7.5,7.5)；剪影内无 1px 尖角。")
    tiles = [cell(dirn=d) for d in DIRS]
    s.add("四向", tiles, [DCN[d] for d in DIRS])
    lpath = [cell(dirn="right"), cell(dirn="right"), cell(dirn="down"), cell(dirn="down")]
    s.add("L 型路径（4 格）", lpath, ["右", "右", "下", "下"])
    im = s.build()
    save(im, "dirs", strip_1x(lpath))


def make_states():
    s = Sheet("I5 状态 · 墙 / 两行空 / 亮缝+V / 连续流",
              "split 中间是 2 整行纯背景；merge 是 1px 亮缝 + 后缘 V。两者不得互换。")
    tiles = [cell(state=st) for st in STATES]
    s.add("五态（水/多/常温/右）", tiles, [SCN[st] for st in STATES])
    im = s.build()
    save(im, "states", strip_1x(tiles))


def make_loop():
    s = Sheet("动效 · 12 帧 × 3 温度（1.0s / 12fps，83ms/帧）",
              "包络锁 12 帧：尾渐隐 2→4→2、1px 光扫、行 4/行 11 ±1px 呼吸（面积守恒）；"
              "呼吸另走浮点相位 k=0.7/1.0/1.4。")
    for t in ("normal", "cold", "hot"):
        s.add(TCN[t], [cell(comp="water", temp=t, frame=i) for i in range(12)],
              [f"F{i}" for i in range(12)])
    ring = [cell(ring_alpha=89), cell(ring_alpha=45), cell(ring_alpha=None)]
    s.add("进格 2 帧边框", ring, ["t+0 α89", "t+83 α45", "t+166 移除"])
    im = s.build()
    save(im, "loop", strip_1x([cell(comp="water", frame=i) for i in range(12)]))


def make_occlusion():
    bgs = [("深底 #0B0D10", (0x0B, 0x0D, 0x10)),
           ("浅底 #E8E2D6", (0xE8, 0xE2, 0xD6)),
           ("棋盘 #2A2622/#4A423A", None)]
    s = Sheet("遮挡 · 深 / 浅 / 棋盘三种底 + 图标占位",
              "本体 alpha 208 让 12×12 图标透出；1px 边 alpha 255 兜底轮廓。")
    for name, col in bgs:
        tiles = []
        for comp in ("water", "acid", "energy"):
            if col is not None:
                tiles.append(cell(comp=comp, bg=col, icon=True))
            else:
                base = Image.new("RGBA", (16, 16), (0x2A, 0x26, 0x22, 255))
                for by in range(0, 16, 8):
                    for bx in range(0, 16, 8):
                        if (bx // 8 + by // 8) % 2 == 0:
                            for yy in range(by, by + 8):
                                for xx in range(bx, bx + 8):
                                    base.putpixel((xx, yy), (0x4A, 0x42, 0x3A, 255))
                tiles.append(render(comp=comp, bg=(0, 0, 0), icon=True)[0])
                # 重画在棋盘上
                _, lay, _, _ = render(comp=comp, bg=(0, 0, 0), icon=True)
                tiles[-1] = Image.alpha_composite(base, lay)
        s.add(name, tiles, [CN[c] for c in ("water", "acid", "energy")])
    im = s.build()
    save(im, "occlusion", strip_1x([cell(comp=c, bg=bgs[1][1], icon=True) for c in ("water", "acid", "energy")]))


def make_hero():
    COLS, ROWS = 12, 6
    field = Image.new("RGBA", (COLS * 16, ROWS * 16), BG + (255,))
    d = ImageDraw.Draw(field)
    for c in range(COLS + 1):
        d.line([(c * 16, 0), (c * 16, ROWS * 16 - 1)], fill=GRID + (255,))
    for r in range(ROWS + 1):
        d.line([(0, r * 16), (COLS * 16 - 1, r * 16)], fill=GRID + (255,))
    sources = [
        (1, 1, dict(comp="water", content="large", temp="normal", dirn="right", rng=3, icon=True)),
        (5, 2, dict(comp="oil", content="mid", temp="hot", dirn="up", rng=2, icon=True)),
        (7, 1, dict(comp="blood", content="small", temp="cold", dirn="left", rng=1, icon=True)),
        (8, 3, dict(comp="acid", content="large", temp="normal", dirn="down", rng=3, icon=True)),
    ]
    for (cx, cy, kw) in sources:
        field.alpha_composite(cell(**kw), (cx * 16, cy * 16))
    for (cx, cy) in ((3, 4), (10, 1), (2, 4)):
        ic = Image.alpha_composite(Image.new("RGBA", (16, 16), BG + (255,)), ICON_L)
        field.alpha_composite(ic, (cx * 16, cy * 16))
    S = 4
    big = up(field, S)
    W = big.width + 40
    H = big.height + 148
    im = Image.new("RGBA", (W, H), BG + (255,))
    im.alpha_composite(big, (20, 20))
    d = ImageDraw.Draw(im)
    for i, (cx, cy, _) in enumerate(sources, 1):
        d.text((20 + cx * 64 + 3, 20 + cy * 64 + 2), str(i), font=F(12, True), fill=INK + (255,))
    d.text((20, big.height + 30), "① 水/多/常温/右/射程3   ② 油/中/热/上/射程2   "
                                  "③ 血/少/冷/左/射程1   ④ 酸/多/常温/下/射程3",
           font=F(14, True), fill=INK + (255,))
    d.text((20, big.height + 54), "12×6 格；四条流源都压在物品图标占位（小瓶剪影 12×12）上："
                                  "本体 alpha 208 让图标透出，1px 边兜底轮廓。", font=F(12), fill=INK2 + (255,))
    d.text((20, big.height + 74), "底部 y=14 三点 = 射程（亮 3/2/1）；射程点是常驻层，任何状态都画在最上面。",
           font=F(12), fill=INK2 + (255,))
    im.convert("RGB").save(PNG / "hero-4x.png")
    field.convert("RGB").save(PNG / "hero-1x.png")


# ---------------------------------------------------------------- tiles / atlas
def make_tiles():
    TILES.mkdir(exist_ok=True, parents=True)
    items = []

    def add(path, group, answer, meta, **kw):
        kw.setdefault("icon", False)
        im, lay, bbox, g = render(**kw)
        im.save(TILES / path)
        items.append({"path": f"tiles/{path}", "group": group, "answer": answer,
                      "meta": meta, "bbox": bbox,
                      "quads": quads(lay), "quads_runs": [runs_h(lay), runs_v(lay)]})

    for c in COMPS:
        add(f"comp-{c}.png", "comp", c, f"{c}/large/normal/flow/right", comp=c)
    for t in TEMPS:
        add(f"temp-{t}.png", "temp", t, f"water/large/{t}/flow/right", temp=t)
    for c in CONTENT:
        add(f"content-{c}.png", "content", c, f"water/{c}/normal/flow/right", content=c)
    for n in (3, 2, 1):
        add(f"range-{n}.png", "range", str(n), f"water/large/normal/flow/right/range{n}", rng=n)
    for dn in DIRS:
        add(f"dir-{dn}.png", "dir", dn, f"water/large/normal/flow/{dn}", dirn=dn)
    for st in STATES:
        add(f"state-{st}.png", "state", st, f"water/large/normal/{st}/right", state=st)

    (TILES / "manifest.json").write_text(json.dumps(items, ensure_ascii=False, indent=1), encoding="utf-8")
    # 盲测分组 manifest（相对 final/blind/）
    BLIND.mkdir(exist_ok=True)
    for grp in ("comp", "temp", "content", "range", "dir", "state"):
        sub = [{"path": "../" + it["path"], "answer": it["answer"], "meta": it["meta"]}
               for it in items if it["group"] == grp]
        (BLIND / f"g-{grp}.json").write_text(json.dumps(sub, ensure_ascii=False, indent=1), encoding="utf-8")
    return items


def make_atlas(items):
    tiles = [Image.open(HERE / it["path"]).convert("RGBA") for it in items]
    per = 16
    rows = (len(tiles) + per - 1) // per
    atlas = Image.new("RGBA", (per * 16, rows * 16), (0, 0, 0, 0))
    idx = {}
    for i, (it, t) in enumerate(zip(items, tiles)):
        x, y = (i % per) * 16, (i // per) * 16
        atlas.paste(t, (x, y))
        idx[it["path"].split("/")[-1].replace(".png", "")] = [x, y]
    atlas.save(HERE / "atlas.png")
    (HERE / "atlas.json").write_text(json.dumps({"key": idx}, ensure_ascii=False, indent=1), encoding="utf-8")


# ---------------------------------------------------------------- dump / 自检
def dump():
    fails = []

    def chk(cond, msg):
        if not cond:
            fails.append(msg)

    print("=== 单源 quads（h=横切游程 / v=纵切游程 / q=实现取优轴，含底部 3 点）===")
    for st in STATES:
        for dn in DIRS:
            _, lay, bbox, g = render(state=st, dirn=dn)
            h, v = runs_h(lay), runs_v(lay)
            print(f"{st:8s} {dn:5s} q={quads(lay):2d} (h={h:2d} v={v:2d}) area={area_of(g):3d} bbox={bbox}")
    print("\n=== 含量 × 温度（水/流动/右/射程3）===")
    for c in CONTENT:
        for t in TEMPS:
            im, lay, bbox, g = render(content=c, temp=t)
            print(f"{c:6s} {t:6s} area={area_of(g):3d} body_bbox={bbox} q={quads(lay)}")
    print("\n=== 温度包围盒逐像素一致（同含量）===")
    for c in CONTENT:
        bs = [bbox_of(build_grid("water", c, "flow", "right", t)) for t in TEMPS]
        chk(len(set(bs)) == 1, f"温度改 bbox: {c} {bs}")
        print(f"{c:6s} {bs[0]} {'OK' if len(set(bs)) == 1 else 'FAIL'}")
    print("\n=== 含量 bbox ===")
    for c in CONTENT:
        _, _, bbox, _ = render(content=c)
        exp = CBBOX[c]
        print(f"{c:6s} {bbox} exp={exp} {'OK' if bbox == exp else 'FAIL'}")
        chk(bbox == exp, f"含量 bbox 不符 {c}: {bbox} != {exp}")
    print("\n=== 四向 bbox 中心 / 前后端宽度 ===")
    for dn in DIRS:
        _, lay, bbox, g = render(dirn=dn)
        cx, cy = bbox[0] + (bbox[2] - 1) / 2, bbox[1] + (bbox[3] - 1) / 2
        dx, dy = DIRV[dn]
        front = sum(1 for y in range(16) for x in range(16)
                    if g[y][x] != "." and (x - bbox[0]) * dx + (y - bbox[1]) * dy
                    == max((xx - bbox[0]) * dx + (yy - bbox[1]) * dy for yy in range(16) for xx in range(16) if g[yy][xx] != "."))
        back_line = min((xx - bbox[0]) * dx + (yy - bbox[1]) * dy for yy in range(16) for xx in range(16) if g[yy][xx] != ".")
        back = sum(1 for y in range(16) for x in range(16)
                   if g[y][x] != "." and (x - bbox[0]) * dx + (y - bbox[1]) * dy == back_line)
        print(f"{dn:5s} bbox={bbox} center=({cx},{cy}) front={front}px back={back}px")
        chk((cx, cy) == (7.5, 7.5), f"方向中心偏了 {dn}: {(cx, cy)}")
        chk(front >= 2 * back and back >= 2, f"方向宽窄比不足 {dn}: {front}/{back}")
    print("\n=== 面积 / quads 逐帧（水/多/常温/右）===")
    prev = None
    for i in range(12):
        _, lay, bbox, g = render(frame=i)
        a = area_of(g)
        d = 0 if prev is None else int(np.sum(np.array(prev[1]) != np.array(lay)))
        dpos = 0 if prev is None else (int(np.sum(np.array(prev[1])[..., 3] > 0)
                                            + np.sum(np.array(lay)[..., 3] > 0))
                                       - 2 * int(np.sum((np.array(prev[1])[..., 3] > 0)
                                                        & (np.array(lay)[..., 3] > 0))))
        print(f"F{i:<2d} area={a:3d} bbox={bbox} q={quads(lay):2d} diff={d} dpos={dpos}")
        chk(prev is None or a == prev[0], f"呼吸改了面积 F{i}: {a} != {prev[0] if prev else a}")
        chk(prev is None or (d >= 2 and dpos >= 2), f"相邻帧变化 <2px F{i}")
        prev = (a, lay)
    print("\n=== 状态差量（vs flow，像素数）===")
    _, flow_lay, _, flow_g = render(state="flow")
    for st in STATES:
        _, lay, bbox, g = render(state=st)
        diff = int(np.sum(np.array(flow_lay) != np.array(lay)))
        print(f"{st:8s} area={area_of(g):3d} bbox={bbox} diff={diff} q={quads(lay)}")
    # split/merge 一空一亮
    _, _, _, sp = render(state="split")
    _, _, _, mg = render(state="merge")
    empty_mid = sum(1 for y in (7, 8) for x in range(2, 14) if sp[y][x] == ".")
    seam = sum(1 for y in (7, 8) for x in range(16) if mg[y][x] == "S")
    print(f"\nsplit 中间空 px={empty_mid} (期望 24)  merge 亮缝 px={seam} (期望 2)")
    chk(empty_mid == 24, f"split 中间不是 2 整行背景: {empty_mid}")
    chk(seam == 2, f"merge 没有 1px 亮缝: {seam}")
    print("\n=== 射程 ===")
    for n in (3, 2, 1):
        _, lay, _, _ = render(rng=n)
        lit = [x for x in (4, 6, 8) if tuple(int(v) for v in np.asarray(lay)[14, x])[3] == 255]
        print(f"range={n} 亮点 x={lit}")
        chk(len(lit) == n, f"射程亮点数错 {n}: {lit}")
    print("\n=== 判定 ===")
    if fails:
        for f in fails:
            print("FAIL", f)
        return 1
    print("ALL OK")
    return 0


# ---------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dump", action="store_true")
    args = ap.parse_args()
    if args.dump:
        return dump()
    PNG.mkdir(parents=True, exist_ok=True)
    make_comps()
    make_temp()
    make_content()
    make_range()
    make_dirs()
    make_states()
    make_loop()
    make_occlusion()
    make_hero()
    items = make_tiles()
    make_atlas(items)
    print(f"png={len(list(PNG.glob('*.png')))} tiles={len(items)} atlas=ok")
    return 0


if __name__ == "__main__":
    sys.exit(main())

