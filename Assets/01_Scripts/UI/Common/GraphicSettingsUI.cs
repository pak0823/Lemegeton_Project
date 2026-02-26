using UnityEngine;
using UnityEngine.UI;
using TMPro; // TMP Dropdown을 사용한다고 가정
using Project.UI;
using Project.Settings;
using System.Collections.Generic;

namespace Project.UI
{
    /// <summary>
    /// 그래픽(해상도, 창모드) 설정을 담당하는 UI 컨트롤러 프리팹용 스크립트.
    /// ModalWindowBase를 상속받아 기존 Esc 닫기 및 UI 매니저 시스템과 연동됩니다.
    /// </summary>
    public class GraphicSettingsUI : ModalWindowBase
    {
        [Header("UI Reference - Settings")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("UI Reference - Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button closeButton;

        private ScreenSettingsManager _mgr;

        protected override void Awake()
        {
            base.Awake();

            // 버튼 이벤트 연결
            if (applyButton != null)
                applyButton.onClick.AddListener(OnBtnApply);

            if (closeButton != null)
                closeButton.onClick.AddListener(OnBtnClose);

            // 매니저를 찾거나 없으면 런타임에 초기화되도록 대비
            _mgr = ScreenSettingsManager.Instance;
        }

        protected override void OnShown()
        {
            base.OnShown();

            // 매니저가 null이라면 한번 더 찾아봄
            if (_mgr == null)
                _mgr = ScreenSettingsManager.Instance;

            RefreshUI();
        }

        /// <summary>
        /// 현재 적용되어 있는(혹은 저장되어 있는) 설정값으로 UI를 동기화합니다.
        /// </summary>
        private void RefreshUI()
        {
            if (_mgr == null) return;

            // 1. 드롭다운 초기화 및 옵션 추가
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                List<string> options = new List<string>();

                foreach (var resData in _mgr.SupportedResolutions)
                {
                    options.Add(resData.label); // ex) "1920 x 1080 (FHD)"
                }

                resolutionDropdown.AddOptions(options);

                // 현재 설정된 해상도 인덱스로 맞춤
                resolutionDropdown.value = _mgr.GetCurrentResolutionIndex();
                resolutionDropdown.RefreshShownValue();
            }

            // 2. 전체화면 토글 상태 맞춤
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = _mgr.GetCurrentFullScreenState();
            }
        }

        /// <summary>
        /// '적용(Apply)' 버튼 클릭 시 호출
        /// </summary>
        public void OnBtnApply()
        {
            if (_mgr == null) return;

            int selectedIndex = resolutionDropdown != null ? resolutionDropdown.value : 2; // 기본 FHD
            bool isFullScreen = fullscreenToggle != null ? fullscreenToggle.isOn : true;

            // 범위를 벗어난 예외 방어
            if (selectedIndex < 0 || selectedIndex >= _mgr.SupportedResolutions.Count)
                selectedIndex = 2;

            var targetRes = _mgr.SupportedResolutions[selectedIndex];

            // 매니저에게 해상도 변경 지시 (내부에서 PlayerPrefs 저장까지 수행)
            _mgr.SetResolution(targetRes.width, targetRes.height, isFullScreen);

            // 임시로 창 닫기 안함 (해상도 변경 적용 확인용)
            Debug.Log("[GraphicSettingsUI] 해상도 및 화면 모드가 성공적으로 적용되었습니다.");
        }

        /// <summary>
        /// '닫기/취소(Close)' 버튼 클릭 시 호출
        /// </summary>
        public void OnBtnClose()
        {
            // UiModalManager에 의해 닫거나, 직접 Hide
            var uiMgr = UiModalManager.Instance;
            if (uiMgr != null)
                uiMgr.Close(this);
            else
                Hide();
        }
    }
}
