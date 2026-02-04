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

    private PlayerBattery _battery;

    private class ShooterState
    {
        public Transform Source;
        public Vector2 AimDir;
        public Coroutine Loop;
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

    /// <summary>
    /// Server: start firing arcs for this player connection, originating from 'source'.
    /// </summary>
    public void ServerStartForPlayer(NetworkConnection owner, Transform source, Vector2 aimDir)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        if (owner == null)
            return;

        if (!_shooters.TryGetValue(owner, out ShooterState state) || state == null)
        {
            state = new ShooterState();
            _shooters[owner] = state;
        }

        state.Source = source;
        state.AimDir = aimDir;

        // Restart loop if already running
        if (state.Loop != null)
            StopCoroutine(state.Loop);

        state.Loop = StartCoroutine(ServerFireLoop(owner));

        _battery = state.Source.GetComponent<PlayerBattery>();
    }

    /// <summary>
    /// Server: stop firing arcs for this player.
    /// </summary>
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

    /// <summary>
    /// Server: update the current aim direction while firing.
    /// </summary>
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

    private IEnumerator ServerFireLoop(NetworkConnection owner)
    {
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

                if (_battery != null)
                {
                    if (!_battery.TryConsume(batteryCostPerArc))
                    {
                        ServerStopForPlayer(owner);
                        yield break;
                    }
                }   

                SoundWaveArcNet arc = Instantiate(arcPrefab, Vector3.zero, Quaternion.identity);
                InstanceFinder.ServerManager.Spawn(arc.gameObject, owner);

                uint serverTick = InstanceFinder.TimeManager.Tick;
                arc.Init(origin, dir, serverTick);
            }

            yield return new WaitForSeconds(arcFireInterval);
        }
    }
}
