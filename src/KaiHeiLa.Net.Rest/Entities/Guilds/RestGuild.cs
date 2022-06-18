using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using KaiHeiLa.API;
using KaiHeiLa.Audio;
using Model = KaiHeiLa.API.Guild;
using ExtendedModel = KaiHeiLa.API.Rest.ExtendedGuild;

namespace KaiHeiLa.Rest;

/// <summary>
///     Represents a REST-based guild/server.
/// </summary>
[DebuggerDisplay(@"{DebuggerDisplay,nq}")]
public class RestGuild : RestEntity<ulong>, IGuild, IUpdateable
{
    #region RestGuild

    private ImmutableDictionary<uint, RestRole> _roles;
    private ImmutableDictionary<ulong, RestChannel> _channels;
    // private ImmutableArray<GuildEmote> _emotes;
    
    /// <inheritdoc />
    public string Name { get; private set; }
    /// <inheritdoc />
    public string Topic { get; private set; }
    /// <inheritdoc />
    public ulong OwnerId { get; private set; }
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

    internal bool Available { get; private set; }
    
    /// <summary>
    ///     Gets the built-in role containing all users in this guild.
    /// </summary>
    public RestRole EveryoneRole => GetRole(0);

    // /// <inheritdoc />
    // public IReadOnlyCollection<GuildEmote> Emotes { get; set; }

    /// <summary>
    ///     Gets a collection of all roles in this guild.
    /// </summary>
    public IReadOnlyCollection<RestRole> Roles => _roles.ToReadOnlyCollection();
    
    /// <summary>
    ///     Gets a collection of all channels in this guild.
    /// </summary>
    public IReadOnlyCollection<RestChannel> Channels => _channels.ToReadOnlyCollection();
    
    // /// <inheritdoc />
    // public IReadOnlyCollection<GuildEmote> Emotes => _emotes;
    
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
    
    internal RestGuild(BaseKaiHeiLaClient client, ulong id)
        : base(client, id)
    {
    }
    internal static RestGuild Create(BaseKaiHeiLaClient kaiHeiLa, Model model)
    {
        var entity = new RestGuild(kaiHeiLa, model.Id);
        entity.Update(model);
        return entity;
    }

    internal void Update(ExtendedModel model)
    {
        Update(model as Model);

        Features = model.Features;
        BoostNumber = model.BoostNumber;
        BufferBoostNumber = model.BufferBoostNumber;
        BoostLevel = model.BoostLevel;
        Status = model.Status;
        AutoDeleteTime = model.AutoDeleteTime;
        RecommendInfo = model.RecommendInfo?.ToEntity();
    }
    internal void Update(Model model)
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

        Available = true;

        var roles = ImmutableDictionary.CreateBuilder<uint, RestRole>();
        if (model.Roles != null)
        {
            for (int i = 0; i < model.Roles.Length; i++)
                roles[model.Roles[i].Id] = RestRole.Create(KaiHeiLa, this, model.Roles[i]);
        }
        _roles = roles.ToImmutable();
        
