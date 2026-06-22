using System;
using System.Collections;
using Mirror;
using UnityEngine;

namespace NetTank
{
    /// <summary>
    /// Server-authoritative health, death, respawn and score state.
    /// </summary>
    [RequireComponent(typeof(TankIdentity))]
    public sealed class TankHealth : NetworkBehaviour
    {
        public static event Action<TankHealth> ClientStateChanged;

        [Header("Health")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float respawnDelay = 3f;

        [Header("Effects")]
        [SerializeField] private GameObject explosionPrefab;

        [SyncVar(hook = nameof(OnHealthChanged))] [SerializeField] private int health = 100;
        [SyncVar(hook = nameof(OnScoreChanged))] [SerializeField] private int score;
        [SyncVar(hook = nameof(OnDeadChanged))] [SerializeField] private bool isDead;

        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;
        private TankController tankController;
        private Coroutine respawnRoutine;

        public int MaxHealth => maxHealth;
        public int Health => health;
        public int Score => score;
        public bool IsDead => isDead;

        private void Awake()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedColliders = GetComponentsInChildren<Collider>(true);
            tankController = GetComponent<TankController>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            health = maxHealth;
            isDead = false;
            ApplyAliveState(true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyAliveState(!isDead);
            ClientStateChanged?.Invoke(this);
        }

        [Server]
        public void ServerResetForNewConnection()
        {
            health = maxHealth;
            score = 0;
            isDead = false;
            ApplyAliveState(true);
        }

        [Server]
        public void TakeDamage(int amount, TankHealth attacker)
        {
            // 扣血只允许服务端执行，客户端不能直接改 health。
            if (isDead || amount <= 0)
            {
                return;
            }

            health = Mathf.Max(health - amount, 0);

            if (health <= 0)
            {
                ServerDie(attacker);
            }
        }

        [Server]
        private void ServerDie(TankHealth attacker)
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            health = 0;
            ApplyAliveState(false);

            if (attacker != null && attacker != this)
            {
                attacker.ServerAddScore(1);
            }

            // 爆炸是瞬时事件，用 RPC 广播；血量/死亡是持续状态，用 SyncVar 同步。
            RpcPlayExplosion(transform.position);

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
            }

            respawnRoutine = StartCoroutine(ServerRespawnAfterDelay());
        }

        [Server]
        private void ServerAddScore(int amount)
        {
            score += Mathf.Max(0, amount);
        }

        [Server]
        private IEnumerator ServerRespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);

            Transform spawnPoint = null;
            if (NetTankNetworkManager.Instance != null)
            {
                spawnPoint = NetTankNetworkManager.Instance.GetNextSpawnPoint();
            }
            else if (SpawnPointGroup.Instance != null)
            {
                int index = 0;
                spawnPoint = SpawnPointGroup.Instance.GetNextSpawnPoint(ref index);
            }

            Vector3 respawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.up;
            Quaternion respawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            if (tankController != null)
            {
                tankController.ServerTeleport(respawnPosition, respawnRotation);
            }
            else
            {
                transform.SetPositionAndRotation(respawnPosition, respawnRotation);
            }

            health = maxHealth;
            isDead = false;
            ApplyAliveState(true);
            respawnRoutine = null;
        }

        [ClientRpc]
        private void RpcPlayExplosion(Vector3 position)
        {
            if (explosionPrefab == null)
            {
                return;
            }

            GameObject explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
            Destroy(explosion, 3f);
        }

        private void OnHealthChanged(int oldValue, int newValue)
        {
            ClientStateChanged?.Invoke(this);
        }

        private void OnScoreChanged(int oldValue, int newValue)
        {
            ClientStateChanged?.Invoke(this);
        }

        private void OnDeadChanged(bool oldValue, bool newValue)
        {
            ApplyAliveState(!newValue);
            ClientStateChanged?.Invoke(this);
        }

        private void ApplyAliveState(bool alive)
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                cachedRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (cachedColliders == null || cachedColliders.Length == 0)
            {
                cachedColliders = GetComponentsInChildren<Collider>(true);
            }

            foreach (Renderer tankRenderer in cachedRenderers)
            {
                if (tankRenderer != null)
                {
                    tankRenderer.enabled = alive;
                }
            }

            foreach (Collider tankCollider in cachedColliders)
            {
                if (tankCollider != null)
                {
                    tankCollider.enabled = alive;
                }
            }
        }
    }
}
