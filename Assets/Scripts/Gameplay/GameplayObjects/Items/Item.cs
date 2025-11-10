using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.GameplayObjects.Items
{
    public class Item : MonoBehaviour
    {
        [SerializeField] private string id = "";public string Id { get { return id; } }
        public int Count { get; set; } = 0;
        private string _networkId = "";public string NetworkId { get=>_networkId; set =>_networkId = value; } 

        private Rigidbody _rigidbody=null;
        private Collider _collider=null;

        private bool _canBePickUp = false;public bool CanBePickUp { get => _canBePickUp; set => _canBePickUp = value; }
        private bool _initialized = false;

        [System.Serializable]
        public struct Data
        {
            public string Id;
            public string NetworkId;
            public int Value;
            public float[] Position;
            public float[] Rotation;
        }

        public Data GetData()
        {
            Data data = new Data();
            data.Id = Id;
            data.NetworkId = NetworkId;
            if (this is Weapon weapon)
            {
                data.Value = weapon.AmmoCount;
            }else if(this is Ammo ammo)
            {
                data.Value=ammo.Count;
            }

            data.Position = new float[3];
            data.Position[0] = transform.position.x;
            data.Position [1] = transform.position.y;
            data.Position[2]=transform.position.z;

            data.Rotation = new float[3];
            data.Rotation[0] = transform.eulerAngles.x;
            data.Rotation [1] = transform.eulerAngles.y;
            data.Rotation[2]=transform.eulerAngles.z;
            return data;
        }
        protected virtual void Awake()
        {
            Initialize();   
        }

        protected virtual void Start()
        {
            if(transform.parent == null)
            {
                SetOnGroundStatus(true);
            }
        }
        public void Initialize()
        {
            if(_initialized) return;
            _initialized = true;
            gameObject.tag = "Item";
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _collider.isTrigger = false;
            _rigidbody.mass = 40f;
        }

        public void SetOnGroundStatus(bool status)
        {
            _rigidbody.isKinematic = !status;
            _collider.enabled = status;
            _canBePickUp= status;
        }
    }
}

