using OpenAI_API.Completions;
using OpenAI_API.Models;
using Microsoft.Extensions.Configuration;
using System;

namespace Program
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Secrets which are not in the repo. Right click on C# project in Solution Explorer -> Manage User Secrets -> Add "OPENAI_API_KEY": your_key
            var builder = new ConfigurationBuilder().AddUserSecrets<Program>();
            var configurationRoot = builder.Build();
            string openAiKey = configurationRoot.GetSection("OPENAI_API_KEY").Value;

            var api = new OpenAI_API.OpenAIAPI(openAiKey); //loads the API key from the .openai file that is in the same directory as the .exe

            string text = "Create a json block from prompt.\nExample:\ntext:Create a blue cube at position one one one\njson:{\"id:\" 1, \"position\" : {\"x\" : 1, \"y\" : 1, \"z\" : 1}, \"shape\" : \"cube\", \"color\" : {\"r\" : 0.0, \"g\" : 0.0, \"b\" : 1.0}}\ntext:Create a red sphere at position five four three\njson:";

            var task = GenerateAIResponce(api, text); //this is the async call to the API
            while (!task.IsCompleted)
            {
                Console.WriteLine("Waiting for task to complete");
                Task.Delay(1000).Wait();
            }
            Console.WriteLine(task.Result);
        }
        
        static async Task<string> GenerateAIResponce(OpenAI_API.OpenAIAPI anApi, string aPrompt)
        {
            var request = new CompletionRequest(
                    prompt: aPrompt,
                    model: Model.CushmanCode,
                    temperature: 0.1,
                    max_tokens: 100,
                    top_p: 1.0,
                    frequencyPenalty: 0.0,
                    presencePenalty: 0.0,
                    stopSequences: new string[] { "text:", "json:", "\n" }
                    );
            var result = await anApi.Completions.CreateCompletionAsync(request);
            return result.ToString();
        }
    }
}