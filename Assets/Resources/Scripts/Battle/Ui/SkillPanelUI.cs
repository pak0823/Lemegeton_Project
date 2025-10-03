using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class SkillPanelUI : MonoBehaviour
{
    [Header("Refs")]
    public BattleManager battle;        // 인스펙터로 할당
    public GameObject panel;            // SkillPanel 오브젝트
    public Button[] buttons;            // 4~5개

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

                    // 기존 배틀 호출(이 시점에서 Targeting 진입 → 잠금이 걸려도 OK)
                    battle.SelectSkill(capture);
                });
            }
            else
            {
                if((buttons.Length - 1) > i)
                    btn.gameObject.SetActive(false);
            }
            
        }
    }

    // 초기에 불필요한 리스너 삭제
    void WireTempButtons()
    {
        if (buttons == null) return;
        foreach (var b in buttons)
            if (b != null) b.onClick.RemoveAllListeners();
    }
}
