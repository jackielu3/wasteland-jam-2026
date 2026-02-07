using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PushablePresserNet : NetworkBehaviour
{
    [Header("Ownership Handoff")]
    [Tooltip("How often ownership is allowed to change while being contacted (prevents thrashing when both push).")]
    [SerializeField] private float minOwnershipSwitchInterval = 0.20f;

    [Header("Client Physics")]
    [SerializeField] private float linearDamping = 8f;
    [SerializeField] private bool freezeRotation = true;

    private Rigidbody2D _rb;

    private float _nextAllowedSwitchTime;

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
            if (Owner.IsLocalClient)
                SetupDynamicBody();
            else
                SetupKinematicBody();
        }
    }

    public override void OnOwnershipClient(NetworkConnection prevOwner)
    {
        base.OnOwnershipClient(prevOwner);

        if (!IsClientStarted)
            return;

        if (Owner.IsLocalClient)
            SetupDynamicBody();
        else
            SetupKinematicBody();
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsServerStarted)
            return;

        TryHandoffOwnershipFromCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!IsServerStarted)
            return;

        TryHandoffOwnershipFromCollision(collision);
    }

    private void TryHandoffOwnershipFromCollision(Collision2D collision)
    {
        if (Time.time < _nextAllowedSwitchTime)
            return;

        NetworkObject nob = collision.collider.GetComponentInParent<NetworkObject>();
        if (nob == null)
            return;

        NetworkConnection toucher = nob.Owner;
        if (toucher == null)
            return;

        if (Owner == toucher)
            return;

        GiveOwnership(toucher);
        _nextAllowedSwitchTime = Time.time + minOwnershipSwitchInterval;
    }
}
