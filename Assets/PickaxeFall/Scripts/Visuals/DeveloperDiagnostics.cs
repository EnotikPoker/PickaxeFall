#if PICKAXEFALL_DEVTOOLS
using System.Collections.Generic;
using UnityEngine;
namespace PickaxeFall
{
    public sealed class DeveloperDiagnostics:MonoBehaviour
    {
        GameController game;Material material;readonly List<LineRenderer> lines=new();readonly List<Vector2> points=new();
        public void Initialize(GameController owner){game=owner;material=new Material(Shader.Find("Sprites/Default"));}
        void LateUpdate()
        {
            int used=0;if(game.DeveloperColliders)foreach(var tool in game.Pickaxes)if(tool.IsAlive)
            {
                var shape=tool.GetComponent<PolygonCollider2D>();for(int p=0;p<shape.pathCount;p++)
                {
                    shape.GetPath(p,points);LineRenderer line;
                    if(used==lines.Count){var obj=new GameObject("Collision outline");obj.transform.SetParent(transform);line=obj.AddComponent<LineRenderer>();line.sharedMaterial=material;line.startColor=line.endColor=Color.green;line.startWidth=line.endWidth=.012f;line.sortingOrder=50;line.loop=true;lines.Add(line);}else line=lines[used];
                    used++;line.enabled=true;line.positionCount=points.Count;for(int i=0;i<points.Count;i++)line.SetPosition(i,tool.Body.position+ImpactGate.Rotate(points[i]*PixelArt.PickaxeScale,tool.Body.rotation));
                }
            }
            for(int i=used;i<lines.Count;i++)lines[i].enabled=false;
        }
        void OnGUI(){if(!game.DeveloperColliders)return;GUI.color=Color.yellow;GUI.skin.label.fontSize=24;GUI.Label(new Rect(15,Screen.height*.28f,Screen.width-30,140),"DEV · "+game.DeveloperContactInfo);}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
#endif
