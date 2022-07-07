using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class EditorTest
{
    [MenuItem("MyTools/Test")]
    public static void Test()
    {
        Handles.ArrowHandleCap(1, Vector3.zero, Quaternion.identity, 1, EventType.MouseMove);
    }
}
