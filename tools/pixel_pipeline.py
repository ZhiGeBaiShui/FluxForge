#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
AI 高清图 -> 16xN 游戏贴图管线（定版）

用法:
    python tools/pixel_pipeline.py <src.png> <out.png> [cols] [rows]

默认 cols=1 rows=2（16x32，1x2 格）。

步骤:
    1. 洪泛去背景（从四边扩散，相邻像素容差 TOL）
    2. 最大连通域（剔除星云 / 小行星等残片）
    3. 裁主体 bbox -> 补透明边至 1:2
    4. LANCZOS 缩至 16*cols x 16*rows
    5. alpha 硬切（>= ALPHA_CUT 保留）
    6. quantize N_COLORS 色（MEDIANCUT）
    7. 众数滤波 3x3
"""
import sys
from collections import deque

import numpy as np
from PIL import Image

TOL = 40          # 洪泛容差（相邻像素 RGB 最大通道差）
ALPHA_CUT = 77    # alpha 硬切阈值（0.3 * 255）
N_COLORS = 14     # 量化色数


def flood_background(rgb, H, W):
    """从四边洪泛标记背景，返回前景 bool 数组（非洪泛区）。"""
    r = rgb[:, :, 0].tolist()
    g = rgb[:, :, 1].tolist()
    b = rgb[:, :, 2].tolist()
    visited = [bytearray(W) for _ in range(H)]
    dq = deque()
    for x in range(W):
        if not visited[0][x]:
            visited[0][x] = 1
            dq.append((0, x))
        if not visited[H - 1][x]:
            visited[H - 1][x] = 1
            dq.append((H - 1, x))
    for y in range(H):
        if not visited[y][0]:
            visited[y][0] = 1
            dq.append((y, 0))
        if not visited[y][W - 1]:
            visited[y][W - 1] = 1
            dq.append((y, W - 1))
    while dq:
        y, x = dq.popleft()
        cr, cg, cb = r[y][x], g[y][x], b[y][x]
        # 上
        if y > 0 and not visited[y - 1][x]:
            ny = y - 1
            if max(abs(r[ny][x] - cr), abs(g[ny][x] - cg), abs(b[ny][x] - cb)) <= TOL:
                visited[ny][x] = 1
                dq.append((ny, x))
        # 下
        if y < H - 1 and not visited[y + 1][x]:
            ny = y + 1
            if max(abs(r[ny][x] - cr), abs(g[ny][x] - cg), abs(b[ny][x] - cb)) <= TOL:
                visited[ny][x] = 1
                dq.append((ny, x))
        # 左
        if x > 0 and not visited[y][x - 1]:
            nx = x - 1
            if max(abs(r[y][nx] - cr), abs(g[y][nx] - cg), abs(b[y][nx] - cb)) <= TOL:
                visited[y][nx] = 1
                dq.append((y, nx))
        # 右
        if x < W - 1 and not visited[y][x + 1]:
            nx = x + 1
            if max(abs(r[y][nx] - cr), abs(g[y][nx] - cg), abs(b[y][nx] - cb)) <= TOL:
                visited[y][nx] = 1
                dq.append((y, nx))
    fg = np.array([[1 - visited[y][x] for x in range(W)] for y in range(H)], dtype=bool)
    return fg


def largest_cc(mask):
    """保留最大连通域（四邻）。"""
    H, W = mask.shape
    labels = np.zeros((H, W), dtype=np.int32)
    best_label, best_cnt = 0, 0
    cur = 0
    for sy in range(H):
        row = mask[sy]
        for sx in range(W):
            if row[sx] and labels[sy, sx] == 0:
                cur += 1
                cnt = 1
                labels[sy, sx] = cur
                dq = deque([(sy, sx)])
                while dq:
                    y, x = dq.popleft()
                    if y > 0 and mask[y - 1, x] and labels[y - 1, x] == 0:
                        labels[y - 1, x] = cur
                        cnt += 1
                        dq.append((y - 1, x))
                    if y < H - 1 and mask[y + 1, x] and labels[y + 1, x] == 0:
                        labels[y + 1, x] = cur
                        cnt += 1
                        dq.append((y + 1, x))
                    if x > 0 and mask[y, x - 1] and labels[y, x - 1] == 0:
                        labels[y, x - 1] = cur
                        cnt += 1
                        dq.append((y, x - 1))
                    if x < W - 1 and mask[y, x + 1] and labels[y, x + 1] == 0:
                        labels[y, x + 1] = cur
                        cnt += 1
                        dq.append((y, x + 1))
                if cnt > best_cnt:
                    best_label, best_cnt = cur, cnt
    return labels == best_label


def mode_filter3(arr):
    """3x3 众数滤波（RGB 三通道按颜色元组投票，中心优先）。"""
    H, W = arr.shape[:2]
    out = arr.copy()
    for y in range(H):
        for x in range(W):
            votes = {}
            best = tuple(int(v) for v in arr[y, x])
            votes[best] = 1
            best_c = 1
            for dy in (-1, 0, 1):
                ny = min(max(y + dy, 0), H - 1)
                for dx in (-1, 0, 1):
                    if dy == 0 and dx == 0:
                        continue
                    nx = min(max(x + dx, 0), W - 1)
                    c = tuple(int(v) for v in arr[ny, nx])
                    votes[c] = votes.get(c, 0) + 1
                    if votes[c] > best_c:
                        best, best_c = c, votes[c]
            out[y, x] = best
    return out


def main():
    src = sys.argv[1]
    out = sys.argv[2]
    cols = int(sys.argv[3]) if len(sys.argv) > 3 else 1
    rows = int(sys.argv[4]) if len(sys.argv) > 4 else 2
    TW, TH = 16 * cols, 16 * rows

    img = Image.open(src).convert("RGB")
    W, H = img.size
    print("source:", W, "x", H)
    rgb = np.array(img)

    # 1. 洪泛去背景
    fg = flood_background(rgb, H, W)
    print("foreground after flood:", int(fg.sum()), "/", W * H)

    # 2. 最大连通域
    cc = largest_cc(fg)
    print("foreground after largest-cc:", int(cc.sum()))

    # 3. 裁主体 bbox -> 补透明边至 1:2
    ys, xs = np.where(cc)
    y0, y1 = int(ys.min()), int(ys.max()) + 1
    x0, x1 = int(xs.min()), int(xs.max()) + 1
    sub_rgb = rgb[y0:y1, x0:x1].copy()
    sub_a = (cc[y0:y1, x0:x1] * 255).astype(np.uint8)
    ch, cw = sub_rgb.shape[:2]
    print("subject bbox:", cw, "x", ch, "ratio", round(cw / ch, 3))

    target_ratio = TW / TH  # 0.5 for 1x2
    if cw / ch < target_ratio:
        tw = int(round(ch * target_ratio))
        pad = (tw - cw) // 2
        canvas_rgb = np.zeros((ch, tw, 3), dtype=np.uint8)
        canvas_a = np.zeros((ch, tw), dtype=np.uint8)
        canvas_rgb[:, pad:pad + cw] = sub_rgb
        canvas_a[:, pad:pad + cw] = sub_a
    else:
        th = int(round(cw / target_ratio))
        pad = (th - ch) // 2
        canvas_rgb = np.zeros((th, cw, 3), dtype=np.uint8)
        canvas_a = np.zeros((th, cw), dtype=np.uint8)
        canvas_rgb[pad:pad + ch, :] = sub_rgb
        canvas_a[pad:pad + ch, :] = sub_a
    print("padded canvas:", canvas_rgb.shape[1], "x", canvas_rgb.shape[0])

    # 4. LANCZOS 缩至目标尺寸
    rgb_small = Image.fromarray(canvas_rgb).resize((TW, TH), Image.LANCZOS)
    a_small = Image.fromarray(canvas_a).resize((TW, TH), Image.BILINEAR)
    alpha = np.array(a_small)

    # 5. alpha 硬切
    alpha = np.where(alpha >= ALPHA_CUT, 255, 0).astype(np.uint8)

    # 6. quantize 14 色
    q = rgb_small.quantize(colors=N_COLORS, method=Image.MEDIANCUT)
    out_rgb = np.array(q.convert("RGB"))

    # 7. 众数滤波 3x3
    out_rgb = mode_filter3(out_rgb)

    final = Image.fromarray(out_rgb).convert("RGBA")
    final.putalpha(Image.fromarray(alpha))
    final.save(out)

    opaque = int((alpha > 0).sum())
    colors = len({tuple(int(v) for v in out_rgb[y, x]) for y in range(TH) for x in range(TW)})
    print("result:", TW, "x", TH, "| opaque:", opaque, "/", TW * TH, "| colors:", colors)
    print("saved:", out)

    # 8 倍预览
    preview_path = out.replace(".png", "_preview8x.png")
    final.resize((TW * 8, TH * 8), Image.NEAREST).save(preview_path)
    print("preview:", preview_path)


if __name__ == "__main__":
    main()
