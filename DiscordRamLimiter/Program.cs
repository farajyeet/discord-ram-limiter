using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordRamLimiter
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        public static int GetDiscord()
        {
            int DiscordId = -1;
            long workingSet = 0;
            foreach(Process discord in Process.GetProcessesByName("Discord"))
            {
                if(discord.WorkingSet64 > workingSet)
                {
                    workingSet = discord.WorkingSet64;
                    DiscordId = discord.Id;
                }
            }
            return DiscordId;
        }

        static void RamLimiter(int min, int max)
        {
            while (GetDiscord() != -1)
            {
                if (GetDiscord() != -1)
                {
                    GC.Collect(); // Force garbage collection

                    GC.WaitForPendingFinalizers(); // Wait for all finalizers to complete before continuing. 

                    if (Environment.OSVersion.Platform == PlatformID.Win32NT) // Check OS version platform 
                    {
                        SetProcessWorkingSetSize(Process.GetProcessById(GetDiscord()).Handle, min, max);
                    }

                    var wmiObject = new ManagementObjectSearcher("select * from Win32_OperatingSystem");

                    var memoryValues = wmiObject.Get().Cast<ManagementObject>().Select(mo => new {
                        FreePhysicalMemory = Double.Parse(mo["FreePhysicalMemory"].ToString()),
                        TotalVisibleMemorySize = Double.Parse(mo["TotalVisibleMemorySize"].ToString())
                    }).FirstOrDefault();

                    if (memoryValues != null)
                    {


                        var percent = ((memoryValues.TotalVisibleMemorySize - memoryValues.FreePhysicalMemory) / memoryValues.TotalVisibleMemorySize) * 100;
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Your current memory usage : {0}", percent);
                        Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("Developers: miaf#2458 - faraj#2607");
                        Thread.Sleep(600);
                    }

                    Thread.Sleep(1);
                }
            }

        }

        static void Main(string[] args)
        {
            Console.Title = "Discord RAM Limiter by Archwish";
            Console.Write("Discord RAM Limiter - "); Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("https://github.com/miaaf - https://github.com/faraaj");

            RamLimiter(-1, -1);
            
        }
    }
}
