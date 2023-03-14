using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;

class Program
{
    static void OutputSpeechRecognitionResult(SpeechRecognitionResult speechRecognitionResult)
    {
        switch (speechRecognitionResult.Reason)
        {
            case ResultReason.RecognizedSpeech:
                Console.WriteLine($"RECOGNIZED: Text={speechRecognitionResult.Text}");
                break;
            case ResultReason.NoMatch:
                Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                break;
            case ResultReason.Canceled:
                var cancellation = CancellationDetails.FromResult(speechRecognitionResult);
                Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                if (cancellation.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                    Console.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                    Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                }
                break;
        }
    }

    static void Main(string[] args)
    {
        //Secrets which are not in the repo. Right click on C# project in Solution Explorer -> Manage User Secrets -> Add "SPEECH_KEY": your_key
        var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
        string speechKey = config.GetSection("SPEECH_KEY").Value;
        string speechRegion = config.GetSection("SPEECH_REGION").Value;

        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = "en-US";

        var keywordModel = KeywordRecognitionModel.FromFile("HeyComputer.table");
        using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        audioConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, "3000");

        using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        var phraseList = PhraseListGrammar.FromRecognizer(speechRecognizer);
        phraseList.AddPhrase("create");

        speechRecognizer.SpeechStartDetected += (s, e) =>
        {
            Console.WriteLine("SpeechStartDetected");
        };

        speechRecognizer.SpeechEndDetected += (s, e) =>
        {
            Console.WriteLine("SpeechEndDetected");
        };


        speechRecognizer.Recognizing += (s, e) =>
        {
            Console.WriteLine($"Recognizing: {e.Result.Text}");
        };

        speechRecognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Console.WriteLine($"Recognized: {e.Result.Text}");
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine("No speech could be recognized.");
            }
            else if (e.Result.Reason == ResultReason.Canceled)
            {
                var cancellation = CancellationDetails.FromResult(e.Result);
                Console.WriteLine($"Canceled, Reason: {cancellation.Reason}");
            }
        };

        speechRecognizer.Canceled += (s, e) =>
        {
            Console.WriteLine($"Canceled, Reason: {e.Reason}");
        };

        speechRecognizer.SessionStarted += (s, e) =>
        {
            Console.WriteLine("Session started event.");
        };

        speechRecognizer.SessionStopped += (s, e) =>
        {
            Console.WriteLine("Session stopped event.");
            Console.WriteLine("\nStop recognition.");
            speechRecognizer.StartKeywordRecognitionAsync(keywordModel).Wait();
        };

        Console.WriteLine("Start recognition.");
        speechRecognizer.StartKeywordRecognitionAsync(keywordModel).Wait();

        Console.WriteLine("Press any key to stop1");
        Console.ReadKey();

        speechRecognizer.StopContinuousRecognitionAsync().Wait();
        Console.WriteLine("Press any key to stop2");
        Console.ReadKey();

    }
}