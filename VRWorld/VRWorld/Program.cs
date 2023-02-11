using StereoKit;
using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenAI_API.Completions;

namespace VRWorld
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Initialize StereoKit
            SKSettings settings = new SKSettings
            {
                appName = "CreateWorldWithAI",
                assetsFolder = "Assets",
            };
            if (!SK.Initialize(settings))
                Environment.Exit(1);

            //Open AI
            var api = new OpenAI_API.OpenAIAPI(); //loads the API key from the .openai file that is in the same directory as the .exe
            string aiText = "Create a json block from prompt.\nExample:\ntext: Create a blue cube at position one one one\njson: {\"id:\" 0, \"position\" : {\"x\" : 1, \"y\" : 1, \"z\" : 1}, \"shape\" : \"cube\", \"color\" : {\"r\" : 0.0, \"g\" : 0.0, \"b\" : 1.0}}\nReal start with id 0:\ntext: ";
            string startSequence = "\njson:";
            string restartSequence = "\ntext:\n";
            string textInput = "";
            Task<CompletionResult> generateTask = null;

            //GameObjects are stored in a list
            int myIdCounter = 0;
            List<VRWorld.Object> objects = new List<VRWorld.Object>();

            var JObj = new JObject();
            JObj.Add("position", VRWorld.JSONConverter.ToJSON(new Vec3(1, 0, -1)));
            JObj.Add("shape", new JValue("cube"));
            JObj.Add("color", VRWorld.JSONConverter.ToJSON(new Color(0.1f, 0.2f, 0.3f)));

            objects.Add(new VRWorld.Object(myIdCounter++, JObj));
            JObj["position"] = VRWorld.JSONConverter.ToJSON(new Vec3(1.0f, 0.2f, 0.3f));
            JObj["color"] = VRWorld.JSONConverter.ToJSON(new Color(1.0f, 0.2f, 0.3f));
            objects.Add(new VRWorld.Object(myIdCounter++, JObj));

            Matrix floorTransform = Matrix.TS(0, -1.5f, 0, new Vec3(30, 0.1f, 30));
            Material floorMaterial = new Material(Shader.FromFile("floor.hlsl"));
            floorMaterial.Transparency = Transparency.Blend;

            Pose windowPose = new Pose(0, 0, -0.5f, Quat.LookDir(0, 0, 1));

            //var task = Task.Run(async () =>
            //{
            //    var api = new OpenAI_API.OpenAIAPI();
            //    var result = await api.Completions.GetCompletion("One Two Three One Two", );
            //});

            // Core application loop
            while (SK.Step(() =>
            {
                if (SK.System.displayType == Display.Opaque)
                    Default.MeshCube.Draw(floorMaterial, floorTransform);

                UI.WindowBegin("Open AI chat", ref windowPose, new Vec2(20, 0) * U.cm);
                UI.Text(aiText);
                UI.Input("Input", ref textInput);
                if (UI.Button("Submit text"))
                {
                    aiText += textInput + startSequence;
                    generateTask = GenerateAIResponce(api, aiText);
                   
                    textInput = ""; //Clear input
                    
                }
                UI.WindowEnd();

                if (generateTask != null && generateTask.IsCompleted)
                {
                    string responce = generateTask.Result.ToString();
                    HandleAIResponce(responce, objects, myIdCounter);
                    aiText += responce + restartSequence;
                    generateTask = null;
                }

                foreach(VRWorld.Object o in objects)
                {
                    o.Draw();
                }
            }));
            SK.Shutdown();
        }

        static async Task<CompletionResult> GenerateAIResponce(OpenAI_API.OpenAIAPI anApi, string aPrompt)
        {
            var request = new CompletionRequest(
                    prompt: aPrompt,
                    model: OpenAI_API.Models.Model.CushmanCode,
                    temperature: 0.1,
                    max_tokens: 100,
                    top_p: 1.0,
                    frequencyPenalty: 0.0,
                    presencePenalty: 0.0,
                    stopSequences: new string[] { "text:", "json:", "\n" }
                    );
            var result = await anApi.Completions.CreateCompletionAsync(request);
            return result;
        }

        static void HandleAIResponce(string aResponce, List<VRWorld.Object> someObjects, int someIdCounter)
        {
            
        }
    }
}
