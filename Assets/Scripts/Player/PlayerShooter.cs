using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class PlayerShooter : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private NetworkObject bulletPrefab;

    [Header("Tuning")]
    [SerializeField] private float bulletSpeed = 10f;
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private float shootCooldown = 0.12f;

    private float _nextShootTime;

    private void Update()
    {
        if (!IsOwner) return;

        if (Time.time < _nextShootTime) return;

        if (Input.GetMouseButtonDown(0))
        {
            _nextShootTime = Time.time + shootCooldown;

            Vector3 shooterPos = transform.position;

            Vector3 mouse = Input.mousePosition;
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouse);
            mouseWorld.z = shooterPos.z;

            Vector2 dir = (mouseWorld - shooterPos);
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            FireServerRpc(shooterPos, dir);
        }
    }

    [ServerRpc(RequireOwnership = true)]
    private void FireServerRpc(Vector3 spawnPos, Vector2 direction, Channel channel = Channel.Unreliable)
    {
        NetworkObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        if (bulletRb != null)
            bulletRb.linearVelocity = direction * bulletSpeed;

        // Direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        bullet.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        ServerManager.Spawn(bullet);

        PlayShootSfxRpc(spawnPos);
    }

    [ObserversRpc(BufferLast = false)]
    private void PlayShootSfxRpc(Vector3 pos)
    {
        if (shootClip == null) return;

        AudioSource.PlayClipAtPoint(shootClip, pos, 1f);
    }
}
