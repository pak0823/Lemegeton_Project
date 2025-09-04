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

    [SerializeField] private GameSpeedController speedCtrl;

    public bool IsVisible { get; private set; } = true;
    bool initialized = false; // 초기 Apply 시 속도변경 방지

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

        Apply(IsVisible, true); // 초기엔 속도 변경하지 않음
        initialized = true;
    }

    void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(Toggle);
    }

    public void Toggle()
    {
        IsVisible = !IsVisible;
        Apply(IsVisible, false);
    }

    public void Show()
    {
        IsVisible = true;
        Apply(true, false);
    }

    public void Hide()
    {
        IsVisible = false;
        Apply(false, false);
    }

    void Apply(bool show, bool isInitial)
    {
        // HUDRoot는 항상 CanvasGroup 방식(화면 가리기)
        if (hudRoot)
        {
            hudRoot.alpha = show ? 1f : 0f;
            hudRoot.interactable = show;
            hudRoot.blocksRaycasts = show;
        }

        //// 추가 CanvasGroup들도 동일하게 처리(상태값 불변)
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

        // CanvasGroup이 전혀 없는 단순 GO는 SetActive로 처리(상태 영향 없을 때만 사용)
        if (extraGameObjects != null)
        {
            foreach (var go in extraGameObjects)
            {
                if (!go) continue;
                go.SetActive(show);
            }
        }
        // 속도 제어 연동
        if (initialized && !isInitial && speedCtrl != null)
        {
            if (!show) speedCtrl.PauseRemember();   // HUD 숨김 → 일시정지(배속 기억)
            else speedCtrl.ResumeRemembered(); // HUD 표시 → 기억한 배속으로 재생
        }
    }
}
