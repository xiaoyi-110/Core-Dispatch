using DevelopersHub.RealtimeNetworking.Client;
using Gameplay.GameplayObjects.Items;
using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.VisualScripting;
using UnityEngine;
using Utility;
using NetcodeDiagnostics;

namespace Managers
{
    public class SessionManager : NetworkBehaviour
    {
        [System.Serializable]
        public struct TransferRequest : INetworkSerializable
        {
            public string NetworkId;
            public int Count;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref NetworkId);
                serializer.SerializeValue(ref Count);
            }
        }

        [System.Serializable]
        public struct TradeItemNetData : INetworkSerializable
        {
            public Item.Data item;
            public bool merge;
            public string mergeID;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref item.Id);
                serializer.SerializeValue(ref item.NetworkId);
                serializer.SerializeValue(ref item.Value);
                serializer.SerializeValue(ref merge);
                serializer.SerializeValue(ref mergeID);
            }
        }

        [System.Serializable]
        public struct SplitItemNetData : INetworkSerializable
        {
            public string Id;
            public string NetworkId;
            public int Count;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Id);
                serializer.SerializeValue(ref NetworkId);
                serializer.SerializeValue(ref Count);
            }
        }

        [System.Serializable]
        public struct ItemStateNetData : INetworkSerializable
        {
            public string Id;
            public string NetworkId;
            public int Count;
            public Vector3 Position;
            public Vector3 Rotation;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Id);
                serializer.SerializeValue(ref NetworkId);
                serializer.SerializeValue(ref Count);
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref Rotation);
            }
        }

        [System.Serializable]
        public struct ItemEntryNetData : INetworkSerializable
        {
            public string ItemId;
            public string NetworkId;
            public int Count;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref ItemId);
                serializer.SerializeValue(ref NetworkId);
                serializer.SerializeValue(ref Count);
            }
        }

        [System.Serializable]
        public struct CharacterInitNetData : INetworkSerializable
        {
            public float Health;
            public ItemEntryNetData[] Items;
            public string[] EquippedIds;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Health);
                SerializeItemEntries(ref Items, serializer);
                SerializeStringArray(ref EquippedIds, serializer);
            }

            private static void SerializeItemEntries<T>(ref ItemEntryNetData[] entries, BufferSerializer<T> serializer) where T : IReaderWriter
            {
                int count = entries != null ? entries.Length : 0;
                serializer.SerializeValue(ref count);

                if (serializer.IsReader)
                {
                    entries = new ItemEntryNetData[count];
                }

                for (int i = 0; i < count; i++)
                {
                    ItemEntryNetData entry = entries[i];
                    serializer.SerializeValue(ref entry);
                    if (serializer.IsReader)
                    {
                        entries[i] = entry;
                    }
                }
            }

            private static void SerializeStringArray<T>(ref string[] values, BufferSerializer<T> serializer) where T : IReaderWriter
            {
                int count = values != null ? values.Length : 0;
                serializer.SerializeValue(ref count);

                if (serializer.IsReader)
                {
                    values = new string[count];
                }

                for (int i = 0; i < count; i++)
                {
                    string value = values[i];
                    serializer.SerializeValue(ref value);
                    if (serializer.IsReader)
                    {
                        values[i] = value;
                    }
                }
            }
        }

        private float _destroyServerAfterSecondsIfNoClientConnected = 300;
        private float _destroyServerAfterSecondsWithoutAnyClient = 120;
        private float _timer = 0;
        private int _connectedClients = 0;
        private bool _atLeastOneClientConnected = false;
        private bool _closingServer = false;
        private static SessionManager _instance;
        private static Role _role = Role.Client; public static Role _Role { get { return _role; } set { _role = value; } }
        private static ushort _port = 0; public static ushort Port { get { return _port; } set { _port = value; } }
        private static string _overrideAddress = ""; public static string OverrideAddress { get { return _overrideAddress; } set { _overrideAddress = value; } }
        [Header("Network Port")]
        [SerializeField] private bool useFixedPort = true;
        [SerializeField] private ushort fixedPort = 7777;
        public enum Role
        {
            Server = 1, Client = 2
        }
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

        [Header("Item Sync")]
        [SerializeField] private float itemSyncInterval = 0.1f;
        [SerializeField] private float minItemSyncInterval = 0.05f;
        [SerializeField] private float maxItemSyncInterval = 0.25f;
        [SerializeField] private int highLoadItemCount = 12;
        private float _nextItemSyncTime = 0f;
        private readonly List<ItemStateNetData> _pendingItemStates = new List<ItemStateNetData>();
        private readonly Dictionary<string, int> _pendingItemIndexByNetId = new Dictionary<string, int>();

        [Header("Trade Validation")]
        [SerializeField] private float maxTradeDistance = 3f;
        [SerializeField] private float minTradeInterval = 0.25f;
        private readonly Dictionary<ulong, float> _lastTradeTime = new Dictionary<ulong, float>();
        private void Start()
        {
            
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (_role == Role.Server)
            {
                transport.ConnectionData.Address = "0.0.0.0";
                _port = useFixedPort ? fixedPort : (ushort)DevelopersHub.RealtimeNetworking.Client.Tools.FindFreeTcpPort();
                transport.ConnectionData.Port = _port;
                StartServer();
            }
            else
            {
                if (!string.IsNullOrEmpty(_overrideAddress))
                {
                    transport.ConnectionData.Address = _overrideAddress;
                }
                else
                {
                    transport.ConnectionData.Address = Client.instance.settings.ip;
                }
                transport.ConnectionData.Port = _port;
                StartClient();
            }
        }

        private void Update()
        {
            if (_role == Role.Server)
            {
                if (_closingServer)
                {
                    return;
                }
                if (_atLeastOneClientConnected)
                {
                    if (_connectedClients > 0)
                    {
                        _timer = 0;
                    }
                    else
                    {
                        _timer += Time.deltaTime;
                        if (_timer >= _destroyServerAfterSecondsWithoutAnyClient)
                        {
                            CloseServer();
                        }
                    }
                }
                else
                {
                    _destroyServerAfterSecondsIfNoClientConnected -= Time.deltaTime;
                    if (_destroyServerAfterSecondsIfNoClientConnected <= 0)
                    {
                        CloseServer();
                    }
                }
            }

            if (NetworkManager.Singleton.IsServer)
            {
                FlushPendingItemStates();
            }
        }
        public void StartServer()
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            NetworkManager.Singleton.StartServer();
            EnsureDiagnosticsHud();
            foreach (var item in WorldRegistry.Items)
            {
                if (item != null)
                {
                    item.ServerInitialize();
                }
            }
            StartCoroutine(InformClients());
        }
        private void CloseServer()
        {
            if (_role == Role.Server)
            {
                if (_closingServer)
                {
                    return;
                }
                _closingServer = true;
                RealtimeNetworking.NetcodeCloseServer();
            }
        }
        private void OnClientDisconnect(ulong obj)
        {
            _connectedClients = Mathf.Max(0, _connectedClients - 1);
            if (_characters.TryGetValue(obj, out Character character))
            {
                if (character != null)
                {
                    Unity.Netcode.NetworkObject netObj = character.GetComponent<Unity.Netcode.NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn(true);
                    }
                    else
                    {
                        Destroy(character.gameObject);
                    }
                }
                _characters.Remove(obj);
            }
        }
        private IEnumerator InformClients()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(2f);
            RealtimeNetworking.NetcodeServerIsReady(_port);
        }
        private void OnClientConnected(ulong clientId)
        {
            _connectedClients++;
            _atLeastOneClientConnected = true;
            if (_role == Role.Server)
            {
                Debug.Log($"[Session] Client connected id={clientId}");
                SpawnCharacterForClient(clientId);
            }
            ulong[] target = new ulong[1];
            target[0] = clientId;
            ClientRpcParams clientRpcParams = default;
            clientRpcParams.Send.TargetClientIds = target;
            OnClientConnectedClientRpc(clientRpcParams);
        }
        [ClientRpc]
        public void OnClientConnectedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            // Client hook if needed. Spawn is handled on server.
        }

        [ServerRpc(RequireOwnership = false)]
        public void SpawnCharacterServerRpc(long accountID,ServerRpcParams serverRpcParams = default)
        {
            if (!InventoryOps.ValidateServerOnlySender(serverRpcParams.Receive.SenderClientId, "SpawnCharacter")) return;
            SpawnCharacterForClient(serverRpcParams.Receive.SenderClientId);

        }

        private void SpawnCharacterForClient(ulong clientId)
        {
            Character prefab = PrefabManager.Instance.GetCharacterPrefab("Bot");
            if (prefab == null)
            {
                Debug.LogWarning("[Session] Character prefab not found.");
                return;
            }

            Vector3 position = new Vector3(Random.Range(-5, 5), 0, Random.Range(-5, 5));
            Character character = Instantiate(prefab, position, Quaternion.identity);
            Unity.Netcode.NetworkObject netObj = character.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null)
            {
                Debug.LogWarning("[Session] Character NetworkObject missing.");
                return;
            }
            netObj.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[Session] Spawned player character netId={netObj.NetworkObjectId} owner={netObj.OwnerClientId} client={clientId}");

            if (_characters.TryGetValue(clientId, out Character existing) && existing != null)
            {
                Unity.Netcode.NetworkObject existingNet = existing.GetComponent<Unity.Netcode.NetworkObject>();
                if (existingNet != null && existingNet.IsSpawned)
                {
                    existingNet.Despawn(true);
                }
                else
                {
                    Destroy(existing.gameObject);
                }
            }
            _characters[clientId] = character;

            Dictionary<string, (string, int)> items = new Dictionary<string, (string, int)> { { "0", ("AK47", 1) }, { "1", ("7.62x39mm", 300) } };
            List<string> itemIds = new List<string>();
            List<string> equippedIds = new List<string>();
            List<ItemEntryNetData> entries = new List<ItemEntryNetData>();
            for (int i = 0; i < items.Count; i++)
            {
                itemIds.Add(System.Guid.NewGuid().ToString());
            }

            int index = 0;
            foreach (var entry in items)
            {
                entries.Add(new ItemEntryNetData
                {
                    ItemId = entry.Value.Item1,
                    NetworkId = itemIds[index],
                    Count = entry.Value.Item2
                });
                index++;
            }

            CharacterInitNetData initData = new CharacterInitNetData
            {
                Health = 100f,
                Items = entries.ToArray(),
                EquippedIds = equippedIds.ToArray()
            };

            List<ItemStateNetData> itemsOnGround = new List<ItemStateNetData>();
            foreach (var item in WorldRegistry.Items)
            {
                if (item == null) continue;
                if (string.IsNullOrEmpty(item.NetworkId))
                {
                    item.NetworkId = System.Guid.NewGuid().ToString();
                }
                if (item.transform.parent == null)
                {
                    itemsOnGround.Add(new ItemStateNetData
                    {
                        Id = item.Id,
                        NetworkId = item.NetworkId,
                        Count = item.GetCount(),
                        Position = item.transform.position,
                        Rotation = item.transform.eulerAngles
                    });
                }
            }

            character.InitializeServer(items, itemIds, equippedIds, clientId);
            character.InitializeClientRpc(initData, itemsOnGround.ToArray(), clientId);

            foreach (var client in _characters)
            {
                if (client.Value != null && client.Value != character)
                {
                    CharacterInitNetData remoteData = BuildCharacterInitData(client.Value);

                    ulong[] target = new ulong[1];
                    target[0] = clientId;
                    ClientRpcParams clientRpcParams = default;
                    clientRpcParams.Send.TargetClientIds = target;

                    client.Value.InitializeClientRpc(remoteData, client.Key, clientRpcParams);
                }
            }
        }

        private void EnsureDiagnosticsHud()
        {
            GameObject existing = GameObject.Find("__NetDiagnostics");
            if (existing != null)
            {
                return;
            }
            GameObject go = new GameObject("__NetDiagnostics");
            DontDestroyOnLoad(go);
            go.AddComponent<NetcodeDiagnostics.NetworkDiagnosticsHUD>();
        }

        private void NotifyTradeResult(ulong character1Id, ulong character2Id, List<TradeItemNetData> items1To2, List<SplitItemNetData> splitItems1, List<TradeItemNetData> items2To1, List<SplitItemNetData> splitItems2)
        {
            if (items1To2 == null) items1To2 = new List<TradeItemNetData>();
            if (items2To1 == null) items2To1 = new List<TradeItemNetData>();
            if (splitItems1 == null) splitItems1 = new List<SplitItemNetData>();
            if (splitItems2 == null) splitItems2 = new List<SplitItemNetData>();

            if (items1To2.Count == 0 && items2To1.Count == 0)
            {
                InventoryOps.LogInfo($"Trade no valid transfers. c1={character1Id} c2={character2Id}");
                return;
            }

            TradeResultNetData result = new TradeResultNetData
            {
                Character1Id = character1Id,
                Character2Id = character2Id,
                Items1To2 = items1To2.ToArray(),
                SplitItems1 = splitItems1.ToArray(),
                Items2To1 = items2To1.ToArray(),
                SplitItems2 = splitItems2.ToArray()
            };

            TradeItemsBetweenCharactersClientRpc(result);
            InventoryOps.LogInfo($"Trade success. c1={character1Id} c2={character2Id} c1To2={items1To2.Count} c2To1={items2To1.Count}");
        }
        public void StartClient()
        {
            NetworkManager.Singleton.StartClient();
        }


        private CharacterInitNetData BuildCharacterInitData(Character character)
        {
            if (character == null)
            {
                return default;
            }

            List<ItemEntryNetData> entries = new List<ItemEntryNetData>();
            List<string> equippedIds = new List<string>();

            for (int i = 0; i < character.Inventory.Count; i++)
            {
                Item item = character.Inventory[i];
                if (item == null)
                {
                    continue;
                }
                entries.Add(new ItemEntryNetData
                {
                    ItemId = item.Id,
                    NetworkId = item.NetworkId,
                    Count = item.GetCount()
                });
            }

            Weapon equippedWeapon = character.CurrentWeapon;
            if (equippedWeapon != null)
            {
                equippedIds.Add(equippedWeapon.NetworkId);
            }
            Ammo equippedAmmo = character.CurrentAmmo;
            if (equippedAmmo != null)
            {
                equippedIds.Add(equippedAmmo.NetworkId);
            }

            return new CharacterInitNetData
            {
                Health = character.Health,
                Items = entries.ToArray(),
                EquippedIds = equippedIds.ToArray()
            };
        }

        public void TradeItemsBetweenCharacters(Character character1, Character character2,Dictionary<Item,int> character1To2Items,Dictionary<Item,int> character2To1Items)
        {
            if(character1==null || character2==null||character1==character2)
            {
                InventoryOps.LogWarning("Trade invalid characters.");
                return;
            }
            List<TransferRequest> request1To2 = InventoryOps.BuildTransferRequests(character1To2Items, character1.Inventory);
            List<TransferRequest> request2To1 = InventoryOps.BuildTransferRequests(character2To1Items, character2.Inventory);
            if (request1To2.Count > 0 || request2To1.Count > 0)
            {
                TradeItemsBetweenCharactersServerRpc(character1.ClientID, character2.ClientID, request1To2.ToArray(), request2To1.ToArray());
            }
            else
            {
                InventoryOps.LogInfo("Trade empty request list.");
            }
        }
        [ServerRpc(RequireOwnership =false)]
        private void TradeItemsBetweenCharactersServerRpc(ulong character1Id, ulong character2Id, TransferRequest[] character1To2, TransferRequest[] character2To1, ServerRpcParams serverRpcParams = default)
        {
            if (character1To2 == null) character1To2 = new TransferRequest[0];
            if (character2To1 == null) character2To1 = new TransferRequest[0];

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
            if (!ValidateTradeRequest(serverRpcParams.Receive.SenderClientId, character1, character2))
            {
                return;
            }

            List<TradeItemNetData> items1To2 = new List<TradeItemNetData>();
            List<SplitItemNetData> splitItems1 = new List<SplitItemNetData>();
            List<TradeItemNetData> items2To1 = new List<TradeItemNetData>();
            List<SplitItemNetData> splitItems2 = new List<SplitItemNetData>();

            ulong sender = serverRpcParams.Receive.SenderClientId;
            foreach (var item in character1To2)
            {
                if (sender != character1Id)
                {
                    InventoryOps.LogWarning($"Trade sender mismatch for character1 transfers. sender={sender} c1={character1Id}");
                    break;
                }
                if (!InventoryOps.TryGetValidItemForTransfer(character1.Inventory, item.NetworkId, item.Count, out Item source))
                {
                    InventoryOps.LogWarning($"Trade invalid request from character1. netId={item.NetworkId} count={item.Count} c1={character1Id}");
                    continue;
                }
                if (!InventoryOps.IsOwnedByCharacter(source, character1))
                {
                    InventoryOps.LogWarning($"Trade ownership mismatch for character1. netId={item.NetworkId} c1={character1Id}");
                    continue;
                }
                if (!InventoryOps.TryTransferItemWithSource(
                        source,
                        character2.Inventory,
                        item.Count,
                        transform,
                        out Item movedItem,
                        out Item merge,
                        out Item splitItem,
                        out int movedCount))
                {
                    continue;
                }

                if (splitItem != null)
                {
                    character1.AddItemToInventoryLocally(splitItem);
                    splitItems1.Add(new SplitItemNetData
                    {
                        Id = splitItem.Id,
                        NetworkId = splitItem.NetworkId,
                        Count = splitItem.GetCount()
                    });
                }

                character2.AddItemToInventoryLocally(movedItem, merge);

                TradeItemNetData data = new TradeItemNetData();
                data.item = movedItem.GetData();
                data.item.Value = movedCount;
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

                character1.RemoveItemFromInventoryLocally(movedItem);
            }

            foreach (var item in character2To1)
            {
                if (sender != character2Id)
                {
                    InventoryOps.LogWarning($"Trade sender mismatch for character2 transfers. sender={sender} c2={character2Id}");
                    break;
                }
                if (!InventoryOps.TryGetValidItemForTransfer(character2.Inventory, item.NetworkId, item.Count, out Item source))
                {
                    InventoryOps.LogWarning($"Trade invalid request from character2. netId={item.NetworkId} count={item.Count} c2={character2Id}");
                    continue;
                }
                if (!InventoryOps.IsOwnedByCharacter(source, character2))
                {
                    InventoryOps.LogWarning($"Trade ownership mismatch for character2. netId={item.NetworkId} c2={character2Id}");
                    continue;
                }
                if (!InventoryOps.TryTransferItemWithSource(
                        source,
                        character1.Inventory,
                        item.Count,
                        transform,
                        out Item movedItem,
                        out Item merge,
                        out Item splitItem,
                        out int movedCount))
                {
                    continue;
                }

                if (splitItem != null)
                {
                    character2.AddItemToInventoryLocally(splitItem);
                    splitItems2.Add(new SplitItemNetData
                    {
                        Id = splitItem.Id,
                        NetworkId = splitItem.NetworkId,
                        Count = splitItem.GetCount()
                    });
                }

                character1.AddItemToInventoryLocally(movedItem, merge);

                TradeItemNetData data = new TradeItemNetData();
                data.item = movedItem.GetData();
                data.item.Value = movedCount;
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

                character2.RemoveItemFromInventoryLocally(movedItem);
            }

            NotifyTradeResult(character1Id, character2Id, items1To2, splitItems1, items2To1, splitItems2);
        }
        [ClientRpc]
        private void TradeItemsBetweenCharactersClientRpc(TradeResultNetData result)
        {
            Character character1 = null;
            Character character2 = null;
            WorldRegistry.TryGetCharacter(result.Character1Id, out character1);
            WorldRegistry.TryGetCharacter(result.Character2Id, out character2);

            if (character1 == null || character2 == null || character1 == character2)
            {
                return;
            }

            TradeItemNetData[] items1To2 = result.Items1To2 ?? new TradeItemNetData[0];
            SplitItemNetData[] splitItems1 = result.SplitItems1 ?? new SplitItemNetData[0];
            TradeItemNetData[] items2To1 = result.Items2To1 ?? new TradeItemNetData[0];
            SplitItemNetData[] splitItems2 = result.SplitItems2 ?? new SplitItemNetData[0];

            foreach (var item in items1To2)
            {
                Item source = InventoryOps.FindItemByNetworkId(character1.Inventory, item.item.NetworkId);
                if (source == null)
                {
                    InventoryOps.LogWarning($"Trade source item not found in character1 inventory. itemId={item.item.Id} netId={item.item.NetworkId}");
                    continue;
                }
                source.SetCount(item.item.Value);

                Item merge = InventoryOps.ResolveMergeTarget(character2.Inventory, item.merge, item.mergeID, source);
                if (item.merge && merge == null)
                {
                    InventoryOps.LogWarning($"Trade merge target not found for character2. mergeID={item.mergeID}");
                }

                character2.AddItemToInventoryLocally(source, merge);
                character1.RemoveItemFromInventoryLocally(source);
            }

            foreach (var item in splitItems1)
            {
                Item prefab = PrefabManager.Instance.GetItemPrefab(item.Id);
                if (prefab != null)
                {
                    Item splitItem = Instantiate(prefab, transform);
                    splitItem.NetworkId = item.NetworkId;
                    splitItem.SetCount(item.Count);
                    character1.AddItemToInventoryLocally(splitItem);
                }
            }

            foreach (var item in items2To1)
            {
                Item source = InventoryOps.FindItemByNetworkId(character2.Inventory, item.item.NetworkId);
                if (source == null)
                {
                    InventoryOps.LogWarning($"Trade source item not found in character2 inventory. itemId={item.item.Id} netId={item.item.NetworkId}");
                    continue;
                }
                source.SetCount(item.item.Value);

                Item merge = InventoryOps.ResolveMergeTarget(character1.Inventory, item.merge, item.mergeID, source);
                if (item.merge && merge == null)
                {
                    InventoryOps.LogWarning($"Trade merge target not found for character1. mergeID={item.mergeID}");
                }

                character1.AddItemToInventoryLocally(source, merge);
                character2.RemoveItemFromInventoryLocally(source);
            }

            foreach (var item in splitItems2)
            {
                Item prefab = PrefabManager.Instance.GetItemPrefab(item.Id);
                if (prefab != null)
                {
                    Item splitItem = Instantiate(prefab, transform);
                    splitItem.NetworkId = item.NetworkId;
                    splitItem.SetCount(item.Count);
                    character2.AddItemToInventoryLocally(splitItem);
                }
            }
        }
        public void QueueItemState(Item item)
        {
            if (item == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }
            if (string.IsNullOrEmpty(item.NetworkId))
            {
                return;
            }

            ItemStateNetData data = new ItemStateNetData
            {
                Id = item.Id,
                NetworkId = item.NetworkId,
                Count = item.GetCount(),
                Position = item.transform.position,
                Rotation = item.transform.eulerAngles
            };

            if (_pendingItemIndexByNetId.TryGetValue(data.NetworkId, out int index))
            {
                _pendingItemStates[index] = data;
            }
            else
            {
                _pendingItemIndexByNetId[data.NetworkId] = _pendingItemStates.Count;
                _pendingItemStates.Add(data);
            }
        }
        [ClientRpc]
        private void UpdateItemPositionClientRpc(ItemStateNetData[] dataBatch)
        {
            if (dataBatch == null || dataBatch.Length == 0)
            {
                return;
            }
            NetworkDiagnostics.RecordItemSync(dataBatch.Length);

            for (int i = 0; i < dataBatch.Length; i++)
            {
                ItemStateNetData data = dataBatch[i];
                if (WorldRegistry.TryGetItemByNetworkId(data.NetworkId, out Item item))
                {
                    item.transform.position = data.Position;
                    item.transform.eulerAngles = data.Rotation;
                }
            }
        }

        private void FlushPendingItemStates()
        {
            if (_pendingItemStates.Count == 0)
            {
                return;
            }
            if (Time.time < _nextItemSyncTime)
            {
                return;
            }
            if (_pendingItemStates.Count >= highLoadItemCount)
            {
                itemSyncInterval = Mathf.Max(minItemSyncInterval, itemSyncInterval * 0.8f);
            }
            else
            {
                itemSyncInterval = Mathf.Min(maxItemSyncInterval, itemSyncInterval * 1.05f);
            }
            _nextItemSyncTime = Time.time + itemSyncInterval;

            UpdateItemPositionClientRpc(_pendingItemStates.ToArray());
            _pendingItemStates.Clear();
            _pendingItemIndexByNetId.Clear();
        }

        private bool ValidateTradeRequest(ulong sender, Character character1, Character character2)
        {
            if (character1 == null || character2 == null || character1 == character2)
            {
                InventoryOps.LogWarning("Trade invalid server targets.");
                return false;
            }

            if (!InventoryOps.ValidateTradeSender(sender, character1.ClientID, character2.ClientID))
            {
                return false;
            }

            float distance = Vector3.Distance(character1.transform.position, character2.transform.position);
            if (distance > maxTradeDistance)
            {
                InventoryOps.LogWarning($"Trade distance too far. dist={distance:0.00} max={maxTradeDistance:0.00}");
                return false;
            }

            if (!ValidateTradeRateLimit(character1.ClientID) || !ValidateTradeRateLimit(character2.ClientID))
            {
                InventoryOps.LogWarning("Trade rate limited.");
                return false;
            }

            return true;
        }

        private bool ValidateTradeRateLimit(ulong clientId)
        {
            float now = Time.time;
            if (_lastTradeTime.TryGetValue(clientId, out float lastTime))
            {
                if (now - lastTime < minTradeInterval)
                {
                    return false;
                }
            }
            _lastTradeTime[clientId] = now;
            return true;
        }

        [System.Serializable]
        public struct TradeResultNetData : INetworkSerializable
        {
            public ulong Character1Id;
            public ulong Character2Id;
            public TradeItemNetData[] Items1To2;
            public SplitItemNetData[] SplitItems1;
            public TradeItemNetData[] Items2To1;
            public SplitItemNetData[] SplitItems2;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Character1Id);
                serializer.SerializeValue(ref Character2Id);
                serializer.SerializeValue(ref Items1To2);
                serializer.SerializeValue(ref SplitItems1);
                serializer.SerializeValue(ref Items2To1);
                serializer.SerializeValue(ref SplitItems2);
            }
        }
    }
}


