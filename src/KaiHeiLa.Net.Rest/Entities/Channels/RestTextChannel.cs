using Model = KaiHeiLa.API.Channel;

using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using KaiHeiLa.API;
using KaiHeiLa.API.Rest;
using KaiHeiLa.Net.Converters;

namespace KaiHeiLa.Rest;

/// <summary>
///     Represents a REST-based channel in a guild that can send and receive messages.
/// </summary>
[DebuggerDisplay(@"{DebuggerDisplay,nq}")]
internal class RestTextChannel : RestGuildChannel, IRestMessageChannel, ITextChannel
{
    #region RestTextChannel
    
    /// <inheritdoc />
    public string Topic { get; private set; }
    /// <inheritdoc />
    public virtual int SlowModeInterval { get; private set; }
    /// <inheritdoc />
    public ulong? CategoryId { get; private set; }
    /// <inheritdoc />
    public bool IsPermissionSynced { get; set; }
    
    /// <inheritdoc />
    public string KMarkdownMention => MentionUtils.KMarkdownMentionChannel(Id);
    /// <inheritdoc />
    public string PlainTextMention => MentionUtils.PlainTextMentionChannel(Id);

    
    internal RestTextChannel(BaseKaiHeiLaClient kaiHeiLa, IGuild guild, ulong id)
        : base(kaiHeiLa, guild, id, ChannelType.Text)
    {
    }
    
    internal new static RestTextChannel Create(BaseKaiHeiLaClient kaiHeiLa, IGuild guild, Model model)
    {
        var entity = new RestTextChannel(kaiHeiLa, guild, model.Id);
        entity.Update(model);
        return entity;
    }
    /// <inheritdoc />
    internal override void Update(Model model)
    {
        base.Update(model);
        CategoryId = model.CategoryId;
        Topic = model.Topic;
        SlowModeInterval = model.SlowMode;
        IsPermissionSynced = model.PermissionSync == 1;
    }
    /// <inheritdoc />
    public Task<RestMessage> GetMessageAsync(Guid id, RequestOptions options = null)
        => ChannelHelper.GetMessageAsync(this, KaiHeiLa, id, options);
    
    public Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> SendTextMessageAsync(string text, Quote quote = null, IUser ephemeralUser = null, RequestOptions options = null)
        => ChannelHelper.SendMessageAsync(this, KaiHeiLa, MessageType.Text, text, options, quote: quote, ephemeralUser: ephemeralUser);
    public async Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> SendImageMessageAsync(string path, string fileName = null, Quote quote = null, IUser ephemeralUser = null, RequestOptions options = null)
    {
        CreateAssetResponse createAssetResponse = await KaiHeiLa.ApiClient.CreateAssetAsync(new CreateAssetParams {File = File.OpenRead(path), FileName = fileName}, options);
        return await ChannelHelper.SendMessageAsync(this, KaiHeiLa, MessageType.Image, createAssetResponse.Url, options, quote: quote,
            ephemeralUser: ephemeralUser);
    }
    public async Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> SendVideoMessageAsync(string path, string fileName = null, Quote quote = null, IUser ephemeralUser = null, RequestOptions options = null)
    {
        CreateAssetResponse createAssetResponse = await KaiHeiLa.ApiClient.CreateAssetAsync(new CreateAssetParams {File = File.OpenRead(path), FileName = fileName}, options);
        return await ChannelHelper.SendMessageAsync(this, KaiHeiLa, MessageType.Video, createAssetResponse.Url, options, quote: quote,
            ephemeralUser: ephemeralUser);
    }
    public async Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> SendFileMessageAsync(string path, string fileName = null, Quote quote = null, IUser ephemeralUser = null, RequestOptions options = null)
    {
        CreateAssetResponse createAssetResponse = await KaiHeiLa.ApiClient.CreateAssetAsync(new CreateAssetParams {File = File.OpenRead(path), FileName = fileName}, options);
        return await ChannelHelper.SendMessageAsync(this, KaiHeiLa, MessageType.File, createAssetResponse.Url, options, quote: quote,
            ephemeralUser: ephemeralUser);
    }
    // public async Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> SendAudioMessageAsync(string path, string fileName = null, Quote quote = null, IUser ephemeralUser = null, RequestOptions options = null)
    // {
    //     CreateAssetResponse createAssetResponse = await KaiHeiLa.ApiClient.CreateAssetAsync(path, fileName, options);
    //     return await ChannelHelper.SendMessageAsync(this, KaiHeiLa, MessageType.Audio, createAssetResponse.Url, options, quote: quote,
    //         ephemeralUser: ephemeralUser);
    // }
    public Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> SendKMarkdownMessageAsync(string text, Quote quote = null, IUser ephemeralUser = null, RequestOptions options = null)
        => ChannelHelper.SendMessageAsync(this, KaiHeiLa, MessageType.KMarkdown, text, options, quote: quote, ephemeralUser: ephemeralUser);

