using UnityEngine;

[ExecuteAlways]
public sealed class UnderwaterPostProcessingController : MonoBehaviour
{
    [System.Serializable]
    public sealed class TintLightSettings
    {
        public Light light;
        public string name;
        public bool enabled;
        public Color color;
        [Min(0f)] public float intensity;
        public Vector3 localPosition;
        public Vector3 eulerAngles;
        [Min(0.1f)] public float range;
        [Range(1f, 179f)] public float spotAngle;
        [Range(1f, 179f)] public float innerSpotAngle;
        [Header("Beam Post Effect")]
        public Color beamColor;
        public bool disableBeamEffect;
        [Min(0f)] public float beamOpacity;
        [Min(0.01f)] public float beamLength;
        [Min(0.01f)] public float beamWidth;
        [Range(0.01f, 1f)] public float beamCoreWidth;
        [Range(0.01f, 1f)] public float beamFeather;
        [Min(0.1f)] public float beamFalloff;
        [Tooltip("Turn this on only when the beam should stay opaque at its lower end.")]
        public bool disableBottomFade;
        [Range(0.01f, 1f)] public float beamBottomFadeStart;
        public Vector3 beamLocalOffset;
        public Vector3 beamEulerAngles;
        public int beamSortingOrder;
    }

    [SerializeField] Camera targetCamera;
    [SerializeField] Component underwaterVolume;
    [SerializeField] bool alwaysUnderwater = true;
    [SerializeField] float waterSurfaceY = 0f;
    [SerializeField, Min(0.01f)] float blendDepth = 1.5f;
    [SerializeField, Min(0f)] float blendSpeed = 6f;
    [SerializeField, Range(0f, 1f)] float maxWeight = 1f;
    [SerializeField] bool enableCameraPostProcessing = true;

    [Header("Underwater Sun Light")]
    [SerializeField] bool enableUnderwaterSunLight = true;
    [SerializeField] Color sunLightColor = new(0.62f, 0.95f, 1f, 1f);
    [SerializeField, Min(0f)] float sunLightIntensity = 2.2f;
    [SerializeField] Vector3 sunLightEulerAngles = new(58f, -22f, 0f);
    [SerializeField, Min(0f)] float sunLightPulseAmount = 0.18f;
    [SerializeField, Min(0f)] float sunLightPulseSpeed = 0.65f;

    [Header("Underwater Tint Light")]
    [SerializeField] bool enableUnderwaterTintLight = true;
    [SerializeField] bool applyTintSettingsToLights = true;
    [SerializeField] bool enableTintBeamPostEffect = true;
    [SerializeField, Min(16)] int beamTextureWidth = 96;
    [SerializeField, Min(16)] int beamTextureHeight = 256;
    [SerializeField] TintLightSettings[] tintLights =
    {
        new()
        {
            name = "Center Beam",
            enabled = true,
            color = new Color(0.34f, 0.86f, 1f, 1f),
            intensity = 3.2f,
            localPosition = new Vector3(0f, 6f, 3.5f),
            eulerAngles = new Vector3(68f, 0f, 0f),
            range = 16f,
            spotAngle = 28f,
            innerSpotAngle = 8f,
            beamColor = new Color(0.82f, 0.96f, 1f, 1f),
            disableBeamEffect = false,
            beamOpacity = 0.72f,
            beamLength = 11f,
            beamWidth = 2.8f,
            beamCoreWidth = 0.18f,
            beamFeather = 0.62f,
            beamFalloff = 1.6f,
            disableBottomFade = false,
            beamBottomFadeStart = 0.45f,
            beamLocalOffset = new Vector3(0f, -0.2f, -0.05f),
            beamEulerAngles = Vector3.zero,
            beamSortingOrder = 80
        }
    };

