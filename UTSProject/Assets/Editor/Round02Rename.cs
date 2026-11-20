using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
public static class Round02Rename {
 static string[] Old={"porsche-959-toon-style","toon-style-crystal","toon-suv-animated","choco-bunny","juice-carton-shop","mario-toon-2023","flan-quesillo","elaina-the-witchs-journey"};
 static string[] Names={"porsche","crystal","suv","bunny","shop","mario","quesillo","witch"};
 static string Number(float f)=>Math.Round(f,5).ToString(CultureInfo.InvariantCulture);
 static string Signature(string scene){
  EditorSceneManager.OpenScene(scene);var root=GameObject.Find("ModelRoot");var b=new StringBuilder();
  foreach(var tr in root.GetComponentsInChildren<Transform>(true).OrderBy(t=>AnimationUtility.CalculateTransformPath(t,root.transform))){b.Append(AnimationUtility.CalculateTransformPath(tr,root.transform)).Append(tr.gameObject.activeSelf);for(int i=0;i<16;i++)b.Append('|').Append(Number(tr.localToWorldMatrix[i]));}
  foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true).OrderBy(r=>AnimationUtility.CalculateTransformPath(r.transform,root.transform))){var mesh=r.GetComponent<MeshFilter>().sharedMesh;b.Append(mesh.vertexCount).Append(mesh.triangles.Length);foreach(var m in r.sharedMaterials){if(!m||!m.shader||m.shader.name!="Toon/Toon"||!m.shader.isSupported||ShaderUtil.ShaderHasError(m.shader))throw new Exception("Invalid material");b.Append(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)));var s=m.shader;for(int i=0;i<s.GetPropertyCount();i++){var p=s.GetPropertyName(i);switch(s.GetPropertyType(i)){case ShaderPropertyType.Color:var c=m.GetColor(p);b.Append(Number(c.r)).Append(Number(c.g)).Append(Number(c.b)).Append(Number(c.a));break;case ShaderPropertyType.Float:case ShaderPropertyType.Range:b.Append(Number(m.GetFloat(p)));break;case ShaderPropertyType.Texture:var t=m.GetTexture(p);b.Append(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t)));break;}}}}
  foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).OrderBy(l=>l.name)){b.Append(light.name).Append(Number(light.intensity)).Append(light.color.ToString("F5"));for(int i=0;i<16;i++)b.Append(Number(light.transform.localToWorldMatrix[i]));}
  foreach(var name in new[]{"View01","View02","Reference0","Reference1"}){var go=GameObject.Find(name);if(!go)throw new Exception("Missing "+name);for(int i=0;i<16;i++)b.Append(Number(go.transform.localToWorldMatrix[i]));var c=go.GetComponent<Camera>();if(c){b.Append(Number(c.orthographicSize));for(int i=0;i<16;i++)b.Append(Number(c.projectionMatrix[i]));}}
  return b.ToString();
 }
 static void Move(string a,string b){string e=AssetDatabase.MoveAsset(a,b);if(!string.IsNullOrEmpty(e))throw new Exception(a+": "+e);}
 public static void Run(){var signatures=new string[8];var guids=new string[8];for(int i=0;i<8;i++){if(AssetDatabase.IsValidFolder("Assets/Models/Round_02/"+Names[i])||File.Exists("Assets/Scenes/Round_02/"+Names[i]+".unity"))throw new Exception("Rename target exists");signatures[i]=Signature("Assets/Scenes/Round_02/"+Old[i]+".unity");guids[i]=AssetDatabase.AssetPathToGUID("Assets/Scenes/Round_02/"+Old[i]+".unity");}
 EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 for(int i=0;i<8;i++){Move("Assets/Models/Round_02/"+Old[i],"Assets/Models/Round_02/"+Names[i]);Move("Assets/Models/Round_02/"+Names[i]+"/"+Old[i]+".fbx","Assets/Models/Round_02/"+Names[i]+"/"+Names[i]+".fbx");Move("Assets/Scenes/Round_02/"+Old[i]+".unity","Assets/Scenes/Round_02/"+Names[i]+".unity");}
 AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
 for(int i=0;i<8;i++){string path="Assets/Scenes/Round_02/"+Names[i]+".unity";if(AssetDatabase.AssetPathToGUID(path)!=guids[i]||Signature(path)!=signatures[i])throw new Exception("Rename changed scene configuration: "+Names[i]);Debug.Log("[Round02Rename] Verified "+Old[i]+" -> "+Names[i]);}
 File.WriteAllText("Tools/Deliveries/Round_02/reports/unity_rename_verified.txt",string.Join("\n",Names));
 }
}
