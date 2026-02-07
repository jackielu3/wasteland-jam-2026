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

    private readonly Dictionary<int, float> _arcPressedUntil = new();
    private float _nextArcExpiryCheckTime = 0f;

    private void Awake() => Instance = this;

    private void Update()
    {
        if (!IsServerRunning())
            return;

        if (Time.time < _nextArcExpiryCheckTime)
            return;

        if (_arcPressedUntil.Count == 0)
            return;

        bool anyExpired = false;
        float nextExpiry = float.PositiveInfinity;

        foreach (var kvp in _arcPressedUntil)
        {
            float until = kvp.Value;
            if (until <= Time.time)
                anyExpired = true;
            else if (until < nextExpiry)
                nextExpiry = until;
        }

        if (anyExpired)
            RecomputeDoors();

        _nextArcExpiryCheckTime = float.IsInfinity(nextExpiry) ? (Time.time + 999f) : nextExpiry;
    }

    public void ServerSetPlatePressed(int plateId, bool pressed)
    {
        if (!IsServerRunning()) return;

        int count = _plateCounts.TryGetValue(plateId, out var c) ? c : 0;
        count = pressed ? count + 1 : Mathf.Max(0, count - 1);
        _plateCounts[plateId] = count;

        RecomputeDoors();
    }

    public void ServerPulsePlate(int plateId, float pressSeconds)
    {
        if (!IsServerRunning()) return;

        float now = Time.time;
        float newUntil = now + Mathf.Max(0.01f, pressSeconds);

        if (_arcPressedUntil.TryGetValue(plateId, out float currentUntil))
        {
            if (newUntil > currentUntil)
                _arcPressedUntil[plateId] = newUntil;
        }
        else
        {
            _arcPressedUntil[plateId] = newUntil;
        }

        _nextArcExpiryCheckTime = Mathf.Min(_nextArcExpiryCheckTime, _arcPressedUntil[plateId]);

        RecomputeDoors();
    }

    private bool IsServerRunning()
    {
        return InstanceFinder.NetworkManager != null &&
               InstanceFinder.NetworkManager.IsServerStarted;
    }

    private bool IsPlateActive(int plateId)
    {
        if (_plateCounts.TryGetValue(plateId, out var c) && c > 0)
            return true;

        if (_arcPressedUntil.TryGetValue(plateId, out var until) && until > Time.time)
            return true;

        return false;
    }

    private void RecomputeDoors()
    {
        foreach (var d in doors)
        {
            bool allActive = true;
            foreach (var id in d.requiredPlateIds)
            {
                if (!IsPlateActive(id)) { allActive = false; break; }
            }

            bool shouldOpen = d.stayOpenOnceOpened ? (d.everOpened || allActive) : allActive;
            if (allActive) d.everOpened = true;

            if (d.isOpen == shouldOpen) continue;
            d.isOpen = shouldOpen;

            PuzzleRpcHub.SendDoorActiveToAll(d.doorId, active: !d.isOpen);
        }
    }

    public void ApplyDoorLocal(int doorId, bool active)
    {
        foreach (var d in doors)
        {
            if (d.doorId != doorId) continue;
            if (d.doorObject != null) d.doorObject.SetActive(active);
            return;
        }
    }
}
