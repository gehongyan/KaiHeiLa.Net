using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using KaiHeiLa.API;
using KaiHeiLa.API.Gateway;
using KaiHeiLa.Audio;
using KaiHeiLa.Rest;
using Model = KaiHeiLa.API.Guild;
using ChannelModel = KaiHeiLa.API.Channel;
using MemberModel = KaiHeiLa.API.Rest.GuildMember;
using ExtendedModel = KaiHeiLa.API.Rest.ExtendedGuild;
using RecommendInfo = KaiHeiLa.Rest.RecommendInfo;
using RoleModel = KaiHeiLa.API.Role;
using UserModel = KaiHeiLa.API.User;

namespace KaiHeiLa.WebSocket;

/// <summary>
///     Represents a WebSocket-based guild object.
/// </summary>
[DebuggerDisplay(@"{DebuggerDisplay,nq}")]
public class SocketGuild : SocketEntity<ulong>, IGuild, IDisposable, IUpdateable
{
    #region SocketGuild

    private readonly SemaphoreSlim _audioLock;
    private TaskCompletionSource<AudioClient> _audioConnectPromise;
    private ConcurrentDictionary<ulong, SocketGuildChannel> _channels;
    private ConcurrentDictionary<ulong, SocketGuildUser> _members;
    private ConcurrentDictionary<uint, SocketRole> _roles;
    // private ImmutableArray<GuildEmote> _emotes;
    
    private AudioClient _audioClient;
    
    /// <inheritdoc />
    public string Name { get; private set; }

    /// <inheritdoc />
    public string Topic { get; private set; }

    /// <inheritdoc />
    public ulong OwnerId { get; private set; }
    /// <summary> Gets the user that owns this guild. </summary>
    public SocketGuildUser Owner => GetUser(OwnerId);
    /// <inheritdoc />
    public string Icon { get; private set; }
    /// <inheritdoc />
    public NotifyType NotifyType { get; private set; }
    /// <inheritdoc />
    public string Region { get; private set; }
    /// <inheritdoc />
    public bool IsOpenEnabled { get; private set; }
    /// <inheritdoc />
    public uint OpenId { get; private set; }
    /// <inheritdoc />
    public ulong DefaultChannelId { get; private set; }
    /// <inheritdoc />
    public ulong WelcomeChannelId { get; private set; }
    /// <summary>
    ///     Gets the features of this guild.
    /// </summary>
    /// <returns>
    ///     An array of objects representing the features of this guild.
    /// </returns>
    /// <remarks>
    ///      <note type="important">
    ///         What this property represents is not well investigated.
    ///     </note>
    /// </remarks>
    public object[] Features { get; private set; }
    /// <inheritdoc />
    public int BoostNumber { get; private set; }
    /// <inheritdoc />
    public int BufferBoostNumber { get; private set; }
    /// <inheritdoc />
    public BoostLevel BoostLevel { get; private set; }
    /// <summary>
    ///     TODO: To be documented.
    /// </summary>
    public int Status { get; private set; }
    /// <summary>
    ///     TODO: To be documented.
    /// </summary>
    public string AutoDeleteTime { get; private set; }
    /// <inheritdoc cref="IGuild.RecommendInfo"/>
    public RecommendInfo RecommendInfo { get; private set; }
    
    /// <summary>
    ///     Gets the number of members.
    /// </summary>
    /// <remarks>
    ///     This property retrieves the number of members returned by KaiHeiLa.
    ///     <note type="tip">
    ///     <para>
    ///         Due to how this property is returned by KaiHeiLa instead of relying on the WebSocket cache, the
    ///         number here is the most accurate in terms of counting the number of users within this guild.
    ///     </para>
    ///     <para>
    ///         Use this instead of enumerating the count of the
    ///         <see cref="KaiHeiLa.WebSocket.SocketGuild.Users" /> collection, as you may see discrepancy
    ///         between that and this property.
    ///     </para>
    ///     </note>
    /// </remarks>
    public int MemberCount { get; internal set; }
    /// <summary> Gets the number of members downloaded to the local guild cache. </summary>
    public int DownloadedMemberCount { get; private set; }
    
    internal bool IsAvailable { get; private set; }
    /// <summary> Indicates whether the client is connected to this guild. </summary>
    public bool IsConnected { get; internal set; }
    /// <summary> Indicates whether the client has all the members downloaded to the local guild cache. </summary>
    public bool HasAllMembers => MemberCount <= DownloadedMemberCount;
    /// <summary>
    ///     Gets the <see cref="IAudioClient" /> associated with this guild.
    /// </summary>
    public IAudioClient AudioClient => _audioClient;
    
    /// <summary>
    ///     Gets the current logged-in user.
    /// </summary>
    public SocketGuildUser CurrentUser => _members.TryGetValue(KaiHeiLa.CurrentUser.Id, out SocketGuildUser member) ? member : null;
    /// <summary>
    ///     Gets the built-in role containing all users in this guild.
    /// </summary>
    /// <returns>
    ///     A role object that represents an <c>@everyone</c> role in this guild.
    /// </returns>
    public SocketRole EveryoneRole => GetRole(0);
    
