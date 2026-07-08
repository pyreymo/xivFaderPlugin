using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using faderPlugin.Data;
using FaderPlugin.Data;
using faderPlugin.Resources;

namespace FaderPlugin.Windows.Config;

public partial class ConfigWindow
{
    private const float RuleStateColumnWidth = 200.0f;
    private const float FadeControlColumnWidth = 200.0f;

    private void DrawRuleEditor(
        string targetId,
        List<ConfigEntry> rules,
        bool isDisabled,
        Action<bool> setDisabled,
        FadeOverride fadeOverride,
        Action save
    )
    {
        EnsureDefaultRule(rules);
        using var id = ImRaii.PushId(targetId);

        DrawDefaultRule(rules, save);
        DrawConditionalRuleSection(rules, save);
        ImGui.Spacing();

        var disabled = isDisabled;
        if (ImGui.Checkbox(Language.SettingsDisable, ref disabled))
        {
            isDisabled = disabled;
            setDisabled(disabled);
            save();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Language.SettingsDisableTooltip);

        if (isDisabled && rules.Any(rule => rule.state == State.Hover))
        {
            ImGui.SameLine();
            Helper.TextColored(ImGuiColors.WarningForeground, Language.StateWarning);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawFadeOverrideEditor(fadeOverride, save);
    }

    private static void EnsureDefaultRule(List<ConfigEntry> rules)
    {
        if (rules.All(rule => rule.state != State.Default))
            rules.Add(new ConfigEntry(State.Default, Setting.Show));
    }

    private void DrawConditionalRuleSection(List<ConfigEntry> rules, Action save)
    {
        var conditionalIndexes = rules
            .Select((rule, index) => (rule, index))
            .Where(entry => entry.rule.state != State.Default)
            .Select(entry => entry.index)
            .ToArray();

        if (conditionalIndexes.Length > 0)
            DrawConditionalRuleTable(rules, conditionalIndexes, save);

        DrawAddRuleButton(rules, save);
    }

    private void DrawConditionalRuleTable(List<ConfigEntry> rules, int[] conditionalIndexes, Action save)
    {
        var style = ImGui.GetStyle();
        var scale = ImGuiHelpers.GlobalScale;
        var actionButtonSize = ImGui.GetFrameHeight();
        var actionColumnWidth = actionButtonSize * 3.0f + style.ItemSpacing.X * 2.0f;
        var tableFlags =
            ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.NoSavedSettings;

        using var table = ImRaii.Table("ConditionalRuleTable", 3, tableFlags);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn(Language.SettingsRuleState, ImGuiTableColumnFlags.WidthFixed, RuleStateColumnWidth * scale);
        ImGui.TableSetupColumn(Language.Opacity, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(Language.SettingsRuleActions, ImGuiTableColumnFlags.WidthFixed, actionColumnWidth);
        ImGui.TableHeadersRow();

        for (var logicalIndex = 0; logicalIndex < conditionalIndexes.Length; logicalIndex++)
        {
            var ruleIndex = conditionalIndexes[logicalIndex];
            var rule = rules[ruleIndex];
            using var rowId = ImRaii.PushId(ruleIndex);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            DrawRuleStateCombo(rule, save);

            ImGui.TableSetColumnIndex(1);
            DrawRuleOpacity(rule, save);

            ImGui.TableSetColumnIndex(2);
            var moveUp = DrawRuleActionButton(FontAwesomeIcon.ArrowUp, "move-up", logicalIndex > 0);
            ImGui.SameLine();
            var moveDown = DrawRuleActionButton(FontAwesomeIcon.ArrowDown, "move-down", logicalIndex < conditionalIndexes.Length - 1);
            ImGui.SameLine();
            var delete = DrawRuleActionButton(FontAwesomeIcon.TrashAlt, "delete", true);

            if (moveUp)
            {
                var previousRuleIndex = conditionalIndexes[logicalIndex - 1];
                (rules[previousRuleIndex], rules[ruleIndex]) = (rules[ruleIndex], rules[previousRuleIndex]);
                save();
                break;
            }

            if (moveDown)
            {
                var nextRuleIndex = conditionalIndexes[logicalIndex + 1];
                (rules[nextRuleIndex], rules[ruleIndex]) = (rules[ruleIndex], rules[nextRuleIndex]);
                save();
                break;
            }

            if (delete)
            {
                rules.RemoveAt(ruleIndex);
                save();
                break;
            }
        }
    }

    private void DrawRuleStateCombo(ConfigEntry rule, Action save)
    {
        ImGui.SetNextItemWidth(-1.0f);
        var stateName = StateUtil.GetStateName(rule.state);

        using var combo = ImRaii.Combo("##state", stateName);
        if (!combo.Success)
            return;

        foreach (var state in StateUtil.OrderedStates)
        {
            if (state is State.None or State.Default)
                continue;

            var selected = state == rule.state;
            if (ImGui.Selectable(StateUtil.GetStateName(state), selected))
            {
                rule.state = state;
                save();
            }

            if (selected)
                ImGui.SetItemDefaultFocus();
        }
    }

    private static void DrawRuleOpacity(ConfigEntry rule, Action save)
    {
        ImGui.SetNextItemWidth(-1.0f);
        var opacity = rule.Opacity;

        if (ImGui.SliderFloat("##opacity", ref opacity, 0.0f, 1.0f, $"{Language.Opacity}: %.2f"))
        {
            rule.Opacity = opacity;
            save();
        }
    }

    private static bool DrawRuleActionButton(FontAwesomeIcon icon, string id, bool enabled)
    {
        var buttonSize = new Vector2(ImGui.GetFrameHeight());
        using var disabled = ImRaii.Disabled(!enabled);
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        return ImGui.Button($"{icon.ToIconString()}##{id}", buttonSize);
    }

    private void DrawAddRuleButton(List<ConfigEntry> rules, Action save)
    {
        ImGui.Spacing();
        if (ImGui.Button($"+  {Language.SettingsAddRule}##add-rule"))
            ImGui.OpenPopup("AddRulePopup");

        using var popup = ImRaii.Popup("AddRulePopup");
        if (!popup.Success)
            return;

        foreach (var state in StateUtil.OrderedStates)
        {
            if (state is State.None or State.Default)
                continue;

            if (!ImGui.Selectable(StateUtil.GetStateName(state)))
                continue;

            var defaultIndex = rules.FindIndex(rule => rule.state == State.Default);
            if (defaultIndex < 0)
                defaultIndex = rules.Count;

            rules.Insert(defaultIndex, new ConfigEntry(state, Setting.Show));
            save();
            ImGui.CloseCurrentPopup();
        }
    }

    private void DrawDefaultRule(List<ConfigEntry> rules, Action save)
    {
        var defaultRule = rules.First(rule => rule.state == State.Default);
        var tableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings;

        using var table = ImRaii.Table("DefaultRuleTable", 3, tableFlags);
        if (!table.Success)
            return;

        var actionColumnWidth = ImGui.GetFrameHeight() * 3.0f + ImGui.GetStyle().ItemSpacing.X * 2.0f;
        ImGui.TableSetupColumn("DefaultState", ImGuiTableColumnFlags.WidthFixed, RuleStateColumnWidth * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("DefaultOpacity", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("DefaultActions", ImGuiTableColumnFlags.WidthFixed, actionColumnWidth);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(StateUtil.GetStateName(State.Default));

        ImGui.TableSetColumnIndex(1);
        DrawRuleOpacity(defaultRule, save);
    }

    private void DrawFadeOverrideEditor(FadeOverride fadeOverride, Action save)
    {
        var useOverride = fadeOverride.UseCustomFadeTimes;
        if (ImGui.Checkbox(Language.SettingsFadeOverride, ref useOverride))
        {
            fadeOverride.UseCustomFadeTimes = useOverride;
            save();
        }

        using var disabled = ImRaii.Disabled(!useOverride);
        var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings;
        using var table = ImRaii.Table("FadeOverrideTable", 2, tableFlags);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("FadeLabel", ImGuiTableColumnFlags.WidthFixed, 200.0f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("FadeControl", ImGuiTableColumnFlags.WidthFixed, FadeControlColumnWidth * ImGuiHelpers.GlobalScale);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Language.SettingsEnterTransition);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Language.SettingsEnterTransitionTooltip);

        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(FadeControlColumnWidth * ImGuiHelpers.GlobalScale);
        var fadeInTime =
            fadeOverride.EnterTransitionSpeedOverride > AlphaTolerance ? 1000.0f / fadeOverride.EnterTransitionSpeedOverride : 1000.0f;

        if (Helper.SliderFloatDiscrete("##fade-in", ref fadeInTime, 10.0f, 2000.0f, 10.0f, "{0:0} ms"))
        {
            fadeOverride.EnterTransitionSpeedOverride = 1000.0f / fadeInTime;
            save();
        }

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Language.SettingsExitTransition);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Language.SettingsExitTransitionTooltip);

        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(FadeControlColumnWidth * ImGuiHelpers.GlobalScale);
        var fadeOutTime =
            fadeOverride.ExitTransitionSpeedOverride > AlphaTolerance ? 1000.0f / fadeOverride.ExitTransitionSpeedOverride : 1000.0f;

        if (Helper.SliderFloatDiscrete("##fade-out", ref fadeOutTime, 10.0f, 2000.0f, 10.0f, "{0:0} ms"))
        {
            fadeOverride.ExitTransitionSpeedOverride = 1000.0f / fadeOutTime;
            save();
        }
    }
}
