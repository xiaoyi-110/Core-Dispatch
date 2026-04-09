using System.Collections.Generic;
using Gameplay.GameplayObjects.Items;
using StarterAssets;
using UnityEngine;

namespace Managers
{
    public static class WorldRegistry
    {
        private static readonly HashSet<Item> ItemsSet = new HashSet<Item>();
        private static readonly Dictionary<string, Item> ItemsByNetworkId = new Dictionary<string, Item>();

        private static readonly HashSet<Character> CharactersSet = new HashSet<Character>();
        private static readonly Dictionary<ulong, Character> CharactersByClientId = new Dictionary<ulong, Character>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            ItemsSet.Clear();
            ItemsByNetworkId.Clear();
            CharactersSet.Clear();
            CharactersByClientId.Clear();
        }

        public static IReadOnlyCollection<Item> Items => ItemsSet;
        public static IReadOnlyCollection<Character> Characters => CharactersSet;

        public static void RegisterItem(Item item)
        {
            if (item == null) return;
            ItemsSet.Add(item);
            if (!string.IsNullOrEmpty(item.NetworkId))
            {
                ItemsByNetworkId[item.NetworkId] = item;
            }
        }

        public static void UnregisterItem(Item item)
        {
            if (item == null) return;
            ItemsSet.Remove(item);
            if (!string.IsNullOrEmpty(item.NetworkId) &&
                ItemsByNetworkId.TryGetValue(item.NetworkId, out Item existing) &&
                existing == item)
            {
                ItemsByNetworkId.Remove(item.NetworkId);
            }
        }

        public static void UpdateItemNetworkId(Item item, string oldId, string newId)
        {
            if (!string.IsNullOrEmpty(oldId) &&
                ItemsByNetworkId.TryGetValue(oldId, out Item existing) &&
                existing == item)
            {
                ItemsByNetworkId.Remove(oldId);
            }

            if (!string.IsNullOrEmpty(newId))
            {
                ItemsByNetworkId[newId] = item;
            }
        }

        public static bool TryGetItemByNetworkId(string networkId, out Item item)
        {
            if (string.IsNullOrEmpty(networkId))
            {
                item = null;
                return false;
            }
            return ItemsByNetworkId.TryGetValue(networkId, out item);
        }

        public static bool TryResolveItemByNetworkId(string networkId, out Item item)
        {
            if (TryGetItemByNetworkId(networkId, out item))
            {
                return true;
            }
            if (!string.IsNullOrEmpty(networkId))
            {
                foreach (var existing in ItemsSet)
                {
                    if (existing != null && existing.NetworkId == networkId)
                    {
                        item = existing;
                        return true;
                    }
                }
            }
            item = null;
            return false;
        }

        public static void RegisterCharacter(Character character)
        {
            if (character == null) return;
            CharactersSet.Add(character);
        }

        public static void UnregisterCharacter(Character character)
        {
            if (character == null) return;
            CharactersSet.Remove(character);
            if (CharactersByClientId.TryGetValue(character.ClientID, out Character existing) &&
                existing == character)
            {
                CharactersByClientId.Remove(character.ClientID);
            }
        }

        public static void UpdateCharacterClientId(Character character, bool hadOldId, ulong oldId, ulong newId)
        {
            if (hadOldId &&
                CharactersByClientId.TryGetValue(oldId, out Character existing) &&
                existing == character)
            {
                CharactersByClientId.Remove(oldId);
            }

            CharactersByClientId[newId] = character;
        }

        public static bool TryGetCharacter(ulong clientId, out Character character)
        {
            return CharactersByClientId.TryGetValue(clientId, out character);
        }
    }
}