    [Header("Distance Contrast")]
    [SerializeField] bool enableDistanceContrast = true;
    [SerializeField, Min(0f)] float contrastNearDistance = 5f;
    [SerializeField, Min(0f)] float contrastFarDistance = 24f;
    [SerializeField] float nearContrast = 30f;
    [SerializeField] float farContrast = -100f;

    float _currentWeight;
    Light _sunLight;
    Light[] _tintLights = new Light[0];
    SpriteRenderer[] _beamRenderers = new SpriteRenderer[0];
    Sprite[] _beamSprites = new Sprite[0];
    int[] _beamSpriteKeys = new int[0];
    object _colorAdjustments;
    object _contrastParameter;

    void Reset()
    {
        underwaterVolume = FindVolumeComponent();
        targetCamera = Camera.main;
        ApplyWeight(maxWeight);
        EnablePostProcessing();
    }

    void OnEnable()
    {
        if (underwaterVolume == null)
            underwaterVolume = FindVolumeComponent();

        if (targetCamera == null)
            targetCamera = Camera.main;

        _currentWeight = ReadVolumeWeight();
        EnablePostProcessing();
        EnsureSunLight();
        EnsureTintLights();
        UpdateVolumeWeight(immediate: true);
    }

    void OnDisable()
    {
        DestroyLight(ref _sunLight);
        DestroyTintLights();
    }

    void OnValidate()
    {
        if (underwaterVolume == null)
            underwaterVolume = FindVolumeComponent();

        blendDepth = Mathf.Max(0.01f, blendDepth);
        blendSpeed = Mathf.Max(0f, blendSpeed);
        NormalizeTintLightSettings();
        UpdateVolumeWeight(immediate: true);
    }

    void Update()
    {
        if (underwaterVolume == null)
            underwaterVolume = FindVolumeComponent();

        if (targetCamera == null)
            targetCamera = Camera.main;

        EnablePostProcessing();
        UpdateVolumeWeight(immediate: !Application.isPlaying);
        UpdateSunLight();
        UpdateTintLight();
        UpdateDistanceContrast();
    }

    void UpdateVolumeWeight(bool immediate)
    {
        if (underwaterVolume == null)
            return;

        float targetWeight = CalculateTargetWeight();
        if (immediate || blendSpeed <= 0f)
            ApplyWeight(targetWeight);
        else
            ApplyWeight(Mathf.MoveTowards(_currentWeight, targetWeight, blendSpeed * Time.deltaTime));
    }

    float CalculateTargetWeight()
    {
        if (alwaysUnderwater)
            return maxWeight;

        if (targetCamera == null)
            return 0f;

        float submersion = Mathf.Clamp01((waterSurfaceY - targetCamera.transform.position.y) / blendDepth);
        return submersion * maxWeight;
    }

    void ApplyWeight(float weight)
    {
        _currentWeight = Mathf.Clamp01(weight);

        if (underwaterVolume != null)
            WriteFloatProperty(underwaterVolume, "weight", _currentWeight);
    }

    void UpdateSunLight()
    {
        if (!enableUnderwaterSunLight)
        {
            SetLightEnabled(_sunLight, false);
            return;
        }

        EnsureSunLight();
        if (_sunLight == null)
            return;

        float pulse = 1f + Mathf.Sin(Time.time * sunLightPulseSpeed) * sunLightPulseAmount;
        _sunLight.enabled = _currentWeight > 0.001f;
        _sunLight.color = sunLightColor;
        _sunLight.intensity = sunLightIntensity * pulse * _currentWeight;
        _sunLight.transform.localRotation = Quaternion.Euler(sunLightEulerAngles);
    }

