using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Gameplay.GameplayObjects.Items
{
    public class Projectile : MonoBehaviour
    {
        private static readonly Dictionary<int, Stack<Projectile>> Pools = new Dictionary<int, Stack<Projectile>>();
        public static int TotalSpawned { get; private set; }
        public static int TotalReused { get; private set; }
        public static int TotalReturned { get; private set; }

        [SerializeField]private float speed = 20f;
        [SerializeField] private Transform defaultImpact = null;
        private float _damage = 1f;
        private bool _initialized = false;
        private StarterAssets.Character _character=null;
        private Rigidbody _rigidbody=null;
        private Collider _collider = null;
        private Coroutine _despawnRoutine = null;
        private int _poolKey = 0;
        private bool _pooled = false;
        private bool _enableServerDamage = true;

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
            _collider.enabled = true;
        }

        public static Projectile Spawn(Projectile prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            int key = prefab.GetInstanceID();
            if (!Pools.TryGetValue(key, out Stack<Projectile> pool))
            {
                pool = new Stack<Projectile>();
                Pools.Add(key, pool);
            }

            Projectile instance = null;
            if (pool.Count > 0)
            {
                instance = pool.Pop();
                if (instance == null)
                {
                    instance = Instantiate(prefab, position, rotation);
                    TotalSpawned++;
                }
                else
                {
                    instance.transform.SetPositionAndRotation(position, rotation);
                    instance.gameObject.SetActive(true);
                    TotalReused++;
                }
            }
            else
            {
                instance = Instantiate(prefab, position, rotation);
                TotalSpawned++;
            }

            instance._poolKey = key;
            instance._pooled = true;
            instance.Initialize();
            if (instance._collider != null)
            {
                instance._collider.enabled = true;
            }
            if (instance._rigidbody != null)
            {
                instance._rigidbody.velocity = Vector3.zero;
                instance._rigidbody.angularVelocity = Vector3.zero;
            }
            return instance;
        }

        public void Initialize(StarterAssets.Character character,Vector3 target,float damage, bool enableServerDamage = true)
        {
            Initialize();
            _character = character;
            _damage = damage;
            _enableServerDamage = enableServerDamage;
            transform.LookAt(target);
            _rigidbody.velocity = transform.forward.normalized * speed;
            ScheduleDespawn(5f);
        }

        private void ScheduleDespawn(float seconds)
        {
            if (_despawnRoutine != null)
            {
                StopCoroutine(_despawnRoutine);
            }
            _despawnRoutine = StartCoroutine(DespawnAfter(seconds));
        }

        private IEnumerator DespawnAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (_despawnRoutine != null)
            {
                StopCoroutine(_despawnRoutine);
                _despawnRoutine = null;
            }

            if (!_pooled)
            {
                Destroy(gameObject);
                return;
            }

            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            if (_collider != null)
            {
                _collider.enabled = false;
            }
            _character = null;
            _damage = 0f;
            gameObject.SetActive(false);

            if (!Pools.TryGetValue(_poolKey, out Stack<Projectile> pool))
            {
                pool = new Stack<Projectile>();
                Pools.Add(_poolKey, pool);
            }
            pool.Push(this);
            TotalReturned++;
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
                if (_enableServerDamage && character != null)
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


            ReturnToPool();
        }
    }
}
    
