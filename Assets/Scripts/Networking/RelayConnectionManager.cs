using System;
using System.Threading.Tasks;
using FishNet.Managing;
using FishNet.Transporting.UTP;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class RelayConnectionManager : MonoBehaviour
{
    [Header("FishNet")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport unityTransport;

    private bool servicesReady;

    private async void Awake()
    {
        try
        {
            await InitUnityServices();
        }
        catch (Exception e)
        {
            Debug.LogError($"Unity Services init failed in Awake: {e}");
        }
    }

    public async Task InitUnityServices()
    {
        if (servicesReady) return;

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        servicesReady = true;
    }

    public async Task<string> HostOnlineAsync(int maxRemoteClients = 1)
    {
        await InitUnityServices();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxRemoteClients);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
        unityTransport.SetRelayServerData(relayServerData);

        networkManager.ServerManager.StartConnection();
        networkManager.ClientManager.StartConnection();

        return joinCode;
    }

    public async Task JoinOnlineAsync(string joinCode)
    {
        await InitUnityServices();

        string cleaned = (joinCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(cleaned))
            throw new ArgumentException("Join code is empty.");

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(cleaned);

        var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
        unityTransport.SetRelayServerData(relayServerData);

        networkManager.ClientManager.StartConnection();
    }

    public void StopAllConnections(bool stopServerToo)
    {
        if (networkManager == null) return;

        networkManager.ClientManager.StopConnection();
        if (stopServerToo)
            networkManager.ServerManager.StopConnection(true);
    }

    public async void HostOnline()
    {
        try
        {
            await HostOnlineAsync(1);
        }
        catch (Exception e)
        {
            Debug.LogError($"Host failed: {e}");
        }
    }

    public async void JoinOnline()
    {
        Debug.LogWarning("JoinOnline() called without passing a join code. Use JoinOnlineAsync(joinCode) from ConnectionUI.");
    }

    private void Reset()
    {
        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();
        if (unityTransport == null)
            unityTransport = FindFirstObjectByType<UnityTransport>();
    }
}
