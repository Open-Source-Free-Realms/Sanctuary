#!/usr/bin/env python3
"""Publishes a renamed copy of a client zone into src/Resources/ClientMods.

A zone is its <zone>.gzne, <zone>_<x>_<z>.gcnk/.gck2 chunks and optional <zone>_Tile_*.dds textures
listed in <zone>_TileInfo.txt. None of those files contain the zone's own name except TileInfo.txt,
so a fork is the same files under the new name. The WebAPI adds them to the asset manifest, and the
client downloads them the first time it loads the forked zone.

Only zones whose files are all in the client's local packs can be forked: the upstream asset server
doesn't serve the rest. The default source, hsg_hum_chromakey_01, is a 4-chunk zone that is.

A matching src/Resources/Zones/<name>.json makes it a Sanctuary zone (see gm_fork.json).
"""

import re
import struct
import zlib
from argparse import ArgumentParser
from pathlib import Path

COMPRESSED_MAGIC = 0xA1B2C3D4

output_dir = Path(__file__).resolve().parent.parent / "src" / "Resources" / "ClientMods"


def read_pack_index(client_dir):
    """Every asset in the client's Assets*.pack files, by lowercase name. Later packs win, like the client."""
    entries = {}
    for pack in sorted(client_dir.glob("Assets*.pack")):
        with pack.open("rb") as f:
            offset = 0
            while True:
                f.seek(offset)
                next_offset, count = struct.unpack(">II", f.read(8))
                for _ in range(count):
                    name_length, = struct.unpack(">I", f.read(4))
                    name = f.read(name_length).decode("latin-1")
                    data_offset, size, _ = struct.unpack(">III", f.read(12))
                    entries[name.lower()] = (pack, data_offset, size)
                if next_offset == 0:
                    break
                offset = next_offset
    return entries


def read_asset(entries, name):
    pack, offset, size = entries[name.lower()]
    with pack.open("rb") as f:
        f.seek(offset)
        return f.read(size)


def compress(data):
    return struct.pack(">II", COMPRESSED_MAGIC, len(data)) + zlib.compress(data, 9)


def zone_files(manifest_lines, zone):
    """The zone's manifest entries as (listed name, crc of the uncompressed asset)."""
    pattern = re.compile(re.escape(zone) + r"(\.gzne|_-?\d+_-?\d+\.gc(nk|k2)|_Tile_.+\.dds|_TileInfo\.txt)(\.z)?$", re.IGNORECASE)
    files = []
    for line in manifest_lines:
        if line.count(",") != 2:
            continue
        name, crc, _ = line.split(",")
        if pattern.match(name):
            files.append((name, int(crc)))
    return files


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--client", type=Path, required=True, help="Free Realms client folder")
    parser.add_argument("--source", default="hsg_hum_chromakey_01", help="Zone to copy")
    parser.add_argument("--name", default="gm_fork", help="Name of the copy")
    args = parser.parse_args()

    if not re.fullmatch(r"[a-z0-9_]+", args.name):
        parser.error("--name must be lowercase letters, digits and '_'")

    manifest_lines = (args.client / "Assets_manifest.txt").read_text(encoding="latin-1").splitlines()
    if any(line.lower().startswith(args.name + "_") or line.lower().startswith(args.name + ".") for line in manifest_lines):
        parser.error(f"The client already has assets named {args.name}")

    files = zone_files(manifest_lines, args.source)
    if not any(name.lower().startswith(args.source.lower() + ".gzne") for name, _ in files):
        parser.error(f"{args.source} is not a zone in the client's manifest")

    entries = read_pack_index(args.client)
    missing = [name for name, _ in files if name.removesuffix(".z").lower() not in entries]
    if missing:
        parser.error(f"{args.source} has {len(missing)} file(s) the client streams instead of shipping, e.g. {missing[0]}")

    output_dir.mkdir(parents=True, exist_ok=True)

    for listed_name, crc in files:
        plain_name = listed_name.removesuffix(".z")
        data = read_asset(entries, plain_name)

        if zlib.crc32(data) != crc:
            raise ValueError(f"{plain_name} in the packs doesn't match the manifest's crc")

        if plain_name.lower().endswith("_tileinfo.txt"):
            data = data.decode("latin-1").replace(args.source, args.name).encode("latin-1")

        fork_name = args.name + listed_name[len(args.source):]
        (output_dir / fork_name).write_bytes(compress(data) if listed_name.endswith(".z") else data)
        print(f"Wrote {fork_name}")

    print(f"Forked {args.source} to {args.name} ({len(files)} files). Rebuild the WebAPI to serve them.")


if __name__ == "__main__":
    main()
