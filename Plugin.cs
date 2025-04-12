using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using System;
using EmployeeAssignmentsRevived.Data;
using EmployeeAssignmentsRevived.Components;

namespace EmployeeAssignmentsRevived
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private static bool Initialized;
        public static ManualLogSource Log { get; private set; }

        private void Start() => Initialize();
        private void OnDestroy() => Initialize();

        /// <summary>
        /// Initializes the mod once on startup or reload.
        /// </summary>
        private void Initialize()
        {
            Log = Logger;

            if (!Initialized)
            {
                Initialized = true;

                // Create a persistent root object
                GameObject root = new GameObject("EmployeeAssignmentManager");
                DontDestroyOnLoad(root);

                // Attach main logic and config handler
                var manager = root.AddComponent<EmployeeAssignmentManager>();
                manager.Config = new EAConfig(Config);

                // Optional components
                root.AddComponent<KillableEnemiesOutput>();
                root.AddComponent<UpdateChecker>();

                Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded successfully.");
            }
        }
    }

    /// <summary>
    /// Assignment types supported by the mod.
    /// </summary>
    [Serializable]
    public enum AssignmentType
    {
        CollectScrapItem,
        KillMonster,
        RepairValve
    }

    /// <summary>
    /// A data structure representing a single assignment instance.
    /// </summary>
    [Serializable]
    public struct Assignment
    {
        public string Name;
        public string UID;
        public byte ID;
        public string BodyText;
        public string ShortText;
        public string TargetText;
        public string FailureReason;
        public int CashReward;
        public int XPReward;
        public AssignmentType Type;
        public ulong PlayerId;
        public ulong[] TargetIds;
        public int TargetsComplete;
        public Vector3 FixedTargetPosition;
    }

    /// <summary>
    /// Registry of available assignment types and definitions.
    /// </summary>
    public static class Assignments
    {
        public static readonly Assignment[] All = new Assignment[]
        {
            new Assignment
            {
                Name = "SCRAP RETRIEVAL",
                UID = "collect_scrap",
                BodyText = "YOU MUST COLLECT THE FOLLOWING SCRAP ITEM: [{0}] IT WILL BE MARKED AS [ASSIGNMENT TARGET]",
                ShortText = "FIND THE [{0}] MARKED 'ASSIGNMENT TARGET'",
                FailureReason = "ANOTHER EMPLOYEE COLLECTED THE ITEM",
                CashReward = 100,
                XPReward = 0,
                Type = AssignmentType.CollectScrapItem,
                TargetIds = new ulong[1]
            },
            new Assignment
            {
                Name = "HUNT & KILL",
                UID = "hunt_kill",
                BodyText = "YOU MUST HUNT AND KILL THE FOLLOWING ENEMY: [{0}] IT WILL BE MARKED AS [ASSIGNMENT TARGET]",
                ShortText = "FIND AND KILL THE [{0}]",
                FailureReason = "THE ENEMY WAS NOT KILLED",
                CashReward = 200,
                XPReward = 0,
                Type = AssignmentType.KillMonster,
                TargetIds = new ulong[1]
            },
            new Assignment
            {
                Name = "REPAIR VALVE",
                UID = "repair_valve",
                BodyText = "YOU MUST FIND AND REPAIR THE BROKEN VALVE",
                ShortText = "FIND AND REPAIR THE BROKEN VALVE",
                TargetText = "BROKEN VALVE",
                FailureReason = "THE BROKEN VALVE WAS NOT FIXED",
                CashReward = 100,
                XPReward = 0,
                Type = AssignmentType.RepairValve,
                TargetIds = new ulong[1]
            }
        };

        public static Assignment GetAssignment(string guid)
        {
            foreach (var a in All)
                if (a.UID == guid) return a;
            return default;
        }
    }

    /// <summary>
    /// Miscellaneous utilities used throughout the mod.
    /// </summary>
    public static class Utils
    {
        public static float EaseInOutBack(float x)
        {
            return (x < 0.5f)
                ? ((float)Math.Pow(2.0 * x, 2.0) * (7.1000004f * x - 2.5500002f) / 2f)
                : (((float)Math.Pow(2.0 * x - 2.0, 2.0) * (3.5500002f * (x * 2f - 2f) + 2.5500002f) + 2f) / 2f);
        }

        public static int WeightedRandom(int[] weights)
        {
            int total = 0;
            foreach (var weight in weights) total += weight;
            int rnd = UnityEngine.Random.Range(0, total);
            int cumulative = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (cumulative >= rnd) return i;
            }
            return 0;
        }
    }

    /// <summary>
    /// Central mod metadata for internal and external version checks.
    /// </summary>
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "FluxTeam.EmployeeAssignmentsRevived";
        public const string PLUGIN_NAME = "EmployeeAssignmentsRevived";
        public const string PLUGIN_AUTHOR = "FluxTeam";
        public const string PLUGIN_VERSION = "2.0.1";
        public const string VERSION_TEXT = "Employee Assignments Revived mod is out of date. Please update to at least v" + PLUGIN_VERSION + ".";
    }
}