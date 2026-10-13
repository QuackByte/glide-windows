#!/usr/bin/env python3
"""Draws Glide's app icon (a rounded square holding three columns, the focused one bright) and
writes a multi-size .ico with PNG frames. No dependencies: python3 tools/make-icon.py out.ico"""
import struct, sys, zlib

def coverage_rounded(x, y, x0, y0, x1, y1, r):
    cx = min(max(x, x0 + r), x1 - r)
    cy = min(max(y, y0 + r), y1 - r)
    d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
    if x0 <= x <= x1 and y0 <= y <= y1 and d <= r:
        return 1.0
    return 0.0

def render(size, ss=4):
    n = size * ss
    pixels = []
    # Layout in a 0..1 square.
    bg = (0.06, 0.06, 0.94, 0.94, 0.2)
    columns = [  # x0, x1, color
        (0.17, 0.33, (0x9C, 0xC9, 0xF5, 150)),
        (0.39, 0.61, (0xFF, 0xFF, 0xFF, 255)),
        (0.67, 0.83, (0x9C, 0xC9, 0xF5, 150)),
    ]
    rows = []
    for py in range(size):
        row = bytearray()
        for px in range(size):
            acc = [0.0, 0.0, 0.0, 0.0]
            for sy in range(ss):
                for sx in range(ss):
                    x = (px * ss + sx + 0.5) / n
                    y = (py * ss + sy + 0.5) / n
                    r, g, b, a = 0, 0, 0, 0.0
                    if coverage_rounded(x, y, *bg):
                        # Vertical gradient, Windows-blue.
                        t = (y - 0.06) / 0.88
                        r, g, b, a = 0x1E + (0x00 - 0x1E) * t, 0x7B + (0x4E - 0x7B) * t, 0xF0 + (0xB8 - 0xF0) * t, 1.0
                        for x0, x1, (cr, cg, cb, ca) in columns:
                            if coverage_rounded(x, y, x0, 0.22, x1, 0.78, 0.035):
                                k = ca / 255
                                r, g, b = r + (cr - r) * k, g + (cg - g) * k, b + (cb - b) * k
                    acc[0] += r * a; acc[1] += g * a; acc[2] += b * a; acc[3] += a
            count = ss * ss
            alpha = acc[3] / count
            if alpha > 0:
                row += bytes([int(acc[0] / acc[3] + 0.5), int(acc[1] / acc[3] + 0.5), int(acc[2] / acc[3] + 0.5), int(alpha * 255 + 0.5)])
            else:
                row += bytes(4)
        rows.append(bytes(row))
    raw = b"".join(b"\x00" + r for r in rows)
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")

sizes = [16, 20, 24, 32, 40, 48, 64, 256]
frames = [render(s, 4 if s <= 64 else 2) for s in sizes]
header = struct.pack("<HHH", 0, 1, len(frames))
offset = 6 + 16 * len(frames)
entries = b""
for s, data in zip(sizes, frames):
    entries += struct.pack("<BBBBHHII", s % 256, s % 256, 0, 0, 1, 32, len(data), offset)
    offset += len(data)
open(sys.argv[1], "wb").write(header + entries + b"".join(frames))
