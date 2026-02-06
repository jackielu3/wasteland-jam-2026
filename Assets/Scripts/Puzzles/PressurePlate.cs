using UnityEngine;
using FishNet;

public class PressurePlate : MonoBehaviour
{
    public int plateId;

    private bool IsServerRunning()
    {
        return InstanceFinder.NetworkManager != null &&
               InstanceFinder.NetworkManager.IsServerStarted;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServerRunning()) return;
        if (!other.transform.root.CompareTag("Player")) return;

        CoopPuzzleManager.Instance?.ServerSetPlatePressed(plateId, true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServerRunning()) return;
        if (!other.transform.root.CompareTag("Player")) return;

        CoopPuzzleManager.Instance?.ServerSetPlatePressed(plateId, false);
    }
}
