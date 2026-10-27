"""Draws the Sanctuary loading screen art: the OSFR tree with "Open Source" arched over it and
"Free Realms" curved under it, in the client's own Free Realms font.

build_client_mods.py renders it at build time with the font exported from gfxfontlib.gfx, so no
game font is stored here. To preview the art on its own:
  python background.py <FreeRealmsDemiBold.ttf> preview.png
"""

import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

LOGO_PATH = Path(__file__).resolve().parent / "osfr_logo.png"

# The stock loading screen's stage.
WIDTH, HEIGHT = 1024, 768
SUPERSAMPLE = 2

LOGO_CENTER = (512, 318)
LOGO_WIDTH = 300

# (text, font size, baseline radius from the logo center, arc above the logo, gradient top, gradient bottom)
ARCS = [
    ("Open Source", 66, 196, True, (255, 255, 255), (186, 176, 255)),
    ("Free Realms", 82, 252, False, (255, 232, 140), (242, 126, 38)),
]

OUTLINE = (24, 10, 58)
OUTLINE_WIDTH = 7


def lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def vertical_gradient(size, top, bottom):
    width, height = size
    column = Image.new("RGB", (1, height))
    for y in range(height):
        column.putpixel((0, y), lerp(top, bottom, y / max(height - 1, 1)))
    return column.resize(size)


def soft_shadow(image, color, opacity, blur, offset):
    """A blurred silhouette of image, on a canvas padded so the blur isn't clipped."""
    pad = blur * 3
    shadow = Image.new("RGBA", (image.width + pad * 2, image.height + pad * 2), color + (0,))
    alpha = Image.new("L", shadow.size, 0)
    alpha.paste(image.getchannel("A").point(lambda a: round(a * opacity)), (pad, pad))
    shadow.putalpha(alpha.filter(ImageFilter.GaussianBlur(blur)))
    return shadow, (offset[0] - pad, offset[1] - pad)


def draw_backdrop(scale):
    size = (WIDTH * scale, HEIGHT * scale)
    image = vertical_gradient(size, (52, 30, 112), (12, 12, 40)).convert("RGBA")

    cx, cy, radius = LOGO_CENTER[0] * scale, LOGO_CENTER[1] * scale, 330 * scale
    glow = Image.new("L", size, 0)
    ImageDraw.Draw(glow).ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=140)
    layer = Image.new("RGBA", size, (150, 120, 255, 255))
    layer.putalpha(glow.filter(ImageFilter.GaussianBlur(radius * 0.45)))
    image.alpha_composite(layer)

    # Fixed seed so every build draws the same sky.
    stars = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(stars)
    rng = random.Random(1337)
    for _ in range(140):
        x, y = rng.uniform(0, size[0]), rng.uniform(0, size[1] * 0.8)
        r = rng.uniform(0.6, 1.8) * scale
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(255, 255, 255, rng.randint(60, 200)))
    image.alpha_composite(stars)

    # Darken the bottom so the tip text and loading bar stay readable.
    shade = Image.new("L", (1, 256))
    for y in range(256):
        shade.putpixel((0, y), round(200 * max(0.0, (y - 150) / 105) ** 1.4))
    shade = shade.resize((size[0], 240 * scale))
    band = Image.new("RGBA", shade.size, (6, 4, 20, 255))
    band.putalpha(shade)
    image.alpha_composite(band, (0, size[1] - band.height))
    return image


def render_glyph(char, font, top, bottom):
    """Draws one outlined, gradient filled glyph with its baseline midpoint at the cell's center."""
    outline_width = OUTLINE_WIDTH * font.size // 80 + 2
    pad = outline_width * 2
    ascent, descent = font.getmetrics()
    half = max(ascent, descent) + pad
    width = round(font.getlength(char)) + pad * 2
    cell = (width, half * 2)
    origin = (width / 2, half)

    outline = Image.new("L", cell, 0)
    ImageDraw.Draw(outline).text(origin, char, font=font, anchor="ms", fill=255,
                                 stroke_width=outline_width, stroke_fill=255)
    fill = Image.new("L", cell, 0)
    ImageDraw.Draw(fill).text(origin, char, font=font, anchor="ms", fill=255)

    # Gradient runs cap height to baseline, so every letter shades the same way.
    cap_top = half + font.getbbox("H", anchor="ls")[1]
    gradient = Image.new("RGB", cell, bottom)
    gradient.paste(Image.new("RGB", (width, cap_top), top), (0, 0))
    gradient.paste(vertical_gradient((width, half - cap_top), top, bottom), (0, cap_top))

    glyph = Image.new("RGBA", cell, OUTLINE + (0,))
    glyph.putalpha(outline)
    glyph.paste(gradient, (0, 0), fill)
    return glyph


def draw_arc(layer, text, font, radius, above, top, bottom, scale):
    cx, cy = LOGO_CENTER[0] * scale, LOGO_CENTER[1] * scale
    advances = [font.getlength(text[:i + 1]) - font.getlength(text[:i]) for i in range(len(text))]
    total = sum(advances)

    # Space letters along the middle of the x-height rather than the baseline, so they read evenly
    # (letters fan out above the top arc's baseline and close in above the bottom arc's).
    middle = -font.getbbox("x", anchor="ls")[1] / 2
    spacing_radius = radius + middle if above else radius - middle

    offset = 0.0
    for char, advance in zip(text, advances):
        angle = (offset + advance / 2 - total / 2) / spacing_radius
        offset += advance
        if char == " ":
            continue
        glyph = render_glyph(char, font, top, bottom)
        if above:
            x, y = cx + radius * math.sin(angle), cy - radius * math.cos(angle)
            glyph = glyph.rotate(-math.degrees(angle), resample=Image.BICUBIC, expand=True)
        else:
            x, y = cx + radius * math.sin(angle), cy + radius * math.cos(angle)
            glyph = glyph.rotate(math.degrees(angle), resample=Image.BICUBIC, expand=True)
        layer.alpha_composite(glyph, (round(x - glyph.width / 2), round(y - glyph.height / 2)))


def render(font_path):
    scale = SUPERSAMPLE
    image = draw_backdrop(scale)

    logo = Image.open(LOGO_PATH).convert("RGBA")
    logo_width = LOGO_WIDTH * scale
    logo = logo.resize((logo_width, round(logo.height * logo_width / logo.width)), Image.LANCZOS)
    logo_pos = (LOGO_CENTER[0] * scale - logo.width // 2, LOGO_CENTER[1] * scale - logo.height // 2)
    shadow, shadow_pos = soft_shadow(logo, (8, 4, 30), 0.7, 10 * scale, (logo_pos[0], logo_pos[1] + 8 * scale))
    image.alpha_composite(shadow, shadow_pos)
    image.alpha_composite(logo, logo_pos)

    text = Image.new("RGBA", image.size, (0, 0, 0, 0))
    for words, size, radius, above, top, bottom in ARCS:
        font = ImageFont.truetype(str(font_path), size * scale)
        draw_arc(text, words, font, radius * scale, above, top, bottom, scale)

    shadow, shadow_pos = soft_shadow(text, (6, 2, 24), 0.75, 5 * scale, (0, 5 * scale))
    image.alpha_composite(shadow, shadow_pos)
    image.alpha_composite(text)

    return image.resize((WIDTH, HEIGHT), Image.LANCZOS).convert("RGB")


if __name__ == "__main__":
    render(sys.argv[1]).save(sys.argv[2])
