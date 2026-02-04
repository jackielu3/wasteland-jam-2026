using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class PlayerShooter : NetworkBehaviour
{
    [Header("Networking")]
    [SerializeField] private float aimSendInterval = 0.05f;

    private bool _holding;
    private float _nextAimSend;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
            enabled = false;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            _holding = true;
            _nextAimSend = Time.time;

            Vector2 aimDir = GetAimDirection();
            StartSoundServerRpc(aimDir);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _holding = false;
            StopSoundServerRpc();
        }

        if (_holding && Time.time >= _nextAimSend)
        {
            _nextAimSend = Time.time + aimSendInterval;

            Vector2 aimDir = GetAimDirection();
            UpdateAimServerRpc(aimDir);
        }
    }

    private Vector2 GetAimDirection()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return Vector2.right;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = (Vector2)(mouseWorld - transform.position);

        if (dir.sqrMagnitude < 0.0001f)
            return Vector2.right;

        return dir.normalized;
    }

    [ServerRpc]
    private void StartSoundServerRpc(Vector2 aimDir, NetworkConnection conn = null)
    {
        if (SoundWaveManager.Instance != null)
            SoundWaveManager.Instance.ServerStartForPlayer(conn, transform, aimDir);
    }

    [ServerRpc]
    private void StopSoundServerRpc(NetworkConnection conn = null)
    {
        if (SoundWaveManager.Instance != null)
            SoundWaveManager.Instance.ServerStopForPlayer(conn);
    }

    [ServerRpc]
    private void UpdateAimServerRpc(Vector2 aimDir, NetworkConnection conn = null)
    {
        if (SoundWaveManager.Instance != null)
            SoundWaveManager.Instance.ServerUpdateAimForPlayer(conn, aimDir);
    }
}
