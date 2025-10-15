using UnityEngine;

public class DescriptionData : MonoBehaviour
{
    [TextArea(2, 4)] public string description;

    // 힌트 활성화 대상으로도 쓰고 싶다면 체크박스 하나
    public bool enableHintOnContact = true;

    // 누르면 다른 상세 로직(팝업 등)을 열고 싶을 때 훅을 추가해도 됨
    // public void OnCommunication() { ... }
}
