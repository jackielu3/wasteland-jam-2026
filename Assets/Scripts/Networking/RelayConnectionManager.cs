using System;
using System.Threading.Tasks;
using FishNet.Managing;
using TMPro;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using FishNet.Transporting.UTP;

public class RelayConnectionManager : MonoBehaviour
{
    [Header("FishNet")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport unityTransport;

    [Header("UI")]
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_InputField joinCodeInput;

    private bool servicesReady;

    async void Awake()
    {
        await InitUnityServices();
    }

    async Task InitUnityServices()
    {
        if (servicesReady) return;

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        servicesReady = true;
    }

    // =========================
    // HOST
    // =========================
    public async void HostOnline()
    {
        try
        {
            await InitUnityServices();

            // 1 client max (2 players total)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            joinCodeText.text = $"JOIN CODE: {joinCode}";

            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            unityTransport.SetRelayServerData(relayServerData);

            networkManager.ServerManager.StartConnection();
            networkManager.ClientManager.StartConnection();
        }
        catch (Exception e)
        {
            Debug.LogError($"Host failed: {e}");
        }
    }

    // =========================
    // CLIENT
    // =========================
    public async void JoinOnline()
    {
        try
        {
            await InitUnityServices();

            string joinCode = joinCodeInput.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("Join code is empty.");
                return;
            }

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            unityTransport.SetRelayServerData(relayServerData);

            networkManager.ClientManager.StartConnection();
        }
        catch (Exception e)
        {
            Debug.LogError($"Join failed: {e}");
        }
    }
}
