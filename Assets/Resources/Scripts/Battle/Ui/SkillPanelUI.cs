using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class SkillPanelUI : MonoBehaviour
{
    [Header("Refs")]
    public BattleManager battleManager;        // 인스펙터로 할당
    public GameObject panel;            // SkillPanel 오브젝트
    public Button[] buttons;            // 4~5개
    [SerializeField] private Text descriptionText;   // 스킬 설명 표시
    [SerializeField] private Image rangeImage;     // 범위(아이콘 재활용)
    [SerializeField] private Text selectTargetHintText; //행동 텍스트 ui

    private SkillAsset[] cachedAssets;  // 마지막으로 채운 SO 목록 캐시
    private bool[] cachedUsable;

    // 적 라벨이 표기 중인지(패널 토글과 무관하게 유지)
    private bool _pinnedEnemyLabel = false;

    void Awake()
    {
        if (!battleManager)
            battleManager =FindAnyObjectByType<BattleManager>();

        // 이벤트 구독
        if (battleManager != null)
        {
            battleManager.OnSkillPanelToggled += HandleToggle;
            battleManager.OnSkillPanelPopulateSO += HandlePopulateSO;
            battleManager.OnHint += HandleHint;
            battleManager.OnUnitActionLabel += HandleUnitActionLabel;
            battleManager.OnUnitEndTurn += HandleAnyUnitEndTurn_ClearLabel;
            battleManager.OnWaveStarted += HandleWaveStarted_ClearLabel;
            battleManager.OnUnitPassiveLabel += HandleUnitPassiveLabel;
            battleManager.OnUnitTurnLabel += HandleUnitTurnLabel;
        }

        if (panel != null) panel.SetActive(false); // 기본 비활성
        WireTempButtons(); // 초기 onClick 정리
    }

    void OnDestroy()
    {
        if (battleManager != null)
        {
            battleManager.OnSkillPanelToggled -= HandleToggle;
            battleManager.OnSkillPanelPopulateSO -= HandlePopulateSO;
            battleManager.OnHint -= HandleHint;
            battleManager.OnUnitActionLabel -= HandleUnitActionLabel;
            battleManager.OnUnitEndTurn -= HandleAnyUnitEndTurn_ClearLabel;
            battleManager.OnWaveStarted -= HandleWaveStarted_ClearLabel;
            battleManager.OnUnitPassiveLabel -= HandleUnitPassiveLabel;
            battleManager.OnUnitTurnLabel -= HandleUnitTurnLabel;
        }
    }

    void LateUpdate()
    {
        if (panel == null || !panel.activeInHierarchy) return;
        if (buttons == null || cachedUsable == null) return;

        int len = Mathf.Min(buttons.Length, cachedUsable.Length);
        for (int i = 0; i < len; i++)
        {
            var b = buttons[i];
            if (b == null) continue;
            if (!b.gameObject.activeInHierarchy) continue;

            bool should = cachedUsable[i];

            // 다른 어디서 바꿔도 여기서 되돌린다
            if (b.interactable != should)
                b.interactable = should;
        }
    }

    void HandleToggle(bool show)
    {
        if (panel != null) panel.SetActive(show);

        // 패널이 닫혀도, 적 행동 라벨이 고정중이면 힌트 텍스트를 지우지 않음
        if (!show && selectTargetHintText && !_pinnedEnemyLabel)
        {
            selectTargetHintText.text = string.Empty;
            selectTargetHintText.gameObject.SetActive(false);
        }
    }

    void HandlePopulateSO(SkillAsset[] assets)
    {
        if (buttons == null) return;
        cachedAssets = assets; // 캐시
        int n = Mathf.Min(assets?.Length ?? 0, buttons.Length);
        int last = buttons.Length - 1;
        if (cachedUsable == null || cachedUsable.Length != buttons.Length)
            cachedUsable = new bool[buttons.Length];

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null) continue;

            // --- 마지막 버튼은 항상 Cancel로 구성 ---
            if (i == last)
            {
                button.gameObject.SetActive(true);
                button.interactable = true;
                if (cachedUsable != null && i < cachedUsable.Length)
                    cachedUsable[i] = true;
                // 클릭 → 취소 이벤트 발생 (인스펙터에서 배틀 취소 함수 연결)
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    // 상세를 비우고 패널 상태는 유지/닫기는 배틀 로직에 맡김
                    UpdateDetail(null, forceClear: true, showCancelDesc: true);
                });

                // 호버/선택 시 상세를 비우기
                var et = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
                ClearTriggers(et);
                AddTrigger(et, EventTriggerType.PointerEnter, () => UpdateDetail(null, forceClear: true, showCancelDesc: true));
                AddTrigger(et, EventTriggerType.Select, () => UpdateDetail(null, forceClear: true, showCancelDesc: true));
                continue;
            }

            // --- 스킬 버튼 구성 ---
            if (i < n)
            {
                button.gameObject.SetActive(true);
                SkillAsset asset = assets[i];

                Text txt = button.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    string label = (asset != null ? asset.displayName : "(empty)");

                    // 남은 cooldown 표시
                    int cooldown = 0;
                    BattleUnit actor = battleManager != null ? battleManager.GetType().GetField("acting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(battleManager) as BattleUnit : null;
                    if (actor != null && asset != null) 
                        cooldown = actor.GetCooldownRemaining(asset);

                    txt.text = (cooldown > 0) ? $"{label} (CD:{cooldown})" : label;
                }

                // 쿨다운 스킬 버튼 잠금
                bool usable = (battleManager != null && battleManager.IsPlayerTurn && battleManager.currentSkillSO == null);
                if (usable && asset != null)
                {
                    BattleUnit actor = Shared.BattleManager != null ? Shared.BattleManager.GetType()
                        .GetField("acting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(Shared.BattleManager) as BattleUnit : null;

                    if (actor != null)
                        usable = !actor.IsSkillOnCooldown(asset) && actor.HasMP(asset.mpCost);
                }

                button.interactable = usable;
                cachedUsable[i] = usable;

                button.onClick.RemoveAllListeners();
                int capture = i;
                button.onClick.AddListener(() =>
                {
                    Debug.Log($"[SkillButton] click index={capture}, asset={asset?.name}");


                    // 하이라이트를 먼저 현재 버튼으로 고정
                    UIArrowNavigator navigator = (panel != null ? panel.GetComponent<UIArrowNavigator>() : null)
                                ?? GetComponentInChildren<UIArrowNavigator>(true);
                    navigator?.RebuildAndRefocus(); // 비활성 버튼 건너뛰도록 갱신

                    if (navigator != null) navigator.SelectIndexImmediate(capture, focus: false, updateHighlight: true);

                    // EventSystem에도 선택으로 알려주고 싶다면
                    if (EventSystem.current != null)
                        EventSystem.current.SetSelectedGameObject(button.gameObject);

                    // 상세 패널 즉시 갱신
                    UpdateDetail(asset);

                    // 기존 배틀 호출(이 시점에서 Targeting 진입 → 잠금이 걸려도 OK)
                    battleManager.SelectSkill(capture);
                });

                // 호버/선택 시에도 상세 갱신 (마우스/패드 모두 커버)
                EventTrigger eventTrigger = button.GetComponent<EventTrigger>();
                if (eventTrigger == null) eventTrigger = button.gameObject.AddComponent<EventTrigger>();
                AddTrigger(eventTrigger, EventTriggerType.PointerEnter, () => UpdateDetail(asset));
                AddTrigger(eventTrigger, EventTriggerType.Select, () => UpdateDetail(asset));
            }
            else
            {
                button.gameObject.SetActive(false);
                if (cachedUsable != null && i < cachedUsable.Length)
                    cachedUsable[i] = false;
            }
        }

        // 첫 항목으로 상세 초기화
        if (n > 0 && assets[0] != null) UpdateDetail(assets[0]);
        else UpdateDetail(null);

        var navigator = (panel != null ? panel.GetComponent<UIArrowNavigator>()
                               : GetComponentInChildren<UIArrowNavigator>(true));
        navigator?.RebuildAndRefocus();
    }

    // 초기에 불필요한 리스너 삭제
    void WireTempButtons()
    {
        if (buttons == null) return;
        foreach (var b in buttons)
            if (b != null) b.onClick.RemoveAllListeners();
    }

    // === 상세 갱신 ===
    void UpdateDetail(SkillAsset _skillasset, bool forceClear = false, bool showCancelDesc = false)
    {
        // 타겟팅 중에는 설명을 바꾸지 않는다. 단, forceClear(true)인 경우만 예외적으로 허용
        if (battleManager != null && battleManager.IsTargeting)
            return;

        // Cancel 전용 설명
        if (showCancelDesc)
        {
            if (descriptionText) descriptionText.text = "선택을 취소합니다.";
            if (rangeImage)
            {
                rangeImage.sprite = null;
                rangeImage.enabled = false;
            }
            return;
        }

        // Cancel 버튼 혹은 forceClear=true 면 항상 비우기
        bool clear = forceClear || _skillasset == null;

        if (descriptionText)
        {
            if (_skillasset == null)
            {
                descriptionText.text = "";
            }
            else
            {
                BattleUnit actor = null;

                if (battleManager != null)
                {
                    actor = battleManager.ActingUnit;
                }

                if (actor != null)
                    descriptionText.text = _skillasset.GetFullDescriptionRich(actor);
                else
                    descriptionText.text = _skillasset.GetFullDescriptionRich();
            }
        }

        if (rangeImage)
        {
            // rangeSprite를 따로 둘 계획이면: a.rangeSprite ?? a.descriptionImage
            rangeImage.sprite = _skillasset != null ? _skillasset.descriptionImage : null;
            rangeImage.enabled = (rangeImage.sprite != null);
        }
        else
        {
            rangeImage.sprite = _skillasset.descriptionImage;
            rangeImage.enabled = true;
        }
    }

    // === EventTrigger 유틸 ===
    void AddTrigger(EventTrigger _eventtrigger, EventTriggerType type, System.Action _callback)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => _callback?.Invoke());
        _eventtrigger.triggers.RemoveAll(e => e.eventID == type && e.callback == null); // 청소(옵션)
        _eventtrigger.triggers.Add(entry);
    }
    void ClearTriggers(EventTrigger _eventtrigger)
    {
        if (_eventtrigger == null) return;
        _eventtrigger.triggers.Clear();
    }

    void HandleHint(string msg)
    {
        if (!selectTargetHintText) return;

        bool show = !string.IsNullOrEmpty(msg);
        if (show)
            _pinnedEnemyLabel = false;

        selectTargetHintText.text = show ? msg : string.Empty;
        selectTargetHintText.gameObject.SetActive(show);
    }
    void HandleUnitActionLabel(BattleUnit unit, string label)
    {
        if (!selectTargetHintText) return;

        // 빈 라벨이면 고정 해제 & 숨김
        if (string.IsNullOrEmpty(label))
        {
            _pinnedEnemyLabel = false;
            selectTargetHintText.text = string.Empty;
            selectTargetHintText.gameObject.SetActive(false);
            return;
        }

        // 유닛 표시 이름(없으면 GameObject 이름 fallback)
        string unitName = unit != null && !string.IsNullOrEmpty(unit.name) ? unit.name : "유닛";

        // 보기 좋게 색만 살짝(원하면 제거 가능)
        selectTargetHintText.text = $"<color=#FF5555>{unitName}</color> 의 {label}";
        selectTargetHintText.gameObject.SetActive(true);

        // 패널 열림/닫힘과 무관하게 유지
        _pinnedEnemyLabel = true;
    }
    void HandleUnitPassiveLabel(BattleUnit unit, string label)
    {
        if (!selectTargetHintText) return;

        if (string.IsNullOrEmpty(label))
        {
            // 패시브 라벨 클리어
            _pinnedEnemyLabel = false;
            selectTargetHintText.text = string.Empty;
            selectTargetHintText.gameObject.SetActive(false);
            return;
        }

        string unitName = unit != null && !string.IsNullOrEmpty(unit.name) ? unit.name : "유닛";
        // 패시브는 색상만 다르게(예: 노랑)
        selectTargetHintText.text = $"<color=#F2C94C>{unitName}</color> 의 <b>{label}</b>";
        selectTargetHintText.gameObject.SetActive(true);

        _pinnedEnemyLabel = true; // 패시브 표시 중에는 힌트/패널 토글에 영향받지 않게 고정
    }
    void HandleAnyUnitEndTurn_ClearLabel(BattleUnit u)
    {
        // 적/아군 관계없이, UI 라벨은 이 타이밍에 정리
        _pinnedEnemyLabel = false;
        if (selectTargetHintText)
        {
            selectTargetHintText.text = string.Empty;
            selectTargetHintText.gameObject.SetActive(false);
        }
    }
    void HandleWaveStarted_ClearLabel()
    {
        _pinnedEnemyLabel = false;
        if (selectTargetHintText)
        {
            selectTargetHintText.text = string.Empty;
            selectTargetHintText.gameObject.SetActive(false);
        }
    }
    void HandleUnitTurnLabel(BattleUnit unit)
    {
        if (!selectTargetHintText) return;

        if (unit == null)
        {
            // 혹시 null로 클리어 신호를 보낼 일이 생기면 여기서도 처리 가능
            _pinnedEnemyLabel = false;
            selectTargetHintText.text = string.Empty;
            selectTargetHintText.gameObject.SetActive(false);
            return;
        }

        string unitName = !string.IsNullOrEmpty(unit.name) ? unit.name : "유닛";

        // 턴 시작 배너 텍스트
        selectTargetHintText.text = $"<color=#FFFFFF>{unitName}</color> 의 턴";

        selectTargetHintText.gameObject.SetActive(true);

        // 이 라벨도 "고정 라벨" 취급해서, 패널 on/off와 무관하게 유지
        // (턴이 끝나면 HandleAnyUnitEndTurn_ClearLabel에서 지워짐)
        _pinnedEnemyLabel = true;
    }
}
