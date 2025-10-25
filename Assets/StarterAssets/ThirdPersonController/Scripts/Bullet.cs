using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    public class Bullet : MonoBehaviour
    {
        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }
        private void Start()
        {
            float speed = 10f;
            rb.velocity = transform.forward * speed;
        }

        private void OnTriggerEnter(Collider other)
        {
            if(other.GetComponent<BulletTarget>() != null)
            {
                Debug.Log("Hit Target");
            }
            else
            {
                Debug.Log("Hit Something Else");
            }
                Destroy(gameObject);
           Debug.Log("Destroy Bullet");
        }
    }
}

