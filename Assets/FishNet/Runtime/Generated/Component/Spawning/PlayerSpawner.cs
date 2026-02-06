using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [Header("FishNet")]
    [SerializeField] private NetworkManager networkManager;

    [Header("Prefabs")]
    [SerializeField] private NetworkObject hostPlayerPrefab;
    [SerializeField] private NetworkObject clientPlayerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform hostSpawn;
    [SerializeField] private Transform clientSpawn;

    // Tracks which connections already got a player.
    private readonly HashSet<int> _spawnedClientIds = new();
    // Tracks which connections we are waiting on (start scenes not loaded yet).
    private readonly HashSet<int> _pendingClientIds = new();

    private void Awake()
    {
        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();
    }

    private void OnEnable()
    {
        if (networkManager == null) return;

        networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        networkManager.SceneManager.OnLoadEnd += OnLoadEnd;
    }

    private void OnDisable()
    {
        if (networkManager == null) return;

        networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        networkManager.SceneManager.OnLoadEnd -= OnLoadEnd;
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (!networkManager.IsServerStarted || conn == null)
            return;

        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            // Wait until THIS connection finishes loading FishNet start scenes.
            if (_spawnedClientIds.Contains(conn.ClientId))
                return;

            _pendingClientIds.Add(conn.ClientId);
            conn.OnLoadedStartScenes += OnConnectionLoadedStartScenes;
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            _pendingClientIds.Remove(conn.ClientId);
            _spawnedClientIds.Remove(conn.ClientId);
            conn.OnLoadedStartScenes -= OnConnectionLoadedStartScenes;
        }
    }

    private void OnConnectionLoadedStartScenes(NetworkConnection conn, bool asServer)
    {
        if (!asServer || conn == null)
            return;

        // Unhook immediately to avoid multiple calls.
        conn.OnLoadedStartScenes -= OnConnectionLoadedStartScenes;

        if (!_pendingClientIds.Contains(conn.ClientId))
            return;

        _pendingClientIds.Remove(conn.ClientId);
        TrySpawnForConnection(conn);
    }

    // This fires when FishNet finishes loading scenes (server side).
    // Handy for host/local cases where start scenes are already loaded.
    private void OnLoadEnd(SceneLoadEndEventArgs args)
    {
        if (!networkManager.IsServerStarted)
            return;

        // Try spawn anyone connected that isn't spawned yet.
        foreach (var conn in networkManager.ServerManager.Clients.Values)
            TrySpawnForConnection(conn);
    }

    private void TrySpawnForConnection(NetworkConnection conn)
    {
        if (conn == null || !networkManager.IsServerStarted)
            return;

        if (_spawnedClientIds.Contains(conn.ClientId))
            return;

        // Choose prefab/spawn: host is usually ClientId == 0.
        bool isHostLike = (conn.ClientId == 0);
        NetworkObject prefab = isHostLike ? hostPlayerPrefab : clientPlayerPrefab;
        Transform spawn = isHostLike ? hostSpawn : clientSpawn;

        if (prefab == null)
        {
            Debug.LogError("PlayerSpawner: Missing player prefab reference.");
            return;
        }
        if (spawn == null)
        {
            Debug.LogError("PlayerSpawner: Missing spawn point reference.");
            return;
        }

        NetworkObject player = Instantiate(prefab, spawn.position, spawn.rotation);
        networkManager.ServerManager.Spawn(player, conn);

        _spawnedClientIds.Add(conn.ClientId);
        Debug.Log($"Spawned player for ClientId={conn.ClientId} at {spawn.name}");
    }
}
