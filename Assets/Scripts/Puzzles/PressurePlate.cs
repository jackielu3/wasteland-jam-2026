using System.Collections.Generic;
using UnityEngine;
using FishNet;

public class PressurePlate : MonoBehaviour
{
    public int plateId;

    private readonly HashSet<PlatePresser> _pressers = new();

    private bool IsServerRunning()
    {
        return InstanceFinder.NetworkManager != null &&
               InstanceFinder.NetworkManager.IsServerStarted;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServerRunning())
            return;

        PlatePresser presser = other.gameObject.GetComponent<PlatePresser>();
        if (presser == null)
            return;

        bool wasEmpty = _pressers.Count == 0;
        _pressers.Add(presser);

        if (wasEmpty && _pressers.Count == 1)
            CoopPuzzleManager.Instance?.ServerSetPlatePressed(plateId, true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServerRunning())
            return;

        PlatePresser presser = other.gameObject.GetComponent<PlatePresser>();
        if (presser == null)
            return;

        bool removed = _pressers.Remove(presser);
        if (!removed)
            return;

        if (_pressers.Count == 0)
            CoopPuzzleManager.Instance?.ServerSetPlatePressed(plateId, false);
    }
}
