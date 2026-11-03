import os
from collections import Counter
from PIL import Image

GEN = "E:/code/FluxForge/generated-images/"
REF = "E:/code/FluxForge/assets/"

NAMES = [
    "ice_block", "route_straight", "route_l_turn", "route_t_junction",
    "route_cross", "route_double_emit", "chem_reactor_base",
    "chem_reactor_acid", "chem_reactor_alkali", "chem_reactor_heat",
    "chem_reactor_cool",
]

KEY = {
    (0x1C, 0x17, 0x26): "outline",
    (0x30, 0x84, 0x7E): "accent",
    (0xD6, 0x90, 0x3A): "iface",
    (0x6D, 0x6D, 0x6D): "glass",
    (0x14, 0x1E, 0x2A): "dark",
    (0x3E, 0x35, 0x4E): "metal_dark",
    (0x38, 0x3E, 0x52): "metal_mid",
    (0x4C, 0x3C, 0x60): "metal_purple",
    (0x62, 0xAC, 0xA1): "accent2",
    (0x91, 0xC6, 0xB9): "highlight",
    (0x7C, 0x7A, 0x7A): "glass_bright",
    (0xE8, 0xF4, 0xF8): "ice_white",
    (0xA2, 0xB4, 0xBD): "ice_blue",
    (0x2C, 0x32, 0x46): "ice_shadow",
    (0xE0, 0x7A, 0x8A): "acid",
    (0x7A, 0x8A, 0xE0): "alkali",
    (0xE0, 0xA9, 0x4E): "heat",
}


def load(p):
    im = Image.open(p)
    return im.convert("RGBA")


def analyze(name, path):
    im = load(path)
    w, h = im.size
    px = im.load()
    print("=" * 76)
    print(f"{name}  [{w}x{h}]")
    print("-" * 76)

    cols = Counter(im.getdata())

    # A. alpha 集合
    alphas = sorted({a for (_, _, _, a) in cols})
    print(f"  [alpha] 出现值集合: {alphas}")
    ok_alpha = set(alphas) <= {0, 1, 90, 124, 255}
    print(f"          {'合规' if ok_alpha else '违规'}")

    # B. 命中区 alpha=1 的像素颜色是否全为黑
    hit = [(x, y) for y in range(h) for x in range(w) if px[x, y][3] == 1]
    if hit:
        hitc = Counter(px[x, y][:3] for x, y in hit)
        allblack = all(c == (0, 0, 0) for c in hitc)
        print(f"  [命中区] alpha=1 像素 {len(hit)} 个, 颜色={dict(hitc)}, "
              f"{'纯黑(隐式标记)' if allblack else '非纯黑 -> 会显示为黑块!'}")

    # C. 是否存在"半透明边缘像素"(玻璃 a=90 但紧邻非玻璃色,产生羽化观感)
    #    玻璃像素的 alpha 应全为 90; 检查有无 a=124 之外的中间值
    # D. 椭圆抗锯齿检测: PIL ellipse 无抗锯齿, 但 polygon/line 有。
    #    检查 outline 色 #1C1726 是否构成"1px 环" —— 找 outline 像素的 8 邻域最大环宽
    outline_px = [(x, y) for y in range(h) for x in range(w) if px[x, y][:3] == (0x1C, 0x17, 0x26)]
    # 检测 2px 宽的描边: 存在一个 outline 像素, 其 3x3 邻域内 outline 像素数 >= 6
    thick = []
    for (x, y) in outline_px:
        cnt = 0
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and px[nx, ny][:3] == (0x1C, 0x17, 0x26):
                    cnt += 1
        if cnt >= 6:
            thick.append((x, y))
    print(f"  [描边] outline 像素 {len(outline_px)}, 疑似 >=2px 粗描边处 {len(thick)}")
    if thick:
        print(f"          样例坐标: {thick[:12]}")

    # E. 渐变检测: 相邻像素同族不同色 (亮度差 <=8) 且非刻意分层
    grad = 0
    for y in range(h):
        for x in range(w - 1):
            r1, g1, b1, a1 = px[x, y]
            r2, g2, b2, a2 = px[x + 1, y]
            if a1 == 0 or a2 == 0:
                continue
            if (r1, g1, b1) == (r2, g2, b2):
                continue
            dm = max(abs(r1 - r2), abs(g1 - g2), abs(b1 - b2))
            if dm <= 10:
                grad += 1
    print(f"  [渐变] 相邻像素近似色对(差<=10)数量: {grad}")

    # F. 色彩总数
    rgb_only = {(r, g, b) for (r, g, b, a) in cols if a > 0}
    print(f"  [色彩] 不透明唯一 RGB 数: {len(rgb_only)}")
    off = [c for c in rgb_only if c not in KEY]
    if off:
        print(f"          非调色板色: {['#%02X%02X%02X' % c for c in off]}")
    else:
        print(f"          全部在脚本调色板内")

    # G. 调色板构成: 与参考图对比
    cnt = Counter()
    for (r, g, b, a), n in cols.items():
        if a == 0:
            continue
        cnt[KEY.get((r, g, b), "?") + (f"_a{a}" if a not in (255,) else "")] += n
    print(f"  [构成] {dict(cnt.most_common())}")


def compare_ref():
    print("\n" + "#" * 76)
    print("参考图对比")
    print("#" * 76)
    for nm, p in [("l_turn_device", REF + "l_turn_device.png"),
                  ("chem_reactor_top", REF + "chem_reactor_top.png")]:
        analyze(nm, p)


if __name__ == "__main__":
    for n in NAMES:
        p = GEN + n + ".png"
        if os.path.exists(p):
            analyze(n, p)
    compare_ref()
