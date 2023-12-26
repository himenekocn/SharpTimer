using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using System.Runtime.InteropServices;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace SharpTimer
{
    [MinimumApiVersion(125)]
    public partial class SharpTimer : BasePlugin
    {
        public override void Load(bool hotReload)
        {
            gameDir = Server.GameDirectory;
            
            string recordsFileName = "SharpTimer/player_records.json";
            playerRecordsPath = Path.Join(gameDir + "/csgo/cfg", recordsFileName);

            string mysqlConfigFileName = "SharpTimer/mysqlConfig.json";
            mySQLpath = Path.Join(gameDir + "/csgo/cfg", mysqlConfigFileName);

            currentMapName = Server.MapName;

            RegisterListener<Listeners.OnMapStart>(OnMapStartHandler);

            RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
            {
                var player = @event.Userid;

                if (player.IsBot || !player.IsValid)
                {
                    return HookResult.Continue;
                }
                else
                {

                    connectedPlayers[player.Slot] = player;

                    Console.WriteLine($"Added player {player.PlayerName} with UserID {player.UserId} to connectedPlayers");
                    playerTimers[player.Slot] = new PlayerTimerInfo();

                    if (connectMsgEnabled == true) Server.PrintToChatAll($"{msgPrefix}Player {ChatColors.Red}{player.PlayerName} {ChatColors.White}connected!");

                    if (cmdJoinMsgEnabled == true)
                    {
                        PrintAllEnabledCommands(player);
                    }
                    
                    playerTimers[player.Slot].MovementService = new CCSPlayer_MovementServices(player.PlayerPawn.Value.MovementServices!.Handle);
                    playerTimers[player.Slot].SortedCachedRecords = GetSortedRecords();

                    if (removeLegsEnabled == true) player.PlayerPawn.Value.Render = Color.FromArgb(254, 254, 254, 254);

                    //PlayerSettings
                    if (useMySQL == true)
                    {
                        //_ = GetPlayerSettingFromDatabase(player, "Azerty");
                        //_ = GetPlayerSettingFromDatabase(player, "HideTimerHud");
                        //_ = GetPlayerSettingFromDatabase(player, "TimesConnected");
                        //_ = GetPlayerSettingFromDatabase(player, "SoundsEnabled");
                        //_ = SavePlayerStatToDatabase(player.SteamID.ToString(), "MouseSens", player.GetConVarValue("sensitivity").ToString());
                    }

                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventRoundStart>((@event, info) =>
            {
                LoadConfig();
                return HookResult.Continue;
            });

            RegisterEventHandler<EventPlayerSpawned>((@event, info) =>
            {
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
                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
            {
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
                        Console.WriteLine($"Removed player {connectedPlayer.PlayerName} with UserID {connectedPlayer.UserId} from connectedPlayers");

                        if (connectMsgEnabled == true) Server.PrintToChatAll($"{msgPrefix}Player {ChatColors.Red}{connectedPlayer.PlayerName} {ChatColors.White}disconnected!");
                    }

                    return HookResult.Continue;
                }
            });

            RegisterListener<Listeners.OnTick>(() =>
            {
                foreach (var playerEntry in connectedPlayers)
                {
                    var player = playerEntry.Value;
                    if (player == null) continue;
                    TimerOnTick(player);
                }
            });

            HookEntityOutput("trigger_multiple", "OnStartTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        if (activator == null || output == null || value == null || caller == null) return HookResult.Continue;
                        if (activator.DesignerName != "player" || useTriggers == false) return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                        if (!IsAllowedPlayer(player) || caller.Entity.Name == null) return HookResult.Continue;

                        if (IsValidEndTriggerName(caller.Entity.Name.ToString()) && IsAllowedPlayer(player) && playerTimers[player.Slot].IsTimerRunning && !playerTimers[player.Slot].IsTimerBlocked)
                        {
                            OnTimerStop(player);
                            return HookResult.Continue;
                        }

                        if (IsValidStartTriggerName(caller.Entity.Name.ToString()) && IsAllowedPlayer(player))
                        {
                            playerTimers[player.Slot].IsTimerRunning = false;
                            playerTimers[player.Slot].TimerTicks = 0;
                            playerCheckpoints.Remove(player.Slot);
                            if (maxStartingSpeedEnabled == true && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed)
                            {
                                AdjustPlayerVelocity(player, maxStartingSpeed);
                            }
                            return HookResult.Continue;
                        }

                        return HookResult.Continue;
                    });

            HookEntityOutput("trigger_multiple", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        if (activator == null || output == null || value == null || caller == null) return HookResult.Continue;
                        if (activator.DesignerName != "player" || useTriggers == false) return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                        if (!IsAllowedPlayer(player) || caller.Entity.Name == null) return HookResult.Continue;

                        if (IsValidStartTriggerName(caller.Entity.Name.ToString()) && IsAllowedPlayer(player) && !playerTimers[player.Slot].IsTimerBlocked)
                        {
                            OnTimerStart(player);
                            if (maxStartingSpeedEnabled == true && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed)
                            {
                                AdjustPlayerVelocity(player, maxStartingSpeed);
                            }
                            return HookResult.Continue;
                        }

                        return HookResult.Continue;
                    });

            HookEntityOutput("trigger_teleport", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        if (activator == null || output == null || value == null || caller == null) return HookResult.Continue;
                        if (activator.DesignerName != "player" || resetTriggerTeleportSpeedEnabled == false) return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                        if (!IsAllowedPlayer(player)) return HookResult.Continue;

                        if (IsAllowedPlayer(player) && resetTriggerTeleportSpeedEnabled == true)
                        {
                            AdjustPlayerVelocity(player, 0);
                            return HookResult.Continue;
                        }

                        return HookResult.Continue;
                    });

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && disableDamage == true)
            {
                VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook((h =>
                {
                    if (disableDamage == false || h == null) return HookResult.Continue;

                    var damageInfoParam = h.GetParam<CTakeDamageInfo>(1);

                    if (damageInfoParam == null) return HookResult.Continue;

                    if (disableDamage == true) damageInfoParam.Damage = 0;

                    return HookResult.Continue;
                }), HookMode.Pre);
            }

            Console.WriteLine("[SharpTimer] Plugin Loaded");
        }

        private void CheckPlayerCoords(CCSPlayerController? player)
        {
            if (!IsAllowedPlayer(player)) return;

            Vector incorrectVector = new Vector(0, 0, 0);

            Vector playerPos = player.Pawn.Value.CBodyComponent!.SceneNode.AbsOrigin;

            if (!IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2) && IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2) && currentMapStartC1 != incorrectVector && currentMapStartC2 != incorrectVector && currentMapEndC1 != incorrectVector && currentMapEndC2 != incorrectVector)
            {
                OnTimerStart(player);

                if (maxStartingSpeedEnabled == true && (float)Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y + player.PlayerPawn.Value.AbsVelocity.Z * player.PlayerPawn.Value.AbsVelocity.Z) > maxStartingSpeed)
                {
                    AdjustPlayerVelocity(player, maxStartingSpeed);
                }
            }

            if (IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2) && !IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2) && currentMapStartC1 != incorrectVector && currentMapStartC2 != incorrectVector && currentMapEndC1 != incorrectVector && currentMapEndC2 != incorrectVector)
            {
                OnTimerStop(player);
            }
        }

        public void OnTimerStart(CCSPlayerController? player)
        {
            if (!IsAllowedPlayer(player)) return;

            // Remove checkpoints for the current player
            playerCheckpoints.Remove(player.Slot);

            playerTimers[player.Slot].IsTimerRunning = true;
            playerTimers[player.Slot].TimerTicks = 0;
        }

        public void OnTimerStop(CCSPlayerController? player)
        {
            if (!IsAllowedPlayer(player) || playerTimers[player.Slot].IsTimerRunning == false) return;

            int currentTicks = playerTimers[player.Slot].TimerTicks;
            int previousRecordTicks = GetPreviousPlayerRecord(player);

            SavePlayerTime(player, currentTicks);
            if (useMySQL == true) _ = SavePlayerTimeToDatabase(player, currentTicks, player.SteamID.ToString(), player.PlayerName, player.Slot);
            playerTimers[player.Slot].IsTimerRunning = false;

            string timeDifference = "";
            char ifFirstTimeColor;
            if (previousRecordTicks != 0)
            {
                timeDifference = FormatTimeDifference(currentTicks, previousRecordTicks);
                ifFirstTimeColor = ChatColors.Red;
            }
            else
            {
                ifFirstTimeColor = ChatColors.Yellow;
            }

            if (currentTicks < previousRecordTicks)
            {
                Server.PrintToChatAll(msgPrefix + $"{ParseColorToSymbol(primaryHUDcolor)}{player.PlayerName} {ChatColors.White}just finished the map in: {ChatColors.Green}[{FormatTime(currentTicks)}]! {timeDifference}");
            }
            else if (currentTicks > previousRecordTicks)
            {
                Server.PrintToChatAll(msgPrefix + $"{ParseColorToSymbol(primaryHUDcolor)}{player.PlayerName} {ChatColors.White}just finished the map in: {ifFirstTimeColor}[{FormatTime(currentTicks)}]! {timeDifference}");
            }
            else
            {
                Server.PrintToChatAll(msgPrefix + $"{ParseColorToSymbol(primaryHUDcolor)}{player.PlayerName} {ChatColors.White}just finished the map in: {ChatColors.Yellow}[{FormatTime(currentTicks)}]! (No change in time)");
            }

            if (useMySQL == false) _ = RankCommandHandler(player, player.SteamID.ToString(), player.Slot, true);

            if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {beepSound}");
        }
    }
}
