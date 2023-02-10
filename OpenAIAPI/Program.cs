using OpenAI_API.Completions;
using OpenAI_API.Models;
using System;
using System.Threading.Tasks;

namespace Program
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var api = new OpenAI_API.OpenAIAPI(); //loads the API key from the .openai file that is in the same directory as the .exe

            var task = Task.Run(async () =>
            {
                string text = "Create a json block from prompt.\nExample:\ntext: Create a blue cube at position one one one\njson: {\"id:\" 1, \"position\" : {\"x\" : 1, \"y\" : 1, \"z\" : 1}, \"shape\" : \"cube\", \"color\" : {\"r\" : 0.0, \"g\" : 0.0, \"b\" : 1.0}}\ntext: Create a red sphere at position five four three\njson:";

                var request = new CompletionRequest(
                    prompt: text,
                    model: Model.CushmanCode,
                    temperature: 0.1,
                    max_tokens: 100,
                    top_p: 1.0,
                    frequencyPenalty: 0.0,
                    presencePenalty: 0.0,
                    stopSequences: new string[]{ "text:", "json:", "\n" }
                    );
                var result = await api.Completions.CreateCompletionAsync(request);
                return result;
            });
            while (!task.IsCompleted)
            {
                Console.WriteLine("Waiting for task to complete");
                Task.Delay(1000).Wait();
            }
            Console.WriteLine(task.Result);
        }
    }
}