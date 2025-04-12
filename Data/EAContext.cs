using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Unity.Netcode;

namespace EmployeeAssignmentsRevived.Data
{
    /// <summary>
    /// Shared runtime context containing current assignment state and configuration-driven filters.
    /// </summary>
    public class EAContext
    {
        public bool AssignmentsGenerated;
        public Assignment? CurrentAssignment;

        public readonly List<Assignment> ActiveAssignments = new();
        public readonly List<Assignment> CompletedAssignments = new();
        public readonly List<ulong> ExcludeTargetIds = new();
        public readonly List<(NetworkObject, int)> SyncedRewardValues = new();

        public string[] EnemyWhitelist = Array.Empty<string>();
        public int[] EnemyWeights = Array.Empty<int>();
        public int[] EnemyRewards = Array.Empty<int>();

        public string[] AssignmentWhitelist = Array.Empty<string>();
        public int[] AssignmentWeights = Array.Empty<int>();

        public bool AssignAllPlayers;
        public bool AllPlayersComplete;

        /// <summary>
        /// Parses a comma-separated list of strings from config into a string array.
        /// </summary>
        public void ParseStringArray(ref string[] array, ConfigEntry<string> data)
        {
            string[] split = GetConfigValue(data).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            array = new string[split.Length];
            for (int i = 0; i < split.Length; i++)
                array[i] = split[i].Trim();
        }

        /// <summary>
        /// Parses a comma-separated list of integers from config into an int array.
        /// </summary>
        public void ParseIntArray(ref int[] array, ConfigEntry<string> data)
        {
            string[] split = GetConfigValue(data).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            array = new int[split.Length];
            for (int i = 0; i < split.Length; i++)
            {
                array[i] = int.TryParse(split[i].Trim(), out int value) ? value : 0;
            }
        }

        private string GetConfigValue(ConfigEntry<string> data)
        {
            string value = data.Value;
            if (string.IsNullOrWhiteSpace(value))
                value = (string)((ConfigEntryBase)data).DefaultValue;

            return value;
        }
    }

    /// <summary>
    /// Configuration binding class for exposed mod settings in BepInEx.
    /// </summary>
    public class EAConfig
    {
        public readonly ConfigEntry<int> MaxAssignedPlayers;
        public readonly ConfigEntry<int> MinAssignedPlayers;
        public readonly ConfigEntry<bool> AssignAllPlayers;
        public readonly ConfigEntry<bool> AllPlayersCanComplete;

        public readonly ConfigEntry<string> AssignmentWhiteList;
        public readonly ConfigEntry<string> AssignmentWeights;

        public readonly ConfigEntry<int> ScrapRetrievalReward;
        public readonly ConfigEntry<int> BrokenValveReward;

        public readonly ConfigEntry<string> HuntAndKillWhitelist;
        public readonly ConfigEntry<string> HuntAndKillWeights;
        public readonly ConfigEntry<string> HuntAndKillRewards;

        public EAConfig(ConfigFile config)
        {
            MaxAssignedPlayers = config.Bind("HostOnly.General", "MaxAssignedPlayers", 10, "Maximum players assigned per round.");
            MinAssignedPlayers = config.Bind("HostOnly.General", "MinAssignedPlayers", 1, "Minimum players assigned per round.");
            AssignAllPlayers = config.Bind("HostOnly.General", "AssignAllPlayers", false, "Assign every player instead of a random subset.");
            AllPlayersCanComplete = config.Bind("HostOnly.General", "AllPlayersCanComplete", false, "Allow other players to complete someone's assignment.");

            AssignmentWhiteList = config.Bind("HostOnly.General", "AssignmentWhiteList", "collect_scrap,hunt_kill,repair_valve", "Assignment types allowed to spawn.");
            AssignmentWeights = config.Bind("HostOnly.General", "AssignmentWeights", "50,25,25", "Weighting for assignment type selection.");

            ScrapRetrievalReward = config.Bind("Assignment.ScrapRetrieval", "AssignmentReward", 100, "Reward for collecting marked scrap.");
            BrokenValveReward = config.Bind("Assignment.RepairValve", "AssignmentReward", 100, "Reward for fixing the broken valve.");

            HuntAndKillWhitelist = config.Bind("Assignment.HuntAndKill", "EnemyWhitelist", "Centipede,Bunker Spider,Hoarding bug,Crawler", "List of valid enemies for hunt and kill.");
            HuntAndKillWeights = config.Bind("Assignment.HuntAndKill", "EnemyWeights", "50,25,50,25", "Spawn weighting for enemy selection.");
            HuntAndKillRewards = config.Bind("Assignment.HuntAndKill", "EnemyRewards", "100,200,100,200", "Cash reward per enemy type.");
        }
    }

    /// <summary>
    /// Enumeration of states the game can be in from the mod’s perspective.
    /// </summary>
    public enum GameStateEnum
    {
        MainMenu,
        Orbit,
        CompanyHQ,
        Level
    }

    /// <summary>
    /// Wrapper class that notifies listeners when the game state changes.
    /// </summary>
    public class GameContext
    {
        private GameStateEnum _gameState;
        public Action<GameStateEnum> GameStateChanged;

        public GameStateEnum GameState
        {
            get => _gameState;
            set
            {
                if (_gameState != value)
                {
                    _gameState = value;
                    GameStateChanged?.Invoke(value);
                }
            }
        }
    }
}