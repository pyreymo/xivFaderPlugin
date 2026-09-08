using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FaderPlugin.Data;
using faderPlugin.Resources;

namespace FaderPlugin.Windows.Config;

public partial class ConfigWindow
{
    private const string ElementTooltipIndicator = "   ?";

    private void DrawRuleTargetList(float buttonWidth, float childSize, float scrollbarSize)
    {
        using var child = ImRaii.Child("RuleTargetList", new Vector2(childSize, 0), true);
        if (!child.Success)
            return;

        DrawGroupList(buttonWidth);

        ImGui.Separator();

        var elementsExpanded = ImGui.CollapsingHeader($"{Language.RuleElementsHeader}##RuleElements", ImGuiTreeNodeFlags.None);
        if (!elementsExpanded)
            return;

        foreach (var element in ElementUtil.OrderedElements)
        {
            if (element.ShouldIgnoreElement())
                continue;

            DrawElementListItem(element, buttonWidth, scrollbarSize);
        }
    }

    private void DrawGroupList(float buttonWidth)
    {
        var style = ImGui.GetStyle();
        DrawRuleTargetListHeader(Language.RuleGroupsHeader, buttonWidth, true);

        for (var i = 0; i < Configuration.HoverGroups.Count; i++)
        {
            var group = Configuration.HoverGroups[i];
            var label = $"{GetGroupListButtonText(group)}##RuleGroup{i}";

            using var pushedStyle = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));

            var desiredButtonColor =
                SelectedGroupIndex == i ? ImGui.GetColorU32(ImGuiCol.ButtonActive) : ImGui.GetColorU32(ImGuiCol.Button);

            var hasScrollbar = ImGui.GetScrollMaxY() > 0.0f;
            using var pushedColor = ImRaii.PushColor(ImGuiCol.Button, desiredButtonColor);

            if (ImGui.Button(label, new Vector2(buttonWidth - (hasScrollbar ? style.ScrollbarSize : 0.0f), 0)))
                SelectGroup(i);

            if (ImGui.IsItemHovered())
            {
                var addonNames = group.Elements.SelectMany(ElementUtil.GetAddonName).ToArray();
                DrawAddonBounds(addonNames);
            }
        }
    }

    private void DrawRuleTargetListHeader(string label, float buttonWidth, bool showAddGroup = false)
    {
        var startX = ImGui.GetCursorPosX();
        ImGui.TextUnformatted(label);
        if (!showAddGroup)
            return;

        var icon = FontAwesomeIcon.Plus.ToIconString();
        var hasScrollbar = ImGui.GetScrollMaxY() > 0.0f;
        var headerWidth = buttonWidth - (hasScrollbar ? ImGui.GetStyle().ScrollbarSize : 0.0f);
        var buttonSize = new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight());
        ImGui.SameLine(startX + headerWidth - buttonSize.X);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button($"{icon}##AddRuleGroup", buttonSize))
                AddGroup();
        }

        if (ImGui.IsItemHovered())
            Helper.Tooltip(Language.HoverGroupsAddGroup);
    }

    private void DrawElementListItem(Element element, float buttonWidth, float scrollbarSize)
    {
        var buttonText = GetElementListButtonText(element);
        var tooltipText = element.TooltipForElement();

        using var pushedStyle = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));

        var desiredButtonColor = SelectedElement == element ? ImGui.GetColorU32(ImGuiCol.ButtonActive) : ImGui.GetColorU32(ImGuiCol.Button);

        var hasScrollbar = ImGui.GetScrollMaxY() > 0.0f;
        using var pushedColor = ImRaii.PushColor(ImGuiCol.Button, desiredButtonColor);
        if (ImGui.Button(buttonText, new Vector2(buttonWidth - (hasScrollbar ? scrollbarSize : 0.0f), 0)))
        {
            ClearGroupSelection();
            SelectElement(element);
        }

        if (!ImGui.IsItemHovered())
            return;

        if (!string.IsNullOrEmpty(tooltipText))
            Helper.Tooltip(tooltipText);

        DrawAddonBounds(ElementUtil.GetAddonName(element));
    }

    private (float ButtonWidth, float ChildWidth) GetRuleTargetListSizes()
    {
        var style = ImGui.GetStyle();

        var elementWidths = ElementUtil
            .OrderedElements.Where(element => !element.ShouldIgnoreElement())
            .Select(element => ImGui.CalcTextSize(GetElementListButtonText(element)).X);

        var groupWidths = Configuration.HoverGroups.Select(group => ImGui.CalcTextSize(GetGroupListButtonText(group)).X);

        var maxTextWidth = elementWidths
            .Concat(groupWidths)
            .Append(ImGui.CalcTextSize(Language.RuleGroupsHeader).X + style.ItemSpacing.X + ImGui.GetFrameHeight())
            .Append(ImGui.CalcTextSize(Language.RuleElementsHeader).X)
            .DefaultIfEmpty(0.0f)
            .Max();

        var buttonWidth = maxTextWidth + style.FramePadding.X * 2 + style.ScrollbarSize;
        var childWidth = buttonWidth + style.WindowPadding.X * 2;
        return (buttonWidth, childWidth);
    }

    private static string GetElementListButtonText(Element element)
    {
        var elementName = ElementUtil.GetElementName(element);
        var tooltipText = element.TooltipForElement();

        return string.IsNullOrEmpty(tooltipText) ? elementName : $"{elementName}{ElementTooltipIndicator}";
    }

    private static string GetGroupListButtonText(HoverGroup group) => group.GroupName;

    private static void DrawAddonBounds(IEnumerable<string> addonNames)
    {
        var color = ImGui.GetColorU32(ImGuiCol.ButtonActive);
        var drawlist = ImGui.GetBackgroundDrawList();

        foreach (var addonName in addonNames)
        {
            var addonPosition = Addon.GetAddonPosition(addonName);
            if (!addonPosition.IsPresent)
                continue;

            drawlist.AddRect(addonPosition.Start, addonPosition.End, color, 0, ImDrawFlags.None, 5.0f * ImGuiHelpers.GlobalScale);
        }
    }
}
