using System.Collections.Generic;
using Gameplay.GameplayObjects.Character.PlayerModules;
using Gameplay.GameplayObjects.Character.PlayerState;
using Gameplay.IFSM;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.GameplayObjects.Character
{
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed=5f;
        [SerializeField] private float rotateSpeed = 5f;

        private bool _useNetwork = false;

        private PlayerControls _playerControls;
        private InputAction moveAction;
        private CharacterController _characterController;        
        private FSM<PlayerController> _fsm;
        public PlayerAnimation PlayerAnimator;

        private LifeState _localLifeState = LifeState.Alive;
        public readonly NetworkVariable<LifeState> CurrentLifeState = new NetworkVariable<LifeState>(LifeState.Alive);
        public LifeState LifeStateValue
        {
            get => _useNetwork ? CurrentLifeState.Value : _localLifeState;
            set
            {
                if (_useNetwork) CurrentLifeState.Value = value;
                else _localLifeState = value;
            }
        }

        private bool IsServerSafe => _useNetwork ? IsServer : true;
        private bool IsOwnerSafe => _useNetwork ? IsOwner : true;

 
        private Quaternion _targetRotation;
        private Vector3 _lastSentInput = Vector3.zero;
        private Vector3 _serverMoveInput=Vector3.zero;
        public float Speed => moveSpeed;
        public Vector3 ServerMoveInput => _serverMoveInput;
        public override void OnNetworkSpawn()
        {
            if (!_useNetwork) return;

            CurrentLifeState.OnValueChanged += OnLifeStateChanged;
            if (IsOwner)
            {
                _playerControls.Enable();
            }            
            if (IsServer)
            {
                _fsm.ChangeState<PlayerAliveState>();
            }
        }
        
        public override void OnNetworkDespawn()
        {
            if (!_useNetwork) return;

            CurrentLifeState.OnValueChanged -= OnLifeStateChanged;

            if (IsOwner)
            {
                _playerControls.Disable();
            }
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            PlayerAnimator = GetComponentInChildren<PlayerAnimation>();
            _playerControls = new PlayerControls();
            moveAction = _playerControls.Player.Move;
            CreateLifeFSM();
        }

        private void CreateLifeFSM()
        {
            List<PlayerStateBase> stateList = new List<PlayerStateBase>()
            {
                new PlayerAliveState(),
                new PlayerDeadState(),
                new PlayerRevivingState(),
            };

            _fsm = new FSM<PlayerController>(this);

            foreach (var state in stateList)
            {
                _fsm.AddState(state);
            }
        }

        private void Start()
        {
            if (!_useNetwork)
            {
                _playerControls.Enable();
                _fsm.ChangeState<PlayerAliveState>();
            }
        }
        void Update()
        {
            if (IsOwnerSafe)
            {
                HandleClientInput();
            }

            if (IsServerSafe)
            {
                _fsm.Update();
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return; 

            _ExecuteServerMovement(_serverMoveInput);
        }

        [Rpc(SendTo.Server)]
        void SubmitMovementRequestRpc(Vector3 moveInput,RpcParams rpcParams = default)
        {
            if (CurrentLifeState.Value != LifeState.Alive)
            {
                _serverMoveInput = Vector3.zero;
                return;
            }
            _serverMoveInput = moveInput;
        }

        public void SetServerMoveInput(Vector3 direction)
        {
            if (!IsServer) return; // 只有服务器上的 FSM 才能设置
            _serverMoveInput = direction;
        }

        private void OnLifeStateChanged(LifeState previous, LifeState current)
        {
            // 如果不是服务器，就只管视觉表现
            if (!IsServer) 
            {
                // 玩家 B 看到：播放玩家 A 的死亡动画
                // PlayerAnimation.Play(current); 
            }
        }
        

        // 接口方法：提供给状态类调用
        public void StopMove()
        {
            if (!IsServerSafe) return;
            
            _StopServerMovement();
        }

        // 接口方法：供状态类调用发起权威状态转换
        public void RequestLifeStateChange(LifeState newState)
        {
            if (!IsServerSafe) return;
            
            _SetLifeState(newState);
        }
        
        private void _ExecuteServerMovement(Vector3 direction)
        {
            if (direction.magnitude > 0.01f)
            {
                Vector3 moveVector = direction * moveSpeed * Time.fixedDeltaTime;
                _characterController.Move(moveVector);

                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
                    rotateSpeed * Time.fixedDeltaTime);
            }
        }
        
        // 实际停止移动的私有方法
        private void _StopServerMovement()
        {
            _serverMoveInput = Vector3.zero;
        }

        // 实际设置 NetworkVariable 的私有方法
        private void _SetLifeState(LifeState newState)
        {
            if (CurrentLifeState.Value != newState)
            {
                CurrentLifeState.Value = newState;
                
                // 立即通知 FSM 切换状态 (保证权威状态与 FSM 内部状态同步)
                switch (newState)
                {
                    case LifeState.Alive:
                        _fsm.ChangeState<PlayerAliveState>();
                        break;
                    case LifeState.Dead:
                        _fsm.ChangeState<PlayerDeadState>();
                        break;
                    case LifeState.Reviving:
                        _fsm.ChangeState<PlayerRevivingState>();
                        break;
                }
            }
        }
        
        
        private void HandleClientInput()
        {
            Vector2 input2D=moveAction.ReadValue<Vector2>();
            float h = input2D.x;
            float v = input2D.y;

            Vector3 inputVector = new Vector3(h, 0, v);

            Vector3 normalizedInput = inputVector.magnitude > 0.01f ? inputVector.normalized : Vector3.zero;
            Vector3 moveDirection = Vector3.zero;


            if (normalizedInput.sqrMagnitude > 0.01f)
            {

                _targetRotation = Quaternion.LookRotation(moveDirection);
            }
            PlayerAnimator.UpdateMovement(new Vector2(normalizedInput.x, normalizedInput.z));
            if (CurrentLifeState.Value == LifeState.Alive)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, _targetRotation,
    rotateSpeed * Time.deltaTime);
                if (moveDirection.magnitude > 0.01f)
                {
                    Vector3 moveVector = moveDirection * moveSpeed * Time.deltaTime;
                    _characterController.Move(moveVector);
                }
            }


            if (_useNetwork)
            {
                if ((normalizedInput - _lastSentInput).sqrMagnitude > 0.0001f)
                {
                    SubmitMovementRequestRpc(normalizedInput);
                    _lastSentInput = inputVector;
                }
            }
            else
            {
                _serverMoveInput = inputVector;
            }
        }

        public void StartRevive(NetworkObject reviver)
        {
            if (!IsServerSafe) return;
            //if()
        }
        

    }
}
