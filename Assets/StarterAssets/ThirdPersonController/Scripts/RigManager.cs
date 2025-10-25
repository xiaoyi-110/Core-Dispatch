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
        public Vector3 AimTarget { set => aimTarget.position = value; }

        public float LeftHandWeight {set => leftHand.weight = value;}
        public float AimWeight 
        {
            set
            {
                rightHand.weight = value;
                body.weight = value;
            }
        }

    }
}

