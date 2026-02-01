using FishNet.Object;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    [SerializeField] private float lifeSeconds = 2.5f;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Invoke(nameof(DespawnSelf), lifeSeconds);
    }

    [Server]
    private void DespawnSelf()
    {
        if (IsSpawned)
        {
            Despawn();
        }
    }
}
