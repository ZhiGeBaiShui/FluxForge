#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
channels.md 的数值依据（信息设计研究；不碰 src/ 与 assets/）。

四件事：
1. WCAG 2.x 相对亮度 / 对比度（背景 #332B28 与 #1B1715）。
2. 二色觉模拟：Machado et al. 2009（severity=1.0）的线性 RGB 矩阵，
   deutan / protan 各一套；输出模拟后 sRGB、CIE L*、成对 ΔE76。
3. 候选（朴素语义色）与推荐（保留色相、加 1px 常量轮廓）两套调色板的分组/塌缩检测。
4. 生成 contrast.png：16px 色块 8x NEAREST 放大，原始 / deutan / protan 三行对照。

公式
----
WCAG 相对亮度:  L = 0.2126 R' + 0.7152 G' + 0.0722 B'   (R' 为 sRGB 线性化)
对比度:         CR = (L_hi + 0.05) / (L_lo + 0.05)
二色觉:         [R'G'B']_sim = clip(M_kind · [R'G'B']_lineal)，再 gamma 编码
                M_deutan / M_protan 取自 Machado, Oliveira & Fernandes (2009) 附表，severity=1.0。

运行：
  C:\\Users\\Administrator\\.dsh\\dsh-runtimes\\dsh-primary-runtime\\dependencies\\python\\python.exe ^
    docs\\flow-visual\\research\\contrast.py
