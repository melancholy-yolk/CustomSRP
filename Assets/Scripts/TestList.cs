using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class TestList : MonoBehaviour
{
    private List<int> testList = new List<int>();
    
    void Start()
    {
        
        for (int i = 0; i < 10000; i++)
        {
            testList.Add(i);
        }

        Profiler.BeginSample("Test List");
        // for (int j = 0; j < testList.Count; j++)
        // {
        //     if (testList[j] % 2 == 0)
        //     {
        //         //Debug.Log(testList[j]);
        //     }
        // }
        int listCount = testList.Count;
        for (int j = 0; j < listCount; j++)
        {
            if (testList[j] % 2 == 0)
            {
                //Debug.Log(testList[j]);
            }
        }
        Profiler.EndSample();
    }

    void Update()
    {
        
    }
}
