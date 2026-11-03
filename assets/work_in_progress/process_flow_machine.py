"""Process new flow machine image to game-ready size."""
from PIL import Image
import os

WORK = r"E:\code\FluxForge\assets\work_in_progress"
SRC = os.path.join(WORK, "flow_machine_new_raw.png")
DST = r"E:\code\FluxForge\assets\flow_machine_new.png"
TARGET_SIZE = (640, 960)  # Match existing flow_machine.png

img = Image.open(SRC).convert("RGBA")
print(f"Raw: {img.size}")

# Make white background transparent
pixels = img.load()
w, h = img.size
for y in range(h):
    for x in range(w):
        r, g, b, a = pixels[x, y]
        # White / near-white background
        if r > 240 and g > 240 and b > 240:
            pixels[x, y] = (r, g, b, 0)

# Crop to subject
alpha = img.split()[3]
bbox = alpha.point(lambda x: 255 if x > 20 else 0).getbbox()
if bbox:
    img = img.crop(bbox)
    print(f"Cropped: {img.size}")

# Resize to target with high-quality downscale (LANCZOS for smooth, then we can pixelate if needed)
img = img.resize(TARGET_SIZE, Image.LANCZOS)
print(f"Resized: {img.size}")

# Save
img.save(DST, "PNG")
print(f"Saved: {DST}")

# Also save an 8x pixelated preview
preview = img.resize((TARGET_SIZE[0]//4, TARGET_SIZE[1]//4), Image.NEAREST)
preview_path = os.path.join(WORK, "flow_machine_new_preview.png")
preview.save(preview_path, "PNG")
print(f"Preview saved: {preview_path}")
