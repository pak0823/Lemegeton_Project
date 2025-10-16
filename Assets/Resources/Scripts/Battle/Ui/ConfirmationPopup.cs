using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmationPopup : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text messageText;
    [SerializeField] private Text okLabel;
    [SerializeField] private Text cancelLabel;
    [SerializeField] private Button btnOk;
    [SerializeField] private Button btnCancel;
    [SerializeField] private Transform window; // 스케일 애니메이션용(선택)

    [Header("Keyboard (optional)")]
    [SerializeField] private bool enableKeyboard = true;
    [SerializeField] private KeyCode keyConfirmA = KeyCode.Return;  // Enter
    [SerializeField] private KeyCode keyConfirmB = KeyCode.E;       // E
    [SerializeField] private KeyCode keyCancelA = KeyCode.Escape;  // Esc
    [SerializeField] private KeyCode keyCancelB = KeyCode.Q;       // Q

    TaskCompletionSource<bool> _tcs;
    bool _showCancel;
    bool _closing;

    void Awake()
    {
        if (btnOk) btnOk.onClick.AddListener(() => Close(true));
        if (btnCancel) btnCancel.onClick.AddListener(() => Close(false));
        HideImmediate();
    }

    void Update()
    {
        if (!enableKeyboard || _tcs == null) return;

        if (Input.GetKeyDown(keyConfirmA) || Input.GetKeyDown(keyConfirmB))
        {
            Close(true);
        }
        else if (_showCancel && (Input.GetKeyDown(keyCancelA) || Input.GetKeyDown(keyCancelB)))
        {
            Close(false);
        }
    }

    public Task<bool> Show(string message, string ok = "확인", string cancel = "취소", bool showCancel = true)
    {
        _closing = false;
        _showCancel = showCancel;
        _tcs = new TaskCompletionSource<bool>();

        if (messageText) messageText.text = message;
        if (okLabel) okLabel.text = ok;
        if (cancelLabel) cancelLabel.text = cancel;

        if (btnCancel) btnCancel.gameObject.SetActive(showCancel);
        gameObject.SetActive(true);
        EnableButtons(true);
        AnimateIn();
        return _tcs.Task;
    }

    /// <summary>OK 전용(정보창) 헬퍼.</summary>
    public Task<bool> ShowOk(string message, string ok = "확인")
    {
        return Show(message, ok, "", false);
    }

    void EnableButtons(bool on)
    {
        if (btnOk) btnOk.interactable = on;
        if (btnCancel) btnCancel.interactable = on && _showCancel;
    }

    void HideImmediate()
    {
        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        if (window) window.localScale = Vector3.one * 0.98f;
        gameObject.SetActive(false);
    }

    void AnimateIn()
    {
        if (!canvasGroup) { canvasGroup = GetComponent<CanvasGroup>(); }
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        if (window) window.localScale = Vector3.one;
    }

    async void Close(bool result)
    {
        if (_closing) return;
        _closing = true;
        EnableButtons(false);

        // 간단 페이드아웃
        if (canvasGroup)
        {
            float t = 0f, dur = 0.12f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(t / dur);
                await Task.Yield();
            }
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        gameObject.SetActive(false);

        _tcs?.TrySetResult(result);
        _tcs = null;
    }
}
