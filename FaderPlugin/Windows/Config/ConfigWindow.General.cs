using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using faderPlugin.Resources;
using FaderPlugin.Data;
using Dalamud.Bindings.ImGui;
using System;

namespace FaderPlugin.Windows.Config;

public partial class ConfigWindow
{
    private void General()
    {
        using var tabItem = ImRaii.TabItem(Language.SettingsGeneralHeader);
        if (!tabItem.Success)
            return;

        using var table = ImRaii.Table("FaderGeneralSettingsTable", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Control", ImGuiTableColumnFlags.WidthFixed, 220.0f * ImGuiHelpers.GlobalScale);

        DrawOverrideKeySetting();
        DrawFocusOnHotbarsUnlockSetting();
        DrawEmoteActivitySetting();
        DrawImportantActivitySetting();
        DrawChatActivityTimeoutSetting();
        DrawDefaultDelaySetting();
        DrawRelativeOpacitySetting();
        DrawEnterTransitionSetting();
        DrawExitTransitionSetting();
    }

    private void DrawOverrideKeySetting()
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsFocusKey);
        ImGuiComponents.HelpMarker(Language.SettingsFocusKeyTooltip);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        using var combo = ImRaii.Combo("##UserFocusCombo", CurrentOverrideKey.ToString());
        if (!combo.Success)
            return;

        foreach (var option in Enum.GetValues<Constants.OverrideKeys>())
        {
            if (ImGui.Selectable(option.ToString(), option.Equals(CurrentOverrideKey)))
            {
                Configuration.OverrideKey = (int)option;
                Configuration.Save();
            }
        }
    }

    private void DrawFocusOnHotbarsUnlockSetting()
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsFocusHotbarUnlock);
        ImGuiComponents.HelpMarker(Language.SettingsFocusHotbarUnlockTooltip);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        var focusOnHotbarsUnlock = Configuration.FocusOnHotbarsUnlock;
        if (ImGui.Checkbox("##focus_on_unlocked_bars", ref focusOnHotbarsUnlock))
        {
            Configuration.FocusOnHotbarsUnlock = focusOnHotbarsUnlock;
            Configuration.Save();
        }
    }

    private void DrawEmoteActivitySetting()
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsEmoteActivity);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        var emoteChat = Configuration.EmoteActivity;
        if (ImGui.Checkbox("##emote_activity", ref emoteChat))
        {
            Configuration.EmoteActivity = emoteChat;
            Configuration.Save();
        }
    }

    private void DrawImportantActivitySetting()
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsSystemTrigger);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        var importChat = Configuration.ImportantActivity;
        if (ImGui.Checkbox("##important_activity", ref importChat))
        {
            Configuration.ImportantActivity = importChat;
            Configuration.Save();
        }
    }

    private void DrawChatActivityTimeoutSetting()
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsChatActivityTimeout);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        var chatActivityTimeout = (int)TimeSpan.FromMilliseconds(Configuration.ChatActivityTimeout).TotalSeconds;
        if (ImGui.SliderInt("##chat_activity_timeout", ref chatActivityTimeout, 1, 20, $"%d {Language.Seconds}"))
        {
            Configuration.ChatActivityTimeout = (int)TimeSpan.FromSeconds(chatActivityTimeout).TotalMilliseconds;
            Configuration.Save();
        }
    }

    private void DrawDefaultDelaySetting()
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsDelay);
        ImGuiComponents.HelpMarker(Language.SettingsDelayTooltip);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        var defaultDelayEnabled = Configuration.DefaultDelayEnabled;
        if (ImGui.Checkbox("##default_delay_enabled", ref defaultDelayEnabled))
        {
            Configuration.DefaultDelayEnabled = defaultDelayEnabled;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        if (!defaultDelayEnabled)
            return;

        var idleDelay = (float)TimeSpan.FromMilliseconds(Configuration.DefaultDelay).TotalSeconds;
        if (ImGui.SliderFloat("##default_delay", ref idleDelay, 0.1f, 15f, $"%.1f {Language.Seconds}"))
        {
            Configuration.DefaultDelay = (int)TimeSpan.FromSeconds(Math.Round(idleDelay, 1)).TotalMilliseconds;
            Configuration.Save();
        }
    }

    private void DrawRelativeOpacitySetting()
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsRelativeOpacity);
        ImGuiComponents.HelpMarker(Language.SettingsRelativeOpacityTooltip);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        var relativeOpacity = Configuration.RelativeOpacity;
        if (ImGui.Checkbox("##relative_opacity_enabled", ref relativeOpacity))
        {
            Configuration.RelativeOpacity = relativeOpacity;
            Configuration.Save();
        }
    }

    private void DrawEnterTransitionSetting()
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsEnterTransition);
        ImGuiComponents.HelpMarker(Language.SettingsEnterTransitionTooltip);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        var enterTransitionTimeMs = Configuration.EnterTransitionSpeed > AlphaTolerance
            ? (1.0f / Configuration.EnterTransitionSpeed) * 1000.0f
            : 1000.0f;
        if (Helper.SliderFloatDiscrete("##enter_transition_time_ms", ref enterTransitionTimeMs, 10.0f, 2000.0f, 10.0f, "{0:0} ms"))
        {
            Configuration.EnterTransitionSpeed = 1000.0f / enterTransitionTimeMs;
            Configuration.Save();
        }
    }

    private void DrawExitTransitionSetting()
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Language.SettingsExitTransition);
        ImGuiComponents.HelpMarker(Language.SettingsExitTransitionTooltip);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        var exitTransitionTimeMs = Configuration.ExitTransitionSpeed > AlphaTolerance
            ? (1.0f / Configuration.ExitTransitionSpeed) * 1000.0f
            : 1000.0f;
        if (Helper.SliderFloatDiscrete("##exit_transition_time_ms", ref exitTransitionTimeMs, 10.0f, 2000.0f, 10.0f, "{0:0} ms"))
        {
            Configuration.ExitTransitionSpeed = 1000.0f / exitTransitionTimeMs;
            Configuration.Save();
        }
    }
}
