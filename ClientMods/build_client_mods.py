#!/usr/bin/env python3
"""Builds Sanctuary's client mods into src/Resources/ClientMods.

The WebAPI asset endpoint serves every file in that folder and patches it into the asset
manifest it hands the client. The client only downloads assets missing from its local packs,
so mods are published under new names, and install_client_mods.py points the client's UI at them.

Mods:
  sanctuaryChatWindowCtrl.gfx (+ .lst.z) - chat window with the referee (GM) panel
                                           (chatWindowCtrl/gm_panel.as appended).
  sanctuaryGameDock.gfx (+ .lst.z)       - main hub with a purple Referee Tools tree that opens the panel
                                           (gamedock/gm_hub_button.as appended, gmTreeIcon added).
  sanctuaryDialog.gfx (+ .lst.z)         - NPC dialog that stays hidden when sent empty, so GM conversations
                                           get its camera zoom with the panel's text (dialog/dialog_hide.as).
  sanctuaryHousingEditPanel.gfx (+ .lst.z) - decorate-mode furniture tray with 10 items per page and a
                                           search box (housing/tray.as).

The stock movies are read from your Free Realms client's local asset packs (--client), or from a
local copy of the asset server (--assets). Nothing from the game is stored in this repository.

Requires JPEXS FFDec (ffdec.jar), Java and Pillow. See README.md.

Example:
  python build_client_mods.py --client "%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client" --ffdec path/to/ffdec.jar
"""

import io
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import zlib
from argparse import ArgumentParser
from pathlib import Path

from PIL import Image

script_dir = Path(__file__).resolve().parent
output_dir = script_dir.parent / "src" / "Resources" / "ClientMods"

COMPRESSED_MAGIC = 0xA1B2C3D4

# (exported symbol to copy, export name of the copy, hue shift in degrees)
GM_TREE_ICON = ("treeIcon", "gmTreeIcon", 155)
REDEEM_TREE_ICON = ("treeIcon", "gmRedeemIcon", 35)

# (stock movie, published name, script inside the movie to append to, patch source, recolored symbols to add)
MODS = [
    ("chatWindowCtrl", "sanctuaryChatWindowCtrl", "\\frame_2\\DoAction", script_dir / "chatWindowCtrl" / "gm_panel.as", []),
    ("gamedock", "sanctuaryGameDock", "\\frame_1\\DoAction", script_dir / "gamedock" / "gm_hub_button.as", [GM_TREE_ICON, REDEEM_TREE_ICON]),
    ("dialog", "sanctuaryDialog", "\\frame_1\\DoAction", script_dir / "dialog" / "dialog_hide.as", []),
    ("housingEditPanel", "sanctuaryHousingEditPanel", "\\frame_1\\DoAction", script_dir / "housing" / "tray.as", []),
]

# ffdec's XML export puts each top-level tag on its own line at this indentation.
TAG_PREFIX = "\n    <item type="
DEFINITION_ID = r'(?:spriteId|shapeId|characterID|buttonId)="(\d+)"'
ID_ATTRIBUTE = r'\b(spriteId|shapeId|characterID|characterId|bitmapId|buttonId)="(\d+)"'


class ClientPacks:
    """Stock assets from a client's Assets_NNN.pack files (stored uncompressed under their plain names)."""

    def __init__(self, client_dir):
        self.entries = {}
        # Later packs win, like the client.
        for pack in sorted(client_dir.glob("Assets_*.pack")):
            with pack.open("rb") as f:
                offset = 0
                while True:
                    f.seek(offset)
                    next_offset, count = struct.unpack(">II", f.read(8))
                    for _ in range(count):
                        name_length, = struct.unpack(">I", f.read(4))
                        name = f.read(name_length).decode("latin-1")
                        data_offset, size, _ = struct.unpack(">III", f.read(12))
                        self.entries[name] = (pack, data_offset, size)
                    if next_offset == 0:
                        break
                    offset = next_offset
        if not self.entries:
            raise FileNotFoundError(f"No Assets_*.pack files in {client_dir}")

    def read(self, name):
        if name not in self.entries:
            raise FileNotFoundError(f"{name} not found in the client's asset packs")
        pack, offset, size = self.entries[name]
        with pack.open("rb") as f:
            f.seek(offset)
            return f.read(size)


