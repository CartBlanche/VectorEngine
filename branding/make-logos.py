#!/usr/bin/env python3
"""Builds the VectorEngine logo files from logo-geometry.json (the splash's own geometry; VectorEngineLogoTests keeps the two in step).

    python3 branding/make-logos.py        (needs ImageMagick's `magick` for the PNGs and the .ico; the SVGs need nothing)

Two themes, both on a transparent background:
  dark   glowing cyan and magenta lines, the splash's look, for dark backgrounds
  light  the same shapes as flat, darker lines with no glow, for light backgrounds (a glow turns to a smudge on white)
and one opaque tile (dark rounded square) for places that want a solid icon: NuGet, the favicon, the GitHub social preview.

  mark-{dark,light}.svg / -512.png / -128.png     the V+E mark, square
  logo-{dark,light}.svg / .png                    the mark over the name, 1280 x 400
  icon-tile-512.png, icon-tile-128.png            solid tile (NuGet package icon), favicon.ico
  social-preview.png                              1280 x 640, opaque (upload it in the repo's Settings > Social preview)
"""
import json, os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
G = json.load(open(os.path.join(HERE, "logo-geometry.json")))
BG = "#05070d"          # the opaque tile and the social preview
# colours: (mark, name, line weight factor). Dark is the splash's own pair; light is darker so it holds on white.
THEMES = {
    "dark": ("#ff4fd0", "#35f2ff", 1.0),
    "light": ("#c0128a", "#007f8f", 1.25),
}

mark = [(s[0], s[1], s[2], s[3]) for s in G["mark"]]
word = [(s[0], s[1], s[2], s[3]) for s in G["wordmark"]]


def bounds(segs):
    xs = [v for s in segs for v in (s[0], s[2])]
    ys = [v for s in segs for v in (s[1], s[3])]
    return min(xs), min(ys), max(xs), max(ys)


def lighten(hexcol, amount):
    r, g, b = (int(hexcol[i:i + 2], 16) for i in (1, 3, 5))
    r, g, b = (round(c + (255 - c) * amount) for c in (r, g, b))
    return "#%02x%02x%02x" % (r, g, b)


# (segments, colour) pairs in design coordinates, and a transform to the output
def layout_mark(size, theme="dark"):
    x0, y0, x1, y1 = bounds(mark)
    k = size * 0.56 / max(x1 - x0, y1 - y0)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    return k, (size / 2 - cx * k, size / 2 - cy * k), [(mark, THEMES[theme][0])], 0.032 * size * THEMES[theme][2]


def layout_logo(w, h, fill, theme="dark"):
    x0, y0, x1, y1 = bounds(mark + word)
    k = w * fill / (x1 - x0)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    return k, (w / 2 - cx * k, h / 2 - cy * k), [(mark, THEMES[theme][0]), (word, THEMES[theme][1])], 3.6 * k * THEMES[theme][2]


def lines_of(layers, k, off):
    for segs, col in layers:
        yield col, [(a * k + off[0], b * k + off[1], c * k + off[0], d * k + off[1]) for a, b, c, d in segs]


# ---------- SVG ----------
def svg(path, w, h, k, off, layers, unit, glow=True, tile=0):
    """tile > 0 puts the mark on a dark rounded square (corner radius `tile`); otherwise the background is transparent."""
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}">', '<title>VectorEngine</title>']
    if glow:
        out.append('<defs>')
        for name, blur in (("wide", unit * 2.4), ("mid", unit * 1.2)):
            out.append(f'<filter id="{name}" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="{blur:.2f}"/></filter>')
        out.append('</defs>')
    if tile:
        out.append(f'<rect width="{w}" height="{h}" rx="{tile}" fill="{BG}"/>')
    for col, segs in lines_of(layers, k, off):
        d = " ".join(f"M{a:.2f} {b:.2f}L{c:.2f} {dd:.2f}" for a, b, c, dd in segs)
        if glow:
            for name, width, op in (("wide", unit * 5, .55), ("mid", unit * 2.4, .85)):
                out.append(f'<path d="{d}" fill="none" stroke="{col}" stroke-width="{width:.2f}" stroke-linecap="round" stroke-linejoin="round" opacity="{op}" filter="url(#{name})"/>')
            core = lighten(col, .55)
        else:
            core = col
        out.append(f'<path d="{d}" fill="none" stroke="{core}" stroke-width="{unit:.2f}" stroke-linecap="round" stroke-linejoin="round"/>')
    out.append('</svg>')
    open(path, "w").write("\n".join(out) + "\n")


