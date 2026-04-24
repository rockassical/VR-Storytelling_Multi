using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class OwnerTransformSync : NetworkBehaviour
{
    [Tooltip("How quickly non-owners interpolate toward the received transform. Higher = snappier, lower = smoother.")]
    [SerializeField] float interpolationSpeed = 20f;

    [Tooltip("Don't send/apply updates smaller than this (meters). Prevents jitter when stationary.")]
    [SerializeField] float positionThreshold = 0.0005f;

    readonly NetworkVariable<Vector3> netPos = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<Quaternion> netRot = new(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    Vector3 _lastSentPos;
    Quaternion _lastSentRot;

    public override void OnNetworkSpawn()
    {
        // Seed the variables from the current transform on the initial owner
        // so non-owners don't snap to (0,0,0) before the first write arrives.
        if (IsOwner)
        {
            netPos.Value = transform.position;
            netRot.Value = transform.rotation;
            _lastSentPos = transform.position;
            _lastSentRot = transform.rotation;
        }
        else
        {
            transform.position = netPos.Value;
            transform.rotation = netRot.Value;
        }
    }

    void Update()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            // Only write if we've actually moved — avoids spamming unchanged state.
            if ((transform.position - _lastSentPos).sqrMagnitude > positionThreshold * positionThreshold ||
                Quaternion.Angle(transform.rotation, _lastSentRot) > 0.1f)
            {
                netPos.Value = transform.position;
                netRot.Value = transform.rotation;
                _lastSentPos = transform.position;
                _lastSentRot = transform.rotation;
            }
        }
        else
        {
            // Smooth toward the networked value rather than snapping.
            float t = 1f - Mathf.Exp(-interpolationSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, netPos.Value, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, netRot.Value, t);
        }
    }
}
