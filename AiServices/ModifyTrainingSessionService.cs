using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using OpenAI.Chat;
using Model.Tools;
using OpenAI;
using Ardalis.Result;


namespace lionheart.Services.AI
{
    public interface IModifyTrainingSessionService
    {
        Task<Result<string>> ModifySessionAsync(IdentityUser user);
    }
    /// <summary>
    /// This service is responsible for modifying training sessions.
    /// It uses the OpenAI API to generate modifications and execute tool calls as needed.
    /// </summary>
    public class ModifyTrainingSessionService : IModifyTrainingSessionService
    {
        private readonly ChatClient _chatClient;
        private readonly IToolCallExecutor _toolCallExecutor;

        public ModifyTrainingSessionService(IConfiguration config, IToolCallExecutor toolCallExecutor)
        {
            // Instantiate a new client in each service instance so different models can be used for different tasks 
            _chatClient = new(model: "gpt-4o", apiKey: config["OpenAI:ApiKey"]);
            _toolCallExecutor = toolCallExecutor;
        }


        public async Task<Result<string>> ModifySessionAsync(IdentityUser user)
        {
            /*
            PROMPTT BUILDING is currently stubbed out - 
              just to test the logic of the ai, the prompt asks the ai to use the tools to retrieve available movement bases.

              in the future this should obviously be replaced with the actual fucntionality for modifying a training session based on users performance and wearables. 
            
            **/




            var prompt = $@"You are an AI assistant tasked with retreiving available movemement bases";

            var tools = OpenAiToolHandler.GetModifyTrainingSessionTools();
            List<ChatMessage> messages = new List<ChatMessage>
            {
                new UserChatMessage(prompt)
            };
            ChatCompletionOptions options = new()
            {

            };
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }


            bool requiresAction;

            do
            {
                requiresAction = false;
                ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        {
                            // Add the assistant message to the conversation history.
                            messages.Add(new AssistantChatMessage(completion));

                            var content = completion.Content[0].Text;
                            if (content is null)
                            {
                                return Result<string>.Error("No content returned from AI model.");
                            }

                            return Result<string>.Success(content);
                        }

                    case ChatFinishReason.ToolCalls:
                        {
                            // First, add the assistant message with tool calls to the conversation history.
                            messages.Add(new AssistantChatMessage(completion));

                            var toolCallResults = await _toolCallExecutor.ExecuteToolCallsAsync(completion.ToolCalls, user);
                            foreach (var result in toolCallResults)
                            {
                                if (!result.IsSuccess)
                                {
                                    return Result<string>.Error(result.Errors.ToString());
                                }
                                messages.Add(result.Value);
                            }

                            requiresAction = true;
                            break;
                        }

                    case ChatFinishReason.Length:
                        return Result<string>.Error(completion.FinishReason.ToString() + ": " + completion.Content.ToString());

                    case ChatFinishReason.ContentFilter:
                        return Result<string>.Error(completion.FinishReason.ToString() + ": " + completion.Content.ToString());
                    default:
                        return Result<string>.Error(completion.FinishReason.ToString() + ": " + completion.FinishReason.ToString());
                }
            } while (requiresAction);
            return Result<string>.Error("Unexpected completion finish reason");
        }





    }
}
