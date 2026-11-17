using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

internal static class UtsSampleScene
{
    const string Package = "Packages/com.unity.toonshader";

    internal static void Render(string relativeScene, string[] args)
    {
        string assetScene = relativeScene;
        string staged = null;
        try
        {
            if (!relativeScene.StartsWith("Assets/", StringComparison.Ordinal))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(Package + "/package.json");
                if (package == null) throw new InvalidOperationException("Unity Toon Shader is not installed.");
                string samples = Path.Combine(package.resolvedPath, "Samples~", "Built-In_RP");
                string original = Under(samples, relativeScene);
                if (!File.Exists(original)) throw new FileNotFoundException("UTS sample scene not found.", original);

                string guid = File.ReadLines(original + ".meta").First(line => line.StartsWith("guid: ")).Substring(6).Trim();
                assetScene = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetScene))
                {
                    staged = $"Assets/__UtsRenderSample_{Guid.NewGuid():N}";
                    string[] folders = new[] { Path.GetDirectoryName(relativeScene) }
                        .Concat(ReadList(args, "-utsSampleDependencies")).Distinct().ToArray();
                    foreach (string folder in folders) CopyAssets(samples, staged, folder);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    assetScene = $"{staged}/{relativeScene}";
                }
            }
            else if (AssetDatabase.LoadAssetAtPath<SceneAsset>(assetScene) == null)
                throw new FileNotFoundException("Project sample scene not found.", assetScene);

            var scene = EditorSceneManager.OpenScene(assetScene);
            GameObject[] roots = scene.GetRootGameObjects();
            var model = OrbitRenderMatch.FindSceneObject(roots, OrbitRenderMatch.BatchArgument(args, "-matchModel"));
            var light = GetLight(roots, OrbitRenderMatch.BatchArgument(args, "-matchLight"));
            var additional = ReadList(args, "-utsAdditionalLights").Select(path => GetLight(roots, path)).ToArray();
            if (additional.Contains(light) || additional.Distinct().Count() != additional.Length)
                throw new ArgumentException("Scene lights must be distinct.");
            foreach (string path in ReadList(args, "-utsSampleHide")) OrbitRenderMatch.FindSceneObject(roots, path).SetActive(false);
            foreach (var root in roots)
                foreach (var camera in root.GetComponentsInChildren<Camera>()) camera.enabled = false;
            var materials = model.GetComponentsInChildren<Renderer>().SelectMany(item => item.sharedMaterials).Distinct().ToArray();
            if (materials.Length == 0 || materials.Any(material =>
                material == null || material.shader == null || !material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader)))
                throw new InvalidOperationException($"The sample materials did not load: {model.name}");
            // UTS releases predating the render-pipeline keyword still use the same shader asset.
            foreach (var material in materials)
                if (AssetDatabase.GetAssetPath(material.shader) == Package + "/Runtime/Shaders/UnityToon.shader")
                    material.EnableKeyword("UTS_RP_BUILTIN");
            Debug.Log($"[UtsRender] UTS sample {relativeScene}: {model.name}, {materials.Length} scene materials, " +
                $"lights: {string.Join(", ", additional.Prepend(light).Select(item => item.name))}.");

            string pose = OrbitRenderMatch.BatchArgument(args, "-utsSamplePose", false);
            if (pose != null)
            {
                var controller = model.GetComponent<Animator>()?.runtimeAnimatorController;
                var clip = controller?.animationClips.FirstOrDefault(item => item.name == pose);
                if (clip == null) throw new ArgumentException($"Sample clip {pose} not found on {model.name}.");
                float time = OrbitRenderMatch.BatchFloat(args, "-utsSamplePoseTime", 0f, 0f, clip.length);
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                try { AnimationMode.SampleAnimationClip(model, clip, time); }
                finally { AnimationMode.EndSampling(); }
            }
            var cameras = UtsRender.CreateReferenceCameras(model, args, PosedBounds(model));
            foreach (var camera in cameras) camera.renderingPath = RenderingPath.Forward;
            OrbitRenderMatch.RenderPreparedModel(model, light, args, cameras, additional);
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            if (staged != null)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                if (!AssetDatabase.DeleteAsset(staged) && Directory.Exists(staged))
                {
                    Directory.Delete(staged, recursive: true);
                    File.Delete(staged + ".meta");
                    AssetDatabase.Refresh();
                }
            }
        }
    }

    static string[] ReadList(string[] args, string option) =>
        (OrbitRenderMatch.BatchArgument(args, option, false) ?? "").Split(',')
            .Select(item => item.Trim()).Where(item => item.Length != 0).ToArray();

    static Light GetLight(GameObject[] roots, string path) =>
        OrbitRenderMatch.FindSceneObject(roots, path).GetComponent<Light>()
        ?? throw new ArgumentException($"{path} has no Light component.");

    static string Under(string root, string relative)
    {
        string directory = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(directory, relative));
        if (!path.StartsWith(directory, StringComparison.Ordinal))
            throw new ArgumentException($"Expected a path under {root}: {relative}");
        return path;
    }

    static void CopyAssets(string samples, string staged, string folder)
    {
        string original = Under(samples, folder);
        foreach (string file in Directory.EnumerateFiles(original, "*", SearchOption.AllDirectories))
        {
            // Static sampling only needs the scene assets, not demo UI or input assemblies.
            string asset = file.EndsWith(".meta", StringComparison.Ordinal) ? Path.GetFileNameWithoutExtension(file) : file;
            if (Path.GetExtension(asset) is ".cs" or ".asmdef") continue;
            string destination = Path.Combine(staged, Path.GetRelativePath(samples, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(file, destination, overwrite: true);
        }
    }

    static Bounds PosedBounds(GameObject model)
    {
        Bounds? bounds = null;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            Mesh baked = null;
            try
            {
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    skinned.updateWhenOffscreen = true;
                    baked = new Mesh();
                    skinned.BakeMesh(baked, false);
                    mesh = baked;
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter))
                    mesh = filter.sharedMesh;
                else continue;
                if (mesh == null) continue;

                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 point = renderer.transform.TransformPoint(vertex);
                    Bounds box = bounds ?? new Bounds(point, Vector3.zero);
                    box.Encapsulate(point);
                    bounds = box;
                }
            }
            finally { if (baked != null) UnityEngine.Object.DestroyImmediate(baked); }
        }
        return bounds ?? throw new ArgumentException($"No mesh vertices found on {model.name}.");
    }
}
