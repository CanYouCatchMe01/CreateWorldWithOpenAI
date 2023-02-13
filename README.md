# Create world with voice commands
My first OpenAI project. I can place objects in the world with my voice.
I'm using "OpenAI Codex" to generate .json and to convert my voice to text I use "Microsoft Azure Speech-To-Text".

[Youtube demo link](https://youtu.be/7q-3J6OqiMY)
[![Youtube link to demo](img/YoutubeVideoThumbnail.PNG)](https://youtu.be/7q-3J6OqiMY "Youtube link to demo")

## How it works
### 1. Create a world
To display the VR world I use the open source VR game engine StereoKit. Its main programming language in C#. This is just a basic example of how to draw a cube.
```csharp
using StereoKit;

class Program
{
	static void Main(string[] args)
	{
		SK.Initialize(new SKSettings{ appName = "Project" });

		SK.Run(() =>
		{
			Mesh.Cube.Draw(Material.Default, Matrix.S(0.1f));
		});
	}
}
```
### 2. Use the OpenAI API
#### Right prompts
The first thing I have to do is create a start prompt for the AI, which it is going to continue on. In the start prompt you set up the rules for the AI.
```
Create a json block from prompt.
Example:
text:Create a blue cube at position one one one
json:{"id": 0, "position": {"x": 0, "y": 0, "z": -1}, "scale": {"x": 1.0, "y": 1.0, "z": 1.0}, "shape": "cube", "color": {"r": 0.0, "g": 0.0, "b": 1.0}}
text:remove or delete the blue cube
json:{"id": 0, "remove": true}
Real start with id 0:
text:
```
Using [OpenAI playground](https://platform.openai.com/playground) is a good place to test our prompts. I used the Codex Cushman model.
#### API
To use the OpenAI API you first need an API key which can be created under [Personal -> View API keys](https://platform.openai.com/account/api-keys)

Create an `api` object and `async GenerateAIResponce` function which can be run on a different thread. It need to be run on a diffent thread so the program doesn't freeze, when waiting on an response.
```csharp
var api = new OpenAI_API.OpenAIAPI(openAiKey);

static async Task<CompletionResult> GenerateAIResponce(OpenAI_API.OpenAIAPI anApi, string aPrompt)
{
    var request = new CompletionRequest(
            prompt: aPrompt,
            model: OpenAI_API.Models.Model.CushmanCode,
            temperature: 0.1,
            max_tokens: 256,
            top_p: 1.0,
            frequencyPenalty: 0.0,
            presencePenalty: 0.0,
            stopSequences: new string[] { "text:", "json:", "\n" }
            );
    var result = await anApi.Completions.CreateCompletionAsync(request);
    return result;
}
```
The responce you get from OpenAI is a `string` that I convert to a `JSON object`

### 3. Convert speech to text

## Packeges that are used
- [StereoKit](https://github.com/StereoKit/StereoKit) which is an open source VR game engine
- [OpenAI API C#/.NET](https://github.com/OkGoDoIt/OpenAI-API-dotnet) wrapper to make API calls to Open AI
- [Microsoft Azure Speech to text](https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/) to make API calls to to convert speech to text
- [Newtonsoft](https://www.newtonsoft.com/json) which is a JSON framework

## NuGet Links
- [StereoKit](https://www.nuget.org/packages/StereoKit)
- [OpenAI C#/.Net](https://www.nuget.org/packages/OpenAI/)
- [Microsoft Azure Speech to text](https://www.nuget.org/packages/Microsoft.CognitiveServices.Speech/)
- [Newtonsoft](https://www.nuget.org/packages/Newtonsoft.Json)