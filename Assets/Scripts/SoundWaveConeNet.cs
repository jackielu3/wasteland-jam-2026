using FishNet.Object;
using UnityEngine;

public class SoundWaveConeNet : NetworkBehaviour
{
    [Header("Cone Shape")]
    [SerializeField] private float maxRange = 8f;
    [SerializeField] private float growthSpeed = 18f;       // how fast the wave front reaches max
    [SerializeField] private float maxAngleDegrees = 45f;   // widest cone angle
    [SerializeField] private bool capByMaxWidth = true;
    [SerializeField] private float maxWidthAtFullRange = 5f; // clamp width at far edge

    [Header("Walls / Occlusion")]
    [SerializeField] private LayerMask wallMask;
    [SerializeField] private int rayCount = 9; // more rays = better occlusion

    [Header("Damage")]
    [SerializeField] private float dps = 10f;
    [SerializeField] private LayerMask hittableMask;

    [Header("Visuals")]
    [SerializeField] private LineRenderer[] arcs;
    [SerializeField] private int arcSegments = 32;
    [SerializeField] private int arcCount = 6;
    [SerializeField] private float rippleSpeed = 4f;   // how fast ripples move outward
    [SerializeField] private float rippleSpacing = 1.0f;

    private PolygonCollider2D _poly;
    private Rigidbody2D _rb;

    private Transform _source;
    private Vector2 _aimDir = Vector2.right;
    private bool _active;

    private float _currentFront;     // how far the “wave front” has grown
    private float _effectiveRange;   // clamped by wall

    // client-side cached for rendering
    private Vector2 _renderAimDir;
    private float _renderRange;
    private float _renderHalfAngleRad;

    // throttled observer updates
    private float _nextObsSend;

    private void Awake()
    {
        _poly = GetComponent<PolygonCollider2D>();
        _rb = GetComponent<Rigidbody2D>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // clients: no physics/damage authority; keep collider disabled to avoid double trigger weirdness
        if (!IsServer)
        {
            if (_poly != null) _poly.enabled = false;
            if (_rb != null) _rb.simulated = false;
        }
    }

    // ---------------- SERVER API ----------------

    public void ServerAttachTo(Transform source)
    {
        if (!IsServer) return;
        _source = source;
    }

    public void ServerSetAim(Vector2 aimDir)
    {
        if (!IsServer) return;
        if (aimDir.sqrMagnitude < 0.0001f) return;
        _aimDir = aimDir.normalized;
    }

    public void ServerSetActive(bool active)
    {
        if (!IsServer) return;
        _active = active;
        if (!active)
        {
            _currentFront = 0f;
            _effectiveRange = 0f;
            ServerBroadcastState(_aimDir, 0f, 0f);
        }
    }

    // ---------------- SERVER LOOP ----------------

    private void Update()
    {
        if (!IsServer) return;

        if (_source != null)
            transform.position = _source.position;

        if (!_active) return;

        // Grow the wave front toward maxRange
        _currentFront = Mathf.Min(maxRange, _currentFront + growthSpeed * Time.deltaTime);

        // Find occluded distance inside cone
        float halfAngleRad = ComputeHalfAngle(_currentFront);
        float blockedRange = ComputeBlockedRange(_aimDir, halfAngleRad, _currentFront);
        _effectiveRange = Mathf.Min(_currentFront, blockedRange);

        // Update server collider wedge
        UpdateWedgeCollider(_aimDir, halfAngleRad, _effectiveRange);

        // Send state to clients at ~20 Hz (enough for smooth visuals)
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
            // width at far edge = 2 * range * tan(halfAngle)
            // clamp halfAngle so width doesn't exceed maxWidthAtFullRange
            float halfWidth = maxWidthAtFullRange * 0.5f;
            float cappedHalfAngle = Mathf.Atan2(halfWidth, range);
            halfAngleRad = Mathf.Min(halfAngleRad, cappedHalfAngle);
        }

