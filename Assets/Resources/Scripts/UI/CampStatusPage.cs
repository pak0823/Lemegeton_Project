using UnityEngine;
using UnityEngine.UI;

public class CampStatusPage : MonoBehaviour
{
    [Header("Text Components")]
    [SerializeField] private Text nameText; // 캐릭터 이름

    [Header("Stats Texts")]
    [SerializeField] private Text strText; // 근력 (PhysicalDamage)
    [SerializeField] private Text magText; // 총명 (MagicDamage)
    [SerializeField] private Text agiText; // 민첩 (AGI)
    [SerializeField] private Text bdyText; // 신체 (BDY)
    [SerializeField] private Text mndText; // 정신 (MND)
    [SerializeField] private Text insText; // 통찰 (INS)

    // 페이지가 켜질 때마다 자동 갱신
    private void OnEnable()
    {
        RefreshUI();
    }

    // 외부(CampUIManager)에서 캐릭터를 바꿀 때 호출할 함수
    public void RefreshUI()
    {
        // 현재 선택된 유닛 가져오기
        if (CampUIManager.Instance == null) return;
        UnitData unit = CampUIManager.Instance.selectedUnit;

        // 유닛이 없으면 비워두기
        if (unit == null)
        {
            ClearUI();
            return;
        }

        // UI 갱신
        nameText.text = unit.DisplayName;

        // 스탯 표시 형식: "최종값 (기본값 + 추가값)"
        // 지금은 장비가 없으니 bonus는 0으로 고정. 나중에 여기서 장비 스탯을 가져오면 됨.
        strText.text = GetStatString(unit.PhysicalDamage, 0);
        magText.text = GetStatString(unit.MagicDamage, 0);
        agiText.text = GetStatString(unit.AGI, 0);
        bdyText.text = GetStatString(unit.BDY, 0);
        mndText.text = GetStatString(unit.MND, 0);
        insText.text = GetStatString(unit.INS, 0);
    }

    // 포맷팅 헬퍼 함수
    private string GetStatString(int baseVal, int bonusVal)
    {
        int total = baseVal + bonusVal;
        // 예: 50 (50 + 0)
        return $"{total} ({baseVal} + {bonusVal})";
    }

    private void ClearUI()
    {
        nameText.text = "-";
        strText.text = "-";
        magText.text = "-";
        agiText.text = "-";
        bdyText.text = "-";
        mndText.text = "-";
        insText.text = "-";
    }
}