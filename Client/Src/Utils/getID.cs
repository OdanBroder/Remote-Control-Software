using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace Client.Src.Utils
{
    public static class GetID
    {
        public static string GetUniqueID()
        {
            string macAddress = GetMacAddress();
            string biosSerial = GetBiosSerial();
            string hddSerial = GetDiskDriveSerial();
            string osInstallationId = GetOsInstallationId();
            string cpuIdentifier = GetCpuIdentifier();
            string infoMachine = macAddress + biosSerial + hddSerial + osInstallationId + cpuIdentifier;
            // Hash the string to get a unique identifier
            SHA256 mySHA256 = SHA256.Create();
            byte[] hashValue = mySHA256.ComputeHash(Encoding.ASCII.GetBytes(infoMachine));
            // Convert the byte array to hexadecimal string
            StringBuilder hexString = new StringBuilder();
            foreach (byte b in hashValue)
            {
                hexString.Append(b.ToString("x2"));
            }
            // Take the first 16 digits of the hex string and convert it to decimal value
            string decimalValue = Convert.ToUInt64(hexString.ToString().Substring(0, 16), 16).ToString().Substring(0, 16);

            return decimalValue;
        }
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

        public static string GetDiskDriveSerial()
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
