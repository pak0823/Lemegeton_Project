using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance { get; private set; }

    [Header("Setup")]
    [SerializeField] private Transform worldCanvasRoot; // World Space Canvas의 RectTransform
    [SerializeField] private FloatingText floatingTextPrefab;

    [Header("Styles")]
    [SerializeField] private List<FloatingTextConfig> styleConfigs = new List<FloatingTextConfig>();
    private Dictionary<FloatingTextStyle, FloatingTextConfig> _styleDict;

    [Header("Pooling")]
    [SerializeField] private int prewarm = 20;

    [Header("Spawn Throttle")]
    [SerializeField] private float spawnInterval = 0.05f; // 간격 줄임 (랜덤 오프셋이므로 겹침 덜함)
    private Coroutine _drainCo;

    private readonly Queue<FloatingText> _pool = new();
    private readonly Queue<SpawnRequest> _spawnQueue = new();

    private struct SpawnRequest
    {
        public Vector3 pos;
        public string text;
        public FloatingTextStyle style; // [New] 스타일 정보 포함
        public SpawnRequest(Vector3 p, string t, FloatingTextStyle s) { pos = p; text = t; style = s; }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 리스트 -> 딕셔너리 변환 (빠른 검색)
        _styleDict = new Dictionary<FloatingTextStyle, FloatingTextConfig>();
        foreach (var cfg in styleConfigs)
        {
            if (!_styleDict.ContainsKey(cfg.style))
                _styleDict.Add(cfg.style, cfg);
        }

        Prewarm();
    }

    private void Prewarm()
    {
        for (int i = 0; i < prewarm; i++)
        {
            var ft = CreateNew();
            ft.gameObject.SetActive(false);
            _pool.Enqueue(ft);
        }
    }

    private FloatingText CreateNew()
    {
        var ft = Instantiate(floatingTextPrefab, worldCanvasRoot);
        return ft;
    }

    // [Legacy Support] 기존 코드 호환용 (기본 Damage 스타일 적용)
    public void Spawn(Vector3 worldPos, string text)
    {
        Spawn(worldPos, text, FloatingTextStyle.Damage);
    }

    // [New] 스타일 지정 스폰
    public void Spawn(Vector3 worldPos, string text, FloatingTextStyle style)
    {
        if (floatingTextPrefab == null || worldCanvasRoot == null) return;

        _spawnQueue.Enqueue(new SpawnRequest(worldPos, text, style));

        if (_drainCo == null)
            _drainCo = StartCoroutine(Co_DrainQueue());
    }

    public void Despawn(FloatingText ft)
    {
        if (ft == null) return;

        ft.gameObject.SetActive(false);
        ft.transform.SetParent(worldCanvasRoot, true);
        _pool.Enqueue(ft);
    }

    //큐를 일정 간격으로 비우는 코루틴
    private IEnumerator Co_DrainQueue()
    {
        while (_spawnQueue.Count > 0)
        {
            var req = _spawnQueue.Dequeue();

            // [Mod] 랜덤 오프셋 적용 (-0.2 ~ 0.2 범위)
            float randomX = Random.Range(-0.2f, 0.2f);
            float randomY = Random.Range(-0.1f, 0.2f);
            Vector3 finalPos = req.pos + new Vector3(randomX, randomY, 0);

            var ft = (_pool.Count > 0) ? _pool.Dequeue() : CreateNew();
            ft.gameObject.SetActive(true);

            // 스타일 설정 적용
            if (_styleDict != null && _styleDict.TryGetValue(req.style, out var config))
            {
                ft.SetStyle(config);
            }
            else
            {
                // 기본값 (흰색, 1.0)
                ft.SetStyle(new FloatingTextConfig { color = Color.white, scaleMultiplier = 1f });
            }

            ft.Play(req.text, finalPos);

            // 다음 텍스트까지 간격
            float wait = Mathf.Max(0f, spawnInterval);
            if (wait > 0f)
                yield return new WaitForSeconds(wait);
            else
                yield return null;
        }

        _drainCo = null;
    }
}
