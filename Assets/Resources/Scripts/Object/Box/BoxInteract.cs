using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BoxInteract : MonoBehaviour, IExplorationPersistable
{
    private ExplorationPersistId pid;
    public Animator animator;
    public GameObject targetMarker;   //인식 표시
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

    // 인식된 상자 확인
    private bool isFocused = false;

    // === 외부 확인용 프로퍼티 ===
    public bool IsOpened => isOpened;

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
        if (targetMarker != null) targetMarker.SetActive(false);
    }

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (targetMarker != null)
            targetMarker.SetActive(false); // 시작시 꺼두기

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

        // 포커스 기준으로만 안내 UI 제어
        if (targetMarker != null)
            targetMarker.SetActive(isFocused && !isOpened);
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
        isOpened = true;
        animator.SetBool("IsOpen", isOpened);
        Shared.ObjectGaugeManager.IncrementChest();

        int idx = openDescriptions ? openDescriptions.PickIndex() : -1;
        if (idx >= 0 && idx < openDescriptions.entries.Length)
        {
            switch (idx)
            {
                case 0: /* 40% 케이스 로직 */ break;
                case 1: /* 30% 케이스 로직 */ break;
                case 2: Shared.ObjectGaugeManager.TryIncrementAwarenessByChest(); break;    //20% 확률로 인지 게이지 증가
                case 3: /* 10% 케이스 로직 */ break;
                default: /* 예외 처리(프리셋이 더 길어질 수도) */ break;
            }

            // 문구도 띄우고 싶다면
            var text = openDescriptions.entries[idx].text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                Shared.descriptionDialogUI?.ShowTemporarily(text, 1.0f);    // 1초 잠금+유지
                Shared.interactionHintUI?.HideAll();
            }
                
        }

        // 열린 뒤에는 포커스/하이라이트/안내 UI 정리
        SetHighlight(false);
        if (targetMarker != null) targetMarker.SetActive(false);
        isFocused = false;
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
                if (targetMarker != null) targetMarker.SetActive(false);
                isFocused = false;
            }
        }
    }

    // 복원용: 즉시 열린 상태로 세팅
    public void OpenImmediately()
    {
        isOpened = true;
        ForceOpenedVisual();
        // 하이라이트/포커스 끄기 등 부가 처리 필요 시 추가
    }

    private void ForceOpenedVisual()
    {
        // 애니메이터가 비어있으면 즉시 캐싱
        if (animator == null) animator = GetComponent<Animator>();
        if (animator != null)
        {
            //TrySetAnimatorBool(animator, "IsOpen", isOpened);
            animator.SetBool("IsOpen", isOpened);
            animator.Update(0f); // 한 프레임 강제 평가로 시각 확정
        }
        if (targetMarker) targetMarker.SetActive(false);
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
