using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExplorationLogUI : MonoBehaviour
{
    public static ExplorationLogUI Instance {  get; private set; }

    private bool pauseGameOnPush = false;          // 로그가 뜰 때 게임도 멈출지

    [Header("Layout")]
    [SerializeField] Transform contentRoot;     // VerticalLayoutGroup 붙어있는 컨테이너
    [SerializeField] Text logItemPrefab;        // 한 줄용 Text 프리팹

    [Header("Config")]
    int maxEntries = 10;        // 표시 최대 개수
    float defaultLifetime = 10f;    // 기본 유지 시간(초)

    [Header("Style")]
    [SerializeField] Color newestColor = Color.white; // 가장 최근 로그 색
    [SerializeField] Color olderColor = new Color(147f / 255f, 147f / 255f, 147f / 255f, 1f); // 이전 로그 색

    [Header("Typing")]
    [SerializeField] bool enableTypewriter = true;  // 타자기 효과
    [SerializeField] float charsPerSecond = 40f;   // 초당 글자 수
    [SerializeField] bool preserveRichText = true;  // <b> 등 태그는 한 번에
    [SerializeField] int baseFontSize = 20; // 기본(이전 로그) 글씨 크기
    [SerializeField] int newestFontSize = 24; // 최신 로그 글씨 크기

    [Header("Effects")]
    [SerializeField] bool fadeOnAdd = true;   // 등장 페이드 사용
    [SerializeField] float fadeInSeconds = 0.15f;
    [SerializeField] bool fadeOnRemove = true;       // 제거 시 페이드 여부
    [SerializeField] float fadeOutSeconds = 0.25f;

    [Header("Behavior")]
    [SerializeField] bool pauseOnPush = true;  // 로그 띄울 때 잠깐 정지(플레이어+탐험 타이머)
    [SerializeField] float pushPauseSeconds = 1.0f;

    private Coroutine _pauseCo;

    class Entry
    {
        public GameObject go;
        public Text txt;
        public CanvasGroup cg;  // 있으면 전체 항목 페이드
        public float remaining; // 수명(초) - GamePause.IsPaused면 감소 멈춤
        public bool expiring;   // 중복 페이드 방지
        public Coroutine fadeInCo;
        public Coroutine fadeOutCo;
        // 타자기
        public string fullText;
        public Coroutine typeCo;
    }
    readonly List<Entry> _entries = new();

    void Awake()
    {
        // Shared에 연결(다른 스크립트에서 접근)
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 게임 일시정지 예약
    private void PauseGameForSeconds(float seconds)
    {
        var speed = GameSpeedController.Instance;
        if (speed == null) return;

        // 이미 예약되어 있으면 끊고 새로 예약(가장 최근 로그 기준으로 타이밍 갱신)
        if (_pauseCo != null) StopCoroutine(_pauseCo);

        speed.RequestPause();
        _pauseCo = StartCoroutine(Co_ReleasePauseAfter(seconds));
    }

    private IEnumerator Co_ReleasePauseAfter(float seconds)
    {
        float end = Time.unscaledTime + Mathf.Max(0f, seconds);
        while (Time.unscaledTime < end)
            yield return null;

        var speed = GameSpeedController.Instance;
        if (speed != null) speed.ReleasePause();
        _pauseCo = null;
    }

    public void Push(string message, float? lifetime = null, bool? pause = null, float? pauseSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(message) || !contentRoot || !logItemPrefab) return;

        // 초과되면 가장 오래된 것부터 페이드 아웃 예약
        while (_entries.Count >= maxEntries) RemoveAt(0);

        // 기존 항목 색상을 "이전 색(검정)"으로 강등
        for (int i = 0; i < _entries.Count; i++)
        {
            SetTextColorRGB(_entries[i], olderColor);
            SetFontSize(_entries[i], baseFontSize);
        }
            

        var item = Instantiate(logItemPrefab, contentRoot);
        //item.text = message;

        // CanvasGroup 있으면 전체 페이드, 없으면 텍스트만 페이드
        var e = new Entry
        {
            go = item.gameObject,
            txt = item,
            cg = item.GetComponent<CanvasGroup>(),
            remaining = Mathf.Max(0.01f, lifetime ?? defaultLifetime),
            fullText = message
        };
        _entries.Add(e);

        // 신규 항목 색상 = 흰색(알파는 유지)
        SetTextColorRGB(e, newestColor);
        SetFontSize(e, newestFontSize);

        // 등장 페이드
        if (fadeOnAdd)
        {
            if (e.cg == null) e.cg = item.GetComponent<CanvasGroup>();
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

        // 타자기 효과
        if (enableTypewriter && e.txt != null)
        {
            e.txt.text = string.Empty;
            if (e.typeCo != null) StopCoroutine(e.typeCo);
            e.typeCo = StartCoroutine(Co_Typewriter(e, e.fullText));
        }
        else
        {
            if (e.txt != null) e.txt.text = e.fullText;
        }

        // 잠깐 이동락 + 타이머 일시정지
        bool doPause = (pause ?? pauseOnPush);
        if (doPause)
        {
            float sec = Mathf.Max(0.01f, (float)(pauseSeconds ?? pushPauseSeconds));
            PlayerMovement.Instance?.LockMovementFor(sec);    // 이동만 잠금

            if (pauseGameOnPush)
                PauseGameForSeconds(sec);
        }
    }

    void Update()
    {
        // 수명 감소: "옵션 등으로 게임이 일시정지면" 감소 중단 (= 로그도 멈춤)
        if (!GamePause.IsPaused)
        {
            float dt = Time.unscaledDeltaTime; // 게임 진행 중엔 실시간 기준으로 감소
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!e.expiring)
                {
                    e.remaining -= dt;
                    if (e.remaining <= 0f)
                        BeginExpire(e);
                }
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

        // 타자기/등장 페이드 중이면 중단
        if (e.typeCo != null) { StopCoroutine(e.typeCo); e.typeCo = null; }
        if (e.fadeInCo != null) { StopCoroutine(e.fadeInCo); e.fadeInCo = null; }

        if (!fadeOnRemove || fadeOutSeconds <= 0f)
        {
            RemoveEntryImmediate(e);
            return;
        }

        if (e.fadeOutCo != null) StopCoroutine(e.fadeOutCo);
        e.fadeOutCo = StartCoroutine(Co_FadeOutThenRemove(e));
    }
    void RemoveEntryImmediate(Entry e)
    {
        int idx = _entries.IndexOf(e);
        if (idx >= 0) _entries.RemoveAt(idx);
        if (e.go) Destroy(e.go);
    }
    System.Collections.IEnumerator Co_FadeInCanvasGroup(Entry e)
    {
        if (e.cg == null) yield break;
        e.cg.alpha = 0f;
        float dur = Mathf.Max(0.01f, fadeInSeconds);
        float elapsed = 0f;
        while (elapsed < dur)
        {
            if (!GamePause.IsPaused)
            {
                elapsed += Time.unscaledDeltaTime;
                e.cg.alpha = Mathf.Clamp01(elapsed / dur);
            }
            yield return null;
        }
        e.cg.alpha = 1f;
        e.fadeInCo = null;
    }

    System.Collections.IEnumerator Co_FadeOutThenRemove(Entry e)
    {
        float dur = Mathf.Max(0.01f, fadeOutSeconds);
        if (e.cg != null)
        {
            float startAlpha = Mathf.Min(1f, e.cg.alpha);
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (!GamePause.IsPaused)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / dur);
                    e.cg.alpha = Mathf.Lerp(startAlpha, 0f, t);
                }
                yield return null;
            }
        }
        else if (e.txt != null)
        {
            // Text만 있을 때도 일시정지 동안 멈추게 수동 구현
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (!GamePause.IsPaused) elapsed += Time.unscaledDeltaTime;
                // 알파 보정 (CanvasRenderer를 직접 건드리기보단 Color 알파 조정 권장)
                var c = e.txt.color; c.a = 1f - Mathf.Clamp01(elapsed / dur); e.txt.color = c;
                yield return null;
            }
        }
        RemoveEntryImmediate(e);
    }
    System.Collections.IEnumerator Co_Typewriter(Entry e, string full)
    {
        if (e.txt == null || string.IsNullOrEmpty(full)) yield break;

        e.txt.text = string.Empty;
        float secPerChar = (charsPerSecond > 0f) ? (1f / charsPerSecond) : 0f;
        float accum = 0f;
        int i = 0;

        while (i < full.Length)
        {
            if (!GamePause.IsPaused)
            {
                accum += Time.unscaledDeltaTime;

                while (accum >= secPerChar && i < full.Length)
                {
                    if (preserveRichText && full[i] == '<')
                    {
                        // 태그는 한 번에 통째로 붙이기
                        int close = full.IndexOf('>', i);
                        if (close >= 0)
                        {
                            e.txt.text += full.Substring(i, close - i + 1);
                            i = close + 1;
                            continue;
                        }
                    }

                    e.txt.text += full[i];
                    i++;
                    accum -= secPerChar;
                }
            }
            yield return null;
        }
        e.typeCo = null;
    }
    // 알파는 보존, RGB만 변경
    void SetTextColorRGB(Entry e, Color rgb)
    {
        if (e?.txt == null) return;
        var c = e.txt.color;
        e.txt.color = new Color(rgb.r, rgb.g, rgb.b, c.a);
    }

    // 옵션에서 런타임 조절용
    public void SetMaxEntries(int n)
    {
        maxEntries = Mathf.Max(1, n);
        // 초과분은 페이드로 제거
        while (_entries.Count > maxEntries) RemoveAt(0);
    }

    //폰트 사이즈 조정
    void SetFontSize(Entry e, int size)
    {
        if (e?.txt) e.txt.fontSize = size;
    }
    public void SetDefaultLifetime(float seconds) => defaultLifetime = Mathf.Max(0.01f, seconds);
}
