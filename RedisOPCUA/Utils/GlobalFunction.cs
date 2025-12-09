using Microsoft.Data.Sqlite;
using Opc.Ua;
using Opc.Ua.Client;
using RedisOPCUA.Services;
using StackExchange.Redis;
using System.IO;
using System.Text;
using RedisOPCUA.Models;

namespace RedisOPCUA.Utils
{

    
    public static class GlobalFunction
    {
        // Inizializza il database SQLite con le colonne dinamiche
        public static void InitDbaseRedis(ref SqliteConnection connection, string sqlitePath, List<string> _chiaviDaMonitorare)
        {


            var sb = new StringBuilder();
            sb.Append("CREATE TABLE IF NOT EXISTS RedisSnapshot (Timestamp TEXT");

            // aggiungi colonne per ogni chiave
            foreach (var chiave in _chiaviDaMonitorare)
            {
                sb.Append($", [{chiave}] TEXT");
            }
            sb.Append(");");

            connection = new SqliteConnection($"Data Source={sqlitePath}");
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = sb.ToString();
            cmd.ExecuteNonQuery();
        }


        public static void InitDbaseOpcua(ref SqliteConnection connection, string sqlitePath, List<OpcUaNode> nodiDaMonitorare)
        {
           

            var sb = new StringBuilder();
            sb.Append("CREATE TABLE IF NOT EXISTS OpcUaSnapshot (Timestamp TEXT");

            // aggiungi colonne per ogni DisplayName dei nodi
            foreach (var nodo in nodiDaMonitorare)
            {
                sb.Append($", [{nodo.DisplayName}] TEXT");
            }
            sb.Append(");");

            connection = new SqliteConnection($"Data Source={sqlitePath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = sb.ToString();
            cmd.ExecuteNonQuery();
        }



        // Scrive i valori delle chiavi monitorate in un file CSV
        public static void ScriviCsvRedis(string _logPath, ConnectionMultiplexer _redis, List<string> _chiaviDaMonitorare)
        {
            try
            {
                // Prepara le righe CSV di tutte le chiavi
                var csvLines = new List<string>();
                var chiaveValore = new List<string>(); ;

                string Linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}   ;   ";
                //csvLines.Add("Data/Ora,Chiave,Valore"); // intestazione

         

                foreach (var chiave in _chiaviDaMonitorare)
                {
                    var val = _redis.GetDatabase().StringGet(chiave);
                 
                    Linea += $"{chiave}   =   {val}   ;   ";

                    // ChiaveValore.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{chiave},{val}");
                }

                chiaveValore.Add(Linea);

                string line = string.Join(Environment.NewLine, chiaveValore) + Environment.NewLine;

                // Controlla se il file esiste e se l'ultima riga è uguale
                bool scrivi = true;
                if (File.Exists(_logPath))
                {
                    var tutteLeRighe = File.ReadAllLines(_logPath);
                    if (tutteLeRighe.Length > 0)
                    {
                        string ultimaRiga = tutteLeRighe.Last();
                        if (ultimaRiga.Trim() == Linea.Trim())
                        {
                            scrivi = false; // stessa riga, non scrivere
                        }
                    }
                }

                if (scrivi)
                {
                    File.AppendAllText(_logPath, line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore scrittura CSV: {ex.Message}");
            }
        }


        // Scrive i valori dei nodi monitorati in un file CSV
        // Scrive i valori dei nodi monitorati in un file CSV
        public static void ScriviCsvOpcUa(string logPath, Session session, List<OpcUaNode> nodiDaMonitorare)
        {
            try
            {
                var chiaveValore = new List<string>();
                string Linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}   ;   ";

                foreach (var nodo in nodiDaMonitorare)
                {
                    // Legge il valore del nodo OPC UA usando NodeIdFull
                    DataValue val = session.ReadValue(new NodeId(nodo.NodeIdFull));

                    // Scrive DisplayName come chiave e il valore letto
                    Linea += $"{nodo.DisplayName}   =   {val.Value}   ;   ";
                }

                chiaveValore.Add(Linea);

                string line = string.Join(Environment.NewLine, chiaveValore) + Environment.NewLine;

                // Controlla se il file esiste e se l'ultima riga è uguale
                bool scrivi = true;
                if (File.Exists(logPath))
                {
                    var tutteLeRighe = File.ReadAllLines(logPath);
                    if (tutteLeRighe.Length > 0)
                    {
                        string ultimaRiga = tutteLeRighe.Last();
                        if (ultimaRiga.Trim() == Linea.Trim())
                        {
                            scrivi = false; // stessa riga, non scrivere
                        }
                    }
                }

                if (scrivi)
                {
                    File.AppendAllText(logPath, line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore scrittura CSV OPC UA: {ex.Message}");
            }
        }




        public static void ScriviDbaseRedis(SqliteConnection connection, ConnectionMultiplexer _redis, List<string> chiaviDaMonitorare)
        {
            try
            {
                // Legge l'ultima riga della tabella
                var ultimaRiga = new Dictionary<string, string>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM RedisSnapshot ORDER BY Timestamp DESC LIMIT 1";
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            foreach (var chiave in chiaviDaMonitorare)
                            {
                                ultimaRiga[chiave] = reader[chiave]?.ToString() ?? "";
                            }
                        }
                    }
                }

                bool valoriCambiati = false;
                var columns = new List<string> { "Timestamp" };
                var parameters = new List<string> { "@ts" };
                string timestampCorrente = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var values = new Dictionary<string, object> { ["@ts"] = timestampCorrente };

                foreach (var chiave in chiaviDaMonitorare)
                {
                    columns.Add($"[{chiave}]");
                    var param = $"@{chiave.Replace(":", "_").Replace(";", "_").Replace("=", "_")}";
                    parameters.Add(param);

                    string valStr = _redis.GetDatabase().StringGet(chiave).ToString().Replace("\r", "").Replace("\n", "");
                    values[param] = valStr;

                    // Controlla se il valore è cambiato rispetto all'ultima riga
                    if (!ultimaRiga.ContainsKey(chiave) || ultimaRiga[chiave] != valStr)
                    {
                        valoriCambiati = true;
                    }
                }

                // Forza l'inserimento anche se i valori non sono cambiati,
                // perché il timestamp indica che sono arrivati nuovi dati
                valoriCambiati = true;

                if (valoriCambiati)
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = $"INSERT INTO RedisSnapshot ({string.Join(",", columns)}) VALUES ({string.Join(",", parameters)});";

                    foreach (var kvp in values)
                    {
                        insertCmd.Parameters.AddWithValue(kvp.Key, kvp.Value ?? "");
                    }

                    insertCmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore scrittura DB Redis: {ex.Message}");
            }
        }


        



