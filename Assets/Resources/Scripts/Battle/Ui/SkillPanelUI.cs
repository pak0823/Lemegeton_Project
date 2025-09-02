using UnityEngine;
using UnityEngine.UI;

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
            battle.OnSkillPanelPopulate += HandlePopulate;
        }

        if (panel != null) panel.SetActive(false); // 기본 비활성
        WireTempButtons(); // 초기 onClick 정리
    }

    void OnDestroy()
    {
        if (battle != null)
        {
            battle.OnSkillPanelToggled -= HandleToggle;
            battle.OnSkillPanelPopulate -= HandlePopulate;
        }
    }

    void HandleToggle(bool show)
    {
        if (panel != null) panel.SetActive(show);
    }

    void HandlePopulate(SkillDefinition[] defs)
    {
        if (buttons == null) return;
        int n = Mathf.Min(defs.Length, buttons.Length);
        for (int i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            if (btn == null) continue;

            if (i < n)
            {
                btn.gameObject.SetActive(true);
                var def = defs[i];
                // 라벨(Text 또는 TMP에 맞춰 세팅)
                var txt = btn.GetComponentInChildren<Text>();
                if (txt != null) txt.text = def.name;

                btn.onClick.RemoveAllListeners();
                int capture = i;
                btn.onClick.AddListener(() =>
                {
                    battle.SelectSkill(capture);
                });
            }
            else
            {
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
