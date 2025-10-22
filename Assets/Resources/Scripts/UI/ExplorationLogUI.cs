using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExplorationLogUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] Transform contentRoot;     // VerticalLayoutGroup 붙어있는 컨테이너
    [SerializeField] Text logItemPrefab;        // 한 줄용 Text 프리팹

    [Header("Config")]
    int maxEntries = 10;        // 표시 최대 개수
    float defaultLifetime = 10f;

    [Header("Effects")]
    [SerializeField] bool fadeOnAdd = true;   // 등장 페이드 사용
    [SerializeField] float fadeInSeconds = 0.15f;   // 페이드 시간
    [SerializeField] bool fadeOnRemove = true;       // 제거 시 페이드 여부
    [SerializeField] float fadeOutSeconds = 0.25f;   // 페이드 시간

    // 로그가 뜰 때 잠깐 게임 정지할지 여부/시간(실시간 기준)
    [SerializeField] bool pauseOnPush = true;
    float pushPauseSeconds = 1.0f;

    class Entry
    {
        public GameObject go;
        public Text txt;
        public CanvasGroup cg;        // 있으면 전체 항목 페이드
        public float expireAt;
        public bool expiring;         // 중복 페이드 방지
        public Coroutine fadeInCo;
        public Coroutine fadeOutCo;
    }
    readonly List<Entry> _entries = new();

    void Awake()
    {
        // Shared에 연결(다른 스크립트에서 접근)
        Shared.explorationLogUI = this;
    }

    public void Push(string message, float? lifetime = null, bool? pause = null, float? pauseSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(message) || !contentRoot || !logItemPrefab) return;

        // 초과되면 가장 오래된 것부터 페이드 아웃 예약
        while (_entries.Count >= maxEntries)
            RemoveAt(0);

        var item = Instantiate(logItemPrefab, contentRoot);
        item.text = message;

        // CanvasGroup 있으면 전체 페이드, 없으면 텍스트만 페이드
        var e = new Entry
        {
            go = item.gameObject,
            txt = item,
            cg = item.GetComponent<CanvasGroup>(),
            expireAt = Time.unscaledTime + Mathf.Max(0.01f, lifetime ?? defaultLifetime),
        };
        _entries.Add(e);

        // 등장 페이드
        if (fadeOnAdd)
        {
            if (e.cg == null) e.cg = item.GetComponent<CanvasGroup>(); // 혹시 프리팹에 붙어 있으면 씀
            if (e.cg != null)
            {
                if (e.fadeInCo != null) StopCoroutine(e.fadeInCo);
                e.fadeInCo = StartCoroutine(Co_FadeInCanvasGroup(e));
            }
            else if (e.txt != null)
            {
                e.txt.canvasRenderer.SetAlpha(0f);
                e.txt.CrossFadeAlpha(1f, Mathf.Max(0.01f, fadeInSeconds), true);
            }
        }

        // 잠깐 이동락 + 타이머 일시정지
        bool doPause = (pause ?? pauseOnPush);
        if (doPause)
        {
            float sec = Mathf.Max(0.01f, (float)(pauseSeconds ?? pushPauseSeconds));
            Shared.PlayerMovement?.LockMovementFor(sec);    // 이동만 잠금
            Shared.ExplorationTimerUi?.PauseForRealtime(sec);   // 타이머만 멈춤
        }
    }

    void Update()
    {
        // 만료 체크 실시간
        for (int i = 0; i < _entries.Count;)
        {
            var e = _entries[i];
            if (!e.expiring && Time.unscaledTime >= e.expireAt)
            {
                BeginExpire(e);   // 페이드 시작(리스트에서 당장 제거하지 않음)
                // i 증가 안 하고 다음 항목 계속 검사
            }
            else
            {
                i++;
            }
        }
    }

    void RemoveAt(int index)
    {
        if (index < 0 || index >= _entries.Count) return;
        BeginExpire(_entries[index]);
    }
    void BeginExpire(Entry e)
    {
        if (e == null || e.expiring) return;
        e.expiring = true;

        // 등장 페이드 중이면 중단
        if (e.fadeInCo != null) { StopCoroutine(e.fadeInCo); e.fadeInCo = null; }

        if (!fadeOnRemove || fadeOutSeconds <= 0f)
        {
            RemoveEntryImmediate(e);
            return;
        }

        if (e.fadeOutCo != null) StopCoroutine(e.fadeOutCo);
        e.fadeOutCo = StartCoroutine(Co_FadeOutThenRemove(e));
    }
    System.Collections.IEnumerator Co_FadeInCanvasGroup(Entry e)
    {
        if (e.cg == null) yield break;
        e.cg.alpha = 0f;
        float t0 = Time.unscaledTime, dur = Mathf.Max(0.01f, fadeInSeconds);
        while (true)
        {
            float t = (Time.unscaledTime - t0) / dur;
            if (t >= 1f) break;
            // expiring 되면 즉시 중단
            if (e.expiring) yield break;
            e.cg.alpha = t;
            yield return null;
        }
        e.cg.alpha = 1f;
        e.fadeInCo = null;
    }

    System.Collections.IEnumerator Co_FadeOutThenRemove(Entry e)
    {
        if (e.cg != null)
        {
            e.cg.alpha = Mathf.Min(e.cg.alpha, 1f);
            float start = e.cg.alpha;
            float t0 = Time.unscaledTime, dur = Mathf.Max(0.01f, fadeOutSeconds);
            while (true)
            {
                float t = (Time.unscaledTime - t0) / dur;
                if (t >= 1f) break;
                e.cg.alpha = Mathf.Lerp(start, 0f, t);
                yield return null;
            }
        }
        else if (e.txt != null)
        {
            e.txt.canvasRenderer.SetAlpha(1f);
            e.txt.CrossFadeAlpha(0f, Mathf.Max(0.01f, fadeOutSeconds), true);
            yield return new WaitForSecondsRealtime(fadeOutSeconds);
        }
        RemoveEntryImmediate(e);
    }
    void RemoveEntryImmediate(Entry e)
    {
        int idx = _entries.IndexOf(e);
        if (idx >= 0) _entries.RemoveAt(idx);
        if (e.go) Destroy(e.go);
    }

    // 옵션에서 런타임 조절용
    public void SetMaxEntries(int n)
    {
        maxEntries = Mathf.Max(1, n);
        // 초과분은 페이드로 제거
        while (_entries.Count > maxEntries)
            RemoveAt(0);
    }
    public void SetDefaultLifetime(float seconds) => defaultLifetime = Mathf.Max(0.01f, seconds);
}
