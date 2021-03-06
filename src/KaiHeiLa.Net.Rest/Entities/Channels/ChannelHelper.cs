using System.Collections.Immutable;
using KaiHeiLa.API;
using KaiHeiLa.API.Rest;
using Model = KaiHeiLa.API.Channel;

namespace KaiHeiLa.Rest;

internal static class ChannelHelper
{
    #region General

    public static async Task DeleteGuildChannelAsync(IGuildChannel channel, BaseKaiHeiLaClient client,
        RequestOptions options)
    {
        await client.ApiClient.DeleteGuildChannelAsync(channel.Id, options).ConfigureAwait(false);
    }

    public static async Task<Model> ModifyAsync(IGuildChannel channel, BaseKaiHeiLaClient client,
        Action<ModifyGuildChannelProperties> func,
        RequestOptions options)
    {
        var args = new ModifyGuildChannelProperties();
        func(args);
        var apiArgs = new API.Rest.ModifyGuildChannelParams
        {
            Name = args.Name,
            Position = args.Position,
            CategoryId = args.CategoryId,
        };
        return await client.ApiClient.ModifyGuildChannelAsync(channel.Id, apiArgs, options).ConfigureAwait(false);
    }

    public static async Task<Model> ModifyAsync(ITextChannel channel, BaseKaiHeiLaClient client,
        Action<ModifyTextChannelProperties> func,
        RequestOptions options)
    {
        var args = new ModifyTextChannelProperties();
        func(args);
        var apiArgs = new API.Rest.ModifyTextChannelParams
        {
            Name = args.Name,
            Position = args.Position,
            CategoryId = args.CategoryId,
            Topic = args.Topic,
            SlowModeInterval = args.SlowModeInterval * 1000,
        };
        return await client.ApiClient.ModifyGuildChannelAsync(channel.Id, apiArgs, options).ConfigureAwait(false);
    }

    public static async Task<Model> ModifyAsync(IVoiceChannel channel, BaseKaiHeiLaClient client,
        Action<ModifyVoiceChannelProperties> func,
        RequestOptions options)
    {
        var args = new ModifyVoiceChannelProperties();
        func(args);
        var apiArgs = new API.Rest.ModifyVoiceChannelParams
        {
            Name = args.Name,
            Position = args.Position,
            CategoryId = args.CategoryId,
            VoiceQuality = args.VoiceQuality,
            UserLimit = args.UserLimit
        };
        return await client.ApiClient.ModifyGuildChannelAsync(channel.Id, apiArgs, options).ConfigureAwait(false);
    }

    public static async Task DeleteDMChannelAsync(IDMChannel channel, BaseKaiHeiLaClient client,
        RequestOptions options)
    {
        await client.ApiClient.DeleteUserChatAsync(channel.ChatCode, options).ConfigureAwait(false);
    }

    public static async Task UpdateAsync(RestChannel channel, BaseKaiHeiLaClient client, RequestOptions options)
    {
        var model = await client.ApiClient.GetGuildChannelAsync(channel.Id, options).ConfigureAwait(false);
        channel.Update(model);
    }

    #endregion

    #region Messages

    public static async Task<RestMessage> GetMessageAsync(IMessageChannel channel, BaseKaiHeiLaClient client,
        Guid id, RequestOptions options)
    {
        var guildId = (channel as IGuildChannel)?.GuildId;
        var guild = guildId != null
            ? await (client as IKaiHeiLaClient).GetGuildAsync(guildId.Value, CacheMode.CacheOnly).ConfigureAwait(false)
            : null;
        var model = await client.ApiClient.GetMessageAsync(id, options).ConfigureAwait(false);
        if (model == null)
            return null;
        var author = MessageHelper.GetAuthor(client, guild, model.Author);
        return RestMessage.Create(client, channel, author, model);
    }

