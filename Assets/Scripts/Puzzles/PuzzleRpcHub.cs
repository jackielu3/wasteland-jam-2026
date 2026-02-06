using UnityEngine;
using FishNet.Object;

public class PuzzleRpcHub : NetworkBehaviour
{
    private static PuzzleRpcHub _serverHub;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (_serverHub == null) _serverHub = this;
    }

    public static void SendDoorActiveToAll(int doorId, bool active)
    {
        if (_serverHub == null)
        {
            Debug.LogWarning("[PuzzleRpcHub] No server hub available yet.");
            return;
        }

        _serverHub.ObserversSetDoorActive(doorId, active);
    }

    [ObserversRpc(BufferLast = true)]
    private void ObserversSetDoorActive(int doorId, bool active)
    {
        if (CoopPuzzleManager.Instance != null)
            CoopPuzzleManager.Instance.ApplyDoorLocal(doorId, active);
    }
}
