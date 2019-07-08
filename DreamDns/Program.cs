using clempaul;
using clempaul.Dreamhost.ResponseData;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DreamDns
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage - <api key> <domain to update>");
                Environment.Exit(1);
            }
            Console.WriteLine($"Starting, access key [{args[0]}], domain [{args[1]}]");
            DynamicDns d = new DynamicDns(args[0], args[1]);
            AutoResetEvent are = new AutoResetEvent(false);
            d.Run(are);
            are.WaitOne();
            Environment.Exit(0);
        }
    }

    public class DynamicDns
    {
        DreamhostAPI m_api;
        string m_domain;

        public DynamicDns(string apiKey, string domainToUpdate)
        {
            m_api = new DreamhostAPI(apiKey);
            m_domain = domainToUpdate.ToLower();
        }

        public async void Run(AutoResetEvent are)
        {
            try
            {
                await DoWork();
            }
            catch(Exception e)
            {
                WriteError($"Exception thrown: {e.Message}");
                Console.WriteLine($"Exception thrown: {e.Message}");
                Environment.Exit(1);
            }
            Console.WriteLine("Done!");
            are.Set();
        }

        private async Task DoWork()
        {
            // Get the public IP.
            string publicIp = await GetPublicIp();
            Console.WriteLine($"Found our public Ip as: {publicIp}");

            // Get all of the current records    
            var list = m_api.DNS.ListRecords();
            foreach(DNSRecord record in list)
            {
                // Find all records that match the domain
                if(record.record.ToLower().Equals(m_domain))
                {
                    Console.WriteLine($"Found record, {record.Print()}");
                    if(!record.value.Equals(publicIp))
                    {
                        // If the ip doesn't match remove and re-add it.                        
                        Console.WriteLine($"The IP doesn't match... updating...");

                        // Add a delay so we don't get rate limited.
                        await Task.Delay(1000);

                        RemoveRecord(record);

                        // Add the new updated record back.
                        AddRecord(m_domain, publicIp);
                    }
                    else
                    {
                        Console.WriteLine($"The IP matches our public ip, no need to update.");
                    }
                    return;
                }
            }    

            // Add the record.
            Console.WriteLine($"The domain {m_domain} wasn't found! Adding...");
            AddRecord(m_domain, publicIp);
        }

        private void RemoveRecord(DNSRecord existingRecord)
        {
            // First remove the record.
            Console.WriteLine($"Removing record for {existingRecord.Print()}");
            m_api.DNS.RemoveRecord(existingRecord);
        }

        private void AddRecord(string domain, string ip)
        {
            Console.WriteLine($"Adding A record for {domain} : {ip}");
            m_api.DNS.AddRecord(domain, "A", ip);
        }

        public async Task<string> GetPublicIp()
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync("https://ifconfig.me/");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public void WriteError(string err)
        {
            File.AppendAllText("Error.txt", $"{err}\r\n");
        }
    }

    public static class Extension
    {
        public static string Print(this DNSRecord r)
        {
            return $"{r.record} - {r.type} - {r.value}";
        }
    }
}
