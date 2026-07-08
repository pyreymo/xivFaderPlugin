using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FaderPlugin.Data;
using faderPlugin.Resources;

namespace FaderPlugin.Windows.Config;

public partial class ConfigWindow
{
    private List<ConfigEntry> SelectedConfig = [];
    private Element? SelectedElement;
    private const float AlphaTolerance = 1f / 255f;
    private Constants.OverrideKeys CurrentOverrideKey => (Constants.OverrideKeys)Configuration.OverrideKey;

    private void Settings()
    {
        using var tabItem = ImRaii.TabItem(Language.TabSettings);
        if (!tabItem.Success)
            return;

        var startPos = ImGui.GetCursorPos();
        var style = ImGui.GetStyle();
        var (buttonWidth, childSize) = GetRuleTargetListSizes();

        DrawRuleTargetList(buttonWidth, childSize, style.ScrollbarSize);

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

        var isDisabled = Configuration.DisabledElements.TryGetValue(selectedElement, out var elementDisabled) && elementDisabled;
        DrawRuleEditor(
            selectedElement.ToString(),
            SelectedConfig,
            isDisabled,
            disabled => Configuration.DisabledElements[selectedElement] = disabled,
            Configuration.FadeOverrides[selectedElement],
            SaveSelectedElementConfig
        );
    }
}
