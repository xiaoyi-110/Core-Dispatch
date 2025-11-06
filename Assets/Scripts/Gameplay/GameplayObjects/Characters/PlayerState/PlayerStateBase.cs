using UnityEngine;
using Gameplay.IFSM;
namespace Gameplay.GameplayObjects.Characters.PlayerState
{
    public abstract class PlayerStateBase : FSMStateBase<PlayerController>
    {
        protected PlayerController Player; // Reference to the player controller
        public override void SetStateMachine(FSM<PlayerController> fsm)
        {
            base.SetStateMachine(fsm);
            Player = fsm.Owner;
        }

        public override void OnEnter(FSM<PlayerController> fsm)
        {
            //Debug.Log($"[Player] Enter State: {GetType().Name}");
        }

        public override void OnExit(FSM<PlayerController> fsm)
        {
            //Debug.Log($"[Player] Exit State: {GetType().Name}");
        }
    }
}