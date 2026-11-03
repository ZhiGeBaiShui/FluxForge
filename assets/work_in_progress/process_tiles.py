"""
Process AI-generated high-res tiles into game-ready pixel art tiles.
- Crop to subject
- Downscale to target resolution with NEAREST
- Make background transparent
- Quantize to limited palette matching reference style
"""
from PIL import Image
import os
import sys

WORK_DIR = r"E:\code\FluxForge\assets\work_in_progress"
REF_DIR = r"E:\code\FluxForge\assets"

# Target sizes based on existing assets
TILE_SIZES = {
    "route_cross": (32, 32),
    "route_t_split": (32, 32),
    "route_straight": (32, 32),
    "ice_block": (32, 32),
    "chem_acid": (48, 48),
    "chem_alkali": (48, 48),
    "chem_cool": (48, 48),
    "chem_heat": (48, 48),
}

def crop_to_subject(img, threshold=10):
    """Crop image to non-transparent / non-background bounding box."""
    # Convert to RGBA
    img = img.convert("RGBA")
    # Get alpha channel
    alpha = img.split()[3]
    # Find bounding box where alpha > threshold
    bbox = alpha.point(lambda x: 255 if x > threshold else 0).getbbox()
    if bbox:
        # Add small padding
        pad = 2
        left = max(0, bbox[0] - pad)
        top = max(0, bbox[1] - pad)
        right = min(img.width, bbox[2] + pad)
        bottom = min(img.height, bbox[3] + pad)
        return img.crop((left, top, right, bottom))
    return img

def make_background_transparent(img, bg_threshold=30):
    """Make near-black or near-white background transparent."""
    img = img.convert("RGBA")
    pixels = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            # If pixel is near-black (dark background) or near-white (light background)
            is_dark = r < bg_threshold and g < bg_threshold and b < bg_threshold
            is_light = r > 240 and g > 240 and b > 240
            if is_dark or is_light:
                pixels[x, y] = (r, g, b, 0)
    return img

def process_tile(name, raw_filename, target_size):
    """Process a single tile from raw AI image to game-ready pixel tile."""
    raw_path = os.path.join(WORK_DIR, raw_filename)
    img = Image.open(raw_path)
    print(f"Processing {name}: raw size {img.size}")

    # Step 1: Make background transparent
    img = make_background_transparent(img)

    # Step 2: Crop to subject
    img = crop_to_subject(img)
    print(f"  Cropped to: {img.size}")

    # Step 3: Resize to target with NEAREST (pixel-perfect downscale)
    img = img.resize(target_size, Image.NEAREST)
    print(f"  Resized to: {img.size}")

    # Step 4: Quantize colors to reduce palette (optional, keeps pixel art feel)
    # Convert to RGB for quantization, then back to RGBA
    img_rgb = img.convert("RGB")
    # Quantize to 32 colors
    img_q = img_rgb.quantize(colors=32, method=Image.MEDIANCUT).convert("RGBA")
    # Restore alpha from original
    alpha = img.split()[3]
    img_q.putalpha(alpha)

    # Save
    out_path = os.path.join(WORK_DIR, f"{name}.png")
    img_q.save(out_path, "PNG")
    print(f"  Saved: {out_path}")
    return img_q

def main():
    results = {}
    for name, size in TILE_SIZES.items():
        raw_name = f"{name}_raw.png"
        raw_path = os.path.join(WORK_DIR, raw_name)
        if os.path.exists(raw_path):
            result = process_tile(name, raw_name, size)
            results[name] = result
        else:
            print(f"WARNING: {raw_name} not found")

    # Create a preview contact sheet
    preview_dir = os.path.join(WORK_DIR, "preview")
    os.makedirs(preview_dir, exist_ok=True)

    # Scale up 4x for preview
    for name, img in results.items():
        preview = img.resize((img.width * 8, img.height * 8), Image.NEAREST)
        preview_path = os.path.join(preview_dir, f"{name}_8x.png")
        preview.save(preview_path, "PNG")
        print(f"Preview saved: {preview_path}")

if __name__ == "__main__":
    main()
