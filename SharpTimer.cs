using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Entities.Constants;
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
            string recordsFileName = "SharpTimer/player_records.json";
            playerRecordsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", recordsFileName);

            string mysqlConfigFileName = "SharpTimer/mysqlConfig.json";
            mySQLpath = Path.Join(Server.GameDirectory + "/csgo/cfg", mysqlConfigFileName);

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
                        player.PrintToChat($"{msgPrefix}Welcome {ChatColors.Red}{player.PlayerName} {ChatColors.White}to the server!");
                        player.PrintToChat($"{msgPrefix}Available Commands:");

                        if (respawnEnabled) player.PrintToChat($"{msgPrefix}!r (css_r) - Respawns you");
                        if (topEnabled) player.PrintToChat($"{msgPrefix}!top (css_top) - Lists top 10 records on this map");
                        if (rankEnabled) player.PrintToChat($"{msgPrefix}!rank (css_rank) - Shows your current rank");
                        if (pbComEnabled) player.PrintToChat($"{msgPrefix}!pb (css_pb) - Shows your current PB");

                        if (cpEnabled)
                        {
                            player.PrintToChat($"{msgPrefix}!cp (css_cp) - Sets a Checkpoint");
                            player.PrintToChat($"{msgPrefix}!tp (css_tp) - Teleports you to the last Checkpoint");
                            player.PrintToChat($"{msgPrefix}!prevcp (css_prevcp) - Teleports you one Checkpoint back");
                            player.PrintToChat($"{msgPrefix}!nextcp (css_nextcp) - Teleports you one Checkpoint forward");
                        }
                    }

                    _ = RankCommandHandler(player, player.SteamID.ToString(), player.Slot, true);

                    playerTimers[player.Slot].MovementService = new CCSPlayer_MovementServices(player.PlayerPawn.Value.MovementServices!.Handle);
                    playerTimers[player.Slot].SortedCachedRecords = GetSortedRecords();

                    //_ = PBCommandHandler(player, player.SteamID.ToString(), player.Slot);

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

                if (player.IsBot || !player.IsValid)
                {
                    return HookResult.Continue;
                }
                else
                {
                    if (removeCollisionEnabled == true && player.PlayerPawn != null)
                    {
                        player.PlayerPawn.Value.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING;
                        player.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING;

                        VirtualFunctionVoid<nint> collisionRulesChanged = new VirtualFunctionVoid<nint>(player.PlayerPawn.Value.Handle, OnCollisionRulesChangedOffset.Get());
                        collisionRulesChanged.Invoke(player.PlayerPawn.Value.Handle);
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

            /* RegisterEventHandler<EventPlayerHurt>((@event, info) =>
            {
                if (@event.Userid == null) return HookResult.Continue;

                var player = @event.Userid;

                if (player.IsBot || !player.IsValid)
                {
                    return HookResult.Continue;
                }
                else
                {
                    if (disableDamage == true) @event.Userid.PlayerPawn.Value.Health = 100; //reset player health to 100 on damage taken
                    return HookResult.Continue;
                }
            }); */

            RegisterListener<Listeners.OnTick>(() =>
            {
                foreach (var playerEntry in connectedPlayers)
                {
                    var player = playerEntry.Value;

                    if (player.IsValid && !player.IsBot && player.PawnIsAlive)
                    {
                        var buttons = player.Buttons;
                        string formattedPlayerVel = Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()).ToString().PadLeft(4, '0');
                        string formattedPlayerPre = Math.Round(ParseVector(playerTimers[player.Slot].PreSpeed ?? "0 0 0").Length2D()).ToString();
                        string playerTime = FormatTime(playerTimers[player.Slot].TimerTicks);
                        string forwardKey = "W";
                        string leftKey = "A";
                        string backKey = "S";
                        string rightKey = "D";

                        if (playerTimers[player.Slot].Azerty == true)
                        {
                            forwardKey = "Z";
                            leftKey = "Q";
                            backKey = "S";
                            rightKey = "D";
                        }

                        if (playerTimers[player.Slot].IsTimerRunning)
                        {
                            if (playerTimers[player.Slot].HideTimerHud != true) player.PrintToCenterHtml(
                                $"<font color='gray'>{GetPlayerPlacement(player)}</font> <font class='fontSize-l' color='{primaryHUDcolor}'>{playerTime}</font><br>" +
                                $"<font color='{tertiaryHUDcolor}'>Speed:</font> <font color='{secondaryHUDcolor}'>{formattedPlayerVel}</font> <font class='fontSize-s' color='gray'>({formattedPlayerPre})</font><br>" +
                                $"<font class='fontSize-s' color='gray'>{playerTimers[player.Slot].TimerRank} | PB: {playerTimers[player.Slot].PB}</font><br>" +
                                $"<font color='{tertiaryHUDcolor}'>{((buttons & PlayerButtons.Moveleft) != 0 ? leftKey : "_")} " +
                                $"{((buttons & PlayerButtons.Forward) != 0 ? forwardKey : "_")} " +
                                $"{((buttons & PlayerButtons.Moveright) != 0 ? rightKey : "_")} " +
                                $"{((buttons & PlayerButtons.Back) != 0 ? backKey : "_")} " +
                                $"{((buttons & PlayerButtons.Jump) != 0 ? "J" : "_")} " +
                                $"{((buttons & PlayerButtons.Duck) != 0 ? "C" : "_")}</font>");

                            playerTimers[player.Slot].TimerTicks++;
                        }
                        else
                        {
                            if (playerTimers[player.Slot].HideTimerHud != true) player.PrintToCenterHtml(
                                $"<font color='{tertiaryHUDcolor}'>Speed:</font> <font color='{secondaryHUDcolor}'>{formattedPlayerVel}</font> <font class='fontSize-s' color='gray'>({formattedPlayerPre})</font><br>" +
                                $"<font class='fontSize-s' color='gray'>{playerTimers[player.Slot].TimerRank} | PB: {playerTimers[player.Slot].PB}</font><br>" +
                                $"<font color='{tertiaryHUDcolor}'>{((buttons & PlayerButtons.Moveleft) != 0 ? leftKey : "_")} " +
                                $"{((buttons & PlayerButtons.Forward) != 0 ? forwardKey : "_")} " +
                                $"{((buttons & PlayerButtons.Moveright) != 0 ? rightKey : "_")} " +
                                $"{((buttons & PlayerButtons.Back) != 0 ? backKey : "_")} " +
                                $"{((buttons & PlayerButtons.Jump) != 0 ? "J" : "_")} " +
                                $"{((buttons & PlayerButtons.Duck) != 0 ? "C" : "_")}</font>");
                        }

                        if (!useTriggers)
                        {
                            CheckPlayerActions(player);
                        }

                        if (playerTimers[player.Slot].MovementService != null && removeCrouchFatigueEnabled == true)
                        {
                            if (playerTimers[player.Slot].MovementService.DuckSpeed != 7.0f) playerTimers[player.Slot].MovementService.DuckSpeed = 7.0f;
                        }

                        if (!player.PlayerPawn.Value.OnGroundLastTick)
                        {
                            playerTimers[player.Slot].TicksInAir++;
                            if (playerTimers[player.Slot].TicksInAir == 1)
                            {
                                playerTimers[player.Slot].PreSpeed = $"{player.PlayerPawn.Value.AbsVelocity.X} {player.PlayerPawn.Value.AbsVelocity.Y} {player.PlayerPawn.Value.AbsVelocity.Z}";
                            }
                        }
                        else
                        {
                            playerTimers[player.Slot].TicksInAir = 0;
                        }

                        playerTimers[player.Slot].TicksSinceLastCmd++;
                    }
                }
            });

            HookEntityOutput("trigger_multiple", "OnStartTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        if (activator == null || caller == null) return HookResult.Continue;
                        if (activator.DesignerName != "player" || useTriggers == false || activator == null || caller == null) return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                        if (player == null) return HookResult.Continue;
                        if (!player.PawnIsAlive || player == null || !connectedPlayers.ContainsKey(player.Slot) || caller.Entity.Name == null) return HookResult.Continue;

                        if (IsValidEndTriggerName(caller.Entity.Name.ToString()) && player.IsValid && playerTimers.ContainsKey(player.Slot) && playerTimers[player.Slot].IsTimerRunning)
                        {
                            OnTimerStop(player);
                            return HookResult.Continue;
                        }

                        if (IsValidStartTriggerName(caller.Entity.Name.ToString()) && player.IsValid && playerTimers.ContainsKey(player.Slot))
                        {
                            playerTimers[player.Slot].IsTimerRunning = false;
                            playerTimers[player.Slot].TimerTicks = 0;
                            playerCheckpoints.Remove(player.Slot);
                            if (maxStartingSpeedEnabled == true && (float)Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y + player.PlayerPawn.Value.AbsVelocity.Z * player.PlayerPawn.Value.AbsVelocity.Z) > maxStartingSpeed)
                            {
                                AdjustPlayerVelocity(player, maxStartingSpeed);
                            }
                            return HookResult.Continue;
                        }

                        return HookResult.Continue;
                    });

            HookEntityOutput("trigger_multiple", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        if (activator == null || caller == null) return HookResult.Continue;
                        if (activator.DesignerName != "player" || useTriggers == false || activator == null || caller == null) return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                        if (player == null) return HookResult.Continue;
                        if (!player.PawnIsAlive || !connectedPlayers.ContainsKey(player.Slot) || caller.Entity.Name == null) return HookResult.Continue;

                        if (IsValidStartTriggerName(caller.Entity.Name.ToString()) && player.IsValid && playerTimers.ContainsKey(player.Slot))
                        {
                            OnTimerStart(player);
                            if (maxStartingSpeedEnabled == true && (float)Math.Sqrt(player.PlayerPawn.Value.AbsVelocity.X * player.PlayerPawn.Value.AbsVelocity.X + player.PlayerPawn.Value.AbsVelocity.Y * player.PlayerPawn.Value.AbsVelocity.Y + player.PlayerPawn.Value.AbsVelocity.Z * player.PlayerPawn.Value.AbsVelocity.Z) > maxStartingSpeed)
                            {
                                AdjustPlayerVelocity(player, maxStartingSpeed);
                            }
                            return HookResult.Continue;
                        }

                        return HookResult.Continue;
                    });

            HookEntityOutput("trigger_teleport", "OnEndTouch", (CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay) =>
                    {
                        if (activator == null || caller == null) return HookResult.Continue;
                        if (activator.DesignerName != "player" || resetTriggerTeleportSpeedEnabled == false) return HookResult.Continue;

                        var player = new CCSPlayerController(new CCSPlayerPawn(activator.Handle).Controller.Value.Handle);

                        if (player == null) return HookResult.Continue;
                        if (!player.PawnIsAlive || !connectedPlayers.ContainsKey(player.Slot)) return HookResult.Continue;

                        if (player.IsValid && resetTriggerTeleportSpeedEnabled == true)
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

        private void CheckPlayerActions(CCSPlayerController? player)
        {
            if (!player.PawnIsAlive || player == null) return;

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
            if (!player.PawnIsAlive || player == null || !player.IsValid) return;

            // Remove checkpoints for the current player
            playerCheckpoints.Remove(player.Slot);

            playerTimers[player.Slot].IsTimerRunning = true;
            playerTimers[player.Slot].TimerTicks = 0;
        }

        public void OnTimerStop(CCSPlayerController? player)
        {
            if (!player.PawnIsAlive || player == null || playerTimers[player.Slot].IsTimerRunning == false || !player.IsValid) return;

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
