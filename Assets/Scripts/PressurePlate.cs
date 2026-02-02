using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PressurePlate : NetworkBehaviour
{
    private readonly SyncVar<bool> _isPressed = new();

    public bool IsPressed => _isPressed.Value;

    private int _insideCount;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        _isPressed.OnChange += OnPressedChanged;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        _isPressed.OnChange -= OnPressedChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _insideCount = 0;
        _isPressed.Value = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServerInitialized) return;

        Debug.Log("Trigger hit by: " + other.name);

        if (!other.CompareTag("Player")) return;

        _insideCount++;
        UpdatePressed();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServerInitialized) return;

        Debug.Log("Trigger left by: " + other.name);

        if (!other.CompareTag("Player")) return;

        _insideCount = Mathf.Max(0, _insideCount - 1);
        UpdatePressed();
    }

    [Server]
    private void UpdatePressed()
    {
        bool pressed = _insideCount > 0;
        if (_isPressed.Value != pressed)
            _isPressed.Value = pressed;
    }

    private void OnPressedChanged(bool oldValue, bool newValue, bool asServer)
    {
        // Optional: visuals / sound here
        // This runs on BOTH server and clients
    }
}
