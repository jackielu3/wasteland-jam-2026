using FishNet.Object;
using System;
using UnityEngine;

public class PlayerBattery : NetworkBehaviour
{
    [Header("Battery")]
    [SerializeField] private float maxBattery = 100f;
    [SerializeField] private float rechargePerSecond = 25f;
    [SerializeField] private float minBattery = 0f;

    public readonly FishNet.Object.Synchronizing.SyncVar<float> Battery
        = new FishNet.Object.Synchronizing.SyncVar<float>();

    public float MaxBattery => maxBattery;

    public event Action<float, float> OnBatteryChanged;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (IsServerStarted)
            Battery.Value = maxBattery;

        OnBatteryChanged?.Invoke(Battery.Value, maxBattery);
        Battery.OnChange += OnBatteryChangedSync;
    }

    private void OnBatteryChangedSync(float prev, float next, bool asServer)
    {
        OnBatteryChanged?.Invoke(next, maxBattery);
    }

    /* -------------- Server-side API -------------- */

    [Server]
    public bool Has(float amount) => Battery.Value >= amount;

    [Server]
    public bool TryConsume(float amount)
    {
        if (amount <= 0f) return true;
        if (Battery.Value < amount) return false;

        Battery.Value = Mathf.Max(minBattery, Battery.Value - amount);
        return true;
    }

    [Server]
    public void Add(float amount)
    {
        if (amount <= 0f) return;
        Battery.Value = Mathf.Min(maxBattery, Battery.Value + amount);
    }

    [Server]
    public void RechargeTick(float deltaTime)
    {
        Add(rechargePerSecond * deltaTime);
    }
}