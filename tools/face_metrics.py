# -*- coding: utf-8 -*-
"""从原版立绘里量出脸部五官比例，供 SVG 造型参考。"""
import numpy as np
from PIL import Image

REF = r"C:\Users\Administrator\AppData\Roaming\Tencent\TIM\Temp\b3ad03b7a9559f2a8128690291dea419.png"
im = Image.open(REF).convert("RGB")
arr = np.array(im).astype(int)

# 上排第 4 个（金发女性）与第 5 个（棕发女性）
chars = {
    "blonde_woman": (924, 40, 1092, 218),
    "brown_woman": (1220, 40, 1380, 218),
    "dark_woman": (1508, 40, 1668, 218),
}


def analyze(name, box):
    sub = arr[box[1]:box[3], box[0]:box[2]]
    r, g, b = sub[..., 0], sub[..., 1], sub[..., 2]
    mx, mn = sub.max(axis=2), sub.min(axis=2)
    # 肤色：暖、较亮、非灰
    skin = (r > g + 8) & (g > b) & (mx > 95) & (mx < 250) & ((mx - mn) > 12)
    ys, xs = np.nonzero(skin)
    if len(ys) < 200:
        print(name, "肤色像素太少")
        return
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    fh, fw = y1 - y0 + 1, x1 - x0 + 1
    print(f"\n== {name} ==  裁切 {sub.shape[1]}x{sub.shape[0]}")
    print(f"  脸部框 x{x0}-{x1} y{y0}-{y1}  宽{fw} 高{fh}  宽高比 {fw/fh:.2f}")

    # 眼睛：脸部框上半区的深色像素
    dark = (mx < 95)
    region = np.zeros_like(dark)
    region[y0:y0 + int(fh * 0.60), x0:x1] = True
    eye = dark & region
    ys2, xs2 = np.nonzero(eye)
    if len(ys2):
        print(f"  眼线区 y {ys2.min()}-{ys2.max()}（占脸高 {(ys2.min()-y0)/fh:.2f}~{(ys2.max()-y0)/fh:.2f}）")
        left = xs2[xs2 < (x0 + x1) / 2]
        right = xs2[xs2 >= (x0 + x1) / 2]
        if len(left) and len(right):
            print(f"  左眼中心 x≈{left.mean():.0f} 右眼中心 x≈{right.mean():.0f}  眼距 {right.mean()-left.mean():.0f}"
                  f"（占脸宽 {(right.mean()-left.mean())/fw:.2f}）")

    # 唇：红色偏移
    lip = (r > g + 25) & (r > b + 25) & (r > 90) & (r < 210)
    region2 = np.zeros_like(lip)
    region2[y0 + int(fh * 0.5):y1, x0:x1] = True
    lip = lip & region2
    ys3, xs3 = np.nonzero(lip)
    if len(ys3) > 30:
        print(f"  唇区 y {ys3.min()}-{ys3.max()}（占脸高 {(ys3.mean()-y0)/fh:.2f}）宽 {xs3.max()-xs3.min()}"
              f"（占脸宽 {(xs3.max()-xs3.min())/fw:.2f}）")


for n, b in chars.items():
    analyze(n, b)
