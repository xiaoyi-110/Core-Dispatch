using Gameplay.GameplayObjects.Items;
using LitJson;
using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
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
            NetworkManager.Singleton.StartServer();
            Item[] allItems = FindObjectsByType<Item>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (allItems != null)
            {
                for (int i = 0; i < allItems.Length; i++)
                {
                    allItems[i].ServerInitialize();
                }
            }
        }

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

                Dictionary<string,(string, int)> items = new Dictionary<string,(string, int)> { {"0",( "AK47", 1) },{ "1",("7.62x39mm", 300) } };
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

        [System.Serializable]
        public struct TradeItemData
        {
            public Item.Data item;
            public bool merge;
            public string mergeID;
        }
        public void TradeItemsBetweenCharacters(Character character1, Character character2,Dictionary<Item,int> character1To2Items,Dictionary<Item,int> character2To1Items)
        {
            if(character1==null || character2==null||character1==character2)
            {
                return;
            }
            Dictionary<string, int> serializable1To2 = new Dictionary<string, int>();
            Dictionary<string,int> serializable2To1=new Dictionary<string,int>();
            if (character1To2Items != null)
            {
                foreach(var item in character1To2Items)
                {
                    if(item.Value<=0&&item.Key is Ammo ammo)
                    {
                        continue;
                    }
                    if (item.Key != null && character1.Inventory.Contains(item.Key))
                    {
                        serializable1To2.Add(item.Key.NetworkId, item.Value);
                    }
                }
            }
            if (character2To1Items != null)
            {
                foreach (var item in character2To1Items)
                {
                    if (item.Value <= 0 && item.Key is Ammo ammo)
                    {
                        continue;
                    }
                    if (item.Key != null && character2.Inventory.Contains(item.Key))
                    {
                        serializable2To1.Add(item.Key.NetworkId, item.Value);
                    }
                }
            }
            if (serializable1To2.Count > 0 || serializable2To1.Count > 0)
            {
                string json1 = JsonMapper.ToJson(serializable1To2);
                string json2= JsonMapper.ToJson(serializable2To1);
                TradeItemsBetweenCharactersServerRpc(character1.ClientID, character2.ClientID, json1, json2);
            }
        }
        [ServerRpc(RequireOwnership =false)]
        private void TradeItemsBetweenCharactersServerRpc(ulong character1Id, ulong character2Id, string character1To2Json, string character2To1Json)
        {
            Character character1 = null;
            Character character2 = null;
            if (_characters.ContainsKey(character1Id))
            {
                character1 = _characters[character1Id];
            }
            if (_characters.ContainsKey(character2Id))
            {
                character2 = _characters[character2Id];
            }
            if (character1 == null || character2 == null || character1 == character2)
            {
                return;
            }

            Dictionary<string, int> serializable1To2 = JsonMapper.ToObject<Dictionary<string, int>>(character1To2Json);
            Dictionary<string, int> serializable2To1 = JsonMapper.ToObject<Dictionary<string, int>>(character2To1Json);

            List<TradeItemData> items1To2 = new List<TradeItemData>();
            List<Item.Data> splitItems1 = new List<Item.Data>();
            List<TradeItemData> items2To1 = new List<TradeItemData>();
            List<Item.Data> splitItems2 = new List<Item.Data>();

            foreach (var item in serializable1To2)
            {
                for (int i = 0; i < character1.Inventory.Count; i++)
                {
                    if (item.Key == character1.Inventory[i].NetworkId)
                    {
                        int count = item.Value;
                        int remained = 0;
                        int c = 0;
                        if (character1.Inventory[i].GetType() == typeof(Weapon))
                        {
                            count = ((Weapon)character1.Inventory[i]).AmmoCount;
                        }
                        else
                        {
                            c = character1.Inventory[i].GetCount();
                            if (count <= 0)
                            {
                                break;
                            }
                            else if (c < count)
                            {
                                count = c;
                            }
                            else if (c > count)
                            {
                                remained = c - count;
                                c = count;
                                character1.Inventory[i].SetCount(c);
                            }
                        }
                        if (remained > 0)
                        {
                            Item prefab = PrefabManager.Instance.GetItemPrefab(character1.Inventory[i].Id);
                            if (prefab != null)
                            {
                                Item splitItem = Instantiate(prefab, transform);
                                splitItem.NetworkId = System.Guid.NewGuid().ToString();
                                splitItem.SetCount(remained);
                                character1.AddItemToInventoryLocally(splitItem);
                                splitItems1.Add(splitItem.GetData());
                            }
                            else
                            {
                                break;
                            }
                        }

                        Item merge = null;
                        for (int j = 0; j < character2.Inventory.Count; j++)
                        {
                            if (character2.Inventory[j].Id == character1.Inventory[i].Id)
                            {
                                merge = character2.Inventory[j];
                                break;
                            }
                        }

                        character2.AddItemToInventoryLocally(character1.Inventory[i], merge);

                        TradeItemData data = new TradeItemData();
                        data.item = character1.Inventory[i].GetData();
                        data.item.Value = count;
                        if (merge == null)
                        {
                            data.merge = false;
                        }
                        else
                        {
                            data.merge = true;
                            data.mergeID = merge.NetworkId;
                        }
                        items1To2.Add(data);

                        character1.RemoveItemFromInventoryLocally(character1.Inventory[i]);
                        break;
                    }
                }
            }

            foreach (var item in serializable2To1)
            {
                for (int i = 0; i < character2.Inventory.Count; i++)
                {
                    if (item.Key == character2.Inventory[i].NetworkId)
                    {
                        int count = item.Value;
                        int remained = 0;
                        int c = 0;
                        if (character2.Inventory[i].GetType() == typeof(Weapon))
                        {
                            count = ((Weapon)character2.Inventory[i]).AmmoCount;
                        }
                        else
                        {
                            c = character2.Inventory[i].GetCount();
                            if (count <= 0)
                            {
                                break;
                            }
                            else if (c < count)
                            {
                                count = c;
                            }
                            else if (c > count)
                            {
                                remained = c - count;
                                c = count;
                                character2.Inventory[i].SetCount(c);
                            }
                        }
                        if (remained > 0)
                        {
                            Item prefab = PrefabManager.Instance.GetItemPrefab(character2.Inventory[i].Id);
                            if (prefab != null)
                            {
                                Item splitItem = Instantiate(prefab, transform);
                                splitItem.NetworkId = System.Guid.NewGuid().ToString();
                                splitItem.SetCount(remained);
                                character2.AddItemToInventoryLocally(splitItem);
                                splitItems2.Add(splitItem.GetData());
                            }
                            else
                            {
                                break;
                            }
                        }

                        Item merge = null;
                        for (int j = 0; j < character1.Inventory.Count; j++)
                        {
                            if (character1.Inventory[j].Id == character2.Inventory[i].Id)
                            {
                                merge = character1.Inventory[j];
                                break;
                            }
                        }

                        character1.AddItemToInventoryLocally(character2.Inventory[i], merge);

                        TradeItemData data = new TradeItemData();
                        data.item = character2.Inventory[i].GetData();
                        data.item.Value = count;
                        if (merge == null)
                        {
                            data.merge = false;
                        }
                        else
                        {
                            data.merge = true;
                            data.mergeID = merge.NetworkId;
                        }
                        items2To1.Add(data);

                        character2.RemoveItemFromInventoryLocally(character2.Inventory[i]);
                        break;
                    }
                }
            }

            if (items2To1.Count > 0 || items1To2.Count > 0)
            {
                string json1To2 = JsonMapper.ToJson(items1To2);
                string json1Split = JsonMapper.ToJson(splitItems1);
                string json2To1 = JsonMapper.ToJson(items2To1);
                string json2Split = JsonMapper.ToJson(splitItems2);

                TradeItemsBetweenCharactersClientRpc(
                    character1Id,
                    character2Id,
                    json1To2,
                    json1Split,
                    json2To1,
                    json2Split
                );
            }
        }
        [ClientRpc]
        private void TradeItemsBetweenCharactersClientRpc(ulong character1Id, ulong character2Id, string json1To2, string json1Split, string json2To1, string json2Split)
        {
            Character character1 = null;
            Character character2 = null;

            Character[] allCharacters = FindObjectsByType<Character>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (allCharacters != null)
            {
                for (int i = 0; i < allCharacters.Length; i++)
                {
                    if (allCharacters[i].ClientID == character1Id)
                    {
                        character1 = allCharacters[i];
                    }
                    else if (allCharacters[i].ClientID == character2Id)
                    {
                        character2 = allCharacters[i];
                    }
                    if (character1 != null && character2 != null)
                    {
                        break;
                    }
                }
            }

            if (character1 == null || character2 == null || character1 == character2)
            {
                return;
            }

            List<TradeItemData> items1To2 = JsonMapper.ToObject<List<TradeItemData>>(json1To2);
            List<Item.Data> splitItems1 = JsonMapper.ToObject<List<Item.Data>>(json1Split);
            List<TradeItemData> items2To1 = JsonMapper.ToObject<List<TradeItemData>>(json2To1);
            List<Item.Data> splitItems2 = JsonMapper.ToObject<List<Item.Data>>(json2Split);

            foreach (var item in items1To2)
            {
                bool found = false;
                for (int i = 0; i < character1.Inventory.Count; i++)
                {
                    if (character1.Inventory[i].NetworkId == item.item.NetworkId)
                    {
                        character1.Inventory[i].SetCount(item.item.Value);

                        Item merge = null;
                        if (item.merge && string.IsNullOrEmpty(item.mergeID) == false)
                        {
                            for (int j = 0; j < character2.Inventory.Count; j++)
                            {
                                if (character2.Inventory[j].NetworkId == item.mergeID)
                                {
                                    merge = character2.Inventory[j];
                                    break;
                                }
                            }
                            if (merge == null)
                            {
                                // Problem
                            }
                        }

                        character2.AddItemToInventoryLocally(character1.Inventory[i], merge);
                        character1.RemoveItemFromInventoryLocally(character1.Inventory[i]);
                        found = true;
                        break;
                    }
                }
                if (found == false)
                {
                    // Problem
                }
            }

            foreach (var item in splitItems1)
            {
                Item prefab = PrefabManager.Instance.GetItemPrefab(item.Id);
                if (prefab != null)
                {
                    Item splitItem = Instantiate(prefab, transform);
                    splitItem.NetworkId = item.NetworkId;
                    splitItem.SetCount(item.Value);
                    character1.AddItemToInventoryLocally(splitItem);
                }
            }

            foreach (var item in items2To1)
            {
                bool found = false;
                for (int i = 0; i < character2.Inventory.Count; i++)
                {
                    if (character2.Inventory[i].NetworkId == item.item.NetworkId)
                    {
                        character2.Inventory[i].SetCount(item.item.Value);

                        Item merge = null;
                        if (item.merge && string.IsNullOrEmpty(item.mergeID) == false)
                        {
                            for (int j = 0; j < character1.Inventory.Count; j++)
                            {
                                if (character1.Inventory[j].NetworkId == item.mergeID)
                                {
                                    merge = character1.Inventory[j];
                                    break;
                                }
                            }
                            if (merge == null)
                            {
                                // Problem
                            }
                        }

                        character1.AddItemToInventoryLocally(character2.Inventory[i], merge);
                        character2.RemoveItemFromInventoryLocally(character2.Inventory[i]);
                        found = true;
                        break;
                    }
                }
                if (found == false)
                {
                    // Problem
                }
            }

            foreach (var item in splitItems2)
            {
                Item prefab = PrefabManager.Instance.GetItemPrefab(item.Id);
                if (prefab != null)
                {
                    Item splitItem = Instantiate(prefab, transform);
                    splitItem.NetworkId = item.NetworkId;
                    splitItem.SetCount(item.Value);
                    character2.AddItemToInventoryLocally(splitItem);
                }
            }
        }
        public void UpdateItemPosition(Item item)
        {
            if (item != null)
            {
                Item.Data data = item.GetData();
                string json = JsonMapper.ToJson(data);
                UpdateItemPositionClientRpc(json);
            }
        }
        [ClientRpc]
        private void UpdateItemPositionClientRpc(string itemJson)
        {
            Item.Data data = JsonMapper.ToObject<Item.Data>(itemJson);
            Item[] allItems = FindObjectsByType<Item>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (allItems != null)
            {
                for (int i = 0; i < allItems.Length; i++)
                {
                    if (allItems[i].NetworkId== data.NetworkId)
                    {
                        allItems[i].transform.position = new Vector3(data.Position[0], data.Position[1], data.Position[2]);
                        allItems[i].transform.eulerAngles = new Vector3(data.Rotation[0], data.Rotation[1], data.Rotation[2]);
                        break;
                    }
                }
            }
        }
    }
}

