using UnityEngine;

public class DescriptionData : MonoBehaviour
{
    [TextArea(2, 4)] public string description;

    // 힌트 활성화 대상으로도 쓰고 싶다면 체크박스 하나
    public bool enableHintOnContact = true;

    [Header("상자 오브젝트가 열린 뒤에는 이 텍스트로 교체(선택)")]
    public bool useAlternateAfterOpened = false;
    [TextArea(2, 4)] public string descriptionAfterOpened;

    // 상자가 열렸을 때 호출: description을 열린 뒤 문구로 교체
    public void ApplyOpenedTextIfAny()
    {
        if (useAlternateAfterOpened && !string.IsNullOrWhiteSpace(descriptionAfterOpened))
            description = descriptionAfterOpened;
    }
}
