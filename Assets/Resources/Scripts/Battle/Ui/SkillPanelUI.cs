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

    private SkillAsset[] cachedAssets;  // 마지막으로 채운 SO 목록 캐시

    void Awake()
    {
        if (battle == null)
            battle = FindObjectOfType<BattleManager>();

        // 이벤트 구독
        if (battle != null)
        {
            battle.OnSkillPanelToggled += HandleToggle;
            battle.OnSkillPanelPopulateSO += HandlePopulateSO;
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
        }
    }

    void HandleToggle(bool show)
    {
        if (panel != null) panel.SetActive(show);
    }

    void HandlePopulateSO(SkillAsset[] assets)
    {
        if (buttons == null) return;
        cachedAssets = assets; // 캐시
        int n = Mathf.Min(assets?.Length ?? 0, buttons.Length);

        for (int i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            if (btn == null) continue;

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
                    if (nav != null) nav.SelectIndexImmediate(capture);

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
                if((buttons.Length - 1) > i)
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
    void UpdateDetail(SkillAsset _skillasset)
    {
        if (descriptionText)
            descriptionText.text = _skillasset != null ? (string.IsNullOrEmpty(_skillasset.description) ? "" : _skillasset.description) : "";

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
}
