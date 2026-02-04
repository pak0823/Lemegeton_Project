using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public abstract class ModalWindowBase : MonoBehaviour, IModalWindow, Project.UI.ISceneUiModule
{
    [SerializeField] protected CanvasGroup root;
    public bool IsOpen { get; private set; }
    public GameObject Root => gameObject;
    public virtual int Priority => 0;

    protected virtual void Awake()
    {
        // 안전 초기화: 닫힘
        if (root)
        {
            root.alpha = 0f;
            root.blocksRaycasts = false;
            root.interactable = false;
        }
        else
        {
            // 필요 시 자동 탐색
            root = GetComponentInChildren<CanvasGroup>(true);
        }
    }

    public virtual void Show()
    {
        IsOpen = true;
        if (root) { root.alpha = 1f; root.blocksRaycasts = true; root.interactable = true; }
        OnShown(); // 확장 훅
    }

    public virtual void Hide()
    {
        IsOpen = false;
        if (root) { root.alpha = 0f; root.blocksRaycasts = false; root.interactable = false; }
        OnHidden(); // 확장 훅
    }

    public void Toggle()
    {
        var m = UiModalManager.Instance;
        if (m != null) m.Toggle(this);
        else if (IsOpen) Hide(); else Show();
    }

    public virtual void OnUiShown()
    {
        // 규칙: 활성화 시 "항상 닫힌 상태"에서 시작 (필요 시 다음 프레임 보정)
        if (IsOpen) Hide();
        // 레이아웃/앵커가 늦게 잡히는 경우 대비(선택)
        StartCoroutine(_RepositionNextFrame());
    }
    public virtual void OnUiHidden()
    {
        // 열려 있었다면 반드시 정리
        if (IsOpen) Hide();
    }

    /// <summary>Show 직후에 확장 처리가 필요할 때 오버라이드.</summary>
    protected virtual void OnShown() { }
    /// <summary>Hide 직후에 확장 처리가 필요할 때 오버라이드.</summary>
    protected virtual void OnHidden() { }

    System.Collections.IEnumerator _RepositionNextFrame()
    {
        yield return null;
        // 필요 시 자식 UI의 초기 선택/포커스/화살표 위치 보정 등을 여기서 처리하도록
        // 파생 클래스에서 OnShown과 함께 사용하세요.
    }

}