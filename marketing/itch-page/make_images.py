"""Crops game frames into the itch.io page images: covers 630x500, header strips, screenshots."""
import sys
from pathlib import Path
from PIL import Image

repo = Path(sys.argv[1])
out = Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)

frames = repo / "marketing/capsule-mockups/frames"
style = repo / "marketing/capsule-refs/style"
saves = repo / "docs/mockups/saves-2026-10-09"
isle = repo / "media/screenshots/Vitaria_3_colony_valley/Vitaria_4_colony_island/final"

src = {
    "island": frames / "v2-island-a.png",      # 2560x1440, island in clouds
    "chain": frames / "v1-chain-a.png",        # 2560x1440, colony close
    "troll_b": frames / "v3-troll-b.png",      # 2560x1440, troll by the market
    "arena": style / "07_arena.png",           # 1920x1080, hex arena
    "hud": saves / "now-hud.png",              # 2560x1440? HUD frame
    "reward": saves / "v3-reward.png",
    "max": isle / "colony_isle_max.png",       # 1920x1080
    "sky": style / "03_sky_islands_clouds.png",
}


def load(key):
    im = Image.open(src[key]).convert("RGB")
    print(key, im.size)
    return im


def crop_rel(im, cx, cy, w_frac, aspect):
    """Crop around a relative centre with a width fraction of the image and a target aspect (w/h)."""
    W, H = im.size
    w = int(W * w_frac)
    h = int(w / aspect)
    if h > H:
        h = H
        w = int(h * aspect)
    x0 = min(max(int(W * cx - w / 2), 0), W - w)
    y0 = min(max(int(H * cy - h / 2), 0), H - h)
    return im.crop((x0, y0, x0 + w, y0 + h))


def save(im, name, size, quality=86):
    im = im.resize(size, Image.LANCZOS)
    path = out / name
    im.save(path, "JPEG", quality=quality, optimize=True, progressive=True)
    print("  ->", path.name, im.size, path.stat().st_size // 1024, "KB")


island = load("island")
chain = load("chain")
troll = load("troll_b")
arena = load("arena")
hud = load("hud")
reward = load("reward")
maxi = load("max")
sky = load("sky")

# Covers 630x500 (63:50)
save(crop_rel(island, 0.555, 0.47, 0.62, 1.26), "cover-island.jpg", (630, 500))
save(crop_rel(chain, 0.48, 0.42, 0.66, 1.26), "cover-chain.jpg", (630, 500))
save(crop_rel(troll, 0.50, 0.42, 0.50, 1.26), "cover-troll.jpg", (630, 500))

# Header strips (wide), shown with object-fit: cover
save(crop_rel(island, 0.5, 0.40, 1.0, 1920 / 620), "header-island.jpg", (1920, 620))
save(crop_rel(chain, 0.5, 0.30, 1.0, 1920 / 620), "header-chain.jpg", (1920, 620))
save(crop_rel(troll, 0.5, 0.33, 1.0, 1920 / 620), "header-troll.jpg", (1920, 620))

# Page backgrounds
save(crop_rel(sky, 0.5, 0.5, 1.0, 16 / 9), "bg-sky.jpg", (1920, 1080), quality=80)

# Screenshots 1280x720
save(crop_rel(chain, 0.5, 0.5, 1.0, 16 / 9), "shot-colony.jpg", (1280, 720))
save(crop_rel(island, 0.5, 0.5, 1.0, 16 / 9), "shot-island.jpg", (1280, 720))
save(crop_rel(arena, 0.5, 0.5, 1.0, 16 / 9), "shot-arena.jpg", (1280, 720))
save(crop_rel(hud, 0.5, 0.5, 1.0, 16 / 9), "shot-hud.jpg", (1280, 720))
save(crop_rel(reward, 0.5, 0.5, 1.0, 16 / 9), "shot-reward.jpg", (1280, 720))
save(crop_rel(maxi, 0.5, 0.5, 1.0, 16 / 9), "shot-max.jpg", (1280, 720))
save(crop_rel(troll, 0.5, 0.5, 1.0, 16 / 9), "shot-troll.jpg", (1280, 720))
