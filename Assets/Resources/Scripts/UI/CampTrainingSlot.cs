using UnityEngine;
using UnityEngine.UI;

public class CampTrainingSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text nameText;
    [SerializeField] private Button button;
    [SerializeField] private Image lockIcon; // 자물쇠 아이콘 (있으면 좋음)
    //[SerializeField] private Image selectionFrame; // 선택됐을 때 테두리(선택사항)

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

        // 자물쇠 아이콘
        if (lockIcon) lockIcon.gameObject.SetActive(isLocked);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClickTraining);
    }

    private void OnClickTraining()
    {
        if (isLocked)
        {
            // [잠김 상태]
            // 선택 효과(초록색)를 내지 않고, 부모에게 "잠긴거 눌렸다"고 보고함
            parentSlot.OnLockedTrainingSelected(routeIndex, unlockCost);
        }
        else
        {
            // [해금 상태]
            // 정상적으로 선택
            parentSlot.OnTrainingSelected(routeIndex);
        }
    }

    // 상태에 따른 비주얼 갱신
    public void UpdateVisualState(int currentActiveRoute)
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
    }
}