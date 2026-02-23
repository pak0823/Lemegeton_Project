using UnityEngine;

public interface IInteractable
{
    void OnInteract();
    void SetHighlight(bool isActive);
    Transform GetTransform();
    bool CanInteract { get; }
    string GetInteractLabel();
}
