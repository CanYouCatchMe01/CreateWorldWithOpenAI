using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StereoKit;
using SimpleECS;
using System.Numerics;

namespace VRWorld
{
    public class Grabbable
    {
    }

    internal class Grabbing
    {
        class GrabData
        {
            public Matrix myOffset = Matrix.Identity;
            public Entity myEntity = new Entity();
        }

        static GrabData[] myGrabDatas = new GrabData[(int)Handed.Max] { new GrabData(), new GrabData() };
        static Handed myScalingHand = Handed.Max; //Max is like invalid
        static Vec3 myStartScale = Vec3.Zero;
        static float myStartScaleDistance = 0.0f;
        static ScalingCoordinateSystem myScalingCoordinateSystem = new ScalingCoordinateSystem();

        public static void Start()
        {

        }

        public static void Update(SimpleECS.World aWorld)
        {
            Grab(aWorld);
            Scale();
            DrawCoordinateSystem();
        }

        static void Grab(SimpleECS.World aWorld)
        {
            //Seeing which object that is grabed
            for (Handed h = 0; h < Handed.Max; h++)
            {
                Hand hand = Input.Hand(h);
                Handed otherHand = h == Handed.Right ? Handed.Left : Handed.Right;
                Matrix handMatrix = Matrix.TR(hand.pinchPt, hand.palm.orientation);

                Entity otherGrabedEntity = myGrabDatas[(int)otherHand].myEntity;

                var query = aWorld.CreateQuery().Has(typeof(Pose), typeof(Vec3), typeof(Grabbable));
                bool foundObject = false;

                //very ugly function for finding the grabed object
                foreach (var archetype in query)
                {
                    if (archetype.TryGetEntityBuffer(out Entity[] entityBuffer) &&
                        archetype.TryGetComponentBuffer(out Model[] modelBuffer) &&
                        archetype.TryGetComponentBuffer(out Pose[] poseBuffer) &&
                        archetype.TryGetComponentBuffer(out Vec3[] scaleBuffer))
                    {
                        for (int i = 0; i < archetype.EntityCount; ++i)
                        {
                            Entity entity = entityBuffer[i];
                            Model model = modelBuffer[i];
                            Pose pose = poseBuffer[i];
                            Vec3 scale = scaleBuffer[i];

                            //Getting pinch point in object bounds space for more exact collision check
                            Matrix objectMatrix = pose.ToMatrix(scale + Vec3.One * 2.5f * U.cm);
                            Vec3 pinchPtObjectSpace = objectMatrix.Inverse * hand.pinchPt;
                            Bounds bounds = model.Bounds;

                            if (bounds.Contains(pinchPtObjectSpace))
                            {
                                if (hand.IsJustPinched)
                                {
                                    if (otherGrabedEntity == entity) //Scaling with other hand
                                    {
                                        myScalingHand = h;
                                        myStartScale = scale;
                                        myStartScaleDistance = (Input.Hand(Handed.Left).pinchPt - Input.Hand(Handed.Right).pinchPt).Length;
                                    }
                                    else //Grabbing with first hand
                                    {
                                        myGrabDatas[(int)h].myEntity = entity;
                                        myGrabDatas[(int)h].myOffset = pose.ToMatrix() * handMatrix.Inverse;
                                    }
                                    foundObject = true;
                                }

                                //if (!myGrabDatas[(int)h].myEntity.IsValid())
                                //{
                                //    DrawBounds(pose, scale * 1.3f, bounds);
                                //}
                            }

                            if (foundObject)
                                break;
                        }

                        if (foundObject)
                            break;
                    }
                }

                //Move the grabed object
                if (hand.IsPinched && myGrabDatas[(int)h].myEntity.IsValid())
                {
                    Matrix newMatrix = myGrabDatas[(int)h].myOffset * handMatrix;
                    myGrabDatas[(int)h].myEntity.Get<Pose>() = newMatrix.Pose;
                }
                //Ungrab the object
                else if (hand.IsJustUnpinched)
                {
                    myGrabDatas[(int)h].myEntity = new Entity();
                    myScalingHand = Handed.Max;
                }
            }
        }

        static void Scale()
        {
            if (myScalingHand != Handed.Max)
            {
                float currentScalingDistance = (Input.Hand(Handed.Left).pinchPt - Input.Hand(Handed.Right).pinchPt).Length;

                float scaleFactor = currentScalingDistance / myStartScaleDistance;

                Handed grabingHand = myScalingHand == Handed.Right ? Handed.Left : Handed.Right;
                Entity grabingEntity = myGrabDatas[(int)grabingHand].myEntity;

                grabingEntity.Get<Vec3>() = myStartScale * scaleFactor;
            }
        }

        static void DrawCoordinateSystem()
        {
            Entity leftEntity = myGrabDatas[(int)Handed.Left].myEntity;
            Entity rightEntity = myGrabDatas[(int)Handed.Right].myEntity;

            if (leftEntity.IsValid() && !rightEntity.IsValid())
            {
                Pose pose = leftEntity.Get<Pose>();
                myScalingCoordinateSystem.Draw(pose, Handed.Left);
            }
            else if (!leftEntity.IsValid() && rightEntity.IsValid())
            {
                Pose pose = rightEntity.Get<Pose>();
                myScalingCoordinateSystem.Draw(pose, Handed.Right);
            }
        }

        static void DrawBounds(Pose aPose, Vec3 aScale, Bounds aBounds)
        {
            Matrix boundsMatrix = Matrix.TS(aBounds.center, aBounds.dimensions);
            Matrix globalBoundsMatrix = boundsMatrix * aPose.ToMatrix(aScale);

            Vec3 leftTopForward = globalBoundsMatrix * new Vec3(-0.5f, 0.5f, 0.5f);
            Vec3 rightTopForward = globalBoundsMatrix * new Vec3(0.5f, 0.5f, 0.5f);
            Vec3 leftBottomForward = globalBoundsMatrix * new Vec3(-0.5f, -0.5f, 0.5f);
            Vec3 rightBottomForward = globalBoundsMatrix * new Vec3(0.5f, -0.5f, 0.5f);
            Vec3 leftTopBack = globalBoundsMatrix * new Vec3(-0.5f, 0.5f, -0.5f);
            Vec3 rightTopBack = globalBoundsMatrix * new Vec3(0.5f, 0.5f, -0.5f);
            Vec3 leftBottomBack = globalBoundsMatrix * new Vec3(-0.5f, -0.5f, -0.5f);
            Vec3 rightBottomBack = globalBoundsMatrix * new Vec3(0.5f, -0.5f, -0.5f);

            float thickness = 0.5f * U.cm;
            Color color = Color.White;

            Lines.Add(leftTopForward, rightTopForward, color, thickness);
            Lines.Add(leftTopForward, leftBottomForward, color, thickness);
            Lines.Add(leftTopForward, leftTopBack, color, thickness);
            Lines.Add(rightTopForward, rightBottomForward, color, thickness);
            Lines.Add(rightTopForward, rightTopBack, color, thickness);
            Lines.Add(leftBottomForward, rightBottomForward, color, thickness);
            Lines.Add(leftBottomForward, leftBottomBack, color, thickness);
            Lines.Add(rightBottomForward, rightBottomBack, color, thickness);
            Lines.Add(leftTopBack, rightTopBack, color, thickness);
            Lines.Add(leftTopBack, leftBottomBack, color, thickness);
            Lines.Add(rightTopBack, rightBottomBack, color, thickness);
            Lines.Add(leftBottomBack, rightBottomBack, color, thickness);

        }
    }
}
