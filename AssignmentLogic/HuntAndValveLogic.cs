using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using EmployeeAssignmentsRevived.Data;

namespace EmployeeAssignmentsRevived.AssignmentLogic
{
    /// <summary>
    /// Handles the "Hunt & Kill" assignment logic, including enemy spawning and completion detection.
    /// </summary>
    public class HuntAndKillLogic : IAssignmentLogic
    {
        private readonly EAContext _context;
        private const float MAX_PLAYER_DISTANCE = 10f;

        public HuntAndKillLogic(EAContext context) => _context = context;

        public bool ServerSetupAssignment(ref Assignment assignment)
        {
            if (RoundManager.Instance == null) return false;

            int typeIndex = Utils.WeightedRandom(_context.EnemyWeights);
            string enemyName = _context.EnemyWhitelist[typeIndex];
            assignment.CashReward = _context.EnemyRewards[typeIndex];

            // Try to reuse a live enemy
            foreach (var enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy.enemyType.enemyName == enemyName && enemy.IsSpawned && !enemy.isEnemyDead &&
                    !_context.ExcludeTargetIds.Contains(enemy.NetworkObjectId))
                {
                    assignment.TargetIds[0] = enemy.NetworkObjectId;
                    assignment.TargetText = enemy.enemyType.enemyName.ToUpper();
                    _context.ExcludeTargetIds.Add(enemy.NetworkObjectId);
                    enemy.GetComponentInChildren<ScanNodeProperties>().headerText = "ASSIGNMENT TARGET";
                    return true;
                }
            }

            // Otherwise, spawn one
            var targetEnemy = RoundManager.Instance.currentLevel.Enemies
                .FirstOrDefault(e => e.enemyType.enemyName == enemyName);
            if (targetEnemy == null) return false;

            Vector3 spawnPos = Vector3.zero;
            var entrance = GameObject.FindObjectsOfType<EntranceTeleport>()
                .FirstOrDefault(e => !e.isEntranceToBuilding && e.entranceId == 0);
            if (entrance != null) spawnPos = entrance.transform.position;

            var vents = RoundManager.Instance.allEnemyVents
                .Select(v => (v, Vector3.Distance(v.floorNode.position, spawnPos)))
                .OrderByDescending(p => p.Item2)
                .ToList();

            NavMeshHit hit = default;
            bool placed = false;
            int attempts = 0;

            while (!placed && attempts < 5)
            {
                var vent = vents[UnityEngine.Random.Range(0, vents.Count / 2)].v;
                placed = NavMesh.SamplePosition(vent.floorNode.position, out hit, 20f, NavMesh.AllAreas);
                attempts++;
            }

            if (!placed) return false;

            RoundManager.Instance.SpawnEnemyOnServer(hit.position, 0f,
                RoundManager.Instance.currentLevel.Enemies.IndexOf(targetEnemy));

            var newEnemy = RoundManager.Instance.SpawnedEnemies.Last();
            assignment.TargetIds[0] = newEnemy.NetworkObjectId;
            assignment.TargetText = newEnemy.enemyType.enemyName.ToUpper();
            _context.ExcludeTargetIds.Add(assignment.TargetIds[0]);
            newEnemy.GetComponentInChildren<ScanNodeProperties>().headerText = "ASSIGNMENT TARGET";
            return true;
        }

