using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;


namespace SharpTimer
{
    public partial class SharpTimer
    {
        [ConsoleCommand("css_addstartzone", "Adds a startzone to the mapdata.json file")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AddStartZoneCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;

            if (playerTimers[player.Slot].IsAddingStartZone == true)
            {
                playerTimers[player.Slot].IsAddingStartZone = false;
                playerTimers[player.Slot].IsAddingEndZone = false;
                playerTimers[player.Slot].StartZoneC1 = "";
                playerTimers[player.Slot].StartZoneC2 = "";
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Grey}Startzone cancelled...");
            }
            else
            {
                playerTimers[player.Slot].StartZoneC1 = "";
                playerTimers[player.Slot].StartZoneC2 = "";
                playerTimers[player.Slot].IsAddingStartZone = true;
                playerTimers[player.Slot].IsAddingEndZone = false;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} Please stand on one of the opposite start zone corners and type {ParseColorToSymbol(primaryHUDcolor)}!c1 & !c2");
                player.PrintToCenter($" {ChatColors.Grey}Please stand on one of the opposite start zone corners and type !c1 & !c2");
                player.PrintToChat($" {ChatColors.Grey}Type !addendzone again to cancel...");
                player.PrintToChat($" {ChatColors.Grey}Commands:!addstartzone, !addendzone,");
                player.PrintToChat($" {ChatColors.Grey}!addrespawnpos, !savezones");
            }
        }

        [ConsoleCommand("css_addendzone", "Adds a endzone to the mapdata.json file")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AddEndZoneCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;

            if (playerTimers[player.Slot].IsAddingEndZone == true)
            {
                playerTimers[player.Slot].IsAddingStartZone = false;
                playerTimers[player.Slot].IsAddingEndZone = false;
                playerTimers[player.Slot].EndZoneC1 = "";
                playerTimers[player.Slot].EndZoneC2 = "";
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Grey}Endzone cancelled...");
            }
            else
            {
                playerTimers[player.Slot].EndZoneC1 = "";
                playerTimers[player.Slot].EndZoneC2 = "";
                playerTimers[player.Slot].IsAddingStartZone = false;
                playerTimers[player.Slot].IsAddingEndZone = true;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} Please stand on one of the opposite end zone corners and type {ParseColorToSymbol(primaryHUDcolor)}!c1 & !c2");
                player.PrintToCenter($" Please stand on one of the opposite end zone corners and type !c1 & !c2");
                player.PrintToChat($" {ChatColors.Grey}Type !addendzone again to cancel...");
                player.PrintToChat($" {ChatColors.Grey}Commands:!addstartzone, !addendzone,");
                player.PrintToChat($" {ChatColors.Grey}!addrespawnpos, !savezones");
            }
        }

        [ConsoleCommand("css_addrespawnpos", "Adds a RespawnPos to the mapdata.json file")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AddRespawnPosCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;

            // Get the player's current position
            Vector currentPosition = player.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin ?? new Vector(0, 0, 0);

            // Convert position
            string positionString = $"{currentPosition.X} {currentPosition.Y} {currentPosition.Z}";
            playerTimers[player.Slot].RespawnPos = positionString;
            player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} RespawnPos added!");
        }

        [ConsoleCommand("css_savezones", "Saves defined zones")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SaveZonesCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;

            if (playerTimers[player.Slot].EndZoneC1 == null || playerTimers[player.Slot].EndZoneC2 == null || playerTimers[player.Slot].StartZoneC1 == null || playerTimers[player.Slot].StartZoneC2 == null || playerTimers[player.Slot].RespawnPos == null)
            {
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Red} Please make sure you have done all 3 zoning steps (!addstartzone, !addendzone, !addrespawnpos)");
                return;
            }

            // Create a new MapInfo object with the necessary data
            MapInfo newMapInfo = new MapInfo
            {
                MapStartC1 = playerTimers[player.Slot].StartZoneC1,
                MapStartC2 = playerTimers[player.Slot].StartZoneC2,
                MapEndC1 = playerTimers[player.Slot].EndZoneC1,
                MapEndC2 = playerTimers[player.Slot].EndZoneC2,
                RespawnPos = playerTimers[player.Slot].RespawnPos
            };

            // Load existing map data from the JSON file
            string mapdataFileName = $"SharpTimer/MapData/{currentMapName}.json"; // Use the map name in the filename
            string mapdataPath = Path.Join(gameDir + "/csgo/cfg", mapdataFileName);

            // Save the updated data back to the JSON file without including the map name inside the JSON
            string updatedJson = JsonSerializer.Serialize(newMapInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(mapdataPath, updatedJson);

            // For example, you might want to print a message to the player
            player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default}Zones saved successfully! {ChatColors.Grey}Reloading data...");
            Server.ExecuteCommand("mp_restartgame 1");
        }

        [ConsoleCommand("css_c1", "Adds a zone to the mapdata.json file")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AddZoneCorner1Command(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;

            // Get the player's current position
            Vector currentPosition = player.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin ?? new Vector(0, 0, 0);

            // Convert position
            string positionString = $"{currentPosition.X} {currentPosition.Y} {currentPosition.Z}";

            //Start Zone
            if (playerTimers[player.Slot].IsAddingStartZone == true)
            {
                playerTimers[player.Slot].StartZoneC1 = positionString;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} Start Zone Corner 1 added!");
            }

            //End Zone
            if (playerTimers[player.Slot].IsAddingEndZone == true)
            {
                playerTimers[player.Slot].EndZoneC1 = positionString;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} End Zone Corner 1 added!");
            }
        }

        [ConsoleCommand("css_c2", "Adds a zone to the mapdata.json file")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AddZoneCorner2Command(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;

            // Get the player's current position
            Vector currentPosition = player.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin ?? new Vector(0, 0, 0);

            // Convert position
            string positionString = $"{currentPosition.X} {currentPosition.Y} {currentPosition.Z}";

            //Start Zone
            if (playerTimers[player.Slot].IsAddingStartZone == true)
            {
                playerTimers[player.Slot].StartZoneC2 = positionString;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default} Start Zone Corner 2 added!");
                player.PrintToChat($" {ChatColors.Grey}Commands:!addstartzone, !addendzone, !addrespawnpos, !savezones");
                playerTimers[player.Slot].IsAddingStartZone = false;
            }

            //End Zone
            if (playerTimers[player.Slot].IsAddingEndZone == true)
            {
                playerTimers[player.Slot].EndZoneC2 = positionString;
                player.PrintToChat($" {ChatColors.LightPurple}[ZONE TOOL]{ChatColors.Default}End Zone Corner 2 added!");
                player.PrintToChat($" {ChatColors.Grey}Commands:!addstartzone, !addendzone, !addrespawnpos, !savezones");
                playerTimers[player.Slot].IsAddingEndZone = false;
            }
        }

        [ConsoleCommand("css_noclip", "toggles noclip for admin")]
        [RequiresPermissions("@css/root")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AdminNoclipCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;
            SharpTimerDebug($"{player.PlayerName} calling css_noclip...");

            playerTimers[player.Slot].IsNoclipEnabled = playerTimers[player.Slot].IsNoclipEnabled ? false : true;

            if (playerTimers[player.Slot].IsNoclipEnabled)
            {
                player.Pawn.Value.MoveType = MoveType_t.MOVETYPE_WALK;
                SharpTimerDebug($"MoveType set to MOVETYPE_WALK for {player.PlayerName}");
            }
            else
            {
                player.Pawn.Value.MoveType = MoveType_t.MOVETYPE_NOCLIP;
                SharpTimerDebug($"MoveType set to MOVETYPE_NOCLIP for {player.PlayerName}");
            }
        }

        [ConsoleCommand("css_azerty", "Switches layout to AZERTY")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void AzertySwitchCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;
            SharpTimerDebug($"{player.PlayerName} calling css_azerty...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            playerTimers[player.Slot].Azerty = playerTimers[player.Slot].Azerty ? false : true;

            player.PrintToChat($"Azerty Layout set to: {ParseColorToSymbol(primaryHUDcolor)}{playerTimers[player.Slot].Azerty}");
            SharpTimerDebug($"Azerty Layout set to: {playerTimers[player.Slot].Azerty} for {player.PlayerName}");

            //if(useMySQL == true) _ = SavePlayerBoolStatToDatabase(player.SteamID.ToString(), "Azerty", playerTimers[player.Slot].Azerty);

        }

        [ConsoleCommand("css_hud", "Draws/Hides The timer HUD")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void HUDSwitchCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;
            SharpTimerDebug($"{player.PlayerName} calling css_hud...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            playerTimers[player.Slot].HideTimerHud = playerTimers[player.Slot].HideTimerHud ? false : true;

            player.PrintToChat($"Hide Timer HUD set to: {ParseColorToSymbol(primaryHUDcolor)}{playerTimers[player.Slot].HideTimerHud}");
            SharpTimerDebug($"Hide Timer HUD set to: {playerTimers[player.Slot].HideTimerHud} for {player.PlayerName}");

            //if(useMySQL == true) _ = SavePlayerBoolStatToDatabase(player.SteamID.ToString(), "Azerty", playerTimers[player.Slot].HideTimerHud);

        }

        [ConsoleCommand("css_keys", "Draws/Hides HUD Keys")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void KeysSwitchCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;
            SharpTimerDebug($"{player.PlayerName} calling css_keys...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            playerTimers[player.Slot].HideKeys = playerTimers[player.Slot].HideKeys ? false : true;

            player.PrintToChat($"Hide Timer HUD set to: {ParseColorToSymbol(primaryHUDcolor)}{playerTimers[player.Slot].HideKeys}");
            SharpTimerDebug($"Hide Timer HUD set to: {playerTimers[player.Slot].HideKeys} for {player.PlayerName}");

            //if(useMySQL == true) _ = SavePlayerBoolStatToDatabase(player.SteamID.ToString(), "Azerty", playerTimers[player.Slot].HideKeys);

        }

        [ConsoleCommand("css_sounds", "Toggles Sounds")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SoundsSwitchCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;
            SharpTimerDebug($"{player.PlayerName} calling css_sounds...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            playerTimers[player.Slot].SoundsEnabled = playerTimers[player.Slot].SoundsEnabled ? false : true;

            player.PrintToChat($"Timer Sounds set to: {ParseColorToSymbol(primaryHUDcolor)}{playerTimers[player.Slot].SoundsEnabled}");
            SharpTimerDebug($"Timer Sounds set to: {playerTimers[player.Slot].SoundsEnabled} for {player.PlayerName}");

            //if(useMySQL == true) _ = SavePlayerBoolStatToDatabase(player.SteamID.ToString(), "Azerty", playerTimers[player.Slot].SoundsEnabled);

        }

        [ConsoleCommand("css_fov", "Sets the player's FOV")]
        [CommandHelper(minArgs: 1, usage: "[fov]")]
        public void FovCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player) || fovChangerEnabled == false) return;

            if (!Int32.TryParse(command.GetArg(1), out var desiredFov)) return;

            player.DesiredFOV = (uint)desiredFov;
            Utilities.SetStateChanged(player, "CBasePlayerController", "m_iDesiredFOV");
        }

        [ConsoleCommand("css_top", "Prints top players of this map")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void PrintTopRecords(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player) || topEnabled == false) return;
            SharpTimerDebug($"{player.PlayerName} calling css_top...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            _ = PrintTopRecordsHandler(player);
        }

        [ConsoleCommand("css_topbonus", "Prints top players of this map bonus")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void PrintTopBonusRecords(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player) || topEnabled == false) return;
            SharpTimerDebug($"{player.PlayerName} calling css_topbonus...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            if (!int.TryParse(command.ArgString, out int bonusX))
            {
                SharpTimerDebug("css_topbonus conversion failed. The input string is not a valid integer.");
                player.PrintToChat(msgPrefix + $" Please enter a valid Bonus stage i.e: {ParseColorToSymbol(primaryHUDcolor)}!topbonus 1");
                return;
            }

            _ = PrintTopRecordsHandler(player, bonusX);
        }

        public async Task PrintTopRecordsHandler(CCSPlayerController? player, int bonusX = 0)
        {
            if (!IsAllowedPlayer(player) || topEnabled == false) return;
            SharpTimerDebug($"Handling !top for {player.PlayerName}");
            string currentMapNamee = bonusX == 0 ? currentMapName : $"{currentMapName}_bonus{bonusX}";
            Dictionary<string, PlayerRecord> sortedRecords;
            if (useMySQL == true)
            {
                sortedRecords = await GetSortedRecordsFromDatabase(bonusX);
            }
            else
            {
                sortedRecords = GetSortedRecords(bonusX);
            }

            if (sortedRecords.Count == 0 && IsAllowedPlayer(player))
            {
                Server.NextFrame(() => player.PrintToChat(msgPrefix + $" No records available for{(bonusX != 0 ? $" Bonus {bonusX} on" : "")} {currentMapName}."));
                return;
            }

            Server.NextFrame(() =>
            {
                if (IsAllowedPlayer(player)) player.PrintToChat($"{msgPrefix} Top 10 Records for{(bonusX != 0 ? $" Bonus {bonusX} on" : "")} {currentMapName}:");
            });

            int rank = 1;

            foreach (var kvp in sortedRecords.Take(10))
            {
                string playerName = kvp.Value.PlayerName; // Get the player name from the dictionary value
                int timerTicks = kvp.Value.TimerTicks; // Get the timer ticks from the dictionary value

                Server.NextFrame(() =>
                {
                    if (IsAllowedPlayer(player)) player.PrintToChat(msgPrefix + $" #{rank}: {ParseColorToSymbol(primaryHUDcolor)}{playerName} {ChatColors.White}- {ParseColorToSymbol(primaryHUDcolor)}{FormatTime(timerTicks)}");
                    rank++;
                });
            }
        }

        [ConsoleCommand("css_rank", "Tells you your rank on this map")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void RankCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player) || rankEnabled == false) return;
            SharpTimerDebug($"{player.PlayerName} calling css_rank...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            _ = RankCommandHandler(player, player.SteamID.ToString(), player.Slot, player.PlayerName);
        }

        public async Task RankCommandHandler(CCSPlayerController? player, string steamId, int playerSlot, string playerName, bool sendRankToHUD = false)
        {
            if (!IsAllowedPlayer(player)) return;
            SharpTimerDebug($"Handling !rank for {playerName}...");
            string ranking = await GetPlayerPlacementWithTotal(player, steamId, playerName);
            string rankIcon = await GetPlayerPlacementWithTotal(player, steamId, playerName, true);

            int pbTicks;
            if (useMySQL == false)
            {
                pbTicks = GetPreviousPlayerRecord(player);
            }
            else
            {
                pbTicks = await GetPreviousPlayerRecordFromDatabase(player, steamId, currentMapName, playerName);
            }

            string rankHUDstring = $"{(string.IsNullOrEmpty(rankIcon) ? "" : "<font class='fontSize-s' color='gray'>|</font> " + rankIcon)}<font class='fontSize-s' color='gray'>{(string.IsNullOrEmpty(ranking) ? "" : " | " + ranking)}{(pbTicks != 0 ? $" | {FormatTime(pbTicks)}" : "")}";

            Server.NextFrame(() =>
            {
                if (!IsAllowedPlayer(player)) return;
                playerTimers[playerSlot].RankHUDString = rankHUDstring;
            });

            if (sendRankToHUD == false)
            {
                Server.NextFrame(() =>
                {
                    if (!IsAllowedPlayer(player)) return;
                    player.PrintToChat(msgPrefix + $" You are currently {ParseColorToSymbol(primaryHUDcolor)}{ranking}");
                    if (pbTicks != 0) player.PrintToChat(msgPrefix + $" Your current PB: {ParseColorToSymbol(primaryHUDcolor)}{FormatTime(pbTicks)}");
                });
            }
        }

        [ConsoleCommand("css_sr", "Tells you the Server record on this map")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SRCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player) || rankEnabled == false) return;
            SharpTimerDebug($"{player.PlayerName} calling css_sr...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            _ = SRCommandHandler(player);
        }

        public async Task SRCommandHandler(CCSPlayerController? player)
        {
            if (!IsAllowedPlayer(player) || rankEnabled == false) return;
            SharpTimerDebug($"Handling !sr for {player.PlayerName}...");
            Dictionary<string, PlayerRecord> sortedRecords;
            if (useMySQL == false)
            {
                sortedRecords = GetSortedRecords();
            }
            else
            {
                sortedRecords = await GetSortedRecordsFromDatabase();
            }

            if (sortedRecords.Count == 0)
            {
                return;
            }

            Server.NextFrame(() =>
            {
                if (!IsAllowedPlayer(player)) return;
                player.PrintToChat($"{msgPrefix} Current Server Record on {ParseColorToSymbol(primaryHUDcolor)}{currentMapName}{ChatColors.White}: ");
            });

            foreach (var kvp in sortedRecords.Take(1))
            {
                string playerName = kvp.Value.PlayerName; // Get the player name from the dictionary value
                int timerTicks = kvp.Value.TimerTicks; // Get the timer ticks from the dictionary value
                Server.NextFrame(() =>
                {
                    if (!IsAllowedPlayer(player)) return;
                    player.PrintToChat(msgPrefix + $" {ParseColorToSymbol(primaryHUDcolor)}{playerName} {ChatColors.White}- {ParseColorToSymbol(primaryHUDcolor)}{FormatTime(timerTicks)}");
                });
            }
        }

        [ConsoleCommand("css_rb", "Teleports you to Bonus start")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void RespawnBonusPlayer(CCSPlayerController? player, CommandInfo command)
        {
            try
            {
                if (!IsAllowedPlayer(player) || respawnEnabled == false) return;
                SharpTimerDebug($"{player.PlayerName} calling css_rb...");

                if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
                {
                    player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                    return;
                }

                playerTimers[player.Slot].TicksSinceLastCmd = 0;

                if (!int.TryParse(command.ArgString, out int bonusX))
                {
                    SharpTimerDebug("css_rb conversion failed. The input string is not a valid integer.");
                    player.PrintToChat(msgPrefix + $" Please enter a valid Bonus stage i.e: {ParseColorToSymbol(primaryHUDcolor)}!rb <index>");
                    return;
                }

                // Remove checkpoints for the current player
                playerCheckpoints.Remove(player.Slot);

                if (bonusRespawnPoses[bonusX] != null)
                {
                    if (bonusRespawnAngs.TryGetValue(bonusX, out QAngle bonusAng) && bonusAng != null)
                    {
                        player.PlayerPawn.Value.Teleport(bonusRespawnPoses[bonusX], bonusRespawnAngs[bonusX], new Vector(0, 0, 0));
                    }
                    else
                    {
                        player.PlayerPawn.Value.Teleport(bonusRespawnPoses[bonusX], player.PlayerPawn.Value.EyeAngles ?? new QAngle(0, 0, 0), new Vector(0, 0, 0));
                    }
                    SharpTimerDebug($"{player.PlayerName} css_rb {bonusX} to {bonusRespawnPoses[bonusX]}");
                }
                else
                {
                    player.PrintToChat(msgPrefix + $" {ChatColors.LightRed} No RespawnBonusPos with index {bonusX} found for current map!");
                }

                Server.NextFrame(() =>
                {
                    playerTimers[player.Slot].IsTimerRunning = false;
                    playerTimers[player.Slot].TimerTicks = 0;
                    playerTimers[player.Slot].IsBonusTimerRunning = false;
                    playerTimers[player.Slot].BonusTimerTicks = 0;
                });

                if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {respawnSound}");
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in RespawnBonusPlayer: {ex.Message}");
            }
        }

        [ConsoleCommand("css_stage", "Teleports you to a stage")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void TPtoStagePlayer(CCSPlayerController? player, CommandInfo command)
        {
            try
            {
                if (!IsAllowedPlayer(player) || respawnEnabled == false) return;
                SharpTimerDebug($"{player.PlayerName} calling css_stage...");

                if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
                {
                    player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                    return;
                }

                playerTimers[player.Slot].TicksSinceLastCmd = 0;

                if (playerTimers[player.Slot].IsTimerBlocked == false)
                {
                    SharpTimerDebug($"css_stage failed. Player {player.PlayerName} had timer running.");
                    player.PrintToChat(msgPrefix + $" Please stop your timer first using: {ParseColorToSymbol(primaryHUDcolor)}!timer");
                    return;
                }

                if (!int.TryParse(command.ArgString, out int stageX))
                {
                    SharpTimerDebug("css_stage conversion failed. The input string is not a valid integer.");
                    player.PrintToChat(msgPrefix + $" Please enter a valid stage i.e: {ParseColorToSymbol(primaryHUDcolor)}!stage <index>");
                    return;
                }

                // Remove checkpoints for the current player
                playerCheckpoints.Remove(player.Slot);

                if (stageTriggerPoses.TryGetValue(stageX, out Vector stagePos) && stagePos != null)
                {
                    player.PlayerPawn.Value.Teleport(stagePos, stageTriggerAngs[stageX] ?? player.PlayerPawn.Value.EyeAngles, new Vector(0, 0, 0));
                    SharpTimerDebug($"{player.PlayerName} css_stage {stageX} to {stagePos}");
                }
                else
                {
                    player.PrintToChat(msgPrefix + $" {ChatColors.LightRed} No RespawnStagePos with index {stageX} found for current map!");
                }

                Server.NextFrame(() =>
                {
                    playerTimers[player.Slot].IsTimerRunning = false;
                    playerTimers[player.Slot].TimerTicks = 0;
                    playerTimers[player.Slot].IsBonusTimerRunning = false;
                    playerTimers[player.Slot].BonusTimerTicks = 0;
                });

                if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {respawnSound}");
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in TPtoStagePlayer: {ex.Message}");
            }
        }

        [ConsoleCommand("css_r", "Teleports you to start")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void RespawnPlayer(CCSPlayerController? player, CommandInfo command)
        {
            try
            {
                if (!IsAllowedPlayer(player) || respawnEnabled == false) return;
                SharpTimerDebug($"{player.PlayerName} calling css_r...");

                if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
                {
                    player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                    return;
                }

                playerTimers[player.Slot].TicksSinceLastCmd = 0;

                // Remove checkpoints for the current player
                playerCheckpoints.Remove(player.Slot);
                if (stageTriggers.Any()) playerTimers[player.Slot].StageRecords.Clear(); //remove previous stage times if the map has stages

                if (currentRespawnPos != null)
                {
                    if (currentRespawnAng != null)
                    {
                        player.PlayerPawn.Value.Teleport(currentRespawnPos, currentRespawnAng, new Vector(0, 0, 0));
                    }
                    else
                    {
                        player.PlayerPawn.Value.Teleport(currentRespawnPos, player.PlayerPawn.Value.EyeAngles ?? new QAngle(0, 0, 0), new Vector(0, 0, 0));
                    }
                    SharpTimerDebug($"{player.PlayerName} css_r to {currentRespawnPos}");
                }
                else
                {
                    player.PrintToChat(msgPrefix + $" {ChatColors.LightRed} No RespawnPos found for current map!");
                }

                Server.NextFrame(() =>
                {
                    playerTimers[player.Slot].IsTimerRunning = false;
                    playerTimers[player.Slot].TimerTicks = 0;
                    playerTimers[player.Slot].IsBonusTimerRunning = false;
                    playerTimers[player.Slot].BonusTimerTicks = 0;
                });
                playerTimers[player.Slot].SortedCachedRecords = GetSortedRecords();
                if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {respawnSound}");
            }
            catch (Exception ex)
            {
                SharpTimerError($"Exception in RespawnPlayer: {ex.Message}");
            }
        }

        [ConsoleCommand("css_timer", "Stops your timer")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void StopTimer(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player)) return;
            SharpTimerDebug($"{player.PlayerName} calling css_timer...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            // Remove checkpoints for the current player
            playerCheckpoints.Remove(player.Slot);

            playerTimers[player.Slot].IsTimerBlocked = playerTimers[player.Slot].IsTimerBlocked ? false : true;
            player.PrintToChat(msgPrefix + $" Stop timer set to: {ParseColorToSymbol(primaryHUDcolor)}{playerTimers[player.Slot].IsTimerBlocked}");
            playerTimers[player.Slot].IsTimerRunning = false;
            playerTimers[player.Slot].TimerTicks = 0;
            playerTimers[player.Slot].IsBonusTimerRunning = false;
            playerTimers[player.Slot].BonusTimerTicks = 0;
            playerTimers[player.Slot].SortedCachedRecords = GetSortedRecords();
            if (stageTriggers.Any()) playerTimers[player.Slot].StageRecords.Clear(); //remove previous stage times if the map has stages
            if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {beepSound}");
            SharpTimerDebug($"{player.PlayerName} css_timer to {playerTimers[player.Slot].IsTimerBlocked}");
        }

        [ConsoleCommand("css_stver", "Prints SharpTimer Version")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void STVerCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player))
            {
                SharpTimerConPrint($"This server is running SharpTimer v{ModuleVersion}");
                return;
            }

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            player.PrintToChat($"This server is running SharpTimer v{ModuleVersion}");
        }

        [ConsoleCommand("css_goto", "Teleports you to a player")]
        [CommandHelper(minArgs: 1, usage: "[name]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void GoToPlayer(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player) || goToEnabled == false) return;
            SharpTimerDebug($"{player.PlayerName} calling css_goto...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            if (!playerTimers[player.Slot].IsTimerBlocked)
            {
                player.PrintToChat(msgPrefix + $" Please stop your timer using {ParseColorToSymbol(primaryHUDcolor)}!timer{ChatColors.White} first!");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            var name = command.GetArg(1);
            bool isPlayerFound = false;
            CCSPlayerController foundPlayer = null;


            foreach (var playerEntry in connectedPlayers.Values)
            {
                if (playerEntry.PlayerName == name)
                {
                    foundPlayer = playerEntry;
                    isPlayerFound = true;
                    break;
                }
            }

            if (!isPlayerFound)
            {
                player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} Player name not found! If the name contains spaces please try {ParseColorToSymbol(primaryHUDcolor)}!goto 'some name'");
                return;
            }


            playerCheckpoints.Remove(player.Slot);
            playerTimers[player.Slot].IsTimerRunning = false;
            playerTimers[player.Slot].TimerTicks = 0;
            playerTimers[player.Slot].SortedCachedRecords = GetSortedRecords();

            if (playerTimers[player.Slot].SoundsEnabled != false)
                player.ExecuteClientCommand($"play {respawnSound}");

            if (foundPlayer != null && playerTimers[player.Slot].IsTimerBlocked)
            {
                player.PrintToChat(msgPrefix + $"Teleporting to {ParseColorToSymbol(primaryHUDcolor)}{foundPlayer.PlayerName}");

                if (player != null && IsAllowedPlayer(foundPlayer) && playerTimers[player.Slot].IsTimerBlocked)
                {
                    player.PlayerPawn.Value.Teleport(foundPlayer.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin ?? new Vector(0, 0, 0),
                        foundPlayer.PlayerPawn.Value.EyeAngles ?? new QAngle(0, 0, 0), new Vector(0, 0, 0));
                    SharpTimerDebug($"{player.PlayerName} css_goto to {foundPlayer.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin ?? new Vector(0, 0, 0)}");
                }
            }
            else
            {
                player.PrintToChat(msgPrefix + $"{ChatColors.LightRed} Player name not found! If the name contains spaces please try {ParseColorToSymbol(primaryHUDcolor)}!goto 'some name'");
            }
        }

        [ConsoleCommand("css_cp", "Sets a checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void SetPlayerCP(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player) || cpEnabled == false) return;
            SharpTimerDebug($"{player.PlayerName} calling css_cp...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            if (!player.PlayerPawn.Value.OnGroundLastTick && removeCpRestrictEnabled == false)
            {
                player.PrintToChat(msgPrefix + $"{ChatColors.LightRed}Cant set checkpoint while in air");
                if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {cpSoundAir}");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            // Get the player's current position and rotation
            Vector currentPosition = player.Pawn.Value.CBodyComponent?.SceneNode?.AbsOrigin ?? new Vector(0, 0, 0);
            Vector currentSpeed = player.PlayerPawn.Value.AbsVelocity ?? new Vector(0, 0, 0);
            QAngle currentRotation = player.PlayerPawn.Value.EyeAngles ?? new QAngle(0, 0, 0);

            // Convert position and rotation to strings
            string positionString = $"{currentPosition.X} {currentPosition.Y} {currentPosition.Z}";
            string rotationString = $"{currentRotation.X} {currentRotation.Y} {currentRotation.Z}";
            string speedString = $"{currentSpeed.X} {currentSpeed.Y} {currentSpeed.Z}";

            // Add the current position and rotation strings to the player's checkpoint list
            if (!playerCheckpoints.ContainsKey(player.Slot))
            {
                playerCheckpoints[player.Slot] = new List<PlayerCheckpoint>();
            }

            playerCheckpoints[player.Slot].Add(new PlayerCheckpoint
            {
                PositionString = positionString,
                RotationString = rotationString,
                SpeedString = speedString
            });

            // Get the count of checkpoints for this player
            int checkpointCount = playerCheckpoints[player.Slot].Count;

            // Print the chat message with the checkpoint count
            player.PrintToChat(msgPrefix + $"Checkpoint set! {ParseColorToSymbol(primaryHUDcolor)}#{checkpointCount}");
            if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {cpSound}");
            SharpTimerDebug($"{player.PlayerName} css_cp to {checkpointCount} {positionString} {rotationString} {speedString}");
        }

        [ConsoleCommand("css_tp", "Tp to the most recent checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void TpPlayerCP(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player) || cpEnabled == false) return;
            SharpTimerDebug($"{player.PlayerName} calling css_tp...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            // Check if the player has any checkpoints
            if (!playerCheckpoints.ContainsKey(player.Slot) || playerCheckpoints[player.Slot].Count == 0)
            {
                player.PrintToChat(msgPrefix + "No checkpoints set!");
                return;
            }

            // Get the most recent checkpoint from the player's list
            PlayerCheckpoint lastCheckpoint = playerCheckpoints[player.Slot].Last();

            // Convert position and rotation strings to Vector and QAngle
            Vector position = ParseVector(lastCheckpoint.PositionString ?? "0 0 0");
            QAngle rotation = ParseQAngle(lastCheckpoint.RotationString ?? "0 0 0");
            Vector speed = ParseVector(lastCheckpoint.SpeedString ?? "0 0 0");

            // Teleport the player to the most recent checkpoint, including the saved rotation
            if (removeCpRestrictEnabled == true)
            {
                player.PlayerPawn.Value.Teleport(position, rotation, speed);
            }
            else
            {
                player.PlayerPawn.Value.Teleport(position, rotation, new Vector(0, 0, 0));
            }

            // Play a sound or provide feedback to the player
            if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {tpSound}");
            player.PrintToChat(msgPrefix + "Teleported to most recent checkpoint!");
            SharpTimerDebug($"{player.PlayerName} css_tp to {position} {rotation} {speed}");
        }

        [ConsoleCommand("css_prevcp", "Tp to the previous checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void TpPreviousCP(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player) || cpEnabled == false) return;
            SharpTimerDebug($"{player.PlayerName} calling css_prevcp...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            if (!playerCheckpoints.TryGetValue(player.Slot, out List<PlayerCheckpoint> checkpoints) || checkpoints.Count == 0)
            {
                player.PrintToChat(msgPrefix + "No checkpoints set!");
                return;
            }

            int index = playerTimers.TryGetValue(player.Slot, out var timer) ? timer.CheckpointIndex : 0;

            if (checkpoints.Count == 1)
            {
                TpPlayerCP(player, command);
            }
            else
            {
                // Calculate the index of the previous checkpoint, circling back if necessary
                index = (index - 1 + checkpoints.Count) % checkpoints.Count;

                PlayerCheckpoint previousCheckpoint = checkpoints[index];

                // Update the player's checkpoint index
                playerTimers[player.Slot].CheckpointIndex = index;

                // Convert position and rotation strings to Vector and QAngle
                Vector position = ParseVector(previousCheckpoint.PositionString ?? "0 0 0");
                QAngle rotation = ParseQAngle(previousCheckpoint.RotationString ?? "0 0 0");

                // Teleport the player to the previous checkpoint, including the saved rotation
                player.PlayerPawn.Value.Teleport(position, rotation, new Vector(0, 0, 0));
                // Play a sound or provide feedback to the player
                if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {tpSound}");
                player.PrintToChat(msgPrefix + "Teleported to the previous checkpoint!");
                SharpTimerDebug($"{player.PlayerName} css_prevcp to {position} {rotation}");
            }
        }

        [ConsoleCommand("css_nextcp", "Tp to the next checkpoint")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void TpNextCP(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsAllowedPlayer(player) || cpEnabled == false) return;
            SharpTimerDebug($"{player.PlayerName} calling css_nextcp...");

            if (playerTimers[player.Slot].TicksSinceLastCmd < cmdCooldown)
            {
                player.PrintToChat(msgPrefix + $" Command is on cooldown. Chill...");
                return;
            }

            playerTimers[player.Slot].TicksSinceLastCmd = 0;

            if (!playerCheckpoints.TryGetValue(player.Slot, out List<PlayerCheckpoint> checkpoints) || checkpoints.Count == 0)
            {
                player.PrintToChat(msgPrefix + "No checkpoints set!");
                return;
            }

            int index = playerTimers.TryGetValue(player.Slot, out var timer) ? timer.CheckpointIndex : 0;

            if (checkpoints.Count == 1)
            {
                TpPlayerCP(player, command);
            }
            else
            {
                // Calculate the index of the next checkpoint, circling back if necessary
                index = (index + 1) % checkpoints.Count;

                PlayerCheckpoint nextCheckpoint = checkpoints[index];

                // Update the player's checkpoint index
                playerTimers[player.Slot].CheckpointIndex = index;

                // Convert position and rotation strings to Vector and QAngle
                Vector position = ParseVector(nextCheckpoint.PositionString ?? "0 0 0");
                QAngle rotation = ParseQAngle(nextCheckpoint.RotationString ?? "0 0 0");

                // Teleport the player to the next checkpoint, including the saved rotation
                player.PlayerPawn.Value.Teleport(position, rotation, new Vector(0, 0, 0));

                // Play a sound or provide feedback to the player
                if (playerTimers[player.Slot].SoundsEnabled != false) player.ExecuteClientCommand($"play {tpSound}");
                player.PrintToChat(msgPrefix + "Teleported to the next checkpoint!");
                SharpTimerDebug($"{player.PlayerName} css_nextcp to {position} {rotation}");
            }
        }
    }
}