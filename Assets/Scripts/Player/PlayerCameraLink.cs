using System.Collections;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public class PlayerCameraLink : NetworkBehaviour
{
    [SerializeField] private float findTimeoutSeconds = 5f;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
            return;

        StartCoroutine(AssignCinemachineTargetWhenReady());
    }

    private IEnumerator AssignCinemachineTargetWhenReady()
    {
        float startTime = Time.time;
        CinemachineCamera cam = null;

        while (cam == null && (Time.time - startTime) < findTimeoutSeconds)
        {
            cam = FindCinemachineCameraInActiveScene();
            if (cam != null)
                break;

            yield return null;
        }

        if (cam == null)
        {
            Debug.LogError($"No CinemachineCamera found in the active scene within {findTimeoutSeconds:0.0}s.");
            yield break;
        }

        cam.Target = new CameraTarget
        {
            TrackingTarget = transform
        };

        cam.Priority = 100;

        Debug.Log($"PlayerCameraLink: Assigned CinemachineCamera '{cam.name}' target to owner player '{name}'.");
    }

    private static CinemachineCamera FindCinemachineCameraInActiveScene()
    {
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var cams = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);

        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].gameObject.scene == active)
                return cams[i];
        }

        return null;
    }
}
