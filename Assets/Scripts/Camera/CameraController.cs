using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Camera
{
    public class CameraController:MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Vector3 offset;

        [SerializeField] private float speed = 2f;
        [SerializeField] private float minVertical = -20f;
        [SerializeField] private float maxVertical = 20f;
        [SerializeField] private Vector2 framingOffset;
        private float _rotationX;
        private float _rotationY;
        private Quaternion _planarRotation => Quaternion.Euler(0, _rotationY, 0);

        private void Start()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        private void Update()
        {
            _rotationX += Input.GetAxis("Mouse Y")*speed;
            _rotationX=Mathf.Clamp(_rotationX,minVertical,maxVertical);
            _rotationY += Input.GetAxis("Mouse X")*speed;
            
            var focusPosition = player.position + new Vector3(framingOffset.x,framingOffset.y);
            var targetRotation = Quaternion.Euler(_rotationX, _rotationY, 0);
            transform.position = focusPosition+targetRotation*offset;
            transform.rotation = targetRotation;
        }

        public Quaternion GetPlanarRotation()
        {
            return _planarRotation;
        }
    }
}
