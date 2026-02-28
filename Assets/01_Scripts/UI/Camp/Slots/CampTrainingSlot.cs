using UnityEngine;
using UnityEngine.UI;

public class CampTrainingSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text nameText;
    [SerializeField] private Button button;
    [SerializeField] private Image selectionHighlight; // 선택 시 하이라이트

    [Header("Colors")]
    [SerializeField] private Color activeColor = Color.green;      // 적용됨
    [SerializeField] private Color inactiveColor = Color.white;    // 미적용 (개방됨)
    [SerializeField] private Color lockedColor = new Color(1, 1, 1, 0.4f); // 미개방 (반투명)

    private int routeIndex; // 0, 1, 2...
    private int unlockCost;
    private CampSkillSlot parentSlot; // 나를 관리하는 스킬 슬롯(부모)
    private bool isLocked = false;

    public void Setup(int index, string trainingName, int cost, bool locked, CampSkillSlot parent)
    {
        routeIndex = index;
        parentSlot = parent;
        isLocked = locked;
        unlockCost = cost;

        if (nameText) nameText.text = trainingName;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClickTraining);
    }

    private void OnClickTraining()
    {
        if (isLocked)
        {
            // [잠김 상태]
            // 선택 효과(초록색)를 내지 않고, 부모에게 "잠긴거 눌렸다"고 보고함
            parentSlot.OnLockedTrainingSelected(routeIndex, unlockCost, this.transform);
        }
        else
        {
            // [해금 상태]
            // 정상적으로 선택
            parentSlot.OnTrainingSelected(routeIndex, this.transform);
        }
    }

    // 상태에 따른 비주얼 갱신
    public void UpdateVisualState(int currentActiveRoute, int focusedRouteIndex)
    {
        // 버튼 Interactable은 항상 true로 둬야 클릭해서 정보를 볼 수 있음
        button.interactable = true;

        // 버튼의 배경 이미지 가져오기 (없으면 button.targetGraphic 사용)
        Image btnImage = button.GetComponent<Image>();

        if (isLocked)
        {
            // [잠김] 텍스트와 버튼 배경 모두 반투명 색상 적용
            if (nameText) nameText.color = lockedColor;
            if (btnImage) btnImage.color = lockedColor;
        }
        else if (currentActiveRoute == routeIndex)
        {
            // [선택됨] 텍스트는 초록색(activeColor), 배경은 원래대로(흰색)
            if (nameText) nameText.color = activeColor;
            if (btnImage) btnImage.color = Color.white;
        }
        else
        {
            // [미선택/해금됨] 텍스트 흰색(inactiveColor), 배경 흰색
            if (nameText) nameText.color = inactiveColor;
            if (btnImage) btnImage.color = Color.white;
        }

        // 하이라이트 표시 처리
        if (selectionHighlight != null)
        {
            // focusedRouteIndex(현재 클릭된 훈련 번호)
            bool isFocused = (routeIndex == focusedRouteIndex);

            // 잠겨있으면 포커스되어도 하이라이트 끄기
            if (isLocked)
            {
                selectionHighlight.gameObject.SetActive(false);
            }
            else
            {
                selectionHighlight.gameObject.SetActive(isFocused);
            }
        }
    }
}