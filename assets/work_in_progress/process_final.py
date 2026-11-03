"""
Final tile processing script.
- Process AI-generated raw images into game-ready pixel tiles
- Derive route_straight and route_t_split from route_cross base
- Clean up edges and ensure style consistency
"""
from PIL import Image
import os
import copy

WORK_DIR = r"E:\code\FluxForge\assets\work_in_progress"
OUT_DIR = r"E:\code\FluxForge\assets"

def make_background_transparent(img, dark_thresh=25, light_thresh=245):
    """Make near-black or near-white background transparent."""
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
    """Crop to non-transparent bounding box."""
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

def process_raw(raw_filename, target_size, out_name):
    """Process a raw AI image to final pixel tile."""
    raw_path = os.path.join(WORK_DIR, raw_filename)
    img = Image.open(raw_path)
    print(f"Processing {out_name}: raw {img.size}")

    img = make_background_transparent(img)
    img = crop_to_subject(img)
    print(f"  Cropped: {img.size}")

    # Resize to target
    img = img.resize(target_size, Image.NEAREST)

    # Quantize to limited palette for pixel art feel
    img_rgb = img.convert("RGB")
    img_q = img_rgb.quantize(colors=24, method=Image.MEDIANCUT).convert("RGBA")
    # Restore alpha
    alpha = img.split()[3]
    # Resize alpha to match
    alpha = alpha.resize(target_size, Image.NEAREST)
    img_q.putalpha(alpha)

    # Clean edge pixels: if alpha < 50, make fully transparent
    pixels = img_q.load()
    w, h = img_q.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            if a < 50:
                pixels[x, y] = (0, 0, 0, 0)

    out_path = os.path.join(WORK_DIR, f"{out_name}_final.png")
    img_q.save(out_path, "PNG")
    print(f"  Saved: {out_path}")
    return img_q

def derive_route_from_cross(cross_img, block_directions):
    """
    Derive a route tile from the cross tile by blocking specified directions.
    cross_img: 32x32 PIL image of cross router
    block_directions: list of 'top', 'bottom', 'left', 'right' to block
    Returns new 32x32 image.
    """
    img = cross_img.copy()
    pixels = img.load()
    w, h = img.size

    # Sample the body color (dark blue-grey) from a known solid area
    # Use pixel (2, 2) as reference for body color
    body_color = pixels[2, 2][:3]
    print(f"  Body color sample: {body_color}")

    # Block the specified directions by painting over the channel opening
    with Image.new("RGBA", img.size, (0,0,0,0)) as overlay:
        op = overlay.load()
        for y in range(h):
            for x in range(w):
                # Top direction: block rows 0-3, center columns 13-18
                if 'top' in block_directions and y < 4 and 12 <= x <= 19:
                    op[x, y] = (*body_color, 255)
                # Bottom direction: block rows 28-31, center columns 13-18
                if 'bottom' in block_directions and y >= 28 and 12 <= x <= 19:
                    op[x, y] = (*body_color, 255)
                # Left direction: block columns 0-3, center rows 13-18
                if 'left' in block_directions and x < 4 and 12 <= y <= 19:
                    op[x, y] = (*body_color, 255)
                # Right direction: block columns 28-31, center rows 13-18
                if 'right' in block_directions and x >= 28 and 12 <= y <= 19:
                    op[x, y] = (*body_color, 255)
        img = Image.alpha_composite(img, overlay)

    return img

def main():
    # Step 1: Process all raw images
    tiles = {}

    # Route devices (32x32)
    tiles['route_cross'] = process_raw('route_cross_raw.png', (32, 32), 'route_cross')

    # Reaction devices (48x48)
    tiles['chem_acid'] = process_raw('chem_acid_raw.png', (48, 48), 'chem_acid')
    tiles['chem_alkali'] = process_raw('chem_alkali_v2_raw.png', (48, 48), 'chem_alkali')
    tiles['chem_cool'] = process_raw('chem_cool_raw.png', (48, 48), 'chem_cool')
    tiles['chem_heat'] = process_raw('chem_heat_raw.png', (48, 48), 'chem_heat')

    # Ice block (32x32)
    tiles['ice_block'] = process_raw('ice_block_v2_raw.png', (32, 32), 'ice_block')

    # Step 2: Derive route_straight from cross (block top and bottom)
    print("\nDeriving route_straight from route_cross...")
    straight = derive_route_from_cross(tiles['route_cross'], ['top', 'bottom'])
    straight_path = os.path.join(WORK_DIR, 'route_straight_final.png')
    straight.save(straight_path, 'PNG')
    tiles['route_straight'] = straight
    print(f"  Saved: {straight_path}")

    # Step 3: Derive route_t_split from cross (block right)
    print("\nDeriving route_t_split from route_cross...")
    t_split = derive_route_from_cross(tiles['route_cross'], ['right'])
    t_split_path = os.path.join(WORK_DIR, 'route_t_split_final.png')
    t_split.save(t_split_path, 'PNG')
    tiles['route_t_split'] = t_split
    print(f"  Saved: {t_split_path}")

    # Step 4: Generate 8x previews
    preview_dir = os.path.join(WORK_DIR, "final_previews")
    os.makedirs(preview_dir, exist_ok=True)

    # Dark background for preview
    bg = Image.new("RGBA", (400, 400), (27, 23, 21, 255))

    for name, img in tiles.items():
        preview = img.resize((img.width * 8, img.height * 8), Image.NEAREST)
        preview_path = os.path.join(preview_dir, f"{name}_8x.png")
        preview.save(preview_path, "PNG")

        # Also save on dark background preview
        on_bg = bg.copy()
        offset_x = (400 - preview.width) // 2
        offset_y = (400 - preview.height) // 2
        on_bg.paste(preview, (offset_x, offset_y), preview)
        on_bg_path = os.path.join(preview_dir, f"{name}_on_bg.png")
        on_bg.save(on_bg_path, "PNG")

    print(f"\nAll previews saved to: {preview_dir}")

if __name__ == "__main__":
    main()
