using FishNet;
using FishNet.Object;
using UnityEngine;

public class BatteryPickup : NetworkBehaviour
{
    [SerializeField] private float restoreAmount = 35f;

    private void OnTriggerEnter2D(Collider2D colider)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        PlayerBattery b = colider.GetComponentInParent<PlayerBattery>();
        if (b == null)
            return;

        b.Add(restoreAmount);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }
}
