using FishNet;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ConnectionUI : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private RelayConnectionManager relayConnectionManager;

    [Header("Canvases")]
    [SerializeField] private GameObject mainMenuCanvas;
    [SerializeField] private GameObject lobbyCanvas;

    [Header("UI Elements")]
    [SerializeField] private TMP_Text hostKeyText;

    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private GameObject hostButtonRoot;
    [SerializeField] private GameObject clientButtonRoot;
    [SerializeField] private GameObject startButtonRoot;
    [SerializeField] private GameObject backButtonRoot;

    [Header("Scene")]
    [SerializeField] private int gameplaySceneBuildIndex = -1;

    private Button _hostButton;
    private Button _clientButton;
    private Button _startButton;
    private Button _backButton;

    private bool _isHosting;
    private bool _hasRemoteClient;
    private bool _isShuttingDown;

    private GameObject _localLobbyAvatar;

    private void Awake()
    {
        CacheButtons();

        if (joinCodeInput != null)
        {
            joinCodeInput.characterLimit = 6;
            joinCodeInput.onValueChanged.AddListener(OnJoinCodeChanged);

            OnJoinCodeChanged(joinCodeInput.text);
        }

        SetStartVisible(false);
        SetHostKey("");
    }

    private void OnEnable()
    {
        HookFishNetEvents();
    }

    private void OnDisable()
    {
        UnhookFishNetEvents();
    }

    private void CacheButtons()
    {
        _hostButton = hostButtonRoot ? hostButtonRoot.GetComponentInChildren<Button>(true) : null;
        _clientButton = clientButtonRoot ? clientButtonRoot.GetComponentInChildren<Button>(true) : null;
        _startButton = startButtonRoot ? startButtonRoot.GetComponentInChildren<Button>(true) : null;
        _backButton = backButtonRoot ? backButtonRoot.GetComponentInChildren<Button>(true) : null;
    }

    public async void PressHost()
    {
        if (_isShuttingDown) return;
        if (_isHosting) return;

        _isHosting = true;

        if (_hostButton != null) _hostButton.interactable = false;
        if (_clientButton != null) _clientButton.interactable = false;

        try
        {
            string code = await relayConnectionManager.HostOnlineAsync(1);
            SetHostKey(code);

            SetStartVisible(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Host failed: {e}");

            _isHosting = false;
            if (_hostButton != null) _hostButton.interactable = true;
            if (_clientButton != null) _clientButton.interactable = IsJoinCodeValid(joinCodeInput ? joinCodeInput.text : "");
            DespawnLobbyAvatar();
            SetHostKey("");
        }
    }

    public async void PressClient()
    {
        if (_isShuttingDown) return;
        if (_isHosting) return;

        string code = joinCodeInput ? joinCodeInput.text : "";
        if (!IsJoinCodeValid(code))
            return;

        if (_hostButton != null) _hostButton.interactable = false;
        if (_clientButton != null) _clientButton.interactable = false;

        try
        {
            await relayConnectionManager.JoinOnlineAsync(code);
            SetStartVisible(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Join failed: {e}");

            if (_hostButton != null) _hostButton.interactable = true;
            if (_clientButton != null) _clientButton.interactable = true;

            DespawnLobbyAvatar();
        }
    }

    public void PressStartGame()
    {
        if (_isShuttingDown) return;
        if (!_isHosting) return;
        if (!_hasRemoteClient) return;

        if (gameplaySceneBuildIndex < 0)
        {
            Debug.LogError("Gameplay scene build index is invalid. Set gameplaySceneBuildIndex in the inspector.");
            return;
        }

        string scenePath = SceneUtility.GetScenePathByBuildIndex(gameplaySceneBuildIndex);
        if (string.IsNullOrEmpty(scenePath))
        {
            Debug.LogError(
                $"No scene path found for build index {gameplaySceneBuildIndex}. " +
                $"Make sure the scene is added to Build Settings and the index is correct."
            );
            return;
        }

        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"Could not resolve scene name from path '{scenePath}'.");
            return;
        }

        Debug.Log($"[Lobby] Loading gameplay scene by index {gameplaySceneBuildIndex} -> '{sceneName}'");

        SceneLoadData sld = new SceneLoadData(sceneName)
        {
            ReplaceScenes = ReplaceOption.All
        };

        PlayerSpawner lobbySpawner = FindFirstObjectByType<PlayerSpawner>();
        if (lobbySpawner != null)
            lobbySpawner.DespawnAllSpawnedPlayers();

        networkManager.SceneManager.LoadGlobalScenes(sld);
    }


    public void PressBack()
    {
        if (_isShuttingDown) return;

        bool stopServerToo = _isHosting;

        ShutdownAndReset(stopServerToo);
    }

    private void OnJoinCodeChanged(string raw)
    {
        if (joinCodeInput == null) return;

        string cleaned = CleanJoinCode(raw);

        if (cleaned != raw)
        {
            int caret = joinCodeInput.caretPosition;
            joinCodeInput.SetTextWithoutNotify(cleaned);
            joinCodeInput.caretPosition = Mathf.Clamp(caret, 0, cleaned.Length);
        }

        if (_clientButton != null && !_isHosting)
            _clientButton.interactable = IsJoinCodeValid(cleaned);
    }

    private static string CleanJoinCode(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        StringBuilder sb = new StringBuilder(6);
        for (int i = 0; i < raw.Length && sb.Length < 6; i++)
        {
            char c = char.ToUpperInvariant(raw[i]);
            bool isAZ = (c >= 'A' && c <= 'Z');
            bool is09 = (c >= '0' && c <= '9');
            if (isAZ || is09)
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static bool IsJoinCodeValid(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;
        if (code.Length != 6) return false;

        for (int i = 0; i < 6; i++)
        {
            char c = code[i];
            bool isAZ = (c >= 'A' && c <= 'Z');
            bool is09 = (c >= '0' && c <= '9');
            if (!isAZ && !is09)
                return false;
        }
        return true;
    }

    private void HookFishNetEvents()
    {
        if (networkManager == null) return;

        networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
    }

    private void UnhookFishNetEvents()
    {
        if (networkManager == null) return;

        networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            if (_isHosting && !_isShuttingDown)
                ShutdownAndReset(stopServerToo: true);
        }
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped && !_isShuttingDown)
        {
            ShutdownAndReset(stopServerToo: false);
        }
    }

    private void OnRemoteConnectionState(FishNet.Connection.NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (!_isHosting) return;

        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            _hasRemoteClient = true;
            SetStartVisible(true);
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            _hasRemoteClient = false;
            SetStartVisible(false);
        }
    }

    private void ShutdownAndReset(bool stopServerToo)
    {
        _isShuttingDown = true;

        if (relayConnectionManager != null)
            relayConnectionManager.StopAllConnections(stopServerToo);
        else if (networkManager != null)
        {
            networkManager.ClientManager.StopConnection();
            if (stopServerToo)
                networkManager.ServerManager.StopConnection(true);
        }

        _isHosting = false;
        _hasRemoteClient = false;

        SetStartVisible(false);
        SetHostKey("");
        DespawnLobbyAvatar();

        if (joinCodeInput != null)
        {
            joinCodeInput.SetTextWithoutNotify("");
            OnJoinCodeChanged("");
        }

        if (_hostButton != null) _hostButton.interactable = true;
        if (_clientButton != null) _clientButton.interactable = IsJoinCodeValid(joinCodeInput ? joinCodeInput.text : "");

        mainMenuCanvas.SetActive(true);
        lobbyCanvas.SetActive(false);

        _isShuttingDown = false;
    }

    private void SetStartVisible(bool visible)
    {
        if (startButtonRoot == null) return;

        bool shouldShow = visible && _isHosting;
        startButtonRoot.SetActive(shouldShow);

        if (_startButton != null)
            _startButton.interactable = shouldShow;
    }

    private void SetHostKey(string joinCode)
    {
        if (hostKeyText == null) return;

        if (string.IsNullOrWhiteSpace(joinCode))
            hostKeyText.text = "";
        else
            hostKeyText.text = $"HOST KEY: {joinCode}";
    }

    private void DespawnLobbyAvatar()
    {
        if (_localLobbyAvatar == null) return;
        Destroy(_localLobbyAvatar);
        _localLobbyAvatar = null;
    }

    private void Reset()
    {
        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();
        if (relayConnectionManager == null)
            relayConnectionManager = FindFirstObjectByType<RelayConnectionManager>();
    }
}
