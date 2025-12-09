namespace RedisOPCUA.Utils
{
    using RedisOPCUA.Models;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public static class Ini
    {
        // --- Proprietà dirette (tutte string) ---

        // [General]
        public static string Redis { get; private set; }
        public static string OPCUA { get; private set; }

        // [ServerOPCUA]
        public static string ServerOPCUAAddress { get; private set; }
        public static string ServerOPCUACsvFile { get; private set; }

        // [Redis]
        public static string RedisAddress { get; private set; }
        public static string RedisCsvFile { get; private set; }

        // Liste
        public static List<string> RedisKeys { get; private set; } = new();
        public static List<OpcUaNode> OpcuaKeys { get; private set; } = new();

        private static bool _loaded = false;

        // --- Loader unico ---
        public static void Load(string path)
        {
            if (_loaded) return;
            if (!File.Exists(path))
                throw new FileNotFoundException("File INI non trovato", path);

            string section = null;
            string appPath = Directory.GetCurrentDirectory();

            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                // Sezione
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line[1..^1].Trim();
                    continue;
                }

                if (section == null || !line.Contains("="))
                    continue;

                // KEY = VALUE
                var parts = line.Split('=', 2);
                string key = parts[0].Trim();
                string value = parts[1].Trim();

                value = value.Replace("<APP>", appPath);

                switch (section.ToUpper())
                {
                    case "GENERAL":
                        if (key.Equals("Redis", StringComparison.OrdinalIgnoreCase)) Redis = value;
                        if (key.Equals("OPCUA", StringComparison.OrdinalIgnoreCase)) OPCUA = value;
                        break;

                    case "SERVEROPCUA":
                        if (key.Equals("Address", StringComparison.OrdinalIgnoreCase)) ServerOPCUAAddress = value;
                        if (key.Equals("CsvFile", StringComparison.OrdinalIgnoreCase)) ServerOPCUACsvFile = value;
                        break;

                    case "REDIS":
                        if (key.Equals("Address", StringComparison.OrdinalIgnoreCase)) RedisAddress = value;
                        if (key.Equals("CsvFile", StringComparison.OrdinalIgnoreCase)) RedisCsvFile = value;
                        break;

                    case "REDISKEYS":
                        RedisKeys.Add(value);
                        break;

                    case "OPCUAKEYS":

                        var segments = value.Split(';');
                        if (segments.Length < 2)
                            continue; // ignora linee malformate

                        string uri = segments[0];       // può essere "ns=3" oppure URI
                        string nodeId = segments[1];    // esempio "i=1002"

                        OpcuaKeys.Add(new OpcUaNode
                        {
                            Uri = uri,
                            NodeId = nodeId,
                            NodeIdFull = "",      // lasciamo vuoto per ora
                            DisplayName = ""      // lasciamo vuoto per ora
                        });

                        break;
                }
            }

            _loaded = true;
        }
    }

}
