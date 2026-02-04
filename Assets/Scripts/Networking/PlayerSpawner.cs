using FishNet;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private NetworkObject hostPlayerPrefab;
    [SerializeField] private NetworkObject clientPlayerPrefab;

    [SerializeField] private Transform hostSpawn;
    [SerializeField] private Transform clientSpawn;

    private ServerManager _server;

    private int _hostClientId = -1;

    private void Awake()
    {
        _server = InstanceFinder.ServerManager;
    }

    private void OnEnable()
    {
        _server.OnRemoteConnectionState += OnRemoteConnectionState;
        _server.OnServerConnectionState += OnServerConnectionState;
    }

    private void OnDisable()
    {
        _server.OnRemoteConnectionState -= OnRemoteConnectionState;
        _server.OnServerConnectionState -= OnServerConnectionState;
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
            _hostClientId = -1;
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Started)
            return;

        SpawnPlayerFor(conn);
    }

    private void SpawnPlayerFor(NetworkConnection conn)
    {
        if (_hostClientId == -1)
            _hostClientId = conn.ClientId;

        bool isHostPlayer = (conn.ClientId == _hostClientId);

        NetworkObject prefab = isHostPlayer ? hostPlayerPrefab : clientPlayerPrefab;
        Transform spawn = isHostPlayer ? hostSpawn : clientSpawn;

        Vector3 pos = spawn ? spawn.position : Vector3.zero;
        Quaternion rot = spawn ? spawn.rotation : Quaternion.identity;

        NetworkObject player = Instantiate(prefab, pos, rot);
        InstanceFinder.ServerManager.Spawn(player, conn);
    }
}
