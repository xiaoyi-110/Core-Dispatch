using Gameplay.GameplayObjects.Items;
using Managers;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;
using UnityEngine.Windows;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM 
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float WalkSpeed = 2.0f;
        public float RunSpeed = 4.0f;
        private float targetSpeed;
        //private bool _isRunning = false;
        //private float _speedAnimationMultiplier = 0;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;
        [Header("Sprint Settings")]
        public float SprintDuration = 1.0f; // 冲刺持续时间（秒）
        public float SprintCooldown = 1.0f; // 冲刺冷却（可选）

        //private bool _isSprinting = false;
        private float _sprintTimer = 0f;
        private float _sprintCooldownTimer = 0f;


        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;
        public float Sensitivity = 1.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        //[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        //public float FallTimeout = 0.15f;


        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        //private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        //private float _fallTimeoutDelta;

        //public bool IsAiming { get; private set; } = false;

        private PlayerInput _playerInput;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;
        private PlayerAnimation _playerAnimation;
        private Character _character;
        private CameraManager _cameraManager;

        private bool _rotateOnMove = true;
        private bool _intialized = false;

        private const float _threshold = 0.01f;
        //private Vector2 _aimedMovingAnimationInput=Vector2.zero;


        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            _playerAnimation = GetComponent<PlayerAnimation>();
            _character = GetComponent<Character>();
            _playerInput = GetComponent<PlayerInput>();
            _cameraManager = FindObjectOfType<CameraManager>();
        }

        private void Start()
        {           

        }

        private void DestroyControllers()
        {
            Destroy(this);
            Destroy(_playerInput);
            Destroy(_input);
            Destroy(_controller);
        }

        private void Update()
        {
            CheckInitialize();
            HandleAim();
            HandleShooting();
            HandleReload();
            HandleSwitchWeapon();
            HandleSprint();
            JumpAndGravity();
            HandleHoslterWeapon();
            ShowInventoryUI();
            ShowPickupUI();
            Move();
        }

        private void LateUpdate()
        {
            if (!_intialized)
            {
                return;
            }
            CameraRotation();
        }


        private void CheckInitialize()
        {
            if (!_intialized)
            {
                if (_character.IsOwner)
                {
                    _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
                    _mainCamera = _cameraManager.MainCamera.gameObject;
                    _cameraManager.PlayerVirtualCamera.Follow = CinemachineCameraTarget.transform;
                    _cameraManager.AimVirtualCamera.Follow = CinemachineCameraTarget.transform;
                    _jumpTimeoutDelta = JumpTimeout;
                    _intialized = true;
                }
                else
                {
                    if (_character.ClientID > 0)
                    {
                        DestroyControllers();
                    }
                    return;
                }
            }
        }
        private void CameraRotation()
        {
            Vector2 lookInput = _input.look;
            if (UIManager.Instance.IsInventoryOpen)
            {
                lookInput=Vector2.zero;
            }

            // if there is an input and camera position is not fixed
            if (lookInput.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += lookInput.x * deltaTimeMultiplier*Sensitivity;
                _cinemachineTargetPitch += lookInput.y * deltaTimeMultiplier*Sensitivity;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f);
        }

        private void HandleAim()
        {
            if (_input.aim&&_character.IsArmed)
            {
                _character.IsAiming = true;
            }
            else
            {
                _character.IsAiming = false;
            }
            if (_character.IsAiming && _character.IsArmed)
            {
                _cameraManager.IsAiming = true;
                SetSensitivity(_cameraManager.AimSensitivity);
                SetRotateOnMove(false);
                _playerAnimation.SetAimLayerWeight(1f);

                Vector3 worldAimTarget = _cameraManager.AimTargetPoint;
                worldAimTarget.y = transform.position.y;
                Vector3 aimDirection = (worldAimTarget - transform.position).normalized;

                transform.forward = Vector3.Lerp(transform.forward, aimDirection, Time.deltaTime * 20f);
            }
            else
            {
                _cameraManager.IsAiming = false;
                SetSensitivity(_cameraManager.NormalSensitivity);
                SetRotateOnMove(true);
                _playerAnimation.SetAimLayerWeight(0f);
            }
        }

        private void HandleShooting()
        {
            if (_input.shoot)// && _character.IsArmed && _character.IsAiming && !_character.IsReloading && _character.CurrentWeapon.Shoot(_character, _cameraManager.MouseWorldPosition))
            {
                Debug.Log("tps shoot");
                _character.Shoot();
                //_rigManager.ApplyWeaponKick(_character.CurrentWeapon.HandKick, _character.CurrentWeapon.BodyKick);
                //_playerAnimation.TriggerShoot();
                //Debug.Log("Shoot");
                _input.shoot = false;
            }
        }
        private void HandleReload()
        {
            if (_input.reload)
            {
                if (!_character.IsReloading&&!_character.IsSwitchingWeapon)
                {
                    _character.Reload();
                }
                _input.reload = false;
            }
        }

        private void HandleSwitchWeapon()
        {
            if (_input.switchWeapon!=0)
            {
                _character.SwitchWeapon(_input.switchWeapon);
                            
            }
        }

        private void HandleHoslterWeapon()
        {
            if (_input.holsterWeapon)
            {
                if (!_character.IsReloading && !_character.IsSwitchingWeapon)
                { _character.HolsterWeapon(); }
                _input.holsterWeapon = false;
            }
        }

        private void HandleSprint()
        {
            if (_sprintCooldownTimer > 0f)
                _sprintCooldownTimer -= Time.deltaTime;

            if (_input.sprint && !_character.IsSprinting && _sprintCooldownTimer <= 0f&&!_character.IsAiming)
            {
                _character.IsSprinting = true;
                _sprintTimer = SprintDuration;
            }

            if (_character.IsSprinting)
            {
                _sprintTimer -= Time.deltaTime;
                if (_sprintTimer <= 0f)
                {
                    _character.IsSprinting = false;
                    _sprintCooldownTimer = SprintCooldown; 
                }
            }
        }
        private void ShowInventoryUI()
        {
            if (_input.inventory)
            {
                if (UIManager.Instance.IsInventoryOpen)
                {
                    UIManager.Instance.CloseInventory();
                }
                else
                {
                    UIManager.Instance.OpenInventory();
                } 
                _input.inventory=false;
            }
        }
        private void ShowPickupUI()
        {
            float maxPickupDistance = 3f;
            Item itemToPick=null;
            Character characterToLoot = null;
            if (!UIManager.Instance.IsInventoryOpen&& CameraManager.Instance.AimTargetObject != null)
            {
                if (CameraManager.Instance.AimTargetObject.tag == "Item" && Vector3.Distance(CameraManager.Instance.AimTargetObject.position, transform.position) <= maxPickupDistance)
                {
                    itemToPick = CameraManager.Instance.AimTargetObject.GetComponent<Item>();
                    if (itemToPick!=null&&!itemToPick.CanBePickUp)
                    {
                        itemToPick = null;
                    }
                }else
                if (CameraManager.Instance.AimTargetObject.root.tag == "Character" && Vector3.Distance(CameraManager.Instance.AimTargetObject.position, transform.position) <= maxPickupDistance)
                {
                    characterToLoot = CameraManager.Instance.AimTargetObject.root.GetComponent<Character>();
                    if (characterToLoot != null && characterToLoot.Health > 0)
                    {
                        characterToLoot = null;
                    }
                }
            }

            if (UIManager.Instance.ItemToPick != itemToPick&&UIManager.Instance.CharacterToLoot==null)
            {
                UIManager.Instance.ItemToPick = itemToPick;
            }else if (UIManager.Instance.ItemToPick == null && UIManager.Instance.CharacterToLoot != characterToLoot)
            {
                UIManager.Instance.CharacterToLoot = characterToLoot;
            }
            if (_input.pickupItem)
            {
                if (UIManager.Instance.ItemToPick != null)
                {
                    _character.PickupItem(UIManager.Instance.ItemToPick.NetworkId);
                }else if (UIManager.Instance.CharacterToLoot != null)
                {
                    UIManager.Instance.OpenInventoryForLoot(UIManager.Instance.CharacterToLoot);
                }
                    _input.pickupItem = false;
            }
        }
        private void Move()
        {
            // set target speed based on move speed, sprint speed and if sprint is pressed
            if(_input.run)
            {
                _character.IsRunning = !_character.IsRunning;
                _input.run = false;
            }

            if (_character.IsSprinting)
            {
                targetSpeed = SprintSpeed;
            }
            else if (_character.IsRunning)
            {
                targetSpeed = RunSpeed;
            }
            else
            {
                targetSpeed =WalkSpeed;
            }

            if (_input.move == Vector2.zero)
            {
                targetSpeed = 0.0f;
                _character.SpeedAnimationMultiplier = 0;
            }

            //_character.IsGrounded=_controller.isGrounded;
            _cameraManager.IsAiming=_character.IsAiming;
            _character.AimTarget = _cameraManager.AimTargetPoint;
            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }
            _character.MoveSpeed=_input.move==Vector2.zero?0.0f : _character.SpeedAnimationMultiplier;
            //_animationBlend = Mathf.Lerp(_animationBlend, Time.deltaTime * SpeedChangeRate);
            //if (_animationBlend < 0.01f) _animationBlend = 0f;

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                if (_rotateOnMove)
                {
                    // rotate to face input direction relative to camera position
                    transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
                }
            }


            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            //// update animator if using character
            //_playerAnimation?.SetSpeed(_animationBlend);
            //_playerAnimation?.SetMotionSpeed(inputMagnitude);
        }

        private void JumpAndGravity()
        {
            if (_character.IsGrounded)
            {

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _jumpTimeoutDelta = JumpTimeout;
                    _character.Jump();
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                

                // if we are not grounded, do not jump
                _input.jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        //private void OnFootstep(AnimationEvent animationEvent)
        //{
        //    if (animationEvent.animatorClipInfo.weight > 0.5f)
        //    {
        //        if (FootstepAudioClips.Length > 0)
        //        {
        //            var index = Random.Range(0, FootstepAudioClips.Length);
        //            AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
        //        }
        //    }
        //}

        //private void OnLand(AnimationEvent animationEvent)
        //{
        //    if (animationEvent.animatorClipInfo.weight > 0.5f)
        //    {
        //        AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
        //    }
        //}

        public void SetSensitivity(float sensitivity)
        {
            Sensitivity = sensitivity;
        }

        public void SetRotateOnMove(bool rotateOnMove)
        {
            _rotateOnMove = rotateOnMove;
        }
    }
}