using System.Diagnostics;
using Model = KaiHeiLa.API.Rest.SelfUser;

namespace KaiHeiLa.Rest;

/// <summary>
///     Represents the logged-in REST-based user.
/// </summary>
[DebuggerDisplay(@"{DebuggerDisplay,nq}")]
public class RestSelfUser : RestUser, ISelfUser
{
    /// <inheritdoc />
    public string MobilePrefix { get; private set; }
    /// <inheritdoc />
    public string Mobile { get; private set; }
    /// <inheritdoc />
    public int InvitedCount { get; private set; }
    /// <inheritdoc />
    public bool IsMobileVerified { get; private set; }
    
    internal RestSelfUser(BaseKaiHeiLaClient kaiHeiLa, ulong id)
        : base(kaiHeiLa, id)
    {
    }
    internal static RestSelfUser Create(BaseKaiHeiLaClient kaiHeiLa, Model model)
    {
        var entity = new RestSelfUser(kaiHeiLa, model.Id);
        entity.Update(model);
        return entity;
    }
    internal void Update(Model model)
    {
        base.Update(model);
        
        MobilePrefix = model.MobilePrefix;
        Mobile = model.Mobile;
        InvitedCount = model.InvitedCount ?? 0;
        IsMobileVerified = model.MobileVerified;
    }
    
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Unable to update this object using a different token.</exception>
    public override async Task UpdateAsync(RequestOptions options = null)
    {
        var model = await KaiHeiLa.ApiClient.GetSelfUserAsync(options).ConfigureAwait(false);
        if (model.Id != Id)
            throw new InvalidOperationException("Unable to update this object using a different token.");
        Update(model);
    }

    #region ISelfUser
    
    /// <inheritdoc />
    public async Task StartPlayingAsync(IGame game, RequestOptions options = null)
    {
        await UserHelper.StartPlayingAsync(this, KaiHeiLa, game, options).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task StartPlayingAsync(Music music, RequestOptions options = null)
    {
        await UserHelper.StartPlayingAsync(this, KaiHeiLa, music, options).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task StopPlayingAsync(ActivityType type, RequestOptions options = null)
    {
        await UserHelper.StopPlayingAsync(this, KaiHeiLa, type, options).ConfigureAwait(false);
    }

    #endregion
}