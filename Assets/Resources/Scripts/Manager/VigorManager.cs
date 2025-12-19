using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public enum VigorSpendReason
{
    MoveTile,
    InspectBox,
    TriggerTrap,
    PushBox
}

public class VigorManager : MonoBehaviour
{
    [Header("Vigor")]
    public int maxVigor = 30;
    [SerializeField] private int currentVigor;

    [Header("Costs")]
    public int costMovePerTile = 2; //타일 이동 1칸당 활기 소모값
    public int costInspectBox = 1;  //상자 조사 1개당 활기 소모값
    public int costTriggerTrap = 5; //함정 발동 1개당 활기 소모값
    public int costPushBoxPerTile = 3;  //상자 밀기 1칸당 활기 소모값

    [Header("UI (optional)")]
    [SerializeField] private Text vigorText;

    [Header("Fail Handling")]
    public UnityEvent onExplorationFailed;

    [Header("Fail Popup (temporary)")]
    [SerializeField] private string titleSceneName = "TitleScene";
    private GameObject _failPopupInstance;

    public int CurrentVigor => currentVigor;
    public bool IsDepleted => currentVigor <= 0;

    void Awake()
    {
        Shared.VigorManager = this;

        // 기본은 풀로 시작(첫 탐험 진입)
        currentVigor = Mathf.Clamp(maxVigor, 0, int.MaxValue);
        RefreshUI();

        RefreshUI();
    }

    public bool CanSpend(int amount) => amount <= 0 || currentVigor >= amount;

    public bool TrySpend(int amount, VigorSpendReason reason)
    {
        if (amount <= 0) return true;
        if (currentVigor < amount) return false;

        currentVigor -= amount;
        RefreshUI();

        if (currentVigor <= 0)
        {
            FailExploration($"활기가 소진되었습니다. (사유: {reason})");
        }

        var playerMovement = Shared.PlayerMovement;

        if (playerMovement != null)
        {
            var pos = playerMovement.transform.position + Vector3.up * 0.3f;
            FloatingTextManager.Instance?.Spawn(pos, $"-{amount}");
        }

        return true;
    }

    public void FailExploration(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Shared.explorationLogUI?.Push(message);

        Shared.PlayerMovement?.HaltImmediately();
        Shared.PlayerMovement?.LockMovementIndefinite();

        ShowFailPopupTemporary();

        onExplorationFailed?.Invoke();
    }

    public void SetMaxAndFill(int newMax)
    {
        maxVigor = Mathf.Max(0, newMax);
        currentVigor = maxVigor;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (vigorText != null)
            vigorText.text = $"활기: {currentVigor}/{maxVigor}";
    }
    public void SetCurrentVigor(int value, bool refresh = true)
    {
        currentVigor = Mathf.Clamp(value, 0, maxVigor);
        if (refresh) RefreshUI();
    }

    //(임시 팝업 UI를 런타임 생성) -> 팝업 Ui 프리팹이 생기면 수정해야함
    private void ShowFailPopupTemporary()
    {
        if (_failPopupInstance != null) return;

        // Canvas
        var canvasGO = new GameObject("ExplorationFailPopup_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Dim background
        var dimGO = new GameObject("Dim");
        dimGO.transform.SetParent(canvasGO.transform, false);
        var dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.6f);
        var dimRt = dimGO.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        // Panel
        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(dimGO.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = Color.white;
        var panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(520, 220);
        panelRt.anchoredPosition = Vector2.zero;

        // Message Text
        var textGO = new GameObject("Message");
        textGO.transform.SetParent(panelGO.transform, false);
        var txt = textGO.AddComponent<Text>();
        txt.text = "탐색을 실패했습니다. 타이틀로 이동합니다";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.black;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 24;
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.1f, 0.45f);
        textRt.anchorMax = new Vector2(0.9f, 0.9f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        // Confirm Button
        var btnGO = new GameObject("ConfirmButton");
        btnGO.transform.SetParent(panelGO.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.6f, 0.9f, 1f);
        var btn = btnGO.AddComponent<Button>();
        var btnRt = btnGO.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.35f, 0.1f);
        btnRt.anchorMax = new Vector2(0.65f, 0.3f);
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;

        // Button text
        var btnTextGO = new GameObject("Text");
        btnTextGO.transform.SetParent(btnGO.transform, false);
        var btnTxt = btnTextGO.AddComponent<Text>();
        btnTxt.text = "확인";
        btnTxt.alignment = TextAnchor.MiddleCenter;
        btnTxt.color = Color.white;
        btnTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        btnTxt.fontSize = 22;
        var btnTextRt = btnTextGO.GetComponent<RectTransform>();
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;

        // Click: go to title
        btn.onClick.AddListener(() =>
        {
            // (선택) 이동 잠금 토큰 해제는 타이틀에서 PlayerMovement가 없으니 생략 가능
            if (Shared.SceneTransitionManager != null)
                Shared.SceneTransitionManager.FadeToScene(titleSceneName);
            else
                SceneManager.LoadScene(titleSceneName);
        });

        _failPopupInstance = canvasGO;
    }
}