    /// <summary>
    ///     Gets a collection of all text channels in this guild.
    /// </summary>
    /// <returns>
    ///     A read-only collection of message channels found within this guild.
    /// </returns>
    public IReadOnlyCollection<SocketTextChannel> TextChannels
        => Channels.OfType<SocketTextChannel>().ToImmutableArray();
    /// <summary>
    ///     Gets a collection of all voice channels in this guild.
    /// </summary>
    /// <returns>
    ///     A read-only collection of voice channels found within this guild.
    /// </returns>
    public IReadOnlyCollection<SocketVoiceChannel> VoiceChannels
        => Channels.OfType<SocketVoiceChannel>().ToImmutableArray();
    /// <summary>
    ///     Gets a collection of all stage channels in this guild.
    /// </summary>
    /// <returns>
    ///     A read-only collection of stage channels found within this guild.
    /// </returns>
    /// <summary>
    ///     Gets a collection of all category channels in this guild.
    /// </summary>
    /// <returns>
    ///     A read-only collection of category channels found within this guild.
    /// </returns>
    public IReadOnlyCollection<SocketCategoryChannel> CategoryChannels
        => Channels.OfType<SocketCategoryChannel>().ToImmutableArray();
    
    /// <summary>
    ///     Gets a collection of all channels in this guild.
    /// </summary>
    /// <returns>
    ///     A read-only collection of generic channels found within this guild.
    /// </returns>
    public IReadOnlyCollection<SocketGuildChannel> Channels
    {
        get
        {
            var channels = _channels;
            var state = KaiHeiLa.State;
            return channels.Select(x => x.Value).Where(x => x != null).ToReadOnlyCollection(channels);
        }
    }
    /// <summary>
    ///     Gets the default text channel for this guild.
    /// </summary>
    /// <remarks>
    ///     This property retrieves default text channel for this guild.
    /// </remarks>
    /// <returns>
    ///     A <see cref="SocketTextChannel"/> representing the default text channel for this guild.
    /// </returns>
    public SocketTextChannel DefaultChannel => TextChannels
        .Where(c => CurrentUser.GetPermissions(c).ViewChannel)
        .SingleOrDefault(c => c.Id == DefaultChannelId);
    // /// <inheritdoc />
    // public IReadOnlyCollection<GuildEmote> Emotes => _emotes;
    /// <summary>
    ///     Gets a collection of users in this guild.
    /// </summary>
    /// <remarks>
    ///     This property retrieves all users found within this guild.
    ///     <note type="warning">
    ///         <para>
    ///             This property may not always return all the members for large guilds (i.e. guilds containing
    ///             100+ users). If you are simply looking to get the number of users present in this guild,
    ///             consider using <see cref="MemberCount"/> instead.
    ///         </para>
    ///         <para>
    ///             Otherwise, you may need to enable <see cref="KaiHeiLaSocketConfig.AlwaysDownloadUsers"/> to fetch
    ///             the full user list upon startup, or use <see cref="DownloadUsersAsync"/> to manually download
    ///             the users.
    ///         </para>
    ///     </note>
    /// </remarks>
    /// <returns>
    ///     A collection of guild users found within this guild.
    /// </returns>
    public IReadOnlyCollection<SocketGuildUser> Users => _members.ToReadOnlyCollection();
    /// <summary>
    ///     Gets a collection of all roles in this guild.
    /// </summary>
    /// <returns>
    ///     A read-only collection of roles found within this guild.
    /// </returns>
    /// <remarks>
    ///     <note type="warning">
    ///         Due to the lack of event args which should contains the reordered roles data
    ///         when roles are reordered, this property may not be completely accurate.
    ///         To ensure the most accurate results, it is recommended to
    ///         call <see cref="UpdateAsync"/> before this property is used.
    ///     </note>
    /// </remarks>
    public IReadOnlyCollection<SocketRole> Roles => _roles.ToReadOnlyCollection();
    
    internal SocketGuild(KaiHeiLaSocketClient kaiHeiLa, ulong id) : base(kaiHeiLa, id)
    {
        _audioLock = new SemaphoreSlim(1, 1);
    }
    internal static SocketGuild Create(KaiHeiLaSocketClient client, ClientState state, Model model)
    {
        var entity = new SocketGuild(client, model.Id);
        entity.Update(state, model);
        return entity;
    }

    internal void Update(ClientState state, IReadOnlyCollection<ChannelModel> models)
    {
        var channels = new ConcurrentDictionary<ulong, SocketGuildChannel>(ConcurrentHashSet.DefaultConcurrencyLevel,
            (int) (models.Count * 1.05));
        foreach (ChannelModel model in models)
        {
            var channel = SocketGuildChannel.Create(this, state, model);
            state.AddChannel(channel);
            channels.TryAdd(channel.Id, channel);
        }

        _channels = channels;
    }
    internal void Update(ClientState state, IReadOnlyCollection<MemberModel> models)
    {
        var members = new ConcurrentDictionary<ulong, SocketGuildUser>(ConcurrentHashSet.DefaultConcurrencyLevel, (int)(models.Count * 1.05));
        foreach (MemberModel model in models)
        {
            var member = SocketGuildUser.Create(this, state, model);
            if (members.TryAdd(member.Id, member))
                member.GlobalUser.AddRef();
        }
        
        DownloadedMemberCount = members.Count;
        _members = members;
        MemberCount = members.Count;
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
        RecommendInfo = model.RecommendInfo?.ToEntity();
    }
    
