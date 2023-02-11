using StereoKit;
using System;
using System.Threading.Tasks;

namespace CreateWorldWithAI
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

            Matrix floorTransform = Matrix.TS(0, -1.5f, 0, new Vec3(30, 0.1f, 30));
            Material floorMaterial = new Material(Shader.FromFile("floor.hlsl"));
            floorMaterial.Transparency = Transparency.Blend;

            Pose windowPose = new Pose(0, 0, -0.5f, Quat.LookDir(1, 0, 1));
            string textInput = "";

            //var task = Task.Run(async () =>
            //{
            //    var api = new OpenAI_API.OpenAIAPI();
            //    var result = await api.Completions.GetCompletion("One Two Three One Two", );
            //});

            // Core application loop
            while (SK.Step(() =>
            {
                if (SK.System.displayType == Display.Opaque)
                    Default.MeshCube.Draw(floorMaterial, floorTransform);

                UI.WindowBegin("Open AI chat", ref windowPose, new Vec2(20, 0) * U.cm);
                UI.Text(textInput);
                UI.Input("Input", ref textInput);
                if (UI.Button("Submit text"))
                {
                    //Do some Open AI call
                    textInput = ""; //Clear input
                }
                UI.WindowEnd();
            })) ;
            SK.Shutdown();
        }
    }
}