        return halfAngleRad;
    }

    private float ComputeBlockedRange(Vector2 dir, float halfAngleRad, float maxCheck)
    {
        // Cast several rays across the cone; take the nearest hit as the stop distance.
        float nearest = maxCheck;

        float baseAngle = Mathf.Atan2(dir.y, dir.x);

        if (rayCount < 2) rayCount = 2;

        for (int i = 0; i < rayCount; i++)
        {
            float t = (rayCount == 1) ? 0.5f : (float)i / (rayCount - 1);
            float ang = baseAngle + Mathf.Lerp(-halfAngleRad, halfAngleRad, t);
            Vector2 rdir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

            RaycastHit2D hit = Physics2D.Raycast(transform.position, rdir, maxCheck, wallMask);
            if (hit.collider != null)
                nearest = Mathf.Min(nearest, hit.distance);
        }

        return nearest;
    }

    private void UpdateWedgeCollider(Vector2 dir, float halfAngleRad, float range)
    {
        if (_poly == null) return;

        // Wedge polygon: origin + two edge points + a few arc points for smoother shape
        int arcPts = 8;
        Vector2[] pts = new Vector2[arcPts + 2];
        pts[0] = Vector2.zero;

        float baseAngle = Mathf.Atan2(dir.y, dir.x);

        for (int i = 0; i <= arcPts; i++)
        {
            float t = (float)i / arcPts;
            float ang = baseAngle + Mathf.Lerp(-halfAngleRad, halfAngleRad, t);
            pts[i + 1] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * range;
        }

        _poly.SetPath(0, pts);
    }

    // ---------------- DAMAGE (SERVER) ----------------
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsServer || !_active) return;
        if (((1 << other.gameObject.layer) & hittableMask) == 0) return;

        // Future-proof: let anything react to sound
        var reactive = other.GetComponent<ISoundReactive>();
        if (reactive != null)
        {
            reactive.OnSoundStay(this, dps * Time.deltaTime);
            return;
        }

        // Or do your enemy health check here
        // other.GetComponent<Health>()?.ApplyDamage(dps * Time.deltaTime);
    }

    // ---------------- CLIENT VISUALS ----------------
    private void LateUpdate()
    {
        if (IsServer)
        {
            // server can also render if you want; leaving enabled is fine
            _renderAimDir = _aimDir;
            _renderHalfAngleRad = ComputeHalfAngle(Mathf.Max(_effectiveRange, 0.01f));
            _renderRange = _effectiveRange;
        }

        RenderRipples(_renderAimDir, _renderHalfAngleRad, _renderRange);
    }

    private void RenderRipples(Vector2 dir, float halfAngleRad, float range)
    {
        if (arcs == null || arcs.Length == 0) return;
        if (range <= 0.01f)
        {
            foreach (var lr in arcs) if (lr) lr.enabled = false;
            return;
        }

        float baseAngle = Mathf.Atan2(dir.y, dir.x);

        // ensure enabled count
        for (int i = 0; i < arcs.Length; i++)
            if (arcs[i]) arcs[i].enabled = (i < arcCount);

        float time = Time.time * rippleSpeed;

        for (int a = 0; a < Mathf.Min(arcCount, arcs.Length); a++)
        {
            LineRenderer lr = arcs[a];
            if (!lr) continue;

            lr.useWorldSpace = false;
            lr.positionCount = arcSegments;

            // Each arc is at a different radius; animated to “travel outward”
            float r = Mathf.Repeat(time + a * rippleSpacing, range);
            // Make it feel like slivers near the front: bias toward outer area
            r = Mathf.Lerp(range * 0.2f, range, r / Mathf.Max(range, 0.01f));

            for (int i = 0; i < arcSegments; i++)
            {
                float t = (arcSegments == 1) ? 0.5f : (float)i / (arcSegments - 1);
                float ang = baseAngle + Mathf.Lerp(-halfAngleRad, halfAngleRad, t);

                // slight waviness so it feels “soundy”
                float wobble = 0.08f * Mathf.Sin((t * 8f + Time.time * 6f) + a);
                float rr = r * (1f + wobble);

                Vector3 p = new Vector3(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr, 0f);
                lr.SetPosition(i, p);
            }
        }
    }

    // ---------------- NETWORK SYNC ----------------
    [ObserversRpc(BufferLast = true)]
    private void ServerBroadcastState(Vector2 aimDir, float halfAngleRad, float range)
    {
        _renderAimDir = aimDir.sqrMagnitude < 0.0001f ? Vector2.right : aimDir.normalized;
        _renderHalfAngleRad = halfAngleRad;
        _renderRange = range;
    }
}

// Future-proof hook for puzzles/enemies/relays
public interface ISoundReactive
{
    void OnSoundStay(SoundWaveConeNet source, float soundAmount);
}
