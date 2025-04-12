using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using EmployeeAssignmentsRevived.Data;
using EmployeeAssignmentsRevived.Network;
using EmployeeAssignmentsRevived.UI;
using EmployeeAssignmentsRevived.AssignmentLogic;
using EmployeeAssignmentsRevived.Components;

namespace EmployeeAssignmentsRevived
{
    /// <summary>
    /// Central mod controller. Handles lifecycle, assignment dispatching, syncing, and state transitions.
    /// </summary>
    public class EmployeeAssignmentManager : MonoBehaviour
    {
        private readonly List<IAssignmentLogic> _assignmentLogic = new List<IAssignmentLogic>();
        private NetworkUtils _networkUtils;
        private AssignmentUI _assignmentUI;
        private EAContext _context;
        private GameContext _gameContext;
        public EAConfig Config;

        private int _maxAssignedPlayers = 20;
        private int _minAssignedPlayers = 1;
        private Coroutine _lootValueSyncRoutine;
        private readonly WaitForSecondsRealtime _rewardValueSyncDelay = new WaitForSecondsRealtime(1f);

        private void Log(string log) => Plugin.Log.LogInfo(log);

        #region Unity Lifecycle

        private void Awake()
        {
            _context = new EAContext();
            _assignmentUI = new GameObject("UI").AddComponent<AssignmentUI>();
            _assignmentUI.transform.SetParent(transform);

            _networkUtils = gameObject.AddComponent<NetworkUtils>();
            _networkUtils.OnNetworkData += OnNetworkData;
            _networkUtils.OnDisconnect += HandleResetEvent;

            _gameContext = new GameContext();
            var sync = gameObject.AddComponent<GameStateSync>();
            sync.Inject(_gameContext, _networkUtils);
            _gameContext.GameStateChanged += GameStateChanged;

            InstallAssignmentLogic();
        }

        private void Start()
        {
            _maxAssignedPlayers = Config.MaxAssignedPlayers.Value;
            _minAssignedPlayers = Config.MinAssignedPlayers.Value;
            _context.AssignAllPlayers = Config.AssignAllPlayers.Value;
            _context.AllPlayersComplete = Config.AllPlayersCanComplete.Value;
            Assignments.All[0].CashReward = Config.ScrapRetrievalReward.Value;
            Assignments.All[2].CashReward = Config.BrokenValveReward.Value;
            _context.ParseStringArray(ref _context.EnemyWhitelist, Config.HuntAndKillWhitelist);
            _context.ParseStringArray(ref _context.AssignmentWhitelist, Config.AssignmentWhiteList);
            _context.ParseIntArray(ref _context.AssignmentWeights, Config.AssignmentWeights);
            _context.ParseIntArray(ref _context.EnemyWeights, Config.HuntAndKillWeights);
            _context.ParseIntArray(ref _context.EnemyRewards, Config.HuntAndKillRewards);
        }

        private void Update()
        {
            if (_gameContext.GameState != GameStateEnum.Level) return;

            if (_context.CurrentAssignment.HasValue)
                _assignmentUI.SetAssignment(_context.CurrentAssignment.Value);

            if (NetworkManager.Singleton.IsHost)
            {
                if (_context.ActiveAssignments.Count > 0) CheckCompleted();
                if (_lootValueSyncRoutine == null && _context.SyncedRewardValues.Count > 0)
                    _lootValueSyncRoutine = StartCoroutine(SyncLootValues());
            }
        }

        #endregion

        #region Initialization

        private void InstallAssignmentLogic()
        {
            _assignmentLogic.Add(new CollectScrapLogic(_context));
            _assignmentLogic.Add(new HuntAndKillLogic(_context));
            _assignmentLogic.Add(new RepairValveLogic(_context));
        }

        #endregion

        #region Networking

        private void SendNetworkEvent<T>(T netEvent) where T : IPAEvent
        {
            string json = JsonUtility.ToJson(netEvent);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            _networkUtils.SendToAll(netEvent.TAG, bytes);
        }

        private void OnNetworkData(string tag, byte[] data)
        {
            if (!tag.StartsWith("EA-")) return;
            string json = Encoding.UTF8.GetString(data);
            Log($"Incoming data {tag} {json}");

            switch (tag)
            {
                case "EA-Reset":
                    HandleResetEvent();
                    break;
                case "EA-Allocation":
                    HandleAllocationEvent(JsonUtility.FromJson<AssignmentEvent>(json));
                    break;
                case "EA-Complete":
                    HandleCompleteEvent(JsonUtility.FromJson<CompleteEvent>(json));
                    break;
                case "EA-Failed":
                    HandleFailedEvent(JsonUtility.FromJson<FailedEvent>(json));
                    break;
            }
        }

