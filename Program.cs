using Grpc.Core;
using Grpc.Net.Client;
using GrpcConnect;
using System.Threading.Channels;

namespace grpcClientTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var httpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true
            };

            var channel = GrpcChannel.ForAddress("https://con-bob-acr.braveglacier-2d105bbd.uksouth.azurecontainerapps.io", new GrpcChannelOptions
            {
                HttpHandler = httpHandler
            });

            var client = new Greeter.GreeterClient(channel);

            using var call = client.SayHelloBiStream();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var inputChannel = Channel.CreateUnbounded<string>();

            _ = Task.Run(async () =>
            {

                var names = new[] { "Liam", "Bob", "Alice", "Denver", "Kevin", "Matthew" };
                foreach (var name in names)
                {
                    await Task.Delay(1000);
                    await inputChannel.Writer.WriteAsync(name, cts.Token);
                }

                inputChannel.Writer.Complete();
            });

            // Tasks to send messages to the server
            var sendTask = Task.Run(async () =>
            {
                await foreach (var name in inputChannel.Reader.ReadAllAsync(cts.Token))
                {
                    Console.WriteLine($"Sending: {name}");
                    await call.RequestStream.WriteAsync(new HelloRequest { Name = name });
                }

                await call.RequestStream.CompleteAsync();
            });

            //Tasks to receive replies from the server
            var receiveTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var reply in call.ResponseStream.ReadAllAsync(cts.Token))
                    {
                        Console.WriteLine($"Received: {reply.Message}");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Stream Cancelled");                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            });

            await Task.WhenAll(sendTask, receiveTask);
            Console.WriteLine("Press any key to Exit");
        }        
    }
}