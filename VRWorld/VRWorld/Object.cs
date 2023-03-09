using Newtonsoft.Json.Linq;
using StereoKit;

namespace VRWorld
{
    internal class Object
    {
        int myId { get; }
        public Model myModel;
        public Color myColor;
        public Pose myPose = Pose.Identity;
        public Vec3 myScale = Vec3.One;
        string myShape; //Can be "cube", "cylinder", "plane", "rounded cube". See generate funcitons at https://stereokit.net/Pages/StereoKit/Mesh.html

        public Object(int anId, JObject someData) //JObject is a JSON object
        {
            myId = anId;

            UpdateFromJSON(someData);
        }

        public void UpdateFromJSON(JObject someData)
        {
            someData.TryGetValue("position", out JToken JPos);
            someData.TryGetValue("scale", out JToken JScale);
            someData.TryGetValue("shape", out JToken JShape);
            someData.TryGetValue("color", out JToken JColor);

            //Position
            if (JPos != null)
            {
                myPose.position = JSONConverter.FromJSONVec3((JObject)JPos);
            }
            //Scale
            if (JScale != null)
            {
                myScale = JSONConverter.FromJSONVec3((JObject)JScale);
            }
            //Mesh
            if (JShape != null)
            {
                string str = JShape.ToString();
                myShape = str;

                if (str == "cube")
                {
                    myModel = Model.FromMesh(Mesh.Cube, Material.UI);
                }
                else if (str == "sphere")
                {
                    myModel = Model.FromMesh(Mesh.Sphere, Material.UI);
                }
                else if (str == "cylinder")
                {
                    Mesh cylinder = Mesh.GenerateCylinder(1.0f, 1.0f, Vec3.Up);
                    myModel = Model.FromMesh(cylinder, Material.UI);
                }
                //continue with more meshes
                else //default cube
                {
                    myModel = Model.FromMesh(Mesh.Cube, Material.UI);
                }
                
            }
            //Color
            if (JColor != null)
            {
                Color color = JSONConverter.FromJSONColor((JObject)JColor);
                myColor = color;
            }
        }

        JObject ToJson()
        {
            JObject result = new JObject();
            result.Add("id", myId);
            result.Add("position", JSONConverter.ToJSON(myPose.position));
            result.Add("scale", JSONConverter.ToJSON(myScale));
            result.Add("shape", myShape);
            result.Add("color", JSONConverter.ToJSON(myColor));

            return result;
        }

        public void Draw()
        {   
            myModel.Draw(myPose.ToMatrix(myScale), myColor);
        }
    }
}
