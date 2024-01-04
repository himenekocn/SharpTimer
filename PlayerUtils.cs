using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        private bool IsPlayerATester(string steamId)
        {
            return testerPersonalGifs.ContainsKey(steamId);
        }

        public static Tuple<string, string> GetTesterPersonalGif(string steamId)
        {
            if (testerPersonalGifs.ContainsKey(steamId))
            {
                return testerPersonalGifs[steamId];
            }
            else
            {
                return Tuple.Create("Index not found", "");
            }
        }

        public void HandleTesterGifs(int playerSlot, string steamId)
        {
            Tuple<string, string> gifs = GetTesterPersonalGif(steamId);
            playerTimers[playerSlot].TesterSparkleGif = gifs.Item1;
            playerTimers[playerSlot].TesterPausedGif = gifs.Item2;
        }

        public void TimerOnTick(CCSPlayerController player, int playerSlot)
        {
            if (!IsAllowedPlayer(player))
            {
                playerTimers[playerSlot].IsTimerRunning = false;
                playerTimers[playerSlot].TimerTicks = 0;
                playerCheckpoints.Remove(playerSlot);
                playerTimers[playerSlot].TicksSinceLastCmd++;
                return;
            }

            var buttons = player.Buttons;
            var formattedPlayerVel = Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()).ToString().PadLeft(4, '0');
            var formattedPlayerPre = Math.Round(ParseVector(playerTimers[playerSlot].PreSpeed ?? "0 0 0").Length2D()).ToString().PadLeft(3, '0');
            var playerTime = FormatTime(playerTimers[playerSlot].TimerTicks);
            var playerBonusTime = FormatTime(playerTimers[playerSlot].BonusTimerTicks);
            var timerLine = playerTimers[playerSlot].IsBonusTimerRunning
                ? $" <font color='gray' class='fontSize-s'>奖励关: {playerTimers[playerSlot].BonusStage}</font> <font class='fontSize-l' color='{primaryHUDcolor}'>{playerBonusTime}</font> <br>"
                : playerTimers[playerSlot].IsTimerRunning
                    ? $" <font color='gray' class='fontSize-s'>{GetPlayerPlacement(player)}</font> <font class='fontSize-l' color='{primaryHUDcolor}'>{playerTime}</font>{((playerTimers[playerSlot].CurrentMapStage != 0 && useStageTriggers == true) ? $"<font color='gray' class='fontSize-s'> {playerTimers[playerSlot].CurrentMapStage}/{stageTriggerCount}</font>" : "")} <br>"
                    : "";

            var veloLine = $" {(playerTimers[playerSlot].IsTester ? playerTimers[playerSlot].TesterSparkleGif : "")}<font class='fontSize-s' color='{tertiaryHUDcolor}'>速度 </font> <font class='fontSize-l' color='{secondaryHUDcolor}'>{formattedPlayerVel}</font> <font class='fontSize-s' color='gray'>({formattedPlayerPre})</font>{(playerTimers[playerSlot].IsTester ? playerTimers[playerSlot].TesterSparkleGif : "")} <br>";
            var veloLineAlt = $" {GetSpeedBar(Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()))} ";
            var infoLine = $"{playerTimers[playerSlot].RankHUDString}" +
                              $"{(currentMapTier != null ? $" | Tier: {currentMapTier}" : "")}" +
                              $"{(currentMapType != null ? $" | {currentMapType}" : "")}" +
                              $"{((currentMapType == null && currentMapTier == null) ? $" {currentMapName} " : "")} </font><br><font color='orange' class='fontSize-s'> himeneko.cn</font>";

            var forwardKey = playerTimers[playerSlot].Azerty ? "🆉" : "🆆";
            var leftKey = playerTimers[playerSlot].Azerty ? "🆀" : "🅰";

            var keysLineNoHtml = $"{((buttons & PlayerButtons.Moveleft) != 0 ? leftKey : "_")} " +
                                    $"{((buttons & PlayerButtons.Forward) != 0 ? forwardKey : "_")} " +
                                    $"{((buttons & PlayerButtons.Moveright) != 0 ? "🅳" : "_")} " +
                                    $"{((buttons & PlayerButtons.Back) != 0 ? "🆂" : "_")} " +
                                    $"{((buttons & PlayerButtons.Jump) != 0 ? "🅹" : "_")} " +
                                    $"{((buttons & PlayerButtons.Duck) != 0 ? "🅲" : "_")}";

            var hudContentBuilder = new StringBuilder();
            hudContentBuilder.Append(timerLine);
            hudContentBuilder.Append(veloLine);
            if (alternativeSpeedometer) hudContentBuilder.Append(veloLineAlt);
            hudContentBuilder.Append(infoLine);
            if (playerTimers[playerSlot].IsTester && !playerTimers[playerSlot].IsTimerRunning && !playerTimers[playerSlot].IsBonusTimerRunning)
                hudContentBuilder.Append(playerTimers[playerSlot].TesterPausedGif);

            var hudContent = hudContentBuilder.ToString();

            if (playerTimers[playerSlot].HideTimerHud != true)
            {
                player.PrintToCenterHtml(hudContent);
            }

            if (playerTimers[playerSlot].HideKeys != true)
            {
                player.PrintToCenter(keysLineNoHtml);
            }

            if (playerTimers[playerSlot].IsTimerRunning)
            {
                playerTimers[playerSlot].TimerTicks++;
            }
            else if (playerTimers[playerSlot].IsBonusTimerRunning)
            {
                playerTimers[playerSlot].BonusTimerTicks++;
            }

            if (!useTriggers)
            {
                CheckPlayerCoords(player);
            }

            if (playerTimers[playerSlot].MovementService != null && removeCrouchFatigueEnabled == true)
            {
                if (playerTimers[playerSlot].MovementService.DuckSpeed != 7.0f)
                {
                    playerTimers[playerSlot].MovementService.DuckSpeed = 7.0f;
                }
            }

            //find a better solution for this by combining all into 1 string to store
            if (playerTimers[playerSlot].RankHUDString == null && playerTimers[playerSlot].IsRankPbCached == false)
            {
                SharpTimerDebug($"{player.PlayerName} has rank and pb null... calling handler");
                _ = RankCommandHandler(player, player.SteamID.ToString(), playerSlot, player.PlayerName, true);
                playerTimers[playerSlot].IsRankPbCached = true;
            }

            if (removeCollisionEnabled == true)
            {
                if (player.PlayerPawn.Value.Collision.CollisionGroup != (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING || player.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup != (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING)
                {
                    SharpTimerDebug($"{player.PlayerName} has wrong collision group... RemovePlayerCollision");
                    RemovePlayerCollision(player);
                }
            }

            if (!player.PlayerPawn.Value.OnGroundLastTick)
            {
                playerTimers[playerSlot].TicksInAir++;
                if (playerTimers[playerSlot].TicksInAir == 1)
                {
                    playerTimers[playerSlot].PreSpeed = $"{player.PlayerPawn.Value.AbsVelocity.X} {player.PlayerPawn.Value.AbsVelocity.Y} {player.PlayerPawn.Value.AbsVelocity.Z}";
                }
            }
            else
            {
                playerTimers[playerSlot].TicksInAir = 0;
            }

            playerTimers[playerSlot].TicksSinceLastCmd++;
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
            Vector playerPos = player.Pawn?.Value.CBodyComponent?.SceneNode.AbsOrigin ?? incorrectVector;

            if (new[] { playerPos, currentMapStartC1, currentMapStartC2, currentMapEndC1, currentMapEndC2 }.All(v => v != incorrectVector))
            {
                bool isInsideStartBox = IsVectorInsideBox(playerPos, currentMapStartC1, currentMapStartC2);
                bool isInsideEndBox = IsVectorInsideBox(playerPos, currentMapEndC1, currentMapEndC2);

                if (!isInsideStartBox && isInsideEndBox)
                {
                    OnTimerStop(player);
                }
                else if (isInsideStartBox)
                {
                    OnTimerStart(player);

                    if (maxStartingSpeedEnabled == true && Math.Round(player.PlayerPawn.Value.AbsVelocity.Length2D()) > maxStartingSpeed)
                    {
                        AdjustPlayerVelocity(player, maxStartingSpeed, true);
                    }
                }
            }
        }

        private void AdjustPlayerVelocity(CCSPlayerController? player, float velocity, bool forceNoDebug = false)
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
            int previousStageTime = GetStageTime(player.SteamID.ToString(), stageTriggers[triggerHandle]);

            if (previousStageTime != 0)
            {
                player.PrintToChat(msgPrefix + $" 进入阶段: {stageTriggers[triggerHandle]} {ParseColorToSymbol(primaryHUDcolor)}[{FormatTime(playerTimers[player.Slot].TimerTicks)}{ChatColors.White}] [{FormatTimeDifference(playerTimers[player.Slot].TimerTicks, previousStageTime)}{ChatColors.White}]");
            }

            if (playerTimers[player.Slot].StageRecords != null && playerTimers[player.Slot].IsTimerRunning == true)
            {
                playerTimers[player.Slot].StageRecords[stageTriggers[triggerHandle]] = playerTimers[player.Slot].TimerTicks;
                SharpTimerDebug($"Player {player.PlayerName} Entering stage {stageTriggers[triggerHandle]} Time {playerTimers[player.Slot].StageRecords[stageTriggers[triggerHandle]]}");
            }

            playerTimers[player.Slot].CurrentMapStage = stageTriggers[triggerHandle];
        }

        private void HandlePlayerCheckpointTimes(CCSPlayerController player, nint triggerHandle)
        {
            if (!IsAllowedPlayer(player) || playerTimers[player.Slot].CurrentMapCheckpoint == cpTriggers[triggerHandle])
            {
                return;
            }

            SharpTimerDebug($"Player {player.PlayerName} has a checkpoint trigger with handle {triggerHandle}");
            int previousStageTime = GetStageTime(player.SteamID.ToString(), cpTriggers[triggerHandle]);

            if (previousStageTime != 0)
            {
                player.PrintToChat(msgPrefix + $" 记录点: {cpTriggers[triggerHandle]} {ParseColorToSymbol(primaryHUDcolor)}[{FormatTime(playerTimers[player.Slot].TimerTicks)}{ChatColors.White}] [{FormatTimeDifference(playerTimers[player.Slot].TimerTicks, previousStageTime)}{ChatColors.White}]");
            }

            if (playerTimers[player.Slot].StageRecords != null && playerTimers[player.Slot].IsTimerRunning == true)
            {
                playerTimers[player.Slot].StageRecords[cpTriggers[triggerHandle]] = playerTimers[player.Slot].TimerTicks;
                SharpTimerDebug($"Player {player.PlayerName} Entering checkpoint {cpTriggers[triggerHandle]} Time {playerTimers[player.Slot].StageRecords[cpTriggers[triggerHandle]]}");
            }

            playerTimers[player.Slot].CurrentMapCheckpoint = cpTriggers[triggerHandle];
        }

        public int GetStageTime(string steamId, int stageIndex)
        {
            try
            {
                string fileName = $"{currentMapName.ToLower()}_stage_times.json";
                string playerStageRecordsPath = Path.Join(gameDir + "/csgo/cfg/SharpTimer/PlayerStageData", fileName);

                if (!File.Exists(playerStageRecordsPath))
                {
                    //file doesnt exist so create it
                    File.WriteAllText(playerStageRecordsPath, "{}");
                }

                using (var stream = File.OpenRead(playerStageRecordsPath))
                using (var document = JsonDocument.Parse(stream))
                {
                    var root = document.RootElement;
                    if (root.TryGetProperty(steamId, out var playerRecord))
                    {
                        if (playerRecord.TryGetProperty("StageRecords", out var stageRecords) &&
                            stageRecords.TryGetProperty(stageIndex.ToString(), out var stageTime))
                        {
                            return stageTime.GetInt32();
                        }
                        else
                        {
                            SharpTimerDebug($"Stage {stageIndex} not found for SteamID {steamId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SharpTimerError($"An error occurred in GetStageTime: {ex.Message}");
            }

            return 0;
        }

        private void DumpPlayerStageTimesToJson(CCSPlayerController player)
        {
            string fileName = $"{currentMapName.ToLower()}_stage_times.json";
            string playerStageRecordsPath = Path.Join(gameDir + "/csgo/cfg/SharpTimer/PlayerStageData", fileName);

            var stageTimes = playerTimers[player.Slot].StageRecords;

            var playerStageData = new Dictionary<string, object>
            {
                { "StageRecords", stageTimes }
            };

            if (File.Exists(playerStageRecordsPath))
            {
                var existingData = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(playerStageRecordsPath));

                existingData[player.SteamID.ToString()] = playerStageData;

                string jsonData = JsonSerializer.Serialize(existingData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(playerStageRecordsPath, jsonData);
            }
            else
            {
                var newData = new Dictionary<string, object>
                {
                    { player.SteamID.ToString(), playerStageData }
                };

                string jsonData = JsonSerializer.Serialize(newData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(playerStageRecordsPath, jsonData);
            }

            SharpTimerDebug($"Player {player.PlayerName} stage times for map {currentMapName} dumped to {playerStageRecordsPath}");
        }

        private int GetPreviousPlayerRecord(CCSPlayerController? player, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player)) return 0;

            string currentMapName = bonusX == 0 ? Server.MapName : $"{Server.MapName}_bonus{bonusX}";

            string steamId = player.SteamID.ToString();

            Dictionary<string, Dictionary<string, PlayerRecord>> records;
            if (File.Exists(playerRecordsPath))
            {
                string json = File.ReadAllText(playerRecordsPath);
                records = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PlayerRecord>>>(json) ?? new Dictionary<string, Dictionary<string, PlayerRecord>>();

                if (records.ContainsKey(currentMapName) && records[currentMapName].ContainsKey(steamId))
                {
                    return records[currentMapName][steamId].TimerTicks;
                }
            }

            return 0;
        }

        public string GetPlayerPlacement(CCSPlayerController? player)
        {
            if (!IsAllowedPlayer(player) || !playerTimers[player.Slot].IsTimerRunning) return "";


            int currentPlayerTime = playerTimers[player.Slot].TimerTicks;

            int placement = 1;

            foreach (var kvp in playerTimers[player.Slot].SortedCachedRecords.Take(100))
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

                if (percentage <= 6.4)
                {
                    rank = "<img src='https://i.imgur.com/IFAyj0y.png' class=''>";
                }
                else if (percentage <= 28)
                {
                    rank = "<img src='https://i.imgur.com/5O4OfR5.png' class=''>";
                }
                else if (percentage <= 75.6)
                {
                    rank = "<img src='https://i.imgur.com/pc0TN7z.png' class=''>";
                }
                else if (percentage <= 95.2)
                {
                    rank = "<img src='https://i.imgur.com/kKzdpqu.png' class=''>";
                }
                else
                {
                    rank = "<img src='https://i.imgur.com/gL6Ru8t.png' class=''>";
                }
            }
            else
            {
                rank = $"Rank: {placement}/{totalPlayers}";
            }

            return rank;
        }

        public void SavePlayerTime(CCSPlayerController? player, int timerTicks, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player)) return;
            if ((bonusX == 0 && playerTimers[player.Slot].IsTimerRunning == false) || (bonusX != 0 && playerTimers[player.Slot].IsBonusTimerRunning == false)) return;

            SharpTimerDebug($"Saving player {(bonusX != 0 ? $"bonus {bonusX} time" : "time")} of {timerTicks} ticks for {player.PlayerName} to json");

            string currentMapName = bonusX == 0 ? Server.MapName : $"{Server.MapName}_bonus{bonusX}";
            string steamId = player.SteamID.ToString();
            string playerName = player.PlayerName;

            Dictionary<string, Dictionary<string, PlayerRecord>> records;
            if (File.Exists(playerRecordsPath))
            {
                string json = File.ReadAllText(playerRecordsPath);
                records = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PlayerRecord>>>(json) ?? new Dictionary<string, Dictionary<string, PlayerRecord>>();
            }
            else
            {
                records = new Dictionary<string, Dictionary<string, PlayerRecord>>();
            }

            if (!records.ContainsKey(currentMapName))
            {
                records[currentMapName] = new Dictionary<string, PlayerRecord>();
            }

            if (!records[currentMapName].ContainsKey(steamId) || records[currentMapName][steamId].TimerTicks > timerTicks)
            {
                records[currentMapName][steamId] = new PlayerRecord
                {
                    PlayerName = playerName,
                    TimerTicks = timerTicks
                };

                string updatedJson = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(playerRecordsPath, updatedJson);
                if ((stageTriggerCount != 0 || cpTriggerCount != 0) && bonusX == 0) DumpPlayerStageTimesToJson(player);
            }
        }
    }
}