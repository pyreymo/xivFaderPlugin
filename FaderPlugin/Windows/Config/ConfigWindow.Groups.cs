using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using faderPlugin.Data;
using FaderPlugin.Data;
using faderPlugin.Resources;

namespace FaderPlugin.Windows.Config;

public partial class ConfigWindow
{
    private int SelectedGroupIndex = -1;

    private void ClearGroupSelection()
    {
        SelectedGroupIndex = -1;
    }

    private static string GetGroupListButtonText(HoverGroup group) => group.GroupName;

    private void DrawGroupList(float buttonWidth)
    {
        var style = ImGui.GetStyle();
        DrawRuleTargetListHeader(Language.RuleGroupsHeader, buttonWidth, true);

        for (var i = 0; i < Configuration.HoverGroups.Count; i++)
        {
            var group = Configuration.HoverGroups[i];
            var label = $"{GetGroupListButtonText(group)}##RuleGroup{i}";

            using var pushedStyle = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));

            var desiredButtonColor = ImGui.GetColorU32(ImGuiCol.Button);
            if (SelectedGroupIndex == i)
                desiredButtonColor = ImGui.GetColorU32(ImGuiCol.ButtonActive);

            var hasScrollbar = ImGui.GetScrollMaxY() > 0.0f;
            using var pushedColor = ImRaii.PushColor(ImGuiCol.Button, desiredButtonColor);

            if (ImGui.Button(label, new Vector2(buttonWidth - (hasScrollbar ? style.ScrollbarSize : 0.0f), 0)))
            {
                SelectGroup(i);
            }

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

    private void DrawSelectedGroupSettings()
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

    private void SaveGroupRules(HoverGroup selectedGroup)
    {
        selectedGroup.UpdateRules(SelectedConfig);
        SelectedConfig = selectedGroup.CreateEditableRules();
        Configuration.Save();
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

    private bool DrawSharedRuleElementNotice(Element element)
    {
        var group = GetSharedRuleGroup(element);
        if (group == null)
            return false;

        ImGui.TextUnformatted(Language.RuleGroupInherited.Format(ElementUtil.GetElementName(element), group.GroupName));
        ImGui.TextUnformatted(Language.RuleGroupIndependentRulesPreserved);

        var groupIndex = Configuration.HoverGroups.IndexOf(group);
        if (groupIndex >= 0 && ImGui.Button(Language.RuleGroupEditGroup))
            SelectGroup(groupIndex);

        ImGui.SameLine();
        if (ImGui.Button(Language.RuleGroupDetachKeepRules))
        {
            DetachElementAndKeepGroupRules(element, group);
            SelectedConfig = Configuration.GetElementConfig(element);
        }

        return true;
    }

    private HoverGroup? GetSharedRuleGroup(Element element) =>
        Configuration.HoverGroups.FirstOrDefault(group => group.SharedRules && group.Elements.Contains(element));

    private void AddElementToGroup(HoverGroup group, Element element)
    {
        if (group.SharedRules)
            RemoveElementFromOtherSharedGroups(group, element);

        if (!group.Elements.Contains(element))
            group.Elements.Add(element);
    }

    private void RemoveSharedMembersFromOtherGroups(HoverGroup owner)
    {
        foreach (var element in owner.Elements.ToArray())
        {
            RemoveElementFromOtherSharedGroups(owner, element);
        }
    }

    private void RemoveElementFromOtherSharedGroups(HoverGroup owner, Element element)
    {
        foreach (var group in Configuration.HoverGroups)
        {
            if (group == owner || !group.SharedRules)
                continue;

            group.Elements.Remove(element);
        }
    }

    private void DetachElementAndKeepGroupRules(Element element, HoverGroup group)
    {
        Configuration.elementsConfig[element] = CloneRules(group.GetRuleEntries());
        Configuration.DisabledElements[element] = group.Disabled;
        Configuration.FadeOverrides[element] = CloneFadeOverride(group.FadeOverride);
        group.Elements.Remove(element);
        Configuration.Save();
    }

    private static List<ConfigEntry> CloneRules(IEnumerable<ConfigEntry> rules) =>
        rules.Select(entry => new ConfigEntry(entry.state, entry.setting) { Opacity = entry.Opacity }).ToList();

    private static FadeOverride CloneFadeOverride(FadeOverride fadeOverride) =>
        new()
        {
            UseCustomFadeTimes = fadeOverride.UseCustomFadeTimes,
            EnterTransitionSpeedOverride = fadeOverride.EnterTransitionSpeedOverride,
            ExitTransitionSpeedOverride = fadeOverride.ExitTransitionSpeedOverride,
        };

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
