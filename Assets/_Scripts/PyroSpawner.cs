using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PyroSpawner : MonoBehaviour
{
    public GameObject prefab;          // Prefab to spawn
    public float spawnInterval = 2f;   // Time interval between spawns
    public float yOffset;

    void OnEnable()
    {
        // Call the 'Spawn' function repeatedly at an interval of 'spawnInterval' seconds
        InvokeRepeating("Spawn", spawnInterval, spawnInterval);
        
    }

    void Spawn()
    {
        Vector3 newPosition = transform.position + new Vector3(0, yOffset, 0);
        // Instantiate the prefab at this object's location
        Instantiate(prefab, newPosition, Quaternion.identity);
    }
    private void OnDisable()
    {
        CancelInvoke();
    }
}
