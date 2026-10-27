using System;
using BunnyOS.Domain.Common;

namespace BunnyOS.Domain.Entities;

public class AiUsageRecord : BaseEntity
{
    public Guid? TenantId { get; set; }
    public string ModelName { get; set; } = "NLP Offline"; // GPT-4o, GPT-4o mini, Gemini, NLP Offline
    public string ToolName { get; set; } = string.Empty;
    public string UserPromptSnippet { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostBdt { get; set; }
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
