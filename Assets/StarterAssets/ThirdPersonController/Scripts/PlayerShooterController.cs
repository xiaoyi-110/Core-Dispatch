using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace StarterAssets
{
    public class PlayerShooterController : MonoBehaviour
    {
        [SerializeField] private Transform bulletPrefab;
        [SerializeField] private Transform bulletSpawnPoint;
        private StarterAssetsInputs starterAssetsInputs;
        private ThirdPersonController thirdPersonController;
        private PlayerAnimation playerAnimation;
        private CharacterController characterController;

        private float _aimRigWeight = 0f;
        private float _leftHandWeight = 0f;

        private bool isArmed = false;
        private void Awake()
        {
            thirdPersonController = GetComponent<ThirdPersonController>();
            starterAssetsInputs = GetComponent<StarterAssetsInputs>();
            playerAnimation = GetComponent<PlayerAnimation>();
            characterController=GetComponent<CharacterController>();
        }
        
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.Q))
            {
                isArmed = !isArmed;
                playerAnimation.SetArmed(isArmed);
            }
            HandleAiming();
            HandleShooting();         
        }

        private void HandleAiming()
        {
            _aimRigWeight = Mathf.Lerp(_aimRigWeight, thirdPersonController.IsAiming && !thirdPersonController.IsReloading ? 1f : 0f, Time.deltaTime * 10f);
            _leftHandWeight=Mathf.Lerp(_leftHandWeight,(thirdPersonController.IsAiming||characterController.isGrounded) && !thirdPersonController.IsReloading ? 1f : 0f, Time.deltaTime * 10f);
            
            RigManager.Instance.AimWeight = _aimRigWeight;
            RigManager.Instance.LeftHandWeight = _leftHandWeight;
            RigManager.Instance.AimTarget = CameraManager.Instance.MouseWorldPosition;
            if (thirdPersonController.IsAiming&&isArmed)
            {
                CameraManager.Instance.IsAiming=true;
                thirdPersonController.SetSensitivity(CameraManager.Instance.AimSensitivity);
                thirdPersonController.SetRotateOnMove(false);
                playerAnimation.SetAimLayerWeight(1f);

                Vector3 worldAimTarget = CameraManager.Instance.MouseWorldPosition;
                worldAimTarget.y = transform.position.y;
                Vector3 aimDirection = (worldAimTarget - transform.position).normalized;

                transform.forward = Vector3.Lerp(transform.forward, aimDirection, Time.deltaTime * 20f);
            }
            else
            {
                CameraManager.Instance.IsAiming = false;
                thirdPersonController.SetSensitivity(CameraManager.Instance.NormalSensitivity);
                thirdPersonController.SetRotateOnMove(true);
                playerAnimation.SetAimLayerWeight(0f);
            }


        }

        private void HandleShooting()
        {
            if (starterAssetsInputs.shoot&&isArmed)
            {
                playerAnimation.TriggerShoot();
                Debug.Log("Shoot");
                starterAssetsInputs.shoot = false;
            }

            if (thirdPersonController.IsReloading&& isArmed)
            {
                playerAnimation.SetAimLayerWeight(1f);          
            }
            else
            {
                playerAnimation.SetAimLayerWeight(0f);
            }
        }
    }
    


}
