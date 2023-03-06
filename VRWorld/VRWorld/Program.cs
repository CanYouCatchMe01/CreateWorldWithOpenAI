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

            Pose debugWindowPose = new Pose(0.4f, 0.09f, -0.32f, Quat.LookDir(-0.7f, 0.09f, 0.71f));
            string debugText = "";

            Matrix[] grabedOffsets = new Matrix[(int)Handed.Max] { Matrix.Identity, Matrix.Identity };
            int[] grabedIds = new int[(int)Handed.Max] { -1, -1 };

            // Core application loop
            while (SK.Step(() =>
            {
                debugText = ""; //clear
                if (SK.System.displayType == Display.Opaque)
                    Default.MeshCube.Draw(floorMaterial, floorTransform);

                foreach(VRWorld.Object o in objects)
                {
                    for (int h = 0; h < (int)Handed.Max; h++)
                    {
                        Bounds bounds = o.myModel.Bounds;
                        bounds.dimensions *= o.myScale * 1.5f;
                        bounds.center += o.myPose.position;

                        Hand hand = Input.Hand((Handed)h);

                        //Start grabing
                        if (hand.IsJustPinched && bounds.Contains(hand.pinchPt))
                        {
                            Vec3 posOffset = o.myPose.position - hand.pinchPt;
                            Quat rotOffset = hand.palm.orientation.Inverse * o.myPose.orientation;
                            
                            grabedOffsets[(int)h] = Matrix.TR(posOffset, rotOffset);
                            grabedIds[(int)h] = o.myId;
                        }
                        //Move the grabed object
                        if (hand.IsPinched && grabedIds[h] != -1)
                        {
                            Matrix current = Matrix.TR(hand.pinchPt, hand.palm.orientation);
                            Matrix newMatrix = current * grabedOffsets[(int)h];
                            o.myPose = newMatrix.Pose;

                            debugText += "pose: " + o.myPose + "\n";
                            debugText += "pos: " + o.myPose.position + "\n";
                            debugText += "rot: " + o.myPose.orientation + "\n";
                        }
                        //Ungrab the object
                        else if (hand.IsJustUnpinched)
                        {
                            grabedIds[(int)h] = -1;
                        }
                    }

                    o.Draw();
                }

                UI.WindowBegin("Debug window", ref debugWindowPose, new Vec2(30, 0) * U.cm);
                
                UI.Text(debugText);
                UI.WindowEnd();
            }));
            SK.Shutdown();
        }
    }
}
