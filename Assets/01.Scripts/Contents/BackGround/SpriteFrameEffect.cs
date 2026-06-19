using UnityEngine;

sealed class SpriteFrameEffect : MonoBehaviour
{
    SpriteRenderer _renderer;
    Sprite[] _frames;
    float _fps;
    bool _destroyOnComplete;
    float _elapsed;
    int _frameIndex;

    public void Initialize(SpriteRenderer renderer, Sprite[] frames, float fps, bool destroyOnComplete)
    {
        _renderer = renderer;
        _frames = frames;
        _fps = Mathf.Max(1f, fps);
        _destroyOnComplete = destroyOnComplete;
        _elapsed = 0f;
        _frameIndex = 0;

        if (_renderer != null && _frames != null && _frames.Length > 0)
            _renderer.sprite = _frames[0];
    }

    void Update()
    {
        if (_renderer == null || _frames == null || _frames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        _elapsed += Time.deltaTime;
        int nextFrame = Mathf.FloorToInt(_elapsed * _fps);
        if (nextFrame == _frameIndex)
            return;

        if (nextFrame >= _frames.Length)
        {
            if (_destroyOnComplete)
            {
                Destroy(gameObject);
                return;
            }

            nextFrame %= _frames.Length;
            _elapsed = nextFrame / _fps;
        }

        _frameIndex = nextFrame;
        _renderer.sprite = _frames[_frameIndex];
    }
}

