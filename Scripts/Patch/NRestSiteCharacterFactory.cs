using BaseLib.Extensions;
using BaseLib.Utils.NodeFactories;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace STS2_Tomorin_Mod.Patch;

internal class NRestSiteCharacterFactory : NodeFactory<NRestSiteCharacter>
{
    public NRestSiteCharacterFactory() : base(new List<INodeInfo>()
    {
      (NodeFactory.INodeInfo) new NodeFactory.NodeInfo<Control>("ControlRoot"),
      (NodeFactory.INodeInfo) new NodeFactory.NodeInfo<NSelectionReticle>("%SelectionReticle"),
      (NodeFactory.INodeInfo) new NodeFactory.NodeInfo<Control>("%HitBox"),
      (NodeFactory.INodeInfo) new NodeFactory.NodeInfo<Control>("%ThoughtBubbleRight"),
      (NodeFactory.INodeInfo) new NodeFactory.NodeInfo<Control>("%ThoughtBubbleLeft"),
    })
    {
        
    }

    protected override void GenerateNode(Node target, INodeInfo required)
    {
        switch (required.Path)
        {
            case "ControlRoot":
                target.AddUnique((Godot.Node) new Control()
                {
                    Size = new Vector2(240f, 280f),
                    Position = new Vector2(-120f, -280f)
                }, "ControlRoot");
                break;
            case "%SelectionReticle":
                var defalut = CreateSelectionReticle();
                target.AddUnique(defalut);
                break;
            case "%HitBox":
                Control control = new Control()
                {
                    Size = new Vector2(420.0f, 553f),
                    Position = new Vector2(2f, -258f)
                };
                target.AddUnique(control, "HitBox");
                break;
            case "%ThoughtBubbleRight":
                Control right = new Control()
                {
                    Position = new Vector2(330.39f, -291.7f)
                };
                target.AddUnique(right, "ThoughtBubbleRight");
                break;
            case "%ThoughtBubbleLeft":
                Control left = new Control()
                {
                    Position = new Vector2(41.136f, -273.537f)
                };
                target.AddUnique(left, "ThoughtBubbleLeft");
                break;
        }
    }

    private static NSelectionReticle CreateSelectionReticle()
    {
        NSelectionReticle target = new NSelectionReticle();
        target.Name = "SelectionReticle";
        target.Size = new Vector2(420.0f, 553f);
        target.Position = new Vector2(2f, -258f);
        return target;
    }
}