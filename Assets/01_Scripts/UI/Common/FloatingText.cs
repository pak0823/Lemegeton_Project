using System.Collections;
using TMPro;
using UnityEngine;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tmp;

    [Header("Motion")]
    public float floatUpDistance = 0.6f;
    public float duration = 0.7f;

    [Header("Fade")]
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    // [New] 현재 설정 저장용
    private FloatingTextConfig _currentConfig;
    private Vector3 _startScale;

    private Coroutine _co;

    private void Awake()
    {
        if (tmp == null) tmp = GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.raycastTarget = false; // [Fix] 클릭 방해 금지
        _startScale = transform.localScale;
    }

    public void SetStyle(FloatingTextConfig config)
    {
        _currentConfig = config;
        
        // 색상 적용
        if (tmp != null) tmp.color = config.color;

        // 크기 적용 (기본 스케일 * 배율)
        transform.localScale = _startScale * (config.scaleMultiplier > 0 ? config.scaleMultiplier : 1f);
    }

    public void Play(string text, Vector3 worldPos)
    {
        if (_co != null) StopCoroutine(_co);

        transform.position = worldPos;
        tmp.text = text;

        // 알파 초기화 (색상은 SetStyle에서 이미 설정됨)
        var c = tmp.color;
        c.a = 1f;
        tmp.color = c;

        _co = StartCoroutine(Co_Play());
    }

    private IEnumerator Co_Play()
    {
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * floatUpDistance;

        // 속도 배율 적용 (기본 1.0)
        float speedMult = _currentConfig.moveSpeedMultiplier > 0 ? _currentConfig.moveSpeedMultiplier : 1f;
        float finalDuration = duration / speedMult;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, finalDuration);
            float a = alphaCurve.Evaluate(Mathf.Clamp01(t));

            transform.position = Vector3.Lerp(start, end, t);

            // 색상 알파값 갱신
            var c = tmp.color;
            c.a = a;
            tmp.color = c;

            // [New] 스케일 애니메이션 (커브가 있다면)
            if (_currentConfig.scaleCurve != null && _currentConfig.scaleCurve.length > 0)
            {
                float s = _currentConfig.scaleCurve.Evaluate(Mathf.Clamp01(t));
                transform.localScale = _startScale * (_currentConfig.scaleMultiplier > 0 ? _currentConfig.scaleMultiplier : 1f) * s;
            }

            yield return null;
        }

        FloatingTextManager.Instance?.Despawn(this);
    }
}
