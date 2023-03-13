using SimpleECS;
using StereoKit;
using System;

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

            SimpleECS.World world = SimpleECS.World.Create();

            //Floor
            {
                //Model
                Material material = new Material(Shader.FromFile("floor.hlsl"));
                material.Transparency = Transparency.Blend;
                Model model = Model.FromMesh(Default.MeshCube, material);

                //Pose
                Pose pose = new Pose(new Vec3(0, -1.5f, 0), Quat.Identity);
                Vec3 scale = new Vec3(30, 0.1f, 30);

                world.CreateEntity(model, pose, scale);
            }

            //Two yellow cubes
            for (int i = 0; i < 2; i++)
            {
                Model model = Model.FromMesh(Mesh.Cube, Material.UI);
                Pose pose = new Pose(new Vec3(0, -0, -0.3f), Quat.Identity);
                Vec3 scale = Vec3.One * 5.0f * U.cm;
                Color color = new Color(1, 1, 0); //yellow

                world.CreateEntity(model, pose, scale, color);
            }

            //Cordinate system
            ScalingCoordinateSystem scalingCoordinateSystem = new ScalingCoordinateSystem();

            //Debug window
            Pose AIWindowPose = new Pose(0.0f, 0.09f, -0.32f, Quat.LookDir(-0.0f, 0.09f, 0.71f));
            Pose debugWindowPose = new Pose(0.04f, -0.32f, -0.34f, Quat.LookDir(-0.03f, 0.64f, 0.76f));
            string debugText = "";

            //Grabbing
            Matrix[] grabedOffsets = new Matrix[(int)Handed.Max] { Matrix.Identity, Matrix.Identity };
            int[] grabedIndexs = new int[(int)Handed.Max] { -1, -1 };
            Handed scalingHand = Handed.Max; //Max is like invalid
            Vec3 startScale = Vec3.Zero;
            float startScalingDistance = 0.0f;

            OpenAISpeech.Start();

            // Core application loop
            while (SK.Step(() =>
            {
                debugText = ""; //clear

                OpenAISpeech.Update(world);

                ////Seeing which object that is grabed
                //for (Handed h = 0; h < Handed.Max; h++)
                //{
                //    Hand hand = Input.Hand(h);
                //    Handed otherHand = h == Handed.Right ? Handed.Left : Handed.Right;
                //    Matrix handMatrix = Matrix.TR(hand.pinchPt, hand.palm.orientation);

                //    int otherGrabedIndex = grabedIndexs[(int)otherHand];

                //    for (int i = 0; i < objects.Count; i++)
                //    {
                //        Bounds bounds = objects[i].myModel.Bounds;
                //        bounds.dimensions *= objects[i].myScale * 1.5f;
                //        bounds.center += objects[i].myPose.position;

                //        if (hand.IsJustPinched && bounds.Contains(hand.pinchPt))
                //        {
                //            if (otherGrabedIndex == i) //Scaling with other hand
                //            {
                //                scalingHand = h;
                //                startScale = objects[i].myScale;
                //                startScalingDistance = (Input.Hand(Handed.Left).pinchPt - Input.Hand(Handed.Right).pinchPt).Length;
                //            }
                //            else //Grabbing with first hand
                //            {
                //                grabedIndexs[(int)h] = i;
                //                grabedOffsets[(int)h] = objects[i].myPose.ToMatrix() * handMatrix.Inverse;
                //            }
                //            break;
                //        }
                //    }

                //    //Move the grabed object
                //    if (hand.IsPinched && grabedIndexs[(int)h] != -1)
                //    {
                //        Matrix newMatrix = grabedOffsets[(int)h] * handMatrix;
                //        objects[grabedIndexs[(int)h]].myPose = newMatrix.Pose;

                //        //debugText += "pos offset" + grabedOffsets[(int)h].Pose.position + "\n";
                //        //debugText += "rot offset" + grabedOffsets[(int)h].Pose.orientation + "\n";
                //    }
                //    //Ungrab the object
                //    else if (hand.IsJustUnpinched)
                //    {
                //        grabedIndexs[(int)h] = -1;
                //        scalingHand = Handed.Max;
                //    }

                //    //debugText += "scaling hand" + scalingHand + "\n";
                //}

                //if (scalingHand != Handed.Max)
                //{
                //    float currentScalingDistance = (Input.Hand(Handed.Left).pinchPt - Input.Hand(Handed.Right).pinchPt).Length;

                //    float scaleFactor = currentScalingDistance / startScalingDistance;

                //    debugText += "currentDistance" + startScalingDistance + "\n";
                //    debugText += "startDistance" + currentScalingDistance + "\n";
                //    debugText += "scaleFactor" + scaleFactor + "\n";

                //    Handed grabingHand = scalingHand == Handed.Right ? Handed.Left : Handed.Right;
                //    int grabingIndex = grabedIndexs[(int)grabingHand];

                //    objects[grabingIndex].myScale = startScale * scaleFactor;
                //}

                ////Cordinate system
                //if (grabedIndexs[(int)Handed.Left] != -1 && grabedIndexs[(int)Handed.Right] == -1)
                //{
                //    int oneGrabedIndex = grabedIndexs[(int)Handed.Left];
                //    scalingCoordinateSystem.Draw(objects[oneGrabedIndex].myPose, Handed.Left);
                //}
                //else if (grabedIndexs[(int)Handed.Left] == -1 && grabedIndexs[(int)Handed.Right] != -1)
                //{
                //    int oneGrabedIndex = grabedIndexs[(int)Handed.Right];
                //    scalingCoordinateSystem.Draw(objects[oneGrabedIndex].myPose, Handed.Right);
                //}

                Render(world);

                //Debug window
                UI.WindowBegin("Debug window", ref debugWindowPose, new Vec2(30, 0) * U.cm);
                UI.Text(debugText);
                UI.WindowEnd();

                //Chat window
                UI.WindowBegin("Open AI window", ref AIWindowPose, new Vec2(30, 0) * U.cm);

                //Get the 200 last characters of aiText
                int showLength = 200;
                string showText = OpenAISpeech.myAIText.Length > showLength ? "..." + OpenAISpeech.myAIText.Substring(OpenAISpeech.myAIText.Length - showLength) : OpenAISpeech.myAIText;
                UI.Text(showText);
                UI.HSeparator();
                UI.Label(OpenAISpeech.mySpeechText);
                UI.WindowEnd();
            }));

            world.Destroy();
            SK.Shutdown();
        }

        static void Render(SimpleECS.World aWorld)
        {
            var query = aWorld.CreateQuery().Has(typeof(StereoKit.Model), typeof(StereoKit.Pose));

            query.Foreach((Entity entity, ref StereoKit.Model model, ref StereoKit.Pose pose) =>
            {
                //try get scale
                Vec3 scale = Vec3.One;
                {
                    if (entity.TryGet(out Vec3 value))
                    {
                        scale = value;
                    }
                }
                
                //try get color
                Color color = Color.White;
                {
                    if (entity.TryGet(out Color value))
                    {
                        color = value;
                    }
                }

                Matrix matrix = pose.ToMatrix(scale);
                model.Draw(matrix, color);
            });
        }
    }
}
