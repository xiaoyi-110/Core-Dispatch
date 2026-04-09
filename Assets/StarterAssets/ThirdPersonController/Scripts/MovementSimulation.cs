using UnityEngine;

namespace StarterAssets
{
    internal static class MovementSimulation
    {
        public static void SimulateCharacterControllerMove(
            CharacterController controller,
            Transform transform,
            Vector2 moveInput,
            bool isSprinting,
            bool isRunning,
            bool jumpPressed,
            bool analogMovement,
            float deltaTime,
            float walkSpeed,
            float runSpeed,
            float sprintSpeed,
            float speedChangeRate,
            float gravity,
            float jumpHeight,
            float jumpTimeout,
            float groundedOffset,
            float groundedRadius,
            LayerMask groundLayers,
            float terminalVelocity,
            ref float horizontalSpeed,
            ref float verticalVelocity,
            ref float jumpTimeoutDelta,
            float facingYaw,
            float moveYaw)
        {
            if (controller == null || transform == null)
            {
                return;
            }

            float clampedDeltaTime = Mathf.Clamp(deltaTime, 0.001f, 0.05f);
            float targetSpeed = 0f;
            if (moveInput != Vector2.zero)
            {
                if (isSprinting)
                {
                    targetSpeed = sprintSpeed;
                }
                else if (isRunning)
                {
                    targetSpeed = runSpeed;
                }
                else
                {
                    targetSpeed = walkSpeed;
                }
            }

            float inputMagnitude = analogMovement
                ? Mathf.Clamp01(moveInput.magnitude)
                : (moveInput == Vector2.zero ? 0f : 1f);
            float desiredSpeed = targetSpeed * inputMagnitude;

            float speedOffset = 0.1f;
            if (horizontalSpeed < desiredSpeed - speedOffset ||
                horizontalSpeed > desiredSpeed + speedOffset)
            {
                horizontalSpeed = Mathf.Lerp(horizontalSpeed, desiredSpeed, clampedDeltaTime * speedChangeRate);
                horizontalSpeed = Mathf.Round(horizontalSpeed * 1000f) / 1000f;
            }
            else
            {
                horizontalSpeed = desiredSpeed;
            }

            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
            bool grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);

            if (grounded)
            {
                if (verticalVelocity < 0.0f)
                {
                    verticalVelocity = -2f;
                }

                if (jumpPressed && jumpTimeoutDelta <= 0.0f)
                {
                    jumpTimeoutDelta = jumpTimeout;
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }

                if (jumpTimeoutDelta >= 0.0f)
                {
                    jumpTimeoutDelta -= clampedDeltaTime;
                }
            }
            else
            {
                jumpTimeoutDelta = jumpTimeout;
            }

            if (verticalVelocity < terminalVelocity)
            {
                verticalVelocity += gravity * clampedDeltaTime;
            }

            transform.rotation = Quaternion.Euler(0.0f, facingYaw, 0.0f);

            Vector3 moveDirection = Vector3.zero;
            if (moveInput != Vector2.zero)
            {
                moveDirection = Quaternion.Euler(0.0f, moveYaw, 0.0f) * Vector3.forward;
            }

            Vector3 move = moveDirection.normalized * (horizontalSpeed * clampedDeltaTime) +
                           new Vector3(0.0f, verticalVelocity, 0.0f) * clampedDeltaTime;
            controller.Move(move);
        }
    }
}
