using System.Collections.Generic;
using UnityEngine;
using FishNet;

public class BatteryRechargeZone : MonoBehaviour
{
    private readonly HashSet<PlayerBattery> _inside = new();

    private bool IsServerRunning()
    {
        return InstanceFinder.NetworkManager != null &&
               InstanceFinder.NetworkManager.IsServerStarted;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServerRunning()) return;

        PlayerBattery b = other.GetComponentInParent<PlayerBattery>();
        if (b != null)
            _inside.Add(b);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServerRunning()) return;

        PlayerBattery b = other.GetComponentInParent<PlayerBattery>();
        if (b != null)
            _inside.Remove(b);
    }

    private void Update()
    {
        if (!IsServerRunning()) return;

        float dt = Time.deltaTime;
        foreach (var b in _inside)
            b.RechargeTick(dt);
    }
}
