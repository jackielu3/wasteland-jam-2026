using FishNet;
using FishNet.Connection;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SoundWaveManager : MonoBehaviour
{
    public static SoundWaveManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private SoundWaveArcNet arcPrefab;

    [Header("Firing")]
    [SerializeField] private float batteryCostPerArc = 2f;

    [Header("Arc")]
    [SerializeField] private float arcFireInterval = 0.08f;

    private class ShooterState
    {
        public Transform Source;
        public Vector2 AimDir;
        public Coroutine Loop;

        public PlayerBattery Battery;
    }

    private readonly Dictionary<NetworkConnection, ShooterState> _shooters = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ServerStartForPlayer(NetworkConnection owner, Transform source, Vector2 aimDir)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        if (owner == null || source == null)
            return;

        if (!_shooters.TryGetValue(owner, out ShooterState state) || state == null)
        {
            state = new ShooterState();
            _shooters[owner] = state;
        }

        state.Source = source;
        state.AimDir = aimDir;

        state.Battery = source.GetComponent<PlayerBattery>();
        if (state.Battery == null)
            state.Battery = source.GetComponentInParent<PlayerBattery>();

        if (state.Loop != null)
            StopCoroutine(state.Loop);

        state.Loop = StartCoroutine(ServerFireLoop(owner));
    }

    public void ServerStopForPlayer(NetworkConnection owner)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        if (owner == null)
            return;

        if (_shooters.TryGetValue(owner, out ShooterState state) && state != null)
        {
            if (state.Loop != null)
                StopCoroutine(state.Loop);

            _shooters.Remove(owner);
        }
    }

    public void ServerUpdateAimForPlayer(NetworkConnection owner, Vector2 aimDir)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        if (owner == null)
            return;

        if (_shooters.TryGetValue(owner, out ShooterState state) && state != null)
        {
            state.AimDir = aimDir;
        }
    }

    public void ServerSpawnArcFrom(FishNet.Connection.NetworkConnection owner, Vector2 origin, Vector2 dir)
    {
        if (!FishNet.InstanceFinder.IsServerStarted) return;
        if (arcPrefab == null) return;

        SoundWaveArcNet arc = Instantiate(arcPrefab, Vector3.zero, Quaternion.identity);
        FishNet.InstanceFinder.ServerManager.Spawn(arc.gameObject, owner);

        uint serverTick = FishNet.InstanceFinder.TimeManager.Tick;
        arc.Init(origin, dir, serverTick);
    }

    private IEnumerator ServerFireLoop(NetworkConnection owner)
    {
        WaitForSeconds wait = new WaitForSeconds(arcFireInterval);

        while (true)
        {
            if (owner == null)
                yield break;

            if (!_shooters.TryGetValue(owner, out ShooterState state) || state == null)
                yield break;

            if (state.Source == null)
            {
                _shooters.Remove(owner);
                yield break;
            }

            if (arcPrefab != null)
            {
                Vector2 origin = state.Source.position;
                Vector2 dir = state.AimDir.sqrMagnitude > 0.0001f
                    ? state.AimDir.normalized
                    : Vector2.right;

                if (state.Battery == null)
                {
                    state.Battery = state.Source.GetComponent<PlayerBattery>();
                    if (state.Battery == null)
                        state.Battery = state.Source.GetComponentInParent<PlayerBattery>();
                }

                if (state.Battery != null)
                {
                    if (!state.Battery.TryConsume(batteryCostPerArc))
                    {
                        ServerStopForPlayer(owner);
                        yield break;
                    }
                }
                else
                {
                    Debug.LogWarning($"[SoundWaveManager] No PlayerBattery found for owner {owner.ClientId}. Stopping firing.");
                    ServerStopForPlayer(owner);
                    yield break;
                }

                SoundWaveArcNet arc = Instantiate(arcPrefab, Vector3.zero, Quaternion.identity);
                InstanceFinder.ServerManager.Spawn(arc.gameObject, owner);

                uint serverTick = InstanceFinder.TimeManager.Tick;
                arc.Init(origin, dir, serverTick);
            }

            yield return wait;
        }
    }
}
