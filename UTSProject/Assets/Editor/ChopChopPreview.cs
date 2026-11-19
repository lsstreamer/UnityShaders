using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ChopChopPreview
{
    const string Root = "Assets/ChopChop";
    static readonly string[] Names = { "PigChef", "BardHare", "PhoenixChick", "RockCritter", "SlimeCritter", "PlantCritter", "LegendaryChef", "Townsfolk_F", "Townsfolk_M" };
    static readonly string[] Prefabs = { "", "", "", "", "", "", "", "", "" };

    [MenuItem("Tools/Chop Chop/Render character comparisons")]
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-chopOutput");
        string output = at < 0 ? Path.GetFullPath("Comparison/ChopChop") : args[at+1];
        Directory.CreateDirectory(output);
        Debug.Log("[ChopChop] Starting character comparisons (including transparent materials).");
        var previous = GraphicsSettings.defaultRenderPipeline;
        var quality = QualitySettings.renderPipeline;
        bool async = EditorSettings.asyncShaderCompilation;
        bool linearLights=GraphicsSettings.lightsUseLinearIntensity;
        bool temperature=GraphicsSettings.lightsUseColorTemperature;
        int msaa=QualitySettings.antiAliasing;
        try
        {
            EditorSettings.asyncShaderCompilation = false;
            var pipeline = Pipeline();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            for (int i=0;i<Names.Length;i++) RenderCharacter(Names[i], Prefabs[i], output);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            GraphicsSettings.defaultRenderPipeline = previous;
            QualitySettings.renderPipeline = quality;
            EditorSettings.asyncShaderCompilation = async;
            GraphicsSettings.lightsUseLinearIntensity=linearLights;
            GraphicsSettings.lightsUseColorTemperature=temperature;
            QualitySettings.antiAliasing=msaa;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
        Debug.Log($"[ChopChop] Completed {Names.Length} character comparisons: {output}; original pipeline restored.");
    }

    public static void RunReferences()
    {
        var args = Environment.GetCommandLineArgs();
        string output = Path.GetFullPath(OrbitRenderMatch.BatchArgument(args, "-chopOutput"));
        string selected = OrbitRenderMatch.BatchArgument(args, "-chopNames", false);
        string[] names = selected == null ? Names.Where(name => name != "SlimeCritter").ToArray() : selected.Split(',');
        if (names.Length == 0 || names.Distinct().Count() != names.Length || names.Any(name => !Names.Contains(name)))
            throw new ArgumentException("Pass unique Chop Chop character names to -chopNames.");
        float intensity = OrbitRenderMatch.BatchFloat(args, "-chopLightIntensity", 1.6f, 0f);
        float ambient = OrbitRenderMatch.BatchFloat(args, "-chopAmbientIntensity", .2f, 0f, 1f);
        bool refsOnly = Array.IndexOf(args, "-matchRefsOnly") >= 0;
        bool async = EditorSettings.asyncShaderCompilation;
        var previous = GraphicsSettings.defaultRenderPipeline;
        var quality = QualitySettings.renderPipeline;
        bool linearLights = GraphicsSettings.lightsUseLinearIntensity;
        bool temperature = GraphicsSettings.lightsUseColorTemperature;
        try
        {
            EditorSettings.asyncShaderCompilation = false;
            var pipeline = Pipeline();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            foreach (string name in names)
            {
                string source = $"{Root}/PreviewScenes/{name}.unity";
                var scene = EditorSceneManager.OpenScene(source);
                var roots = scene.GetRootGameObjects();
                var model = OrbitRenderMatch.FindSceneObject(roots, name);
                var front = OrbitRenderMatch.FindSceneObject(roots, "Front Camera").GetComponent<Camera>();
                var light = OrbitRenderMatch.FindSceneObject(roots, "White Directional Light").GetComponent<Light>();
                foreach (var camera in roots.SelectMany(root => root.GetComponentsInChildren<Camera>())) camera.enabled = false;
                front.aspect = 1f;
                front.ResetProjectionMatrix();
                front.orthographic = true;
                light.intensity = intensity;
                RenderSettings.ambientLight = new Color(ambient, ambient, ambient, 1f);
                var renderers = model.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToArray();
                Bounds bounds = BoundsOf(renderers);
                var back = new GameObject("Back Camera").AddComponent<Camera>();
                back.CopyFrom(front);
                back.aspect = front.aspect;
                back.orthographic = true;
                back.orthographicSize = front.orthographicSize;
                back.ResetProjectionMatrix();
                back.transform.position = bounds.center - (front.transform.position - bounds.center);
                back.transform.LookAt(bounds.center);
                back.enabled = false;
                var warmup = RenderTexture.GetTemporary(1024, 1024, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
                try
                {
                    front.targetTexture = warmup;
                    for (int index = 0; index < 3; index++) front.Render();
                }
                finally
                {
                    front.targetTexture = null;
                    RenderTexture.ReleaseTemporary(warmup);
                }
                string destination = Path.Combine(output, name);
                var renderArguments = new[] { "-matchOutput", destination, "-matchAxes", "(-x,+y,+z)" };
                if (refsOnly) renderArguments = renderArguments.Concat(new[] { "-matchRefsOnly" }).ToArray();
                string[] renderArgs = args.Concat(renderArguments).ToArray();
                OrbitRenderMatch.RenderPreparedModel(model, light, renderArgs, new[] { front, back });
                var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray();
                File.WriteAllText(Path.Combine(destination, "settings.json"), JsonUtility.ToJson(new Record {
                    name = name, unity = Application.unityVersion, prefab = $"{Root}/Characters/{name}.prefab",
                    lightIntensity = intensity, lightColor = light.color, lightRotation = light.transform.eulerAngles,
                    ambient = RenderSettings.ambientLight, cameraPosition = front.transform.position,
                    cameraRotation = front.transform.eulerAngles, orthographicSize = front.orthographicSize,
                    materials = materials.Select(material => AssetDatabase.GetAssetPath(material)).ToArray(),
                    transparentMaterials = materials.Where(material => material.GetTag("RenderType", false, "") == "Transparent" ||
                        (material.HasProperty("_Surface") && material.GetFloat("_Surface") > .5f)).Select(material => material.name).ToArray()
                }, true));
                Debug.Log($"[ChopChop] Saved front/back refs for {name}: {destination}");
            }
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GraphicsSettings.defaultRenderPipeline = previous;
            QualitySettings.renderPipeline = quality;
            EditorSettings.asyncShaderCompilation = async;
            GraphicsSettings.lightsUseLinearIntensity = linearLights;
            GraphicsSettings.lightsUseColorTemperature = temperature;
        }
    }

    [MenuItem("Tools/Chop Chop/Enable URP for preview scene")]
    public static void EnablePipeline() { GraphicsSettings.defaultRenderPipeline=Pipeline(); QualitySettings.renderPipeline=null; }
    [MenuItem("Tools/Chop Chop/Restore Built-in pipeline")]
    public static void RestorePipeline() { GraphicsSettings.defaultRenderPipeline=null; QualitySettings.renderPipeline=null; }

    static UniversalRenderPipelineAsset Pipeline()
    {
        string folder=Root+"/PreviewSettings";
        Directory.CreateDirectory(folder);
        string path=folder+"/CharacterPipeline.asset";
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if(pipeline!=null)return pipeline;
        var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
        renderer.name="ChopChopCharacterRenderer";
        AssetDatabase.CreateAsset(renderer,folder+"/CharacterRenderer.asset");
        pipeline=UniversalRenderPipelineAsset.Create(renderer);
        pipeline.name="ChopChopCharacterPipeline";
        pipeline.msaaSampleCount=8;
        pipeline.supportsCameraDepthTexture=true;
        pipeline.supportsCameraOpaqueTexture=true;
        pipeline.shadowDistance=20;
        AssetDatabase.CreateAsset(pipeline,path);
        AssetDatabase.SaveAssets();
        return pipeline;
    }

    static void RenderCharacter(string name,string prefabName,string output)
    {
        Debug.Log("[ChopChop] Preparing "+name);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Characters/{name}.prefab");
        bool fresh=prefab==null;
        if(fresh)
        {
            string modelPath=$"{Root}/Art/Characters/{name}/{name}.fbx";
            prefab=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        }
        if(prefab==null)throw new Exception("Missing original character model: "+name);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.name=name;
        if(PrefabUtility.IsPartOfPrefabInstance(model))PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        foreach(var t in model.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer=0;
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }
        foreach(var behavior in model.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(behavior);
        foreach(var animator in model.GetComponentsInChildren<Animator>(true)){animator.enabled=false;animator.runtimeAnimatorController=null;}
        if(name=="PigChef")foreach(var t in model.GetComponentsInChildren<Transform>(true))if(t!=model.transform && t.name.Contains("PhoenixChick"))t.gameObject.SetActive(false);
        foreach(var light in model.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(light.gameObject);
        foreach(var camera in model.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(camera.gameObject);
        foreach(var lod in model.GetComponentsInChildren<LODGroup>())lod.ForceLOD(0);
        var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && (r is SkinnedMeshRenderer || r is MeshRenderer)).ToArray();
        if(renderers.Length==0)throw new Exception("No visible character geometry in "+name);
        if(fresh)
        {
            var sourceMaterial=AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Art/Characters/{name}/{name}.mat");
            if(sourceMaterial==null)throw new Exception("Missing original opaque material for "+name);
            foreach(var r in renderers)r.sharedMaterials=Enumerable.Repeat(sourceMaterial,r.sharedMaterials.Length).ToArray();
        }
        var originals=renderers.Select(r=>r.sharedMaterials.ToArray()).ToArray();
        foreach(var m in originals.SelectMany(x=>x).Where(m=>m!=null))
            if(m.GetTag("RenderType",false,"")=="Transparent" || (m.HasProperty("_Surface") && m.GetFloat("_Surface")>.5f))
                Debug.Log($"[ChopChop] Transparent material: {name} / {m.name}");
        foreach(var mat in originals.SelectMany(m=>m).Distinct())
        {
            if(mat==null || mat.shader==null)throw new Exception("Missing material/shader in "+name);
            Debug.Log($"[ChopChop] {name} material={mat.name}, shader={mat.shader.name}");
        }
        var baseline=originals.Select(ms=>ms.Select(m=>BaseColor(m)).ToArray()).ToArray();
        Bounds bounds=BoundsOf(renderers);
        RenderSettings.skybox=null;
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.15f,.15f,.15f,1);
        RenderSettings.reflectionIntensity=0;
        RenderSettings.fog=false;
        var sun=new GameObject("White Directional Light").AddComponent<Light>();
        sun.type=LightType.Directional;sun.color=Color.white;sun.intensity=1;
        sun.shadows=LightShadows.Soft;
        sun.transform.rotation=Quaternion.LookRotation(-(model.transform.rotation*new Vector3(-.65f,1,.8f)).normalized);
        RenderSettings.sun=sun;
        var cam=new GameObject("Front Camera").AddComponent<Camera>();
        cam.orthographic=true;cam.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x)/.86f;
        cam.transform.position=bounds.center+model.transform.forward*Mathf.Max(5,bounds.size.magnitude*2);
        cam.transform.LookAt(bounds.center);cam.nearClipPlane=.01f;cam.farClipPlane=200;
        cam.aspect=1;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color32(232,232,232,255);
        cam.allowHDR=false;cam.allowMSAA=true;
        var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.renderShadows=true;
        data.antialiasing=AntialiasingMode.None;
        var target=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=8};
        var resolved=new RenderTexture(1024,1024,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var pixels=new Texture2D(1024,1024,TextureFormat.RGB24,false,false);
        target.Create();resolved.Create();
        try
        {
            Save("shader",originals);
            Save("basecolor",baseline);
            for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterials=originals[i];
            foreach(var m in originals.SelectMany(x=>x).Distinct())
            {
                if(ShaderUtil.ShaderHasError(m.shader))
                {
                    foreach(var e in ShaderUtil.GetShaderMessages(m.shader))Debug.LogError($"[ChopChop] {m.shader.name}: {e.message} ({e.file}:{e.line})");
                    throw new Exception("Shader compilation failed for "+name);
                }
            }
            Directory.CreateDirectory(Root+"/Characters");
            PrefabUtility.SaveAsPrefabAsset(model,$"{Root}/Characters/{name}.prefab");
            Directory.CreateDirectory(Root+"/PreviewScenes");
            EditorSceneManager.SaveScene(scene,$"{Root}/PreviewScenes/{name}.unity");
            File.WriteAllText(Path.Combine(output,name+"-settings.json"),JsonUtility.ToJson(new Record{
                name=name,unity=Application.unityVersion,prefab=AssetDatabase.GetAssetPath(prefab),
                lightIntensity=1,lightColor=Color.white,lightRotation=sun.transform.eulerAngles,
                ambient=RenderSettings.ambientLight,cameraPosition=cam.transform.position,
                cameraRotation=cam.transform.eulerAngles,orthographicSize=cam.orthographicSize,
                materials=originals.SelectMany(x=>x).Distinct().Select(m=>AssetDatabase.GetAssetPath(m)).ToArray(),
                transparentMaterials=originals.SelectMany(x=>x).Distinct().Where(m=>m.GetTag("RenderType",false,"")=="Transparent" || (m.HasProperty("_Surface") && m.GetFloat("_Surface")>.5f)).Select(m=>m.name).ToArray()
            },true));
        }
        finally
        {
            RenderTexture.active=null;cam.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(resolved);UnityEngine.Object.DestroyImmediate(pixels);
            foreach(var m in baseline.SelectMany(x=>x))UnityEngine.Object.DestroyImmediate(m);
        }
        void Save(string suffix,Material[][] materials)
        {
            for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterials=materials[i];
            cam.targetTexture=target;
            for(int i=0;i<3;i++)cam.Render();
            cam.targetTexture=null;
            Graphics.Blit(target,resolved);RenderTexture.active=resolved;
            pixels.ReadPixels(new Rect(0,0,1024,1024),0,0);pixels.Apply();
            var colors=pixels.GetPixels32();
            if(colors.Count(c=>c.r>240&&c.g<12&&c.b>240)>1000)throw new Exception("Pink shader error in "+name+"/"+suffix);
            if(colors.Distinct().Take(50).Count()<50)throw new Exception("Empty render in "+name+"/"+suffix);
            File.WriteAllBytes(Path.Combine(output,name+"-"+suffix+".png"),pixels.EncodeToPNG());
            Debug.Log($"[ChopChop] Saved {name}-{suffix}.png, bounds={bounds}");
        }
    }
    static Material BaseColor(Material original)
    {
        var path=AssetDatabase.GetAssetPath(original.shader);
        Shader shader=original.shader;
        if(path.EndsWith(".shadergraph",StringComparison.OrdinalIgnoreCase))
        {
            string baseline=$"{Root}/BaseColorShaders/{Path.GetFileNameWithoutExtension(path)}_BaseColor.shadergraph";
            shader=AssetDatabase.LoadAssetAtPath<Shader>(baseline);
            if(shader==null)throw new Exception("Missing Base Color graph for "+path);
        }
        var material=new Material(original){name=original.name+" BaseColor"};
        material.shader=shader;
        return material;
    }
    static Bounds BoundsOf(Renderer[] renderers)
    {
        Bounds? result=null;
        foreach(var r in renderers)
        {
            if(r is SkinnedMeshRenderer s){s.updateWhenOffscreen=true;var mesh=new Mesh();s.BakeMesh(mesh,false);UnityEngine.Object.DestroyImmediate(mesh);}
            var b=result??r.bounds;b.Encapsulate(r.bounds);result=b;
        }
        return result??throw new Exception("Missing character bounds.");
    }
    [Serializable] class Record
    {
        public string name,unity,prefab;
        public float lightIntensity,orthographicSize;
        public Color lightColor,ambient;
        public Vector3 lightRotation,cameraPosition,cameraRotation;
        public string[] materials,transparentMaterials;
    }
}
