using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Gameplay.GameplayObjects.Items
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField]private float speed = 20f;
        [SerializeField] private Transform defaultImpact = null;
        private float _damage = 1f;
        private bool _initialized = false;
        private StarterAssets.Character _character=null;
        private Rigidbody _rigidbody=null;
        private Collider _collider = null;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if(_initialized) return;
            _initialized = true;

            _rigidbody = GetComponent<Rigidbody>();
            if(_rigidbody==null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
            }
            _rigidbody.useGravity = false;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _collider = GetComponent<Collider>();
            if(_collider==null)
            {
                _collider = gameObject.AddComponent<SphereCollider>();
            }
            _collider.isTrigger = false;
            _collider.tag="Projectile";
        }

        public void Initialize(StarterAssets.Character character,Vector3 target,float damage)
        {
            Initialize();
            _character = character;
            _damage = damage;
            transform.LookAt(target);
            _rigidbody.velocity = transform.forward.normalized * speed;
            Destroy(gameObject, 5f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if((_character!=null&&collision.transform.root==_character.transform.root)||collision.gameObject.CompareTag("Projectile"))
            {
                Physics.IgnoreCollision(_collider,collision.collider);
                return;
            }

            StarterAssets.Character character = collision.transform.root.GetComponent<StarterAssets.Character>();
            if (NetworkManager.Singleton.IsServer)
            {
                if(character != null)
                {
                    character.TakeDamage(_character,collision.transform,_damage);
                }
            }
            else
            {
                if (character != null)
                {
                    //character.TakeDamage(_character, collision.transform, _damage);
                }
                else if (defaultImpact != null)
                {
                    if (collision.gameObject.layer != LayerMask.NameToLayer("LocalPlayer") && collision.gameObject.layer != LayerMask.NameToLayer("NetworkPlayer"))
                    {
                        Transform impact = Instantiate(defaultImpact, collision.contacts[0].point, Quaternion.FromToRotation(Vector3.up, collision.contacts[0].normal));
                        Destroy(impact.gameObject, 30f);
                    }

                }
            }


                Destroy(gameObject);
        }
    }
}
    
