import os
from collections import Counter
from PIL import Image

GEN = "E:/code/FluxForge/generated-images/"
REF = "E:/code/FluxForge/assets/"

PALETTE = {
    "outline": (0x1C, 0x17, 0x26),
    "accent":  (0x30, 0x84, 0x7E),
    "iface":   (0xD6, 0x90, 0x3A),
    "glass":   (0x6D, 0x6D, 0x6D),
}
NAMED = {v: k for k, v in PALETTE.items()}

FILES = [
    ("ice_block", GEN), ("route_straight", GEN), ("route_l_turn", GEN),
    ("route_t_junction", GEN), ("route_cross", GEN), ("route_double_emit", GEN),
    ("chem_reactor_base", GEN), ("chem_reactor_acid", GEN),
    ("chem_reactor_alkali", GEN), ("chem_reactor_heat", GEN),
    ("chem_reactor_cool", GEN),
    ("l_turn_device", REF), ("chem_reactor_top", REF),
]

ALLOWED_ALPHA = (0, 255, 90, 124)


def analyze(path):
    im = Image.open(path)
    orig_mode = im.mode
    if im.mode != "RGBA":
        im = im.convert("RGBA")
    w, h = im.size
    colors = Counter(im.getdata())
    alphas = Counter(a for (_, _, _, a) in colors)
    opaque_alpha = Counter(a for (_, _, _, a) in colors if a > 0)
    bad_alpha = {a: n for a, n in opaque_alpha.items() if a not in ALLOWED_ALPHA}
    in_pal = Counter()
    off_pal = Counter()
    for (r, g, b, a), n in colors.items():
        if a == 0:
            continue
        if (r, g, b) in NAMED:
            in_pal[NAMED[(r, g, b)]] += n
        else:
            off_pal[(r, g, b)] += n
    return dict(im=im, w=w, h=h, mode=orig_mode, colors=colors, alphas=alphas,
                opaque_alpha=opaque_alpha, bad_alpha=bad_alpha,
                in_pal=in_pal, off_pal=off_pal)


def report(name, path):
    d = analyze(path)
    im, w, h, colors = d["im"], d["w"], d["h"], d["colors"]
    opaque_px = sum(n for (_, _, _, a), n in colors.items() if a > 0)
    print("=" * 74)
    print(f"{name}  [{w}x{h}]  mode={d['mode']}")
    print("-" * 74)
    print(f"  alpha 分布: {dict(sorted(d['alphas'].items()))}")
    print(f"  非法 alpha: {d['bad_alpha'] if d['bad_alpha'] else '无'}")
    print(f"  不透明像素: {opaque_px} / 总 {w*h}")
    print(f"  色板命中: {dict(d['in_pal'])}")
    if d["off_pal"]:
        print(f"  偏离色板像素数: {sum(d['off_pal'].values())}")
    print("  颜色清单:")
    for (r, g, b, a), n in colors.most_common():
        if a == 0:
            tag = "(透明)"
        elif (r, g, b) in NAMED:
            tag = f"PALETTE/{NAMED[(r, g, b)]}"
        else:
            tag = "偏离色板"
        print(f"    #{r:02X}{g:02X}{b:02X} a={a:<4} x{n:<6} {tag}")
    return d


def gradient_check(d):
    """检测渐变: 同一色板上出现大量近似色 / 局部亮度连续变化"""
    im = d["im"]
    px = im.load()
    w, h = d["w"], d["h"]
    hits = []
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            # 右侧邻居
            if x + 1 < w:
                r2, g2, b2, a2 = px[x + 1, y]
                if a2 > 0:
                    dmax = max(abs(r - r2), abs(g - g2), abs(b - b2))
                    if 0 < dmax <= 6:
                        hits.append((x, y, (r, g, b), (r2, g2, b2), dmax))
    return hits


if __name__ == "__main__":
    for name, base in FILES:
        p = base + name + ".png"
        if os.path.exists(p):
            report(name, p)
