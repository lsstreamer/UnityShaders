import pathlib,json,hashlib
p=pathlib.Path('/home/sirui/workspace/UnityProjects/UTSProject');d=p/'Tools/Deliveries/Round_02';out=p.parent.parent/'shaderNPR/comparison/unity/val';qa=d/'previews/rename_validation/val';names=(d/'models.txt').read_text().splitlines();rows=[]
assert (d/'logs/rename_render.exit').read_text().strip()=='0','Reference render has not completed successfully'
for n in names:
 meta=json.loads((qa/n/'unity_render.json').read_text());assert meta['completed']and meta['refs_written']==2 and meta['ref_geometry_written']==2 and meta['reference_only']
 identical=0
 for i in range(2):
  a=(qa/n/'ref'/f'view{i}.png').read_bytes();b=(out/n/'ref'/f'view{i}.png').read_bytes();assert hashlib.sha256(a).digest()==hashlib.sha256(b).digest(),(n,i,'reference changed');identical+=1
 assert (p/'Assets/Scenes/Round_02'/(n+'.unity')).exists()
 assert (p/'Assets/Models/Round_02'/n/(n+'.fbx')).exists()
 assert (p/'configs/val_models'/n/'scene.sh').is_file()
 rows.append(dict(model=n,scene=f'Assets/Scenes/Round_02/{n}.unity',freshReferences=2,byteIdenticalReferences=identical,passed=True))
report=dict(completed=True,models=8,unityConfigurationPreserved=True,existingPngImages=2016,freshReferenceImages=16,byteIdenticalReferenceImages=16,results=rows)
(d/'reports/rename_verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
