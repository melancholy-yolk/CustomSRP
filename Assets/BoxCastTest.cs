using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxCastTest : MonoBehaviour
{
    List<RendererInfo> lastRendererList = new List<RendererInfo>();

    public class RendererInfo
    {
        public Renderer renderer;
        public Color color;

        public void RevertColor()
        {
            renderer.material.color = Color.blue;
        }
    }


    void Start()
    {
        
    }

    void Update()
    {
        //RaycastHit[] hitArr = Physics.BoxCastAll(transform.position, Vector3.one, transform.forward, Quaternion.identity, 4);
        //if (hitArr != null && hitArr.Length > 0)
        //{
        //    for (int i = 0; i < hitArr.Length; i++)
        //    {
        //        Debug.Log(hitArr[i].collider.name);
        //    }
        //}

        if (lastRendererList != null && lastRendererList.Count > 0)
        {
            foreach (var info in lastRendererList)
            {
                info.RevertColor();
            }
        }
        lastRendererList.Clear();

        RaycastHit[] hits;
        CharacterController charCtrl = GetComponent<CharacterController>();
        Vector3 p1 = transform.position + charCtrl.center + Vector3.up * - charCtrl.height * 0.5F;
        Vector3 p2 = p1 + Vector3.up * charCtrl.height;

        // Cast character controller shape 10 meters forward, to see if it is about to hit anything
        hits = Physics.CapsuleCastAll(p1, p2, charCtrl.radius, transform.forward, 5);

        // Change the material of all hit colliders
        // to use a transparent Shader
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Renderer rend = hit.transform.GetComponent<Renderer>();
            
            if (rend)
            {
                //rend.material.shader = Shader.Find("Transparent/Diffuse");
                Color tempColor = rend.material.color;
                RendererInfo info = new RendererInfo();
                info.renderer = rend;
                info.color = tempColor;
                lastRendererList.Add(info);

                rend.material.color = Color.green;
            }
        }

    }
}
