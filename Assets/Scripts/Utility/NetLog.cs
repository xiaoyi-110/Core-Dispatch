using System;
using System.IO;
using UnityEngine;
using Unity.Netcode;

namespace Utility
{
    public static class NetLog
    {
        private static readonly object FileLock = new object();

        public static void Write(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            string role = "client";
            string suffix = "";
            try
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    role = "server";
                    suffix = "";
                }
                else if (NetworkManager.Singleton != null)
                {
                    suffix = $"_{NetworkManager.Singleton.LocalClientId}";
                }
            }
            catch
            {
                role = "client";
            }

            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            string path = Path.Combine(Application.persistentDataPath, $"{role}_net{suffix}_{pid}.log");
            string line = $"{DateTime.UtcNow:O} {message}{Environment.NewLine}";

            try
            {
                lock (FileLock)
                {
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
                // Ignore file IO errors to avoid breaking gameplay.
            }
        }
    }
}
