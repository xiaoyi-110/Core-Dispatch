using System;
using System.Collections.Generic;
using Gameplay.GameplayObjects.Items;
using Managers;
using StarterAssets;
using UnityEngine;

namespace Utility
{
    public static class InventoryOps
    {
        public static bool EnableInventoryLogs = true;
        public static bool EnableInventoryWarnings = true;

        public static Item FindMergeForPickup(List<Item> inventory, Item target)
        {
            if (inventory == null || target == null) return null;
            if (target is Ammo == false) return null;

            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i] != null && inventory[i].Id == target.Id)
                {
                    return inventory[i];
                }
            }
            return null;
        }

        public static Item FindMergeByItemId(List<Item> inventory, Item target)
        {
            if (inventory == null || target == null) return null;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i] != null && inventory[i].Id == target.Id)
                {
                    return inventory[i];
                }
            }
            return null;
        }

        public static Item CreateSplitItem(Item source, int remained, Transform parent)
        {
            if (source == null || remained <= 0) return null;
            Item prefab = PrefabManager.Instance.GetItemPrefab(source.Id);
            if (prefab == null) return null;

            Item splitItem = UnityEngine.Object.Instantiate(prefab, parent);
            splitItem.NetworkId = System.Guid.NewGuid().ToString();
            splitItem.SetCount(remained);
            return splitItem;
        }

        public static Item FindMergeByNetworkId(List<Item> inventory, string networkId)
        {
            if (inventory == null || string.IsNullOrEmpty(networkId)) return null;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i] != null && inventory[i].NetworkId == networkId)
                {
                    return inventory[i];
                }
            }
            return null;
        }

        public static Item FindItemByNetworkId(List<Item> inventory, string networkId)
        {
            if (inventory == null || string.IsNullOrEmpty(networkId)) return null;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i] != null && inventory[i].NetworkId == networkId)
                {
                    return inventory[i];
                }
            }
            return null;
        }

        public static bool TryGetValidItemForTransfer(List<Item> inventory, string networkId, int count, out Item item)
        {
            item = null;
            if (inventory == null || string.IsNullOrEmpty(networkId))
            {
                return false;
            }
            item = FindItemByNetworkId(inventory, networkId);
            if (item == null)
            {
                return false;
            }
            if (count <= 0 && item is Ammo)
            {
                item = null;
                return false;
            }
            return true;
        }

        public static bool IsOwnedByCharacter(Item item, StarterAssets.Character owner)
        {
            if (item == null || owner == null)
            {
                return false;
            }
            return owner.Inventory.Contains(item);
        }

        public static bool ValidateOwnerSender(ulong sender, ulong owner, string action, bool asError = false)
        {
            if (sender != owner)
            {
                if (asError)
                {
                    LogError($"{action} sender mismatch. sender={sender} owner={owner}");
                }
                else
                {
                    LogWarning($"{action} sender mismatch. sender={sender} owner={owner}");
                }
                return false;
            }
            return true;
        }

        public static bool ValidateTradeSender(ulong sender, ulong character1Id, ulong character2Id)
        {
            if (sender != character1Id && sender != character2Id)
            {
                LogWarning($"Trade sender mismatch. sender={sender} c1={character1Id} c2={character2Id}");
                return false;
            }
            return true;
        }

        public static bool ValidateServerOnlySender(ulong sender, string action)
        {
            if (sender != 0)
            {
                LogWarning($"{action} sender mismatch. sender={sender}");
                return false;
            }
            return true;
        }

        private static string FormatLog(string message)
        {
            return $"[Inventory] {message}";
        }

        public static void LogInfo(string message)
        {
            if (!EnableInventoryLogs)
            {
                return;
            }
            Debug.Log(FormatLog(message));
            NetLog.Write(FormatLog(message));
        }

        public static void LogError(string message)
        {
            if (!EnableInventoryLogs)
            {
                return;
            }
            Debug.LogError(FormatLog(message));
            NetLog.Write(FormatLog(message));
        }

        public static List<SessionManager.TransferRequest> BuildTransferRequests(Dictionary<Item, int> items, List<Item> inventory)
        {
            List<SessionManager.TransferRequest> requests = new List<SessionManager.TransferRequest>();
            if (items == null || inventory == null)
            {
                return requests;
            }
            foreach (var kvp in items)
            {
                Item item = kvp.Key;
                int count = kvp.Value;
                if (ShouldSkipTransferRequest(item, count))
                {
                    continue;
                }
                if (!IsValidInventoryItem(inventory, item))
                {
                    continue;
                }
                requests.Add(new SessionManager.TransferRequest
                {
                    NetworkId = item.NetworkId,
                    Count = count
                });
            }
            return requests;
        }

        public static bool TryTransferItem(
            List<Item> fromInventory,
            List<Item> toInventory,
            string networkId,
            int requestedCount,
            Transform instantiateParent,
            out Item movedItem,
            out Item mergeTarget,
            out Item splitItem,
            out int movedCount)
        {
            movedItem = null;
            mergeTarget = null;
            splitItem = null;
            movedCount = 0;

            if (fromInventory == null || toInventory == null)
            {
                return false;
            }

            Item source = FindItemByNetworkId(fromInventory, networkId);
            if (source == null)
            {
                return false;
            }

            return TryTransferItemWithSource(
                source,
                toInventory,
                requestedCount,
                instantiateParent,
                out movedItem,
                out mergeTarget,
                out splitItem,
                out movedCount);
        }

        public static bool TryTransferItemWithSource(
            Item source,
            List<Item> toInventory,
            int requestedCount,
            Transform instantiateParent,
            out Item movedItem,
            out Item mergeTarget,
            out Item splitItem,
            out int movedCount)
        {
            movedItem = null;
            mergeTarget = null;
            splitItem = null;
            movedCount = 0;

            if (source == null || toInventory == null)
            {
                return false;
            }

            if (!TryComputeTransfer(source, requestedCount, out int count, out int remained))
            {
                return false;
            }

            if (remained > 0)
            {
                source.SetCount(count);
                splitItem = CreateSplitItem(source, remained, instantiateParent);
                if (splitItem == null)
                {
                    return false;
                }
            }

            movedItem = source;
            movedCount = count;
            mergeTarget = FindMergeByItemId(toInventory, source);
            return true;
        }

        public static Item TryResolvePickupMerge(List<Item> inventory, string mergeNetworkId, Item pickupItem)
        {
            Item merge = FindMergeByNetworkId(inventory, mergeNetworkId);
            if (merge != null)
            {
                return merge;
            }
            return FindMergeForPickup(inventory, pickupItem);
        }

        public static bool ShouldSkipTransferRequest(Item item, int count)
        {
            if (item == null)
            {
                return true;
            }
            if (count <= 0 && item is Ammo)
            {
                return true;
            }
            return false;
        }

        public static bool IsValidInventoryItem(List<Item> inventory, Item item)
        {
            if (item == null || inventory == null)
            {
                return false;
            }
            if (string.IsNullOrEmpty(item.NetworkId))
            {
                return false;
            }
            return inventory.Contains(item);
        }

        public static bool IsWorldPickupCandidate(Item item)
        {
            if (item == null)
            {
                return false;
            }
            return item.transform.parent == null;
        }

        public static Item ResolveMergeTarget(List<Item> inventory, bool expectMerge, string mergeNetworkId, Item fallbackItem)
        {
            if (!expectMerge)
            {
                return null;
            }
            Item merge = FindMergeByNetworkId(inventory, mergeNetworkId);
            if (merge != null)
            {
                return merge;
            }
            if (fallbackItem != null)
            {
                return FindMergeByItemId(inventory, fallbackItem);
            }
            return null;
        }

        public static bool IsValidPickupRequest(Item target, string networkId)
        {
            if (string.IsNullOrEmpty(networkId))
            {
                return false;
            }
            if (target == null)
            {
                return false;
            }
            if (target.NetworkId != networkId)
            {
                return false;
            }
            return IsWorldPickupCandidate(target);
        }

        public static void LogWarning(string message)
        {
            if (!EnableInventoryLogs || !EnableInventoryWarnings)
            {
                return;
            }
            Debug.LogWarning(FormatLog(message));
            NetLog.Write(FormatLog(message));
        }

        public static bool TryComputeTransfer(Item item, int requested, out int transferCount, out int remained)
        {
            transferCount = 0;
            remained = 0;
            if (item == null) return false;

            if (item is Weapon weapon)
            {
                transferCount = weapon.AmmoCount;
                return transferCount > 0;
            }

            int current = item.GetCount();
            if (requested <= 0 || current <= 0)
            {
                return false;
            }

            transferCount = Math.Min(requested, current);
            remained = current - transferCount;
            return transferCount > 0;
        }
    }
}
