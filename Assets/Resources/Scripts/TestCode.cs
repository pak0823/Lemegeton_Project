using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestCode : MonoBehaviour
{
    public void StartGame()
    {
        Shared.SceneTransitionManager.FadeToScene("TestScene");
        Debug.Log("인게임 씬으로 이동");
    }

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
