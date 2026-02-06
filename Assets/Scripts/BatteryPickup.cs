using UnityEngine;
using FishNet;

public class BatteryPickup : MonoBehaviour
{
    [SerializeField] private float restoreAmount = 35f;

    [SerializeField] private int worldId;

    private bool IsServerRunning()
    {
        return InstanceFinder.NetworkManager != null &&
               InstanceFinder.NetworkManager.IsServerStarted;
    }

    private void OnTriggerEnter2D(Collider2D colider)
    {
        if (!IsServerRunning())
            return;

        PlayerBattery b = colider.GetComponentInParent<PlayerBattery>();
        if (b == null)
            return;

        b.Add(restoreAmount);

        // Tell everyone to hide this pickup locally
        WorldRpcHub.SetActiveForAll(worldId, false);

        // Remove on server instance too
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
