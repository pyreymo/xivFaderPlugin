using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using faderPlugin.Data;
using FaderPlugin.Data;
using faderPlugin.Resources;

namespace FaderPlugin.Windows.Config;

public partial class ConfigWindow
{
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
}
