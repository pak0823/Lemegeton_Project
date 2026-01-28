using UnityEngine;
using UnityEngine.UI;

public class RegionDisplay : MonoBehaviour
{
    public Text regionNameText;
    public Text descriptionText;
    public Image regionImage;

    public void ShowRegion(RegionData data)
    {
        if (data == null) return;

        regionNameText.text = data.regionName;
        descriptionText.text = data.description;
        regionImage.sprite = data.image;
    }
}
