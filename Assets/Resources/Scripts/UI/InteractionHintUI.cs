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
    [SerializeField] Vector2 surveyOffset = new Vector2(60f, 0f); // F는 오른쪽

    [Header("Communication")]
    [SerializeField] GameObject commRoot;
    [SerializeField] Text commLabel;
    [SerializeField] RectTransform commRect;
    [SerializeField] Vector2 commOffset = new Vector2(0f, 80f);  // E는 위쪽

    RectTransform rootRect;
    Canvas rootCanvas;

    void Awake()
    {
        if (!cam) cam = Shared.MapManager.playerPrefab.GetComponentInChildren<Camera>();
        rootRect = transform as RectTransform;
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        HideAll();
        Shared.interactionHintUI = this; // 싱글톤 접근
    }

    void LateUpdate()
    {
        if (!group || group.alpha <= 0f || !followTarget || !cam) return;

        var sp = cam.WorldToScreenPoint(followTarget.position);
        var rt = transform as RectTransform;

        // 각 키 UI를 개별 오프셋으로 배치
        if (surveyRoot && surveyRoot.activeSelf && surveyRect)
            surveyRect.anchoredPosition = surveyOffset;

        if (commRoot && commRoot.activeSelf && commRect)
            commRect.anchoredPosition = commOffset;
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
    public void ShowBoth()
    {
        ShowSurvey();
        ShowCommunication();
    }
    public void HideSurvey() { if (surveyRoot) surveyRoot.SetActive(false); TryHideRoot(); }
    public void HideCommunication() { if (commRoot) commRoot.SetActive(false); TryHideRoot(); }
    public void HideAll()
    {
        if (surveyRoot) surveyRoot.SetActive(false);
        if (commRoot) commRoot.SetActive(false);
        if (group) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
    }

    void ShowRoot()
    {
        if (!group) return;
        group.alpha = 1f; group.blocksRaycasts = false; group.interactable = false;
    }
    void TryHideRoot()
    {
        if (!group) return;
        bool any = (surveyRoot && surveyRoot.activeSelf) || (commRoot && commRoot.activeSelf);
        if (!any) HideAll();
    }
}
