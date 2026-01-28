using UnityEngine.UI;
using UnityEngine;

public class UI_Departure : MonoBehaviour
{
    [SerializeField] private Button backButton;

    private void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(UI_Lobby.Instance.ToggleDeparturePanel);

    }
}
