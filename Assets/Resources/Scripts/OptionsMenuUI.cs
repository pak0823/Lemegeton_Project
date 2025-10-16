using Project.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Project.UI
{
    public class OptionsMenuUI : ModalWindowBase
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitButton;

        [Header("Focus container")]
        [SerializeField] private RectTransform buttonsContainer; // ButtonList 오브젝트를 할당
        [SerializeField] private bool autoCollect = true;        // 자식 버튼 자동 수집
        [SerializeField] private bool excludeResumeFromFocus = true; // resume 제외

        [Header("Focus Arrow")]
        [SerializeField] private RectTransform focusArrow;              // 화살표 이미지 RectTransform
        [SerializeField] private Vector2 arrowOffset = new Vector2(-40f, 0f); // 타겟 버튼 기준 왼쪽 오프셋

        [Header("Key bindings (optional)")]
        private KeyCode upKey = KeyCode.W;
        private KeyCode downKey = KeyCode.S;
        private KeyCode submitKey = KeyCode.E;      // '현재 포커스된 버튼' 실행
        private KeyCode submit_V2Key = KeyCode.Return;
        private KeyCode cancelKey = KeyCode.Q;      // 취소

        readonly List<Button> focusButtons = new List<Button>();
        int focusIndex = 0;
        public bool IsShow { get; private set; }

        void OnEnable()
        {
            RebuildFocusList();
        }

        void OnTransformChildrenChanged()
        {
            if (!buttonsContainer) return;
            RebuildFocusList();
            // 첫 유효 항목으로 화살표 갱신
            if (IsOpen && focusButtons.Count > 0) SetFocus(0);
            else if (focusArrow) focusArrow.gameObject.SetActive(false);
        }

        protected override void Awake()
        {
            base.Awake();
            if (resumeButton) resumeButton.onClick.AddListener(() => Toggle());
            if (quitButton) quitButton.onClick.AddListener(OnBtnReturnTitle);

            // 화살표 좌표계 맞춤
            if (focusArrow && buttonsContainer && focusArrow.parent != buttonsContainer)
                focusArrow.SetParent(buttonsContainer, worldPositionStays: false);

            if (focusArrow) focusArrow.gameObject.SetActive(false);

            // 포커스 목록 구성
            RebuildFocusList();
        }

        protected override void OnShown() 
        { 
            Shared.GameSpeedController?.RequestPause();
            IsShow = true;
            if (focusButtons.Count == 0)
            {
                if (focusArrow) focusArrow.gameObject.SetActive(false);
                return;
            }
            SetFocus(0);
        }
        protected override void OnHidden() 
        { 
            Shared.GameSpeedController?.ReleasePause();
            IsShow = false;
            if (focusArrow) focusArrow.gameObject.SetActive(false); 
        }

        void Update()
        {
            if (!IsOpen) return; // ModalWindowBase가 관리

            // 좌우 이동
            if (Input.GetKeyDown(upKey)) MoveFocus(-1);
            if (Input.GetKeyDown(downKey)) MoveFocus(+1);

            // E = 현재 포커스 버튼 실행
            if (Input.GetKeyDown(submitKey) || Input.GetKeyDown(submit_V2Key))
            {
                var b = GetFocusedButton();
                b?.onClick?.Invoke();
            }

            // Q = 취소(옵션창 닫기)
            if (Input.GetKeyDown(cancelKey))
            {
                Toggle(); // UiModalManager 통해 닫힘 or 직접 Hide()
            }
        }

        void RebuildFocusList()
        {
            focusButtons.Clear();
            if (!buttonsContainer) return;

            if (autoCollect)
            {
                // 보이는(활성/상호작용 가능한) 버튼만 수집
                var found = buttonsContainer.GetComponentsInChildren<Button>(false) // ← false: 비활성 미포함
                            .Where(b => b != null
                                        && b.isActiveAndEnabled
                                        && b.gameObject.activeInHierarchy
                                        && b.interactable)
                            .ToList();

                // 포커스에서 제외할 것들 필터링
                if (excludeResumeFromFocus && resumeButton)
                {
                    found.RemoveAll(b => b == resumeButton);
                }

                // 혹시 null 섞였으면 제거
                found.RemoveAll(b => b == null);

                focusButtons.AddRange(found);

                focusIndex = 0; // 리스트가 바뀌었으니 시작 포커스 리셋
            }
        }

        Button GetFocusedButton()
        {
            if (focusButtons.Count == 0) return null;
            focusIndex = Mathf.Clamp(focusIndex, 0, focusButtons.Count - 1);
            return focusButtons[focusIndex];
        }

        void MoveFocus(int delta)
        {
            if (focusButtons.Count == 0) return;

            int start = focusIndex;
            for (int i = 0; i < focusButtons.Count; i++)
            {
                focusIndex = (focusIndex + delta + focusButtons.Count) % focusButtons.Count;
                var b = focusButtons[focusIndex];
                if (b != null && b.isActiveAndEnabled && b.gameObject.activeInHierarchy && b.interactable)
                    break; // 유효한 버튼을 찾으면 정지
            }
            UpdateArrowPosition();
        }

        void SetFocus(int idx)
        {
            if (focusButtons.Count == 0) return;
            focusIndex = Mathf.Clamp(idx, 0, focusButtons.Count - 1);
            UpdateArrowPosition();
        }

        void UpdateArrowPosition()
        {
            if (!focusArrow || focusButtons.Count == 0) return;

            var targetBtn = GetFocusedButton();
            if (!targetBtn)
            {
                focusArrow.gameObject.SetActive(false);
                return;
            }

            var target = targetBtn.transform as RectTransform;

            if (!focusArrow.gameObject.activeSelf)
                focusArrow.gameObject.SetActive(true);

            // 같은 부모(buttonsContainer) 좌표계에서 오프셋 적용
            // (focusArrow는 Awake에서 buttonsContainer로 SetParent됨)
            focusArrow.anchoredPosition = target.anchoredPosition + arrowOffset;
        }

        public void OnBtnQuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

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

