using StereoKit;
using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenAI_API.Completions;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography.X509Certificates;

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

            //Secrets which are not in the repo. Right click on C# project in Solution Explorer -> Manage User Secrets -> Add "OPENAI_API_KEY": your_key
            var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
            string openAiKey = config.GetSection("OPENAI_API_KEY").Value;

            //Open AI
            var api = new OpenAI_API.OpenAIAPI(openAiKey);
            string aiText = "Create a json block from prompt.\nExample:\ntext:Create a blue cube at position zero zero zero\njson:{\"id\": 0, \"position\": {\"x\": 0, \"y\": 0, \"z\": 0}, \"scale\": {\"x\": 1.0, \"y\": 1.0, \"z\": 1.0}, \"shape\": \"cube\", \"color\": {\"r\": 0.0, \"g\": 0.0, \"b\": 1.0}}\ntext:remove or delete the blue cube\njson:{\"id\": 0, \"remove\": true}\nReal start with id 0:\ntext:";
            string startSequence = "\njson:";
            string restartSequence = "\ntext:\n";
            Task<CompletionResult> generateTask = null;

            //Microphone and text
            bool record = true;
            string textInput = "";
            string speechAIText = "";

            //Azure speech to text AI
            string speechKey = config.GetSection("SPEECH_KEY").Value;
            string speechRegion = config.GetSection("SPEECH_REGION").Value;

            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            speechConfig.SpeechRecognitionLanguage = "en-US";

            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

            speechRecognizer.Recognizing += (s, e) =>
            {
                speechAIText = e.Result.Text;
            };

            speechRecognizer.Recognized += (s, e) =>
            {
                textInput += speechAIText;
                speechAIText = "";
            };

            Action checkRecordMic = () =>
            {
                if (record)
                {
                    speechRecognizer.StartContinuousRecognitionAsync().Wait();
                }
                else
                {
                    speechRecognizer.StopContinuousRecognitionAsync().Wait();
                }
            };
            checkRecordMic();

            //GameObjects are stored in a list
            int myIdCounter = 0;
            List<VRWorld.Object> objects = new List<VRWorld.Object>();

            Matrix floorTransform = Matrix.TS(0, -1.5f, 0, new Vec3(30, 0.1f, 30));
            Material floorMaterial = new Material(Shader.FromFile("floor.hlsl"));
            floorMaterial.Transparency = Transparency.Blend;

            JObject someData = new JObject();
            //position
            JObject position = new JObject();
            position.Add("x", 0);
            position.Add("y", 0);
            position.Add("z", -0.3f);
            someData.Add("position", position);
            //scale
            JObject scale = new JObject();
            scale.Add("x", 5.0f * U.cm);
            scale.Add("y", 5.0f * U.cm);
            scale.Add("z", 5.0f * U.cm);
            someData.Add("scale", scale);
            //color
            JObject color = new JObject();
            color.Add("r", 1.0f);
            color.Add("g", 1.0f);
            color.Add("b", 0.0f);
            someData.Add("color", color);
            //shape
            someData.Add("shape", "cube");

            //Create a cube
            objects.Add(new VRWorld.Object(myIdCounter++, someData));
            objects.Add(new VRWorld.Object(myIdCounter++, someData));

            //Cordinate system
            ScalingCoordinateSystem scalingCoordinateSystem = new ScalingCoordinateSystem();

            //Debug window
            Pose debugWindowPose = new Pose(0.4f, 0.09f, -0.32f, Quat.LookDir(-0.7f, 0.09f, 0.71f));
            string debugText = "";

            //Grabbing
            Matrix[] grabedOffsets = new Matrix[(int)Handed.Max] { Matrix.Identity, Matrix.Identity };
            int[] grabedIndexs = new int[(int)Handed.Max] { -1, -1 };
            Handed scalingHand = Handed.Max; //Max is like invalid
            Vec3 startScale = Vec3.Zero;
            float startScalingDistance = 0.0f;

            // Core application loop
            while (SK.Step(() =>
            {
                debugText = ""; //clear
                if (SK.System.displayType == Display.Opaque)
                    Default.MeshCube.Draw(floorMaterial, floorTransform);

                //Seeing which object that is grabed
                for (Handed h = 0; h < Handed.Max; h++)
                {
                    Hand hand = Input.Hand(h);
                    Handed otherHand = h == Handed.Right ? Handed.Left : Handed.Right;
                    Matrix handMatrix = Matrix.TR(hand.pinchPt, hand.palm.orientation);

                    int otherGrabedIndex = grabedIndexs[(int)otherHand];

                    for (int i = 0; i < objects.Count; i++)
                    {
                        Bounds bounds = objects[i].myModel.Bounds;
                        bounds.dimensions *= objects[i].myScale * 1.5f;
                        bounds.center += objects[i].myPose.position;

                        if (hand.IsJustPinched && bounds.Contains(hand.pinchPt))
                        {
                            if (otherGrabedIndex == i) //Scaling with other hand
                            {
                                scalingHand = h;
                                startScale = objects[i].myScale;
                                startScalingDistance = (Input.Hand(Handed.Left).pinchPt - Input.Hand(Handed.Right).pinchPt).Length;
                            }
                            else //Grabbing with first hand
                            {
                                grabedIndexs[(int)h] = i;
                                grabedOffsets[(int)h] = objects[i].myPose.ToMatrix() * handMatrix.Inverse;
                            }
                            break;
                        }
                    }

                    //Move the grabed object
                    if (hand.IsPinched && grabedIndexs[(int)h] != -1)
                    {
                        Matrix newMatrix = grabedOffsets[(int)h] * handMatrix;
                        objects[grabedIndexs[(int)h]].myPose = newMatrix.Pose;

                        //debugText += "pos offset" + grabedOffsets[(int)h].Pose.position + "\n";
                        //debugText += "rot offset" + grabedOffsets[(int)h].Pose.orientation + "\n";
                    }
                    //Ungrab the object
                    else if (hand.IsJustUnpinched)
                    {
                        grabedIndexs[(int)h] = -1;
                        scalingHand = Handed.Max;
                    }

                    //debugText += "scaling hand" + scalingHand + "\n";
                }

                if (scalingHand != Handed.Max)
                {
                    float currentScalingDistance = (Input.Hand(Handed.Left).pinchPt - Input.Hand(Handed.Right).pinchPt).Length;

                    float scaleFactor = currentScalingDistance / startScalingDistance;

                    debugText += "currentDistance" + startScalingDistance + "\n";
                    debugText += "startDistance" + currentScalingDistance + "\n";
                    debugText += "scaleFactor" + scaleFactor + "\n";

                    Handed grabingHand = scalingHand == Handed.Right ? Handed.Left : Handed.Right;
                    int grabingIndex = grabedIndexs[(int)grabingHand];

                    objects[grabingIndex].myScale = startScale * scaleFactor;
                }

                //Cordinate system
                if (grabedIndexs[(int)Handed.Left] != -1 && grabedIndexs[(int)Handed.Right] == -1)
                {
                    int oneGrabedIndex = grabedIndexs[(int)Handed.Left];
                    scalingCoordinateSystem.Draw(objects[oneGrabedIndex].myPose, Handed.Left);
                }
                else if (grabedIndexs[(int)Handed.Left] == -1 && grabedIndexs[(int)Handed.Right] != -1)
                {
                    int oneGrabedIndex = grabedIndexs[(int)Handed.Right];
                    scalingCoordinateSystem.Draw(objects[oneGrabedIndex].myPose, Handed.Right);
                }

                //Draw the object
                for (int i = 0; i < objects.Count; i++)
                {
                    objects[i].Draw();
                }

                //Debug window
                UI.WindowBegin("Debug window", ref debugWindowPose, new Vec2(30, 0) * U.cm);
                UI.Text(debugText);
                UI.WindowEnd();
            }));
            SK.Shutdown();
        }
    }
}
