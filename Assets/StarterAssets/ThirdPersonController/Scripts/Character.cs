using Gameplay.GameplayObjects.Characters.PlayerModules;
using Gameplay.GameplayObjects.Items;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.TextCore.Text;
using UnityEngine.Windows;
using static UnityEngine.ParticleSystem;

namespace StarterAssets
{
    public class Character : MonoBehaviour
    {
        public bool IsLocalPlayer = false;
        [SerializeField] private string id = "";public string Id { get => id; }
        [SerializeField] private Transform weaponHolder = null;
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
        private PlayerShooterController _psc = null;
        private CharacterController _controller = null;

        private bool _grounded = false;public bool IsGrounded { get => _grounded; set => _grounded = value; }
        private bool _isRunning = false;public bool IsRunning { get => _isRunning; set => _isRunning = value; }
        private bool _isSprinting = false;public bool IsSprinting { get => _isSprinting; set => _isSprinting = value; }
        private bool _isAiming = false; public bool IsAiming { get=>_isAiming; set=>_isAiming=value; } 

        private bool _isArmed = false;public bool IsArmed { get => _isArmed; set => _isArmed = value; }
        private float _speedAnimationMultiplier = 0;public float SpeedAnimationMultiplier { get => _speedAnimationMultiplier; set => _speedAnimationMultiplier = value; }
        private float _aimLayerWeight = 0f;public float AimLayerWeight { get => _aimLayerWeight; }
        private float _aimRigWeight = 0f;
        private float _leftHandWeight = 0f;
        private Vector3 _lastPositon=Vector3.zero;
        private Vector2 _aimedMovingAnimationInput = Vector2.zero;
        private Vector3 _aimTarget=Vector3.zero;public Vector3 AimTarget { get => _aimTarget; set => _aimTarget = value; }
        
        private float _health = 100f;

        private void Awake()
        {
            _ragdollColliders = GetComponentsInChildren<Collider>();
            _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
            _tpc= GetComponent<ThirdPersonController>();
            _psc= GetComponent<PlayerShooterController>();
            _controller= GetComponent<CharacterController>();
            if (_ragdollColliders!=null)
            {
                for(int i=0;i<_ragdollColliders.Length;i++)
                {
                    _ragdollColliders[i].isTrigger = false;
                }
            }
            if (_ragdollRigidbodies != null)
            {
                for(int i=0;i<_ragdollRigidbodies.Length;i++)
                {
                    _ragdollRigidbodies[i].mass *= 50;
                }
            }
            SetRagdollStatus(false);
            _playerAnimation = GetComponent<PlayerAnimation>();
            _rigManager = GetComponent<RigManager>();
            
        }
        private void Start()
        {
            Initialize(new Dictionary<string, int> { { "AK47", 1 }, { "MP5", 1 }, { "7.62x39mm", 300 } });
            if (IsLocalPlayer)
                SetLayer(transform, LayerMask.NameToLayer("LocalPlayer"));
            else
                SetLayer(transform, LayerMask.NameToLayer("NetworkPlayer"));
        }

        private void Update()
        {
            IsArmed = CurrentWeapon != null;
            _playerAnimation.SetArmed(_isArmed);
            _playerAnimation.SetAim(_isAiming);
            float targetWeight = IsSwitchingWeapon || (_isArmed && (_isAiming || IsReloading)) ? 1f : 0f;

            _playerAnimation.SetAimLayerWeight(targetWeight);
            _aimRigWeight = Mathf.Lerp(_aimRigWeight, IsArmed &&IsAiming && !IsReloading ? 1f : 0f, Time.deltaTime * 10f);
            _leftHandWeight = Mathf.Lerp(_leftHandWeight, IsArmed && !IsSwitchingWeapon && (_isAiming || _grounded&& CurrentWeapon.GetWeaponType == Weapon.WeaponType.TwoHanded) && !IsReloading ? 1f : 0f, Time.deltaTime * 10f);

            _rigManager.AimWeight = _aimRigWeight;
            _rigManager.LeftHandWeight = _leftHandWeight;
            _rigManager.AimTarget = _aimTarget;

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

            Vector3 deltaPosition=transform.InverseTransformDirection(transform.position - _lastPositon).normalized;
            _aimedMovingAnimationInput = Vector2.Lerp(_aimedMovingAnimationInput,new Vector2(deltaPosition.x,deltaPosition.z)*_speedAnimationMultiplier, Time.deltaTime * 10f);
            _playerAnimation.SetAimMoveSpeed(_aimedMovingAnimationInput.x, _aimedMovingAnimationInput.y);
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
        public void Initialize(Dictionary<string, int> items)
        {
            if (items != null)
            {
                int firstWeaponIndex = -1;
                foreach (var itemEntry in items) // 仍然使用 foreach，但调用 PrefabManager 实例化
                {
                    string itemID = itemEntry.Key;
                    int count = itemEntry.Value;

                    if (count > 0)
                    {
                        Item prefab = PrefabManager.Instance.GetItemPrefab(itemID);
                        if (prefab == null) continue;

                        for (int i = 0; i < count; i++)
                        {
                            bool done = false;
                            Item newItem = PrefabManager.Instance.GetItemInstance(itemID);

                            if (newItem != null)
                            {
                                if (newItem is Weapon newWeapon)
                                {
                                    newItem.transform.SetParent(weaponHolder);
                                    newItem.transform.localPosition = newWeapon.RightHandPosition;
                                    newItem.transform.localEulerAngles = newWeapon.RightHandRotation;

                                    if (firstWeaponIndex < 0)
                                    {
                                        firstWeaponIndex = _items.Count;
                                    }
                                }
                                else if (newItem is Ammo newAmmo)
                                {
                                    newItem.Count = count;
                                    done = true;
                                }                              
                                newItem.gameObject.SetActive(false);
                                _items.Add(newItem);
                                if (done) break;
                            }
                        }
                    }
                }
                if (firstWeaponIndex >= 0 && CurrentWeapon == null)
                {
                    _weaponToEquip=(Weapon)_items[firstWeaponIndex];
                    OnEquip();
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
                CurrentWeapon = CurrentWeapon;
                CurrentAmmo = null;
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i] is Ammo ammo && ammo.Id == CurrentWeapon.AmmoID)
                    {
                        CurrentAmmo = ammo;
                        break;
                    }

                }
        }
        public void EquipWeapon(Weapon weapon)
        {
            if (IsSwitchingWeapon||weapon==null)
            {
                return;
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
                IsSwitchingWeapon = true;
                _playerAnimation.TriggerHolster();
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
                    Destroy(_psc);
                    Destroy(_controller);
                    Destroy(this);
                }
            }
        }

        public void Reload()
        {
            if (CurrentWeapon != null&& CurrentWeapon.AmmoCount < CurrentWeapon.ClipSize && CurrentAmmo != null && CurrentAmmo.Count > 0)
            {
                IsReloading = true;
                //_playerAnimation.SetAimLayerWeight(1f);
                _playerAnimation.TriggerReload();
                Debug.Log("Reloading...");
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

    }
}

