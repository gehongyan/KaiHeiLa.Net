using KaiHeiLa.Audio;

namespace KaiHeiLa;

/// <summary>
///     Represents a generic audio channel.
/// </summary>
public interface IAudioChannel : IChannel
{
    /// <summary>Connects to this audio channel.</summary>
    /// <returns>
    ///     A task representing the asynchronous connection operation. The task result contains the
    ///     <see cref="T:KaiHeiLa.Audio.IAudioClient" /> responsible for the connection.
    /// </returns>
    Task<IAudioClient> ConnectAsync();

    /// <summary>Disconnects from this audio channel.</summary>
    /// <returns>
    ///     A task representing the asynchronous operation for disconnecting from the audio channel.
    /// </returns>
    Task DisconnectAsync();
}