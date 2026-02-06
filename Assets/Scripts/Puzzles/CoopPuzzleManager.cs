using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet;

public class CoopPuzzleManager : MonoBehaviour
{
    public static CoopPuzzleManager Instance;

    [Serializable]
    public class DoorGroup
    {
        public int doorId;
        public GameObject doorObject;        // local tilemap GO
        public List<int> requiredPlateIds = new();
        public bool stayOpenOnceOpened = true;

        [NonSerialized] public bool isOpen;
        [NonSerialized] public bool everOpened;
    }

    [SerializeField] private List<DoorGroup> doors = new();

    private readonly Dictionary<int, int> _plateCounts = new();

    private void Awake() => Instance = this;

    public void ServerSetPlatePressed(int plateId, bool pressed)
    {
        if (!IsServerRunning()) return;

        int count = _plateCounts.TryGetValue(plateId, out var c) ? c : 0;
        count = pressed ? count + 1 : Mathf.Max(0, count - 1);
        _plateCounts[plateId] = count;

        RecomputeDoors();
    }

    private bool IsServerRunning()
    {
        return InstanceFinder.NetworkManager != null &&
               InstanceFinder.NetworkManager.IsServerStarted;
    }

    private void RecomputeDoors()
    {
        foreach (var d in doors)
        {
            bool allHeld = true;
            foreach (var id in d.requiredPlateIds)
            {
                if (!_plateCounts.TryGetValue(id, out var c) || c <= 0) { allHeld = false; break; }
            }

            bool shouldOpen = d.stayOpenOnceOpened ? (d.everOpened || allHeld) : allHeld;
            if (allHeld) d.everOpened = true;

            if (d.isOpen == shouldOpen) continue;
            d.isOpen = shouldOpen;

            // Server tells everyone via Player-spawned hub.
            PuzzleRpcHub.SendDoorActiveToAll(d.doorId, active: !d.isOpen);
        }
    }

    public void ApplyDoorLocal(int doorId, bool active)
    {
        // called on every client when RPC arrives
        foreach (var d in doors)
        {
            if (d.doorId != doorId) continue;
            if (d.doorObject != null) d.doorObject.SetActive(active);
            return;
        }
    }
}
