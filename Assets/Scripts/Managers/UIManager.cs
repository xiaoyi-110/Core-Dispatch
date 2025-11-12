using Gameplay.GameplayObjects.Items;
using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Managers
{
    public class UIManager : MonoSingleton<UIManager>
    {
        [Header("Pickup-Box")]
        public GameObject ItemPickupPanel = null;
        public RectTransform ItemPickupBox = null;
        public TextMeshProUGUI ItemPickupName = null;
        public TextMeshProUGUI ItemPickupCount=null;
        public float RightOffset = 5f;
        public float LeftOffset = 5f;
        public float TopOffset = 5f;
        public float ButtomOffset = 5f;

        [Header("Loot Box")]
        public GameObject ItemLootPanel=null;
        public RectTransform ItemLootBox = null;
        public TextMeshProUGUI ItemLootName = null;
        
        [Header("Inventory")]
        public GameObject InventoryPanel=null;
        public InventoryItem InventoryItemPrefab = null;
        public RectTransform InventoryGrid1=null;
        public RectTransform InventoryGrid2=null;
        public TextMeshProUGUI InventoryGridTitle1=null;
        public TextMeshProUGUI InventoryGridTitle2 =null;
        public Button CloseButton=null;

        public Text Debug=null;
        private Item _itemToPick=null;public Item ItemToPick { get => _itemToPick; set { _itemToPick = value; OnItemToPickUpdated(); } }
        private Character _characterToLoot = null;public Character CharacterToLoot { get => _characterToLoot; set { _characterToLoot = value;OnCharacterToLootUpdated(); } }
        private Character _characterLootTarget = null;

        private Vector2 _referenceResolution = new Vector2(1920, 1080);
        private Vector2 _screenScale=new Vector2(1,1);

        private List<InventoryItem> _inventoryItems1=new List<InventoryItem>();
        private List<InventoryItem> _inventoryItems2=new List<InventoryItem>();

        private bool _isInventoryOpen=false;public bool IsInventoryOpen { get => _isInventoryOpen; }

        protected override void Awake()
        {
            base.Awake();
            ItemPickupPanel.gameObject.SetActive(false);
            InventoryPanel.gameObject.SetActive(false);
        }
        private void Start()
        {
            CloseButton.onClick.AddListener(CloseInventory);

            ItemPickupBox.anchorMax=Vector2.zero;
            ItemPickupBox.anchorMin=Vector2.zero;
            ItemPickupBox.pivot=Vector2.zero;

            ItemLootBox.anchorMax = Vector2.zero;
            ItemLootBox.anchorMin = Vector2.zero;
            ItemLootBox.pivot = Vector2.zero;

            CanvasScaler scaler=GetComponent<CanvasScaler>();
            if(scaler != null )
            {
                _referenceResolution = scaler.referenceResolution;
                _screenScale=new Vector2(_referenceResolution.x/Screen.width,_referenceResolution.y/Screen.height);
            }
        }

        private Vector2 GetClampedScreenPosition(Vector3 worldPos)
        {
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(CameraManager.Instance.MainCamera, worldPos);
            screenPos *= _screenScale;

            screenPos.x = Mathf.Clamp(screenPos.x, LeftOffset, _referenceResolution.x - ItemPickupBox.sizeDelta.x - RightOffset);
            screenPos.y = Mathf.Clamp(screenPos.y, ButtomOffset, _referenceResolution.y - ItemPickupBox.sizeDelta.y - TopOffset);

            return screenPos;
        }

        private void Update()
        {
            if (ItemToPick == null) return;
            ItemPickupBox.anchoredPosition = GetClampedScreenPosition(_itemToPick.transform.position);
            ItemLootBox.anchoredPosition=GetClampedScreenPosition(_characterToLoot.transform.position);
        }

        //private void StartServer()
        //{
        //    SessionManager.Instance.StartServer();
        //}

        //private void StartClient()
        //{
        //    SessionManager.Instance.StartClient();
        //}

        private void OnCharacterToLootUpdated()
        {
            if(_characterToLoot != null)
            {
                ItemLootName.text = _characterToLoot.Id;
                ItemLootPanel.gameObject.SetActive(true);
            }
            else
            {
                ItemLootPanel.gameObject.SetActive(false);
            }
        }
        private void OnItemToPickUpdated()
        {
            if (_itemToPick != null)
            {
                ItemPickupName.text = _itemToPick.Id;
                ItemPickupCount.text = "x" + _itemToPick.GetCount().ToString();
                ItemPickupPanel.gameObject.SetActive(true);
            }
            else
            {
                ItemPickupPanel.gameObject.SetActive(false);
            }
        }

        public void CloseInventory()
        {
            if (!_isInventoryOpen)
            {
                return;
            }

            if (_characterLootTarget != null)
            {
                if(Character.LocalPlayer != null)
                {
                    Dictionary<Item,int> itemsToStore=new Dictionary<Item, int>();
                    Dictionary<Item,int> itemsToTake=new Dictionary<Item,int>();
                    for(int i = 0; i < _inventoryItems2.Count; i++)
                    {
                        if (_inventoryItems2[i] != null && _inventoryItems2[i]._Item != null && Character.LocalPlayer.Inventory.Contains(_inventoryItems2[i]._Item))
                        {
                            itemsToStore.Add(_inventoryItems2[i]._Item, _inventoryItems2[i].Count);
                        }
                    }
                    for (int i = 0; i < _inventoryItems1.Count; i++)
                    {
                        if (_inventoryItems1[i] != null && _inventoryItems1[i]._Item != null && _characterLootTarget.Inventory.Contains(_inventoryItems1[i]._Item))
                        {
                            itemsToTake.Add(_inventoryItems1[i]._Item, _inventoryItems1[i].Count);
                        }
                    }
                    if(itemsToStore.Count > 0 || itemsToTake.Count > 0)
                    {
                        SessionManager.Instance.TradeItemsBetweenCharacters(Character.LocalPlayer,_characterLootTarget, itemsToStore,itemsToTake);
                    }
                }
            }
            else
            {
                if (_inventoryItems2.Count > 0 && Character.LocalPlayer != null)
                {
                    Dictionary<Item, int> items = new Dictionary<Item, int>();
                    for (int i = 0; i < _inventoryItems2.Count; i++)
                    {
                        if (_inventoryItems2[i] != null && _inventoryItems2[i]._Item != null)
                        {
                            items.Add(_inventoryItems2[i]._Item, _inventoryItems2[i].Count);
                        }
                    }
                    Character.LocalPlayer.DropItems(items);
                }
            }
            _characterLootTarget = null;
            _isInventoryOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            InventoryPanel.gameObject.SetActive(false);
        }

        public void OpenInventory()
        {
            if (_isInventoryOpen)
            {
                return;
            }
            if (Character.LocalPlayer != null)
            {
                ClearInventoryItems();
                InventoryGridTitle1.text = "Inventory";
                InventoryGridTitle2.text = "On Ground";
                for (int i = 0; i < Character.LocalPlayer.Inventory.Count; i++)
                {
                    if (Character.LocalPlayer.Inventory[i].GetType() != typeof(Weapon) && Character.LocalPlayer.Inventory[i].GetCount() <= 0)
                    {
                        continue;
                    }
                    InventoryItem item = Instantiate(InventoryItemPrefab, InventoryGrid1);
                    item.Initialize(Character.LocalPlayer.Inventory[i]);
                    _inventoryItems1.Add(item);
                }
                _isInventoryOpen = true;
                Cursor.lockState = CursorLockMode.None;
                InventoryPanel.gameObject.SetActive(true);
            }
        }


        public void OpenInventoryForLoot(Character lootTarget)
        {
            if (_isInventoryOpen)
            {
                return;
            }
            if (lootTarget != null && lootTarget.Health <= 0 && Character.LocalPlayer != null && lootTarget != Character.LocalPlayer)
            {
                _characterLootTarget = lootTarget;
                ClearInventoryItems();
                InventoryGridTitle1.text = "Inventory";
                InventoryGridTitle2.text = "Player" + lootTarget.ClientID.ToString();

                for (int i = 0; i < Character.LocalPlayer.Inventory.Count; i++)
                {
                    if (Character.LocalPlayer.Inventory[i].GetType() != typeof(Weapon) && Character.LocalPlayer.Inventory[i].GetCount() <= 0)
                    {
                        continue;
                    }
                    InventoryItem item = Instantiate(InventoryItemPrefab, InventoryGrid1);
                    item.Initialize(Character.LocalPlayer.Inventory[i]);
                    _inventoryItems1.Add(item);
                } 

                for (int i = 0; i < _characterLootTarget.Inventory.Count; i++)
                {
                    if (_characterLootTarget.Inventory[i].GetType() != typeof(Weapon) && _characterLootTarget.Inventory[i].GetCount() <= 0)
                    {
                        continue;
                    }
                    InventoryItem item = Instantiate(InventoryItemPrefab, InventoryGrid2);
                    item.Initialize(_characterLootTarget.Inventory[i]);
                    _inventoryItems2.Add(item);
                }
                _isInventoryOpen = true;
                Cursor.lockState = CursorLockMode.None;
                InventoryPanel.gameObject.SetActive(true);
            }
        }
        public void ItemClicked(InventoryItem item)
        {
            if(item!=null&&item._Item!=null)
            {
                if (_inventoryItems1.Contains(item))
                {
                    _inventoryItems1.Remove(item);
                    item.transform.SetParent(InventoryGrid2);
                    _inventoryItems2.Add(item);
                }else if(_inventoryItems2.Contains(item))
                {
                    item.transform.SetParent(InventoryGrid1);
                    _inventoryItems2.Remove(item);
                    _inventoryItems1.Add(item);
                }
            }
        }

        private void ClearInventoryItems()
        {
            for(int i = 0; i < _inventoryItems1.Count; i++)
            {
                if( _inventoryItems1[i] != null)
                {
                    Destroy(_inventoryItems1[i].gameObject);
                }
            }
            for (int i = 0; i < _inventoryItems2.Count; i++)
            {
                if (_inventoryItems2[i] != null)
                {
                    Destroy(_inventoryItems2[i].gameObject);
                }
            }
            _inventoryItems1.Clear();
            _inventoryItems2.Clear();
        }
    }
}