    internal void Update(ClientState state, Model model)
    {
        Name = model.Name;
        Topic = model.Topic;
        OwnerId = model.OwnerId;
        Icon = model.Icon;
        NotifyType = model.NotifyType;
        Region = model.Region;
        IsOpenEnabled = model.EnableOpen;
        OpenId = model.OpenId;
        DefaultChannelId = model.DefaultChannelId;
        WelcomeChannelId = model.WelcomeChannelId;

        IsAvailable = true;

        var roles = new ConcurrentDictionary<uint, SocketRole>(ConcurrentHashSet.DefaultConcurrencyLevel,
            (int) ((model.Roles ?? Array.Empty<RoleModel>()).Length * 1.05));
        if (model.Roles != null)
        {
            for (int i = 0; i < (model.Roles ?? Array.Empty<RoleModel>()).Length; i++)
            {
                var role = SocketRole.Create(this, state, model.Roles![i]);
                roles.TryAdd(role.Id, role);
            }
        }
        _roles = roles;

        var channels = new ConcurrentDictionary<ulong, SocketGuildChannel>(ConcurrentHashSet.DefaultConcurrencyLevel,
            (int) ((model.Channels ?? Array.Empty<Channel>()).Length * 1.05));
        {
            for (int i = 0; i < (model.Channels ?? Array.Empty<Channel>()).Length; i++)
            {
                var channel = SocketGuildChannel.Create(this, state, model.Channels![i]);
                state.AddChannel(channel);
                channels.TryAdd(channel.Id, channel);
            }
        }
        _channels = channels;

        _members ??= new ConcurrentDictionary<ulong, SocketGuildUser>();
    }
    internal void Update(ClientState state, GuildEvent model)
    {
        Name = model.Name;
        Icon = model.Icon;
        NotifyType = model.NotifyType;
        Region = model.Region;
        IsOpenEnabled = model.EnableOpen == 1;
        OpenId = model.OpenId;
        DefaultChannelId = model.DefaultChannelId;
        WelcomeChannelId = model.WelcomeChannelId;
        BoostNumber = model.BoostNumber;
        BufferBoostNumber = model.BufferBoostNumber;
        BoostLevel = model.BoostLevel;
        Status = model.Status;

        IsAvailable = true;
    }
    
    /// <inheritdoc />
    public Task UpdateAsync(RequestOptions options = null)
        => SocketGuildHelper.UpdateAsync(this, KaiHeiLa, options);
    
    #endregion
    
    /// <summary>
    ///     Gets the name of the guild.
    /// </summary>
    /// <returns>
    ///     A string that resolves to <see cref="KaiHeiLa.WebSocket.SocketGuild.Name"/>.
    /// </returns>
    public override string ToString() => Name;

    private string DebuggerDisplay => $"{Name} ({Id})";
    internal SocketGuild Clone() => MemberwiseClone() as SocketGuild;

    #region General

    /// <inheritdoc />
    public Task LeaveAsync(RequestOptions options = null)
        => GuildHelper.LeaveAsync(this, KaiHeiLa, options);

    #endregion
    
    #region Bans
    /// <inheritdoc cref="IGuild.GetBansAsync(RequestOptions)"/>
    public Task<IReadOnlyCollection<RestBan>> GetBansAsync(RequestOptions options = null)
        => GuildHelper.GetBansAsync(this, KaiHeiLa, options);
    /// <inheritdoc cref="IGuild.GetBanAsync(IUser,RequestOptions)"/>
    public Task<RestBan> GetBanAsync(IUser user, RequestOptions options = null)
        => GuildHelper.GetBanAsync(this, KaiHeiLa, user.Id, options);
    /// <inheritdoc cref="IGuild.GetBanAsync(ulong,RequestOptions)"/>
    public Task<RestBan> GetBanAsync(ulong userId, RequestOptions options = null)
        => GuildHelper.GetBanAsync(this, KaiHeiLa, userId, options);

    /// <inheritdoc />
    public Task AddBanAsync(IUser user, int pruneDays = 0, string reason = null, RequestOptions options = null)
        => GuildHelper.AddBanAsync(this, KaiHeiLa, user.Id, pruneDays, reason, options);
    /// <inheritdoc />
    public Task AddBanAsync(ulong userId, int pruneDays = 0, string reason = null, RequestOptions options = null)
        => GuildHelper.AddBanAsync(this, KaiHeiLa, userId, pruneDays, reason, options);