class AssetMirror:
    """Stock assets from a local copy of the asset server: <bucket>/<name>, large files as <name>.z."""

    def __init__(self, assets_dir):
        self.assets_dir = assets_dir

    def read(self, name):
        # The bucket isn't derivable from the name, so search them all.
        for candidate in self.assets_dir.glob(f"*/{name}"):
            return candidate.read_bytes()
        for candidate in self.assets_dir.glob(f"*/{name}.z"):
            return decompress(candidate.read_bytes())
        raise FileNotFoundError(f"{name} not found in {self.assets_dir}")


def decompress(data):
    magic, size = struct.unpack(">II", data[:8])
    if magic != COMPRESSED_MAGIC:
        raise ValueError("not a compressed asset")
    return zlib.decompress(data[8:])


def compress(data):
    return struct.pack(">II", COMPRESSED_MAGIC, len(data)) + zlib.compress(data, 9)


def load_texture(stock, file_name):
    return Image.open(io.BytesIO(stock.read(file_name))).convert("RGBA")


def hue_shift(image, degrees):
    alpha = image.getchannel("A")
    hue, saturation, value = image.convert("RGB").convert("HSV").split()
    hue = hue.point(lambda x: (x + round(degrees * 256 / 360)) % 256)
    shifted = Image.merge("HSV", (hue, saturation, value)).convert("RGB")
    shifted.putalpha(alpha)
    return shifted


