using UnityEngine.UI;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("공통 UI")]
    [SerializeField] private GameObject optionPanel;
    [SerializeField] private Button optionButton;

    private void Awake()
    {
        // 싱글톤 + DontDestroyOnLoad 처리
        if (Shared.UIManager == null)
        {
            Shared.UIManager = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 초기화
        if (optionPanel != null)
            optionPanel.SetActive(false);

        if (optionButton != null)
            optionButton.onClick.AddListener(ToggleOptionPanel);
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // ESC 키 입력 시 옵션창 닫기
            if (optionPanel != null && optionPanel.activeSelf)
                optionPanel.SetActive(false);
        }
    }

    // 옵션창 열기/닫기
    public void ToggleOptionPanel()
    {
        if (optionPanel == null) return;

        bool isActive = optionPanel.activeSelf;
        optionPanel.SetActive(!isActive);
    }
}
