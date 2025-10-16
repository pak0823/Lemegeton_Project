using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RetreatConfirmPopup : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Text messageText;    // 본문
    [SerializeField] Button btnOk;        // 확인 (확률 계산 진행)
    [SerializeField] Button btnCancel;    // 취소
    [SerializeField] Image focusOk;       // 포커스 표시용(선택)
    [SerializeField] Image focusCancel;   // 포커스 표시용(선택)

    [Header("InputKey")]
    [SerializeField] private KeyCode leftMove_Key = KeyCode.A; //왼쪽 이동 키
    [SerializeField] private KeyCode rightMove_Key = KeyCode.D; //오른쪽 이동 키
    [SerializeField] private KeyCode current_Key = KeyCode.E; //선택 확정 키
    [SerializeField] private KeyCode cancel_Key = KeyCode.Q; //선택 취소 키
    [SerializeField] private KeyCode cancelClose_Key = KeyCode.Escape; //창 닫기 키

    int focus = 0; // 0=OK, 1=Cancel
    TaskCompletionSource<bool> tcs;

    void OnEnable()
    {
        SetFocus(0);
    }

    void Update()
    {
        // A/D로 좌우 이동
        if (Input.GetKeyDown(leftMove_Key)) MoveFocus(-1);
        if (Input.GetKeyDown(rightMove_Key)) MoveFocus(+1);

        // 현재 포커스된 버튼 클릭
        if (Input.GetKeyDown(current_Key))
        {
            if (focus == 0) ClickOk();
            else ClickCancel();
        }
        if (Input.GetKeyDown(cancel_Key) || Input.GetKeyDown(cancelClose_Key)) ClickCancel();
    }

    void MoveFocus(int dir) => SetFocus((focus + dir + 2) % 2);

    void SetFocus(int idx)
    {
        focus = idx;
        if (focusOk) focusOk.enabled = (focus == 0);
        if (focusCancel) focusCancel.enabled = (focus == 1);
    }

    public async Task<bool> ShowAsync(string message, float successChance01)
    {
        tcs = new TaskCompletionSource<bool>();

        if (messageText) messageText.text = message;

        btnOk.onClick.RemoveAllListeners();
        btnCancel.onClick.RemoveAllListeners();
        btnOk.onClick.AddListener(ClickOk);
        btnCancel.onClick.AddListener(ClickCancel);

        gameObject.SetActive(true);
        var result = await tcs.Task;
        gameObject.SetActive(false);
        return result;
    }

    void ClickOk()
    {
        if (tcs == null) return;
        tcs.TrySetResult(true);
    }
    void ClickCancel()
    {
        if (tcs == null) return;
        tcs.TrySetResult(false);
    }
}
