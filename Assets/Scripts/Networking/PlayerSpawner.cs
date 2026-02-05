using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private NetworkObject hostPlayerPrefab;
    [SerializeField] private NetworkObject clientPlayerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform hostSpawn;
    [SerializeField] private Transform clientSpawn;

    private ServerManager _server;
    private ClientManager _client;
    private FishNet.Managing.Scened.SceneManager _sceneManager;

    private readonly HashSet<int> _spawnedClientIds = new HashSet<int>();
    private int _hostClientId = -1;
    private int _fallbackHostClientId = -1;
    
    private void Awake()
    {
        _server = InstanceFinder.ServerManager;
        _client = InstanceFinder.ClientManager;
        _sceneManager = InstanceFinder.SceneManager;
    }

    private void OnEnable()
    {
        if (_server != null)
        {
            _server.OnRemoteConnectionState += OnRemoteConnectionState;
            _server.OnServerConnectionState += OnServerConnectionState;
        }

        if (_sceneManager != null)
        {
            // Fires after FishNet completes scene loading.
            _sceneManager.OnLoadEnd += OnFishNetLoadEnd;
        }
    }

    private void OnDisable()
    {
        if (_server != null)
        {
            _server.OnRemoteConnectionState -= OnRemoteConnectionState;
            _server.OnServerConnectionState -= OnServerConnectionState;
        }

        if (_sceneManager != null)
        {
            _sceneManager.OnLoadEnd -= OnFishNetLoadEnd;
        }
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            _spawnedClientIds.Clear();
            _fallbackHostClientId = -1;
        }
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (!InstanceFinder.IsServerStarted) return;

        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            // If a new client joins while gameplay scene is active, spawn them too.
            TrySpawnForConnection(conn);
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            _spawnedClientIds.Remove(conn.ClientId);
        }
    }

    private void OnFishNetLoadEnd(SceneLoadEndEventArgs args)
    {
        // Only server spawns.
        if (!InstanceFinder.IsServerStarted) return;

        // This is the key: when gameplay scene finishes loading, spawn for everyone already connected.
        TrySpawnForAllExistingConnections();
    }

    private void TrySpawnForAllExistingConnections()
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        if (_server == null)
        {
            Debug.LogError("PlayerSpawner: ServerManager is null.");
            return;
        }

        _hostClientId = GetLowestActiveClientId();

        foreach (NetworkConnection c in _server.Clients.Values)
        {
            if (c == null || !c.IsActive)
                continue;

            TrySpawnForConnection(c);
        }
    }

    private void TrySpawnForConnection(NetworkConnection conn)
    {
        if (conn == null || !conn.IsActive)
            return;

        if (_spawnedClientIds.Contains(conn.ClientId))
            return;

        bool isHostPlayer = IsHostConnection(conn);

        NetworkObject prefab = isHostPlayer ? hostPlayerPrefab : clientPlayerPrefab;
        Transform spawn = isHostPlayer ? hostSpawn : clientSpawn;

        if (prefab == null)
        {
            Debug.LogError($"PlayerSpawner: Missing prefab for {(isHostPlayer ? "HOST" : "CLIENT")}.");
            return;
        }

        Vector3 pos = spawn ? spawn.position : Vector3.zero;
        Quaternion rot = spawn ? spawn.rotation : Quaternion.identity;

        NetworkObject player = Instantiate(prefab, pos, rot);

        // If spawning fails, FishNet will usually log why (often prefab registration).
        _server.Spawn(player, conn);

        _spawnedClientIds.Add(conn.ClientId);

        Debug.Log($"PlayerSpawner: Spawned {(isHostPlayer ? "HOST" : "CLIENT")} player for ClientId={conn.ClientId}");
    }

    private bool IsHostConnection(NetworkConnection conn)
    {
        // Host is whichever active connection has the lowest ClientId.
        if (_hostClientId == -1)
            _hostClientId = GetLowestActiveClientId();

        return conn.ClientId == _hostClientId;
    }


    private int GetLowestActiveClientId()
    {
        int lowest = int.MaxValue;

        foreach (NetworkConnection c in _server.Clients.Values)
        {
            if (c == null || !c.IsActive)
                continue;

            if (c.ClientId < lowest)
                lowest = c.ClientId;
        }

        return lowest == int.MaxValue ? -1 : lowest;
    }

}
