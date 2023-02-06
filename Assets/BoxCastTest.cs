using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxCastTest : MonoBehaviour
{
    private CharacterController _charCtrl;
    private List<RendererInfo> _lastRendererList = new List<RendererInfo>();

    private class RendererInfo
    {
        public Renderer renderer;
        public Color color;

        public void RevertColor()
        {
            renderer.material.color = Color.blue;
        }
    }

    private void Awake()
    {
        _charCtrl = GetComponent<CharacterController>();
    }

    private void Start()
    {
        
    }

    private void Update()
    {
        //RaycastHit[] hitArr = Physics.BoxCastAll(transform.position, Vector3.one, transform.forward, Quaternion.identity, 4);
        //if (hitArr != null && hitArr.Length > 0)
        //{
        //    for (int i = 0; i < hitArr.Length; i++)
        //    {
        //        Debug.Log(hitArr[i].collider.name);
        //    }
        //}

        if (_lastRendererList != null && _lastRendererList.Count > 0)
        {
            foreach (var info in _lastRendererList)
            {
                info.RevertColor();
            }
        }
        _lastRendererList.Clear();

        RaycastHit[] hits;
        
        Vector3 p1 = transform.position + _charCtrl.center + Vector3.up * - _charCtrl.height * 0.5F;
        Vector3 p2 = p1 + Vector3.up * _charCtrl.height;

        // Cast character controller shape 10 meters forward, to see if it is about to hit anything
        hits = Physics.CapsuleCastAll(p1, p2, _charCtrl.radius, transform.forward, 5);

        // Change the material of all hit colliders
        // to use a transparent Shader
        if (hits != null)
        {
            int length = hits.Length;
            if (hits.Length > 0)
            {
                for (int i = 0; i < length; i++)
                {
                    RaycastHit hit = hits[i];
                    Renderer renderer = hit.transform.GetComponent<Renderer>();
            
                    if (renderer)
                    {
                        //rend.material.shader = Shader.Find("Transparent/Diffuse");
                        Color tempColor = renderer.material.color;
                        RendererInfo info = new RendererInfo();
                        info.renderer = renderer;
                        info.color = tempColor;
                        _lastRendererList.Add(info);

                        renderer.material.color = Color.green;
                    }
                }
            }
        }
    }

    // private void OnDrawGizmos()
    // {
    //     Gizmos.color = Color.green;
    //     Gizmos.DrawWireSphere(this.transform.position, 0.1f);
    //     Gizmos.DrawWireSphere(this._charCtrl.center, 0.1f);
    //     Gizmos.DrawWireSphere(_charCtrl.center + Vector3.up * - _charCtrl.height, 0.1f);
    //     Gizmos.DrawWireSphere(_charCtrl.center + Vector3.up * - _charCtrl.height * 0.5F, 0.1f);
    // }
    
    private void OnDrawGizmos()
    {
        var color = Gizmos.color;
        Gizmos.color = Color.white; //颜色
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix; //位置
        Gizmos.DrawCube(transform.position, Vector3.one); //绘制图形
        Gizmos.color = color;
        Gizmos.matrix = oldMatrix;
    }

    private void OnDrawGizmosSelected()
    {
        var color = Gizmos.color;
        Gizmos.color = Color.red;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
        Gizmos.color = color;
        Gizmos.matrix = oldMatrix;
    }
    
}
