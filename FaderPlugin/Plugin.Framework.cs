using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace FaderPlugin;

public partial class Plugin
{
    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!IsSafeToWork())
            return;

        var hudOpen = Addon.IsHudManagerOpen();
        var forceShow = !Enabled || hudOpen;

        if (hudOpen && !HudManagerPrevOpened)
            RestoreGameOpacity();
        HudManagerPrevOpened = hudOpen;

        if (forceShow)
            return;

        StateChanged = false;
        UpdateInputStates();
        UpdateMouseHoverState();

        if (StateChanged || ConfigChanged || AnyDelayExpired())
        {
            RecalculateAddonOpacityTargets();
            ConfigChanged = false;
        }

        AdvanceTweens();
    }

    private bool AnyDelayExpired()
    {
        var now = Environment.TickCount64;
        foreach (var timer in DelayTimers.Values)
        {
            if ((now - timer) >= Config.DefaultDelay)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if it is safe for the plugin to perform work.
    /// </summary>
    private static bool IsSafeToWork() => !Condition[ConditionFlag.BetweenAreas] && ClientState.IsLoggedIn;
}
