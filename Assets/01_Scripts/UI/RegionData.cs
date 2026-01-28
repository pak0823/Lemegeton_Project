using UnityEngine;


[CreateAssetMenu(menuName = "Game/RegionData")]
public class RegionData : ScriptableObject
{
    public string regionName;
    [TextArea] public string description;
    public Sprite image;
}
