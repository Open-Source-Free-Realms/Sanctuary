#!/usr/bin/env python3
"""Rewrites a zone's terrain chunks in src/Resources/ClientMods from a heightmap.

Each <zone>_<row>_<col>.gcnk covers 4x4 tiles of 64 units, at world x = col * 64 and z = row * 64.
It is "GCNK", version, then two zlib blocks:

- Tiles and render mesh: per-tile ecosystems, objects, lights, areas and groups, then the terrain
  mesh: tile count, one (index offset, index count, vertex offset, vertex count) batch per tile, a
  height image (bits per pixel, 0 in version 5), u16 batch-local indices, and 32-byte vertices:
  f32 x, y, z in world space, a D3DCOLOR normal (bytes z, y, x, 0), two colours, a texture UV for
  the chunk's .gck2 colour map and a detail UV, both u16 / 1024.
- Collision: per tile, u32 size, 12 bytes of padding and a serialized Bullet btQuantizedBvh over
  that tile's render triangles. Without it, players walk through the new ground.

Each <zone>_<row>_<col>.gck2 is the chunk's colour map: "GCK2", version, texture size, uncompressed
and compressed sizes, then a zlib DDS: 1024x1024 DXT1, 4 texels per unit. Columns run along +z and
rows along -x (row 0 is the chunk's far x edge), each tile taking 256x256 texels.

This replaces the mesh with a regular grid, rebuilds the collision trees and repaints the colour
maps by height and slope (--keep-colours keeps them). The tiles' objects are kept. Only version 5
chunks (no height image) are supported, which includes zones forked from hsg_hum_chromakey_01 with
fork_zone.py.

Rebuild the WebAPI to serve the new chunks; clients download them the next time they start.
"""

import json
import re
import struct
import zlib
from argparse import ArgumentParser
from pathlib import Path

import numpy as np

from fork_zone import COMPRESSED_MAGIC, compress, output_dir

TILE_SIZE = 64
CHUNK_TILES = 4
VERTEX_SIZE = 32

f32 = np.float32

zones_dir = Path(__file__).resolve().parent.parent / "src" / "Resources" / "Zones"


