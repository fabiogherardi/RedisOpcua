using RedisOPCUA.Services;
using Microsoft.AspNetCore.Mvc;
using Opc.Ua;
using Opc.Ua.Client;
using StackExchange.Redis;
using RedisOPCUA.Utils;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RedisOPCUA.Controllers
{
    public class HomeController : Controller
    {
        private readonly ConnectionMultiplexer? _redis;
        private readonly OpcUaMonitorService? _opcService;
        private readonly Session? _session;

        public HomeController(ConnectionMultiplexer? redis, OpcUaMonitorService? opcService)
        {
            _redis = redis;
            _opcService = opcService;
            _session = opcService?._session;
        }

        public async Task<IActionResult> Index()
        {
            string iniPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitor.ini");

            var chiaviRedis = IniReader.ReadRedisKeys(iniPath);

         
            // Valori Redis
            var valoriRedis = new Dictionary<string, string>();
            if (_redis != null)
            {
                var db = _redis.GetDatabase();
                foreach (var key in chiaviRedis)
                {
                    valoriRedis[key] = db.StringGet(key);
                }
            }

            // Valori OPC UA
            var valoriOpcUa = new Dictionary<string, string>();
            if (_opcService != null && _session != null)
            {
                foreach (var nodo in _opcService._nodiDaMonitorare)
                {
                    DataValue val = _session.ReadValue(new NodeId(nodo.NodeIdFull));
                    // Usa DisplayName come chiave
                    valoriOpcUa[nodo.DisplayName] = val.Value?.ToString() ?? "";
                }
            }

            // Unisci chiavi e valori
            var tuttelechiavi = chiaviRedis.Concat(_opcService?._nodiDaMonitorare.Select(n => n.DisplayName) ?? Enumerable.Empty<string>()).ToList();
            var tuttiivalori = valoriRedis.Concat(valoriOpcUa).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            ViewData["ChiaviJson"] = System.Text.Json.JsonSerializer.Serialize(tuttelechiavi);
            ViewData["ValoriJson"] = System.Text.Json.JsonSerializer.Serialize(tuttiivalori);

            return View("Index");
        }
    }
}