    public static IAsyncEnumerable<IReadOnlyCollection<RestMessage>> GetMessagesAsync(IMessageChannel channel,
        BaseKaiHeiLaClient client,
        Guid? referenceMessageId, Direction dir, int limit, bool includeReferenceMessage, RequestOptions options)
    {
        var guildId = (channel as IGuildChannel)?.GuildId;
        var guild = guildId != null
            ? (client as IKaiHeiLaClient).GetGuildAsync(guildId.Value, CacheMode.CacheOnly).GetAwaiter().GetResult()
            : null;

        if (dir == Direction
                .Around) //  && limit > KaiHeiLaConfig.MaxMessagesPerBatch // Around mode returns error messages from endpoint
        {
            int around = limit / 2;
            if (referenceMessageId.HasValue)
            {
                var messages = GetMessagesAsync(channel, client, referenceMessageId, Direction.Before, around,
                    includeReferenceMessage, options);
                messages = messages.Concat(GetMessagesAsync(channel, client, referenceMessageId, Direction.After,
                    around, false, options));
                return messages;
            }
            else // Shouldn't happen since there's no public overload for Guid? and Direction
                return GetMessagesAsync(channel, client, null, Direction.Before, around + 1, includeReferenceMessage,
                    options);
        }

        return new PagedAsyncEnumerable<RestMessage>(
            KaiHeiLaConfig.MaxMessagesPerBatch,
            async (info, ct) =>
            {
                var models = await client.ApiClient.QueryMessagesAsync(channel.Id, referenceMessageId: info.Position,
                    dir: dir, count: limit, options: options).ConfigureAwait(false);
                var builder = ImmutableArray.CreateBuilder<RestMessage>();
                // Insert the reference message before query results
                if (includeReferenceMessage && info.Position.HasValue && dir == Direction.After)
                {
                    Message currentMessage = await client.ApiClient.GetMessageAsync(info.Position.Value, options);
                    var currentMessageAuthor = MessageHelper.GetAuthor(client, guild, currentMessage.Author);
                    builder.Add(RestMessage.Create(client, channel, currentMessageAuthor, currentMessage));
                }

                foreach (var model in models)
                {
                    var author = MessageHelper.GetAuthor(client, guild, model.Author);
                    builder.Add(RestMessage.Create(client, channel, author, model));
                }

                // Append the reference message after query results
                if (includeReferenceMessage && info.Position.HasValue && dir == Direction.Before)
                {
                    Message currentMessage = await client.ApiClient.GetMessageAsync(info.Position.Value, options);
                    var currentMessageAuthor = MessageHelper.GetAuthor(client, guild, currentMessage.Author);
                    builder.Add(RestMessage.Create(client, channel, currentMessageAuthor, currentMessage));
                }

                return builder.ToImmutable();
            },
            nextPage: (info, lastPage) =>
            {
                if (lastPage.Count != KaiHeiLaConfig.MaxMessagesPerBatch)
                    return false;
                if (dir == Direction.Before)
                    info.Position = lastPage.MinBy(x => x.Timestamp)?.Id;
                else
                    info.Position = lastPage.MaxBy(x => x.Timestamp)?.Id;
                return true;
            },
            start: referenceMessageId,
            count: limit
        );
    }

    public static async Task<IReadOnlyCollection<RestMessage>> GetPinnedMessagesAsync(IMessageChannel channel,
        BaseKaiHeiLaClient client,
        RequestOptions options)
    {
        var guildId = (channel as IGuildChannel)?.GuildId;
        var guild = guildId != null
            ? await (client as IKaiHeiLaClient).GetGuildAsync(guildId.Value, CacheMode.CacheOnly).ConfigureAwait(false)
            : null;
        var models = await client.ApiClient.QueryMessagesAsync(channel.Id, queryPin: true, options: options)
            .ConfigureAwait(false);
        var builder = ImmutableArray.CreateBuilder<RestMessage>();
        foreach (var model in models)
        {
            var author = MessageHelper.GetAuthor(client, guild, model.Author);
            RestMessage message = RestMessage.Create(client, channel, author, model);
            if (message is RestUserMessage userMessage)
                userMessage.IsPinned = true;
            builder.Add(message);
        }

        return builder.ToImmutable();
    }

    public static async Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> SendMessageAsync(
        IMessageChannel channel,
        BaseKaiHeiLaClient client, MessageType messageType, string content, RequestOptions options, IQuote quote = null,
        IUser ephemeralUser = null)
    {
        CreateMessageParams args = new(messageType, channel.Id, content)
        {
            QuotedMessageId = quote?.QuotedMessageId,
            EphemeralUserId = ephemeralUser?.Id
        };
        CreateMessageResponse model = await client.ApiClient.CreateMessageAsync(args, options).ConfigureAwait(false);
        return (model.MessageId, model.MessageTimestamp);
    }

