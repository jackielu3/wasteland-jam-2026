using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using TMPro;
using UnityEngine;

public class ConnectionUI : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private Tugboat tugboat;

    [Header("UI")]
    [SerializeField] private TMP_InputField addressInput;

    public void StartHost()
    {
        ApplyClientAddressFromInput();

        networkManager.ServerManager.StartConnection();
        networkManager.ClientManager.StartConnection();
    }

    public void StartClient()
    {
        ApplyClientAddressFromInput();
        networkManager.ClientManager.StartConnection();
    }

    public void StopAll()
    {
        networkManager.ClientManager.StopConnection();
        networkManager.ServerManager.StopConnection(true);
    }

    private void ApplyClientAddressFromInput()
    {
        if (tugboat == null || addressInput == null) return;

        var addr = addressInput.text.Trim();
        if (string.IsNullOrEmpty(addr))
            addr = "localhost";

        tugboat.SetClientAddress(addr);
    }

    private void Reset()
    {
        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();
        if (tugboat == null)
            tugboat = FindFirstObjectByType<Tugboat>();
    }
}
