using UnityEngine;
using Gameplay.GameplayObjects.Character;
using Unity.Netcode;

namespace ConnectionsManagement
{
    public class HelloWorldManager : MonoBehaviour
    {
        private NetworkManager _NetworkManager;
        private bool _useNetwork =false;

        void Awake()
        {
            _NetworkManager = GetComponent<NetworkManager>();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 300));

            if (!_useNetwork || (!_NetworkManager.IsClient && !_NetworkManager.IsServer))
            {
                StartButtons();
            }
            else
            {
                StatusLabels();
                //SubmitNewPosition();
            }

            GUILayout.EndArea();
        }

        void StartButtons()
        {
            if (_useNetwork)
            {
                if (GUILayout.Button("Host")) _NetworkManager.StartHost();
                if (GUILayout.Button("Client")) _NetworkManager.StartClient();
                if (GUILayout.Button("Server")) _NetworkManager.StartServer();
            }
            else
            {
                GUILayout.Label("单机模式");
            }
        }

        void StatusLabels()
        {
            if (!_useNetwork) return;

            var mode = _NetworkManager.IsHost ?
                "Host" : _NetworkManager.IsServer ? "Server" : "Client";

            GUILayout.Label("Transport: " +
                _NetworkManager.NetworkConfig.NetworkTransport.GetType().Name);
            GUILayout.Label("Mode: " + mode);
        }

       
    }
}
