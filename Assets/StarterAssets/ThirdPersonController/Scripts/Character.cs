using Gameplay.GameplayObjects.Items;
using LitJson;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.TextCore.Text;
using UnityEngine.Windows;

namespace StarterAssets
{
    public class Character : NetworkBehaviour
    {
        [SerializeField] private string id = "";public string Id { get => id; }
        [SerializeField] private Transform weaponHolder = null;

        public float GroundedOffset = -0.14f;

        public float GroundedRadius = 0.28f;

        public LayerMask GroundLayers;
        private float _fallTimeoutDelta;
        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;
        public Weapon CurrentWeapon { get; private set; } = null;
        private Weapon _weaponToEquip= null;
        public Ammo CurrentAmmo { get; private set; } = null; 
        private List<Item> _items = new List<Item>();
        private Transform _leftHandIKTarget = null;
        
        public bool IsReloading { get; private set; } = false;
        public bool IsSwitchingWeapon { get; private set; } = false;
        private PlayerAnimation _playerAnimation =null;
        private RigManager _rigManager = null;
        private Rigidbody[] _ragdollRigidbodies=null;
        private Collider[] _ragdollColliders=null;
        private ThirdPersonController _tpc = null;
        private CharacterController _controller = null;
        private Animator _animator;

        private NetworkVariable<bool> _isGroundedNet =
    new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public bool IsGrounded => _isGroundedNet.Value;
        private bool _isRunning = false;public bool IsRunning { get => _isRunning; set => _isRunning = value; }
        private bool _isSprinting = false;public bool IsSprinting { get => _isSprinting; set => _isSprinting = value; }
        private bool _isAiming = false; public bool IsAiming { get=>_isAiming; set=>_isAiming=value; } 

        private bool _isArmed = false;public bool IsArmed { get => _isArmed; set => _isArmed = value; }
        private float _speedAnimationMultiplier = 0;public float SpeedAnimationMultiplier { get => _speedAnimationMultiplier; set => _speedAnimationMultiplier = value; }
        private float _aimLayerWeight = 0f;public float AimLayerWeight { get => _aimLayerWeight; }
        private float _aimRigWeight = 0f;
        private float _leftHandWeight = 0f;
        private Vector3 _lastPositon=Vector3.zero;
        private Vector3 _lastAimTarget=Vector3.zero;
        private Vector2 _aimedMovingAnimationInput = Vector2.zero;
        private Vector3 _aimTarget=Vector3.zero;public Vector3 AimTarget { get => _aimTarget; set => _aimTarget = value; }
        
        private float _health = 100f;

        private ulong _clientID = 0; public ulong ClientID { get => _clientID; set => _clientID = value; }
        private bool _isInitialized = false; public bool IsInitialized { get => _isInitialized; set => _isInitialized = value; }
        private bool _componentsInitialized = false;
        private float _moveSpeed = 0f;public float MoveSpeed { get=>_moveSpeed; set => _moveSpeed = value; }
        private float _moveSpeedBlend = 0f;
        private float _lastMoveSpeed = 0f;
        private Vector2 _aimedMoveSpeed=Vector2.zero;
        private bool _lastAiming = false;
        private Vector2 _lastAimedMoveSpeed=Vector2.zero ;

        [System.Serializable]
        public struct Data
        {
            public Dictionary<string, int> items;
            public List<string> itemsId;
            public List<string> equippedIds;
        }

        public Data GetData() {
            Data data = new Data();

            data.items = new Dictionary<string, int>();
            data.itemsId = new List<string>();
            data.equippedIds = new List<string>();

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null)
                {
                    continue;
                }

                int value = 0;
                if (_items[i] is Weapon weapon) { 
                value=weapon.AmmoCount;
                }else if (_items[i] is Ammo ammo)
                {
                    value = ammo.Count;
                }
                data.items.Add(_items[i].Id,value);
                data.itemsId.Add(_items[i].NetworkId);

