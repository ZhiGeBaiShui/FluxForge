# -*- coding: utf-8 -*-
"""
概念 A「实体化」——流源 = 会走路的小瓶机（Vial Walker）
所有像素在 16x16 格内程序化生成，无任何外部贴图。

运行：
  python render_a.py
输出（脚本同目录）：
  a-main-1x.png / a-main-4x.png   16px 格底 + 12x12 灰物品图标占位（遮挡验证）
  a-attrs-4x.png                  6 成分一排
  a-temp-4x.png                   冷 / 常温 / 热
  a-content-4x.png                3 档含量
  a-dirs-4x.png                   4 向
  a-anim-4x.png                   0.0-1.0s 关键帧（附加）
  a-ascii.txt                     静止态字符网格（右向 / 上向）
"""
import os
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.dirname(os.path.abspath(__file__))
T = 16  # 格边长

# ---------------------------------------------------------------- 调色板
OUTLINE = (11, 15, 24)      # #0B0F18  系统色：描边
METAL   = (167, 179, 199)   # #A7B3C7  系统色：金属
GLASS   = (23, 30, 46)      # #171E2E  系统色：空玻璃

# 每种成分恰好 3 个自有色（+3 个系统色 = 单件 6 色）
COMP = {
    'water':  dict(main=(0x3E, 0x8F, 0xE2), hi=(0xA6, 0xDA, 0xFF), sh=(0x1F, 0x4F, 0x8F)),
    'poison': dict(main=(0x7F, 0xC6, 0x3F), hi=(0xD8, 0xFF, 0x8A), sh=(0x3E, 0x6E, 0x17)),
    'oil':    dict(main=(0xC4, 0x8A, 0x2A), hi=(0xF3, 0xD2, 0x7C), sh=(0x6B, 0x45, 0x08)),
    'blood':  dict(main=(0xC4, 0x34, 0x32), hi=(0xF5, 0x7C, 0x64), sh=(0x6E, 0x17, 0x15)),
    'acid':   dict(main=(0xCB, 0xDF, 0x3C), hi=(0xF7, 0xFF, 0x9E), sh=(0x6F, 0x7E, 0x12)),
    'energy': dict(main=(0xD0, 0x6B, 0xFF), hi=(0xF2, 0xD9, 0xFF), sh=(0x5A, 0x2A, 0x96)),
}
COMP_ORDER = ['water', 'poison', 'oil', 'blood', 'acid', 'energy']
CN = dict(water='水', poison='毒', oil='油', blood='血', acid='酸', energy='能量')

FROST = (0xDF, 0xF6, 0xFF)   # 系统色：霜
HEAT  = (0xFF, 0x4D, 0x3A)   # 系统色：热
EMBER = (0xFF, 0xC2, 0x4A)   # 系统色：余烬
EMBER_DIM = (0x7A, 0x3A, 0x10)
PLATE = (0x06, 0x09, 0x11)
GLYPH = (0xEA, 0xF2, 0xFF)

# ---------------------------------------------------------------- 旋转
def rot(x, y, d):
    """以 16x16 格中心 (7.5,7.5) 的整数 90 度旋转；四向像素级精确。"""
    if d == 'right':
        return x, y
    if d == 'up':            # 逆时针 90
        return y, 15 - x
    if d == 'left':          # 180
        return 15 - x, 15 - y
    if d == 'down':          # 顺时针 90
        return 15 - y, x
    raise ValueError(d)


# ---------------------------------------------------------------- 本体（8x9，四向不旋转）
def body_layer():
    p = {}
    for y in range(6, 11):           # 玻璃腔 6x5
        for x in range(5, 11):
            p[(x, y)] = 'glass'
    for x in range(4, 12):           # 肩线
        p[(x, 5)] = 'out'
    for x in range(5, 11):           # 底（左右下角切角 -> 圆润剪影）
        p[(x, 11)] = 'out'
    for y in range(6, 11):           # 左右壁
        p[(4, y)] = 'out'
        p[(11, y)] = 'out'
    for x in range(5, 11):           # 瓶盖（6 宽，4px 金属）
        p[(x, 4)] = 'out'
    for x in range(6, 10):
        p[(x, 4)] = 'metal'
    for x in (5, 10):                # 两只金属脚（嵌在底边，四向不冲突）
        p[(x, 11)] = 'metal'
    return p


