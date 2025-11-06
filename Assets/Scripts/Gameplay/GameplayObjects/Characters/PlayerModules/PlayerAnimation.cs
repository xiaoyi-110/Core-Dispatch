using UnityEngine;

namespace Gameplay.GameplayObjects.Characters.PlayerModules
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimation : MonoBehaviour
    {
        private Animator animator;

        private readonly int speedHash = Animator.StringToHash("Speed");
        private readonly int inputXHash = Animator.StringToHash("InputX");
        private readonly int inputYHash = Animator.StringToHash("InputY");
        private readonly int isIdleHash = Animator.StringToHash("IsIdle");
        private readonly int isMovingHash = Animator.StringToHash("IsMoving");

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void UpdateMovement(Vector2 movement)
        {
            float damp = 0.1f;
            animator.SetFloat(inputXHash, movement.x, damp, Time.deltaTime);
            animator.SetFloat(inputYHash, movement.y, damp, Time.deltaTime);
            animator.SetFloat(speedHash, movement.magnitude,0.2f,Time.deltaTime);
        }

        public void SetBool(string boolName, bool value)
        {
            animator.SetBool(Animator.StringToHash(boolName), value);
        }
    }
}
