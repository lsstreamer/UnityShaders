using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class UtsRender
{
    const string StagedModel = "Assets/__ShaderNprInput.glb";
    const string ToonShader = "Packages/com.unity.toonshader/Runtime/Shaders/UnityToon.shader";

    public static void Run()
    {
        if (GraphicsSettings.currentRenderPipeline != null)
            throw new InvalidOperationException("Use the Built-in Render Pipeline for this project.");
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
            throw new InvalidOperationException($"Expected Vulkan, got {SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}.");

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ToonShader);
        if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException($"The Unity Toon Shader failed to load: {ToonShader}");

        string[] args = Environment.GetCommandLineArgs();
        string source = Path.GetFullPath(OrbitRenderMatch.BatchArgument(args, "-matchGlb"));
        bool replay = OrbitRenderMatch.BatchArgument(args, "-matchCameras", false) != null;
        var toon = (
            baseStep: OrbitRenderMatch.BatchFloat(args, "-utsBaseStep", 0.61f, 0f, 1f),
            shadeStep: OrbitRenderMatch.BatchFloat(args, "-utsShadeStep", 0.37f, 0f, 1f),
            baseFeather: OrbitRenderMatch.BatchFloat(args, "-utsBaseFeather", 0.045f, 0f, 1f),
            shadeFeather: OrbitRenderMatch.BatchFloat(args, "-utsShadeFeather", 0.03f, 0f, 1f),
            outlineWidth: OrbitRenderMatch.BatchFloat(args, "-utsOutlineWidth", 1f, 0f)
        );
        string staged = Path.Combine(Application.dataPath, Path.GetFileName(StagedModel));
        var materials = new Dictionary<Material, Material>();
        GameObject model = null;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            Debug.Log($"[UtsRender] Importing {Path.GetFileName(source)} and preparing UTS materials.");
            File.Copy(source, staged, true);
            AssetDatabase.ImportAsset(StagedModel, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(StagedModel);
            if (asset == null) throw new InvalidOperationException($"glTFast could not import {source}.");
            // Keep the imported scene root's own glTF transform inside the alignment root.
            model = new GameObject(Path.GetFileNameWithoutExtension(source));
            GameObject imported = UnityEngine.Object.Instantiate(asset, model.transform, false);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                for (int index = 0; index < slots.Length; index++)
                {
                    var original = slots[index];
                    if (original == null) throw new InvalidOperationException($"Missing imported material on {renderer.name}.");
                    if (!materials.TryGetValue(original, out var material))
                    {
                        material = CreateToonMaterial(shader, original, toon);
                        materials.Add(original, material);
                    }
                    slots[index] = material;
                }
                renderer.sharedMaterials = slots;
            }
            if (materials.Count == 0) throw new InvalidOperationException($"No renderable materials in {source}.");
            ApplySlotMaterials(imported.transform, renderers, args);
            Debug.Log($"[UtsRender] {model.name}: {materials.Count} materials using {shader.name} on {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}).");
            if (replay)
                OrbitRenderMatch.RenderPreparedModel(model, null, args);
            else
            {
                Camera[] refs = CreateReferenceCameras(model, args);
                var light = new GameObject("Key Light").AddComponent<Light>();
                light.transform.SetParent(model.transform);
                light.type = LightType.Directional;
                float pitch = OrbitRenderMatch.BatchFloat(args, "-utsLightPitch", 40f);
                float yaw = OrbitRenderMatch.BatchFloat(args, "-utsLightYaw", -30f);
                light.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                light.intensity = OrbitRenderMatch.BatchFloat(args, "-utsLightIntensity", 1f, 0f);
                string lightColor = OrbitRenderMatch.BatchArgument(args, "-utsLightColor", false);
                if (lightColor != null) light.color = OrbitRenderMatch.ParseBatchRgb(lightColor, "-utsLightColor");
                OrbitRenderMatch.RenderPreparedModel(model, light, args, refs);
            }
        }
        finally
        {
            if (model != null) UnityEngine.Object.DestroyImmediate(model);
            foreach (var material in materials.Values) UnityEngine.Object.DestroyImmediate(material);
            AssetDatabase.DeleteAsset(StagedModel);
        }
    }

    static void ApplySlotMaterials(Transform root, Renderer[] renderers, string[] args)
    {
        var assigned = new HashSet<(string, int)>();
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] != "-utsSlotMaterial") continue;
            if (index + 3 >= args.Length || !int.TryParse(args[index + 2], NumberStyles.None, CultureInfo.InvariantCulture, out int slot))
                throw new ArgumentException("-utsSlotMaterial requires a renderer path, a zero-based slot and a .mat asset path.");
            string path = args[index + 1];
            string assetPath = args[index + 3];
            index += 3;
            if (!assigned.Add((path, slot))) throw new ArgumentException($"Duplicate material assignment for {path}, slot {slot}.");
            Renderer[] matches = renderers.Where(renderer => AnimationUtility.CalculateTransformPath(renderer.transform, root) == path).ToArray();
            if (matches.Length != 1) throw new ArgumentException($"Expected one renderer at {path}, found {matches.Length}.");
            var renderer = matches[0];
            var slots = renderer.sharedMaterials;
            if (slot >= slots.Length) throw new ArgumentException($"{path} has {slots.Length} material slots, cannot assign {slot}.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null || material.shader == null || !material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader))
                throw new ArgumentException($"Cannot load a supported material at {assetPath}.");
            slots[slot] = material;
            renderer.sharedMaterials = slots;
            Debug.Log($"[UtsRender] {path} slot {slot}: {assetPath} ({material.shader.name}).");
        }
    }

    static Camera[] CreateReferenceCameras(GameObject model, string[] args)
    {
        string[] azimuths = (OrbitRenderMatch.BatchArgument(args, "-matchRefAzimuths", false) ?? "30,210").Split(',');
        float elevation = OrbitRenderMatch.BatchFloat(args, "-matchRefElevation", 15f, -90f, 90f);
        if (Mathf.Abs(elevation) >= 90f) throw new ArgumentException("-matchRefElevation must be between -90 and 90 (exclusive).");
        float fill = OrbitRenderMatch.BatchFloat(args, "-matchFrameFill", 0.9f, float.Epsilon, 1f);
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        float radius = bounds.extents.magnitude;
        if (radius <= 0f) throw new InvalidOperationException("The model has zero size.");

        float tilt = elevation * Mathf.Deg2Rad;
        Vector3 extent = bounds.extents;
        float horizontal = new Vector2(extent.x, extent.z).magnitude;
        float vertical = extent.y * Mathf.Cos(tilt) + horizontal * Mathf.Abs(Mathf.Sin(tilt));
        float halfSize = Mathf.Max(horizontal, vertical) / fill;
        if (float.IsInfinity(halfSize)) throw new ArgumentException("-matchFrameFill is too small for this model.");
        var refs = new Camera[azimuths.Length];
        for (int index = 0; index < refs.Length; index++)
        {
            if (!float.TryParse(azimuths[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float azimuth) ||
                float.IsNaN(azimuth) || float.IsInfinity(azimuth))
                throw new ArgumentException($"Invalid reference azimuth: {azimuths[index]}.");
            Vector3 horizontalBack = Quaternion.AngleAxis(azimuth % 360f, Vector3.up) * Vector3.forward;
            Vector3 back = horizontalBack * Mathf.Cos(tilt) + Vector3.up * Mathf.Sin(tilt);
            var camera = new GameObject($"view{index}").AddComponent<Camera>();
            camera.transform.SetParent(model.transform);
            camera.enabled = false;
            camera.transform.SetPositionAndRotation(bounds.center + back * (3f * radius), Quaternion.LookRotation(-back, Vector3.up));
            camera.orthographic = true;
            camera.aspect = 1f;
            camera.orthographicSize = halfSize;
            camera.nearClipPlane = radius;
            camera.farClipPlane = 5f * radius;
            refs[index] = camera;
        }
        return refs;
    }

    static Material CreateToonMaterial(
        Shader shader, Material source, (float baseStep, float shadeStep, float baseFeather, float shadeFeather, float outlineWidth) toon)
    {
        var material = new Material(shader) { name = source.name + " (UTS)" };
        Color tint = source.HasProperty("baseColorFactor") ? source.GetColor("baseColorFactor") : Color.white;
        material.SetTexture("_MainTex", source.mainTexture);
        material.SetTextureScale("_MainTex", source.mainTextureScale);
        material.SetTextureOffset("_MainTex", source.mainTextureOffset);
        material.SetColor("_BaseColor", tint);
        material.SetColor("_1st_ShadeColor", new Color(tint.r * 0.76f, tint.g * 0.78f, tint.b * 0.86f, tint.a));
        material.SetColor("_2nd_ShadeColor", new Color(tint.r * 0.49f, tint.g * 0.53f, tint.b * 0.67f, tint.a));
        material.SetFloat("_Use_BaseAs1st", 1f);
        material.SetFloat("_Use_1stAs2nd", 1f);
        material.SetFloat("_BaseColor_Step", toon.baseStep);
        material.SetFloat("_ShadeColor_Step", toon.shadeStep);
        material.SetFloat("_BaseShade_Feather", toon.baseFeather);
        material.SetFloat("_1st2nd_Shades_Feather", toon.shadeFeather);
        material.SetFloat("_Outline_Width", toon.outlineWidth);
        material.SetColor("_Outline_Color", new Color(0.10f, 0.09f, 0.15f));
        if (source.HasProperty("_CullMode")) material.SetInteger("_CullMode", (int)source.GetFloat("_CullMode"));
        material.EnableKeyword("UTS_RP_BUILTIN");
        material.EnableKeyword("_OUTLINE_NML");

        bool cutout = source.IsKeywordEnabled("_ALPHATEST_ON");
        bool blended = source.renderQueue >= (int)RenderQueue.Transparent;
        if (cutout || blended)
        {
            // The UTS trans-clipping variant reads the original base texture's alpha.
            float cutoff = cutout ? source.GetFloat("alphaCutoff") : 0f;
            material.SetFloat("_IsBaseMapAlphaAsClippingMask", 1f);
            material.SetFloat("_Clipping_Level", 0.5f - cutoff);
            material.SetInteger("_ClippingMode", 2);
            material.SetInteger("_ZWriteMode", blended ? 0 : 1);
            material.SetInteger("_TransparentEnabled", blended ? 1 : 0);
            material.EnableKeyword("_IS_CLIPPING_TRANSMODE");
            material.renderQueue = (int)(blended ? RenderQueue.Transparent : RenderQueue.AlphaTest);
        }
        else material.EnableKeyword("_IS_CLIPPING_OFF");
        if (cutout || blended || toon.outlineWidth == 0f) material.EnableKeyword("_DISABLE_OUTLINE");
        return material;
    }
}
