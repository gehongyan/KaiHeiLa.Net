using Model = KaiHeiLa.API.SelfUser;

using System.Diagnostics;
using System.Globalization;

namespace KaiHeiLa.WebSocket;

/// <summary>
///     Represents the logged-in WebSocket-based user.
/// </summary>
[DebuggerDisplay(@"{DebuggerDisplay,nq}")]
public class SocketSelfUser : SocketUser, ISelfUser
{
    internal override SocketGlobalUser GlobalUser { get; }
    
    /// <inheritdoc />
    public override bool IsBot { get => GlobalUser.IsBot; internal set => GlobalUser.IsBot = value; }
    /// <inheritdoc />
    public override string Username { get => GlobalUser.Username; internal set => GlobalUser.Username = value; }
    /// <inheritdoc />
    public override string IdentifyNumber { get => GlobalUser.IdentifyNumber; internal set => GlobalUser.IdentifyNumber = value; }
    /// <inheritdoc />
    public override ushort IdentifyNumberValue { get => GlobalUser.IdentifyNumberValue; internal set => GlobalUser.IdentifyNumberValue = value; }
    /// <inheritdoc />
    public override string Avatar { get => GlobalUser.Avatar; internal set => GlobalUser.Avatar = value; }
    /// <inheritdoc />
    public override string VIPAvatar { get => GlobalUser.VIPAvatar; internal set => GlobalUser.VIPAvatar = value; }
    /// <inheritdoc />
    public override bool IsBanned { get => GlobalUser.IsBanned; internal set => GlobalUser.IsBanned = value; }
    /// <inheritdoc />
    public override bool IsOnline { get => GlobalUser.IsOnline; internal set => GlobalUser.IsOnline = value; }
    /// <inheritdoc />
    public override bool IsVIP { get => GlobalUser.IsVIP; internal set => GlobalUser.IsVIP = value; }
    /// <inheritdoc />
    
    public string MobilePrefix { get; internal set; }
    public string Mobile { get; internal set; }
    public int InvitedCount { get; internal set; }
    public bool IsMobileVerified { get; internal set; }

    internal SocketSelfUser(KaiHeiLaSocketClient kaiHeiLa, SocketGlobalUser globalUser)
        : base(kaiHeiLa, globalUser.Id)
    {
        GlobalUser = globalUser;
    }
    internal static SocketSelfUser Create(KaiHeiLaSocketClient kaiHeiLa, ClientState state, Model model)
    {
        var entity = new SocketSelfUser(kaiHeiLa, kaiHeiLa.GetOrCreateSelfUser(state, model));
        entity.Update(state, model);
        return entity;
    }

    internal bool Update(ClientState state, Model model)
    {
        bool hasGlobalChanges = base.Update(state, model);
        if (model.MobilePrefix != MobilePrefix)
        {
            MobilePrefix = model.MobilePrefix;
            hasGlobalChanges = true;
        }
        if (model.Mobile != Mobile)
        {
            Mobile = model.Mobile;
            hasGlobalChanges = true;
        }
        if (model.InvitedCount != InvitedCount)
        {
            InvitedCount = model.InvitedCount ?? 0;
            hasGlobalChanges = true;
        }
        if (model.MobileVerified != IsMobileVerified)
        {
            IsMobileVerified = model.MobileVerified;
            hasGlobalChanges = true;
        }
        return hasGlobalChanges;
    }
    
    private string DebuggerDisplay => $"{Username}#{IdentifyNumber} ({Id}{(IsBot ? ", Bot" : "")}, Self)";
    internal new SocketSelfUser Clone() => MemberwiseClone() as SocketSelfUser;
}