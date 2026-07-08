using System;
using System.Collections.Generic;
using FaderPlugin.Animation;
using faderPlugin.Data;
using FaderPlugin.Data;

namespace FaderPlugin;

public partial class Plugin
{
    private void RecalculateAddonOpacityTargets()
    {
        // If delay is disabled, clear any stored delay state.
        if (!Config.DefaultDelayEnabled)
        {
            DelayTimers.Clear();
            LastNonDefaultEntry.Clear();
        }

        var now = Environment.TickCount64;
        foreach (var addonName in AddonNames)
        {
            var element = AddonNameToElement[addonName];
            var ruleTarget = ResolveRuleTarget(element);
            AddonDisabled[addonName] = ruleTarget.Disabled;
            var currentAddonHovered = AddonHoverStates.TryGetValue(addonName, out var hovered) && hovered;

            var candidate = GetCandidateConfig(addonName, ruleTarget.Rules, currentAddonHovered);
            var currentAlpha = CurrentAlphas.TryGetValue(addonName, out var alpha) ? alpha : Config.DefaultAlpha;
            var targetAlpha = GetTargetAlpha(addonName, candidate, currentAddonHovered, currentAlpha);

            TargetAlphas[addonName] = targetAlpha;

            if (Math.Abs(currentAlpha - targetAlpha) <= AlphaTolerance)
            {
                CurrentAlphas[addonName] = targetAlpha;
                Addon.SetAddonOpacity(addonName, targetAlpha);
                Tweens.Remove(addonName);
                UpdateAddonVisibility(addonName, ruleTarget.Disabled);
                continue;
            }

            // animation
            var transitionSpeed = GetTransitionSpeed(ruleTarget, currentAlpha, targetAlpha);
            var duration = transitionSpeed > 0f ? (long)((1f / transitionSpeed) * 1000f) : 0L;

            if (duration <= 0)
            {
                CurrentAlphas[addonName] = targetAlpha;
                Addon.SetAddonOpacity(addonName, targetAlpha);
                Tweens.Remove(addonName);
            }
            else
            {
                if (!Tweens.TryGetValue(addonName, out var tween) || Math.Abs(tween.EndValue - targetAlpha) > AlphaTolerance)
                {
                    tween = new Tween(currentAlpha, targetAlpha, now, duration, Easing.Linear);
                    Tweens[addonName] = tween;
                }
            }

            UpdateAddonVisibility(addonName, ruleTarget.Disabled);
        }
    }

    private RuleTarget ResolveRuleTarget(Element element)
    {
        if (RuleTargetCache.TryGetValue(element, out var ruleTarget))
            return ruleTarget;

        return CreateRuleTarget(element);
    }

    private RuleTarget CreateRuleTarget(Element element)
    {
        foreach (var group in Config.HoverGroups)
        {
            if (group.SharedRules && group.Elements.Contains(element))
                return new RuleTarget(group.GetRuleEntries(), group.Disabled, group.FadeOverride);
        }

        var disabled = Config.DisabledElements.TryGetValue(element, out var isDisabled) && isDisabled;
        var fadeOverride = Config.FadeOverrides.TryGetValue(element, out var elementFadeOverride)
            ? elementFadeOverride
            : new FadeOverride();

        return new RuleTarget(Config.GetElementConfig(element), disabled, fadeOverride);
    }

    private void AdvanceTweens()
    {
        if (Tweens.Count == 0)
            return;

        CompletedTweens.Clear();
        var now = Environment.TickCount64;

        foreach (var kvp in Tweens)
        {
            var addonName = kvp.Key;
            var tween = kvp.Value;
            var newAlpha = tween.Value(now);

            if (tween.IsComplete(now))
            {
                newAlpha = tween.EndValue;
                CompletedTweens.Add(addonName);
            }

            CurrentAlphas[addonName] = newAlpha;
            Addon.SetAddonOpacity(addonName, newAlpha);

            var disabled = AddonDisabled.TryGetValue(addonName, out var isDisabled) && isDisabled;
            UpdateAddonVisibility(addonName, disabled);
        }

        foreach (var addonName in CompletedTweens)
            Tweens.Remove(addonName);
    }

