using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using System.Text.Json;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;



namespace SharpTimer
{
    public partial class SharpTimer
    {
        private async void ServerRecordADtimer()
        {
            if (isADTimerRunning) return;

            var timer = AddTimer(srTimer, async () =>
            {
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

                Server.NextFrame(() => Server.PrintToChatAll($"{msgPrefix} Current Server Record on {ParseColorToSymbol(primaryHUDcolor)}{currentMapName}{ChatColors.White}: "));

                foreach (var kvp in sortedRecords.Take(1))
                {
                    string playerName = kvp.Value.PlayerName; // Get the player name from the dictionary value
                    int timerTicks = kvp.Value.TimerTicks; // Get the timer ticks from the dictionary value

                    Server.NextFrame(() => Server.PrintToChatAll(msgPrefix + $" {ParseColorToSymbol(primaryHUDcolor)}{playerName} {ChatColors.White}- {ParseColorToSymbol(primaryHUDcolor)}{FormatTime(timerTicks)}"));
                }
            }, TimerFlags.REPEAT);
            isADTimerRunning = true;
        }

        private bool IsValidStartTriggerName(string triggerName)
        {
            if (triggerName.Contains("map_start") ||
                triggerName.Contains("s1_start") ||
                triggerName.Contains("stage1_start") ||
                triggerName.Contains("timer_startzone") ||
                triggerName.Contains("zone_start") ||
                triggerName.Contains(currentMapStartTrigger))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool IsValidEndTriggerName(string triggerName)
        {
            if (triggerName.Contains("map_end") ||
                triggerName.Contains("timer_endzone") ||
                triggerName.Contains("zone_end") ||
                triggerName.Contains(currentMapEndTrigger))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static string FormatTime(int ticks)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(ticks / 64.0);

            // Format seconds with three decimal points
            string secondsWithMilliseconds = $"{timeSpan.Seconds:D2}.{(ticks % 64) * (1000.0 / 64.0):000}";

            int totalMinutes = (int)timeSpan.TotalMinutes;
            if (totalMinutes >= 60)
            {
                return $"{totalMinutes / 60:D1}:{totalMinutes % 60:D2}:{secondsWithMilliseconds}";
            }

            return $"{totalMinutes:D1}:{secondsWithMilliseconds}";
        }

        private static string FormatTimeDifference(int currentTicks, int previousTicks)
        {
            int differenceTicks = previousTicks - currentTicks;
            string sign = (differenceTicks > 0) ? "-" : "+";

            TimeSpan timeDifference = TimeSpan.FromSeconds(Math.Abs(differenceTicks) / 64.0);

            // Format seconds with three decimal points
            string secondsWithMilliseconds = $"{timeDifference.Seconds:D2}.{(Math.Abs(differenceTicks) % 64) * (1000.0 / 64.0):000}";

            int totalDifferenceMinutes = (int)timeDifference.TotalMinutes;
            if (totalDifferenceMinutes >= 60)
            {
                return $"{sign}{totalDifferenceMinutes / 60:D1}:{totalDifferenceMinutes % 60:D2}:{secondsWithMilliseconds}";
            }

            return $"{sign}{totalDifferenceMinutes:D1}:{secondsWithMilliseconds}";
        }

        static string ParseColorToSymbol(string input)
        {
            // Check if the input is a recognized color name
            Dictionary<string, string> colorNameSymbolMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
             {
                 { "white", "\u0001" },
                 { "darkred", "\u0002" },
                 { "purple", "\u0003" },
                 { "darkgreen", "\u0004" },
                 { "lightgreen", "\u0005" },
                 { "green", "\u0006" },
                 { "red", "\u0007" },
                 { "lightgray", "\u0008" },
                 { "orange", "\u000F" },
                 { "darkpurple", "\u000E" },
                 { "lightred", "\u000F" }
             };

            string lowerInput = input.ToLower();

            if (colorNameSymbolMap.TryGetValue(lowerInput, out var symbol))
            {
                return symbol;
            }

            // If the input is not a recognized color name, check if it's a valid hex color code
            if (IsHexColorCode(input))
            {
                return ParseHexToSymbol(input);
            }

            return "\u0010"; // Default symbol for unknown input
        }

        static bool IsHexColorCode(string input)
        {
            if (input.StartsWith("#") && (input.Length == 7 || input.Length == 9))
            {
                try
                {
                    Color color = ColorTranslator.FromHtml(input);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing hex color code: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Invalid hex color code format");
            }

            return false;
        }

        static string ParseHexToSymbol(string hexColorCode)
        {
            Color color = ColorTranslator.FromHtml(hexColorCode);

            Dictionary<string, string> predefinedColors = new Dictionary<string, string>
            {
                { "#FFFFFF", "" },  // White
                { "#8B0000", "" },  // Dark Red
                { "#800080", "" },  // Purple
                { "#006400", "" },  // Dark Green
                { "#00FF00", "" },  // Light Green
                { "#008000", "" },  // Green
                { "#FF0000", "" },  // Red
                { "#D3D3D3", "" },  // Light Gray
                { "#FFA500", "" },  // Orange
                { "#780578", "" },  // Dark Purple
                { "#FF4500", "" }   // Light Red
            };

            hexColorCode = hexColorCode.ToUpper();

            if (predefinedColors.TryGetValue(hexColorCode, out var colorName))
            {
                return colorName;
            }

            Color targetColor = ColorTranslator.FromHtml(hexColorCode);
            string closestColor = FindClosestColor(targetColor, predefinedColors.Keys);

            if (predefinedColors.TryGetValue(closestColor, out var symbol))
            {
                return symbol;
            }

            return "";
        }

        static string FindClosestColor(Color targetColor, IEnumerable<string> colorHexCodes)
        {
            double minDistance = double.MaxValue;
            string closestColor = null;

            foreach (var hexCode in colorHexCodes)
            {
                Color color = ColorTranslator.FromHtml(hexCode);
                double distance = ColorDistance(targetColor, color);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestColor = hexCode;
                }
            }

            return closestColor;
        }

        static double ColorDistance(Color color1, Color color2)
        {
            int rDiff = color1.R - color2.R;
            int gDiff = color1.G - color2.G;
            int bDiff = color1.B - color2.B;

            return Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
        }

        public void DrawLaserBetween(Vector startPos, Vector endPos)
        {
            CBeam beam = Utilities.CreateEntityByName<CBeam>("beam");
            if (beam == null)
            {
                Console.WriteLine($"Failed to create beam...");
                return;
            }

            if(IsHexColorCode(primaryHUDcolor))
            {
                beam.Render = ColorTranslator.FromHtml(primaryHUDcolor);
            }
            else
            {
                beam.Render = Color.FromName(primaryHUDcolor);
            }

            beam.Width = 1.5f;

            beam.Teleport(startPos, new QAngle(0, 0, 0), new Vector(0, 0, 0));

            beam.EndPos.X = endPos.X;
            beam.EndPos.Y = endPos.Y;
            beam.EndPos.Z = endPos.Z;

            beam.DispatchSpawn();
            Console.WriteLine("Laser spawned");
        }

        public void DrawWireframe2D(Vector corner1, Vector corner2, float height = 50)
        {
            Vector corner3 = new Vector(corner2.X, corner1.Y, corner1.Z);
            Vector corner4 = new Vector(corner1.X, corner2.Y, corner1.Z);

            Vector corner1_top = new Vector(corner1.X, corner1.Y, corner1.Z + height);
            Vector corner2_top = new Vector(corner2.X, corner2.Y, corner2.Z + height);
            Vector corner3_top = new Vector(corner2.X, corner1.Y, corner1.Z + height);
            Vector corner4_top = new Vector(corner1.X, corner2.Y, corner1.Z + height);

            DrawLaserBetween(corner1, corner3);
            DrawLaserBetween(corner1, corner4);
            DrawLaserBetween(corner2, corner3);
            DrawLaserBetween(corner2, corner4);

            DrawLaserBetween(corner1_top, corner3_top);
            DrawLaserBetween(corner1_top, corner4_top);
            DrawLaserBetween(corner2_top, corner3_top);
            DrawLaserBetween(corner2_top, corner4_top);

            DrawLaserBetween(corner1, corner1_top);
            DrawLaserBetween(corner2, corner2_top);
            DrawLaserBetween(corner3, corner3_top);
            DrawLaserBetween(corner4, corner4_top);
        }

        public void DrawWireframe3D(Vector corner1, Vector corner8)
        {
            Vector corner2 = new Vector(corner1.X, corner8.Y, corner1.Z);
            Vector corner3 = new Vector(corner8.X, corner8.Y, corner1.Z);
            Vector corner4 = new Vector(corner8.X, corner1.Y, corner1.Z);

            Vector corner5 = new Vector(corner8.X, corner1.Y, corner8.Z);
            Vector corner6 = new Vector(corner1.X, corner1.Y, corner8.Z);
            Vector corner7 = new Vector(corner1.X, corner8.Y, corner8.Z);

            //top square
            DrawLaserBetween(corner1, corner2);
            DrawLaserBetween(corner2, corner3);
            DrawLaserBetween(corner3, corner4);
            DrawLaserBetween(corner4, corner1);

            //bottom square
            DrawLaserBetween(corner5, corner6);
            DrawLaserBetween(corner6, corner7);
            DrawLaserBetween(corner7, corner8);
            DrawLaserBetween(corner8, corner5);

            //connect them both to build a cube
            DrawLaserBetween(corner1, corner6);
            DrawLaserBetween(corner2, corner7);
            DrawLaserBetween(corner3, corner8);
            DrawLaserBetween(corner4, corner5);
        }

        static bool IsVectorInsideBox(Vector playerVector, Vector corner1, Vector corner2, float height = 50)
        {
            float minX = Math.Min(corner1.X, corner2.X);
            float minY = Math.Min(corner1.Y, corner2.Y);
            float minZ = Math.Min(corner1.Z, corner2.Z);

            float maxX = Math.Max(corner1.X, corner2.X);
            float maxY = Math.Max(corner1.Y, corner2.Y);
            float maxZ = Math.Max(corner1.Z, corner1.Z);

            return playerVector.X >= minX && playerVector.X <= maxX &&
                   playerVector.Y >= minY && playerVector.Y <= maxY &&
                   playerVector.Z >= minZ && playerVector.Z <= maxZ + height;
        }

        private static void AdjustPlayerVelocity(CCSPlayerController? player, float velocity)
        {
            var currentX = player.PlayerPawn.Value.AbsVelocity.X;
            var currentY = player.PlayerPawn.Value.AbsVelocity.Y;
            var currentSpeed2D = Math.Sqrt(currentX * currentX + currentY * currentY);
            var normalizedX = currentX / currentSpeed2D;
            var normalizedY = currentY / currentSpeed2D;
            var adjustedX = normalizedX * velocity; // Adjusted speed limit
            var adjustedY = normalizedY * velocity; // Adjusted speed limit
            player.PlayerPawn.Value.AbsVelocity.X = (float)adjustedX;
            player.PlayerPawn.Value.AbsVelocity.Y = (float)adjustedY;
        }

        private Vector? FindStartTriggerPos()
        {
            var triggers = Utilities.FindAllEntitiesByDesignerName<CBaseTrigger>("trigger_multiple");

            foreach (var trigger in triggers)
            {
                if (trigger == null || trigger.Entity.Name == null) continue;

                if (IsValidStartTriggerName(trigger.Entity.Name.ToString()))
                {
                    return trigger.CBodyComponent?.SceneNode?.AbsOrigin;
                }
            }
            return null;
        }

        private (Vector? startRight, Vector? startLeft, Vector? endRight, Vector? endLeft) FindTriggerCorners()
        {
            var targets = Utilities.FindAllEntitiesByDesignerName<CPointEntity>("info_target");

            Vector? startRight = null;
            Vector? startLeft = null;
            Vector? endRight = null;
            Vector? endLeft = null;

            foreach (var target in targets)
            {
                if (target == null || target.Entity.Name == null) continue;

                switch (target.Entity.Name)
                {
                    case "start_right":
                        startRight = target.AbsOrigin;
                        break;
                    case "start_left":
                        startLeft = target.AbsOrigin;
                        break;
                    case "end_right":
                        endRight = target.AbsOrigin;
                        break;
                    case "end_left":
                        endLeft = target.AbsOrigin;
                        break;
                }
            }

            return (startRight, startLeft, endRight, endLeft);
        }

        private static Vector ParseVector(string vectorString)
        {
            var values = vectorString.Split(' ');
            if (values.Length == 3 &&
                float.TryParse(values[0], out float x) &&
                float.TryParse(values[1], out float y) &&
                float.TryParse(values[2], out float z))
            {
                return new Vector(x, y, z);
            }

            return new Vector(0, 0, 0);
        }

        private static QAngle ParseQAngle(string qAngleString)
        {
            var values = qAngleString.Split(' ');
            if (values.Length == 3 &&
                float.TryParse(values[0], out float pitch) &&
                float.TryParse(values[1], out float yaw) &&
                float.TryParse(values[2], out float roll))
            {
                return new QAngle(pitch, yaw, roll);
            }

            return new QAngle(0, 0, 0);
        }

        public Dictionary<string, PlayerRecord> GetSortedRecords()
        {
            string currentMapName = Server.MapName;

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

            if (records.ContainsKey(currentMapName))
            {
                var sortedRecords = records[currentMapName]
                    .OrderBy(record => record.Value.TimerTicks)
                    .ToDictionary(record => record.Key, record => new PlayerRecord
                    {
                        PlayerName = record.Value.PlayerName,
                        TimerTicks = record.Value.TimerTicks
                    });

                return sortedRecords;
            }

            return new Dictionary<string, PlayerRecord>();
        }

        private int GetPreviousPlayerRecord(CCSPlayerController? player)
        {
            if (player == null) return 0;

            string currentMapName = Server.MapName;
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
            if (player == null || !playerTimers.ContainsKey(player.Slot) || !playerTimers[player.Slot].IsTimerRunning) return "";


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

        public async Task<string> GetPlayerPlacementWithTotal(CCSPlayerController? player, string steamId, int playerSlot)
        {
            if (player == null || !playerTimers.ContainsKey(playerSlot))
            {
                return "Unranked";
            }

            int savedPlayerTime;
            if (useMySQL == true)
            {
                savedPlayerTime = await GetPreviousPlayerRecordFromDatabase(player, steamId, currentMapName);
            }
            else
            {
                savedPlayerTime = GetPreviousPlayerRecord(player);
            }

            if (savedPlayerTime == 0)
            {
                return "Unranked";
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

            return $"Rank: {placement}/{totalPlayers}";
        }

        public void SavePlayerTime(CCSPlayerController? player, int timerTicks)
        {
            if (player == null) return;
            if (playerTimers[player.Slot].IsTimerRunning == false) return;

            string currentMapName = Server.MapName;
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
                    TimerTicks = playerTimers[player.Slot].TimerTicks
                };

                string updatedJson = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(playerRecordsPath, updatedJson);
            }
        }

        private void OnMapStartHandler(string mapName)
        {
            Server.NextFrame(() =>
            {
                Server.ExecuteCommand("sv_autoexec_mapname_cfg 0");
                Server.ExecuteCommand("execifexists SharpTimer/custom_exec.cfg");
                if (removeCrouchFatigueEnabled == true) Server.ExecuteCommand("sv_timebetweenducks 0");
            });
        }

        private void LoadConfig()
        {
            Server.ExecuteCommand($"execifexists SharpTimer/config.cfg");

            if (srEnabled == true) ServerRecordADtimer();

            string recordsFileName = "SharpTimer/player_records.json";
            playerRecordsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", recordsFileName);

            string mysqlConfigFileName = "SharpTimer/mysqlConfig.json";
            mySQLpath = Path.Join(Server.GameDirectory + "/csgo/cfg", mysqlConfigFileName);

            currentMapName = Server.MapName;

            string mapdataFileName = $"SharpTimer/MapData/{currentMapName}.json"; // Assuming the map JSON files are in the SharpTimer folder
            string mapdataPath = Path.Join(Server.GameDirectory + "/csgo/cfg", mapdataFileName);

            if (File.Exists(mapdataPath))
            {
                string json = File.ReadAllText(mapdataPath);
                var mapInfo = JsonSerializer.Deserialize<MapInfo>(json);

                if (!string.IsNullOrEmpty(mapInfo.RespawnPos))
                {
                    currentRespawnPos = ParseVector(mapInfo.RespawnPos);
                }

                if (!string.IsNullOrEmpty(mapInfo.MapStartC1) && !string.IsNullOrEmpty(mapInfo.MapStartC2) && !string.IsNullOrEmpty(mapInfo.MapEndC1) && !string.IsNullOrEmpty(mapInfo.MapEndC2))
                {
                    currentMapStartC1 = ParseVector(mapInfo.MapStartC1);
                    currentMapStartC2 = ParseVector(mapInfo.MapStartC2);
                    currentMapEndC1 = ParseVector(mapInfo.MapEndC1);
                    currentMapEndC2 = ParseVector(mapInfo.MapEndC2);
                    useTriggers = false;
                }

                if (!string.IsNullOrEmpty(mapInfo.MapStartTrigger) && !string.IsNullOrEmpty(mapInfo.MapEndTrigger))
                {
                    currentMapStartTrigger = mapInfo.MapStartTrigger;
                    currentMapEndTrigger = mapInfo.MapEndTrigger;
                    useTriggers = true;
                }
            }
            else
            {
                Console.WriteLine($"Map data json not found for map: {currentMapName}! Using default trigger names instead!");
                useTriggers = true;
            }

            if (useTriggers == false)
            {
                DrawWireframe2D(currentMapStartC1, currentMapStartC2, 50);
                DrawWireframe2D(currentMapEndC1, currentMapEndC2, 50);
            }
            else
            {
                var (startRight, startLeft, endRight, endLeft) = FindTriggerCorners();

                if (startRight == null || startLeft == null || endRight == null || endLeft == null) return;

                DrawWireframe3D(startRight, startLeft);
                DrawWireframe3D(endRight, endLeft);
            }

        }
    }
}