using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))] // 토글 컴포넌트 필수
public class CharacterSelectToggle : MonoBehaviour
{
    [Header("이 토글이 담당하는 유닛 데이터")]
    public UnitData myUnitData;
    [Header("UI References")]
    // 아이콘을 표시할 이미지 컴포넌트
    [SerializeField] private Image iconImage;

    private Toggle toggle;

    void Start()
    {
        toggle = GetComponent<Toggle>();

        // 토글 값이 바뀔 때마다 OnToggleChanged 함수 실행하도록 연결
        toggle.onValueChanged.AddListener(OnToggleChanged);

        UpdateIconVisual();

        // 시작할 때 켜져 있으면 바로 선택 처리
        if (toggle.isOn)
        {
            OnToggleChanged(true);
        }
    }

    // 데이터에 있는 이미지로 갈아끼우는 함수
    private void UpdateIconVisual()
    {
        if (myUnitData != null && iconImage != null)
        {
            if (myUnitData.UnitIcon != null)
            {
                iconImage.sprite = myUnitData.UnitIcon;
                iconImage.color = Color.white;
            }
        }
    }

    // 토글 상태가 변할 때 호출됨 (isOn: 켜졌는지 꺼졌는지)
    public void OnToggleChanged(bool isOn)
    {
        // 켜졌을 때만(=선택됐을 때만) 매니저에게 알림
        if (isOn)
        {
            if (CampUIManager.Instance != null && myUnitData != null)
            {
                CampUIManager.Instance.OnSelectCharacter(myUnitData);
                //Debug.Log($"[Toggle] {myUnitData.DisplayName} 선택됨!");
            }
        }
    }

    // (선택 사항) 나중에 유닛 데이터가 런타임에 바뀌는 경우를 대비해
    public void SetUnitData(UnitData data)
    {
        myUnitData = data;
        UpdateIconVisual();
    }
}