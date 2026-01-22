using System;
using UnityEngine;
using UnityEngine.UI;

public class CampTraitSlot : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Button selfButton; // 버튼 컴포넌트
    [SerializeField] private UI_ButtonFeedback visualFeedback; // 버튼 이펙트 처리

    // 나중에 설명 띄울 때 쓸 데이터 저장용
    private TraitAsset _trait;

    public void Setup(TraitAsset trait, bool isActive, Action<string, Transform> onSelected, Action<TraitAsset> onEquip)
    {
        _trait = trait;

        if (selfButton == null) selfButton = GetComponent<Button>();

        // 이름 설정
        if (nameText) nameText.text = trait.displayName;

        // 색상 결정 (활성화면 초록, 아니면 흰색)
        Color displayColor = isActive ? Color.green : Color.white;

        // 텍스트 색상 적용 (Feedback 스크립트 통해야 눌렀다 떼도 색 유지됨)
        if (visualFeedback) visualFeedback.SetNormalColor(displayColor);
        else nameText.color = displayColor;

        // 클릭 이벤트
        selfButton.onClick.RemoveAllListeners();
        selfButton.onClick.AddListener(() =>
        {
            if (_trait != null)
            {
                Transform target = (nameText != null) ? nameText.transform : this.transform;
                onSelected?.Invoke(_trait.description, target);
                onEquip?.Invoke(_trait);
            }
        });
    }
}