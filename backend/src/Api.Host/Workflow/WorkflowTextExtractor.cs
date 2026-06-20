using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CohereLoanAndMortgage.Api.Host.Workflow;

internal static class WorkflowTextExtractor
{
    public static string FromAgentResponseBasic(
        AgentResponse response)
    {
        return response.Messages?
            .LastOrDefault(m => m.Role == ChatRole.Assistant)?
            .Text?
            .Trim()
            ?? string.Empty;
    }
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
            AppendMessageContent(builder, message);
        }

        return builder.ToString().Trim();
    }

    private static void AppendMessageContent(StringBuilder builder, ChatMessage message)
    {
        string messageText = message.Text;
        if (!string.IsNullOrWhiteSpace(messageText))
        {
            builder.Append('[')
                .Append(message.Role)
                .Append("] ")
                .AppendLine(messageText);
        }

        if (message.Contents is null)
        {
            if (string.IsNullOrWhiteSpace(messageText))
            {
                builder.Append('[')
                    .Append(message.Role)
                    .Append("] ")
                    .AppendLine();
            }

            return;
        }

        bool wroteRolePrefix = !string.IsNullOrWhiteSpace(messageText);
        foreach (AIContent content in message.Contents)
        {
            if (content is ErrorContent errorContent)
            {
                if (!wroteRolePrefix)
                {
                    builder.Append('[')
                        .Append(message.Role)
                        .Append("] ");
                    wroteRolePrefix = true;
                }

                builder
                    .Append("Error")
                    .Append(string.IsNullOrWhiteSpace(errorContent.ErrorCode) ? string.Empty : $" ({errorContent.ErrorCode})")
                    .Append(": ")
                    .AppendLine(errorContent.Message);

                if (!string.IsNullOrWhiteSpace(errorContent.Details))
                {
                    builder.AppendLine(errorContent.Details);
                }

                continue;
            }

            if (content is TextContent textContent && !string.IsNullOrWhiteSpace(textContent.Text))
            {
                if (!wroteRolePrefix)
                {
                    builder.Append('[')
                        .Append(message.Role)
                        .Append("] ");
                    wroteRolePrefix = true;
                }

                builder.AppendLine(textContent.Text);
                continue;
            }

            string? rendered = content.ToString();
            if (string.IsNullOrWhiteSpace(rendered))
            {
                continue;
            }

            if (!wroteRolePrefix)
            {
                builder.Append('[')
                    .Append(message.Role)
                    .Append("] ");
                wroteRolePrefix = true;
            }

            builder.AppendLine(rendered);
        }

        if (!wroteRolePrefix)
        {
            builder.Append('[')
                .Append(message.Role)
                .Append("] ")
                .AppendLine();
        }
    }
}
