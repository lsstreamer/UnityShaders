using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UniGLTF;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Export the approved Round2 snapshots without resampling animations or changing materials/lights.
public static class ExportToonArtistReferences
{
    static readonly string[] Names = { "Arisa", "Warrior", "Mike", "CrazyCat", "SupercyanMale", "CityFemale", "CityMaleA", "CityMaleB", "CityPolice" };

    [Serializable] class Snapshot
    {
        public string sourceHierarchy;
        public Vector3 boundsCenter;
    }

    [MenuItem("Tools/URP Toon/Export artist renders and Base Color GLBs")]
    public static void Run()
    {
        string workspace = Path.GetFullPath("../..");
        string refsRoot = Argument("-artistRefs") ?? Path.Combine(workspace, "shaderNPR/comparison/unity/artist");
        string glbRoot = Argument("-artistGlbs") ?? Path.Combine(workspace, "shaderNPR/glb/artist");
        string[] names = (Argument("-artistNames") ?? string.Join(",", Names)).Split(',');
        if (names.Distinct().Count() != names.Length || names.Any(name => !Names.Contains(name)))
            throw new ArgumentException("Select unique approved Round2 character names.");
        bool async = EditorSettings.asyncShaderCompilation;
        try
        {
            EditorSettings.asyncShaderCompilation = false;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/ComparisonTools/Settings/ComparisonPipeline.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            GraphicsSettings.lightsUseLinearIntensity = true;
            foreach (string name in names) Export(name, refsRoot, glbRoot);
            Debug.Log($"[ToonArtist] SUCCESS: {names.Length} characters; orthographic refs, orbits, and Base Color GLBs.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
        finally
        {
            EditorSettings.asyncShaderCompilation = async;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static void Export(string name, string refsRoot, string glbRoot)
    {
        string settingsPath = $"Comparison/Round2/{name}/settings.json";
        var settings = JsonUtility.FromJson<Snapshot>(File.ReadAllText(settingsPath));
        var scene = EditorSceneManager.OpenScene($"Assets/ComparisonTools/Scenes/Round2/{name}.unity", OpenSceneMode.Single);
        var model = OrbitRenderMatch.FindSceneObject(scene.GetRootGameObjects(), settings.sourceHierarchy);
        var front = GameObject.Find("Comparison Camera").GetComponent<Camera>();
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
        front.orthographic = true;
        front.aspect = 1f;
        front.ResetProjectionMatrix();
        var back = new GameObject("Back Camera").AddComponent<Camera>();
        back.CopyFrom(front);
        back.enabled = false;
        // Horizontal half-turn retains the approved camera's elevation and framing.
        back.transform.position = settings.boundsCenter + Quaternion.Euler(0, 180, 0) * (front.transform.position - settings.boundsCenter);
        back.transform.LookAt(settings.boundsCenter);
        back.ResetProjectionMatrix();
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(light => light.enabled).ToArray();
        var key = lights.First(light => light.type == LightType.Directional);
        var warmup = RenderTexture.GetTemporary(2048, 2048, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
        try
        {
            front.targetTexture = warmup;
            for (int index = 0; index < 3; index++) front.Render();
        }
        finally { front.targetTexture = null; RenderTexture.ReleaseTemporary(warmup); }

        string destination = Path.Combine(refsRoot, name);
        OrbitRenderMatch.RenderPreparedModel(model, key, new[] {
            "-matchOutput", destination, "-matchAxes", "(-x,+y,+z)",
            "-matchFrameCount", "120", "-matchGeometrySamples", "8",
            "-matchResolution", "1024", "-matchMsaa", "8", "-matchSupersampling", "2"
        }, new[] { front, back }, lights.Where(light => light != key).ToArray());

        string output = Path.Combine(glbRoot, name);
        Directory.CreateDirectory(output);
        ExportGlb(model, name, Path.Combine(output, name + ".glb"));
        foreach (string file in new[] { "view0.png", "view1.png" })
            File.Copy(Path.Combine(destination, "ref", file), Path.Combine(output, file), true);
        string cameras = File.ReadAllText(Path.Combine(destination, "ref/cameras.json"));
        cameras = Regex.Replace(cameras, @"^[ \t]*""geometry_file"":\s*""[^""]+"",\r?\n", "", RegexOptions.Multiline);
        File.WriteAllText(Path.Combine(output, "cameras.json"), cameras);
        File.Copy(settingsPath, Path.Combine(destination, "settings.json"), true);
        Debug.Log($"[ToonArtist] Exported {name}: {destination}; {output}");
    }

    static void ExportGlb(GameObject source, string name, string output)
    {
        var model = new GameObject(name);
        var meshes = new List<Mesh>();
        var materials = new Dictionary<Material, Material>();
        try
        {
            foreach (var renderer in source.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy))
            {
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skinned) mesh = OrbitRenderMatch.CaptureSkinnedMesh(skinned);
                else if (renderer is MeshRenderer && renderer.GetComponent<MeshFilter>()?.sharedMesh != null)
                    mesh = Object.Instantiate(renderer.GetComponent<MeshFilter>().sharedMesh);
                else continue;
                meshes.Add(mesh);
                Matrix4x4 toRoot = source.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                mesh.vertices = mesh.vertices.Select(toRoot.MultiplyPoint3x4).ToArray();
                Matrix4x4 normalMatrix = toRoot.inverse.transpose;
                mesh.normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
                mesh.tangents = Array.Empty<Vector4>(); // regenerate from the final static geometry if needed
                mesh.ClearBlendShapes();
                mesh.boneWeights = Array.Empty<BoneWeight>();
                mesh.bindposes = Array.Empty<Matrix4x4>();
                if (toRoot.determinant < 0f)
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        var triangles = mesh.GetTriangles(sub);
                        for (int i = 0; i < triangles.Length; i += 3)
                            (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                        mesh.SetTriangles(triangles, sub);
                    }
                mesh.RecalculateBounds();
                // Fur uses repeated material slots to draw shells. Export only actual base mesh submeshes.
                var assigned = renderer.sharedMaterials.Take(mesh.subMeshCount).Select(original => {
                    if (!materials.TryGetValue(original, out var material))
                        materials.Add(original, material = BaseColorMaterial(original));
                    return material;
                }).ToArray();
                if (assigned.Length != mesh.subMeshCount) throw new InvalidOperationException("Missing submesh material: " + renderer.name);
                var child = new GameObject(renderer.name);
                child.transform.SetParent(model.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterials = assigned;
            }
            var data = new ExportingGltfData();
            using (var exporter = new gltfExporter(data, new GltfExportSettings {
                InverseAxis = Axes.X, DivideVertexBuffer = true, ExportVertexColor = false,
                FreezeMesh = false, FreezeMeshUseCurrentBlendShapeWeight = true
            }, materialExporter: new BaseColorExporter(), textureSerializer: new RuntimeTextureSerializer()))
            {
                exporter.Prepare(model);
                exporter.Export();
            }
            File.WriteAllBytes(output, data.ToGlbBytes());
        }
        finally
        {
            Object.DestroyImmediate(model);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            foreach (var material in materials.Values) Object.DestroyImmediate(material);
        }
    }

    static Material BaseColorMaterial(Material source)
    {
        if (source.IsKeywordEnabled("_VERTEX_COLOR"))
            throw new InvalidOperationException("Vertex color export must be enabled for " + source.name);
        var result = new Material(Shader.Find("Standard")) { name = source.name + " BaseColor" };
        result.SetColor("_Color", source.GetColor("_BaseColor"));
        result.SetTexture("_MainTex", source.GetTexture("_BaseMap"));
        result.SetTextureScale("_MainTex", source.GetTextureScale("_BaseMap"));
        result.SetTextureOffset("_MainTex", source.GetTextureOffset("_BaseMap"));
        result.SetFloat("_Metallic", 0);
        result.SetFloat("_Glossiness", 0);
        result.doubleSidedGI = source.HasProperty("_Cull") && source.GetFloat("_Cull") == 0;
        result.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : .5f);
        if (source.IsKeywordEnabled("_ALPHATEST_ON"))
        {
            result.SetOverrideTag("RenderType", "TransparentCutout");
            result.SetFloat("_Mode", 1);
            result.EnableKeyword("_ALPHATEST_ON");
        }
        else if (source.HasProperty("_Surface") && source.GetFloat("_Surface") > 0)
            result.SetOverrideTag("RenderType", "Transparent");
        return result;
    }

    class BaseColorExporter : IMaterialExporter
    {
        readonly BuiltInGltfMaterialExporter inner = new BuiltInGltfMaterialExporter();
        public glTFMaterial ExportMaterial(Material material, ITextureExporter textures, GltfExportSettings settings)
        {
            var result = inner.ExportMaterial(material, textures, settings);
            result.doubleSided = material.doubleSidedGI;
            return result;
        }
    }

    static string Argument(string name) => OrbitRenderMatch.BatchArgument(Environment.GetCommandLineArgs(), name, false);
}
