using FishNet.Object;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerCameraLink : NetworkBehaviour
{
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
            return;

        var cam = Object.FindFirstObjectByType<CinemachineCamera>();
        if (cam == null)
        {
            Debug.LogError("No CinemachineCamera found in scene.");
            return;
        }

        cam.Target = new CameraTarget
        {
            TrackingTarget = transform
        };
    }
}