输出：stdout 表格（供 channels.md 引用）+ 同目录 contrast.png
"""

import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PNG = os.path.join(HERE, "contrast.png")

BG = {"#332B28": (0x33, 0x2B, 0x28), "#1B1715": (0x1B, 0x17, 0x15)}

# ---------------------------------------------------------------- 颜色基础
def hx(s):
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


def to_hex(rgb):
    return "#%02X%02X%02X" % tuple(max(0, min(255, int(round(c)))) for c in rgb)


def s2l(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def l2s(c):
    c = max(0.0, min(1.0, c))
    return 12.92 * c if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055


def lin(rgb):
    return tuple(s2l(c) for c in rgb)


def rel_lum(rgb):
    r, g, b = lin(rgb)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a, b):
    la, lb = rel_lum(a), rel_lum(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


# CIE Lab (D65)
_WP = (0.95047, 1.0, 1.08883)


def _f(t):
    return t ** (1 / 3) if t > (6 / 29) ** 3 else t / (3 * (6 / 29) ** 2) + 4 / 29


def lab(rgb):
    r, g, b = lin(rgb)
    x = (0.4124564 * r + 0.3575761 * g + 0.1804375 * b) / _WP[0]
    y = (0.2126729 * r + 0.7151522 * g + 0.0721750 * b) / _WP[1]
    z = (0.0193339 * r + 0.1191920 * g + 0.9503041 * b) / _WP[2]
    fx, fy, fz = _f(x), _f(y), _f(z)
    return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz))


def de76(a, b):
    la, lb = lab(a), lab(b)
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(la, lb)))


# Machado et al. 2009（severity 1.0），作用在**线性** RGB 上，再 clip、再 gamma 编码
MACHADO = {
    "deutan": ((0.367322, 0.860646, -0.227968),
               (0.280085, 0.672501, 0.047413),
               (-0.011820, 0.042940, 0.968881)),
    "protan": ((0.152286, 1.052583, -0.204868),
               (0.114503, 0.786281, 0.099216),
               (-0.003882, -0.048116, 1.051998)),
}


def sim_lin(rgb, kind):
    m = MACHADO[kind]
    r, g, b = lin(rgb)
    out = [m[i][0] * r + m[i][1] * g + m[i][2] * b for i in range(3)]
    return tuple(max(0.0, min(1.0, c)) for c in out)


def sim_rgb(rgb, kind):
    return tuple(int(round(c * 255)) for c in sim_lin(rgb, kind))


# ---------------------------------------------------------------- 两套调色板
# 朴素语义色（第一版候选）：饱和、直觉，但没有亮度规划
CAND = [
    ("水", "#3FA9F5"), ("毒", "#57C84D"), ("油", "#C9A227"),
    ("血", "#D0342C"), ("酸", "#B7E22B"), ("能量", "#B25CFF"),
]

# 推荐色：色相语义不动，只改两处
#   能量 288° → 320°（品红，deutan 下退成蓝灰，与水的蓝拉开 52.7 ΔE）
#   全部主体加 1px 常量暖白轮廓 #E8E0D8（不随任何数据变化，只保可见性）
RECO = [
    ("水", "#3FA9F5"), ("毒", "#57C84D"), ("油", "#C9A227"),
    ("血", "#D0342C"), ("酸", "#B7E22B"), ("能量", "#FF6BD6"),
]
OUTLINE = "#E8E0D8"


def table(pal):
    print("| 成分 | hex | L* | 对比 #332B28 | 对比 #1B1715 | deutan hex | L*_d | protan hex | L*_p |")
    print("|---|---|---|---|---|---|---|---|---|")
    for name, h in pal:
        c = hx(h)
        d, p = sim_rgb(c, "deutan"), sim_rgb(c, "protan")
        print("| %s | `%s` | %.1f | %.2f:1 | %.2f:1 | `%s` | %.1f | `%s` | %.1f |" % (
            name, to_hex(c), lab(c)[0], contrast(c, BG["#332B28"]), contrast(c, BG["#1B1715"]),
            to_hex(d), lab(d)[0], to_hex(p), lab(p)[0]))


def groups(pal, thr=12.0):
    """并查集：两种模拟里只要有一个塌缩（ΔE<thr）就归同组。"""
    n = len(pal)
    parent = list(range(n))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    for i in range(n):
        for j in range(i + 1, n):
            d = min(de76(sim_rgb(hx(pal[i][1]), "deutan"), sim_rgb(hx(pal[j][1]), "deutan")),
                    de76(sim_rgb(hx(pal[i][1]), "protan"), sim_rgb(hx(pal[j][1]), "protan")))
            if d < thr:
                ri, rj = find(i), find(j)
                if ri != rj:
                    parent[ri] = rj
    return len({find(i) for i in range(n)})


def matrix(pal, kind):
    names = [n for n, _ in pal]
    print("\n%s ΔE76 矩阵\n" % kind)
    print("| | " + " | ".join(names) + " |")
    print("|---|" + "---|" * len(names))
    for i, ni in enumerate(names):
        row = []
        for j, nj in enumerate(names):
            if i == j:
                row.append("—")
            else:
                a = hx(pal[i][1]) if kind == "normal" else sim_rgb(hx(pal[i][1]), kind)
                b = hx(pal[j][1]) if kind == "normal" else sim_rgb(hx(pal[j][1]), kind)
                row.append("%.1f" % de76(a, b))
        print("| %s | %s |" % (ni, " | ".join(row)))


def collapse(pal, thr=25.0):
    rows = []
    for i in range(len(pal)):
        for j in range(i + 1, len(pal)):
            ci, cj = hx(pal[i][1]), hx(pal[j][1])
            dd = de76(sim_rgb(ci, "deutan"), sim_rgb(cj, "deutan"))
            dp = de76(sim_rgb(ci, "protan"), sim_rgb(cj, "protan"))
            dl_d = abs(lab(sim_rgb(ci, "deutan"))[0] - lab(sim_rgb(cj, "deutan"))[0])
            dl_p = abs(lab(sim_rgb(ci, "protan"))[0] - lab(sim_rgb(cj, "protan"))[0])
            if min(dd, dp) < thr:
                rows.append((pal[i][0], pal[j][0], dd, dp, dl_d, dl_p))
    return rows


# ---------------------------------------------------------------- 亮度排序
def lum_report(pal, title):
    print("\n%s 亮度排序（Y=WCAG 相对亮度 0..1；L*=CIE 明度）" % title)
    for kind in ("normal", "deutan", "protan"):
        rows = []
        for name, h in pal:
            rgb = hx(h) if kind == "normal" else sim_rgb(hx(h), kind)
            rows.append((rel_lum(rgb), lab(rgb)[0], name, to_hex(rgb)))
        rows.sort(reverse=True)
        print("  %-6s " % kind + " > ".join("%s %.3f/L%.0f(%s)" % (r[2], r[0], r[1], r[3]) for r in rows))
        low = []
        for i in range(len(rows)):
            for j in range(i + 1, len(rows)):
                dy = abs(rows[i][0] - rows[j][0])
                if dy < 0.05:
                    low.append("%s-%s ΔY=%.3f" % (rows[i][2], rows[j][2], dy))
        print("         ΔY<0.05 的对: " + ("；".join(low) if low else "无"))


# ---------------------------------------------------------------- PNG
def draw_panel(d, x0, y0, title, pal, scale=8, gap=8):
    font = ImageFont.load_default()
    d.text((x0, y0), title, fill=(235, 235, 235), font=font)
    rows = [("normal", "normal", (250, 250, 250)),
            ("deutan", "deutan", (200, 220, 255)),
            ("protan", "protan", (255, 210, 200))]
    yy = y0 + 18
    for label, kind, col in rows:
        d.text((x0, yy), label, fill=col, font=font)
        yy += 14
        for i, (name, h) in enumerate(pal):
            rgb = hx(h) if kind == "normal" else sim_rgb(hx(h), kind)
            x = x0 + i * (16 * scale + gap)
            d.rectangle([x, yy + 14, x + 16 * scale - 1, yy + 14 + 16 * scale - 1],
                        fill=rgb, outline=(80, 80, 80))
            d.text((x + 2, yy + 14 + 16 * scale + 2),
                   "%s L%.0f" % (to_hex(rgb), lab(rgb)[0]), fill=(190, 190, 190), font=font)
        yy += 14 + 16 * scale + 16
    # 1x 真实像素行（brief §6：图必须同时给 1x 与 4x+）
    d.text((x0, yy), "1x actual", fill=(180, 180, 180), font=font)
    yy += 14
    for i, (name, h) in enumerate(pal):
        x = x0 + i * (16 + 4)
        d.rectangle([x, yy, x + 15, yy + 15], fill=hx(h), outline=(80, 80, 80))
    return yy + 16 + 12


def make_png(cand, reco):
    scale, gap = 8, 8
    w = 40 + 6 * (16 * scale + gap) + 20
    h = 40 + 2 * (18 + 3 * (28 + 16 * scale + 16) + 44) + 40
    img = Image.new("RGB", (w, h), (26, 22, 21))
    d = ImageDraw.Draw(img)
    d.text((20, 10), "16px flow-source colours, 8x NEAREST | bg #332B28",
           fill=(220, 220, 220), font=ImageFont.load_default())
    y = draw_panel(d, 20, 30, "CANDIDATE  (naive semantic hues)", cand, scale, gap)
    draw_panel(d, 20, y + 16, "RECOMMENDED (hue kept, energy -> 320deg, +1px rim)", reco, scale, gap)
    img.save(PNG)
    print("\nwrote %s (%dx%d)" % (PNG, w, h))


# ---------------------------------------------------------------- main
def main():
    print("== A. 朴素候选 6 色 ==")
    table(CAND)
    print("\nCVD 分组数（阈值 ΔE76）: " +
          "  ".join("<%d:%d" % (t, groups(CAND, t)) for t in (12, 20, 25, 30)))
    matrix(CAND, "deutan")
    matrix(CAND, "protan")
    print("\n候选塌缩对（阈值 ΔE<25；ΔL* 为模拟后亮度差）:")
    for r in collapse(CAND):
        print("  %s-%s  ΔE_d=%.1f ΔE_p=%.1f  ΔL*_d=%.1f ΔL*_p=%.1f" % r)

    print("\n== B. 推荐 6 色 ==")
    table(RECO)
    print("\nCVD 分组数（阈值 ΔE76）: " +
          "  ".join("<%d:%d" % (t, groups(RECO, t)) for t in (12, 20, 25, 30)))
    matrix(RECO, "normal")
    matrix(RECO, "deutan")
    matrix(RECO, "protan")
    print("\n推荐色塌缩对（阈值 ΔE<25）:")
    rows = collapse(RECO)
    if not rows:
        print("  无")
    for r in rows:
        print("  %s-%s  ΔE_d=%.1f ΔE_p=%.1f  ΔL*_d=%.1f ΔL*_p=%.1f" % r)

    print("\n== C. 常量轮廓（不承载数据）==")
    o = hx(OUTLINE)
    print("轮廓 %s: L*=%.1f, vs #332B28 %.2f:1, protan 模拟 %s L*=%.1f, 对比 %.2f:1" % (
        OUTLINE, lab(o)[0], contrast(o, BG["#332B28"]), to_hex(sim_rgb(o, "protan")),
        lab(sim_rgb(o, "protan"))[0], contrast(sim_rgb(o, "protan"), BG["#332B28"])))
    print("血色 %s 与轮廓 ΔL*=%.1f（轮廓是常量，不编码）" % (
        dict(RECO)["血"], lab(o)[0] - lab(hx(dict(RECO)["血"]))[0]))

    print("\n== D. 亮度排序与 ΔY<0.05 对 ==")
    lum_report(CAND, "候选")
    lum_report(RECO, "推荐")

    make_png(CAND, RECO)


if __name__ == "__main__":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    main()
