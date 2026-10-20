"""量取截图里的格子像素与射线尺寸（只读分析，产物不随包发布）。"""
from PIL import Image
import numpy as np

SHOT_GAME = r"C:\Users\Administrator\.workbuddy-ai\clipboard-images\clipboard-2026-10-19T16-16-30-837Z-7a1c7e2f.png"
SHOT_BPH = r"C:\Users\Administrator\.workbuddy-ai\clipboard-images\clipboard-2026-10-19T11-00-30-738Z-70b89bbb.png"


def dark_lines(lum_profile, threshold=2.0):
    mins = []
    for i in range(2, len(lum_profile) - 2):
        if (lum_profile[i] <= lum_profile[i - 1]
                and lum_profile[i] <= lum_profile[i + 1]
                and lum_profile[i] < lum_profile.mean() - threshold):
            mins.append(i)
    merged = []
    for m in mins:
        if merged and m - merged[-1] <= 2:
            continue
        merged.append(m)
    diffs = [merged[i + 1] - merged[i] for i in range(len(merged) - 1)]
    return merged, diffs


def analyze(path, crop, axis=0, label=""):
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(float)
    x0, y0, x1, y1 = crop
    a = a[y0:y1, x0:x1]
    lum = a.mean(axis=2)
    prof = lum.mean(axis=0) if axis == 0 else lum.mean(axis=1)
    mins, diffs = dark_lines(prof)
    print(f"== {label} {im.size} crop={crop} axis={'x' if axis == 0 else 'y'}")
    print("  dark positions:", mins[:24])
    print("  spacing:", diffs[:24])
    if diffs:
        print("  median spacing:", float(np.median(diffs)))
    return im, a


def beam_profile(path, box, label=""):
    """在给定框内找射线：按列统计非背景像素的高度/颜色。"""
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(float)
    x0, y0, x1, y1 = box
    sub = a[y0:y1, x0:x1]
    print(f"== beam box {label} {box} mean rgb = {sub.reshape(-1,3).mean(axis=0).round(1)}")
    # 每列最亮像素与亮像素数
    lum = sub.mean(axis=2)
    for x in range(0, sub.shape[1], max(1, sub.shape[1] // 12)):
        col = lum[:, x]
        bright = np.where(col > col.min() + (col.max() - col.min()) * 0.6)[0]
        print(f"  x={x + x0:4d} max={col.max():6.1f} min={col.min():6.1f} bright_rows={len(bright)}"
              f" span={(bright.max() - bright.min()) if len(bright) else 0}")


if __name__ == "__main__":
    # 游戏底部背包格：只取底部背包条
    analyze(SHOT_GAME, (0, 470, 899, 678), axis=0, label="game inventory x")
    analyze(SHOT_GAME, (0, 470, 899, 678), axis=1, label="game inventory y")
    # 背包英雄参考：格子区
    analyze(SHOT_BPH, (60, 20, 760, 410), axis=0, label="bph grid x")
    analyze(SHOT_BPH, (60, 20, 760, 410), axis=1, label="bph grid y")
    # 背包英雄射线区域（中间偏右）
    beam_profile(SHOT_BPH, (420, 180, 640, 240), label="bph beam")
