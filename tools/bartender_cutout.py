# -*- coding: utf-8 -*-
"""抠图：识别边框主色（纯白 / 棋盘格），从边界泛洪，得到透明底角色图。
   额外处理生成图自带的白色描边。"""
import os
import numpy as np
from PIL import Image, ImageFilter

SRC_DIR = r"E:/code/FluxForge/generated-images/bartender"
OUT_DIR = r"E:/code/FluxForge/generated-images/bartender/cut"
os.makedirs(OUT_DIR, exist_ok=True)


def flood(mask, seeds):
    """在 mask 内，从 seeds 出发做 span fill，返回连通的 True 区域。"""
    h, w = mask.shape
    out = np.zeros((h, w), dtype=bool)
    stack = []

    ys, xs = np.nonzero(seeds & mask)
    for y, x in zip(ys, xs):
        stack.append((int(y), int(x), int(x)))

    while stack:
        y, x0, x1 = stack.pop()
        row = mask[y] & ~out[y]
        while x0 > 0 and row[x0 - 1]:
            x0 -= 1
        while x1 < w - 1 and row[x1 + 1]:
            x1 += 1
        if out[y, x0:x1 + 1].all():
            continue
        out[y, x0:x1 + 1] = True
        for ny in (y - 1, y + 1):
            if 0 <= ny < h:
                seg = mask[ny, x0:x1 + 1] & ~out[ny, x0:x1 + 1]
                if seg.any():
                    idx = np.flatnonzero(seg)
                    breaks = np.flatnonzero(np.diff(idx) > 1)
                    starts = np.concatenate(([0], breaks + 1))
                    ends = np.concatenate((breaks, [len(idx) - 1]))
                    for s, e in zip(starts, ends):
                        stack.append((ny, x0 + idx[s], x0 + idx[e]))
    return out


def dilate1(m):
    out = np.zeros_like(m)
    out[1:, :] |= m[:-1, :]
    out[:-1, :] |= m[1:, :]
    out[:, 1:] |= m[:, :-1]
    out[:, :-1] |= m[:, 1:]
    return out


def border_colors(arr):
    band = np.concatenate([
        arr[0:4, :, :].reshape(-1, 3),
        arr[-4:, :, :].reshape(-1, 3),
        arr[:, 0:4, :].reshape(-1, 3),
        arr[:, -4:, :].reshape(-1, 3),
    ]).astype(np.int16)
    mx = band.max(axis=1)
    mn = band.min(axis=1)
    neutral = band[(mx - mn) <= 12]
    if len(neutral) == 0:
        return []
    buckets = {}
    for c in neutral:
        k = tuple((c // 4).tolist())
        buckets[k] = buckets.get(k, 0) + 1
    top = sorted(buckets.items(), key=lambda kv: -kv[1])[:4]
    return [np.array([v * 4 + 2 for v in k], dtype=np.int16) for k, n in top if n > 200]


def cutout(path, out_path):
    im = Image.open(path).convert("RGB")
    arr = np.array(im).astype(np.int16)
    h, w, _ = arr.shape
    colors = border_colors(arr)
    if not colors:
        print("  !! 未识别到背景色", path)
        return None

    mask = np.zeros((h, w), dtype=bool)
    for c in colors:
        d = np.abs(arr - c.reshape(1, 1, 3)).max(axis=2)
        mask |= (d <= 7)
    mx = arr.max(axis=2)
    mn = arr.min(axis=2)
    mask &= ((mx - mn) <= 14)

    border = np.zeros((h, w), dtype=bool)
    border[0, :] = border[-1, :] = True
    border[:, 0] = border[:, -1] = True
    filled = flood(mask, border)
    alpha = np.where(filled, 0, 255).astype(np.uint8)

    # 清掉背景残留的小碎块（棋盘格没被泛洪吃干净的部分）
    solid = alpha > 0
    visited = np.zeros_like(solid)
    for y0, x0 in zip(*np.nonzero(solid)):
        if visited[y0, x0]:
            continue
        seed = np.zeros_like(solid)
        seed[y0, x0] = True
        comp = flood(solid, seed)
        visited |= comp
        if comp.sum() < 2000:
            alpha[comp] = 0

    # 直接向内收缩 4px：把生成图那圈柔和的白色过渡带整体切掉
    a_img = Image.fromarray(alpha, "L")
    a_img = a_img.filter(ImageFilter.MinFilter(9))
    a_img = a_img.filter(ImageFilter.GaussianBlur(1.0))
    alpha = np.array(a_img)

    # 颜色外溢：边缘半透明像素 + 贴边的纯白边光，都染成邻近实体像素的颜色
    rgb = arr.astype(np.float32)
    lum = 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    ring = (alpha == 0)
    for _ in range(10):
        ring = ring | dilate1(ring)
    whity = ring & (lum >= 198) & (sat <= 26)
    opaque = (alpha >= 250) & ~whity
    for _ in range(8):
        acc = np.zeros_like(rgb)
        cnt = np.zeros(alpha.shape, dtype=np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            s_rgb = np.roll(rgb, (dy, dx), (0, 1))
            s_op = np.roll(opaque, (dy, dx), (0, 1))
            acc += s_rgb * s_op[..., None]
            cnt += s_op
        target = (alpha > 0) & ~opaque & (cnt > 0)
        rgb[target] = acc[target] / cnt[target][:, None]
        opaque = opaque | target

    out = Image.fromarray(np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), alpha]), "RGBA")
    out.save(out_path)
    print(f"  {os.path.basename(path)} -> 前景占比 {(alpha > 8).mean():.2%}")
    return out


TARGETS = {
    "v6_danger.png": "Premium_RPG_NPC_portrait__pain_2026-11-01T10-05-00.png",
    "v9_warm.png": "Keep_this_exact_woman__face__e_2026-11-01T10-06-46.png",
    "v10_allure.png": "Same_woman__same_face__same_au_2026-11-01T10-07-21.png",
    "v11_final.png": "Same_woman__same_face__same_au_2026-11-01T10-15-40.png",
}

if __name__ == "__main__":
    for name, f in TARGETS.items():
        p = os.path.join(SRC_DIR, f)
        if os.path.exists(p):
            print(name)
            cutout(p, os.path.join(OUT_DIR, name))
        else:
            print("missing", p)
