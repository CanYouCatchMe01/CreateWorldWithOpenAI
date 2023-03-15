using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StereoKit;
using SimpleECS;
using System.Numerics;
using System.ComponentModel.DataAnnotations;

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
        static eScaleAxis myScaleAxis = eScaleAxis.none;

        public static void Start()
        {

        }

        public static void Update(SimpleECS.World aWorld)
        {
            Grab(aWorld);
            Scale();
            DrawCoordinateSystem();
            VRWorld.Program.myDebugText += myScaleAxis.ToString() + "\n";
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

                            //Debbugging size
                            //myScalingCoordinateSystem.GetScaleAxis(pose, scale, model.Bounds, h);

                            if (hand.IsJustPinched)
                            {
                                //Getting pinch point in object bounds space for more exact collision check
                                Matrix objectMatrix = pose.ToMatrix(scale + Vec3.One * 2.5f * U.cm);
                                Vec3 pinchPtObjectSpace = objectMatrix.Inverse * hand.pinchPt;
                                Bounds bounds = model.Bounds;

                                if (otherGrabedEntity == entity) //Scaling with other hand
                                {
                                    
                                    eScaleAxis scaleAxis = myScalingCoordinateSystem.GetScaleAxis(pose, scale, model.Bounds, h);

                                    if (scaleAxis != eScaleAxis.none)
                                    {
                                        myScaleAxis = scaleAxis;
                                        myScalingHand = h;
                                        myStartScale = scale;
                                        myStartScaleDistance = (Input.Hand(Handed.Left).pinchPt - Input.Hand(Handed.Right).pinchPt).Length;

                                        foundObject = true;
                                    }
                                }
                                else if (bounds.Contains(pinchPtObjectSpace)) //Grabbing with first hand
                                {
                                    myGrabDatas[(int)h].myEntity = entity;
                                    myGrabDatas[(int)h].myOffset = pose.ToMatrix() * handMatrix.Inverse;
                                    foundObject = true;
                                }
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

                Vec3 scaleVector = Vec3.One;

                switch (myScaleAxis)
                {
                    case eScaleAxis.x:
                        scaleVector.x = scaleFactor;
                        break;
                    case eScaleAxis.y:
                        scaleVector.y = scaleFactor;
                        break;
                    case eScaleAxis.z:
                        scaleVector.z = scaleFactor;
                        break;
                    case eScaleAxis.uniform:
                        scaleVector *= scaleFactor;
                        break;
                    default:
                        break;
                }

                grabingEntity.Get<Vec3>() = myStartScale * scaleVector;
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

        //Not working
        public static Entity GetPointingEntity(SimpleECS.World aWorld, Handed ahand, out Vec3 aClosestPoint)
        {
            Hand hand = Input.Hand(ahand);

            if (myGrabDatas[(int)ahand].myEntity.IsValid())
            {
                aClosestPoint = Vec3.Zero;
                return new Entity();
            }

            Vec3 closestPoint = Vec3.Zero;
            float closestDistance = float.MaxValue;
            Entity closestEntity = new Entity();
            var query = aWorld.CreateQuery().Has(typeof(Pose), typeof(Vec3), typeof(Grabbable), typeof(Model));
            
            Pose fingertip = hand[FingerId.Index, JointId.Tip].Pose;

            //Vec3 end = fingertip.position + fingertip.orientation * Vec3.Forward * 0.1f;
            //Color color = new Color(1,0,0);
            //Lines.Add(fingertip.position, end, color, 0.01f);

            query.Foreach((Entity entity, ref Pose pose, ref StereoKit.Vec3 scale, ref StereoKit.Model model) =>
            {
                Matrix objectMatrix = pose.ToMatrix(scale);
                Matrix fingerInObjectSpace = fingertip.ToMatrix() * objectMatrix.Inverse;

                Vec3 fingerForward = fingerInObjectSpace.Pose.Forward;
                Ray ray = new Ray(fingerInObjectSpace.Pose.position, fingerForward);

                Bounds bounds = model.Bounds;
                //bounds.Scale(1.5f);

                if (bounds.Intersect(ray, out Vec3 at))
                {
                    Vec3 atWorldSpace = (Matrix.T(at) * objectMatrix).Pose.position;
                    Program.myDebugText += objectMatrix.ToString() + "\n";
                    float distance = (atWorldSpace - fingertip.position).Length;

                    Program.myDebugText += "distance: " + distance.ToString() + "\n";

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestEntity = entity;
                        closestPoint = atWorldSpace;
                    }
                }
            });
            
            aClosestPoint = closestPoint;
            return closestEntity;
        }
            
        
        public static void DrawBounds(Matrix aMatrix, Bounds aBounds)
        {
            Matrix boundsMatrix = Matrix.TS(aBounds.center, aBounds.dimensions);
            Matrix globalBoundsMatrix = boundsMatrix * aMatrix;

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
