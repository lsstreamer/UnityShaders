using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using DELTation.ToonRP;
using DELTation.ToonRP.Shadows;

public static class RenderAikaComparison
{
    const string ScenePath = "Assets/Samples/Main/MainSampleScene.unity";
    const string OutputDirectory = "Comparison";
    internal const string ClothMaterialName = "Shoes CLOTH";
    const string ClothTextureFile = "aika_white_waist_body.png";
    const int Resolution = 1024;
    const int Supersampling = 2;

    public static void Run()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        bool dataset = Array.IndexOf(arguments, "-aikaDataset") >= 0;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var model = GameObject.Find("Aika Sailor Uniform");
        if (model == null) throw new InvalidOperationException("Aika Sailor Uniform was not found.");

        var renderers = model.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("Aika has no renderers.");

        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!renderers.Contains(renderer)) renderer.enabled = false;

        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            canvas.enabled = false;

        foreach (var existingCamera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            existingCamera.enabled = false;
        var cameraObject = GameObject.Find("Main Camera_Ortho") ?? GameObject.Find("Main Camera_Perspective");
        if (cameraObject == null) throw new InvalidOperationException("Configured camera was not found.");
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = true;
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        FrameCamera(camera, bounds);

        var outputDirectory = Path.Combine(Application.dataPath, "..", OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var originalMaterials = renderers.Select(renderer => renderer.sharedMaterials.ToArray()).ToArray();
        var projectPipeline = GraphicsSettings.currentRenderPipeline as ToonRenderPipelineAsset;
        if (projectPipeline == null) throw new InvalidOperationException("The scene did not select a Toon RP pipeline.");
        bool projectFog = RenderSettings.fog;
        var originalPipeline = EditorJsonUtility.ToJson(projectPipeline);
        var originalLights = CaptureLights();
        Material clothOverride = null;
        Texture2D clothTexture = null;
        Material[][] baseMaterials = Array.Empty<Material[]>();
        try
        {
            TunePipeline(projectPipeline);
            TuneLights();
            RecreatePipeline(projectPipeline);
            FrameCamera(camera, bounds);
            RenderSettings.fog = false;
            var cloth = originalMaterials.SelectMany(materials => materials).Distinct()
                .Single(material => material != null && material.name == ClothMaterialName);
            clothTexture = LoadClothTexture(cloth.GetTexture("_MainTexture"), arguments);
            clothOverride = new Material(cloth) { name = cloth.name };
            clothOverride.SetTexture("_MainTexture", clothTexture);
            var renderMaterials = originalMaterials.Select(materials => materials
                .Select(material => material == cloth ? clothOverride : material).ToArray()).ToArray();
            RestoreMaterials(renderers, renderMaterials);
            if (dataset)
            {
                int outputIndex = Array.IndexOf(arguments, "-matchOutput");
                if (outputIndex < 0 || outputIndex + 1 >= arguments.Length)
                    throw new ArgumentException("Pass -matchOutput for dataset export.");
                outputDirectory = Path.GetFullPath(arguments[outputIndex + 1]);
                Directory.CreateDirectory(outputDirectory);
                var backCamera = new GameObject("view1").AddComponent<Camera>();
                backCamera.CopyFrom(camera);
                backCamera.enabled = false;
                Vector3 pivot = bounds.center + Vector3.up * 0.06f;
                backCamera.transform.SetPositionAndRotation(
                    pivot + Quaternion.Euler(0f, 180f, 0f) * (camera.transform.position - pivot),
                    Quaternion.Euler(0f, 180f, 0f) * camera.transform.rotation);
                camera.name = "view0";
                var activeLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                    .Where(light => light.enabled && light.gameObject.activeInHierarchy).ToArray();
                var keyLight = activeLights.Single(light => light.type == LightType.Directional);
                OrbitRenderMatch.RenderPreparedModel(model, keyLight, arguments, new[] { camera, backCamera },
                    activeLights.Where(light => light != keyLight).ToArray());
            }
            else
            {
                baseMaterials = CreateBaseColorMaterials(renderMaterials);
                Render(camera, Path.Combine(outputDirectory, "aika_front_project_lit.png"));
                Render(camera, Path.Combine(outputDirectory, "aika_front_basecolor.png"), renderers, baseMaterials);
            }
            var report = new ComparisonReport
            {
                scene = ScenePath,
                model = model.name,
                pipelineAsset = AssetDatabase.GetAssetPath(projectPipeline),
                resolution = Resolution,
                supersampling = Supersampling,
                msaa = 8,
                cameraProjection = "orthographic",
                azimuths = new[] { 0f },
                elevation = 0f,
                frameFill = 0.88f,
                cameraPosition = camera.transform.position,
                cameraRotation = camera.transform.eulerAngles,
                cameraFieldOfView = camera.fieldOfView,
                cameraOrthographicSize = camera.orthographicSize,
                fogEnabled = RenderSettings.fog,
                boundsCenter = bounds.center,
                boundsSize = bounds.size,
                lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                    .Select(light => new LightRecord
                    {
                        name = light.name,
                        enabled = light.enabled && light.gameObject.activeInHierarchy,
                        position = light.transform.position,
                        rotation = light.transform.eulerAngles,
                        color = light.color,
                        intensity = light.intensity,
                        type = light.type.ToString(),
                        sourceSettings = EditorJsonUtility.ToJson(light)
                    }).ToArray(),
                materials = originalMaterials.SelectMany(materials => materials).Where(material => material != null).Distinct()
                    .Select(material => new MaterialRecord
                    {
                        path = AssetDatabase.GetAssetPath(material),
                        shader = material.shader.name,
                        sourceSettings = EditorJsonUtility.ToJson(material)
                    }).ToArray(),
                pipelineSettings = EditorJsonUtility.ToJson(projectPipeline),
                tuning = "Front-only orthographic preview; 8x MSAA and 2x supersampling; 4096 shadow atlas; 24-bit shadow map; smooth shadow ramp without striped shadow pattern; front-left key light for a clear light-dark boundary; weak neutral fill; fog disabled"
            };
            File.WriteAllText(Path.Combine(outputDirectory, "settings.json"), JsonUtility.ToJson(report, true));
        }
        finally
        {
            RenderSettings.fog = projectFog;
            RestoreMaterials(renderers, originalMaterials);
            if (clothOverride != null) UnityEngine.Object.DestroyImmediate(clothOverride);
            if (clothTexture != null) UnityEngine.Object.DestroyImmediate(clothTexture);
            RestoreLights(originalLights);
            EditorJsonUtility.FromJsonOverwrite(originalPipeline, projectPipeline);
            RecreatePipeline(projectPipeline);
            foreach (var material in baseMaterials.SelectMany(materials => materials).Where(material => material != null))
                UnityEngine.Object.DestroyImmediate(material);
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log($"[AikaComparison] Wrote front BaseColor and project-lit images to {outputDirectory}");
        EditorApplication.Exit(0);
    }

    internal static Texture2D LoadClothTexture(Texture original, string[] arguments)
    {
        string path = OrbitRenderMatch.BatchArgument(arguments, "-aikaClothTexture", false)
            ?? Path.Combine(Application.dataPath, "..", OutputDirectory, ClothTextureFile);
        byte[] pixels = File.ReadAllBytes(path);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false)
        {
            name = Path.GetFileNameWithoutExtension(path),
            filterMode = original.filterMode,
            wrapModeU = original.wrapModeU,
            wrapModeV = original.wrapModeV,
            anisoLevel = original.anisoLevel,
        };
        if (texture.LoadImage(pixels)) return texture;
        UnityEngine.Object.DestroyImmediate(texture);
        throw new InvalidDataException($"Could not load the cloth texture: {path}");
    }

    static Material[][] CreateBaseColorMaterials(Material[][] originals)
    {
        var shader = Shader.Find("Toon RP/Unlit");
        if (shader == null) throw new InvalidOperationException("Toon RP Unlit shader was not found.");
        return originals.Select(materials => materials.Select(original =>
        {
            if (original == null) return null;
            var material = new Material(shader) { name = $"BaseColor_{original.name}" };
            CopyTexture(original, material, "_MainTexture", "_MainTexture", "_MainTex", "_Texture");
            CopyColor(original, material, "_MainColor", "_MainColor", "_BaseColor", "_Albedo");
            CopyFloat(original, material, "_AlphaClipping");
            CopyFloat(original, material, "_AlphaClipThreshold");
            CopyFloat(original, material, "_RenderFace");
            CopyFloat(original, material, "_BlendSrc");
            CopyFloat(original, material, "_BlendDst");
            CopyFloat(original, material, "_ZWrite");
            material.renderQueue = original.renderQueue;
            if (original.IsKeywordEnabled("_ALPHATEST_ON")) material.EnableKeyword("_ALPHATEST_ON");
            if (original.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")) material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            return material;
        }).ToArray()).ToArray();
    }

    static void CopyTexture(Material source, Material destination, string destinationProperty, params string[] sourceProperties)
    {
        if (!destination.HasProperty(destinationProperty)) return;
        foreach (var property in sourceProperties)
        {
            if (!source.HasProperty(property) || source.GetTexture(property) == null) continue;
            destination.SetTexture(destinationProperty, source.GetTexture(property));
            destination.SetTextureScale(destinationProperty, source.GetTextureScale(property));
            destination.SetTextureOffset(destinationProperty, source.GetTextureOffset(property));
            return;
        }
    }

    static void CopyColor(Material source, Material destination, string destinationProperty, params string[] sourceProperties)
    {
        if (!destination.HasProperty(destinationProperty)) return;
        foreach (var property in sourceProperties)
        {
            if (!source.HasProperty(property)) continue;
            destination.SetColor(destinationProperty, source.GetColor(property));
            return;
        }
    }

    static void CopyFloat(Material source, Material destination, string property)
    {
        if (source.HasProperty(property) && destination.HasProperty(property)) destination.SetFloat(property, source.GetFloat(property));
    }

    static void RestoreMaterials(Renderer[] renderers, Material[][] materials)
    {
        for (int index = 0; index < renderers.Length; index++) renderers[index].sharedMaterials = materials[index];
    }

    static void Render(Camera camera, string path, Renderer[] renderers = null, Material[][] materials = null)
    {
        if (renderers != null) for (int index = 0; index < renderers.Length; index++) renderers[index].sharedMaterials = materials[index];
        var oldFlags = camera.clearFlags;
        var oldColor = camera.backgroundColor;
        var oldTarget = camera.targetTexture;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.055f, 0.055f, 1f);
        var target = new RenderTexture(Resolution * Supersampling, Resolution * Supersampling, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 8
        };
        camera.allowMSAA = true;
        camera.targetTexture = target;
        for (int index = 0; index < 3; index++) camera.Render();
        var resolved = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(target, resolved);
        RenderTexture.active = resolved;
        var image = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        RenderTexture.active = null;
        camera.targetTexture = oldTarget;
        camera.clearFlags = oldFlags;
        camera.backgroundColor = oldColor;
        UnityEngine.Object.DestroyImmediate(resolved);
        UnityEngine.Object.DestroyImmediate(target);
    }

    static void FrameCamera(Camera camera, Bounds bounds)
    {
        float radius = bounds.extents.magnitude;
        Vector3 target = bounds.center + Vector3.up * 0.06f;
        Vector3 back = Vector3.forward;
        float distance = radius * 3f;
        camera.transform.SetPositionAndRotation(target + back * distance, Quaternion.LookRotation(-back, Vector3.up));
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) / 0.88f;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.ResetProjectionMatrix();
        camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
        camera.farClipPlane = distance + radius * 2f;
        camera.aspect = 1f;
    }

    static void TunePipeline(ToonRenderPipelineAsset pipeline)
    {
        pipeline.CameraRendererSettings.Msaa = ToonCameraRendererSettings.MsaaMode._8x;
        pipeline.ShadowSettings.CrispAntiAliased = false;
        pipeline.ShadowSettings.Smoothness = 0.24f;
        pipeline.ShadowSettings.Pattern = null;
        pipeline.ShadowSettings.ShadowMaps.DepthBits = ToonShadowMapsSettings.ShadowMapBits._24;
        pipeline.ShadowSettings.ShadowMaps.Directional.AtlasSize = TextureSize._4096;
        pipeline.ShadowSettings.ShadowMaps.SoftShadows.Enabled = true;
        pipeline.ShadowSettings.ShadowMaps.SoftShadows.Quality = ToonShadowMapsSettings.SoftShadowsQuality.High;
        pipeline.ShadowSettings.ShadowMaps.SoftShadows.Mode = ToonShadowMapsSettings.SoftShadowsMode.Poisson;
        pipeline.ShadowSettings.ShadowMaps.SoftShadows.Spread = 1.0f;
    }

    static void TuneLights()
    {
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (!light.gameObject.activeInHierarchy) continue;
            if (light.type == LightType.Directional)
            {
                light.transform.rotation = Quaternion.Euler(30f, 135f, 0f);
                light.intensity = 1.15f;
                light.color = new Color(1f, 0.97f, 0.93f);
                light.shadows = LightShadows.Soft;
            }
            else if (light.type == LightType.Point)
            {
                light.intensity *= 0.08f;
                light.color = Color.Lerp(light.color, Color.white, 0.72f);
                light.shadows = LightShadows.None;
            }
        }
    }

    static void RecreatePipeline(ToonRenderPipelineAsset pipeline)
    {
        GraphicsSettings.defaultRenderPipeline = null;
        GraphicsSettings.defaultRenderPipeline = pipeline;
    }

    static LightRecord[] CaptureLights()
    {
        return UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Select(light => new LightRecord
        {
            name = light.name,
            enabled = light.enabled,
            position = light.transform.position,
            rotation = light.transform.eulerAngles,
            color = light.color,
            intensity = light.intensity,
            type = light.type.ToString(),
            sourceSettings = EditorJsonUtility.ToJson(light)
        }).ToArray();
    }

    static void RestoreLights(LightRecord[] records)
    {
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            var record = records.FirstOrDefault(item => item.name == light.name);
            if (record == null) continue;
            light.enabled = record.enabled;
            light.transform.position = record.position;
            light.transform.eulerAngles = record.rotation;
            light.color = record.color;
            light.intensity = record.intensity;
            EditorJsonUtility.FromJsonOverwrite(record.sourceSettings, light);
        }
    }

    [Serializable]
    class ComparisonReport
    {
        public string scene;
        public string model;
        public string pipelineAsset;
        public int resolution;
        public int supersampling;
        public int msaa;
        public string cameraProjection;
        public float[] azimuths;
        public float elevation;
        public float frameFill;
        public Vector3 cameraPosition;
        public Vector3 cameraRotation;
        public float cameraFieldOfView;
        public float cameraOrthographicSize;
        public bool fogEnabled;
        public Vector3 boundsCenter;
        public Vector3 boundsSize;
        public LightRecord[] lights;
        public MaterialRecord[] materials;
        public string pipelineSettings;
        public string tuning;
    }

    [Serializable]
    class LightRecord
    {
        public string name;
        public bool enabled;
        public Vector3 position;
        public Vector3 rotation;
        public Color color;
        public float intensity;
        public string type;
        public string sourceSettings;
    }

    [Serializable]
    class MaterialRecord
    {
        public string path;
        public string shader;
        public string sourceSettings;
    }
}
