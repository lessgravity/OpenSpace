using System.Collections.Generic;
using EngineKit.UI;

namespace OpenSpace.Windows;

public class HierarchyUiWindow : UiWindow
{
    //private readonly IEntityWorld _entityWorld;
    private readonly IList<int> _rootEntities;

    public HierarchyUiWindow(/*IEntityWorld entityWorld*/) : base($"{MaterialDesignIcons.FileTree}  Hierarchy")
    {
        //_entityWorld = entityWorld;
        _rootEntities = new List<int>();
    }

    protected override void RenderInternal()
    {
        _rootEntities.Clear();
        /*
        var rootEntities = _entityWorld.GetEntitiesWithComponents<RootComponent>();
        foreach (var rootEntity in rootEntities)
        {
            RenderElement(rootEntity.Id);
        }
        */
    }

    private void RenderElement(int entityId)
    {
        /*
        var entity = _entityWorld.GetEntity(entityId);
        var isNodeExpanded = ImGui.TreeNode(entity.Name);
        if (!isNodeExpanded)
        {
            return;
        }
        
        var children = new List<int>();
        var possibleChildren = _entityWorld.GetEntitiesWithComponents<ParentComponent>();
        foreach (var possibleChild in possibleChildren)
        {
            var possibleChildParent = _entityWorld.GetComponent<ParentComponent>(possibleChild.Id);
            if (possibleChildParent.ParentEntity == entity.Id)
            {
                children.Add(possibleChild.Id);
            }
        }

        foreach (var child in children)
        {
            RenderElement(child);
        }

        ImGui.TreePop();
        */
    }
}