        #endregion

        #region Assignment Handlers

        private void HandleResetEvent()
        {
            _context.AssignmentsGenerated = false;
            _context.CurrentAssignment = null;
            _context.ActiveAssignments.Clear();
            _context.CompletedAssignments.Clear();
            _assignmentUI.ClearAssignment();
        }

        private void HandleAllocationEvent(AssignmentEvent e)
        {
            var assignment = Assignments.GetAssignment(e.AssignmentUID);
            assignment.PlayerId = e.PlayerId;
            assignment.TargetIds = e.TargetIds;
            assignment.CashReward = e.RewardValue;
            assignment.TargetText = e.TargetName;
            assignment.ID = e.AssignmentID;

            bool isLocal = assignment.PlayerId == NetworkManager.Singleton.LocalClientId;
            bool valid = _assignmentLogic[(int)assignment.Type].HandleAllocationEvent(ref assignment, isLocal);

            if (isLocal && valid)
            {
                _context.CurrentAssignment = assignment;
                ShowAssignment(assignment);
            }
        }

        private void HandleCompleteEvent(CompleteEvent e)
        {
            var assignment = Assignments.GetAssignment(e.AssignmentUID);
            assignment.CashReward = e.RewardValue;
            assignment.TargetIds = e.TargetIds;
            assignment.PlayerId = e.PlayerId;
            assignment.ID = e.AssignmentID;

            bool isLocal = _context.CurrentAssignment.HasValue &&
                           _context.CurrentAssignment.Value.ID == e.AssignmentID &&
                           assignment.PlayerId == NetworkManager.Singleton.LocalClientId;

            _assignmentLogic[(int)assignment.Type].CompleteAssignment(ref assignment, isLocal);
            if (isLocal) CompleteAssignment();
        }

        private void HandleFailedEvent(FailedEvent e)
        {
            if (_context.CurrentAssignment.HasValue &&
                _context.CurrentAssignment.Value.ID == e.AssignmentID &&
                e.PlayerId == NetworkManager.Singleton.LocalClientId)
            {
                FailAssignment(e.Reason);
            }
        }

        #endregion

        #region Game State Integration

        private void GameStateChanged(GameStateEnum state)
        {
            switch (state)
            {
                case GameStateEnum.MainMenu:
                case GameStateEnum.CompanyHQ:
                    HandleResetEvent();
                    break;
                case GameStateEnum.Orbit:
                    ClearAllAssignments();
                    break;
                case GameStateEnum.Level:
                    LandedOnMoon();
                    if (NetworkManager.Singleton.IsHost)
                        GenerateAssignments();
                    break;
            }
        }

        public void LandedOnMoon() { }
        public void ShipTookOff() => ClearAllAssignments();
        private void ClearAllAssignments() => SendNetworkEvent(default(ResetEvent));

        #endregion

        #region Assignment Logic

        private void GenerateAssignments()
        {
            int attempts = 0;
            _context.CompletedAssignments.Clear();
            _context.ActiveAssignments.Clear();
            _context.ExcludeTargetIds.Clear();

            var clients = NetworkManager.Singleton.ConnectedClientsIds;
            int count = Mathf.Max(_minAssignedPlayers,
                                  Mathf.Min(_maxAssignedPlayers,
                                            RoundManager.Instance.playersManager.connectedPlayersAmount / 2));
            var list = new List<int>();
            for (int i = 0; i < clients.Count; i++) list.Add(i);
            count = Mathf.Min(count, list.Count);
            if (_context.AssignAllPlayers) count = list.Count;

            Log($"Starting assignment generation for {count} players");
            byte id = 0;

            for (int j = 0; j < count; j++)
            {
                id++;
                int index = UnityEngine.Random.Range(0, list.Count);
                int clientIndex = list[index];
                int typeIndex = Utils.WeightedRandom(_context.AssignmentWeights);
                var assignment = Assignments.GetAssignment(_context.AssignmentWhitelist[typeIndex]);

                if (!string.IsNullOrEmpty(assignment.UID))
                {
                    assignment.PlayerId = clients[clientIndex];
                    assignment.ID = id;

                    bool ok = _assignmentLogic[(int)assignment.Type].ServerSetupAssignment(ref assignment);
                    if (!ok && attempts < 5)
                    {
                        j--; attempts++; continue;
                    }
                    else if (ok)
                    {
                        Log($"Created assignment for player {clients[clientIndex]}");
                        SendNetworkEvent(new AssignmentEvent
                        {
                            PlayerId = clients[clientIndex],
                            AssignmentUID = _context.AssignmentWhitelist[typeIndex],
                            TargetIds = assignment.TargetIds,
                            RewardValue = assignment.CashReward,
                            TargetName = assignment.TargetText,
                            AssignmentID = id
                        });
                        _context.ActiveAssignments.Add(assignment);
                        list.RemoveAt(index);
                    }
                }
            }

            _context.AssignmentsGenerated = true;
            Log("Finished assignment generation.");
        }

