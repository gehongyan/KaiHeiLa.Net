using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using KaiHeiLa.API;
using KaiHeiLa.Net;
using KaiHeiLa.Net.Queue;
using KaiHeiLa.Net.Rest;
using System.Linq;
using System.Web;
using KaiHeiLa.API.Rest;
using KaiHeiLa.Net.Converters;

namespace KaiHeiLa.API;

internal class KaiHeiLaRestApiClient : IDisposable
{
    #region KaiHeiLaRestApiClient
    
    private static readonly ConcurrentDictionary<string, Func<BucketIds, BucketId>> _bucketIdGenerators = new ConcurrentDictionary<string, Func<BucketIds, BucketId>>();

    public event Func<HttpMethod, string, double, Task> SentRequest { add { _sentRequestEvent.Add(value); } remove { _sentRequestEvent.Remove(value); } }
    private readonly AsyncEvent<Func<HttpMethod, string, double, Task>> _sentRequestEvent = new AsyncEvent<Func<HttpMethod, string, double, Task>>();

    protected readonly JsonSerializerOptions _serializerOptions;
    protected readonly SemaphoreSlim _stateLock;
    private readonly RestClientProvider _restClientProvider;

    protected bool _isDisposed;
    private CancellationTokenSource _loginCancelToken;

    public RetryMode DefaultRetryMode { get; }
    public string UserAgent { get; }
    
    internal RequestQueue RequestQueue { get; }
    public LoginState LoginState { get; internal set; }
    public TokenType AuthTokenType { get; set; }
    public string AuthToken { get; set; }
    internal IRestClient RestClient { get; private set; }
    internal ulong? CurrentUserId { get; set; }
    internal Func<IRateLimitInfo, Task> DefaultRatelimitCallback { get; set; }
    
