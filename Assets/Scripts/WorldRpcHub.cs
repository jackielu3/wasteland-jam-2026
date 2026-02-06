using UnityEngine;
using FishNet.Object;

public class WorldRpcHub : NetworkBehaviour
{
    private static WorldRpcHub _serverHub;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (_serverHub == null)
            _serverHub = this;
    }

    public static void SetActiveForAll(int worldId, bool active)
    {
        if (_serverHub == null)
        {
            Debug.LogWarning("[WorldRpcHub] No server hub available yet.");
            return;
        }

        _serverHub.ObserversSetWorldActive(worldId, active);
    }

    [ObserversRpc(BufferLast = true)]
    private void ObserversSetWorldActive(int worldId, bool active)
    {
        if (WorldId.TryGet(worldId, out var target) && target != null)
            target.gameObject.SetActive(active);
    }
}
