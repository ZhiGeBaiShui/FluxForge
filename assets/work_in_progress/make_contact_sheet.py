"""
Generate a contact sheet showing reference tiles vs new tiles.
"""
from PIL import Image
import os

ASSETS = r"E:\code\FluxForge\assets"
OUT = os.path.join(ASSETS, "work_in_progress", "contact_sheet.png")

# Dark background
BG = (27, 23, 21, 255)
SCALE = 8
PADDING = 20
LABEL_H = 30

# Groups: (label, [filenames])
groups = [
    ("Reference (style anchors)", ["l_turn_device.png", "chem_reactor_top.png"]),
    ("Route devices (new)", ["route_straight.png", "route_t_split.png", "route_cross.png"]),
    ("Chem reactors (new)", ["chem_reactor_acid.png", "chem_reactor_alkali.png", "chem_reactor_cool.png", "chem_reactor_heat.png"]),
    ("Ice block", ["ice_block_new.png"]),
]

# Calculate layout
max_cols = max(len(g[1]) for g in groups)
rows = len(groups)

# Width: max_cols * (max tile width * scale + padding) + padding
# Height: rows * (max tile height * scale + LABEL_H + padding) + padding

# First pass: load all images and get scaled sizes
all_tiles = []
for label, files in groups:
    row_tiles = []
    for f in files:
        path = os.path.join(ASSETS, f)
        img = Image.open(path).convert("RGBA")
        scaled = img.resize((img.width * SCALE, img.height * SCALE), Image.NEAREST)
        row_tiles.append((f, scaled))
    all_tiles.append((label, row_tiles))

# Find max tile size
max_w = max(t[1].width for g in all_tiles for t in g[1])
max_h = max(t[1].height for g in all_tiles for t in g[1])

# Sheet dimensions
sheet_w = max_cols * (max_w + PADDING) + PADDING
sheet_h = rows * (max_h + LABEL_H + PADDING) + PADDING

sheet = Image.new("RGBA", (sheet_w, sheet_h), BG)

# Draw tiles
from PIL import ImageDraw
draw = ImageDraw.Draw(sheet)

for row_idx, (label, tiles) in enumerate(all_tiles):
    y = PADDING + row_idx * (max_h + LABEL_H + PADDING)
    # Draw group label
    draw.text((PADDING, y), label, fill=(200, 200, 200, 255))
    y += LABEL_H

    for col_idx, (fname, tile) in enumerate(tiles):
        x = PADDING + col_idx * (max_w + PADDING)
        # Center tile in cell
        ox = x + (max_w - tile.width) // 2
        oy = y + (max_h - tile.height) // 2
        sheet.paste(tile, (ox, oy), tile)

sheet.save(OUT, "PNG")
print(f"Contact sheet saved: {OUT}")
print(f"Size: {sheet.size}")
