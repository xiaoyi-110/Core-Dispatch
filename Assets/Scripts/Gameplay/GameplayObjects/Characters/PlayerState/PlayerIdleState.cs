using Gameplay.IFSM;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.GameplayObjects.Characters.PlayerState
{
    public class PlayerIdleState:PlayerActionStateBase
    {
        public override void OnEnter(FSM<PlayerController> fsm)
        {
            //base.OnEnter(fsm);
            Debug.Log("Enter Idle State");
        }

        public override void OnUpdate(FSM<PlayerController> fsm)
        {

            if (fsm.Owner.ServerMoveInput.sqrMagnitude > 0.01f)
            {
                fsm.ChangeState<PlayerMovingState>();
            }
        }

        public override void OnExit(FSM<PlayerController> fsm)
        {
            //base.OnExit(fsm);
        }
    }
}