def lossless_tag(character_id, image):
    # DefineBitsLossless2 holds zlib'd, premultiplied ARGB.
    argb = bytearray()
    for r, g, b, a in image.convert("RGBA").getdata():
        argb += bytes((a, r * a // 255, g * a // 255, b * a // 255))
    return (f'{TAG_PREFIX}"DefineBitsLossless2Tag" bitmapColorTableSize="0" bitmapFormat="5" '
            f'bitmapHeight="{image.height}" bitmapWidth="{image.width}" characterID="{character_id}" '
            f'forceWriteAsLong="true" zlibBitmapData="{zlib.compress(bytes(argb), 9).hex()}"/>')


def add_recolored_symbol(xml, stock, export_name, new_export_name, degrees):
    """Exports a copy of a symbol under a new name, with its external textures hue shifted and embedded."""
    tags = {}
    for match in re.finditer(re.escape(TAG_PREFIX) + r'"(\w+)"[^>]*?' + DEFINITION_ID, xml):
        end = xml.find(TAG_PREFIX, match.start() + 1)
        tags.setdefault(match.group(2), (match.group(1), xml[match.start():end]))

    export = re.search(r'<tags>\s*<item>(\d+)</item>\s*</tags>\s*<names>\s*<item>' + re.escape(export_name) + r'</item>', xml)
    if export is None:
        raise ValueError(f"{export_name} is not exported")

    # Dependencies first, so every copy is defined before it's used.
    order = []

    def visit(character_id):
        if character_id in order or character_id not in tags:
            return
        for child in re.findall(r'(?:characterId|bitmapId)="(\d+)"', tags[character_id][1]):
            if child != character_id:
                visit(child)
        order.append(character_id)

    visit(export.group(1))

    next_id = max(int(x) for x in re.findall(DEFINITION_ID, xml)) + 1
    remap = {old: str(next_id + i) for i, old in enumerate(order)}

    copies = []
    for old in order:
        tag_type, text = tags[old]
        if tag_type == "DefineExternalImage":
            image = load_texture(stock, re.search(r'fileName="([^"]*)"', text).group(1))
            copies.append(lossless_tag(remap[old], hue_shift(image, degrees)))
        else:
            copies.append(re.sub(ID_ATTRIBUTE, lambda m: f'{m.group(1)}="{remap.get(m.group(2), m.group(2))}"', text))

    copies.append(f'{TAG_PREFIX}"ExportAssetsTag" forceWriteAsLong="true">\n'
                  f'      <tags>\n        <item>{remap[export.group(1)]}</item>\n      </tags>\n'
                  f'      <names>\n        <item>{new_export_name}</item>\n      </names>\n    </item>')

    insert_at = xml.find(TAG_PREFIX, export.end())
    return xml[:insert_at] + "".join(copies) + xml[insert_at:]


def ffdec(java, jar, *args):
    result = subprocess.run([str(java), "-jar", str(jar), *map(str, args)], capture_output=True, text=True)
    if result.returncode != 0:
        print(result.stdout)
        print(result.stderr, file=sys.stderr)
        raise RuntimeError(f"ffdec failed: {' '.join(map(str, args))}")


def build_movie(java, jar, stock, stock_name, script_name, patch_path, symbols, output_path):
    with tempfile.TemporaryDirectory() as temp:
        temp = Path(temp)

        stock_path = temp / f"{stock_name}.gfx"
        stock_path.write_bytes(stock.read(f"{stock_name}.gfx"))

        if symbols:
            ffdec(java, jar, "-swf2xml", stock_path, temp / "stock.xml")
            xml = (temp / "stock.xml").read_text(encoding="utf-8")
            for symbol in symbols:
                xml = add_recolored_symbol(xml, stock, *symbol)
            (temp / "stock.xml").write_text(xml, encoding="utf-8")
            ffdec(java, jar, "-xml2swf", temp / "stock.xml", temp / "stock.gfx")
            stock_path = temp / "stock.gfx"

        ffdec(java, jar, "-export", "script", temp / "export", stock_path)

        script_path = temp / "export" / "scripts" / Path(*script_name.strip("\\").split("\\")).with_suffix(".as")
        if not script_path.exists():
            raise FileNotFoundError(f"{script_name} not found in {stock_path.name}")

        patched = temp / "patched.as"
        patched.write_text(
            script_path.read_text(encoding="utf-8") + "\n" + patch_path.read_text(encoding="utf-8"),
            encoding="utf-8")

        ffdec(java, jar, "-replace", stock_path, output_path, script_name, patched)


def build_list(stock, stock_name, published_name, output_path):
    # The .lst preloads the movie's images and ends with the movie itself.
    text = stock.read(f"{stock_name}.lst").decode("latin-1")
    text = text.replace(f"{stock_name}.gfx", f"{published_name}.gfx")
    output_path.write_bytes(compress(text.encode("latin-1")))


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--client", type=Path, help="Free Realms client folder (reads its Assets_*.pack files)")
    source.add_argument("--assets", type=Path, help="Local copy of the asset server (<bucket>/<name> folders)")
    parser.add_argument("--ffdec", type=Path, required=True, help="Path to ffdec.jar")
    parser.add_argument("--java", type=Path, default=Path(shutil.which("java") or "java"), help="Java executable")
    args = parser.parse_args()

    stock = ClientPacks(args.client) if args.client else AssetMirror(args.assets)

    output_dir.mkdir(parents=True, exist_ok=True)

    for stock_name, published_name, script_name, patch_path, symbols in MODS:
        movie_path = output_dir / f"{published_name}.gfx"
        list_path = output_dir / f"{published_name}.lst.z"

        build_movie(args.java, args.ffdec, stock, stock_name, script_name, patch_path, symbols, movie_path)
        build_list(stock, stock_name, published_name, list_path)

        print(f"Built {movie_path.name} ({movie_path.stat().st_size} bytes) and {list_path.name}")


if __name__ == "__main__":
    main()
