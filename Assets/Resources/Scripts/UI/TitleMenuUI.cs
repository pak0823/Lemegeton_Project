using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace Project.UI
{
    public class TitleMenuUI : MonoBehaviour, ISceneUiModule
    {
        [System.Serializable]
        public class MenuItem
        {
            public string id;               // 식별자 (디버그용)
            public Button button;           // UI 버튼
            public bool requiresSaveData;   // 세이브 데이터가 있어야 활성화되는지
            // 필요하다면 여기에 UnityEvent onClick 등을 추가해 인스펙터에서 연결 가능
        }

        [Header("Configuration")]
        [SerializeField] private List<MenuItem> menuItems = new List<MenuItem>(); // 버튼 리스트로 통합 관리

        [Header("Navigation & Visuals")]
        [SerializeField] private RectTransform arrow;
        [SerializeField] private Vector2 arrowOffset = new Vector2(-40f, 0f);
        [SerializeField] private Color focusTextColor = new Color(1f, 0.6f, 0f); // 주황색

        [Header("Settings")]
        [SerializeField] private bool simulateHasSaveData = false; // 임시 데이터 플래그
        [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.5f;

        // 상태 관리
        private int _currentIndex = -1;
        private bool _isInputActive = false;

        // 중복 실행 방지용 플래그 추가
        private bool _isBusy = false;

        // 텍스트 색상 캐싱
        private Dictionary<Text, Color> _originalTextColors = new Dictionary<Text, Color>();

        // 외부 의존성 (옵션 패널 등) -> 인터페이스나 매니저를 통하는 것이 좋으나, 편의상 유지하되 의존성 최소화
        public OptionsMenuUI optionPanel;

        private void Awake()
        {
            InitializeButtons();
        }

        private void InitializeButtons()
        {
            foreach (var item in menuItems)
            {
                if (item.button == null) continue;

                // 1. 텍스트 색상 캐싱
                var texts = item.button.GetComponentsInChildren<Text>(true);
                foreach (var t in texts)
                {
                    if (!_originalTextColors.ContainsKey(t))
                        _originalTextColors.Add(t, t.color);
                }

                // 2. 마우스 이벤트 연결 (EventTrigger 대신 가벼운 방식 권장하지만, 기존 로직 존중하여 유지)
                // 람다 캡처 주의: foreach 변수를 직접 쓰지 말고 로컬 변수에 할당
                var targetBtn = item.button;
                var itemIndex = menuItems.IndexOf(item);

                var trigger = targetBtn.gameObject.GetComponent<EventTrigger>() ?? targetBtn.gameObject.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener((data) => OnPointerEnterButton(itemIndex));
                trigger.triggers.Add(entry);

                // 3. 클릭 리스너 (인스펙터에서 연결했다면 생략 가능하지만, 코드 제어를 원한다면 여기서)
                // 예: if (item.id == "Start") item.button.onClick.AddListener(OnBtnStartGame);
                // 현재 구조는 인스펙터 onClick 사용을 가정합니다.
            }
        }

        // --- ISceneUiModule Implementation ---
        public void OnUiShown()
        {
            _isInputActive = true;
            _isBusy = false;

            RefreshSaveDataState(); // 세이브 데이터 유무에 따른 버튼 상태 갱신

            // 초기 포커스 설정 (가능한 첫 번째 버튼)
            int startIndex = GetNextValidIndex(-1, 1);
            SelectButton(startIndex);

            if (arrow) arrow.gameObject.SetActive(true);
        }

        public void OnUiHidden()
        {
            _isInputActive = false;
            if (arrow) arrow.gameObject.SetActive(false);
            ResetVisuals();
        }
        // -------------------------------------

        private void Update()
        {
            // 옵션 창이 열려있거나 입력 비활성 상태면 무시
            if (!_isInputActive || _isBusy || (optionPanel != null && optionPanel.IsShow)) return;

            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                Navigate(-1);
            }
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                Navigate(1);
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                PressCurrentButton();
            }
        }

        private void Navigate(int direction)
        {
            int nextIndex = GetNextValidIndex(_currentIndex, direction);

            // 변경사항이 있을 때만 갱신
            if (nextIndex != -1 && nextIndex != _currentIndex)
            {
                SelectButton(nextIndex);
            }
        }

        // 방향(direction)으로 탐색하여 클릭 가능한 다음 버튼 인덱스 반환
        private int GetNextValidIndex(int startIdx, int direction)
        {
            if (menuItems.Count == 0) return -1;

            int current = startIdx;
            // 리스트 크기만큼만 반복해서 무한루프 방지
            for (int i = 0; i < menuItems.Count; i++)
            {
                current += direction;

                // 범위 체크 (Wrap 방지: 끝에 도달하면 멈춤)
                // Wrap을 원하면: current = (current + menuItems.Count) % menuItems.Count;
                if (current < 0 || current >= menuItems.Count)
                    return startIdx; // 더 이상 갈 곳이 없으면 제자리

                if (IsButtonInteractable(current))
                {
                    return current;
                }
            }
            return startIdx;
        }

        private bool IsButtonInteractable(int index)
        {
            if (index < 0 || index >= menuItems.Count) return false;
            var item = menuItems[index];
            return item.button != null && item.button.gameObject.activeInHierarchy && item.button.interactable;
        }

        private void SelectButton(int index)
        {
            if (index < 0 || index >= menuItems.Count) return;

            // 이전 선택 해제 효과 (필요시)
            // ResetVisuals(); // 전체 리셋보다는 최적화 가능하지만, 안전하게 전체 리셋 사용

            _currentIndex = index;
            UpdateVisuals();
        }

        private void PressCurrentButton()
        {
            if (_isBusy) return;

            if (_currentIndex >= 0 && _currentIndex < menuItems.Count)
            {
                var btn = menuItems[_currentIndex].button;
                if (btn.interactable) btn.onClick.Invoke();
            }
        }

        private void OnPointerEnterButton(int index)
        {
            if (!_isInputActive || _isBusy || (optionPanel && optionPanel.IsShow)) return;
            if (IsButtonInteractable(index))
            {
                SelectButton(index);
            }
        }

        // 세이브 데이터 상태에 따라 버튼 활성/비활성 처리
        private void RefreshSaveDataState()
        {
            bool hasSave = HasAnySaveData();

            foreach (var item in menuItems)
            {
                if (item.requiresSaveData)
                {
                    // 로직상 인터랙터블 설정
                    item.button.interactable = hasSave;

                    // 시각적 처리 (CanvasGroup)
                    var cg = item.button.GetComponent<CanvasGroup>();
                    if (!cg) cg = item.button.gameObject.AddComponent<CanvasGroup>();

                    cg.alpha = hasSave ? 1f : disabledAlpha;
                    cg.blocksRaycasts = hasSave; // 마우스 클릭 방지
                }
            }
        }

        private void UpdateVisuals()
        {
            // 1. 화살표 이동
            if (arrow && _currentIndex >= 0 && _currentIndex < menuItems.Count)
            {
                var targetRect = menuItems[_currentIndex].button.transform as RectTransform;
                arrow.SetParent(targetRect.parent, true); // worldPositionStays=true
                // 앵커와 피벗을 타겟과 맞추거나, 단순 위치 이동
                arrow.position = targetRect.position + (Vector3)arrowOffset;
                // 필요시 SetAsLastSibling 등으로 렌더링 순서 조정
            }

            // 2. 텍스트 색상 변경
            foreach (var kv in _originalTextColors)
            {
                // 선택된 버튼의 텍스트인지 확인
                bool isSelected = IsTextChildOfSelectedButton(kv.Key);
                kv.Key.color = isSelected ? focusTextColor : kv.Value;
            }
        }

        private bool IsTextChildOfSelectedButton(Text t)
        {
            if (_currentIndex < 0 || _currentIndex >= menuItems.Count) return false;
            return t.transform.IsChildOf(menuItems[_currentIndex].button.transform);
        }

        private void ResetVisuals()
        {
            foreach (var kv in _originalTextColors)
            {
                if (kv.Key) kv.Key.color = kv.Value;
            }
        }

        private bool HasAnySaveData()
        {
            // 추후 실제 데이터 매니저 연결
            return simulateHasSaveData;
        }

        // --- 버튼 이벤트 연결용 (인스펙터에서 사용) ---
        public void OnBtnStartGame()
        {
            if (_isBusy) return; // 이미 실행 중이면 무시
            _isBusy = true;      // 실행 시작 잠금

            GameResetter.ResetAll(deleteSaves: true);
            Shared.SceneTransitionManager.FadeToScene("ExplorationScene");
        }

        public void OnBtnQuitGame()
        {
            if (_isBusy) return; // 이미 실행 중이면 무시
            _isBusy = true;      // 실행 시작 잠금

            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
