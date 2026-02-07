using FishNet;
using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public class SoundWaveArcNet : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private int segments = 24;
    [Tooltip("LineRenderer thickness (visual).")]
    [SerializeField] private float lineWidth = 0.12f;

    [Header("Shape")]
    [Tooltip("Half-angle in degrees (total arc = 2x).")]
    [SerializeField] private float arcHalfAngleDegrees = 35f;

    [Tooltip("Collider thickness (hitbox ring thickness).")]
    [SerializeField] private float hitboxThickness = 0.5f;

    [Tooltip("If > 0, clamps the OUTER arc length in world units, so it stops getting longer.")]
    [SerializeField] private float maxArcLengthWorld = 0f;

    [Tooltip("When radius reaches this, arc despawns.")]
    [SerializeField] private float maxRadius = 8f;

    [Header("Motion (radius)")]
    [Tooltip("Initial outward speed (units/sec).")]
    [SerializeField] private float startSpeed = 6f;
    [Tooltip("Outward acceleration (units/sec^2).")]
    [SerializeField] private float acceleration = 18f;
    [Tooltip("Max outward speed (units/sec).")]
    [SerializeField] private float maxSpeed = 12f;

    [Header("Walls / Clipping")]
    [SerializeField] private LayerMask wallMask;
    [SerializeField] private bool clipOnWalls = true;
    [Tooltip("Small inset to avoid flicker at collider edges.")]
    [SerializeField] private float wallSkin = 0.02f;

    [Header("Damage")]
    [SerializeField] private LayerMask damageMask;
    [SerializeField] private float damagePerSecond = 10f;

    [Header("Puzzle Buttons (Arc Press)")]
    [SerializeField] private LayerMask arcButtonMask;

    [Header("Deflection")]
    [SerializeField] private LayerMask deflectorMask;
    [SerializeField] private float deflectOffset = 0.05f;


    private LineRenderer _lr;
    private PolygonCollider2D _poly;

    // Network init
    private Vector2 _origin;
    private Vector2 _dir;
    private uint _spawnTick;
    private bool _initialized;

    // Server damage tick
    private float _serverNextDamageTick;

    private void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _poly = GetComponent<PolygonCollider2D>();

        _lr.useWorldSpace = true;

        _lr.widthMultiplier = 1f;
        _lr.startWidth = lineWidth;
        _lr.endWidth = lineWidth;
        _lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        _lr.positionCount = 0;
        _poly.isTrigger = true;
        _poly.pathCount = 0;
    }

    protected override void OnValidate()
    {
        if (_lr == null) _lr = GetComponent<LineRenderer>();
        if (_lr != null)
        {
            _lr.widthMultiplier = 1f;
            _lr.startWidth = lineWidth;
            _lr.endWidth = lineWidth;
            _lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        }

        segments = Mathf.Max(4, segments);
        hitboxThickness = Mathf.Max(0.001f, hitboxThickness);
        maxRadius = Mathf.Max(0.01f, maxRadius);
        maxSpeed = Mathf.Max(0.01f, maxSpeed);
        startSpeed = Mathf.Max(0f, startSpeed);
    }

    // SERVER
    [Server]
    public void Init(Vector2 origin, Vector2 dir, uint serverSpawnTick)
    {
        _origin = origin;
        _dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        _spawnTick = serverSpawnTick;
        _initialized = true;

        // Broadcast init
        ObsInit(_origin, _dir, _spawnTick);
    }

    // CLIENTS
    [ObserversRpc(BufferLast = true)]
    private void ObsInit(Vector2 origin, Vector2 dir, uint serverSpawnTick)
    {
        _origin = origin;
        _dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        _spawnTick = serverSpawnTick;
        _initialized = true;

        BuildArc(GetCurrentRadius());
    }

    private void Update()
    {
        if (!_initialized)
            return;

        if (_lr != null)
        {
            _lr.widthMultiplier = 1f;
            _lr.startWidth = lineWidth;
            _lr.endWidth = lineWidth;
        }

        float radius = GetCurrentRadius();

        if (radius >= maxRadius)
        {
            if (IsServerStarted) Despawn();
            return;
        }

        bool anyVisible = BuildArc(radius);

        if (!anyVisible)
        {
            if (IsServerStarted) Despawn();
        }
    }

    private float GetCurrentRadius()
    {
        uint nowTick = InstanceFinder.TimeManager.Tick;

        float dtPerTick = (float)InstanceFinder.TimeManager.TickDelta;

        float t = (nowTick >= _spawnTick)
            ? (nowTick - _spawnTick) * dtPerTick
            : 0f;

        float v0 = startSpeed;
        float a = acceleration;
        float vmax = Mathf.Max(v0, maxSpeed);

        if (a <= 0.0001f || v0 >= vmax)
            return v0 * t;

        float tToMax = (vmax - v0) / a;

        if (t <= tToMax)
            return v0 * t + 0.5f * a * t * t;

        float distAccel = v0 * tToMax + 0.5f * a * tToMax * tToMax;
        float distConst = vmax * (t - tToMax);
        return distAccel + distConst;
    }

    private bool BuildArc(float radius)
    {
        if (_lr == null || _poly == null)
            return false;

        int n = segments + 1;

        float halfAngleDeg = arcHalfAngleDegrees;
        if (maxArcLengthWorld > 0.0001f && radius > 0.0001f)
        {
            float maxHalfAngleRad = Mathf.Deg2Rad * arcHalfAngleDegrees;
            float halfAngleRadFromLength = (maxArcLengthWorld / radius) * 0.5f;
            float usedHalfAngleRad = Mathf.Min(maxHalfAngleRad, halfAngleRadFromLength);
            halfAngleDeg = usedHalfAngleRad * Mathf.Rad2Deg;
        }

        float baseAngleDeg = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        float startDeg = -halfAngleDeg;
        float endDeg = halfAngleDeg;

        bool[] vis = new bool[n];
        int visibleCount = 0;

        if (!clipOnWalls || wallMask.value == 0)
        {
            for (int i = 0; i < n; i++) vis[i] = true;
            visibleCount = n;
        }
        else
        {
            float rayDist = Mathf.Max(0f, radius - wallSkin);

            for (int i = 0; i < n; i++)
            {
                float u = i / (float)segments;
                float aDeg = baseAngleDeg + Mathf.Lerp(startDeg, endDeg, u);
                Vector2 d = DirFromDeg(aDeg);

                RaycastHit2D hit = Physics2D.Raycast(_origin, d, rayDist, wallMask);
                bool blocked = hit.collider != null;
                vis[i] = !blocked;
                if (vis[i]) visibleCount++;
            }
        }

        if (visibleCount <= 0)
        {
            _lr.positionCount = 0;
            _poly.pathCount = 0;
            return false;
        }

        int bestStart = 0, bestLen = 0;
        int curStart = 0, curLen = 0;

        for (int i = 0; i < n; i++)
        {
            if (vis[i])
            {
                if (curLen == 0) curStart = i;
                curLen++;
            }
            else
            {
                if (curLen > bestLen)
                {
                    bestLen = curLen;
                    bestStart = curStart;
                }
                curLen = 0;
            }
        }
        if (curLen > bestLen)
        {
            bestLen = curLen;
            bestStart = curStart;
        }

        if (bestLen <= 1)
        {
            _lr.positionCount = 0;
            _poly.pathCount = 0;
            return false;
        }

        int bestEnd = bestStart + bestLen - 1;

        _lr.positionCount = bestLen;

        for (int k = 0; k < bestLen; k++)
        {
            int i = bestStart + k;
            float u = i / (float)segments;
            float aDeg = baseAngleDeg + Mathf.Lerp(startDeg, endDeg, u);
            Vector2 worldP = _origin + DirFromDeg(aDeg) * radius;
            _lr.SetPosition(k, worldP);
        }

        float innerR = Mathf.Max(0.01f, radius - hitboxThickness);

        int m = bestLen;
        Vector2[] poly = new Vector2[m * 2];

        for (int k = 0; k < m; k++)
        {
            Vector3 worldP = _lr.GetPosition(k);
            poly[k] = (Vector2)transform.InverseTransformPoint(worldP);
        }

        for (int k = 0; k < m; k++)
        {
            int i = bestEnd - k;
            float u = i / (float)segments;
            float aDeg = baseAngleDeg + Mathf.Lerp(startDeg, endDeg, u);
            Vector2 worldInner = _origin + DirFromDeg(aDeg) * innerR;
            poly[m + k] = (Vector2)transform.InverseTransformPoint(worldInner);
        }

        _poly.pathCount = 1;
        _poly.SetPath(0, poly);

        return true;
    }

    private static Vector2 DirFromDeg(float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    private void Despawn()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsServerStarted)
            return;

        // Deflectors
        if (((1 << other.gameObject.layer) & deflectorMask) != 0)
        {
            ArcDeflector deflector = other.GetComponentInParent<ArcDeflector>();
            if (deflector != null)
            {
                int arcId = GetInstanceID();

                if (deflector.TryDeflect(arcId, _dir, out Vector2 reflected))
                {
                    Vector2 hitPoint = deflector.GetDeflectPoint(other, transform.position);
                    Vector2 newOrigin = hitPoint + reflected * deflectOffset;

                    SoundWaveManager.Instance.ServerSpawnArcFrom(
                        Owner,
                        newOrigin,
                        reflected
                    );

                    Despawn();
                    return;
                }
            }
        }

        // Buttons
        if (arcButtonMask.value != 0 && ((1 << other.gameObject.layer) & arcButtonMask) != 0)
        {
            ArcButtonPlate plate = other.GetComponentInParent<ArcButtonPlate>();
            if (plate != null && CoopPuzzleManager.Instance != null)
            {
                CoopPuzzleManager.Instance.ServerPulsePlate(plate.PlateId, plate.PressSeconds);
            }
        }

        if (Time.time < _serverNextDamageTick)
            return;

        _serverNextDamageTick = Time.time + 0.1f;

        if (((1 << other.gameObject.layer) & damageMask) == 0)
            return;
    }
}
