using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private NetworkObject hostPlayerPrefab;
    [SerializeField] private NetworkObject clientPlayerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform hostSpawn;
    [SerializeField] private Transform clientSpawn;

    private NetworkManager _nm;
    private ServerManager _server;

    private readonly Dictionary<int, NetworkObject> _spawnedByClientId = new();

    private int _hostClientId = -1;

    private void Awake()
    {
        _nm = InstanceFinder.NetworkManager;
        if (_nm != null)
            _server = _nm.ServerManager;
    }

    private void OnEnable()
    {
        if (_nm == null || _server == null)
            return;

        _server.OnRemoteConnectionState += OnRemoteConnectionState;

        if (InstanceFinder.IsServerStarted && _server.Started)
            StartCoroutine(SpawnForAllConnectedNextFrame());
    }

    private void OnDisable()
    {
        if (_server != null)
            _server.OnRemoteConnectionState -= OnRemoteConnectionState;
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            _hostClientId = -1;
            TrySpawnForConnection(conn);
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            _hostClientId = -1;

            if (conn != null)
                _spawnedByClientId.Remove(conn.ClientId);
        }
    }

    private IEnumerator SpawnForAllConnectedNextFrame()
    {
        yield return null;

        if (!InstanceFinder.IsServerStarted || !_server.Started)
            yield break;

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

        if (_spawnedByClientId.ContainsKey(conn.ClientId))
            return;

        if (conn.Objects != null && conn.Objects.Count > 0)
        {
            return;
        }

        bool isHost = IsHostConnection(conn);
        NetworkObject prefab = isHost ? hostPlayerPrefab : clientPlayerPrefab;
        Transform spawn = isHost ? hostSpawn : clientSpawn;

        if (prefab == null)
        {
            Debug.LogError("PlayerSpawner: Prefab is null.");
            return;
        }

        Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
        Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;

        NetworkObject nob = Instantiate(prefab, pos, rot);

        _server.Spawn(nob, conn);

        _spawnedByClientId[conn.ClientId] = nob;

        Debug.Log($"PlayerSpawner[{gameObject.scene.name}]: Spawned {(isHost ? "HOST" : "CLIENT")} player for ClientId={conn.ClientId}");
    }

    private bool IsHostConnection(NetworkConnection conn)
    {
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

    public void DespawnAllSpawnedPlayers()
    {
        if (!InstanceFinder.IsServerStarted || _server == null || !_server.Started)
            return;

        foreach (var kvp in _spawnedByClientId)
        {
            NetworkObject nob = kvp.Value;
            if (nob != null && nob.IsSpawned)
                _server.Despawn(nob);
        }

        _spawnedByClientId.Clear();
        Debug.Log($"PlayerSpawner[{gameObject.scene.name}]: Despawned all spawned players.");
    }
}
