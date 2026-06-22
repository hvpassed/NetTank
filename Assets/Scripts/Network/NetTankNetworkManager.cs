using Mirror;
using UnityEngine;

namespace NetTank
{
    /// <summary>
    /// Mirror NetworkManager extension that assigns spawn points and player names.
    /// </summary>
    public sealed class NetTankNetworkManager : NetworkManager
    {
        public static NetTankNetworkManager Instance { get; private set; }

        [Header("NetTank Spawn")]
        [SerializeField] private SpawnPointGroup spawnPointGroup;

        private int nextPlayerNumber = 1;
        private int nextSpawnIndex;

        public override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            nextPlayerNumber = 1;
            nextSpawnIndex = 0;
            ResolveSpawnPointGroup();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            ResolveSpawnPointGroup();

            Transform spawnPoint = GetNextSpawnPoint();
            Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.up;
            Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            GameObject player = Instantiate(playerPrefab, spawnPosition, spawnRotation);

            TankIdentity identity = player.GetComponent<TankIdentity>();
            if (identity != null)
            {
                identity.ServerInitialize(nextPlayerNumber, conn.connectionId);
            }

            TankController controller = player.GetComponent<TankController>();
            if (controller != null)
            {
                controller.ServerTeleport(spawnPosition, spawnRotation);
            }

            TankHealth health = player.GetComponent<TankHealth>();
            if (health != null)
            {
                health.ServerResetForNewConnection();
            }

            // 只有服务端真正把 player 对象加入连接，客户端不能自己创建玩家对象。
            NetworkServer.AddPlayerForConnection(conn, player);
            nextPlayerNumber++;
        }

        [Server]
        public Transform GetNextSpawnPoint()
        {
            ResolveSpawnPointGroup();

            if (spawnPointGroup == null)
            {
                return null;
            }

            return spawnPointGroup.GetNextSpawnPoint(ref nextSpawnIndex);
        }

        private void ResolveSpawnPointGroup()
        {
            if (spawnPointGroup == null)
            {
                spawnPointGroup = SpawnPointGroup.Instance;
            }

            if (spawnPointGroup == null)
            {
                spawnPointGroup = FindObjectOfType<SpawnPointGroup>();
            }
        }
    }
}
