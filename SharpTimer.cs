using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using System.Drawing;
using System.Runtime.InteropServices;

namespace SharpTimer
{
    [MinimumApiVersion(141)]
    public partial class SharpTimer : BasePlugin
    {
        public override void Load(bool hotReload)
        {
            SharpTimerDebug("Loading Plugin...");

            gameDir = Server.GameDirectory;
            SharpTimerDebug($"Set gameDir to {gameDir}");

            defaultServerHostname = ConVar.Find("hostname").StringValue;

            string recordsFileName = "SharpTimer/player_records.json";
            playerRecordsPath = Path.Join(gameDir + "/csgo/cfg", recordsFileName);
            SharpTimerDebug($"Set playerRecordsPath to {playerRecordsPath}");

            string mysqlConfigFileName = "SharpTimer/mysqlConfig.json";
            mySQLpath = Path.Join(gameDir + "/csgo/cfg", mysqlConfigFileName);
            SharpTimerDebug($"Set mySQLpath to {mySQLpath}");

            currentMapName = Server.MapName;

            RegisterListener<Listeners.OnMapStart>(OnMapStartHandler);

            RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
            {
                Server.ExecuteCommand("sv_cheats 0");
                Svcheats.SetValue(false);
                Server.ExecuteCommand("sv_hibernate_when_empty 0");
                
                var player = @event.Userid;

                if (player.IsBot || !player.IsValid)
                {
                    return HookResult.Continue;
                }
                else
                {

                    connectedPlayers[player.Slot] = new CCSPlayerController(player.Handle);
                    playerTimers[player.Slot] = new PlayerTimerInfo();

                    if (connectMsgEnabled == true) Server.PrintToChatAll($"{msgPrefix}玩家 {ChatColors.Red}{player.PlayerName} {ChatColors.White}插入服务器!");

                    if (cmdJoinMsgEnabled == true) PrintAllEnabledCommands(player);

                    playerTimers[player.Slot].MovementService = new CCSPlayer_MovementServices(player.PlayerPawn.Value.MovementServices!.Handle);
                    playerTimers[player.Slot].StageTimes = new Dictionary<int, int>();
                    playerTimers[player.Slot].StageVelos = new Dictionary<int, string>();
                    playerTimers[player.Slot].CurrentMapStage = 0;
                    playerTimers[player.Slot].CurrentMapCheckpoint = 0;

                    _ = IsPlayerATester(player.SteamID.ToString(), player.Slot);

                    if (removeLegsEnabled == true) player.PlayerPawn.Value.Render = Color.FromArgb(254, 254, 254, 254);

                    player.ExecuteClientCommand("sv_minrate 64000");
                    player.ExecuteClientCommand("rate 80000");
                    player.ExecuteClientCommand("sv_maxrate 90000");

                    //PlayerSettings
                    if (useMySQL == true)
                    {
                        //_ = GetPlayerSettingFromDatabase(player, "Azerty");
                        //_ = GetPlayerSettingFromDatabase(player, "HideTimerHud");
                        //_ = GetPlayerSettingFromDatabase(player, "TimesConnected");
                        //_ = GetPlayerSettingFromDatabase(player, "SoundsEnabled");
                        //_ = SavePlayerStatToDatabase(player.SteamID.ToString(), "MouseSens", player.GetConVarValue("sensitivity").ToString());
                    }

                    SharpTimerDebug($"Added player {player.PlayerName} with UserID {player.UserId} to connectedPlayers");
                    SharpTimerDebug($"Total players connected: {connectedPlayers.Count}");
                    SharpTimerDebug($"Total playerTimers: {playerTimers.Count}");
                    SharpTimerDebug($"Total playerCheckpoints: {playerCheckpoints.Count}");

                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventRoundStart>((@event, info) =>
            {
                LoadMapData();
                SharpTimerDebug($"Loading MapData on RoundStart...");

                SharpTimerDebug("Re-Executing custom_exec with 2sec delay...");
                var custom_exec_delay = AddTimer(2.0f, () =>
                {
                    SharpTimerDebug("Re-Executing SharpTimer/custom_exec");
                    Server.ExecuteCommand("execifexists SharpTimer/custom_exec.cfg");
                    Server.ExecuteCommand("sv_cheats 0");
                    Svcheats.SetValue(false);
                    Server.ExecuteCommand("sv_hibernate_when_empty 0");
                });

                var forceanticheats = AddTimer(5.0f, () =>
                {
                    Server.ExecuteCommand("sv_cheats 0");
                    Server.ExecuteCommand("sv_hibernate_when_empty 0");
                    Svcheats.SetValue(false);
                }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

                foreach (CCSPlayerController player in connectedPlayers.Values)
                {
                    if (player == null || player.IsBot || !player.IsValid) continue;

                    player.ExecuteClientCommand("sv_minrate 64000");
                    player.ExecuteClientCommand("rate 64000");
                    player.ExecuteClientCommand("sv_maxrate 89000");
                }
                return HookResult.Continue;
            });

            RegisterEventHandler<EventPlayerSpawned>((@event, info) =>
            {
                Server.ExecuteCommand("sv_cheats 0");
                Svcheats.SetValue(false);

                if (@event.Userid == null) return HookResult.Continue;

                var player = @event.Userid;

                if (player.IsBot || !player.IsValid || player == null)
                {
                    return HookResult.Continue;
                }
                else
                {
                    if (removeCollisionEnabled == true && player.PlayerPawn != null)
                    {
                        RemovePlayerCollision(player);
                    }

                    foreach (CCSPlayerController iplayer in connectedPlayers.Values)
                    {
                        if (iplayer == null || iplayer.IsBot || !iplayer.IsValid) continue;

                        iplayer.ExecuteClientCommand("sv_minrate 64000");
                        iplayer.ExecuteClientCommand("rate 65000");
                        iplayer.ExecuteClientCommand("sv_maxrate 90000");
                    }

                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
            {
                Server.ExecuteCommand("sv_cheats 0");
                Svcheats.SetValue(false);

                var player = @event.Userid;

                if (player.IsBot || !player.IsValid)
                {
                    return HookResult.Continue;
                }
                else
                {
                    if (connectedPlayers.TryGetValue(player.Slot, out var connectedPlayer))
                    {
                        connectedPlayers.Remove(player.Slot);
                        playerTimers.Remove(player.Slot);
                        playerCheckpoints.Remove(player.Slot);
                        SharpTimerDebug($"Removed player {connectedPlayer.PlayerName} with UserID {connectedPlayer.UserId} from connectedPlayers.");
                        SharpTimerDebug($"Total players connected: {connectedPlayers.Count}");
                        SharpTimerDebug($"Total playerTimers: {playerTimers.Count}");
                        SharpTimerDebug($"Total playerCheckpoints: {playerCheckpoints.Count}");

                        if (connectMsgEnabled == true) Server.PrintToChatAll($"{msgPrefix}玩家 {ChatColors.Red}{connectedPlayer.PlayerName} {ChatColors.White}拔出服务器!");
                    }

                    foreach (CCSPlayerController iplayer in connectedPlayers.Values)
                    {
                        if (iplayer == null || iplayer.IsBot || !iplayer.IsValid) continue;

                        iplayer.ExecuteClientCommand("sv_minrate 64000");
                        iplayer.ExecuteClientCommand("rate 64000");
                        iplayer.ExecuteClientCommand("sv_maxrate 89000");
                    }

                    return HookResult.Continue;
                }
            });

            RegisterListener<Listeners.OnTick>(TimerOnTick);

            HookEntityOutput("trigger_multiple", "OnStartTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
            {
                try
                {
                    if (activator == null || output == null || value == null || caller == null)
                    {
                        SharpTimerDebug("Null reference detected in trigger_multiple OnStartTouch hook.");
                        return HookResult.Continue;
                    }

                    if (activator.DesignerName != "player" || useTriggers == false) return HookResult.Continue;

                    var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                    if (player == null)
                    {
                        SharpTimerDebug("Player is null in trigger_multiple OnStartTouch hook.");
                        return HookResult.Continue;
                    }

                    if (!IsAllowedPlayer(player) || caller.Entity.Name == null) return HookResult.Continue;

                    if (useStageTriggers == true && stageTriggers.ContainsKey(caller.Handle) && playerTimers[player.Slot].IsTimerBlocked == false && playerTimers[player.Slot].IsTimerRunning == true && IsAllowedPlayer(player))
                    {
                        if (stageTriggers[caller.Handle] == 1)
                        {
                            playerTimers[player.Slot].CurrentMapStage = 1;
                        }
                        else
                        {
                            HandlePlayerStageTimes(player, caller.Handle);
                        }
                    }

                    if (useCheckpointTriggers == true && cpTriggers.ContainsKey(caller.Handle) && playerTimers[player.Slot].IsTimerBlocked == false && playerTimers[player.Slot].IsTimerRunning == true && IsAllowedPlayer(player))
                    {
                        HandlePlayerCheckpointTimes(player, caller.Handle);
                    }

                    if (IsValidEndTriggerName(caller.Entity.Name.ToString()) && IsAllowedPlayer(player) && playerTimers[player.Slot].IsTimerRunning && !playerTimers[player.Slot].IsTimerBlocked)
                    {
                        OnTimerStop(player);
                        SharpTimerDebug($"Player {player.PlayerName} entered EndZone");
                        return HookResult.Continue;
                    }

                    if (IsValidStartTriggerName(caller.Entity.Name.ToString()) && IsAllowedPlayer(player))
                    {
                        playerTimers[player.Slot].IsTimerRunning = false;
                        playerTimers[player.Slot].TimerTicks = 0;
                        playerTimers[player.Slot].IsBonusTimerRunning = false;
                        playerTimers[player.Slot].BonusTimerTicks = 0;
                        playerCheckpoints.Remove(player.Slot);
                        if (stageTriggerCount != 0 && useStageTriggers == true)
                        {
                            playerTimers[player.Slot].StageTimes.Clear();
                            playerTimers[player.Slot].StageVelos.Clear();
                            playerTimers[player.Slot].CurrentMapStage = stageTriggers.GetValueOrDefault(caller.Handle, 0);
                        }
                        else if (cpTriggerCount != 0 && useStageTriggers == false)
                        {
                            playerTimers[player.Slot].StageTimes.Clear();
                            playerTimers[player.Slot].StageVelos.Clear();
                            playerTimers[player.Slot].CurrentMapCheckpoint = 0;
                        }

                        if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length()) > maxStartingSpeed) ||
                            (maxStartingSpeedEnabled == true && use2DSpeed == true  && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed))
                        {
                            Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                            adjustVelocity(player, maxStartingSpeed, false);
                        }

                        SharpTimerDebug($"Player {player.PlayerName} entered StartZone");

                        return HookResult.Continue;
                    }

                    var (validEndBonus, endBonusX) = IsValidEndBonusTriggerName(caller.Entity.Name.ToString(), player.Slot);

                    if (validEndBonus && IsAllowedPlayer(player) && playerTimers[player.Slot].IsBonusTimerRunning && !playerTimers[player.Slot].IsTimerBlocked)
                    {
                        OnBonusTimerStop(player, endBonusX);
                        SharpTimerDebug($"Player {player.PlayerName} entered Bonus{endBonusX} EndZone");
                        return HookResult.Continue;
                    }

                    var (validStartBonus, startBonusX) = IsValidStartBonusTriggerName(caller.Entity.Name.ToString());

                    if (validStartBonus && IsAllowedPlayer(player))
                    {
                        playerTimers[player.Slot].IsTimerRunning = false;
                        playerTimers[player.Slot].TimerTicks = 0;
                        playerTimers[player.Slot].IsBonusTimerRunning = false;
                        playerTimers[player.Slot].BonusTimerTicks = 0;
                        playerCheckpoints.Remove(player.Slot);

                        if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length()) > maxStartingSpeed) ||
                            (maxStartingSpeedEnabled == true && use2DSpeed == true  && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed))
                        {
                            Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                            adjustVelocity(player, maxStartingSpeed, false);
                        }
                        SharpTimerDebug($"Player {player.PlayerName} entered Bonus{startBonusX} StartZone");
                        return HookResult.Continue;
                    }
                    return HookResult.Continue;
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Exception in trigger_multiple OnStartTouch hook: {ex.Message}");
                    return HookResult.Continue;
                }
            });

