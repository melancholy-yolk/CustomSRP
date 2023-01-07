using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VisualScript.Runtime
{
    public class Graph
    {
        private List<Node> _nodes;
        public List<Node> nodes => _nodes;

        private List<Connection> _connections;
        public List<Connection> connections => _connections;

        public virtual void OnGraphStart()
        {

        }

        public virtual void OnGraphUpdate()
        {

        }

        public virtual void OnGraphStop()
        {

        }
    }
}
