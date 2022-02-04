using System.Diagnostics;
using KaiHeiLa.API;
using Model = KaiHeiLa.API.Guild;
using ExtendedModel = KaiHeiLa.API.ExtendedGuild;

namespace KaiHeiLa.WebSocket;

/// <summary>
///     Represents a WebSocket-based guild object.
/// </summary>
[DebuggerDisplay(@"{DebuggerDisplay,nq}")]
public class SocketGuild : SocketEntity<ulong>, IGuild, IDisposable
{
    #region SocketGuild

    public string Name { get; private set; }
    public string Topic { get; private set; }
    public uint MasterId { get; private set; }
    public string Icon { get; private set; }
    public NotifyType NotifyType { get; private set; }
    public string Region { get; private set; }
    public bool IsOpenEnabled { get; private set; }
    public uint OpenId { get; private set; }
    public ulong DefaultChannelId { get; private set; }
    public ulong WelcomeChannelId { get; private set; }
    
    
    public object[] Features { get; private set; }

    public int BoostNumber { get; private set; }
    
    public int BufferBoostNumber { get; private set; }

    public BoostLevel BoostLevel { get; private set; }
    
    public int Status { get; private set; }

    public string AutoDeleteTime { get; private set; }

    // TODO: Public RecommendInfo
    internal RecommendInfo RecommendInfo { get; private set; }
    
    internal bool IsAvailable { get; private set; }
    /// <summary> Indicates whether the client is connected to this guild. </summary>
    public bool IsConnected { get; internal set; }

    internal SocketGuild(KaiHeiLaSocketClient kaiHeiLa, ulong id) : base(kaiHeiLa, id)
    {
    }
    internal static SocketGuild Create(KaiHeiLaSocketClient client, ClientState state, Model model)
    {
        var entity = new SocketGuild(client, model.Id);
        entity.Update(state, model);
        return entity;
    }

    internal void Update(ClientState state, ExtendedModel model)
    {
        Update(state, model as Model);

        Features = model.Features;
        BoostNumber = model.BoostNumber;
        BufferBoostNumber = model.BufferBoostNumber;
        BoostLevel = model.BoostLevel;
        Status = model.Status;
        AutoDeleteTime = model.AutoDeleteTime;
        RecommendInfo = model.RecommendInfo;
    }
    
    internal void Update(ClientState state, Model model)
    {
        Name = model.Name;
        Topic = model.Topic;
        MasterId = model.MasterId;
        Icon = model.Icon;
        NotifyType = model.NotifyType;
        Region = model.Region;
        IsOpenEnabled = model.EnableOpen;
        OpenId = model.OpenId;
        DefaultChannelId = model.DefaultChannelId;
        WelcomeChannelId = model.WelcomeChannelId;
    }
    #endregion
    
    /// <summary>
    ///     Gets the name of the guild.
    /// </summary>
    /// <returns>
    ///     A string that resolves to <see cref="KaiHeiLa.WebSocket.SocketGuild.Name"/>.
    /// </returns>
    public override string ToString() => Name;
    private string DebuggerDisplay => $"{Name} ({Id})";

    #region IGuild

    public void Dispose()
    {
    }

    #endregion
}