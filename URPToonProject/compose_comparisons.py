#!/usr/bin/env python3
"""Validate character image pairs and compose a labeled grid of all characters."""

import argparse
import json
from pathlib import Path
import math

from PIL import Image, ImageChops, ImageDraw, ImageFont, ImageStat


def compose(output: Path) -> None:
    font_path = Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
    font = ImageFont.truetype(str(font_path), 30) if font_path.exists() else ImageFont.load_default()
    cards = []
    validation = {}
    inventory = json.loads((output / "character_inventory.json").read_text())
    names = [item["name"] for item in inventory["characters"]]
    for name in names:
        directory = output / name
        with Image.open(directory / "basecolor.png") as source:
            base = source.convert("RGB")
        with Image.open(directory / "original_shader.png") as source:
            shaded = source.convert("RGB")
        if base.size != (1024, 1024) or shaded.size != base.size:
            raise ValueError(f"Unexpected image dimensions for {name}")
        difference = ImageChops.difference(base, shaded)
        mean_difference = sum(ImageStat.Stat(difference).mean) / 3
        if mean_difference < 0.1:
            raise ValueError(f"Base Color and shaded images are unexpectedly similar for {name}")
        for kind, image in (("basecolor", base), ("original_shader", shaded)):
            pixels = list(image.get_flattened_data() if hasattr(image, "get_flattened_data") else image.getdata())
            background = image.getpixel((0, 0))
            magenta = sum(r > 240 and g < 12 and b > 240 for r, g, b in pixels)
            foreground = sum(max(abs(value - channel) for value, channel in zip(pixel, background)) > 15
                             for pixel in pixels)
            if magenta > 100 or foreground < 10000:
                raise ValueError(f"Invalid {kind} image for {name}: magenta={magenta}, foreground={foreground}")
        report = json.loads((directory / "settings.json").read_text())
        if not report["originalMaterialsUnchanged"]:
            raise ValueError(f"Original materials changed for {name}")
        card = Image.new("RGB", (2048, 1096), "white")
        card.paste(base, (0, 72))
        card.paste(shaded, (1024, 72))
        draw = ImageDraw.Draw(card)
        draw.text((28, 20), f"{name} | Base Color", fill="#222222", font=font)
        shader_label = "Two-band Toon Shader" if report.get("twoBand") else "Original Toon Shader"
        draw.text((1052, 20), f"{name} | {shader_label}", fill="#222222", font=font)
        card.save(directory / "comparison.png")
        cards.append(card)
        validation[name] = {
            "image_size": list(base.size),
            "mean_absolute_rgb_difference_0_255": mean_difference,
            "material_count": len(report["materials"]),
            "original_materials_unchanged_during_comparison": True,
            "two_band": report.get("twoBand", False),
            "only_two_band_switch_changed": report.get("onlyTwoBandSwitchChanged", False),
        }
        print(f"{name}: 1024x1024 images validated; {len(report['materials'])} original materials; RGB difference={mean_difference:.3f}")
    columns = 3 if len(cards) % 3 == 0 else min(2, len(cards))
    gutter = 24
    rows = math.ceil(len(cards) / columns)
    width = columns * 2048 + (columns + 1) * gutter
    height = rows * 1096 + (rows + 1) * gutter
    overview = Image.new("RGB", (width, height), "#d4d4d4")
    for index, card in enumerate(cards):
        overview.paste(card, (gutter + (index % columns) * (2048 + gutter),
                              gutter + (index // columns) * (1096 + gutter)))
    overview.save(output / "all_characters_comparison.png")
    preview = overview.copy()
    preview.thumbnail((2400, 2400), Image.Resampling.LANCZOS)
    preview.save(output / "all_characters_preview.jpg", quality=94, subsampling=0)
    two_models = Image.new("RGB", (2048, 2192), "white")
    for index, name in enumerate(("Arisa", "Warrior")):
        two_models.paste(cards[names.index(name)], (0, index * 1096))
    two_models.save(output / "two_models_comparison.png")
    (output / "image_validation.json").write_text(json.dumps(validation, indent=2) + "\n")
    print(f"All {len(cards)} characters: {output / 'all_characters_comparison.png'} ({width}x{height})")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    compose(parser.parse_args().output)
