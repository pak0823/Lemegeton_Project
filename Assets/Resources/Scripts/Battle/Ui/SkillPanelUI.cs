using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class SkillPanelUI : MonoBehaviour
{
    [Header("Refs")]
    public BattleManager battle;        // 인스펙터로 할당
    public GameObject panel;            // SkillPanel 오브젝트
    public Button[] buttons;            // 4~5개
    [SerializeField] private Text descriptionText;   // 스킬 설명 표시
    [SerializeField] private Image rangeImage;     // 범위(아이콘 재활용)
    [SerializeField] private Text selectTargetHintText; //행동 텍스트 ui

    private SkillAsset[] cachedAssets;  // 마지막으로 채운 SO 목록 캐시

    // 적 라벨이 표기 중인지(패널 토글과 무관하게 유지)
    private bool _pinnedEnemyLabel = false;

    void Awake()
    {
        if (battle == null)
            battle = FindObjectOfType<BattleManager>();

        // 이벤트 구독
        if (battle != null)
        {
            battle.OnSkillPanelToggled += HandleToggle;
            battle.OnSkillPanelPopulateSO += HandlePopulateSO;
            battle.OnHint += HandleHint;
            battle.OnUnitActionLabel += HandleUnitActionLabel;
        }

        if (panel != null) panel.SetActive(false); // 기본 비활성
        WireTempButtons(); // 초기 onClick 정리
    }

    void OnDestroy()
    {
        if (battle != null)
        {
            battle.OnSkillPanelToggled -= HandleToggle;
            battle.OnSkillPanelPopulateSO -= HandlePopulateSO;
            battle.OnHint -= HandleHint;
            battle.OnUnitActionLabel -= HandleUnitActionLabel;
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

        for (int i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            if (btn == null) continue;

            // --- 마지막 버튼은 항상 Cancel로 구성 ---
            if (i == last)
            {
                btn.gameObject.SetActive(true);

                // 클릭 → 취소 이벤트 발생 (인스펙터에서 배틀 취소 함수 연결)
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    // 상세를 비우고 패널 상태는 유지/닫기는 배틀 로직에 맡김
                    UpdateDetail(null, forceClear: true, showCancelDesc: true);
                });

                // 호버/선택 시 상세를 비우기
                var et = btn.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
                ClearTriggers(et);
                AddTrigger(et, EventTriggerType.PointerEnter, () => UpdateDetail(null, forceClear: true, showCancelDesc: true));
                AddTrigger(et, EventTriggerType.Select, () => UpdateDetail(null, forceClear: true, showCancelDesc: true));
                continue;
            }

            // --- 스킬 버튼 구성 ---
            if (i < n)
            {
                btn.gameObject.SetActive(true);
                var asset = assets[i];

                var txt = btn.GetComponentInChildren<Text>();
                if (txt != null) txt.text = asset != null ? asset.displayName : "(empty)";

                btn.onClick.RemoveAllListeners();
                int capture = i;
                btn.onClick.AddListener(() =>
                {
                    // 하이라이트를 먼저 현재 버튼으로 고정
                    var nav = (panel != null ? panel.GetComponent<UIArrowNavigator>() : null)
                                    ?? GetComponentInChildren<UIArrowNavigator>(true);
                    if (nav != null) nav.SelectIndexImmediate(capture, focus: false, updateHighlight: true);

                    // EventSystem에도 선택으로 알려주고 싶다면
                    if (EventSystem.current != null)
                        EventSystem.current.SetSelectedGameObject(btn.gameObject);

                    // 상세 패널 즉시 갱신
                    UpdateDetail(asset);

                    // 기존 배틀 호출(이 시점에서 Targeting 진입 → 잠금이 걸려도 OK)
                    battle.SelectSkill(capture);
                });

                // 호버/선택 시에도 상세 갱신 (마우스/패드 모두 커버)
                var et = btn.GetComponent<EventTrigger>();
                if (et == null) et = btn.gameObject.AddComponent<EventTrigger>();
                AddTrigger(et, EventTriggerType.PointerEnter, () => UpdateDetail(asset));
                AddTrigger(et, EventTriggerType.Select, () => UpdateDetail(asset));
            }
            else
            {
                btn.gameObject.SetActive(false);
            }
        }

        // 첫 항목으로 상세 초기화
        if (n > 0 && assets[0] != null) UpdateDetail(assets[0]);
        else UpdateDetail(null);
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
            descriptionText.text = _skillasset != null ? (string.IsNullOrEmpty(_skillasset.description) ? "" : _skillasset.GetFullDescriptionRich()) : "";

        if (rangeImage)
        {
            // rangeSprite를 따로 둘 계획이면: a.rangeSprite ?? a.descriptionImage
            rangeImage.sprite = _skillasset != null ? _skillasset.descriptionImage : null;
            rangeImage.enabled = (rangeImage.sprite != null);
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
        if (_pinnedEnemyLabel) return;

        bool show = !string.IsNullOrEmpty(msg);
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
}
