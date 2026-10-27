#!/usr/bin/env python3
"""Installs Sanctuary's client mods into a Free Realms client (or removes them).

UI movies are loaded synchronously from the local asset packs, and only when their crc
matches the asset manifest the client downloaded from the asset server. So installing:
  - writes the built mods (src/Resources/ClientMods) into a new Assets_NNN.pack, and
  - switches the UI window definitions to the renamed mod movies.
Sanctuary's WebAPI publishes the same files in the manifest it serves, which keeps them "current".

Originals are backed up next to each file as <name>.sanctuary-backup.

Examples:
  python install_client_mods.py --client "%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client"
  python install_client_mods.py --client "..." --uninstall
"""

import shutil
import struct
import zlib
from argparse import ArgumentParser
from pathlib import Path

script_dir = Path(__file__).resolve().parent
mods_dir = script_dir.parent / "src" / "Resources" / "ClientMods"

BACKUP_SUFFIX = ".sanctuary-backup"
MARKER_FILE = "sanctuary_client_mods.txt"

COMPRESSED_MAGIC = 0xA1B2C3D4
PACK_DATA_ALIGNMENT = 8192

# (window definition, [(original text, modded text), ...]), applied to the original file.
WINDOW_PATCHES = [
    (Path("UI/UiModules/Main/wndChatLog.xml"), [
        ("UI\\chatWindowCtrl.swf", "UI\\sanctuaryChatWindowCtrl.swf"),
        # The client clips a movie to its window, so the chat window is enlarged to about the size of
        # the screen to give the Referee Tools panel room. Its Lua bottom-aligns the window and the mod
        # draws chat 1:1 at the bottom left, so chat itself stays where it was.
        ('Width="550" Height="345"', 'Width="2000" Height="1045"'),
    ]),
    (Path("UI/UiModules/Main/wndGameDock.xml"), [
        ("UI\\gamedock.swf", "UI\\sanctuaryGameDock.swf"),
    ]),
]


def load_mods():
    # Packs hold assets uncompressed under their plain names.
    mods = []
    for path in sorted(mods_dir.iterdir()):
        data = path.read_bytes()
        name = path.name
        if name.endswith(".z"):
            magic, _ = struct.unpack(">II", data[:8])
            if magic != COMPRESSED_MAGIC:
                raise ValueError(f"{name} is not a compressed asset")
            data = zlib.decompress(data[8:])
            name = name[:-2]
        mods.append((name, data))
    return mods


def write_pack(path, assets):
    # Single chunk: next chunk offset (0 = last), entry count, then
    # (name length, name, data offset, data size, crc32) per entry, all big-endian.
    index = struct.pack(">II", 0, len(assets))
    index_size = len(index) + sum(4 + len(name.encode()) + 12 for name, _ in assets)

    offset = max(PACK_DATA_ALIGNMENT, index_size)
    blobs = []
    for name, data in assets:
        encoded = name.encode()
        index += struct.pack(">I", len(encoded)) + encoded + struct.pack(">III", offset, len(data), zlib.crc32(data))
        blobs.append(data)
        offset += len(data)

    with path.open("wb") as f:
        f.write(index)
        f.write(b"\0" * (max(PACK_DATA_ALIGNMENT, index_size) - len(index)))
        for blob in blobs:
            f.write(blob)


def next_pack_path(client_dir):
    index = 0
    while (client_dir / f"Assets_{index:03d}.pack").exists():
        index += 1
    return client_dir / f"Assets_{index:03d}.pack"


def install(client_dir):
    mods = load_mods()
    if not mods:
        raise FileNotFoundError(f"No built mods in {mods_dir}; run build_client_mods.py first.")

    marker = client_dir / MARKER_FILE
    pack_path = client_dir / marker.read_text().strip() if marker.exists() else next_pack_path(client_dir)

    write_pack(pack_path, mods)
    marker.write_text(pack_path.name)
    print(f"{pack_path.name}: {', '.join(name for name, _ in mods)}")

    for relative_path, replacements in WINDOW_PATCHES:
        path = client_dir / relative_path
        backup = path.with_name(path.name + BACKUP_SUFFIX)

        if not backup.exists():
            shutil.copy2(path, backup)

        # Always patch the original, so reinstalling (or upgrading an older install) is idempotent.
        text = backup.read_text(encoding="utf-8-sig")

        for original, modded in replacements:
            if original not in text:
                raise ValueError(f"{relative_path}: {original} not found, refusing to patch")
            text = text.replace(original, modded)

        # Keep the BOM the client's files are saved with.
        path.write_text(text, encoding="utf-8-sig")
        print(f"{relative_path}: patched ({len(replacements)} change(s))")


def uninstall(client_dir):
    for relative_path, _ in WINDOW_PATCHES:
        path = client_dir / relative_path
        backup = path.with_name(path.name + BACKUP_SUFFIX)

        if not backup.exists():
            print(f"{relative_path}: no backup, nothing to restore")
            continue

        shutil.copy2(backup, path)
        backup.unlink()
        print(f"{relative_path}: restored")

    marker = client_dir / MARKER_FILE
    if marker.exists():
        pack_path = client_dir / marker.read_text().strip()
        pack_path.unlink(missing_ok=True)
        marker.unlink()
        print(f"{pack_path.name}: removed")


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--client", type=Path, required=True, help="Free Realms client folder (contains FreeRealms.exe)")
    parser.add_argument("--uninstall", action="store_true", help="Remove the mods and restore the original UI")
    args = parser.parse_args()

    if not (args.client / "FreeRealms.exe").exists():
        raise FileNotFoundError(f"FreeRealms.exe not found in {args.client}")

    if args.uninstall:
        uninstall(args.client)
    else:
        install(args.client)


if __name__ == "__main__":
    main()
