using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Kurukuru;

namespace get_azure
{
  class Program
  {
    public static int Main(string[] args)
    {
      var installed = false;

      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        var tempFile = Path.ChangeExtension(Path.GetTempFileName(), "msi");
        var msiName = "https://azurecliprod.blob.core.windows.net/msi/azure-cli-latest.msi";

        Spinner.Start("Checking for Python depency", spinner =>
        {
          if (!DependencyChecker.Python())
          {
            spinner.Fail("Python required to run azure cli");
            return;
          }
        });

        Spinner.Start($"Download Azure CLI from {msiName}", async () =>
        {
          Task t = WebUtils.DownloadAsync(msiName, tempFile);
          t.Wait();
          await t;
        });

        Spinner.Start($"Running Azure CLI installer at {tempFile}", spinner =>
        {
          var p = Process.Start("msiexec.exe", $"/package \"{tempFile}\"");
          p.WaitForExit();
          installed = true;
        });
      }

      if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        Spinner.Start("Running Azure CLI installer via homebrew", spinner =>
        {
          spinner.Info("Checking for dependency of ruby");
          if (!DependencyChecker.Ruby())
          {
            spinner.Fail("ruby required to install azure cli");
            return;
          }
          spinner.Succeed();
        });

        Spinner.Start("Checking for dependency of homebrew", spinner =>
        {
          if (!DependencyChecker.Homebrew())
          {
            spinner.Fail("homebrew required to install azure cli");
            return;
          }
          spinner.Succeed();
        });

        Spinner.Start("Installing azure cli using homebrew", spinner =>
        {
          ShellHelper.Bash("brew update && brew install azure-cli");
          installed = true;
        });
      }

      if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {

      }

      if (installed)
      {
        Console.WriteLine("Close and reopen this command prompt and run \"az login\" to setup the Azure Command Line");
      }

      return 0;
    }
  }

  public static class WebUtils
  {
    //private static Lazy<IWebProxy> proxy = new Lazy<IWebProxy>(() => string.IsNullOrEmpty(Settings.Default.WebProxyAddress) ? null : new WebProxy { Address = new Uri(Settings.Default.WebProxyAddress), UseDefaultCredentials = true });

    public static IWebProxy Proxy
    {
      get { return null; } //WebUtils.proxy.Value; }
    }

    public static Task DownloadAsync(string requestUri, string filename)
    {
      if (requestUri == null)
        throw new ArgumentNullException("requestUri");

      return DownloadAsync(new Uri(requestUri), filename);
    }

    public static async Task DownloadAsync(Uri requestUri, string filename)
    {
      if (filename == null)
        throw new ArgumentNullException("filename");

      if (Proxy != null)
      {
        WebRequest.DefaultWebProxy = Proxy;
      }

      using (var httpClient = new HttpClient())
      {
        using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
        {
          using (
              Stream contentStream = await (await httpClient.SendAsync(request)).Content.ReadAsStreamAsync(),
              stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
          {
            await contentStream.CopyToAsync(stream);
          }
        }
      }
    }
  }

  public static class ShellHelper
  {
    public static string Bash(string cmd)
    {
      var escapedArgs = cmd.Replace("\"", "\\\"");

      var process = new Process()
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "/bin/bash",
          Arguments = $"-c \"{escapedArgs}\"",
          RedirectStandardOutput = true,
          UseShellExecute = false,
          CreateNoWindow = true,
        }
      };
      process.Start();
      string result = process.StandardOutput.ReadToEnd();
      process.WaitForExit();
      return result;
    }
  }

  public static class DependencyChecker
  {
    public static bool Ruby()
    {
      var result = ShellHelper.Bash("command -v ruby");
      return result != string.Empty;
    }
    public static bool Homebrew()
    {
      var result = ShellHelper.Bash("command -v brew");
      return result != string.Empty;
    }

    public static bool Python()
    {
      // checking for windows only
      string keyName = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
      string valueName = "Python.exe";
      if (Registry.GetValue(keyName, valueName, null) == null)
      {
        return false;
      }

      return true;
    }
  }
}



