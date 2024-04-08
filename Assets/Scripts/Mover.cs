using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mover : MonoBehaviour
{
    
    public Vector3 dir = new Vector3(0,0,0);

    public int threshold;

    public float speed;

    public float jitterspeed;
    
    void Update()
    {
        transform.Translate(dir * Time.deltaTime * speed);
        
        if ((transform.position.z) > threshold || transform.position.z < 0)
        {
            dir = -dir;
        }
        
        transform.Translate(Random.insideUnitSphere * jitterspeed * Time.deltaTime);
    }
}
