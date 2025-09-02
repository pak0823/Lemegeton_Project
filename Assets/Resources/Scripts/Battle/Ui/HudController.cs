using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HudController : MonoBehaviour
{
    [Header("Toggle Button (옵션)")]
    [SerializeField] private Button toggleButton;

    [Header("HUD Root (공통 부모, 반드시 CanvasGroup 부착)")]
    [SerializeField] private CanvasGroup hudRoot;

    //[Header("추가로 함께 토글할 CanvasGroup들 (HUDRoot 밖 버튼/바 포함)")]
    //[SerializeField] private List<CanvasGroup> extraCanvasGroups = new List<CanvasGroup>();

    [Header("추가로 함께 토글할 GameObject들 (CanvasGroup 없을 때만)")]
    [SerializeField] private List<GameObject> extraGameObjects = new List<GameObject>();

    public bool IsVisible { get; private set; } = true;

    void Awake()
    {
        if (!hudRoot)
        {
            // 같은 오브젝트에 붙어 있다면 자동 획득 시도
            hudRoot = GetComponent<CanvasGroup>();
        }

        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);

        // 초기 상태 동기화
        if (hudRoot != null)
            IsVisible = hudRoot.alpha > 0.5f && hudRoot.interactable && hudRoot.blocksRaycasts;

        Apply(IsVisible);
    }

    void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(Toggle);
    }

    public void Toggle()
    {
        IsVisible = !IsVisible;
        Apply(IsVisible);
    }

    public void Show()
    {
        IsVisible = true;
        Apply(true);
    }

    public void Hide()
    {
        IsVisible = false;
        Apply(false);
    }

    void Apply(bool show)
    {
        // 1) HUDRoot는 항상 CanvasGroup 방식(화면 가리기)
        if (hudRoot)
        {
            hudRoot.alpha = show ? 1f : 0f;
            hudRoot.interactable = show;
            hudRoot.blocksRaycasts = show;
        }

        //// 2) 추가 CanvasGroup들도 동일하게 처리(상태값 불변)
        //if (extraCanvasGroups != null)
        //{
        //    foreach (var cg in extraCanvasGroups)
        //    {
        //        if (!cg) continue;
        //        cg.alpha = show ? 1f : 0f;
        //        cg.interactable = show;
        //        cg.blocksRaycasts = show;
        //    }
        //}

        // 3) CanvasGroup이 전혀 없는 단순 GO는 SetActive로 처리(상태 영향 없을 때만 사용)
        if (extraGameObjects != null)
        {
            foreach (var go in extraGameObjects)
            {
                if (!go) continue;
                go.SetActive(show);
            }
        }
    }
}
