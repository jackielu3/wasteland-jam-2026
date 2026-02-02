using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class PlayerShooter : NetworkBehaviour
{
    [SerializeField] private NetworkBehaviour soundWaveConePrefab;

    [SerializeField] private float aimSendInterval = 0.05f;

    private bool _holding;
    private float _nextAimSend;

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner) enabled = false;
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetMouseButtonDown(0))
        {
            _holding = true;
            Vector2 aimDir = GetAimDirection();
            StartSoundServerRpc(aimDir);
            _nextAimSend = Time.time;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _holding = false;
            StopSoundServerRpc();
        }

        if (_holding && Time.time > aimSendInterval)
        {
            _nextAimSend = Time.time + aimSendInterval;
            UpdateAimServerRpc(GetAimDirection());
        }
    }

    private Vector2 GetAimDirection()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = (mouseWorld - transform.position);
        if (dir.sqrMagnitude < 0.0001f) return Vector2.right;
        return dir.normalized;
    }

    [ServerRpc]
    private void StartSoundServerRpc(Vector2 aimDir)
    {
        SoundWaveConeManager.Instance.ServerStartForPlayer(Owner, soundWaveConePrefab, transform, aimDir);
    }

    [ServerRpc]
    private void StopSoundServerRpc()
    {
        SoundWaveConeManager.Instance.ServerStopForPlayer(Owner);
    }

    [ServerRpc]
    private void UpdateAimServerRpc(Vector2 aimDir)
    {
        SoundWaveConeManager.Instance.ServerUpdateAimForPlayer(Owner, aimDir);
    }
}
