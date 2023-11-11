﻿using System.Drawing;
using System.Windows.Forms;
using YYHEggEgg.Logger;

namespace HappyGenyuanImsactUpdate
{
    /// <summary>
    /// Methods in console interface.
    /// </summary>
    public class Helper
    {
        /// <summary>
        /// 7z.exe and hdiff.exe check.
        /// </summary>
        public static void CheckForTools(bool requirediff = false)
        {
            bool ok = true;
            if (!File.Exists($"{exePath}\\7z.exe"))
            {
                Log.Erro("7z.exe was missing. " +
                    "Please copy it to the path of this program " +
                    "or download the newest release in " +
                    "https://github.com/YYHEggEgg/HappyGenyuanImsactUpdate/releases");
                ok = false;
            }
            if (!File.Exists($"{exePath}\\hpatchz.exe"))
            {
                Log.Erro("hpatchz.exe was missing. " +
                    "Please copy it to the path of this program " +
                    "or download the newest release in " +
                    "https://github.com/YYHEggEgg/HappyGenyuanImsactUpdate/releases");
                ok = false;
            }
            if (requirediff)
            {
                if (!File.Exists($"{exePath}\\hdiffz.exe"))
                {
                    Log.Erro("hpatchz.exe was missing. " +
                        "Please copy it to the path of this program " +
                        "or download the newest release in " +
                        "https://github.com/YYHEggEgg/HappyGenyuanImsactUpdate/releases");
                    ok = false;
                }
            }
            if (!ok)
            {
                Log.Erro("The program will exit after an enter. " +
                    "Please get missing file(s) the right location and restart.");
                Console.ReadLine();
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Program executable path.
        /// </summary>
        public static string exePath { get => AppDomain.CurrentDomain.BaseDirectory; }

        //"ei hei"
        public static readonly string[] certaingames = new string[]
        { 
            // anime game
            "\u0067\u0065\u006e\u0073\u0068\u0069\u006e\u0069\u006d\u0070\u0061\u0063\u0074",
            "\u0079\u0075\u0061\u006e\u0073\u0068\u0065\u006e",
            // Honkai: March 7th
            "\u0053\u0074\u0061\u0072\u0052\u0061\u0069\u006C",
            // "Reserved Executable Name:/\\<>" // OS exename == CN
        };

        public static bool AnyCertainGameExists(DirectoryInfo checkdir)
        {
            foreach (var certaingame in Helper.certaingames)
            {
                if (File.Exists($"{checkdir.FullName}\\{certaingame}.exe")) return true;
            }
            return false;
        }

        private static string? _tmpdir = null;
        private static FileStream? _tmpdirhandle = null;
        public static string tempPath
        {
            get
            {
                if (string.IsNullOrEmpty(_tmpdir))
                {
                    _tmpdir = $"{exePath}\\Temp-HappyGenyuanImsactUpdate-{DateTime.Now:yyyyMMdd-HHmmss}-{new Random().NextInt64()}";
                    Directory.CreateDirectory(_tmpdir);
                    _tmpdirhandle = File.Create($"{_tmpdir}\\_Created By HappyGenyuanImsactUpdate for temp files.txt");
                }
                return _tmpdir;
            }
        }

        public static void TryDisposeTempFiles()
        {
            try
            {
                if (_tmpdir == null) return;
                _tmpdirhandle?.Dispose();
                Directory.Delete(_tmpdir, true);
                _tmpdir = null;
            }
            catch (Exception ex)
            {
                Log.Warn($"----------------------------\n{ex}\n----------------------------", nameof(TryDisposeTempFiles));
                Log.Warn($"无法自动删除临时文件， 你可以自己删除： {_tmpdir} 文件夹。", nameof(TryDisposeTempFiles));
            }
        }

        public static void CheckForRunningInZipFile()
        {
            if (Environment.CurrentDirectory.StartsWith(
                $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/Local/Temp"))
            {
                Log.Warn("你可能在没有解压的情况下运行程序，这将使" +
                    "日志文件在关闭程序后丢失！");
                Log.Warn("请考虑解压程序，以防在更新时遇到问题。");
            }
        }

        public static void ShowWarningBalloonTip(int persisting_ms, string title, string text)
            => ShowBalloonTipCore(title, text, persisting_ms, SystemIcons.Warning);

        public static void ShowInformationBalloonTip(int persisting_ms, string title, string text)
            => ShowBalloonTipCore(title, text, persisting_ms, SystemIcons.Information);

        public static void ShowErrorBalloonTip(int persisting_ms, string title, string text)
            => ShowBalloonTipCore(title, text, persisting_ms, SystemIcons.Error);

        private static void ShowBalloonTipCore(string title, string text, int persisting_ms,
            Icon notifyShowingIcon)
        {
            // Generated by ChatGPT
            NotifyIcon notifyIcon = new NotifyIcon();
            notifyIcon.Visible = true;
            notifyIcon.Icon = notifyShowingIcon;
            notifyIcon.BalloonTipTitle = title;
            notifyIcon.BalloonTipText = text;
            notifyIcon.ShowBalloonTip(persisting_ms);
        }
    }
}
