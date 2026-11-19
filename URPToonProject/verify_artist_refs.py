"""Validate the nine exported packages with shaderNPR's actual GLB/camera readers and GPU geometry renderer.

Run inside the existing GPU container: python3 verify_artist_refs.py
"""
import hashlib
import json
from pathlib import Path
import sys
from types import SimpleNamespace

import numpy as np
from PIL import Image, ImageDraw
import torch

PROJECT = Path(__file__).resolve().parent
WORKSPACE = PROJECT.parent.parent
sys.path.insert(0, str(WORKSPACE / 'shaderNPR'))
from core.geometry.gltf import GLTF
from tools.metrics.geometry import GeometryRenderer
from tools.metrics.reference_cameras import reference_frames

NAMES = ['Arisa', 'Warrior', 'Mike', 'CrazyCat', 'SupercyanMale', 'CityFemale', 'CityMaleA', 'CityMaleB', 'CityPolice']
REFS = WORKSPACE / 'shaderNPR/comparison/unity/artist'
GLBS = WORKSPACE / 'shaderNPR/glb/artist'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    report = {}
    preview = Image.new('RGB', (1536, len(NAMES) * 286), (232, 232, 232))
    draw = ImageDraw.Draw(preview)
    torch.cuda.set_device(0)
    for row, name in enumerate(NAMES):
        folder = GLBS / name
        reader = GLTF(folder / (name + '.glb'))
        parts, materials = reader.load()
        assert parts and materials
        assert not reader.doc.get('skins') and not reader.doc.get('animations')
        assert all('bufferView' in image for image in reader.doc.get('images', []))
        for material in reader.doc['materials']:
            assert not any(key in material for key in ('normalTexture', 'occlusionTexture', 'emissiveTexture'))
            pbr = material['pbrMetallicRoughness']
            assert pbr['metallicFactor'] == 0 and pbr['roughnessFactor'] == 1
        vertices = np.concatenate([part['vertices'] for part in parts])
        assert np.isfinite(vertices).all()
        lo, hi = vertices.min(0), vertices.max(0)
        vertices = (vertices - (lo + hi) * .5) * (2 / (hi - lo).max())
        faces, offset = [], 0
        for part in parts:
            faces.append(part['faces'] + offset)
            offset += len(part['vertices'])
        mesh = SimpleNamespace(verts=torch.tensor(vertices, device='cuda', dtype=torch.float32),
                               faces=torch.tensor(np.concatenate(faces), device='cuda', dtype=torch.int32))
        geometry = GeometryRenderer(mesh)
        cameras = reference_frames(folder / 'cameras.json', [0, 1, 0])
        assert len(cameras) == 2 and all(item['camera']['proj_type'] == 'ortho' for item in cameras)
        original = json.loads((REFS / name / 'ref/cameras.json').read_text())
        copied = json.loads((folder / 'cameras.json').read_text())
        assert original['render_complete'] is True and copied['render_complete'] is True
        metrics = []
        for index, frame in enumerate(cameras):
            filename = f'view{index}.png'
            assert digest(folder / filename) == digest(REFS / name / 'ref' / filename)
            assert copied['ref_cameras'][index]['renderer_camera'] == original['ref_cameras'][index]['renderer_camera']
            assert 'geometry_file' not in copied['ref_cameras'][index]
            image = Image.open(folder / filename).convert('RGBA')
            rgba = np.array(image)
            assert image.size == (1024, 1024) and 0 < np.count_nonzero(rgba[..., 3]) < 1024 * 1024
            assert not ((rgba[..., 0] > 245) & (rgba[..., 1] < 10) & (rgba[..., 2] > 245)).any(), name
            rendered = geometry.render(frame['camera'], 1024)
            saved = np.array(Image.open(REFS / name / 'geometry/optimization_views' / filename).convert('L')) / 255
            a, b = rendered > .5, saved > .5
            iou = np.count_nonzero(a & b) / np.count_nonzero(a | b)
            assert iou > .995, (name, index, iou)
            metrics.append({'view': filename, 'geometry_iou': float(iou), 'foreground_pixels': int(np.count_nonzero(rgba[..., 3]))})
            thumb = image.resize((256, 256), Image.Resampling.LANCZOS)
            preview.paste(thumb, (index * 256, row * 286 + 25), thumb)
        draw.text((8, row * 286 + 5), name, fill=(0, 0, 0))
        draw.text((520, row * 286 + 50), f"{len(parts)} primitives | {len(materials)} materials | {len(vertices)} vertices", fill=(0, 0, 0))
        draw.text((520, row * 286 + 75), f"Geometry IoU: {metrics[0]['geometry_iou']:.6f}, {metrics[1]['geometry_iou']:.6f}", fill=(0, 0, 0))
        report[name] = {'glb_bytes': (folder / (name + '.glb')).stat().st_size,
                        'materials': len(materials), 'primitives': len(parts), 'vertices': len(vertices),
                        'triangles': len(mesh.faces), 'embedded_images': len(reader.doc.get('images', [])),
                        'alpha_modes': [m['alpha_mode'] for m in materials], 'views': metrics}
        print(name, json.dumps(report[name]), flush=True)
        del geometry, mesh
    output = PROJECT / 'Comparison/ArtistRefs'
    output.mkdir(exist_ok=True)
    (output / 'validation.json').write_text(json.dumps(report, indent=2) + '\n')
    preview.crop((0, 0, 1050, preview.height)).save(output / 'refs_overview.jpg', quality=90)
    print('PASS: all 9 packages; 18 orthographic refs; GLB/camera GPU geometry alignment.', flush=True)


if __name__ == '__main__':
    main()