    void UpdateTintLight()
    {
        if (!enableUnderwaterTintLight)
        {
            SetTintLightsEnabled(false);
            SetBeamRenderersEnabled(false);
            return;
        }

        EnsureTintLights();
        bool active = _currentWeight > 0.001f;
        for (int i = 0; i < _tintLights.Length; i++)
        {
            var light = _tintLights[i];
            if (light == null)
                continue;

            var settings = tintLights[i];
            light.enabled = active && settings.enabled;
            if (applyTintSettingsToLights)
            {
                light.color = settings.color;
                light.intensity = settings.intensity * _currentWeight;
                light.range = settings.range;
                light.spotAngle = settings.spotAngle;
                light.innerSpotAngle = Mathf.Min(settings.innerSpotAngle, settings.spotAngle);
                light.transform.localPosition = settings.localPosition;
                light.transform.localRotation = Quaternion.Euler(settings.eulerAngles);
            }

            UpdateTintBeam(i, settings, active && settings.enabled && !settings.disableBeamEffect);
        }
    }

    void EnsureSunLight()
    {
        _sunLight = EnsureDirectionalLight(_sunLight, "Underwater Sun Light", enableUnderwaterSunLight);
    }

    void EnsureTintLights()
    {
        if (!enableUnderwaterTintLight)
            return;

        NormalizeTintLightSettings();
        if (_tintLights == null || _tintLights.Length != tintLights.Length)
            System.Array.Resize(ref _tintLights, tintLights.Length);
        if (_beamRenderers == null || _beamRenderers.Length != tintLights.Length)
            System.Array.Resize(ref _beamRenderers, tintLights.Length);
        if (_beamSprites == null || _beamSprites.Length != tintLights.Length)
            System.Array.Resize(ref _beamSprites, tintLights.Length);
        if (_beamSpriteKeys == null || _beamSpriteKeys.Length != tintLights.Length)
            System.Array.Resize(ref _beamSpriteKeys, tintLights.Length);

        for (int i = 0; i < tintLights.Length; i++)
        {
            string lightName = $"Underwater Tint Light {i + 1:00} {SafeLightName(tintLights[i].name)}";
            if (tintLights[i].light != null)
            {
                _tintLights[i] = tintLights[i].light;
                ConfigureSpotLight(_tintLights[i]);
            }
            else
            {
                _tintLights[i] = EnsureSpotLight(_tintLights[i], lightName, true);
                tintLights[i].light = _tintLights[i];
            }

            EnsureTintBeam(i, $"Underwater Tint Beam {i + 1:00} {SafeLightName(tintLights[i].name)}");
        }
    }

    void EnsureTintBeam(int index, string objectName)
    {
        if (!enableTintBeamPostEffect || index < 0 || index >= _tintLights.Length)
            return;

        Transform parent = transform;
        if (_beamRenderers[index] == null)
        {
            var existing = parent.Find(objectName);
            if (existing != null)
                _beamRenderers[index] = existing.GetComponent<SpriteRenderer>();
        }

        if (_beamRenderers[index] == null)
        {
            var beamObject = new GameObject(objectName);
            beamObject.hideFlags = HideFlags.DontSave;
            beamObject.transform.SetParent(parent, false);
            _beamRenderers[index] = beamObject.AddComponent<SpriteRenderer>();
        }

        _beamRenderers[index].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _beamRenderers[index].receiveShadows = false;
    }

    void UpdateTintBeam(int index, TintLightSettings settings, bool active)
    {
        if (!enableTintBeamPostEffect || index < 0 || index >= _beamRenderers.Length)
        {
            SetBeamRendererEnabled(index, false);
            return;
        }

        EnsureTintBeam(index, $"Underwater Tint Beam {index + 1:00} {SafeLightName(settings.name)}");
        var renderer = _beamRenderers[index];
        if (renderer == null)
            return;

        renderer.enabled = active;
        if (!active)
            return;

        int spriteKey = CalculateBeamSpriteKey(settings);
        if (_beamSprites[index] == null || _beamSpriteKeys[index] != spriteKey)
        {
            DestroyBeamSprite(index);
            _beamSprites[index] = CreateBeamSprite(settings);
            _beamSpriteKeys[index] = spriteKey;
        }

        renderer.sprite = _beamSprites[index];
        renderer.color = new Color(
            settings.beamColor.r,
            settings.beamColor.g,
            settings.beamColor.b,
            settings.beamOpacity * _currentWeight);
        renderer.sortingOrder = settings.beamSortingOrder;
        Vector3 basePosition = _tintLights[index] != null
            ? _tintLights[index].transform.localPosition
            : settings.localPosition;
        renderer.transform.localPosition = basePosition + settings.beamLocalOffset;
        renderer.transform.localRotation = Quaternion.Euler(settings.beamEulerAngles);
        float spriteAspectHeight = Mathf.Max(0.01f, beamTextureHeight / (float)Mathf.Max(1, beamTextureWidth));
        renderer.transform.localScale = new Vector3(settings.beamWidth, settings.beamLength / spriteAspectHeight, 1f);
    }