class QuantizedBvh:
    """Bullet 2.74's btQuantizedBvh build over a triangle mesh, reproducing its node order and bounds."""

    MAX_SUBTREE_SIZE_IN_BYTES = 2048
    NODE_SIZE = 16

    def __init__(self, positions, triangles):
        corners = positions[triangles]

        self.aabb_min = (corners.min(axis=(0, 1)) - f32(1)).astype(f32)
        self.aabb_max = (corners.max(axis=(0, 1)) + f32(1)).astype(f32)
        self.quantization = (f32(65533) / (self.aabb_max - self.aabb_min)).astype(f32)

        # Flat triangles get a minimum thickness, like btOptimizedBvh's triangle callback.
        tri_min = corners.min(axis=1)
        tri_max = corners.max(axis=1)
        thin = tri_max - tri_min < f32(0.002)
        tri_min = np.where(thin, tri_min - f32(0.001), tri_min)
        tri_max = np.where(thin, tri_max + f32(0.001), tri_max)

        self.leaf_min = self.quantize(tri_min, False)
        self.leaf_max = self.quantize(tri_max, True)
        self.leaf_value = np.arange(len(triangles))

        self.nodes = []
        self.subtrees = []
        self._build(0, len(triangles))

        if not self.subtrees:
            self._add_subtree(0)

    def quantize(self, points, is_max):
        v = ((points - self.aabb_min) * self.quantization).astype(f32)
        if is_max:
            return ((v + f32(1)).astype(np.int64) | 1) & 0xFFFF
        return v.astype(np.int64) & 0xFFFE

    def unquantize(self, q):
        return (q.astype(f32) / self.quantization + self.aabb_min).astype(f32)

    def _centers(self, start, end):
        return ((self.unquantize(self.leaf_max[start:end]) + self.unquantize(self.leaf_min[start:end])) * f32(0.5)).astype(f32)

    @staticmethod
    def _mean(values):
        # Bullet sums sequentially in single precision; cumsum does too, unlike sum's pairwise adds.
        return (np.cumsum(values, axis=0, dtype=f32)[-1] * (f32(1) / f32(len(values)))).astype(f32)

    def _splitting_axis(self, start, end):
        centers = self._centers(start, end)
        diff = (centers - self._mean(centers)).astype(f32)
        variance = np.cumsum((diff * diff).astype(f32), axis=0, dtype=f32)[-1] * (f32(1) / (f32(end - start) - f32(1)))
        x, y, z = variance
        return (2 if y < z else 1) if x < y else (2 if x < z else 0)

    def _split(self, start, end, axis):
        centers = self._centers(start, end)
        split_value = self._mean(centers)[axis]

        # Moves leaves above the mean to the front, swapping in Bullet's order.
        split = start
        for i in np.nonzero(centers[:, axis] > split_value)[0] + start:
            for array in (self.leaf_min, self.leaf_max, self.leaf_value):
                array[[i, split]] = array[[split, i]]
            split += 1

        count = end - start
        balance = count // 3
        if split <= start + balance or split >= end - 1 - balance:
            split = start + (count >> 1)
        return split

    def _build(self, start, end):
        first = len(self.nodes)

        if end - start == 1:
            self.nodes.append([self.leaf_min[start].copy(), self.leaf_max[start].copy(), int(self.leaf_value[start])])
            return

        split = self._split(start, end, self._splitting_axis(start, end))

        # Internal bounds merge the leaves' bounds unquantized and quantized again, which can grow them by 2.
        node_min = self.quantize(self.unquantize(self.leaf_min[start:end]), False).min(axis=0)
        node_max = self.quantize(self.unquantize(self.leaf_max[start:end]), True).max(axis=0)
        node = [node_min, node_max, 0]
        self.nodes.append(node)

        left = len(self.nodes)
        self._build(start, split)
        right = len(self.nodes)
        self._build(split, end)

        escape = len(self.nodes) - first
        if escape * self.NODE_SIZE > self.MAX_SUBTREE_SIZE_IN_BYTES:
            for child in (left, right):
                if self._subtree_size(child) * self.NODE_SIZE <= self.MAX_SUBTREE_SIZE_IN_BYTES:
                    self._add_subtree(child)
        node[2] = -escape

    def _subtree_size(self, index):
        value = self.nodes[index][2]
        return 1 if value >= 0 else -value

    def _add_subtree(self, index):
        node_min, node_max, _ = self.nodes[index]
        self.subtrees.append((node_min, node_max, index, self._subtree_size(index)))

    def serialize(self, template):
        """The tile's collision record, taking the fields Bullet leaves unchanged from an existing record."""
        buffer = bytearray(template[:16])
        for vector in (self.aabb_min, self.aabb_max, self.quantization):
            buffer += struct.pack("<4f", *vector, 0)
        buffer += template[64:192]
        struct.pack_into("<i", buffer, 68, len(self.nodes))
        struct.pack_into("<i", buffer, 180, len(self.subtrees))

        for node_min, node_max, value in self.nodes:
            buffer += struct.pack("<6Hi", *node_min, *node_max, value)
        for node_min, node_max, root, size in self.subtrees:
            buffer += struct.pack("<6Hii12x", *node_min, *node_max, root, size)

        return struct.pack("<I12x", len(buffer)) + buffer


def read_collision_records(block):
    records = []
    offset = 0
    while offset < len(block):
        size, = struct.unpack_from("<I", block, offset)
        records.append(block[offset + 16 : offset + 16 + size])
        offset += 16 + size
    return records


def read_chunk(path):
    blob = path.read_bytes()
    compressed = path.suffix == ".z"
    if compressed:
        magic, size = struct.unpack(">II", blob[:8])
        assert magic == COMPRESSED_MAGIC
        blob = zlib.decompress(blob[8:])

    magic, version, size, length = struct.unpack("<4sIII", blob[:16])
    if magic != b"GCNK":
        raise ValueError(f"{path.name} is not a GCNK chunk")
    payload = zlib.decompress(blob[16 : 16 + length])

    offset = 16 + length
    size, length = struct.unpack_from("<II", blob, offset)
    collision = zlib.decompress(blob[offset + 8 : offset + 8 + length])

    return version, payload, collision, compressed


