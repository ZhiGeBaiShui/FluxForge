"""用自相关量格子周期：比找暗线稳。"""
from PIL import Image
import numpy as np

SHOT_GAME = r"C:\Users\Administrator\.workbuddy-ai\clipboard-images\clipboard-2026-10-19T16-16-30-837Z-7a1c7e2f.png"
SHOT_BPH = r"C:\Users\Administrator\.workbuddy-ai\clipboard-images\clipboard-2026-10-19T11-00-30-738Z-70b89bbb.png"


def period(path, box, axis="x"):
    im = Image.open(path).convert("L")
    a = np.asarray(im).astype(float)
    x0, y0, x1, y1 = box
    sub = a[y0:y1, x0:x1]
    prof = sub.mean(axis=0) if axis == "x" else sub.mean(axis=1)
    prof = prof - prof.mean()
    ac = np.correlate(prof, prof, mode="full")[len(prof) - 1:]
    ac /= ac[0]
    # 找 20..160 之间的局部峰
    peaks = []
    for lag in range(20, min(161, len(ac) - 1)):
        if ac[lag] > ac[lag - 1] and ac[lag] >= ac[lag + 1]:
            peaks.append((round(float(ac[lag]), 3), lag))
    peaks.sort(reverse=True)
    print(path.split("\\")[-1], axis, "size", im.size, "crop", box)
    print("  top peaks (corr, lag):", peaks[:8])
    return peaks


if __name__ == "__main__":
    period(SHOT_GAME, (0, 470, 899, 678), "x")
    period(SHOT_GAME, (0, 470, 899, 678), "y")
    period(SHOT_BPH, (60, 20, 760, 410), "x")
    period(SHOT_BPH, (60, 20, 760, 410), "y")
