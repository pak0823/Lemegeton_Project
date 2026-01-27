using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class CraftSlotUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Button button;
    [SerializeField] private GameObject highlightObj; // 선택됐을 때 켜질 테두리 같은 거 (옵션)

    // 데이터 세팅 함수
    public void Setup(CraftRecipe recipe, UnityAction onClickAction)
    {
        // 1. 기본 정보 표시
        if (recipe.resultItem != null)
        {
            iconImage.sprite = recipe.resultItem.itemIcon;
            nameText.text = recipe.resultItem.itemName;
        }

        // 클릭 이벤트 연결
        // 기존 리스너 제거 (재사용 시 중복 방지)
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClickAction);

        // 클릭 시 하이라이트 갱신 로직 등은 여기서 추가 가능
    }

    // 선택 표시 켜고 끄기 (필요하면 호출)
    public void SetSelected(bool isSelected)
    {
        if (highlightObj) highlightObj.SetActive(isSelected);
    }
}