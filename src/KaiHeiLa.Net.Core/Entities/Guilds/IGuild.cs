using KaiHeiLa.Audio;

namespace KaiHeiLa;

/// <summary>
///     Represents a generic guild/server.
/// </summary>
public interface IGuild : IEntity<ulong>
{
    #region General

    /// <summary>
    ///     Gets the name of this guild.
    /// </summary>
    /// <returns>
    ///     A string containing the name of this guild.
    /// </returns>
    string Name { get; }
    /// <summary>
    ///     Gets the topic for the guild.
    /// </summary>
    /// <returns>
    ///     The description for the guild; <see langword="null" /> if none is set.
    /// </returns>
    string Topic { get; }
    /// <summary>
    ///     Gets the ID of the user that owns this guild.
    /// </summary>
    /// <returns>
    ///     A <see langword="ulong"/> representing the identifier of the user that owns this guild.
    /// </returns>
    ulong OwnerId { get; }
    /// <summary>
    ///     Gets the URL of this guild's icon.
    /// </summary>
    /// <returns>
    ///     A URL pointing to the guild's icon; <see langword="null" /> if none is set.
    /// </returns>
    string Icon { get; }
    /// <summary>
    ///     Gets the default notification type for this guild.
    /// </summary>
    /// <returns>
    ///     A <see cref="NotifyType"/> representing the default notification type for this guild.
    /// </returns>
    NotifyType NotifyType { get; }
    /// <summary>
    ///     Gets the default voice region for this guild.
    /// </summary>
    /// <returns>
    ///     A string representing the default voice region for this guild.
    /// </returns>
    string Region { get; }
    /// <summary>
    ///     Gets whether this guild is public.
    /// </summary>
    /// <returns>
    ///     <see langword="true"/> if this guild is public; <see langword="false"/> otherwise.
    /// </returns>
    bool IsOpenEnabled { get; }
    /// <summary>
    ///     Gets the open ID for this guild.
    /// </summary>
    /// <returns>
    ///     A <see langword="uint"/> representing the open ID for this guild.
    /// </returns>
    uint OpenId { get; }
    /// <summary>
    ///     Gets the default channel ID for this guild.
    /// </summary>
    /// <returns>
    ///     A <see langword="ulong"/> representing the default channel ID for this guild.
    /// </returns>
    ulong DefaultChannelId { get; }
    /// <summary>
    ///     Gets the welcome channel ID for this guild.
    /// </summary>
    /// <returns>
    ///     A <see langword="ulong"/> representing the welcome channel ID for this guild.
    /// </returns>
    ulong WelcomeChannelId { get; }
    /// <summary>
    ///     Determines if this guild is currently connected and ready to be used.
    /// </summary>
    /// <remarks>
    ///     <note>
    ///         This property only applies to a WebSocket-based client.
    ///     </note>
    ///     This boolean is used to determine if the guild is currently connected to the WebSocket and is ready to be used/accessed.
    /// </remarks>
    /// <returns>
    ///     <see langword="true"/> if this guild is currently connected and ready to be used; otherwise <see langword="false"/>.
    /// </returns>
    bool Available { get; }
    /// <summary>
    ///     Gets the <see cref="IAudioClient"/> currently associated with this guild.
    /// </summary>
    /// <returns>
    ///     An <see cref="IAudioClient"/> currently associated with this guild.
    /// </returns>
    IAudioClient AudioClient { get; }
    /// <summary>
    ///     Gets the built-in role containing all users in this guild.
    /// </summary>
    /// <returns>
    ///     A role object that represents an <c>@everyone</c> role in this guild.
    /// </returns>
    IRole EveryoneRole { get; }
    /// <summary>
    ///     Gets the number of boost subscribers of this guild.
    /// </summary>
    /// <returns>
    ///     The number of boost subscribers of this guild.
    /// </returns>
    int BoostNumber { get; }
    /// <summary>
    ///     Gets the number of boost subscribers who activates buffer of this guild.
    /// </summary>
    /// <returns>
    ///     The number of boost subscribers who activates buffer of this guild.
    /// </returns>
    int BufferBoostNumber { get; }
    /// <summary>
    ///     Gets the level of guild boosting in this guild.
    /// </summary>
    /// <returns>
    ///     The level of guild boosting in this guild.
    /// </returns>
    BoostLevel BoostLevel { get; }
    /// <summary>
    ///     Gets a collection of all custom emotes for this guild.
    /// </summary>
    /// <returns>
    ///     A read-only collection of all custom emotes for this guild.
    /// </returns>
    IReadOnlyCollection<GuildEmote> Emotes { get; }
    /// <summary>
    ///     Gets a collection of all roles in this guild.
    /// </summary>
    /// <returns>
    ///     A read-only collection of roles found within this guild.
    /// </returns>
    IReadOnlyCollection<IRole> Roles { get; }
    /// <summary>
    ///     Gets the recommendation information for this guild.
    /// </summary>
    /// <returns>
    ///     A recommendation object that represents the recommendation information for this guild;
    ///     <see langword="null"/> if the guild does not have a recommendation.
    /// </returns>
    IRecommendInfo RecommendInfo { get; }
    