# ---------- PNG: ImageMagick draws each glow layer on a transparent canvas, blurs it and lays it over the last ----------
def png(path, w, h, k, off, layers, unit, glow=True, tile=0):
    cmd = ["magick", "-size", f"{w}x{h}", "xc:none"]
    if tile:
        cmd += ["-fill", BG, "-draw", f"roundrectangle 0,0 {w - 1},{h - 1} {tile},{tile}"]
    for col, segs in lines_of(layers, k, off):
        draw = "stroke-linecap round stroke-linejoin round " + " ".join(f"line {a:.2f},{b:.2f} {c:.2f},{d:.2f}" for a, b, c, d in segs)
        passes = [(unit * 5, unit * 2.4, col, .55), (unit * 2.4, unit * 1.2, col, .85), (unit, 0, lighten(col, .55), 1.0)] if glow else [(unit, 0, col, 1.0)]
        for width, blur, col2, opacity in passes:
            cmd += ["(", "-size", f"{w}x{h}", "xc:none", "-fill", "none", "-stroke", col2, "-strokewidth", f"{width:.2f}", "-draw", draw]
            if blur:
                cmd += ["-blur", f"0x{blur:.2f}"]
            if opacity < 1:
                cmd += ["-channel", "A", "-evaluate", "multiply", str(opacity), "+channel"]
            cmd += [")", "-compose", "Over", "-composite"]
    if tile:    # the glow must not spill past the rounded corners
        cmd += ["(", "-size", f"{w}x{h}", "xc:none", "-fill", "white", "-draw", f"roundrectangle 0,0 {w - 1},{h - 1} {tile},{tile}", ")",
                "-compose", "DstIn", "-composite"]
    cmd += ["-strip", path]
    subprocess.run(cmd, check=True)


def main():
    try:
        subprocess.run(["magick", "-version"], check=True, capture_output=True)
        have_magick = True
    except Exception:
        have_magick = False
        print("ImageMagick (magick) not found: writing the SVGs only", file=sys.stderr)

    for theme in ("dark", "light"):
        glow = theme == "dark"
        k, off, layers, unit = layout_mark(512, theme)
        svg(os.path.join(HERE, f"mark-{theme}.svg"), 512, 512, k, off, layers, unit, glow)
        k, off, layers, unit = layout_logo(1280, 400, 0.78, theme)
        svg(os.path.join(HERE, f"logo-{theme}.svg"), 1280, 400, k, off, layers, unit, glow)
    if not have_magick:
        return
    for theme in ("dark", "light"):
        glow = theme == "dark"
        for size in (512, 128):
            k, off, layers, unit = layout_mark(size, theme)
            png(os.path.join(HERE, f"mark-{theme}-{size}.png"), size, size, k, off, layers, unit, glow)
        k, off, layers, unit = layout_logo(1280, 400, 0.78, theme)
        png(os.path.join(HERE, f"logo-{theme}.png"), 1280, 400, k, off, layers, unit, glow)
    for size in (512, 128):
        k, off, layers, unit = layout_mark(size, "dark")
        png(os.path.join(HERE, f"icon-tile-{size}.png"), size, size, k, off, layers, unit, True, tile=round(size * 0.1875))
    subprocess.run(["magick", os.path.join(HERE, "icon-tile-512.png"), "-define", "icon:auto-resize=64,48,32,16", os.path.join(HERE, "favicon.ico")], check=True)
    k, off, layers, unit = layout_logo(1280, 640, 0.70, "dark")     # GitHub's social preview size, opaque
    png(os.path.join(HERE, "social-preview.png"), 1280, 640, k, off, layers, unit, True, tile=1)
    subprocess.run(["magick", os.path.join(HERE, "social-preview.png"), "-background", BG, "-alpha", "remove", "-alpha", "off", os.path.join(HERE, "social-preview.png")], check=True)


main()
