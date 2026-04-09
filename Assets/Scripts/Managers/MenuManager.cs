using DevelopersHub.RealtimeNetworking.Client;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Managers
{
    public class MenuManager : MonoBehaviour
    {

        [SerializeField] private float reconnectPeriod = 3f;
        [SerializeField] private Text connectionStatus = null;
        [SerializeField] private Text username = null;
        [SerializeField] private Text matchmakingText = null;
        [SerializeField] private Button matchmakingStart = null;
        [SerializeField] private Button matchmakingStop = null;
        private static MenuManager _singleton = null;
        public static MenuManager Instance
        {
            get
            {
                if (_singleton == null)
                {
                    _singleton = FindFirstObjectByType<MenuManager>();
                }
                return _singleton;
            }
        }

        private void Awake()
        {
            //base.Awake();
            matchmakingStart.gameObject.SetActive(false);
            matchmakingStop.gameObject.SetActive(false);
            if (TryGetCommandLineOverride(out SessionManager.Role role, out string ip, out ushort port))
            {
                SessionManager._Role = role;
                SessionManager.Port = port;
                SessionManager.OverrideAddress = ip;
                DirectAwake();
                return;
            }
            if (Application.platform == RuntimePlatform.WindowsServer || Application.platform == RuntimePlatform.LinuxServer || Application.platform == RuntimePlatform.OSXServer)
            {
                SessionManager._Role = SessionManager.Role.Server;
                ServerAwake();
            }
            else
            {
                SessionManager._Role = SessionManager.Role.Client;
                ClientAwake();
            }
        }

        private void DirectAwake()
        {
            matchmakingText.text = "";
            username.text = "";
            matchmakingStart.gameObject.SetActive(false);
            matchmakingStop.gameObject.SetActive(false);
            SceneManager.LoadScene(1);
        }

        private bool TryGetCommandLineOverride(out SessionManager.Role role, out string ip, out ushort port)
        {
            role = SessionManager.Role.Client;
            ip = "";
            port = 7777;

            string[] args = Environment.GetCommandLineArgs();
            bool hasRole = false;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "-role", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    string value = args[i + 1].ToLowerInvariant();
                    if (value == "server")
                    {
                        role = SessionManager.Role.Server;
                        hasRole = true;
                    }
                    else if (value == "client")
                    {
                        role = SessionManager.Role.Client;
                        hasRole = true;
                    }
                }
                else if (string.Equals(arg, "-ip", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    ip = args[i + 1];
                }
                else if (string.Equals(arg, "-port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    ushort parsed;
                    if (ushort.TryParse(args[i + 1], out parsed))
                    {
                        port = parsed;
                    }
                }
            }

            if (!hasRole)
            {
                return false;
            }

            if (role == SessionManager.Role.Client && string.IsNullOrEmpty(ip))
            {
                ip = "127.0.0.1";
            }

            return true;
        }


        #region Client
        private bool _startingMatch = false;
        private void ClientAwake()
        {
            matchmakingText.text = "";
            username.text = "";
            matchmakingStart.onClick.AddListener(StartMatchmaking);
            matchmakingStop.onClick.AddListener(StopMatchmaking);
            RealtimeNetworking.OnConnectingToServerResult += OnConnectingToServerResult;
            RealtimeNetworking.OnDisconnectedFromServer += OnDisconnected;
            RealtimeNetworking.OnAuthentication += OnAuthentication;
            RealtimeNetworking.OnStartMatchmaking += OnStartMatchmaking;
            RealtimeNetworking.OnStopMatchmaking += OnStopMatchmaking;
            RealtimeNetworking.OnNetcodeServerReady += OnNetcodeServerReady;
            Connect();
        }
        private void OnNetcodeServerReady(int port, Data.RuntimeGame gameData)
        {
            SessionManager._Role = SessionManager.Role.Client;
            _startingMatch = true;
            RealtimeNetworking.Disconnect();
            SessionManager.Port = (ushort)port;
            if (gameData.mapID == 0)
            {
                SceneManager.LoadScene(1);
            }
        }

        private void OnStopMatchmaking(RealtimeNetworking.StopMatchmakingResponse response)
        {
            if (response == RealtimeNetworking.StopMatchmakingResponse.SUCCESSFULL)
            {
                matchmakingText.text = "";
                matchmakingStart.gameObject.SetActive(true);
                matchmakingStop.gameObject.SetActive(false);
            }
            matchmakingStop.interactable = true;
        }

        private void OnStartMatchmaking(RealtimeNetworking.StartMatchmakingResponse response)
        {
            if (response == RealtimeNetworking.StartMatchmakingResponse.SUCCESSFULL)
            {
                matchmakingText.text = "Searching ...";
                matchmakingStart.gameObject.SetActive(false);
                matchmakingStop.gameObject.SetActive(true);
            }
            matchmakingStart.interactable = true;
        }
        private void OnAuthentication(RealtimeNetworking.AuthenticationResponse response, Data.PlayerProfile accountData = null)
        {
            if (response == RealtimeNetworking.AuthenticationResponse.SUCCESSFULL)
            {
                matchmakingStart.gameObject.SetActive(true);
                username.text = accountData.username;
            }
            else
            {
                Debug.LogError("Failed to authenticate the player. Code: " + response);
            }
        }

        private void OnDisconnected()
        {
            matchmakingText.text = "";
            matchmakingStart.gameObject.SetActive(false);
            matchmakingStop.gameObject.SetActive(false);
            SetConnectionStatus("Disconnected", Color.red);
            if (_startingMatch == false)
            {
                StartCoroutine(Reconnect());
            }
        }

        private void OnConnectingToServerResult(bool successful)
        {
            if (successful)
            {
                SetConnectionStatus("Connected", Color.green);
                RealtimeNetworking.Authenticate();
            }
            else
            {
                if (_startingMatch == false)
                {
                    StartCoroutine(Reconnect());
                }
            }
        }

        private void Connect()
        {
            SetConnectionStatus("Disconnected", Color.red);
            RealtimeNetworking.Connect();
        }

        private IEnumerator Reconnect()
        {
            yield return new WaitForSeconds(reconnectPeriod);
            Connect();
        }

        private void SetConnectionStatus(string text, Color color)
        {
            connectionStatus.text = text;
            connectionStatus.color = color;
        }

        private void StartMatchmaking()
        {
            matchmakingStart.interactable = false;
            RealtimeNetworking.StartMatchmaking(0, 0, Data.Extension.NETCODE_SERVER);
        }

        private void StopMatchmaking()
        {
            matchmakingStop.interactable = false;
            RealtimeNetworking.StopMatchmaking();
        }


        private void OnDestroy()
        {
            //base.OnDestroy();
            if (SessionManager._Role == SessionManager.Role.Client)
            {
                RealtimeNetworking.OnConnectingToServerResult -= OnConnectingToServerResult;
                RealtimeNetworking.OnDisconnectedFromServer -= OnDisconnected;
                RealtimeNetworking.OnAuthentication -= OnAuthentication;
                RealtimeNetworking.OnStartMatchmaking -= OnStartMatchmaking;
                RealtimeNetworking.OnStopMatchmaking -= OnStopMatchmaking;
                RealtimeNetworking.OnNetcodeServerReady -= OnNetcodeServerReady;
            }
        }
        #endregion

        #region Server
        private void ServerAwake()
        {
            Data.RuntimeGame game = RealtimeNetworking.NetcodeGetGameData();
            if (game != null)
            {
                if (game.mapID == 0)
                {
                    SceneManager.LoadScene(1);
                }
            }
            else
            {
                // Problem
                Application.Quit();
            }
        }
        #endregion
    }
}

