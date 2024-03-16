using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using StereoKit;

namespace VRWorld
{
    internal class JSONConverter
    {
        public static Color HexToRGBA(string hexColor)
        {
            // Remove leading hash character, if it exists
            if (hexColor.StartsWith("#"))
            {
                hexColor = hexColor.Substring(1);
            }

            // Parse the hex color string into three separate hex strings
            string hexRed = hexColor.Substring(0, 2);
            string hexGreen = hexColor.Substring(2, 2);
            string hexBlue = hexColor.Substring(4, 2);

            // Convert each hex string into a byte value
            byte r = Convert.ToByte(hexRed, 16);
            byte g = Convert.ToByte(hexGreen, 16);
            byte b = Convert.ToByte(hexBlue, 16);

            // Divide each byte value by 255 to get a value between 0 and 1
            float R = (float)r / 255.0f;
            float G = (float)g / 255.0f;
            float B = (float)b / 255.0f;

            return new Color(R, G, B, 1);
        }

        //Color
        public static Color FromJSONColor(JObject someData)
        {
            Color result = HexToRGBA(someData.ToString());
            return result;
        }
    }
}
