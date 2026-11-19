using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UniGLTF;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExportAikaBaseColorGlb
{
    const string ScenePath = "Assets/Samples/Main/MainSampleScene.unity";
    const string ModelName = "Aika Sailor Uniform";
    const string DefaultOutput = "/workspace/shaderNPR/glb/artist/aika_sailor_uniform";

    public static void Run()
    {
        string output = Argument("-aikaGlbOutput") ?? DefaultOutput;
        Directory.CreateDirectory(output);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var source = GameObject.Find(ModelName);
        if (source == null) throw new InvalidOperationException($"Missing model {ModelName}");
        var model = new GameObject("aika_sailor_uniform");
        var renderers = source.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToArray();
        var originalMaterials = renderers.Select(renderer => renderer.sharedMaterials.ToArray()).ToArray();
        var exportMaterials = originalMaterials.Select(materials => materials.Select(CreateBaseColorMaterial).ToArray()).ToArray();
        var meshes = new Mesh[renderers.Length];
        Texture2D clothTexture = null;

        try
        {
            var cloth = exportMaterials.SelectMany(materials => materials)
                .Single(material => material.name == RenderAikaComparison.ClothMaterialName + " BaseColor");
            clothTexture = RenderAikaComparison.LoadClothTexture(cloth.GetTexture("_MainTex"), Environment.GetCommandLineArgs());
            cloth.SetTexture("_MainTex", clothTexture);
            for (int index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                var mesh = renderer is SkinnedMeshRenderer skinned
                    ? OrbitRenderMatch.CaptureSkinnedMesh(skinned)
                    : UnityEngine.Object.Instantiate(renderer.GetComponent<MeshFilter>().sharedMesh);
                meshes[index] = mesh;
                Matrix4x4 toRoot = source.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                mesh.vertices = mesh.vertices.Select(toRoot.MultiplyPoint3x4).ToArray();
                Matrix4x4 normalMatrix = toRoot.inverse.transpose;
                mesh.normals = mesh.normals.Select(normal => normalMatrix.MultiplyVector(normal).normalized).ToArray();
                mesh.ClearBlendShapes();
                mesh.boneWeights = Array.Empty<BoneWeight>();
                mesh.bindposes = Array.Empty<Matrix4x4>();
                mesh.RecalculateBounds();
                var child = new GameObject(renderer.name);
                child.transform.SetParent(model.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterials = exportMaterials[index];
            }
            var settings = new GltfExportSettings
            {
                InverseAxis = Axes.X,
                DivideVertexBuffer = true,
                ExportVertexColor = false,
                FreezeMesh = false,
                FreezeMeshUseCurrentBlendShapeWeight = true,
            };
            var data = new ExportingGltfData();
            using (var exporter = new gltfExporter(data, settings,
                materialExporter: new BuiltInGltfMaterialExporter(),
                textureSerializer: new RuntimeTextureSerializer()))
            {
                exporter.Prepare(model);
                exporter.Export();
            }
            File.WriteAllBytes(Path.Combine(output, "aika_sailor_uniform.glb"), data.ToGlbBytes());
            CopyComparisonFiles(output);
            Debug.Log($"[AikaBaseColorGlb] Exported {output}/aika_sailor_uniform.glb");
        }
        finally
        {
            foreach (var material in exportMaterials.SelectMany(materials => materials).Where(material => material != null))
                UnityEngine.Object.DestroyImmediate(material);
            if (clothTexture != null) UnityEngine.Object.DestroyImmediate(clothTexture);
            foreach (var mesh in meshes.Where(mesh => mesh != null)) UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(model);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        EditorApplication.Exit(0);
    }

    static Material CreateBaseColorMaterial(Material source)
    {
        var shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Standard shader is unavailable.");
        var material = new Material(shader) { name = source.name + " BaseColor" };
        Texture texture = FirstTexture(source, "_MainTexture", "_MainTex", "_Texture");
        if (texture != null) material.SetTexture("_MainTex", texture);
        Color color = FirstColor(source, "_MainColor", "_BaseColor", "_Albedo", "_Color");
        material.SetColor("_Color", color);
        material.SetFloat("_Glossiness", 0f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Cutoff", source.HasProperty("_AlphaClipThreshold") ? source.GetFloat("_AlphaClipThreshold") : 0.5f);
        string renderType = source.GetTag("RenderType", false);
        if (renderType == "Transparent")
        {
            material.SetOverrideTag("RenderType", "Transparent");
        }
        else if (renderType == "TransparentCutout" || source.IsKeywordEnabled("_ALPHATEST_ON"))
        {
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Mode", 1f);
            material.renderQueue = 2450;
            material.SetOverrideTag("RenderType", "TransparentCutout");
        }
        return material;
    }

    static Texture FirstTexture(Material source, params string[] properties)
    {
        foreach (var property in properties)
            if (source.HasProperty(property) && source.GetTexture(property) != null) return source.GetTexture(property);
        return null;
    }

    static Color FirstColor(Material source, params string[] properties)
    {
        foreach (var property in properties)
            if (source.HasProperty(property)) return source.GetColor(property);
        return Color.white;
    }

    static void CopyComparisonFiles(string output)
    {
        string source = Argument("-aikaRefSource") ?? "/workspace/shaderNPR/comparison/unity/artist/aika_sailor_uniform/ref";
        foreach (string file in new[] { "view0.png", "view1.png" })
            File.Copy(Path.Combine(source, file), Path.Combine(output, file), true);
        string cameras = File.ReadAllText(Path.Combine(source, "cameras.json"));
        cameras = Regex.Replace(cameras, @"^[ \t]*""geometry_file"":\s*""[^""]+"",\r?\n", "", RegexOptions.Multiline);
        File.WriteAllText(Path.Combine(output, "cameras.json"), cameras);
    }

    static string Argument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, name);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }
}