        // Scrive i valori dei nodi monitorati nel database SQLite

        public static void ScriviDbaseOpcUa(SqliteConnection connection, Session session, List<OpcUaNode> nodiDaMonitorare)
        {
            try
            {
                // Legge l'ultima riga della tabella
                var ultimaRiga = new Dictionary<string, string>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"SELECT * FROM OpcUaSnapshot ORDER BY Timestamp DESC LIMIT 1";
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            foreach (var nodo in nodiDaMonitorare)
                            {
                                ultimaRiga[nodo.DisplayName] = reader[nodo.DisplayName]?.ToString() ?? "";
                            }
                        }
                    }
                }

                bool valoriCambiati = false;
                var columns = new List<string> { "Timestamp" };
                var parameters = new List<string> { "@ts" };
                var values = new Dictionary<string, object> { ["@ts"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };

                foreach (var nodo in nodiDaMonitorare)
                {
                    columns.Add($"[{nodo.DisplayName}]");
                    var param = $"@{nodo.DisplayName.Replace(":", "_").Replace(";", "_").Replace("=", "_")}";
                    parameters.Add(param);

                    DataValue val = session.ReadValue(new NodeId(nodo.NodeIdFull));
                    string valStr = val.Value?.ToString().Replace("\r", "").Replace("\n", "") ?? "";
                    values[param] = valStr;

                    // Controlla se il valore è cambiato rispetto all'ultima riga
                    if (!ultimaRiga.ContainsKey(nodo.DisplayName) || ultimaRiga[nodo.DisplayName] != valStr)
                    {
                        valoriCambiati = true;
                    }
                }

                // Scrive solo se almeno un valore è cambiato
                if (valoriCambiati)
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = $"INSERT INTO OpcUaSnapshot ({string.Join(",", columns)}) VALUES ({string.Join(",", parameters)});";

                    foreach (var kvp in values)
                    {
                        insertCmd.Parameters.AddWithValue(kvp.Key, kvp.Value ?? "");
                    }

                    insertCmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore scrittura DB OPC UA: {ex.Message}");
            }
        }



    }
}
