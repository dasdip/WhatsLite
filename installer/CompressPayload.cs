using System;
using System.IO;
using System.IO.Compression;

class CompressPayload
{
    static void Main()
    {
        string instDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        string srcDir = Path.GetDirectoryName(instDir);

        string[] files = new string[] {
            Path.Combine(srcDir, "WhatsLite.exe"),
            Path.Combine(srcDir, "Microsoft.Web.WebView2.Core.dll"),
            Path.Combine(srcDir, "Microsoft.Web.WebView2.WinForms.dll"),
            Path.Combine(srcDir, "WebView2Loader.dll"),
            Path.Combine(srcDir, "app.ico"),
            Path.Combine(instDir, "Uninstall.exe")
        };

        long totalRaw = 0;
        long totalGz = 0;

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            string outGz = Path.Combine(instDir, name + ".gz");

            byte[] rawBytes = File.ReadAllBytes(file);
            totalRaw += rawBytes.Length;

            using (FileStream fs = new FileStream(outGz, FileMode.Create, FileAccess.Write))
            using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress))
            {
                gz.Write(rawBytes, 0, rawBytes.Length);
            }

            long gzLen = new FileInfo(outGz).Length;
            totalGz += gzLen;

            Console.WriteLine(string.Format("  Compressed {0}: {1:N0} bytes -> {2:N0} bytes", name, rawBytes.Length, gzLen));
        }

        Console.WriteLine(string.Format("\n  Total: {0:N0} bytes compressed to {1:N0} bytes ({2:F1}% compression)",
            totalRaw, totalGz, 100.0 * (1.0 - (double)totalGz / totalRaw)));
    }
}
