using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.MsgPack;
using Context = CachingFramework.Redis.Context;

namespace TemporaryCache
{
    class Program
    {
        static void Main(string[] args)
        {
            Test_WithSignal();
            Test_WithBusyWaiting();
        }

        private static void Test_WithSignal()
        {
            var context = new CachingFramework.Redis.Json.Context();
            var guid = Guid.NewGuid().ToString();
            var pendingFlag = $"flag{guid}";
            context.Cache.SetObject(pendingFlag, true, TimeSpan.FromSeconds(15));
            var tProducer = Task.Run(() =>
            {
                Thread.Sleep(5500);
                context.Cache.SetObject(guid, new { Name = "alex", Created = DateTime.UtcNow }, TimeSpan.FromSeconds(10));
                context.Cache.Remove(pendingFlag);
                context.PubSub.Publish(pendingFlag, true);
            });

            var tConsumer = Task.Run(() =>
            {
                var handle = new ManualResetEventSlim();
                context.PubSub.Subscribe<bool>(pendingFlag, x => handle.Set());

                if (context.Cache.KeyExists(pendingFlag))
                {

                    Console.WriteLine("Waiting for data");
                    var sw = new Stopwatch();
                    sw.Start();
                    if (handle.Wait(20000))
                    {
                        sw.Stop();
                        Console.WriteLine($"Data ready after {sw.Elapsed.TotalMilliseconds} ms");
                    }
                    else
                        Console.WriteLine("Timeout waiting for data");
                }

                Console.WriteLine(
                    context.Cache.FetchObject(guid, () => new { Name = "alex2", Created = DateTime.UtcNow },
                        TimeSpan.FromSeconds(10)).Name);
            });

            Task.WaitAll(tProducer, tConsumer);
            Console.ReadKey();
        }

        private static void Test_WithBusyWaiting()
        {
            var context = new CachingFramework.Redis.Json.Context();
            var guid = Guid.NewGuid().ToString();
            var pendingFlag = $"flag{guid}";
            context.Cache.SetObject(pendingFlag, true, TimeSpan.FromSeconds(15));
            var tProducer = Task.Run(() =>
            {
                Thread.Sleep(5500);
                context.Cache.SetObject(guid, new { Name = "alex", Created = DateTime.UtcNow }, TimeSpan.FromSeconds(10));
                context.Cache.Remove(pendingFlag);
            });

            var tConsumer = Task.Run(() =>
            {
                Console.WriteLine("Waiting for data");
                var sw = new Stopwatch();
                sw.Start();
                while (context.Cache.KeyExists(pendingFlag))
                {
                    Thread.Sleep(1000);
                }
                sw.Stop();
                Console.WriteLine($"Data ready after {sw.Elapsed.TotalMilliseconds} ms");
                Console.WriteLine(
                    context.Cache.FetchObject(guid, () => new { Name = "alex2", Created = DateTime.UtcNow },
                        TimeSpan.FromSeconds(10)).Name);
            });

            Task.WaitAll(tProducer, tConsumer);
            Console.ReadKey();
        }
    }
}
