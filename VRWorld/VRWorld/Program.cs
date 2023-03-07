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
            objects.Add(new VRWorld.Object(myIdCounter++, someData));

            Pose debugWindowPose = new Pose(0.4f, 0.09f, -0.32f, Quat.LookDir(-0.7f, 0.09f, 0.71f));
            string debugText = "";

            Matrix[] grabedOffsets = new Matrix[(int)Handed.Max] { Matrix.Identity, Matrix.Identity };
            int[] grabedIndexs = new int[(int)Handed.Max] { -1, -1 };
            Handed scalingHand = Handed.Max; //Max is like invalid
            Vec3 startScale = Vec3.Zero;

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
                            if (otherGrabedIndex == i) //the other grabed object is this object
                            {
                                scalingHand = h;
                                startScale = objects[i].myScale;
                            }
                            else
                            {
                                grabedIndexs[(int)h] = i;
                            }
                            grabedOffsets[(int)h] = objects[i].myPose.ToMatrix() * handMatrix.Inverse;
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
                    Handed grabingHand = scalingHand == Handed.Right ? Handed.Left : Handed.Right;
                    int grabingIndex = grabedIndexs[(int)grabingHand];

                    Hand hand = Input.Hand(scalingHand);
                    Matrix handMatrix = Matrix.TR(hand.pinchPt, hand.palm.orientation);

                    Matrix currentOffset = objects[grabingIndex].myPose.ToMatrix() * handMatrix.Inverse;
                    Matrix startGrabOffset = grabedOffsets[(int)scalingHand];

                    float currentDistance = (currentOffset.Pose.position - grabedOffsets[(int)grabingHand].Pose.position).Length;
                    float startDistance = (startGrabOffset.Pose.position - grabedOffsets[(int)grabingHand].Pose.position).Length;
                    float scaleFactor = currentDistance / startDistance;

                    //Getting how much the user has scaled
                    //Vec3 scaledVector = currentOffset.Pose.position / startGrabOffset.Pose.position;
                    //debugText += "scaledVector" + scaledVector + "\n";
                    //debugText += "currentOffset pos" + currentOffset.Pose.position + "\n";
                    //debugText += "startGrabOffset pos" + currentOffset.Pose.position + "\n";

                    objects[grabingIndex].myScale = startScale * scaleFactor;
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
