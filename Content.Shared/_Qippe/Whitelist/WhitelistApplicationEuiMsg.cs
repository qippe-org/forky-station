using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Qippe.Whitelist;

/// <summary>
/// Why a submitted application was not stored, so the client can show a specific message.
/// </summary>
public enum WhitelistApplicationSubmitResult : byte
{
    Success,
    EmptyMessage,
    MessageTooLong,
    AlreadyApplied,
    Failed,
}

public static class WhitelistApplicationEuiMsg
{
    /// <summary>
    /// Sent by the player to submit an application.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class SubmitRequest(string message) : EuiMessageBase
    {
        public string Message { get; set; } = message;
    }

    /// <summary>
    /// Sent to the player when a submit was rejected, with the reason.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class SubmitRejected(WhitelistApplicationSubmitResult result) : EuiMessageBase
    {
        public WhitelistApplicationSubmitResult Result { get; set; } = result;
    }

    /// <summary>
    /// Sent by an admin to approve an application.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class ApproveRequest(int id) : EuiMessageBase
    {
        public int Id { get; set; } = id;
    }

    /// <summary>
    /// Sent by an admin to deny an application.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class DenyRequest(int id) : EuiMessageBase
    {
        public int Id { get; set; } = id;
    }
}