    /// <inheritdoc />
    public Task RemoveBanAsync(IUser user, RequestOptions options = null)
        => GuildHelper.RemoveBanAsync(this, KaiHeiLa, user.Id, options);
    /// <inheritdoc />
    public Task RemoveBanAsync(ulong userId, RequestOptions options = null)
        => GuildHelper.RemoveBanAsync(this, KaiHeiLa, userId, options);
    #endregion

    #region Channels

    /// <summary>
    ///     Gets a channel in this guild.
    /// </summary>
    /// <param name="id">The identifier for the channel.</param>
    /// <returns>
    ///     A generic channel associated with the specified <paramref name="id" />; <see langword="null"/> if none is found.
    /// </returns>
    public SocketGuildChannel GetChannel(ulong id)
    {
        var channel = KaiHeiLa.State.GetChannel(id) as SocketGuildChannel;
        if (channel?.Guild.Id == Id)
            return channel;
        return null;
    }
    /// <summary>
    ///     Gets a text channel in this guild.
    /// </summary>
    /// <param name="id">The identifier for the text channel.</param>
    /// <returns>
    ///     A text channel associated with the specified <paramref name="id" />; <see langword="null"/> if none is found.
    /// </returns>
    public SocketTextChannel GetTextChannel(ulong id)
        => GetChannel(id) as SocketTextChannel;
    /// <summary>
    ///     Gets a voice channel in this guild.
    /// </summary>
    /// <param name="id">The snowflake identifier for the voice channel.</param>
    /// <returns>
    ///     A voice channel associated with the specified <paramref name="id" />; <see langword="null"/> if none is found.
    /// </returns>
    public SocketVoiceChannel GetVoiceChannel(ulong id)
        => GetChannel(id) as SocketVoiceChannel;
    
    internal SocketGuildChannel AddChannel(ClientState state, ChannelModel model)
    {
        var channel = SocketGuildChannel.Create(this, state, model);
        _channels.TryAdd(model.Id, channel);
        state.AddChannel(channel);
        return channel;
    }

    /// <summary>
    ///     Creates a new text channel in this guild.
    /// </summary>
    /// <param name="name">The new name for the text channel.</param>
    /// <param name="func">The delegate containing the properties to be applied to the channel upon its creation.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous creation operation. The task result contains the newly created
    ///     text channel.
    /// </returns>
    public Task<RestTextChannel> CreateTextChannelAsync(string name, Action<CreateTextChannelProperties> func = null, RequestOptions options = null)
        => GuildHelper.CreateTextChannelAsync(this, KaiHeiLa, name, options, func);
    /// <summary>
    ///     Creates a new voice channel in this guild.
    /// </summary>
    /// <param name="name">The new name for the voice channel.</param>
    /// <param name="func">The delegate containing the properties to be applied to the channel upon its creation.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <returns>
    ///     A task that represents the asynchronous creation operation. The task result contains the newly created
    ///     voice channel.
    /// </returns>
    public Task<RestVoiceChannel> CreateVoiceChannelAsync(string name, Action<CreateVoiceChannelProperties> func = null, RequestOptions options = null)
        => GuildHelper.CreateVoiceChannelAsync(this, KaiHeiLa, name, options, func);
    /// <summary>
    ///     Creates a new channel category in this guild.
    /// </summary>
    /// <param name="name">The new name for the category.</param>
    /// <param name="func">The delegate containing the properties to be applied to the channel upon its creation.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <returns>
    ///     A task that represents the asynchronous creation operation. The task result contains the newly created
    ///     category channel.
    /// </returns>
    public Task<RestCategoryChannel> CreateCategoryChannelAsync(string name, Action<CreateGuildChannelProperties> func = null, RequestOptions options = null)
        => GuildHelper.CreateCategoryChannelAsync(this, KaiHeiLa, name, options, func);

    internal SocketGuildChannel AddOrUpdateChannel(ClientState state, ChannelModel model)
    {
        if (_channels.TryGetValue(model.Id, out SocketGuildChannel channel))
            channel.Update(KaiHeiLa.State, model);
        else
        {
            channel = SocketGuildChannel.Create(this, KaiHeiLa.State, model);
            _channels[channel.Id] = channel;
            state.AddChannel(channel);
        }
        return channel;
    }

    internal SocketGuildChannel RemoveChannel(ClientState state, ulong id)
    {
        if (_channels.TryRemove(id, out var _))
            return state.RemoveChannel(id) as SocketGuildChannel;
        return null;
    }
    internal void PurgeChannelCache(ClientState state)
    {
        foreach (var channelId in _channels)
            state.RemoveChannel(channelId.Key);

        _channels.Clear();
    }

    #endregion

    // #region Invites
    //
    // /// <summary>
    // ///     Gets a collection of all invites in this guild.
    // /// </summary>
    // /// <param name="options">The options to be used when sending the request.</param>
    // /// <returns>
    // ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    // ///     invite metadata, each representing information for an invite found within this guild.
    // /// </returns>
    // public Task<IReadOnlyCollection<RestInvite>> GetInvitesAsync(RequestOptions options = null)
    //     => GuildHelper.GetInvitesAsync(this, KaiHeiLa, options);
    //
    // #endregion
    
