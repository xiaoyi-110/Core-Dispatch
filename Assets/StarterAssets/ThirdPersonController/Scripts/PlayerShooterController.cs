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
        private RigManager _rigManager;
        private CameraManager _cameraManager;   

        //private float _aimRigWeight = 0f;
        //private float _leftHandWeight = 0f;

        private void Awake()
        {
            _thirdPersonController = GetComponent<ThirdPersonController>();
            _inputs = GetComponent<StarterAssetsInputs>();
            _animation = GetComponent<PlayerAnimation>();
            _characterController=GetComponent<CharacterController>();
            _character = GetComponent<Character>();
            _cameraManager = FindObjectOfType<CameraManager>();
            _rigManager = GetComponent<RigManager>();
        }
        
        private void Update()
        {
            HandleAiming();
            HandleShooting();
        }

        private void HandleAiming()
        {
            if (_character.IsAiming&&_character.IsArmed)
            {
                _cameraManager.IsAiming=true;
                _thirdPersonController.SetSensitivity(_cameraManager.AimSensitivity);
                _thirdPersonController.SetRotateOnMove(false);
                _animation.SetAimLayerWeight(1f);

                Vector3 worldAimTarget = _cameraManager.MouseWorldPosition;
                worldAimTarget.y = transform.position.y;
                Vector3 aimDirection = (worldAimTarget - transform.position).normalized;

                transform.forward = Vector3.Lerp(transform.forward, aimDirection, Time.deltaTime * 20f);
            }
            else
            {
                _cameraManager.IsAiming = false;
                _thirdPersonController.SetSensitivity(_cameraManager.NormalSensitivity);
                _thirdPersonController.SetRotateOnMove(true);
                _animation.SetAimLayerWeight(0f);
            }


        }

        private void HandleShooting()
        {
            if (_inputs.shoot&&_character.IsArmed && _character.IsAiming&&!_character.IsReloading&&_character.CurrentWeapon.Shoot(_character,_cameraManager.MouseWorldPosition))
            {
                _rigManager.ApplyWeaponKick(_character.CurrentWeapon.HandKick, _character.CurrentWeapon.BodyKick);
                _animation.TriggerShoot();
                Debug.Log("Shoot");
                _inputs.shoot = false;
            }
        }
    }
}
