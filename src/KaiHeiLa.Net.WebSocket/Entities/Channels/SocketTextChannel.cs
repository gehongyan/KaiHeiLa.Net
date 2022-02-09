using System.Collections.Immutable;
using Model = KaiHeiLa.API.Channel;

using System.Diagnostics;

namespace KaiHeiLa.WebSocket;

/// <summary>
///     Represents a WebSocket-based channel in a guild that can send and receive messages.
/// </summary>
[DebuggerDisplay(@"{DebuggerDisplay,nq}")]
public class SocketTextChannel : SocketGuildChannel, ITextChannel, ISocketMessageChannel
{
    #region SocketTextChannel

    private readonly MessageCache _messages;
    
    /// <inheritdoc />
    public string Topic { get; set; }
    /// <inheritdoc />
    public int SlowModeInterval { get; set; }
    /// <inheritdoc />
    public ulong? CategoryId { get; private set; }
    /// <inheritdoc />
    public bool IsPermissionSynced { get; set; }
    /// <inheritdoc />
    public string Mention => MentionUtils.MentionChannel(Id);
    
    public IReadOnlyCollection<SocketMessage> CachedMessages => _messages?.Messages ?? ImmutableArray.Create<SocketMessage>();
    /// <inheritdoc />
    public override IReadOnlyCollection<SocketGuildUser> Users
        => Guild.Users.Where(x => Permissions.GetValue(
            Permissions.ResolveChannel(Guild, x, this, Permissions.ResolveGuild(Guild, x)),
            ChannelPermission.ViewChannels)).ToImmutableArray();

    internal SocketTextChannel(KaiHeiLaSocketClient kaiHeiLa, ulong id, SocketGuild guild)
        : base(kaiHeiLa, id, guild)
    {
        Type = ChannelType.Text;
        if (KaiHeiLa.MessageCacheSize > 0)
            _messages = new MessageCache(KaiHeiLa);
    }
    internal new static SocketTextChannel Create(SocketGuild guild, ClientState state, Model model)
    {
        var entity = new SocketTextChannel(guild.KaiHeiLa, model.Id, guild);
        entity.Update(state, model);
        return entity;
    }
    internal override void Update(ClientState state, Model model)
    {
        base.Update(state, model);
        CategoryId = model.CategoryId;
        Topic = model.Topic;
        SlowModeInterval = model.SlowMode; // some guilds haven't been patched to include this yet?
    }
    
    internal void AddMessage(SocketMessage msg)
        => _messages?.Add(msg);
    internal SocketMessage RemoveMessage(Guid id)
        => _messages?.Remove(id);
    #endregion

    #region Users
    /// <inheritdoc />
    public override SocketGuildUser GetUser(ulong id)
    {
        var user = Guild.GetUser(id);
        if (user != null)
        {
            var guildPerms = Permissions.ResolveGuild(Guild, user);
            var channelPerms = Permissions.ResolveChannel(Guild, user, this, guildPerms);
            if (Permissions.GetValue(channelPerms, ChannelPermission.ViewChannels))
                return user;
        }
        return null;
    }
    #endregion
    
    private string DebuggerDisplay => $"{Name} ({Id}, Text)";
    internal new SocketTextChannel Clone() => MemberwiseClone() as SocketTextChannel;

    #region IGuildChannel

    /// <inheritdoc />
    Task<IGuildUser> IGuildChannel.GetUserAsync(ulong id, CacheMode mode, RequestOptions options)
        => Task.FromResult<IGuildUser>(GetUser(id));

    #endregion
    
}