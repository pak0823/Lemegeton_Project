using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ActionPanelUI : MonoBehaviour
{
    [Header("Refs")]
    public BattleManager battle;     // BattleManager 할당
    public GameObject actionPanel;   // Move/Attack/EndTurn 패널 GO

    [Header("Behavior")]
    [SerializeField] bool disableInteractWhenSkillOpen = true; // 스킬창 열리면 입력만 차단

    // 내부 상태
    bool _skillPanelOpen;
    bool _lastIsPlayerTurn;

    CanvasGroup _cg;

    void Awake()
    {
        if (battle == null)
            battle = FindObjectOfType<BattleManager>();

        // 이벤트 구독: 스킬 패널 열림/닫힘 알림
        if (battle != null)
        {
            battle.OnSkillPanelToggled += HandleSkillPanelToggled;
        }

        // CanvasGroup 확보(없으면 붙임)
        if (actionPanel != null)
        {
            _cg = actionPanel.GetComponent<CanvasGroup>();
            if (_cg == null) _cg = actionPanel.AddComponent<CanvasGroup>();
            _cg.alpha = 1f; // 항상 보이게
        }

        // 초기 표시: 플레이어 턴이면 켜고, 아니면 끄기
        _lastIsPlayerTurn = (battle != null && battle.IsPlayerTurn);
        ApplyVisibility();
    }

    void OnDestroy()
    {
        if (battle != null)
            battle.OnSkillPanelToggled -= HandleSkillPanelToggled;
    }

    void Update()
    {
        // 턴 소유권 변경 감지(플레이어 턴 ↔ 적 턴)
        bool nowPlayerTurn = (battle != null && battle.IsPlayerTurn);
        if (nowPlayerTurn != _lastIsPlayerTurn)
        {
            _lastIsPlayerTurn = nowPlayerTurn;
            ApplyVisibility();
        }

        // 안전망: isSelectingSkill 변화 추적
        if (battle != null && _skillPanelOpen != battle.isSelectingSkill)
        {
            _skillPanelOpen = battle.isSelectingSkill;
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
        // 규칙:
        // 1) 플레이어 턴이면 ActionPanel "보임(Active=true)"을 유지
        // 2) 스킬 패널이 열려 있으면 ActionPanel의 "입력만 차단"(보이는 건 그대로)
        bool visible = _lastIsPlayerTurn;

        if (actionPanel != null && actionPanel.activeSelf != visible)
            actionPanel.SetActive(visible);

        if (_cg != null)
        {
            if (disableInteractWhenSkillOpen && _skillPanelOpen)
            {
                _cg.interactable = false;     // 버튼 클릭/포커스 차단
                _cg.blocksRaycasts = false;   // 레이캐스트 차단(스킬 패널이 위에서 이벤트 받도록)
                _cg.alpha = 1f;               // 시각적으로는 유지
            }
            else
            {
                _cg.interactable = true;
                _cg.blocksRaycasts = true;
                _cg.alpha = 1f;
            }
        }
        else
        {
            // 혹시 CanvasGroup을 못 쓸 상황이면 버튼 인터랙터블만 토글(차선책)
            if (actionPanel != null)
            {
                bool enable = !(disableInteractWhenSkillOpen && _skillPanelOpen);
                foreach (var b in actionPanel.GetComponentsInChildren<Button>(true))
                    b.interactable = enable;
            }
        }
    }
}
