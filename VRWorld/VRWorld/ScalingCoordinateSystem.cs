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
        Vec3 myCenterScale = Vec3.One * 10.0f * U.cm; //should be 3 cm
        Vec3 myPinArmScale = new Vec3(10, 1, 1) * U.cm;
        Vec3 myPinHeadScale = Vec3.One * 4.0f * U.cm;
        Model myModel;

        //Create constuctior
        public ScalingCoordinateSystem()
        {
            //Create a model
            myModel = Model.FromMesh(Mesh.Cube, Material.UI);
        }

        private void DrawPin(Pose anObjectPose, Vec3 aRotation, Color color)
        {
            Quat quatRotation = Quat.FromAngles(aRotation);
            Matrix matrix = anObjectPose.ToMatrix() * Matrix.R(quatRotation) * Matrix.T(Vec3.Right * myPinArmScale.x) *  Matrix.S(myPinArmScale);

            myModel.Draw(matrix, color, RenderLayer.Layer1);
        }
        public void Draw(Pose anObjectPose)
        {
            myModel.Draw(anObjectPose.ToMatrix(myCenterScale), new Color(0.5f, 0.5f, 0.5f), RenderLayer.Layer1);
            DrawPin(anObjectPose, new Vec3(0, 0, 0), new Color(0.0f, 0.0f, 1.0f));
        }
    }
}
