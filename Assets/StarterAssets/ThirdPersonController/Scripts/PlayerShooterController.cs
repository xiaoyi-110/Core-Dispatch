using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using Gameplay.GameplayObjects.Items;
namespace StarterAssets
{
    public class PlayerShooterController : MonoBehaviour
    {
        [SerializeField] private Transform bulletPrefab;
        [SerializeField] private Transform bulletSpawnPoint;
        private StarterAssetsInputs _inputs;
        private ThirdPersonController _thirdPersonController;
        private PlayerAnimation _animation;
        private CharacterController _characterController;
        private Character _character;

        private float _aimRigWeight = 0f;
        private float _leftHandWeight = 0f;

        public bool IsArmed { get; private set; } = false;
        private void Awake()
        {
            _thirdPersonController = GetComponent<ThirdPersonController>();
            _inputs = GetComponent<StarterAssetsInputs>();
            _animation = GetComponent<PlayerAnimation>();
            _characterController=GetComponent<CharacterController>();
            _character = GetComponent<Character>();
        }
        
        private void Update()
        {
            //if(Input.GetKeyDown(KeyCode.Q))
            //{
            //    isArmed = !isArmed;
            //    playerAnimation.SetArmed(isArmed);
            //}
            IsArmed = _character.CurrentWeapon != null;
            _animation.SetArmed(IsArmed);
            HandleAiming();
            HandleShooting();
            HandleReloading();
        }

        private void HandleAiming()
        {
            _aimRigWeight = Mathf.Lerp(_aimRigWeight, IsArmed&&_thirdPersonController.IsAiming && !_thirdPersonController.IsReloading ? 1f : 0f, Time.deltaTime * 10f);
            _leftHandWeight=Mathf.Lerp(_leftHandWeight,IsArmed&&(_thirdPersonController.IsAiming||(_characterController.isGrounded)&&_character.CurrentWeapon.GetWeaponType == Weapon.WeaponType.TwoHanded ) && !_thirdPersonController.IsReloading ? 1f : 0f, Time.deltaTime * 10f);
            
            RigManager.Instance.AimWeight = _aimRigWeight;
            RigManager.Instance.LeftHandWeight = _leftHandWeight;
            RigManager.Instance.AimTarget = CameraManager.Instance.MouseWorldPosition;
            if (_thirdPersonController.IsAiming&&IsArmed)
            {
                CameraManager.Instance.IsAiming=true;
                _thirdPersonController.SetSensitivity(CameraManager.Instance.AimSensitivity);
                _thirdPersonController.SetRotateOnMove(false);
                _animation.SetAimLayerWeight(1f);

                Vector3 worldAimTarget = CameraManager.Instance.MouseWorldPosition;
                worldAimTarget.y = transform.position.y;
                Vector3 aimDirection = (worldAimTarget - transform.position).normalized;

                transform.forward = Vector3.Lerp(transform.forward, aimDirection, Time.deltaTime * 20f);
            }
            else
            {
                CameraManager.Instance.IsAiming = false;
                _thirdPersonController.SetSensitivity(CameraManager.Instance.NormalSensitivity);
                _thirdPersonController.SetRotateOnMove(true);
                _animation.SetAimLayerWeight(0f);
            }


        }

        private void HandleShooting()
        {
            if (_inputs.shoot&&IsArmed&& _thirdPersonController.IsAiming&&!_thirdPersonController.IsReloading&&_character.CurrentWeapon.CanShoot(_character,CameraManager.Instance.MouseWorldPosition))
            {
                RigManager.Instance.ApplyWeaponKick(_character.CurrentWeapon.HandKick, _character.CurrentWeapon.BodyKick);
                _animation.TriggerShoot();
                Debug.Log("Shoot");
                _inputs.shoot = false;
            }
        }

        private void HandleReloading()
        {
            if (_thirdPersonController.IsReloading && IsArmed)
            {
                _animation.SetAimLayerWeight(1f);
            }
            else
            {
                _animation.SetAimLayerWeight(0f);
            }
        }
    }
    


}
