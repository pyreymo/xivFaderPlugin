using System.Collections.Generic;
using faderPlugin.Data;
using FaderPlugin.Data;

namespace FaderPlugin;

public partial class Plugin
{
    private void RebuildAddonCaches()
    {
        AddonNames.Clear();
        foreach (var addonName in AddonNameToElement.Keys)
            AddonNames.Add(addonName);

        RebuildRuleTargetCache();
        RebuildHoverCaches();
    }

    private void RebuildRuleTargetCache()
    {
        RuleTargetCache.Clear();
        foreach (var element in AllElements)
        {
            if (element.ShouldIgnoreElement())
                continue;

            RuleTargetCache[element] = CreateRuleTarget(element);
        }
    }

    private void RebuildHoverCaches()
    {
        HoverAddonNames.Clear();
        HoverGroupAddonCaches.Clear();

        var hoverElements = new HashSet<Element>();
        foreach (var element in AllElements)
        {
            if (element.ShouldIgnoreElement())
                continue;

            if (HasHoverRule(element))
                hoverElements.Add(element);
        }

        foreach (var group in Config.HoverGroups)
        {
            if (!group.LinkHover)
                continue;

            var groupHasHoverRule = false;
            foreach (var element in group.Elements)
            {
                if (!hoverElements.Contains(element))
                    continue;

                groupHasHoverRule = true;
                break;
            }

            if (!groupHasHoverRule)
                continue;

            foreach (var element in group.Elements)
                hoverElements.Add(element);
        }

        foreach (var kvp in AddonNameToElement)
        {
            if (hoverElements.Contains(kvp.Value))
                HoverAddonNames.Add(kvp.Key);
        }

        foreach (var group in Config.HoverGroups)
        {
            if (!group.LinkHover)
                continue;

            var addonNames = new List<string>();
            foreach (var kvp in AddonNameToElement)
            {
                if (group.Elements.Contains(kvp.Value) && hoverElements.Contains(kvp.Value))
                    addonNames.Add(kvp.Key);
            }

            if (addonNames.Count > 0)
                HoverGroupAddonCaches.Add(new HoverGroupAddonCache([.. addonNames]));
        }
    }

    private bool HasHoverRule(Element element)
    {
        if (!RuleTargetCache.TryGetValue(element, out var ruleTarget))
            ruleTarget = CreateRuleTarget(element);

        foreach (var rule in ruleTarget.Rules)
        {
            if (rule.state == State.Hover)
                return true;
        }

        return false;
    }
}
