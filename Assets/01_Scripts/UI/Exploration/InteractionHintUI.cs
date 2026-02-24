using UnityEngine;

using UnityEngine.UI;



public class InteractionHintUI : MonoBehaviour

{

    public static InteractionHintUI Instance { get; private set; }



    [Header("Follow")]

    [SerializeField] Transform followTarget;   // 보통 Player

    [SerializeField] Camera cam;



    [Header("Root")]

    [SerializeField] CanvasGroup group;



    [Header("UI Offset")]
    [SerializeField] Vector2 defaultOffset = new Vector2(90f, 35f); // 기본 전체 힌트 위치
    Vector2 _currentOffset;   // 현재 적용중

    [Header("Survey")]
    [SerializeField] GameObject surveyRoot;
    [SerializeField] RectTransform surveyRect;
    [SerializeField] Text surveyLabel;

    [Header("Communication")]
    [SerializeField] GameObject commRoot;
    [SerializeField] Text commLabel;
    [SerializeField] RectTransform commRect;

    [Header("Cancel")]
    [SerializeField] GameObject cancelRoot;
    [SerializeField] Text cancelLabel;
    [SerializeField] RectTransform cancelRect;



    RectTransform rootRect;

    Canvas rootCanvas;



    public void ShowSurveyAt(Transform t) { BindFollow(t); SetOffsetsFrom(t); ShowSurvey(); }

    public void ShowSurveyAt(Transform t, string label)

    {

        BindFollow(t);

        SetOffsetsFrom(t);



        if (surveyLabel) surveyLabel.text = label;

        if (surveyRoot) surveyRoot.SetActive(true);



        // 필요 시 기존 comm/cancel 상태는 호출하는 쪽에서 제어

        ShowRoot();

    }



    public void ShowCommunicationAt(Transform t) { BindFollow(t); SetOffsetsFrom(t); ShowCommunication(); }

    public void ShowCommunicationAt(Transform t, string label)

    {

        BindFollow(t);

        SetOffsetsFrom(t);



        if (commLabel) commLabel.text = label;

        if (commRoot) commRoot.SetActive(true);



        ShowRoot();

    }

    public void ShowCancelAt(Transform t) { BindFollow(t);SetOffsetsFrom(t); ShowCancel(); }

    public void ShowBothAt(Transform t) { BindFollow(t); SetOffsetsFrom(t); ShowBoth(); }
    public void ShowBothAt(Transform t, string label)
    {
        BindFollow(t);
        SetOffsetsFrom(t);

        if (surveyLabel) surveyLabel.text = label;
        ShowSurvey();
        ShowCommunication();
    }



    void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);

        rootRect = transform as RectTransform;
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (!cam) cam = Camera.main;

        if (defaultOffset == Vector2.zero)
        {
            defaultOffset = new Vector2(90f, 35f);
        }

        HideAll();
        _currentOffset = defaultOffset;
    }



    void LateUpdate()

    {

        if (!group || group.alpha <= 0f || !followTarget || !cam) return;



        var sp = cam.WorldToScreenPoint(followTarget.position);



        if (!rootRect) rootRect = transform as RectTransform;

        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;



        if (rootCanvas && rootRect)

        {

            if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)

            {

                // 오버레이: 스크린 좌표 그대로

                rootRect.position = sp;
                rootRect.anchoredPosition += _currentOffset;

            }

            else

            {

                // 카메라/월드 스페이스: 로컬 앵커 좌표로 변환

                RectTransform canvasRect = rootCanvas.transform as RectTransform;

                Vector2 lp;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(

                    canvasRect, sp,

                    rootCanvas.renderMode == RenderMode.ScreenSpaceCamera ? rootCanvas.worldCamera : cam,

                    out lp);

                rootRect.anchoredPosition = lp + _currentOffset;

            }

        }

    }

    void SetOffsetsFrom(Transform t)
    {
        // 우선 개체의 HintAnchor가 있으면 그것을 사용
        var anchor = t ? t.GetComponent<HintAnchor>() : null;
        if (anchor)
        {
            _currentOffset = anchor.uiOffset;
            return;
        }

        // 없으면 기본값 적용
        _currentOffset = defaultOffset;
    }



    public void SetOffsets(Vector2? offset = null)
    {
        _currentOffset = offset ?? defaultOffset;
    }



    void ResetOffsets()

    {

        _currentOffset = defaultOffset;

    }



    public void BindFollow(Transform t) => followTarget = t;



    public void ShowSurvey()

    {

        if (surveyLabel) surveyLabel.text = "조사";

        if (surveyRoot) surveyRoot.SetActive(true);

        ShowRoot();

    }

    public void ShowCommunication()

    {

        if (commLabel) commLabel.text = "관찰";

        if (commRoot) commRoot.SetActive(true);

        ShowRoot();

    }

    public void ShowCancel()

    {

        if (cancelLabel) cancelLabel.text = "취소";

        if (cancelRoot) cancelRoot.SetActive(true);

        ShowRoot();

    }



    public void ShowBoth()

    {

        ShowSurvey();   //조사

        ShowCommunication();    //관찰

    }



    public void HideBoth()

    {

        HideSurvey();

        HideCommunication();

    }



    public void ShowPushCancelAt(Transform t)

    {

        BindFollow(t);

        SetOffsetsFrom(t);



        // Survey 버튼을 "밀기"로 재사용

        if (surveyLabel) surveyLabel.text = "밀기";

        if (surveyRoot) surveyRoot.SetActive(true);



        // Communication은 숨김(2버튼만)

        if (commRoot) commRoot.SetActive(false);



        // Cancel은 켬

        if (cancelLabel) cancelLabel.text = "취소";

        if (cancelRoot) cancelRoot.SetActive(true);



        ShowRoot();

    }



    public void ResetSurveyLabel()

    {

        if (surveyLabel) surveyLabel.text = "조사";

    }



    public void HideSurvey() { if (surveyRoot) surveyRoot.SetActive(false); TryHideRoot(); }

    public void HideCommunication() { if (commRoot) commRoot.SetActive(false); TryHideRoot(); }

    public void HideCancel() { if (cancelRoot) cancelRoot.SetActive(false);TryHideRoot(); }

    public void HideAll()

    {

        if (surveyRoot) surveyRoot.SetActive(false);

        if (commRoot) commRoot.SetActive(false);

        if (cancelRoot) cancelRoot.SetActive(false);

        if (group) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }

        ResetOffsets();

    }



    void ShowRoot()

    {

        if (!group) return;

        group.alpha = 1f;

        group.blocksRaycasts = true;

        group.interactable = true;

    }

    void TryHideRoot()

    {

        if (!group) return;

        bool any = (surveyRoot && surveyRoot.activeSelf) || (commRoot && commRoot.activeSelf) || (cancelRoot && cancelRoot.activeSelf);

        if (!any) HideAll();

    }



    // === UI 버튼에서 호출할 이벤트 ===

    public void OnClickSurveyButton()

    {

        PlayerMovement.Instance?.OnClickSurveyButton();

    }



    public void OnClickCommunicationButton()

    {

        PlayerMovement.Instance?.OnClickCommunicationButton();

    }



    public void OnClickCancelButton()

    {

        PlayerMovement.Instance?.OnClickCancelButton();

    }

}