# 头（右向）：2x3 暗块 + 1px 喷口
HEAD_R = {(12, 6): 'out', (13, 6): 'out',
          (12, 7): 'main', (13, 7): 'hi',
          (12, 8): 'out', (13, 8): 'out'}

# 尾（右向）：三颗余烬点，间隔 2px，亮几颗 = 剩余射程档位
TAIL_R = [(3, 6), (3, 8), (3, 10)]

# 含量档位 -> 液柱行数（0..3 档 -> 0/1/3/5 行）
FILL_ROWS = {0: 0, 1: 1, 2: 3, 3: 5}

# 每种成分的液面纹理（6 px，x=5..10）。这是 I1 的形差通道。
SURFACE = {
    'water':  ['hi', 'hi', 'main', 'main', 'main', 'sh'],
    'poison': ['main', 'hi', 'main', 'hi', 'main', 'hi'],
    'oil':    ['main', 'hi', 'hi', 'hi', 'main', 'sh'],
    'blood':  ['sh', 'sh', 'main', 'main', 'sh', 'sh'],
    'acid':   ['hi', 'main', 'sh', 'main', 'hi', 'sh'],
    'energy': ['main', 'sh', 'hi', 'sh', 'main', 'main'],
}


def temp_pixels(temp):
    """I2：静态覆层，位置固定，不改变外形。"""
    if temp == 'cold':   # 顶部霜晶：盖子两角 + 上壁
        return {(6, 4): 'frost', (9, 4): 'frost', (4, 6): 'frost', (11, 6): 'frost'}
    if temp == 'hot':    # 盖顶发热 + 底座发红
        return {(7, 4): 'heat', (8, 4): 'heat', (5, 11): 'heat', (10, 11): 'heat'}
    return {}


BADGE = {  # 3x3 角标字形（I5）；流动 = 无
    'blocked':  [(0, 0), (2, 0), (1, 1), (0, 2), (2, 2)],
    'split':    [(0, 0), (2, 0), (1, 1), (1, 2)],
    'merge':    [(1, 0), (1, 1), (0, 2), (2, 2)],
    'collect':  [(0, 0), (1, 1), (0, 2), (2, 2)],
    'flow':     [],
}


def compose(comp='water', temp='normal', tier=3, state='flow', d='right', range_n=3,
            bob=0, slosh=0, spark=0, leg=0):
    """返回 dict[(x,y)] = rgb。"""
    pal = COMP[comp]
    px = {}
    # 1) 空玻璃底
    for (x, y), role in body_layer().items():
        if role == 'glass':
            px[(x, y)] = GLASS
    # 2) 液柱（I3）
    n = FILL_ROWS[tier]
    if n:
        top = 11 - n                       # y = 10 起往上
        for i in range(n):
            y = top + i
            for x in range(5, 11):
                px[(x, y)] = pal['main']
            px[(5, y)] = pal['hi']         # 左上光源：左壁反光
            px[(10, y)] = pal['sh']        # 右壁内阴影
        # 液面纹理（只覆盖最上一行；slosh 为 -1/0/1 的水平错位）
        for i, role in enumerate(SURFACE[comp]):
            x = 5 + i
            if slosh == 1 and i == 5:
                continue
            if slosh == -1 and i == 0:
                continue
            px[(x, top)] = pal[role]
    # 3) 本体描边 / 金属 / 脚
    for (x, y), role in body_layer().items():
        if role == 'out':
            px[(x, y)] = OUTLINE
        elif role == 'metal':
            px[(x, y)] = METAL
    # 4) 头（方向性）
    for (x, y), role in HEAD_R.items():
        xx, yy = rot(x, y, d)
        px[(xx, yy)] = pal[role] if role in pal else OUTLINE
    # 5) 尾管 + 余烬（I4：亮格数 = 档位，位置固定）
    for i, (x, y) in enumerate(TAIL_R):
        xx, yy = rot(x, y, d)
        px[(xx, yy)] = EMBER if (i < range_n and not (spark == 2 and i == 0)) else EMBER_DIM
    # 6) 温度覆层（I2）
    for (x, y), role in temp_pixels(temp).items():
        px[(x, y)] = FROST if role == 'frost' else HEAT
    # 7) 状态角标（I5）
    if state != 'flow':
        for x in range(0, 5):
            for y in range(0, 5):
                px[(x, y)] = PLATE
        for (gx, gy) in BADGE[state]:
            px[(1 + gx, 1 + gy)] = GLYPH
    # 8) 离格抖动（整数像素）
    if bob or leg:
        px = {(x, y + bob): c for (x, y), c in px.items()}
    return px


