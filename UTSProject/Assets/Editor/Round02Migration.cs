using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class Round02Migration {
 [Serializable] public class Manifest {public string unityVersion;public string colorSpace;public Model[] models;}
 [Serializable] public class Model {public string name;public string scene;public View[] views;public int meshes;public int triangles;public Binding[] materials;public float[] lightRotation;public float lightIntensity;public float[] lightColor;}
 [Serializable] public class View {public string marker;public float[] position;public float[] rotation;public float orthoSize;public float near;public float far;public int cullingMask;}
 [Serializable] public class Binding {public string renderer;public string[] materials;}
 [Serializable] public class Check {public string name;public int meshes;public int triangles;public float localOrtho;public float orbitSafeOrtho;public bool verified;}
 [Serializable] public class Report {public Check[] models;public bool verified;}
 public static void Prepare(){
  var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Tools/Deliveries/Round_02/reports/manifest.json"));if(Application.unityVersion!=manifest.unityVersion||PlayerSettings.colorSpace.ToString()!=manifest.colorSpace)throw new Exception("Unity/color-space mismatch");var checks=new List<Check>();
  foreach(var spec in manifest.models){var scene=EditorSceneManager.OpenScene(spec.scene);var root=GameObject.Find("ModelRoot");var rs=root.GetComponentsInChildren<MeshRenderer>();if(rs.Length!=spec.meshes)throw new Exception("Mesh count mismatch: "+spec.name);int tris=root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);if(tris!=spec.triangles)throw new Exception("Triangle mismatch: "+spec.name);
   foreach(var binding in spec.materials){var r=rs.Single(x=>AnimationUtility.CalculateTransformPath(x.transform,root.transform)==binding.renderer);if(!r.sharedMaterials.Select(AssetDatabase.GetAssetPath).SequenceEqual(binding.materials))throw new Exception("Material binding mismatch: "+spec.name+"/"+binding.renderer);foreach(var m in r.sharedMaterials)if(!m||!m.shader||m.shader.name!="Toon/Toon"||!m.shader.isSupported||ShaderUtil.ShaderHasError(m.shader))throw new Exception("Unsupported shader: "+spec.name);}
   var light=GameObject.Find("Directional Light").GetComponent<Light>();var q=spec.lightRotation;if(Quaternion.Angle(light.transform.rotation,new Quaternion(q[0],q[1],q[2],q[3]))>.01f||Mathf.Abs(light.intensity-spec.lightIntensity)>.0001f)throw new Exception("Light mismatch: "+spec.name);
   var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);float safe=0;
   for(int i=0;i<spec.views.Length;i++){var v=spec.views[i];var marker=GameObject.Find(v.marker).transform;var pos=new Vector3(v.position[0],v.position[1],v.position[2]);var rot=new Quaternion(v.rotation[0],v.rotation[1],v.rotation[2],v.rotation[3]);if(Vector3.Distance(marker.position,pos)>.001f||Quaternion.Angle(marker.rotation,rot)>.01f)throw new Exception("View mismatch");
    var c=new GameObject("Reference"+i).AddComponent<Camera>();c.enabled=false;c.transform.SetPositionAndRotation(pos,rot);c.orthographic=true;c.aspect=1;c.nearClipPlane=v.near;c.farClipPlane=v.far;c.cullingMask=v.cullingMask;c.allowHDR=false;c.allowMSAA=true;c.renderingPath=RenderingPath.Forward;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.clear;
    float elevation=Mathf.Asin(Mathf.Clamp((pos-b.center).normalized.y,-1,1));float horizontal=new Vector2(b.extents.x,b.extents.z).magnitude;float vertical=b.extents.y*Mathf.Cos(elevation)+horizontal*Mathf.Abs(Mathf.Sin(elevation));c.orthographicSize=Mathf.Max(v.orthoSize,Mathf.Max(horizontal,vertical)/.9f);safe=c.orthographicSize;
   }
   EditorSceneManager.SaveScene(scene,spec.scene);checks.Add(new Check{name=spec.name,meshes=rs.Length,triangles=tris,localOrtho=spec.views[0].orthoSize,orbitSafeOrtho=safe,verified=true});Debug.Log("[Round02Migration] Verified "+spec.name+" meshes="+rs.Length+" triangles="+tris+" ortho="+safe);
  }
  AssetDatabase.SaveAssets();File.WriteAllText("Tools/Deliveries/Round_02/reports/migration_verification.json",JsonUtility.ToJson(new Report{models=checks.ToArray(),verified=true},true));
 }
}
