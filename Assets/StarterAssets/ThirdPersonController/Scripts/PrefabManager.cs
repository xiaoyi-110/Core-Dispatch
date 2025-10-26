using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gameplay.GameplayObjects.Items;

namespace StarterAssets
{
    public class PrefabManager : MonoSingleton<PrefabManager>   
    {
        [SerializeField] private Item[] _itemPrefabsArray = null;
        private Dictionary<string, Item> _itemPrefabs = new Dictionary<string, Item>();
        protected override void Awake() // ʹ�� Awake ����ʼ�� Dictionary
        {
            base.Awake();
            if (_itemPrefabsArray != null)
            {
                // ʹ�� LINQ ������ת��Ϊ Dictionary���� Id Ϊ��
                _itemPrefabs = _itemPrefabsArray.ToDictionary(item => item.Id, item => item);
            }
        }

        public Item GetItemPrefab(string itemID)
        {
            // O(1) ����
            if (_itemPrefabs.TryGetValue(itemID, out Item itemPrefab))
            {
                return itemPrefab;
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

