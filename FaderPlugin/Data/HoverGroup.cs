using faderPlugin.Data;
using faderPlugin.Resources;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FaderPlugin.Data;

[Serializable]
public class HoverGroup
{
    public string GroupName { get; set; } = Language.HoverGroupNewGroup;
    public List<Element> Elements { get; set; } = [];

    public bool LinkHover { get; set; } = true;
    public bool SharedRules { get; set; }
    public bool Disabled { get; set; }

    public List<ConfigEntry> ConditionalRules { get; set; } = [];
    public ConfigEntry DefaultRule { get; set; } = CreateDefaultRule();

    public FadeOverride FadeOverride { get; set; } = new();

    public IReadOnlyList<ConfigEntry> GetRuleEntries()
    {
        return [.. ConditionalRules, DefaultRule];
    }

    public List<ConfigEntry> CreateEditableRules()
    {
        return [.. GetRuleEntries().Select(CloneRule)];
    }

    public void UpdateRules(IEnumerable<ConfigEntry> rules)
    {
        var conditionalRules = new List<ConfigEntry>();
        ConfigEntry? defaultRule = null;

        foreach (var rule in rules)
        {
            if (rule.state == State.Default)
                defaultRule = CloneRule(rule);
            else
                conditionalRules.Add(CloneRule(rule));
        }

        ConditionalRules = conditionalRules;
        DefaultRule = defaultRule
            ?? throw new InvalidOperationException("The rule list must contain a default rule.");
    }

    private static ConfigEntry CreateDefaultRule()
    {
        return new ConfigEntry(State.Default, Setting.Show)
        {
            Opacity = 1.0f,
        };
    }

    private static ConfigEntry CloneRule(ConfigEntry rule)
    {
        return new ConfigEntry(rule.state, rule.setting)
        {
            Opacity = rule.Opacity,
        };
    }
}
