using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.GameplayObjects.Items
{
    public class Weapon : Item
    {
        public enum WeaponType 
        { 
            OneHanded,
            TwoHanded
        }
        [SerializeField] private WeaponType weaponType = WeaponType.TwoHanded;public WeaponType GetWeaponType { get=>weaponType; } 
        [SerializeField] private float damage = 1f;
        [SerializeField] private float fireRate = 0.2f;
        [SerializeField] private int clipSize = 30;
        [SerializeField] private Transform muzzle = null;
        [SerializeField] private Projectile projectile = null;
        [SerializeField] private float handKick = 5f;public float HandKick { get => handKick; }
        [SerializeField] private float bodyKick = 5f;public float BodyKick { get => bodyKick; }
        [SerializeField] private Vector3 leftHandPosition=Vector3.zero;public Vector3 LeftHandPosition { get => leftHandPosition; }
        [SerializeField] private Vector3 leftHandRotation=Vector3.zero;public Vector3 LeftHandRotation { get => leftHandRotation; }
        [SerializeField] private Vector3 rightHandPosition=Vector3.zero;public Vector3 RightHandPosition { get => rightHandPosition; }
        [SerializeField] private Vector3 rightHandRotation=Vector3.zero;public Vector3 RightHandRotation { get => rightHandRotation; }
    
        private float _fireTimer = 0f;

        private void Awake()
        {
            _fireTimer += Time.realtimeSinceStartup;
        }
        //private void Update()
        //{
        //    _fireTimer += Time.deltaTime;
        //}
        public bool CanShoot(StarterAssets.Character character,Vector3 target)
        {
            float passedTime = Time.realtimeSinceStartup - _fireTimer;
            if(passedTime>=fireRate)
            {
                _fireTimer = Time.realtimeSinceStartup;
                Projectile newProjectile = Instantiate(projectile, muzzle.position, Quaternion.identity);
                newProjectile.Initialize(character, target, damage);
                return true;
            }
            return false;
        }
        //public bool CanShoot(float lastShootTime)
        //{
        //    return Time.time >= lastShootTime + fireRate;
        //}
    }

}
