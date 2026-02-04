using UnityEngine;
using System.Collections.Generic;

public class ResolutionTester : MonoBehaviour
{
    [System.Serializable]
    public struct ResolutionSet
    {
        public string label; // 에디터 식별용 (예: FHD)
        public int width;
        public int height;
    }

    [Header("테스트할 해상도 목록")]
    public List<ResolutionSet> resolutions = new List<ResolutionSet>()
    {
        // 자주 쓰는 거 미리 넣어둠 (인스펙터에서 수정 가능)
        new ResolutionSet { label = "FHD", width = 1920, height = 1080 },
        new ResolutionSet { label = "HD+", width = 1600, height = 900 },
        new ResolutionSet { label = "HD", width = 1280, height = 720 }
    };

    [Header("설정")]
    public bool isFullScreen = true;

    // UI 버튼의 OnClick 이벤트에 연결할 함수
    public void SetResolutionByIndex(int index)
    {
        if (index < 0 || index >= resolutions.Count)
        {
            Debug.LogError($"[Resolution] 잘못된 인덱스다: {index}");
            return;
        }

        ResolutionSet target = resolutions[index];

        // 실제 해상도 변경 로직
        Screen.SetResolution(target.width, target.height, isFullScreen);

        Debug.Log($"[Resolution] 해상도 변경됨: {target.width} x {target.height} (전체화면: {isFullScreen})");
    }

    public void SetFullScreen(bool isFull)
    {
        isFullScreen = isFull;

        if (isFull)
        {
            // 테두리 없는 전체화면
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        }
        else
        {
            // 일반 창모드
            Screen.fullScreenMode = FullScreenMode.Windowed;
        }

        Debug.Log($"[Resolution] 모드 변경: {Screen.fullScreenMode}");
    }
}