    public static Task DeleteMessageAsync(IMessageChannel channel, Guid messageId, BaseKaiHeiLaClient client,
        RequestOptions options)
        => MessageHelper.DeleteAsync(messageId, client, options);

    public static Task DeleteDirectMessageAsync(IMessageChannel channel, Guid messageId, BaseKaiHeiLaClient client,
        RequestOptions options)
        => MessageHelper.DeleteDirectAsync(messageId, client, options);


    public static async Task ModifyMessageAsync(IMessageChannel channel, Guid messageId, Action<MessageProperties> func,
        BaseKaiHeiLaClient client, RequestOptions options)
        => await MessageHelper.ModifyAsync(messageId, client, func, options).ConfigureAwait(false);

    #endregion

    #region Direct Messages

    public static async Task<RestMessage> GetDirectMessageAsync(IDMChannel channel, BaseKaiHeiLaClient client,
        Guid id, RequestOptions options)
    {
        var model = await client.ApiClient.GetDirectMessageAsync(id, chatCode: channel.Id, options: options)
            .ConfigureAwait(false);
        if (model == null)
            return null;
        // User userModel = await client.ApiClient.GetUserAsync(model.AuthorId, options);
        // var author = MessageHelper.GetAuthor(client, null, userModel);
        return RestMessage.Create(client, channel, channel.Recipient, model);
    }

    public static IAsyncEnumerable<IReadOnlyCollection<RestMessage>> GetDirectMessagesAsync(IDMChannel channel,
        BaseKaiHeiLaClient client,
        Guid? referenceMessageId, Direction dir, int limit, bool includeReferenceMessage, RequestOptions options)
    {
        if (dir == Direction
                .Around) //  && limit > KaiHeiLaConfig.MaxMessagesPerBatch // Around mode returns error messages from endpoint
        {
            int around = limit / 2;
            if (referenceMessageId.HasValue)
            {
                var messages = GetDirectMessagesAsync(channel, client, referenceMessageId, Direction.Before, around,
                    includeReferenceMessage, options);
                messages = messages.Concat(GetDirectMessagesAsync(channel, client, referenceMessageId, Direction.After,
                    around, false, options));
                return messages;
            }
            else // Shouldn't happen since there's no public overload for Guid? and Direction
                return GetDirectMessagesAsync(channel, client, null, Direction.Before, around + 1,
                    includeReferenceMessage, options);
        }

        return new PagedAsyncEnumerable<RestMessage>(
            KaiHeiLaConfig.MaxMessagesPerBatch,
            async (info, ct) =>
            {
                var models = await client.ApiClient.QueryDirectMessagesAsync(channel.ChatCode,
                    referenceMessageId: info.Position, dir: dir, count: limit, options: options).ConfigureAwait(false);
                var builder = ImmutableArray.CreateBuilder<RestMessage>();
                // Insert the reference message before query results
                if (includeReferenceMessage && info.Position.HasValue && dir == Direction.After)
                {
                    DirectMessage currentMessage = await client.ApiClient.GetDirectMessageAsync(info.Position.Value,
                        chatCode: channel.ChatCode, options: options);
                    builder.Add(RestMessage.Create(client, channel, channel.Recipient, currentMessage));
                }

                foreach (var model in models)
                {
                    builder.Add(RestMessage.Create(client, channel, channel.Recipient, model));
                }

                // Append the reference message after query results
                if (includeReferenceMessage && info.Position.HasValue && dir == Direction.Before)
                {
                    DirectMessage currentMessage = await client.ApiClient.GetDirectMessageAsync(info.Position.Value,
                        chatCode: channel.ChatCode, options: options);
                    builder.Add(RestMessage.Create(client, channel, channel.Recipient, currentMessage));
                }

                return builder.ToImmutable();
            },
            nextPage: (info, lastPage) =>
            {
                if (lastPage.Count != KaiHeiLaConfig.MaxMessagesPerBatch)
                    return false;
                if (dir == Direction.Before)
                    info.Position = lastPage.MinBy(x => x.Timestamp)?.Id;
                else
                    info.Position = lastPage.MaxBy(x => x.Timestamp)?.Id;
                return true;
            },
            start: referenceMessageId,
            count: limit
        );
    }

