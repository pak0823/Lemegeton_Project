using UnityEngine.UI;
using UnityEngine;

public class EncounterBannerUI : MonoBehaviour
{
    [SerializeField] private Text messageText;

    public void SetMessage(string msg)
    {
        if (messageText != null) messageText.text = msg;
    }
}
