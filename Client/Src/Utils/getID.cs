using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;

namespace Client.Src.Utils
{
    public static class GetID
    {
        public static string GetMacAddress()
        {
            try
            {
                return NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                    .Select(nic => nic.GetPhysicalAddress().ToString())
                    .FirstOrDefault() ?? "Unknown";
            }
            catch { return "Unknown"; }
        }

        public static string GetBiosSerial()
        {
            return GetWmiProperty("Win32_BIOS", "SerialNumber");
        }

        public static string GetHddSerial()
        {
            return GetWmiProperty("Win32_DiskDrive", "SerialNumber");
        }

        public static string GetOsInstallationId()
        {
            return GetWmiProperty("Win32_OperatingSystem", "SerialNumber");
        }

        public static string GetCpuIdentifier()
        {
            return GetWmiProperty("Win32_Processor", "Name");
        }

        private static string GetWmiProperty(string wmiClass, string wmiProperty)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT {wmiProperty} FROM {wmiClass}"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        return obj[wmiProperty]?.ToString().Trim() ?? "Unknown";
                    }
                }
            }
            catch { }
            return "Unknown";
        }
    }
}
