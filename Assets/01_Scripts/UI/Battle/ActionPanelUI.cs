using UnityEngine;
using UnityEngine.UI;

public class ActionPanelUI : MonoBehaviour
{
    [Header("Refs")]
    public BattleManager battleManager;     // BattleManager 할당
    public GameObject actionPanel;   // Move/Attack/EndTurn 패널 GO

    [Header("Behavior")]
    [SerializeField] bool disableInteractWhenSkillOpen = true; // 스킬창 열리면 입력만 차단

    // 내부 상태
    bool _skillPanelOpen;
    bool _lastIsPlayerTurn;

    CanvasGroup _canvasGroup;

    void Awake()
    {
        // CanvasGroup 확보(없으면 붙임)
        if (actionPanel != null)
        {
            _canvasGroup = actionPanel.GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = actionPanel.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f; // 항상 보이게
        }

        // 초기 표시: 플레이어 턴이면 켜고, 아니면 끄기
        _lastIsPlayerTurn = (battleManager != null && battleManager.IsPlayerTurn);
        ApplyVisibility();
    }

    void Start()
    {
        if (battleManager == null)
            battleManager = BattleManager.Instance;

        // 스킬 패널 열림/닫힘 알림
        if (battleManager != null)
        {
            battleManager.OnSkillPanelToggled += HandleSkillPanelToggled;
        }
    }

    void OnDestroy()
    {
        if (battleManager != null)
            battleManager.OnSkillPanelToggled -= HandleSkillPanelToggled;
    }

    void Update()
    {
        // 턴 소유권 변경 감지(플레이어 턴 ↔ 적 턴)
        bool nowPlayerTurn = (battleManager != null && battleManager.IsPlayerTurn);
        if (nowPlayerTurn != _lastIsPlayerTurn)
        {
            _lastIsPlayerTurn = nowPlayerTurn;
            ApplyVisibility();
        }

        // 안전망: isSelectingSkill 변화 추적
        if (battleManager != null && _skillPanelOpen != battleManager.isSelectingSkill)
        {
            _skillPanelOpen = battleManager.isSelectingSkill;
            ApplyVisibility();
        }

        // [디버깅용] 상태가 바뀔 때만 로그 출력
        if (nowPlayerTurn != _lastIsPlayerTurn)
        {
            Debug.Log($"[UI] 턴 상태 변경 감지! 플레이어 턴인가요? {nowPlayerTurn}"); // <--- 이 로그 확인!

            _lastIsPlayerTurn = nowPlayerTurn;
            ApplyVisibility();
        }
    }

    void HandleSkillPanelToggled(bool open)
    {
        _skillPanelOpen = open;
        ApplyVisibility();
    }

    void ApplyVisibility()
    {
        // 현재 내 턴인지 확인
        bool isPlayerTurn = _lastIsPlayerTurn;

        // 대신 CanvasGroup으로 투명도 조절
        if (_canvasGroup != null)
        {
            if (isPlayerTurn)
            {
                // [내 턴일 때]
                if (disableInteractWhenSkillOpen && _skillPanelOpen)
                {
                    // 스킬창 열림: 보이긴 하되(Alpha 1), 클릭만 막음
                    _canvasGroup.alpha = 1f;
                    _canvasGroup.interactable = false;
                    _canvasGroup.blocksRaycasts = false;
                }
                else
                {
                    // 기본 상태: 보이고 클릭 가능
                    _canvasGroup.alpha = 1f;
                    _canvasGroup.interactable = true;
                    _canvasGroup.blocksRaycasts = true;
                }
            }
            else
            {
                // [적 턴일 때]
                // 끄지 말고(SetActive false 금지), 투명하게만 만듦
                _canvasGroup.alpha = 0f;          // 투명도 0 (완전 투명)
                _canvasGroup.interactable = false; // 클릭 불가
                _canvasGroup.blocksRaycasts = false; // 레이캐스트 차단
            }
        }
        else
        {
            // CanvasGroup이 없는 비상 상황용 (기존 코드 유지하되 주의 필요)
            // 만약 actionPanel이 이 스크립트가 붙은 오브젝트라면 여기서 문제가 생깁니다.
            // 반드시 인스펙터에서 ActionPanel 오브젝트에 CanvasGroup 컴포넌트를 추가해주세요.
            if (actionPanel != null)
                actionPanel.SetActive(isPlayerTurn);
        }
    }
}
