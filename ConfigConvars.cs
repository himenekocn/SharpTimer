using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;


namespace SharpTimer
{
    public partial class SharpTimer
    {
        [ConsoleCommand("sharptimer_mysql_enabled", "Whether player times should be put into a mysql database by default or not. Default value: false")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerMySQLConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            useMySQL = bool.TryParse(args, out bool useMySQLValue) ? useMySQLValue : args != "0" && useMySQL;
        }

        [ConsoleCommand("sharptimer_command_spam_cooldown", "Defines the time between commands can be called. Default value: 1")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerCmdCooldownConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            if (float.TryParse(args, out float cooldown) && cooldown > 0)
            {
                cmdCooldown = (int)(cooldown * 64);
                Console.WriteLine($"SharpTimer command cooldown set to {cooldown} seconds.");
            }
            else
            {
                Console.WriteLine("Invalid interval value. Please provide a positive integer.");
            }
        }

        [ConsoleCommand("sharptimer_respawn_enabled", "Whether !r is enabled by default or not. Default value: true")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerRespawnConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            respawnEnabled = bool.TryParse(args, out bool respawnEnabledValue) ? respawnEnabledValue : args != "0" && respawnEnabled;
        }

        [ConsoleCommand("sharptimer_top_enabled", "Whether !top is enabled by default or not. Default value: true")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerTopConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            topEnabled = bool.TryParse(args, out bool topEnabledValue) ? topEnabledValue : args != "0" && topEnabled;
        }

        [ConsoleCommand("sharptimer_rank_enabled", "Whether !rank is enabled by default or not. Default value: true")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerRankConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            rankEnabled = bool.TryParse(args, out bool rankEnabledValue) ? rankEnabledValue : args != "0" && rankEnabled;
        }

        [ConsoleCommand("sharptimer_remove_legs", "Whether Legs should be removed or not. Default value: true")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerRemoveLegsConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            removeLegsEnabled = bool.TryParse(args, out bool removeLegsEnabledValue) ? removeLegsEnabledValue : args != "0" && removeLegsEnabled;
        }

        [ConsoleCommand("sharptimer_remove_damage", "Whether dealing damage should be disabled or not. Default value: true")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerRemoveDamageConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            disableDamage = bool.TryParse(args, out bool disableDamageValue) ? disableDamageValue : args != "0" && disableDamage;
        }

        /* [ConsoleCommand("sharptimer_remove_collision", "Whether Player collision should be removed or not. Default value: true")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerRemoveCollisionConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            removeCollisionEnabled = bool.TryParse(args, out bool removeCollisionEnabledValue) ? removeCollisionEnabledValue : args != "0" && removeCollisionEnabled;
        } */

        [ConsoleCommand("sharptimer_checkpoints_enabled", "Whether !cp, !tp and !prevcp are enabled by default or not. Default value: false")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerCPConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            cpEnabled = bool.TryParse(args, out bool cpEnabledValue) ? cpEnabledValue : args != "0" && cpEnabled;
        }

        [ConsoleCommand("sharptimer_remove_checkpoints_restrictions", "Whether checkpoints should save in the air with the current player speed. Default value: false")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerCPRestrictConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            removeCpRestrictEnabled = bool.TryParse(args, out bool removeCpRestrictEnabledValue) ? removeCpRestrictEnabledValue : args != "0" && removeCpRestrictEnabled;
        }

        [ConsoleCommand("sharptimer_reset_teleport_trigger_speed_enabled", "Whether the players speed should loose all speed when entring a teleport map trigger or not. Default value: false")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerResetTeleportSpeedConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            resetTriggerTeleportSpeedEnabled = bool.TryParse(args, out bool resetTriggerTeleportSpeedEnabledValue) ? resetTriggerTeleportSpeedEnabledValue : args != "0" && resetTriggerTeleportSpeedEnabled;
        }

        [ConsoleCommand("sharptimer_max_start_speed_enabled", "Whether the players speed should be limited on exiting the starting trigger or not. Default value: false")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerMaxStartSpeedBoolConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            maxStartingSpeedEnabled = bool.TryParse(args, out bool maxStartingSpeedEnabledValue) ? maxStartingSpeedEnabledValue : args != "0" && maxStartingSpeedEnabled;
        }

