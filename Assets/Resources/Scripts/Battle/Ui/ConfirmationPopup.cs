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

    TaskCompletionSource<bool> _tcs;
    bool _closing;

    void Awake()
    {
        btnOk.onClick.AddListener(() => Close(true));
        btnCancel.onClick.AddListener(() => Close(false));
        HideImmediate();
    }

    public Task<bool> Show(string message, string ok = "확인", string cancel = "취소")
    {
        _closing = false;
        _tcs = new TaskCompletionSource<bool>();
        messageText.text = message;
        okLabel.text = ok;
        cancelLabel.text = cancel;
        gameObject.SetActive(true);
        EnableButtons(true);
        AnimateIn();
        return _tcs.Task;
    }

    void EnableButtons(bool on)
    {
        btnOk.interactable = on;
        btnCancel.interactable = on;
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
        if (window)
        {
            window.localScale = Vector3.one;
        }
    }

    async void Close(bool result)
    {
        if (_closing) return;
        _closing = true;
        EnableButtons(false);

        // 페이드아웃 (간단히)
        if (canvasGroup)
        {
            float t = 0f;
            float dur = 0.12f;
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
