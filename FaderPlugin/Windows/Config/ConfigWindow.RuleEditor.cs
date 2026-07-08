using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using faderPlugin.Data;
using faderPlugin.Resources;
using FaderPlugin.Data;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FaderPlugin.Windows.Config;

public partial class ConfigWindow
{
    private void DrawRuleEditor(string targetId, List<ConfigEntry> rules, bool isDisabled, Action<bool> setDisabled, FadeOverride fadeOverride, Action save)
    {
        if (rules.Count == 0)
            rules.Add(new ConfigEntry(State.Default, Setting.Show));

        for (var i = 0; i < rules.Count; i++)
        {
            var elementState = rules[i].state;

            var itemWidth = 200.0f * ImGuiHelpers.GlobalScale;
            ImGui.SetNextItemWidth(itemWidth);

            var stateName = StateUtil.GetStateName(elementState);
            if (elementState == State.Default)
            {
                ImGui.NewLine();
                var pos = ImGui.GetCursorPos();
                ImGui.TextUnformatted(stateName);
                ImGui.SetCursorPos(pos with { X = pos.X + itemWidth + ImGui.GetStyle().ItemSpacing.X });
            }
            else
            {
                using (var combo = ImRaii.Combo($"##{targetId}-{i}-state", stateName))
                {
                    if (combo.Success)
                    {
                        foreach (var state in StateUtil.OrderedStates)
                        {
                            if (state is State.None or State.Default)
                                continue;

                            if (ImGui.Selectable(StateUtil.GetStateName(state)))
                            {
                                rules[i].state = state;
                                save();
                            }
                        }
                    }
                }

                ImGui.SameLine();
            }

            var opacity = rules[i].Opacity;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(itemWidth);
            if (ImGui.SliderFloat($"##{targetId}-{i}-opacity", ref opacity, 0.0f, 1.0f, $"{Language.Opacity}: %.2f"))
            {
                rules[i].Opacity = opacity;
                save();
            }

            ImGui.SameLine();
            if (rules[i].state == State.Default)
            {
                var disabled = isDisabled;
                if (ImGui.Checkbox($"##{targetId}-disabled", ref disabled))
                {
                    isDisabled = disabled;
                    setDisabled(disabled);
                    save();
                }
                ImGui.SameLine();
                ImGui.TextUnformatted(Language.SettingsDisable);
                ImGuiComponents.HelpMarker(Language.SettingsDisableTooltip);
            }

            if (elementState != State.Default)
            {
                ImGui.SameLine();
                using var innerFont = ImRaii.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{FontAwesomeIcon.ArrowUp.ToIconString()}##{targetId}-{i}-up"))
                {
                    if (i > 0)
                    {
                        var swap1 = rules[i - 1];
                        var swap2 = rules[i];
                        if (swap1.state != State.Default && swap2.state != State.Default)
                        {
                            rules[i] = swap1;
                            rules[i - 1] = swap2;
                            save();
                        }
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button($"{FontAwesomeIcon.ArrowDown.ToIconString()}##{targetId}-{i}-down"))
                {
                    if (i < rules.Count - 1)
                    {
                        var swap1 = rules[i + 1];
                        var swap2 = rules[i];
                        if (swap1.state != State.Default && swap2.state != State.Default)
                        {
                            rules[i] = swap1;
                            rules[i + 1] = swap2;
                            save();
                        }
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button($"{FontAwesomeIcon.TrashAlt.ToIconString()}##{targetId}-{i}-delete"))
                {
                    rules.RemoveAt(i);
                    save();
                }
            }
        }

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button($"{FontAwesomeIcon.Plus.ToIconString()}##{targetId}-add"))
            {
                rules.Add(new ConfigEntry(State.None, Setting.Show));
                var swap1 = rules[^1];
                var swap2 = rules[^2];
                rules[^2] = swap1;
                rules[^1] = swap2;
                save();
            }
        }

        var hoverPresent = rules.Any(e => e.state == State.Hover);
        if (isDisabled && hoverPresent)
        {
            Helper.TextColored(ImGuiColors.DalamudRed, Language.StateWarning);
        }
        else
        {
            ImGui.NewLine();
        }

        using var overrideTable = ImRaii.Table($"{targetId}-FadeOverrideTable", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings);
        if (!overrideTable.Success)
            return;

        ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 200.0f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, 200.0f * ImGuiHelpers.GlobalScale);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsFadeOverride);
        ImGui.SameLine();
        var useOverride = fadeOverride.UseCustomFadeTimes;
        if (ImGui.Checkbox($"##{targetId}-fadeOverride", ref useOverride))
        {
            fadeOverride.UseCustomFadeTimes = useOverride;
            save();
        }
        ImGui.TableNextColumn();
        if (!useOverride)
            return;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsEnterTransition);
        ImGuiComponents.HelpMarker(Language.SettingsEnterTransitionTooltip);
        ImGui.TableNextColumn();

        var fadeItemWidth = 200.0f * ImGuiHelpers.GlobalScale;
        ImGui.SetNextItemWidth(fadeItemWidth);
        var fadeInTime = fadeOverride.EnterTransitionSpeedOverride > AlphaTolerance
            ? (1.0f / fadeOverride.EnterTransitionSpeedOverride) * 1000.0f
            : 1000.0f;
        if (Helper.SliderFloatDiscrete($"##{targetId}-fadeIn", ref fadeInTime, 10.0f, 2000.0f, 10.0f, "{0:0} ms"))
        {
            fadeOverride.EnterTransitionSpeedOverride = 1000.0f / fadeInTime;
            save();
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsExitTransition);
        ImGuiComponents.HelpMarker(Language.SettingsExitTransitionTooltip);
        ImGui.TableNextColumn();

        ImGui.SetNextItemWidth(fadeItemWidth);
        var fadeOutTime = fadeOverride.ExitTransitionSpeedOverride > AlphaTolerance
            ? (1.0f / fadeOverride.ExitTransitionSpeedOverride) * 1000.0f
            : 1000.0f;
        if (Helper.SliderFloatDiscrete($"##{targetId}-fadeOut", ref fadeOutTime, 10.0f, 2000.0f, 10.0f, "{0:0} ms"))
        {
            fadeOverride.ExitTransitionSpeedOverride = 1000.0f / fadeOutTime;
            save();
        }
    }
}
