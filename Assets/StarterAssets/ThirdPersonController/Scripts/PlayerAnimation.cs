using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    public class PlayerAnimation : MonoBehaviour
    {
        private Animator _animator;

        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDArmed;
        private int _animIDShoot;
        private int _animIDReload;
        private int _animIDMotionSpeed;
        private int _animIDAim;
        private int _animSpeedX;
        private int _animSpeedY;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            AssignAnimationIDs();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            _animIDArmed = Animator.StringToHash("IsArmed");
            _animIDShoot = Animator.StringToHash("Shoot");
            _animIDReload = Animator.StringToHash("Reload");
            _animIDAim = Animator.StringToHash("IsAiming");
            _animSpeedX = Animator.StringToHash("Speed_X");
            _animSpeedY = Animator.StringToHash("Speed_Y");
        }

        public void SetSpeed(float speed) => _animator.SetFloat(_animIDSpeed, speed);
        public void SetMotionSpeed(float motionSpeed) => _animator.SetFloat(_animIDMotionSpeed, motionSpeed);
        public void SetGrounded(bool grounded) => _animator.SetBool(_animIDGrounded, grounded);
        public void SetJump(bool jump) => _animator.SetBool(_animIDJump, jump);
        public void SetFreeFall(bool fall) => _animator.SetBool(_animIDFreeFall, fall);

        public void SetArmed(bool armed) => _animator.SetBool(_animIDArmed, armed);
        public void TriggerShoot() => _animator.SetTrigger(_animIDShoot);
        public void TriggerReload() => _animator.SetTrigger(_animIDReload);

        public void SetAim(bool aim) => _animator.SetBool(_animIDAim, aim);

        public void SetAimMoveSpeed(float x,float y)
        {
            _animator.SetFloat(_animSpeedX, x);
            _animator.SetFloat(_animSpeedY, y);
        }
        public void SetAimLayerWeight(float targetWeight)
        {
            float current = _animator.GetLayerWeight(1);
            float newWeight = Mathf.Lerp(current, targetWeight, Time.deltaTime * 10f);
            _animator.SetLayerWeight(1, newWeight);
        }
    }

}
