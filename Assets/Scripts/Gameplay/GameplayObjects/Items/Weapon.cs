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
        [SerializeField] private string ammoID = "";public string AmmoID { get => ammoID; }
        [SerializeField] private float damage = 1f;public float Damage { get => damage; }
        [SerializeField] private float fireRate = 0.2f;public float FireRate { get => fireRate; }
        [SerializeField] private int clipSize = 30;public int ClipSize { get => clipSize; }
        [SerializeField] private float handKick = 5f;public float HandKick { get => handKick; }
        [SerializeField] private float bodyKick = 5f;public float BodyKick { get => bodyKick; }
        [SerializeField] private Vector3 leftHandPosition=Vector3.zero;public Vector3 LeftHandPosition { get => leftHandPosition; }
        [SerializeField] private Vector3 leftHandRotation=Vector3.zero;public Vector3 LeftHandRotation { get => leftHandRotation; }
        [SerializeField] private Vector3 rightHandPosition=Vector3.zero;public Vector3 RightHandPosition { get => rightHandPosition; }
        [SerializeField] private Vector3 rightHandRotation=Vector3.zero;public Vector3 RightHandRotation { get => rightHandRotation; }
       
        [Header("References")]
        [SerializeField] private Transform muzzle = null;
        public Transform Muzzle { get => muzzle; }
        [SerializeField] private ParticleSystem flash = null;
        [SerializeField] private Projectile projectile = null;
        private float _fireTimer = 0f;
        private int _ammoCount = 0;public int AmmoCount { get => _ammoCount; set => _ammoCount = value; }

        protected override void Awake()
        {
            base.Awake();
            _fireTimer += Time.realtimeSinceStartup;
        }
        //private void Update()
        //{
        //    _fireTimer += Time.deltaTime;
        //}
        public bool Shoot(StarterAssets.Character character,Vector3 target, bool enableServerDamage = true)
        {
            if (projectile == null || muzzle == null)
            {
                return false;
            }
            float passedTime = Time.realtimeSinceStartup - _fireTimer;
            if(_ammoCount>0&&passedTime>=fireRate)
            {
                _ammoCount--;
                _fireTimer = Time.realtimeSinceStartup;
                Projectile newProjectile = Projectile.Spawn(projectile, muzzle.position, Quaternion.identity);
                if (newProjectile != null)
                {
                    newProjectile.Initialize(character, target, damage, enableServerDamage);
                }
                if (flash!=null)
                {
                    flash.Play();
                }
                return true;
            }
            return false;
        }

        public bool PlayRemoteShotFx(Vector3 target)
        {
            if (muzzle == null)
            {
                return false;
            }

            if (projectile != null)
            {
                Projectile visualProjectile = Projectile.Spawn(projectile, muzzle.position, Quaternion.identity);
                if (visualProjectile != null)
                {
                    visualProjectile.Initialize(null, target, 0f, false);
                }
            }

            if (flash != null)
            {
                flash.Play();
            }

            return true;
        }
    }

}
