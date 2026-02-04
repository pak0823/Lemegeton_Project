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

    private Coroutine _co;

    public void Play(string text, Vector3 worldPos)
    {
        if (_co != null) StopCoroutine(_co);

        transform.position = worldPos;
        tmp.text = text;

        // 알파 초기화
        var c = tmp.color;
        c.a = 1f;
        tmp.color = c;

        _co = StartCoroutine(Co_Play());
    }

    private IEnumerator Co_Play()
    {
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * floatUpDistance;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            float a = alphaCurve.Evaluate(Mathf.Clamp01(t));

            transform.position = Vector3.Lerp(start, end, t);

            var c = tmp.color;
            c.a = a;
            tmp.color = c;

            yield return null;
        }

        FloatingTextManager.Instance?.Despawn(this);
    }
}
