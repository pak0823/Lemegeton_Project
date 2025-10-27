using UnityEngine;
using UnityEngine.UI;

public class InteractionHintUI : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] Transform followTarget;   // 보통 Player
    [SerializeField] Camera cam;

    [Header("Root")]
    [SerializeField] CanvasGroup group;

    [Header("Survey")]
    [SerializeField] GameObject surveyRoot;
    [SerializeField] RectTransform surveyRect;
    [SerializeField] Text surveyLabel;
    [SerializeField] Vector2 defaultSurveyOffset = new Vector2(60f, 0f); // 기본 힌트 위치 F
    Vector2 _surveyOffset;   // 현재 적용중

    [Header("Communication")]
    [SerializeField] GameObject commRoot;
    [SerializeField] Text commLabel;
    [SerializeField] RectTransform commRect;
    [SerializeField] Vector2 defaultCommOffset = new Vector2(0f, 80f);  // 기본 힌트 위치 E
    Vector2 _commOffset;     // 현재 적용중

    [Header("Cancel")]
    [SerializeField] GameObject cancelRoot;
    [SerializeField] Text cancelLabel;
    [SerializeField] RectTransform cancelRect;
    [SerializeField] Vector2 defaultCancelOffset = new Vector2(60f, 0f); // 기본 힌트 위치 F
    Vector2 _cancelOffset;   // 현재 적용중

    RectTransform rootRect;
    Canvas rootCanvas;

    public void ShowSurveyAt(Transform t) { BindFollow(t); SetOffsetsFrom(t); ShowSurvey(); }
    public void ShowCommunicationAt(Transform t) { BindFollow(t); SetOffsetsFrom(t); ShowCommunication(); }
    public void ShowCancelAt(Transform t) { BindFollow(t);SetOffsetsFrom(t); ShowCancel(); }
    public void ShowBothAt(Transform t) { BindFollow(t); SetOffsetsFrom(t); ShowBoth(); }

    void Awake()
    {
        rootRect = transform as RectTransform;
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (!cam) cam = FindAnyObjectByType<Camera>();
        HideAll();
        Shared.interactionHintUI = this; // 싱글톤 접근
        _surveyOffset = defaultSurveyOffset;
        _commOffset = defaultCommOffset;
        _cancelOffset = defaultCancelOffset;
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
                rootRect.anchoredPosition = lp;
            }
        }

        // 키별 오프셋은 기존처럼 유지
        if (surveyRoot && surveyRoot.activeSelf && surveyRect) surveyRect.anchoredPosition = _surveyOffset;
        if (commRoot && commRoot.activeSelf && commRect) commRect.anchoredPosition = _commOffset;
        if (cancelRoot && cancelRoot.activeSelf && cancelRect) cancelRect.anchoredPosition = _cancelOffset;
    }
    void SetOffsetsFrom(Transform t)
    {
        // 우선 개체의 HintAnchor가 있으면 그것을 사용
        var anchor = t ? t.GetComponent<HintAnchor>() : null;
        if (anchor)
        {
            _surveyOffset = anchor.surveyOffset;
            _commOffset = anchor.commOffset;
            _cancelOffset = anchor.cancelOffset;    //surveyOffset과 같은 위치를 사용하기에 그대로 사용
            return;
        }
        // 없으면 기본값
        _surveyOffset = defaultSurveyOffset;
        _commOffset = defaultCommOffset;
        _cancelOffset = defaultCancelOffset;
    }

    public void SetOffsets(Vector2? survey = null, Vector2? comm = null)
    {
        _surveyOffset = survey ?? defaultSurveyOffset;
        _commOffset = comm ?? defaultCommOffset;
        _cancelOffset = survey ?? defaultCancelOffset;
    }

    void ResetOffsets()
    {
        _surveyOffset = defaultSurveyOffset;
        _commOffset = defaultCommOffset;
        _cancelOffset = defaultCancelOffset;
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
        if (commLabel) commLabel.text = "소통";
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
        ShowSurvey();
        ShowCommunication();
    }

    public void HideBoth()
    {
        HideSurvey();
        HideCommunication();
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
        group.alpha = 1f; group.blocksRaycasts = false; group.interactable = false;
    }
    void TryHideRoot()
    {
        if (!group) return;
        bool any = (surveyRoot && surveyRoot.activeSelf) || (commRoot && commRoot.activeSelf) || (cancelRoot && cancelRoot.activeSelf);
        if (!any) HideAll();
    }
}