    public static async Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> SendDirectMessageAsync(
        IDMChannel channel,
        BaseKaiHeiLaClient client, MessageType messageType, string content, RequestOptions options, IQuote quote = null)
    {
        CreateDirectMessageParams args = new(messageType, channel.Recipient.Id, content)
        {
            QuotedMessageId = quote?.QuotedMessageId,
        };
        CreateDirectMessageResponse model =
            await client.ApiClient.CreateDirectMessageAsync(args, options).ConfigureAwait(false);
        return (model.MessageId, model.MessageTimestamp);
    }

    public static async Task ModifyDirectMessageAsync(IDMChannel channel, Guid messageId,
        Action<MessageProperties> func,
        BaseKaiHeiLaClient client, RequestOptions options)
        => await MessageHelper.ModifyDirectAsync(messageId, client, func, options).ConfigureAwait(false);

    #endregion

    #region Permission Overwrites

    public static async Task<UserPermissionOverwrite> AddPermissionOverwriteAsync(IGuildChannel channel,
        BaseKaiHeiLaClient client,
        IUser user, RequestOptions options)
    {
        var args = new CreateOrRemoveChannelPermissionOverwriteParams(channel.Id, PermissionOverwriteTargetType.User,
            user.Id);
        var resp = await client.ApiClient.CreateChannelPermissionOverwriteAsync(args, options).ConfigureAwait(false);
        return new UserPermissionOverwrite(user, new OverwritePermissions(resp.Allow, resp.Deny));
    }

    public static async Task<RolePermissionOverwrite> AddPermissionOverwriteAsync(IGuildChannel channel,
        BaseKaiHeiLaClient client,
        IRole role, RequestOptions options)
    {
        var args = new CreateOrRemoveChannelPermissionOverwriteParams(channel.Id, PermissionOverwriteTargetType.Role,
            role.Id);
        var resp = await client.ApiClient.CreateChannelPermissionOverwriteAsync(args, options).ConfigureAwait(false);
        return new RolePermissionOverwrite(role.Id, new OverwritePermissions(resp.Allow, resp.Deny));
    }

    public static async Task RemovePermissionOverwriteAsync(IGuildChannel channel, BaseKaiHeiLaClient client,
        IUser user, RequestOptions options)
    {
        var args = new CreateOrRemoveChannelPermissionOverwriteParams(channel.Id, PermissionOverwriteTargetType.Role,
            user.Id);
        await client.ApiClient.RemoveChannelPermissionOverwriteAsync(args, options).ConfigureAwait(false);
    }

    public static async Task RemovePermissionOverwriteAsync(IGuildChannel channel, BaseKaiHeiLaClient client,
        IRole role, RequestOptions options)
    {
        var args = new CreateOrRemoveChannelPermissionOverwriteParams(channel.Id, PermissionOverwriteTargetType.Role,
            role.Id);
        await client.ApiClient.RemoveChannelPermissionOverwriteAsync(args, options).ConfigureAwait(false);
    }

    public static async Task<UserPermissionOverwrite> ModifyPermissionOverwriteAsync(IGuildChannel channel,
        BaseKaiHeiLaClient client,
        IGuildUser user, Func<OverwritePermissions, OverwritePermissions> func, RequestOptions options)
    {
        var perms = channel.UserPermissionOverwrites.SingleOrDefault(x => x.Target.Id == user.Id)?.Permissions;
        if (!perms.HasValue) return null;
        perms = func(perms.Value);
        var args = new ModifyChannelPermissionOverwriteParams(channel.Id, PermissionOverwriteTargetType.User, user.Id,
            perms.Value.AllowValue, perms.Value.DenyValue);
        var resp = await client.ApiClient.ModifyChannelPermissionOverwriteAsync(args, options).ConfigureAwait(false);
        return new UserPermissionOverwrite(user, new OverwritePermissions(resp.Allow, resp.Deny));
    }

    public static async Task<RolePermissionOverwrite> ModifyPermissionOverwriteAsync(IGuildChannel channel,
        BaseKaiHeiLaClient client,
        IRole role, Func<OverwritePermissions, OverwritePermissions> func, RequestOptions options)
    {
        var perms = channel.RolePermissionOverwrites.SingleOrDefault(x => x.Target == role.Id)?.Permissions;
        if (!perms.HasValue) return null;
        perms = func(perms.Value);
        var args = new ModifyChannelPermissionOverwriteParams(channel.Id, PermissionOverwriteTargetType.Role, role.Id,
            perms.Value.AllowValue, perms.Value.DenyValue);
        var resp = await client.ApiClient.ModifyChannelPermissionOverwriteAsync(args, options).ConfigureAwait(false);
        return new RolePermissionOverwrite(role.Id, new OverwritePermissions(resp.Allow, resp.Deny));
    }