            HookEntityOutput("trigger_multiple", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
            {
                try
                {
                    if (activator == null || output == null || value == null || caller == null)
                    {
                        SharpTimerDebug("Null reference detected in trigger_multiple OnEndTouch hook.");
                        return HookResult.Continue;
                    }

                    if (activator.DesignerName != "player" || useTriggers == false) return HookResult.Continue;

                    var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                    if (player == null)
                    {
                        SharpTimerDebug("Player is null in trigger_multiple OnEndTouch hook.");
                        return HookResult.Continue;
                    }

                    if (!IsAllowedPlayer(player) || caller.Entity.Name == null) return HookResult.Continue;

                    if (IsValidStartTriggerName(caller.Entity.Name.ToString()) && IsAllowedPlayer(player) && !playerTimers[player.Slot].IsTimerBlocked)
                    {
                        OnTimerStart(player);

                        if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length()) > maxStartingSpeed) ||
                            (maxStartingSpeedEnabled == true && use2DSpeed == true  && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed))
                        {
                            Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                            adjustVelocity(player, maxStartingSpeed, false);
                        }

                        SharpTimerDebug($"Player {player.PlayerName} left StartZone");

                        return HookResult.Continue;
                    }

                    var (validStartBonus, StartBonusX) = IsValidStartBonusTriggerName(caller.Entity.Name.ToString());

