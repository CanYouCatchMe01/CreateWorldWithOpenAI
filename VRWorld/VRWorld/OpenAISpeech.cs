using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using OpenAI_API.Completions;
using StereoKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VRWorld
{
    internal class OpenAISpeech
    {
        static string myAIStartText;

        class Command
        {
            public string myUserText = "";
            public string myAIJsonText = "";
        };

        //A list of the previous commands, the size is around 6, becuase don't want to store too much
        static List<Command> myAIHistory = new List<Command>();
        public static string mySpeechText { get; private set; } //Just for debugging

        static string myStartSequence = "\njson:";
        static string myRestartSequence = "\ntext:";
        
        static SpeechRecognizer mySpeechRecognizer;
        static Task<CompletionResult> myGenerateTask = null;
        static KeywordRecognitionModel myKeywordModel;

        public static void Start()
        {
            //Secrets which are not in the repo. Right click on C# project in Solution Explorer -> Manage User Secrets -> Add "OPENAI_API_KEY": your_key
            var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
            string openAiKey = config.GetSection("OPENAI_API_KEY").Value;
            string speechKey = config.GetSection("SPEECH_KEY").Value;
            string speechRegion = config.GetSection("SPEECH_REGION").Value;

            //Open AI
            var api = new OpenAI_API.OpenAIAPI(openAiKey);
            myAIStartText = Platform.ReadFileText("Assets/AIStartText.txt");
            
            //Azure speech
            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            speechConfig.SpeechRecognitionLanguage = "en-US";

            myKeywordModel = KeywordRecognitionModel.FromFile("Assets/HeyComputer.table");
            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            //audioConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, "3000");
            mySpeechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

            var phraseList = PhraseListGrammar.FromRecognizer(mySpeechRecognizer);
            phraseList.AddPhrase("create");

            mySpeechRecognizer.Recognizing += (s, e) =>
            {
                mySpeechText = e.Result.Text;
            };

            mySpeechRecognizer.Recognized += (s, e) => //User finished speeching
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    mySpeechText = e.Result.Text;

                    Command command = new Command();
                    command.myUserText = e.Result.Text;

                    var grabDatas = Grabbing.GetGrabDatas();

                    SimpleECS.Entity rightEntity = grabDatas[(int)Handed.Right].myEntity;
                    SimpleECS.Entity leftEntity = grabDatas[(int)Handed.Left].myEntity;

                    if (rightEntity.IsValid())
                    {
                        command.myUserText += $". Grabbing object with right hand";
                    }
                    else
                    {
                        command.myUserText += $". Not grabbing object with right hand";
                    }
                    
                    if (leftEntity.IsValid())
                    {
                        command.myUserText += $". Grabbing object with in left hand";
                    }
                    else
                    {
                        command.myUserText += $". Not grabbing object with left hand";
                    }

                    myAIHistory.Add(command);
                    myGenerateTask = GenerateAIResponce(api);
                    //mySpeechRecognizer.StopKeywordRecognitionAsync();
                }
            };

            mySpeechRecognizer.Canceled += (s, e) =>
            {
                Log.Info($"Speech recognition canceled: {e.Reason}");
            };

            mySpeechRecognizer.StartKeywordRecognitionAsync(myKeywordModel).Wait();
        }

        public static void Update(SimpleECS.World aWorld)
        {
            if (myGenerateTask != null && myGenerateTask.IsCompleted)
            {
                string responce = myGenerateTask.Result.ToString();
                HandleAIResponce(responce, aWorld);

                myAIHistory.Last().myAIJsonText = responce;
                myGenerateTask = null;

                //Don't want to fill up the history to the AI
                int maxCommandSize = 3;
                int commandsToRemoveCount = Math.Max(0, myAIHistory.Count - maxCommandSize); //Don't want the count to be negative
                myAIHistory.RemoveRange(0, commandsToRemoveCount);

                //Start listening again
                mySpeechRecognizer.StartKeywordRecognitionAsync(myKeywordModel).Wait();
            }
        }

        public static string GetTotalAIText()
        {
            string prompt = myAIStartText;

            for (int i = 0; i < myAIHistory.Count; i++)
            {
                prompt += myRestartSequence + myAIHistory[i].myUserText + myStartSequence + myAIHistory[i].myAIJsonText;
            }

            return prompt;
        }

        static async Task<CompletionResult> GenerateAIResponce(OpenAI_API.OpenAIAPI anApi)
        {
            string prompt = GetTotalAIText();

            var request = new CompletionRequest(
                    prompt: prompt,
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

        static void HandleAIResponce(string aResponce, SimpleECS.World aWorld)
        {
            JObject JResponce = JObject.Parse(aResponce);
            JArray JAddObjects = (JArray)JResponce["add objects"];
            JArray JRemove = (JArray)JResponce["remove"];
            JArray JDuplicate = (JArray)JResponce["duplicate"];

            //Add
            if (JAddObjects != null)
            {
                int[] handCount = new int[(int)Handed.Max] { 0, 0 };

                foreach (JObject JObject in JAddObjects)
                {
                    int count = 1;
                    if (JObject.TryGetValue("count", out JToken JCount))
                    {
                        count = (int)JCount;
                    }

                    Handed hand = Handed.Right;
                    if (JObject.TryGetValue("hand", out JToken JHand))
                    {
                        if (JHand.ToString().Equals("left", System.StringComparison.OrdinalIgnoreCase))
                        {
                            hand = Handed.Left;
                        }
                    }

                    Material material = Material.UI;
                    StereoKit.Model model = StereoKit.Model.FromMesh(Mesh.Cube, material);

                    Vec3 scale = Vec3.One * 5.0f * U.cm;
                    StereoKit.Color color = StereoKit.Color.White;

                    JObject.TryGetValue("shape", out JToken JShape);
                    JObject.TryGetValue("color", out JToken JColor);

                    //Mesh
                    if (JShape != null)
                    {
                        string str = JShape.ToString();

                        if (str == "cube")
                        {
                            model = StereoKit.Model.FromMesh(Mesh.Cube, material);
                        }
                        else if (str == "sphere")
                        {
                            model = StereoKit.Model.FromMesh(Mesh.Sphere, material);
                        }
                        else if (str == "cylinder")
                        {
                            Mesh cylinder = Mesh.GenerateCylinder(1.0f, 1.0f, Vec3.Up);
                            model = StereoKit.Model.FromMesh(cylinder, material);
                        }
                        //continue with more meshes
                    }
                    //Color
                    if (JColor != null)
                    {
                        color = JSONConverter.FromJSONColor((JObject)JColor);
                    }

                    for (int i = 0; i < count; i++)
                    {
                        handCount[(int)hand]++;

                        //Pose
                        Pose pose = Pose.Identity;
                        int countOffset = handCount[(int)hand];
                        Vec3 offset = new Vec3(0, 4, -7 * countOffset) * U.cm;
                        Matrix matrixOffset = Matrix.T(offset) * Input.Hand(hand).palm.ToMatrix();
                        pose.position = matrixOffset.Pose.position;

                        aWorld.CreateEntity(pose, model, scale, color, new Grabbable());
                    }
                }
            }

            var grabDatas = Grabbing.GetGrabDatas();
            SimpleECS.Entity rightEntity = grabDatas[(int)Handed.Right].myEntity;
            SimpleECS.Entity leftEntity = grabDatas[(int)Handed.Left].myEntity;

            //Remove
            if (JRemove != null)
            {
                foreach (string hand in JRemove)
                {
                    if (hand == "right" && rightEntity.IsValid())
                    {
                        rightEntity.Destroy();
                    }
                    else if (hand == "left" && leftEntity.IsValid())
                    {
                        leftEntity.Destroy();
                    }
                }
            }

            //Duplicate
            if (JDuplicate != null)
            {
                foreach (string hand in JDuplicate)
                {
                    SimpleECS.Entity entity = new SimpleECS.Entity();

                    if (hand == "right")
                    {
                        entity = rightEntity;
                    }
                    else if (hand == "left")
                    {
                        entity = leftEntity;
                    }

                    if (entity.IsValid())
                    {
                        var components = entity.GetAllComponents();
                        var types = entity.GetAllComponentTypes();
                        int count = entity.GetComponentCount();

                        SimpleECS.Entity entityCopy = aWorld.CreateEntity();

                        //Copy over every component
                        for (int i = 0; i < count; i++)
                        {
                            entityCopy.Set(types[i], components[i]);
                        }
                    }
                }
            }
        }
    }
}
