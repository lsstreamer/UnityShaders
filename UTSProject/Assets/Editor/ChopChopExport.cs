using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniGLTF;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ChopChopExport
{
    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        string output = Path.GetFullPath(OrbitRenderMatch.BatchArgument(args, "-chopExportOutput"));
        string[] names = { "PigChef", "BardHare", "PhoenixChick", "RockCritter", "PlantCritter", "LegendaryChef", "Townsfolk_F", "Townsfolk_M" };
        foreach (string name in names) Export(name, output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    static void Export(string name, string output)
    {
        var scene = EditorSceneManager.OpenScene($"Assets/ChopChop/PreviewScenes/{name}.unity");
        var model = OrbitRenderMatch.FindSceneObject(scene.GetRootGameObjects(), name);
        var root = new GameObject(name + " Base Color");
        var meshes = new List<Mesh>();
        var materials = new Dictionary<Material, Material>();
        try
        {
            Bounds? bounds = null;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh { indexFormat = skinned.sharedMesh.indexFormat };
                    skinned.BakeMesh(mesh, true);
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter))
                    mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                else continue;
                meshes.Add(mesh);
                Matrix4x4 transform = model.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                for (int index = 0; index < vertices.Length; index++)
                {
                    vertices[index] = transform.MultiplyPoint3x4(vertices[index]);
                    Bounds box = bounds ?? new Bounds(vertices[index], Vector3.zero);
                    box.Encapsulate(vertices[index]);
                    bounds = box;
                }
                Vector3[] normals = mesh.normals;
                Matrix4x4 normalTransform = transform.inverse.transpose;
                for (int index = 0; index < normals.Length; index++)
                    normals[index] = normalTransform.MultiplyVector(normals[index]).normalized;
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.colors = null;
                mesh.tangents = null;
                mesh.ClearBlendShapes();
                mesh.RecalculateBounds();
                var item = new GameObject(renderer.name);
                item.transform.SetParent(root.transform, false);
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                item.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials.Select(original =>
                {
                    if (!materials.TryGetValue(original, out Material material))
                    {
                        material = BaseColor(original);
                        materials.Add(original, material);
                    }
                    return material;
                }).ToArray();
            }
            if (!bounds.HasValue) throw new InvalidOperationException($"No visible meshes on {name}.");
            string folder = Path.Combine(output, name);
            Directory.CreateDirectory(folder);
            var data = new ExportingGltfData();
            using (var exporter = new gltfExporter(data, new GltfExportSettings { InverseAxis = Axes.X },
                materialExporter: new BuiltInGltfMaterialExporter()))
            {
                exporter.Prepare(root);
                exporter.Export();
            }
            File.WriteAllBytes(Path.Combine(folder, name + ".glb"), data.ToGlbBytes());
            File.WriteAllText(Path.Combine(folder, "export_mapping.json"), JsonUtility.ToJson(new Mapping {
                localToWorld = model.transform.localToWorldMatrix,
                boundsCenter = bounds.Value.center,
                boundsSize = bounds.Value.size,
                worldScale = model.transform.lossyScale.x
            }, true));
            Debug.Log($"[ChopChopExport] {name}: {meshes.Count} meshes, {materials.Count} Base Color materials, {bounds.Value}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            foreach (var material in materials.Values) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    static Material BaseColor(Material original)
    {
        string textureProperty = original.HasProperty("_MainTexture") ? "_MainTexture" : "_BaseMap";
        var texture = original.GetTexture(textureProperty);
        if (texture == null) throw new InvalidOperationException($"Missing Base Color texture on {original.name}.");
        var material = new Material(Shader.Find("Standard")) { name = original.name + " Base Color" };
        material.mainTexture = texture;
        material.color = textureProperty == "_BaseMap" ? original.GetColor("_BaseColor") : Color.white;
        material.mainTextureScale = original.GetTextureScale(textureProperty);
        material.mainTextureOffset = original.GetTextureOffset(textureProperty);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0f);
        bool transparent = original.GetTag("RenderType", false, "") == "Transparent" ||
            (original.HasProperty("_Surface") && original.GetFloat("_Surface") > .5f);
        if (transparent) material.SetOverrideTag("RenderType", "Transparent");
        return material;
    }

    [Serializable]
    class Mapping
    {
        public Matrix4x4 localToWorld;
        public Vector3 boundsCenter, boundsSize;
        public float worldScale;
    }
}
