using Gameplay.IFSM;
using UnityEngine;

namespace Gameplay.GameplayObjects.Character.PlayerState
{
    public abstract class PlayerActionStateBase : PlayerStateBase
    {
        protected virtual void StopMovement()
        {
            //Player.StopMove();
        }

        protected void UpdateAnimation(Vector2 moveInput)
        {
            Player.PlayerAnimator.UpdateMovement(moveInput);
        }
    }
}
