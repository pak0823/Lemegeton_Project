using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }
    [SerializeField] private Canvas popupCanvas; // 씬의 공용 Canvas를 드래그해둘 수 있음
    [SerializeField] private RectTransform popupChildParent; // Canvas 아래 전용 레이어(자식)에 붙일 경우 지정
    [SerializeField] private RetreatConfirmPopup retreatPrefab;
    [SerializeField] private ConfirmationPopup confirmationPrefab;


    public static int ModalDepth { get; private set; } // 모달 중첩
    public static bool IsModalOpen => ModalDepth > 0;   // 모달 여부
    readonly Queue<(string msg,string ok, string cancel,
                    TaskCompletionSource<bool> tcs)> queue
        = new Queue<(string, string, string, TaskCompletionSource<bool>)>();

    ConfirmationPopup _active;
    bool _showing;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureEventSystem();
        EnsureCanvas(); // popupCanvas를 보장
    }

    public Task<bool> ConfirmAsync(string message, string ok = "확인", string cancel = "취소")
    {
        var tcs = new TaskCompletionSource<bool>();
        queue.Enqueue((message, ok, cancel, tcs));
        _ = TryDequeueAndShow();
        return tcs.Task;
    }
    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }
    }

    void EnsureCanvas()
    {
        // 이미 배정되어 있으면 최상단 Overlay로 보정하고, 자식 레이어가 없으면 만든다.
        if (popupCanvas != null)
        {
            if (popupCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (popupCanvas.sortingOrder < 5000)
                popupCanvas.sortingOrder = 5000;

            if (popupChildParent == null)
            {
                var layer = new GameObject("PopupLayer", typeof(RectTransform));
                layer.transform.SetParent(popupCanvas.transform, false);
                popupChildParent = layer.GetComponent<RectTransform>();
                popupChildParent.anchorMin = Vector2.zero;
                popupChildParent.anchorMax = Vector2.one;
                popupChildParent.offsetMin = Vector2.zero;
                popupChildParent.offsetMax = Vector2.zero;
            }
            return;
        }

        // 전용 Overlay Canvas를 생성(DDOL)
        var go = new GameObject("PopupCanvas(DDOL)");
        DontDestroyOnLoad(go);
        popupCanvas = go.AddComponent<Canvas>();
        popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        popupCanvas.sortingOrder = 5000; // 최상위

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        // 전용 자식 레이어 생성
        var childLayer = new GameObject("PopupLayer", typeof(RectTransform));
        childLayer.transform.SetParent(popupCanvas.transform, false);
        popupChildParent = childLayer.GetComponent<RectTransform>();
        popupChildParent.anchorMin = Vector2.zero;
        popupChildParent.anchorMax = Vector2.one;
        popupChildParent.offsetMin = Vector2.zero;
        popupChildParent.offsetMax = Vector2.zero;
    }

    async Task TryDequeueAndShow()
    {
        if (_showing) return;
        _showing = true;

        while (queue.Count > 0)
        {
            var (msg, ok, cancel, tcs) = queue.Dequeue();

            if (_active == null)
            {
                // 부모 확보(전용 레이어가 있으면 그 아래, 없으면 Canvas 루트)
                EnsureCanvas();
                var parent = (popupChildParent != null)
                                    ? popupChildParent
                                    : (popupCanvas.transform as RectTransform);
                
                _active = Instantiate(confirmationPrefab, parent); // 전용 레이어/Canvas 아래에 생성
                (_active.transform as RectTransform).SetAsLastSibling(); // 항상 맨 위로
                
                                // 방어적 활성화 및 정렬 보장
                                if (!_active.gameObject.activeInHierarchy)
                    _active.gameObject.SetActive(true);
                var c = _active.GetComponentInParent<Canvas>();
                                if (c != null && c.sortingOrder < 5000)
                    c.sortingOrder = 5000;
            }

            bool showCancel = !string.IsNullOrEmpty(cancel);  // cancel이 비면 OK-only
            // 모달 진입/해제
            ModalDepth++;
            bool result = await _active.Show(msg, ok, cancel, showCancel); // 팝업 종료까지 대기
            ModalDepth--;
            tcs.TrySetResult(result);
        }

        _showing = false;
    }

    public async Task<bool> ConfirmRetreatAsync(string message, float successChance01)    //탈출 전용 팝업
    {
        EnsureCanvas();
        var parent = (popupChildParent != null)
                   ? popupChildParent
                   : (popupCanvas.transform as RectTransform);

        var inst = Instantiate(retreatPrefab, parent);
        (inst.transform as RectTransform).SetAsLastSibling();
        if (!inst.gameObject.activeInHierarchy) inst.gameObject.SetActive(true);

        // 모달 진입/해제
        ModalDepth++;
        bool ok = await inst.ShowAsync(message, successChance01);
        ModalDepth--;
        Destroy(inst.gameObject);
        return ok;
    }
}
