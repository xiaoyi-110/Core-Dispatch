using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarterAssets;
namespace Gameplay.GameplayObjects.AnimatorStateMachines
{
    public class EquipStateMachine : StateMachineBehaviour
    {
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.SetLayerWeight(layerIndex, 1f);
        }
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Character character = animator.gameObject.GetComponent<Character>();
            if (character != null)
            {
                character.EquipFinished();
            }
        }
    }

}