        private void CheckCompleted()
        {
            for (int i = _context.ActiveAssignments.Count - 1; i >= 0; i--)
            {
                var assignment = _context.ActiveAssignments[i];
                var state = _assignmentLogic[(int)assignment.Type].CheckCompletion(ref assignment);

                if (state != AssignmentState.InProgress)
                {
                    _context.ActiveAssignments.RemoveAt(i);
                    _context.CompletedAssignments.Add(assignment);

                    switch (state)
                    {
                        case AssignmentState.Complete:
                            SendNetworkEvent(new CompleteEvent
                            {
                                PlayerId = assignment.PlayerId,
                                AssignmentUID = assignment.UID,
                                TargetIds = assignment.TargetIds,
                                RewardValue = assignment.CashReward,
                                AssignmentID = assignment.ID
                            });
                            break;

                        case AssignmentState.Failed:
                            SendNetworkEvent(new FailedEvent
                            {
                                PlayerId = assignment.PlayerId,
                                Reason = assignment.FailureReason,
                                AssignmentID = assignment.ID
                            });
                            break;
                    }
                }
            }
        }

        #endregion

        #region UI Feedback

        private void ShowAssignment(Assignment a)
        {
            if (HUDManager.Instance != null)
            {
                var seg = new[]
                {
                    new DialogueSegment
                    {
                        speakerText = "ASSIGNMENT:" + a.Name,
                        bodyText = $"YOU HAVE BEEN SELECTED BY THE COMPANY FOR ASSIGNMENT, {string.Format(a.BodyText, a.TargetText)}",
                        waitTime = 10f
                    }
                };
                _assignmentUI.SetAssignment(a);
                HUDManager.Instance.ReadDialogue(seg);
            }
        }

        private void CompleteAssignment()
        {
            if (_context.CurrentAssignment.HasValue)
            {
                var seg = new[]
                {
                    new DialogueSegment
                    {
                        speakerText = "ASSIGNMENT COMPLETE",
                        bodyText = "YOU HAVE COMPLETED THE ASSIGNMENT, WELL DONE. THE COMPANY VALUES YOUR LOYALTY",
                        waitTime = 5f
                    }
                };
                _assignmentUI.ClearAssignment();
                HUDManager.Instance.ReadDialogue(seg);
                _context.CurrentAssignment = null;
            }
        }

        private void FailAssignment(string reason)
        {
            if (_context.CurrentAssignment.HasValue)
            {
                var seg = new[]
                {
                    new DialogueSegment
                    {
                        speakerText = "ASSIGNMENT FAILED",
                        bodyText = "YOU FAILED TO COMPLETE THE ASSIGNMENT. REASON: " + reason,
                        waitTime = 5f
                    }
                };
                _assignmentUI.ClearAssignment();
                HUDManager.Instance.ReadDialogue(seg);
                _context.CurrentAssignment = null;
            }
        }

        #endregion

        #region Reward Synchronization

        /// <summary>
        /// Coroutine to sync reward values back to clients for modified scrap items.
        /// </summary>
        private IEnumerator SyncLootValues()
        {
            yield return _rewardValueSyncDelay;

            _context.SyncedRewardValues.RemoveAll(x => x.Item1 == null);
            if (_context.SyncedRewardValues.Count > 0)
            {
                var refs = new List<NetworkObjectReference>();
                var values = new List<int>();

                foreach (var (netObj, value) in _context.SyncedRewardValues)
                {
                    refs.Add(netObj);
                    values.Add(value);
                }

                RoundManager.Instance.SyncScrapValuesClientRpc(refs.ToArray(), values.ToArray());
            }

            _lootValueSyncRoutine = null;
        }

        #endregion

    }
}