    #region Roles

    /// <summary>
    ///     Gets a role in this guild.
    /// </summary>
    /// <param name="id">The identifier for the role.</param>
    /// <returns>
    ///     A role that is associated with the specified <paramref name="id"/>; <see langword="null"/> if none is found.
    /// </returns>
    public SocketRole GetRole(uint id)
    {
        if (_roles.TryGetValue(id, out SocketRole value))
            return value;
        return null;
    }

    /// <summary>
    ///     Creates a new role with the provided name.
    /// </summary>
    /// <param name="name">The new name for the role.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <returns>
    ///     A task that represents the asynchronous creation operation. The task result contains the newly created
    ///     role.
    /// </returns>
    public Task<RestRole> CreateRoleAsync(string name, RequestOptions options = null)
        => GuildHelper.CreateRoleAsync(this, KaiHeiLa, name, options);
    
    internal SocketRole AddRole(RoleModel model)
    {
        var role = SocketRole.Create(this, KaiHeiLa.State, model);
        _roles[model.Id] = role;
        return role;
    }
    internal SocketRole RemoveRole(uint id)
    {
        if (_roles.TryRemove(id, out SocketRole role))
            return role;
        return null;
    }
    internal SocketRole AddOrUpdateRole(RoleModel model)
    {
        if (_roles.TryGetValue(model.Id, out SocketRole role))
            _roles[model.Id].Update(KaiHeiLa.State, model);
        else
            role = AddRole(model);

        return role;
    }
    
    #endregion
    
    #region Users

    /// <summary>
    ///     Gets a user from this guild.
    /// </summary>
    /// <remarks>
    ///     This method retrieves a user found within this guild.
    ///     <note>
    ///         This may return <see langword="null"/> in the WebSocket implementation due to incomplete user collection in
    ///         large guilds.
    ///     </note>
    /// </remarks>
    /// <param name="id">The identifier of the user.</param>
    /// <returns>
    ///     A guild user associated with the specified <paramref name="id"/>; <see langword="null"/> if none is found.
    /// </returns>
    public SocketGuildUser GetUser(ulong id)
    {
        if (_members.TryGetValue(id, out SocketGuildUser member))
            return member;
        return null;
    }

    /// <summary>
    ///     Gets the users who are muted or deafened in this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains
    ///     the collection of muted or deafened users in this guild.
    /// </returns>
    public async Task<(IReadOnlyCollection<Cacheable<SocketUser, ulong>> Muted, IReadOnlyCollection<Cacheable<SocketUser, ulong>> Deafened)> 
        GetMutedDeafenedUsersAsync(RequestOptions options = null)
    {
        Cacheable<SocketUser, ulong> ParseUser(ulong id)
        {
            Cacheable<SocketUser, ulong> cacheable;
            SocketUser user = Users.SingleOrDefault(x => x.Id == id);
            if (user is not null)
            {
                cacheable = new Cacheable<SocketUser, ulong>(user, id, true, () => Task.FromResult(user));
            }
            else
            {
                user = SocketUnknownUser.Create(KaiHeiLa, KaiHeiLa.State, id);

                async Task<SocketUser> DownloadFunc()
                {
                    var model = await KaiHeiLa.ApiClient.GetUserAsync(id, options)
                        .ConfigureAwait(false);
                    var guildUser = SocketGlobalUser.Create(KaiHeiLa, KaiHeiLa.State, model);
                    return guildUser;
                }

                cacheable = new Cacheable<SocketUser, ulong>(user, id, false, DownloadFunc);
            }
            return cacheable;
        }

        var users = await GuildHelper.GetGuildMutedDeafenedUsersAsync(this, KaiHeiLa, options);
        var mutedUsers = users.Muted.Select(ParseUser).ToImmutableArray();
        var deafenedUsers = users.Deafened.Select(ParseUser).ToImmutableArray();
        return (mutedUsers, deafenedUsers);
    }

    internal SocketGuildUser AddOrUpdateUser(UserModel model)
    {
        if (_members.TryGetValue(model.Id, out SocketGuildUser member))
        {
            member.GlobalUser?.Update(KaiHeiLa.State, model);
            member.UpdatePresence(model.Online, model.OperatingSystem);
        }
        else
        {
            member = SocketGuildUser.Create(this, KaiHeiLa.State, model);
            member.GlobalUser.AddRef();
            _members[member.Id] = member;
            DownloadedMemberCount++;
        }
        return member;
    }
    internal SocketGuildUser AddOrUpdateUser(MemberModel model)
    {
        if (_members.TryGetValue(model.Id, out SocketGuildUser member))
            member.Update(KaiHeiLa.State, model);
        else
        {
            member = SocketGuildUser.Create(this, KaiHeiLa.State, model);
            member.GlobalUser.AddRef();
            _members[member.Id] = member;
            DownloadedMemberCount++;
        }
        return member;
    }
    internal SocketGuildUser RemoveUser(ulong id)
    {
        if (_members.TryRemove(id, out SocketGuildUser member))
        {
            DownloadedMemberCount--;
            member.GlobalUser.RemoveRef(KaiHeiLa);
            return member;
        }
        return null;
    }

