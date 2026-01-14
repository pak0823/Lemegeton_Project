using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))] // 토글 컴포넌트 필수
public class CharacterSelectToggle : MonoBehaviour
{
    [Header("이 토글이 담당하는 유닛 데이터")]
    public UnitData myUnitData;

    private Toggle toggle;

    void Start()
    {
        toggle = GetComponent<Toggle>();

        // 토글 값이 바뀔 때마다 OnToggleChanged 함수 실행하도록 연결
        toggle.onValueChanged.AddListener(OnToggleChanged);

        // (옵션) 시작할 때 켜져 있으면 바로 선택 처리
        if (toggle.isOn)
        {
            OnToggleChanged(true);
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
                Debug.Log($"[Toggle] {myUnitData.DisplayName} 선택됨!");
            }
        }
    }
}