    public async Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> SendCardMessageAsync(IEnumerable<ICard> cards, Quote quote = null, IUser ephemeralUser = null, RequestOptions options = null)
    {
        string json = MessageHelper.SerializeCards(cards);
        return await ChannelHelper.SendMessageAsync(this, KaiHeiLa, MessageType.Card, json, options, quote: quote,
            ephemeralUser: ephemeralUser);
    }
    public Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> SendCardMessageAsync(ICard card, Quote quote = null, IUser ephemeralUser = null, RequestOptions options = null) => 
        SendCardMessageAsync(new[] { card }, quote, ephemeralUser, options);

    
    #endregion

    private string DebuggerDisplay => $"{Name} ({Id}, Text)";

    #region IMessageChannel
    
    /// <inheritdoc />
    Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> IMessageChannel.SendTextMessageAsync(string text,
        IQuote quote, IUser ephemeralUser, RequestOptions options)
        => SendTextMessageAsync(text, (Quote) quote, ephemeralUser, options);
    /// <inheritdoc />
    Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> IMessageChannel.SendImageMessageAsync(string path, string fileName,
        IQuote quote, IUser ephemeralUser, RequestOptions options)
        => SendImageMessageAsync(path, fileName, (Quote) quote, ephemeralUser, options);
    /// <inheritdoc />
    Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> IMessageChannel.SendVideoMessageAsync(string path, string fileName,
        IQuote quote, IUser ephemeralUser, RequestOptions options)
        => SendVideoMessageAsync(path, fileName, (Quote) quote, ephemeralUser, options);
    /// <inheritdoc />
    Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> IMessageChannel.SendFileMessageAsync(string path, string fileName,
        IQuote quote, IUser ephemeralUser, RequestOptions options)
        => SendFileMessageAsync(path, fileName, (Quote) quote, ephemeralUser, options);
    // /// <inheritdoc />
    // Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> IMessageChannel.SendAudioMessageAsync(string path, string fileName = null,
    //     IQuote quote, IUser ephemeralUser, RequestOptions options)
    //     => SendAudioMessageAsync(path, fileName, (Quote) quote, ephemeralUser, options);
    /// <inheritdoc />
    Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> IMessageChannel.SendKMarkdownMessageAsync(string text,
        IQuote quote, IUser ephemeralUser, RequestOptions options)
        => SendKMarkdownMessageAsync(text, (Quote) quote, ephemeralUser, options);
    /// <inheritdoc />
    Task<(Guid MessageId, DateTimeOffset MessageTimestamp)> IMessageChannel.SendCardMessageAsync(IEnumerable<ICard> cards,
        IQuote quote, IUser ephemeralUser, RequestOptions options)
        => SendCardMessageAsync(cards, (Quote) quote, ephemeralUser, options);

    /// <inheritdoc />
    async Task<IMessage> IMessageChannel.GetMessageAsync(Guid id, CacheMode mode, RequestOptions options)
    {
        if (mode == CacheMode.AllowDownload)
            return await GetMessageAsync(id, options).ConfigureAwait(false);
        else
            return null;
    }
    
    /// <inheritdoc />
    public Task DeleteMessageAsync(Guid messageId, RequestOptions options = null)
        => ChannelHelper.DeleteMessageAsync(this, messageId, KaiHeiLa, options);
    /// <inheritdoc />
    public Task DeleteMessageAsync(IMessage message, RequestOptions options = null)
        => ChannelHelper.DeleteMessageAsync(this, message.Id, KaiHeiLa, options);
    
    /// <inheritdoc />
    public async Task ModifyMessageAsync(Guid messageId, Action<MessageProperties> func, RequestOptions options = null)
        => await ChannelHelper.ModifyMessageAsync(this, messageId, func, KaiHeiLa, options).ConfigureAwait(false);

    #endregion
}