    /// <summary>
    ///     Purges this guild's user cache.
    /// </summary>
    public void PurgeUserCache() => PurgeUserCache(_ => true);
    /// <summary>
    ///     Purges this guild's user cache.
    /// </summary>
    /// <param name="predicate">The predicate used to select which users to clear.</param>
    public void PurgeUserCache(Func<SocketGuildUser, bool> predicate)
    {
        var membersToPurge = Users.Where(x => predicate.Invoke(x) && x?.Id != KaiHeiLa.CurrentUser.Id);
        var membersToKeep = Users.Where(x => !predicate.Invoke(x) || x?.Id == KaiHeiLa.CurrentUser.Id);

        foreach (var member in membersToPurge)
            if(_members.TryRemove(member.Id, out _))
                member.GlobalUser.RemoveRef(KaiHeiLa);

        foreach (var member in membersToKeep)
            _members.TryAdd(member.Id, member);

        DownloadedMemberCount = _members.Count;
    }

    /// <summary>
    ///     Gets a collection of all users in this guild.
    /// </summary>
    /// <remarks>
    ///     <para>This method retrieves all users found within this guild through REST.</para>
    ///     <para>Users returned by this method are not cached.</para>
    /// </remarks>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a collection of guild
    ///     users found within this guild.
    /// </returns>
    public IAsyncEnumerable<IReadOnlyCollection<IGuildUser>> GetUsersAsync(RequestOptions options = null)
    {
        if (HasAllMembers)
            return ImmutableArray.Create(Users).ToAsyncEnumerable<IReadOnlyCollection<IGuildUser>>();
        return GuildHelper.GetUsersAsync(this, KaiHeiLa, KaiHeiLaConfig.MaxUsersPerBatch, 1, options);
    }
    
