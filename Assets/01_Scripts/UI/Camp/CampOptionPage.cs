using Project.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class CampOptionPage : MonoBehaviour
{
    [Header("Dynamic Generation")]
    [SerializeField] private GameObject buttonPrefab; // 아까 만든 기본 Text 기반 버튼 프리팹
    [SerializeField] private string[] buttonNames =
    {
        "심연으로 돌아가기",
        "탐색 포기",
        "탐색 가이드",
        "그래픽 설정",
        "사운드 설정",
        "시스템 설정",
        "크레딧",
        "게임 종료",
        "타이틀로 돌아가기"
    };

    [Header("Focus Container")]
    [SerializeField] private RectTransform buttonsContainer; // ButtonList 오브젝트       
    [SerializeField] private RectTransform focusArrow;       // 선택 화살표
    [SerializeField] private Vector2 arrowOffset = new Vector2(0f, 0f); 

    [Header("Key bindings")]
    private KeyCode upKey = KeyCode.W;
    private KeyCode downKey = KeyCode.S;
    private KeyCode submitKey = KeyCode.E;      
    private KeyCode submit_V2Key = KeyCode.Return;
    private KeyCode cancelKey = KeyCode.Q;      

    private readonly List<Button> focusButtons = new List<Button>();
    private int focusIndex = 0;

    private void Awake() => GenerateButtons(); // 씬 시작 시 버튼들을 먼저 생성함

    private void OnEnable()
    {
        // 페이지가 켜질 때마다 포커스 리스트 갱신 및 화살표 위치 초기화
        // UI 레이아웃이 계산될 시간을 주기 위해 코루틴으로 실행
        StartCoroutine(ReinitFocusNextFrame());
    }

    private void OnDisable()
    {
        if (focusArrow) focusArrow.gameObject.SetActive(false);

        // 창이 닫힐 때 모든 버튼 하이라이트 초기화
        foreach (var btn in focusButtons)
        {
            SetButtonHighlight(btn, false);
        }
    }

    private void GenerateButtons()
    {
        if (buttonPrefab == null || buttonsContainer == null) return;

        // 중복 생성 방지 기존 자식들 제거 (화살표 제외)
        foreach (Transform child in buttonsContainer)
        {
            if (focusArrow && child == focusArrow) continue;
            Destroy(child.gameObject);
        }

        for (int i = 0; i < buttonNames.Length; i++)
        {
            int index = i; // 람다식 클로저 문제 방지용 로컬 변수
            GameObject btnObj = Instantiate(buttonPrefab, buttonsContainer);
            btnObj.name = $"Btn_{index}_{buttonNames[i]}";

            // 텍스트 설정
            Text t = btnObj.GetComponentInChildren<Text>();
            if (t != null) t.text = buttonNames[i];

            // 클릭 이벤트 연결
            Button b = btnObj.GetComponent<Button>();
            if (b != null)
            {
                // 마우스 클릭 시에도 해당 인덱스로 포커스를 옮긴 후 로직 실행
                b.onClick.AddListener(() => {
                    SetFocus(index); 
                    OnButtonClick(index);
                });
            }
        }

        // 화살표가 항상 가장 위에 보이도록 Hierarchy 순서 조정
        if (focusArrow) focusArrow.SetAsLastSibling();
    }

    private void Update()
    {
        // 위아래 이동
        if (Input.GetKeyDown(upKey)) MoveFocus(-1);
        if (Input.GetKeyDown(downKey)) MoveFocus(+1);

        // 선택 (E / Enter)
        if (Input.GetKeyDown(submitKey) || Input.GetKeyDown(submit_V2Key))
        {
            var b = GetFocusedButton();
            if (b != null && b.interactable) b.onClick.Invoke();
        }

        // 취소 (Q)
        if (Input.GetKeyDown(cancelKey)) OnBtnResume();
    }

    // --- 버튼 클릭 처리 로직 ---
    private void OnButtonClick(int index)
    {
        string btnName = buttonNames[index];
        Debug.Log($"[{btnName}] 버튼이 클릭되었습니다.");

        switch (btnName)
        {
            case "심연으로 돌아가기":
                OnBtnResume();
                break;
            case "게임 종료":
                OnBtnQuitGame();
                break;
            case "타이틀로 돌아가기":
                OnBtnReturnTitle();
                break;
            default:
                // 나머지 기능(그래픽, 사운드 등)은 여기에 추가 구현
                Debug.Log($"{btnName} 기능은 아직 구현되지 않았습니다.");
                break;
        }
    }

    // --- 기존 기능 유지 ---
    public void OnBtnResume()
    {
        if (CampUIManager.Instance != null) CampUIManager.Instance.Hide();
    }

    public void OnBtnReturnTitle()
    {
        GameSpeedController.Instance?.ReleasePause();
        if (CampUIManager.Instance != null) CampUIManager.Instance.Hide();
        SceneTransitionManager.Instance.FadeToScene("TitleScene");
    }

    public void OnBtnQuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // --- 포커스 시스템 로직 ---
    void RebuildFocusList()
    {
        focusButtons.Clear();
        if (!buttonsContainer) return;

        // 생성된 버튼들 중 interactable한 버튼만 수집
        var found = buttonsContainer.GetComponentsInChildren<Button>(false)
                    .OrderBy(b => b.transform.GetSiblingIndex()).ToList();

        focusButtons.AddRange(found);
        //focusIndex = 0;
    }

    IEnumerator ReinitFocusNextFrame()
    {
        // 한 프레임 대기하여 Vertical Layout Group이 자식들 배치를 끝내길 기다림
        yield return null; 
        RebuildFocusList();

        if (focusButtons.Count > 0) SetFocus(0);
        //else if (focusArrow) focusArrow.gameObject.SetActive(false);
    }

    void MoveFocus(int delta)
    {
        if (focusButtons.Count == 0) return;
        int newIndex = Mathf.Clamp(focusIndex + delta, 0, focusButtons.Count - 1);
        if (newIndex != focusIndex) SetFocus(newIndex);
    }

void SetFocus(int index)
{
    // 기존에 포커스된 버튼 하이라이트 끄기
    if (focusButtons.Count > focusIndex) SetButtonHighlight(focusButtons[focusIndex], false);

    focusIndex = index;

    // 새로 포커스된 버튼 하이라이트 켜기
    if (focusButtons.Count > focusIndex)SetButtonHighlight(focusButtons[focusIndex], true);

    UpdateArrowPosition();
}
    // 하이라이트 상태를 변경하는 헬퍼 함수
void SetButtonHighlight(Button btn, bool isVisible)
{
    if (btn == null) return;
    
    // 버튼의 Image 컴포넌트(배경)를 가져와서 알파값이나 활성화 상태 조절
    Image bg = btn.GetComponent<Image>();
    if (bg != null)
    {
        // 포커스되면 흰색(Alpha 1), 안되면 투명(Alpha 0)
        bg.color = isVisible ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0f);
    }
}

    Button GetFocusedButton() => (focusButtons.Count == 0) ? null : focusButtons[focusIndex];

    void UpdateArrowPosition()
    {
        if (!focusArrow || focusButtons.Count == 0) return;
        var targetBtn = GetFocusedButton();
        if (!targetBtn)
        {
            focusArrow.gameObject.SetActive(false);
            return;
        }

        focusArrow.gameObject.SetActive(true);
        RectTransform targetRect = targetBtn.transform as RectTransform;
    
        // 버튼의 네 모서리 월드 좌표를 가져올 배열 (0:왼쪽하단, 1:왼쪽상단, 2:오른쪽상단, 3:오른쪽하단)
        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);

        // 왼쪽 두 모서리의 중간 지점(왼쪽 끝 중앙) 계산
        Vector3 leftCenter = (corners[0] + corners[1]) * 0.5f;

        // 화살표 위치를 버튼의 왼쪽 끝 중앙으로 보냄
        focusArrow.position = leftCenter;

        // arrowOffset은 버튼 끝으로부터 얼마나 떨어질지 결정
        focusArrow.anchoredPosition += arrowOffset;
    }
}