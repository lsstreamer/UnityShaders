import json, pathlib, struct, math
delivery=pathlib.Path(__file__).resolve().parents[1];project=delivery.parents[2]
root=project.parent.parent/"shaderNPR/comparison/unity/val"
manifest=json.loads((delivery/'reports/manifest.json').read_text())
reports=[]
for spec in manifest['models']:
 n=spec['name'];p=root/n;meta=json.loads((p/'unity_render.json').read_text());assert meta['completed'] and meta['frames_written']==240 and meta['refs_written']==2 and meta['geometry_written']==8 and meta['ref_geometry_written']==2 and meta['resolution']==1024 and meta['msaa']==8 and meta['color_space']=='Linear',n
 paths=[]
 for mode in ['bounding_sphere','camera_only']:
  ps=sorted((p/mode/'frames').glob('*.png'));assert len(ps)==120,(n,mode,len(ps));assert [f.name for f in ps]==[f'frame_{i:04d}.png'for i in range(120)];paths+=ps
 for folder,count in [('ref',2),('geometry/orbit',8),('geometry/optimization_views',2)]:
  ps=sorted((p/folder).glob('*.png'));assert len(ps)==count,(n,folder);paths+=ps
 for f in paths:
  with f.open('rb')as fp:header=fp.read(29)
  assert header[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack('>II',header[16:24])==(1024,1024),(n,str(f))
  assert header[25] in ([0] if 'geometry' in f.parts else [6]),(n,'PNG channel format',str(f),header[25])
 cams=json.loads((p/'cameras.json').read_text());assert cams['render_complete'] and len(cams['frames'])==240
 refs=json.loads((p/'ref/cameras.json').read_text());assert refs['render_complete']and len(refs['ref_cameras'])==2
 for f in cams['frames']:assert f['image_rendered']and(p/f['file']).is_file()
 for mode in ['bounding_sphere','camera_only']:
  frames=[f for f in cams['frames']if f['render_mode']==mode];assert len(frames)==120
  diffs=[sum((a-b)**2 for a,b in zip(frames[0]['light_dir'],f['light_dir']))for f in frames]
  assert max(diffs)<1e-7 if mode=='camera_only' else max(diffs)>.01,(n,mode,'light trajectory')
 reports.append(dict(model=n,images=len(paths),refs=2,orbitFrames=240,geometryMasks=10,resolution=1024,msaa=8,validated=True))
print(json.dumps(reports,indent=2));(delivery/'reports/render_validation.json').write_text(json.dumps(reports,indent=2))
