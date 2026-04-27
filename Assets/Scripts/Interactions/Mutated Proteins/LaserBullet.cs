using Unity.Netcode;
using UnityEngine;

public class LaserBullet : NetworkBehaviour
{
    public float lifeTime;
    public float speed;

    void Update()
    {
        // Only the server drives movement + lifetime. NetworkTransform replicates
        // position to clients, and Despawn cleans up everywhere.
        if (!IsSpawned || !IsServer) return;

        if (lifeTime > 0f)
        {
            transform.position += transform.forward * speed * Time.deltaTime;
            lifeTime -= Time.deltaTime;
        }
        else
        {
            NetworkObject.Despawn();
        }
    }
}
