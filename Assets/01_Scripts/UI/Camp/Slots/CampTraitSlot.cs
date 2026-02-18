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

    public void Setup(TraitAsset trait, bool isActive, Action<string, string, Transform> onSelected, Action<TraitAsset> _unused = null)
    {
        _trait = trait;

        if (selfButton == null) selfButton = GetComponent<Button>();

        // 이름 설정
        if (nameText) nameText.text = trait.displayName;

        // 색상 결정 (해금되면 무조건 초록/활성 상태)
        Color displayColor = isActive ? Color.green : Color.white;

        // 텍스트 색상 적용
        if (visualFeedback) visualFeedback.SetNormalColor(displayColor);
        else nameText.color = displayColor;

        // 클릭 이벤트
        selfButton.onClick.RemoveAllListeners();
        selfButton.onClick.AddListener(() =>
        {
            if (_trait != null)
            {
                Transform target = (nameText != null) ? nameText.transform : this.transform;
                onSelected?.Invoke(_trait.displayName, _trait.description, target);
                // 장착 로직 제거됨
            }
        });
    }
}