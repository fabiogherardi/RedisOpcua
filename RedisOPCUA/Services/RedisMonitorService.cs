using IniParser;
using IniParser.Model;
using RedisOPCUA.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using RedisOPCUA.Utils;

namespace RedisOPCUA.Services
{
    public class RedisMonitorService : BackgroundService
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IHubContext<DataHub> _hub;
        private readonly List<string> _chiaviDaMonitorare;


        private readonly string _logPath;
  
        private SqliteConnection connection;


        public RedisMonitorService(ConnectionMultiplexer redis, IHubContext<DataHub> hub)
        {
            _redis = redis;
            _hub = hub;

            _chiaviDaMonitorare = IniReader.ReadRedisKeys(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitor.ini"));


            // inizializza il percorso del file CSV
            //_logPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "log_redis.csv");

            _logPath = IniReader.ReadIniValue(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitor.ini"), "Redis", "CsvFile").Replace("<APP>", Path.Combine(Directory.GetCurrentDirectory()));

            // inizializza il Dbase sqLite
            GlobalFunction.InitDbaseRedis(ref connection, _chiaviDaMonitorare);

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var subscriber = _redis.GetSubscriber();

            // Sottoscrizione ai Keyspace Notifications
            foreach (var chiave in _chiaviDaMonitorare)
            {
                // Canale speciale di Redis per Keyspace Notifications
                string keyspaceChannel = $"__keyspace@0__:{chiave}";

                await subscriber.SubscribeAsync(keyspaceChannel, async (channel, message) =>
                {
                    // message = "set" o tipo di operazione
                    if (message == "set")
                    {
                        // Leggi il nuovo valore della chiave
                        var valore = _redis.GetDatabase().StringGet(chiave);

                        // Invia al client SignalR
                        await _hub.Clients.All.SendAsync("fieldChanged", new { chiave, valore = valore.ToString() });




                        // -------------  gestion CSV  ----------------
                        GlobalFunction.ScriviCsvRedis(_logPath, _redis, _chiaviDaMonitorare);

                        // -------------  gestion dbase sqlite  ----------------
                        GlobalFunction.ScriviDbaseRedis(connection,_redis, _chiaviDaMonitorare);



                    }
                });
            }

            // Mantieni il servizio attivo
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}