    Sprite CreateBeamSprite(TintLightSettings settings)
    {
        int width = Mathf.Max(16, beamTextureWidth);
        int height = Mathf.Max(16, beamTextureHeight);
        var texture = new Texture2D(width, height, TextureFormat.ARGB32, false)
        {
            name = "Underwater Tint Beam Texture",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[width * height];
        float core = Mathf.Clamp01(settings.beamCoreWidth);
        float feather = Mathf.Clamp01(settings.beamFeather);
        float falloff = Mathf.Max(0.1f, settings.beamFalloff);
        float bottomFadeStart = Mathf.Clamp01(settings.beamBottomFadeStart);

        for (int y = 0; y < height; y++)
        {
            float vertical = y / (height - 1f);
            float distanceFromTop = 1f - vertical;
            float lengthAlpha = Mathf.Pow(Mathf.Clamp01(1f - distanceFromTop * 0.28f), falloff);
            if (!settings.disableBottomFade)
            {
                float bottomFade = Mathf.SmoothStep(0f, Mathf.Max(0.01f, bottomFadeStart), vertical);
                lengthAlpha *= bottomFade;
            }

            for (int x = 0; x < width; x++)
            {
                float centerDistance = Mathf.Abs((x / (width - 1f)) * 2f - 1f);
                float coreAlpha = 1f - Mathf.SmoothStep(core, Mathf.Clamp01(core + feather), centerDistance);
                float sideStreak = 1f - Mathf.SmoothStep(0.62f, 1f, centerDistance);
                float alpha = Mathf.Clamp01((coreAlpha * 0.86f + sideStreak * 0.14f) * lengthAlpha);
                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * width + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 1f), width);
    }

    int CalculateBeamSpriteKey(TintLightSettings settings)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Mathf.Max(16, beamTextureWidth);
            hash = hash * 31 + Mathf.Max(16, beamTextureHeight);
            hash = hash * 31 + Mathf.RoundToInt(settings.beamCoreWidth * 1000f);
            hash = hash * 31 + Mathf.RoundToInt(settings.beamFeather * 1000f);
            hash = hash * 31 + Mathf.RoundToInt(settings.beamFalloff * 1000f);
            hash = hash * 31 + (settings.disableBottomFade ? 1 : 0);
            hash = hash * 31 + Mathf.RoundToInt(settings.beamBottomFadeStart * 1000f);
            return hash;
        }
    }

    Light EnsureDirectionalLight(Light light, string objectName, bool enabled)
    {
        if (!enabled)
            return light;

        light = EnsureLight(light, objectName);
        if (light == null)
            return null;

        light.type = LightType.Directional;
        light.shadows = LightShadows.None;
        return light;
    }

    Light EnsureSpotLight(Light light, string objectName, bool enabled)
    {
        if (!enabled)
            return light;

        light = EnsureLight(light, objectName);
        if (light == null)
            return null;

        ConfigureSpotLight(light);
        return light;
    }

    static void ConfigureSpotLight(Light light)
    {
        if (light == null)
            return;

        light.type = LightType.Spot;
        light.shadows = LightShadows.None;
    }

    Light EnsureLight(Light light, string objectName)
    {
        if (light != null)
            return light;

        var existing = transform.Find(objectName);
        if (existing != null)
            light = existing.GetComponent<Light>();

        if (light == null)
        {
            var lightObject = new GameObject(objectName);
            lightObject.hideFlags = HideFlags.DontSave;
            lightObject.transform.SetParent(transform, false);
            light = lightObject.AddComponent<Light>();
        }

        return light;
    }

    void NormalizeTintLightSettings()
    {
        if (tintLights == null || tintLights.Length == 0)
        {
            tintLights = new[]
            {
                new TintLightSettings
                {
                    name = "Center Beam",
                    enabled = true,
                    color = new Color(0.34f, 0.86f, 1f, 1f),
                    intensity = 3.2f,
                    localPosition = new Vector3(0f, 6f, 3.5f),
                    eulerAngles = new Vector3(68f, 0f, 0f),
                    range = 16f,
                    spotAngle = 28f,
                    innerSpotAngle = 8f
                }
            };
        }

        for (int i = 0; i < tintLights.Length; i++)
        {
            if (tintLights[i] == null)
                tintLights[i] = CreateDefaultTintLightSettings(i);

            var settings = tintLights[i];
            if (string.IsNullOrWhiteSpace(settings.name))
                settings.name = $"Beam {i + 1:00}";
            settings.intensity = Mathf.Max(0f, settings.intensity);
            settings.range = Mathf.Max(0.1f, settings.range);
            settings.spotAngle = Mathf.Clamp(settings.spotAngle, 1f, 179f);
            settings.innerSpotAngle = Mathf.Clamp(settings.innerSpotAngle, 1f, settings.spotAngle);
            if (settings.beamColor == default)
                settings.beamColor = new Color(0.82f, 0.96f, 1f, 1f);
            if (settings.beamOpacity <= 0f)
                settings.beamOpacity = 0.72f;
            if (settings.beamLength <= 0f)
                settings.beamLength = Mathf.Max(4f, settings.range * 0.7f);
            if (settings.beamWidth <= 0f)
                settings.beamWidth = Mathf.Max(0.5f, settings.spotAngle * 0.1f);
            if (settings.beamCoreWidth <= 0f)
                settings.beamCoreWidth = 0.18f;
            if (settings.beamFeather <= 0f)
                settings.beamFeather = 0.62f;
            if (settings.beamFalloff <= 0f)
                settings.beamFalloff = 1.6f;
            if (settings.beamBottomFadeStart <= 0f)
                settings.beamBottomFadeStart = 0.45f;
            if (settings.beamLocalOffset == default)
                settings.beamLocalOffset = new Vector3(0f, -0.2f, -0.05f);
            settings.beamCoreWidth = Mathf.Clamp01(settings.beamCoreWidth);
            settings.beamFeather = Mathf.Clamp01(settings.beamFeather);
            settings.beamFalloff = Mathf.Max(0.1f, settings.beamFalloff);
            settings.beamBottomFadeStart = Mathf.Clamp01(settings.beamBottomFadeStart);
            tintLights[i] = settings;
        }
    }

    static TintLightSettings CreateDefaultTintLightSettings(int index)
    {
        return new TintLightSettings
        {
            name = $"Beam {index + 1:00}",
            enabled = true,
            color = new Color(0.34f, 0.86f, 1f, 1f),
            intensity = 3.2f,
            localPosition = new Vector3(index * 2f, 6f, 3.5f),
            eulerAngles = new Vector3(68f, 0f, 0f),
            range = 16f,
            spotAngle = 28f,
            innerSpotAngle = 8f,
            beamColor = new Color(0.82f, 0.96f, 1f, 1f),
            disableBeamEffect = false,
            beamOpacity = 0.72f,
            beamLength = 11f,
            beamWidth = 2.8f,
            beamCoreWidth = 0.18f,
            beamFeather = 0.62f,
            beamFalloff = 1.6f,
            disableBottomFade = false,
            beamBottomFadeStart = 0.45f,
            beamLocalOffset = new Vector3(0f, -0.2f, -0.05f),
            beamEulerAngles = Vector3.zero,
            beamSortingOrder = 80
        };
    }

    void UpdateDistanceContrast()
    {
        if (!enableDistanceContrast || targetCamera == null)
            return;

        if (_contrastParameter == null)
            ResolveContrastParameter();
        if (_contrastParameter == null)
            return;

        float distance = Mathf.Abs(targetCamera.transform.position.z - transform.position.z);
        float range = Mathf.Max(0.01f, contrastFarDistance - contrastNearDistance);
        float t = Mathf.Clamp01((distance - contrastNearDistance) / range);
        float contrast = Mathf.Lerp(nearContrast, farContrast, t) * _currentWeight;
        WriteFloatProperty(_contrastParameter, "value", contrast);
        WriteBoolProperty(_contrastParameter, "overrideState", true);
    }

    void ResolveContrastParameter()
    {
        _colorAdjustments = null;
        _contrastParameter = null;

        var profile = ReadObjectProperty(underwaterVolume, "profile") ??
                      ReadObjectProperty(underwaterVolume, "sharedProfile");
        if (profile == null)
            return;

        var componentsObject = ReadObjectProperty(profile, "components");
        if (componentsObject is not System.Collections.IEnumerable components)
            return;

        foreach (var component in components)
        {
            if (component == null || component.GetType().Name != "ColorAdjustments")
                continue;

            _colorAdjustments = component;
            _contrastParameter = ReadObjectField(component, "contrast");
            return;
        }
    }

    void SetTintLightsEnabled(bool enabled)
    {
        if (_tintLights == null)
            return;

        foreach (var light in _tintLights)
            SetLightEnabled(light, enabled);

        SetBeamRenderersEnabled(enabled);
    }

    void SetBeamRenderersEnabled(bool enabled)
    {
        if (_beamRenderers == null)
            return;

        for (int i = 0; i < _beamRenderers.Length; i++)
            SetBeamRendererEnabled(i, enabled);
    }

    void SetBeamRendererEnabled(int index, bool enabled)
    {
        if (_beamRenderers == null || index < 0 || index >= _beamRenderers.Length)
            return;

        if (_beamRenderers[index] != null)
            _beamRenderers[index].enabled = enabled;
    }

    void DestroyTintLights()
    {
        if (_tintLights == null)
            return;

        DestroyBeamRenderers();
        for (int i = 0; i < _tintLights.Length; i++)
            DestroyLight(ref _tintLights[i]);
        _tintLights = new Light[0];
    }

    void DestroyBeamRenderers()
    {
        if (_beamRenderers != null)
        {
            for (int i = 0; i < _beamRenderers.Length; i++)
            {
                if (_beamRenderers[i] != null)
                    DestroyLocal(_beamRenderers[i].gameObject);
            }
        }

        if (_beamSprites != null)
        {
            for (int i = 0; i < _beamSprites.Length; i++)
                DestroyBeamSprite(i);
        }

        _beamRenderers = new SpriteRenderer[0];
        _beamSprites = new Sprite[0];
        _beamSpriteKeys = new int[0];
    }

    void DestroyBeamSprite(int index)
    {
        if (_beamSprites == null || index < 0 || index >= _beamSprites.Length)
            return;

        if (_beamSprites[index] != null)
        {
            var texture = _beamSprites[index].texture;
            DestroyLocal(_beamSprites[index]);
            DestroyLocal(texture);
            _beamSprites[index] = null;
        }
    }

    static string SafeLightName(string rawName)
    {
        return string.IsNullOrWhiteSpace(rawName) ? "Beam" : rawName.Trim();
    }

    void EnablePostProcessing()
    {
        if (!enableCameraPostProcessing || targetCamera == null)
            return;

        var cameraData = FindComponentByTypeName(targetCamera.gameObject, "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
        if (cameraData == null)
            cameraData = AddComponentByTypeName(targetCamera.gameObject, "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");

        if (cameraData != null)
            WriteBoolProperty(cameraData, "renderPostProcessing", true);
    }

    Component FindVolumeComponent()
    {
        return FindComponentByTypeName(gameObject, "UnityEngine.Rendering.Volume");
    }

    float ReadVolumeWeight()
    {
        if (underwaterVolume == null)
            return 0f;

        var property = underwaterVolume.GetType().GetProperty("weight");
        if (property == null || property.PropertyType != typeof(float))
            return 0f;

        return (float)property.GetValue(underwaterVolume);
    }

    static Component FindComponentByTypeName(GameObject owner, string typeName)
    {
        if (owner == null)
            return null;

        foreach (var component in owner.GetComponents<Component>())
            if (component != null && component.GetType().FullName == typeName)
                return component;

        return null;
    }

    static Component AddComponentByTypeName(GameObject owner, string typeName)
    {
        var type = FindTypeByName(typeName);
        return type != null && typeof(Component).IsAssignableFrom(type)
            ? owner.AddComponent(type)
            : null;
    }

    static System.Type FindTypeByName(string typeName)
    {
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }

        return null;
    }

    static void SetLightEnabled(Light light, bool enabled)
    {
        if (light != null)
            light.enabled = enabled;
    }

    static void DestroyLight(ref Light light)
    {
        if (light != null)
        {
            DestroyLocal(light.gameObject);
            light = null;
        }
    }

    static void WriteFloatProperty(Component component, string propertyName, float value)
    {
        var property = component.GetType().GetProperty(propertyName);
        if (property != null && property.PropertyType == typeof(float) && property.CanWrite)
            property.SetValue(component, value);
    }

    static void WriteBoolProperty(Component component, string propertyName, bool value)
    {
        var property = component.GetType().GetProperty(propertyName);
        if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
            property.SetValue(component, value);
    }

    static void WriteBoolProperty(object target, string propertyName, bool value)
    {
        if (target == null)
            return;

        var property = target.GetType().GetProperty(propertyName);
        if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
            property.SetValue(target, value);
    }

    static void WriteFloatProperty(object target, string propertyName, float value)
    {
        if (target == null)
            return;

        var property = target.GetType().GetProperty(propertyName);
        if (property != null && property.PropertyType == typeof(float) && property.CanWrite)
            property.SetValue(target, value);
    }

    static object ReadObjectProperty(object target, string propertyName)
    {
        if (target == null)
            return null;

        var property = target.GetType().GetProperty(propertyName);
        return property != null ? property.GetValue(target) : null;
    }

    static object ReadObjectField(object target, string fieldName)
    {
        if (target == null)
            return null;

        var field = target.GetType().GetField(fieldName);
        return field != null ? field.GetValue(target) : null;
    }

    static void DestroyLocal(Object obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    public void ApplyLevelVisual(LevelVisualSO visual)
    {
        if (visual == null) return;

        sunLightColor        = visual.sunLightColor;
        sunLightIntensity    = visual.sunLightIntensity;
        sunLightEulerAngles  = visual.sunLightEulerAngles;
        sunLightPulseAmount  = visual.sunLightPulseAmount;
        sunLightPulseSpeed   = visual.sunLightPulseSpeed;

        nearContrast = visual.nearContrast;
        farContrast  = visual.farContrast;

        if (tintLights != null && tintLights.Length > 0)
        {
            tintLights[0].color      = visual.tintLightColor;
            tintLights[0].intensity  = visual.tintLightIntensity;
            tintLights[0].beamColor  = visual.beamColor;
            tintLights[0].beamOpacity = visual.beamOpacity;
        }
    }
}
