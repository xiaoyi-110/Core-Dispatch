using Gameplay.GameplayObjects.Items;
using Managers;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
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
    public partial class ThirdPersonController : MonoBehaviour
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
        public float SprintCooldown = 1.0f; // 冲刺冷却（可选）

        //private bool _isSprinting = false;
        private float _sprintCooldownTimer = 0f;
        private bool _wasRunningBeforeSprint = false;


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
        private bool _loggedInitRole = false;
        private uint _movementTick = 0;
        private float _nextInputRouteLogTime = 0f;

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
            if (!_intialized || _character == null || !_character.IsOwner || !_character.IsSpawned)
            {
                return;
            }
            StabilizeInputRouting();
            HandleAim();
            HandleShooting();
            HandleReload();
            HandleSwitchWeapon();
            HandleSprint();
            HandleHoslterWeapon();
            ShowInventoryUI();
            ShowPickupUI();
            Move();
            SendMovementCommand();
        }

        private void StabilizeInputRouting()
        {
#if ENABLE_INPUT_SYSTEM
            if (_playerInput == null || _input == null)
            {
                return;
            }

            if (!Application.isFocused)
            {
                // Prevent stale input states when a client window loses focus.
                _input.move = Vector2.zero;
                _input.look = Vector2.zero;
                _input.jump = false;
                _input.shoot = false;
                _input.sprint = false;
                _input.run = false;
                return;
            }

            string scheme = _playerInput.currentControlScheme ?? string.Empty;
            if (scheme == "Xbox Controller")
            {
                bool hasGamepad = Gamepad.all.Count > 0;
                bool keyboardIntent = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
                bool mouseIntent = Mouse.current != null &&
                                   (Mouse.current.leftButton.wasPressedThisFrame ||
                                    Mouse.current.rightButton.wasPressedThisFrame ||
                                    Mouse.current.delta.ReadValue().sqrMagnitude > 0.0001f);

                if ((!hasGamepad || keyboardIntent || mouseIntent) &&
                    Keyboard.current != null &&
                    Mouse.current != null)
                {
                    _playerInput.SwitchCurrentControlScheme("KeyboardMouse", Keyboard.current, Mouse.current);
                    if (Time.time >= _nextInputRouteLogTime)
                    {
                        _nextInputRouteLogTime = Time.time + 1f;
                        Debug.Log($"[Input] Switched control scheme to KeyboardMouse (hasGamepad={hasGamepad}, keyboardIntent={keyboardIntent}, mouseIntent={mouseIntent})");
                    }
                }
            }
#endif
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
                    if (_character.IsSpawned)
                    {
                        bool shouldDestroyControllers = !_character.IsOwner && _character.IsClient && !_character.IsServer;
                        if (!_loggedInitRole)
                        {
                            _loggedInitRole = true;
                            Debug.Log($"[MoveInit] non-owner role client={_character.IsClient} server={_character.IsServer} destroyControllers={shouldDestroyControllers} netId={( _character.NetworkObject != null ? _character.NetworkObject.NetworkObjectId : 0UL)} ownerClientId={_character.OwnerClientId}");
                        }
                        // Only destroy local control components on non-owner client proxies.
                        if (shouldDestroyControllers)
                        {
                            DestroyControllers();
                        }
                    }
                    return;
                }
            }
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
                _input.switchWeapon = 0;
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

            bool hasMoveInput = _input.move != Vector2.zero;
            if (_character.IsAiming || !hasMoveInput)
            {
                if (_character.IsSprinting)
                {
                    _character.IsSprinting = false;
                    _sprintCooldownTimer = SprintCooldown;
                }
            }

            if (_input.sprint && !_character.IsSprinting && _sprintCooldownTimer <= 0f && !_character.IsAiming && hasMoveInput)
            {
                _character.IsSprinting = true;
                _wasRunningBeforeSprint = _character.IsRunning;
                _character.IsRunning = false;
            }

            if (_character.IsSprinting)
            {
                if (!_input.sprint)
                {
                    _character.IsSprinting = false;
                    if (_wasRunningBeforeSprint)
                    {
                        _character.IsRunning = true;
                        _wasRunningBeforeSprint = false;
                    }
                    _sprintCooldownTimer = SprintCooldown;
                    return;
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
