using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] SpriteRenderer targetRenderer;
    [SerializeField] Sprite[] frames = new Sprite[0];
    [SerializeField, Min(0.1f), Tooltip("Playback speed for this object. Each animated prefab can use a different value.")]
    float framesPerSecond = 6f;
    [SerializeField, Tooltip("Play forward to the last frame, then rewind to the first frame.")]
    bool pingPong = true;
    [SerializeField] bool randomizeStartFrame = true;

    float _elapsed;
    int _frameIndex;
    int _direction = 1;
    double _lastRealtime;

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        _frameIndex = randomizeStartFrame && frames.Length > 1
            ? Random.Range(0, frames.Length)
            : 0;
        _direction = 1;
        _elapsed = 0f;
        _lastRealtime = Time.realtimeSinceStartupAsDouble;
        ApplyFrame();
    }

    void Update()
    {
        if (frames == null || frames.Length < 2 || targetRenderer == null)
            return;

        double now = Time.realtimeSinceStartupAsDouble;
        float deltaTime = Application.isPlaying
            ? Time.deltaTime
            : Mathf.Max(0f, (float)(now - _lastRealtime));
        _lastRealtime = now;
        _elapsed += deltaTime;
        float frameDuration = 1f / Mathf.Max(0.1f, framesPerSecond);
        while (_elapsed >= frameDuration)
        {
            _elapsed -= frameDuration;
            AdvanceFrame();
        }

        ApplyFrame();
    }

    void ApplyFrame()
    {
        if (targetRenderer != null && frames != null && frames.Length > 0)
            targetRenderer.sprite = frames[Mathf.Clamp(_frameIndex, 0, frames.Length - 1)];
    }

    void AdvanceFrame()
    {
        if (!pingPong || frames.Length < 3)
        {
            _frameIndex = (_frameIndex + 1) % frames.Length;
            return;
        }

        int nextFrame = _frameIndex + _direction;
        if (nextFrame >= frames.Length)
        {
            _direction = -1;
            nextFrame = frames.Length - 2;
        }
        else if (nextFrame < 0)
        {
            _direction = 1;
            nextFrame = 1;
        }

        _frameIndex = nextFrame;
    }
}
