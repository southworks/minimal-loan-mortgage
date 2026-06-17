using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CohereLoanAndMortgage.Api.Host.Workflow;

internal static class WorkflowTextExtractor
{
    public static string FromAgentResponse(AgentResponse response)
    {
        if (response.Messages is { Count: > 0 })
        {
            return FromChatMessages(response.Messages);
        }

        return response.ToString() ?? string.Empty;
    }

    public static string FromChatMessages(IEnumerable<ChatMessage> messages)
    {
        var builder = new StringBuilder();

        foreach (ChatMessage message in messages)
        {
            builder.Append('[')
                .Append(message.Role)
                .Append("] ")
                .AppendLine(message.Text);

            if (message.Contents is not null)
            {
                foreach (AIContent content in message.Contents)
                {
                    if (!string.IsNullOrWhiteSpace(content.ToString()))
                    {
                        builder.AppendLine(content.ToString());
                    }
                }
            }
        }

        return builder.ToString().Trim();
    }
}
