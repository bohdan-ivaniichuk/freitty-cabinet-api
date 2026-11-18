"""Draws the sample photos of the seed: flat placeholder pictures of freight on a dock, not photographs.

Run from anywhere with Python 3 and nothing else installed; it writes the PNG files next to itself:

    python3 src/Freitty.Infrastructure/Persistence/Seed/Images/draw_sample_photos.py

The output is committed and embedded in the Infrastructure assembly; run this again only to change the pictures.
"""

import pathlib
import struct
import zlib

WIDTH, HEIGHT = 640, 480

# One palette for every picture: a palette PNG of flat shapes compresses to a few kilobytes.
PALETTE = [
    (0xE5, 0xE7, 0xEB),  # 0 wall
    (0x9C, 0xA3, 0xAF),  # 1 dock floor
    (0x1F, 0x2A, 0x3A),  # 2 dark: door opening, trailer interior
    (0x8B, 0x5A, 0x2B),  # 3 pallet wood
    (0x5C, 0x3A, 0x1A),  # 4 pallet shadow
    (0xD6, 0xA8, 0x6C),  # 5 cardboard
    (0xB7, 0x86, 0x4B),  # 6 cardboard edge
    (0xF5, 0xF0, 0xE1),  # 7 tape and labels
    (0xED, 0x1C, 0x2E),  # 8 red: dock door frame, damage marker
    (0xC7, 0xD2, 0xFE),  # 9 stretch film
    (0xFA, 0xCC, 0x15),  # 10 yellow: safety line
    (0xFF, 0xFF, 0xFF),  # 11 trailer body
]


class Canvas:
    def __init__(self):
        self.pixels = bytearray(WIDTH * HEIGHT)

    def rect(self, left, top, width, height, color):
        for y in range(max(top, 0), min(top + height, HEIGHT)):
            start = y * WIDTH + max(left, 0)
            end = y * WIDTH + min(left + width, WIDTH)
            self.pixels[start:end] = bytes([color]) * max(end - start, 0)

    def frame(self, left, top, width, height, thickness, color):
        self.rect(left, top, width, thickness, color)
        self.rect(left, top + height - thickness, width, thickness, color)
        self.rect(left, top, thickness, height, color)
        self.rect(left + width - thickness, top, thickness, height, color)

    def line(self, x0, y0, x1, y1, thickness, color):
        steps = max(abs(x1 - x0), abs(y1 - y0), 1)
        for step in range(steps + 1):
            x = x0 + (x1 - x0) * step // steps
            y = y0 + (y1 - y0) * step // steps
            self.rect(x - thickness // 2, y - thickness // 2, thickness, thickness, color)

    def png(self):
        rows = b"".join(b"\x00" + bytes(self.pixels[y * WIDTH:(y + 1) * WIDTH]) for y in range(HEIGHT))
        palette = b"".join(bytes(rgb) for rgb in PALETTE)
        header = struct.pack(">IIBBBBB", WIDTH, HEIGHT, 8, 3, 0, 0, 0)
        return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"PLTE", palette) \
            + chunk(b"IDAT", zlib.compress(rows, 9)) + chunk(b"IEND", b"")


def chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)


def dock(canvas):
    canvas.rect(0, 0, WIDTH, 300, 0)
    canvas.rect(0, 300, WIDTH, HEIGHT - 300, 1)
    canvas.rect(0, 296, WIDTH, 8, 10)


def pallet(canvas, left, bottom, width):
    canvas.rect(left, bottom - 18, width, 18, 3)
    for slot in range(3):
        canvas.rect(left + 10 + slot * (width - 40) // 2, bottom - 10, 20, 10, 4)


def box(canvas, left, bottom, width, height):
    canvas.rect(left, bottom - height, width, height, 5)
    canvas.frame(left, bottom - height, width, height, 3, 6)
    canvas.rect(left + width // 2 - 6, bottom - height, 12, height, 7)


def loaded_pallet(canvas, left, bottom, width, layers):
    pallet(canvas, left, bottom, width)
    box_width = width // 2
    for layer in range(layers):
        for column in range(2):
            box(canvas, left + column * box_width, bottom - 18 - layer * 50, box_width, 50)


def pallets_on_dock():
    canvas = Canvas()
    dock(canvas)
    for index, layers in enumerate([3, 2, 3]):
        loaded_pallet(canvas, 60 + index * 190, 420, 150, layers)
    return canvas


def trailer_at_door():
    canvas = Canvas()
    dock(canvas)
    canvas.rect(150, 40, 340, 270, 8)
    canvas.rect(170, 60, 300, 250, 11)
    canvas.rect(190, 80, 260, 230, 2)
    loaded_pallet(canvas, 210, 310, 100, 2)
    loaded_pallet(canvas, 330, 310, 100, 3)
    canvas.rect(150, 310, 340, 14, 4)
    return canvas


def damaged_pallet():
    canvas = Canvas()
    dock(canvas)
    pallet(canvas, 220, 420, 200)
    box(canvas, 220, 402, 100, 70)
    box(canvas, 320, 402, 100, 70)
    canvas.line(250, 332, 330, 262, 50, 5)
    canvas.line(250, 332, 330, 262, 8, 6)
    canvas.line(430, 250, 530, 350, 14, 8)
    canvas.line(530, 250, 430, 350, 14, 8)
    return canvas


def wrapped_stack():
    canvas = Canvas()
    dock(canvas)
    loaded_pallet(canvas, 230, 430, 180, 5)
    for top in range(162, 412, 24):
        canvas.rect(226, top, 188, 10, 9)
    canvas.rect(290, 300, 60, 40, 7)
    canvas.frame(290, 300, 60, 40, 3, 2)
    loaded_pallet(canvas, 470, 430, 120, 1)
    return canvas


PICTURES = {
    "pallets-on-dock.png": pallets_on_dock,
    "trailer-at-door.png": trailer_at_door,
    "damaged-pallet.png": damaged_pallet,
    "wrapped-stack.png": wrapped_stack,
}


if __name__ == "__main__":
    folder = pathlib.Path(__file__).resolve().parent
    for name, draw in PICTURES.items():
        data = draw().png()
        (folder / name).write_bytes(data)
        print(f"{name}: {len(data)} bytes")
