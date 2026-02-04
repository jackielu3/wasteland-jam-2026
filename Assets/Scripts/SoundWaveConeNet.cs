using FishNet.Object;
using UnityEngine;

public class SoundWaveConeNet : NetworkBehaviour
{
    [Header("Cone Shape")]
    [SerializeField] private float maxRange = 8f;
    [SerializeField] private float growthSpeed = 18f;
    [SerializeField] private float maxAngleDegrees = 45f;
    [SerializeField] private bool capByMaxWidth = true;
    [SerializeField] private float maxWidthAtFullRange = 5f;

    [Header("Walls / Occlusion")]
    [SerializeField] private LayerMask wallMask;
    [SerializeField] private int rayCount = 9;

    [Header("Damage")]
    [SerializeField] private float dps = 10f;
    [SerializeField] private LayerMask hittableMask;

    [Header("Visuals")]
    [SerializeField] private LineRenderer[] arcs;
    [SerializeField] private int arcSegments = 32;
    [SerializeField] private int arcCount = 6;
    [SerializeField] private float rippleSpeed = 4f;
    [SerializeField] private float rippleSpacing = 1.0f;

    private PolygonCollider2D _poly;
    private Rigidbody2D _rb;

    private Transform _source;
    private Vector2 _aimDir = Vector2.right;
    private bool _active;

    private float _currentFront;
    private float _desiredFront;

    private float _effectiveRange;
    private float _nextObsSend;

    private bool _visualsEnabled = true;

    private void Awake()
    {
        _poly = GetComponent<PolygonCollider2D>();
        _rb = GetComponent<Rigidbody2D>();

        if (_poly != null)
            _poly.isTrigger = true;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsServerStarted)
        {
            if (_poly != null) _poly.enabled = true;

            if (_rb != null)
            {
                _rb.simulated = true;
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
        }

        ApplyVisualsEnabled(_visualsEnabled);
    }

    // ---------------- SERVER API ----------------

    public void ServerAttachTo(Transform source)
    {
        if (!IsServerStarted) return;
        _source = source;
    }

    public void ServerSetAim(Vector2 aimDir)
    {
        if (!IsServerStarted) return;
        if (aimDir.sqrMagnitude < 0.0001f) return;
        _aimDir = aimDir.normalized;
    }

    public void ServerSetDesiredRange(float range)
    {
        if (!IsServerStarted) return;
        _desiredFront = Mathf.Clamp(range, 0f, maxRange);
    }

    public void ServerSetVisualsEnabled(bool enabled)
    {
        if (!IsServerStarted) return;
        _visualsEnabled = enabled;
        ObsSetVisualsEnabled(enabled);
        ApplyVisualsEnabled(enabled);
    }

    [ObserversRpc(BufferLast = true)]
    private void ObsSetVisualsEnabled(bool enabled)
    {
        _visualsEnabled = enabled;
        ApplyVisualsEnabled(enabled);
    }

    private void ApplyVisualsEnabled(bool enabled)
    {
        if (arcs == null) return;
        for (int i = 0; i < arcs.Length; i++)
        {
            if (arcs[i] != null)
                arcs[i].enabled = enabled;
        }
    }

    public void ServerSetActive(bool active)
    {
        if (!IsServerStarted) return;
        _active = active;
        if (!active)
        {
            _currentFront = 0f;
            _desiredFront = 0f;
            _effectiveRange = 0f;
        }
    }

    // ---------------- UPDATE (SERVER DRIVES STATE) ----------------

    private void Update()
    {
        if (!IsServerStarted) return;

        if (_source != null)
            transform.position = _source.position;

        if (!_active) return;

        _currentFront = Mathf.MoveTowards(
            _currentFront,
            Mathf.Clamp(_desiredFront, 0f, maxRange),
            growthSpeed * Time.deltaTime
        );

        float halfAngleRad = ComputeHalfAngle(_currentFront);
        float blockedRange = ComputeBlockedRange(_aimDir, halfAngleRad, _currentFront);
        _effectiveRange = Mathf.Min(_currentFront, blockedRange);

        UpdateWedgeCollider(_aimDir, halfAngleRad, _effectiveRange);

        if (Time.time >= _nextObsSend)
        {
            _nextObsSend = Time.time + 0.05f;
            ServerBroadcastState(_aimDir, halfAngleRad, _effectiveRange);
        }
    }

    private float ComputeHalfAngle(float range)
    {
        float halfAngleRad = (maxAngleDegrees * Mathf.Deg2Rad) * 0.5f;

        if (capByMaxWidth && range > 0.01f)
        {
            float maxHalfWidth = maxWidthAtFullRange * 0.5f;
            float widthLimitedHalfAngle = Mathf.Atan2(maxHalfWidth, range);
            halfAngleRad = Mathf.Min(halfAngleRad, widthLimitedHalfAngle);
        }

        return halfAngleRad;
    }

    private float ComputeBlockedRange(Vector2 aim, float halfAngleRad, float range)
    {
        if (range <= 0.01f)
            return 0f;

        float best = range;

        int count = Mathf.Max(3, rayCount);
        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0.5f : i / (float)(count - 1);
            float angle = Mathf.Lerp(-halfAngleRad, halfAngleRad, t);
            Vector2 dir = Rotate(aim, angle);

            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, range, wallMask);
            if (hit.collider != null)
                best = Mathf.Min(best, hit.distance);
        }

        return best;
    }

    private void UpdateWedgeCollider(Vector2 aim, float halfAngleRad, float range)
    {
        if (_poly == null) return;

        int points = Mathf.Max(6, arcSegments);
        Vector2[] verts = new Vector2[points + 2];
        verts[0] = Vector2.zero;

        for (int i = 0; i <= points; i++)
        {
            float t = i / (float)points;
            float a = Mathf.Lerp(-halfAngleRad, halfAngleRad, t);
            Vector2 dir = Rotate(aim, a);
            Vector2 pWorld = (Vector2)transform.position + dir * range;
            verts[i + 1] = transform.InverseTransformPoint(pWorld);
        }

        _poly.SetPath(0, verts);
    }

    private void ServerBroadcastState(Vector2 aim, float halfAngleRad, float range)
    {
        ObsSetState((Vector2)transform.position, aim, halfAngleRad, range);
    }

    [ObserversRpc]
    private void ObsSetState(Vector2 originWorld, Vector2 aim, float halfAngleRad, float range)
    {
        transform.position = originWorld;
        UpdateWedgeCollider(aim, halfAngleRad, range);
    }

    private static Vector2 Rotate(Vector2 v, float rad)
    {
        float s = Mathf.Sin(rad);
        float c = Mathf.Cos(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