        var channels = ImmutableDictionary.CreateBuilder<ulong, RestChannel>();
        if (model.Channels != null)
        {
            for (int i = 0; i < model.Channels.Length; i++)
                channels[model.Channels[i].Id] = RestChannel.Create(KaiHeiLa, model.Channels[i], this);
        }
        _channels = channels.ToImmutable();
    }
    
    #endregion

    #region Generals

    /// <inheritdoc />
    public async Task UpdateAsync(RequestOptions options = null)
    {
        var model = await KaiHeiLa.ApiClient.GetGuildAsync(Id, options).ConfigureAwait(false);
        Update(model);
    }
    /// <inheritdoc />
    public Task LeaveAsync(RequestOptions options = null)
        => GuildHelper.LeaveAsync(this, KaiHeiLa, options);
    
    #endregion
    
    #region Bans
    
    /// <summary>
    ///     Gets a collection of all users banned in this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    ///     ban objects that this guild currently possesses, with each object containing the user banned and reason
    ///     behind the ban.
    /// </returns>
    public Task<IReadOnlyCollection<RestBan>> GetBansAsync(RequestOptions options = null)
        => GuildHelper.GetBansAsync(this, KaiHeiLa, options);
    /// <summary>
    ///     Gets a ban object for a banned user.
    /// </summary>
    /// <param name="user">The banned user.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a ban object, which
    ///     contains the user information and the reason for the ban; <see langword="null"/> if the ban entry cannot be found.
    /// </returns>
    public Task<RestBan> GetBanAsync(IUser user, RequestOptions options = null)
        => GuildHelper.GetBanAsync(this, KaiHeiLa, user.Id, options);
    /// <summary>
    ///     Gets a ban object for a banned user.
    /// </summary>
    /// <param name="userId">The identifier for the banned user.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a ban object, which
    ///     contains the user information and the reason for the ban; <see langword="null"/> if the ban entry cannot be found.
    /// </returns>
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
    public RestRole GetRole(uint id)
    {
        if (_roles.TryGetValue(id, out RestRole value))
            return value;
        return null;
    }
    
    /// <summary>
    ///     Creates a new role with the provided name.
    /// </summary>
    /// <param name="name">The new name for the role.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous creation operation. The task result contains the newly created
    ///     role.
    /// </returns>
    public async Task<RestRole> CreateRoleAsync(string name, RequestOptions options = null)
    {
        var role = await GuildHelper.CreateRoleAsync(this, KaiHeiLa, name, options).ConfigureAwait(false);
        _roles = _roles.Add(role.Id, role);
        return role;
    }

    #endregion

    #region Users
    
    /// <summary>
    ///     Gets a collection of all users in this guild.
    /// </summary>
    /// <remarks>
    ///     This method retrieves all users found within this guild.
    /// </remarks>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a collection of guild
    ///     users found within this guild.
    /// </returns>
    public IAsyncEnumerable<IReadOnlyCollection<RestGuildUser>> GetUsersAsync(RequestOptions options = null)
        => GuildHelper.GetUsersAsync(this, KaiHeiLa, KaiHeiLaConfig.MaxUsersPerBatch, 1, options);

    /// <summary>
    ///     Gets a user from this guild.
    /// </summary>
    /// <remarks>
    ///     This method retrieves a user found within this guild.
    /// </remarks>
    /// <param name="id">The identifier of the user.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the guild user
    ///     associated with the specified <paramref name="id"/>; <see langword="null"/> if none is found.
    /// </returns>
    public Task<RestGuildUser> GetUserAsync(ulong id, RequestOptions options = null)
        => GuildHelper.GetUserAsync(this, KaiHeiLa, id, options);

    /// <summary>
    ///     Gets the current user for this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the currently logged-in
    ///     user within this guild.
    /// </returns>
    public Task<RestGuildUser> GetCurrentUserAsync(RequestOptions options = null)
        => GuildHelper.GetUserAsync(this, KaiHeiLa, KaiHeiLa.CurrentUser.Id, options);

    /// <summary>
    ///     Gets the owner of this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the owner of this guild.
    /// </returns>
    public Task<RestGuildUser> GetOwnerAsync(RequestOptions options = null)
        => GuildHelper.GetUserAsync(this, KaiHeiLa, OwnerId, options);

    /// <summary>
    ///     Gets the users who are muted or deafened in this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains
    ///     the collection of muted or deafened users in this guild.
    /// </returns>
    public async Task<(IReadOnlyCollection<Cacheable<RestUser, ulong>> Muted, IReadOnlyCollection<Cacheable<RestUser, ulong>> Deafened)> 
        GetMutedDeafenedUsersAsync(RequestOptions options = null)
    {
        Cacheable<RestUser, ulong> ParseUser(ulong id) => new(null, id, false,
            async () => await ClientHelper.GetUserAsync(KaiHeiLa, id, options).ConfigureAwait(false));

        var users = await GuildHelper.GetGuildMutedDeafenedUsersAsync(this, KaiHeiLa, options);
        var mutedUsers = users.Muted.Select(ParseUser).ToImmutableArray();
        var deafenedUsers = users.Deafened.Select(ParseUser).ToImmutableArray();
        return (mutedUsers, deafenedUsers);
    }
    
    /// <summary>
    ///     Gets a collection of users in this guild that the name or nickname contains the
    ///     provided <see langword="string"/> at <paramref name="func"/>.
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
    public IAsyncEnumerable<IReadOnlyCollection<RestGuildUser>> SearchUsersAsync(Action<SearchGuildMemberProperties> func, int limit = KaiHeiLaConfig.MaxUsersPerBatch, RequestOptions options = null)
        => GuildHelper.SearchUsersAsync(this, KaiHeiLa, func, limit, options);
    
    #endregion
    
    #region Channels
    
    /// <summary>
    ///     Gets a collection of all channels in this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    ///     generic channels found within this guild.
    /// </returns>
    public Task<IReadOnlyCollection<RestGuildChannel>> GetChannelsAsync(RequestOptions options = null)
        => GuildHelper.GetChannelsAsync(this, KaiHeiLa, options);
    /// <summary>
    ///     Gets a channel in this guild.
    /// </summary>
    /// <param name="id">The identifier for the channel.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the generic channel
    ///     associated with the specified <paramref name="id"/>; <see langword="null"/> if none is found.
    /// </returns>
    public Task<RestGuildChannel> GetChannelAsync(ulong id, RequestOptions options = null)
        => GuildHelper.GetChannelAsync(this, KaiHeiLa, id, options);
    /// <summary>
    ///     Gets a text channel in this guild.
    /// </summary>
    /// <param name="id">The snowflake identifier for the text channel.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the text channel
    ///     associated with the specified <paramref name="id"/>; <see langword="null"/> if none is found.
    /// </returns>
    public async Task<RestTextChannel> GetTextChannelAsync(ulong id, RequestOptions options = null)
    {
        var channel = await GuildHelper.GetChannelAsync(this, KaiHeiLa, id, options).ConfigureAwait(false);
        return channel as RestTextChannel;
    }
    /// <summary>
    ///     Gets a collection of all text channels in this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    ///     message channels found within this guild.
    /// </returns>
    public async Task<IReadOnlyCollection<RestTextChannel>> GetTextChannelsAsync(RequestOptions options = null)
    {
        var channels = await GuildHelper.GetChannelsAsync(this, KaiHeiLa, options).ConfigureAwait(false);
        return channels.OfType<RestTextChannel>().ToImmutableArray();
    }
    /// <summary>
    ///     Gets a voice channel in this guild.
    /// </summary>
    /// <param name="id">The snowflake identifier for the voice channel.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the voice channel associated
    ///     with the specified <paramref name="id"/>; <see langword="null"/> if none is found.
    /// </returns>
    public async Task<RestVoiceChannel> GetVoiceChannelAsync(ulong id, RequestOptions options = null)
    {
        var channel = await GuildHelper.GetChannelAsync(this, KaiHeiLa, id, options).ConfigureAwait(false);
        return channel as RestVoiceChannel;
    }
    /// <summary>
    ///     Gets a collection of all voice channels in this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    ///     voice channels found within this guild.
    /// </returns>
    public async Task<IReadOnlyCollection<RestVoiceChannel>> GetVoiceChannelsAsync(RequestOptions options = null)
    {
        var channels = await GuildHelper.GetChannelsAsync(this, KaiHeiLa, options).ConfigureAwait(false);
        return channels.OfType<RestVoiceChannel>().ToImmutableArray();
    }
    /// <summary>
    ///     Gets a collection of all category channels in this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    ///     category channels found within this guild.
    /// </returns>
    public async Task<IReadOnlyCollection<RestCategoryChannel>> GetCategoryChannelsAsync(RequestOptions options = null)
    {
        var channels = await GuildHelper.GetChannelsAsync(this, KaiHeiLa, options).ConfigureAwait(false);
        return channels.OfType<RestCategoryChannel>().ToImmutableArray();
    }
    
    /// <summary>
    ///     Gets the default text channel in this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the default text channel of this guild;
    ///     <see langword="null" /> if none is found.
    /// </returns>
    public async Task<RestTextChannel> GetDefaultChannelAsync(RequestOptions options = null)
    {
        var channels = await GetTextChannelsAsync(options).ConfigureAwait(false);
        var user = await GetCurrentUserAsync(options).ConfigureAwait(false);
        return channels
            .Where(c => user.GetPermissions(c).ViewChannel)
            .MinBy(c => c.Position);
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
    ///     Creates a voice channel with the provided name.
    /// </summary>
    /// <param name="name">The name of the new channel.</param>
    /// <param name="func">The delegate containing the properties to be applied to the channel upon its creation.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name" /> is <see langword="null"/>.</exception>
    /// <returns>
    ///     The created voice channel.
    /// </returns>
    public Task<RestVoiceChannel> CreateVoiceChannelAsync(string name, Action<CreateVoiceChannelProperties> func = null, RequestOptions options = null)
        => GuildHelper.CreateVoiceChannelAsync(this, KaiHeiLa, name, options, func);
    /// <summary>
    ///     Creates a category channel with the provided name.
    /// </summary>
    /// <param name="name">The name of the new channel.</param>
    /// <param name="func">The delegate containing the properties to be applied to the channel upon its creation.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name" /> is <see langword="null"/>.</exception>
    /// <returns>
    ///     The created category channel.
    /// </returns>
    public Task<RestCategoryChannel> CreateCategoryChannelAsync(string name, Action<CreateGuildChannelProperties> func = null, RequestOptions options = null)
        => GuildHelper.CreateCategoryChannelAsync(this, KaiHeiLa, name, options, func);
    
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
    public Task<GuildEmote> CreateEmoteAsync(string name, Image image, RequestOptions options = null)
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
    
    #region IGuild
    
    /// <inheritdoc />
    bool IGuild.Available => Available;
    /// <inheritdoc />
    IAudioClient IGuild.AudioClient => null;
    
    /// <inheritdoc />
    IReadOnlyCollection<IRole> IGuild.Roles => Roles;

    /// <inheritdoc />
    /// <remarks>
    ///     Not implemented.
    /// </remarks>
    IReadOnlyCollection<GuildEmote> IGuild.Emotes => null;

    /// <inheritdoc />
    IRecommendInfo IGuild.RecommendInfo => RecommendInfo;
    
    /// <inheritdoc />
    IRole IGuild.EveryoneRole => EveryoneRole;

    /// <inheritdoc />
    IRole IGuild.GetRole(uint id) => GetRole(id);
    
    /// <inheritdoc />
    async Task<IRole> IGuild.CreateRoleAsync(string name, RequestOptions options)
        => await CreateRoleAsync(name, options).ConfigureAwait(false);

    /// <inheritdoc />
    async Task<IGuildUser> IGuild.GetCurrentUserAsync(CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetCurrentUserAsync(options).ConfigureAwait(false);
        else
            return null;
    }
    /// <inheritdoc />
    async Task<IGuildUser> IGuild.GetOwnerAsync(CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetOwnerAsync(options).ConfigureAwait(false);
        else
            return null;
    }
    /// <inheritdoc />
    async Task<IReadOnlyCollection<IGuildUser>> IGuild.GetUsersAsync(CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return (await GetUsersAsync(options).FlattenAsync().ConfigureAwait(false)).ToImmutableArray();
        else
            return ImmutableArray.Create<IGuildUser>();
    }
    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Downloading users is not supported for a REST-based guild.</exception>
    Task IGuild.DownloadUsersAsync() =>
        throw new NotSupportedException();

    /// <inheritdoc />
    IAsyncEnumerable<IReadOnlyCollection<IGuildUser>> IGuild.SearchUsersAsync(Action<SearchGuildMemberProperties> func, int limit, CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return SearchUsersAsync(func, limit, options);
        else
            return AsyncEnumerable.Empty<IReadOnlyCollection<IGuildUser>>();
    }
    /// <inheritdoc />
    async Task<IGuildUser> IGuild.GetUserAsync(ulong id, CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetUserAsync(id, options).ConfigureAwait(false);
        else
            return null;
    }
    
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
    async Task<IReadOnlyCollection<IGuildChannel>> IGuild.GetChannelsAsync(CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetChannelsAsync(options).ConfigureAwait(false);
        else
            return ImmutableArray.Create<IGuildChannel>();
    }
    /// <inheritdoc />
    async Task<IGuildChannel> IGuild.GetChannelAsync(ulong id, CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetChannelAsync(id, options).ConfigureAwait(false);
        else
            return null;
    }
    /// <inheritdoc />
    async Task<ITextChannel> IGuild.GetDefaultChannelAsync(CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetDefaultChannelAsync(options).ConfigureAwait(false);
        else
            return null;
    }
    /// <inheritdoc />
    async Task<IReadOnlyCollection<ITextChannel>> IGuild.GetTextChannelsAsync(CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetTextChannelsAsync(options).ConfigureAwait(false);
        else
            return ImmutableArray.Create<ITextChannel>();
    }
    /// <inheritdoc />
    async Task<ITextChannel> IGuild.GetTextChannelAsync(ulong id, CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetTextChannelAsync(id, options).ConfigureAwait(false);
        else
            return null;
    }
    /// <inheritdoc />
    async Task<IReadOnlyCollection<IVoiceChannel>> IGuild.GetVoiceChannelsAsync(CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetVoiceChannelsAsync(options).ConfigureAwait(false);
        else
            return ImmutableArray.Create<IVoiceChannel>();
    }
    /// <inheritdoc />
    async Task<IVoiceChannel> IGuild.GetVoiceChannelAsync(ulong id, CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetVoiceChannelAsync(id, options).ConfigureAwait(false);
        else
            return null;
    }
    /// <inheritdoc />
    async Task<IReadOnlyCollection<ICategoryChannel>> IGuild.GetCategoryChannelsAsync(CacheMode mode,
        RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetCategoryChannelsAsync(options).ConfigureAwait(false);
        else
            return null;
    }
    
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

    #endregion

    /// <summary>
    ///     Returns the name of the guild.
    /// </summary>
    /// <returns>
    ///     The name of the guild.
    /// </returns>
    public override string ToString() => Name;
    private string DebuggerDisplay => $"{Name} ({Id})";
}