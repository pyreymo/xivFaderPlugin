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

        if (SelectedConfig.All(rule => rule.state != State.Default))
            SelectedConfig.Add(new ConfigEntry(State.Default, Setting.Show));

        using var ruleEditorId = ImRaii.PushId(selectedElement.ToString());

        var defaultRule = SelectedConfig.First(rule => rule.state == State.Default);
        var defaultTableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings;

        using (var defaultTable = ImRaii.Table("DefaultRuleTable", 3, defaultTableFlags))
        {
            if (defaultTable.Success)
            {
                var actionColumnWidth = ImGui.GetFrameHeight() * 3.0f + ImGui.GetStyle().ItemSpacing.X * 2.0f;
                ImGui.TableSetupColumn("DefaultState", ImGuiTableColumnFlags.WidthFixed, RuleStateColumnWidth * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("DefaultOpacity", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("DefaultActions", ImGuiTableColumnFlags.WidthFixed, actionColumnWidth);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(StateUtil.GetStateName(State.Default));

                ImGui.TableSetColumnIndex(1);
                ImGui.SetNextItemWidth(-1.0f);
                var opacity = defaultRule.Opacity;
                if (ImGui.SliderFloat("##opacity", ref opacity, 0.0f, 1.0f, $"{Language.Opacity}: %.2f"))
                {
                    defaultRule.Opacity = opacity;
                    SaveSelectedElementConfig();
                }
            }
        }

        var conditionalIndexes = SelectedConfig
            .Select((rule, index) => (rule, index))
            .Where(entry => entry.rule.state != State.Default)
            .Select(entry => entry.index)
            .ToArray();

        if (conditionalIndexes.Length > 0)
        {
            var actionButtonSize = ImGui.GetFrameHeight();
            var actionColumnWidth = actionButtonSize * 3.0f + style.ItemSpacing.X * 2.0f;
            var conditionalTableFlags =
                ImGuiTableFlags.SizingStretchProp
                | ImGuiTableFlags.RowBg
                | ImGuiTableFlags.BordersInnerH
                | ImGuiTableFlags.NoSavedSettings;

            using var conditionalTable = ImRaii.Table("ConditionalRuleTable", 3, conditionalTableFlags);
            if (conditionalTable.Success)
            {
                ImGui.TableSetupColumn(
                    Language.SettingsRuleState,
                    ImGuiTableColumnFlags.WidthFixed,
                    RuleStateColumnWidth * ImGuiHelpers.GlobalScale
                );
                ImGui.TableSetupColumn(Language.Opacity, ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn(Language.SettingsRuleActions, ImGuiTableColumnFlags.WidthFixed, actionColumnWidth);
                ImGui.TableHeadersRow();

                for (var logicalIndex = 0; logicalIndex < conditionalIndexes.Length; logicalIndex++)
                {
                    var ruleIndex = conditionalIndexes[logicalIndex];
                    var rule = SelectedConfig[ruleIndex];
                    using var rowId = ImRaii.PushId(ruleIndex);

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.SetNextItemWidth(-1.0f);
                    var stateName = StateUtil.GetStateName(rule.state);

                    using (var combo = ImRaii.Combo("##state", stateName))
                    {
                        if (combo.Success)
                        {
                            foreach (var state in StateUtil.OrderedStates)
                            {
                                if (state is State.None or State.Default)
                                    continue;

                                var selected = state == rule.state;
                                if (ImGui.Selectable(StateUtil.GetStateName(state), selected))
                                {
                                    rule.state = state;
                                    SaveSelectedElementConfig();
                                }

                                if (selected)
                                    ImGui.SetItemDefaultFocus();
                            }
                        }
                    }

                    ImGui.TableSetColumnIndex(1);
                    ImGui.SetNextItemWidth(-1.0f);
                    var opacity = rule.Opacity;
                    if (ImGui.SliderFloat("##opacity", ref opacity, 0.0f, 1.0f, $"{Language.Opacity}: %.2f"))
                    {
                        rule.Opacity = opacity;
                        SaveSelectedElementConfig();
                    }

                    ImGui.TableSetColumnIndex(2);
                    bool moveUp;
                    using (ImRaii.Disabled(logicalIndex <= 0))
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                        moveUp = ImGui.Button($"{FontAwesomeIcon.ArrowUp.ToIconString()}##move-up", new Vector2(actionButtonSize));

                    ImGui.SameLine();
                    bool moveDown;
                    using (ImRaii.Disabled(logicalIndex >= conditionalIndexes.Length - 1))
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                        moveDown = ImGui.Button($"{FontAwesomeIcon.ArrowDown.ToIconString()}##move-down", new Vector2(actionButtonSize));

                    ImGui.SameLine();
                    bool delete;
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                        delete = ImGui.Button($"{FontAwesomeIcon.TrashAlt.ToIconString()}##delete", new Vector2(actionButtonSize));

                    if (moveUp)
                    {
                        var previousRuleIndex = conditionalIndexes[logicalIndex - 1];
                        (SelectedConfig[previousRuleIndex], SelectedConfig[ruleIndex]) =
                            (SelectedConfig[ruleIndex], SelectedConfig[previousRuleIndex]);
                        SaveSelectedElementConfig();
                        break;
                    }

                    if (moveDown)
                    {
                        var nextRuleIndex = conditionalIndexes[logicalIndex + 1];
                        (SelectedConfig[nextRuleIndex], SelectedConfig[ruleIndex]) =
                            (SelectedConfig[ruleIndex], SelectedConfig[nextRuleIndex]);
                        SaveSelectedElementConfig();
                        break;
                    }

                    if (delete)
                    {
                        SelectedConfig.RemoveAt(ruleIndex);
                        SaveSelectedElementConfig();
                        break;
                    }
                }
            }
        }

        ImGui.Spacing();
        if (ImGui.Button($"+  {Language.SettingsAddRule}##add-rule"))
            ImGui.OpenPopup("AddRulePopup");

        using (var popup = ImRaii.Popup("AddRulePopup"))
        {
            if (popup.Success)
            {
                foreach (var state in StateUtil.OrderedStates)
                {
                    if (state is State.None or State.Default)
                        continue;

                    if (!ImGui.Selectable(StateUtil.GetStateName(state)))
                        continue;

                    var defaultIndex = SelectedConfig.FindIndex(rule => rule.state == State.Default);
                    if (defaultIndex < 0)
                        defaultIndex = SelectedConfig.Count;

                    SelectedConfig.Insert(defaultIndex, new ConfigEntry(state, Setting.Show));
                    SaveSelectedElementConfig();
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        ImGui.Spacing();

        var isDisabled = Configuration.DisabledElements.TryGetValue(selectedElement, out var elementDisabled) && elementDisabled;
        if (ImGui.Checkbox(Language.SettingsDisable, ref isDisabled))
        {
            Configuration.DisabledElements[selectedElement] = isDisabled;
            SaveSelectedElementConfig();
        }

        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Language.SettingsDisableTooltip);

        if (isDisabled && SelectedConfig.Any(rule => rule.state == State.Hover))
        {
            ImGui.SameLine();
            Helper.TextColored(ImGuiColors.WarningForeground, Language.StateWarning);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var fadeOverride = Configuration.FadeOverrides[selectedElement];
        var useOverride = fadeOverride.UseCustomFadeTimes;
        if (ImGui.Checkbox(Language.SettingsFadeOverride, ref useOverride))
        {
            fadeOverride.UseCustomFadeTimes = useOverride;
            SaveSelectedElementConfig();
        }

        using var fadeControlsDisabled = ImRaii.Disabled(!useOverride);
        var fadeTableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings;
        using var fadeTable = ImRaii.Table("FadeOverrideTable", 2, fadeTableFlags);
        if (!fadeTable.Success)
            return;

        ImGui.TableSetupColumn("FadeLabel", ImGuiTableColumnFlags.WidthFixed, 200.0f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn(
            "FadeControl",
            ImGuiTableColumnFlags.WidthFixed,
            FadeControlColumnWidth * ImGuiHelpers.GlobalScale
        );

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Language.SettingsEnterTransition);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Language.SettingsEnterTransitionTooltip);

        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(FadeControlColumnWidth * ImGuiHelpers.GlobalScale);
        var fadeInTime = fadeOverride.EnterTransitionSpeedOverride > AlphaTolerance
            ? 1000.0f / fadeOverride.EnterTransitionSpeedOverride
            : 1000.0f;

        if (Helper.SliderFloatDiscrete("##fade-in", ref fadeInTime, 10.0f, 2000.0f, 10.0f, "{0:0} ms"))
        {
            fadeOverride.EnterTransitionSpeedOverride = 1000.0f / fadeInTime;
            SaveSelectedElementConfig();
        }

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Language.SettingsExitTransition);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(Language.SettingsExitTransitionTooltip);

        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(FadeControlColumnWidth * ImGuiHelpers.GlobalScale);
        var fadeOutTime = fadeOverride.ExitTransitionSpeedOverride > AlphaTolerance
            ? 1000.0f / fadeOverride.ExitTransitionSpeedOverride
            : 1000.0f;

        if (Helper.SliderFloatDiscrete("##fade-out", ref fadeOutTime, 10.0f, 2000.0f, 10.0f, "{0:0} ms"))
        {
            fadeOverride.ExitTransitionSpeedOverride = 1000.0f / fadeOutTime;
            SaveSelectedElementConfig();
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
