using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        private struct PendingMovePrediction
        {
            public uint Tick;
            public Vector3 Position;
        }

        private readonly List<PendingMovePrediction> _pendingMovePredictions = new List<PendingMovePrediction>();
        private bool _hasPendingReconcile;
        private Vector3 _reconcileTargetPosition;
        private Vector3 _reconcileVelocity;
        private float _reconcileSmoothTime;

        private void Move()
        {
            if (_input.run)
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
                targetSpeed = WalkSpeed;
            }

            if (_input.move == Vector2.zero)
            {
                targetSpeed = 0.0f;
                _character.SpeedAnimationMultiplier = 0;
            }

            _cameraManager.IsAiming = _character.IsAiming;
            _character.AimTarget = _cameraManager.AimTargetPoint;
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }
            _character.MoveSpeed = _input.move == Vector2.zero ? 0.0f : _character.SpeedAnimationMultiplier;

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                if (_rotateOnMove)
                {
                    transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
                }
            }

            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

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

                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
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
                _input.jump = false;
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

            PlayerMovementCommand cmd = new PlayerMovementCommand
            {
                Tick = ++_movementTick,
                ClientPosition = transform.position,
                MoveInput = _input.move,
                DeltaTime = Time.deltaTime,
                IsRunning = _character.IsRunning,
                IsSprinting = _character.IsSprinting,
                JumpPressed = _input.jump
            };

            _character.SubmitMovementCommandServerRpc(cmd);
            _pendingMovePredictions.Add(new PendingMovePrediction
            {
                Tick = cmd.Tick,
                Position = transform.position
            });

            if (_pendingMovePredictions.Count > 256)
            {
                _pendingMovePredictions.RemoveAt(0);
            }
        }

        public void HandleServerMovementAck(uint tick, Vector3 authoritativePosition, bool rejected, float normalSmoothTime, float rejectSmoothTime, float threshold)
        {
            int ackIndex = -1;
            for (int i = 0; i < _pendingMovePredictions.Count; i++)
            {
                if (_pendingMovePredictions[i].Tick == tick)
                {
                    ackIndex = i;
                    break;
                }
            }

            Vector3 replayedTarget = authoritativePosition;
            if (ackIndex >= 0)
            {
                for (int i = ackIndex + 1; i < _pendingMovePredictions.Count; i++)
                {
                    Vector3 previous = _pendingMovePredictions[i - 1].Position;
                    Vector3 current = _pendingMovePredictions[i].Position;
                    replayedTarget += current - previous;
                }

                _pendingMovePredictions.RemoveRange(0, ackIndex + 1);
            }
            else
            {
                _pendingMovePredictions.Clear();
            }

            if (Vector3.Distance(transform.position, replayedTarget) < threshold)
            {
                return;
            }

            _reconcileTargetPosition = replayedTarget;
            _reconcileSmoothTime = rejected ? rejectSmoothTime : normalSmoothTime;
            _hasPendingReconcile = true;
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

            if (Vector3.Distance(transform.position, _reconcileTargetPosition) <= 0.01f)
            {
                transform.position = _reconcileTargetPosition;
                _hasPendingReconcile = false;
                _reconcileVelocity = Vector3.zero;
            }
        }
    }
}
