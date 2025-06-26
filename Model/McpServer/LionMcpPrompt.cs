using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using lionheart.Model.DTOs;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;

namespace Model.McpServer
{
    /// <summary>
    /// Aggregates multiple prompt sections into a single chat prompt message
    /// for the LLM, including user context and any fetched data (Oura, wellness).
    /// </summary>
    public class LionMcpPrompt
    {
        /// <summary>
        /// The ASP.NET Identity user for whom the prompt is generated.
        /// </summary>
        public required IdentityUser User { get; set; }

        /// <summary>
        /// The role under which the system message will be sent (usually System).
        /// </summary>
        public ChatRole Role { get; set; } = ChatRole.System;

        /// <summary>
        /// The ordered list of prompt sections that compose the full message.
        /// </summary>
        public List<IPromptSection> Sections { get; set; } = new();

        /// <summary>
        /// Converts the assembled prompt into a single <see cref="ChatMessage"/>
        /// for submission to the chat client.
        /// </summary>
        public List<ChatMessage> ToChatMessage()
        {
            return new List<ChatMessage>
            {
                new ChatMessage(Role, ComposeContent())
            };
        }

        /// <summary>
        /// Converts the assembled prompt into a single string.
        /// </summary>
        public string ToStringPrompty()
        {
            return ComposeContent();
        }

        /// <summary>
        /// Renders all sections plus the user-ID footer into a single string.
        /// </summary>
        private string ComposeContent()
        {
            var content = string.Join(
                "\n\n",
                Sections.Select(section => section.ToString()));

            var footer = $"Active User Context:\n" +
                         $"• ID: {User.Id}\n" +
                         "[Use this identifier when invoking tools or services]";

            return $"{content}\n\n{footer}";
        }

        /// <summary>
        /// Adds an Oura data section by fetching the user's daily Oura data
        /// over the given date range.
        /// </summary>
        /// <param name="ouraService">The service to retrieve Oura data.</param>
        /// <param name="range">The date range for which to fetch Oura data.</param>
        public async Task AddOuraDataSectionAsync(
            IOuraService ouraService,
            DateRangeRequest range,
            CancellationToken cancellationToken = default)
        {
            var section = new OuraDataPromptSection(ouraService);
            await section.LoadDataAsync(User, range, cancellationToken);
            Sections.Add(section);
        }

        /// <summary>
        /// Adds a wellness data section by fetching the user's wellness
        /// states over the given date range.
        /// </summary>
        /// <param name="wellnessService">The service to retrieve wellness data.</param>
        /// <param name="range">The date range for which to fetch wellness states.</param>
        public async Task AddWellnessDataSectionAsync(
            IWellnessService wellnessService,
            DateRangeRequest range,
            CancellationToken cancellationToken = default)
        {
            var section = new WellnessDataPromptSection(wellnessService);
            await section.LoadDataAsync(User, range, cancellationToken);
            Sections.Add(section);
        }


       
    }
    

    /// <summary>
    /// Defines a named section of a prompt that can render its content as text.
    /// </summary>
    public interface IPromptSection
    {
        /// <summary>
        /// The heading or title of this prompt section.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// The textual content of this section.
        /// </summary>
        string Content { get; }

        /// <summary>
        /// Returns the display text, including the section title and its content.
        /// </summary>
        /// <returns>The formatted section string.</returns>
        string ToString();
    }

    /// <summary>
    /// A simple instruction list section: groups related instructions
    /// under a single heading.
    /// </summary>
    public class InstructionPromptSection : IPromptSection
    {
        /// <inheritdoc/>
        public required string Name { get; set; }

        private readonly List<string> _instructions = new();

        /// <inheritdoc/>
        public string Content => string.Join("\n", _instructions);

        /// <summary>
        /// Adds a single line of instruction to this section.
        /// </summary>
        /// <param name="instruction">The instruction text to include.</param>
        public void AddInstruction(string instruction)
        {
            if (!string.IsNullOrWhiteSpace(instruction))
                _instructions.Add(instruction);
        }

        /// <inheritdoc/>
        public override string ToString()
            => $"{Name}:\n{Content}";
    }

    /// <summary>
    /// A prompt section that fetches and displays Oura sleep/readiness data
    /// for the user over a specified date range.
    /// </summary>
    public class OuraDataPromptSection : IPromptSection
    {
        private readonly IOuraService _ouraService;

        /// <inheritdoc/>
        public string Name { get; set; } = "Oura Data";

        /// <inheritdoc/>
        public string Content { get; private set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance with the given Oura service.
        /// </summary>
        public OuraDataPromptSection(IOuraService ouraService)
            => _ouraService = ouraService;

        /// <summary>
        /// Fetches the Oura data and serializes it into this section's content.
        /// </summary>
        /// <param name="user">The user whose data to fetch.</param>
        /// <param name="range">The date range for the data.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public async Task LoadDataAsync(
            IdentityUser user,
            DateRangeRequest range,
            CancellationToken cancellationToken = default)
        {
            var result = await _ouraService
                .GetDailyOuraInfoRangeAsync(user, range);
            Content = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <inheritdoc/>
        public override string ToString()
            => $"{Name}:\n{Content}";
    }

    /// <summary>
    /// A prompt section that fetches and displays user wellness states
    /// over a specified date range.
    /// </summary>
    public class WellnessDataPromptSection : IPromptSection
    {
        private readonly IWellnessService _wellnessService;

        /// <inheritdoc/>
        public string Name { get; set; } = "Wellness Data";

        /// <inheritdoc/>
        public string Content { get; private set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance with the given wellness service.
        /// </summary>
        public WellnessDataPromptSection(IWellnessService wellnessService)
            => _wellnessService = wellnessService;

        /// <summary>
        /// Fetches the wellness data and serializes it into this section's content.
        /// </summary>
        /// <param name="user">The user whose data to fetch.</param>
        /// <param name="range">The date range for the data.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public async Task LoadDataAsync(
            IdentityUser user,
            DateRangeRequest range,
            CancellationToken cancellationToken = default)
        {
            var result = await _wellnessService
                .GetWellnessStatesMCP(user.Id, range);
            Content = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <inheritdoc/>
        public override string ToString()
            => $"{Name}:\n{Content}";
    }



}