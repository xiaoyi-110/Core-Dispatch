using System.Collections;
using System.Collections.Generic;
using Gameplay.GameplayObjects.character;
using Gameplay.GameplayObjects.character.PlayerState;
using Gameplay.IFSM;
using UnityEngine;

namespace Gameplay.GameplayObjects.Character.PlayerState
{
    public class PlayerAliveState : PlayerLifeStateBase
    {
        private FSM<PlayerController> _actionFSM;

        public override void OnEnter(FSM<PlayerController> fsm)
        {
            base.OnEnter(fsm);
            if (_actionFSM == null)
            {
                _actionFSM = new FSM<PlayerController>(Player);
                _actionFSM.AddState(new PlayerIdleState());
                _actionFSM.AddState(new PlayerMovingState());
                _actionFSM.AddState(new PlayerShootingState());
                _actionFSM.AddState(new PlayerControlledState());
            }
            if (_actionFSM.CurrentState == null)
                _actionFSM.ChangeState<PlayerIdleState>();
        }

        public override void OnExit(FSM<PlayerController> fsm)
        {
            _actionFSM.CurrentState?.OnExit(_actionFSM);
        }

        public override void OnUpdate(FSM<PlayerController> fsm)
        {
            _actionFSM.CurrentState?.OnUpdate(_actionFSM);
        }

        public void ChangeActionState<T>() where T : IState<PlayerController>
        {
            _actionFSM.ChangeState<T>();
        }
    }
}