def to_img(px, scale=1):
    im = Image.new('RGBA', (T, T), (0, 0, 0, 0))
    for (x, y), c in px.items():
        if 0 <= x < T and 0 <= y < T:
            im.putpixel((x, y), c + (255,))
    if scale != 1:
        im = im.resize((T * scale, T * scale), Image.NEAREST)
    return im


# ---------------------------------------------------------------- 图集
def bg_tile(kind):
    im = Image.new('RGBA', (T, T), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    if kind == 'dark':
        d.rectangle([0, 0, 15, 15], fill=(0x12, 0x18, 0x2A, 255))
    elif kind == 'light':
        d.rectangle([0, 0, 15, 15], fill=(0xE6, 0xE0, 0xD2, 255))
    elif kind == 'checker':
        for y in range(T):
            for x in range(T):
                c = (0x20, 0x28, 0x38) if ((x // 2 + y // 2) % 2 == 0) else (0x2C, 0x36, 0x48)
                im.putpixel((x, y), c + (255,))
    elif kind == 'icon':
        d.rectangle([0, 0, 15, 15], fill=(0x1A, 0x21, 0x30, 255))
        # 12x12 灰物品图标占位（居中 x2..13, y2..13），画成瓶子剪影
        d.rectangle([2, 2, 13, 13], fill=(0x6A, 0x70, 0x7C, 255))
        d.rectangle([3, 3, 12, 12], fill=(0x8A, 0x91, 0x9E, 255))
        d.rectangle([6, 3, 9, 4], fill=(0x5A, 0x60, 0x6C, 255))     # 瓶颈
        d.rectangle([4, 6, 11, 11], fill=(0x9A, 0xA1, 0xAE, 255))   # 瓶身
        d.rectangle([5, 7, 6, 9], fill=(0xB4, 0xBB, 0xC8, 255))     # 高光
    return im


def compose_tile(px, bg):
    im = bg_tile(bg)
    im.alpha_composite(to_img(px))
    return im


def sheet(tiles, labels, path_base, gap=4, scale=4, pad=6, title=None):
    n = len(tiles)
    w = pad * 2 + n * T + (n - 1) * gap
    h = pad * 2 + T
    im1 = Image.new('RGBA', (w, h), (0x0D, 0x11, 0x1A, 255))
    for i, t in enumerate(tiles):
        im1.alpha_composite(t, (pad + i * (T + gap), pad))
    im1.save(os.path.join(OUT, path_base + '-1x.png'))
    # 1x 图（干净、无文字）
    im4 = im1.resize((w * scale, h * scale), Image.NEAREST)
    # 4x 图 + 标签
    lab_h = 26 if labels else 0
    out = Image.new('RGBA', (w * scale, h * scale + lab_h + (22 if title else 0)), (0x0D, 0x11, 0x1A, 255))
    out.paste(im4, (0, 22 if title else 0))
    d = ImageDraw.Draw(out)
    font_path = r'C:\Windows\Fonts\msyh.ttc'
    if not os.path.exists(font_path):
        font_path = r'C:\Windows\Fonts\simhei.ttf'
    try:
        f = ImageFont.truetype(font_path, 13)
        ft = ImageFont.truetype(font_path, 15)
    except Exception:
        f = ft = ImageFont.load_default()
    if title:
        d.text((pad * scale, 3), title, font=ft, fill=(0xC9, 0xD3, 0xE6))
    for i, lb in enumerate(labels):
        x = (pad + i * (T + gap)) * scale
        d.text((x, (22 if title else 0) + h * scale + 4), lb, font=f, fill=(0x9F, 0xAD, 0xC6))
    out.save(os.path.join(OUT, path_base + '-4x.png'))
    return im1


def ascii_grid(px, legend):
    rev = {}
    for k, v in legend.items():
        rev[v] = k
    lines = []
    for y in range(T):
        row = ''
        for x in range(T):
            c = px.get((x, y))
            row += rev.get(c, '.')
        lines.append(row)
    return lines


LEGEND = {
    '#': OUTLINE, '+': None, 'o': None, 'x': None, 'm': METAL, ':': GLASS,
    '~': FROST, '^': HEAT, 'e': EMBER, 'd': EMBER_DIM, '*': GLYPH, 'B': PLATE,
}


def dump_ascii():
    out = []
    for comp in ['water', 'poison']:
        pal = COMP[comp]
        lg = dict(LEGEND)
        lg['+'] = pal['main']; lg['o'] = pal['hi']; lg['x'] = pal['sh']
        for d in ['right', 'up']:
            px = compose(comp=comp, tier=3, state='flow', d=d, range_n=3)
            out.append('=== %s / %s / full / flow ===' % (comp, d))
            for ln in ascii_grid(px, lg):
                out.append(ln)
            out.append('')
    # 状态 / 温度 / 含量各一
    lg = dict(LEGEND); lg['+'] = COMP['water']['main']; lg['o'] = COMP['water']['hi']; lg['x'] = COMP['water']['sh']
    for name, kw in [('blocked', dict(state='blocked')), ('split', dict(state='split')),
                     ('merge', dict(state='merge')), ('collect', dict(state='collect')),
                     ('cold', dict(temp='cold')), ('hot', dict(temp='hot')),
                     ('low', dict(tier=1)), ('mid', dict(tier=2)), ('empty', dict(tier=0, range_n=0))]:
        px = compose(**kw)
        out.append('=== water / right / %s ===' % name)
        for ln in ascii_grid(px, lg):
            out.append(ln)
        out.append('')
    txt = '\n'.join(out)
    with open(os.path.join(OUT, 'a-ascii.txt'), 'w', encoding='utf-8') as f:
        f.write(txt)
    return txt


def counts():
    px = compose()
    lit = len(px)
    print('静止态（水/满/流动/右向）不透明像素 =', lit)
    for n in [0, 1, 2, 3]:
        print('  含量档 %d 液柱行数 %d, 总像素 %d' % (n, FILL_ROWS[n], len(compose(tier=n))))
    for s in BADGE:
        print('  状态 %-8s 额外像素 %d' % (s, 25 + len(BADGE[s])))


def main():
    # 1) 主图：不同底色 + 12x12 图标占位
    tiles = [compose_tile(compose(), 'dark'),
             compose_tile(compose(), 'light'),
             compose_tile(compose(), 'checker'),
             compose_tile(compose(), 'icon')]
    sheet(tiles, ['深底 #12182A', '浅底 #E6E0D2', '棋盘 2px', '12x12 灰图标'], 'a-main',
          title='A 实体化 · 16px 格 · 右向 · 水/满/常温/流动')
    # 2) 成分 6 连
    tiles = [compose_tile(compose(comp=c), 'dark') for c in COMP_ORDER]
    sheet(tiles, [CN[c] + ' ' + c for c in COMP_ORDER], 'a-attrs', gap=5, title='I1 成分：色相 + 液面纹理')
    # 3) 温度 3 连
    tiles = [compose_tile(compose(comp='water', temp=t), 'dark') for t in ['cold', 'normal', 'hot']]
    sheet(tiles, ['冷 cold', '常温 normal', '热 hot'], 'a-temp', gap=8, title='I2 温度：静态覆层（霜 / 无 / 热）')
    # 4) 含量 3 连
    tiles = [compose_tile(compose(comp='water', tier=t), 'dark') for t in [1, 2, 3]]
    sheet(tiles, ['少 1 行', '中 3 行', '满 5 行'], 'a-content', gap=8, title='I3 含量：液柱行数 1 / 3 / 5')
    # 5) 四向
    tiles = [compose_tile(compose(d=d), 'dark') for d in ['right', 'up', 'left', 'down']]
    sheet(tiles, ['右', '上', '左', '下'], 'a-dirs', gap=8, title='I6 四向：仅头/尾绕格心 90 度旋转')
    # 6) 关键帧（附加）
    frames = [
        compose(bob=0, slosh=0, range_n=3), compose(bob=-1, slosh=0, range_n=3),
        compose(bob=-1, slosh=1, range_n=2, spark=2), compose(bob=0, slosh=1, range_n=2, spark=2),
        compose(bob=0, slosh=0, range_n=1, spark=0), compose(bob=-1, slosh=-1, range_n=1, spark=2),
        compose(bob=-1, slosh=-1, range_n=1, spark=2), compose(bob=0, slosh=0, range_n=0, spark=0),
    ]
    tiles = [compose_tile(f, 'dark') for f in frames]
    sheet(tiles, ['f00', 'f06', 'f12', 'f18', 'f24', 'f30', 'f36', 'f48'], 'a-anim', gap=6,
          title='0.0-1.0s 关键帧（附加）')
    dump_ascii()
    counts()
    print('done ->', OUT)


if __name__ == '__main__':
    main()
