using ChaseLabs.CLLogger;
using ChaseLabs.CLLogger.Interfaces;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CCMusicConverter;
class Program
{
    ILog log = LogManager.Init().SetDumpMethod(DumpType.NoDump).SetMinimumLogType(Lists.LogTypes.All).SetLogDirectory(Path.GetFullPath("./latest.log"));
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
    /// <summary>
    /// Convert input files to wav format
    /// </summary>
    /// <param name="input">Input folder</param>
    /// <param name="output">Output folder</param>
    /// <param name="exe">FFMPEG Executable</param>
    void ConvertToWav(string input, string output, string exe)
    {
        Parallel.ForEach(Directory.GetFiles(input, "*.*", SearchOption.AllDirectories), new ParallelOptions() { MaxDegreeOfParallelism = 10 }, file =>
        {
            FileInfo info = new(file);
            log.Debug($"Working on \"{Path.GetRelativePath(input, file)}\"");
            string name = info.Name.Replace(info.Extension, "").Trim('.').Trim().Replace(" ", "_");
            name = name.ToLower();
            foreach (char c in name.ToCharArray())
            {
                if (!"1234567890-_qwertyuiopasdfghjklzxcvbnm.".ToCharArray().Contains(c))
                {
                    name = name.Replace("" + c, "");
                }
            }
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

    /// <summary>
    /// Converts the wav file to the Computer Craft audio format
    /// </summary>
    /// <param name="working_dir"></param>
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

    /// <summary>
    /// Extracts the embedded jar from computer craft
    /// </summary>
    /// <returns></returns>
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

    /// <summary>
    /// Downloads FFMPEG
    /// </summary>
    /// <returns></returns>
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
    static void Main(string[] args)
    {
        _ = new Program();
    }
}
