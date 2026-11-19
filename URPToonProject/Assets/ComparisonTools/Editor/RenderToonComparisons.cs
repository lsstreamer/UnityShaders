using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class RenderToonComparisons
{
    const int Resolution = 1024;
    const int Supersampling = 2;
    const string Commit = "6981f7dc931b53a0bd797725f4dac30b824041d7";
    const string Settings = "Assets/ComparisonTools/Settings";
    const string Round = "Round2";
    const string External = "Assets/Graphics/_External Assets/";
    const string Citizens = External + "Tiny_Toony_City_Demo/PolygonBlacksmith/TT_Citizens_demo/";
    const string FemaleIdle = Citizens + "animation/female/f_idle_A.FBX";
    const string MaleIdle = Citizens + "animation/male/m_idle_A.FBX";
    static readonly ModelSpec[] Models =
    {
        new ModelSpec("Arisa", "SceneArisa", "Arisa", External + "ArisaNew/Arisa.fbx", MaleIdle, "m_idle_A", 1.2f),
        new ModelSpec("Warrior", "Warrior", "Warrior", External + "RPG Characters - Nov 2020/Warrior.fbx", MaleIdle, "m_idle_A", 1.2f),
        new ModelSpec("Mike", "SampleScene", "Mike", External + "Animated Mech Pack - March 2021/FBX/Mike.fbx", null, "Idle", .6f),
        new ModelSpec("CrazyCat", "Fur", "Crazy_Cat", External + "Snowconesolid Assets/Crazy Cats Toon Pack/Mesh/Crazy_Cat.fbx", null, "CatPos_8_NormalStance", .15f),
        new ModelSpec("SupercyanMale", "Forest", "MaleFree1", External + "Supercyan Character Pack Free Sample/Models/free_male_1.FBX", MaleIdle, "m_idle_A", 1.2f),
        new ModelSpec("CityFemale", "TinyToonyCityDemo", "Characters/TT_demo_female", Citizens + "models/TT_demo_female.FBX", FemaleIdle, "f_idle_A", 1.2f),
        new ModelSpec("CityMaleA", "TinyToonyCityDemo", "Characters/TT_demo_male_A", Citizens + "models/TT_demo_male_A.FBX", MaleIdle, "m_idle_A", 1.2f),
        new ModelSpec("CityMaleB", "TinyToonyCityDemo", "Characters/TT_demo_male_B", Citizens + "models/TT_demo_male_B.FBX", MaleIdle, "m_idle_A", 1.2f),
        new ModelSpec("CityPolice", "TinyToonyCityDemo", "Characters/TT_demo_police", Citizens + "models/TT_demo_police.FBX", MaleIdle, "m_idle_A", 1.2f)
    };

    [MenuItem("Tools/URP Toon/Render all character comparisons")]
    public static void Run()
    {
        bool oldAsync = EditorSettings.asyncShaderCompilation;
        try
        {
            if (Application.unityVersion != "6000.3.24f1")
                Debug.LogWarning("[ToonComparison] Expected Unity 6000.3.24f1; actual " + Application.unityVersion);
            EditorSettings.asyncShaderCompilation = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var pipeline = ConfigurePipeline();
            string output = Argument("-comparisonOutput") ?? Path.GetFullPath("Comparison/" + Round);
            Directory.CreateDirectory(output);
            WriteInventory(output);
            var selected = (Argument("-comparisonNames") ?? string.Join(",", Models.Select(model => model.name))).Split(',');
            foreach (string name in selected)
                RenderModel(Models.Single(model => model.name == name), output, pipeline);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ToonComparison] SUCCESS: {selected.Length} character comparisons: {output}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
        finally
        {
            EditorSettings.asyncShaderCompilation = oldAsync;
        }
    }

    static UniversalRenderPipelineAsset ConfigurePipeline()
    {
        Directory.CreateDirectory(Settings);
        const string path = Settings + "/ComparisonPipeline.asset";
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (pipeline == null)
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "ComparisonForwardRenderer";
            renderer.renderingMode = RenderingMode.Forward;
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            AssetDatabase.CreateAsset(renderer, Settings + "/ComparisonRenderer.asset");
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.name = "ComparisonURP17";
            pipeline.msaaSampleCount = 8;
            pipeline.supportsHDR = false;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.maxAdditionalLightsCount = 8;
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.mainLightShadowmapResolution = 4096;
            pipeline.additionalLightsShadowmapResolution = 2048;
            pipeline.shadowDistance = 50;
            pipeline.shadowCascadeCount = 1;
            AssetDatabase.CreateAsset(pipeline, path);
        }
        GraphicsSettings.defaultRenderPipeline = pipeline;
        int quality = QualitySettings.GetQualityLevel();
        for (int index = 0; index < QualitySettings.names.Length; index++)
        {
            QualitySettings.SetQualityLevel(index, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(quality, false);
        GraphicsSettings.lightsUseLinearIntensity = true;
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
        AssetDatabase.SaveAssets();
        return pipeline;
    }

    static void RenderModel(ModelSpec spec, string output, UniversalRenderPipelineAsset pipeline)
    {
        string name = spec.name;
        string sourceScene = "Assets/Scenes/" + spec.scene + ".unity";
        Debug.Log("[ToonComparison] Opening " + sourceScene);
        var scene = EditorSceneManager.OpenScene(sourceScene, OpenSceneMode.Single);
        var parts = spec.hierarchy.Split('/');
        var model = scene.GetRootGameObjects().SingleOrDefault(root => root.name == parts[0]);
        for (int index = 1; model != null && index < parts.Length; index++)
            model = model.transform.Find(parts[index])?.gameObject;
        if (spec.instantiate)
            model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(spec.modelAsset));
        if (model == null) throw new InvalidOperationException("Missing original model " + name);
        for (Transform parent = model.transform; parent != null; parent = parent.parent)
            parent.gameObject.SetActive(true);
        foreach (var transform in model.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
        foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var animation in model.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
        PoseRecord pose = ApplyPose(model, spec);
        foreach (var lod in model.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
        var renderers = model.GetComponentsInChildren<Renderer>()
            .Where(renderer => renderer.enabled && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("No model geometry in " + name);
        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (!renderers.Contains(renderer)) renderer.enabled = false;
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None)) volume.enabled = false;
        foreach (var existingCamera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) existingCamera.enabled = false;

        var originalMaterials = renderers.Select(renderer => renderer.sharedMaterials.ToArray()).ToArray();
        var materials = originalMaterials.SelectMany(array => array).Distinct().ToArray();
        foreach (var material in materials)
        {
            if (material == null || material.shader == null)
                throw new InvalidOperationException("Missing original material/shader in " + name);
            if (!material.shader.name.StartsWith("DELTation/") && material.shader.name != "Universal Render Pipeline/Lit")
                throw new InvalidOperationException("Unexpected original shader: " + material.shader.name);
            if (material.shader.name == "Universal Render Pipeline/Lit")
                BaseShaderGUI.SetMaterialKeywords(material);
            Debug.Log($"[ToonComparison] {name}: {AssetDatabase.GetAssetPath(material)}; keywords={string.Join(",", material.shaderKeywords)}");
        }
        var originalSignatures = materials.Select(MaterialSignature).ToArray();
        bool twoBand = name != "Arisa";
        var variants = materials.ToDictionary(material => material, material => twoBand ? TwoBandMaterial(name, material) : material);
        var renderMaterials = originalMaterials.Select(array => array.Select(material => variants[material]).ToArray()).ToArray();
        var baselineShader = Shader.Find("Research/URP Toon Base Color");
        if (baselineShader == null) throw new InvalidOperationException("Base Color shader not imported.");
        var baselineMaterials = originalMaterials.Select(array => array.Select(original =>
        {
            var material = new Material(original) { name = original.name + " BaseColor", hideFlags = HideFlags.HideAndDontSave };
            material.shader = baselineShader;
            // Shader-local keywords must be enabled again after changing shader.
            foreach (string keyword in new[] { "_VERTEX_COLOR", "_ALPHATEST_ON", "_ALPHAPREMULTIPLY_ON" })
                if (original.IsKeywordEnabled(keyword)) material.EnableKeyword(keyword);
                else material.DisableKeyword(keyword);
            material.renderQueue = original.renderQueue;
            return material;
        }).ToArray()).ToArray();

        Bounds bounds = GeometryBounds(renderers);
        foreach (var renderer in renderers)
            Debug.Log($"[ToonComparison] Geometry {name}/{renderer.name}: bounds={renderer.bounds}; scale={renderer.transform.lossyScale}; active={renderer.gameObject.activeInHierarchy}");
        Debug.Log($"[ToonComparison] Framing {name}: geometry bounds={bounds}");
        var camera = new GameObject("Comparison Camera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.aspect = 1;
        Vector3 viewingDirection = Quaternion.Euler(0, model.transform.eulerAngles.y, 0) * new Vector3(.16f, .10f, 1f).normalized;
        camera.transform.position = bounds.center + viewingDirection * Mathf.Max(5, bounds.size.magnitude * 3);
        camera.transform.LookAt(bounds.center);
        camera.orthographicSize = CameraSize(camera, bounds) / .88f;
        camera.nearClipPlane = .01f;
        camera.farClipPlane = Mathf.Max(100, bounds.size.magnitude * 10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(232, 232, 232, 255);
        camera.allowHDR = false;
        camera.allowMSAA = true;
        camera.enabled = false;
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.SetRenderer(0);
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = true;
        cameraData.antialiasing = AntialiasingMode.None;
        RenderSettings.fog = false;
        string directory = Path.Combine(output, name);
        Directory.CreateDirectory(directory);
        try
        {
            Assign(renderers, renderMaterials);
            SaveImage(camera, Path.Combine(directory, "original_shader.png"));
            CheckShaderErrors(materials.Select(material => material.shader).Append(baselineShader));
            Assign(renderers, baselineMaterials);
            SaveImage(camera, Path.Combine(directory, "basecolor.png"));
            CheckShaderErrors(new[] { baselineShader });
        }
        finally
        {
            Assign(renderers, originalMaterials);
            foreach (var material in baselineMaterials.SelectMany(array => array)) Object.DestroyImmediate(material);
        }
        for (int index = 0; index < materials.Length; index++)
            if (originalSignatures[index] != MaterialSignature(materials[index]))
            {
                var before = originalSignatures[index].Split('\n');
                var after = MaterialSignature(materials[index]).Split('\n');
                Debug.LogError($"[ToonComparison] Material difference {materials[index].name}: BEFORE {string.Join("; ", before.Except(after))}; AFTER {string.Join("; ", after.Except(before))}");
                throw new InvalidOperationException("Original material parameters were modified: " + materials[index].name);
            }

        foreach (var pair in variants)
            if (MaterialSignature(pair.Key, true) != MaterialSignature(pair.Value, true))
                throw new InvalidOperationException("A property other than the two-band switch changed: " + pair.Key.name);
        Assign(renderers, renderMaterials);
        Directory.CreateDirectory("Assets/ComparisonTools/Scenes/" + Round);
        camera.enabled = true;
        string previewScene = "Assets/ComparisonTools/Scenes/" + Round + "/" + name + ".unity";
        if (!EditorSceneManager.SaveScene(scene, previewScene)) throw new IOException("Could not save preview scene.");
        var report = new Report
        {
            model = name, unity = Application.unityVersion, urp = "17.3.0", upstreamCommit = Commit,
            sourceModel = spec.modelAsset, sourceHierarchy = spec.hierarchy, pose = pose,
            round = Round, twoBand = twoBand, onlyTwoBandSwitchChanged = true,
            sourceScene = sourceScene, previewScene = previewScene, resolution = Resolution,
            supersampling = Supersampling, msaa = pipeline.msaaSampleCount,
            pipeline = AssetDatabase.GetAssetPath(pipeline), colorSpace = QualitySettings.activeColorSpace.ToString(),
            cameraPosition = camera.transform.position, cameraRotation = camera.transform.eulerAngles,
            orthographicSize = camera.orthographicSize, boundsCenter = bounds.center, boundsSize = bounds.size,
            background = camera.backgroundColor, ambientMode = RenderSettings.ambientMode.ToString(),
            ambientLight = RenderSettings.ambientLight, ambientIntensity = RenderSettings.ambientIntensity,
            skybox = AssetDatabase.GetAssetPath(RenderSettings.skybox),
            originalMaterialsUnchanged = true,
            baseline = "Unlit original _BaseMap * _BaseColor, optional RGB vertex color; same UV transform, alpha clipping, blend, culling and depth write; no lighting, shadows, fog, emission or post-processing.",
            notes = "Round 2: Arisa uses male idle for a neutral non-crossed stance and retains all original shader parameters. Other characters only disable _RampTriple/_RAMP_TRIPLE on separate material assets; thresholds, smoothness, colors, highlights, fur, lights and poses retained. White dance reference robot excluded. Base Color uses original source materials. " + "Camera reframed; non-model renderers, volumes, post-processing and fog disabled. Dedicated URP 17 Forward renderer, 4096 main shadow map, one cascade.",
            materials = materials.Select(source => variants[source]).Select(material => new MaterialRecord
            {
                path = AssetDatabase.GetAssetPath(material), shader = material.shader.name,
                sourcePath = AssetDatabase.GetAssetPath(variants.First(pair => pair.Value == material).Key),
                rampTriple = ReadFloat(material, "_RampTriple"),
                keywords = material.shaderKeywords, serializedSettings = EditorJsonUtility.ToJson(material),
                texture = AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")),
                baseColor = material.GetColor("_BaseColor"), shadowTint = material.HasProperty("_ShadowTint") ? material.GetColor("_ShadowTint") : Color.clear,
                ramp0 = ReadFloat(material, "_Ramp0"), ramp1 = ReadFloat(material, "_Ramp1"),
                rampSmoothness = ReadFloat(material, "_RampSmoothness"), renderQueue = material.renderQueue
            }).ToArray(),
            renderers = renderers.Select(renderer => new RendererRecord
            {
                path = AnimationUtility.CalculateTransformPath(renderer.transform, model.transform),
                materials = renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray()
            }).ToArray(),
            lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Select(light => new LightRecord
            {
                name = light.name, enabled = light.isActiveAndEnabled, type = light.type.ToString(),
                position = light.transform.position, rotation = light.transform.eulerAngles,
                color = light.color, intensity = light.intensity, shadows = light.shadows.ToString()
            }).ToArray()
        };
        File.WriteAllText(Path.Combine(directory, "settings.json"), JsonUtility.ToJson(report, true));
        Debug.Log($"[ToonComparison] Saved {name}: {materials.Length} original materials; bounds={bounds}; {directory}");
    }

    static PoseRecord ApplyPose(GameObject model, ModelSpec spec)
    {
        string path = spec.animationAsset ?? spec.modelAsset;
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
        var clip = clips.FirstOrDefault(candidate => candidate.name == spec.clip)
            ?? clips.FirstOrDefault(candidate => candidate.name.EndsWith("|" + spec.clip));
        if (clip == null)
        {
            Debug.LogWarning($"[ToonComparison] {spec.name}: no suitable clip; retaining source pose. Available: {string.Join(",", clips.Select(item => item.name))}");
            return new PoseRecord { method = "source_pose", notes = "No compatible bundled animation; original pose retained, no manual bone posing." };
        }
        float time = Mathf.Min(spec.time, clip.length * .8f);
        var position = model.transform.localPosition;
        var rotation = model.transform.localRotation;
        var scale = model.transform.localScale;
        string method;
        if (clip.humanMotion)
        {
            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(spec.modelAsset).OfType<Avatar>()
                    .FirstOrDefault(avatar => avatar.isValid && avatar.isHuman);
            if (animator.avatar == null)
                return new PoseRecord { method = "source_pose", notes = "No valid humanoid avatar for the bundled clip; source pose retained." };
            string folder = "Assets/ComparisonTools/Poses/" + Round;
            Directory.CreateDirectory(folder);
            string controllerPath = folder + "/" + spec.name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0);
            animator.Play(Animator.StringToHash(controller.layers[0].stateMachine.defaultState.name), 0, clip.length > 0 ? time / clip.length : 0);
            animator.Update(0);
            animator.enabled = false;
            method = path == spec.modelAsset ? "bundled_humanoid_animation" : "bundled_humanoid_retarget";
        }
        else
        {
            clip.SampleAnimation(model, time);
            method = "bundled_generic_animation";
        }
        model.transform.SetLocalPositionAndRotation(position, rotation);
        model.transform.localScale = scale;
        Debug.Log($"[ToonComparison] Pose {spec.name}: {method}, {clip.name} at {time:F3}s");
        return new PoseRecord { method = method, asset = path, clip = clip.name, timeSeconds = time,
            notes = "Bundled animation sampled once; root transform preserved; identical static pose for both images." };
    }

    static void WriteInventory(string output)
    {
        var items = new List<InventoryItem>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Graphics" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) continue;
            var meshes = model.GetComponentsInChildren<Renderer>(true);
            if (meshes.Length == 0) continue;
            items.Add(new InventoryItem { path = path, rendererCount = meshes.Length,
                materials = meshes.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null)
                    .Distinct().Select(material => $"{material.name} | {material.shader.name} | {AssetDatabase.GetAssetPath(material)}").ToArray(),
                clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(clip => !clip.name.StartsWith("__preview__")).Select(clip => clip.name).ToArray() });
        }
        File.WriteAllText(Path.Combine(output, "character_inventory.json"), JsonUtility.ToJson(new Inventory
            { characters = Models, meshAssets = items.ToArray(), notes = "Round 2 includes nine main sample characters. The white dance reference robot is excluded at the user's request; Mike is retained. Arisa changes pose; the other eight only switch to two-band diffuse shading." }, true));
    }

    static float ReadFloat(Material material, string property) => material.HasProperty(property) ? material.GetFloat(property) : 0;

    static void SaveImage(Camera camera, string path)
    {
        var target = new RenderTexture(Resolution * Supersampling, Resolution * Supersampling, 24,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
        var resolved = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var pixels = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false, false);
        try
        {
            target.Create();
            resolved.Create();
            camera.targetTexture = target;
            for (int index = 0; index < 4; index++) camera.Render();
            camera.targetTexture = null;
            Graphics.Blit(target, resolved);
            RenderTexture.active = resolved;
            pixels.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
            pixels.Apply();
            var colors = pixels.GetPixels32();
            int uniqueColors = colors.Distinct().Take(100).Count();
            Debug.Log($"[ToonComparison] Image {path}: uniqueColors(up to 100)={uniqueColors}; center={colors[Resolution * Resolution / 2 + Resolution / 2]}; corner={colors[0]}");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            if (colors.Count(color => color.r > 240 && color.g < 12 && color.b > 240) > 100)
                throw new InvalidOperationException("Magenta shader error: " + path);
            if (uniqueColors < 16)
                throw new InvalidOperationException("Empty or invalid render: " + path);
            Debug.Log("[ToonComparison] Wrote " + path);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = null;
            target.Release();
            resolved.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(resolved);
            Object.DestroyImmediate(pixels);
        }
    }

    static void Assign(Renderer[] renderers, Material[][] materials)
    {
        for (int index = 0; index < renderers.Length; index++) renderers[index].sharedMaterials = materials[index];
    }

    static Bounds GeometryBounds(Renderer[] renderers)
    {
        Bounds? bounds = null;
        foreach (var renderer in renderers)
        {
            Vector3[] vertices;
            if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
            {
                skin.updateWhenOffscreen = true;
                // Skin directly into world space. Imported FBX scale and stale
                // renderer bounds otherwise give wrong framing after posing.
                var mesh = skin.sharedMesh;
                var source = mesh.vertices;
                var weights = mesh.boneWeights;
                var bindposes = mesh.bindposes;
                var bones = skin.bones;
                var matrices = bones.Select((bone, index) => bone != null && index < bindposes.Length
                    ? bone.localToWorldMatrix * bindposes[index] : renderer.localToWorldMatrix).ToArray();
                if (weights.Length == source.Length && matrices.Length > 0)
                {
                    vertices = new Vector3[source.Length];
                    for (int index = 0; index < source.Length; index++)
                    {
                        var weight = weights[index];
                        vertices[index] = Skin(weight.boneIndex0, weight.weight0) + Skin(weight.boneIndex1, weight.weight1) +
                            Skin(weight.boneIndex2, weight.weight2) + Skin(weight.boneIndex3, weight.weight3);
                        Vector3 Skin(int bone, float value) => value > 0 && bone < matrices.Length
                            ? matrices[bone].MultiplyPoint3x4(source[index]) * value : Vector3.zero;
                    }
                }
                else vertices = source.Select(vertex => renderer.transform.TransformPoint(vertex)).ToArray();
            }
            else
            {
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                vertices = mesh.vertices.Select(vertex => renderer.transform.TransformPoint(vertex)).ToArray();
            }
            foreach (Vector3 vertex in vertices)
                if (bounds == null) bounds = new Bounds(vertex, Vector3.zero);
                else { var value = bounds.Value; value.Encapsulate(vertex); bounds = value; }
        }
        var result = bounds ?? throw new InvalidOperationException("Cannot compute model bounds.");
        result.Expand(result.size.magnitude * .02f);
        return result;
    }

    static Material TwoBandMaterial(string model, Material source)
    {
        string folder = "Assets/ComparisonTools/Materials/" + Round + "/" + model;
        Directory.CreateDirectory(folder);
        string path = folder + "/" + source.name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
        }
        else EditorUtility.CopySerialized(source, material);
        material.name = source.name + " (Two Band)";
        if (material.HasProperty("_RampTriple")) material.SetFloat("_RampTriple", 0);
        material.DisableKeyword("_RAMP_TRIPLE");
        EditorUtility.SetDirty(material);
        return material;
    }

    static string MaterialSignature(Material material) => MaterialSignature(material, false);

    static string MaterialSignature(Material material, bool ignoreRampTriple)
    {
        // Unity can serialize previously implicit shader defaults on first draw.
        // Compare every effective shader property, rather than serialization layout.
        var values = new List<string>
        {
            AssetDatabase.GetAssetPath(material.shader), material.renderQueue.ToString(),
            string.Join(",", material.shaderKeywords.Where(keyword => material.shader.keywordSpace.FindKeyword(keyword).isValid &&
                (!ignoreRampTriple || keyword != "_RAMP_TRIPLE")).OrderBy(keyword => keyword)),
            material.GetTag("RenderType", false, "")
        };
        for (int index = 0; index < material.shader.GetPropertyCount(); index++)
        {
            string property = material.shader.GetPropertyName(index);
            if (ignoreRampTriple && property == "_RampTriple") continue;
            string value;
            switch (material.shader.GetPropertyType(index))
            {
                case ShaderPropertyType.Color:
                    value = material.GetColor(property).ToString("R");
                    break;
                case ShaderPropertyType.Vector:
                    value = material.GetVector(property).ToString("R");
                    break;
                case ShaderPropertyType.Texture:
                    value = AssetDatabase.GetAssetPath(material.GetTexture(property)) + ";" +
                        material.GetTextureScale(property).ToString("R") + ";" + material.GetTextureOffset(property).ToString("R");
                    break;
                default:
                    value = material.GetFloat(property).ToString("R", CultureInfo.InvariantCulture);
                    break;
            }
            values.Add(property + "=" + value);
        }
        return string.Join("\n", values);
    }

    static float CameraSize(Camera camera, Bounds bounds)
    {
        float extent = 0;
        for (int index = 0; index < 8; index++)
        {
            Vector3 offset = Vector3.Scale(bounds.extents, new Vector3((index & 1) == 0 ? -1 : 1,
                (index & 2) == 0 ? -1 : 1, (index & 4) == 0 ? -1 : 1));
            Vector3 local = camera.transform.InverseTransformPoint(bounds.center + offset);
            extent = Mathf.Max(extent, Mathf.Abs(local.x), Mathf.Abs(local.y));
        }
        return extent;
    }

    static void CheckShaderErrors(IEnumerable<Shader> shaders)
    {
        foreach (var shader in shaders.Distinct())
        {
            if (!ShaderUtil.ShaderHasError(shader)) continue;
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                Debug.LogError($"[ToonComparison] {shader.name}: {message.message} ({message.file}:{message.line})");
            throw new InvalidOperationException("Shader compile error: " + shader.name);
        }
    }

    static string Argument(string name)
    {
        var arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, name);
        return index < 0 ? null : arguments[index + 1];
    }

    [Serializable] class Report
    {
        public string model, unity, urp, upstreamCommit, sourceScene, previewScene, pipeline, colorSpace;
        public string sourceModel, sourceHierarchy;
        public string round;
        public bool twoBand, onlyTwoBandSwitchChanged;
        public PoseRecord pose;
        public string baseline, notes, ambientMode, skybox;
        public int resolution, supersampling, msaa;
        public bool originalMaterialsUnchanged;
        public float orthographicSize, ambientIntensity;
        public Vector3 cameraPosition, cameraRotation, boundsCenter, boundsSize;
        public Color background, ambientLight;
        public MaterialRecord[] materials;
        public RendererRecord[] renderers;
        public LightRecord[] lights;
    }
    [Serializable] class ModelSpec
    {
        public string name, scene, hierarchy, modelAsset, animationAsset, clip;
        public float time;
        public bool instantiate;
        public ModelSpec(string name, string scene, string hierarchy, string modelAsset, string animationAsset, string clip, float time, bool instantiate = false)
        {
            this.name = name; this.scene = scene; this.hierarchy = hierarchy; this.modelAsset = modelAsset;
            this.animationAsset = animationAsset; this.clip = clip; this.time = time; this.instantiate = instantiate;
        }
    }
    [Serializable] class PoseRecord { public string method, asset, clip, notes; public float timeSeconds; }
    [Serializable] class Inventory { public ModelSpec[] characters; public InventoryItem[] meshAssets; public string notes; }
    [Serializable] class InventoryItem { public string path; public int rendererCount; public string[] clips, materials; }
    [Serializable] class MaterialRecord
    {
        public string path, sourcePath, shader, texture, serializedSettings;
        public string[] keywords;
        public Color baseColor, shadowTint;
        public float ramp0, ramp1, rampSmoothness, rampTriple;
        public int renderQueue;
    }
    [Serializable] class RendererRecord { public string path; public string[] materials; }
    [Serializable] class LightRecord
    {
        public string name, type, shadows;
        public bool enabled;
        public Vector3 position, rotation;
        public Color color;
        public float intensity;
    }
}
