using Unity.Netcode; // If using Netcode
using Unity.Cinemachine; // Unity 6 uses the new namespace
using UnityEngine;

public class PlayerCameraSetup : NetworkBehaviour
{
    [SerializeField] private CinemachineCamera vCam; // Formerly CinemachineVirtualCamera

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            vCam.Priority = 10;
        }
        else
        {
            vCam.Priority = 0;
        }
    }
}