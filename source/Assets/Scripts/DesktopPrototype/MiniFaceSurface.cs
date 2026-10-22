using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UmaDesktop
{
    public sealed class MiniFaceSurface
    {
        private sealed class Part {public Material Material;public Shader Original;public int Queue;}
        private readonly List<Part> parts=new List<Part>();
        private readonly Shader shader;
        public MiniFaceSurface(UmaContainerCharacter model)
        {
            shader=Resources.Load<Shader>("UmaDesktop/FaceSurface");
            if(shader==null)throw new InvalidOperationException("Face-surface shader is missing.");
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="M_Eye"||r.name=="M_Mouth"))
            {
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var uv=mesh.uv;bool eyes=renderer.name=="M_Eye";
                var material=renderer.sharedMaterial;parts.Add(new Part {Material=material,Original=material.shader,Queue=material.renderQueue});
                float mask=material.HasProperty("_StencilMask")?material.GetFloat("_StencilMask"):100;
                float comparison=material.HasProperty("_StencilComp")?material.GetFloat("_StencilComp"):8;
                float operation=material.HasProperty("_StencilOp")?material.GetFloat("_StencilOp"):0;
                Color tint=material.HasProperty("_CharaColor")?material.GetColor("_CharaColor"):Color.white;
                material.shader=shader;
                material.SetFloat("_StencilMask",mask);material.SetFloat("_StencilComp",comparison);material.SetFloat("_StencilOp",operation);material.SetColor("_CharaColor",tint);
                Func<IEnumerable<Vector2>,Vector4> bounds=points=>{var a=points.ToArray();return new Vector4(a.Min(p=>p.x),a.Min(p=>p.y),a.Max(p=>p.x),a.Max(p=>p.y));};
                material.SetVector("_UvBoundsLeft",bounds(eyes?uv.Where(p=>p.x<=.125f):uv));
                material.SetVector("_UvBoundsRight",bounds(eyes?uv.Where(p=>p.x>.125f):uv));
                material.SetFloat("_EyeMode",eyes?1:0);
            }
            SetMode("fixed");
        }
        // Source comparison is reachable only through the explicit face audit.
        public void SetMode(string mode)
        {
            foreach(var part in parts)
            {
                part.Material.shader=mode=="baseline"?part.Original:shader;
                part.Material.renderQueue=part.Queue;
            }
        }
    }
}
