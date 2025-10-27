using UnityEngine;
using UnityEngine.Events;

public class BarrierController : MonoBehaviour
{
    [Header("Barrier Components")]
    [Tooltip("Barrier의 Collider2D (닫힐 때 활성화)")]
    Collider2D barrierCollider;
    [Tooltip("Barrier 앞에 위치한 트리거 Collider2D (Player 진입 감지)")]
    Collider2D exitTrigger;

    public int stageIndex;  // 이 배리어가 속한 스테이지 번호

    [Header("Interaction")]
    [Tooltip("UI로 'Press F to Exit' 표시용 이벤트")]
    public UnityEvent onShowPrompt;
    public UnityEvent onHidePrompt;

    private bool isOpen = false;
    private bool playerInRange = false;
    private bool hasLoaded = false;
    [SerializeField] private KeyCode surveyKey = KeyCode.F;

    void Start()
    {
        // 장벽용 콜라이더와 진입 감지용 트리거를 구분해서 찾기
        var allCols = GetComponentsInChildren<Collider2D>();
        foreach (var col in allCols)
        {
            if (col.isTrigger) exitTrigger = col;
            else barrierCollider = col;
        }

        if (exitTrigger == null) Debug.LogError("ExitTrigger 콜라이더가 없습니다!");
        if (barrierCollider == null) Debug.LogError("BarrierCollider 콜라이더가 없습니다!");

        if (Shared.ObjectGaugeManager != null)
            Shared.ObjectGaugeManager.onThresholdReached.AddListener(Open);

        Close();

        ObjectGaugeManager og = Shared.ObjectGaugeManager;
        if (og != null && og.GetGaugePercent() >= og.thresholdPercent)
            Open();
    }

    void Update()
    {
        if (!isOpen || !playerInRange || hasLoaded) return;

        if (Input.GetKeyDown(surveyKey))
        {
            LoadNextMap();
            Debug.Log("[Barrier] 퀴즈맵으로 이동함");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isOpen || hasLoaded) return;
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            onShowPrompt?.Invoke();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!isOpen || hasLoaded) return;
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            onHidePrompt?.Invoke();
        }
    }

    public void Open()
    {
        isOpen = true;
        barrierCollider.enabled = false;
        Debug.Log("[Barrier] 열림 상태");
    }

    public void Close()
    {
        isOpen = false;
        barrierCollider.enabled = true;
    }

    // 다음 맵으로 이동
    private void LoadNextMap()
    {
        if (Shared.MapToggleManager == null)
        {
            Debug.LogError("[Barrier] MapToggleManager가 설정되지 않았습니다!");
            return;
        }

        hasLoaded = true;
        onHidePrompt?.Invoke();

        Shared.PuzzleManager?.ClearMaps();
        Shared.MapToggleManager.currentStage = stageIndex;

        //StartCoroutine(Shared.SceneTransitionManager.RunWithFade(() =>
        //{
        //    // 페이드 아웃이 완료된 정확한 타이밍에 전환 실행
           
        //}));
        Shared.MapToggleManager.EnterQuizMap();
    }
}
