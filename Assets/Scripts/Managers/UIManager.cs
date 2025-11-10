using Gameplay.GameplayObjects.Items;
using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers
{
    public class UIManager : MonoSingleton<UIManager>
    {
        [SerializeField] private Button serverButton = null;
        [SerializeField] private Button clientButton = null;

        [Header("Pickup-Box")]
        public GameObject ItemPickupPanel = null;
        public RectTransform ItemPickupBox = null;
        public TextMeshProUGUI ItemPickupName = null;
        public TextMeshProUGUI ItemPickupCount=null;
        public float RightOffset = 5f;
        public float LeftOffset = 5f;
        public float TopOffset = 5f;
        public float ButtomOffset = 5f;

        private Item _itemToPick=null;public Item ItemToPick { get => _itemToPick; set { _itemToPick = value; OnItemToPickUpdated(); } }

        private Vector2 _referenceResolution = new Vector2(1920, 1080);
        private Vector2 _screenScale=new Vector2(1,1);

        protected override void Awake()
        {
            base.Awake();
            ItemPickupPanel.gameObject.SetActive(false);
        }
        private void Start()
        {
            serverButton.onClick.AddListener(StartServer);
            clientButton.onClick.AddListener(StartClient);

            ItemPickupBox.anchorMax=Vector2.zero;
            ItemPickupBox.anchorMin=Vector2.zero;
            ItemPickupBox.pivot=Vector2.zero;

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
            ItemPickupBox.anchoredPosition = GetClampedScreenPosition(ItemToPick.transform.position);
        }

        private void StartServer()
        {
            serverButton.gameObject.SetActive(false);
            clientButton.gameObject.SetActive(false);
            SessionManager.Instance.StartServer();
        }

        private void StartClient()
        {
            serverButton.gameObject.SetActive(false);
            clientButton.gameObject.SetActive(false);
            SessionManager.Instance.StartClient();
        }

        private void OnItemToPickUpdated()
        {
            if (_itemToPick != null)
            {
                ItemPickupName.text = _itemToPick.Id;
                if(_itemToPick is Ammo ammo)
                {
                    ItemPickupCount.text = "x"+ammo.Count.ToString();
                }
                else
                {
                    ItemPickupCount.text = "x1";
                }
                ItemPickupPanel.gameObject.SetActive(true);
            }
            else
            {
                ItemPickupPanel.gameObject.SetActive(false);
            }
        }
    }
}
