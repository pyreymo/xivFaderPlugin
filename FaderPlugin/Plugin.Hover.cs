using System.Numerics;
using Dalamud.Bindings.ImGui;
using FaderPlugin.Data;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FaderPlugin;

public partial class Plugin
{
    /// <summary>
    /// For each hover group defined in configuration, if any addon in that group is hovered,
    /// mark all addons in the group as hovered.
    /// </summary>
    private void ApplyHoverGroups()
    {
        foreach (var group in HoverGroupAddonCaches)
        {
            var groupActivated = false;
            foreach (var addonName in group.AddonNames)
            {
                if (!OriginalHoveredAddons.Contains(addonName))
                    continue;

                groupActivated = true;
                break;
            }

            if (!groupActivated)
                continue;

            // Only activate from the original hover set to prevent cascading hover states
            // where multiple groups have overlapping addons.
            foreach (var addonName in group.AddonNames)
            {
                AddonHoverStates[addonName] = true;
                CurrentHoveredAddons.Add(addonName);
            }
        }
    }

    /// <summary>
    /// Collects all addon hover states
    /// </summary>
    private void UpdateHoverStates()
    {
        var mousePos = ImGui.GetMousePos();
        AddonHoverStates.Clear();
        CurrentHoveredAddons.Clear();
        OriginalHoveredAddons.Clear();

        foreach (var addonName in HoverAddonNames)
        {
            // Compute the hover state once per addon.
            var isHovered = IsAddonHovered(addonName, mousePos);
            AddonHoverStates[addonName] = isHovered;
            if (!isHovered)
                continue;

            CurrentHoveredAddons.Add(addonName);
            OriginalHoveredAddons.Add(addonName);
        }
        ApplyHoverGroups();
    }

    private void UpdateMouseHoverState()
    {
        // Update the hover states for all addons.
        UpdateHoverStates();

        if (!CurrentHoveredAddons.SetEquals(PreviousHoveredAddons))
            StateChanged = true;

        PreviousHoveredAddons.Clear();
        foreach (var addonName in CurrentHoveredAddons)
            PreviousHoveredAddons.Add(addonName);

        var hoverDetected = CurrentHoveredAddons.Count != 0;
        UpdateState(State.Hover, hoverDetected);
    }

    /// <summary>
    /// Checks if the addon identified by addonName is currently hovered.
    /// </summary>
    private unsafe bool IsAddonHovered(string addonName, Vector2 mousePos)
    {
        var addonPointer = GameGui.GetAddonByName(addonName);
        if (addonPointer == nint.Zero)
            return false;

        var addon = (AtkUnitBase*)addonPointer.Address;
        float posX = addon->GetX();
        float posY = addon->GetY();
        var width = addon->GetScaledWidth(true);
        var height = addon->GetScaledHeight(true);

        return mousePos.X >= posX && mousePos.X <= posX + width && mousePos.Y >= posY && mousePos.Y <= posY + height;
    }
}
