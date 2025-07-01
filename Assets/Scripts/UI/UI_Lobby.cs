using UnityEngine.UI;
using UnityEngine;

public class UI_Lobby : MonoBehaviour
{
    [SerializeField] private Button departureButton;
    [SerializeField] private GameObject departurePanel;

    private void Awake()
    {
        if (departurePanel != null)
            departurePanel.SetActive(false);

        if(Shared.UI_Lobby == null)
            Shared.UI_Lobby = this;
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
