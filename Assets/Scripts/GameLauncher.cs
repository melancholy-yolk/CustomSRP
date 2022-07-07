using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameLauncher : MonoBehaviour
{
    public Material mat;
    Vector3 pos = Vector3.zero;

    void Start()
    {
        
        for (int i = 0; i < 100; i++)
        {
            pos = Vector3.zero;
            pos.x += i / 10 * 2;
            pos.z += i % 10 * 2;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);
            go.AddComponent<PerObjectMaterialProperties>();
        }
    }

    void Update()
    {
        
    }
}
