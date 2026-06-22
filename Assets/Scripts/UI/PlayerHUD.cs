using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace NetTank
{
    /// <summary>
    /// Shows the local player's health, score and respawn state.
    /// </summary>
    public sealed class PlayerHUD : MonoBehaviour
    {
        [SerializeField] private Text healthText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text respawnText;
        [SerializeField] private bool useOnGuiFallback = true;

        private TankHealth localHealth;

        private void OnEnable()
        {
            TankHealth.ClientStateChanged += HandleTankStateChanged;
        }

        private void OnDisable()
        {
            TankHealth.ClientStateChanged -= HandleTankStateChanged;
        }

        private void Update()
        {
            if (localHealth == null || !localHealth.isLocalPlayer)
            {
                localHealth = FindLocalPlayerHealth();
            }

            Refresh();
        }

        private void HandleTankStateChanged(TankHealth changedHealth)
        {
            if (changedHealth != null && changedHealth.isLocalPlayer)
            {
                localHealth = changedHealth;
                Refresh();
            }
        }

        private void Refresh()
        {
            if (healthText != null)
            {
                healthText.text = localHealth == null ? "HP: --" : $"HP: {localHealth.Health}/{localHealth.MaxHealth}";
            }

            if (scoreText != null)
            {
                scoreText.text = localHealth == null ? "Score: --" : $"Score: {localHealth.Score}";
            }

            if (respawnText != null)
            {
                bool isRespawning = localHealth != null && localHealth.IsDead;
                respawnText.enabled = isRespawning;
                respawnText.text = isRespawning ? "Respawning..." : string.Empty;
            }
        }

        private TankHealth FindLocalPlayerHealth()
        {
            if (!NetworkClient.isConnected)
            {
                return null;
            }

            TankHealth[] tanks = FindObjectsOfType<TankHealth>();
            foreach (TankHealth tank in tanks)
            {
                if (tank != null && tank.isLocalPlayer)
                {
                    return tank;
                }
            }

            return null;
        }

        private bool HasAssignedUi()
        {
            return healthText != null || scoreText != null || respawnText != null;
        }

        private void OnGUI()
        {
            if (!useOnGuiFallback || HasAssignedUi())
            {
                return;
            }

            GUILayout.BeginArea(new Rect(12f, 190f, 220f, 100f), "HUD", GUI.skin.window);
            if (localHealth == null)
            {
                GUILayout.Label("HP: --");
                GUILayout.Label("Score: --");
            }
            else
            {
                GUILayout.Label($"HP: {localHealth.Health}/{localHealth.MaxHealth}");
                GUILayout.Label($"Score: {localHealth.Score}");
                if (localHealth.IsDead)
                {
                    GUILayout.Label("Respawning...");
                }
            }
            GUILayout.EndArea();
        }
    }
}
