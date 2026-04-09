using System.Collections.Generic;
using Unity.Netcode;
using NetcodeDiagnostics;
using UnityEngine;
using Utility;

namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        private struct PendingMovePrediction
        {
            public uint Tick;
            public PlayerMovementCommand Command;
        }

        private readonly List<PendingMovePrediction> _pendingMovePredictions = new List<PendingMovePrediction>();
        private bool _hasPendingReconcile;
        private Vector3 _reconcileTargetPosition;
        private Vector3 _reconcileVelocity;
        private float _reconcileSmoothTime;
        private Quaternion _reconcileTargetRotation = Quaternion.identity;
        private PlayerMovementCommand _pendingCommand;
        private bool _hasPendingCommand;
        private float _nextMoveClientSampleTime;
        private uint _lastAckTick;
        private const int MaxPendingMoves = 128;
        private bool _jumpHeld;
        private bool _jumpPressedThisFrame;

        private void Move()
        {
            if (!Application.isFocused)
            {
                _jumpHeld = false;
                _jumpPressedThisFrame = false;
                if (_input != null)
                {
                    _input.jump = false;
                }
            }

            bool rawJumpPressed = _input != null && _input.jump;
            _jumpPressedThisFrame = rawJumpPressed && !_jumpHeld;
            _jumpHeld = rawJumpPressed;

            if (_input.run && !_character.IsSprinting)
            {
                _character.IsRunning = !_character.IsRunning;
                _input.run = false;
            }

            if (_character.IsAiming)
            {
                _character.IsRunning = false;
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
                targetSpeed = WalkSpeed;
            }

            if (_input.move == Vector2.zero)
            {
                targetSpeed = 0.0f;
                _character.SpeedAnimationMultiplier = 0;
            }

            _cameraManager.IsAiming = _character.IsAiming;
            _character.AimTarget = _cameraManager.AimTargetPoint;

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            float smoothedYaw = transform.eulerAngles.y;
            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                smoothedYaw = rotation;
            }
            else
            {
                _targetRotation = transform.eulerAngles.y;
                smoothedYaw = transform.eulerAngles.y;
            }

            PlayerMovementCommand cmd = new PlayerMovementCommand
            {
                Tick = ++_movementTick,
                MoveInput = _input.move,
                DeltaTime = Time.deltaTime,
                IsRunning = _character.IsRunning,
                IsSprinting = _character.IsSprinting,
                JumpPressed = _jumpPressedThisFrame,
                ClientTime = NetworkManager.Singleton != null ? (float)NetworkManager.Singleton.LocalTime.Time : Time.time,
                FacingYaw = smoothedYaw,
                MoveYaw = _input.move != Vector2.zero ? _targetRotation : smoothedYaw,
                AnalogMovement = _input.analogMovement
            };

            // Keep jump animation/RPC in sync with the movement jump command.
            if (cmd.JumpPressed && _character.IsGrounded && _jumpTimeoutDelta <= 0.0f)
            {
                _character.Jump();
            }

            // Consume jump as a one-shot command; this prevents sticky button states
            // (especially when focus/device switches) from retriggering every tick.
            if (_jumpPressedThisFrame && _input != null)
            {
                _input.jump = false;
                _jumpHeld = false;
            }

            SimulateMoveFromCommand(cmd);
            _pendingCommand = cmd;
            _hasPendingCommand = true;
            _character.MoveSpeed = _input.move == Vector2.zero ? 0.0f : _character.SpeedAnimationMultiplier;

            ApplyOwnerReconciliation();
        }

        private void JumpAndGravity()
        {
            if (_character.IsGrounded)
            {
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                if (_jumpPressedThisFrame && _jumpTimeoutDelta <= 0.0f)
                {
                    _jumpTimeoutDelta = JumpTimeout;
                    _character.Jump();
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;
            }

            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private void SendMovementCommand()
        {
            if (_character == null || !_character.IsOwner || !_character.IsSpawned)
            {
                return;
            }
            if (!_hasPendingCommand)
            {
                return;
            }

            PlayerMovementCommand cmd = _pendingCommand;

            _character.SubmitMovementCommandServerRpc(cmd);
            _pendingMovePredictions.Add(new PendingMovePrediction
            {
                Tick = cmd.Tick,
                Command = cmd
            });
            _hasPendingCommand = false;

            if (Time.time >= _nextMoveClientSampleTime)
            {
                _nextMoveClientSampleTime = Time.time + 0.25f;
                string scheme = "";
#if ENABLE_INPUT_SYSTEM
                scheme = _playerInput != null ? _playerInput.currentControlScheme : "";
#endif
                bool controllerReady = _controller != null && _controller.enabled;
                string sendMsg = $"[Move] CmdSent tick={cmd.Tick} pendingCount={_pendingMovePredictions.Count} pos=({transform.position.x:0.000},{transform.position.y:0.000},{transform.position.z:0.000}) speed={_speed:0.000} jumpTimeout={_jumpTimeoutDelta:0.000} jumpPressed={cmd.JumpPressed} input=({cmd.MoveInput.x:0.000},{cmd.MoveInput.y:0.000}) facingYaw={cmd.FacingYaw:0.0} moveYaw={cmd.MoveYaw:0.0} analog={cmd.AnalogMovement} owner={(_character != null && _character.IsOwner)} spawned={(_character != null && _character.IsSpawned)} ctrlEnabled={controllerReady} focus={Application.isFocused} playerInputEnabled={(_playerInput != null && _playerInput.enabled)} scheme={scheme}";
                NetLog.Write(sendMsg);
            }

            if (_pendingMovePredictions.Count > MaxPendingMoves)
            {
                _pendingMovePredictions.RemoveAt(0);
            }
        }

        public void HandleServerMovementAck(uint tick, Vector3 authoritativePosition, Quaternion authoritativeRotation, float authoritativeVerticalVelocity, float authoritativeSpeed, float authoritativeJumpTimeoutDelta, bool rejected, float normalSmoothTime, float rejectSmoothTime, float threshold)
        {
            if (tick <= _lastAckTick)
            {
                return;
            }

            int ackIndex = -1;
            for (int i = 0; i < _pendingMovePredictions.Count; i++)
            {
                if (_pendingMovePredictions[i].Tick == tick)
                {
                    ackIndex = i;
                    break;
                }
            }

            if (ackIndex < 0)
            {
                string missMsg = $"[Move] AckMissing tick={tick} pendingCount={_pendingMovePredictions.Count}";
                NetLog.Write(missMsg);
                // Soft resync to authoritative state when pending queue no longer matches.
                SnapToAuthoritative(authoritativePosition, authoritativeRotation);
                _verticalVelocity = authoritativeVerticalVelocity;
                _speed = authoritativeSpeed;
                _jumpTimeoutDelta = authoritativeJumpTimeoutDelta;
                _pendingMovePredictions.Clear();
                _hasPendingReconcile = false;
                _lastAckTick = tick;
                return;
            }

            float correctionDistance = Vector3.Distance(transform.position, authoritativePosition);
            if (rejected || correctionDistance >= threshold)
            {
                string msg = $"[Move] Ack tick={tick} rejected={rejected} correction={correctionDistance:0.000} threshold={threshold:0.000} pendingCount={_pendingMovePredictions.Count} ackIndex={ackIndex} speed={_speed:0.000} jumpTimeout={_jumpTimeoutDelta:0.000} authSpeed={authoritativeSpeed:0.000} authJumpTimeout={authoritativeJumpTimeoutDelta:0.000}";
                NetLog.Write(msg);
            }

            if (correctionDistance < threshold && !rejected)
            {
                _speed = authoritativeSpeed;
                _jumpTimeoutDelta = authoritativeJumpTimeoutDelta;
                _pendingMovePredictions.RemoveRange(0, ackIndex + 1);
                _lastAckTick = tick;
                return;
            }

            NetworkDiagnostics.RecordMoveCorrection(correctionDistance, rejected);

            // Roll back to server state.
            SnapToAuthoritative(authoritativePosition, authoritativeRotation);
            _verticalVelocity = authoritativeVerticalVelocity;
            _speed = authoritativeSpeed;
            _jumpTimeoutDelta = authoritativeJumpTimeoutDelta;

            // Re-simulate pending inputs after the acknowledged tick.
            for (int i = ackIndex + 1; i < _pendingMovePredictions.Count; i++)
            {
                SimulateMoveFromCommand(_pendingMovePredictions[i].Command);
            }

            _pendingMovePredictions.RemoveRange(0, ackIndex + 1);
            _hasPendingReconcile = false;
            _lastAckTick = tick;
        }

        private void SnapToAuthoritative(Vector3 position, Quaternion rotation)
        {
            // CharacterController can depenetrate immediately after direct transform assignment.
            // Temporarily disable it so authoritative rewinds apply exactly.
            bool restoreController = _controller != null && _controller.enabled;
            if (restoreController)
            {
                _controller.enabled = false;
            }

            transform.position = position;
            transform.rotation = rotation;

            if (restoreController)
            {
                _controller.enabled = true;
            }
        }

        private void ApplyOwnerReconciliation()
        {
            if (!_hasPendingReconcile)
            {
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                _reconcileTargetPosition,
                ref _reconcileVelocity,
                _reconcileSmoothTime);

            transform.rotation = Quaternion.Slerp(transform.rotation, _reconcileTargetRotation, Time.deltaTime * (1f / Mathf.Max(0.001f, _reconcileSmoothTime)));

            if (Vector3.Distance(transform.position, _reconcileTargetPosition) <= 0.01f)
            {
                transform.position = _reconcileTargetPosition;
                _hasPendingReconcile = false;
                _reconcileVelocity = Vector3.zero;
            }
        }

        private void SimulateMoveFromCommand(PlayerMovementCommand cmd)
        {
            if (_controller == null || _character == null)
            {
                return;
            }

            MovementSimulation.SimulateCharacterControllerMove(
                _controller,
                transform,
                cmd.MoveInput,
                cmd.IsSprinting,
                cmd.IsRunning,
                cmd.JumpPressed,
                cmd.AnalogMovement,
                cmd.DeltaTime,
                WalkSpeed,
                RunSpeed,
                SprintSpeed,
                SpeedChangeRate,
                Gravity,
                JumpHeight,
                JumpTimeout,
                _character.GroundedOffset,
                _character.GroundedRadius,
                _character.GroundLayers,
                _terminalVelocity,
                ref _speed,
                ref _verticalVelocity,
                ref _jumpTimeoutDelta,
                cmd.FacingYaw,
                cmd.MoveYaw);
        }
    }
}


