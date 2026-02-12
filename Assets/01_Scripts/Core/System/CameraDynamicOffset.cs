using UnityEngine;

[ExecuteAlways] // 에디터 모드에서도 확인 가능하게 함
public class CameraDynamicOffset : MonoBehaviour
{
    [Header("UI Settings")]
    public float uiWidthPixels = 450f; // UI가 차지하는 가로 픽셀
    public float referenceHeight = 1080f; // 기준 해상도 세로

    [Header("Target")]
    public Transform cameraParent; // CameraRig (부모)

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (cam == null || cameraParent == null) return;

        // 현재 카메라 Size와 해상도를 기반으로 오프셋 계산
        // 공식: (UI너비 / 2) * (카메라높이유닛 / 해상도세로픽셀)
        float currentOffset = (uiWidthPixels / 2f) * (cam.orthographicSize * 2f / Screen.height);

        // 카메라의 로컬 위치만 살짝 밀어줌
        // 부모(CameraRig)는 플레이어를 쫓아가고, 이 스크립트는 카메라 자체만 왼쪽으로 미는 역할
        // 카메라가 왼쪽으로 가야 캐릭터가 화면 오른쪽(가용 영역 중앙)으로 옵니다.
        transform.localPosition = new Vector3(-currentOffset, 0, transform.localPosition.z);
    }
}