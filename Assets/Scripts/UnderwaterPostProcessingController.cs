using UnityEngine;

[ExecuteAlways]
public sealed class UnderwaterPostProcessingController : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] Component underwaterVolume;
    [SerializeField] bool alwaysUnderwater = true;
    [SerializeField] float waterSurfaceY = 0f;
    [SerializeField, Min(0.01f)] float blendDepth = 1.5f;
    [SerializeField, Min(0f)] float blendSpeed = 6f;
    [SerializeField, Range(0f, 1f)] float maxWeight = 1f;
    [SerializeField] bool enableCameraPostProcessing = true;

    float _currentWeight;

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
        UpdateVolumeWeight(immediate: true);
    }

    void OnValidate()
    {
        if (underwaterVolume == null)
            underwaterVolume = FindVolumeComponent();

        blendDepth = Mathf.Max(0.01f, blendDepth);
        blendSpeed = Mathf.Max(0f, blendSpeed);
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
        {
            if (component != null && component.GetType().FullName == typeName)
                return component;
        }

        return null;
    }

    static Component AddComponentByTypeName(GameObject owner, string typeName)
    {
        var type = FindTypeByName(typeName);

        if (type == null || !typeof(Component).IsAssignableFrom(type))
            return null;

        return owner.AddComponent(type);
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
}
