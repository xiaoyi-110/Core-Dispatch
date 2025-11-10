using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using LitJson;
using StarterAssets;
using Gameplay.GameplayObjects.Items;
namespace Managers
{
    public class SessionManager : NetworkBehaviour
    {
        private static SessionManager _instance;

        public static SessionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<SessionManager>();                    
                }
                return _instance;
            }
        }

        private Dictionary<ulong,Character> _characters= new Dictionary<ulong,Character>();
        public void StartServer()
        {
            NetworkManager.Singleton.OnClientConnectedCallback +=OnClientConnected;
            //NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            NetworkManager.Singleton.StartServer();
        }

        //private void OnClientDisconnect(ulong clientId)
        //{
        //    _characters.Remove(clientId);
        //}

        private void OnClientConnected(ulong clientId)
        {
            ulong[] target = new ulong[1];
            target[0] = clientId;
            ClientRpcParams clientRpcParams = default;
            clientRpcParams.Send.TargetClientIds = target;
            OnClientConnectedClientRpc(clientRpcParams);
        }
        [ClientRpc]
        public void OnClientConnectedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            long accountID = 0; 
            SpawnCharacterServerRpc(accountID);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SpawnCharacterServerRpc(long accountID,ServerRpcParams serverRpcParams = default)
        {
            Character prefab=PrefabManager.Instance.GetCharacterPrefab("Bot");
            if (prefab != null)
            {
                Vector3 position=new Vector3(Random.Range(-5,5),0,Random.Range(-5,5));
                Character character=Instantiate(prefab, position, Quaternion.identity);
                character.GetComponent<NetworkObject>().SpawnWithOwnership(serverRpcParams.Receive.SenderClientId);

                _characters.Add(serverRpcParams.Receive.SenderClientId, character);

                Dictionary<string, int> items = new Dictionary<string, int> { { "AK47", 30 },{ "7.62x39mm", 300 } };
                List<string> itemIds = new List<string> ();
                List<string> equippedIds = new List<string> ();
                for(int i=0;i<items.Count;i++)
                {
                    itemIds.Add(System.Guid.NewGuid().ToString());
                }

                string itemsJson = JsonMapper.ToJson(items);
                string itemIdJson = JsonMapper.ToJson(itemIds);
                string equippedJson = JsonMapper.ToJson(equippedIds);

                Item[] Items = FindObjectsByType<Item>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                List<Item.Data> itemsOnGround = new List<Item.Data>();
                if (Items != null)
                {
                    for (int i = 0; i < Items.Length; i++)
                    {
                        if (string.IsNullOrEmpty(Items[i].NetworkId))
                        {
                            Items[i].NetworkId = System.Guid.NewGuid().ToString();
                        }
                        if (Items[i].transform.parent == null)
                        {
                            itemsOnGround.Add(Items[i].GetData());
                        }
                    }
                }
                string itemsOnGroundJson = JsonMapper.ToJson(itemsOnGround);

                character.InitializeServer(items, itemIds, equippedIds,serverRpcParams.Receive.SenderClientId);
                character.InitializeClientRpc(itemsJson, itemIdJson, equippedJson,itemsOnGroundJson,serverRpcParams.Receive.SenderClientId);

                foreach (var client in _characters) {
                    if (client.Value != null && client.Value != character)
                    {
                        Character.Data data=client.Value.GetData();
                        string json=JsonMapper.ToJson(data);

                        ulong[] target = new ulong[1];
                        target[0]=serverRpcParams.Receive.SenderClientId;
                        ClientRpcParams clientRpcParams = default;
                        clientRpcParams.Send.TargetClientIds = target;

                        client.Value.InitializeClientRpc(json, client.Key, clientRpcParams);
                    }
                }
            }
        }
        public void StartClient()
        {
            NetworkManager.Singleton.StartClient();
        }
    }
}

