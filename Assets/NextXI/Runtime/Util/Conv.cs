using NextXI.Core;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Conversions between the engine-independent core and Unity types.</summary>
    public static class Conv
    {
        public static Vector3 ToUnity(this Vec3 v) => new Vector3(v.X, v.Y, v.Z);
        public static Vec3 ToCore(this Vector3 v) => new Vec3(v.x, v.y, v.z);

        /// <summary>Core facing angle (radians, atan2(z,x)) to a Unity rotation looking along that direction.</summary>
        public static Quaternion FacingToRotation(float facing) =>
            Quaternion.LookRotation(new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing)), Vector3.up);

        public static Color Hex(string hex, Color fallback = default)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return fallback == default ? Color.magenta : fallback;
        }
    }

    public sealed class UnityLogSink : ILogSink
    {
        public void Write(LogLevel level, string category, string message)
        {
            string line = $"[NEXT XI][{category}] {message}";
            switch (level)
            {
                case LogLevel.Error: Debug.LogError(line); break;
                case LogLevel.Warning: Debug.LogWarning(line); break;
                default: Debug.Log(line); break;
            }
        }
    }
}
