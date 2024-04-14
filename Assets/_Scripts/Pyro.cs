using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pyro : MonoBehaviour
{
    public float upwardForce = 10f; // Adjust this value to control the upward force
    public float destroyAfterSeconds = 5f; // Adjust this value to control the destruction delay

    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(transform.up * upwardForce, ForceMode.Impulse);
        }

        Destroy(gameObject, destroyAfterSeconds);
    }
}
