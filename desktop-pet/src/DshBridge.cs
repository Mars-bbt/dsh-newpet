using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace MarsNewPet
{
    internal static class DshBridge
    {
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
        private static readonly string[] Endpoints = {
            Environment.GetEnvironmentVariable("MARS_PET_API") ?? "http://127.0.0.1:19387",
            "http://127.0.0.1:19388"
        };
        internal static Dictionary<string, object> Request(string route)
        {
            Exception failure = null;
            foreach (string origin in Endpoints)
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(origin + "/api/dsh-newpet/" + route);
                    request.Timeout = route == "balance" ? 18000 : 2500;
                    using (var response = request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                        return Json.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                }
                catch (Exception error) { failure = error; }
            }
            throw new InvalidOperationException("暂时无法连接桌宠服务", failure);
        }
        internal static string Text(Dictionary<string, object> value, string key, string fallback = "")
        {
            object result;
            return value != null && value.TryGetValue(key, out result) && result != null ? Convert.ToString(result) : fallback;
        }
        internal static bool Flag(Dictionary<string, object> value, string key)
        {
            object result;
            return value != null && value.TryGetValue(key, out result) && result is bool && (bool)result;
        }
        internal static void OpenDesktop()
        {
            string filename = null;
            foreach (Process process in Process.GetProcessesByName("DeepSeek Harness"))
            {
                using (process)
                {
                    if (filename != null) continue;
                    try { if (File.Exists(process.MainModule.FileName)) filename = process.MainModule.FileName; }
                    catch (Exception) { }
                }
            }
            if (filename == null) throw new InvalidOperationException("请先启动 DeepSeek Harness 桌面端");
            var launch = new ProcessStartInfo { FileName = filename, WorkingDirectory = Path.GetDirectoryName(filename),
                UseShellExecute = false, CreateNoWindow = true };
            foreach (string variable in new[] { "ELECTRON_RUN_AS_NODE", "ELECTRON_NO_ASAR", "NODE_OPTIONS", "NODE_PATH" })
                launch.EnvironmentVariables.Remove(variable);
            using (Process temporary = Process.Start(launch)) { }
        }
    }
}
