using Gameplay.GameplayObjects.Items;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers
{
    public class InventoryItem : MonoBehaviour
    {
        public TextMeshProUGUI ItemName=null;
        public TextMeshProUGUI ItemCount=null;

        private Item _item=null;public Item _Item { get=> _item;}
        private int _count = 1;public int Count { get => _count; set => _count = value; }

        private void Awake()
        {
            Button button = GetComponent<Button>();
            if(button != null)
            {
                button.onClick.AddListener(Clicked);
            }
        }

        public void Initialize(Item item)
        {
            if(item != null)
            {
                _item = item;
                ItemName.text = _item.Id;
                _count = item.GetCount();
                ItemCount.text = "x" + _count.ToString();
            }
        }
        private void Clicked()
        {
            UIManager.Instance.ItemClicked(this);
        }
    }
}

