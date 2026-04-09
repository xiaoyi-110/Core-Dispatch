using Unity.Netcode;
using UnityEngine;

namespace NetcodeDiagnostics
{
    public class NetworkManagerSafety : MonoBehaviour
    {
        private void Awake()
        {
            NetworkManager[] managers = FindObjectsOfType<NetworkManager>();
            if (managers.Length > 1)
            {
                Debug.LogWarning($"[NetSafety] Multiple NetworkManager instances found: {managers.Length}. Disabling extras.");
                bool keepOne = false;
                foreach (NetworkManager manager in managers)
                {
                    if (!keepOne)
                    {
                        keepOne = true;
                        continue;
                    }
                    manager.gameObject.SetActive(false);
                }
            }
        }

        private void OnDestroy()
        {
            ShutdownNetwork();
        }

        private void OnApplicationQuit()
        {
            ShutdownNetwork();
        }

        private static void ShutdownNetwork()
        {
            if (NetworkManager.Singleton == null)
            {
                return;
            }
            if (NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
    }
}
