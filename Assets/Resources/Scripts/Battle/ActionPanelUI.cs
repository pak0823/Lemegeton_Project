using UnityEngine;

public class ActionPanelUI : MonoBehaviour
{
    [Header("Refs")]
    public BattleManager battle;     // BattleManager 할당
    public GameObject actionPanel;   // Move/Attack/EndTurn 패널 GO

    // 내부 상태
    bool _skillPanelOpen;
    bool _lastIsPlayerTurn;

    void Awake()
    {
        if (battle == null)
            battle = FindObjectOfType<BattleManager>();

        // 이벤트 구독: 스킬 패널 열림/닫힘 알림
        if (battle != null)
        {
            battle.OnSkillPanelToggled += HandleSkillPanelToggled;
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

        // 혹시 다른 코드에서 isSelectingSkill을 직접 변경하는 경우를 커버(안전망)
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
        // 1) 플레이어 턴일 때만 ActionPanel 활성
        // 2) 스킬 패널이 열려 있으면 ActionPanel 비활성
        bool active = _lastIsPlayerTurn && !_skillPanelOpen;

        if (actionPanel != null && actionPanel.activeSelf != active)
            actionPanel.SetActive(active);
    }
}