    #endregion

    #region Invites

    public static async Task<IReadOnlyCollection<RestInvite>> GetInvitesAsync(IGuildChannel channel,
        BaseKaiHeiLaClient client,
        RequestOptions options)
    {
        var models = await client.ApiClient.GetGuildInvitesAsync(channel.GuildId, channel.Id, options: options)
            .FlattenAsync().ConfigureAwait(false);
        return models.Select(x => RestInvite.Create(client, channel.Guild, channel, x)).ToImmutableArray();
    }

    /// <exception cref="ArgumentException">
    /// <paramref name="channel.Id"/> may not be equal to zero.
    /// <paramref name="maxAge"/> and <paramref name="maxUses"/> must be greater than zero.
    /// <paramref name="maxAge"/> must be lesser than 604800.
    /// </exception>
    public static async Task<RestInvite> CreateInviteAsync(IGuildChannel channel, BaseKaiHeiLaClient client,
        int? maxAge, int? maxUses, RequestOptions options)
    {
        var args = new CreateGuildInviteParams()
        {
            GuildId = channel.GuildId,
            ChannelId = channel.Id,
            MaxAge = (InviteMaxAge) (maxAge ?? 0),
            MaxUses = (InviteMaxUses) (maxUses ?? -1),
        };
        var model = await client.ApiClient.CreateGuildInviteAsync(args, options).ConfigureAwait(false);
        var invites = await client.ApiClient.GetGuildInvitesAsync(channel.GuildId, channel.Id, options: options)
            .FlattenAsync().ConfigureAwait(false);
        return RestInvite.Create(client, channel.Guild, channel, invites.SingleOrDefault(x => x.Url == model.Url));
    }

    public static Task<RestInvite> CreateInviteAsync(IGuildChannel channel, BaseKaiHeiLaClient client,
        InviteMaxAge maxAge, InviteMaxUses maxUses, RequestOptions options)
    {
        return CreateInviteAsync(channel, client, (int?) maxAge, (int?) maxUses, options);
    }

    #endregion

    #region Categories

    public static async Task<ICategoryChannel> GetCategoryAsync(INestedChannel channel, BaseKaiHeiLaClient client,
        RequestOptions options)
    {
        // if no category id specified, return null
        if (!channel.CategoryId.HasValue)
            return null;
        // CategoryId will contain a value here
        var model = await client.ApiClient.GetGuildChannelAsync(channel.CategoryId.Value, options)
            .ConfigureAwait(false);
        return RestCategoryChannel.Create(client, model) as ICategoryChannel;
    }

    #endregion

    #region Users

    /// <exception cref="InvalidOperationException">Resolving permissions requires the parent guild to be downloaded.</exception>
    public static async Task<RestGuildUser> GetUserAsync(IGuildChannel channel, IGuild guild, BaseKaiHeiLaClient client,
        ulong id, RequestOptions options)
    {
        var model = await client.ApiClient.GetGuildMemberAsync(channel.GuildId, id, options).ConfigureAwait(false);
        if (model == null)
            return null;
        var user = RestGuildUser.Create(client, guild, model);
        if (!user.GetPermissions(channel).ViewChannel)
            return null;

        return user;
    }

    /// <exception cref="InvalidOperationException">Resolving permissions requires the parent guild to be downloaded.</exception>
    public static IAsyncEnumerable<IReadOnlyCollection<RestGuildUser>> GetUsersAsync(IGuildChannel channel,
        IGuild guild, BaseKaiHeiLaClient client,
        int limit, int fromPage, RequestOptions options)
    {
        return client.ApiClient.GetGuildMembersAsync(guild.Id, limit: limit, fromPage: fromPage, options: options)
            .Select(x => x.Select(y => RestGuildUser.Create(client, guild, y))
                .Where(y => y.GetPermissions(channel).ViewChannel)
                .ToImmutableArray() as IReadOnlyCollection<RestGuildUser>);
    }

    #endregion
}