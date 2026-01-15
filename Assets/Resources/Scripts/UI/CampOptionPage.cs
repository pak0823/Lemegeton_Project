using Project.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class CampOptionPage : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button resumeButton; // 게임으로 돌아가기 (캠프 닫기)
    [SerializeField] private Button titleButton;  // 타이틀 화면으로
    [SerializeField] private Button quitButton;   // 게임 종료

    [Header("Focus Container")]
    [SerializeField] private RectTransform buttonsContainer; // 버튼들이 모여있는 부모 (ButtonList)
    [SerializeField] private bool autoCollect = true;        // 자식 버튼 자동 수집 여부
    [SerializeField] private bool excludeResumeFromFocus = false; // "돌아가기" 버튼을 포커스에서 뺄지 여부

    [Header("Focus Arrow")]
    [SerializeField] private RectTransform focusArrow;              // 화살표 이미지
    [SerializeField] private Vector2 arrowOffset = new Vector2(-40f, 0f); // 화살표 위치 오프셋

    [Header("Key bindings")]
    private KeyCode upKey = KeyCode.W;
    private KeyCode downKey = KeyCode.S;
    private KeyCode submitKey = KeyCode.E;      // 선택
    private KeyCode submit_V2Key = KeyCode.Return;
    private KeyCode cancelKey = KeyCode.Q;      // 취소(뒤로가기)

    private readonly List<Button> focusButtons = new List<Button>();
    private int focusIndex = 0;

    // 이 컴포넌트가 켜질 때마다(탭이 선택될 때마다) 실행됨
    private void OnEnable()
    {
        // 1. 버튼 목록 갱신
        RebuildFocusList();

        // 2. 레이아웃 안정화 후 포커스 초기화
        StartCoroutine(ReinitFocusNextFrame());
    }

    private void OnDisable()
    {
        // 페이지 꺼질 때 화살표도 숨김
        if (focusArrow) focusArrow.gameObject.SetActive(false);
    }

    private void Start()
    {
        // 버튼 이벤트 연결
        if (resumeButton)
            resumeButton.onClick.AddListener(OnBtnResume);

        if (titleButton)
            titleButton.onClick.AddListener(OnBtnReturnTitle);

        if (quitButton)
            quitButton.onClick.AddListener(OnBtnQuitGame);

        // 화살표 부모 설정 (안전장치)
        if (focusArrow && buttonsContainer && focusArrow.parent != buttonsContainer)
            focusArrow.SetParent(buttonsContainer, worldPositionStays: false);
    }

    private void Update()
    {
        // 페이지가 켜져 있을 때만 입력 받음
        // (CampUIManager가 켜져 있어도 다른 탭이면 이 오브젝트는 비활성 상태라 Update 안 돔 -> 안전함)

        // 위아래 이동
        if (Input.GetKeyDown(upKey)) MoveFocus(-1);
        if (Input.GetKeyDown(downKey)) MoveFocus(+1);

        // 선택 (E / Enter)
        if (Input.GetKeyDown(submitKey) || Input.GetKeyDown(submit_V2Key))
        {
            var b = GetFocusedButton();
            if (b != null && b.interactable) b.onClick.Invoke();
        }

        // 취소 (Q) -> 캠프 메뉴 닫기
        if (Input.GetKeyDown(cancelKey))
        {
            OnBtnResume();
        }
    }

    // --- 기능 구현 ---

    public void OnBtnResume()
    {
        // "돌아가기" 누르면 캠프 창 자체를 닫음
        if (CampUIManager.Instance != null)
            CampUIManager.Instance.Hide();
    }

    public void OnBtnReturnTitle()
    {
        // 타이틀로 이동
        // 일시정지 해제는 CampUIManager가 닫힐 때 하거나, 씬 전환 시 풀리도록 처리
        Shared.GameSpeedController?.ReleasePause();

        // 캠프 UI 강제 닫기 (상태 초기화)
        if (CampUIManager.Instance != null)
            CampUIManager.Instance.Hide();

        Shared.SceneTransitionManager.FadeToScene("TitleScene");
    }

    public void OnBtnQuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // --- 포커스 로직 (기존 유지) ---

    void RebuildFocusList()
    {
        focusButtons.Clear();
        if (!buttonsContainer) return;

        if (autoCollect)
        {
            var found = buttonsContainer.GetComponentsInChildren<Button>(false) // 활성 객체만
                        .Where(b => b != null && b.interactable)
                        .OrderBy(b => b.transform.GetSiblingIndex()) // 순서대로 정렬
                        .ToList();

            if (excludeResumeFromFocus && resumeButton)
                found.Remove(resumeButton);

            focusButtons.AddRange(found);
        }

        // 인덱스 초기화
        focusIndex = 0;
    }

    IEnumerator ReinitFocusNextFrame()
    {
        yield return null; // 한 프레임 대기 (UI 레이아웃 갱신 대기)
        RebuildFocusList();

        if (focusButtons.Count > 0)
        {
            SetFocus(0);
        }
        else
        {
            if (focusArrow) focusArrow.gameObject.SetActive(false);
        }
    }

    void MoveFocus(int delta)
    {
        if (focusButtons.Count == 0) return;

        int newIndex = Mathf.Clamp(focusIndex + delta, 0, focusButtons.Count - 1);
        if (newIndex != focusIndex)
        {
            SetFocus(newIndex);
        }
    }

    void SetFocus(int index)
    {
        focusIndex = index;
        UpdateArrowPosition();
    }

    Button GetFocusedButton()
    {
        if (focusButtons.Count == 0) return null;
        return focusButtons[focusIndex];
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

        // 화살표 켜고 위치 이동
        if (!focusArrow.gameObject.activeSelf)
            focusArrow.gameObject.SetActive(true);

        var targetRect = targetBtn.transform as RectTransform;
        focusArrow.anchoredPosition = targetRect.anchoredPosition + arrowOffset;
    }
}