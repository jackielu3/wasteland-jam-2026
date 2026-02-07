using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
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


    [Header("Animation")]
    [SerializeField] private Animator animator;

    private readonly SyncVar<Vector2> _netInputDir = new();
    private readonly SyncVar<Vector2> _netLastDir = new(Vector2.down);
    private readonly SyncVar<bool> _netIsWalking = new();

    private Rigidbody2D _rb;
    private Vector2 _localInput;
    private Vector2 _serverMoveInput;

    private float _nextSendTime;

    private Vector2 _localLastDir = Vector2.down;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        _netInputDir.OnChange += OnAnimStateChanged;
        _netLastDir.OnChange += OnAnimStateChanged;
        _netIsWalking.OnChange += (prev, next, asServer) => OnAnimStateChanged(Vector2.zero, Vector2.zero, asServer);

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

        Vector2 inputDir = QuantizeToCardinal(_localInput);
        bool walking = inputDir != Vector2.zero;

        if (walking)
            _localLastDir = inputDir;

        ApplyAnimator(inputDir, _localLastDir, walking);

        // Throttle network sends
        if (Time.time < _nextSendTime)
            return;

        _nextSendTime = Time.time + inputSendInterval;

        SetAnimStateServerRpc(inputDir, _localLastDir, walking);
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

    private void OnAnimStateChanged(Vector2 prev, Vector2 next, bool asServer)
    {
        ApplyAnimator(_netInputDir.Value, _netLastDir.Value, _netIsWalking.Value);
    }

    private static Vector2 QuantizeToCardinal(Vector2 v)
    {
        if (v.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return new Vector2(Mathf.Sign(v.x), 0f);
        else
            return new Vector2(0f, Mathf.Sign(v.y));
    }

    private void ApplyAnimator(Vector2 inputDir, Vector2 lastDir, bool isWalking)
    {
        if (animator == null)
            return;

        animator.SetFloat("InputX", inputDir.x);
        animator.SetFloat("InputY", inputDir.y);
        animator.SetFloat("LastInputX", lastDir.x);
        animator.SetFloat("LastInputY", lastDir.y);
        animator.SetBool("isWalking", isWalking);
    }

    [ServerRpc]
    private void SetMoveInputServerRpc(Vector2 input, NetworkConnection conn = null)
    {
        _serverMoveInput = input;
    }

    [ServerRpc]
    private void SetAnimStateServerRpc(Vector2 inputDir, Vector2 lastDir, bool isWalking, NetworkConnection conn = null)
    {
        _netInputDir.Value = inputDir;
        _netLastDir.Value = lastDir;
        _netIsWalking.Value = isWalking;
    }
}
