using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using EmployeeAssignmentsRevived.Data;

namespace EmployeeAssignmentsRevived.AssignmentLogic
{
    /// <summary>
    /// Contract for all assignment logic modules.
    /// </summary>
    public interface IAssignmentLogic
    {
        /// <summary>
        /// Host-only setup procedure for assignment target creation.
        /// </summary>
        bool ServerSetupAssignment(ref Assignment assignment);

        /// <summary>
        /// Client-side handling when an assignment is allocated.
        /// </summary>
        bool HandleAllocationEvent(ref Assignment assignment, bool localPlayer);

        /// <summary>
        /// Host-side logic that determines assignment success/failure status.
        /// </summary>
        AssignmentState CheckCompletion(ref Assignment assignment);

        /// <summary>
        /// Client-side completion response (e.g., spawn reward).
        /// </summary>
        void CompleteAssignment(ref Assignment assignment, bool localPlayer);
    }

    /// <summary>
    /// Enum representing an assignment's completion status.
    /// </summary>
    public enum AssignmentState
    {
        InProgress,
        Complete,
        Failed
    }

    /// <summary>
    /// Handles logic for the "Collect Scrap" assignment type.
    /// </summary>
    public class CollectScrapLogic : IAssignmentLogic
    {
        private readonly EAContext _context;

        public CollectScrapLogic(EAContext context) => _context = context;

        public bool ServerSetupAssignment(ref Assignment assignment)
        {
            var objects = UnityEngine.Object.FindObjectsByType<GrabbableObject>(FindObjectsSortMode.None);
            foreach (var obj in objects)
            {
                var id = ((NetworkBehaviour)obj).NetworkObjectId;
                if (!_context.ExcludeTargetIds.Contains(id) && !obj.scrapPersistedThroughRounds && obj.itemProperties.isScrap)
                {
                    assignment.TargetIds[0] = id;
                    assignment.TargetText = obj.itemProperties.itemName.ToUpper();
                    _context.ExcludeTargetIds.Add(id);
                    return true;
                }
            }
            return false;
        }

        public bool HandleAllocationEvent(ref Assignment assignment, bool localPlayer)
        {
            if (!localPlayer) return false;

            var objects = UnityEngine.Object.FindObjectsByType<GrabbableObject>(FindObjectsSortMode.None);
            foreach (var obj in objects)
            {
                if (((NetworkBehaviour)obj).NetworkObjectId == assignment.TargetIds[0])
                {
                    assignment.TargetText = obj.itemProperties.itemName.ToUpper();

                    var scan = obj.GetComponentInChildren<ScanNodeProperties>();
                    if (scan != null)
                    {
                        scan.headerText = "ASSIGNMENT TARGET";
                        scan.subText = "Value : ???";
                        return true;
                    }

                    else
                    {
                        Debug.LogError($"Scan node is missing for item: {obj.name}");
                        return false;
                    }
                }
            }
            return false;
        }

        public AssignmentState CheckCompletion(ref Assignment assignment)
        {
            foreach (var item in RoundManager.Instance.scrapCollectedThisRound)
            {
                if (item.isInShipRoom && ((NetworkBehaviour)item).NetworkObjectId == assignment.TargetIds[0])
                {
                    return _context.AllPlayersComplete || ((NetworkBehaviour)item).OwnerClientId == assignment.PlayerId
                        ? AssignmentState.Complete
                        : AssignmentState.Failed;
                }
            }
            return AssignmentState.InProgress;
        }

        public void CompleteAssignment(ref Assignment assignment, bool localPlayer)
        {
            if (!RoundManager.Instance.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(assignment.TargetIds[0], out var obj))
                return;

            var component = obj.gameObject.GetComponent<GrabbableObject>();
            component.SetScrapValue(assignment.CashReward + component.scrapValue);

            var scan = component.GetComponentInChildren<ScanNodeProperties>();
            if (scan != null)
                scan.headerText = component.itemProperties.itemName;

            if (NetworkManager.Singleton.IsHost)
                _context.SyncedRewardValues.Add((component.NetworkObject, component.scrapValue));
        }
    }
}