using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NetTank
{
    /// <summary>
    /// Periodically scans spawned tanks and renders a simple synchronized scoreboard.
    /// </summary>
    public sealed class ScoreboardUI : MonoBehaviour
    {
        [SerializeField] private Text scoreboardText;
        [SerializeField] private float refreshInterval = 0.4f;
        [SerializeField] private bool useOnGuiFallback = true;

        private readonly StringBuilder builder = new StringBuilder();
        private float nextRefreshTime;
        private string cachedScoreboard = "Scoreboard\nWaiting for players...";

        private void Update()
        {
            if (Time.time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.time + refreshInterval;
            cachedScoreboard = BuildScoreboardText();

            if (scoreboardText != null)
            {
                scoreboardText.text = cachedScoreboard;
            }
        }

        private string BuildScoreboardText()
        {
            TankHealth[] tanks = FindObjectsOfType<TankHealth>();
            builder.Length = 0;
            builder.AppendLine("Scoreboard");

            if (tanks.Length == 0)
            {
                builder.AppendLine("Waiting for players...");
                return builder.ToString();
            }

            foreach (TankHealth tank in tanks.OrderByDescending(tank => tank.Score))
            {
                TankIdentity identity = tank.GetComponent<TankIdentity>();
                string playerName = identity != null ? identity.DisplayName : $"Player {tank.netId}";
                builder.AppendLine($"{playerName}  Kills: {tank.Score}  HP: {tank.Health}");
            }

            return builder.ToString();
        }

        private void OnGUI()
        {
            if (!useOnGuiFallback || scoreboardText != null)
            {
                return;
            }

            GUI.Box(new Rect(Screen.width - 290f, 12f, 278f, 150f), cachedScoreboard);
        }
    }
}
