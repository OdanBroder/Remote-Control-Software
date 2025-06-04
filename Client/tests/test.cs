using System;
using Client.Src.Utils;

namespace Client.Tests
{
    public class Test
    {
        public void TestMethod()
        {
            CollectSystemInfo();
        }

        private void CollectSystemInfo()
        {
            string macAddress = GetID.GetMacAddress();
            string biosSerial = GetID.GetBiosSerial();
            string hddSerial = GetID.GetHddSerial();
            string osInstallationId = GetID.GetOsInstallationId();
            string cpuIdentifier = GetID.GetCpuIdentifier();

            string systemInfo = $"{macAddress}-{biosSerial}-{hddSerial}-{osInstallationId}-{cpuIdentifier}";

            Console.WriteLine($"Collected System Information:\n{systemInfo}");
        }

        public static void Main(string[] args)
        {
            Test test = new Test();
            test.TestMethod();
        }
    }
}
