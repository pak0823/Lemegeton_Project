using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Project.UI;

namespace Project.UI
{
    public class OptionsMenuUI : MonoBehaviour, ISceneUiModule
    {
        [Header("Wiring")]
        [SerializeField] private CanvasGroup rootGroup; // 옵션 패널 루트
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitButton;

        [Header("Events (Optional)")]
        public UnityEvent onOpened;
        public UnityEvent onClosed;

        [Header("(Optional) Behavior")]
        [SerializeField] private bool closeOnBackgroundClick = true;
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;

        private bool _isOpen;
        private bool _initialized;

        private void Reset()
        {
            // 에디터에서 붙였을 때 자동 배선
            if (!rootGroup) rootGroup = GetComponentInChildren<CanvasGroup>(true);
        }

        private void Awake()
        {
            WireButtons();
            SetVisible(false, instant: true);
        }

        private void Update()
        {
            if (closeKey != KeyCode.None && Input.GetKeyDown(closeKey))
                Toggle();

            // (선택) 배경 클릭으로 닫기
            //if (closeOnBackgroundClick && _isOpen && Input.GetMouseButtonDown(0))
            //    Hide();
        }

        public void OnUiShown()
        {
            if (!_initialized)
            {
                SetVisible(false, instant: true);
                _initialized = true;
            }
        }
        public void OnUiHidden()
        {
            // 열려 있었다면 닫고 정리
            if (_isOpen) Hide();
        }

        private void WireButtons()
        {
            if (resumeButton) resumeButton.onClick.AddListener(Hide);
            if (quitButton) quitButton.onClick.AddListener(OnBtnReturnTitle);
        }

        public void Toggle()
        {
            if (_isOpen) Hide();
            else Show();
        }

        public void Show()
        {
            if (_isOpen) return;
            SetVisible(true);
            Shared.GameSpeedController?.RequestPause();
            onOpened?.Invoke();
        }

        public void Hide()
        {
            if (!_isOpen) return;
            onClosed?.Invoke();
            SetVisible(false);
            Shared.GameSpeedController?.ReleasePause();
        }

        private void SetVisible(bool visible, bool instant = false)
        {
            _isOpen = visible;
            if (!rootGroup)
            {
                gameObject.SetActive(visible);
                return;
            }
            // CanvasGroup으로 페이드/입력 제어(에니메이션은 프로젝트 취향대로)
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.blocksRaycasts = visible;
            rootGroup.interactable = visible;
            if (!gameObject.activeSelf) gameObject.SetActive(true); // 활성 상태는 유지
        }

        public void OnBtnReturnTitle()  // - EndScene에서 사용중
        {
            Shared.SceneTransitionManager.FadeToScene("TitleScene");
        }
    }
}

