using Grpc.Net.Client;

var channel = GrpcChannel.ForAddress("http://localhost:8334");
var client = new InternalCtrl.InternalCtrlClient(channel);
Console.WriteLine("About to Gc...");
var reply = await client.GcAsync(new GcReq { });
Console.WriteLine($"Gc returned: {reply}");
