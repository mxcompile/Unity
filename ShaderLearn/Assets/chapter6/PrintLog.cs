using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrintLog : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.LogError(transform.forward);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
