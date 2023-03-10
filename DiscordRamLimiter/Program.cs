using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiscordRamLimiter
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        [DllImport("kernel32.dll")]
        static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);
        static void RamLimiter()
        {
            int min = -1;
            int max = -1;
            int DiscordId = -1;
            long workingSet = 0;
            foreach (Process discord in Process.GetProcessesByName("Discord"))
            {
                if (discord.WorkingSet64 > workingSet)
                {
                    workingSet = discord.WorkingSet64;
                    DiscordId = discord.Id;
                }
            }
            while (DiscordId != -1)
            {
                if (DiscordId != -1)
                {
                    GC.Collect(); // Force garbage collection
                    GC.WaitForPendingFinalizers(); // Wait for all finalizers to complete before continuing. 
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT) // Check OS version platform 
                    {
                        SetProcessWorkingSetSize(Process.GetProcessById(DiscordId).Handle, min, max);
                    }
                    var wmiObject = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
                    var memoryValues = wmiObject.Get().Cast<ManagementObject>().Select(mo => new {
                        FreePhysicalMemory = Double.Parse(mo["FreePhysicalMemory"].ToString()),
                        TotalVisibleMemorySize = Double.Parse(mo["TotalVisibleMemorySize"].ToString())
                    }).FirstOrDefault();
                    if (memoryValues != null)
                    {
                        var percent = ((memoryValues.TotalVisibleMemorySize - memoryValues.FreePhysicalMemory) / memoryValues.TotalVisibleMemorySize) * 100;
                        Thread.Sleep(200);
                    }
                    Thread.Sleep(1);
                }
            }
        }
        static void Main(string[] args)
        {
            Thread thr = new Thread(RamLimiter);
            thr.Name = "RamLimiterThread";
            thr.IsBackground = true;
            thr.Start();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