    #endregion

    #region Guilds

    /// <summary>
    ///     Leaves this guild.
    /// </summary>
    /// <remarks>
    ///     This method will make the currently logged-in user leave the guild.
    ///     <note>
    ///         If the user is the owner of this guild, use <see cref="IDeletable.DeleteAsync"/> instead.
    ///     </note>
    /// </remarks>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous leave operation.
    /// </returns>
    Task LeaveAsync(RequestOptions options = null);

    #endregion

    #region Guild Bans

    /// <summary>
    ///     Gets a collection of all users banned in this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    ///     ban objects that this guild currently possesses, with each object containing the user banned and reason
    ///     behind the ban.
    /// </returns>
    Task<IReadOnlyCollection<IBan>> GetBansAsync(RequestOptions options = null);
    /// <summary>
    ///     Gets a ban object for a banned user.
    /// </summary>
    /// <param name="user">The banned user.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a ban object, which
    ///     contains the user information and the reason for the ban; <see langword="null" /> if the ban entry cannot be found.
    /// </returns>
    Task<IBan> GetBanAsync(IUser user, RequestOptions options = null);
    /// <summary>
    ///     Gets a ban object for a banned user.
    /// </summary>
    /// <param name="userId">The snowflake identifier for the banned user.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a ban object, which
    ///     contains the user information and the reason for the ban; <see langword="null" /> if the ban entry cannot be found.
    /// </returns>
    Task<IBan> GetBanAsync(ulong userId, RequestOptions options = null);
    /// <summary>
    ///     Bans the user from this guild and optionally prunes their recent messages.
    /// </summary>
    /// <param name="user">The user to ban.</param>
    /// <param name="pruneDays">The number of days to remove messages from this user for, and this number must be between [0, 7].</param>
    /// <param name="reason">The reason of the ban to be written in the audit log.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <exception cref="ArgumentException"><paramref name="pruneDays"/> is not between 0 to 7.</exception>
    /// <returns>
    ///     A task that represents the asynchronous add operation for the ban.
    /// </returns>
    Task AddBanAsync(IUser user, int pruneDays = 0, string reason = null, RequestOptions options = null);
    /// <summary>
    ///     Bans the user from this guild and optionally prunes their recent messages.
    /// </summary>
    /// <param name="userId">The snowflake ID of the user to ban.</param>
    /// <param name="pruneDays">The number of days to remove messages from this user for, and this number must be between [0, 7].</param>
    /// <param name="reason">The reason of the ban to be written in the audit log.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <exception cref="ArgumentException"><paramref name="pruneDays"/> is not between 0 to 7.</exception>
    /// <returns>
    ///     A task that represents the asynchronous add operation for the ban.
    /// </returns>
    Task AddBanAsync(ulong userId, int pruneDays = 0, string reason = null, RequestOptions options = null);
    /// <summary>
    ///     Unbans the user if they are currently banned.
    /// </summary>
    /// <param name="user">The user to be unbanned.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous removal operation for the ban.
    /// </returns>
    Task RemoveBanAsync(IUser user, RequestOptions options = null);
    /// <summary>
    ///     Unbans the user if they are currently banned.
    /// </summary>
    /// <param name="userId">The snowflake identifier of the user to be unbanned.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous removal operation for the ban.
    /// </returns>
    Task RemoveBanAsync(ulong userId, RequestOptions options = null);

