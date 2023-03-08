using StereoKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRWorld
{
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

        private void DrawPin(Model aModel, Pose anObjectPose, Vec3 aRotation, Color color)
        {
            Quat quatRotation = Quat.FromAngles(aRotation);
            Matrix pinArmMatrix = Matrix.S(myPinArmScale) * Matrix.T(Vec3.Right * myPinArmScale.x / 2.0f) * Matrix.R(quatRotation) * anObjectPose.ToMatrix();
            Matrix pinHeadMatrix = Matrix.S(myPinHeadScale) * Matrix.T(Vec3.Right * myPinArmScale.x) * Matrix.R(quatRotation) * anObjectPose.ToMatrix();

            aModel.Draw(pinArmMatrix, color);
            aModel.Draw(pinHeadMatrix, color);
        }
        private void Draw(Model aModel, Pose anObjectPose)
        {
            aModel.Draw(anObjectPose.ToMatrix(myCenterScale), new Color(0.5f, 0.5f, 0.5f)); //gray middle
            DrawPin(aModel, anObjectPose, new Vec3(0, 0, 0), new Color(1.0f, 0.0f, 0.0f)); //red x
            DrawPin(aModel, anObjectPose, new Vec3(0, 0, 90), new Color(0.0f, 1.0f, 0.0f)); //green y
            DrawPin(aModel, anObjectPose, new Vec3(0, 90, 0), new Color(0.0f, 0.0f, 1.0f)); //blue z
        }
        public void Draw(Pose anObjectPose)
        {
            Draw(myAlwaysModel, anObjectPose);
            Draw(myLessModel, anObjectPose);
        }
    }
}
