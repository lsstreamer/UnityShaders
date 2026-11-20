import pathlib,json,shutil,tarfile,hashlib,subprocess
p=pathlib.Path('/home/sirui/workspace/UnityProjects/UTSProject');d=p/'Tools/Deliveries/Round_02';w=p.parent.parent;out=w/'shaderNPR/comparison/unity/val';glb=w/'shaderNPR/glb/val'
old=['porsche-959-toon-style','toon-style-crystal','toon-suv-animated','choco-bunny','juice-carton-shop','mario-toon-2023','flan-quesillo','elaina-the-witchs-journey'];new=['porsche','crystal','suv','bunny','shop','mario','quesillo','witch'];mapping=dict(zip(old,new))
assert (d/'reports/unity_rename_verified.txt').read_text().splitlines()==new
plans=[]
for a,b in mapping.items():
 for parent in [p/'configs/val_models',out,glb,d/'archives/glb']:
  src=parent/a;dst=parent/b
  assert src.is_dir()and not dst.exists(),str(src)
  plans.append((src,dst))
state={str(f):[f.stat().st_size,f.stat().st_mtime_ns]for a in old for f in (out/a).rglob('*.png')}
with tarfile.open(d/'archives/pre_rename_metadata.tar.gz','w:gz')as t:
 t.add(d/'reports',arcname='reports');t.add(d/'models.txt',arcname='models.txt')
 for a in old:t.add(p/'configs/val_models'/a,arcname='configs/'+a)
for src,dst in plans:src.rename(dst)
for parent in [glb,d/'archives/glb']:
 for a,b in mapping.items():(parent/b/(a+'.glb')).rename(parent/b/(b+'.glb'))
def replace(s):
 for a,b in mapping.items():s=s.replace(a,b)
 return s
def walk(x):
 if isinstance(x,str):return replace(x)
 if isinstance(x,list):return [walk(v)for v in x]
 if isinstance(x,dict):return {replace(k):walk(v)for k,v in x.items()}
 return x
for base in [d/'reports',p/'Assets/Models/Round_02']:
 for f in base.rglob('*.json'):
  if f.name=='asset_sha256.json':continue
  f.write_text(json.dumps(walk(json.loads(f.read_text())),indent=2)+'\n')
for b in new:
 for f in (p/'configs/val_models'/b).glob('*.sh'):f.write_text(replace(f.read_text()))
 for f in (out/b).rglob('*.json'):
  text=f.read_text();changed=replace(text)
  if changed!=text:f.write_text(changed)
(d/'models.txt').write_text('\n'.join(new)+'\n')
for f in [d/'README.md',p/'Assets/Editor/Round02Migration.cs']:
 f.write_text(replace(f.read_text()))
# Preserve source hashes in the metadata archive; update live hashes for current paths.
f=d/'reports/asset_sha256.json';old_checks=json.loads(f.read_text());checks={replace(k):hashlib.sha256((p/replace(k)).read_bytes()).hexdigest()for k in old_checks};f.write_text(json.dumps(checks,indent=2)+'\n')
for path,st in state.items():
 dest=pathlib.Path(replace(path));assert dest.exists()and[dest.stat().st_size,dest.stat().st_mtime_ns]==st,path
# A camera-only smoke run writes into a dedicated QA folder and keeps formal outputs intact.
qa=d/'scripts/rename_camera_settings'
for b in new:
 folder=qa/b;folder.mkdir(parents=True,exist_ok=True)
 shutil.copy2(p/'configs/val_models'/b/'scene.sh',folder/'scene.sh')
 (folder/'camera.sh').write_text('render_refs_only=true\n')
(d/'scripts/rename_refs_config.sh').write_text('model_settings_dir="$project/Tools/Deliveries/Round_02/scripts/rename_camera_settings"\noutput_root="$project/Tools/Deliveries/Round_02/previews/rename_validation"\n')
(d/'reports/rename_mapping.json').write_text(json.dumps(mapping,indent=2)+'\n')
(d/'README.md').write_text((d/'README.md').read_text()+'\n## 简短模型名\n\n'+', '.join(new)+'。场景、FBX、GLB、模型目录、渲染配置和结果目录已统一更名；旧名对照见 `reports/rename_mapping.json`。\n')
listed=subprocess.check_output(['bash',str(d.parent/'render_round.sh'),'Round_02','--list'],text=True).splitlines();assert listed==new
print(json.dumps(dict(names=new,preservedPngFiles=len(state),updatedAssetHashes=len(checks),validated=True),indent=2))
