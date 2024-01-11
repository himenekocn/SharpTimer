using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text;
using System.Text.Json;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace SharpTimer
{
    public partial class SharpTimer
    {
        private bool IsAllowedPlayer(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || player.Pawn == null || !player.PlayerPawn.IsValid || !player.PawnIsAlive || player.IsBot)
            {
                return false;
            }

            CsTeam teamNum = (CsTeam)player.TeamNum;
            bool isTeamValid = teamNum == CsTeam.CounterTerrorist || teamNum == CsTeam.Terrorist;

            bool isTeamSpectatorOrNone = teamNum != CsTeam.Spectator && teamNum != CsTeam.None;
            bool isConnected = connectedPlayers.ContainsKey(player.Slot) || playerTimers.ContainsKey(player.Slot);

            return isTeamValid && isTeamSpectatorOrNone && isConnected;
        }

        async Task IsPlayerATester(string steamId64, int playerSlot)
        {
            try
            {
                string response = await httpClient.GetStringAsync(testerPersonalGifsSource);

                using (JsonDocument jsonDocument = JsonDocument.Parse(response))
                {
                    playerTimers[playerSlot].IsTester = jsonDocument.RootElement.TryGetProperty(steamId64, out JsonElement steamData);

                    if (playerTimers[playerSlot].IsTester)
                    {
                        if (steamData.TryGetProperty("SmolGif", out JsonElement smolGifElement))
                        {
                            playerTimers[playerSlot].TesterSparkleGif = smolGifElement.GetString() ?? "";
                        }

                        if (steamData.TryGetProperty("BigGif", out JsonElement bigGifElement))
                        {
                            playerTimers[playerSlot].TesterPausedGif = bigGifElement.GetString() ?? "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error in IsPlayerATester: {ex.Message}");
            }
        }

        public void TimerOnTick()
        {
            var updates = new Dictionary<int, PlayerTimerInfo>();
            foreach (CCSPlayerController player in connectedPlayers.Values)
            {
                if (player == null) continue;

                if(playerTimers[player.Slot].IsTimerRunning && player.Pawn.Value.MoveType == MoveType_t.MOVETYPE_NOCLIP)
                {
                    player.Pawn.Value.MoveType = MoveType_t.MOVETYPE_WALK;
                    player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} 禁止NOCLIP飞行");
                    playerTimers[player.Slot].IsTimerRunning = false;
                }

                if (playerTimers.TryGetValue(player.Slot, out PlayerTimerInfo playerTimer) && IsAllowedPlayer(player))
                {
                    if (!IsAllowedPlayer(player))
                    {
                        playerTimer.IsTimerRunning = false;
                        playerTimer.TimerTicks = 0;
                        playerCheckpoints.Remove(player.Slot);
                        playerTimer.TicksSinceLastCmd++;
                        return;
                    }

                    bool isTimerRunning = playerTimer.IsTimerRunning;
                    int timerTicks = playerTimer.TimerTicks;
                    PlayerButtons? playerButtons = player.Buttons;
                    StringBuilder stringBuilder = new StringBuilder();

                    stringBuilder.Clear();
                    string formattedPlayerVel = Math.Round(use2DSpeed   ? player.PlayerPawn.Value.AbsVelocity.Length2D()
                                                                        : player.PlayerPawn.Value.AbsVelocity.Length())
                                                                        .ToString("0000");
                    string formattedPlayerPre = Math.Round(ParseVector(playerTimer.PreSpeed ?? "0 0 0").Length2D()).ToString("000");
                    string playerTime = FormatTime(timerTicks);
                    string playerBonusTime = FormatTime(playerTimer.BonusTimerTicks);
                    string timerLine = playerTimer.IsBonusTimerRunning
                                        ? $" <font color='gray' class='fontSize-s'>奖励关 {playerTimer.BonusStage}</font> <font class='fontSize-l' color='{primaryHUDcolor}'>{playerBonusTime}</font> <br>"
                                        : isTimerRunning
                                            ? $" <font color='gray' class='fontSize-s'>{GetPlayerPlacement(player)}</font> <font class='fontSize-l' color='{primaryHUDcolor}'>{playerTime}</font>{((playerTimer.CurrentMapStage != 0 && useStageTriggers == true) ? $"<font color='gray' class='fontSize-s'> {playerTimer.CurrentMapStage}/{stageTriggerCount}</font>" : "")} <br>"
                                            : "";
                    string veloLine = $" {(playerTimer.IsTester ? playerTimer.TesterSparkleGif : "")}<font class='fontSize-s' color='{tertiaryHUDcolor}'>速度</font> <font class='fontSize-l' color='{secondaryHUDcolor}'>{formattedPlayerVel}</font> <font class='fontSize-s' color='gray'>({formattedPlayerPre})</font>{(playerTimer.IsTester ? playerTimer.TesterSparkleGif : "")} <br>";
                    string infoLine = $"{playerTimer.RankHUDString}" +
                                        $"{(currentMapTier != null ? $" | Tier: {currentMapTier}" : "")}" +
                                        $"{(currentMapType != null ? $" | {currentMapType}" : "")}" +
                                        $"{((currentMapType == null && currentMapTier == null) ? $" {currentMapName} " : "")} </font> <br> <font class='fontSize-s' color='orange'> HIMENEKO.CN </font>";

                    stringBuilder.Clear();
                    stringBuilder.Append($"{((playerButtons & PlayerButtons.Moveleft) != 0 ? "🅰" : "_")} " +
                                        $"{((playerButtons & PlayerButtons.Forward) != 0 ? "🆆" : "_")} " +
                                        $"{((playerButtons & PlayerButtons.Moveright) != 0 ? "🅳" : "_")} " +
                                        $"{((playerButtons & PlayerButtons.Back) != 0 ? "🆂" : "_")} " +
                                        $"{((playerButtons & PlayerButtons.Jump) != 0 ? "🅹" : "_")} " +
                                        $"{((playerButtons & PlayerButtons.Duck) != 0 ? "🅲" : "_")}");

                    string keysLineNoHtml = stringBuilder.ToString();

                    stringBuilder.Clear();
                    stringBuilder.Append(timerLine)
                                .Append(veloLine)
                                .Append(infoLine)
                                .Append(playerTimer.IsTester && !isTimerRunning && !playerTimer.IsBonusTimerRunning ? playerTimer.TesterPausedGif : "");

                    string hudContent = stringBuilder.ToString();

                    updates[player.Slot] = playerTimer;

                    var @event = new EventShowSurvivalRespawnStatus(false)
                    {
                        LocToken = hudContent,
                        Duration = 999,
                        Userid = player
                    };

                    if (playerTimer.HideTimerHud != true)
                    {
                        @event.FireEvent(false);
                    }

                    if (playerTimer.HideKeys != true)
                    {
                        player.PrintToCenter(keysLineNoHtml);
                    }

                    if (isTimerRunning)
                    {
                        playerTimer.TimerTicks++;
                    }
                    else if (playerTimer.IsBonusTimerRunning)
                    {
                        playerTimer.BonusTimerTicks++;
                    }

                    if (!useTriggers)
                    {
                        CheckPlayerCoords(player);
                    }

                    if (forcePlayerSpeedEnabled == true) ForcePlayerSpeed(player, player.Pawn.Value.WeaponServices.ActiveWeapon.Value.DesignerName);

                    if (playerTimer.RankHUDString == null && playerTimer.IsRankPbCached == false)
                    {
                        SharpTimerDebug($"{player.PlayerName} has rank and pb null... calling handler");
                        _ = RankCommandHandler(player, player.SteamID.ToString(), player.Slot, player.PlayerName, true);
                        playerTimer.IsRankPbCached = true;
                    }

                    if (removeCollisionEnabled == true)
                    {
                        if (player.PlayerPawn.Value.Collision.CollisionGroup != (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING || player.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup != (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING)
                        {
                            SharpTimerDebug($"{player.PlayerName} has wrong collision group... RemovePlayerCollision");
                            RemovePlayerCollision(player);
                        }
                    }

                    if (playerTimer.MovementService != null && removeCrouchFatigueEnabled == true)
                    {
                        if (playerTimer.MovementService.DuckSpeed != 7.0f)
                        {
                            playerTimer.MovementService.DuckSpeed = 7.0f;
                        }
                    }

                    if (!player.PlayerPawn.Value.OnGroundLastTick)
                    {
                        playerTimer.TicksInAir++;
                        if (playerTimer.TicksInAir == 1)
                        {
                            playerTimer.PreSpeed = $"{player.PlayerPawn.Value.AbsVelocity.X.ToString()} {player.PlayerPawn.Value.AbsVelocity.Y.ToString()} {player.PlayerPawn.Value.AbsVelocity.Z.ToString()}";
                        }
                    }
                    else
                    {
                        playerTimer.TicksInAir = 0;
                    }

                    if (playerTimer.TicksSinceLastCmd < cmdCooldown) playerTimer.TicksSinceLastCmd++;

                    playerButtons = null;
                    formattedPlayerVel = null;
                    formattedPlayerPre = null;
                    playerTime = null;
                    playerBonusTime = null;
                    keysLineNoHtml = null;
                    hudContent = null;
                    @event = null;
                }
            }

            foreach (var update in updates)
            {
                playerTimers[update.Key] = update.Value;
            }
        }

        public void PrintAllEnabledCommands(CCSPlayerController player)
        {
            SharpTimerDebug($"Printing Commands for {player.PlayerName}");
            player.PrintToChat($"{msgPrefix}可用指令:");

            if (respawnEnabled) player.PrintToChat($"{msgPrefix}!r (css_r) - 重生");
            if (topEnabled) player.PrintToChat($"{msgPrefix}!top (css_top) - 地图排行前十");
            if (rankEnabled) player.PrintToChat($"{msgPrefix}!rank (css_rank) - 显示你当前的排行");
            if (goToEnabled) player.PrintToChat($"{msgPrefix}!goto <name> (css_goto) - tp到一个玩家那");

            if (cpEnabled)
            {
                player.PrintToChat($"{msgPrefix}!cp (css_cp) - 设置记录点");
                player.PrintToChat($"{msgPrefix}!tp (css_tp) - 传送到最后一个记录点");
                player.PrintToChat($"{msgPrefix}!prevcp (css_prevcp) - 传送到上一个记录点");
                player.PrintToChat($"{msgPrefix}!nextcp (css_nextcp) - 传送到下一个记录点");
            }
        }

        private void CheckPlayerCoords(CCSPlayerController? player)
        {
            if (player == null || !IsAllowedPlayer(player))
            {
                return;
            }

            Vector incorrectVector = new Vector(0, 0, 0);
            Vector? playerPos = player.Pawn?.Value.CBodyComponent?.SceneNode.AbsOrigin;

            if (playerPos == null || currentMapStartC1 == incorrectVector || currentMapStartC2 == incorrectVector || currentMapEndC1 == incorrectVector || currentMapEndC2 == incorrectVector)
            {
                return;
            }

            bool isInsideStartBox = IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2);
            bool isInsideEndBox = IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2);

            if (!isInsideStartBox && isInsideEndBox)
            {
                OnTimerStop(player);
            }
            else if (isInsideStartBox)
            {
                OnTimerStart(player);

                if ((maxStartingSpeedEnabled == true && use2DSpeed == false && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length()) > maxStartingSpeed) ||
                    (maxStartingSpeedEnabled == true && use2DSpeed == true  && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed))
                {
                    Action<CCSPlayerController?, float, bool> adjustVelocity = use2DSpeed ? AdjustPlayerVelocity2D : AdjustPlayerVelocity;
                    adjustVelocity(player, maxStartingSpeed, true);       
                }
            }
        }

        public void ForcePlayerSpeed(CCSPlayerController player, string activeWeapon)
        {

            activeWeapon ??= "no_knife";
            if (!weaponSpeedLookup.TryGetValue(activeWeapon, out WeaponSpeedStats weaponStats) || !player.IsValid) return;

            player.PlayerPawn.Value.VelocityModifier = (float)(forcedPlayerSpeed / weaponStats.GetSpeed(player.PlayerPawn.Value.IsWalking));
        }

        private void AdjustPlayerVelocity(CCSPlayerController? player, float velocity, bool forceNoDebug = false)
        {
            if (!IsAllowedPlayer(player)) return;

            var currentX = player.PlayerPawn.Value.AbsVelocity.X;
            var currentY = player.PlayerPawn.Value.AbsVelocity.Y;
           var currentZ = player.PlayerPawn.Value.AbsVelocity.Z;

            var currentSpeed3D = Math.Sqrt(currentX * currentX + currentY * currentY + currentZ * currentZ);

            var normalizedX = currentX / currentSpeed3D;
            var normalizedY = currentY / currentSpeed3D;
            var normalizedZ = currentZ / currentSpeed3D;
            var adjustedX = normalizedX * velocity; // Adjusted speed limit
            var adjustedY = normalizedY * velocity; // Adjusted speed limit
            var adjustedZ = normalizedZ * velocity; // Adjusted speed limit

            player.PlayerPawn.Value.AbsVelocity.X = (float)adjustedX;
            player.PlayerPawn.Value.AbsVelocity.Y = (float)adjustedY;
            player.PlayerPawn.Value.AbsVelocity.Z = (float)adjustedZ;
            if (!forceNoDebug) SharpTimerDebug($"Adjusted Velo for {player.PlayerName} to {player.PlayerPawn.Value.AbsVelocity}");
        }

        private void AdjustPlayerVelocity2D(CCSPlayerController? player, float velocity, bool forceNoDebug = false)
        {
            if (!IsAllowedPlayer(player)) return;

            var currentX = player.PlayerPawn.Value.AbsVelocity.X;
            var currentY = player.PlayerPawn.Value.AbsVelocity.Y;
            var currentSpeed2D = Math.Sqrt(currentX * currentX + currentY * currentY);
            var normalizedX = currentX / currentSpeed2D;
            var normalizedY = currentY / currentSpeed2D;
            var adjustedX = normalizedX * velocity; // Adjusted speed limit
            var adjustedY = normalizedY * velocity; // Adjusted speed limit
            player.PlayerPawn.Value.AbsVelocity.X = (float)adjustedX;
            player.PlayerPawn.Value.AbsVelocity.Y = (float)adjustedY;
            if (!forceNoDebug) SharpTimerDebug($"Adjusted Velo for {player.PlayerName} to {player.PlayerPawn.Value.AbsVelocity}");
        }

        private void RemovePlayerCollision(CCSPlayerController? player)
        {
            if (removeCollisionEnabled == false || player == null) return;

            player.PlayerPawn.Value.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING;
            player.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING;
            VirtualFunctionVoid<nint> collisionRulesChanged = new VirtualFunctionVoid<nint>(player.PlayerPawn.Value.Handle, OnCollisionRulesChangedOffset.Get());
            collisionRulesChanged.Invoke(player.PlayerPawn.Value.Handle);
            SharpTimerDebug($"Removed Collison for {player.PlayerName}");
        }

        private void HandlePlayerStageTimes(CCSPlayerController player, nint triggerHandle)
        {
            if (!IsAllowedPlayer(player) || playerTimers[player.Slot].CurrentMapStage == stageTriggers[triggerHandle])
            {
                return;
            }

            SharpTimerDebug($"Player {player.PlayerName} has a stage trigger with handle {triggerHandle}");
            var (previousStageTime, previousStageSpeed) = GetStageTime(player.SteamID.ToString(), stageTriggers[triggerHandle]);

            string currentStageSpeed = Math.Round(use2DSpeed    ? player.PlayerPawn.Value.AbsVelocity.Length2D()
                                                                : player.PlayerPawn.Value.AbsVelocity.Length())
                                                                .ToString("0000");
            if (previousStageTime != 0)
            {
                player.PrintToChat(msgPrefix + $" 进入阶段: {stageTriggers[triggerHandle]}");
                player.PrintToChat(msgPrefix + $" 时长: {ChatColors.White}[{primaryChatColor}{FormatTime(playerTimers[player.Slot].TimerTicks)}{ChatColors.White}] [{FormatTimeDifference(playerTimers[player.Slot].TimerTicks, previousStageTime)}{ChatColors.White}]");
                player.PrintToChat(msgPrefix + $" 速度: {ChatColors.White}[{primaryChatColor}{currentStageSpeed}u/s{ChatColors.White}] [{FormatSpeedDifferenceFromString(currentStageSpeed, previousStageSpeed)}u/s{ChatColors.White}]");
            }

            if (playerTimers[player.Slot].StageVelos != null && playerTimers[player.Slot].StageTimes != null && playerTimers[player.Slot].IsTimerRunning == true)
            {
                playerTimers[player.Slot].StageTimes[stageTriggers[triggerHandle]] = playerTimers[player.Slot].TimerTicks;
                playerTimers[player.Slot].StageVelos[stageTriggers[triggerHandle]] = $"{currentStageSpeed}";
                SharpTimerDebug($"Player {player.PlayerName} Entering stage {stageTriggers[triggerHandle]} Time {playerTimers[player.Slot].StageTimes[stageTriggers[triggerHandle]]}");
            }

            playerTimers[player.Slot].CurrentMapStage = stageTriggers[triggerHandle];
        }

        private void HandlePlayerCheckpointTimes(CCSPlayerController player, nint triggerHandle)
        {
            if (!IsAllowedPlayer(player) || playerTimers[player.Slot].CurrentMapCheckpoint == cpTriggers[triggerHandle])
            {
                return;
            }

            if( useStageTriggers == true) //use stagetime instead
            {
                playerTimers[player.Slot].CurrentMapCheckpoint = cpTriggers[triggerHandle];
                return;
            }

            SharpTimerDebug($"Player {player.PlayerName} has a checkpoint trigger with handle {triggerHandle}");
            var (previousStageTime, previousStageSpeed) = GetStageTime(player.SteamID.ToString(), cpTriggers[triggerHandle]);

            string currentStageSpeed = Math.Round(use2DSpeed    ? player.PlayerPawn.Value.AbsVelocity.Length2D()
                                                                : player.PlayerPawn.Value.AbsVelocity.Length())
                                                                .ToString("0000");

            if (previousStageTime != 0)
            {
                player.PrintToChat(msgPrefix + $" 记录点: {cpTriggers[triggerHandle]}");
                player.PrintToChat(msgPrefix + $" 时长: {ChatColors.White}[{primaryChatColor}{FormatTime(playerTimers[player.Slot].TimerTicks)}{ChatColors.White}] [{FormatTimeDifference(playerTimers[player.Slot].TimerTicks, previousStageTime)}{ChatColors.White}]");
                player.PrintToChat(msgPrefix + $" 速度: {ChatColors.White}[{primaryChatColor}{currentStageSpeed}u/s{ChatColors.White}] [{FormatSpeedDifferenceFromString(currentStageSpeed, previousStageSpeed)}u/s{ChatColors.White}]");
            }

            if (playerTimers[player.Slot].StageVelos != null && playerTimers[player.Slot].StageTimes != null && playerTimers[player.Slot].IsTimerRunning == true)
            {
                playerTimers[player.Slot].StageTimes[cpTriggers[triggerHandle]] = playerTimers[player.Slot].TimerTicks;
                playerTimers[player.Slot].StageVelos[cpTriggers[triggerHandle]] = $"{currentStageSpeed}";
                SharpTimerDebug($"Player {player.PlayerName} Entering checkpoint {cpTriggers[triggerHandle]} Time {playerTimers[player.Slot].StageTimes[cpTriggers[triggerHandle]]}");
            }

            playerTimers[player.Slot].CurrentMapCheckpoint = cpTriggers[triggerHandle];
        }

        public (int time, string speed) GetStageTime(string steamId, int stageIndex)
        {
            string fileName = $"{currentMapName.ToLower()}_stage_times.json";
            string playerStageRecordsPath = Path.Join(gameDir, "csgo", "cfg", "SharpTimer", "PlayerStageData", fileName);

            string jsonContent = "";
            if (File.Exists(playerStageRecordsPath))
            {
                jsonContent = File.ReadAllText(playerStageRecordsPath);
            }

            Dictionary<string, PlayerStageData> playerData;
            if (!string.IsNullOrEmpty(jsonContent))
            {
                playerData = JsonSerializer.Deserialize<Dictionary<string, PlayerStageData>>(jsonContent);

                // Check if the given Steam ID exists in the playerData dictionary
                if (playerData.TryGetValue(steamId, out var playerStageData))
                {
                    // Check if the stage index exists in the player's data
                    if (playerStageData.StageTimes != null && playerStageData.StageTimes.TryGetValue(stageIndex, out var time) &&
                        playerStageData.StageVelos != null && playerStageData.StageVelos.TryGetValue(stageIndex, out var speed))
                    {
                        return (time, speed);
                    }
                }
            }

            return (0, string.Empty);
        }

        public void DumpPlayerStageTimesToJson(CCSPlayerController? player)
        {
            if (!IsAllowedPlayer(player)) return;

            string fileName = $"{currentMapName.ToLower()}_stage_times.json";
            string playerStageRecordsPath = Path.Join(gameDir, "csgo", "cfg", "SharpTimer", "PlayerStageData", fileName);

            string jsonContent = "";
            if (File.Exists(playerStageRecordsPath))
            {
                jsonContent = File.ReadAllText(playerStageRecordsPath);
            }

            Dictionary<string, PlayerStageData> playerData;
            if (!string.IsNullOrEmpty(jsonContent))
            {
                playerData = JsonSerializer.Deserialize<Dictionary<string, PlayerStageData>>(jsonContent);
            }
            else
            {
                playerData = new Dictionary<string, PlayerStageData>();
            }

            string playerId = player.SteamID.ToString();

            if (!playerData.ContainsKey(playerId))
            {
                playerData[playerId] = new PlayerStageData();
            }

            playerData[playerId].StageTimes = playerTimers[player.Slot].StageTimes;
            playerData[playerId].StageVelos = playerTimers[player.Slot].StageVelos;

            string updatedJson = JsonSerializer.Serialize(playerData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(playerStageRecordsPath, updatedJson);
        }

        private int GetPreviousPlayerRecord(CCSPlayerController? player, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player)) return 0;

            string mapRecordsPath = Path.Combine(playerRecordsPath, bonusX == 0 ? $"{currentMapName}.json" : $"{currentMapName}_bonus{bonusX}.json");
            string steamId = player.SteamID.ToString();

            Dictionary<string, PlayerRecord> records;
            if (File.Exists(mapRecordsPath))
            {
                string json = File.ReadAllText(mapRecordsPath);
                records = JsonSerializer.Deserialize<Dictionary<string, PlayerRecord>>(json) ?? new Dictionary<string, PlayerRecord>();

                if (records.ContainsKey(steamId))
                {
                    return records[steamId].TimerTicks;
                }
            }

            return 0;
        }

        public string GetPlayerPlacement(CCSPlayerController? player)
        {
            if (!IsAllowedPlayer(player) || !playerTimers[player.Slot].IsTimerRunning) return "";


            int currentPlayerTime = playerTimers[player.Slot].TimerTicks;

            int placement = 1;

            foreach (var kvp in SortedCachedRecords.Take(100))
            {
                int recordTimerTicks = kvp.Value.TimerTicks;

                if (currentPlayerTime > recordTimerTicks)
                {
                    placement++;
                }
                else
                {
                    break;
                }
            }
            if (placement > 100)
            {
                return "#100" + "+";
            }
            else
            {
                return "#" + placement;
            }
        }

        public async Task<string> GetPlayerPlacementWithTotal(CCSPlayerController? player, string steamId, string playerName, bool getRankImg = false)
        {
            if (!IsAllowedPlayer(player))
            {
                return "";
            }

            int savedPlayerTime;
            if (useMySQL == true)
            {
                savedPlayerTime = await GetPreviousPlayerRecordFromDatabase(player, steamId, currentMapName, playerName);
            }
            else
            {
                savedPlayerTime = GetPreviousPlayerRecord(player);
            }

            if (savedPlayerTime == 0 && getRankImg == false)
            {
                return "无排名";
            }
            else if (savedPlayerTime == 0)
            {
                return "";
            }

            Dictionary<string, PlayerRecord> sortedRecords;
            if (useMySQL == true)
            {
                sortedRecords = await GetSortedRecordsFromDatabase();
            }
            else
            {
                sortedRecords = GetSortedRecords();
            }

            int placement = 1;

            foreach (var kvp in sortedRecords)
            {
                int recordTimerTicks = kvp.Value.TimerTicks; // Get the timer ticks from the dictionary value

                if (savedPlayerTime > recordTimerTicks)
                {
                    placement++;
                }
                else
                {
                    break;
                }
            }

            int totalPlayers = sortedRecords.Count;

            string rank;

            if (getRankImg)
            {
                double percentage = (double)placement / totalPlayers * 100;

                if (percentage <= 1)
                    rank = "<img src='https://i.imgur.com/mL4Z8ZW.png' class=''>";
                else if (percentage <= 2)
                    rank = "<img src='https://i.imgur.com/ZOC1Knl.png' class=''>";
                else if (percentage <= 3)
                    rank = "<img src='https://i.imgur.com/ZbXHaik.png' class=''>";
                else if (percentage <= 4)
                    rank = "<img src='https://i.imgur.com/JzofMpi.png' class=''>";
                else if (percentage <= 5)
                    rank = "<img src='https://i.imgur.com/PgRSBWk.png' class=''>";
                else if (percentage <= 6)
                    rank = "<img src='https://i.imgur.com/0OF3ij0.png' class=''>";
                else if (percentage <= 15)
                    rank = "<img src='https://i.imgur.com/6e3cSwY.png' class=''>";
                else if (percentage <= 20)
                    rank = "<img src='https://i.imgur.com/6pysO2O.png' class=''>";
                else if (percentage <= 25)
                    rank = "<img src='https://i.imgur.com/EgqfpFR.png' class=''>";
                else if (percentage <= 30)
                    rank = "<img src='https://i.imgur.com/IGa9B0o.png' class=''>";
                else if (percentage <= 35)
                    rank = "<img src='https://i.imgur.com/ObC3Y9Z.png' class=''>";
                else if (percentage <= 40)
                    rank = "<img src='https://i.imgur.com/9vPImVK.png' class=''>";
                else if (percentage <= 45)
                    rank = "<img src='https://i.imgur.com/lklvcW2.png' class=''>";
                else
                    rank = "<img src='https://i.imgur.com/HZf9EqX.png' class=''>";
            }
            else
            {
                rank = $"排名 [{placement}/{totalPlayers}]";
            }

            return rank;
        }

        public void SavePlayerTime(CCSPlayerController? player, int timerTicks, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player)) return;
            if ((bonusX == 0 && playerTimers[player.Slot].IsTimerRunning == false) || (bonusX != 0 && playerTimers[player.Slot].IsBonusTimerRunning == false)) return;

            SharpTimerDebug($"Saving player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} of {timerTicks} ticks for {player.PlayerName} to json");
            string mapRecordsPath = Path.Combine(playerRecordsPath, bonusX == 0 ? $"{currentMapName}.json" : $"{currentMapName}_bonus{bonusX}.json");

            Dictionary<string, PlayerRecord> records;
            if (File.Exists(mapRecordsPath))
            {
                string json = File.ReadAllText(mapRecordsPath);
                records = JsonSerializer.Deserialize<Dictionary<string, PlayerRecord>>(json) ?? new Dictionary<string, PlayerRecord>();
            }
            else
            {
                records = new Dictionary<string, PlayerRecord>();
            }

            string steamId = player.SteamID.ToString();
            string playerName = player.PlayerName;

            if (!records.ContainsKey(steamId) || records[steamId].TimerTicks > timerTicks)
            {
                records[steamId] = new PlayerRecord
                {
                    PlayerName = playerName,
                    TimerTicks = timerTicks
                };

                string updatedJson = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(mapRecordsPath, updatedJson);
                if ((stageTriggerCount != 0 || cpTriggerCount != 0) && bonusX == 0) DumpPlayerStageTimesToJson(player);
            }
        }
    }
}