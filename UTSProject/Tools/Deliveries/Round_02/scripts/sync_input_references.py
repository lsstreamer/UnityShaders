import pathlib,json,shutil,hashlib,struct,math
D=pathlib.Path(__file__).resolve().parents[1];P=D.parents[2];W=P.parent.parent
names=(D/'models.txt').read_text().splitlines();source=W/'shaderNPR/comparison/unity/val';target=W/'shaderNPR/glb/val'
reference_format=json.loads((target/'bike/cameras.json').read_text());rows=[]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for name in names:
 src=source/name/'ref';dst=target/name;glb=dst/(name+'.glb');assert glb.is_file(),str(glb);glb_before=sha(glb)
 camera=json.loads((src/'cameras.json').read_text());assert set(camera)==set(reference_format),(name,'reference schema');assert camera['render_complete']and camera['renderer_camera_available']and camera['resolution']==1024
 assert len(camera['ref_cameras'])==2
 for i,view in enumerate(camera['ref_cameras']):
  assert view['file']==f'view{i}.png'and view['resolution']==1024
  assert view['renderer_camera']['proj_type']=='ortho'
  for record in [view,view['renderer_camera']]:
   for key in ['view_matrix','projection_matrix']:
    a=record[key];assert len(a)==4 and all(len(row)==4 for row in a)and all(math.isfinite(v)for row in a for v in row)
  f=src/view['file'];header=f.read_bytes()[:29];assert struct.unpack('>II',header[16:24])==(1024,1024)and header[25]==6
 for namefile in ['view0.png','view1.png','cameras.json']:
  original=src/namefile;dest=dst/namefile
  if not dest.exists() or sha(dest)!=sha(original):shutil.copy2(original,dest)
  assert sha(dest)==sha(original)
 assert sha(glb)==glb_before
 rows.append(dict(model=name,files=[name+'.glb','cameras.json','view0.png','view1.png'],referenceFilesByteIdentical=True,glbUnchanged=True))
report=dict(completed=True,models=len(rows),referenceImages=16,cameraFiles=8,schemaMatches='bike',results=rows)
(D/'reports/glb_reference_validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