    public KaiHeiLaRestApiClient(RestClientProvider restClientProvider, string userAgent, RetryMode defaultRetryMode = RetryMode.AlwaysRetry,
        JsonSerializerOptions serializerOptions = null, Func<IRateLimitInfo, Task> defaultRatelimitCallback = null)
    {
        _restClientProvider = restClientProvider;
        UserAgent = userAgent;
        DefaultRetryMode = defaultRetryMode;
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions {Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
        // SerializerOptions.Converters.Add(new EmbedConverter());
        DefaultRatelimitCallback = defaultRatelimitCallback;
        
        RequestQueue = new RequestQueue();
        _stateLock = new SemaphoreSlim(1, 1);
        SetBaseUrl(KaiHeiLaConfig.APIUrl);
    }

    internal void SetBaseUrl(string baseUrl)
    {
        RestClient?.Dispose();
        RestClient = _restClientProvider(baseUrl);
        RestClient.SetHeader("accept", "*/*");
        RestClient.SetHeader("user-agent", UserAgent);
        RestClient.SetHeader("authorization", GetPrefixedToken(AuthTokenType, AuthToken));
    }
    internal static string GetPrefixedToken(TokenType tokenType, string token)
    {
        return tokenType switch
        {
            TokenType.Bot => $"Bot {token}",
            TokenType.Bearer => $"Bearer {token}",
            _ => throw new ArgumentException(message: "Unknown OAuth token type.", paramName: nameof(tokenType)),
        };
    }
    internal virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
        }
    }
    public void Dispose() => Dispose(true);
    
    public async Task LoginAsync(TokenType tokenType, string token)
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await LoginInternalAsync(tokenType, token).ConfigureAwait(false);
        }
        finally { _stateLock.Release(); }
    }

    private async Task LoginInternalAsync(TokenType tokenType, string token)
    {
        if (LoginState != LoginState.LoggedOut)
            await LogoutInternalAsync().ConfigureAwait(false);
        LoginState = LoginState.LoggingIn;

        try
        {
            _loginCancelToken?.Dispose();
            _loginCancelToken = new CancellationTokenSource();
            
            AuthToken = null;
            await RequestQueue.SetCancelTokenAsync(_loginCancelToken.Token).ConfigureAwait(false);
            RestClient.SetCancelToken(_loginCancelToken.Token);

            AuthTokenType = tokenType;
            AuthToken = token?.TrimEnd();
            RestClient.SetHeader("authorization", GetPrefixedToken(AuthTokenType, AuthToken));

            LoginState = LoginState.LoggedIn;
        }
        catch
        {
            await LogoutInternalAsync().ConfigureAwait(false);
            throw;
        }
    }
    
    public async Task LogoutAsync()
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await LogoutInternalAsync().ConfigureAwait(false);
        }
        finally { _stateLock.Release(); }
    }
    private async Task LogoutInternalAsync()
    {
        //An exception here will lock the client into the unusable LoggingOut state, but that's probably fine since our client is in an undefined state too.
        if (LoginState == LoginState.LoggedOut) return;
        LoginState = LoginState.LoggingOut;

        try { _loginCancelToken?.Cancel(false); }
        catch { }

        await DisconnectInternalAsync(null).ConfigureAwait(false);
        await RequestQueue.ClearAsync().ConfigureAwait(false);

        await RequestQueue.SetCancelTokenAsync(CancellationToken.None).ConfigureAwait(false);
        RestClient.SetCancelToken(CancellationToken.None);

        // CurrentUserId = null;
        LoginState = LoginState.LoggedOut;
    }
    internal virtual Task ConnectInternalAsync() => Task.Delay(0);
    internal virtual Task DisconnectInternalAsync(Exception ex = null) => Task.Delay(0);
    
    #endregion

    #region Core
    
    internal Task SendAsync(HttpMethod method, Expression<Func<string>> endpointExpr, BucketIds ids,
             ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions options = null, [CallerMemberName] string funcName = null)
            => SendAsync(method, GetEndpoint(endpointExpr), GetBucketId(method, ids, endpointExpr, funcName), clientBucket, options);
    public async Task SendAsync(HttpMethod method, string endpoint,
        BucketId bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions options = null)
    {
        options ??= new RequestOptions();
        options.HeaderOnly = true;
        options.BucketId = bucketId;

        var request = new RestRequest(RestClient, method, endpoint, options);
        await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
    }

    internal Task SendJsonAsync(HttpMethod method, Expression<Func<string>> endpointExpr, object payload, BucketIds ids,
         ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions options = null, [CallerMemberName] string funcName = null)
        => SendJsonAsync(method, GetEndpoint(endpointExpr), payload, GetBucketId(method, ids, endpointExpr, funcName), clientBucket, options);
    public async Task SendJsonAsync(HttpMethod method, string endpoint, object payload,
        BucketId bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions options = null)
    {
        options ??= new RequestOptions();
        options.HeaderOnly = true;
        options.BucketId = bucketId;

        string json = payload != null ? SerializeJson(payload) : null;
        var request = new JsonRestRequest(RestClient, method, endpoint, json, options);
        await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
    }

    internal Task SendMultipartAsync(HttpMethod method, Expression<Func<string>> endpointExpr, IReadOnlyDictionary<string, object> multipartArgs, BucketIds ids,
         ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions options = null, [CallerMemberName] string funcName = null)
        => SendMultipartAsync(method, GetEndpoint(endpointExpr), multipartArgs, GetBucketId(method, ids, endpointExpr, funcName), clientBucket, options);
    public async Task SendMultipartAsync(HttpMethod method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs,
        BucketId bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions options = null)
    {
        options ??= new RequestOptions();
        options.HeaderOnly = true;
        options.BucketId = bucketId;

        var request = new MultipartRestRequest(RestClient, method, endpoint, multipartArgs, options);
        await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
    }

    internal async Task<TResponse> SendAsync<TResponse>(HttpMethod method, Expression<Func<string>> endpointExpr, BucketIds ids,
        ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions options = null, [CallerMemberName] string funcName = null, bool bypassDeserialization = false) where TResponse : class
        => await SendAsync<TResponse>(method, GetEndpoint(endpointExpr), GetBucketId(method, ids, endpointExpr, funcName), clientBucket, bypassDeserialization, options);
    internal async Task<TResponse> SendAsync<TResponse, TArg1, TArg2>(HttpMethod method, Expression<Func<TArg1, TArg2, string>> endpointExpr, TArg1 arg1, TArg2 arg2, BucketIds ids,
        ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions options = null, [CallerMemberName] string funcName = null, bool bypassDeserialization = false) where TResponse : class
        => await SendAsync<TResponse>(method, GetEndpoint(endpointExpr, arg1, arg2), GetBucketId(method, ids, endpointExpr, arg1, arg2, funcName), clientBucket, bypassDeserialization, options);
    public async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string endpoint,
        BucketId bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, bool bypassDeserialization = false, RequestOptions options = null) where TResponse : class
    {
        options ??= new RequestOptions();
        options.BucketId = bucketId;
        
        var request = new RestRequest(RestClient, method, endpoint, options);
        Stream response = await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
        return bypassDeserialization ? response as TResponse : DeserializeJson<TResponse>(response);
    }

    internal async Task<TResponse> SendJsonAsync<TResponse>(HttpMethod method, Expression<Func<string>> endpointExpr, object payload, BucketIds ids,
        ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions options = null, [CallerMemberName] string funcName = null, bool bypassDeserialization = false) where TResponse : class
        => await SendJsonAsync<TResponse>(method, GetEndpoint(endpointExpr), payload, GetBucketId(method, ids, endpointExpr, funcName), clientBucket, bypassDeserialization, options);
    public async Task<TResponse> SendJsonAsync<TResponse>(HttpMethod method, string endpoint, object payload,
        BucketId bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, bool bypassDeserialization = false, RequestOptions options = null) where TResponse : class
    {
        options ??= new RequestOptions();
        options.BucketId = bucketId;
        
        string json = payload != null ? SerializeJson(payload) : null;
        
        var request = new JsonRestRequest(RestClient, method, endpoint, json, options);
        Stream response = await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
        return bypassDeserialization ? response as TResponse : DeserializeJson<TResponse>(response);
    }

    internal Task<TResponse> SendMultipartAsync<TResponse>(HttpMethod method, Expression<Func<string>> endpointExpr, IReadOnlyDictionary<string, object> multipartArgs, BucketIds ids,
        ClientBucketType clientBucket = ClientBucketType.Unbucketed, RequestOptions options = null, [CallerMemberName] string funcName = null, bool bypassDeserialization = false) where TResponse : class
        => SendMultipartAsync<TResponse>(method, GetEndpoint(endpointExpr), multipartArgs, GetBucketId(method, ids, endpointExpr, funcName), clientBucket, bypassDeserialization, options);
    public async Task<TResponse> SendMultipartAsync<TResponse>(HttpMethod method, string endpoint, IReadOnlyDictionary<string, object> multipartArgs,
        BucketId bucketId = null, ClientBucketType clientBucket = ClientBucketType.Unbucketed, bool bypassDeserialization = false, RequestOptions options = null) where TResponse : class
    {
        options ??= new RequestOptions();
        options.BucketId = bucketId;

        var request = new MultipartRestRequest(RestClient, method, endpoint, multipartArgs, options);
        Stream response = await SendInternalAsync(method, endpoint, request).ConfigureAwait(false);
        return bypassDeserialization ? response as TResponse : DeserializeJson<TResponse>(response);
    }
    
    private async Task<Stream> SendInternalAsync(HttpMethod method, string endpoint, RestRequest request)
    {
        if (!request.Options.IgnoreState)
            CheckState();

        request.Options.RetryMode ??= DefaultRetryMode;
        request.Options.RatelimitCallback ??= DefaultRatelimitCallback;
        
        var stopwatch = Stopwatch.StartNew();
        var responseStream = await RequestQueue.SendAsync(request).ConfigureAwait(false);
        stopwatch.Stop();
        
        double milliseconds = ToMilliseconds(stopwatch);
        await _sentRequestEvent.InvokeAsync(method, endpoint, milliseconds).ConfigureAwait(false);
        
        return responseStream;
    }
    
    /// <param name="endpointExpr">
    ///     <example>
    ///         <c>$"endpoint?params=args&amp;page_size={pageSize}&amp;page={page}"</c>
    ///     </example>
    /// </param>
    private async IAsyncEnumerable<IReadOnlyCollection<T>> SendPagedAsync<T>(HttpMethod method, 
        Expression<Func<int, int, string>> endpointExpr,
        BucketIds ids, ClientBucketType clientBucket = ClientBucketType.Unbucketed, PageMeta pageMeta = null, RequestOptions options = null)
        where T : class
    {
        pageMeta ??= PageMeta.Default;
    
        while (pageMeta.Page <= pageMeta.PageTotal)
        {
            PagedResponseBase<T> pagedResp = await SendAsync<PagedResponseBase<T>, int, int>(
                    method, endpointExpr, pageMeta.PageSize, pageMeta.Page,
                    ids, clientBucket, options: options)
                .ConfigureAwait(false);
            pageMeta = pagedResp.Meta;
            pageMeta.Page++;
            yield return pagedResp.Items;
        }
    }

    #endregion

    #region Guilds

    public IAsyncEnumerable<IReadOnlyCollection<Guild>> GetGuildsAsync(int limit = KaiHeiLaConfig.MaxItemsPerBatchByDefault, int fromPage = 1, RequestOptions options = null)
    {
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        return SendPagedAsync<Guild>(HttpMethod.Get,
            (pageSize, page) => $"guild/list?page_size={pageSize}&page={page}",
            ids, clientBucket: ClientBucketType.SendEdit, pageMeta: new PageMeta(page: fromPage, pageSize: limit), options: options);
    }
    
    public async Task<ExtendedGuild> GetGuildAsync(ulong guildId, RequestOptions options = null)
    {
        Preconditions.NotEqual(guildId, 0, nameof(guildId));
        options = RequestOptions.CreateOrClone(options);

        try
        {
            var ids = new BucketIds(guildId: guildId);
            return await SendAsync<ExtendedGuild>(HttpMethod.Get, 
                () => $"guild/view?guild_id={guildId}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
        }
        catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.NotFound) { return null; }
    }
    
    public async ValueTask<int> GetGuildMemberCountAsync(ulong guildId, RequestOptions options = null)
    {
        Preconditions.NotEqual(guildId, 0, nameof(guildId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: guildId);
        var response = await SendAsync<GetGuildMemberCountResponse>(HttpMethod.Get, 
            () => $"guild/user-list?guild_id={guildId}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
        return response.UserCount;
    }
    
    public IAsyncEnumerable<IReadOnlyCollection<GuildMember>> GetGuildMembersAsync(ulong guildId, Action<SearchGuildMemberProperties> func = null,
        int limit = KaiHeiLaConfig.MaxUsersPerBatch, int fromPage = 1, RequestOptions options = null)
    {
        Preconditions.NotEqual(guildId, 0, nameof(guildId));
        Preconditions.GreaterThan(limit, 0, nameof(limit));
        Preconditions.AtMost(limit, KaiHeiLaConfig.MaxUsersPerBatch, nameof(limit));
        options = RequestOptions.CreateOrClone(options);
        
        string extendedQuery = string.Empty;
        if (func is not null)
        {
            SearchGuildMemberProperties properties = new();
            func(properties);
            if (!string.IsNullOrWhiteSpace(properties.SearchName)) extendedQuery += $"&search={Uri.EscapeDataString(properties.SearchName)}";
            if (properties.RoleId.HasValue) extendedQuery += $"&role_id={properties.RoleId.Value}";
            if (properties.IsMobileVerified.HasValue) extendedQuery += $"&mobile_verified={properties.IsMobileVerified.Value switch {true => 1, false => 0}}";
            if (properties.SortedByActiveTime.HasValue) extendedQuery += $"&active_time={(int)properties.SortedByActiveTime.Value}";
            if (properties.SortedByJoinTime.HasValue) extendedQuery += $"&joined_at={(int)properties.SortedByJoinTime.Value}";
        }
        var ids = new BucketIds(guildId: guildId);
        return SendPagedAsync<GuildMember>(HttpMethod.Get, (pageSize, page) => $"guild/user-list?guild_id={guildId}&page_size={pageSize}&page={page}{extendedQuery}",
            ids, clientBucket: ClientBucketType.SendEdit, pageMeta: new PageMeta(page: fromPage, pageSize: limit), options: options);
    }

    public async Task ModifyGuildMemberNicknameAsync(ModifyGuildMemberNicknameParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        if (args.Nickname?.Length > KaiHeiLaConfig.MaxNicknameSize)
            throw new ArgumentException(message: $"Nickname is too long, length must be less or equal to {KaiHeiLaConfig.MaxNicknameSize}.", paramName: nameof(args.Nickname));
        if (args.Nickname?.Length < KaiHeiLaConfig.MinNicknameSize)
            throw new ArgumentException(message: $"Nickname is too short, length must be more or equal to {KaiHeiLaConfig.MinNicknameSize}.", paramName: nameof(args.Nickname));

        if (args.UserId is not null)
            Preconditions.NotEqual(args.UserId, 0, nameof(args.UserId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: args.GuildId);
        await SendJsonAsync(HttpMethod.Post, () => $"guild/nickname", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task LeaveGuildAsync(LeaveGuildParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: args.GuildId);
        await SendJsonAsync(HttpMethod.Post, () => $"guild/leave", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task KickOutGuildMemberAsync(KickOutGuildMemberParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        Preconditions.NotEqual(args.UserId, 0, nameof(args.UserId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: args.GuildId);
        await SendJsonAsync(HttpMethod.Post, () => $"guild/kickout", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task<GetGuildMuteDeafListResponse> GetGuildMutedDeafenedUsersAsync(ulong guildId, RequestOptions options = null)
    {
        Preconditions.NotEqual(guildId, 0, nameof(guildId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(guildId: guildId);
        return await SendAsync<GetGuildMuteDeafListResponse>(HttpMethod.Post, () => $"guild-mute/list?guild_id={guildId}&return_type=detail", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task CreateGuildMuteDeafAsync(CreateOrRemoveGuildMuteDeafParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        Preconditions.NotEqual(args.UserId, 0, nameof(args.UserId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(guildId: args.GuildId);
        await SendJsonAsync(HttpMethod.Post, () => $"guild-mute/create", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task RemoveGuildMuteDeafAsync(CreateOrRemoveGuildMuteDeafParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        Preconditions.NotEqual(args.UserId, 0, nameof(args.UserId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(guildId: args.GuildId);
        await SendJsonAsync(HttpMethod.Post, () => $"guild-mute/delete", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    #endregion

    #region Channels

    public IAsyncEnumerable<IReadOnlyCollection<Channel>> GetGuildChannelsAsync(ulong guildId, int limit = KaiHeiLaConfig.MaxItemsPerBatchByDefault, int fromPage = 1, RequestOptions options = null)
    {
        Preconditions.NotEqual(guildId, 0, nameof(guildId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: guildId);
        return SendPagedAsync<Channel>(HttpMethod.Get,
            (pageSize, page) => $"channel/list?guild_id={guildId}&page_size={pageSize}&page={page}",
            ids, clientBucket: ClientBucketType.SendEdit, pageMeta: new PageMeta(page: fromPage, pageSize: limit), options: options);
    }

    public async Task<Channel> GetGuildChannelAsync(ulong channelId, RequestOptions options = null)
    {
        Preconditions.NotEqual(channelId, 0, nameof(channelId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(channelId: channelId);
        return await SendAsync<Channel>(HttpMethod.Get, () => $"channel/view?target_id={channelId}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task<Channel> ModifyGuildChannelAsync(ulong channelId, ModifyGuildChannelParams args, RequestOptions options = null)
    {
        Preconditions.NotEqual(channelId, 0, nameof(channelId));
        Preconditions.NotNull(args, nameof(args));
        Preconditions.AtLeast(args.Position, 0, nameof(args.Position));
        Preconditions.AtMost(args.Name?.Length, 100, nameof(args.Name));

        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(channelId: channelId);
        return await SendJsonAsync<Channel>(HttpMethod.Post, () => $"channel/update", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task<Channel> ModifyGuildChannelAsync(ulong channelId, ModifyTextChannelParams args, RequestOptions options = null)
    {
        Preconditions.NotEqual(channelId, 0, nameof(channelId));
        Preconditions.NotNull(args, nameof(args));
        Preconditions.AtLeast(args.SlowModeInterval, 0, nameof(args.SlowModeInterval));
        Preconditions.AtMost(args.SlowModeInterval, 21600000, nameof(args.SlowModeInterval));
        Preconditions.AtMost(args.Name?.Length, 100, nameof(args.Name));
        Preconditions.AtMost(args.Topic?.Length, 500, nameof(args.Name));

        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(channelId: channelId);
        return await SendJsonAsync<Channel>(HttpMethod.Post, () => $"channel/update", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task<Channel> ModifyGuildChannelAsync(ulong channelId, ModifyVoiceChannelParams args, RequestOptions options = null)
    {
        Preconditions.NotEqual(channelId, 0, nameof(channelId));
        Preconditions.NotNull(args, nameof(args));
        Preconditions.AtLeast(args.UserLimit, 0, nameof(args.UserLimit));
        Preconditions.AtLeast(args.Position, 0, nameof(args.Position));
        Preconditions.AtMost(args.Name?.Length, 100, nameof(args.Name));

        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(channelId: channelId);
        return await SendJsonAsync<Channel>(HttpMethod.Post, () => $"channel/update", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task<Channel> CreateGuildChannelAsync(CreateGuildChannelParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        Preconditions.AtLeast(args.LimitAmount, 0, nameof(args.LimitAmount));
        Preconditions.AtMost(args.LimitAmount, 99, nameof(args.LimitAmount));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(guildId: args.GuildId);
        return await SendJsonAsync<Channel>(HttpMethod.Post, () => $"channel/create", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task DeleteGuildChannelAsync(DeleteGuildChannelParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.ChannelId, 0, nameof(args.ChannelId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(channelId: args.ChannelId);
        await SendJsonAsync(HttpMethod.Post, () => $"channel/delete", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task MoveUsersAsync(MoveUsersParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.ChannelId, 0, nameof(args.ChannelId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(channelId: args.ChannelId);
        await SendJsonAsync(HttpMethod.Post, () => $"channel/move-user", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task<GetChannelPermissionOverwritesResponse> GetChannelPermissionOverwritesAsync(ulong channelId, RequestOptions options = null)
    {
        Preconditions.NotEqual(channelId, 0, nameof(channelId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(channelId: channelId);
        return await SendAsync<GetChannelPermissionOverwritesResponse>(HttpMethod.Post, () => $"channel-role/index?channel_id={channelId}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task<CreateOrModifyChannelPermissionOverwriteResponse> CreateChannelPermissionOverwriteAsync(CreateOrRemoveChannelPermissionOverwriteParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.ChannelId, 0, nameof(args.ChannelId));
        Preconditions.NotEqual(args.TargetId, 0, nameof(args.TargetId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(channelId: args.ChannelId);
        return await SendJsonAsync<CreateOrModifyChannelPermissionOverwriteResponse>(HttpMethod.Post, () => $"channel-role/create", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task<CreateOrModifyChannelPermissionOverwriteResponse> ModifyChannelPermissionOverwriteAsync(ModifyChannelPermissionOverwriteParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.ChannelId, 0, nameof(args.ChannelId));
        Preconditions.NotEqual(args.TargetId, 0, nameof(args.TargetId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(channelId: args.ChannelId);
        return await SendJsonAsync<CreateOrModifyChannelPermissionOverwriteResponse>(HttpMethod.Post, () => $"channel-role/update", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task RemoveChannelPermissionOverwriteAsync(CreateOrRemoveChannelPermissionOverwriteParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.ChannelId, 0, nameof(args.ChannelId));
        Preconditions.NotEqual(args.TargetId, 0, nameof(args.TargetId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(channelId: args.ChannelId);
        await SendJsonAsync(HttpMethod.Post, () => $"channel-role/delete", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    #endregion

    #region Messages

    public async Task<IReadOnlyCollection<Message>> QueryMessagesAsync(ulong channelId, Guid? referenceMessageId = null,
        bool? queryPin = null, Direction dir = Direction.Unspecified, int count = 50, RequestOptions options = null)
    {
        Preconditions.NotEqual(channelId, 0, nameof(channelId));
        if (referenceMessageId is not null)
            Preconditions.NotEqual(referenceMessageId.Value, Guid.Empty, nameof(referenceMessageId));
        Preconditions.AtLeast(count, 1, nameof(count));
        Preconditions.AtMost(count, 100, nameof(count));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(channelId: channelId);
        string query = $"?target_id={channelId}";
        if (referenceMessageId is not null) query += $"&msg_id={referenceMessageId}";
        if (queryPin is not null) query += $"&pin={queryPin switch { true => 1, false => 0 }}";
        string flag = dir switch
        {
            Direction.Before => "&flag=before", 
            Direction.Around => "&flag=around",
            Direction.After => "&flag=after",
            Direction.Unspecified => "",
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
        };
        query += flag;
        query += $"&page_size={count}";
        QueryMessagesResponse queryMessagesResponse = await SendAsync<QueryMessagesResponse>(HttpMethod.Get, () => $"message/list{query}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
        return queryMessagesResponse.Items;
    }

    public async Task<Message> GetMessageAsync(Guid messageId, RequestOptions options = null)
    {
        Preconditions.NotEqual(messageId, Guid.Empty, nameof(messageId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        return await SendAsync<Message>(HttpMethod.Get, () => $"message/view?msg_id={messageId}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task<CreateMessageResponse> CreateMessageAsync(CreateMessageParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.ChannelId, 0, nameof(args.ChannelId));
        
        if (args.Content?.Length > KaiHeiLaConfig.MaxMessageSize)
            throw new ArgumentException(message: $"Message content is too long, length must be less or equal to {KaiHeiLaConfig.MaxMessageSize}.", paramName: nameof(args.Content));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(channelId: args.ChannelId);
        return await SendJsonAsync<CreateMessageResponse>(HttpMethod.Post, () => $"message/create", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task ModifyMessageAsync(ModifyMessageParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.MessageId, Guid.Empty, nameof(args.MessageId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"message/update", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task DeleteMessageAsync(DeleteMessageParams args, RequestOptions options = null)
    {
        Preconditions.NotEqual(args.MessageId, Guid.Empty, nameof(args.MessageId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"message/delete", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ReactionUserResponse>> GetReactionUsersAsync(Guid messageId, string emojiId, RequestOptions options = null)
    {
        Preconditions.NotEqual(messageId, Guid.Empty, nameof(messageId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        return await SendAsync<IReadOnlyCollection<ReactionUserResponse>>(HttpMethod.Get, () => $"message/reaction-list?msg_id={messageId}&emoji={HttpUtility.UrlEncode(emojiId)}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task AddReactionAsync(AddReactionParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.MessageId, Guid.Empty, nameof(args.MessageId));
        
        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"message/add-reaction", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task RemoveReactionAsync(RemoveReactionParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.MessageId, Guid.Empty, nameof(args.MessageId));
        if (args.UserId is not null)
            Preconditions.NotEqual(args.UserId, 0, nameof(args.MessageId));
        
        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"message/delete-reaction", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    #endregion

    #region Guild Users

    public IAsyncEnumerable<IReadOnlyCollection<Channel>> GetAudioChannelsUserConnectsAsync(ulong? guildId = null,
        ulong? userId = null, int limit = KaiHeiLaConfig.MaxItemsPerBatchByDefault, int fromPage = 1,
        RequestOptions options = null)
    {
        Preconditions.NotEqual(guildId, 0, nameof(guildId));
        Preconditions.NotEqual(userId, 0, nameof(userId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: guildId ?? 0);
        return SendPagedAsync<Channel>(HttpMethod.Get,
                (pageSize, page) =>
                    $"channel-user/get-joined-channel?guild_id={guildId}&user_id={userId}&page_size={pageSize}&page={page}",
                ids, clientBucket: ClientBucketType.SendEdit, pageMeta: new PageMeta(page: fromPage, pageSize: limit), options: options);
    }

    #endregion
    
    #region User Chats

    public IAsyncEnumerable<IReadOnlyCollection<UserChat>> GetUserChatsAsync(int limit = KaiHeiLaConfig.MaxItemsPerBatchByDefault, int fromPage = 1, RequestOptions options = null)
    {
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        return SendPagedAsync<UserChat>(HttpMethod.Get,
            (pageSize, page) => $"user-chat/list?page_size={pageSize}&page={page}",
            ids, clientBucket: ClientBucketType.SendEdit, pageMeta: new PageMeta(page: fromPage, pageSize: limit), options: options);;
    }

    public async Task<UserChat> GetUserChatAsync(Guid chatCode, RequestOptions options = null)
    {
        Preconditions.NotEqual(chatCode, Guid.Empty, nameof(chatCode));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        return await SendAsync<UserChat>(HttpMethod.Get, () => $"user-chat/view?chat_code={chatCode:N}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task<UserChat> CreateUserChatAsync(CreateUserChatParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.UserId, 0, nameof(args.UserId));
        
        var ids = new BucketIds();
        return await SendJsonAsync<UserChat>(HttpMethod.Post, () => $"user-chat/create", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task DeleteUserChatAsync(DeleteUserChatParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.ChatCode, Guid.Empty, nameof(args.ChatCode));
        
        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"user-chat/delete", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    #endregion

    #region Direct Messages

    public async Task<DirectMessage> GetDirectMessageAsync(Guid messageId, Guid? chatCode = null, ulong? userId = null,
        RequestOptions options = null)
    {
        // Waiting for direct-message/view endpoint
        // Try getting by fetching all messages
        var messages = await QueryDirectMessagesAsync(chatCode, userId, messageId, Direction.Unspecified, 50, options);
        int count = messages.Count;
        DirectMessage message = messages.SingleOrDefault(x => x.Id == messageId);
        if (message is not null) return message;

        // We have fetched all messages, but the message we're looking for is not there, hence null
        if (count < 50) return null;

        // Try getting by fetching the message next to the targeted one
        // Try getting by before mode
        DirectMessage messageBefore = (await QueryDirectMessagesAsync(chatCode, userId, messageId, Direction.Before, 1, options)).SingleOrDefault(x => x.Id == messageId);
        if (messageBefore is not null)
            return (await QueryDirectMessagesAsync(chatCode, userId, messageBefore.Id, Direction.After, 1, options)).SingleOrDefault(x => x.Id == messageId);
        
        // Try getting by after mode
        DirectMessage messageAfter = (await QueryDirectMessagesAsync(chatCode, userId, messageId, Direction.After, 1, options)).SingleOrDefault(x => x.Id == messageId);
        if (messageAfter is not null)
            return (await QueryDirectMessagesAsync(chatCode, userId, messageAfter.Id, Direction.Before, 1, options)).SingleOrDefault(x => x.Id == messageId);
        
        return null;
    }
    
    public async Task<IReadOnlyCollection<DirectMessage>> QueryDirectMessagesAsync(Guid? chatCode = null,
        ulong? userId = null, Guid? referenceMessageId = null,
        Direction dir = Direction.Unspecified, int count = 50, RequestOptions options = null)
    {
        if (chatCode is null && userId is null)
            throw new ArgumentException(message: $"At least one argument must be provided between {nameof(chatCode)} and {nameof(userId)}.", paramName: $"{nameof(chatCode)}&{nameof(userId)}");
        if (referenceMessageId is not null)
            Preconditions.NotEqual(referenceMessageId.Value, Guid.Empty, nameof(referenceMessageId));
        Preconditions.AtLeast(count, 1, nameof(count));
        Preconditions.AtMost(count, 100, nameof(count));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        string query = (chatCode is not null, userId is not null) switch
        {
            (true, true) => $"?chat_code={chatCode:N}&target_id={userId}",
            (true, false) => $"?chat_code={chatCode:N}",
            (false, true) => $"?target_id={userId}",
            _ => string.Empty
        };
        if (referenceMessageId is not null) query += $"&msg_id={referenceMessageId}";
        string flag = dir switch
        {
            Direction.Before => "before", 
            Direction.Around => "around",
            Direction.After => "after",
            _ => string.Empty
        };
        if (dir != Direction.Unspecified) query += $"&flag={flag}";
        query += $"&page_size={count}";
        QueryUserChatMessagesResponse queryMessagesResponse = await SendAsync<QueryUserChatMessagesResponse>(HttpMethod.Get, () => $"direct-message/list{query}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
        return queryMessagesResponse.Items;
    }

    public async Task<CreateDirectMessageResponse> CreateDirectMessageAsync(CreateDirectMessageParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        if (args.ChatCode is null && args.UserId is null)
            throw new ArgumentException(message: $"At least one argument must be provided between {nameof(args.ChatCode)} and {nameof(args.UserId)}.", paramName: $"{nameof(args.ChatCode)}&{nameof(args.UserId)}");
        
        if (args.Content?.Length > KaiHeiLaConfig.MaxMessageSize)
            throw new ArgumentException(message: $"Message content is too long, length must be less or equal to {KaiHeiLaConfig.MaxMessageSize}.", paramName: nameof(args.Content));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        return await SendJsonAsync<CreateDirectMessageResponse>(HttpMethod.Post, () => $"direct-message/create", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task ModifyDirectMessageAsync(ModifyDirectMessageParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.MessageId, Guid.Empty, nameof(args.MessageId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"direct-message/update", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task DeleteDirectMessageAsync(DeleteDirectMessageParams args, RequestOptions options = null)
    {
        Preconditions.NotEqual(args.MessageId, Guid.Empty, nameof(args.MessageId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"direct-message/delete", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task<IReadOnlyCollection<ReactionUserResponse>> GetDirectMessageReactionUsersAsync(Guid messageId, string emojiId, RequestOptions options = null)
    {
        Preconditions.NotEqual(messageId, Guid.Empty, nameof(messageId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        return await SendAsync<IReadOnlyCollection<ReactionUserResponse>>(HttpMethod.Get, () => $"direct-message/reaction-list?msg_id={messageId}&emoji={HttpUtility.UrlEncode(emojiId)}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task AddDirectMessageReactionAsync(AddReactionParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.MessageId, Guid.Empty, nameof(args.MessageId));
        
        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"direct-message/add-reaction", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task RemoveDirectMessageReactionAsync(RemoveReactionParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.MessageId, Guid.Empty, nameof(args.MessageId));
        if (args.UserId is not null)
            Preconditions.NotEqual(args.UserId, 0, nameof(args.MessageId));
        
        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"direct-message/delete-reaction", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    #endregion
    
    #region Gateway
    
    public async Task<GetBotGatewayResponse> GetBotGatewayAsync(bool isCompressed = true, RequestOptions options = null)
    {
        options = RequestOptions.CreateOrClone(options);
        return await SendAsync<GetBotGatewayResponse>(HttpMethod.Get, () => $"gateway/index?compress={(isCompressed ? 1 : 0)}", new BucketIds(), options: options).ConfigureAwait(false);
    }

    public async Task<GetVoiceGatewayResponse> GetVoiceGatewayAsync(ulong channelId, RequestOptions options = null)
    {
        Preconditions.NotEqual(channelId, 0, nameof(channelId));
        options = RequestOptions.CreateOrClone(options);
        return await SendAsync<GetVoiceGatewayResponse>(HttpMethod.Get, () => $"gateway/voice?channel_id={channelId}", new BucketIds(), options: options).ConfigureAwait(false);
    }

    #endregion
    
    #region Users
    
    public async Task<SelfUser> GetSelfUserAsync(RequestOptions options = null)
    {
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        return await SendAsync<SelfUser>(HttpMethod.Get, () => "user/me", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task<User> GetUserAsync(ulong userId, RequestOptions options = null)
    {
        Preconditions.NotEqual(userId, 0, nameof(userId));
        
        var ids = new BucketIds();
        return await SendAsync<User>(HttpMethod.Get, () => $"user/view?user_id={userId}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task<GuildMember> GetGuildMemberAsync(ulong guildId, ulong userId, RequestOptions options = null)
    {
        Preconditions.NotEqual(userId, 0, nameof(userId));
        Preconditions.NotEqual(guildId, 0, nameof(guildId));
        
        var ids = new BucketIds(guildId: guildId);
        return await SendAsync<GuildMember>(HttpMethod.Get, () => $"user/view?user_id={userId}&guild_id={guildId}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task GoOfflineAsync(RequestOptions options = null)
    {
        var ids = new BucketIds();
        await SendAsync(HttpMethod.Get, () => $"user/offline", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    #endregion

    #region Assets

    public async Task<CreateAssetResponse> CreateAssetAsync(CreateAssetParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        return await SendMultipartAsync<CreateAssetResponse>(HttpMethod.Post, () => $"asset/create", args.ToDictionary(), ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    #endregion

    #region Guild Roles

    public IAsyncEnumerable<IReadOnlyCollection<Role>> GetGuildRolesAsync(ulong guildId, int limit = KaiHeiLaConfig.MaxItemsPerBatchByDefault, int fromPage = 1, RequestOptions options = null)
    {
        Preconditions.NotEqual(guildId, 0, nameof(guildId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        return SendPagedAsync<Role>(HttpMethod.Get,
            (pageSize, page) => $"guild-role/list?guild={guildId}&page_size={pageSize}&page={page}",
            ids, clientBucket: ClientBucketType.SendEdit, pageMeta: new PageMeta(page: fromPage, pageSize: limit), options: options);
    }
    
    public async Task<Role> CreateGuildRoleAsync(CreateGuildRoleParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: args.GuildId);
        return await SendJsonAsync<Role>(HttpMethod.Post, () => $"guild-role/create", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task<Role> ModifyGuildRoleAsync(ModifyGuildRoleParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: args.GuildId);
        return await SendJsonAsync<Role>(HttpMethod.Post, () => $"guild-role/update", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task DeleteGuildRoleAsync(DeleteGuildRoleParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: args.GuildId);
        await SendJsonAsync(HttpMethod.Post, () => $"guild-role/delete", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task<AddOrRemoveRoleResponse> AddRoleAsync(AddOrRemoveRoleParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: args.GuildId);
        return await SendJsonAsync<AddOrRemoveRoleResponse>(HttpMethod.Post, () => $"guild-role/grant", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task<AddOrRemoveRoleResponse> RemoveRoleAsync(AddOrRemoveRoleParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: args.GuildId);
        return await SendJsonAsync<AddOrRemoveRoleResponse>(HttpMethod.Post, () => $"guild-role/revoke", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    #endregion

    #region Intimacy

    public async Task<Intimacy> GetIntimacyAsync(ulong userId, RequestOptions options = null)
    {
        Preconditions.NotEqual(userId, 0, nameof(userId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds();
        return await SendAsync<Intimacy>(HttpMethod.Get, () => $"intimacy/index", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task UpdateIntimacyValueAsync(UpdateIntimacyValueParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.UserId, 0, nameof(args.UserId));
        Preconditions.AtLeast(args.Score, KaiHeiLaConfig.MinIntimacyScore, nameof(args.Score));
        Preconditions.AtMost(args.Score, KaiHeiLaConfig.MaxIntimacyScore, nameof(args.Score));
        
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"intimacy/update", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    #endregion

    #region Guild Emoji

    public IAsyncEnumerable<IReadOnlyCollection<Emoji>> GetGuildEmotesAsync(ulong guildId, int limit = KaiHeiLaConfig.MaxItemsPerBatchByDefault, int fromPage = 1, RequestOptions options = null)
    {
        Preconditions.NotEqual(guildId, 0, nameof(guildId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(guildId: guildId);
        return SendPagedAsync<Emoji>(HttpMethod.Get,
            (pageSize, page) => $"guild-emoji/list?guild_id={guildId}&page_size={pageSize}&page={page}",
            ids, clientBucket: ClientBucketType.SendEdit, pageMeta: new PageMeta(page: fromPage, pageSize: limit), options: options);
    }
    
    public async Task<Emoji> CreateGuildEmoteAsync(CreateGuildEmoteParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        if (args.Name is not null)
        {
            Preconditions.AtLeast(args.Name.Length, 2, nameof(args.Name.Length));
            Preconditions.AtMost(args.Name.Length, 32, nameof(args.Name.Length));
        }
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(guildId: args.GuildId);
        return await SendMultipartAsync<Emoji>(HttpMethod.Post, () => $"guild-emoji/create", args.ToDictionary(), ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task ModifyGuildEmoteAsync(ModifyGuildEmoteParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        if (args.Name is not null)
        {
            Preconditions.AtLeast(args.Name.Length, 2, nameof(args.Name.Length));
            Preconditions.AtMost(args.Name.Length, 32, nameof(args.Name.Length));
        }
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"guild-emoji/update", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task DeleteGuildEmoteAsync(DeleteGuildEmoteParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"guild-emoji/delete", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    #endregion

    #region Guild Invites

    public IAsyncEnumerable<IReadOnlyCollection<Invite>> GetGuildInvitesAsync(ulong? guildId = null, ulong? channelId = null, int limit = KaiHeiLaConfig.MaxItemsPerBatchByDefault, int fromPage = 1, RequestOptions options = null)
    {
        if (guildId is null && channelId is null)
            throw new ArgumentException(message: $"At least one argument must be provided between {nameof(guildId)} and {nameof(channelId)}.", paramName: $"{nameof(guildId)}&{nameof(channelId)}");
        if (guildId is not null)
            Preconditions.NotEqual(guildId, 0, nameof(guildId));
        if (channelId is not null)
            Preconditions.NotEqual(channelId, 0, nameof(channelId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: guildId ?? 0, channelId: channelId ?? 0);
        string query = (guildId is not null, channelId is not null) switch
        {
            (true, true) => $"?guild_id={guildId}&channel_id={channelId}",
            (true, false) => $"?guild_id={guildId}",
            (false, true) => $"?channel_id={channelId}",
            _ => string.Empty
        };
        return SendPagedAsync<Invite>(HttpMethod.Get,
            (pageSize, page) => $"invite/list{query}&page_size={pageSize}&page={page}",
            ids, clientBucket: ClientBucketType.SendEdit, pageMeta: new PageMeta(page: fromPage, pageSize: limit), options: options);
    }

    public async Task<CreateGuildInviteResponse> CreateGuildInviteAsync(CreateGuildInviteParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        if (args.GuildId is null && args.ChannelId is null)
            throw new ArgumentException(message: $"At least one argument must be provided between {nameof(args.GuildId)} and {nameof(args.ChannelId)}.", paramName: $"{nameof(args.GuildId)}&{nameof(args.ChannelId)}");
        if (args.GuildId is not null)
            Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        if (args.ChannelId is not null)
            Preconditions.NotEqual(args.ChannelId, 0, nameof(args.ChannelId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(guildId: args.GuildId ?? 0, channelId: args.ChannelId ?? 0);
        return await SendJsonAsync<CreateGuildInviteResponse>(HttpMethod.Post, () => $"invite/create", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    public async Task DeleteGuildInviteAsync(DeleteGuildInviteParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotNullOrWhitespace(args.UrlCode, nameof(args.UrlCode));
        if (args.GuildId is not null)
            Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        if (args.ChannelId is not null)
            Preconditions.NotEqual(args.ChannelId, 0, nameof(args.ChannelId));
        options = RequestOptions.CreateOrClone(options);
        
        var ids = new BucketIds(guildId: args.GuildId ?? 0, channelId: args.ChannelId ?? 0);
        await SendJsonAsync(HttpMethod.Post, () => $"invite/delete", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    #endregion

    #region Guild Bans

    public async Task<IReadOnlyCollection<Ban>> GetGuildBansAsync(ulong guildId, RequestOptions options = null)
    {
        Preconditions.NotEqual(guildId, 0, nameof(guildId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: guildId);
        return await SendAsync<IReadOnlyCollection<Ban>>(HttpMethod.Get, () => $"blacklist/list?guild_id={guildId}", ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task CreateGuildBanAsync(CreateGuildBanParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        Preconditions.NotEqual(args.UserId, 0, nameof(args.UserId));
        if (args.DeleteMessageDays is not null)
        {
            Preconditions.AtLeast(args.DeleteMessageDays.Value, 0, nameof(args.DeleteMessageDays), "Prune length must be within [0, 7]");
            Preconditions.AtMost(args.DeleteMessageDays.Value, 7, nameof(args.DeleteMessageDays), "Prune length must be within [0, 7]");
        }
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: args.GuildId);
        await SendJsonAsync(HttpMethod.Post, () => $"blacklist/create", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task RemoveGuildBanAsync(RemoveGuildBanParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotEqual(args.GuildId, 0, nameof(args.GuildId));
        Preconditions.NotEqual(args.UserId, 0, nameof(args.UserId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: args.GuildId);
        await SendJsonAsync(HttpMethod.Post, () => $"blacklist/delete", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    

    #endregion

    #region Badges

    public async Task<Stream> GetGuildBadgeAsync(ulong guildId, BadgeStyle style = BadgeStyle.GuildName, RequestOptions options = null)
    {
        Preconditions.NotEqual(guildId, 0, nameof(guildId));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds(guildId: guildId);
        return await SendAsync<Stream>(HttpMethod.Get, () => $"badge/guild", ids, clientBucket: ClientBucketType.SendEdit, bypassDeserialization: true, options: options).ConfigureAwait(false);
    }

    #endregion

    #region Games

    public IAsyncEnumerable<IReadOnlyCollection<Game>> GetGamesAsync(int limit = KaiHeiLaConfig.MaxItemsPerBatchByDefault, int fromPage = 1, RequestOptions options = null)
    {
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds();
        return SendPagedAsync<Game>(HttpMethod.Get,
            (pageSize, page) => $"game?page_size={pageSize}&page={page}",
            ids, clientBucket: ClientBucketType.SendEdit, pageMeta: new PageMeta(page: fromPage, pageSize: limit), options: options);
    }
    
    public async Task<Game> CreateGameAsync(CreateGameParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.NotNullOrWhitespace(args.Name, nameof(args.Name));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds();
        return await SendJsonAsync<Game>(HttpMethod.Post, () => $"game/create", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task<Game> ModifyGameAsync(ModifyGameParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.GreaterThan(args.Id, 0, nameof(args.Id));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds();
        return await SendJsonAsync<Game>(HttpMethod.Post, () => $"game/update", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task DeleteGameAsync(DeleteGameParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.GreaterThan(args.Id, 0, nameof(args.Id));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"game/delete", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task BeginGameActivityAsync(BeginGameActivityParams args, RequestOptions options = null)
    {
        Preconditions.NotNull(args, nameof(args));
        Preconditions.GreaterThan(args.Id, 0, nameof(args.Id));
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"game/activity", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }
    
    public async Task EndGameActivityAsync(EndGameActivityParams args = null, RequestOptions options = null)
    {
        args ??= new EndGameActivityParams();
        options = RequestOptions.CreateOrClone(options);

        var ids = new BucketIds();
        await SendJsonAsync(HttpMethod.Post, () => $"game/delete-activity", args, ids, clientBucket: ClientBucketType.SendEdit, options: options).ConfigureAwait(false);
    }

    #endregion
    
    #region Helpers

    protected void CheckState()
    {
        if (LoginState != LoginState.LoggedIn)
            throw new InvalidOperationException("Client is not logged in.");
    }
    protected static double ToMilliseconds(Stopwatch stopwatch) => Math.Round((double)stopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000.0, 2);
    protected string SerializeJson(object payload)
    {
        return payload is null 
            ? string.Empty 
            : JsonSerializer.Serialize(payload, _serializerOptions);
    }
    
    protected T DeserializeJson<T>(Stream jsonStream)
    {
        return JsonSerializer.Deserialize<T>(jsonStream, _serializerOptions);
    }

    internal class BucketIds
    {
        public ulong GuildId { get; internal set; }
        public ulong ChannelId { get; internal set; }
        public HttpMethod HttpMethod { get; internal set; }

        internal BucketIds(ulong guildId = 0, ulong channelId = 0)
        {
            GuildId = guildId;
            ChannelId = channelId;
        }

        internal object[] ToArray()
            => new object[] { HttpMethod, GuildId, ChannelId };

        internal Dictionary<string, string> ToMajorParametersDictionary()
        {
            var dict = new Dictionary<string, string>();
            if (GuildId != 0)
                dict["GuildId"] = GuildId.ToString();
            if (ChannelId != 0)
                dict["ChannelId"] = ChannelId.ToString();
            return dict;
        }

        internal static int? GetIndex(string name)
        {
            return name switch
            {
                "httpMethod" => 0,
                "guildId" => 1,
                "channelId" => 2,
                _ => null,
            };
        }
    }
    
    private static string GetEndpoint(Expression<Func<string>> endpointExpr)
    {
        return endpointExpr.Compile()();
    }
    private static string GetEndpoint<T1, T2>(Expression<Func<T1, T2, string>> endpointExpr, T1 arg1, T2 arg2)
    {
        return endpointExpr.Compile()(arg1, arg2);
    }
    private static BucketId GetBucketId(HttpMethod httpMethod, BucketIds ids, Expression<Func<string>> endpointExpr, string callingMethod)
    {
        ids.HttpMethod = httpMethod;
        return _bucketIdGenerators.GetOrAdd(callingMethod, x => CreateBucketId(endpointExpr))(ids);
    }
    private static BucketId GetBucketId<TArg1, TArg2>(HttpMethod httpMethod, BucketIds ids, Expression<Func<TArg1, TArg2, string>> endpointExpr, TArg1 arg1, TArg2 arg2, string callingMethod)
    {
        ids.HttpMethod = httpMethod;
        return _bucketIdGenerators.GetOrAdd(callingMethod, x => CreateBucketId(endpointExpr, arg1, arg2))(ids);
    }

    private static Func<BucketIds, BucketId> CreateBucketId<TArg1, TArg2>(Expression<Func<TArg1, TArg2, string>> endpoint, TArg1 arg1, TArg2 arg2)
    {
        return CreateBucketId(() => endpoint.Compile().Invoke(arg1, arg2));
    }

    private static Func<BucketIds, BucketId> CreateBucketId(Expression<Func<string>> endpoint)
    {
        try
        {
            //Is this a constant string?
            if (endpoint.Body.NodeType == ExpressionType.Constant)
                return x => BucketId.Create(x.HttpMethod, (endpoint.Body as ConstantExpression).Value.ToString(), x.ToMajorParametersDictionary());

            var builder = new StringBuilder();
            var methodCall = endpoint.Body as MethodCallExpression;
            var methodArgs = methodCall.Arguments.ToArray();
            string format = methodArgs[0].NodeType == ExpressionType.Constant
                ? (methodArgs[0] as ConstantExpression).Value as string
                : endpoint.Compile()();

            //Unpack the array, if one exists (happens with 4+ parameters)
            if (methodArgs.Length > 1 && methodArgs[1].NodeType == ExpressionType.NewArrayInit)
            {
                var arrayExpr = methodArgs[1] as NewArrayExpression;
                var elements = arrayExpr.Expressions.ToArray();
                Array.Resize(ref methodArgs, elements.Length + 1);
                Array.Copy(elements, 0, methodArgs, 1, elements.Length);
            }

            int endIndex = format.IndexOf('?'); //Don't include params
            if (endIndex == -1)
                endIndex = format.Length;

            int lastIndex = 0;
            while (true)
            {
                int leftIndex = format.IndexOf("{", lastIndex);
                if (leftIndex == -1 || leftIndex > endIndex)
                {
                    builder.Append(format, lastIndex, endIndex - lastIndex);
                    break;
                }
                builder.Append(format, lastIndex, leftIndex - lastIndex);
                int rightIndex = format.IndexOf("}", leftIndex);

                int argId = int.Parse(format.Substring(leftIndex + 1, rightIndex - leftIndex - 1), NumberStyles.None, CultureInfo.InvariantCulture);
                string fieldName = GetFieldName(methodArgs[argId + 1]);

                var mappedId = BucketIds.GetIndex(fieldName);

                if (!mappedId.HasValue && rightIndex != endIndex && format.Length > rightIndex + 1 && format[rightIndex + 1] == '/') //Ignore the next slash
                    rightIndex++;

                if (mappedId.HasValue)
                    builder.Append($"{{{mappedId.Value}}}");

                lastIndex = rightIndex + 1;
            }
            if (builder[builder.Length - 1] == '/')
                builder.Remove(builder.Length - 1, 1);

            format = builder.ToString();

            return x => BucketId.Create(x.HttpMethod, string.Format(format, x.ToArray()), x.ToMajorParametersDictionary());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to generate the bucket id for this operation.", ex);
        }
    }

    private static string GetFieldName(Expression expr)
    {
        if (expr.NodeType == ExpressionType.Convert)
            expr = (expr as UnaryExpression).Operand;

        if (expr.NodeType != ExpressionType.MemberAccess)
            throw new InvalidOperationException("Unsupported expression");

        return (expr as MemberExpression).Member.Name;
    }

    #endregion
}