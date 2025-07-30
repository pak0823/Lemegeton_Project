using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestCode : MonoBehaviour
{
    /// <summary>
    /// 버튼 클릭 시 게임을 종료합니다.
    /// 에디터에서는 플레이 모드를 정지합니다.
    /// </summary>
    public void QuitGame()
    {
        // 빌드된 애플리케이션에서는 실제로 종료
        Application.Quit();

#if UNITY_EDITOR
        // 에디터 상에서는 플레이 모드 종료
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
