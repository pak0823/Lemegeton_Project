using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class TestCode : MonoBehaviour
{
    public GameObject OptionPanel;
    [SerializeField] private GameSpeedController speedCtrl;
    public bool isShow = false;


    public void StartGame() // - TitleScene에서 사용중
    {
        Shared.SceneTransitionManager.FadeToScene("ExplorationScene");
        Debug.Log("인게임 씬으로 이동");
    }

    public void QuitGame()  // - TitleScene에서 사용중
    {
        // 빌드된 애플리케이션에서는 실제로 종료
        Application.Quit();

#if UNITY_EDITOR
        // 에디터 상에서는 플레이 모드 종료
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void ShowPanel() // - BattleScene에서 사용중
    {
        isShow = !isShow;

        if (isShow)
            speedCtrl.RequestPause();   // 옵션창 열림 → 일시정지 요청
        else
            speedCtrl.ReleasePause(); // 옵션창 닫힘 → 일시정지 해제 요청

        OptionPanel.SetActive(isShow);
    }

    public void OnBtnReturnTitle()  // - EndScene에서 사용중
    {
        Shared.SceneTransitionManager.FadeToScene("TitleScene");
    }

}
