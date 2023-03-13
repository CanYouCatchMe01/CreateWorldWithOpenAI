using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using OpenAI_API.Completions;
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
            myAIText = "Create a json block from prompt.\nExample:\ntext:create a blue cube\njson:{\"shape\": \"cube\", \"color\": {\"r\": 0.0, \"g\": 0.0, \"b\": 1.0}}\ntext:";

            //Azure speech
            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            speechConfig.SpeechRecognitionLanguage = "en-US";

            myKeywordModel = KeywordRecognitionModel.FromFile("Assets/HeyComputer.table");
            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            audioConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, "3000");
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

            //var obj = new VRWorld.Object(someIdCounter++, JResponce);
            //obj.myScale = Vec3.One * 5.0f * U.cm;

            //Vec3 offset = new Vec3(0, 4, -7) * U.cm;
            //Matrix matrixOffset = Matrix.T(offset) * Input.Hand(Handed.Right).palm.ToMatrix();
            //obj.myPose.position = matrixOffset.Pose.position;

            //someObjects.Add(obj);
        }
    }
}
