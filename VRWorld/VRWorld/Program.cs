using StereoKit;
using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace VRWorld
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

            int myIdCounter = 0;
            List<VRWorld.Object> objects = new List<VRWorld.Object>();

            var JObj = new JObject();
            JObj.Add("position", VRWorld.JSONConverter.ToJSON(new Vec3(1, 0, -1)));
            JObj.Add("shape", new JValue("cube"));
            JObj.Add("color", VRWorld.JSONConverter.ToJSON(new Color(0.1f, 0.2f, 0.3f)));

            objects.Add(new VRWorld.Object(myIdCounter++, JObj));
            JObj["position"] = VRWorld.JSONConverter.ToJSON(new Vec3(1.0f, 0.2f, 0.3f));
            JObj["color"] = VRWorld.JSONConverter.ToJSON(new Color(1.0f, 0.2f, 0.3f));
            objects.Add(new VRWorld.Object(myIdCounter++, JObj));

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

                foreach(VRWorld.Object o in objects)
                {
                    o.Draw();
                }
            }));
            SK.Shutdown();
        }
    }
}
