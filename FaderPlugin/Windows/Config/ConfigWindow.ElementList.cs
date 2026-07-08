using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
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

    private void ClearElementSelection()
    {
        SelectedElement = null;
    }

    private void SelectElement(Element element)
    {
        SelectedElement = element;
        SelectedConfig = Configuration.GetElementConfig(element);
    }

    private void SaveSelectedElementConfig()
    {
        if (SelectedElement == null)
            return;

        Configuration.elementsConfig[SelectedElement.Value] = SelectedConfig
            .Select(entry => new ConfigEntry(entry.state, entry.setting) { Opacity = entry.Opacity })
            .ToList();

        Configuration.Save();
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
}
