using Ardalis.Result;
using lionheart.Model.Report;
using OpenAI.Chat;
using System.Text.Json;

namespace lionheart.Services;

/// <summary>
/// Service for generating report content using AI (OpenAI ChatGPT).
/// </summary>
public class ReportGenerationService : IReportGenerationService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<ReportGenerationService> _logger;

    public ReportGenerationService(ChatClient chatClient, ILogger<ReportGenerationService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<Result<GeneratedReportContent>> GenerateReportContentAsync(IReportContext context)
    {
        try
        {
            var systemPrompt = GetSystemPrompt(context.ReportType);
            var userPrompt = BuildUserPrompt(context);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var completion = await _chatClient.CompleteChatAsync(messages);

            if (completion?.Value?.Content is null || completion.Value.Content.Count == 0)
            {
                return Result<GeneratedReportContent>.Error("Failed to generate report content.");
            }

            var responseText = completion.Value.Content[0].Text;
            
            // Parse the structured response
            var content = ParseReportResponse(responseText, context.ReportType);
            
            return Result<GeneratedReportContent>.Success(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report content for {ReportType}", context.ReportType);
            return Result<GeneratedReportContent>.Error($"Error generating report: {ex.Message}");
        }
    }

    private static string GetSystemPrompt(ReportType reportType)
    {
        var basePrompt = """
            You are an expert athletic performance coach and wellness advisor for the Lionheart training application.
            Your role is to analyze training and wellness data and provide insightful, personalized reports.
            
            Always respond in a structured format with three sections:
            1. SUMMARY: A concise 2-3 paragraph overview of the key findings
            2. INSIGHTS: A list of 3-5 key insights, each with a category, content, and severity (Info/Positive/Warning/Critical)
            3. RECOMMENDATIONS: A list of 2-4 actionable recommendations, each with a title, description, and priority (Low/Medium/High)
            
            Format your response as follows:
            ---SUMMARY---
            [Your summary here]
            ---INSIGHTS---
            [Category]: [Content] | Severity: [Info/Positive/Warning/Critical]
            [Category]: [Content] | Severity: [Info/Positive/Warning/Critical]
            ---RECOMMENDATIONS---
            [Title]: [Description] | Priority: [Low/Medium/High]
            [Title]: [Description] | Priority: [Low/Medium/High]
            
            Be encouraging but honest. Focus on actionable advice.
            """;

        var typeSpecificPrompt = reportType switch
        {
            ReportType.Morning => """
                
                For MORNING REPORTS, focus on:
                - How last night's sleep quality may affect today's performance
                - Readiness indicators and what they mean for training intensity
                - Mental preparation suggestions based on the data
                - Whether to proceed with planned training or consider modifications
                - Recovery status from previous training
                """,
            
            ReportType.Evening => """
                
                For EVENING REPORTS, focus on:
                - Summarizing the day's activities and training
                - Recovery strategies for tonight
                - What went well and what could improve
                - Preparation tips for tomorrow's training
                - Stress management if needed
                """,
            
            ReportType.TrainingSession => """
                
                For TRAINING SESSION REPORTS, focus on:
                - If it was a GOOD session: Explain what factors contributed to success (sleep, readiness, recovery time, etc.)
                - If it was a BAD session: Analyze what may have caused it (poor sleep, high stress, insufficient recovery, etc.)
                - Personal records achieved and their significance
                - Technical or form observations based on performance
                - Programming adjustments to consider
                - Signs that may have predicted the session quality beforehand
                """,
            
            _ => string.Empty
        };

        return basePrompt + typeSpecificPrompt;
    }

    private static string BuildUserPrompt(IReportContext context)
    {
        var header = context.ReportType switch
        {
            ReportType.Morning => "Please analyze the following morning data and generate a report:",
            ReportType.Evening => "Please analyze the following evening data and generate a report:",
            ReportType.TrainingSession => "Please analyze the following training session data and generate a report:",
            _ => "Please analyze the following data and generate a report:"
        };

        return $"{header}\n\n{context.ToPromptContext()}";
    }

    private static GeneratedReportContent ParseReportResponse(string response, ReportType reportType)
    {
        var content = new GeneratedReportContent();
        var insights = new List<ReportInsight>();
        var recommendations = new List<ReportRecommendation>();

        try
        {
            // Parse summary
            var summaryStart = response.IndexOf("---SUMMARY---", StringComparison.OrdinalIgnoreCase);
            var insightsStart = response.IndexOf("---INSIGHTS---", StringComparison.OrdinalIgnoreCase);
            var recommendationsStart = response.IndexOf("---RECOMMENDATIONS---", StringComparison.OrdinalIgnoreCase);

            string summary;
            if (summaryStart >= 0 && insightsStart > summaryStart)
            {
                summary = response[(summaryStart + 13)..insightsStart].Trim();
            }
            else
            {
                // Fallback: use first paragraph as summary
                var paragraphs = response.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
                summary = paragraphs.Length > 0 ? paragraphs[0].Trim() : response.Trim();
            }

            // Parse insights
            if (insightsStart >= 0 && recommendationsStart > insightsStart)
            {
                var insightsSection = response[(insightsStart + 14)..recommendationsStart].Trim();
                var insightLines = insightsSection.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in insightLines)
                {
                    var insight = ParseInsightLine(line);
                    if (insight is not null)
                        insights.Add(insight);
                }
            }

            // Parse recommendations
            if (recommendationsStart >= 0)
            {
                var recommendationsSection = response[(recommendationsStart + 21)..].Trim();
                var recommendationLines = recommendationsSection.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in recommendationLines)
                {
                    var recommendation = ParseRecommendationLine(line);
                    if (recommendation is not null)
                        recommendations.Add(recommendation);
                }
            }

            // Ensure we have at least some content
            if (insights.Count == 0)
            {
                insights.Add(new ReportInsight
                {
                    Category = "General",
                    Content = "Report generated successfully.",
                    Severity = InsightSeverity.Info
                });
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add(new ReportRecommendation
                {
                    Title = "Continue Training",
                    Description = "Keep up your current training routine.",
                    Priority = RecommendationPriority.Medium
                });
            }

            return new GeneratedReportContent
            {
                Summary = summary,
                Insights = insights,
                Recommendations = recommendations
            };
        }
        catch
        {
            // If parsing fails, return basic content
            return new GeneratedReportContent
            {
                Summary = response.Length > 500 ? response[..500] + "..." : response,
                Insights = [new ReportInsight
                {
                    Category = "General",
                    Content = "Report generated.",
                    Severity = InsightSeverity.Info
                }],
                Recommendations = [new ReportRecommendation
                {
                    Title = "Review Data",
                    Description = "Please review the full report for details.",
                    Priority = RecommendationPriority.Medium
                }]
            };
        }
    }

    private static ReportInsight? ParseInsightLine(string line)
    {
        // Expected format: [Category]: [Content] | Severity: [Severity]
        try
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 1) return null;

            var mainPart = parts[0];
            var colonIndex = mainPart.IndexOf(':');
            if (colonIndex < 0) return null;

            var category = mainPart[..colonIndex].Trim().TrimStart('-', '*', ' ');
            var content = mainPart[(colonIndex + 1)..].Trim();

            var severity = InsightSeverity.Info;
            if (parts.Length > 1)
            {
                var severityPart = parts[1].ToLowerInvariant();
                if (severityPart.Contains("positive"))
                    severity = InsightSeverity.Positive;
                else if (severityPart.Contains("warning"))
                    severity = InsightSeverity.Warning;
                else if (severityPart.Contains("critical"))
                    severity = InsightSeverity.Critical;
            }

            return new ReportInsight
            {
                Category = category,
                Content = content,
                Severity = severity
            };
        }
        catch
        {
            return null;
        }
    }

    private static ReportRecommendation? ParseRecommendationLine(string line)
    {
        // Expected format: [Title]: [Description] | Priority: [Priority]
        try
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 1) return null;

            var mainPart = parts[0];
            var colonIndex = mainPart.IndexOf(':');
            if (colonIndex < 0) return null;

            var title = mainPart[..colonIndex].Trim().TrimStart('-', '*', ' ');
            var description = mainPart[(colonIndex + 1)..].Trim();

            var priority = RecommendationPriority.Medium;
            if (parts.Length > 1)
            {
                var priorityPart = parts[1].ToLowerInvariant();
                if (priorityPart.Contains("high"))
                    priority = RecommendationPriority.High;
                else if (priorityPart.Contains("low"))
                    priority = RecommendationPriority.Low;
            }

            return new ReportRecommendation
            {
                Title = title,
                Description = description,
                Priority = priority
            };
        }
        catch
        {
            return null;
        }
    }
}
