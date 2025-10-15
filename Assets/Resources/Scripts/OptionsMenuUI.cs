using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Project.UI;

namespace Project.UI
{
    public class OptionsMenuUI : ModalWindowBase
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitButton;

        protected override void Awake()
        {
            base.Awake();
            if (resumeButton) resumeButton.onClick.AddListener(() => Toggle());
            if (quitButton) quitButton.onClick.AddListener(OnBtnReturnTitle);
        }

        protected override void OnShown() { Shared.GameSpeedController?.RequestPause(); }
        protected override void OnHidden() { Shared.GameSpeedController?.ReleasePause(); }

        public void OnBtnReturnTitle()  // - EndScene에서도 사용중
        {
            if (quitButton) quitButton.interactable = false;    //  중복 클릭 방지
            Shared.GameSpeedController?.ReleasePause();  // 일시정지 해제                                           
            var mgr = UiModalManager.Instance;  // 옵션창 닫기(안 닫아도 전환되지만, 상태 정리 겸 호출)
            if (mgr != null) mgr.Close(this);
            else Hide();
            Shared.SceneTransitionManager.FadeToScene("TitleScene");    //타이틀로 이동
        }
    }
}

