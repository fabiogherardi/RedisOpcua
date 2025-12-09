using RedisOPCUA.Hubs;
using RedisOPCUA.Utils;
using RedisOPCUA.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Opc.Ua;
using Opc.Ua.Client;

namespace RedisOPCUA.Services
{
    public class OpcUaMonitorService : BackgroundService
    {

        public Session? _session;
        private readonly IHubContext<DataHub> _hub;

        private readonly string _logPath;
        private SqliteConnection? connection;

        // Lista unica di nodi
        public readonly List<OpcUaNode> _nodiDaMonitorare;

        
        public OpcUaMonitorService(IHubContext<DataHub> hub)
        {
            _hub = hub;

            _nodiDaMonitorare = new List<OpcUaNode>();

            _logPath = Ini.ServerOPCUACsvFile;
        }

        // ------------------------------------------------------------
        //  Connessione + namespace + inizializzazione DB
        // ------------------------------------------------------------
        public override async Task StartAsync(CancellationToken cancellationToken)
        {

            int nsIndex = -1;
            string nodeIdFull = "";
            string displayName = "";



            await ConnettiAlServerOpcUa();

            _nodiDaMonitorare.Clear();

            // Legge nodi da file ini
            //var nodiDaIni = IniReader.ReadOpcuaNodeInfo(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitor.ini"));

            var nodiDaIni = Ini.OpcuaKeys;

            foreach (var nodo in nodiDaIni)
            {


                // Controlla se il nodo è già in formato completo
                if (nodo.Uri.StartsWith("ns="))
                {

                    nsIndex = -1;
                    nodeIdFull = $"{nodo.Uri};{nodo.NodeId}";

                    displayName = GetDisplayName(_session, nodeIdFull);

                    // Nodo già in formato completo
                    _nodiDaMonitorare.Add(new OpcUaNode
                    {
                        Uri = nodo.Uri,
                        NodeId = nodo.NodeId,
                        NodeIdFull = nodeIdFull,
                        DisplayName = displayName
                    });                 
                }
                // Nodo in formato abbreviato, risolve namespace
                else
                {
                    nsIndex = _session.NamespaceUris.GetIndex(nodo.Uri);
                    nodeIdFull = $"ns={nsIndex};{nodo.NodeId}";

                    displayName = GetDisplayName(_session, nodeIdFull);

                    _nodiDaMonitorare.Add(new OpcUaNode
                    {
                        Uri = nodo.Uri,
                        NodeId = nodo.NodeId,
                        NodeIdFull = nodeIdFull,
                        DisplayName = displayName
                    });
                }
            }

            // Inizializza il DB con i DisplayName
            GlobalFunction.InitDbaseOpcua(ref connection, _nodiDaMonitorare);

            await base.StartAsync(cancellationToken);
        }

        // ------------------------------------------------------------
        //  Connessione OPC UA
        // ------------------------------------------------------------
        private async Task ConnettiAlServerOpcUa()
        {
            var basePki = Path.Combine(Directory.GetCurrentDirectory(), "pki");

            var config = new ApplicationConfiguration
            {
                ApplicationName = "OpcUaMonitor",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(basePki, "own"),
                        SubjectName = "CN=OpcUaMonitor"
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(basePki, "issuer")
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(basePki, "trusted")
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(basePki, "rejected")
                    },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
            };

            Directory.CreateDirectory(Path.Combine(basePki, "own"));
            Directory.CreateDirectory(Path.Combine(basePki, "issuer"));
            Directory.CreateDirectory(Path.Combine(basePki, "trusted"));
            Directory.CreateDirectory(Path.Combine(basePki, "rejected"));

            await config.Validate(ApplicationType.Client);

            //string endpointURL = IniReader.ReadIniValue(
            //    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitor.ini"),
            //    "ServerOPCUA", "Address");

            string endpointURL = Ini.ServerOPCUAAddress;

            var selectedEndpoint = CoreClientUtils.SelectEndpoint(config, endpointURL, false);
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

            _session = await Session.Create(
                config,
                endpoint,
                false,
                "OPC UA Client",
                60000,
                null,
                null
            );

            Console.WriteLine("✔ OPC UA Sessione creata e connessa");
        }

        // ------------------------------------------------------------
        //  Subscription e Monitoraggio nodi
        // ------------------------------------------------------------
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await CreaSubscription();
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task CreaSubscription()
        {
            if (_session == null)
            {
                Console.WriteLine("ERRORE: Sessione OPC UA non creata!");
                return;
            }

            var subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = 100
            };

            foreach (var nodo in _nodiDaMonitorare)
            {
                MonitoredItem monitoredItem = new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = nodo.DisplayName,
                    StartNodeId = new NodeId(nodo.NodeIdFull)
                };

                monitoredItem.Notification += async (monItem, args) =>
                {
                    var valore = ((MonitoredItemNotification)args.NotificationValue)
                                    .Value.WrappedValue.ToString();

                    // INVIA DISPLAYNAME COME CHIAVE
                    await _hub.Clients.All.SendAsync("fieldChanged",
                        new { chiave = nodo.DisplayName, valore });

                    // Scrivi CSV e DB usando DisplayName
                    GlobalFunction.ScriviCsvOpcUa(_logPath, _session, _nodiDaMonitorare);

                    GlobalFunction.ScriviDbaseOpcUa(connection, _session, _nodiDaMonitorare);


                };

                subscription.AddItem(monitoredItem);
            }

            _session.AddSubscription(subscription);
            subscription.Create();
        }

        // ------------------------------------------------------------
        // Funzione per ottenere il DisplayName di un nodo
        // ------------------------------------------------------------
        public string GetDisplayName(Session session, string nodeIdStr)
        {
            var nodeToRead = new ReadValueId
            {
                NodeId = new NodeId(nodeIdStr),
                AttributeId = Attributes.DisplayName
            };

            var nodesToRead = new ReadValueIdCollection { nodeToRead };

            session.Read(
                null,
                0,
                TimestampsToReturn.Neither,
                nodesToRead,
                out DataValueCollection results,
                out DiagnosticInfoCollection diag);

            if (results.Count > 0 && results[0].Value is LocalizedText lt)
                return lt.Text;

            return null;
        }

    }




}