using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FaderPlugin.Animation;
using faderPlugin.Data;
using FaderPlugin.Data;
using faderPlugin.Resources;
using FaderPlugin.Windows.Config;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace FaderPlugin;

public partial class Plugin : IDalamudPlugin
{
    // Plugin services
    [PluginService]
    public static IDalamudPluginInterface PluginInterface { get; set; } = null!;

    [PluginService]
    public static IKeyState KeyState { get; set; } = null!;

    [PluginService]
    public static IFramework Framework { get; set; } = null!;

    [PluginService]
    public static IClientState ClientState { get; set; } = null!;

    [PluginService]
    public static ICondition Condition { get; set; } = null!;

    [PluginService]
    public static ICommandManager CommandManager { get; set; } = null!;

    [PluginService]
    public static IChatGui ChatGui { get; set; } = null!;

    [PluginService]
    public static IGameGui GameGui { get; set; } = null!;

    [PluginService]
    public static ITargetManager TargetManager { get; set; } = null!;

    [PluginService]
    public static IDataManager Data { get; private set; } = null!;

    [PluginService]
    public static IGamepadState GamepadState { get; private set; } = null!;

    // Configuration and windows.
    public readonly Configuration Config;
    private readonly WindowSystem WindowSystem = new("Fader");
    private readonly ConfigWindow ConfigWindow;

    // State maps and timers.
    private readonly Dictionary<State, bool> StateMap = [];
    private bool StateChanged;
    private long LastChatActivity = Environment.TickCount64;
    private readonly Dictionary<string, bool> AddonHoverStates = [];
    private readonly HashSet<string> CurrentHoveredAddons = [];
    private readonly HashSet<string> OriginalHoveredAddons = [];
    private readonly HashSet<string> PreviousHoveredAddons = [];
    private readonly Dictionary<string, Element> AddonNameToElement = [];
    private readonly List<string> AddonNames = [];
    private readonly List<string> HoverAddonNames = [];
    private readonly List<HoverGroupAddonCache> HoverGroupAddonCaches = [];
    private bool ConfigChanged;
    private bool HudManagerPrevOpened = false;

    // Opacity Management
    private readonly Dictionary<string, float> CurrentAlphas = [];
    private readonly Dictionary<string, float> TargetAlphas = [];
    private readonly Dictionary<string, bool> AddonDisabled = [];
    private readonly Dictionary<Element, RuleTarget> RuleTargetCache = [];

    // smallest possible alpha change
    private const float AlphaTolerance = 1f / 255f;

    // Tween management
    private readonly Dictionary<string, Tween> Tweens = [];
    private readonly List<string> CompletedTweens = [];

    // Commands
    private const string CommandName = "/pfader";
    private bool Enabled = true;

    // Territory Excel sheet.
    private readonly ExcelSheet<TerritoryType> TerritorySheet;

    // Delay management Utility
    private readonly Dictionary<string, long> DelayTimers = [];
    private readonly Dictionary<string, ConfigEntry> LastNonDefaultEntry = [];

    private readonly record struct RuleTarget(IReadOnlyList<ConfigEntry> Rules, bool Disabled, FadeOverride FadeOverride);

    private sealed record HoverGroupAddonCache(string[] AddonNames);

    // Enum Cache
    private static readonly Element[] AllElements = Enum.GetValues<Element>();
    private static readonly State[] AllStates = Enum.GetValues<State>();

    public Plugin()
    {
        LoadConfig(out Config);
        LanguageChanged(PluginInterface.UiLanguage);

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        TerritorySheet = Data.GetExcelSheet<TerritoryType>();

        Framework.Update += OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenMainUi += DrawConfigUi;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUi;

        CommandManager.AddHandler(
            CommandName,
            new CommandInfo(FaderCommandHandler)
            {
                HelpMessage = "Opens settings\n't' toggles whether it's enabled.\n'on' enables the plugin\n'off' disables the plugin.",
            }
        );

        foreach (var state in AllStates)
            StateMap[state] = state == State.Default;

        foreach (var element in AllElements)
        {
            if (element.ShouldIgnoreElement())
                continue;

            var addonNames = ElementUtil.GetAddonName(element);
            foreach (var addonName in addonNames)
            {
                AddonNameToElement.TryAdd(addonName, element);
            }
        }
        RebuildAddonCaches();

        ChatGui.ChatMessageUnhandled += OnChatMessage;
        PluginInterface.LanguageChanged += LanguageChanged;
        Config.OnSave += OnConfigChanged;

        // Recover from previous misconfiguration
        if (Config.DefaultDelay == 0)
            Config.DefaultDelay = 2000;
    }

    public void Dispose()
    {
        // Stop receiving callbacks first.
        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenMainUi -= DrawConfigUi;
        PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUi;

        PluginInterface.LanguageChanged -= LanguageChanged;
        Framework.Update -= OnFrameworkUpdate;
        ChatGui.ChatMessageUnhandled -= OnChatMessage;
        Config.OnSave -= OnConfigChanged;

        CommandManager.RemoveHandler(CommandName);

        // Restore game state before releasing the plugin UI.
        RestoreGameOpacity();

        WindowSystem.RemoveWindow(ConfigWindow);
        ConfigWindow.Dispose();
    }

    private void LanguageChanged(string langCode)
    {
        Language.Culture = new CultureInfo(langCode);
    }

    private static void LoadConfig(out Configuration configuration)
    {
        var existingConfig = PluginInterface.GetPluginConfig();
        configuration = (existingConfig is { Version: 6 }) ? (Configuration)existingConfig : new Configuration();
        configuration.Initialize();
    }

    private void DrawUi() => WindowSystem.Draw();

    private void DrawConfigUi() => ConfigWindow.Toggle();

    private void FaderCommandHandler(string s, string arguments)
    {
        switch (arguments.Trim())
        {
            case "t" or "toggle":
                Enabled = !Enabled;
                if (!Enabled)
                    RestoreGameOpacity();
                ChatGui.Print(Enabled ? Language.ChatPluginEnabled : Language.ChatPluginDisabled);
                break;
            case "on":
                Enabled = true;
                ChatGui.Print(Language.ChatPluginEnabled);
                break;
            case "off":
                Enabled = false;
                RestoreGameOpacity();
                ChatGui.Print(Language.ChatPluginDisabled);
                break;
            case "":
                ConfigWindow.Toggle();
                break;
        }
    }

    private void OnConfigChanged()
    {
        ConfigChanged = true;
        RebuildAddonCaches();
    }
}
