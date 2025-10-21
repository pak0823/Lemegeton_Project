using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

namespace Project.UI
{
    public class TitleMenuUI : MonoBehaviour, ISceneUiModule
    {
        [Header("Wiring")]
        [SerializeField] private Button continueButton; //계속하기
        [SerializeField] private Button startButton;    //처음부터
        [SerializeField] private Button optionButton;   //옵션
        [SerializeField] private Button exitButton;     //종료

        [Header("Keyboard Navigation")]
        [SerializeField] private RectTransform arrow;          // 화살표 이미지(선택 표시용)
        [SerializeField] private Vector2 arrowOffset = new Vector2(-40f, 0f); // 버튼 기준 좌측 오프셋
        [Tooltip("데이터 시스템 도입 전까지 임시로 저장 유무를 가정하는 스위치")]
        [SerializeField] private bool simulateHasSaveData = false;
        [Tooltip("Up/W, Down/S, Enter(Return/Space) 입력 활성화")]
        [SerializeField] private bool enableKeyboardControl = true;
        [Header("Visual")]
        [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.5f; // 저장 없음일 때 '계속하기' 투명도

        public OptionsMenuUI optionPanel;

        private Button[] _order;   // 선택 순서: [계속하기, 처음부터, 설정, 종료하기]
        private int _index;         // 현재 선택 인덱스
        private bool _shown;        // 메뉴 표시 상태(라이프사이클)


        public void OnUiShown()
        {
            // 필요 시 포커스/애니메이션/사운드 트리거 등을 여기에
            _shown = true;
            EnsureOrder();
            var hasSave = HasAnySaveData();
            UpdateContinueAvailability(hasSave);             // 초기 포커스: 저장 데이터 있으면 '계속하기(0)', 없으면 '처음부터(1)'
            _index = (hasSave && continueButton && continueButton.interactable) ? 0 : 1;
            RefreshArrow();
        }
        public void OnUiHidden() 
        {
            // 메뉴 닫힐 때 정리
            _shown = false;
            if (arrow) arrow.gameObject.SetActive(false);
        }
        public void OnBtnStartGame()
        {
            // 모든 데이터/상태 초기화
            Project.GameResetter.ResetAll(deleteSaves: true);
            //  └ 새 게임이지만 세이브는 지우고 싶지 않다면 false로

            Shared.SceneTransitionManager.FadeToScene("ExplorationScene");
            Debug.Log("인게임 씬으로 이동");
        }

        public void OnBtnQuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Awake()
        {
            if (startButton) startButton.onClick.AddListener(OnBtnStartGame);
            if (exitButton) exitButton.onClick.AddListener(OnBtnQuitGame);
            EnsureOrder();
        }

        private void Update()
        {
            if (!_shown || !enableKeyboardControl || optionPanel.IsShow) return;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                Move(-1);
            }
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                Move(+1);
            }
            // Enter/Return/Space : 현재 선택 버튼 실행
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Return))
            {
                Activate();
            }
        }
        private void Move(int delta)
        {
            EnsureOrder();
            if (_order.Length == 0) return;

            int last = _order.Length - 1;
            int next = _index + delta;

            // 래핑 금지: 경계 넘으면 이동하지 않음
            if (next < 0 || next > last)
            {
                RefreshArrow();
                return;
            }

            // 저장 없으면 '계속하기' 스킵 (경계 밖으로 벗어나지 않게 한 번만 보정)
            if (!HasAnySaveData() && _order[next] == continueButton)
            {
                int alt = next + delta; // 같은 방향으로 한 칸 더
                if (alt < 0 || alt > last) // 더 갈 곳이 없으면 이동 취소
                {
                    RefreshArrow();
                    return;
                }
                next = alt;
                // alt도 우연히 계속하기일 가능성은 현재 배열 순서상 없음(연속 배치가 아니므로)
            }

            // 유효성(비활성/숨김) 체크도 겸사겸사
            var target = _order[next];
            if (!target || !target.gameObject.activeInHierarchy || !target.interactable)
            {
                RefreshArrow();
                return; // 갈 곳이 유효하지 않으면 이동 취소
            }

            // 최종 반영
            _index = next;
            RefreshArrow();
        }
        private void Activate()
        {
            EnsureOrder();
            if (_order.Length == 0) return;
            var target = _order[_index];
            if (!target || !target.gameObject.activeInHierarchy || !target.interactable) return;
            target.onClick?.Invoke();
        }

        private void RefreshArrow()
        {
            EnsureOrder();
            if (!arrow || _order.Length == 0) return;
            var target = _order[_index];
            if (!target) { arrow.gameObject.SetActive(false); return; }

            // 화살표를 선택된 버튼 좌측으로 위치
            var tr = target.transform as RectTransform;
            var arrowRt = arrow;
            if (!tr || !arrowRt) return;

            if (!arrowRt.gameObject.activeSelf) arrowRt.gameObject.SetActive(true);
            // 같은 Canvas 기준 좌표로 맞추기
            arrowRt.SetParent(tr.parent, worldPositionStays:false);
            arrowRt.anchorMin = tr.anchorMin;
            arrowRt.anchorMax = tr.anchorMax;
            arrowRt.anchoredPosition = tr.anchoredPosition + arrowOffset;
            arrowRt.SetAsLastSibling(); // z 오더 보정(선택)
        }

        private void UpdateContinueAvailability(bool hasSave)
        {
            if (!continueButton) return;
            // 1) 논리 비활성
            continueButton.interactable = hasSave;

            // 2) 시각 디밋(반투명): CanvasGroup을 버튼 루트에 붙여 전체 텍스트/아이콘 포함 적용
            var cg = continueButton.GetComponent<CanvasGroup>();
            if (!cg) cg = continueButton.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = hasSave ? 1f : disabledAlpha;
            // 상호작용은 Button.interactable에 맡기되, 오입력 방지를 원하면 blocksRaycasts도 동기화
            cg.blocksRaycasts = hasSave;
            cg.interactable = hasSave;

            // 현재 선택이 '계속하기'인데 비활성화되었다면 선택을 유효 항목으로 옮김
            if (!hasSave && _order != null && _order.Length > 0 && _order[_index] == continueButton)
            {
                // 래핑 금지 모드에 맞춰 안전하게 옮김
                int last = _order.Length - 1;
                // 아래로 먼저 시도
                int down = Mathf.Clamp(_index + 1, 0, last);
                if (_order[down] == continueButton) down = Mathf.Clamp(down + 1, 0, last);
                // 위로 대안
                int up = Mathf.Clamp(_index - 1, 0, last);
                                if (_order[up] == continueButton) up = Mathf.Clamp(up - 1, 0, last);
                
                                // 가능한 쪽으로 이동, 둘 다 불가하면 제자리
                                if (down != _index && _order[down] != continueButton) _index = down;
                                else if (up != _index && _order[up] != continueButton) _index = up;
                                // 화살표 갱신
                RefreshArrow();
            }
        }

        private void EnsureOrder()
        {
            if (_order != null && _order.Length == 4) return;
            _order = new[]
            {
                continueButton,  // 0: 계속하기
                startButton,     // 1: 처음부터
                optionButton,    // 2: 설정
                exitButton       // 3: 종료하기
            };
        }

        // 데이터 시스템 도입 전 임시 판정 로직
        private bool HasAnySaveData()
        {
            return simulateHasSaveData;
        }

    }
}
