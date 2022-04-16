using ChaseLabs.CLLogger;
using ChaseLabs.CLLogger.Interfaces;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CCMusicConverter;
class Program
{
    ILog log = LogManager.Init().SetDumpMethod(DumpType.NoDump).SetMinimumLogType(Lists.LogTypes.All).SetLogDirectory(Path.GetFullPath("./latest.log"));
    char[] legalChars = "0123456789.".ToCharArray();
    Program()
    {
        string input = Path.GetFullPath(Directory.CreateDirectory("input").FullName);
        log.Info("Place files in input directory and press enter when ready...");
        Console.ReadLine();
        string output = Path.GetFullPath(Directory.CreateDirectory(Path.Combine("output", DateTime.Now.Ticks.ToString())).FullName);
        ConvertToWav(input, output, DownloadFFMPEG());
        ConvertToCC(output);

        log.Info("Done Processing Files...");
    }
    string GetEmbeddedJar()
    {
        string jar = Path.Combine(Path.GetTempPath(), $"cc-music-{DateTime.Now.Ticks}.jar");
        string resource = Assembly.GetExecutingAssembly().GetManifestResourceNames().ToList().First(n => new FileInfo(n).Extension.Contains("jar")) ?? "";
        if (string.IsNullOrWhiteSpace(resource))
        {
            log.Fatal($"Unable to locate embedded resource!");
            Environment.Exit(1);
        }
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
        {

            if (stream != null)
            {
                using FileStream file = new(jar, FileMode.Create, FileAccess.Write);
                stream.CopyToAsync(file);
            }
            else
            {
                log.Fatal($"Unable to extract jar file from embedded resource!");
                Environment.Exit(1);
            }
        }
        return jar;

    }
    void ConvertToCC(string working_dir)
    {
        string jar = GetEmbeddedJar();

        Parallel.ForEach(Directory.GetFiles(working_dir, "*.wav", SearchOption.TopDirectoryOnly), new() { MaxDegreeOfParallelism = 10 }, file =>
        {
            log.Debug($"Working on \"{Path.GetRelativePath(working_dir, file)}\"");
            FileInfo info = new(file);
            string name = info.Name.Replace(info.Extension, "").Trim('.').Trim().Replace(" ", "_");
            Process process = new()
            {
                StartInfo = new()
                {
                    FileName = "java",
                    Arguments = $"-jar \"{jar}\" \"{file}\" \"{Path.Combine(working_dir, name + ".dfpwm")}\""
                }
            };

            process.Start();
            process.WaitForExit();
            log.Debug($"Finished Processing \"{Path.GetRelativePath(working_dir, file)}\"");
            if (process.ExitCode != 0)
            {
                log.Error($"Unable to convert {info.Name} to DFPWM");
            }
            File.Delete(file);
        });
        Thread.Sleep(1000);
        try
        {
            File.Delete(jar);
        }
        catch { }

    }

    string DownloadFFMPEG()
    {

        string ffmpeg = Path.GetFullPath(Directory.CreateDirectory("ffmpeg").FullName);
        if (!Directory.GetFiles(ffmpeg, "*ffmpeg*", SearchOption.AllDirectories).Any())
        {
            log.Info("Downloading FFMPEG....");
            Xabe.FFmpeg.Downloader.FFmpegDownloader.GetLatestVersion(Xabe.FFmpeg.Downloader.FFmpegVersion.Official, ffmpeg).Wait();
        }
        return Directory.GetFiles(ffmpeg, "*ffmpeg*", SearchOption.AllDirectories).First();
    }
    void ConvertToWav(string input, string output, string exe)
    {
        Parallel.ForEach(Directory.GetFiles(input, "*.*", SearchOption.AllDirectories), new ParallelOptions() { MaxDegreeOfParallelism = 10 }, file =>
        {
            FileInfo info = new(file);
            log.Debug($"Working on \"{Path.GetRelativePath(input, file)}\"");
            string name = info.Name.Replace(info.Extension, "").Trim('.').Trim().Replace(" ", "_");
            name = new Regex("(?![a-zA-Z0-9-_])").Replace(name, "");
            Process process = new()
            {
                StartInfo = new()
                {
                    FileName = exe,
                    Arguments = $"-y -i \"{file}\" -loglevel quiet \"{Path.Combine(output, name + ".wav")}\""
                }
            };

            process.Start();
            process.WaitForExit();
            log.Debug($"Finished Processing \"{Path.GetRelativePath(input, file)}\"");
            if (process.ExitCode != 0)
            {
                log.Error($"Unable to convert {info.Name} to WAV");
            }
        });
        log.Info("Done Converting Files...");
    }
    static void Main(string[] args)
    {
        _ = new Program();
    }
}
