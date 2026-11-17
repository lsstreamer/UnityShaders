// OrbitRenderMatch.cs -- Unity Editor tool (Built-in render pipeline).
// Generate evaluation camera tracks from scene refs, or replay a shaderNPR cameras.json.
// Lighting: keep the scene's own lights (default), or reproduce the Python light (same direction and intensity).
//
// Install: copy into any folder named "Editor", e.g. Assets/Editor/OrbitRenderMatch.cs
// Open:    Tools > shaderNPR > Orbit Render Match

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class OrbitRenderMatch : EditorWindow
{
    enum BackgroundMode { Transparent, JsonColor, CustomColor, CameraSettings }
    enum LightingMode { SceneLights, PythonLight }
    enum FrameSource { CamerasJson, CustomOrbit, SceneRefOrbit }

    const string HelperCameraName = "OrbitRenderMatch Camera";
    const string HelperLightName = "OrbitRenderMatch Light";
    const string ReferenceMode = "optimization_views";
    static readonly string[] LightingLabels = { "Scene light (selected light)", "Python light (same direction + intensity)" };
    static readonly string[] BackgroundLabels = { "Transparent", "Color from cameras.json", "Custom color", "Camera's own clear settings" };

    [SerializeField] GameObject model;
    [NonSerialized] Camera renderCamera;
    [SerializeField] Camera[] refCameras = new Camera[0];
    [SerializeField] bool renderRefsOnly;
    [SerializeField] FrameSource frameSource = FrameSource.SceneRefOrbit;
    [SerializeField] int modelAxes;
    [SerializeField] int customFrameCount = 72;
    [SerializeField] float customStartOffsetDeg = 0f;
    [SerializeField] int customDirection = 1;
    static readonly string[] FrameSourceLabels = { "Replay cameras.json", "Custom orbit from JSON", "Scene ref orbit" };
    static readonly string[] ModelAxesLabels = Alignment.Axes.Select(axis => axis.label).ToArray();
    [SerializeField] LightingMode lightingMode = LightingMode.SceneLights;
    [SerializeField] Light pythonLight;
    [SerializeField] Light sceneLight;
    [NonSerialized] Light[] sceneAdditionalLights = Array.Empty<Light>();
    [SerializeField] string camerasJsonPath = "";
    [SerializeField] string referenceJsonPath = "";
    [SerializeField] string outputDir = "";
    [SerializeField] int resolutionOverride = -1;
    [SerializeField] BackgroundMode backgroundMode = BackgroundMode.Transparent;
    [SerializeField] Color customBackground = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] int msaa = 8;
    [SerializeField] int geometrySamples = 8;
    int previewFrame;
    Vector2 scroll;

    OrbitData data;
    List<FrameData> frames;
    Alignment alignment;
    string status = "";
    CameraState cameraState; // restored by the button, on re-align and when the window closes
    LightingState lightingState; // scene lighting snapshot while the Python light is active
    readonly List<object> referenceRecords = new List<object>();
    int framesWritten, geometryWritten, refGeometryWritten;

    // Unity -batchmode -executeMethod OrbitRenderMatch.RenderBatch -matchScene ...
    public static void RenderBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        var scene = EditorSceneManager.OpenScene(BatchArgument(args, "-matchScene"));
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject root = FindSceneObject(roots, BatchArgument(args, "-matchModel"));
        string refPaths = BatchArgument(args, "-matchRefCameras", false);
        string lightPath = BatchArgument(args, "-matchLight", false);
        Light sceneLight = null;
        if (lightPath != null)
        {
            sceneLight = FindSceneObject(roots, lightPath).GetComponent<Light>();
            if (sceneLight == null) throw new ArgumentException($"{lightPath} has no Light component.");
        }
        Camera[] sceneRefs = null;
        if (refPaths != null)
        {
            if (sceneLight == null) throw new ArgumentException("Pass -matchLight when generating orbits from scene cameras.");
            sceneRefs = refPaths.Split(',').Select(path =>
            {
                string cameraPath = path.Trim();
                Camera camera = FindSceneObject(roots, cameraPath).GetComponent<Camera>();
                return camera != null ? camera : throw new ArgumentException($"{cameraPath} has no Camera component.");
            }).ToArray();
        }
        RenderPreparedModel(root, sceneLight, args, sceneRefs);
    }

    internal static void RenderPreparedModel(GameObject root, Light sceneLight, string[] args, Camera[] sceneRefs = null,
        Light[] additionalLights = null)
    {
        var tool = CreateInstance<OrbitRenderMatch>();
        try
        {
            tool.model = root;
            string axes = BatchArgument(args, "-matchAxes", false);
            if (axes != null)
            {
                tool.modelAxes = Array.FindIndex(ModelAxesLabels, label =>
                    string.Equals(label.Replace(" ", ""), axes.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (tool.modelAxes < 0) throw new ArgumentException($"Invalid -matchAxes {axes}; use e.g. (-x,+y,+z).");
            }
            tool.outputDir = Path.GetFullPath(BatchArgument(args, "-matchOutput"));
            tool.resolutionOverride = BatchInt(args, "-matchResolution", tool.resolutionOverride, 16);
            tool.msaa = BatchInt(args, "-matchMsaa", tool.msaa, 1);
            if (tool.msaa != 1 && tool.msaa != 2 && tool.msaa != 4 && tool.msaa != 8)
                throw new ArgumentException("-matchMsaa must be 1, 2, 4, or 8.");
            tool.geometrySamples = BatchInt(args, "-matchGeometrySamples", tool.geometrySamples, 0);
            string background = BatchArgument(args, "-matchBackground", false);
            if (background != null && background != "transparent")
            {
                tool.backgroundMode = BackgroundMode.CustomColor;
                tool.customBackground = ParseBatchRgb(background, "-matchBackground");
            }
            if (sceneRefs != null)
            {
                tool.frameSource = FrameSource.SceneRefOrbit;
                tool.refCameras = sceneRefs;
                tool.renderRefsOnly = Array.IndexOf(args, "-matchRefsOnly") >= 0;
                tool.customFrameCount = BatchInt(args, "-matchFrameCount", tool.customFrameCount, 1);
                switch (BatchArgument(args, "-matchOrbitDirection", false))
                {
                    case null:
                    case "increasing": break;
                    case "decreasing": tool.customDirection = -1; break;
                    default: throw new ArgumentException("-matchOrbitDirection must be increasing or decreasing.");
                }
            }
            else
            {
                tool.frameSource = FrameSource.CamerasJson;
                if (axes == null) tool.modelAxes = 0;
                tool.camerasJsonPath = Path.GetFullPath(BatchArgument(args, "-matchCameras"));
                OrbitData inputs = OrbitData.Load(tool.camerasJsonPath, false);
                if (inputs.optimizationViews)
                    tool.referenceJsonPath = Path.GetFullPath(BatchArgument(args, "-matchRefs"));
                tool.renderRefsOnly = inputs.frames.Count == 0 && inputs.optimizationViews;
            }

            tool.sceneLight = sceneLight;
            tool.sceneAdditionalLights = additionalLights ?? Array.Empty<Light>();
            tool.lightingMode = sceneLight != null ? LightingMode.SceneLights : LightingMode.PythonLight;

            if (!tool.RenderOutputs()) throw new InvalidOperationException(tool.status);
        }
        finally { DestroyImmediate(tool); }
    }

    internal static string BatchArgument(string[] args, string name, bool required = true)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0 && !required) return null;
        string value = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        if (string.IsNullOrWhiteSpace(value) ||
            (value[0] == '-' && (value.Length == 1 || (!char.IsDigit(value[1]) && value[1] != '.'))))
            throw new ArgumentException($"Pass {name} followed by a value.");
        return value;
    }

    static int BatchInt(string[] args, string name, int fallback, int min)
    {
        string value = BatchArgument(args, name, false);
        if (value == null) return fallback;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed < min)
            throw new ArgumentException($"{name} must be an integer >= {min}.");
        return parsed;
    }

    internal static float BatchFloat(string[] args, string name, float fallback, float min = float.MinValue, float max = float.MaxValue)
    {
        string value = BatchArgument(args, name, false);
        if (value == null) return fallback;
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ||
            float.IsNaN(parsed) || parsed < min || parsed > max)
            throw new ArgumentException($"Invalid value for {name}: {value}.");
        return parsed;
    }

    internal static Color ParseBatchRgb(string value, string name)
    {
        if (value.Length != 7 || value[0] != '#' || !ColorUtility.TryParseHtmlString(value, out Color color))
            throw new ArgumentException($"{name} must be a color in #RRGGBB format.");
        return color;
    }

    internal static GameObject FindSceneObject(GameObject[] roots, string path)
    {
        string[] names = path.Trim('/').Split(new[] { '/' }, 2);
        var matching = roots.Where(root => root.name == names[0]).ToArray();
        if (matching.Length != 1) throw new ArgumentException($"Expected one scene root named {names[0]}; found {matching.Length}.");
        var item = names.Length == 1 ? matching[0].transform : matching[0].transform.Find(names[1]);
        if (item == null || !item.gameObject.activeInHierarchy) throw new ArgumentException($"No active scene object at {path}.");
        return item.gameObject;
    }

    [MenuItem("Tools/shaderNPR/Orbit Render Match")]
    static void Open()
    {
        GetWindow<OrbitRenderMatch>("Orbit Render Match");
    }

    static string ModelAxesPreference => "OrbitRenderMatch.ModelAxes:" + Application.dataPath;

    void OnEnable()
    {
        modelAxes = Mathf.Clamp(EditorPrefs.GetInt(ModelAxesPreference, 0), 0, ModelAxesLabels.Length - 1);
    }

    void OnDisable()
    {
        RestoreCamera();
        RestoreLighting();
    }

    static string SafeDirectory(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        try { return Path.GetDirectoryName(path) ?? ""; }
        catch (ArgumentException) { return ""; }
    }

    // ================================================================== UI
    void OnGUI()
    {
        if (refCameras == null) refCameras = new Camera[0];
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("1. Inputs", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        camerasJsonPath = EditorGUILayout.TextField(new GUIContent("Input cameras.json", "For JSON replay or Python lighting."), camerasJsonPath);
        bool pickJson = GUILayout.Button("...", GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();
        model = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Model (scene object)", "Root of the model in the scene. It is never moved."), model, typeof(GameObject), true);
        int newAxes = EditorGUILayout.Popup(new GUIContent("Model axes", "Renderer XYZ in model-root local coordinates. glTFast uses (-x, +y, +z) with the imported scene under an empty root."), modelAxes, ModelAxesLabels);
        EditorGUILayout.BeginHorizontal();
        referenceJsonPath = EditorGUILayout.TextField(new GUIContent("Saved ref cameras", "Original GLB-side cameras.json. Matches Python's optimization_views."), referenceJsonPath);
        bool pickRefs = GUILayout.Button("...", GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();
        if (string.IsNullOrWhiteSpace(referenceJsonPath))
        {
            int refCount = Mathf.Max(0, EditorGUILayout.IntField("Ref cameras", refCameras.Length));
            if (refCount != refCameras.Length) Array.Resize(ref refCameras, refCount);
            for (int index = 0; index < refCameras.Length; index++)
                refCameras[index] = (Camera)EditorGUILayout.ObjectField($"Ref camera {index}", refCameras[index], typeof(Camera), true);
        }
        renderRefsOnly = EditorGUILayout.Toggle("Render refs only", renderRefsOnly);
        if (string.IsNullOrWhiteSpace(referenceJsonPath) && refCameras.Length > 0)
            EditorGUILayout.HelpBox("Refs include geometry/optimization_views masks and ref/cameras.json in renderer coordinates.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Orbit", EditorStyles.boldLabel);
        FrameSource newSource = renderRefsOnly ? frameSource : (FrameSource)EditorGUILayout.Popup("Source", (int)frameSource, FrameSourceLabels);
        if (newAxes != modelAxes || newSource != frameSource)
        {
            RestoreCamera();
            RestoreLighting();
            alignment = null;
            if (newAxes != modelAxes) EditorPrefs.SetInt(ModelAxesPreference, newAxes);
            modelAxes = newAxes;
            frameSource = newSource;
        }
        if (!renderRefsOnly && frameSource != FrameSource.CamerasJson)
        {
            int newCount = Mathf.Max(1, EditorGUILayout.IntField("Frame count per orbit", customFrameCount));
            float newOffset = EditorGUILayout.FloatField("Start offset (deg)", customStartOffsetDeg);
            int newDirection = EditorGUILayout.IntPopup("Direction", customDirection, new[] { "Increasing azimuth", "Decreasing azimuth" }, new[] { 1, -1 });
            if (newCount != customFrameCount || !Mathf.Approximately(newOffset, customStartOffsetDeg) || newDirection != customDirection)
            {
                customFrameCount = newCount;
                customStartOffsetDeg = newOffset;
                customDirection = newDirection;
                if (alignment != null) RebuildFrames();
            }
        }
        bool align = !renderRefsOnly && GUILayout.Button("Prepare orbit preview");
        int frame = previewFrame;
        if (!renderRefsOnly && alignment != null)
        {
            EditorGUILayout.HelpBox(alignment.Report(), MessageType.Info);
            if (data.lightFollowsCamera && lightingMode == LightingMode.SceneLights)
                EditorGUILayout.HelpBox("bounding_sphere rotates the camera and light together; camera_only keeps the light fixed.", MessageType.Info);
            frame = EditorGUILayout.IntSlider("Preview frame", previewFrame, 0, frames.Count - 1);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Lighting", EditorStyles.boldLabel);
        var newMode = (LightingMode)EditorGUILayout.Popup("Mode", (int)lightingMode, LightingLabels);
        Light newLight = pythonLight;
        Light newSceneLight = sceneLight;
        if (newMode == LightingMode.SceneLights)
        {
            newSceneLight = (Light)EditorGUILayout.ObjectField(new GUIContent("Scene light", "Use this light's existing type, color and intensity; keep scene ambient. bounding_sphere rotates its pose with the camera; camera_only keeps its pose fixed."), sceneLight, typeof(Light), true);
            EditorGUILayout.HelpBox("Uses the selected scene light and scene ambient; light motion follows each orbit mode. Other lights are temporarily disabled.", MessageType.None);
        }
        else
        {
            newLight = (Light)EditorGUILayout.ObjectField(new GUIContent("Directional light (optional)", "Empty: create '" + HelperLightName + "'."), pythonLight, typeof(Light), true);
            EditorGUILayout.HelpBox("Automatically matches Python ambient and disables all other lights. Scene settings are restored afterwards.", MessageType.None);
            EditorGUILayout.HelpBox(PythonLightHelp(), MessageType.None);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("4. Render", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        outputDir = EditorGUILayout.TextField(new GUIContent("Output root", "Matched renders use <mode>/frames/ and geometry/. Manually supplied ref cameras use ref/."), outputDir);
        bool pickOutput = GUILayout.Button("...", GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();
        backgroundMode = (BackgroundMode)EditorGUILayout.Popup("Background", (int)backgroundMode, BackgroundLabels);
        if (backgroundMode == BackgroundMode.CustomColor) customBackground = EditorGUILayout.ColorField("Background color", customBackground);
        msaa = EditorGUILayout.IntPopup("MSAA", msaa, new[] { "Off", "2x", "4x", "8x" }, new[] { 1, 2, 4, 8 });
        resolutionOverride = EditorGUILayout.IntField(new GUIContent("Resolution override", "-1 uses the input JSON resolution or defaults to 1024."), resolutionOverride);
        if (!renderRefsOnly && frameSource == FrameSource.CamerasJson && alignment != null && data.hasGeometrySamples)
            EditorGUILayout.HelpBox($"Geometry evaluation: {GeometryIndices().Count} sampled views, selected automatically from cameras.json.", MessageType.None);
        else if (!renderRefsOnly)
            geometrySamples = Mathf.Max(0, EditorGUILayout.IntField("Geometry samples", geometrySamples));
        bool render = GUILayout.Button(renderRefsOnly ? "Render refs + geometry" : "Render refs + orbit + geometry");

        EditorGUILayout.Space();
        bool restore = GUILayout.Button(new GUIContent("Restore camera + lighting", "Put back everything this tool changed (also done when the window closes)."));
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.EndScrollView();

        // Actions run after all controls are laid out; ExitGUI avoids layout errors after dialogs.
        if (pickJson)
        {
            string picked = EditorUtility.OpenFilePanel("cameras.json from render_orbit.py", SafeDirectory(camerasJsonPath), "json");
            if (!string.IsNullOrEmpty(picked)) camerasJsonPath = picked;
            GUIUtility.ExitGUI();
        }
        if (pickOutput)
        {
            string picked = EditorUtility.OpenFolderPanel("Output folder", outputDir, "");
            if (!string.IsNullOrEmpty(picked)) outputDir = picked;
            GUIUtility.ExitGUI();
        }
        if (pickRefs)
        {
            string picked = EditorUtility.OpenFilePanel("cameras.json beside the original GLB", SafeDirectory(referenceJsonPath), "json");
            if (!string.IsNullOrEmpty(picked)) referenceJsonPath = picked;
            GUIUtility.ExitGUI();
        }
        if (align)
        {
            Align();
            GUIUtility.ExitGUI();
        }
        if (!renderRefsOnly && alignment != null && frame != previewFrame)
        {
            previewFrame = frame;
            ApplyFrame(previewFrame);
        }
        if (newMode != lightingMode || newLight != pythonLight || newSceneLight != sceneLight)
        {
            RestoreLighting();
            lightingMode = newMode;
            pythonLight = newLight;
            sceneLight = newSceneLight;
            if (!renderRefsOnly && alignment != null)
            {
                if (frameSource == FrameSource.SceneRefOrbit) Align();
                else ApplyFrame(previewFrame);
            }
        }
        if (render)
        {
            RenderOutputs();
            GUIUtility.ExitGUI();
        }
        if (restore)
        {
            RestoreCamera();
            RestoreLighting();
            status = "Camera and lighting restored.";
        }
    }

    // ================================================================== align
    void Align()
    {
        try
        {
            if (model == null) throw new Exception("Assign the model's scene object.");
            RestoreCamera();
            RestoreLighting();
            bool fromScene = frameSource == FrameSource.SceneRefOrbit;
            data = fromScene && string.IsNullOrWhiteSpace(camerasJsonPath)
                ? new OrbitData() : OrbitData.Load(camerasJsonPath, !fromScene);
            EnsureCamera();
            foreach (Camera camera in refCameras)
                if (camera == renderCamera) throw new Exception("Ref cameras must be separate from the orbit camera.");
            alignment = Alignment.FromModel(model.transform, modelAxes);
            RendererUnitsPerWorldUnit(); // validates the model scale
            if (fromScene) SeedSceneOrbit();
            RebuildFrames();
            status = $"Prepared {frames.Count} orbit frames.";
            Debug.Log("[OrbitRenderMatch] " + alignment.Report());
        }
        catch (Exception error)
        {
            alignment = null;
            status = "Error: " + error.Message;
            Debug.LogException(error);
        }
    }

    void EnsureCamera()
    {
        if (Array.IndexOf(refCameras, renderCamera) >= 0 && renderCamera != null)
            throw new Exception("Ref cameras must be separate from the automatically created orbit camera.");
        if (renderCamera != null) return;
        var existing = GameObject.Find(HelperCameraName);
        renderCamera = existing != null ? existing.GetComponent<Camera>() : null;
        if (renderCamera != null) return;
        var go = new GameObject(HelperCameraName);
        Undo.RegisterCreatedObjectUndo(go, "Create orbit camera");
        renderCamera = go.AddComponent<Camera>();
    }

    // ================================================================== renderer space <-> Unity world
    // Python renderer space r = s * A * v + t, with v in the model root's local space (see Alignment).
    // Unity world w = model.localToWorld * v, so the camera follows the model wherever it stands.
    Vector3 ToWorldPoint(Vector3 r)
    {
        return model.transform.TransformPoint(alignment.Axis.transpose.MultiplyVector((r - alignment.Translation) / alignment.Scale));
    }

    Vector3 ToWorldDirection(Vector3 r)
    {
        return model.transform.TransformDirection(alignment.Axis.transpose.MultiplyVector(r)).normalized;
    }

    FrameData RendererCamera(Camera camera)
    {
        float units = RendererUnitsPerWorldUnit();
        Matrix4x4 localToRenderer = Matrix4x4.Translate(alignment.Translation)
            * Matrix4x4.Scale(Vector3.one * alignment.Scale) * alignment.Axis;
        Matrix4x4 rendererToWorld = model.transform.localToWorldMatrix * localToRenderer.inverse;
        Matrix4x4 view = Matrix4x4.Scale(Vector3.one * units) * camera.worldToCameraMatrix * rendererToWorld;
        return new FrameData
        {
            View = view,
            Eye = view.inverse.GetColumn(3),
            Projection = ScaledProjection(camera.projectionMatrix, 1f / units),
            near = camera.nearClipPlane * units,
            far = camera.farClipPlane * units,
        };
    }

    // Renderer units per Unity world unit (the model's uniform world scale is divided out).
    float RendererUnitsPerWorldUnit()
    {
        Vector3 scale = model.transform.lossyScale;
        float tolerance = 1e-4f * Mathf.Abs(scale.x);
        if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f || Mathf.Abs(scale.y - scale.x) > tolerance || Mathf.Abs(scale.z - scale.x) > tolerance)
            throw new Exception($"The model and its parents need a uniform positive scale (lossyScale {scale}).");
        return alignment.Scale / scale.x;
    }

    (Vector3 position, Quaternion rotation) CameraPose(FrameData frame)
    {
        // Renderer view rows: right, up, back (right-handed, camera looks down -Z).
        Vector3 up = ToWorldDirection(frame.View.GetRow(1));
        Vector3 forward = -ToWorldDirection(frame.View.GetRow(2));
        return (ToWorldPoint(frame.Eye), Quaternion.LookRotation(forward, up));
    }

    // Renderer camera space is k times Unity camera space, so clip = P * diag(k, k, k, 1) * unityView * w.
    static Matrix4x4 ScaledProjection(Matrix4x4 p, float k)
    {
        Matrix4x4 m = p * Matrix4x4.Scale(new Vector3(k, k, k));
        if (Mathf.Abs(m[3, 2]) > 1e-12f)
        {
            // Perspective: renormalize so that clip w = -z_view, as Unity expects.
            float factor = -1f / m[3, 2];
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    m[row, column] *= factor;
        }
        return m;
    }

    // ================================================================== per frame
    bool ApplyFrame(int index)
    {
        if (data == null || alignment == null || model == null || renderCamera == null) return false;
        try
        {
            FrameData frame = frames[index];
            float k = RendererUnitsPerWorldUnit();
            if (cameraState == null) cameraState = CameraState.Capture(renderCamera);
            var pose = CameraPose(frame);
            renderCamera.usePhysicalProperties = false;
            renderCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            renderCamera.ResetWorldToCameraMatrix(); // rigid view from the transform (drives _WorldSpaceCameraPos too)
            renderCamera.nearClipPlane = frame.near / k;
            renderCamera.farClipPlane = frame.far / k;
            Matrix4x4 projection = ScaledProjection(frame.Projection, k);
            renderCamera.orthographic = Mathf.Abs(projection[3, 2]) < 1e-12f;
            if (renderCamera.orthographic) renderCamera.orthographicSize = 1f / Mathf.Abs(projection[1, 1]);
            else renderCamera.fieldOfView = 2f * Mathf.Atan(1f / Mathf.Abs(projection[1, 1])) * Mathf.Rad2Deg;
            renderCamera.projectionMatrix = projection;
            if (lightingMode == LightingMode.PythonLight) ApplyPythonLight(frame);
            else ApplySceneLight(frame);
            if (!Application.isBatchMode) SceneView.RepaintAll();
            return true;
        }
        catch (Exception error)
        {
            status = "Error: " + error.Message;
            Debug.LogException(error);
            return false;
        }
    }

    // ================================================================== frames
    void SeedSceneOrbit()
    {
        if (refCameras.Length == 0 || refCameras[0] == null) throw new Exception("Assign Ref camera 0 to start the orbit.");
        Vector3 lightDir;
        if (lightingMode == LightingMode.PythonLight)
        {
            FrameData input = data.frames.Find(frame => frame.renderMode == "camera_only") ?? data.frames.FirstOrDefault();
            if (data.Light == null || input == null) throw new Exception("Assign an input cameras.json with Python lighting, or select Scene light.");
            lightDir = input.LightDir;
        }
        else
        {
            if (sceneLight == null) throw new Exception("Assign the Scene light.");
            Vector3 worldDirection = sceneLight.type == LightType.Directional ? -sceneLight.transform.forward : sceneLight.transform.position - ToWorldPoint(Vector3.zero);
            lightDir = alignment.Axis.MultiplyVector(model.transform.InverseTransformDirection(worldDirection)).normalized;
        }
        data.frames.Clear();
        foreach (string mode in new[] { "bounding_sphere", "camera_only" })
        {
            FrameData seed = RendererCamera(refCameras[0]);
            seed.renderMode = mode;
            seed.lightFollowsCamera = mode == "bounding_sphere";
            seed.LightDir = lightDir;
            data.frames.Add(seed);
        }
        data.lightFollowsCamera = true;
    }

    void RebuildFrames()
    {
        frames = frameSource == FrameSource.CamerasJson ? data.frames : CustomOrbit(data, customFrameCount, customStartOffsetDeg, customDirection);
        previewFrame = Mathf.Clamp(previewFrame, 0, frames.Count - 1);
        if (alignment != null) ApplyFrame(previewFrame);
    }

    // render_orbit.py rotates the eye about the renderer +Y axis (azimuth) with a fixed look-at target (origin)
    // and up vector, and turns a camera-following light the same way. A rotation of the world by -angle about
    // +Y gives exactly that: view_i = view_0 * Ry(-angle), eye_i = Ry(angle) * eye_0 in this convention.
    static List<FrameData> CustomOrbit(OrbitData data, int count, float offsetDeg, int direction)
    {
        var result = new List<FrameData>();
        var seenModes = new HashSet<string>();
        foreach (FrameData first in data.frames)
        {
            if (!seenModes.Add(first.renderMode)) continue;
            for (int i = 0; i < count; i++)
            {
                float angle = offsetDeg + direction * 360f * i / count;
                Matrix4x4 eyeRotation = RotateY(-angle); // matches rotate_y(eye, -angle) in render_orbit.py
                result.Add(new FrameData
                {
                    file = $"{first.renderMode}/frames/frame_{i:D4}.png",
                    renderMode = first.renderMode,
                    lightFollowsCamera = first.lightFollowsCamera,
                    near = first.near,
                    far = first.far,
                    Eye = eyeRotation.MultiplyPoint3x4(first.Eye),
                    LightDir = first.lightFollowsCamera ? eyeRotation.MultiplyVector(first.LightDir) : first.LightDir,
                    View = first.View * eyeRotation.inverse,
                    Projection = first.Projection,
                });
            }
        }
        return result;
    }

    // Same as render_orbit.rotate_y: (x, z) -> (c x + s z, -s x + c z).
    static Matrix4x4 RotateY(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        var m = Matrix4x4.identity;
        m[0, 0] = c;
        m[0, 2] = s;
        m[2, 0] = -s;
        m[2, 2] = c;
        return m;
    }

    string PythonLightHelp()
    {
        string text = "One directional light gets the Python direction for every frame and the Python intensity/color. " +
            "Shadows are off on that light (the Python light has no shadow map unless your shader adds one).";
        if (data == null || data.Light == null) return text + "\nLoad cameras.json to see the values.";
        LightSetting light = data.Light;
        return text + $"\nPython radiance (linear) {light.Radiance.ToString("F3")} -> Unity color {light.Color} x intensity {light.Intensity:F3}" +
            $"\nPython ambient (linear) {light.Ambient.ToString("F3")}" +
            (QualitySettings.activeColorSpace == ColorSpace.Linear ? "" : "\nWarning: project is in Gamma color space; lighting values will not match the linear Python renderer.");
    }

    Light EnsurePythonLight()
    {
        if (pythonLight != null) return pythonLight;
        var existing = GameObject.Find(HelperLightName);
        pythonLight = existing != null ? existing.GetComponent<Light>() : null;
        if (pythonLight != null) return pythonLight;
        var go = new GameObject(HelperLightName);
        Undo.RegisterCreatedObjectUndo(go, "Create Python light");
        pythonLight = go.AddComponent<Light>();
        pythonLight.type = LightType.Directional;
        return pythonLight;
    }

    void ApplyPythonLight(FrameData frame)
    {
        if (data.Light == null) throw new Exception("cameras.json has no 'light' block; re-run render_orbit.py.");
        Light light = EnsurePythonLight();
        if (lightingState == null) lightingState = LightingState.Capture(light, true);
        lightingState.ApplyPose(LightOrbitRotation(frame), ToWorldPoint(Vector3.zero));
        light.type = LightType.Directional;
        light.enabled = true;
        light.shadows = LightShadows.None;
        light.color = data.Light.Color;
        light.intensity = data.Light.Intensity;
        light.useColorTemperature = false;
        light.cullingMask = ~0;
        // light_dir points from the surface toward the light; a Unity directional light shines along +forward.
        Vector3 toLight = ToWorldDirection(frame.LightDir);
        FrameData first = data.frames.Find(candidate => candidate.renderMode == frame.renderMode);
        Quaternion cameraRotation = CameraPose(!frame.lightFollowsCamera && first != null ? first : frame).rotation;
        Vector3 up = cameraRotation * Vector3.up;
        if (Mathf.Abs(Vector3.Dot(toLight, up)) > 0.999f) up = cameraRotation * Vector3.forward;
        light.transform.rotation = Quaternion.LookRotation(-toLight, up);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = data.Light.AmbientColor;
    }

    Quaternion LightOrbitRotation(FrameData frame)
    {
        if (frame == null || !frame.lightFollowsCamera) return Quaternion.identity;
        FrameData first = data.frames.Find(candidate => candidate.renderMode == frame.renderMode);
        return CameraPose(frame).rotation * Quaternion.Inverse(CameraPose(first).rotation);
    }

    void ApplySceneLight(FrameData frame = null)
    {
        if (sceneLight == null) throw new Exception("Assign the Scene light to use for this render.");
        if (lightingState == null) lightingState = LightingState.Capture(sceneLight, false, sceneAdditionalLights);
        Vector3 pivot = alignment != null && model != null ? ToWorldPoint(Vector3.zero) : Vector3.zero;
        lightingState.ApplyPose(LightOrbitRotation(frame), pivot);
        sceneLight.enabled = true;
    }

    void RestoreLighting()
    {
        lightingState?.Restore();
        lightingState = null;
    }

    // Snapshot of what the Python-light mode changes: the driven light, other lights' enabled flags, ambient.
    class LightingState
    {
        Light light;
        bool enabled, useTemperature;
        LightType type;
        LightShadows shadows;
        Color color;
        float intensity;
        int cullingMask;
        Quaternion rotation;
        Vector3 position;
        readonly List<Light> disabled = new List<Light>();
        (Light light, bool enabled, Vector3 position, Quaternion rotation)[] additional;
        bool ambientSaved;
        UnityEngine.Rendering.AmbientMode ambientMode;
        Color ambientLight;

        public static LightingState Capture(Light light, bool ambient, Light[] additional = null)
        {
            var state = new LightingState
            {
                light = light,
                enabled = light.enabled,
                useTemperature = light.useColorTemperature,
                type = light.type,
                shadows = light.shadows,
                color = light.color,
                intensity = light.intensity,
                cullingMask = light.cullingMask,
                rotation = light.transform.rotation,
                position = light.transform.position,
                additional = (additional ?? Array.Empty<Light>())
                    .Select(item => (item, item.enabled, item.transform.position, item.transform.rotation)).ToArray(),
            };
            foreach (Light other in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (other == light || !other.enabled || state.additional.Any(item => item.light == other)) continue;
                other.enabled = false;
                state.disabled.Add(other);
            }
            if (ambient)
            {
                state.ambientSaved = true;
                state.ambientMode = RenderSettings.ambientMode;
                state.ambientLight = RenderSettings.ambientLight;
            }
            return state;
        }

        public void ApplyPose(Quaternion orbitRotation, Vector3 pivot)
        {
            light.transform.SetPositionAndRotation(pivot + orbitRotation * (position - pivot), orbitRotation * rotation);
            foreach (var item in additional)
            {
                item.light.transform.SetPositionAndRotation(
                    pivot + orbitRotation * (item.position - pivot), orbitRotation * item.rotation);
                item.light.enabled = true;
            }
        }

        public void Restore()
        {
            foreach (Light other in disabled)
                if (other != null) other.enabled = true;
            foreach (var item in additional)
            {
                if (item.light == null) continue;
                item.light.transform.SetPositionAndRotation(item.position, item.rotation);
                item.light.enabled = item.enabled;
            }
            if (ambientSaved)
            {
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
            }
            if (light == null) return;
            light.type = type;
            light.shadows = shadows;
            light.color = color;
            light.intensity = intensity;
            light.useColorTemperature = useTemperature;
            light.cullingMask = cullingMask;
            light.transform.SetPositionAndRotation(position, rotation);
            light.enabled = enabled && light.name != HelperLightName;
        }
    }

    void RestoreCamera()
    {
        cameraState?.Restore();
        cameraState = null;
        if (!Application.isBatchMode) SceneView.RepaintAll();
    }

    class CameraState
    {
        Camera camera;
        Vector3 position;
        Quaternion rotation;
        float near, far, orthoSize, fov;
        bool physical, ortho;

        public static CameraState Capture(Camera camera)
        {
            return new CameraState
            {
                camera = camera,
                position = camera.transform.position,
                rotation = camera.transform.rotation,
                near = camera.nearClipPlane,
                far = camera.farClipPlane,
                physical = camera.usePhysicalProperties,
                ortho = camera.orthographic,
                orthoSize = camera.orthographicSize,
                fov = camera.fieldOfView,
            };
        }

        public void Restore()
        {
            if (camera == null) return;
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.nearClipPlane = near;
            camera.farClipPlane = far;
            camera.orthographic = ortho;
            camera.orthographicSize = orthoSize;
            camera.fieldOfView = fov;
            camera.usePhysicalProperties = physical;
            camera.ResetWorldToCameraMatrix();
            camera.ResetProjectionMatrix();
        }
    }

    // ================================================================== render
    bool RenderOutputs()
    {
        referenceRecords.Clear();
        framesWritten = geometryWritten = refGeometryWritten = 0;
        bool started = false, completed = false, writeCameraManifest = false;
        bool replay = !string.IsNullOrWhiteSpace(referenceJsonPath);
        List<FrameData> previewFrames = frames;
        try
        {
            if (refCameras == null) refCameras = new Camera[0];
            if (renderRefsOnly && !replay && refCameras.Length == 0)
            {
                status = "No Ref cameras configured; nothing to render.";
                return false;
            }
            if (!replay)
                for (int index = 0; index < refCameras.Length; index++)
                    if (refCameras[index] == null) throw new Exception($"Assign Ref camera {index}.");
            if (model == null) throw new Exception("Assign Model to render the frames and geometry masks.");
            if (string.IsNullOrEmpty(outputDir)) throw new Exception("Choose an Output root.");
            if (resolutionOverride > 0 && resolutionOverride < 16) throw new Exception("Resolution override must be at least 16.");
            string destination = Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar);
            foreach (string source in new[] { camerasJsonPath, referenceJsonPath })
                if (!string.IsNullOrWhiteSpace(source) && string.Equals(destination, Path.GetDirectoryName(Path.GetFullPath(source)), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Choose an output directory separate from the input cameras.json files.");
            if (lightingMode == LightingMode.SceneLights && sceneLight == null) throw new Exception("Assign the Scene light.");
            if (renderRefsOnly)
            {
                RestoreCamera();
                RestoreLighting();
                data = string.IsNullOrWhiteSpace(camerasJsonPath) ? null : OrbitData.Load(camerasJsonPath, false);
                alignment = Alignment.FromModel(model.transform, modelAxes);
                RendererUnitsPerWorldUnit();
                if (replay)
                {
                    EnsureCamera();
                }
                else if (lightingMode == LightingMode.PythonLight)
                {
                    if (data == null || data.Light == null || data.frames.Count == 0) throw new Exception("Python light requires cameras.json with lighting and camera frames.");
                    ApplyPythonLight(data.frames[0]);
                }
                else ApplySceneLight();
            }
            else
            {
                Align();
                if (alignment == null) return false;
                foreach (Camera camera in refCameras)
                    if (camera == renderCamera) throw new Exception("Ref cameras must be separate from the orbit camera.");
            }
            if (replay)
            {
                previewFrames = frames;
                if (frameSource != FrameSource.CamerasJson || data == null || !data.hasGeometrySamples)
                    throw new Exception("Reference replay requires CamerasJson mode and the Python evaluation camera manifest.");
                var referenceFrames = LoadReferenceFrames();
                frames = renderRefsOnly ? referenceFrames : frames.Concat(referenceFrames).ToList();
            }
            PrepareOutputRoot();
            started = true;
            writeCameraManifest = !renderRefsOnly || replay || !File.Exists(Path.Combine(outputDir, "cameras.json"));
            if (writeCameraManifest) WriteCameraManifest(false);
            if (replay || !renderRefsOnly) ClearFrameOutputs();
            if (!replay && refCameras.Length > 0)
            {
                if (frameSource == FrameSource.SceneRefOrbit && !renderRefsOnly)
                {
                    RestoreLighting();
                    if (lightingMode == LightingMode.SceneLights) ApplySceneLight();
                    else ApplyPythonLight(data.frames.Find(frame => frame.renderMode == "camera_only"));
                }
                RenderReferences();
            }
            if ((replay || !renderRefsOnly) && (!RenderAll() || !RenderGeometrySamples())) return false;
            completed = true;
            status = $"Completed: {ReferenceImagesWritten()} refs, {refGeometryWritten} ref geometry masks, {geometryWritten} orbit geometry masks, {OrbitImagesWritten()} orbit frames under {outputDir}";
            Debug.Log("[OrbitRenderMatch] " + status);
        }
        catch (OperationCanceledException)
        {
            status = "Rendering cancelled; the output manifest is marked incomplete.";
        }
        catch (Exception error)
        {
            status = "Error: " + error.Message;
            Debug.LogException(error);
        }
        finally
        {
            try
            {
                if (started)
                {
                    if (writeCameraManifest) WriteCameraManifest(completed);
                    WriteManifest(OutputResolution(), OrbitImagesWritten(), completed);
                }
            }
            catch (Exception error)
            {
                completed = false;
                status = "Error writing render metadata: " + error.Message;
                Debug.LogException(error);
            }
            EditorUtility.ClearProgressBar();
            RestoreCamera();
            RestoreLighting();
            if (replay) frames = previewFrames;
        }
        return completed;
    }

    int OutputResolution()
    {
        return resolutionOverride > 0 ? resolutionOverride : data != null ? data.resolution : 1024;
    }

    int OrbitFrameCount() => renderRefsOnly || frames == null ? 0 : frames.Count(frame => frame.renderMode != ReferenceMode);

    int OrbitImagesWritten() => Math.Min(framesWritten, OrbitFrameCount());

    int ReferenceImagesWritten() => referenceRecords.Count + framesWritten - OrbitImagesWritten();

    List<FrameData> LoadReferenceFrames()
    {
        string pythonRoot = Path.GetDirectoryName(Path.GetFullPath(camerasJsonPath));
        string framesDir = Path.Combine(pythonRoot, ReferenceMode, "frames");
        var images = Directory.GetFiles(framesDir, "view*.png").Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (images.Length == 0) throw new Exception($"No Python reference renders in {framesDir}.");

        string geometryManifest = Path.Combine(pythonRoot, "geometry", ReferenceMode, "geometry.json");
        var geometry = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(geometryManifest));
        var masks = ((List<object>)geometry["frames"]).Cast<string>().ToArray();
        if (!Equals(geometry["geometry_source"], "mesh_triangles") || masks.Length == 0 || masks.Distinct().Count() != masks.Length || masks.Except(images).Any())
            throw new Exception($"Reference geometry must list unique meshes from {framesDir}: {geometryManifest}.");

        var refs = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(referenceJsonPath));
        if (!refs.TryGetValue("render_complete", out object complete) || !Equals(complete, true))
            throw new Exception($"Reference camera export is incomplete: {referenceJsonPath}.");
        var records = (List<object>)refs["ref_cameras"];
        FrameData baseLight = data.frames.Find(frame => frame.renderMode == "camera_only") ?? data.frames.FirstOrDefault();
        if (lightingMode == LightingMode.PythonLight && baseLight == null)
            throw new Exception("Pass -matchLight (or assign Scene light) when the Python manifest contains only reference views.");
        if (lightingMode == LightingMode.PythonLight)
            Debug.LogWarning("[OrbitRenderMatch] Using the main Python light for saved refs. Assign Scene light to replay the reference scene's lighting.");

        var result = new List<FrameData>();
        foreach (string name in masks.Concat(images.Except(masks)))
        {
            if (!int.TryParse(name.Substring(4, name.Length - 8), NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                || index >= records.Count || name != $"view{index}.png")
                throw new Exception($"No original reference camera for {name} in {referenceJsonPath}.");
            var record = (Dictionary<string, object>)records[index];
            if (!record.TryGetValue("renderer_camera", out object saved) || !(saved is Dictionary<string, object> camera) || !Equals(camera["space"], "renderer"))
                throw new Exception($"Missing renderer_camera for {name} in {referenceJsonPath}.");
            Matrix4x4 view = OrbitData.Mat(camera["view_matrix"]);
            result.Add(new FrameData
            {
                file = $"{ReferenceMode}/frames/{name}",
                renderMode = ReferenceMode,
                geometryFile = masks.Contains(name) ? $"geometry/{ReferenceMode}/{name}" : null,
                near = OrbitData.Num(camera["near"]),
                far = OrbitData.Num(camera["far"]),
                Eye = view.inverse.GetColumn(3),
                LightDir = baseLight != null ? baseLight.LightDir : Vector3.zero,
                View = view,
                Projection = OrbitData.Mat(camera["projection_matrix"]),
            });
        }
        return result;
    }

    void RenderReferences()
    {
        if (refCameras.Length == 0) return;
        string folder = Path.Combine(outputDir, "ref");
        Directory.CreateDirectory(folder);
        foreach (string pattern in new[] { "view*.png", "ref_*.png" })
            foreach (string previous in Directory.GetFiles(folder, pattern)) File.Delete(previous);
        string cameraManifest = Path.Combine(folder, "cameras.json");
        if (File.Exists(cameraManifest)) File.Delete(cameraManifest);
        string geometryFolder = Path.Combine(outputDir, "geometry", "optimization_views");
        Directory.CreateDirectory(geometryFolder);
        foreach (string pattern in new[] { "view*.png", "ref_*.png" })
            foreach (string previous in Directory.GetFiles(geometryFolder, pattern)) File.Delete(previous);
        string geometryManifest = Path.Combine(geometryFolder, "geometry.json");
        if (File.Exists(geometryManifest)) File.Delete(geometryManifest);
        int size = OutputResolution();
        bool transparent = backgroundMode == BackgroundMode.Transparent;
        Color clear = transparent ? Color.clear
            : backgroundMode == BackgroundMode.JsonColor && data != null && data.Background.HasValue ? data.Background.Value
            : customBackground;
        var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = msaa };
        var resolved = msaa > 1 ? new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) : null;
        var readback = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        var helper = new GameObject("OrbitRenderMatch Ref Capture") { hideFlags = HideFlags.HideAndDontSave };
        var capture = helper.AddComponent<Camera>();
        capture.enabled = false;
        GeometryCapture geometry = null;
        bool oldAsync = EditorSettings.asyncShaderCompilation;
        EditorSettings.asyncShaderCompilation = false;
        target.Create();
        resolved?.Create();
        try
        {
            WriteReferenceCameraManifest(folder, size, false);
            geometry = new GeometryCapture(this, size);
            ReportProgress("Refs", 0, refCameras.Length);
            for (int index = 0; index < refCameras.Length; index++)
            {
                if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Reference renders", $"Ref {index + 1}/{refCameras.Length}", (float)index / refCameras.Length))
                    throw new OperationCanceledException();
                Camera source = refCameras[index];
                capture.CopyFrom(source);
                capture.enabled = false;
                capture.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                capture.worldToCameraMatrix = source.worldToCameraMatrix;
                capture.projectionMatrix = source.projectionMatrix;
                capture.rect = new Rect(0, 0, 1, 1);
                capture.targetTexture = target;
                capture.allowMSAA = msaa > 1;
                if (transparent) capture.allowHDR = false;
                if (backgroundMode != BackgroundMode.CameraSettings)
                {
                    capture.clearFlags = CameraClearFlags.SolidColor;
                    capture.backgroundColor = clear;
                }
                RenderInto(capture, target, resolved, readback, size);
                if (!transparent) ForceOpaque(readback);
                string relative = $"ref/view{index}.png";
                File.WriteAllBytes(Path.Combine(outputDir, relative), readback.EncodeToPNG());
                geometry.Render(capture, Path.Combine(geometryFolder, $"view{index}.png"));
                refGeometryWritten++;
                var record = new Dictionary<string, object>
                {
                    { "file", relative },
                    { "geometry_file", $"geometry/optimization_views/view{index}.png" },
                    { "camera_name", source.name },
                    { "space", "Unity world" },
                    { "resolution", size },
                    { "view_matrix", MatrixValues(capture.worldToCameraMatrix) },
                    { "projection_matrix", MatrixValues(capture.projectionMatrix) },
                };
                FrameData camera = RendererCamera(capture);
                record["renderer_camera"] = new Dictionary<string, object>
                {
                    { "space", "renderer" },
                    { "proj_type", camera.ProjectionType },
                    { "near", camera.near },
                    { "far", camera.far },
                    { "view_matrix", MatrixValues(camera.View) },
                    { "projection_matrix", MatrixValues(camera.Projection) },
                };
                referenceRecords.Add(record);
                WriteReferenceCameraManifest(folder, size, false);
                ReportProgress("Refs", index + 1, refCameras.Length);
            }
            var geometryFiles = new List<object>();
            foreach (Dictionary<string, object> record in referenceRecords)
                geometryFiles.Add(Path.GetFileName((string)record["geometry_file"]));
            File.WriteAllText(geometryManifest, MiniJson.Serialize(new Dictionary<string, object>
            {
                { "geometry_source", "mesh_triangles" },
                { "resolution", size },
                { "frames", geometryFiles },
            }));
            WriteReferenceCameraManifest(folder, size, true);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            geometry?.Dispose();
            EditorSettings.asyncShaderCompilation = oldAsync;
            capture.targetTexture = null;
            target.Release();
            DestroyImmediate(target);
            if (resolved != null)
            {
                resolved.Release();
                DestroyImmediate(resolved);
            }
            DestroyImmediate(readback);
            DestroyImmediate(helper);
        }
    }

    void WriteReferenceCameraManifest(string folder, int size, bool completed)
    {
        var records = new List<object>();
        foreach (Dictionary<string, object> reference in referenceRecords)
        {
            var record = new Dictionary<string, object>(reference);
            record["file"] = Path.GetFileName((string)reference["file"]);
            record["geometry_file"] = "../geometry/optimization_views/" + Path.GetFileName((string)reference["geometry_file"]);
            records.Add(record);
        }
        var manifest = new Dictionary<string, object>
        {
            { "schema_version", 1 },
            { "resolution", size },
            { "render_complete", completed },
            { "renderer_camera_available", true },
            { "camera_mapping", alignment.Report() },
            { "ref_cameras", records },
        };
        File.WriteAllText(Path.Combine(folder, "cameras.json"), MiniJson.Serialize(manifest));
    }

    bool RenderAll()
    {
        if (model == null || renderCamera == null)
        {
            status = "Error: model or camera missing; align again.";
            return false;
        }
        PrepareOutputRoot();
        int size = OutputResolution();
        bool transparent = backgroundMode == BackgroundMode.Transparent;
        Color clear = transparent ? new Color(0f, 0f, 0f, 0f)
            : backgroundMode == BackgroundMode.JsonColor && data.Background.HasValue ? data.Background.Value
            : customBackground;
        if (backgroundMode == BackgroundMode.JsonColor && !data.Background.HasValue)
            Debug.LogWarning("[OrbitRenderMatch] cameras.json has no background_color (transparent Python frames); using the custom color.");

        var oldTarget = renderCamera.targetTexture;
        var oldFlags = renderCamera.clearFlags;
        var oldColor = renderCamera.backgroundColor;
        var oldHdr = renderCamera.allowHDR;
        var oldMsaa = renderCamera.allowMSAA;
        var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = msaa };
        var resolved = msaa > 1 ? new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) : null;
        var readback = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        target.Create();
        resolved?.Create();
        // Editor compiles shader variants asynchronously by default: the first frames could use a placeholder.
        bool oldAsync = EditorSettings.asyncShaderCompilation;
        EditorSettings.asyncShaderCompilation = false;
        int written = 0, retried = 0;
        try
        {
            renderCamera.targetTexture = target;
            renderCamera.allowMSAA = msaa > 1;
            if (backgroundMode != BackgroundMode.CameraSettings)
            {
                renderCamera.clearFlags = CameraClearFlags.SolidColor;
                renderCamera.backgroundColor = clear;
            }
            if (transparent) renderCamera.allowHDR = false; // keep alpha in the 8-bit target
            ReportProgress("Frames", 0, frames.Count);
            // Warm-up: let Unity create GPU resources and finish shader variants before the first saved frame.
            if (ApplyFrame(0))
            {
                for (int warm = 0; warm < 2; warm++) RenderInto(renderCamera, target, resolved, readback, size);
            }
            for (int i = 0; i < frames.Count; i++)
            {
                if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Orbit Render Match", $"Frame {i + 1}/{frames.Count}", (float)i / frames.Count)) break;
                if (!ApplyFrame(i)) break;
                RenderInto(renderCamera, target, resolved, readback, size);
                // The model never fills the corners: a frame whose corners differ from the clear color is a bad render.
                if (backgroundMode != BackgroundMode.CameraSettings && !CornersMatch(readback, clear))
                {
                    retried++;
                    RenderInto(renderCamera, target, resolved, readback, size);
                    if (!CornersMatch(readback, clear))
                        Debug.LogWarning($"[OrbitRenderMatch] {frames[i].file}: corners still differ from the background after a retry; check this frame.");
                }
                if (!transparent) ForceOpaque(readback);
                string framePath = Path.Combine(outputDir, frames[i].file);
                Directory.CreateDirectory(Path.GetDirectoryName(framePath));
                File.WriteAllBytes(framePath, readback.EncodeToPNG());
                written++;
                framesWritten = written;
                ReportProgress("Frames", written, frames.Count);
            }
            status = $"Saved {written}/{frames.Count} frames to {outputDir}" + (retried > 0 ? $" ({retried} re-rendered)" : "");
            Debug.Log("[OrbitRenderMatch] " + status);
            return written == frames.Count;
        }
        catch (Exception error)
        {
            status = "Error: " + error.Message;
            Debug.LogException(error);
            return false;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            EditorSettings.asyncShaderCompilation = oldAsync;
            renderCamera.targetTexture = oldTarget;
            renderCamera.clearFlags = oldFlags;
            renderCamera.backgroundColor = oldColor;
            renderCamera.allowHDR = oldHdr;
            renderCamera.allowMSAA = oldMsaa;
            target.Release();
            DestroyImmediate(target);
            if (resolved != null)
            {
                resolved.Release();
                DestroyImmediate(resolved);
            }
            DestroyImmediate(readback);
        }
    }

    void PrepareOutputRoot()
    {
        if (string.IsNullOrEmpty(outputDir)) outputDir = Path.Combine(SafeDirectory(camerasJsonPath), "unity_frames");
        Directory.CreateDirectory(outputDir);
        if (frameSource == FrameSource.CamerasJson && data != null && data.hasGeometrySamples)
            foreach (string relative in new[] { "geometry/cameras.json", "optimization_views/cameras.json", "optimization_views/geometry/cameras.json" })
            {
                string previous = Path.Combine(outputDir, relative);
                if (File.Exists(previous)) File.Delete(previous);
            }
    }

    void ClearFrameOutputs()
    {
        foreach (string mode in frames.Select(frame => frame.renderMode).Distinct())
        {
            string folder = Path.Combine(outputDir, mode, "frames");
            if (!Directory.Exists(folder)) continue;
            foreach (string pattern in new[] { "frame_*.png", "view*.png" })
                foreach (string previous in Directory.GetFiles(folder, pattern)) File.Delete(previous);
        }
    }

    List<int> GeometryIndices()
    {
        var indices = new List<int>();
        var seenPaths = new HashSet<string>();
        if (frameSource == FrameSource.CamerasJson && data.hasGeometrySamples)
        {
            for (int index = 0; index < frames.Count; index++)
                if (!string.IsNullOrEmpty(frames[index].geometryFile) && seenPaths.Add(GeometryPath(frames[index]))) indices.Add(index);
        }
        else
        {
            // All orbit modes share the same camera track; sample the first mode once.
            var orbit = new List<int>();
            for (int index = 0; index < frames.Count; index++)
                if (frames[index].renderMode == frames[0].renderMode) orbit.Add(index);
            int count = geometrySamples == 0 ? orbit.Count : Math.Min(geometrySamples, orbit.Count);
            for (int sample = 0; sample < count; sample++) indices.Add(orbit[sample * orbit.Count / count]);
        }
        return indices;
    }

    static string GeometryPath(FrameData frame)
    {
        return string.IsNullOrEmpty(frame.geometryFile) ? Path.Combine("geometry", "orbit", Path.GetFileName(frame.file)) : frame.geometryFile;
    }

    class GeometryDraw
    {
        public Mesh mesh;
        public Matrix4x4 transform;
    }

    List<GeometryDraw> GeometryDraws()
    {
        var draws = new List<GeometryDraw>();
        try
        {
            foreach (var renderer in model.GetComponentsInChildren<UnityEngine.Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Mesh source = null, baked = null;
                try
                {
                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        if (skinned.sharedMesh == null) continue;
                        baked = new Mesh { indexFormat = skinned.sharedMesh.indexFormat };
                        skinned.BakeMesh(baked, false);
                        source = baked;
                    }
                    else if (renderer is MeshRenderer)
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter != null) source = filter.sharedMesh;
                    }
                    if (source == null || source.vertexCount == 0) continue;
                    var triangles = new List<int>();
                    for (int submesh = 0; submesh < source.subMeshCount; submesh++)
                        if (source.GetTopology(submesh) == MeshTopology.Triangles) triangles.AddRange(source.GetTriangles(submesh));
                    if (triangles.Count == 0) continue;
                    var geometry = new Mesh { indexFormat = source.indexFormat };
                    draws.Add(new GeometryDraw { mesh = geometry, transform = renderer.transform.localToWorldMatrix });
                    geometry.vertices = source.vertices;
                    var colors = new Color32[source.vertexCount];
                    for (int vertex = 0; vertex < colors.Length; vertex++) colors[vertex] = new Color32(255, 255, 255, 255);
                    geometry.colors32 = colors;
                    geometry.SetTriangles(triangles, 0);
                }
                finally
                {
                    if (baked != null) DestroyImmediate(baked);
                }
            }
            return draws;
        }
        catch
        {
            foreach (var draw in draws) DestroyImmediate(draw.mesh);
            throw;
        }
    }

    class GeometryCapture : IDisposable
    {
        readonly int size;
        readonly Material material;
        readonly RenderTexture target;
        readonly Texture2D readback;
        readonly byte[] alpha;
        readonly CommandBuffer commands;
        readonly List<GeometryDraw> draws;

        public GeometryCapture(OrbitRenderMatch owner, int size)
        {
            this.size = size;
            try
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null || !shader.isSupported)
                    throw new Exception("Unity's built-in solid-color shader is unavailable.");
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                material.SetColor("_Color", Color.white);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_Cull", (int)CullMode.Off);
                material.SetInt("_ZWrite", 0);
                material.SetInt("_ZTest", (int)CompareFunction.Always);
                target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                readback = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                alpha = new byte[size * size];
                commands = new CommandBuffer { name = "Original mesh geometry" };
                draws = owner.GeometryDraws();
                if (draws.Count == 0) throw new Exception("The model has no enabled triangle mesh renderers.");
                target.Create();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Render(Camera camera, string path)
        {
            commands.Clear();
            commands.SetRenderTarget(target);
            commands.SetViewport(new Rect(0, 0, size, size));
            commands.ClearRenderTarget(true, true, Color.clear);
            commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, GL.GetGPUProjectionMatrix(camera.projectionMatrix, false));
            foreach (var draw in draws) commands.DrawMesh(draw.mesh, draw.transform, material, 0, 0);
            Graphics.ExecuteCommandBuffer(commands);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                readback.Apply();
            }
            finally { RenderTexture.active = previous; }
            var colors = readback.GetRawTextureData<Color32>();
            for (int i = 0; i < alpha.Length; i++) alpha[i] = colors[i].a;
            File.WriteAllBytes(path, ImageConversion.EncodeArrayToPNG(alpha,
                UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm, (uint)size, (uint)size));
        }

        public void Dispose()
        {
            commands?.Release();
            if (target != null)
            {
                target.Release();
                DestroyImmediate(target);
            }
            if (readback != null) DestroyImmediate(readback);
            if (material != null) DestroyImmediate(material);
            if (draws != null)
                foreach (var draw in draws) DestroyImmediate(draw.mesh);
        }
    }

    bool RenderGeometrySamples()
    {
        if (model == null || renderCamera == null || frames == null || frames.Count == 0)
        {
            status = "Error: align the model and camera first.";
            return false;
        }
        PrepareOutputRoot();
        var indices = GeometryIndices();
        var written = new Dictionary<string, List<string>>();
        foreach (int index in indices)
        {
            string geometryDir = Path.GetDirectoryName(Path.Combine(outputDir, GeometryPath(frames[index])));
            if (written.ContainsKey(geometryDir)) continue;
            written.Add(geometryDir, new List<string>());
            Directory.CreateDirectory(geometryDir);
            string manifest = Path.Combine(geometryDir, "geometry.json");
            if (File.Exists(manifest)) File.Delete(manifest);
            foreach (string pattern in new[] { "frame_*.png", "view*.png" })
                foreach (string file in Directory.GetFiles(geometryDir, pattern)) File.Delete(file);
        }
        int size = OutputResolution();
        int count = indices.Count;
        GeometryCapture geometry = null;
        bool oldAsync = EditorSettings.asyncShaderCompilation;
        EditorSettings.asyncShaderCompilation = false;
        try
        {
            ReportProgress("Masks", 0, count);
            geometry = new GeometryCapture(this, size);
            for (int sample = 0; sample < count; sample++)
            {
                int index = indices[sample];
                if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Alignment geometry", $"Sample {sample + 1}/{count}", (float)sample / count)) break;
                if (!ApplyFrame(index)) throw new Exception("Could not apply the sampled camera.");
                string framePath = Path.Combine(outputDir, GeometryPath(frames[index]));
                geometry.Render(renderCamera, framePath);
                written[Path.GetDirectoryName(framePath)].Add("\"" + Escape(Path.GetFileName(framePath)) + "\"");
                if (frames[index].renderMode == ReferenceMode) refGeometryWritten++;
                else geometryWritten++;
                ReportProgress("Masks", sample + 1, count);
            }
            int total = 0;
            foreach (var group in written)
            {
                total += group.Value.Count;
                if (group.Value.Count > 0)
                    File.WriteAllText(Path.Combine(group.Key, "geometry.json"), "{\n  \"geometry_source\": \"mesh_triangles\",\n  \"resolution\": " + size + ",\n  \"frames\": [" + string.Join(", ", group.Value.ToArray()) + "]\n}\n");
            }
            status = $"Saved {total}/{count} original-mesh geometry masks under {outputDir}";
            Debug.Log("[OrbitRenderMatch] " + status);
            return total == count;
        }
        catch (Exception error)
        {
            status = "Error: " + error.Message;
            Debug.LogException(error);
            return false;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            geometry?.Dispose();
            EditorSettings.asyncShaderCompilation = oldAsync;
        }
    }

    static void ReportProgress(string phase, int written, int total)
    {
        if (!Application.isBatchMode || total == 0) return;
        int stride = Mathf.CeilToInt(total / 24f);
        if (written != total && written % stride != 0) return;
        const int width = 24;
        int filled = width * written / total;
        string bar = new string('#', filled) + new string('-', width - filled);
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
            "[RenderProgress] {0} [{1}] {2}/{3} ({4}%)", phase, bar, written, total, 100 * written / total);
    }

    void RenderInto(Camera camera, RenderTexture target, RenderTexture resolved, Texture2D readback, int size)
    {
        camera.Render();
        RenderTexture source = target;
        if (resolved != null)
        {
            target.ResolveAntiAliasedSurface(resolved);
            source = resolved;
        }
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = source;
            readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            readback.Apply();
        }
        finally { RenderTexture.active = previous; }
    }

    // Compares the four corner pixels with the clear color (alpha included), allowing 2/255 of rounding.
    static bool CornersMatch(Texture2D texture, Color clear)
    {
        int w = texture.width - 1, h = texture.height - 1;
        Color32 expected = clear;
        foreach (var (x, y) in new[] { (0, 0), (w, 0), (0, h), (w, h) })
        {
            Color32 c = texture.GetPixel(x, y);
            if (Math.Abs(c.r - expected.r) > 2 || Math.Abs(c.g - expected.g) > 2 || Math.Abs(c.b - expected.b) > 2 || Math.Abs(c.a - expected.a) > 2) return false;
        }
        return true;
    }

    static void ForceOpaque(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++) pixels[i].a = 255;
        texture.SetPixels32(pixels);
        texture.Apply();
    }

    static List<object> MatrixValues(Matrix4x4 matrix)
    {
        var rows = new List<object>();
        for (int row = 0; row < 4; row++)
        {
            var columns = new List<object>();
            for (int column = 0; column < 4; column++) columns.Add(matrix[row, column]);
            rows.Add(columns);
        }
        return rows;
    }

    void WriteCameraManifest(bool completed)
    {
        string destination = Path.Combine(outputDir, "cameras.json");
        var manifest = data?.manifest != null
            ? new Dictionary<string, object>(data.manifest)
            : new Dictionary<string, object>();
        var orbitRecords = new List<object>();
        int orbitCount = OrbitFrameCount();
        var geometryIndices = orbitCount > 0 ? GeometryIndices().Where(index => index < orbitCount).ToArray() : Array.Empty<int>();
        var geometryPaths = new HashSet<string>();
        foreach (int index in geometryIndices) geometryPaths.Add(GeometryPath(frames[index]));
        var modes = new Dictionary<string, object>();
        var modeCounts = new Dictionary<string, int>();
        var inputModes = manifest.TryGetValue("modes", out object savedModes) ? savedModes as Dictionary<string, object> : null;
        bool anyLightFollows = false;
        if (orbitCount > 0)
            for (int index = 0; index < orbitCount; index++)
            {
                FrameData frame = frames[index];
                if (!modeCounts.ContainsKey(frame.renderMode)) modeCounts[frame.renderMode] = 0;
                int modeIndex = modeCounts[frame.renderMode]++;
                anyLightFollows |= frame.lightFollowsCamera;
                var record = frame.source != null
                    ? new Dictionary<string, object>(frame.source)
                    : new Dictionary<string, object>
                    {
                        { "near", frame.near },
                        { "far", frame.far },
                        { "eye", new List<object> { frame.Eye.x, frame.Eye.y, frame.Eye.z } },
                        { "light_dir", new List<object> { frame.LightDir.x, frame.LightDir.y, frame.LightDir.z } },
                        { "view_matrix", MatrixValues(frame.View) },
                        { "projection_matrix", MatrixValues(frame.Projection) },
                    };
                record["index"] = index;
                record["mode_index"] = modeIndex;
                record["proj_type"] = frame.ProjectionType;
                record["render_mode"] = frame.renderMode;
                record["light_follows_camera"] = frame.lightFollowsCamera;
                record["file"] = frame.file;
                record["image_rendered"] = index < framesWritten;
                record.Remove("geometry_file");
                if (geometryPaths.Contains(GeometryPath(frame))) record["geometry_file"] = GeometryPath(frame).Replace('\\', '/');
                orbitRecords.Add(record);
                var inputMode = inputModes != null && inputModes.TryGetValue(frame.renderMode, out object settings) ? settings as Dictionary<string, object> : null;
                modes[frame.renderMode] = new Dictionary<string, object>
                {
                    { "frame_count", modeCounts[frame.renderMode] },
                    { "geometry_frame_count", geometryPaths.Count },
                    { "frame_fill", frameSource != FrameSource.SceneRefOrbit && inputMode != null && inputMode.TryGetValue("frame_fill", out object fill) ? fill : null },
                    { "geometry_directory", "geometry/orbit" },
                    { "light_follows_camera", frame.lightFollowsCamera },
                };
            }
        var enabledModes = new List<object>();
        foreach (string mode in modeCounts.Keys) enabledModes.Add(mode);
        if (referenceRecords.Count > 0 || (frames != null && frames.Any(frame => frame.renderMode == ReferenceMode))) enabledModes.Add(ReferenceMode);
        manifest.Remove("ref_cameras");
        manifest.Remove("model_samples");
        if (manifest.TryGetValue("conventions", out object conventions) && conventions is Dictionary<string, object> conventionMap)
        {
            var savedConventions = new Dictionary<string, object>(conventionMap);
            savedConventions.Remove("model_samples");
            manifest["conventions"] = savedConventions;
        }
        manifest["schema_version"] = 3;
        manifest["enabled_modes"] = enabledModes;
        manifest["frames"] = orbitRecords;
        manifest["frame_count"] = orbitRecords.Count;
        manifest["geometry_source"] = "mesh_triangles";
        manifest["geometry_frame_count"] = geometryIndices.Length;
        manifest["light_follows_camera"] = anyLightFollows;
        manifest["modes"] = modes;
        manifest["resolution"] = OutputResolution();
        if (backgroundMode == BackgroundMode.Transparent || backgroundMode == BackgroundMode.CustomColor)
        {
            bool transparent = backgroundMode == BackgroundMode.Transparent;
            Color32 color = customBackground;
            manifest["transparent_background"] = transparent;
            manifest["background_color"] = transparent ? null : new List<object> { color.r, color.g, color.b };
        }
        manifest["reference_only"] = renderRefsOnly;
        manifest["render_complete"] = completed;
        manifest["frames_written"] = OrbitImagesWritten();
        manifest["geometry_written"] = geometryWritten;
        manifest["ref_geometry_written"] = refGeometryWritten;
        File.WriteAllText(destination, MiniJson.Serialize(manifest));
    }

    void WriteManifest(int size, int written, bool completed)
    {
        var lines = new List<string>
        {
            "{",
            $"  \"source_cameras_json\": \"{Escape(camerasJsonPath)}\",",
            $"  \"unity_version\": \"{Application.unityVersion}\",",
            $"  \"color_space\": \"{PlayerSettings.colorSpace}\",",
            $"  \"resolution\": {size},",
            $"  \"frames_written\": {written},",
            $"  \"background\": \"{backgroundMode}\",",
            $"  \"msaa\": {msaa},",
            $"  \"python_light_follows_camera\": {(data != null && data.lightFollowsCamera ? "true" : "false")},",
            $"  \"lighting\": \"{lightingMode}\",",
            $"  \"frame_source\": \"{frameSource}\",",
            $"  \"frame_count\": {OrbitFrameCount()},",
            $"  \"refs_written\": {ReferenceImagesWritten()},",
            $"  \"ref_geometry_written\": {refGeometryWritten},",
            $"  \"geometry_written\": {geometryWritten},",
            $"  \"reference_only\": {(renderRefsOnly ? "true" : "false")},",
            $"  \"completed\": {(completed ? "true" : "false")},",
            $"  \"scene_light\": {(lightingMode == LightingMode.SceneLights ? "\"" + Escape(sceneLight.name) + "\"" : "null")},",
            $"  \"scene_additional_lights\": [{string.Join(", ", sceneAdditionalLights.Select(item => "\"" + Escape(item.name) + "\""))}],",
            $"  \"custom_start_offset_deg\": {(frameSource != FrameSource.CamerasJson && !renderRefsOnly ? Fmt(customStartOffsetDeg) : "null")},",
            $"  \"custom_direction\": {(frameSource != FrameSource.CamerasJson && !renderRefsOnly ? customDirection.ToString(CultureInfo.InvariantCulture) : "null")},",
            $"  \"light_intensity\": {Fmt(lightingMode == LightingMode.PythonLight ? data.Light.Intensity : sceneLight.intensity)},",
            $"  \"axis_mapping\": {(alignment != null ? "\"" + alignment.AxisLabel + "\"" : "null")},",
            $"  \"renderer_units_per_world_unit\": {Fmt(RendererUnitsPerWorldUnit())}",
            "}",
        };
        File.WriteAllLines(Path.Combine(outputDir, "unity_render.json"), lines);
    }

    static string Fmt(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // Renderer coordinates are a fixed axis permutation followed by bounds normalization.
    class Alignment
    {
        public static readonly (Matrix4x4 matrix, string label)[] Axes = AxisMappings().ToArray();
        public Matrix4x4 Axis;
        public float Scale;
        public Vector3 Translation;
        public string AxisLabel;

        public string Report() => $"Model axes (renderer XYZ): {AxisLabel}\nScale {Scale:G6}; offset {Translation.ToString("G6")}";

        public static Alignment FromModel(Transform root, int axesIndex)
        {
            using (var points = LocalVertices(root).GetEnumerator())
            {
                if (!points.MoveNext()) throw new Exception("The model has no MeshFilter/SkinnedMeshRenderer vertices.");
                Vector3 lo = points.Current, hi = points.Current;
                while (points.MoveNext())
                {
                    lo = Vector3.Min(lo, points.Current);
                    hi = Vector3.Max(hi, points.Current);
                }
                Vector3 size = hi - lo;
                float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                if (longest <= 0f) throw new Exception("The model has zero size.");
                var (axis, label) = Axes[axesIndex];
                float scale = 2f / longest;
                return new Alignment
                {
                    Axis = axis,
                    AxisLabel = label,
                    Scale = scale,
                    Translation = -scale * axis.MultiplyVector((lo + hi) * 0.5f),
                };
            }
        }

        static IEnumerable<(Matrix4x4, string)> AxisMappings()
        {
            int[][] permutations = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
            string[] names = { "x", "y", "z" };
            foreach (int[] permutation in permutations)
                for (int signs = 0; signs < 8; signs++)
                {
                    Matrix4x4 matrix = Matrix4x4.zero;
                    matrix[3, 3] = 1f;
                    var label = new string[3];
                    for (int row = 0; row < 3; row++)
                    {
                        float sign = ((signs >> row) & 1) == 1 ? -1f : 1f;
                        matrix[row, permutation[row]] = sign;
                        label[row] = (sign < 0f ? "-" : "+") + names[permutation[row]];
                    }
                    if (matrix.determinant < 0f) yield return (matrix, $"({string.Join(", ", label)})");
                }
        }

        // Measure the mesh in the root's local space so scene placement has no effect on normalization.
        static IEnumerable<Vector3> LocalVertices(Transform root)
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices) yield return matrix.MultiplyPoint3x4(vertex);
            }
            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh == null) continue;
                var baked = new Mesh();
                try
                {
                    skinned.BakeMesh(baked);
                    Matrix4x4 matrix = toRoot * skinned.transform.localToWorldMatrix;
                    foreach (Vector3 vertex in baked.vertices) yield return matrix.MultiplyPoint3x4(vertex);
                }
                finally { DestroyImmediate(baked); }
            }
        }
    }

    // ================================================================== cameras.json
    class FrameData
    {
        public Dictionary<string, object> source;
        public string file;
        public string geometryFile;
        public string renderMode = "bounding_sphere";
        public bool lightFollowsCamera;
        public float near, far;
        public Vector3 Eye, LightDir;
        public Matrix4x4 View, Projection;
        public string ProjectionType => Mathf.Abs(Projection[3, 2]) < 1e-12f ? "ortho" : "perspective";
    }

    // Python light in linear RGB. Unity: final light = color (gamma-encoded in a Linear project) x intensity.
    class LightSetting
    {
        public readonly Vector3 Radiance, Ambient;
        public readonly Color Color, AmbientColor;
        public readonly float Intensity;

        public LightSetting(Vector3 radiance, Vector3 ambient)
        {
            Radiance = radiance;
            Ambient = ambient;
            Intensity = Mathf.Max(radiance.x, Mathf.Max(radiance.y, radiance.z));
            Vector3 tint = Intensity > 0f ? radiance / Intensity : Vector3.zero;
            // Light.color and RenderSettings.ambientLight are stored gamma-encoded and linearized by Unity in Linear projects.
            Color = new Color(tint.x, tint.y, tint.z, 1f).gamma;
            AmbientColor = new Color(ambient.x, ambient.y, ambient.z, 1f).gamma;
        }
    }

    class OrbitData
    {
        public int resolution = 1024;
        public Dictionary<string, object> manifest;
        public bool lightFollowsCamera;
        public bool hasGeometrySamples;
        public bool optimizationViews;
        public LightSetting Light;
        public Color? Background;
        public readonly List<FrameData> frames = new List<FrameData>();

        public static OrbitData Load(string path, bool requireFrames = true)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new Exception($"cameras.json not found: {path}");
            var json = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
            if (json == null) throw new Exception("cameras.json is not a JSON object.");
            var data = new OrbitData { manifest = json, resolution = Convert.ToInt32(json["resolution"], CultureInfo.InvariantCulture) };
            data.optimizationViews = json.TryGetValue("enabled_modes", out object enabled) && enabled is List<object> modes && modes.Contains(ReferenceMode);
            data.lightFollowsCamera = json.TryGetValue("light_follows_camera", out object follow) && follow is bool b && b;
            data.hasGeometrySamples = json.TryGetValue("geometry_source", out object geometrySource) && geometrySource is string source && source == "mesh_triangles";
            if (json.TryGetValue("light", out object lightBlock) && lightBlock is Dictionary<string, object> lightJson)
                data.Light = new LightSetting(Vec(lightJson["radiance"]), Vec(lightJson["ambient"]));
            if (json.TryGetValue("background_color", out object bg) && bg is List<object> rgb && rgb.Count == 3)
                data.Background = new Color(Num(rgb[0]) / 255f, Num(rgb[1]) / 255f, Num(rgb[2]) / 255f, 1f);
            var cameraFrames = json.TryGetValue("frames", out object frameValues) && frameValues is List<object> savedFrames ? savedFrames : new List<object>();
            foreach (object item in cameraFrames)
            {
                var f = (Dictionary<string, object>)item;
                string filename = ((string)f["file"]).Replace('\\', '/');
                string mode = f.TryGetValue("render_mode", out object modeValue) ? (string)modeValue : "bounding_sphere";
                if (mode != "bounding_sphere" && mode != "camera_only") continue;
                if (filename.StartsWith("optimization_views/", StringComparison.Ordinal)) continue;
                string imageName = Path.GetFileName(filename);
                if (!imageName.StartsWith("frame_", StringComparison.Ordinal) || !imageName.EndsWith(".png", StringComparison.Ordinal))
                    throw new Exception($"Invalid orbit image name: {filename}.");
                string geometryName = f.TryGetValue("geometry_file", out object geometryFile) && geometryFile is string geometryPath
                    ? Path.GetFileName(geometryPath.Replace('\\', '/')) : null;
                data.frames.Add(new FrameData
                {
                    source = f,
                    file = $"{mode}/frames/{imageName}",
                    renderMode = mode,
                    lightFollowsCamera = f.TryGetValue("light_follows_camera", out object follows) ? Convert.ToBoolean(follows) : mode == "bounding_sphere",
                    geometryFile = !string.IsNullOrEmpty(geometryName) ? $"geometry/orbit/{geometryName}" : null,
                    near = Num(f["near"]),
                    far = Num(f["far"]),
                    Eye = Vec(f["eye"]),
                    LightDir = Vec(f["light_dir"]),
                    View = Mat(f["view_matrix"]),
                    Projection = Mat(f["projection_matrix"]),
                });
            }
            if (requireFrames && data.frames.Count == 0) throw new Exception("cameras.json has no orbit frames. Enable bounding_sphere or camera_only in evaluation_render.yaml and regenerate it.");
            return data;
        }

        public static float Num(object value) => Convert.ToSingle(value, CultureInfo.InvariantCulture);

        static Vector3 Vec(object value)
        {
            var v = (List<object>)value;
            return new Vector3(Num(v[0]), Num(v[1]), Num(v[2]));
        }

        // cameras.json stores row-major nested lists [[row0], [row1], [row2], [row3]].
        public static Matrix4x4 Mat(object value)
        {
            var rows = (List<object>)value;
            if (rows.Count != 4) throw new Exception("Expected a 4x4 matrix in cameras.json.");
            var m = new Matrix4x4();
            for (int row = 0; row < 4; row++)
            {
                var columns = (List<object>)rows[row];
                for (int column = 0; column < 4; column++) m[row, column] = Num(columns[column]);
            }
            return m;
        }
    }

    // Minimal JSON reader (objects, arrays, numbers, strings, true/false/null). JsonUtility cannot read nested arrays.
    static class MiniJson
    {
        public static string Serialize(object value, int depth = 0)
        {
            if (value == null) return "null";
            if (value is string text) return Quote(text);
            if (value is bool flag) return flag ? "true" : "false";
            string padding = new string(' ', depth * 2);
            string childPadding = new string(' ', (depth + 1) * 2);
            var entries = new List<string>();
            if (value is Dictionary<string, object> map)
            {
                foreach (var entry in map)
                    entries.Add(childPadding + Quote(entry.Key) + ": " + Serialize(entry.Value, depth + 1));
                return entries.Count == 0 ? "{}" : "{\n" + string.Join(",\n", entries.ToArray()) + "\n" + padding + "}";
            }
            if (value is List<object> values)
            {
                foreach (object entry in values) entries.Add(childPadding + Serialize(entry, depth + 1));
                return entries.Count == 0 ? "[]" : "[\n" + string.Join(",\n", entries.ToArray()) + "\n" + padding + "]";
            }
            if (value is float number) return number.ToString("R", CultureInfo.InvariantCulture);
            if (value is double precise) return precise.ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        static string Quote(string text)
        {
            var result = new System.Text.StringBuilder("\"");
            foreach (char character in text)
            {
                switch (character)
                {
                    case '\\': result.Append("\\\\"); break;
                    case '"': result.Append("\\\""); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default:
                        if (character < 32) result.Append("\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else result.Append(character);
                        break;
                }
            }
            return result.Append('"').ToString();
        }

        public static object Parse(string text)
        {
            int i = 0;
            object value = Value(text, ref i);
            Skip(text, ref i);
            if (i != text.Length) throw new Exception($"Unexpected trailing JSON at {i}");
            return value;
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static bool Literal(string s, ref int i, string word)
        {
            if (s.Length - i < word.Length || string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new Exception("Unexpected end of JSON");
            char c = s[i];
            if (c == '{') return Object(s, ref i);
            if (c == '[') return Array(s, ref i);
            if (c == '"') return String(s, ref i);
            if (Literal(s, ref i, "true")) return true;
            if (Literal(s, ref i, "false")) return false;
            if (Literal(s, ref i, "null")) return null;
            if (Literal(s, ref i, "NaN")) return double.NaN;
            return Number(s, ref i);
        }

        static Dictionary<string, object> Object(string s, ref int i)
        {
            var result = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return result; }
            while (true)
            {
                Skip(s, ref i);
                string key = String(s, ref i);
                Skip(s, ref i);
                if (s[i] != ':') throw new Exception($"Expected ':' at {i}");
                i++;
                result[key] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return result; }
                throw new Exception($"Expected ',' or '}}' at {i}");
            }
        }

        static List<object> Array(string s, ref int i)
        {
            var result = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return result; }
            while (true)
            {
                result.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return result; }
                throw new Exception($"Expected ',' or ']' at {i}");
            }
        }

        static string String(string s, ref int i)
        {
            if (s[i] != '"') throw new Exception($"Expected a string at {i}");
            i++;
            var builder = new System.Text.StringBuilder();
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { builder.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': builder.Append('\n'); break;
                    case 't': builder.Append('\t'); break;
                    case 'r': builder.Append('\r'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'u': builder.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: builder.Append(e); break;
                }
            }
            i++;
            return builder.ToString();
        }

        static double Number(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (start == i) throw new Exception($"Unexpected JSON character '{s[i]}' at {i}");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
