using System.Collections.Generic;
using UnityEngine;

public class ArcDeflector : MonoBehaviour
{
    public enum Facing
    {
        Up,
        Down,
        Left,
        Right,
        UpRight,
        UpLeft,
        DownRight,
        DownLeft
    }

    [SerializeField] private Facing facing = Facing.Right;
    [SerializeField] private bool frontFaceOnly = true;
    [SerializeField] private float perArcCooldownSeconds = 0.08f;

    private readonly Dictionary<int, float> _lastDeflectTime = new();

    public Vector2 Normal
    {
        get
        {
            Vector2 n = facing switch
            {
                Facing.Up => Vector2.up,
                Facing.Down => Vector2.down,
                Facing.Left => Vector2.left,
                Facing.Right => Vector2.right,
                Facing.UpRight => new Vector2(1, 1),
                Facing.UpLeft => new Vector2(-1, 1),
                Facing.DownRight => new Vector2(1, -1),
                Facing.DownLeft => new Vector2(-1, -1),
                _ => Vector2.right
            };
            return n.normalized;
        }
    }

    public bool TryDeflect(int arcId, Vector2 incomingDir, out Vector2 reflected)
    {
        reflected = incomingDir;

        float now = Time.time;
        if (_lastDeflectTime.TryGetValue(arcId, out float last) &&
            now - last < perArcCooldownSeconds)
            return false;

        Vector2 dir = incomingDir.normalized;
        Vector2 n = Normal;

        // Only reflect when moving INTO the face
        if (frontFaceOnly && Vector2.Dot(dir, n) >= 0f)
            return false;

        reflected = Vector2.Reflect(dir, n).normalized;
        _lastDeflectTime[arcId] = now;
        return true;
    }

    public Vector2 GetDeflectPoint(Collider2D col, Vector2 arcPos)
    {
        return col != null ? col.ClosestPoint(arcPos) : (Vector2)transform.position;
    }
}
