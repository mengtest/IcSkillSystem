using System;
using UnityEngine;
using XNode;

namespace CabinIcarus.IcSkillSystem.Runtime.xNode_Nodes
{
    [CreateNodeMenu("CabinIcarus/Nodes/UnityEngine/XRModule/PlaneAddedEventArgs Value")]
    public partial class PlaneAddedEventArgsValueNode:ValueNode
    {
        [SerializeField]
        private UnityEngine.Experimental.XR.PlaneAddedEventArgs _value;

        public override Type ValueType { get; } = typeof(UnityEngine.Experimental.XR.PlaneAddedEventArgs);
        
        protected override object GetOutValue()
        {
            return _value;
        }
    }
}