        public bool HandleAllocationEvent(ref Assignment assignment, bool localPlayer)
        {
            foreach (var enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy.NetworkObjectId == assignment.TargetIds[0])
                {
                    assignment.TargetText = enemy.enemyType.enemyName.ToUpper();
                    enemy.GetComponentInChildren<ScanNodeProperties>().headerText = "ASSIGNMENT TARGET";
                    return true;
                }
            }
            return true;
        }

        public AssignmentState CheckCompletion(ref Assignment assignment)
        {
            foreach (var enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy.NetworkObjectId == assignment.TargetIds[0] && enemy.isEnemyDead)
                {
                    float closest = NetworkManager.Singleton.ConnectedClientsList
                        .Select(c => Vector3.Distance(c.PlayerObject.transform.position, enemy.agent.transform.position))
                        .Min();

                    return closest < MAX_PLAYER_DISTANCE
                        ? AssignmentState.Complete
                        : AssignmentState.Failed;
                }
            }
            return AssignmentState.InProgress;
        }

        public void CompleteAssignment(ref Assignment assignment, bool localPlayer)
        {
            if (!NetworkManager.Singleton.IsHost) return;

            foreach (var enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy.NetworkObjectId == assignment.TargetIds[0])
                {
                    var prefab = StartOfRound.Instance.allItemsList.itemsList
                        .First(i => i.itemName == "Gold bar").spawnPrefab;

                    var instance = UnityEngine.Object.Instantiate(prefab, enemy.serverPosition, Quaternion.identity,
                        RoundManager.Instance.spawnedScrapContainer);

                    var scrap = instance.GetComponent<GrabbableObject>();
                    scrap.transform.rotation = Quaternion.Euler(scrap.itemProperties.restingRotation);
                    scrap.fallTime = 0f;
                    scrap.SetScrapValue(assignment.CashReward);

                    var netObj = instance.GetComponent<NetworkObject>();
                    netObj.Spawn(false);

                    _context.SyncedRewardValues.Add((netObj, assignment.CashReward));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Logic for the "Repair Valve" assignment, which requires player interaction with a broken valve hazard.
    /// </summary>
    public class RepairValveLogic : IAssignmentLogic
    {
        private readonly EAContext _context;

        public RepairValveLogic(EAContext context) => _context = context;

        public bool ServerSetupAssignment(ref Assignment assignment)
        {
            var valves = UnityEngine.Object.FindObjectsByType<SteamValveHazard>(FindObjectsSortMode.None);
            if (valves.Length == 0) return false;

            Vector3 entrance = RoundManager.FindMainEntrancePosition(false, false);
            var sorted = valves.OrderByDescending(v => Vector3.Distance(entrance, v.transform.position)).ToList();
            var target = sorted.FirstOrDefault(v =>
                !_context.ExcludeTargetIds.Contains(v.NetworkObjectId) && v.IsSpawned);

            if (target == null) return false;

            assignment.TargetIds[0] = target.NetworkObjectId;
            _context.ExcludeTargetIds.Add(target.NetworkObjectId);

            target.valveCrackTime = 0.001f;
            target.valveBurstTime = 0.01f;
            target.triggerScript.interactable = true;
            assignment.FixedTargetPosition = target.transform.position;
            return true;
        }

        public bool HandleAllocationEvent(ref Assignment assignment, bool localPlayer) => true;

        public AssignmentState CheckCompletion(ref Assignment assignment)
        {
            var valves = UnityEngine.Object.FindObjectsByType<SteamValveHazard>(FindObjectsSortMode.None);
            foreach (var valve in valves)
            {
                if (valve.NetworkObjectId == assignment.TargetIds[0] && !valve.triggerScript.interactable)
                {
                    return _context.AllPlayersComplete || valve.triggerScript.OwnerClientId == assignment.PlayerId
                        ? AssignmentState.Complete
                        : AssignmentState.Failed;
                }
            }
            return AssignmentState.InProgress;
        }

        public void CompleteAssignment(ref Assignment assignment, bool localPlayer)
        {
            if (!NetworkManager.Singleton.IsHost) return;

            var valves = UnityEngine.Object.FindObjectsByType<SteamValveHazard>(FindObjectsSortMode.None);
            foreach (var valve in valves)
            {
                if (valve.NetworkObjectId == assignment.TargetIds[0])
                {
                    var prefab = StartOfRound.Instance.allItemsList.itemsList
                        .First(i => i.itemName == "Gold bar").spawnPrefab;

                    var instance = UnityEngine.Object.Instantiate(prefab, valve.transform.position, Quaternion.identity,
                        RoundManager.Instance.spawnedScrapContainer);

                    var scrap = instance.GetComponent<GrabbableObject>();
                    scrap.transform.rotation = Quaternion.Euler(scrap.itemProperties.restingRotation);
                    scrap.fallTime = 0f;
                    scrap.SetScrapValue(assignment.CashReward);

                    var netObj = instance.GetComponent<NetworkObject>();
                    netObj.Spawn(false);

                    _context.SyncedRewardValues.Add((netObj, assignment.CashReward));
                    break;
                }
            }
        }
    }
}