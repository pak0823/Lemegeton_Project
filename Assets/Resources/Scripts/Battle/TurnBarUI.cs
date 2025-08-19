using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TurnBarUI : MonoBehaviour
{
    [Header("UI Settings")]
    public RectTransform bar;          // 긴 바
    public GameObject unitIconPrefab;  // 원형 아이콘 프리팹
    public float barWidth;      // 바 길이
    public RectTransform barImage;

    Dictionary<BattleUnit, Image> unitIcons = new();

    void Start()
    {
        var battle = FindObjectOfType<BattleManager>();
        if (battle != null)
        {
            battle.OnATBChanged += UpdateTurnBar;
            InitializeIcons(battle);
        }
    }

    void InitializeIcons(BattleManager battle)
    {
        barWidth = barImage.rect.width;

        foreach (var units in FindObjectsOfType<BattleUnit>())
        {
            var iconGO = Instantiate(unitIconPrefab, barImage);
            var img = iconGO.GetComponent<Image>();
            img.sprite = units.data.UnitIcon; // 유닛 스프라이트 할당
            unitIcons[units] = img;

            // 팀에 따라 y 위치 다르게
            float yOffset = (units.team == Team.Player) ? 20f : -20f; // 위쪽 20, 아래쪽 -20
            iconGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, yOffset);

            // 사망 이벤트 구독
            units.OnDied += RemoveUnitIcon;
        }
    }

    void UpdateTurnBar(BattleUnit unit, float currentATB, float maxATB)
    {
        if (!unitIcons.ContainsKey(unit)) return;

        float normalized = Mathf.Clamp01(currentATB / maxATB);
        float xPos = normalized * barWidth;

        var rt = unitIcons[unit].GetComponent<RectTransform>();

        // y 위치는 InitializeIcons에서 설정한 값 유지
    rt.anchoredPosition = new Vector2(xPos, rt.anchoredPosition.y);
    }

    // 유닛 사망 시 아이콘 제거
    void RemoveUnitIcon(BattleUnit unit)
    {
        if (!unitIcons.ContainsKey(unit)) return;

        Destroy(unitIcons[unit].gameObject);
        unitIcons.Remove(unit);
    }
}
