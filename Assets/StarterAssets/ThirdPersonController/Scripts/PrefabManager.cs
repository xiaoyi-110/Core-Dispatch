using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gameplay.GameplayObjects.Items;

namespace StarterAssets
{
    public class PrefabManager : MonoSingleton<PrefabManager>   
    {
        [SerializeField] private Item[] itemPrefabsArray = null;
        [SerializeField] private Character[] characterPrefabsArray = null;
        private Dictionary<string, Item> _itemPrefabs = new Dictionary<string, Item>();
        private Dictionary<string, Character> _characterPrefabs = new Dictionary<string, Character>();
        protected override void Awake() // 使用 Awake 来初始化 Dictionary
        {
            base.Awake();
            if (itemPrefabsArray != null)
            {
                // 使用 LINQ 将数组转换为 Dictionary，以 Id 为键
                _itemPrefabs = itemPrefabsArray.ToDictionary(item => item.Id, item => item);
            }
            if (characterPrefabsArray != null)
            {
                _characterPrefabs = characterPrefabsArray.ToDictionary(character => character.Id, character => character);
            }
        }

        public Item GetItemPrefab(string itemID)
        {
            if (_itemPrefabs.TryGetValue(itemID, out Item itemPrefab))
            {
                return itemPrefab;
            }
            return null;
        }
        public Character GetCharacterPrefab(string id)
        {           
            if (_characterPrefabs.TryGetValue(id, out Character characterPrefab))
            {
                return characterPrefab;
            }
            return null;
        }
        public Item GetItemInstance(string itemID)
        {
            Item itemPrefab=GetItemPrefab(itemID);
            if(itemPrefab!=null)
            {
                return Instantiate(itemPrefab);
            }
            return null;
        }
    }
}

