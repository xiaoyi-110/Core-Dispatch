using Gameplay.IFSM;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.GameplayObjects.Character.PlayerState
{
    public class PlayerMovingState: PlayerActionStateBase
    {
        public override void OnEnter(FSM<PlayerController> fsm)
        {
            Debug.Log("Enter Moving State");
        }

        public override void OnUpdate(FSM<PlayerController> fsm)
        {
            // 如果没有输入则切回 Idle
            if (fsm.Owner.ServerMoveInput.sqrMagnitude < 0.01f)
            {
                fsm.ChangeState<PlayerIdleState>();
            }
            else
            {
                //UpdateAnimation(new Vector2(Player.ServerMoveInput.x, Player.ServerMoveInput.z));
            }
        }

        public override void OnExit(FSM<PlayerController> fsm) { }
    }
}