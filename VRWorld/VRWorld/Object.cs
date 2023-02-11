using Newtonsoft.Json.Linq;
using StereoKit;

namespace VRWorld
{
    internal class Object
    {
        int myId;
        Model myModel;
        Material myMaterial;
        Pose myPose;
        string myShape; //Can be "cube", "cylinder", "plane", "rounded cube". See generate funcitons at https://stereokit.net/Pages/StereoKit/Mesh.html

        public Object(int anId, JObject someData) //JObject is a JSON object
        {
            myId = anId;
            myMaterial = new Material(Shader.UI);

            someData.TryGetValue("position", out JToken JPos);
            someData.TryGetValue("shape", out JToken JShape);
            someData.TryGetValue("color", out JToken JColor);

            //Position
            if (JPos != null)
            {
                myPose.position = JSONConverter.FromJSONVec3((JObject)JPos);
            }
            //Mesh
            if (JShape != null)
            {
                string str = JShape.ToString();
                myShape = str;

                if (str == "cube")
                {
                    myModel = Model.FromMesh(Mesh.Cube, myMaterial);
                }
                else if (str == "sphere")
                {
                    myModel = Model.FromMesh(Mesh.Sphere, myMaterial);
                }
                //continue with more meshes
            }
            //Color
            if (JColor != null)
            {
                Color color = JSONConverter.FromJSONColor((JObject)JColor);
                myMaterial.SetColor("color", color);
            }
        }
    }
}