    public async Task DownloadUsersAsync()
    {
        await KaiHeiLa.DownloadUsersAsync(new[] { this }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a collection of users in this guild that the name or nickname contains the
    ///     provided string at <paramref name="func"/>.
    /// </summary>
    /// <remarks>
    ///     The <paramref name="limit"/> can not be higher than <see cref="KaiHeiLaConfig.MaxUsersPerBatch"/>.
    /// </remarks>
    /// <param name="func">A delegate containing the properties to search users with.</param>
    /// <param name="limit">The maximum number of users to be gotten.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a collection of guild
    ///     users that matches the properties with the provided <see cref="Action{SearchGuildMemberProperties}"/> at <paramref name="func"/>.
    /// </returns>
    public IAsyncEnumerable<IReadOnlyCollection<RestGuildUser>> SearchUsersAsync(Action<SearchGuildMemberProperties> func, 
        int limit = KaiHeiLaConfig.MaxUsersPerBatch, RequestOptions options = null)
        => GuildHelper.SearchUsersAsync(this, KaiHeiLa, func, limit, options);
    
    #endregion

    #region Voices
    
    /// <inheritdoc />
    public async Task MoveUsersAsync(IEnumerable<IGuildUser> users, IVoiceChannel targetChannel, RequestOptions options)
        => await ClientHelper.MoveUsersAsync(KaiHeiLa, users, targetChannel, options).ConfigureAwait(false);

    #endregion
    
    #region Emotes

    /// <inheritdoc />
    public Task<IReadOnlyCollection<GuildEmote>> GetEmotesAsync(RequestOptions options = null)
        => GuildHelper.GetEmotesAsync(this, KaiHeiLa, options);
    /// <inheritdoc />
    public Task<GuildEmote> GetEmoteAsync(string id, RequestOptions options = null)
        => GuildHelper.GetEmoteAsync(this, KaiHeiLa, id, options);
    /// <inheritdoc />
    public Task<GuildEmote> CreateEmoteAsync(string name, Image image , RequestOptions options = null)
        => GuildHelper.CreateEmoteAsync(this, KaiHeiLa, name, image, options);
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public Task ModifyEmoteNameAsync(GuildEmote emote, Action<string> func, RequestOptions options = null)
        => GuildHelper.ModifyEmoteNameAsync(this, KaiHeiLa, emote, func, options);
    /// <inheritdoc />
    public Task DeleteEmoteAsync(GuildEmote emote, RequestOptions options = null)
        => GuildHelper.DeleteEmoteAsync(this, KaiHeiLa, emote.Id, options);

    #endregion
    
    #region Invites
    
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<IInvite>> GetInvitesAsync(RequestOptions options = null)
        => await GuildHelper.GetInvitesAsync(this, KaiHeiLa, options).ConfigureAwait(false);
    /// <inheritdoc />
    public async Task<IInvite> CreateInviteAsync(int? maxAge = 604800, int? maxUses = null, RequestOptions options = null)
        => await GuildHelper.CreateInviteAsync(this, KaiHeiLa, maxAge, maxUses, options).ConfigureAwait(false);
    /// <inheritdoc />
    public async Task<IInvite> CreateInviteAsync(InviteMaxAge maxAge = InviteMaxAge.OneWeek, InviteMaxUses maxUses = InviteMaxUses.Unlimited, RequestOptions options = null)
        => await GuildHelper.CreateInviteAsync(this, KaiHeiLa, maxAge, maxUses, options).ConfigureAwait(false);

    #endregion
    
    #region Audio
    
    internal AudioInStream GetAudioStream(ulong userId)
    {
        return _audioClient?.GetInputStream(userId);
    }
    internal async Task<IAudioClient> ConnectAudioAsync(ulong channelId)
    {
        TaskCompletionSource<AudioClient> promise;

        await _audioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisconnectAudioInternalAsync().ConfigureAwait(false);
            promise = new TaskCompletionSource<AudioClient>();
            _audioConnectPromise = promise;

            if (_audioClient == null)
            {
                var audioClient = new AudioClient(this, KaiHeiLa.GetAudioId(), channelId);
                audioClient.Disconnected += async ex =>
                {
                    if (!promise.Task.IsCompleted)
                    {
                        try
                        { audioClient.Dispose(); }
                        catch { }
                        _audioClient = null;
                        if (ex != null)
                            await promise.TrySetExceptionAsync(ex);
                        else
                            await promise.TrySetCanceledAsync();
                        return;
                    }
                };
                audioClient.Connected += () =>
                {
                    var _ = promise.TrySetResultAsync(_audioClient);
                    return Task.Delay(0);
                };
                _audioClient = audioClient;
            }
        }
        catch
        {
            await DisconnectAudioInternalAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _audioLock.Release();
        }

        var _ = FinishConnectAudio().ConfigureAwait(false);
        
        try
        {
            var timeoutTask = Task.Delay(15000);
            if (await Task.WhenAny(promise.Task, timeoutTask).ConfigureAwait(false) == timeoutTask)
                throw new TimeoutException();
            return await promise.Task.ConfigureAwait(false);
        }
        catch
        {
            await DisconnectAudioAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal async Task DisconnectAudioAsync()
    {
        await _audioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisconnectAudioInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _audioLock.Release();
        }
    }
    private async Task DisconnectAudioInternalAsync()
    {
        _audioConnectPromise?.TrySetCanceledAsync(); //Cancel any previous audio connection
        _audioConnectPromise = null;
        if (_audioClient != null)
            await _audioClient.StopAsync().ConfigureAwait(false);
        _audioClient?.Dispose();
        _audioClient = null;
    }

    internal async Task FinishConnectAudio()
    {
        await _audioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_audioClient != null)
            {
                await _audioClient.StartAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            await DisconnectAudioInternalAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            await _audioConnectPromise.SetExceptionAsync(e).ConfigureAwait(false);
            await DisconnectAudioInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _audioLock.Release();
        }
    }
    
    #endregion
    
    #region IGuild
    
    /// <inheritdoc />
    IAudioClient IGuild.AudioClient => AudioClient;
    /// <inheritdoc />
    bool IGuild.Available => true;
    
    public void Dispose() { }
    
    /// <inheritdoc />
    async Task<IReadOnlyCollection<IGuildUser>> IGuild.GetUsersAsync(CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload && !HasAllMembers)
            return (await GetUsersAsync(options).FlattenAsync().ConfigureAwait(false)).ToImmutableArray();
        else
            return Users;
    }
    /// <inheritdoc />
    Task<IGuildUser> IGuild.GetUserAsync(ulong id, CacheMode mode, RequestOptions options)
        => Task.FromResult<IGuildUser>(GetUser(id));
    /// <inheritdoc />
    Task<IGuildUser> IGuild.GetCurrentUserAsync(CacheMode mode, RequestOptions options)
        => Task.FromResult<IGuildUser>(CurrentUser);
    /// <inheritdoc />
    Task<IGuildUser> IGuild.GetOwnerAsync(CacheMode mode, RequestOptions options)
        => Task.FromResult<IGuildUser>(Owner);
    /// <inheritdoc />
    IAsyncEnumerable<IReadOnlyCollection<IGuildUser>> IGuild.SearchUsersAsync(Action<SearchGuildMemberProperties> func, int limit, CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return SearchUsersAsync(func, limit, options);
        else
            return AsyncEnumerable.Empty<IReadOnlyCollection<IGuildUser>>();
    }
    /// <inheritdoc />
    IRole IGuild.EveryoneRole => EveryoneRole;
    /// <inheritdoc />
    IReadOnlyCollection<IRole> IGuild.Roles => Roles;
    /// <inheritdoc />
    /// <remarks>
    ///     Not implemented.
    /// </remarks>
    IReadOnlyCollection<GuildEmote> IGuild.Emotes => null;
    /// <inheritdoc />
    IRole IGuild.GetRole(uint id) => GetRole(id);
    /// <inheritdoc />
    IRecommendInfo IGuild.RecommendInfo => RecommendInfo;
    /// <inheritdoc />
    async Task<IRole> IGuild.CreateRoleAsync(string name, RequestOptions options)
        => await CreateRoleAsync(name, options).ConfigureAwait(false);
    
