using Gameplay.GameplayObjects.Items;
using Netcode;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Rendering.UI;
using UnityEngine.TextCore.Text;
using UnityEngine.Windows;
using Utility;
using Managers;

namespace StarterAssets
{
    public partial class Character : NetworkBehaviour
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
        private List<Item> _items = new List<Item>();public List<Item> Inventory { get=> _items; }
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
        
        private float _health = 100f;public float Health { get => _health; }
        private ulong _clientID = 0;
        private bool _hasClientId = false;
        public ulong ClientID
        {
            get => _clientID;
            set
            {
                if (_hasClientId && _clientID == value) return;
                ulong oldId = _clientID;
                bool hadOld = _hasClientId;
                _clientID = value;
                _hasClientId = true;
                WorldRegistry.UpdateCharacterClientId(this, hadOld, oldId, _clientID);
            }
        }
        private bool _isInitialized = false; public bool IsInitialized { get => _isInitialized; set => _isInitialized = value; }
        private bool _componentsInitialized = false;
        private float _moveSpeed = 0f;public float MoveSpeed { get=>_moveSpeed; set => _moveSpeed = value; }
        private float _moveSpeedBlend = 0f;
        private float _lastMoveSpeed = 0f;
        private Vector2 _aimedMoveSpeed=Vector2.zero;
        private bool _lastAiming = false;
        private Vector2 _lastAimedMoveSpeed=Vector2.zero ;
        private CharacterNetState _lastSentNetState;
        [Header("Net Sync Throttle")]
        [SerializeField] private float netSendInterval = 0.1f;
        [SerializeField] private float aimTargetEpsilon = 0.02f;
        [SerializeField] private float aimedMoveEpsilon = 0.01f;
        [SerializeField] private float moveSpeedEpsilon = 0.02f;
        private float _nextNetSendTime = 0f;

        public static Character LocalPlayer = null;
        private NetworkObject _networkObject=null;

        private void Awake()
        {
            InitializeComponents();
        }
        private void OnEnable()
        {
            WorldRegistry.RegisterCharacter(this);
        }
        private void OnDisable()
        {
            WorldRegistry.UnregisterCharacter(this);
        }


