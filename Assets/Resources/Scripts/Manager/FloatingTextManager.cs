using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance { get; private set; }

    [Header("Setup")]
    [SerializeField] private Transform worldCanvasRoot; // World Space Canvas의 RectTransform
    [SerializeField] private FloatingText floatingTextPrefab;

    [Header("Pooling")]
    [SerializeField] private int prewarm = 20;

    [Header("Spawn Throttle")]
    [SerializeField] private float spawnInterval = 0.5f; // 동시 요청 시 텍스트 간격(초)
    private Coroutine _drainCo;

    private readonly Queue<FloatingText> _pool = new();
    private readonly Queue<SpawnRequest> _spawnQueue = new();

    private struct SpawnRequest
    {
        public Vector3 pos;
        public string text;
        public SpawnRequest(Vector3 p, string t) { pos = p; text = t; }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

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

    //생성할 텍스트를 큐에 적재
    public void Spawn(Vector3 worldPos, string text)
    {
        if (floatingTextPrefab == null || worldCanvasRoot == null) return;

        _spawnQueue.Enqueue(new SpawnRequest(worldPos, text));

        if (_drainCo == null)
            _drainCo = StartCoroutine(Co_DrainQueue());
    }
    //생성한 풀링 오브젝트 비활성화
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
        int idx = 0;

        while (_spawnQueue.Count > 0)
        {
            var req = _spawnQueue.Dequeue();

            req.pos += Vector3.up * (0.1f * idx);
            idx++;

            var ft = (_pool.Count > 0) ? _pool.Dequeue() : CreateNew();
            ft.gameObject.SetActive(true);
            ft.Play(req.text, req.pos);

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
