using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace StarterAssets
{
    public class RigManager : MonoSingleton<RigManager>
    {
        [SerializeField] private MultiAimConstraint rightHand = null;
        [SerializeField] private TwoBoneIKConstraint leftHand=null;
        [SerializeField] private MultiAimConstraint body=null;
        [SerializeField] private Transform aimTarget=null;
        [SerializeField] private Vector3 handKickDirection=Vector3.zero;
        [SerializeField] private Vector3 bodyKickDirection=new Vector3(-1,0,0);
        public Vector3 AimTarget { set => aimTarget.position = value; }

        private Vector3 _originalRightHandOffset=Vector3.zero;
        private Vector3 _originalBodyOffset=Vector3.zero;
        public float LeftHandWeight {set => leftHand.weight = value;}
        public float AimWeight 
        {
            set
            {
                rightHand.weight = value;
                body.weight = value;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _originalRightHandOffset = rightHand.data.offset;
            _originalBodyOffset = body.data.offset;
        }

        private void Update()
        {
            if(rightHand.data.offset!=_originalRightHandOffset)
            {
                rightHand.data.offset = Vector3.Lerp(rightHand.data.offset, _originalRightHandOffset, Time.deltaTime * 10f);
            }
            if(body.data.offset!=_originalBodyOffset)
            {
                body.data.offset = Vector3.Lerp(body.data.offset, _originalBodyOffset, Time.deltaTime * 10f);
            }
        }

        public void SetLeftHandTarget(Transform target)
        {
            leftHand.data.target = target;
        }

        public void ApplyWeaponKick(float rightHandDir,float bodyDir)
        {
            rightHand.data.offset = _originalRightHandOffset + handKickDirection * rightHandDir;
            body.data.offset = _originalBodyOffset + bodyKickDirection * bodyDir;
        }

    }
}

