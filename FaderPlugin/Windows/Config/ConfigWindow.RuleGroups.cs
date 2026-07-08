using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FaderPlugin.Data;
using faderPlugin.Resources;

namespace FaderPlugin.Windows.Config;

public partial class ConfigWindow
{
    private void AddGroup()
    {
        var newGroup = new HoverGroup();
        Configuration.HoverGroups.Add(newGroup);
        SelectGroup(Configuration.HoverGroups.Count - 1);
        Configuration.Save();
    }

    private void SelectGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= Configuration.HoverGroups.Count)
            return;

        ClearElementSelection();
        SelectedGroupIndex = groupIndex;
        SelectedConfig = Configuration.HoverGroups[groupIndex].CreateEditableRules();
    }

    private void ClearGroupSelection()
    {
        SelectedGroupIndex = -1;
    }

    private void DrawSelectedGroupRules()
    {
        if (SelectedGroupIndex < 0 || SelectedGroupIndex >= Configuration.HoverGroups.Count)
        {
            ClearGroupSelection();
            return;
        }

        var selectedGroup = Configuration.HoverGroups[SelectedGroupIndex];
        if (!DrawGroupHeader(selectedGroup))
            return;

        DrawGroupBehavior(selectedGroup);

        ImGui.Separator();
        DrawGroupMembers(selectedGroup);

        ImGui.Separator();
        ImGui.TextUnformatted(Language.RuleGroupConfiguration);
        ImGuiHelpers.ScaledDummy(3);
        DrawRuleEditor(
            $"group-{SelectedGroupIndex}",
            SelectedConfig,
            selectedGroup.Disabled,
            disabled => selectedGroup.Disabled = disabled,
            selectedGroup.FadeOverride,
            () => SaveGroupRules(selectedGroup)
        );
    }

    private bool DrawGroupHeader(HoverGroup selectedGroup)
    {
        var groupName = selectedGroup.GroupName;
        if (ImGui.InputText(Language.RuleGroupName, ref groupName, 100))
        {
            selectedGroup.GroupName = groupName;
            Configuration.Save();
        }

        var deleteIcon = FontAwesomeIcon.TrashAlt.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var deleteButtonWidth = ImGui.CalcTextSize(deleteIcon).X + ImGui.GetStyle().FramePadding.X * 2;
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - deleteButtonWidth);

            if (ImGui.Button($"{deleteIcon}##{groupName}-delete"))
            {
                Configuration.HoverGroups.RemoveAt(SelectedGroupIndex);
                ClearGroupSelection();
                Configuration.Save();
                return false;
            }
        }

        return true;
    }

    private void DrawGroupBehavior(HoverGroup selectedGroup)
    {
        var linkHover = selectedGroup.LinkHover;
        if (ImGui.Checkbox(Language.RuleGroupLinkHover, ref linkHover))
        {
            selectedGroup.LinkHover = linkHover;
            Configuration.Save();
        }
        ImGuiComponents.HelpMarker(Language.RuleGroupLinkHoverTooltip);

        var sharedRules = selectedGroup.SharedRules;
        if (ImGui.Checkbox(Language.RuleGroupSharedRules, ref sharedRules))
        {
            selectedGroup.SharedRules = sharedRules;
            if (selectedGroup.SharedRules)
                RemoveSharedMembersFromOtherGroups(selectedGroup);
            Configuration.Save();
        }
        ImGuiComponents.HelpMarker(Language.RuleGroupSharedRulesTooltip);
    }

    private void DrawGroupMembers(HoverGroup selectedGroup)
    {
        ImGui.TextUnformatted($"{Language.HoverGroupsElements}:");

        using var table = ImRaii.Table(
            $"GroupElementsTable{SelectedGroupIndex}",
            2,
            ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.Resizable
        );
        if (!table.Success)
            return;

        foreach (var element in ElementUtil.OrderedElements)
        {
            if (element.ShouldIgnoreElement())
                continue;

            ImGui.TableNextColumn();
            var elementName = ElementUtil.GetElementName(element);
            var isInGroup = selectedGroup.Elements.Contains(element);

            if (ImGui.Checkbox($"{elementName}##group-{SelectedGroupIndex}-{element}", ref isInGroup))
            {
                if (isInGroup)
                    AddElementToGroup(selectedGroup, element);
                else
                    selectedGroup.Elements.Remove(element);

                Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                var addonNames = ElementUtil.GetAddonName(element);
                DrawAddonBounds(addonNames);
            }
        }
    }

    private void SaveGroupRules(HoverGroup selectedGroup)
    {
        selectedGroup.UpdateRules(SelectedConfig);
        SelectedConfig = selectedGroup.CreateEditableRules();
        Configuration.Save();
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
}
