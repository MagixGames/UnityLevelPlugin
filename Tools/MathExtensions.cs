using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace UnityLevelPlugin.Tools
{
    internal static class MathExtensions
    {
        public static Vector3 ToEuler(this System.Numerics.Quaternion q)
        {
            Vector3 euler = new Vector3();

            // roll (x-axis rotation)
            double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            euler.X = (float)Math.Atan2(sinr_cosp, cosr_cosp);

            // pitch (y-axis rotation)
            double sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (Math.Abs(sinp) >= 1)
                euler.Y = (float)Math.PI / 2 * Math.Sign(sinp); // use 90 degrees if out of range
            else
                euler.Y = (float)Math.Asin(sinp);

            // yaw (z-axis rotation)
            double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            euler.Z = (float)Math.Atan2(siny_cosp, cosy_cosp);

            // Convert radians to degrees
            euler.X = (float)(euler.X * (180 / Math.PI));
            euler.Y = (float)(euler.Y * (180 / Math.PI));
            euler.Z = (float)(euler.Z * (180 / Math.PI));

            return euler;
        }
    }
}
