using UnityEngine;


public class CampHeaderController : MonoBehaviour
{
    [SerializeField] private GameObject charSelectorRoot;
    [SerializeField] private GameObject itemSelectorRoot;

    public void UpdateHeader(CampHeaderType type)
    {
        if (charSelectorRoot) charSelectorRoot.SetActive(type == CampHeaderType.Character);
        if (itemSelectorRoot) itemSelectorRoot.SetActive(type == CampHeaderType.Item);
    }
}
