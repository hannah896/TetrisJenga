using JSAM;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 스테이지 맵 위에서 움직이는 잠수함 VisualElement와 거품 파티클을 관리한다.
/// </summary>
public class StageSubmarineController
{
    const float MoveSpeed      = 450f;   // px/s
    const float BubbleInterval = 0.12f;  // 이동 중 거품 분출 간격 (s)

    VisualElement _map;
    VisualElement _sub;

    float _curX, _curY;
    float _tgtX, _tgtY;
    float _bubbleTimer;

    IVisualElementScheduledItem _moveTicker;

    // ── 초기화 ─────────────────────────────────────────────────────────────

    public void Initialize(VisualElement stageMap, Sprite sprite)
    {
        _map = stageMap;

        _sub = new VisualElement { name = "submarine" };
        _sub.style.position = Position.Absolute;
        _sub.style.width    = 90;
        _sub.style.height   = 54;
        _sub.pickingMode    = PickingMode.Ignore;

        if (sprite != null)
        {
            _sub.style.backgroundImage          = new StyleBackground(sprite);
            _sub.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }
        else
        {
            _sub.style.backgroundColor          = new Color(0.18f, 0.47f, 0.85f, 0.95f);
            _sub.style.borderTopLeftRadius      = 27;
            _sub.style.borderBottomLeftRadius   = 27;
            _sub.style.borderTopRightRadius     = 10;
            _sub.style.borderBottomRightRadius  = 10;
        }

        _map.Add(_sub);
    }

    // ── 위치 제어 ──────────────────────────────────────────────────────────

    /// <summary>애니메이션 없이 즉시 배치 (초기 배치용)</summary>
    public void PlaceAt(Vector2 nodeCenter)
    {
        _curX = _tgtX = nodeCenter.x - 45f;
        _curY = _tgtY = nodeCenter.y - 27f;
        ApplyPos();
    }

    /// <summary>노드 중심으로 애니메이션 이동 + 거품 + 오디오</summary>
    public void MoveTo(Vector2 nodeCenter)
    {
        if (_map == null) return;

        _tgtX = nodeCenter.x - 45f;
        _tgtY = nodeCenter.y - 27f;

        if (_moveTicker == null)
        {
            _bubbleTimer = 0f;
            _moveTicker  = _map.schedule.Execute(Tick).Every(16);
        }

        PlayMoveAudio();
    }

    // ── 이동 틱 ────────────────────────────────────────────────────────────

    void Tick()
    {
        const float dt = 0.016f;
        float dx   = _tgtX - _curX;
        float dy   = _tgtY - _curY;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);

        if (dist < 1.5f)
        {
            _curX = _tgtX;
            _curY = _tgtY;
            ApplyPos();
            _moveTicker?.Pause();
            _moveTicker = null;
            return;
        }

        float step = Mathf.Min(MoveSpeed * dt, dist);
        _curX += dx / dist * step;
        _curY += dy / dist * step;
        ApplyPos();

        _bubbleTimer += dt;
        if (_bubbleTimer >= BubbleInterval)
        {
            _bubbleTimer = 0f;
            SpawnBurst();
        }
    }

    void ApplyPos()
    {
        _sub.style.left = _curX;
        _sub.style.top  = _curY;
    }

    // ── 거품 파티클 ────────────────────────────────────────────────────────

    void SpawnBurst()
    {
        int count = Random.Range(2, 5);
        for (int i = 0; i < count; i++)
            SpawnBubble();
    }

    void SpawnBubble()
    {
        float size     = Random.Range(8f, 22f);
        float ox       = Random.Range(-30f, 30f);
        float startTop = _curY + Random.Range(0f, 27f);
        float startLeft = _curX + 45f + ox - size * 0.5f;

        var b = new VisualElement();
        b.style.position              = Position.Absolute;
        b.style.width                 = size;
        b.style.height                = size;
        b.style.left                  = startLeft;
        b.style.top                   = startTop;
        b.style.borderTopLeftRadius   = size;
        b.style.borderTopRightRadius  = size;
        b.style.borderBottomLeftRadius  = size;
        b.style.borderBottomRightRadius = size;
        b.style.backgroundColor       = new Color(0.55f, 0.88f, 1f, 0.7f);
        b.style.borderTopWidth        = 1.5f;
        b.style.borderRightWidth      = 1.5f;
        b.style.borderBottomWidth     = 1.5f;
        b.style.borderLeftWidth       = 1.5f;
        b.style.borderTopColor        = new StyleColor(new Color(1f, 1f, 1f, 0.55f));
        b.style.borderRightColor      = new StyleColor(new Color(1f, 1f, 1f, 0.55f));
        b.style.borderBottomColor     = new StyleColor(new Color(1f, 1f, 1f, 0.55f));
        b.style.borderLeftColor       = new StyleColor(new Color(1f, 1f, 1f, 0.55f));
        b.pickingMode                 = PickingMode.Ignore;

        _map.Add(b);

        float duration = Random.Range(0.7f, 1.5f);
        float rise     = Random.Range(55f, 110f);
        float elapsed  = 0f;

        // holder 패턴으로 클로저 내 자기 참조 안전하게 처리
        IVisualElementScheduledItem[] holder = { null };
        holder[0] = b.schedule.Execute(() =>
        {
            elapsed += 0.016f;
            float t = Mathf.Clamp01(elapsed / duration);
            b.style.top     = startTop - rise * t;
            b.style.opacity = 1f - t * t;
            if (t >= 1f)
            {
                holder[0]?.Pause();
                if (b.parent != null) _map.Remove(b);
            }
        }).Every(16);
    }

    // ── 오디오 ─────────────────────────────────────────────────────────────

    static void PlayMoveAudio()
    {
        AudioPlayback.PlaySound(_AudioLibrarySounds.BtnClick);
    }
}
