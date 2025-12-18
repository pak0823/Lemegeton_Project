using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BoxInteract : MonoBehaviour, IExplorationPersistable
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

    [Header("확률 설명 (상자 열 때)")]
    public WeightedDescriptionsSO openDescriptions;

    [Header("열린 후 처리")]
    [SerializeField] private bool removeOnOpen = true; // true면 열고 나서 상자를 화면/충돌에서 제거
    private float removeOnOpenDelay = 1.5f; // 애니메이션 보여줄 시간 (초)
    private int _pendingInspectCost = -1; // 상자 비용을 "사라질 때" 결제하기 위한 예약값
    private string _pendingOpenLogText = null;

    // 인식된 상자 확인
    private bool isFocused = false;

    [Header("Animator Restore")]
    [SerializeField] private string openedStateName = "Box_Open"; // 또는 "Opened"/"OpenIdle" 등 프로젝트 상태명
    [SerializeField] private int openedLayer = 0;             // 기본 레이어
    [SerializeField] private bool jumpToOpenedPoseOnRestore = true;

    // === 외부 확인용 프로퍼티 ===
    public bool IsOpened => isOpened;

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

        // 시작 시 안내 UI OFF
        //if (targetMarker != null) targetMarker.SetActive(false);
    }

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        //if (targetMarker != null)
        //    targetMarker.SetActive(false); // 시작시 꺼두기

        // 복귀 직후 Animator 초기 상태가 덮어써도 여기서 다시 강제로 맞춤
        if (isOpened || _applyOpenOnStart)
        {
            _applyOpenOnStart = false;
            ForceOpenedVisual();
        }
    }

    void Update(){}
    // 외부에서 포커스 지정
    public void SetFocused(bool on)
    {
        if (isFocused == on) return;
        isFocused = on;
    }

    // PushObject와 동일 패턴의 하이라이트 메서드
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

    // 실제 열기 동작 함수
    public void OpenChest()
    {
        if (isOpened) return;

        // 상자 처리 중에는 플레이어 입력 완전 차단
        Shared.PlayerMovement?.LockMovementIndefinite();

        // 활기 소모 상자 조사/개봉 비용 ===
        var vigor = Shared.VigorManager;
        if (vigor != null)
        {
            int cost = Mathf.Max(0, vigor.costInspectBox);
            if (cost > 0 && !vigor.CanSpend(cost))
            {
                Shared.explorationLogUI?.Push($"활기가 부족합니다. (상자 조사 / 필요 {cost}, 현재 {vigor.CurrentVigor})");
                return;
            }

            _pendingInspectCost = cost;
        }
        else
        {
            _pendingInspectCost = 0;
        }

        isOpened = true;
        animator.SetBool("IsOpen", isOpened);

        int idx = openDescriptions ? openDescriptions.PickIndex() : -1;
        if (idx >= 0 && idx < openDescriptions.entries.Length)
        {
            switch (idx)
            {
                case 0: /* 40% 케이스 로직 */ break;
                case 1: /* 30% 케이스 로직 */ break;
                case 2:  break;
                case 3: /* 10% 케이스 로직 */ break;
                default: /* 예외 처리(프리셋이 더 길어질 수도) */ break;
            }

            // 문구 출력
            var text = openDescriptions.entries[idx].text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                _pendingOpenLogText = text; // 사라질 때 출력
            }

        }

        var descriptiondata = GetComponent<DescriptionData>();
        if (descriptiondata) descriptiondata.ApplyOpenedTextIfAny();

        // 열린 뒤에는 포커스/하이라이트/안내 UI 정리
        SetHighlight(false);;
        isFocused = false;
        // 열린 뒤 잠깐 애니메이션을 보여준 다음 제거/비활성 처리
        if (removeOnOpen)
        {
            StartCoroutine(Co_DelayedPostOpen());
        }
    }

    // 상자가 열린 뒤 후처리
    // removeOnOpen == true 이면, 화면/충돌에서 제거 (타일 통행 가능)
    private void ApplyPostOpenBehavior()
    {
        if (!removeOnOpen)
            return;

        // 모든 콜라이더 비활성화 → impassableLayerMask 충돌 제거 + 클릭 불가
        foreach (var col in GetComponents<Collider2D>())
        {
            if (col) col.enabled = false;
        }

        // 모든 SpriteRenderer 비활성화 → 시각적으로 완전히 사라짐
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

    // 딜레이 후 오브잭트 제거
    // 애니메이션이 끝난 후 제거가 되게 하기 위해
    private IEnumerator Co_DelayedPostOpen()
    {
        if (removeOnOpenDelay > 0f)
            yield return new WaitForSeconds(removeOnOpenDelay);

        // 사라지는 순간 활기 소모
        var vigor = Shared.VigorManager;
        if (vigor != null && _pendingInspectCost > 0)
        {
            if (!vigor.TrySpend(_pendingInspectCost, VigorSpendReason.InspectBox))
            {
                vigor.FailExploration($"탐색을 실패했습니다. (상자 결제 실패 / 필요 {_pendingInspectCost}, 현재 {vigor.CurrentVigor})");
                yield break;
            }
        }

        if (!string.IsNullOrWhiteSpace(_pendingOpenLogText))
        {
            Shared.explorationLogUI?.Push(_pendingOpenLogText);
            //Shared.interactionHintUI?.HideAll();
            _pendingOpenLogText = null;
        }

        _pendingInspectCost = -1;

        ApplyPostOpenBehavior(); // 상자 제거
                                 
        Shared.PlayerMovement?.UnlockMovementIndefinite();// 입력 해제
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
                //if (targetMarker != null) targetMarker.SetActive(false);
                isFocused = false;
            }
        }
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
        animator.SetBool("IsOpened", true);

        if (jumpToOpenedPoseOnRestore)
        {
            // 1) 상태 이름으로 바로 점프: 트랜지션/크로스페이드 없이 "그 상태의 마지막 포즈"로 고정
            if (!string.IsNullOrEmpty(openedStateName))
            {
                int hash = Animator.StringToHash(openedStateName);
                if (animator.HasState(openedLayer, hash))
                {
                    // Play(..., normalizedTime=1f) → 마지막 프레임로 스냅
                    animator.Play(hash, openedLayer, 1f);
                }
                else
                {
                    // 폴백: 현재 컨트롤러에 "Open"이 없다면 그냥 bool만 맞추고 넘어감
                }
            }
            else
            {
                // 이름 미지정 폴백: bool만 맞추기
            }
        }

        // 즉시 평가(한 프레임 대기 없이 포즈 적용)
        animator.Update(0f);

        // 포커스/UI도 열린 상태에 맞춰 정리
        //if (targetMarker) targetMarker.SetActive(false);
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
