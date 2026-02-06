using System.Collections.Generic;
using UnityEngine;

public class WorldId : MonoBehaviour
{
    public int worldId;

    private static readonly Dictionary<int, WorldId> _byId = new();

    private void Awake()
    {
        _byId[worldId] = this;
    }

    private void OnDestroy()
    {
        if (_byId.TryGetValue(worldId, out var existing) && existing == this)
            _byId.Remove(worldId);
    }

    public static bool TryGet(int id, out WorldId found) => _byId.TryGetValue(id, out found);
}
