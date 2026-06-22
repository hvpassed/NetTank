using Mirror;
using UnityEngine;

namespace NetTank
{
    /// <summary>
    /// Authoritative network bullet. The server owns hit checks and destroys the bullet.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Bullet : NetworkBehaviour
    {
        [SyncVar] [SerializeField] private Vector3 travelDirection = Vector3.forward;
        [SyncVar] [SerializeField] private float speed = 18f;
        [SyncVar] [SerializeField] private uint ownerNetId;

        private TankHealth owner;
        private int damage = 25;
        private float lifetime = 3f;
        private float spawnTime;

        public override void OnStartServer()
        {
            base.OnStartServer();
            spawnTime = Time.time;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            FaceTravelDirection();
        }

        private void Update()
        {
            if (travelDirection.sqrMagnitude > 0.001f)
            {
                transform.position += travelDirection.normalized * speed * Time.deltaTime;
            }

            if (isServer && Time.time - spawnTime >= lifetime)
            {
                NetworkServer.Destroy(gameObject);
            }
        }

        [Server]
        public void ServerInitialize(TankHealth bulletOwner, Vector3 direction, int bulletDamage, float bulletSpeed, float bulletLifetime)
        {
            owner = bulletOwner;
            ownerNetId = bulletOwner != null ? bulletOwner.netId : 0;
            travelDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            damage = Mathf.Max(1, bulletDamage);
            speed = Mathf.Max(1f, bulletSpeed);
            lifetime = Mathf.Max(0.25f, bulletLifetime);
            spawnTime = Time.time;
            FaceTravelDirection();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isServer || other == null)
            {
                return;
            }

            TankHealth target = other.GetComponentInParent<TankHealth>();
            if (target != null)
            {
                if (target == owner)
                {
                    return;
                }

                target.TakeDamage(damage, owner);
                NetworkServer.Destroy(gameObject);
                return;
            }

            if (!other.isTrigger)
            {
                NetworkServer.Destroy(gameObject);
            }
        }

        private void FaceTravelDirection()
        {
            if (travelDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(travelDirection.normalized, Vector3.up);
            }
        }
    }
}
