using KaiHeiLa.Audio;

namespace KaiHeiLa.Net.Samples.AudioBot.Services;

public class KaiHeiLaAudioClientManager
{
    public IAudioClient AudioClient { get; set; }
        
    // public async Task SendAudioStreamFromFileAsync(string path, int silentSeconds = 0)
    // {
    //     using (var ffmpeg = FileUtil.CreateAudioStreamFromFile(path, silentSeconds))
    //     using (var output = ffmpeg.StandardOutput.BaseStream)
    //     using (var discord = AudioClient.CreatePCMStream(AudioApplication.Mixed))
    //     {
    //         try { await output.CopyToAsync(discord); }
    //         finally { await discord.FlushAsync(); }
    //     }
    // }
}