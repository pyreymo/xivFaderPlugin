using System;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Objects.Enums;
using faderPlugin.Data;
using FaderPlugin.Data;

namespace FaderPlugin;

public partial class Plugin
{
    private void OnChatMessage(IChatMessage message)
    {
        // Don't trigger chat for non-standard chat channels.
        if (
            !Constants.ActiveChatTypes.Contains(message.LogKind)
            && (!Config.ImportantActivity || !Constants.ImportantChatTypes.Contains(message.LogKind))
            && (!Config.EmoteActivity || !Constants.EmoteChatTypes.Contains(message.LogKind))
        )
            return;

        LastChatActivity = Environment.TickCount64;
    }

    private bool IsChatActive() => (Environment.TickCount64 - LastChatActivity) < Config.ChatActivityTimeout;

    private void UpdateInputStates()
    {
        UpdateState(State.UserFocus, KeyState[Config.OverrideKey] || (Config.FocusOnHotbarsUnlock && !Addon.AreHotbarsLocked()));
        UpdateState(State.AltKeyFocus, KeyState[(int)Constants.OverrideKeys.Alt]);
        UpdateState(State.CtrlKeyFocus, KeyState[(int)Constants.OverrideKeys.Ctrl]);
        UpdateState(State.ShiftKeyFocus, KeyState[(int)Constants.OverrideKeys.Shift]);
        UpdateState(State.ChatFocus, Addon.IsChatFocused());
        UpdateState(State.ChatActivity, IsChatActive());
        UpdateState(State.IsMoving, Addon.IsMoving());
        UpdateState(State.Combat, Condition[ConditionFlag.InCombat]);
        UpdateState(State.WeaponUnsheathed, Addon.IsWeaponUnsheathed());
        UpdateState(State.InSanctuary, Addon.InSanctuary());
        UpdateState(State.InFate, Addon.InFate());

        UpdateState(State.LeftTrigger, Addon.IsControllerInputHeld(GamepadButtons.L2));
        UpdateState(State.RightTrigger, Addon.IsControllerInputHeld(GamepadButtons.R2));
        UpdateState(State.LeftBumper, Addon.IsControllerInputHeld(GamepadButtons.L1));
        UpdateState(State.RightBumper, Addon.IsControllerInputHeld(GamepadButtons.R1));

        var target = TargetManager.Target;
        UpdateState(State.EnemyTarget, target?.ObjectKind == ObjectKind.BattleNpc);
        UpdateState(State.PlayerTarget, target?.ObjectKind == ObjectKind.Pc);
        UpdateState(State.NPCTarget, target?.ObjectKind == ObjectKind.EventNpc);
        UpdateState(State.GatheringNodeTarget, target?.ObjectKind == ObjectKind.GatheringPoint);
        UpdateState(State.Crafting, Condition[ConditionFlag.Crafting]);
        UpdateState(State.Gathering, Condition[ConditionFlag.Gathering]);
        UpdateState(State.Mounted, Condition[ConditionFlag.Mounted] || Condition[ConditionFlag.RidingPillion]);

        var inIslandSanctuary = (
            TerritorySheet.TryGetRow(ClientState.TerritoryType, out var territory) && territory.TerritoryIntendedUse.RowId == 49
        );
        UpdateState(State.IslandSanctuary, inIslandSanctuary);

        var boundByDuty =
            Condition[ConditionFlag.BoundByDuty] || Condition[ConditionFlag.BoundByDuty56] || Condition[ConditionFlag.BoundByDuty95];
        UpdateState(State.Duty, !inIslandSanctuary && boundByDuty);

        var occupied =
            Condition[ConditionFlag.Occupied]
            || Condition[ConditionFlag.Occupied30]
            || Condition[ConditionFlag.Occupied33]
            || Condition[ConditionFlag.Occupied38]
            || Condition[ConditionFlag.Occupied39]
            || Condition[ConditionFlag.OccupiedInCutSceneEvent]
            || Condition[ConditionFlag.OccupiedInEvent]
            || Condition[ConditionFlag.OccupiedSummoningBell]
            || Condition[ConditionFlag.OccupiedInQuestEvent];

        UpdateState(State.Occupied, occupied);
    }

    private void UpdateState(State state, bool value)
    {
        if (StateMap[state] != value)
        {
            StateMap[state] = value;
            StateChanged = true;
        }
    }
}
