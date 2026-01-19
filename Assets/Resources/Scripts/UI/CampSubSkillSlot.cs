using UnityEngine;
using UnityEngine.UI;

public class CampSubSkillSlot : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Button button;
    [SerializeField] private GameObject highlight; // 선택 표시용

    private SkillAsset mySubSkill;    // 파생 스킬 (예: 스킬 1-1)
    private SkillAsset myParentSkill; // 원본 스킬 (예: 스킬 1-0)
    private CampSkillPage parentPage;
    private UnitData myUnit;

    public void Setup(UnitData unit, SkillAsset subSkill, SkillAsset parentSkill, CampSkillPage page)
    {
        myUnit = unit;
        mySubSkill = subSkill;
        myParentSkill = parentSkill;
        parentPage = page;

        // 이름 앞에 'ㄴ'이나 공백을 넣어 파생임을 티내기
        if (nameText) nameText.text = $"    ㄴ {subSkill.displayName}";

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);

        SetSelected(false);
    }

    private void OnClick()
    {
        // 페이지에게 알림
        // 왼쪽 설명: 파생 스킬 내용
        // 오른쪽 훈련: 부모 스킬의 훈련 내용 (공유하므로)
        parentPage.OnSubSlotClicked(this, mySubSkill, myParentSkill, myUnit);
    }

    public void SetSelected(bool isSelected)
    {
        if (highlight) highlight.SetActive(isSelected);
        // 또는 텍스트 색상 변경
        if (nameText) nameText.color = isSelected ? Color.cyan : Color.gray;
    }
}