using FishNet;
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

public class BatteryRechargeZone : NetworkBehaviour
{
    private readonly HashSet<PlayerBattery> _inside = new();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        PlayerBattery b = other.GetComponentInParent<PlayerBattery>();
        if (b != null)
            _inside.Add(b);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        PlayerBattery b = other.GetComponentInParent<PlayerBattery>();
        if (b != null)
            _inside.Remove(b);
    }

    private void Update()
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        float dt = Time.deltaTime;
        foreach (var b in _inside)
            b.RechargeTick(dt);
    }
}
