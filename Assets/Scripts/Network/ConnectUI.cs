using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace NetTank
{
    /// <summary>
    /// Simple connection panel for Host, Client, Server and Stop actions.
    /// </summary>
    public sealed class ConnectUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button serverButton;
        [SerializeField] private Button stopButton;

        [Header("Fields")]
        [SerializeField] private InputField ipInputField;
        [SerializeField] private Text statusText;
        [SerializeField] private bool useOnGuiFallback = true;

        private string fallbackAddress = "localhost";

        private NetworkManager Manager => NetworkManager.singleton;

        private void Awake()
        {
            if (hostButton != null)
            {
                hostButton.onClick.AddListener(StartHost);
            }

            if (clientButton != null)
            {
                clientButton.onClick.AddListener(StartClient);
            }

            if (serverButton != null)
            {
                serverButton.onClick.AddListener(StartServer);
            }

            if (stopButton != null)
            {
                stopButton.onClick.AddListener(StopNetworking);
            }

            if (ipInputField != null && string.IsNullOrWhiteSpace(ipInputField.text))
            {
                ipInputField.text = fallbackAddress;
            }
        }

        private void Update()
        {
            if (statusText != null)
            {
                statusText.text = $"Status: {GetStatusText()}";
            }
        }

        public void StartHost()
        {
            if (!CanStart())
            {
                return;
            }

            Manager.StartHost();
        }

        public void StartClient()
        {
            if (!CanStart())
            {
                return;
            }

            Manager.networkAddress = GetAddress();
            Manager.StartClient();
        }

        public void StartServer()
        {
            if (!CanStart())
            {
                return;
            }

            Manager.StartServer();
        }

        public void StopNetworking()
        {
            if (Manager == null)
            {
                return;
            }

            if (NetworkServer.active && NetworkClient.isConnected)
            {
                Manager.StopHost();
            }
            else if (NetworkClient.isConnected)
            {
                Manager.StopClient();
            }
            else if (NetworkServer.active)
            {
                Manager.StopServer();
            }
        }

        private bool CanStart()
        {
            return Manager != null && !NetworkServer.active && !NetworkClient.isConnected;
        }

        private string GetAddress()
        {
            string address = ipInputField != null ? ipInputField.text : fallbackAddress;

            if (string.IsNullOrWhiteSpace(address))
            {
                address = "localhost";
            }

            fallbackAddress = address;
            return address;
        }

        private string GetStatusText()
        {
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                return "Host";
            }

            if (NetworkServer.active)
            {
                return "Server";
            }

            if (NetworkClient.isConnected)
            {
                return "Client";
            }

            return "Disconnected";
        }

        private bool HasAssignedUi()
        {
            return hostButton != null || clientButton != null || serverButton != null || stopButton != null || statusText != null;
        }

        private void OnGUI()
        {
            if (!useOnGuiFallback || HasAssignedUi())
            {
                return;
            }

            GUILayout.BeginArea(new Rect(12f, 12f, 260f, 170f), "NetTank Arena", GUI.skin.window);
            GUILayout.Label($"Status: {GetStatusText()}");
            GUILayout.Label("Host IP");
            fallbackAddress = GUILayout.TextField(fallbackAddress);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Host"))
            {
                StartHost();
            }

            if (GUILayout.Button("Client"))
            {
                StartClient();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Server"))
            {
                StartServer();
            }

            if (GUILayout.Button("Stop"))
            {
                StopNetworking();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