    private void UpdateAddonVisibility(string addonName, bool disabled)
    {
        var currentAlpha = CurrentAlphas.TryGetValue(addonName, out var alpha) ? alpha : Config.DefaultAlpha;
        var shouldHide = disabled && currentAlpha < 0.05f;
        Addon.SetAddonVisibility(addonName, !shouldHide);
    }

    private ConfigEntry GetCandidateConfig(string addonName, IReadOnlyList<ConfigEntry> elementConfig, bool isHovered)
    {
        // Prefer Hover state when applicable.
        var candidate = isHovered ? FindRule(elementConfig, State.Hover) : null;

        // Fallback: choose an active non-hover state or default.
        candidate ??= FindActiveNonHoverRule(elementConfig) ?? FindRule(elementConfig, State.Default);

        var now = Environment.TickCount64;
        if (candidate != null && candidate.state != State.Default)
        {
            // Record the non-default state with a timestamp.
            DelayTimers[addonName] = now;
            LastNonDefaultEntry[addonName] = candidate;
        }
        else if (candidate != null && candidate.state == State.Default && Config.DefaultDelayEnabled)
        {
            // Check if there's a recent non-default state that should be used.
            if (DelayTimers.TryGetValue(addonName, out var start) && (now - start) < Config.DefaultDelay)
            {
                if (LastNonDefaultEntry.TryGetValue(addonName, out var nonDefault))
                {
                    candidate = nonDefault;
                }
            }
            else
            {
                // Delay expired; clear stored values.
                DelayTimers.Remove(addonName);
                LastNonDefaultEntry.Remove(addonName);
            }
        }

        return candidate!;
    }

    private static ConfigEntry? FindRule(IReadOnlyList<ConfigEntry> rules, State state)
    {
        foreach (var rule in rules)
        {
            if (rule.state == state)
                return rule;
        }

        return null;
    }

    private ConfigEntry? FindActiveNonHoverRule(IReadOnlyList<ConfigEntry> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.state != State.Hover && StateMap[rule.state])
                return rule;
        }

        return null;
    }

    /// <summary>
    /// Returns the native Opacity of an addon when Relative Opacity is enabled. Returns 1f when disabled.
    /// </summary>
    private float GetAlphaModifier(string addonName)
    {
        if (!Config.RelativeOpacity)
            return 1f;

        return Addon.GetSavedOpacity(addonName);
    }

    private float GetTargetAlpha(string addonName, ConfigEntry candidate, bool currentAddonHovered, float currentAlpha)
    {
        var anyAddonHovered = candidate.state == State.Hover;
        // if any addon is hovered and it isn't the current addon then keep the opacity the same for the current addon otherwise go to target
        var baseAlpha = anyAddonHovered && !currentAddonHovered ? currentAlpha : candidate.Opacity;

        var alphaModifier = GetAlphaModifier(addonName);
        var targetAlpha = baseAlpha * alphaModifier;

        if (anyAddonHovered)
        {
            var fullAlpha = candidate.Opacity * alphaModifier;
            if (currentAlpha < fullAlpha - AlphaTolerance)
            {
                // override targetAlpha so that current addon doesn't get locked at currentAlpha when another addon is hovered
                return fullAlpha;
            }
        }
        return targetAlpha;
    }

    private float GetTransitionSpeed(RuleTarget ruleTarget, float currentAlpha, float targetAlpha)
    {
        if (ruleTarget.FadeOverride.UseCustomFadeTimes)
        {
            return targetAlpha > currentAlpha
                ? ruleTarget.FadeOverride.EnterTransitionSpeedOverride
                : ruleTarget.FadeOverride.ExitTransitionSpeedOverride;
        }

        return targetAlpha > currentAlpha ? Config.EnterTransitionSpeed : Config.ExitTransitionSpeed;
    }

    /// <summary>
    /// Forces all elements to be visible and return to using In-Game opacity values.
    /// </summary>
    private void RestoreGameOpacity()
    {
        foreach (var kvp in AddonNameToElement)
        {
            var addonName = kvp.Key;
            var savedOpacity = Addon.GetSavedOpacity(addonName);
            CurrentAlphas[addonName] = savedOpacity;
            TargetAlphas[addonName] = savedOpacity;
            Addon.SetAddonOpacity(addonName, savedOpacity);
            Addon.SetAddonVisibility(addonName, true);
            Tweens.Remove(addonName);
        }
    }
}
