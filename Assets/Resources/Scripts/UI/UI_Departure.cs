using UnityEngine.UI;
using UnityEngine;

public class UI_Departure : MonoBehaviour
{
    [SerializeField] private Button backButton;

    private void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(Shared.UI_Lobby.ToggleDeparturePanel);

    }
}