        private void InitializeComponents()
        {
            if(_componentsInitialized) return;
            gameObject.tag = "Character";
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
            _networkObject = GetComponent<NetworkObject>();
            if (_networkObject != null)
            {
                _networkObject.DontDestroyWithOwner = false;
            }
        }
        private void EnsureOwnershipSetup()
        {
            if (IsOwner)
            {
                Tools.SetLayerMask(transform, LayerMask.NameToLayer("LocalPlayer"));
                if (LocalPlayer == null || LocalPlayer == this)
                {
                    LocalPlayer = this;
                }
            }
            else
            {
                Tools.SetLayerMask(transform, LayerMask.NameToLayer("NetworkPlayer"));
                if (LocalPlayer == this)
                {
                    LocalPlayer = null;
                }
            }
        }
        public void InitializeServer(Dictionary<string, (string, int)> items,List<string> itemsId,List<string> equippedIds,ulong clientID)
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;
            InitializeComponents();
            ClientID = clientID;
            Tools.SetLayerMask(transform, LayerMask.NameToLayer("NetworkPlayer"));
            _Initialize(items, itemsId,equippedIds);
        }
        [ClientRpc]
        public void InitializeClientRpc(Managers.SessionManager.CharacterInitNetData initData, Managers.SessionManager.ItemStateNetData[] itemsOnGround, ulong clientID)
        {
            if (_isInitialized)
            {
                InitializeComponents();
                ClientID = clientID;
                EnsureOwnershipSetup();
                if (itemsOnGround != null && itemsOnGround.Length > 0)
                {
                    InitializeItemsOnGround(itemsOnGround);
                }
                return;
            }
            _isInitialized = true;
            InitializeComponents();
            ClientID = clientID;
            EnsureOwnershipSetup();
            List<string> itemsId = new List<string>();
            List<string> equippedIds = new List<string>();
            Dictionary<string, (string, int)> items = new Dictionary<string, (string, int)>();

            if (initData.Items != null)
            {
                for (int i = 0; i < initData.Items.Length; i++)
                {
                    itemsId.Add(initData.Items[i].NetworkId);
                    items.Add(i.ToString(), (initData.Items[i].ItemId, initData.Items[i].Count));
                }
            }
            if (initData.EquippedIds != null)
            {
                equippedIds.AddRange(initData.EquippedIds);
            }

            InitializeItemsOnGround(itemsOnGround);
            if (items.Count > 0 && itemsId.Count > 0)
            {
                _Initialize(items, itemsId, equippedIds);
            }
        }

        private void InitializeItemsOnGround(Managers.SessionManager.ItemStateNetData[] itemsOnGround)
        {
            List<Item> itemsOnGroundInScene = new List<Item>();
            foreach (var item in WorldRegistry.Items)
            {
                if (item != null && item.transform.parent == null)
                {
                    itemsOnGroundInScene.Add(item);
                }
            }
            List<Managers.SessionManager.ItemStateNetData> itemsOnGroundList = new List<Managers.SessionManager.ItemStateNetData>();
            if (itemsOnGround != null)
            {
                itemsOnGroundList.AddRange(itemsOnGround);
            }
            for (int i = 0; i < itemsOnGroundInScene.Count; i++) {
                bool matched = false;
                int matchIndex = -1;
                if (!string.IsNullOrEmpty(itemsOnGroundInScene[i].NetworkId))
                {
                    for (int j = 0; j < itemsOnGroundList.Count; j++)
                    {
                        if (itemsOnGroundInScene[i].NetworkId == itemsOnGroundList[j].NetworkId)
                        {
                            matchIndex = j;
                            break;
                        }
                    }
                }
                if (matchIndex < 0)
                {
                    for (int j = 0; j < itemsOnGroundList.Count; j++)
                    {
                        if (string.IsNullOrEmpty(itemsOnGroundList[j].NetworkId) && itemsOnGroundInScene[i].Id == itemsOnGroundList[j].Id)
                        {
                            matchIndex = j;
                            break;
                        }
                    }
                }
                if (matchIndex >= 0)
                {
                    var data = itemsOnGroundList[matchIndex];
                    itemsOnGroundInScene[i].NetworkId = data.NetworkId;
                    itemsOnGroundInScene[i].transform.position = data.Position;
                    itemsOnGroundInScene[i].transform.eulerAngles = data.Rotation;
                    itemsOnGroundInScene[i].SetCount(data.Count);
                    itemsOnGroundInScene[i].SetOnGroundStatus(true);
                    itemsOnGroundList.RemoveAt(matchIndex);
                    matched = true;
                }
                if (!matched)
                {
                    Destroy(itemsOnGroundInScene[i].gameObject);
                }
            }
            for (int i = 0; i < itemsOnGroundList.Count; i++)
            {
                Item item = PrefabManager.Instance.GetItemInstance(itemsOnGroundList[i].Id);
                if (item != null)
                {
                    item.NetworkId = itemsOnGroundList[i].NetworkId;
                    item.Initialize();
                    item.SetOnGroundStatus(true);
                    item.SetCount(itemsOnGroundList[i].Count);
                    item.transform.position = itemsOnGroundList[i].Position;
                    item.transform.eulerAngles = itemsOnGroundList[i].Rotation;
                }
            }
        }

        [ClientRpc]
        public void InitializeClientRpc(Managers.SessionManager.CharacterInitNetData data, ulong clientID,ClientRpcParams rpcParams=default)
        {
            if (_isInitialized)
            {
                InitializeComponents();
                ClientID = clientID;
                EnsureOwnershipSetup();
                return;
            }
            _isInitialized = true;
            InitializeComponents();
            ClientID = clientID;
            EnsureOwnershipSetup();
            _health = data.Health;

            List<string> itemsId = new List<string>();
            List<string> equippedIds = new List<string>();
            Dictionary<string, (string, int)> items = new Dictionary<string, (string, int)>();

            if (data.Items != null)
            {
                for (int i = 0; i < data.Items.Length; i++)
                {
                    itemsId.Add(data.Items[i].NetworkId);
                    items.Add(i.ToString(), (data.Items[i].ItemId, data.Items[i].Count));
                }
            }
            if (data.EquippedIds != null)
            {
                equippedIds.AddRange(data.EquippedIds);
            }

            _Initialize(items, itemsId, equippedIds);
            if (_health <= 0)
            {
                HealthCheck();
            }
        }

        private void Update()
        {
            if (_health <= 0) return;
            GroundedCheck();
            FreeFall();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                RecordLagCompSnapshot();
            }

            if (_shots.Count > 0 && !IsOwner)
            {
                PendingShot shot = _shots[0];
                if (TryPlayRemoteShotFx(shot, "queue"))
                {
                    _shots.RemoveAt(0);
                }
                else
                {
                    shot.RetryCount++;
                    bool expired = (Time.time - shot.EnqueuedAt) > 1.5f || shot.RetryCount > 90;
                    if (expired)
                    {
                        _shots.RemoveAt(0);
                        NetLog.Write($"[Combat] RemoteShotDropped seq={shot.ShotSequence} weapon={shot.WeaponId} retries={shot.RetryCount}");
                    }
                    else
                    {
                        _shots[0] = shot;
                    }
                }
            }
            IsArmed = CurrentWeapon != null;
            if (_playerAnimation != null)
            {
                _playerAnimation.SetArmed(_isArmed);
                _playerAnimation.SetAim(_isAiming);
            }
            float targetWeight = IsSwitchingWeapon || (_isArmed && (_isAiming || IsReloading)) ? 1f : 0f;

            if (_playerAnimation != null)
            {
                _playerAnimation.SetAimLayerWeight(targetWeight);
            }
            _aimRigWeight = Mathf.Lerp(_aimRigWeight, IsArmed &&IsAiming && !IsReloading ? 1f : 0f, Time.deltaTime * 10f);
            _leftHandWeight = Mathf.Lerp(_leftHandWeight, IsArmed && !IsSwitchingWeapon && (_isAiming || IsGrounded&& CurrentWeapon.GetWeaponType == Weapon.WeaponType.TwoHanded) && !IsReloading ? 1f : 0f, Time.deltaTime * 10f);

            if (_rigManager != null)
            {
                _rigManager.AimWeight = _aimRigWeight;
                _rigManager.LeftHandWeight = _leftHandWeight;
                _rigManager.AimTarget = _aimTarget;
            }

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
            if (_playerAnimation != null)
            {
                _playerAnimation.SetAimMoveSpeed(_aimedMovingAnimationInput.x, _aimedMovingAnimationInput.y);
                _playerAnimation.SetSpeed(_moveSpeedBlend);
            }

            if (IsOwner)
            {
                bool aimChanged = _isAiming != _lastAiming;
                bool timeReady = Time.time >= _nextNetSendTime;
                if (aimChanged || timeReady)
                {
                    CharacterNetState state = new CharacterNetState
                    {
                        IsAiming = _isAiming,
                        AimTarget = _aimTarget,
                        AimedMoveSpeed = _aimedMoveSpeed,
                        MoveSpeed = _moveSpeed
                    };

                    bool aimTargetChanged = (_aimTarget - _lastSentNetState.AimTarget).sqrMagnitude > aimTargetEpsilon * aimTargetEpsilon;
                    bool aimedMoveChanged = (_aimedMoveSpeed - _lastSentNetState.AimedMoveSpeed).sqrMagnitude > aimedMoveEpsilon * aimedMoveEpsilon;
                    bool moveSpeedChanged = Mathf.Abs(_moveSpeed - _lastSentNetState.MoveSpeed) > moveSpeedEpsilon;
                    bool aimingChanged = _isAiming != _lastSentNetState.IsAiming;

                    if (aimingChanged || aimTargetChanged || aimedMoveChanged || moveSpeedChanged)
                    {
                        SubmitNetStateServerRpc(state);
                        _lastSentNetState = state;
                        _lastAiming = _isAiming;
                        _lastAimTarget = _aimTarget;
                        _lastAimedMoveSpeed = _aimedMoveSpeed;
                        _lastMoveSpeed = _moveSpeed;
                    }

                    _nextNetSendTime = Time.time + netSendInterval;
                }

                FlushShootRequests();
            }

            ApplyMovementCorrection();
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


        private void _Initialize(Dictionary<string,(string, int)> items,List<string> itemsId,List<string> equippedIds)
        {
            InitializeComponents();
            if (items != null)
            {
                List<string> orderedKeys = new List<string>(items.Keys);
                orderedKeys.Sort((a, b) =>
                {
                    int.TryParse(a, out int ai);
                    int.TryParse(b, out int bi);
                    return ai.CompareTo(bi);
                });
                int equippedWeaponIndex= -1;
                int equippedAmmoIndex = -1;
                for (int idx = 0; idx < orderedKeys.Count; idx++)
                {
                    var itemEntry = items[orderedKeys[idx]];
                    string itemID = itemEntry.Item1;
                    int count = itemEntry.Item2;

                    if (count > 0)
                    {
                        Item newItem = PrefabManager.Instance.GetItemInstance(itemID,transform);
                        if(newItem==null)continue;
                        newItem.Initialize();
                        newItem.SetOnGroundStatus(false);
                        if (itemsId != null && idx < itemsId.Count)
                        {
                            newItem.NetworkId = itemsId[idx];
                        }
                        newItem.SetCount(count);
                        if (newItem != null)
                            {
                                if (newItem is Weapon newWeapon)
                                {
                                    newItem.transform.SetParent(weaponHolder);
                                    newItem.transform.localPosition = newWeapon.RightHandPosition;
                                    newItem.transform.localEulerAngles = newWeapon.RightHandRotation;
                                if (equippedIds.Contains(newItem.NetworkId)||equippedWeaponIndex<0)
                                    {
                                        equippedWeaponIndex = idx;
                                    }
                                }
                                else if (newItem is Ammo newAmmo)
                                {
                                if (equippedIds.Contains(newItem.NetworkId))
                                {
                                    equippedAmmoIndex = idx;
                                }
                                }                              
                                newItem.gameObject.SetActive(false);
                                _items.Add(newItem);
                            }
                    }
                }
                if (_health > 0)
                {
                    if (equippedWeaponIndex >= 0 && CurrentWeapon == null)
                    {
                        _weaponToEquip = (Weapon)_items[equippedWeaponIndex];
                        OnEquip();
                    }
                    if (equippedAmmoIndex >= 0)
                    {
                        _EquipAmmo((Ammo)_items[equippedAmmoIndex]);
                    }

                    if (CurrentAmmo != null && CurrentAmmo.Count > 0 && CurrentWeapon.AmmoCount < CurrentWeapon.ClipSize)
                    {
                        int count = CurrentWeapon.ClipSize - CurrentWeapon.AmmoCount;
                        if (CurrentAmmo.Count < count)
                        {
                            count = CurrentAmmo.Count;
                        }
                        CurrentAmmo.Count -= count;
                        CurrentWeapon.AmmoCount += count;
                    }
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
        public void EquipWeaponServerRpc(string networkID, ServerRpcParams serverRpcParams = default)
        {
            if (!InventoryOps.ValidateOwnerSender(serverRpcParams.Receive.SenderClientId, OwnerClientId, "Equip")) return;
            Item item = InventoryOps.FindItemByNetworkId(_items, networkID);
            if (item == null || item is Weapon == false)
            {
                InventoryOps.LogWarning($"Equip invalid weapon. netId={networkID}");
                return;
            }
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
        public void HolsterWeaponServerRpc(string networkID, ServerRpcParams serverRpcParams = default)
        {
            if (!InventoryOps.ValidateOwnerSender(serverRpcParams.Receive.SenderClientId, OwnerClientId, "Holster")) return;
            if (CurrentWeapon == null || CurrentWeapon.NetworkId != networkID)
            {
                InventoryOps.LogWarning($"Holster invalid weapon. netId={networkID}");
                return;
            }
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
        public void TakeDamage(Character shooter, Transform hit, float damage)
        {
            if (_health > 0&&damage>0)
            {
                if (hit == _animator.GetBoneTransform(HumanBodyBones.Head))
                {
                    damage *= 3f;
                }
                _health -= damage;
                if (_health <= 0)
                {
                    _networkObject.DontDestroyWithOwner = true;
                }
                HealthCheck();
                TakeDamageClientRpc(shooter.ClientID, ClientID, damage, _health);
            }
        }

        [ClientRpc]
        private void TakeDamageClientRpc(ulong shooter,ulong target,float damage,float remainedHealth)
        {
            _health = remainedHealth;
            HealthCheck();
        }
        private void HealthCheck()
        {
            if (_health <= 0)
            {
                _health = 0;
                if (_animator != null) _animator.enabled = false;
                if (GetComponent<RigBuilder>() != null) GetComponent<RigBuilder>().enabled = false;
                if (_tpc != null) _tpc.enabled = false;
                if (_controller != null) _controller.enabled = false;
                SetRagdollStatus(true);
                Destroy(_rigManager);
                Destroy(GetComponent<RigBuilder>());
                Destroy(_animator);
                Destroy(_tpc);
                Destroy(_controller);
                //Destroy(this);
                if(CurrentWeapon != null)
                {
                    _items.Remove(CurrentWeapon);
                    CurrentWeapon.transform.SetParent(null,true);
                    CurrentWeapon.SetOnGroundStatus(true);
                }

                ClientNetworkTransform networkTransform=GetComponent<ClientNetworkTransform>();
                if (networkTransform != null)
                {
                    networkTransform.SyncPositionX = false;
                    networkTransform.SyncPositionY = false;
                    networkTransform.SyncPositionZ = false;
                    networkTransform.SyncRotAngleX = false;
                    networkTransform.SyncRotAngleY = false;
                    networkTransform.SyncRotAngleZ = false;
                }
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
                if (_animator != null)
                {
                    _animator.SetBool("FreeFall", false);
                }
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
                    if (_animator != null)
                    {
                        _animator.SetBool("FreeFall", true);
                    }
                }

            }

        }
private bool _isPickingItem = false;
        public void PickupItem(string networkId)
        {
            if (_isPickingItem) return;
            if (string.IsNullOrEmpty(networkId))
            {
                InventoryOps.LogInfo("Pickup empty networkId.");
                return;
            }
            _isPickingItem = true;
            PickupItemServerRpc(networkId);
        }
        [ServerRpc]
        private void PickupItemServerRpc(string networkId,ServerRpcParams serverRpcParams = default)
        {
            if (!InventoryOps.ValidateOwnerSender(serverRpcParams.Receive.SenderClientId, OwnerClientId, "Pickup")) return;
            Item merge = null;
            Item targetItem = null;
            WorldRegistry.TryResolveItemByNetworkId(networkId, out targetItem);

            if (!InventoryOps.IsValidPickupRequest(targetItem, networkId))
            {
                InventoryOps.LogWarning($"Pickup invalid request. netId={networkId} clientId={serverRpcParams.Receive.SenderClientId}");
                SendPickupResult(false, networkId, "", 0, 0, serverRpcParams.Receive.SenderClientId);
                return;
            }

            merge = InventoryOps.TryResolvePickupMerge(_items, string.Empty, targetItem);
            int pickedCount = Mathf.Max(0, targetItem.GetCount());
            AddItemToInventoryLocally(targetItem, merge);
            int mergedCountAfter = merge != null ? Mathf.Max(0, merge.GetCount()) : 0;
            SendPickupResult(true, networkId, merge != null ? merge.NetworkId : "", pickedCount, mergedCountAfter, serverRpcParams.Receive.SenderClientId);
        }
        private void SendPickupResult(bool success, string networkId, string mergeNetworkId, int pickedCount, int mergedCountAfter, ulong targetClientId)
        {
            if (success)
            {
                InventoryOps.LogInfo($"Pickup success. netId={networkId} clientId={targetClientId}");
                PickupItemClientRpc(networkId, true, mergeNetworkId, pickedCount, mergedCountAfter);
                return;
            }
            ulong[] target = new ulong[1];
            target[0] = targetClientId;
            ClientRpcParams clientRpcParams = default;
            clientRpcParams.Send.TargetClientIds = target;
            PickupItemClientRpc(networkId, false, "", 0, 0, clientRpcParams);
        }

        private void NotifyDropResult(List<Managers.SessionManager.TransferRequest> droppedItems, List<Managers.SessionManager.SplitItemNetData> splitItems, ulong clientId)
        {
            if (droppedItems == null || droppedItems.Count == 0)
            {
                InventoryOps.LogInfo($"Drop no valid items dropped. clientId={clientId}");
                return;
            }
            DropItemsClientRpc(droppedItems.ToArray(), splitItems != null ? splitItems.ToArray() : new Managers.SessionManager.SplitItemNetData[0]);
            InventoryOps.LogInfo($"Drop success. clientId={clientId} count={droppedItems.Count}");
        }
        [ClientRpc]
        private void PickupItemClientRpc(string networkId,bool success,string mergeNetworkId, int pickedCount, int mergedCountAfter, ClientRpcParams clientRpcParams = default)
        {
            if (success)
            {
                Item targetItem = null;
                WorldRegistry.TryResolveItemByNetworkId(networkId, out targetItem);
                if (InventoryOps.IsWorldPickupCandidate(targetItem))
                {
                    Item merge = InventoryOps.TryResolvePickupMerge(_items, mergeNetworkId, targetItem);
                    AddItemToInventoryLocally(targetItem, merge);
                    if (merge != null && mergedCountAfter > 0)
                    {
                        merge.SetCount(mergedCountAfter);
                    }
                    else
                    {
                        Item added = InventoryOps.FindItemByNetworkId(_items, networkId);
                        if (added != null && pickedCount > 0)
                        {
                            added.SetCount(pickedCount);
                        }
                    }
                    _isPickingItem=false;
                    return;
                }
                Item existingItem = InventoryOps.FindItemByNetworkId(_items, networkId);
                if (existingItem != null)
                {
                    if (pickedCount > 0)
                    {
                        existingItem.SetCount(pickedCount);
                    }
                    _isPickingItem = false;
                    return;
                }
                InventoryOps.LogWarning($"Pickup failed on client. netId={networkId}");
            }
            _isPickingItem=false;
        }
        public void AddItemToInventoryLocally(Item item,Item merge=null)
        {
            if(item==null||_items.Contains(item))return;
            if (merge != null && _items.Contains(merge))
            {
                if(merge.GetType() ==item.GetType())
                {
                    merge.AddCount(item.GetCount());
                    Destroy(item.gameObject);
                }
                else
                {

                }
            }
            else
            {
                item.transform.SetParent(transform);
                item.Initialize();
                item.SetOnGroundStatus(false);

                if (item is Weapon weapon)
                {
                    item.transform.SetParent(weaponHolder);
                    item.transform.localPosition = weapon.RightHandPosition;
                    item.transform.localEulerAngles = weapon.RightHandRotation;
                }
                else if (item is Ammo ammo)
                {
                    if (CurrentAmmo == null && CurrentWeapon != null && CurrentWeapon.AmmoID == ammo.Id)
                    {
                        _EquipAmmo(ammo);
                    }
                }
                item.gameObject.SetActive(false);
                _items.Add(item);
            }        
        }

        public void RemoveItemFromInventoryLocally(Item item)
        {
            if (item == null || !_items.Contains(item))
            {
                return;
            }
            if (item == CurrentWeapon)
            {
                CurrentWeapon=null;
            }
            if (item == CurrentAmmo)
            {
                CurrentAmmo=null;
            }
            _items.Remove(item);
        }
        public void DropItem(Item item,int count)
        {
            if (item != null)
            {
                Dictionary<Item,int> items=new Dictionary<Item, int>();
                items.Add(item, count);
                DropItems(items);
            }
        }

        public void DropItems(Dictionary<Item,int> items)
        {
            List<Managers.SessionManager.TransferRequest> requests = InventoryOps.BuildTransferRequests(items, _items);
            if(requests.Count == 0)
            {
                InventoryOps.LogInfo("Drop empty request list.");
                return;
            }
            DropItemsServerRpc(requests.ToArray());
        }
        [ServerRpc]
        private void DropItemsServerRpc(Managers.SessionManager.TransferRequest[] items, ServerRpcParams serverRpcParams = default)
        {
            if (!InventoryOps.ValidateOwnerSender(serverRpcParams.Receive.SenderClientId, OwnerClientId, "Drop")) return;
            if (items == null || items.Length == 0)
            {
                InventoryOps.LogWarning($"Drop empty or null server request. clientId={serverRpcParams.Receive.SenderClientId}");
                return;
            }
            List<Managers.SessionManager.TransferRequest> droppedItems = new List<Managers.SessionManager.TransferRequest>();
            List<Managers.SessionManager.SplitItemNetData> splitItems = new List<Managers.SessionManager.SplitItemNetData>();
            foreach (var item in items)
            {
                if (!InventoryOps.TryGetValidItemForTransfer(_items, item.NetworkId, item.Count, out Item source))
                {
                    InventoryOps.LogWarning($"Drop invalid request. netId={item.NetworkId} count={item.Count} clientId={serverRpcParams.Receive.SenderClientId}");
                    continue;
                }
                if (!InventoryOps.IsOwnedByCharacter(source, this))
                {
                    InventoryOps.LogWarning($"Drop ownership mismatch. netId={item.NetworkId} itemId={source.Id} clientId={serverRpcParams.Receive.SenderClientId}");
                    continue;
                }
                if (!InventoryOps.TryTransferItemWithSource(
                        source,
                        _items,
                        item.Count,
                        transform,
                        out Item movedItem,
                        out _,
                        out Item splitItem,
                        out int movedCount))
                {
                    continue;
                }

                if (splitItem != null)
                {
                    AddItemToInventoryLocally(splitItem);
                    splitItems.Add(new Managers.SessionManager.SplitItemNetData
                    {
                        Id = splitItem.Id,
                        NetworkId = splitItem.NetworkId,
                        Count = splitItem.GetCount()
                    });
                }

                _DropItem(movedItem);
                droppedItems.Add(new Managers.SessionManager.TransferRequest
                {
                    NetworkId = item.NetworkId,
                    Count = movedCount
                });
            }
            NotifyDropResult(droppedItems, splitItems, serverRpcParams.Receive.SenderClientId);
        }
        [ClientRpc]
        private void DropItemsClientRpc(Managers.SessionManager.TransferRequest[] droppedItems, Managers.SessionManager.SplitItemNetData[] splitItems, ClientRpcParams clientRpcParams = default)
        {
            if (droppedItems == null) droppedItems = new Managers.SessionManager.TransferRequest[0];
            if (splitItems == null) splitItems = new Managers.SessionManager.SplitItemNetData[0];

            foreach(var item in droppedItems)
            {
                Item target = InventoryOps.FindItemByNetworkId(_items, item.NetworkId);
                if (target == null)
                {
                    InventoryOps.LogWarning($"Drop source item not found. netId={item.NetworkId}");
                    continue;
                }
                target.SetCount(item.Count);
                _DropItem(target);
            }
            foreach(var item in splitItems)
            {
                Item splitItem = PrefabManager.Instance.GetItemInstance(item.Id,transform);
                splitItem.NetworkId = item.NetworkId;
                splitItem.SetCount(item.Count);
                AddItemToInventoryLocally(splitItem);
            }
        }

        private void _DropItem(Item item)
        {
            if (!_items.Contains(item))
            {
                return;
            }
            if (item==CurrentWeapon)
            {
                CurrentWeapon = null;
            }
            if (item == CurrentAmmo)
            {
                CurrentAmmo = null;
            }
            item.transform.SetParent(null);
            item.SetOnGroundStatus(true);
            Vector3 offset = new Vector3(Random.Range(-0.1f, 0.1f), 0, Random.Range(-0.1f, 0.1f));
            item.transform.position=transform.position+transform.forward.normalized+Vector3.up+offset;
            item.transform.rotation=Quaternion.identity;
            item.gameObject.SetActive(true);
            _items.Remove(item);
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


