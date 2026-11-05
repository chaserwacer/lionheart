namespace lionheart.Model.Chat.Tools;

/// <summary>Which tools an exchange is offered.</summary>
/// <remarks>
/// Proposing a change needs somewhere the athlete can answer it. The thread on the phone draws a
/// card for every proposal; a standalone conversation, which the web app also speaks to, has none, so
/// it is offered only the tools that read.
/// </remarks>
public enum ToolAccess
{
    Read,
    ReadAndPropose,
}
