using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using OpenAI_API.Completions;
using StereoKit;
using System.Threading.Tasks;

namespace VRWorld
{
    internal class OpenAISpeech
    {
        public static string myAIText { get; private set; }
        public static string mySpeechText { get; private set; }

        static string myStartSequence = "\njson:";
        static string myRestartSequence = "\ntext:\n";
        
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
            myAIText = "Create a json block from prompt.\nExample:\ntext: create three blue cube cubes and two white spheres on my left hand\njson:{\"objects\": [{\"count\": 3, \"hand\": \"right\", \"shape\": \"cube\", \"color\": {\"r\": 0.0, \"g\": 0.0, \"b\": 1.0}}, {\"count\": 2, \"hand\": \"left\", \"shape\": \"sphere\", \"color\": {\"r\": 1.0, \"g\": 1.0, \"b\": 1.0}}]}\ntext:";

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
                    myAIText += mySpeechText + myStartSequence;
                    myGenerateTask = GenerateAIResponce(api, myAIText);
                }
            };

            mySpeechRecognizer.StartKeywordRecognitionAsync(myKeywordModel).Wait();
        }

        public static void Update(SimpleECS.World aWorld)
        {
            if (myGenerateTask != null && myGenerateTask.IsCompleted)
            {
                string responce = myGenerateTask.Result.ToString();
                HandleAIResponce(responce, aWorld);
                myAIText += responce + myRestartSequence;
                myGenerateTask = null;

                //Start listening again
                mySpeechRecognizer.StartKeywordRecognitionAsync(myKeywordModel).Wait();
            }
        }

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

        static void HandleAIResponce(string aResponce, SimpleECS.World aWorld)
        {
            JObject JResponce = JObject.Parse(aResponce);
            JArray JObjects = (JArray)JResponce["objects"];

            int[] handCount = new int[(int)Handed.Max] { 0, 0 };

            foreach (JObject JObject in JObjects)
            {
                int count = 1;
                if(JObject.TryGetValue("count", out JToken JCount))
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
                    //Pose
                    Pose pose = Pose.Identity;
                    Vec3 offset = new Vec3(0, 4, -7 * handCount[(int)hand]) * U.cm;
                    Matrix matrixOffset = Matrix.T(offset) * Input.Hand(hand).palm.ToMatrix();
                    pose.position = matrixOffset.Pose.position;
                    
                    aWorld.CreateEntity(pose, model, scale, color, new Grabbable());

                    handCount[(int)hand]++;
                }
            }
        }
    }
}
