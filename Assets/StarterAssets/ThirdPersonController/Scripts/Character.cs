using Gameplay.GameplayObjects.Items;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace StarterAssets
{
    public class Character : MonoBehaviour
    {
        [SerializeField] private Transform weaponHolder = null;
        private Weapon _currentWeapon = null; public Weapon CurrentWeapon { get => _currentWeapon; }
        private List<Item> _items = new List<Item>();

        private void Start()
        {
            Initialize(new Dictionary<string, int> { { "AK47", 1 } });
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
                                    newAmmo.Count = count;
                                    done = true;
                                }

                                newItem.gameObject.SetActive(false);
                                _items.Add(newItem);
                                if (done) break;
                            }
                        }
                    }
                }
                if (firstWeaponIndex >= 0 && _currentWeapon == null)
                {
                    EquipWeapon((Weapon)_items[firstWeaponIndex]);
                }
            }
        }
        private Transform _leftHandIKTarget = null;
        public void EquipWeapon(Weapon weapon)
        {
            if (_currentWeapon != null)
            {
                HolsterWeapon();
            }
            if (_leftHandIKTarget == null)
            {
                GameObject go = new GameObject("LeftHandIKTarget");
                _leftHandIKTarget = go.transform;
                // 将 IK 目标挂在 weaponHolder 下，以便它能被 RigManager 追踪
                _leftHandIKTarget.SetParent(weaponHolder);
            }

            if (weapon != null)
            {
                if (weapon.transform.parent != weaponHolder)
                {
                    weapon.transform.SetParent(weaponHolder);
                    weapon.transform.localPosition = weapon.RightHandPosition;
                    weapon.transform.localEulerAngles = weapon.RightHandRotation;
                }

                _leftHandIKTarget.SetParent(weapon.transform);

                _leftHandIKTarget.localPosition = weapon.LeftHandPosition;
                _leftHandIKTarget.localEulerAngles = weapon.LeftHandRotation;
                RigManager.Instance.SetLeftHandTarget(_leftHandIKTarget);

                _leftHandIKTarget.gameObject.SetActive(true);
                weapon.gameObject.SetActive(true);
                _currentWeapon = weapon;
            }
        }

        public void HolsterWeapon()
        {
            if (_currentWeapon != null)
            {
                _currentWeapon.gameObject.SetActive(false);
                _currentWeapon = null;
            }
        }

        public void TakeDamage(Character character, Transform hit, float damage)
        {
        }
    }

}

