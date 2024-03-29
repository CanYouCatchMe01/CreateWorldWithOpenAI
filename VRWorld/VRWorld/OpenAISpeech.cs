﻿using GroqApiLibrary;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using SimpleECS;
using StereoKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VRWorld
{
    internal class OpenAISpeech
    {
        static List<JObject> myStartChat = new List<JObject>();
        //A list of the previous chat messages, the size is around 6, becuase don't want to store too much
        static List<JObject> myHistoryChat = new List<JObject>();

        public static string mySpeechText { get; private set; } //Just for debugging. Displaying what the user says

        static SpeechRecognizer mySpeechRecognizer;
        static Task<JObject> myGenerateTask = null;
        static KeywordRecognitionModel myKeywordModel;

        public static void Start()
        {
            //Secrets which are not in the repo. Right click on C# project in Solution Explorer -> Manage User Secrets -> Add "OPENAI_API_KEY": your_key
            var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
            string groqAiKey = config.GetSection("GROQ_API_KEY").Value;
            string speechKey = config.GetSection("SPEECH_KEY").Value;
            string speechRegion = config.GetSection("SPEECH_REGION").Value;

            //Open AI
            GroqApiClient api = new GroqApiClient(groqAiKey);
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
            phraseList.AddPhrase("object");
            phraseList.AddPhrase("cube");
            phraseList.AddPhrase("sphere");
            phraseList.AddPhrase("cylinder");
            phraseList.AddPhrase("right");
            phraseList.AddPhrase("left");
            phraseList.AddPhrase("hand");

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

                    myHistoryChat.Add(new JObject
                    {
                        ["role"] = "user",
                        ["content"] = chatText
                    });

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
                string responce = myGenerateTask.Result?["choices"]?[0]?["message"]?["content"].ToString();
                HandleAIResponce(responce, aWorld);
                myGenerateTask = null;

                myHistoryChat.Add(new JObject
                {
                    ["role"] = "assistant",
                    ["content"] = responce
                });

                //Don't want to fill up the history to the AI. Removing multiple 2, the User and the Assistant.
                int maxCommandSize = 2 * 2;
                int commandsToRemoveCount = Math.Max(0, myHistoryChat.Count - maxCommandSize); //Don't want the count to be negative
                myHistoryChat.RemoveRange(0, commandsToRemoveCount);

                //Start listening again
                mySpeechRecognizer.StartKeywordRecognitionAsync(myKeywordModel).Wait();
            }
        }

        public static List<JObject> GetTotalAIChat()
        {
            List<JObject> totalChat = myStartChat.Concat(myHistoryChat).ToList();
            return totalChat;
        }

        public static string GetTotalAIChatString(int lastMessageNumber)
        {
            List<JObject> totalChat = GetTotalAIChat();
            List<JObject> lastChat = totalChat.Skip(totalChat.Count() - lastMessageNumber).ToList();

            string chatString = "";
            foreach (var chat in lastChat)
            {
                chatString += chat["role"].ToString() + ": " + chat["content"].ToString() + "\n";
            }

            return chatString;
        }

        //Adding a bunch of messages so the AI understand the rules
        static void CreateStartChat()
        {
            string stop = " json end";

            myStartChat.Add(new JObject
            {
                ["role"] = "system",
                ["content"] = @"Convert the user message to JSON. Only respond with the JSON object."
            });

            //Add
            myStartChat.Add(new JObject
            {
                ["role"] = "user",
                ["content"] = @"create three orange cube cubes and two white spheres on my left hand"
            });
            myStartChat.Add(new JObject
            {
                ["role"] = "assistant",
                ["content"] = @"{""add objects"": [{""count"": 3, ""hand"": ""right"", ""shape"": ""cube"", ""color"": ""#ffa500""}, {""count"": 2, ""hand"": ""left"", ""shape"": ""sphere"", ""color"": ""#ffffff""}]}" + stop
            });
            //Remove
            myStartChat.Add(new JObject
            {
                ["role"] = "user",
                ["content"] = @"Remove the object in the right hand"
            });
            myStartChat.Add(new JObject
            {
                ["role"] = "assistant",
                ["content"] = @"{""remove"" : [""right""]}" + stop
            });
            //Duplicate
            myStartChat.Add(new JObject
            {
                ["role"] = "user",
                ["content"] = @"copy the object in my left hand"
            });
            myStartChat.Add(new JObject
            {
                ["role"] = "assistant",
                ["content"] = @"{""duplicate"": [""left""]}" + stop
            });
        }

        static async Task<JObject> GenerateAIResponce(GroqApiClient anApi)
        {
            List<JObject> totalChat = GetTotalAIChat();

            //convert List<JObject> totalChat to JArray
            JArray totalChatJArray = new JArray();
            foreach (var chat in totalChat)
            {
                totalChatJArray.Add(chat);
            }

            JObject request = new JObject
            {
                ["model"] = "llama2-70b-4096",
                ["messages"] = totalChatJArray,
                ["temperature"] = 0.7,
                ["max_tokens"] = 1024,
                ["top_p"] = 1.0,
                ["stop"] = "json end",
            };


            var result = await anApi.CreateChatCompletionAsync(request);
            return result;
        }

        public static Color HexToRGBA(string hexColor)
        {
            if(hexColor.Length < 6 || hexColor.Length > 7)
            {
                Log.Err("Hex color must be 6 characters long");
                return Color.Black;
            }

            // Remove leading hash character, if it exists
            if (hexColor.StartsWith("#"))
            {
                hexColor = hexColor.Substring(1);
            }

            // Parse the hex color string into three separate hex strings
            string hexRed = hexColor.Substring(0, 2);
            string hexGreen = hexColor.Substring(2, 2);
            string hexBlue = hexColor.Substring(4, 2);

            // Convert each hex string into a byte value
            byte r = Convert.ToByte(hexRed, 16);
            byte g = Convert.ToByte(hexGreen, 16);
            byte b = Convert.ToByte(hexBlue, 16);

            // Divide each byte value by 255 to get a value between 0 and 1
            float R = (float)r / 255.0f;
            float G = (float)g / 255.0f;
            float B = (float)b / 255.0f;

            return new Color(R, G, B, 1);
        }

        static void Duplicate(SimpleECS.Entity anEntity, SimpleECS.World aWorld)
        {
            if (anEntity.IsValid())
            {
                var components = anEntity.GetAllComponents();
                var types = anEntity.GetAllComponentTypes();
                int count = anEntity.GetComponentCount();

                SimpleECS.Entity entityCopy = aWorld.CreateEntity();

                //Copy over every component
                for (int i = 0; i < count; i++)
                {
                    entityCopy.Set(types[i], components[i]);
                }
            }
        }

        static async void HandleAIResponce(string aResponce, SimpleECS.World aWorld)
        {
            string splitResponse = "";

            int depth = 0;
            int start_char = 0;
            for (int i = 0; i < aResponce.Length; i++)
            {
                if (aResponce[i] == '{')
                {
                    if (depth == 0)
                    {
                        start_char = i;
                    }

                    depth += 1;
                } else if (aResponce[i] == '}')
                {
                    depth -= 1;
                    if (depth == 0)
                    {
                        splitResponse = aResponce.Substring(start_char, i - start_char + 1);
                        break;
                    }
                }
            }

            JObject JResponce;

            //If the text is not JSON, return
            try
            {
                JResponce = JObject.Parse(splitResponse);
            }
            catch (Exception anException)
            {
                Log.Err($"Error parsing JSON: {anException.Message}");
                return;
            }

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
                        color = HexToRGBA(JColor.ToString());
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
                    else if (hand == "both" && rightEntity.IsValid() && leftEntity.IsValid())
                    {
                        rightEntity.Destroy();
                        leftEntity.Destroy();
                    }
                }
            }

            //Duplicate
            if (JDuplicate != null)
            {
                foreach (string hand in JDuplicate)
                {
                    if (hand == "right")
                    {
                        Duplicate(rightEntity, aWorld);
                    }
                    else if (hand == "left")
                    {
                        Duplicate(leftEntity, aWorld);
                    }
                    else if (hand == "both")
                    {
                        Duplicate(rightEntity, aWorld);
                        Duplicate(leftEntity, aWorld);
                    }
                }
            }
        }
    }
}
