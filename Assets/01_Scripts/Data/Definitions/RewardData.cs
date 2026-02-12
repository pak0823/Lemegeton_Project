using System;

[Serializable]
public class RewardData
{
    public string itemID;
    public int count;

    public RewardData(string id, int c)
    {
        itemID = id;
        count = c;
    }
}
