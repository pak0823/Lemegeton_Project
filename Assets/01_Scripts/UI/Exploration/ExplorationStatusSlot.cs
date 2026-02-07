using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ExplorationStatusSlot : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Text nameText;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Slider mpSlider;
    [SerializeField] private Slider rageSlider;

    [Header("Text Indicators")]
    [SerializeField] private Text hpText;
    [SerializeField] private Text mpText;
    [SerializeField] private Text rageText;

    [Header("Equipment Slots")]
    [SerializeField] private List<Image> itemIcons = new List<Image>(); // 아이콘 표시용 (3개)

    private UnitData _targetData;

    // 초기화 및 데이터 바인딩
    public void Bind(UnitData data)
    {
        _targetData = data;

        if (_targetData == null)
        {
            // 데이터가 없으면 비활성화
            Debug.Log($"[ExplorationStatusSlot] {name}: Data is null, disabling slot.");
            gameObject.SetActive(false);
            return;
        }

        Debug.Log($"[ExplorationStatusSlot] {name}: Binding unit {_targetData.DisplayName}");
        gameObject.SetActive(true);

        // 1. 기본 정보 설정
        if (nameText) nameText.text = _targetData.DisplayName;
        else Debug.LogWarning($"[ExplorationStatusSlot] {name}: nameText is null!");

        // 2. 런타임 데이터 가져오기
        Refresh();
    }

    // 상태 갱신 (데이터 매니저에서 최신 상태를 가져옴)
    public void Refresh()
    {
        if (_targetData == null) return;

        // PlayerDataManager에서 런타임 데이터 조회
        // (만약 데이터가 없으면 풀피로 가정하거나 0으로 표시)
        var runtimeData = PlayerDataManager.Instance.GetRuntimeData(_targetData);

        // 최대치 계산 (현재 UnitData에는 Base 스탯만 있고, 장비/보정을 통한 MaxHP 계산 로직이 여기엔 없음)
        // 임시 방편: UnitData의 Base 스탯을 기반으로 대략적인 MaxHP 계산
        // *정확한 계산을 위해서는 BattleUnit과 동일한 스탯 계산 로직을 공유하는 클래스가 필요함 (Refactoring 대상)
        // 일단은 RuntimeData가 있으면 그걸 쓰고, 없으면 기본값
        
        float currentHP = 0;
        float currentMP = 0;
        float currentRage = 0;

        // UnitData의 Helper 메서드 사용하여 Max 스탯 계산
        var (maxHP, maxMP, maxRage) = _targetData.CalcMaxStats();

        // 런타임 데이터가 있으면 적용, 없으면 기본값 (보통 PlayerMng에서 초기화해주지만 안전장치)
        if (runtimeData != null)
        {
            currentHP = runtimeData.currentHP;
            currentMP = runtimeData.currentMP;
            currentRage = runtimeData.currentRage;
        }
        else
        {
            currentHP = maxHP;
            currentMP = 0;
            currentRage = 0;
        }

        Debug.Log($"[ExplorationStatusSlot] {_targetData.DisplayName}: RuntimeData={(runtimeData != null ? "Found" : "Null")}, HP={currentHP}/{maxHP}");

        // 3. UI 반영
        UpdateSlider(hpSlider, hpText, currentHP, maxHP);
        UpdateSlider(mpSlider, mpText, currentMP, maxMP);
        UpdateSlider(rageSlider, rageText, currentRage, maxRage);
        
        // Items (임시)
        // UnitData에 장비 정보가 있다면 여기서 아이콘 갱신
        // currently no logic for equipment in this snippet
    }

    private void UpdateSlider(Slider slider, Text text, float current, float max)
    {
        if (slider != null)
        {
            var prev = slider.value;
            slider.maxValue = max;
            slider.value = current;
            Debug.Log($"[ExplorationStatusSlot] {name} {slider.gameObject.name}: Set Value {current}/{max} (Prev: {prev})");
        }
        else
        {
            Debug.LogWarning($"[ExplorationStatusSlot] {name}: A slider is null! (Target Component: {slider})");
        }

        // 별도의 텍스트 컴포넌트가 연결되어 있다면 갱신
        if (text != null)
        {
            // [수정] "현재/최대" -> "현재" 만 표시
            text.text = $"{Mathf.FloorToInt(current)}";
        }
        else
        {
            // 혹시 슬라이더 내부에 자식으로 텍스트가 있는 경우
            if (slider != null)
            {
                var childText = slider.GetComponentInChildren<Text>();
                if (childText != null)
                {
                    childText.text = $"{Mathf.FloorToInt(current)}";
                }
            }
        }
    }
}
