using System;
using System.Runtime.InteropServices;

namespace EmployeeAssignmentsRevived.Network
{
    /// <summary>
    /// Marker interface for all network events exchanged by the mod.
    /// </summary>
    public interface IPAEvent
    {
        string TAG { get; }
    }

    /// <summary>
    /// Triggered when the host assigns an objective to a player.
    /// </summary>
    [Serializable]
    public struct AssignmentEvent : IPAEvent
    {
        public byte AssignmentID;
        public ulong PlayerId;
        public string AssignmentUID;
        public int RewardValue;
        public ulong[] TargetIds;
        public string TargetName;
        public string TAG => EAEventTags.ALLOCATION;
    }

    /// <summary>
    /// Sent when a player completes their assignment.
    /// </summary>
    [Serializable]
    public struct CompleteEvent : IPAEvent
    {
        public byte AssignmentID;
        public ulong PlayerId;
        public string AssignmentUID;
        public int RewardValue;
        public ulong[] TargetIds;
        public string TAG => EAEventTags.COMPLETE;
    }

    /// <summary>
    /// Sent when an assignment fails to be completed.
    /// </summary>
    [Serializable]
    public struct FailedEvent : IPAEvent
    {
        public byte AssignmentID;
        public ulong PlayerId;
        public string Reason;
        public string TAG => EAEventTags.FAILED;
    }

    /// <summary>
    /// Used to trigger a full assignment reset (usually on state change).
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct ResetEvent : IPAEvent
    {
        public string TAG => EAEventTags.RESET;
    }

    /// <summary>
    /// Static container for network message tags used by the assignment system.
    /// </summary>
    public static class EAEventTags
    {
        public const string PREFIX = "EA-";
        public const string RESET = "EA-Reset";
        public const string ALLOCATION = "EA-Allocation";
        public const string COMPLETE = "EA-Complete";
        public const string FAILED = "EA-Failed";
    }
}