using Gameplay.GameplayObjects.characters.PlayerState;
using Gameplay.IFSM;

namespace Gameplay.GameplayObjects.Characters.PlayerState
{
    public class PlayerDeadState : PlayerLifeStateBase
    {
        public override void OnEnter(FSM<PlayerController> fsm)
        {
            base.OnEnter(fsm);
            // 1. 播放死亡动画
            // Player.Animator.SetBool("IsDead", true); 

            // 2. 禁用所有移动输入和碰撞
            // Player.DisableInput();
        }

        public override void OnUpdate(FSM<PlayerController> fsm)
        {
            // DeadState 的 Update 中不做任何动作处理，只检查复活条件
            // if (Player.CanBeRevived)
            // {
            //     fsm.ChangeState<PlayerRevivingState>();
            // }
        }
    }
}