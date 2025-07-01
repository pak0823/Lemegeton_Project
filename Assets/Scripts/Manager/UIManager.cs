using UnityEngine.UI;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("���� UI")]
    [SerializeField] private GameObject optionPanel;
    [SerializeField] private Button optionButton;

    private void Awake()
    {
        // �̱��� + DontDestroyOnLoad ó��
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

        // �ʱ�ȭ
        if (optionPanel != null)
            optionPanel.SetActive(false);

        if (optionButton != null)
            optionButton.onClick.AddListener(ToggleOptionPanel);
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // ESC Ű �Է� �� �ɼ�â �ݱ�
            if (optionPanel != null && optionPanel.activeSelf)
                optionPanel.SetActive(false);
        }
    }

    // �ɼ�â ����/�ݱ�
    public void ToggleOptionPanel()
    {
        if (optionPanel == null) return;

        bool isActive = optionPanel.activeSelf;
        optionPanel.SetActive(!isActive);
    }
}
