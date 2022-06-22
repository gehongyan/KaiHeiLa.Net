using KaiHeiLa.Audio;
using KaiHeiLa.Net.Samples.AudioBot.Utils;

namespace KaiHeiLa.Net.Samples.AudioBot.Services;

public class KaiHeiLaAudioClientManager
{
    public IAudioClient AudioClient { get; set; }
        
    
    public async Task SendAudioStreamFromFileAsync(string path, int silentSeconds = 0)
    {
        using (var ffmpeg = StreamHelper.CreateAudioStreamFromFile(path, silentSeconds))
        using (var output = ffmpeg.StandardOutput.BaseStream)
        using (var client = AudioClient.CreateDirectOpusStream())
        {
            try { await output.CopyToAsync(client); }
            finally { await client.FlushAsync(); }
        }
    }

    public async Task SendAudioStreamViaFfmpeg(string path)
    {
    }
}