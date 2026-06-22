using Mirror;
using UnityEngine;

namespace NetTank
{
    /// <summary>
    /// Networked display data for a tank player.
    /// </summary>
    public sealed class TankIdentity : NetworkBehaviour
    {
        [SyncVar] [SerializeField] private int playerNumber;
        [SyncVar] [SerializeField] private int connectionId;
        [SyncVar(hook = nameof(OnDisplayNameChanged))] [SerializeField] private string displayName = "Player";

        public int PlayerNumber => playerNumber;
        public int ConnectionId => connectionId;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }

                return netId == 0 ? "Player" : $"Player {netId}";
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyDisplayName(DisplayName);
        }

        [Server]
        public void ServerInitialize(int assignedPlayerNumber, int assignedConnectionId)
        {
            playerNumber = assignedPlayerNumber;
            connectionId = assignedConnectionId;
            displayName = $"Player {assignedPlayerNumber}";
            ApplyDisplayName(displayName);
        }

        private void OnDisplayNameChanged(string oldValue, string newValue)
        {
            ApplyDisplayName(newValue);
        }

        private void ApplyDisplayName(string newName)
        {
            gameObject.name = string.IsNullOrWhiteSpace(newName) ? "PlayerTank" : newName;
        }
    }
}
