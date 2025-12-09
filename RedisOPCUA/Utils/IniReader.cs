using RedisOPCUA.Models;
using RedisOPCUA.Services;
using System;
using System.Collections.Generic;
using System.IO;

namespace RedisOPCUA.Utils
{
    public static class IniReader
    {

        public static string ReadIniValue(string path, string section, string key)
        {
            if (!File.Exists(path))
                return null;

            bool inTargetSection = false;

            foreach (var line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue; // ignora commenti e righe vuote

                // Controlla se è una sezione
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    inTargetSection = trimmed.Equals($"[{section}]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                // Se siamo nella sezione target
                if (inTargetSection && trimmed.Contains("="))
                {
                    var parts = trimmed.Split(new[] { '=' }, 2);
                    string currentKey = parts[0].Trim();
                    string value = parts[1].Trim();

                    if (currentKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return value;
                    }
                }
            }

            return null; // chiave non trovata
        }





        public static List<string> ReadRedisKeys(string path)
        {
            var chiavi = new List<string>();
            if (!File.Exists(path)) return chiavi;

            bool inRedisSection = false;

            foreach (var line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue; // ignora commenti e righe vuote

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    inRedisSection = trimmed.Equals("[RedisKeys]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (inRedisSection && trimmed.Contains("="))
                {
                    var parts = trimmed.Split('=', 2);
                    string value = parts[1].Trim();
                    if (!string.IsNullOrEmpty(value))
                        chiavi.Add(value);
                }
            }

            return chiavi;
        }


        public static List<OpcUaNode> ReadOpcuaNodeInfo(string path)
        {
            var list = new List<OpcUaNode>();

            if (!File.Exists(path)) return list;

            bool inSection = false;

            foreach (var line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue;

                // Inizio sezione
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    inSection = trimmed.Equals("[OpcuaKeys]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (inSection && trimmed.Contains("="))
                {
                    var parts = trimmed.Split('=', 2);
                    string value = parts[1].Trim(); // esempio: "ns=3;i=1002" o "http://...;i=1002"

                    // Parsing del formato ns=3;i=1002 oppure URI;i=1002
                    var segments = value.Split(';');
                    if (segments.Length < 2)
                        continue; // ignora linee malformate

                    string uri = segments[0];       // può essere "ns=3" oppure URI
                    string nodeId = segments[1];    // esempio "i=1002"

                    list.Add(new OpcUaNode
                    {
                        Uri = uri,
                        NodeId = nodeId,
                        NodeIdFull = "",      // lasciamo vuoto per ora
                        DisplayName = ""      // lasciamo vuoto per ora
                    });
                }
            }

            return list;
        }



    }
}