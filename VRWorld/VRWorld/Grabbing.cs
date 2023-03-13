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

        public static void Start()
        {
            
        }

        public static void Update(SimpleECS.World aWorld)
        {
            Grab(aWorld);
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
                query.Foreach((Entity entity, ref StereoKit.Model model, ref StereoKit.Pose pose, ref StereoKit.Vec3 scale) =>
                {
                    Bounds bounds = model.Bounds;
                    bounds.dimensions *= scale * 1.5f;
                    bounds.center += pose.position;
                    
                    if (hand.IsJustPinched && bounds.Contains(hand.pinchPt))
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
                });
            }
        }
    }
}
