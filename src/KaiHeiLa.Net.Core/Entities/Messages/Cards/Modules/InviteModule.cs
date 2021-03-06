using System.Diagnostics;

namespace KaiHeiLa;

/// <summary>
///     邀请模块
/// </summary>
/// <remarks>
///     提供服务器邀请/语音频道邀请
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class InviteModule : IModule
{
    internal InviteModule(string code)
    {
        Code = code;
    }

    /// <inheritdoc />
    public ModuleType Type => ModuleType.Invite;

    public string Code { get; internal set; }
    
    private string DebuggerDisplay => $"{Type}: {Code}";
}