    #endregion
    
    #region Channels
    
    /// <summary>
    ///     Gets a collection of all channels in this guild.
    /// </summary>
    /// <param name="mode">The <see cref="CacheMode" /> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    ///     generic channels found within this guild.
    /// </returns>
    Task<IReadOnlyCollection<IGuildChannel>> GetChannelsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);
    /// <summary>
    ///     Gets a channel in this guild.
    /// </summary>
    /// <param name="id">The identifier for the channel.</param>
    /// <param name="mode">The <see cref="CacheMode"/> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the generic channel
    ///     associated with the specified <paramref name="id"/>; <see langword="null" /> if none is found.
    /// </returns>
    Task<IGuildChannel> GetChannelAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);
    /// <summary>
    ///     Gets a collection of all text channels in this guild.
    /// </summary>
    /// <param name="mode">The <see cref="CacheMode"/> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    ///     message channels found within this guild.
    /// </returns>
    Task<IReadOnlyCollection<ITextChannel>> GetTextChannelsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);
    /// <summary>
    ///     Gets a text channel in this guild.
    /// </summary>
    /// <param name="id">The snowflake identifier for the text channel.</param>
    /// <param name="mode">The <see cref="CacheMode"/> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the text channel
    ///     associated with the specified <paramref name="id"/>; <see langword="null" /> if none is found.
    /// </returns>
    Task<ITextChannel> GetTextChannelAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);
    /// <summary>
    ///     Gets a collection of all voice channels in this guild.
    /// </summary>
    /// <param name="mode">The <see cref="CacheMode"/> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    ///     voice channels found within this guild.
    /// </returns>
    Task<IReadOnlyCollection<IVoiceChannel>> GetVoiceChannelsAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);
    /// <summary>
    ///     Gets a voice channel in this guild.
    /// </summary>
    /// <param name="id">The snowflake identifier for the voice channel.</param>
    /// <param name="mode">The <see cref="CacheMode"/> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the voice channel associated
    ///     with the specified <paramref name="id"/>; <see langword="null" /> if none is found.
    /// </returns>
    Task<IVoiceChannel> GetVoiceChannelAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);

    /// <summary>
    ///     Gets a collection of all category channels in this guild.
    /// </summary>
    /// <param name="mode">The <see cref="CacheMode" /> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    ///     category channels found within this guild.
    /// </returns>
    Task<IReadOnlyCollection<ICategoryChannel>> GetCategoryChannelsAsync(CacheMode mode = CacheMode.AllowDownload,
        RequestOptions options = null);
    
    /// <summary>
    ///     Gets the default text channel for this guild.
    /// </summary>
    /// <param name="mode">The <see cref="CacheMode" /> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the default text channel for this guild;
    ///     <see langword="null" /> if none is found.
    /// </returns>
    Task<ITextChannel> GetDefaultChannelAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);
    
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
    Task<ITextChannel> CreateTextChannelAsync(string name, Action<CreateTextChannelProperties> func = null, RequestOptions options = null);
    /// <summary>
    ///     Creates a new voice channel in this guild.
    /// </summary>
    /// <param name="name">The new name for the voice channel.</param>
    /// <param name="func">The delegate containing the properties to be applied to the channel upon its creation.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous creation operation. The task result contains the newly created
    ///     voice channel.
    /// </returns>
    Task<IVoiceChannel> CreateVoiceChannelAsync(string name, Action<CreateVoiceChannelProperties> func = null, RequestOptions options = null);
    /// <summary>
    ///     Creates a new channel category in this guild.
    /// </summary>
    /// <param name="name">The new name for the category.</param>
    /// <param name="func">The delegate containing the properties to be applied to the channel upon its creation.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous creation operation. The task result contains the newly created
    ///     category channel.
    /// </returns>
    Task<ICategoryChannel> CreateCategoryChannelAsync(string name, Action<CreateGuildChannelProperties> func = null, RequestOptions options = null);
    
    #endregion

    #region Invites

    /// <summary>
    ///     Gets a collection of all invites in this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection of
    ///     invite, each representing information for an invite found within this guild.
    /// </returns>
    Task<IReadOnlyCollection<IInvite>> GetInvitesAsync(RequestOptions options = null);
    
    /// <summary>
    ///     Creates a new invite to this channel.
    /// </summary>
    /// <param name="maxAge">The time until the invite expires. Set to <c>InviteMaxAge.NeverExpires</c> to never expire.</param>
    /// <param name="maxUses">The max amount of times this invite may be used. Set to <c>InviteMaxUses.Unlimited</c> to have unlimited uses.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous invite creation operation. The task result contains an invite
    ///     metadata object containing information for the created invite.
    /// </returns>
    Task<IInvite> CreateInviteAsync(InviteMaxAge maxAge = InviteMaxAge.OneWeek, InviteMaxUses maxUses = InviteMaxUses.Unlimited, RequestOptions options = null);
    
    /// <summary>
    ///     Creates a new invite to this channel.
    /// </summary>
    /// <param name="maxAge">The time (in seconds) until the invite expires. Set to <c>null</c> to never expire.</param>
    /// <param name="maxUses">The max amount of times this invite may be used. Set to <c>null</c> to have unlimited uses.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous invite creation operation. The task result contains an invite
    ///     metadata object containing information for the created invite.
    /// </returns>
    Task<IInvite> CreateInviteAsync(int? maxAge = 604800, int? maxUses = null, RequestOptions options = null);

    #endregion

    #region Roles

    /// <summary>
    ///     Gets a role in this guild.
    /// </summary>
    /// <param name="id">The identifier for the role.</param>
    /// <returns>
    ///     A role that is associated with the specified <paramref name="id"/>; <see langword="null" /> if none is found.
    /// </returns>
    IRole GetRole(uint id);
    /// <summary>
    ///     Creates a new role with the provided name.
    /// </summary>
    /// <param name="name">The new name for the role.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous creation operation. The task result contains the newly created role.
    /// </returns>
    Task<IRole> CreateRoleAsync(string name, RequestOptions options = null);
    
    #endregion

    #region Users
    
    /// <summary>
    ///     Gets a collection of all users in this guild.
    /// </summary>
    /// <remarks>
    ///     This method retrieves all users found within this guild.
    /// </remarks>
    /// <param name="mode">The <see cref="CacheMode" /> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a collection of guild
    ///     users found within this guild.
    /// </returns>
    Task<IReadOnlyCollection<IGuildUser>> GetUsersAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);
    /// <summary>
    ///     Gets a user from this guild.
    /// </summary>
    /// <remarks>
    ///     This method retrieves a user found within this guild.
    ///     <note>
    ///         This may return <see langword="null" /> in the WebSocket implementation due to incomplete user collection in
    ///         large guilds.
    ///     </note>
    /// </remarks>
    /// <param name="id">The identifier of the user.</param>
    /// <param name="mode">The <see cref="CacheMode"/> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the guild user
    ///     associated with the specified <paramref name="id"/>; <see langword="null" /> if none is found.
    /// </returns>
    Task<IGuildUser> GetUserAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);
    /// <summary>
    ///     Gets the current user for this guild.
    /// </summary>
    /// <param name="mode">The <see cref="CacheMode"/> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the currently logged-in
    ///     user within this guild.
    /// </returns>
    Task<IGuildUser> GetCurrentUserAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);
    /// <summary>
    ///     Gets the owner of this guild.
    /// </summary>
    /// <param name="mode">The <see cref="CacheMode"/> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the owner of this guild.
    /// </returns>
    Task<IGuildUser> GetOwnerAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);
    /// <summary>
    ///     Downloads all users for this guild if the current list is incomplete.
    /// </summary>
    /// <remarks>
    ///     This method downloads all users found within this guild through the Gateway and caches them.
    /// </remarks>
    /// <returns>
    ///     A task that represents the asynchronous download operation.
    /// </returns>
    Task DownloadUsersAsync();

    /// <summary>
    ///     Gets a collection of users in this guild that the name or nickname contains the
    ///     provided string at <paramref name="func"/>.
    /// </summary>
    /// <remarks>
    ///     The <paramref name="limit"/> can not be higher than <see cref="KaiHeiLaConfig.MaxUsersPerBatch"/>.
    /// </remarks>
    /// <param name="func">A delegate containing the properties to search users with.</param>
    /// <param name="limit">The maximum number of users to be gotten.</param>
    /// <param name="mode">The <see cref="CacheMode" /> that determines whether the object should be fetched from cache.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a collection of guild
    ///     users that matches the properties with the provided <see cref="Action{SearchGuildMemberProperties}"/> at <paramref name="func"/>.
    /// </returns>
    IAsyncEnumerable<IReadOnlyCollection<IGuildUser>> SearchUsersAsync(Action<SearchGuildMemberProperties> func, int limit = KaiHeiLaConfig.MaxUsersPerBatch, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null);
    
    #endregion

    #region Emotes

    /// <summary>
    ///     Gets a collection of emotes from this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains a read-only collection
    ///     of emotes found within the guild.
    /// </returns>
    Task<IReadOnlyCollection<GuildEmote>> GetEmotesAsync(RequestOptions options = null);
    /// <summary>
    ///     Gets a specific emote from this guild.
    /// </summary>
    /// <param name="id">The snowflake identifier for the guild emote.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the emote found with the
    ///     specified <paramref name="id"/>; <see langword="null" /> if none is found.
    /// </returns>
    Task<GuildEmote> GetEmoteAsync(string id, RequestOptions options = null);
    /// <summary>
    ///     Creates a new <see cref="GuildEmote"/> in this guild.
    /// </summary>
    /// <param name="name">The name of the guild emote.</param>
    /// <param name="image">The image of the new emote.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous creation operation. The task result contains the created emote.
    /// </returns>
    Task<GuildEmote> CreateEmoteAsync(string name, Image image, RequestOptions options = null);

    /// <summary>
    ///     Modifies an existing <see cref="GuildEmote"/> in this guild.
    /// </summary>
    /// <param name="emote">The emote to be modified.</param>
    /// <param name="func">The delegate containing the properties to modify the emote with.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous modification operation. The task result contains the modified
    ///     emote.
    /// </returns>
    Task ModifyEmoteNameAsync(GuildEmote emote, Action<string> func, RequestOptions options = null);

    /// <summary>
    ///     Deletes an existing <see cref="GuildEmote"/> from this guild.
    /// </summary>
    /// <param name="emote">The emote to delete.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous removal operation.
    /// </returns>
    Task DeleteEmoteAsync(GuildEmote emote, RequestOptions options = null);

    #endregion

    #region Voice

    /// <summary>
    /// Moves the user to the voice channel.
    /// </summary>
    /// <param name="users">The users to move.</param>
    /// <param name="targetChannel">the channel where the user gets moved to.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>A task that represents the asynchronous operation for moving a user.</returns>
    Task MoveUsersAsync(IEnumerable<IGuildUser> users, IVoiceChannel targetChannel, RequestOptions options = null);
    
    /// <summary>
    ///     Gets the users who are muted or deafened in this guild.
    /// </summary>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains
    ///     the collection of muted or deafened users in this guild.
    /// </returns>
    Task<(IReadOnlyCollection<Cacheable<IUser, ulong>> Muted, IReadOnlyCollection<Cacheable<IUser, ulong>> Deafened)>
        GetMutedDeafenedUsersAsync(RequestOptions options = null);

    #endregion

    #region Badges

    /// <summary>
    ///     Gets a badge which is associated with this guild.
    /// </summary>
    /// <param name="style">The <see cref="BadgeStyle"/> that specifies the style of the badge.</param>
    /// <param name="options">The options to be used when sending the request.</param>
    /// <returns>
    ///     A task that represents the asynchronous get operation. The task result contains the stream of the badge
    ///     associated with this guild.
    /// </returns>
    Task<Stream> GetBadgeAsync(BadgeStyle style = BadgeStyle.GuildName, RequestOptions options = null);

    #endregion
}