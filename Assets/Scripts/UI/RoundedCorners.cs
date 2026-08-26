using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rounds the corners of the RawImage on this GameObject using the UI/RoundedCorners
/// shader, which evaluates a signed distance field per fragment.
///
/// Deliberately knows nothing about VideoPlayer. The demo video's texture is owned by
/// YogaManager (APIOnly render mode, see YogaManager.StartDemoVideo) and a second
/// component assigning RawImage.texture would race it. Rounding is a material concern
/// only, so this works on a video, a sprite, or anything else a RawImage shows.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public class RoundedCorners : MonoBehaviour
{
    public const string ShaderName = "UI/RoundedCorners";

    [Tooltip("Corner radius as a fraction of the rect's SHORTER side. 0 = square corners, 0.5 = fully rounded (pill or circle).")]
    [Range(0f, 0.5f)]
    public float radius = 0.15f;

    [SerializeField]
    [Tooltip("Auto-filled in the Editor. Serialized so player builds keep a hard reference -- a shader reached only via Shader.Find can be stripped from a build.")]
    private Shader roundedShader;

    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int SizeId = Shader.PropertyToID("_Size");

    private RawImage _graphic;
    private RectTransform _rect;
    private Material _materialInstance;

    // Last values actually pushed to the GPU, so Update can early-out instead of
    // dirtying the canvas every frame.
    private float _appliedRadius = float.NaN;
    private Vector2 _appliedSize = new Vector2(float.NaN, float.NaN);

    private RawImage Graphic
    {
        get
        {
            if (_graphic == null) _graphic = GetComponent<RawImage>();
            return _graphic;
        }
    }

    private RectTransform Rect
    {
        get
        {
            if (_rect == null) _rect = transform as RectTransform;
            return _rect;
        }
    }

    private void OnEnable()
    {
        if (EnsureMaterial()) PushProperties();
    }

    // Both hooks matter: OnDisable covers pooling and scene teardown where OnDestroy
    // may not run promptly, OnDestroy covers the object actually going away.
    private void OnDisable()
    {
        CleanUpAssets();
    }

    private void OnDestroy()
    {
        CleanUpAssets();
    }

    // Fires whenever the layout resizes this rect, which is what the SDF needs to stay
    // aspect-correct.
    private void OnRectTransformDimensionsChange()
    {
        PushProperties();
    }

    // Lets the inspector slider and runtime script changes both take effect. Cheap:
    // PushProperties returns immediately unless radius or rect size actually moved.
    private void Update()
    {
        PushProperties();
    }

    /// <summary>Set the corner radius from script. Clamped to the shader's valid range.</summary>
    public void SetRadius(float value)
    {
        radius = Mathf.Clamp(value, 0f, 0.5f);
        PushProperties();
    }

    private bool EnsureMaterial()
    {
        if (_materialInstance != null) return true;

        Shader shader = roundedShader != null ? roundedShader : Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("[RoundedCorners] Shader '" + ShaderName + "' not found. Is UIRoundedCorners.shader in the project and compiling?", this);
            return false;
        }

        // DontSave keeps this runtime instance out of the scene file -- without it,
        // [ExecuteAlways] would serialise a fresh material into the scene on every save.
        _materialInstance = new Material(shader)
        {
            name = "RoundedCorners (Instance)",
            hideFlags = HideFlags.DontSave
        };

        if (Graphic != null) Graphic.material = _materialInstance;
        return true;
    }

    private void PushProperties()
    {
        if (_materialInstance == null) return;

        RawImage g = Graphic;
        if (g == null || Rect == null) return;

        // Reattach if anything else overwrote the material behind our back.
        if (g.material != _materialInstance) g.material = _materialInstance;

        Vector2 size = Rect.rect.size;
        if (radius == _appliedRadius && size == _appliedSize) return;

        _materialInstance.SetFloat(RadiusId, radius);
        _materialInstance.SetVector(SizeId, new Vector4(size.x, size.y, 0f, 0f));

        _appliedRadius = radius;
        _appliedSize = size;
        g.SetMaterialDirty();
    }

    /// <summary>
    /// Releases the runtime material. There is no RenderTexture to release here by
    /// design -- YogaManager uses VideoRenderMode.APIOnly, which hands back the
    /// decoder's own texture, so nothing in this path allocates VRAM.
    /// </summary>
    private void CleanUpAssets()
    {
        if (_materialInstance == null) return;

        // Detach first so the Graphic falls back to the default UI material instead of
        // holding a destroyed reference and rendering magenta.
        if (Graphic != null && Graphic.material == _materialInstance)
            Graphic.material = null;

        DestroySafe(_materialInstance);
        _materialInstance = null;

        _appliedRadius = float.NaN;
        _appliedSize = new Vector2(float.NaN, float.NaN);
    }

    private static void DestroySafe(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        roundedShader = Shader.Find(ShaderName);
    }

    private void OnValidate()
    {
        if (roundedShader == null) roundedShader = Shader.Find(ShaderName);
        radius = Mathf.Clamp(radius, 0f, 0.5f);
        PushProperties();
    }
#endif
}
