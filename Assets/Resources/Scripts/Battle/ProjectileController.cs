using System;
using System.Collections;
using UnityEngine;

public class ProjectileController : MonoBehaviour
{
    [SerializeField] float travelTime = 0.35f;
    [SerializeField] float arcHeight = 0.6f;
    [SerializeField] GameObject usePrefab;
    [SerializeField] float prefabDuration = 0.25f;

    private Vector3 start;
    private Vector3 target;
    private Action onArrive; // 도착 후(폭발 직후) 콜백

    public void Init(Vector3 startWorld, Vector3 targetWorld, Action onArriveCallback,
                     float? overrideTravelTime = null, float? speedUnitsPerSec = null, float? overrideArc = null)
    {
        start = startWorld;
        target = targetWorld;
        onArrive = onArriveCallback;

        float dist = Vector3.Distance(start, target);
        if (speedUnitsPerSec.HasValue)
            travelTime = dist / Mathf.Max(0.01f, speedUnitsPerSec.Value); // 일정 속도 유지
        if (overrideTravelTime.HasValue)
            travelTime = Mathf.Max(0.01f, overrideTravelTime.Value);      // 시간 직접 지정
        if (overrideArc.HasValue)
            arcHeight = overrideArc.Value;                                 // 필요 시 포물선 높이도 오버라이드

        StartCoroutine(Co_Fly());
    }

    IEnumerator Co_Fly()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, travelTime);
            var p = Vector3.Lerp(start, target, t);
            p.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.position = p;
            yield return null;
        }

        // 폭발
        if (usePrefab != null)
        {
            var fx = Instantiate(usePrefab, target, Quaternion.identity);
            Destroy(fx, prefabDuration);
        }

        onArrive?.Invoke(); // 여기서 범위피해 적용 등을 호출하도록 함
        Destroy(gameObject); // 투사체는 스스로 소멸
    }
}