                if (CurrentWeapon != null && _items[i] == CurrentWeapon)
                {
                    data.equippedIds.Add(_items[i].NetworkId);
                }else if (CurrentAmmo != null && _items[i] == CurrentAmmo)
                {
                    data.equippedIds.Add(_items[i].NetworkId);
                }

            }

            return data;
        }
        private void Awake()
        {
            InitializeComponents();
        }

        public override void OnNetworkSpawn()
        {
            //_animator = GetComponent<Animator>();
            //_ragdollColliders = GetComponentsInChildren<Collider>();
            //_ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
            //_tpc = GetComponent<ThirdPersonController>();
            //_controller = GetComponent<CharacterController>();
            //if (_ragdollColliders != null)
            //{
            //    for (int i = 0; i < _ragdollColliders.Length; i++)
            //    {
            //        _ragdollColliders[i].isTrigger = false;
            //    }
            //}
            //if (_ragdollRigidbodies != null)
            //{
            //    for (int i = 0; i < _ragdollRigidbodies.Length; i++)
            //    {
            //        _ragdollRigidbodies[i].mass *= 50;
            //    }
            //}
        }

        private void InitializeComponents()
        {
            if(_componentsInitialized) return;
            _componentsInitialized = true;
            _animator = GetComponent<Animator>();
            _ragdollColliders = GetComponentsInChildren<Collider>();
            _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
            _tpc = GetComponent<ThirdPersonController>();
            _controller = GetComponent<CharacterController>();
            if (_ragdollColliders != null)
            {
                for (int i = 0; i < _ragdollColliders.Length; i++)
                {
                    _ragdollColliders[i].isTrigger = false;
                }
            }
            if (_ragdollRigidbodies != null)
            {
                for (int i = 0; i < _ragdollRigidbodies.Length; i++)
                {
                    _ragdollRigidbodies[i].mass *= 50;
                }
            }
            SetRagdollStatus(false);
            _playerAnimation = GetComponent<PlayerAnimation>();
            _rigManager = GetComponent<RigManager>();
            _fallTimeoutDelta = FallTimeout;
        }
        public void InitializeServer(Dictionary<string, int> items,List<string> itemsId,List<string> equippedIds,ulong clientID)
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;
            InitializeComponents();
            _clientID = clientID;
            SetLayer(transform, LayerMask.NameToLayer("NetworkPlayer"));
            _Initialize(items, itemsId,equippedIds);
        }
        [ClientRpc]
        public void InitializeClientRpc(string itemsJson,string itemIdJson,string equippedJson,string itemsOnGroundJson,ulong clientID)
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;
            InitializeComponents();
            _clientID= clientID;
            if (IsOwner)
                SetLayer(transform, LayerMask.NameToLayer("LocalPlayer"));
            else
                SetLayer(transform, LayerMask.NameToLayer("NetworkPlayer"));
            Dictionary<string, int> items = LitJson.JsonMapper.ToObject<Dictionary<string, int>>(itemsJson);
            List<string> itemsId = LitJson.JsonMapper.ToObject<List<string>>(itemIdJson);
            List<string> equippedIds=JsonMapper.ToObject<List<string>>(equippedJson);
            List<Item.Data> itemsOnGround=JsonMapper.ToObject<List<Item.Data>>(itemsOnGroundJson);
            InitializeItemsOnGround(itemsOnGround);
            if(items!=null&&itemsId!=null)
            {
                _Initialize(items, itemsId, equippedIds);
            }
        }

        private void InitializeItemsOnGround(List<Item.Data> itemsOnGround)
        {
            Item[] Items=FindObjectsByType<Item>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            List<Item> itemsOnGroundInScene=new List<Item>();
            if (Items != null)
            {
                for (int i = 0; i < Items.Length; i++)
                {
                    if (Items[i].transform.parent == null)
                    {
                        itemsOnGroundInScene.Add(Items[i]);
                    }
                }
            }
            for (int i = 0; i < itemsOnGroundInScene.Count; i++) {
                bool matched = false;
                for (int j = 0; j < itemsOnGround.Count; j++)
                {
                    if (itemsOnGroundInScene[i].Id == itemsOnGround[j].Id)
                    {
                        itemsOnGroundInScene[i].NetworkId= itemsOnGround[j].NetworkId;
                        itemsOnGroundInScene[i].transform.position=new Vector3(itemsOnGround[j].Position[0],itemsOnGround[j].Position[1],itemsOnGround[j].Position[2]);
                        itemsOnGroundInScene[i].transform.eulerAngles=new Vector3(itemsOnGround[j].Rotation[0],itemsOnGround[j].Rotation[1],itemsOnGround[j].Rotation[2]);
                        if (itemsOnGroundInScene[i] is Weapon weapon)
                        {
                            weapon.AmmoCount = itemsOnGround[j].Value;
                        }else if(itemsOnGroundInScene[i] is Ammo ammo)
                        {
                            ammo.Count = itemsOnGround[j].Value;
                        }
                        itemsOnGroundInScene[i].SetOnGroundStatus(true);
                        itemsOnGround.RemoveAt(j);
                        matched=true;
                        break;
                    }
                }
                if (!matched)
                {
                    Destroy(itemsOnGroundInScene[i].gameObject);
                }
            }
            for (int i = 0; i < itemsOnGround.Count; i++)
            {
                Item item = PrefabManager.Instance.GetItemInstance(itemsOnGround[i].Id);
                if (item != null)
                {
                    item.NetworkId = itemsOnGround[i].NetworkId;
                    item.Initialize();
                    item.SetOnGroundStatus(true);
                    if (item is Weapon weapon)
                    {
                        weapon.AmmoCount = itemsOnGround[i].Value;
                    }
                    else if (item is Ammo ammo)
                    {
                        ammo.Count = itemsOnGround[i].Value;
                    }
                    item.transform.position = new Vector3(itemsOnGround[i].Position[0], itemsOnGround[i].Position[1], itemsOnGround[i].Position[2]);
                    item.transform.eulerAngles = new Vector3(itemsOnGround[i].Rotation[0], itemsOnGround[i].Rotation[1], itemsOnGround[i].Rotation[2]);
                }
            }
        }

        [ClientRpc]
        public void InitializeClientRpc(string dataJson, ulong clientID,ClientRpcParams rpcParams=default)
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;
            InitializeComponents();
            _clientID = clientID;
            if (IsOwner)
                SetLayer(transform, LayerMask.NameToLayer("LocalPlayer"));
            else
                SetLayer(transform, LayerMask.NameToLayer("NetworkPlayer"));
            Data data=JsonMapper.ToObject<Data>(dataJson);
            _Initialize(data.items,data.itemsId, data.equippedIds);            
        }

        private void Update()
        {
            GroundedCheck();
            FreeFall();

            if (_shots.Count > 0 && !IsOwner)
            {
                if (CurrentWeapon != null && CurrentWeapon.NetworkId == _shots[0])
                {
                    Debug.Log("character shoot");
                    bool shoot = Shoot();
                    if (shoot)
                    {
                        _shots.RemoveAt(0);
                    }
                }
                else
                {
                    _shots.RemoveAt(0);
                }
            }
            IsArmed = CurrentWeapon != null;
            _playerAnimation.SetArmed(_isArmed);
            _playerAnimation.SetAim(_isAiming);
            float targetWeight = IsSwitchingWeapon || (_isArmed && (_isAiming || IsReloading)) ? 1f : 0f;

            _playerAnimation.SetAimLayerWeight(targetWeight);
            _aimRigWeight = Mathf.Lerp(_aimRigWeight, IsArmed &&IsAiming && !IsReloading ? 1f : 0f, Time.deltaTime * 10f);
            _leftHandWeight = Mathf.Lerp(_leftHandWeight, IsArmed && !IsSwitchingWeapon && (_isAiming || IsGrounded&& CurrentWeapon.GetWeaponType == Weapon.WeaponType.TwoHanded) && !IsReloading ? 1f : 0f, Time.deltaTime * 10f);

            _rigManager.AimWeight = _aimRigWeight;
            _rigManager.LeftHandWeight = _leftHandWeight;
            _rigManager.AimTarget = _aimTarget;

            _moveSpeedBlend=Mathf.Lerp(_moveSpeedBlend,_moveSpeed,Time.deltaTime*10f);
            if (_moveSpeedBlend < 0.01f)
            {
                _moveSpeedBlend = 0f;
            }

            if (IsSprinting)
            {
                _speedAnimationMultiplier = 3.0f;
            }
            else if (IsRunning)
            {
                _speedAnimationMultiplier = 2.0f;
            }
            else
            {
                _speedAnimationMultiplier = 1.0f;
            }
            if (IsOwner)
            { Vector3 deltaPosition = transform.InverseTransformDirection(transform.position - _lastPositon).normalized; 
              _aimedMoveSpeed=new Vector2(deltaPosition.x, deltaPosition.z)*_speedAnimationMultiplier;
            }
            _aimedMovingAnimationInput = Vector2.Lerp(_aimedMovingAnimationInput,_aimedMoveSpeed, Time.deltaTime * 10f);
            _playerAnimation.SetAimMoveSpeed(_aimedMovingAnimationInput.x, _aimedMovingAnimationInput.y);
            _playerAnimation.SetSpeed(_moveSpeedBlend);

            if (IsOwner)
            {
                if (_isAiming != _lastAiming)
                {
                    OnAimingChangedServerRpc(_isAiming);
                    _lastAiming = _isAiming;
                }
                if (_aimTarget != _lastAimTarget)
                {
                    OnAimTargetChangedServerRpc(_aimTarget);
                    _lastAimTarget = _aimTarget;
                }
                if (_isAiming)
                {
                    if (_aimedMoveSpeed != _lastAimedMoveSpeed)
                    {
                        OnAimingMoveChangedServerRpc(_aimedMoveSpeed);
                        _lastAimedMoveSpeed = _aimedMoveSpeed;
                    }
                }
                else
                {
                    if (_moveSpeed != _lastMoveSpeed)
                    {
                        OnMoveSpeedChangedServerRpc(_moveSpeed);
                        _lastMoveSpeed = _moveSpeed;
                    }
                }
            }
        }
        [ServerRpc]
        public void OnAimTargetChangedServerRpc(Vector3 value)
        {
            _aimTarget = value;
            OnAimTargetChangedClientRpc(value);
        }

        [ClientRpc]
        public void OnAimTargetChangedClientRpc(Vector3 value)
        {
            if (!IsOwner)
            {
                _aimTarget = value;
            }
        }
        [ServerRpc]
        public void OnAimingMoveChangedServerRpc(Vector2 value)
        {
            _aimedMoveSpeed = value;
            OnAimingMoveChangedClientRpc(value);
        }

        [ClientRpc]
        public void OnAimingMoveChangedClientRpc(Vector2 value)
        {
            if (!IsOwner)
            {
                _aimedMoveSpeed = value;
            }
        }

        [ServerRpc]
        public void OnAimingChangedServerRpc(bool value)
        {
            _isAiming = value;
            OnAimingChangedClientRpc(value);
        }

        [ClientRpc]
        public void OnAimingChangedClientRpc(bool value)
        {
            if (!IsOwner)
            {
                _isAiming = value;
            }
        }

        [ServerRpc]
        public void OnMoveSpeedChangedServerRpc(float value)
        {
            _moveSpeed=value;
            OnMoveSpeedChangedClientRpc(value);
        }

        [ClientRpc]
        public void OnMoveSpeedChangedClientRpc(float value)
        {
            if (!IsOwner)
            {
                _moveSpeed = value;
            }
        }

        private void LateUpdate()
        {
            _lastPositon = transform.position;
        }
        private void SetRagdollStatus(bool enabled)
        {
            if (_ragdollRigidbodies != null)
            {
                for(int i = 0; i < _ragdollRigidbodies.Length; i++)
                {
                    _ragdollRigidbodies[i].isKinematic = !enabled;
                }
            }
        }


        private void _Initialize(Dictionary<string, int> items,List<string> itemsId,List<string> equippedIds)
        {
            InitializeComponents();
            if (items != null)
            {
                int i = 0;
                int equippedWeaponIndex= -1;
                int equippedAmmoIndex = -1;
                foreach (var itemEntry in items) 
                {
                    string itemID = itemEntry.Key;
                    int count = itemEntry.Value;

                    if (count > 0)
                    {
                        Item prefab = PrefabManager.Instance.GetItemPrefab(itemID);
                        if (prefab == null) continue;

                        Item newItem = PrefabManager.Instance.GetItemInstance(itemID);
                        newItem.Initialize();
                        newItem.SetOnGroundStatus(false);
                        newItem.NetworkId = itemsId[i];
                        if (newItem != null)
                            {
                                if (newItem is Weapon newWeapon)
                                {
                                    newItem.transform.SetParent(weaponHolder);
                                    newItem.transform.localPosition = newWeapon.RightHandPosition;
                                    newItem.transform.localEulerAngles = newWeapon.RightHandRotation;
                                newWeapon.AmmoCount = count;
                                if (equippedIds.Contains(newItem.NetworkId)||equippedWeaponIndex<0)
                                    {
                                        equippedWeaponIndex =i;
                                    }
                                }
                                else if (newItem is Ammo newAmmo)
                                {
                                    newAmmo.Count = count;
                                if (equippedIds.Contains(newItem.NetworkId))
                                {
                                    equippedAmmoIndex = i;
                                }
                                }                              
                                newItem.gameObject.SetActive(false);
                                _items.Add(newItem);
                            i++;
                            }
                    }
                }
                if (equippedWeaponIndex >= 0 && CurrentWeapon == null)
                {
                    _weaponToEquip=(Weapon)_items[equippedWeaponIndex];
                    OnEquip();
                }
                if (equippedAmmoIndex >= 0)
                {
                    _EquipAmmo((Ammo)_items[equippedAmmoIndex]);
                }
            }
        }

        public void SwitchWeapon(float dir)
        {
            int x=dir>0?1:dir<0?-1:0;
            if(x == 0 || IsSwitchingWeapon)
            {
                return;
            }
            if(x>0)
            {
                NextWeapon();
            }
            else
            {
                PrevWeapon();
            }
        }

        private void NextWeapon()
        {
            int first = -1;
            int current = -1;
            for (int i = 0; i < _items.Count; i++) {
                if (_items[i]!=null&&_items[i] is Weapon weapon)
                {
                    if (CurrentWeapon!=null&&_items[i].gameObject==CurrentWeapon.gameObject)
                    {
                        current = i;
                    }
                    else
                    {
                        if (current >= 0)
                        {
                            EquipWeapon(weapon);
                            return;
                        }
                        else if(first < 0)
                        {
                            first = i;
                        }
                    }
                }
            }
            if(first >= 0)
            {
                EquipWeapon((Weapon)_items[first]);
            }
        }

        private void PrevWeapon()
        {
            int last = -1;
            int current = -1;
            for (int i = _items.Count-1;i>=0; i--)
            {
                if (_items[i] != null && _items[i] is Weapon weapon)
                {
                    if (CurrentWeapon != null && _items[i].gameObject == CurrentWeapon.gameObject)
                    {
                        current = i;
                    }
                    else
                    {
                        if (current >= 0)
                        {
                            EquipWeapon(weapon);
                            return;
                        }
                        else if (last < 0)
                        {
                            last = i;
                        }
                    }
                }
            }
            if (last >= 0)
            {
                EquipWeapon((Weapon)_items[last]);
            }
        }
        private void _EquipWeapon()
        {
            if(_weaponToEquip==null)
            {
                return;
            }
            CurrentWeapon = _weaponToEquip;
            _weaponToEquip = null;
            if (_leftHandIKTarget == null)
            {
                GameObject go = new GameObject("LeftHandIKTarget");
                _leftHandIKTarget = go.transform;
                _leftHandIKTarget.SetParent(weaponHolder);
            }
            if (CurrentWeapon.transform.parent != weaponHolder)
            {
                    CurrentWeapon.transform.SetParent(weaponHolder);
                    CurrentWeapon.transform.localPosition = CurrentWeapon.RightHandPosition;
                    CurrentWeapon.transform.localEulerAngles = CurrentWeapon.RightHandRotation;
            }

                _leftHandIKTarget.SetParent(CurrentWeapon.transform);

                _leftHandIKTarget.localPosition = CurrentWeapon.LeftHandPosition;
                _leftHandIKTarget.localEulerAngles = CurrentWeapon.LeftHandRotation;
                _rigManager.SetLeftHandTarget(_leftHandIKTarget);

                _leftHandIKTarget.gameObject.SetActive(true);
                CurrentWeapon.gameObject.SetActive(true);
                CurrentAmmo = null;
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i] is Ammo ammo && ammo.Id == CurrentWeapon.AmmoID)
                    {
                    _EquipAmmo(ammo);
                        break;
                    }

                }
        }

        private void _EquipAmmo(Ammo ammo)
        {
            if (ammo != null)
            {
                if (CurrentAmmo != null && CurrentWeapon != null && CurrentWeapon.AmmoID != CurrentAmmo.Id)
                {
                    return;
                }
                CurrentAmmo = ammo;
                if (CurrentAmmo.transform.parent != transform)
                {
                    CurrentAmmo.transform.SetParent(transform);
                    CurrentAmmo.transform.localPosition = Vector3.zero;
                    CurrentAmmo.transform.localEulerAngles = Vector3.zero;
                    CurrentAmmo.gameObject.SetActive(false);
                }
            }
        }
        public void EquipWeapon(Weapon weapon)
        {
            if (IsSwitchingWeapon||weapon==null)
            {
                return;
            }
            if (IsOwner)
            {
                EquipWeaponServerRpc(weapon.NetworkId);
            }
            _weaponToEquip = weapon;
            if (CurrentWeapon != null)
            {
                HolsterWeapon();
            }
            else
            {
                IsSwitchingWeapon = true;
                _playerAnimation.TriggerEquip();
            }

        }

        [ServerRpc]
        public void EquipWeaponServerRpc(string networkID)
        {
            EquipWeaponSync(networkID);
            EquipWeaponClientRpc(networkID);
        }

        [ClientRpc]
        public void EquipWeaponClientRpc(string networkID)
        {
            if (!IsOwner)
            {
                EquipWeaponSync(networkID);
            }
        }

        private void EquipWeaponSync(string networkID)
        {
            Weapon weapon=null;
            for(int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null && _items[i].NetworkId== networkID && _items[i] is Weapon w)
                {
                    weapon = w; break;
                }
            }
            if (weapon != null)
            {
                EquipWeapon(weapon);
            }
            else
            {

            }
        }
        private void _HolsterWeapon()
        {
            if (CurrentWeapon != null)
            {
                CurrentWeapon.gameObject.SetActive(false);
                CurrentWeapon = null;
                CurrentAmmo = null;
            }
        }
        public void HolsterWeapon()
        {
            if (IsSwitchingWeapon)
            {
                return;
            }
            if (CurrentWeapon != null)
            {
                if (IsOwner)
                {
                    HolsterWeaponServerRpc(CurrentWeapon.NetworkId);
                }
                IsSwitchingWeapon = true;
                _playerAnimation.TriggerHolster();
            }
        }
        [ServerRpc]
        public void HolsterWeaponServerRpc(string networkID)
        {
            HolsterWeaponSync(networkID);
            HolsterWeaponClientRpc(networkID);
        }
        [ClientRpc]
        public void HolsterWeaponClientRpc(string networkID)
        {
            if (!IsOwner)
            {
                HolsterWeaponSync(networkID);
            }
        }
        public void HolsterWeaponSync(string networkID)
        {
            if(CurrentWeapon != null && CurrentWeapon.NetworkId == networkID)
            {
                HolsterWeapon();
            }
            else
            {

            }
        }
        public void TakeDamage(Character character, Transform hit, float damage)
        {
            if (_health > 0)
            {
                _health -= damage;
                if (_health <= 0)
                {
                    _health = 0;
                    SetRagdollStatus(true);
                    Destroy(_rigManager);
                    Destroy(GetComponent<RigBuilder>());
                    Destroy(_playerAnimation);                    
                    Destroy(_tpc);
                    Destroy(_controller);
                    Destroy(this);
                }
            }
        }

        public void Reload()
        {
            if (CurrentWeapon != null&& CurrentWeapon.AmmoCount < CurrentWeapon.ClipSize && CurrentAmmo != null && CurrentAmmo.Count > 0)
            {
                if (IsOwner)
                {
                    ReloadServerRpc(CurrentWeapon.NetworkId, CurrentAmmo.NetworkId);
                }
                IsReloading = true;
                //_playerAnimation.SetAimLayerWeight(1f);
                _playerAnimation.TriggerReload();
                Debug.Log("Reloading...");
            }
        }
        [ServerRpc]
        public void ReloadServerRpc(string weaponId, string ammoId)
        {
            ReloadSync(weaponId, ammoId);
            ReloadClientRpc(weaponId, ammoId); 
        }
        [ClientRpc]
        public void ReloadClientRpc(string weaponId, string ammoId)
        {
            if(!IsOwner)
                ReloadSync(weaponId, ammoId);
        }
        private void ReloadSync(string weaponId,string ammoId)
        {
            if (CurrentWeapon != null && CurrentAmmo != null && CurrentAmmo.NetworkId == ammoId && CurrentWeapon.NetworkId == weaponId)
            {
                Reload();
            }
            else
            {

            }
        }
        public void ReloadFinished()
        {
            if (CurrentWeapon != null && CurrentWeapon.AmmoCount < CurrentWeapon.ClipSize && CurrentAmmo != null && CurrentAmmo.Count > 0)
            {
                int count= Mathf.Min(CurrentWeapon.ClipSize - CurrentWeapon.AmmoCount, CurrentAmmo.Count);
                CurrentAmmo.Count -= count;
                CurrentWeapon.AmmoCount += count;
                IsReloading=false;
                _playerAnimation.SetAimLayerWeight(0f);
                Debug.Log("Reload Finished.");
            }
        }

        public void OnEquip() { 
            _EquipWeapon();
        }

        public void OnHolster() { 
            _HolsterWeapon();
            if(_weaponToEquip!=null)
            {
                OnEquip();
            }
        }
        public void EquipFinished()
        {
            IsSwitchingWeapon = false;
        }

        public void HolsterFinished()
        {
            IsSwitchingWeapon = false;
        }

        private void SetLayer(Transform root,int layer)
        {
            var children=root.GetComponentsInChildren<Transform>(true);
            foreach(var child in children)
            {
                child.gameObject.layer = layer;
            }
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            bool isCurrentlyGrounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,QueryTriggerInteraction.Ignore);

            if (IsOwner) // 只有拥有者（Owner）有权限更新网络状态
            {
                if (_isGroundedNet.Value != isCurrentlyGrounded)
                {
                    _isGroundedNet.Value = isCurrentlyGrounded;
                }
            }

            _playerAnimation?.SetGrounded(_isGroundedNet.Value);
        }

        private void FreeFall()
        {
            if (IsGrounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                //_playerAnimation?.SetJump(false);
                //_animator.SetBool("Jump", false);
                //_playerAnimation?.SetFreeFall(false);
                _animator.SetBool("FreeFall", false);
            }
            else
            {
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    //_playerAnimation?.SetFreeFall(true);
                    _animator.SetBool("FreeFall", true);
                }

            }

        }

        public void Jump()
        {
            //_playerAnimation.SetJump(true);
            _animator.SetTrigger("Jump");
            JumpServerRpc();
        }

        [ServerRpc]
        public void JumpServerRpc()
        {
            //_playerAnimation.SetJump(true);
            _animator.SetTrigger("Jump");
            JumpClientRpc();
        }

        [ClientRpc]
        public void JumpClientRpc()
        {
            if(!IsOwner)
                // _playerAnimation.SetJump(true);
                _animator.SetTrigger("Jump");
        }

        private List<string> _shots=new List<string>();
        public bool Shoot()
        {
            if (CurrentWeapon!=null&& IsAiming && !IsReloading && CurrentWeapon.Shoot(this, _aimTarget))
            {
                if (IsOwner)
                {
                    ShootServerRpc(CurrentWeapon.NetworkId);
                }
                _rigManager.ApplyWeaponKick(CurrentWeapon.HandKick, CurrentWeapon.BodyKick);
                _playerAnimation.TriggerShoot();
                Debug.Log("Shoot");
                return true;
            }
            return false;
        }
        [ServerRpc]
        public void ShootServerRpc(string weaponId)
        {
            ShootSync(weaponId);
            ShootClientRpc(weaponId);
        }
        [ClientRpc]
        public void ShootClientRpc(string weaponId)
        {
            if (!IsOwner)
            {
                ShootSync(weaponId);
            }
        }
        public void ShootSync(string weaponId)
        {
            if (CurrentWeapon != null && CurrentWeapon.NetworkId == weaponId)
            {
                Debug.Log("Sync Shoot");
                bool shoot=Shoot();
                if (!shoot)
                {
                    _shots.Add(weaponId);
                }
            }
            else
            {

            }
        }
        private bool _isPickingItem = false;
        public void PickupItem(string networkId)
        {
            if (_isPickingItem) return;
            _isPickingItem = true;
            PickupItemServerRpc(networkId);
        }
        [ServerRpc]
        private void PickupItemServerRpc(string networkId,ServerRpcParams serverRpcParams = default)
        {
            bool success = false;
            Item[] items = FindObjectsByType<Item>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (items != null)
            {
                for(int i=0; i<items.Length; i++)
                {
                    if (items[i].transform.parent == null && items[i].NetworkId == networkId)
                    {
                        AddItemToInventory(items[i]);
                        success = true;
                        break;
                    }
                }
            }
            if (success)
            {
                PickupItemClientRpc(networkId, true);
            }
            else
            {
                ulong[] target = new ulong[1];
                target[0] = serverRpcParams.Receive.SenderClientId;
                ClientRpcParams clientRpcParams = default;
                clientRpcParams.Send.TargetClientIds = target;
                PickupItemClientRpc(networkId,false, clientRpcParams);
            }
        }
        [ClientRpc]
        private void PickupItemClientRpc(string networkId,bool success,ClientRpcParams clientRpcParams = default)
        {
            if (success)
            {
                bool founded = false;
                Item[] items = FindObjectsByType<Item>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (items != null)
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        if (items[i].transform.parent == null && items[i].NetworkId == networkId)
                        {
                            AddItemToInventory(items[i]);
                            founded = true;
                            break;
                        }
                    }
                }
                if(! founded)
                {

                }
            }
            _isPickingItem=false;
        }
        public void AddItemToInventory(Item item)
        {
            item.transform.SetParent(transform);
            item.Initialize();  
            item.SetOnGroundStatus(false);

            if(item is Weapon weapon)
            {
                item.transform.SetParent(weaponHolder);
                item.transform.localPosition = weapon.RightHandPosition;
                item.transform.localEulerAngles = weapon.RightHandRotation;
            }else if(item is Ammo ammo)
            {

            }
            item.gameObject.SetActive(false);
            _items.Add(item);
        }
        private void OnFootstep(AnimationEvent animationEvent)
        {
            //if (animationEvent.animatorClipInfo.weight > 0.5f)
            //{
            //    if (FootstepAudioClips.Length > 0)
            //    {
            //        var index = Random.Range(0, FootstepAudioClips.Length);
            //        AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
            //    }
            //}
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            //if (animationEvent.animatorClipInfo.weight > 0.5f)
            //{
            //    AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            //}
        }
    }
}

