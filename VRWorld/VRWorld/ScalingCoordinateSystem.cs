using StereoKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace VRWorld
{
    enum eScaleAxis
    {
        none,
        x,
        y,
        z,
        uniform,
    }

    internal class ScalingCoordinateSystem
    {
        Vec3 myCenterScale = Vec3.One * 1.3f * U.cm;
        Vec3 myPinArmScale = new Vec3(7.0f, 0.5f, 0.5f) * U.cm;
        Vec3 myPinHeadScale = Vec3.One * 1.0f * U.cm;
        Model myAlwaysModel;
        Model myLessModel;

        //Create constuctior
        public ScalingCoordinateSystem()
        {
            Shader shader = Shader.Unlit;
            {
                Material mat = new Material(shader);
                mat.DepthTest = DepthTest.Always;
                mat.QueueOffset = 1;
                myAlwaysModel = Model.FromMesh(Mesh.Cube, mat);
            }
            {
                Material mat = new Material(shader);
                mat.DepthTest = DepthTest.Less;
                mat.QueueOffset = 2;
                myLessModel = Model.FromMesh(Mesh.Cube, mat);
            }

        }

        private Matrix GetPinArmMatrix(Pose anObjectPose, Vec3 aRotation)
        {
            Quat quatRotation = Quat.FromAngles(aRotation);
            return Matrix.S(myPinArmScale) * Matrix.T(Vec3.Right * myPinArmScale.x / 2.0f) * Matrix.R(quatRotation) * anObjectPose.ToMatrix();
        }

        private Matrix GetPinHeadMatrix(Pose anObjectPose, Vec3 aRotation)
        {
            Quat quatRotation = Quat.FromAngles(aRotation);
            return Matrix.S(myPinHeadScale) * Matrix.T(Vec3.Right * myPinArmScale.x) * Matrix.R(quatRotation) * anObjectPose.ToMatrix();
        }

        private void DrawPin(Model aModel, Pose anObjectPose, Vec3 aRotation, Color color)
        {
            Matrix pinArmMatrix = GetPinArmMatrix(anObjectPose, aRotation);
            Matrix pinHeadMatrix = GetPinHeadMatrix(anObjectPose, aRotation);

            aModel.Draw(pinArmMatrix, color);
            aModel.Draw(pinHeadMatrix, color);
        }
        private void Draw(Model aModel, Pose anObjectPose, Handed aHand)
        {
            aModel.Draw(anObjectPose.ToMatrix(myCenterScale), new Color(0.5f, 0.5f, 0.5f)); //gray middle

            float xAxisRot = aHand == Handed.Right ? 180.0f : 0.0f;

            DrawPin(aModel, anObjectPose, new Vec3(0, xAxisRot, 0), new Color(1.0f, 0.0f, 0.0f)); //red x
            DrawPin(aModel, anObjectPose, new Vec3(0, 0, 90), new Color(0.0f, 1.0f, 0.0f)); //green y
            DrawPin(aModel, anObjectPose, new Vec3(0, 90, 0), new Color(0.0f, 0.0f, 1.0f)); //blue z
        }
        public void Draw(Pose anObjectPose, Handed aHand)
        {
            Draw(myAlwaysModel, anObjectPose, aHand);
            Draw(myLessModel, anObjectPose, aHand);
        }

        public eScaleAxis GetScaleAxis(Pose anObjectPose, Vec3 anObjectScale, Bounds aObjectBounds, Handed aScalingHand)
        {
            float xAxisRot = aScalingHand == Handed.Left ? 180.0f : 0.0f;

            Bounds armBounds = Mesh.Cube.Bounds; //It's a cube

            Matrix scaleMatrix = Matrix.S(new Vec3(1.0f,10,10)); //Making the bounds a bit bigger
            Matrix moveMatrix = Matrix.T(Vec3.Right * 0.3f); //Making it possible to grab the center

            //Gettings all their global Matrixes
            Matrix xArmMatrix = scaleMatrix *  moveMatrix * GetPinArmMatrix(anObjectPose, new Vec3(0, xAxisRot, 0));
            Matrix yArmMatrix = scaleMatrix * moveMatrix * GetPinArmMatrix(anObjectPose, new Vec3(0, 0, 90));
            Matrix zArmMatrix = scaleMatrix * moveMatrix * GetPinArmMatrix(anObjectPose, new Vec3(0, 90, 0));
            Matrix objectMatrix = anObjectPose.ToMatrix(anObjectScale + Vec3.One * 2.5f * U.cm); //Make it a bit bigger for esier grabbing

            //Converting the pinchPt to local space, to check collision with the bounds
            Hand hand = Input.Hand(aScalingHand);
            Vec3 pinchPtXAxis = xArmMatrix.Inverse * hand.pinchPt;
            Vec3 pinchPtYAxis = yArmMatrix.Inverse * hand.pinchPt;
            Vec3 pinchPtZAxis = zArmMatrix.Inverse * hand.pinchPt;
            Vec3 pinchPtObjectSpace = objectMatrix.Inverse * hand.pinchPt;

            //Drawing for debugging
            //Grabbing.DrawBounds(xArmMatrix, armBounds);
            //Grabbing.DrawBounds(yArmMatrix, armBounds);
            //Grabbing.DrawBounds(zArmMatrix, armBounds);

            if (armBounds.Contains(pinchPtXAxis))
            {
                return eScaleAxis.x;
            }
            else if (armBounds.Contains(pinchPtYAxis))
            {
                return eScaleAxis.y;
            }
            else if (armBounds.Contains(pinchPtZAxis))
            {
                return eScaleAxis.z;
            }
            else if (aObjectBounds.Contains(pinchPtObjectSpace))
            {
                return eScaleAxis.uniform;
            }
            else
            {
                return eScaleAxis.none;
            }
        }
    }
}
