using SimpleECS;
using StereoKit;
using System;

namespace VRWorld
{
    internal class Program
    {
        public static string myDebugText = "";
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

                world.CreateEntity(model, pose, scale, color, new Grabbable());
            }

            //Cordinate system
            ScalingCoordinateSystem scalingCoordinateSystem = new ScalingCoordinateSystem();

            //Debug window
            Pose aiWindowPose = new Pose(0.0f, 0.09f, -0.32f, Quat.LookDir(-0.0f, 0.09f, 0.71f));
            Pose debugWindowPose = new Pose(0.04f, -0.32f, -0.34f, Quat.LookDir(-0.03f, 0.64f, 0.76f));
            
            OpenAISpeech.Start();

            // Core application loop
            while (SK.Step(() =>
            {
                myDebugText = ""; //clear

                OpenAISpeech.Update(world);
                Grabbing.Update(world);
                Draw(world);
                DrawWindows(debugWindowPose, aiWindowPose);

            }));

            world.Destroy();
            SK.Shutdown();
        }

        static void Draw(SimpleECS.World aWorld)
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

        static void DrawWindows(Pose debugWindowPose, Pose aiWindowPose)
        {
            //Debug window
            {
                UI.WindowBegin("Debug window", ref debugWindowPose, new Vec2(30, 0) * U.cm, moveType: UIMove.None);
                UI.Text(myDebugText);
                UI.WindowEnd();
            }
            //Chat window
            {
                UI.WindowBegin("Open AI window", ref aiWindowPose, new Vec2(30, 0) * U.cm, moveType: UIMove.None);
                //Get the 200 last characters of aiText
                int showLength = 200;
                string showText = OpenAISpeech.GetTotalAIText().Length > showLength ? "..." + OpenAISpeech.GetTotalAIText().Substring(OpenAISpeech.GetTotalAIText().Length - showLength) : OpenAISpeech.GetTotalAIText();
                UI.Text(showText);
                UI.HSeparator();
                UI.Label(OpenAISpeech.mySpeechText);
                UI.WindowEnd();
            }            
        }
    }
}
