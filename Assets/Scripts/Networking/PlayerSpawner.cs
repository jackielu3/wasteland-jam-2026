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
    [Header("Player Prefabs")]
    [SerializeField] private NetworkObject hostPlayerPrefab;
    [SerializeField] private NetworkObject clientPlayerPrefab;

    [Header("Player Spawn Points")]
    [SerializeField] private Transform hostSpawn;
    [SerializeField] private Transform clientSpawn;

    [System.Serializable]
    public class RuntimeSpawnGroup
    {
        [Tooltip("NetworkObject prefab to spawn at each spawn point.")]
        public NetworkObject prefab;

        [Tooltip("Local scene transforms used only as spawn markers. (Do NOT put NetworkObjects here.)")]
        public List<Transform> spawnPoints = new();

        [Tooltip("If true, this group will only spawn when there are 2 active connections.")]
        public bool requireTwoPlayers = true;

        [Tooltip("Spawn with no owner (recommended for pushables; ownership can be handed off later).")]
        public bool spawnWithNoOwner = true;
    }

    [Header("Runtime Scene Spawns (Server Runtime-Spawned)")]
    [SerializeField] private List<RuntimeSpawnGroup> runtimeSpawnGroups = new();

    private NetworkManager _nm;
    private ServerManager _server;

    private readonly Dictionary<int, NetworkObject> _spawnedByClientId = new();

    // Track runtime spawns so they can be despawned if you reset/end the session.
    private readonly List<NetworkObject> _spawnedRuntimeObjects = new();
    private bool _runtimeSpawned = false;

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

            // Generalized runtime spawn (crates now, more later)
            TrySpawnOnce();
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            _hostClientId = -1;

            if (conn != null)
                _spawnedByClientId.Remove(conn.ClientId);

            // If your design is “session ends on disconnect”, you may also want:
            // DespawnAllRuntimeSpawns();
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

        // Generalized runtime spawn (crates now, more later)
        TrySpawnOnce();
    }

    // -----------------------------
    // PLAYER SPAWNING (UNCHANGED)
    // -----------------------------
    private void TrySpawnForConnection(NetworkConnection conn)
    {
        if (conn == null || !conn.IsActive)
            return;

        if (_spawnedByClientId.ContainsKey(conn.ClientId))
            return;

        // If FishNet already thinks this conn has objects, don't spawn again.
        if (conn.Objects != null && conn.Objects.Count > 0)
            return;

        bool isHost = IsHostConnection(conn);
        NetworkObject prefab = isHost ? hostPlayerPrefab : clientPlayerPrefab;
        Transform spawn = isHost ? hostSpawn : clientSpawn;

        if (prefab == null)
        {
            Debug.LogError("PlayerSpawner: Player prefab is null.");
            return;
        }

        Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
        Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;

        NetworkObject nob = Instantiate(prefab, pos, rot);
        _server.Spawn(nob, conn);

        _spawnedByClientId[conn.ClientId] = nob;

        Debug.Log($"PlayerSpawner[{gameObject.scene.name}]: Spawned {(isHost ? "HOST" : "CLIENT")} player for ClientId={conn.ClientId}");
    }

    // -----------------------------------------
    // RUNTIME SPAWNS (GENERALIZED, SPAWN ONCE)
    // -----------------------------------------
    private void TrySpawnOnce()
    {
        if (!InstanceFinder.IsServerStarted || _server == null || !_server.Started)
            return;

        if (_runtimeSpawned)
            return;

        if (runtimeSpawnGroups == null || runtimeSpawnGroups.Count == 0)
            return;

        // If any group requires 2 players, enforce that before spawning once.
        int activeCount = GetActiveConnectionCount();

        // If ALL groups that exist require 2 players and we don't have 2 yet, do nothing.
        // But if some groups don't require 2 players, we can still spawn those now.
        bool spawnedAnything = false;

        foreach (var group in runtimeSpawnGroups)
        {
            if (group == null || group.prefab == null)
                continue;

            if (group.requireTwoPlayers && activeCount < 2)
                continue;

            if (group.spawnPoints == null || group.spawnPoints.Count == 0)
                continue;

            foreach (Transform t in group.spawnPoints)
            {
                if (t == null) continue;

                NetworkObject obj = Instantiate(group.prefab, t.position, t.rotation);

                if (group.spawnWithNoOwner)
                    _server.Spawn(obj);
                else
                    _server.Spawn(obj); // Placeholder: keep simple; add owner policies later if desired.

                _spawnedRuntimeObjects.Add(obj);
                spawnedAnything = true;
            }
        }

        // Mark as spawned only if we actually spawned something.
        // This way, if you load in with 1 player and all groups require 2,
        // it will try again when the second player joins.
        if (spawnedAnything)
        {
            _runtimeSpawned = true;
            Debug.Log($"PlayerSpawner[{gameObject.scene.name}]: Spawned {_spawnedRuntimeObjects.Count} runtime objects (spawn once).");
        }
    }

    public void DespawnAllRuntimeSpawns()
    {
        if (!InstanceFinder.IsServerStarted || _server == null || !_server.Started)
            return;

        foreach (var obj in _spawnedRuntimeObjects)
        {
            if (obj != null && obj.IsSpawned)
                _server.Despawn(obj);
        }

        _spawnedRuntimeObjects.Clear();
        _runtimeSpawned = false;

        Debug.Log($"PlayerSpawner[{gameObject.scene.name}]: Despawned all runtime objects.");
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

    // -----------------------------
    // HELPERS
    // -----------------------------
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

    private int GetActiveConnectionCount()
    {
        int count = 0;
        foreach (NetworkConnection c in _server.Clients.Values)
        {
            if (c != null && c.IsActive)
                count++;
        }
        return count;
    }
}
