namespace RedisOPCUA.Utils
{
    public class AppSettings
    {
        private static AppSettings? _instance;
        private static readonly object _lock = new object();
        private readonly string _iniPath;

        // Proprietà principali
        public int Redis { get; private set; }
        public int OpcUa { get; private set; }
        public string ServerOpcUaAddress { get; private set; } = "";
        public string ServerOpcUaCsvFile { get; private set; } = "";
        public string RedisAddress { get; private set; } = "";
        public string RedisCsvFile { get; private set; } = "";

        // Dizionari dinamici per tutte le sezioni chiave/valore
        public Dictionary<string, Dictionary<string, string>> Sections { get; private set; } = new();

        // Singleton
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AppSettings();
                        }
                    }
                }
                return _instance;
            }
        }

        private AppSettings()
        {
            _iniPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitor.ini");
            LoadIni();
        }

        private void LoadIni()
        {
            if (!File.Exists(_iniPath))
                throw new FileNotFoundException($"File INI non trovato: {_iniPath}");

            // Lista di tutte le righe
            var lines = File.ReadAllLines(_iniPath);

            string? currentSection = null;

            foreach (var lineRaw in lines)
            {
                var line = lineRaw.Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue; // ignora commenti e linee vuote

                // Sezione [Nome]
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line[1..^1].Trim();
                    if (!Sections.ContainsKey(currentSection))
                        Sections[currentSection] = new Dictionary<string, string>();
                    continue;
                }

                // Key=Value
                if (currentSection != null && line.Contains('='))
                {
                    var parts = line.Split('=', 2);
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    Sections[currentSection][key] = value;
                }
            }

            // Popola proprietà principali
            if (Sections.TryGetValue("General", out var general))
            {
                int.TryParse(general.GetValueOrDefault("Redis"), out int redis);
                Redis = redis;

                int.TryParse(general.GetValueOrDefault("OPCUA"), out int opcua);
                OpcUa = opcua;
            }

            if (Sections.TryGetValue("ServerOPCUA", out var opc))
            {
                ServerOpcUaAddress = opc.GetValueOrDefault("Address") ?? "";
                ServerOpcUaCsvFile = opc.GetValueOrDefault("CsvFile") ?? "";
            }

            if (Sections.TryGetValue("Redis", out var redisSec))
            {
                RedisAddress = redisSec.GetValueOrDefault("Address") ?? "";
                RedisCsvFile = redisSec.GetValueOrDefault("CsvFile") ?? "";
            }
        }

        // Metodo helper per leggere qualsiasi chiave di qualsiasi sezione
        public string? Get(string section, string key)
        {
            if (Sections.TryGetValue(section, out var sec))
                return sec.GetValueOrDefault(key);
            return null;
        }

        // Metodo helper per ottenere tutte le chiavi di una sezione
        public Dictionary<string, string> GetSection(string section)
        {
            if (Sections.TryGetValue(section, out var sec))
                return sec;
            return new Dictionary<string, string>();
        }
    }
}
