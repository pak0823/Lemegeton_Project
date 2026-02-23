using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BoxInteract : MonoBehaviour, IInteractable, IExplorationPersistable
{
    private ExplorationPersistId pid;
    public Animator animator;
    public bool isPlayerNear = false;
    [SerializeField] private bool isOpened = false;
    private bool _applyOpenOnStart = false;  // 복원 시 다음 프레임에 반영

    // === 하이라이트 필드 ===
    [Header("하이라이트 처리")]
    public SpriteRenderer highlightRenderer;   // 지정 없으면 GetComponent로 자동 할당
    private Color originalColor;
    private bool isHighlighted = false;

    [Header("보상 설정")]
    public ItemLibrary itemLibrary; // 인스펙터에서 할당 필요

    [Header("열린 후 처리")]
    [SerializeField] private bool removeOnOpen = true; // true면 열고 나서 상자를 화면/충돌에서 제거
    private const float REMOVE_ON_OPEN_DELAY = 1.5f; // 애니메이션 보여줄 시간 (초)
    private const string ANIM_PARAM_IS_OPEN = "IsOpen";
    private const string ANIM_PARAM_IS_OPENED = "IsOpened"; // 복원 시 사용되는 파라미터 (일관성 유지 위해 별도 상수)




    // 인식된 상자 확인
    private bool isFocused = false;

    [Header("Animator Restore")]
    [SerializeField] private string openedStateName = "Box_Open"; // 또는 "Opened"/"OpenIdle" 등 프로젝트 상태명
    [SerializeField] private int openedLayer = 0;             // 기본 레이어
    [SerializeField] private bool jumpToOpenedPoseOnRestore = true;

    // === 외부 확인용 프로퍼티 ===
    public bool IsOpened => isOpened;

    private Vector3Int _currentOccupiedCell;

    private void RegisterObstacle()
    {
        if (!isOpened && PathfindingSystem.Instance != null)
        {
            _currentOccupiedCell = PathfindingSystem.Instance.GetCellFromWorldPos(transform.position);
            PathfindingSystem.Instance.RegisterObstacle(_currentOccupiedCell);
        }
    }

    private void UnregisterObstacle()
    {
        if (PathfindingSystem.Instance != null)
        {
            PathfindingSystem.Instance.UnregisterObstacle(_currentOccupiedCell);
        }
    }

    private void OnEnable()
    {
        if (isOpened)
            ForceOpenedVisual();
    }

    private void Awake()
    {
        pid = GetComponent<ExplorationPersistId>();
        if (!pid) pid = gameObject.AddComponent<ExplorationPersistId>();

        // SpriteRenderer 캐시 및 원본 색상 저장
        if (highlightRenderer == null)
            highlightRenderer = GetComponent<SpriteRenderer>();
        if (highlightRenderer != null)
            originalColor = highlightRenderer.color;
    }

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        RegisterObstacle();

        // 데이터 무결성 체크
        if (rewardTable == null)
        {
            Debug.LogError($"[BoxInteract] '{gameObject.name}'의 RewardTable이 할당되지 않았습니다! (Position: {transform.position}) 프리팹 설정을 확인하세요.");
        }

        // 복귀 직후 Animator 초기 상태가 덮어써도 여기서 다시 강제로 맞춤
        if (isOpened || _applyOpenOnStart)
        {
            _applyOpenOnStart = false;
            ForceOpenedVisual();
        }
    }

    void Update() { }
    // 외부에서 포커스 지정
    public void SetFocused(bool on)
    {
        if (isFocused == on) return;
        isFocused = on;
    }

    // IInteractable 구현
    public void SetHighlight(bool on)
    {
        if (highlightRenderer != null && isHighlighted != on)
        {
            // 열렸으면 굳이 켜지 않도록 방어
            if (on && isOpened) on = false;

            // PushObject는 노란색, 상자는 시각적으로 구분되도록 청록 계열 예시
            highlightRenderer.color = on ? new Color(0.4f, 1f, 1f, 1f) : originalColor;
            isHighlighted = on;
        }
    }

    public void OnInteract()
    {
        OpenChest();
    }

    public Transform GetTransform()
    {
        return transform;
    }

    public bool CanInteract => !isOpened;
    public string GetInteractLabel() => "조사";

    [Header("보상 설정")]
    public RewardTableSO rewardTable;

    // 이벤트에 의해 쌓인 로그들을 저장할 버퍼
    private List<string> _pendingRewardLogs = new List<string>();

    // 실제 열기 동작 함수
    public void OpenChest()
    {
        if (isOpened) return;

        // 활기 소모 먼저 체크 및 실행 (즉시 차감)
        var vigor = VigorManager.Instance;
        if (vigor != null)
        {
            int cost = Mathf.Max(0, vigor.costInspectBox);
            if (cost > 0)
            {
                // 즉시 차감 시도
                if (!vigor.TrySpend(cost, VigorSpendReason.InspectBox))
                {
                    ExplorationLogUI.Instance?.Push($"활기가 부족합니다. (상자 조사 / 필요 {cost}, 현재 {vigor.CurrentVigor})", pause: false);
                    return; // 활기 부족 시 열기 중단
                }
            }
        }

        // 상자 처리 중에는 플레이어 입력 완전 차단
        PlayerMovement.Instance?.LockMovementIndefinite();

        isOpened = true;
        animator.SetBool(ANIM_PARAM_IS_OPEN, isOpened);

        // 1. 로그 버퍼 초기화
        _pendingRewardLogs.Clear();

        // 2. 보상 지급 (매니저 위임) -> 지연 지급으로 변경됨 (Co_DelayedPostOpen)
        // if (InventoryManager.Instance != null && rewardTable != null)
        // {
        //     var logs = InventoryManager.Instance.GiveReward(rewardTable);
        //     if (logs != null) _pendingRewardLogs.AddRange(logs);
        // }

        // 3. 설명 텍스트 처리 및 이벤트 실행 (이제 RewardTableSO에서 통합 관리되므로 별도 호출 불필요)
        // 기존 WeightedDescriptionsSO 로직 제거됨.

        var descriptiondata = GetComponent<DescriptionData>();
        if (descriptiondata) descriptiondata.ApplyOpenedTextIfAny();

        // 열린 뒤에는 포커스/하이라이트/안내 UI 정리
        SetHighlight(false);
        isFocused = false;

        // 열린 뒤 잠깐 애니메이션을 보여준 다음 로그 출력 및 제거
        StartCoroutine(Co_DelayedPostOpen());
    }



    // 상자가 열린 뒤 후처리
    private void ApplyPostOpenBehavior()
    {
        UnregisterObstacle();

        if (!removeOnOpen)
            return;

        // 모든 콜라이더 비활성화
        foreach (var col in GetComponents<Collider2D>())
        {
            if (col) col.enabled = false;
        }

        // 모든 SpriteRenderer 비활성화
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr) sr.enabled = false;
        }

        // 필요하다면 Animator도 더 이상 돌지 않게 막을 수 있음
        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    // 딜레이 후 로그 출력 및 오브젝트 제거
    private IEnumerator Co_DelayedPostOpen()
    {
        if (REMOVE_ON_OPEN_DELAY > 0f)
            yield return new WaitForSeconds(REMOVE_ON_OPEN_DELAY);

        // 버퍼에 쌓인 보상 로그 출력 (pause: false) -> 이제 여기서 보상도 같이 지급
        if (InventoryManager.Instance != null && rewardTable != null)
        {
            var logs = InventoryManager.Instance.GiveReward(rewardTable);
            if (logs != null)
            {
                foreach (var log in logs)
                {
                    ExplorationLogUI.Instance?.Push(log, pause: false);
                }
            }
        }

        if (removeOnOpen)
        {
            ApplyPostOpenBehavior(); // 상자 제거
        }

        PlayerMovement.Instance?.UnlockMovementIndefinite();// 입력 해제
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;

            // 안전하게 안내 UI/하이라이트 꺼줌
            if (!isOpened)
            {
                SetHighlight(false);
                isFocused = false;
            }
        }
    }

    private void OnDestroy()
    {
        UnregisterObstacle();
    }

    // 복원용: 즉시 열린 상태로 세팅
    public void OpenImmediately()
    {
        isOpened = true;
        ForceOpenedVisual();

        // 복원/즉시 열기 시에도 같은 처리
        var descriptiondata = GetComponent<DescriptionData>();
        if (descriptiondata) descriptiondata.ApplyOpenedTextIfAny();

        // 복원 시에도 "이미 열린 상자"는 제거/비활성 처리
        ApplyPostOpenBehavior();
    }

    private void ForceOpenedVisual()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) return;

        // 파라미터
        animator.SetBool(ANIM_PARAM_IS_OPENED, true);

        if (jumpToOpenedPoseOnRestore)
        {
            // 1) 상태 이름으로 바로 점프
            if (!string.IsNullOrEmpty(openedStateName))
            {
                int hash = Animator.StringToHash(openedStateName);
                if (animator.HasState(openedLayer, hash))
                {
                    animator.Play(hash, openedLayer, 1f);
                }
            }
        }

        // 즉시 평가
        animator.Update(0f);
    }

    // IExplorationPersistable
    public string PersistID => pid.Id;
    public ExplorationObjectState SaveState()
    {
        return new ExplorationObjectState
        {
            id = PersistID,
            kind = "Chest",
            prefabName = gameObject.name.Replace("(Clone)", "").Trim(),
            position = transform.position,
            b1 = isOpened
        };
    }

    public void LoadState(ExplorationObjectState s)
    {
        transform.position = s.position;
        isOpened = s.b1;
        if (isOpened)
        {
            // 즉시 반영 + 다음 프레임에도 한 번 더 보정
            OpenImmediately();
            _applyOpenOnStart = true;
        }
    }

}
