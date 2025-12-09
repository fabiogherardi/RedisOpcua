//using IniParser.Model;
//using Microsoft.AspNetCore.Mvc;
//using Newtonsoft.Json.Linq;
//using StackExchange.Redis;
//using RedisOPCUA.Utils;

//namespace RedisOPCUA.Controllers
//{
//    public class RedisController : Controller
//    {
//        private readonly ConnectionMultiplexer _redis;     
     
//        public RedisController(ConnectionMultiplexer redis)
//        {

//            _redis = redis;
//        }

//        public IActionResult Index()
//        {

//            string iniPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitor.ini");

//            var chiaviRedis = Ini.RedisKeys;

//            // Legge valori iniziali da Redis
//            var db = _redis.GetDatabase();
//            var valoriRedis = new Dictionary<string, string>();
//            foreach (var key in chiaviRedis)
//            {
//                valoriRedis[key] = db.StringGet(key);
//            }


//            //var chiaviOpcUa = IniReader.ReadOpcuaKeys (iniPath);

//            //var tuttelechiavi = chiaviRedis.Concat(chiaviOpcUa).ToList();

//            var tuttelechiavi = chiaviRedis;

//            //// Passa tutto alla view come JSON serializzato
//            ViewData["ChiaviJson"] = System.Text.Json.JsonSerializer.Serialize(tuttelechiavi);
//            ViewData["ValoriJson"] = System.Text.Json.JsonSerializer.Serialize(valoriRedis);

//            return View("Index");

//        }
//    }
//}
