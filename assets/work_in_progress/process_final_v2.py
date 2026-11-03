"""
Final tile processing v2 - fixed body color and sizing.
"""
from PIL import Image
import os

WORK_DIR = r"E:\code\FluxForge\assets\work_in_progress"
OUT_DIR = r"E:\code\FluxForge\assets"

# Known palette from reference images
BODY_COLOR = (45, 58, 74, 255)  # Deep blue-grey #2D3A4A
BORDER_COLOR = (13, 27, 42, 255)  # Near black navy #0D1B2A

def make_background_transparent(img, dark_thresh=25, light_thresh=245):
    img = img.convert("RGBA")
    pixels = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            is_dark = r < dark_thresh and g < dark_thresh and b < dark_thresh
            is_light = r > light_thresh and g > light_thresh and b > light_thresh
            if is_dark or is_light:
                pixels[x, y] = (r, g, b, 0)
    return img

def crop_to_subject(img, alpha_thresh=15):
    img = img.convert("RGBA")
    alpha = img.split()[3]
    bbox = alpha.point(lambda x: 255 if x > alpha_thresh else 0).getbbox()
    if bbox:
        pad = 1
        left = max(0, bbox[0] - pad)
        top = max(0, bbox[1] - pad)
        right = min(img.width, bbox[2] + pad)
        bottom = min(img.height, bbox[3] + pad)
        return img.crop((left, top, right, bottom))
    return img

def process_raw(raw_filename, target_size, out_name, shrink_to=None):
    raw_path = os.path.join(WORK_DIR, raw_filename)
    img = Image.open(raw_path)
    print(f"Processing {out_name}: raw {img.size}")

    img = make_background_transparent(img)
    img = crop_to_subject(img)
    print(f"  Cropped: {img.size}")

    # Resize to target first
    img = img.resize(target_size, Image.NEAREST)

    # If shrink_to specified, center and shrink within target
    if shrink_to and isinstance(shrink_to, tuple):
        small = img.resize(shrink_to, Image.NEAREST)
        new_img = Image.new("RGBA", target_size, (0, 0, 0, 0))
        offset_x = (target_size[0] - shrink_to[0]) // 2
        offset_y = (target_size[1] - shrink_to[1]) // 2
        new_img.paste(small, (offset_x, offset_y), small)
        img = new_img

    # Quantize
    img_rgb = img.convert("RGB")
    img_q = img_rgb.quantize(colors=24, method=Image.MEDIANCUT).convert("RGBA")
    alpha = img.split()[3].resize(target_size, Image.NEAREST)
    img_q.putalpha(alpha)

    # Clean edges
    pixels = img_q.load()
    w, h = img_q.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            if a < 40:
                pixels[x, y] = (0, 0, 0, 0)

    out_path = os.path.join(WORK_DIR, f"{out_name}_final.png")
    img_q.save(out_path, "PNG")
    print(f"  Saved: {out_path}")
    return img_q

def derive_route(cross_img, block_directions):
    """Derive route tile by painting body color over blocked openings."""
    img = cross_img.copy()
    pixels = img.load()
    w, h = img.size  # 32x32

    # Paint over blocked directions with body color
    for y in range(h):
        for x in range(w):
            # Top opening: rows 0-4, center cols 13-18
            if 'top' in block_directions and y <= 4 and 12 <= x <= 19:
                pixels[x, y] = BODY_COLOR
            # Bottom opening: rows 27-31, center cols 13-18
            if 'bottom' in block_directions and y >= 27 and 12 <= x <= 19:
                pixels[x, y] = BODY_COLOR
            # Left opening: cols 0-4, center rows 13-18
            if 'left' in block_directions and x <= 4 and 12 <= y <= 19:
                pixels[x, y] = BODY_COLOR
            # Right opening: cols 27-31, center rows 13-18
            if 'right' in block_directions and x >= 27 and 12 <= y <= 19:
                pixels[x, y] = BODY_COLOR

    return img

def main():
    tiles = {}

    # Route devices (32x32)
    tiles['route_cross'] = process_raw('route_cross_raw.png', (32, 32), 'route_cross')

    # Reaction devices (48x48)
    tiles['chem_acid'] = process_raw('chem_acid_raw.png', (48, 48), 'chem_acid')
    tiles['chem_alkali'] = process_raw('chem_alkali_v2_raw.png', (48, 48), 'chem_alkali')
    tiles['chem_cool'] = process_raw('chem_cool_raw.png', (48, 48), 'chem_cool')
    tiles['chem_heat'] = process_raw('chem_heat_raw.png', (48, 48), 'chem_heat')

    # Ice block (32x32, shrunk to 24x24 to look like a pickup item)
    tiles['ice_block'] = process_raw('ice_block_v2_raw.png', (32, 32), 'ice_block', shrink_to=(24, 24))

    # Derive route_straight (block top + bottom)
    print("\nDeriving route_straight...")
    straight = derive_route(tiles['route_cross'], ['top', 'bottom'])
    straight.save(os.path.join(WORK_DIR, 'route_straight_final.png'), 'PNG')
    tiles['route_straight'] = straight

    # Derive route_t_split (block right only)
    print("Deriving route_t_split...")
    t_split = derive_route(tiles['route_cross'], ['right'])
    t_split.save(os.path.join(WORK_DIR, 'route_t_split_final.png'), 'PNG')
    tiles['route_t_split'] = t_split

    # Previews
    preview_dir = os.path.join(WORK_DIR, "final_previews_v2")
    os.makedirs(preview_dir, exist_ok=True)
    bg = Image.new("RGBA", (400, 400), (27, 23, 21, 255))

    for name, img in tiles.items():
        preview = img.resize((img.width * 8, img.height * 8), Image.NEAREST)
        preview.save(os.path.join(preview_dir, f"{name}_8x.png"), "PNG")
        on_bg = bg.copy()
        ox = (400 - preview.width) // 2
        oy = (400 - preview.height) // 2
        on_bg.paste(preview, (ox, oy), preview)
        on_bg.save(os.path.join(preview_dir, f"{name}_on_bg.png"), "PNG")

    print(f"\nDone. Previews in: {preview_dir}")

if __name__ == "__main__":
    main()
