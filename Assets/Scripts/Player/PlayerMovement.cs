using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;

    [Header("Stability")]
    [SerializeField] private float linearDamping = 12f;
    [SerializeField] private bool freezeRotation = true;

    [Tooltip("How often the owner sends input to server (seconds).")]
    [SerializeField] private float inputSendInterval = 0.05f;

    private Rigidbody2D _rb;

    private Vector2 _localInput;

    private Vector2 _serverMoveInput;

    private float _nextSendTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (IsServerStarted)
        {
            SetupDynamicBody();
        }
        else if (IsClientStarted)
        {
            if (base.Owner.IsLocalClient)
            {
                SetupDynamicBody();
            }
            else
            {
                SetupKinematicBody();
            }
        }
    }

    private void SetupDynamicBody()
    {
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.simulated = true;

        _rb.linearDamping = linearDamping;
        _rb.angularDamping = 999f;

        if (freezeRotation)
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void SetupKinematicBody()
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.simulated = true;

        if (freezeRotation)
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Update()
    {
        if (!IsOwner || !IsClientStarted)
            return;

        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        _localInput = new Vector2(x, y);
        if (_localInput.sqrMagnitude > 1f)
            _localInput.Normalize();

        // Throttle RPC sends.
        if (Time.time < _nextSendTime)
            return;

        _nextSendTime = Time.time + inputSendInterval;
        SetMoveInputServerRpc(_localInput);
    }

    private void FixedUpdate()
    {
        if (IsServerStarted)
        {
            ApplyVelocity(_serverMoveInput);
            return;
        }

        if (IsClientStarted && IsOwner)
        {
            ApplyVelocity(_localInput);
        }
    }

    private void ApplyVelocity(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        _rb.linearVelocity = input * speed;
    }

    [ServerRpc]
    private void SetMoveInputServerRpc(Vector2 input, NetworkConnection conn = null)
    {
        _serverMoveInput = input;
    }
}
