using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using OpenAI_API.Chat;
using StereoKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VRWorld
{
    internal class OpenAISpeech
    {
        static List<ChatMessage> myStartChat = new List<ChatMessage>();
        //A list of the previous chat messages, the size is around 6, becuase don't want to store too much
        static List<ChatMessage> myHistoryChat = new List<ChatMessage>();

        public static string mySpeechText { get; private set; } //Just for debugging. Displaying what the user says
        
        static SpeechRecognizer mySpeechRecognizer;
        static Task<ChatResult> myGenerateTask = null;
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
            CreateStartChat();

            //Azure speech
            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            speechConfig.SpeechRecognitionLanguage = "en-US";

            myKeywordModel = KeywordRecognitionModel.FromFile("Assets/HeyComputer.table");
            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            //audioConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, "3000");
            mySpeechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

            var phraseList = PhraseListGrammar.FromRecognizer(mySpeechRecognizer);
            phraseList.AddPhrase("create");
            phraseList.AddPhrase("three");

            mySpeechRecognizer.Recognizing += (s, e) =>
            {
                mySpeechText = e.Result.Text;
            };

            mySpeechRecognizer.Recognized += (s, e) => //User finished speeching
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    mySpeechText = e.Result.Text; //Debug
                    string chatText = e.Result.Text;

                    var grabDatas = Grabbing.GetGrabDatas();

                    SimpleECS.Entity rightEntity = grabDatas[(int)Handed.Right].myEntity;
                    SimpleECS.Entity leftEntity = grabDatas[(int)Handed.Left].myEntity;

                    if (rightEntity.IsValid())
                    {
                        chatText += $". Grabbing object with right hand";
                    }
                    else
                    {
                        chatText += $". Not grabbing object with right hand";
                    }
                    
                    if (leftEntity.IsValid())
                    {
                        chatText += $". Grabbing object with in left hand";
                    }
                    else
                    {
                        chatText += $". Not grabbing object with left hand";
                    }

                    myHistoryChat.Add(new ChatMessage(ChatMessageRole.User, chatText));
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
                myGenerateTask = null;

                myHistoryChat.Add(new ChatMessage(ChatMessageRole.Assistant, responce));
                
                //Don't want to fill up the history to the AI. Removing multiple 2, the User and the Assistant.
                int maxCommandSize = 2*2;
                int commandsToRemoveCount = Math.Max(0, myHistoryChat.Count - maxCommandSize); //Don't want the count to be negative
                myHistoryChat.RemoveRange(0, commandsToRemoveCount);

                //Start listening again
                mySpeechRecognizer.StartKeywordRecognitionAsync(myKeywordModel).Wait();
            }
        }

        public static List<ChatMessage> GetTotalAIChat()
        {
            List<ChatMessage> totalChat = myStartChat.Concat(myHistoryChat).ToList();
            return totalChat;
        }

        public static string GetTotalAIChatString(int maxLength)
        {
            List<ChatMessage> totalChat = GetTotalAIChat();
            string chatString = "";
            foreach (var chat in totalChat)
            {
                chatString += chat.Role.ToString() + ": " + chat.Content + "\n";
            }

            string showText = chatString.Length > maxLength ? "..." + chatString.Substring(chatString.Length - maxLength) : chatString;

            return showText;
        }

        //Adding a bunch of messages so the AI understand the rules
        static void CreateStartChat()
        {
            myStartChat.Add(new ChatMessage(ChatMessageRole.System, @"Convert the user message to JSON. Just respond with the JSON object"));
            myStartChat.Add(new ChatMessage(ChatMessageRole.User, @"create three blue cube cubes and two white spheres on my left hand"));
            myStartChat.Add(new ChatMessage(ChatMessageRole.Assistant, @"{""add objects"": [{""count"": 3, ""hand"": ""right"", ""shape"": ""cube"", ""color"": {""r"": 0.0, ""g"": 0.0, ""b"": 1.0}}, {""count"": 2, ""hand"": ""left"", ""shape"": ""sphere"", ""color"": {""r"": 1.0, ""g"": 1.0, ""b"": 1.0}}]}"));
            myStartChat.Add(new ChatMessage(ChatMessageRole.User, @"Remove the object in the right hand. Grabbing object with right hand"));
            myStartChat.Add(new ChatMessage(ChatMessageRole.Assistant, @"{""remove"" : [""right""]}"));
            myStartChat.Add(new ChatMessage(ChatMessageRole.User, @"Remove the object in the right hand. Not Grabbing object with right hand"));
            myStartChat.Add(new ChatMessage(ChatMessageRole.Assistant, @"{""remove"" : []}"));
            myStartChat.Add(new ChatMessage(ChatMessageRole.User, @"copy the object in my left hand. Grabbing object with in left hand"));
            myStartChat.Add(new ChatMessage(ChatMessageRole.Assistant, @"{""duplicate"": [""left""]}"));
        }

        static async Task<ChatResult> GenerateAIResponce(OpenAI_API.OpenAIAPI anApi)
        {
            var request = new ChatRequest();
            request.Model = OpenAI_API.Models.Model.ChatGPTTurbo;
            request.Messages = GetTotalAIChat();
            request.Temperature = 0.7;
            request.MaxTokens = 256;
            request.TopP = 1.0;
            request.FrequencyPenalty = 0.0;
            request.PresencePenalty = 0.0;

            var result = await anApi.Chat.CreateChatCompletionAsync(request);
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
