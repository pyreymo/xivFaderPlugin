using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using faderPlugin.Resources;
using FaderPlugin.Data;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace FaderPlugin.Windows.Config;

public partial class ConfigWindow
{
    private List<ConfigEntry> SelectedConfig = [];
    private Element? SelectedElement;
    private const float AlphaTolerance = 1f / 255f;
    private Constants.OverrideKeys CurrentOverrideKey => (Constants.OverrideKeys)Configuration.OverrideKey;
    private const string ElementTooltipIndicator = "   ?";

    private void Settings()
    {
        using var tabItem = ImRaii.TabItem(Language.TabSettings);
        if (!tabItem.Success)
            return;

        // Layout for element selection + config
        var startPos = ImGui.GetCursorPos();
        var style = ImGui.GetStyle();
        var (buttonWidth, childSize) = GetRuleTargetListSizes();

        #region Left Child : Element Selection
        using (var child = ImRaii.Child("RuleTargetList", new Vector2(childSize, 0), true))
        {
            if (child.Success)
            {
                DrawGroupList(buttonWidth);

                ImGui.Separator();

                var elementsExpanded = ImGui.CollapsingHeader($"{Language.RuleElementsHeader}##RuleElements", ImGuiTreeNodeFlags.None);
                if (elementsExpanded)
                {
                    foreach (var element in ElementUtil.OrderedElements)
                    {
                        if (element.ShouldIgnoreElement())
                            continue;

                        var buttonText = GetElementListButtonText(element);
                        var tooltipText = element.TooltipForElement();

                        using var pushedStyle = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));

                        var desiredButtonColor = SelectedElement == element
                            ? ImGui.GetColorU32(ImGuiCol.ButtonActive)
                            : ImGui.GetColorU32(ImGuiCol.Button);

                        var hasScrollbar = ImGui.GetScrollMaxY() > 0.0f;
                        using var pushedColor = ImRaii.PushColor(ImGuiCol.Button, desiredButtonColor);
                        if (ImGui.Button(buttonText, new Vector2(buttonWidth - (hasScrollbar ? style.ScrollbarSize : 0.0f), 0)))
                        {
                            ClearGroupSelection();
                            SelectElement(element);
                        }

                        if (ImGui.IsItemHovered())
                        {
                            if (!string.IsNullOrEmpty(tooltipText))
                                Helper.Tooltip(tooltipText);

                            DrawAddonBounds(ElementUtil.GetAddonName(element));
                        }
                    }
                }
            }
        }
        #endregion

        #region Right Child : Element Configuration
        ImGui.SetCursorPos(startPos with { X = startPos.X + childSize });
        using var contentChild = ImRaii.Child("ConfigPage", Vector2.Zero, true);

        if (!contentChild.Success)
            return;

        if (SelectedGroupIndex >= 0)
        {
            DrawSelectedGroupSettings();
            return;
        }

        if (SelectedElement == null)
            return;

        var selectedElement = SelectedElement.Value;
        var elementName = ElementUtil.GetElementName(selectedElement);

        ImGui.TextUnformatted(Language.SettingsElementConfiguration.Format(elementName));

        if (DrawSharedRuleElementNotice(selectedElement))
            return;

        // Draw each condition row
        for (var i = 0; i < SelectedConfig.Count; i++)
        {
            var elementState = SelectedConfig[i].state;

            // State
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
                using (var combo = ImRaii.Combo($"##{elementName}-{i}-state", stateName))
                {
                    if (combo.Success)
                    {
                        foreach (var state in StateUtil.OrderedStates)
                        {
                            if (state is State.None or State.Default)
                                continue;

                            if (ImGui.Selectable(StateUtil.GetStateName(state)))
                            {
                                SelectedConfig[i].state = state;
                                SaveSelectedElementConfig();
                            }
                        }
                    }
                }

                ImGui.SameLine();
            }

            // Opacity
            var opacity = SelectedConfig[i].Opacity;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(itemWidth);
            if (ImGui.SliderFloat($"##{elementName}-{i}-opacity", ref opacity, 0.0f, 1.0f, $"{Language.Opacity}: %.2f"))
            {
                SelectedConfig[i].Opacity = opacity;
                SaveSelectedElementConfig();
            }

            // Default Disabled Checkbox
            ImGui.SameLine();
            if (SelectedConfig[i].state == State.Default)
            {
                var isDisabled = Configuration.DisabledElements.TryGetValue(selectedElement, out var disabled) && disabled;
                if (ImGui.Checkbox($"##{elementName}-disabled", ref isDisabled))
                {
                    Configuration.DisabledElements[selectedElement] = isDisabled;
                    SaveSelectedElementConfig();
                }
                ImGui.SameLine();
                ImGui.TextUnformatted(Language.SettingsDisable);
                ImGuiComponents.HelpMarker(Language.SettingsDisableTooltip);
            }

            // If not default, show reordering & delete buttons
            if (elementState != State.Default)
            {
                ImGui.SameLine();
                using var innerFont = ImRaii.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{FontAwesomeIcon.ArrowUp.ToIconString()}##{elementName}-{i}-up"))
                {
                    if (i > 0)
                    {
                        var swap1 = SelectedConfig[i - 1];
                        var swap2 = SelectedConfig[i];
                        if (swap1.state != State.Default && swap2.state != State.Default)
                        {
                            SelectedConfig[i] = swap1;
                            SelectedConfig[i - 1] = swap2;
                            SaveSelectedElementConfig();
                        }
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button($"{FontAwesomeIcon.ArrowDown.ToIconString()}##{elementName}-{i}-down"))
                {
                    if (i < SelectedConfig.Count - 1)
                    {
                        var swap1 = SelectedConfig[i + 1];
                        var swap2 = SelectedConfig[i];
                        if (swap1.state != State.Default && swap2.state != State.Default)
                        {
                            SelectedConfig[i] = swap1;
                            SelectedConfig[i + 1] = swap2;
                            SaveSelectedElementConfig();
                        }
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button($"{FontAwesomeIcon.TrashAlt.ToIconString()}##{elementName}-{i}-delete"))
                {
                    SelectedConfig.RemoveAt(i);
                    SaveSelectedElementConfig();
                }
            }
        }

        // Add new condition row
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button($"{FontAwesomeIcon.Plus.ToIconString()}##{elementName}-add"))
            {
                SelectedConfig.Add(new ConfigEntry(State.None, Setting.Show));
                var swap1 = SelectedConfig[^1];
                var swap2 = SelectedConfig[^2];
                SelectedConfig[^2] = swap1;
                SelectedConfig[^1] = swap2;
                SaveSelectedElementConfig();
            }
        }

        // Warning Label
        var defaultDisabled = Configuration.DisabledElements.TryGetValue(selectedElement, out var isElementDisabled) && isElementDisabled;
        var hoverPresent = SelectedConfig.Any(e => e.state == State.Hover);

        if (defaultDisabled && hoverPresent)
        {
            Helper.TextColored(ImGuiColors.DalamudRed, Language.StateWarning);
        }
        else
        {   // spacing & prevents Layout shift when the warning appears
            ImGui.NewLine();
        }
        // Fade Setting Overrides
        using var overrideTable = ImRaii.Table("FadeOverrideTable", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings);
        if (overrideTable.Success)
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 200.0f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, 200.0f * ImGuiHelpers.GlobalScale);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Language.SettingsFadeOverride);
            ImGui.SameLine();
            var useOverride = Configuration.FadeOverrides[selectedElement].UseCustomFadeTimes;
            if (ImGui.Checkbox($"##{elementName}-fadeOverride", ref useOverride))
            {
                Configuration.FadeOverrides[selectedElement].UseCustomFadeTimes = useOverride;
                Configuration.Save();
            }
            ImGui.TableNextColumn();
            if (useOverride)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(Language.SettingsEnterTransition);
                ImGuiComponents.HelpMarker(Language.SettingsEnterTransitionTooltip);
                ImGui.TableNextColumn();

                var itemWidth = 200.0f * ImGuiHelpers.GlobalScale;
                ImGui.SetNextItemWidth(itemWidth);
                var fadeInTime = Configuration.FadeOverrides[selectedElement].EnterTransitionSpeedOverride > AlphaTolerance
                    ? (1.0f / Configuration.FadeOverrides[selectedElement].EnterTransitionSpeedOverride) * 1000.0f
                    : 1000.0f;
                if (Helper.SliderFloatDiscrete($"##{elementName}-fadeIn", ref fadeInTime, 10.0f, 2000.0f, 10.0f, "{0:0} ms"))
                {
                    Configuration.FadeOverrides[selectedElement].EnterTransitionSpeedOverride = 1000.0f / fadeInTime;
                    Configuration.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(Language.SettingsExitTransition);
                ImGuiComponents.HelpMarker(Language.SettingsExitTransitionTooltip);
                ImGui.TableNextColumn();

                ImGui.SetNextItemWidth(itemWidth);
                var fadeOutTime = Configuration.FadeOverrides[selectedElement].ExitTransitionSpeedOverride > AlphaTolerance
                    ? (1.0f / Configuration.FadeOverrides[selectedElement].ExitTransitionSpeedOverride) * 1000.0f
                    : 1000.0f;
                if (Helper.SliderFloatDiscrete($"##{elementName}-fadeOut", ref fadeOutTime, 10.0f, 2000.0f, 10.0f, "{0:0} ms"))
                {
                    Configuration.FadeOverrides[selectedElement].ExitTransitionSpeedOverride = 1000.0f / fadeOutTime;
                    Configuration.Save();
                }
            }
        }
    }

    #endregion

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

    private (float ButtonWidth, float ChildWidth) GetRuleTargetListSizes()
    {
        var style = ImGui.GetStyle();

        var elementWidths = ElementUtil.OrderedElements
            .Where(element => !element.ShouldIgnoreElement())
            .Select(element => ImGui.CalcTextSize(GetElementListButtonText(element)).X);

        var groupWidths = Configuration.HoverGroups
            .Select(group => ImGui.CalcTextSize(GetGroupListButtonText(group)).X);

        var maxTextWidth = elementWidths
            .Concat(groupWidths)
            .Append(ImGui.CalcTextSize(Language.RuleGroupsHeader).X + style.ItemSpacing.X + ImGui.GetFrameHeight())
            .Append(ImGui.CalcTextSize(Language.RuleElementsHeader).X)
            .DefaultIfEmpty(0.0f)
            .Max();

        var buttonWidth = maxTextWidth + style.FramePadding.X * 2 + style.ScrollbarSize;
        var childWidth = buttonWidth + style.WindowPadding.X * 2;
        return (buttonWidth, childWidth);
    }

    private static string GetElementListButtonText(Element element)
    {
        var elementName = ElementUtil.GetElementName(element);
        var tooltipText = element.TooltipForElement();

        return string.IsNullOrEmpty(tooltipText) ? elementName : $"{elementName}{ElementTooltipIndicator}";
    }
}
