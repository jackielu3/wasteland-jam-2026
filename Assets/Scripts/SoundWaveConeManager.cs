using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class SoundWaveConeManager : MonoBehaviour
{
    public static SoundWaveConeManager Instance { get; private set; }

    private readonly Dictionary<int, SoundWaveConeNet> _active = new();

    private void Awake()
    {
        Instance = this;
    }

    public void ServerStartForPlayer(NetworkConnection conn, NetworkObject prefab, Transform player, Vector2 aimDir)
    {
        if (!InstanceFinder.IsServer) return;

        int key = conn.ClientId;

        if (_active.TryGetValue(key, out SoundWaveConeNet existing) && existing != null)
        {
            existing.ServerSetAim(aimDir);
            existing.ServerSetActive(true);
            return;
        }

        NetworkObject nob = Instantiate(prefab, player.position, Quaternion.identity);
        InstanceFinder.ServerManager.Spawn(nob, conn); // owner can be conn; server still authoritative

        var cone = nob.GetComponent<SoundWaveConeNet>();
        cone.ServerAttachTo(player);
        cone.ServerSetAim(aimDir);
        cone.ServerSetActive(true);

        _active[key] = cone;
    }

    public void ServerStopForPlayer(NetworkConnection conn)
    {
        if (!InstanceFinder.IsServer) return;

        int key = conn.ClientId;
        if (_active.TryGetValue(key, out SoundWaveConeNet cone) && cone != null)
            cone.ServerSetActive(false);
    }

    public void ServerUpdateAimForPlayer(NetworkConnection conn, Vector2 aimDir)
    {
        if (!InstanceFinder.IsServer) return;

        int key = conn.ClientId;
        if (_active.TryGetValue(key, out SoundWaveConeNet cone) && cone != null)
            cone.ServerSetAim(aimDir);
    }
}
