#!/usr/bin/env python3
"""Extracts English names for items, NPCs and houses from a Free Realms client's locale.

Writes src/Resources/Names.json ({"<NameId>": "<text>"}), which the server uses for
GM lookups (item/NPC/house search in the referee panel).

Example:
  python extract_names.py --client "%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client"
"""

import json
from argparse import ArgumentParser
from pathlib import Path

script_dir = Path(__file__).resolve().parent
resources_dir = script_dir.parent / "src" / "Resources"

MASK = 0xFFFFFFFF


def _mix(a, b, c):
    a = (a - b - c) & MASK; a ^= c >> 13
    b = (b - c - a) & MASK; b ^= (a << 8) & MASK
    c = (c - a - b) & MASK; c ^= b >> 13
    a = (a - b - c) & MASK; a ^= c >> 12
    b = (b - c - a) & MASK; b ^= (a << 16) & MASK
    c = (c - a - b) & MASK; c ^= b >> 5
    a = (a - b - c) & MASK; a ^= c >> 3
    b = (b - c - a) & MASK; b ^= (a << 10) & MASK
    c = (c - a - b) & MASK; c ^= b >> 15
    return a, b, c


def lookup2(key):
    """Bob Jenkins' lookup2 hash, which keys the client's locale entries."""
    a = b = 0x9E3779B9
    c = 0
    length = len(key)
    i = 0
    remaining = length
    while remaining >= 12:
        a = (a + key[i] + (key[i + 1] << 8) + (key[i + 2] << 16) + (key[i + 3] << 24)) & MASK
        b = (b + key[i + 4] + (key[i + 5] << 8) + (key[i + 6] << 16) + (key[i + 7] << 24)) & MASK
        c = (c + key[i + 8] + (key[i + 9] << 8) + (key[i + 10] << 16) + (key[i + 11] << 24)) & MASK
        a, b, c = _mix(a, b, c)
        i += 12
        remaining -= 12
    c = (c + length) & MASK
    tail = key[i:]
    for j, shift in ((10, 24), (9, 16), (8, 8)):
        if remaining > j:
            c = (c + (tail[j] << shift)) & MASK
    for j, shift in ((7, 24), (6, 16), (5, 8), (4, 0)):
        if remaining > j:
            b = (b + (tail[j] << shift)) & MASK
    for j, shift in ((3, 24), (2, 16), (1, 8), (0, 0)):
        if remaining > j:
            a = (a + (tail[j] << shift)) & MASK
    return _mix(a, b, c)[2]


def load_locale(client_dir):
    locale_dir = client_dir / "Locale"
    data = (locale_dir / "en_us_data.dat").read_bytes()
    entries = {}
    for line in (locale_dir / "en_us_data.dir").read_text("utf-8", "replace").splitlines():
        parts = line.split("\t")
        if len(parts) >= 3 and parts[0].isdigit():
            offset, length = int(parts[1]), int(parts[2])
            # Entries look like "<hash>\t<flags>\t<text>".
            entries[int(parts[0])] = data[offset:offset + length].decode("utf-8", "replace").split("\t")[-1].strip()
    return entries


def wanted_name_ids():
    ids = set()
    for item in json.loads((resources_dir / "ClientItemDefinitions.json").read_text("utf-8")):
        ids.add(item.get("NameId", 0))
    for npc in json.loads((resources_dir / "Npcs.json").read_text("utf-8")):
        ids.add(npc.get("NameId", 0))
    for house in json.loads((resources_dir / "Houses.json").read_text("utf-8-sig")):
        ids.add(house.get("NameId", 0))
    ids.discard(0)
    return ids


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--client", type=Path, required=True, help="Free Realms client folder (contains Locale)")
    args = parser.parse_args()

    locale = load_locale(args.client)

    names = {}
    for name_id in sorted(wanted_name_ids()):
        text = locale.get(lookup2(f"Global.Text.{name_id}".encode()))
        if text:
            names[str(name_id)] = text

    output = resources_dir / "Names.json"
    output.write_text(json.dumps(names, ensure_ascii=False, indent=0), encoding="utf-8")
    print(f"Wrote {len(names)} names to {output}")


if __name__ == "__main__":
    main()
