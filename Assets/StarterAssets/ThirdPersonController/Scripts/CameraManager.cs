using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
namespace StarterAssets
{
    public class CameraManager : MonoSingleton<CameraManager>
    {
        [SerializeField] private Camera mainCamera;public Camera MainCamera
        {
            get=>mainCamera;  
            private set=>mainCamera = value;
        }
        [SerializeField] private CinemachineVirtualCamera playerVirtualCamera;public CinemachineVirtualCamera PlayerVirtualCamera => playerVirtualCamera;
        [SerializeField] private CinemachineVirtualCamera aimVirtualCamera;public CinemachineVirtualCamera AimVirtualCamera => aimVirtualCamera;
        [SerializeField] private CinemachineBrain cameraBrain;
        [SerializeField] private float normalSensitivity = 1f; public float NormalSensitivity
        {
            get=>normalSensitivity;  
            private set=>normalSensitivity = value; 
        }
        [SerializeField] private float aimSensitivity = 0.2f;public float AimSensitivity
        {
            get=>aimSensitivity;   
            private set =>aimSensitivity = value; 
        }
        [SerializeField] private LayerMask aimColliderLayerMask = new LayerMask();
        public bool IsAiming { get; set; } = false;

        public Vector3 AimTargetPoint{get;private set;}
        private Transform _aimTargetObject=null;public Transform AimTargetObject { get => _aimTargetObject; }

        protected override void Awake()
        {
            base.Awake();
            cameraBrain.m_DefaultBlend.m_Time = 0.1f;
        }

        private void Update()
        {
            aimVirtualCamera.gameObject.SetActive(IsAiming);
            SetAimTarget();
        }

        private void SetAimTarget()
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Ray ray = Camera.main.ScreenPointToRay(screenCenter);
            if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, aimColliderLayerMask))
            {
                AimTargetPoint = raycastHit.point;
                _aimTargetObject = raycastHit.transform;
            }
            else
            {
                AimTargetPoint = ray.GetPoint(1000);
                _aimTargetObject = null;
            }
        }
    }
}
    