                    if (validStartBonus == true && IsAllowedPlayer(player) && !playerTimers[player.Slot].IsTimerBlocked)
                    {
                        OnTimerStart(player, StartBonusX);

                        if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length()) > maxStartingSpeed) ||
                            (maxStartingSpeedEnabled == true && use2DSpeed == true  && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed))
                        {
                            Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                            adjustVelocity(player, maxStartingSpeed, false);
                        }

                        SharpTimerDebug($"Player {player.PlayerName} left BonusStartZone {StartBonusX}");

                        return HookResult.Continue;
                    }
                    return HookResult.Continue;
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Exception in trigger_multiple OnEndTouch hook: {ex.Message}");
                    return HookResult.Continue;
                }
            });

            HookEntityOutput("trigger_teleport", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
            {
                try
                {
                    if (activator == null || output == null || value == null || caller == null)
                    {
                        SharpTimerDebug("Null reference detected in trigger_teleport hook.");
                        return HookResult.Continue;
                    }

                    if (activator.DesignerName != "player" || resetTriggerTeleportSpeedEnabled == false)
                    {
                        return HookResult.Continue;
                    }

                    var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                    if (player == null)
                    {
                        SharpTimerDebug("Player is null in trigger_teleport hook.");
                        return HookResult.Continue;
                    }

                    if (!IsAllowedPlayer(player)) return HookResult.Continue;

                    if (IsAllowedPlayer(player) && resetTriggerTeleportSpeedEnabled == true && currentMapOverrideDisableTelehop == false)
                    {
                        Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                        adjustVelocity(player, 0, false);
                    }

                    return HookResult.Continue;
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Exception in trigger_teleport hook: {ex.Message}");
                    return HookResult.Continue;
                }
            });

            HookEntityOutput("trigger_push", "OnStartTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
            {
                try
                {
                    if (activator == null || output == null || value == null || caller == null)
                    {
                        SharpTimerDebug("Null reference detected in trigger_push hook.");
                        return HookResult.Continue;
                    }

                    if (activator.DesignerName != "player" || triggerPushFixEnabled == false)
                    {
                        return HookResult.Continue;
                    }

                    var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                    if (player == null)
                    {
                        SharpTimerDebug("Player is null in trigger_push hook.");
                        return HookResult.Continue;
                    }

                    if (!IsAllowedPlayer(player)) return HookResult.Continue;

                    if (triggerPushData.TryGetValue(caller.Handle, out TriggerPushData TriggerPushData) && triggerPushFixEnabled == true)
                    {
                        player.PlayerPawn.Value.AbsVelocity.X += TriggerPushData.PushDirEntitySpace.X * TriggerPushData.PushSpeed;
                        player.PlayerPawn.Value.AbsVelocity.Y += TriggerPushData.PushDirEntitySpace.Y * TriggerPushData.PushSpeed;
                        player.PlayerPawn.Value.AbsVelocity.Z += TriggerPushData.PushDirEntitySpace.Z * TriggerPushData.PushSpeed;
                        SharpTimerDebug($"trigger_push OnStartTouch Player velocity adjusted for {player.PlayerName} by {TriggerPushData.PushSpeed}");
                    }

                    return HookResult.Continue;
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Exception in trigger_push hook: {ex.Message}");
                    return HookResult.Continue;
                }
            });

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && disableDamage == true)
            {
                SharpTimerDebug($"TakeDamage hook...");
                VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook((h =>
                {
                    if (disableDamage == false || h == null) return HookResult.Continue;
                    var damageInfoParam = h.GetParam<CTakeDamageInfo>(1);
                    if (damageInfoParam == null) return HookResult.Continue;
                    if (disableDamage == true) damageInfoParam.Damage = 0;
                    return HookResult.Continue;
                }), HookMode.Pre);
            }
            else
            {
                SharpTimerDebug($"Platform is Windows. Blocking TakeDamage hook");
            }

            SharpTimerDebug("Plugin Loaded");
        }

        public void OnTimerStart(CCSPlayerController? player, int bonusX = 0)
        {
            Server.ExecuteCommand("sv_cheats 0");
            Svcheats.SetValue(false);
            if (!IsAllowedPlayer(player)) return;

            if(player.Pawn.Value.MoveType == MoveType_t.MOVETYPE_NOCLIP)
                return;

            if (bonusX != 0)
            {
                if (useTriggers) SharpTimerDebug($"Starting Bonus Timer for {player.PlayerName}");

                // Remove checkpoints for the current player
                playerCheckpoints.Remove(player.Slot);

                playerTimers[player.Slot].IsTimerRunning = false;
                playerTimers[player.Slot].TimerTicks = 0;

                playerTimers[player.Slot].IsBonusTimerRunning = true;
                playerTimers[player.Slot].BonusTimerTicks = 0;
                playerTimers[player.Slot].BonusStage = bonusX;
                player.Pawn.Value.MoveType = MoveType_t.MOVETYPE_WALK;
            }
            else
            {
                if (useTriggers) SharpTimerDebug($"Starting Timer for {player.PlayerName}");

                // Remove checkpoints for the current player
                playerCheckpoints.Remove(player.Slot);

                playerTimers[player.Slot].IsTimerRunning = true;
                playerTimers[player.Slot].TimerTicks = 0;

                playerTimers[player.Slot].IsBonusTimerRunning = false;
                playerTimers[player.Slot].BonusTimerTicks = 0;
                playerTimers[player.Slot].BonusStage = bonusX;
                player.Pawn.Value.MoveType = MoveType_t.MOVETYPE_WALK;
            }

        }

        public void OnTimerStop(CCSPlayerController? player)
        {
            if (!IsAllowedPlayer(player) || playerTimers[player.Slot].IsTimerRunning == false) return;

            if (useStageTriggers == true && useCheckpointTriggers == true)
            {
                if (playerTimers[player.Slot].CurrentMapStage != stageTriggerCount)
                {
                    player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} 保存时间错误: 玩家当前阶段与最终阶段不匹配 ({stageTriggerCount})");
                    playerTimers[player.Slot].IsTimerRunning = false;
                    return;
                }

                if (playerTimers[player.Slot].CurrentMapCheckpoint != cpTriggerCount)
                {
                    player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} 保存时间错误: 玩家当前检查点与最终检查点不匹配 ({cpTriggerCount})");
                    playerTimers[player.Slot].IsTimerRunning = false;
                    return;
                }
            }

            if (useStageTriggers == true && useCheckpointTriggers == false)
            {
                if (playerTimers[player.Slot].CurrentMapStage != stageTriggerCount)
                {
                    player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} 保存时间错误: 玩家当前阶段与最终阶段不匹配 ({stageTriggerCount})");
                    playerTimers[player.Slot].IsTimerRunning = false;
                    return;
                }
            }

            if (useStageTriggers == false && useCheckpointTriggers == true)
            {
                if (playerTimers[player.Slot].CurrentMapCheckpoint != cpTriggerCount)
                {
                    player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} 保存时间错误: 玩家当前检查点与最终检查点不匹配 ({cpTriggerCount})");
                    playerTimers[player.Slot].IsTimerRunning = false;
                    return;
                }
            }

            if (useTriggers) SharpTimerDebug($"Stopping Timer for {player.PlayerName}");

            if(player.Pawn.Value.MoveType == MoveType_t.MOVETYPE_NOCLIP)
                return;

            int currentTicks = playerTimers[player.Slot].TimerTicks;
            int previousRecordTicks = GetPreviousPlayerRecord(player);

            if(currentTicks < 300)
            {
                player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} 保存错误: 时间过短，疑似作弊");
                Server.PrintToChatAll(msgPrefix + $"{primaryChatColor}{player.PlayerName} {ChatColors.White}完成地图 用时: {primaryChatColor}[{FormatTime(currentTicks)}]{ChatColors.LightRed} 疑似作弊 记录不给予保存 玩家可加群进行举报");
                Server.PrintToChatAll(msgPrefix + $"{primaryChatColor}{player.PlayerName} {ChatColors.White}完成地图 用时: {primaryChatColor}[{FormatTime(currentTicks)}]{ChatColors.LightRed} 疑似作弊 记录不给予保存 玩家可加群进行举报");
                Server.PrintToChatAll(msgPrefix + $"{primaryChatColor}{player.PlayerName} {ChatColors.White}完成地图 用时: {primaryChatColor}[{FormatTime(currentTicks)}]{ChatColors.LightRed} 疑似作弊 记录不给予保存 玩家可加群进行举报");
                playerTimers[player.Slot].IsTimerRunning = false;
                return;
            }

            SavePlayerTime(player, currentTicks);
            if (useMySQL == true) _ = SavePlayerTimeToDatabase(player, currentTicks, player.SteamID.ToString(), player.PlayerName, player.Slot);
            playerTimers[player.Slot].IsTimerRunning = false;

            string timeDifference = "";
            if (previousRecordTicks != 0) timeDifference = FormatTimeDifference(currentTicks, previousRecordTicks);

            Server.PrintToChatAll(msgPrefix + $"{primaryChatColor}{player.PlayerName} {ChatColors.White}完成地图 用时: {primaryChatColor}[{FormatTime(currentTicks)}]{ChatColors.White}! {timeDifference}");

            if (useMySQL == false) _ = RankCommandHandler(player, player.SteamID.ToString(), player.Slot, player.PlayerName, true);

            if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {beepSound}");
        }

        public void OnBonusTimerStop(CCSPlayerController? player, int bonusX)
        {
            if (!IsAllowedPlayer(player) || playerTimers[player.Slot].IsBonusTimerRunning == false) return;

            if (useTriggers) SharpTimerDebug($"Stopping Bonus Timer for {player.PlayerName}");

            int currentTicks = playerTimers[player.Slot].BonusTimerTicks;
            int previousRecordTicks = GetPreviousPlayerRecord(player, bonusX);

            SavePlayerTime(player, currentTicks, bonusX);
            if (useMySQL == true) _ = SavePlayerTimeToDatabase(player, currentTicks, player.SteamID.ToString(), player.PlayerName, player.Slot, bonusX);
            playerTimers[player.Slot].IsBonusTimerRunning = false;

            string timeDifference = "";
            if (previousRecordTicks != 0) timeDifference = FormatTimeDifference(currentTicks, previousRecordTicks);

            Server.PrintToChatAll(msgPrefix + $"{primaryChatColor}{player.PlayerName} {ChatColors.White}完成了 奖励关{bonusX} 用时: {primaryChatColor}[{FormatTime(currentTicks)}]{ChatColors.White}! {timeDifference}");

            if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {beepSound}");
        }
    }
}