def find_mesh(payload, tile_count):
    """Offset of the render mesh: the vertex array ends the block, indices and batches precede it."""
    for vertex_count in range(1, len(payload) // VERTEX_SIZE + 1):
        vertices = len(payload) - vertex_count * VERTEX_SIZE - 4
        if struct.unpack_from("<i", payload, vertices)[0] == vertex_count:
            break
    else:
        raise ValueError("vertex array not found")

    for index_count in range(0, vertices // 2):
        indices = vertices - index_count * 2 - 4
        if struct.unpack_from("<i", payload, indices)[0] == index_count:
            mesh = indices - 4 - tile_count * 16 - 4
            if mesh >= 4 and struct.unpack_from("<ii", payload, mesh) == (tile_count, 0) and \
                    struct.unpack_from("<i", payload, indices - 4)[0] == 0:
                return mesh
    raise ValueError("mesh batches not found (only version 5 chunks without a height image are supported)")


def tile_mesh(heights, x0, z0, tile_col, tile_row, step, colors):
    """Vertex bytes, batch-local indices and positions of one tile's grid."""
    cells = TILE_SIZE // step
    local = np.arange(cells + 1) * step
    lz, lx = np.meshgrid(local, local, indexing="ij")
    x = (x0 + lx).astype(f32)
    z = (z0 + lz).astype(f32)
    y = heights(x, z).astype(f32)

    # Central differences on the heightmap itself, so normals match across tile and chunk edges.
    dx = (heights(x + step, z) - heights(x - step, z)) / (2 * step)
    dz = (heights(x, z + step) - heights(x, z - step)) / (2 * step)
    length = np.sqrt(dx * dx + 1 + dz * dz)
    normal = [np.rint(n / length * 127 + 127).astype(np.uint8) for n in (-dz, np.ones_like(dx), -dx)]

    def texture_uv(value, tile):
        return tile * 256 + np.clip(np.rint(value * 4), 1, 255).astype(np.int64)

    vertex_bytes = bytearray()
    for v in np.ndindex(lz.shape):
        vertex_bytes += struct.pack(
            "<3f4B8s4H", x[v], y[v], z[v], normal[0][v], normal[1][v], normal[2][v], 0, colors,
            texture_uv(lz[v], tile_row), texture_uv(lx[v], tile_col), int(lz[v]) * 4, int(lx[v]) * 4)

    indices = []
    width = cells + 1
    for row in range(cells):
        for col in range(cells):
            a = row * width + col
            b, c, d = a + 1, a + width, a + width + 1
            # Alternating diagonals; winding matches the client's own terrain.
            indices += [a, b, c, b, d, c] if (row + col) % 2 else [a, b, d, a, d, c]

    positions = np.stack([x.ravel(), y.ravel(), z.ravel()], axis=1)
    return vertex_bytes, indices, positions


def write_chunk(path, heights, step):
    version, payload, collision, compressed = read_chunk(path)
    if version != 5:
        raise ValueError(f"{path.name} is version {version}; only version 5 chunks are supported")

    row, col = map(int, re.search(r"_(-?\d+)_(-?\d+)\.gcnk", path.name).groups())
    tile_count, = struct.unpack_from("<i", payload, 0)
    if tile_count != CHUNK_TILES * CHUNK_TILES:
        raise ValueError(f"{path.name} has {tile_count} tiles")

    mesh = find_mesh(payload, tile_count)
    old_vertices = mesh + 4 + tile_count * 16 + 4
    old_vertices += 4 + struct.unpack_from("<i", payload, old_vertices)[0] * 2 + 4
    colors = payload[old_vertices + 16 : old_vertices + 24]

    templates = read_collision_records(collision)
    if len(templates) != tile_count:
        raise ValueError(f"{path.name} has {len(templates)} collision records for {tile_count} tiles")

    batches = bytearray()
    all_indices = []
    all_vertices = bytearray()
    records = bytearray()

    for tile in range(tile_count):
        tile_row, tile_col = divmod(tile, CHUNK_TILES)
        x0 = (col + tile_col) * TILE_SIZE
        z0 = (row + tile_row) * TILE_SIZE
        vertex_bytes, indices, positions = tile_mesh(heights, x0, z0, tile_col, tile_row, step, colors)

        batches += struct.pack("<4i", len(all_indices), len(indices), len(all_vertices) // VERTEX_SIZE, len(positions))
        all_indices += indices
        all_vertices += vertex_bytes

        triangles = np.array(indices).reshape(-1, 3)
        records += QuantizedBvh(positions, triangles).serialize(templates[tile])

    new_payload = bytearray(payload[:mesh])
    new_payload += struct.pack("<i", tile_count) + batches + struct.pack("<i", 0)
    new_payload += struct.pack(f"<i{len(all_indices)}H", len(all_indices), *all_indices)
    new_payload += struct.pack("<i", len(all_vertices) // VERTEX_SIZE) + all_vertices

    packed_payload = zlib.compress(bytes(new_payload), 9)
    packed_collision = zlib.compress(bytes(records), 9)
    blob = struct.pack("<4sIII", b"GCNK", version, len(new_payload), len(packed_payload)) + packed_payload
    blob += struct.pack("<II", len(records), len(packed_collision)) + packed_collision

    path.write_bytes(compress(blob) if compressed else blob)
    return len(all_vertices) // VERTEX_SIZE, len(all_indices) // 3


TEXTURE_SIZE = 1024
TEXELS_PER_UNIT = TEXTURE_SIZE // (CHUNK_TILES * TILE_SIZE)

# Ground colours by relative height, blended towards rock on slopes.
HEIGHT_COLOURS = [(0.0, (78, 132, 52)), (0.35, (104, 142, 62)), (0.7, (140, 128, 92)), (1.0, (226, 224, 214))]
ROCK_COLOUR = (122, 108, 94)
LIGHT = np.array([-0.5, 0.75, -0.43]) / np.linalg.norm([-0.5, 0.75, -0.43])


def paint(heights, x, z, low, high):
    """RGB colours of the ground at x, z: height bands, rock on steep slopes and a baked hillshade."""
    y = heights(x, z)
    dx = (heights(x + 1, z) - heights(x - 1, z)) / 2
    dz = (heights(x, z + 1) - heights(x, z - 1)) / 2

    level = np.clip((y - low) / max(high - low, 1e-6), 0, 1)
    stops = [stop for stop, _ in HEIGHT_COLOURS]
    colour = np.stack([np.interp(level, stops, [c[i] for _, c in HEIGHT_COLOURS]) for i in range(3)], axis=-1)

    slope = np.hypot(dx, dz)
    rock = np.clip((slope - 0.45) / 0.5, 0, 1)[..., None]
    colour = colour * (1 - rock) + np.array(ROCK_COLOUR) * rock

    length = np.sqrt(dx * dx + 1 + dz * dz)
    light = (-dx * LIGHT[0] + LIGHT[1] - dz * LIGHT[2]) / length
    shade = np.clip(0.55 + 0.6 * light, 0.6, 1.15)[..., None]
    return np.clip(colour * shade, 0, 255)


def to_565(colour):
    c = np.rint(colour).astype(np.int64)
    return (c[..., 0] >> 3) << 11 | (c[..., 1] >> 2) << 5 | c[..., 2] >> 3


def from_565(value):
    r, g, b = value >> 11 & 31, value >> 5 & 63, value & 31
    return np.stack([r << 3 | r >> 2, g << 2 | g >> 4, b << 3 | b >> 2], axis=-1).astype(np.float64)


def encode_dxt1(image):
    """DXT1 blocks of an RGB image: endpoints at the extremes of each block's main axis, 4-colour mode."""
    rows, cols, _ = image.shape
    blocks = image.reshape(rows // 4, 4, cols // 4, 4, 3).transpose(0, 2, 1, 3, 4).reshape(-1, 16, 3)

    mean = blocks.mean(axis=1, keepdims=True)
    axis = blocks.max(axis=1) - blocks.min(axis=1)
    projection = ((blocks - mean) * axis[:, None, :]).sum(axis=2)
    count = np.arange(len(blocks))
    c0 = to_565(blocks[count, projection.argmax(axis=1)])
    c1 = to_565(blocks[count, projection.argmin(axis=1)])

    # c0 > c1 selects the 4-colour mode; equal endpoints need no blending.
    c0, c1 = np.maximum(c0, c1), np.minimum(c0, c1)
    e0, e1 = from_565(c0), from_565(c1)
    palette = np.stack([e0, e1, (2 * e0 + e1) / 3, (e0 + 2 * e1) / 3], axis=1)
    distance = ((blocks[:, :, None, :] - palette[:, None, :, :]) ** 2).sum(axis=3)
    indices = np.where((c0 == c1)[:, None], 0, distance.argmin(axis=2))
    bits = (indices << (2 * np.arange(16))).sum(axis=1)

    out = np.empty(len(blocks), dtype=[("c0", "<u2"), ("c1", "<u2"), ("bits", "<u4")])
    out["c0"], out["c1"], out["bits"] = c0, c1, bits
    return out.tobytes()


def write_colour_map(path, heights, row, col, low, high):
    blob = path.read_bytes()
    compressed = path.suffix == ".z"
    if compressed:
        blob = zlib.decompress(blob[8:])

    magic, version, size, length, packed_length = struct.unpack_from("<4s4i", blob)
    if magic != b"GCK2" or size != TEXTURE_SIZE:
        raise ValueError(f"{path.name} is not a {TEXTURE_SIZE}x{TEXTURE_SIZE} GCK2 colour map")
    header = zlib.decompress(blob[20 : 20 + packed_length])[:128]

    # Texel centres, with each tile's 256 texels stretched over its 64 units like the mesh's UVs.
    texel = (np.arange(TEXTURE_SIZE) % (TEXTURE_SIZE // CHUNK_TILES) + 0.5) / TEXELS_PER_UNIT
    tile = np.arange(TEXTURE_SIZE) // (TEXTURE_SIZE // CHUNK_TILES) * TILE_SIZE
    along_z = row * TILE_SIZE + tile + texel
    along_x = (col * TILE_SIZE + tile + texel)[::-1]
    x, z = np.meshgrid(along_x, along_z, indexing="ij")

    dds = header + encode_dxt1(paint(heights, x, z, low, high))
    packed = zlib.compress(dds, 9)
    blob = struct.pack("<4s4i", magic, version, size, len(dds), len(packed)) + packed
    path.write_bytes(compress(blob) if compressed else blob)


def heightmap_sampler(image_path, extent, height, base):
    from PIL import Image

    image = Image.open(image_path)
    if image.mode.startswith("I"):
        pixels = np.asarray(image, dtype=np.float64) / 65535
    else:
        pixels = np.asarray(image.convert("L"), dtype=np.float64) / 255
    rows, cols = pixels.shape
    x_min, z_min, x_max, z_max = extent

    def sample(x, z):
        # Bilinear, with the image stretched over the zone: left to right is +x, top to bottom is +z.
        u = np.clip((x - x_min) / (x_max - x_min) * (cols - 1), 0, cols - 1)
        v = np.clip((z - z_min) / (z_max - z_min) * (rows - 1), 0, rows - 1)
        u0 = np.minimum(u.astype(int), cols - 2)
        v0 = np.minimum(v.astype(int), rows - 2)
        fu, fv = u - u0, v - v0
        top = pixels[v0, u0] * (1 - fu) + pixels[v0, u0 + 1] * fu
        bottom = pixels[v0 + 1, u0] * (1 - fu) + pixels[v0 + 1, u0 + 1] * fu
        return base + (top * (1 - fv) + bottom * fv) * height

    return sample


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--zone", default="gm_fork", help="Zone in src/Resources/ClientMods to rewrite")
    parser.add_argument("--heightmap", type=Path, help="Grayscale image (8 or 16 bit) stretched over the whole zone")
    parser.add_argument("--height", type=float, default=64, help="Height of white in the heightmap")
    parser.add_argument("--base", type=float, default=0, help="Height of black, or of the ground without a heightmap")
    parser.add_argument("--hill", type=float, nargs=4, action="append", default=[], metavar=("X", "Z", "RADIUS", "HEIGHT"),
                        help="Adds a smooth hill (negative height for a hollow); repeatable")
    parser.add_argument("--step", type=int, default=4, choices=[2, 4, 8, 16, 32, 64], help="Grid spacing in units")
    parser.add_argument("--keep-colours", action="store_true", help="Keeps the .gck2 colour maps instead of repainting them")
    parser.add_argument("--preview", type=Path, help="Also writes the heights as a grayscale PNG, one pixel per unit")
    args = parser.parse_args()

    chunks = sorted(path for path in output_dir.iterdir() if re.fullmatch(re.escape(args.zone) + r"_-?\d+_-?\d+\.gcnk(\.z)?", path.name))
    if not chunks:
        parser.error(f"No {args.zone} chunks in {output_dir}; fork a zone first with fork_zone.py")

    coords = [tuple(map(int, re.search(r"_(-?\d+)_(-?\d+)\.gcnk", path.name).groups())) for path in chunks]
    extent = (
        min(col for _, col in coords) * TILE_SIZE,
        min(row for row, _ in coords) * TILE_SIZE,
        (max(col for _, col in coords) + CHUNK_TILES) * TILE_SIZE,
        (max(row for row, _ in coords) + CHUNK_TILES) * TILE_SIZE,
    )

    terrain = heightmap_sampler(args.heightmap, extent, args.height, args.base) if args.heightmap else None

    def heights(x, z):
        y = terrain(x, z) if terrain else np.full(np.shape(x), args.base, dtype=np.float64)
        for hx, hz, radius, height in args.hill:
            distance = np.hypot(x - hx, z - hz)
            y = y + np.where(distance < radius, height * (0.5 + 0.5 * np.cos(np.pi * np.minimum(distance / radius, 1))), 0)
        return y

    for path in chunks:
        vertices, triangles = write_chunk(path, heights, args.step)
        print(f"Wrote {path.name}: {vertices} vertices, {triangles} triangles")

    if not args.keep_colours:
        # Colour bands span the whole zone's heights, so they line up across chunks.
        x_min, z_min, x_max, z_max = extent
        z, x = np.mgrid[z_min:z_max + 1:4, x_min:x_max + 1:4].astype(np.float64)
        zone_heights = heights(x, z)

        for path, (row, col) in zip(chunks, coords):
            colour_map = path.with_name(f"{args.zone}_{row}_{col}.gck2")
            colour_map = colour_map if colour_map.exists() else colour_map.with_name(colour_map.name + ".z")
            if not colour_map.exists():
                print(f"No colour map for {path.name}; skipped")
                continue
            write_colour_map(colour_map, heights, row, col, zone_heights.min(), zone_heights.max())
            print(f"Painted {colour_map.name}")

    zone_file = zones_dir / f"{args.zone}.json"
    if zone_file.exists():
        x, y, z = json.loads(zone_file.read_text())["SpawnPosition"]
        ground = float(heights(np.float64(x), np.float64(z)))
        if ground > y:
            print(f"Warning: the ground at the spawn point is {ground:.1f}, above its y of {y}; raise SpawnPosition in {zone_file.name}")

    if args.preview:
        from PIL import Image

        x_min, z_min, x_max, z_max = extent
        z, x = np.mgrid[z_min:z_max + 1, x_min:x_max + 1].astype(np.float64)
        y = heights(x, z)
        span = max(np.ptp(y), 1e-6)
        Image.fromarray(np.rint((y - y.min()) / span * 255).astype(np.uint8)).save(args.preview)
        print(f"Wrote {args.preview} (heights {y.min():.1f} to {y.max():.1f})")

    print(f"Rewrote {len(chunks)} {args.zone} chunks. Rebuild the WebAPI to serve them.")


if __name__ == "__main__":
    main()