        [ConsoleCommand("sharptimer_max_start_speed", "Defines max speed the player is allowed to have while exiting the start trigger. Default value: 120")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerMaxStartSpeedConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            if (int.TryParse(args, out int speed) && speed > 0)
            {
                maxStartingSpeed = speed;
                Console.WriteLine($"SharpTimer max trigger speed set to {speed}.");
            }
            else
            {
                Console.WriteLine("Invalid interval value. Please provide a positive integer.");
            }
        }

        [ConsoleCommand("sharptimer_connectmsg_enabled", "Whether connect/disconnect messages are enabled by default or not. Default value: true")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerConnectMSGConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            connectMsgEnabled = bool.TryParse(args, out bool connectMsgEnabledValue) ? connectMsgEnabledValue : args != "0" && connectMsgEnabled;
        }

        [ConsoleCommand("sharptimer_remove_crouch_fatigue", "Whether the player should get no crouch fatigue or not. Default value: true")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerRemoveCrouchFatigueConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            removeCrouchFatigueEnabled = bool.TryParse(args, out bool removeCrouchFatigueEnabledValue) ? removeCrouchFatigueEnabledValue : args != "0" && removeCrouchFatigueEnabled;
        }

        [ConsoleCommand("sharptimer_sr_ad_enabled", "Whether timed Server Record messages are enabled by default or not. Default value: true")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerSRConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            srEnabled = bool.TryParse(args, out bool srEnabledValue) ? srEnabledValue : args != "0" && srEnabled;
        }

        [ConsoleCommand("sharptimer_sr_ad_timer", "Interval how often SR shall be printed to chat. Default value: 120")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerMaxSpeedConvar(CCSPlayerController? player, CommandInfo command)
        {
            string args = command.ArgString;

            if (int.TryParse(args, out int interval) && interval > 0)
            {
                srTimer = interval;
                Console.WriteLine($"SharpTimer interval set to {interval} seconds.");
            }
            else
            {
                Console.WriteLine("Invalid interval value. Please provide a positive integer.");
            }
        }

        [ConsoleCommand("sharptimer_chat_prefix", "Default value of chat prefix for SharpTimer messages. Default value: [SharpTimer]")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerChatPrefix(CCSPlayerController? player, CommandInfo command)
        {

            string args = command.ArgString.Trim();

            if (string.IsNullOrEmpty(args))
            {
                msgPrefix = $" {ChatColors.Green} [SharpTimer] {ChatColors.White}";
                return;
            }

            msgPrefix = $" {ChatColors.Green} {args} {ChatColors.White}";
        }

        [ConsoleCommand("sharptimer_hud_primary_color", "Primary Color for Timer HUD. Default value: green")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerPrimaryHUDcolor(CCSPlayerController? player, CommandInfo command)
        {

            string args = command.ArgString.Trim();

            if (string.IsNullOrEmpty(args))
            {
                primaryHUDcolor = $"green";
                return;
            }

            primaryHUDcolor = $"{args}";
        }

        [ConsoleCommand("sharptimer_hud_secondary_color", "Secondary Color for Timer HUD. Default value: orange")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerSecondaryHUDcolor(CCSPlayerController? player, CommandInfo command)
        {

            string args = command.ArgString.Trim();

            if (string.IsNullOrEmpty(args))
            {
                secondaryHUDcolor = $"orange";
                return;
            }

            secondaryHUDcolor = $"{args}";
        }

        [ConsoleCommand("sharptimer_hud_tertiary_color", "Tertiary Color for Timer HUD. Default value: white")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void SharpTimerTertiaryHUDcolor(CCSPlayerController? player, CommandInfo command)
        {

            string args = command.ArgString.Trim();

            if (string.IsNullOrEmpty(args))
            {
                tertiaryHUDcolor = $"white";
                return;
            }

            tertiaryHUDcolor = $"{args}";
        }
    }
}