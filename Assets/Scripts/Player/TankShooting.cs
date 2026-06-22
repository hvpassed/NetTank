using Mirror;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NetTank
{
    /// <summary>
    /// Handles local fire input and asks the server to spawn authoritative bullets.
    /// </summary>
    [RequireComponent(typeof(TankHealth))]
    public sealed class TankShooting : NetworkBehaviour
    {
        [Header("Fire")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float fireCooldown = 0.45f;
        [SerializeField] private float bulletSpeed = 18f;
        [SerializeField] private float bulletLifetime = 3f;
        [SerializeField] private int bulletDamage = 25;
        [SerializeField] private GameObject muzzleFlashPrefab;

        private TankHealth tankHealth;
        private float nextServerFireTime;

        private void Awake()
        {
            tankHealth = GetComponent<TankHealth>();
        }

        private void Update()
        {
            if (!isLocalPlayer || tankHealth == null || tankHealth.IsDead)
            {
                return;
            }

            if (WasFirePressed())
            {
                CmdFire();
            }
        }

        [Command]
        private void CmdFire()
        {
            // 开火请求从客户端来，但冷却、生成和 Spawn 必须全部在服务端完成。
            if (bulletPrefab == null || tankHealth == null || tankHealth.IsDead)
            {
                return;
            }

            if (Time.time < nextServerFireTime)
            {
                return;
            }

            nextServerFireTime = Time.time + fireCooldown;

            Transform origin = firePoint != null ? firePoint : transform;
            Vector3 direction = origin.forward.normalized;
            Vector3 spawnPosition = origin.position + direction * 0.7f;
            Quaternion spawnRotation = Quaternion.LookRotation(direction, Vector3.up);

            GameObject bulletObject = Instantiate(bulletPrefab, spawnPosition, spawnRotation);
            Bullet bullet = bulletObject.GetComponent<Bullet>();
            if (bullet != null)
            {
                bullet.ServerInitialize(tankHealth, direction, bulletDamage, bulletSpeed, bulletLifetime);
            }

            NetworkServer.Spawn(bulletObject);
            RpcPlayMuzzleFlash(spawnPosition, spawnRotation);
        }

        [ClientRpc]
        private void RpcPlayMuzzleFlash(Vector3 position, Quaternion rotation)
        {
            if (muzzleFlashPrefab == null)
            {
                return;
            }

            GameObject flash = Instantiate(muzzleFlashPrefab, position, rotation);
            Destroy(flash, 1.5f);
        }

        private bool WasFirePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return keyboard.spaceKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }
    }
}
