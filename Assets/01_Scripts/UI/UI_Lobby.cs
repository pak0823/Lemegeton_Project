using UnityEngine.UI;
using UnityEngine;

public class UI_Lobby : MonoBehaviour
{
    public static UI_Lobby Instance {  get; private set; }

    [SerializeField] private Button departureButton;
    [SerializeField] private GameObject departurePanel;

    private void Awake()
    {
        if (departurePanel != null)
            departurePanel.SetActive(false);

        if(Instance == null) Instance = this;
        else Destroy(gameObject);

    }

    private void Start()
    {
        if (departureButton != null)
            departureButton.onClick.AddListener(ToggleDeparturePanel);

    }

    public void ToggleDeparturePanel()
    {
        if (departurePanel == null) return;

        bool isActive = departurePanel.activeSelf;
        departurePanel.SetActive(!isActive);
    }
}