    /// <inheritdoc />
    async Task<IReadOnlyCollection<IBan>> IGuild.GetBansAsync(RequestOptions options)
        => await GetBansAsync(options).ConfigureAwait(false);
    /// <inheritdoc/>
    async Task<IBan> IGuild.GetBanAsync(IUser user, RequestOptions options)
        => await GetBanAsync(user, options).ConfigureAwait(false);
    /// <inheritdoc/>
    async Task<IBan> IGuild.GetBanAsync(ulong userId, RequestOptions options)
        => await GetBanAsync(userId, options).ConfigureAwait(false);
    
    /// <inheritdoc />
    Task<IReadOnlyCollection<IGuildChannel>> IGuild.GetChannelsAsync(CacheMode mode, RequestOptions options)
        => Task.FromResult<IReadOnlyCollection<IGuildChannel>>(Channels);
    /// <inheritdoc />
    Task<IGuildChannel> IGuild.GetChannelAsync(ulong id, CacheMode mode, RequestOptions options)
        => Task.FromResult<IGuildChannel>(GetChannel(id));
    /// <inheritdoc />
    Task<ITextChannel> IGuild.GetDefaultChannelAsync(CacheMode mode, RequestOptions options)
        => Task.FromResult<ITextChannel>(DefaultChannel);
    /// <inheritdoc />
    Task<IReadOnlyCollection<ITextChannel>> IGuild.GetTextChannelsAsync(CacheMode mode, RequestOptions options)
        => Task.FromResult<IReadOnlyCollection<ITextChannel>>(TextChannels);
    /// <inheritdoc />
    Task<ITextChannel> IGuild.GetTextChannelAsync(ulong id, CacheMode mode, RequestOptions options)
        => Task.FromResult<ITextChannel>(GetTextChannel(id));
    /// <inheritdoc />
    Task<IReadOnlyCollection<IVoiceChannel>> IGuild.GetVoiceChannelsAsync(CacheMode mode, RequestOptions options)
        => Task.FromResult<IReadOnlyCollection<IVoiceChannel>>(VoiceChannels);
    /// <inheritdoc />
    Task<IVoiceChannel> IGuild.GetVoiceChannelAsync(ulong id, CacheMode mode, RequestOptions options)
        => Task.FromResult<IVoiceChannel>(GetVoiceChannel(id));
    /// <inheritdoc />
    Task<IReadOnlyCollection<ICategoryChannel>> IGuild.GetCategoryChannelsAsync(CacheMode mode,
        RequestOptions options)
        => Task.FromResult<IReadOnlyCollection<ICategoryChannel>>(CategoryChannels);
    
    /// <inheritdoc />
    async Task<ITextChannel> IGuild.CreateTextChannelAsync(string name, Action<CreateTextChannelProperties> func, RequestOptions options)
        => await CreateTextChannelAsync(name, func, options).ConfigureAwait(false);
    /// <inheritdoc />
    async Task<IVoiceChannel> IGuild.CreateVoiceChannelAsync(string name, Action<CreateVoiceChannelProperties> func, RequestOptions options)
        => await CreateVoiceChannelAsync(name, func, options).ConfigureAwait(false);
    /// <inheritdoc />
    async Task<ICategoryChannel> IGuild.CreateCategoryChannelAsync(string name, Action<CreateGuildChannelProperties> func, RequestOptions options)
        => await CreateCategoryChannelAsync(name, func, options).ConfigureAwait(false);
    
    /// <inheritdoc />
    async Task<(IReadOnlyCollection<Cacheable<IUser, ulong>> Muted, IReadOnlyCollection<Cacheable<IUser, ulong>> Deafened)> IGuild.GetMutedDeafenedUsersAsync(RequestOptions options)
    {
        var users = await GetMutedDeafenedUsersAsync(options).ConfigureAwait(false);
        var muted = users.Muted.Select(x =>
                new Cacheable<IUser, ulong>(x.Value, x.Id, x.HasValue, async () => await x.DownloadAsync()))
            .ToImmutableArray();
        var deafened = users.Deafened.Select(x =>
                new Cacheable<IUser, ulong>(x.Value, x.Id, x.HasValue, async () => await x.DownloadAsync()))
            .ToImmutableArray();
        return (muted, deafened);
    }

    /// <inheritdoc />
    public async Task<Stream> GetBadgeAsync(BadgeStyle style = BadgeStyle.GuildName, RequestOptions options = null)
    {
        return await GuildHelper.GetBadgeAsync(this, KaiHeiLa, style, options).ConfigureAwait(false);
    }

    void IDisposable.Dispose()
    {
        DisconnectAudioAsync().GetAwaiter().GetResult();
        _audioLock?.Dispose();
        _audioClient?.Dispose();
    }
    
    #endregion
}