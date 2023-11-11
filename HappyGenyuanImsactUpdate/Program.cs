/*******HappyGenyuanImsactUpdate*******/
// A hdiff-using update program of a certain anime game.

using System.Reflection;
using YYHEggEgg.Logger;
using YYHEggEgg.Utils;

namespace HappyGenyuanImsactUpdate
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Log.Initialize(new LoggerConfig(
                    max_Output_Char_Count: -1,
                    use_Console_Wrapper: false,
                    use_Working_Directory: false,
                    global_Minimum_LogLevel: LogLevel.Verbose,
                    console_Minimum_LogLevel: LogLevel.Information,
                    debug_LogWriter_AutoFlush: true));

            string? version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
            Log.Info($"欢迎来到更新程序！ (v{version ?? "<unknown>"})");
            Helper.CheckForRunningInZipFile();

            //Not working path, but the path where the program located
            Helper.CheckForTools();

            var path7z = $"{Helper.exePath}\\7z.exe";
            var hpatchzPath = $"{Helper.exePath}\\hpatchz.exe";

            #region Variables
            DirectoryInfo? datadir = null;
            Patch patch;
            CheckMode checkAfter = CheckMode.Null;
            int t = 0;
            List<FileInfo> zips = new();
            bool ifconfigchange = true;
            bool? ifdeletepackage = null;
            bool[] arghaveread = new bool[6];

            bool usingcommandline = false;
            #endregion

            #region Console Usage
            if (args.Length == 0)
            {
                Log.Info("你也可以通过命令行方式使用本程序！", "CommandLine");

                datadir = GetDataPath();

                Log.Info("");
                // 0 -> none, 1 -> basic check (file size), 2 -> full check (size + md5)
                checkAfter = (CheckMode)AskForCheck();

                Log.Info("");

                if (!PkgVersionCheck(datadir, checkAfter))
                {
                    Log.Erro("很抱歉，由于原始文件不正确，更新程序已退出。", nameof(PkgVersionCheck));
                    Log.Erro("按任意键继续···", nameof(PkgVersionCheck));
                    Console.Read();
                    Environment.Exit(1);
                }
                else Log.Info("文件验证通过！", nameof(PkgVersionCheck));

                t = GetZipCount();

                Log.Info("");

                for (int i = 0; i < t; i++)
                {
                    Log.Info("");
                    if (i > 0) Log.Info("现在，请粘贴另一个zip文件的路径。", nameof(GetUpdatePakPath));
                    zips.Add(GetUpdatePakPath(datadir.FullName));
                }
            }
            #endregion
            #region Command LIne Usage
            else
            {
                usingcommandline = true;

                #region Remove '.\'
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].StartsWith('.'))
                        args[i] = args[i].Substring(1);
                }
                #endregion

                for (int i = 0; i < args.Length; i++)
                {
                    args[i] = RemoveDoubleQuotes(args[i]) ?? string.Empty;
                }

                if (args.Length > 0)
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        switch (args[i])
                        {
                            case "-patchAt":
                                ReadAssert(0);
                                datadir = new DirectoryInfo(args[i + 1]);
                                i += 1;
                                break;
                            case "-checkmode":
                                ReadAssert(1);
                                checkAfter = (CheckMode)int.Parse(args[i + 1]);
                                i += 1;
                                break;
                            case "-zip_count":
                                ReadAssert(2);
                                t = int.Parse(args[i + 1]);
                                for (int j = 0; j < t; j++)
                                {
                                    zips.Add(new FileInfo(args[i + 2 + j]));
                                }
                                i += t + 1;
                                break;
                            case "--config_change_guidance":
                                ReadAssert(3);
                                ifconfigchange = bool.Parse(args[i + 1]);
                                i += 1;
                                break;
                            case "--delete_update_packages":
                                ReadAssert(4);
                                ifdeletepackage = true;
                                break;
                            default:
                                Usage();
                                return;
                        }
                    }
                }
                else
                {
                    Usage();
                    return;
                }

                ifdeletepackage ??= false;
            }
            #endregion

            #region Input lost Assert
            if (datadir == null || checkAfter == CheckMode.Null || t == 0)
            {
                Usage();
                throw new ArgumentException("Input param lack!");
            }
            #endregion

            patch = new Patch(datadir, path7z, hpatchzPath);

            // Backup the original pkg_version file
            var pkgversionpaths = UpCheck.GetPkgVersion(datadir);
            foreach (var pkgversionpath in pkgversionpaths)
            {
                FileInfo pkgver = new(pkgversionpath);
                if (pkgver.Name == "pkg_version") continue;
                File.Move(pkgversionpath, $"{Helper.tempPath}\\{pkgver.Name}");
            }

            // Due to some reasons, if the deleted files are not there,
            // we'll try to delete them afterwards.
            List<string> delete_delays = new();

            foreach (var zipfile in zips)
            {
                #region Unzip the package
                // NOTE: Because some dawn packages from gdrive has a sub folder, we should move it back.
                // Record the directories now
                var predirs = Directory.GetDirectories(datadir.FullName);

                await OuterInvoke.Run(new OuterInvokeInfo
                {
                    ProcessPath = path7z,
                    CmdLine = $"x \"{zipfile.FullName}\" -o\"{datadir.FullName}\" -aoa -bsp1",
                    StartingNotice = "解压文件···",
                    AutoTerminateReason = $"7z decompress package: {zipfile.FullName} to {datadir.FullName} failed."
                }, 3750);

                Unzipped.MoveBackSubFolder(datadir, predirs);
                #endregion

                await patch.Hdiff();
                delete_delays.AddRange(patch.DeleteFiles());

                Log.Info("\n\n");
            }

            // For some reasons, the package check is delayed to the end.
            // It is a proper change because only the newest pkg_version is valid.
            if (!UpdateCheck(datadir, checkAfter))
            {
                Helper.ShowErrorBalloonTip(5000, "更新失败！",
                    "很抱歉，由于文件不正确，更新程序已退出。");

                Log.Erro("很抱歉，由于原始文件不正确，更新程序已退出。", nameof(PkgVersionCheck));
                Log.Erro("按任意键继续···", nameof(PkgVersionCheck));
                Console.Read();
                Environment.Exit(1);
            }
            else Log.Info("文件验证完成！", nameof(PkgVersionCheck));

            foreach (var pkgversionpath in pkgversionpaths)
            {
                FileInfo pkgver = new(pkgversionpath);
                if (pkgver.Name == "pkg_version") continue;
                if (pkgver.Exists) continue; // pkg_version Overrided

                var backuppath = $"{Helper.tempPath}\\{pkgver.Name}";
                File.Move(backuppath, pkgversionpath);
                if (checkAfter == CheckMode.None)
                {
                    Log.Warn($"{pkgver.Name} 尚未检查，可能不适合当前版本。");
                    continue;
                }

                var checkres = UpCheck.CheckByPkgVersion(datadir, pkgversionpath, checkAfter);

                if (!checkres)
                {
                    Log.Warn($"{pkgver.Name} 不再适合当前版本。您可以修复错误或删除游戏数据目录下的文件。");
                }
            }

            Log.Info("\n\n\n\n\n---------------------\n\n\n\n\n");

            // Change the config.ini of official launcher
            if ((usingcommandline && ifconfigchange) || !usingcommandline)
                ConfigChange(datadir, zips[0], zips[zips.Count - 1]);

            // Handling with delayed deletions
            foreach (var deletedfile in delete_delays)
                if (File.Exists(deletedfile))
                    File.Delete(deletedfile);

            Helper.ShowInformationBalloonTip(5000, "更新完成！", "享受新版本吧~");

            DeleteZipFilesReq(zips, ifdeletepackage);
            Log.Info("-------------------------");

            Helper.TryDisposeTempFiles();

            Log.Info("更新完成！");

            if (args.Length == 0)
            {
                Log.Info("按Enter继续");

                Console.ReadLine();
            }
            else
            {
                Log.Info("程序在3秒后退出···");
                await Task.Delay(3000);
            }

            #region Multiple Read Assert
            void ReadAssert(int expected)
            {
                if (arghaveread[expected])
                {
                    Log.Info("Duplicated param!");
                    Usage();
                    Environment.Exit(1);
                }
                arghaveread[expected] = true;
            }
            #endregion
        }

        private static void Usage()
        {
            Log.Info("命令行使用方法: \n" +
                "happygenyuanimsactupdate \n" +
                "-patchAt <游戏路径> \n" +
                "-checkmode <0/1/2> (0 -> 不验证, 1 -> 快速验证 (验证文件大小), 2 -> 完整验证 (文件大小 + md5))\n" +
                "-zip_count <zip更新包数量> <zip路径1...n> \n" +
                "[--config_change_guidance <true/false>] (change the showing version of official launcher, default is true)\n" +
                "[--delete_update_packages] (delete update packages, won't delete if the param isn't given)" +
                "\n\n" +
                "示例： happygenyuanimsactupdate -patchAt \"D:\\Game\" -checkmode 1 -zip_count 2 \"game_1_hdiff.zip\" \"zh-cn_hdiff.zip\" " +
                "--config_change_guidance false\n", "CommandLine");
        }

        #region Change config for official launcher
        /// <summary>
        /// Change config for official launcher
        /// </summary>
        /// <param name="datadir">Game Data dir</param>
        /// <param name="zipstart">Used for infering the update version</param>
        /// <param name="zipend">Used for infering the update version</param>
        public static void ConfigChange(DirectoryInfo datadir, FileInfo zipstart, FileInfo zipend)
        {
            if (!File.Exists($"{datadir}\\config.ini")) return;

            Helper.ShowInformationBalloonTip(5000, "更新程序需要你做出决定。",
                "你可以在这里更新你的官方启动器信息。");

            Log.Info("我们注意到你正在使用官方启动器。", nameof(ConfigChange));
            Log.Info("为了让官方启动器正常识别，需要修改配置文件的游戏版本。", nameof(ConfigChange));

            string verstart = ConfigIni.FindStartVersion(zipstart.Name);
            string verto = ConfigIni.FindToVersion(zipend.Name);

            FileInfo configfile = new($"{datadir}\\config.ini");

            if (verstart == string.Empty || verto == string.Empty)
            {
                Log.Warn("我们不确定你正在更新的版本。", nameof(ConfigChange));
                CustomChangeVersion(configfile);
            }
            else
            {
                GetConfigUpdateOptions(configfile, verstart, verto);
            }
        }

        /// <summary>
        /// Ask user for applying the inferred update options
        /// </summary>
        /// <param name="configfile">config.ini</param>
        /// <param name="verstart">the update version</param>
        /// <param name="verto">the update version</param>
        public static void GetConfigUpdateOptions(FileInfo configfile, string verstart, string verto)
        {
            Log.Info($"我们注意到你正在从 {verstart} 更新到 {verto} 。", nameof(ConfigChange));
            Log.Info("对吗？输入'y'已确认 。" +
                "如果不对，请输入正确的版本号。", nameof(ConfigChange));
            Log.Info("如果你不用官方启动器，或者不想更改，请输入'n'。", nameof(ConfigChange));
            string? s = Console.ReadLine();
            if (s == null || s == string.Empty)
            {
                Log.Warn("错误的版本。", nameof(ConfigChange));
                CustomChangeVersion(configfile);
            }
            else if (s.ToLower() == "y")
                ConfigIni.ApplyConfigChange(configfile, verto);
            else if (s.ToLower() == "n") return;
            else if (ConfigIni.VerifyVersionString(s))
                ConfigIni.ApplyConfigChange(configfile, s);
            else
            {
                Log.Warn("错误的版本。", nameof(ConfigChange));
                CustomChangeVersion(configfile);
            }
        }

        /// <summary>
        /// Type a custom version for update
        /// </summary>
        /// <param name="configfile">config.ini</param>
        public static void CustomChangeVersion(FileInfo configfile)
        {
            Log.Info("请输入你正在更新到的版本：", nameof(CustomChangeVersion));
            Log.Info("如果你不用官方启动器，或者不想更改，请输入'n'。", nameof(CustomChangeVersion));

            string? s = Console.ReadLine();
            if (s == null || s == string.Empty)
            {
                Log.Warn("错误的版本。", nameof(CustomChangeVersion));
                CustomChangeVersion(configfile);
            }
            else if (s.ToLower() == "n") return;
            else if (ConfigIni.VerifyVersionString(s))
                ConfigIni.ApplyConfigChange(configfile, s);
            else
            {
                Log.Warn("错误的版本。", nameof(CustomChangeVersion));
                CustomChangeVersion(configfile);
            }
        }
        #endregion

        #region Package Verify
        public static bool UpdateCheck(DirectoryInfo datadir, CheckMode checkAfter)
        {
            Log.Info("开始执行文件验证···\n", nameof(UpdateCheck));

            if (checkAfter == CheckMode.None)
            {
                Log.Info("由于用户的设定，不执行任何验证~", nameof(UpdateCheck));
                return true;
            }

            var pkgversionPaths = UpCheck.GetPkgVersion(datadir);
            if (pkgversionPaths == null || pkgversionPaths.Count == 0)
            {
                Log.Info("找不到版本文件，不执行文件验证。", nameof(UpdateCheck));
                Log.Info("如果你可以找到，请反馈： " +
                    "https://github.com/YYHEggEgg/HappyGenyuanImsactUpdate/issues", nameof(UpdateCheck));
                return true;
            }

            return UpCheck.CheckByPkgVersion(datadir, pkgversionPaths, checkAfter);
        }

        // Check if pkg_version and Audio_pkg_version can match the real condition
        static bool PkgVersionCheck(DirectoryInfo datadir, CheckMode checkAfter)
        {
            if (checkAfter == CheckMode.None)
            {
                Log.Info("没有验证被执行！", nameof(PkgVersionCheck));
                return true;
            }

            var pkgversionPaths = UpCheck.GetPkgVersion(datadir);
            if (!pkgversionPaths.Contains($"{datadir}\\pkg_version"))
            {
                Log.Warn($"找不到pkg_version文件，将不执行文件检查。", nameof(PkgVersionCheck));
                Log.Info($"如果你正在更新《崩坏：星穹铁道》这是正常的。", nameof(PkgVersionCheck));
                return true;
            }

            // ...\??? game\???_Data\StreamingAssets\Audio\GeneratedSoundBanks\Windows
            string old_audio1 = $@"{datadir.FullName}\{Helper.certaingames[0]}_Data\StreamingAssets\Audio\GeneratedSoundBanks\Windows";
            string old_audio2 = $@"{datadir.FullName}\{Helper.certaingames[1]}_Data\StreamingAssets\Audio\GeneratedSoundBanks\Windows";
            string[]? audio_pkgversions = null;
            if (Directory.Exists(old_audio1)) audio_pkgversions = Directory.GetDirectories(old_audio1);
            else if (Directory.Exists(old_audio2)) audio_pkgversions = Directory.GetDirectories(old_audio2);
            else // ver >= 3.6
            {
                // ...\??? game\???_Data\StreamingAssets\AudioAssets
                string new_audio1 = $@"{datadir.FullName}\{Helper.certaingames[0]}_Data\StreamingAssets\AudioAssets";
                string new_audio2 = $@"{datadir.FullName}\{Helper.certaingames[1]}_Data\StreamingAssets\Audio\AudioAssets";

                if (Directory.Exists(new_audio1)) audio_pkgversions = Directory.GetDirectories(new_audio1);
                else if (Directory.Exists(new_audio2)) audio_pkgversions = Directory.GetDirectories(new_audio2);
                else return UpdateCheck(datadir, checkAfter);
            }

            foreach (string audiopath in audio_pkgversions)
            {
                string audioname = new DirectoryInfo(audiopath).Name;
                if (!pkgversionPaths.Contains($"{datadir}\\Audio_{audioname}_pkg_version"))
                {
                    // ver <= 1.4
                    Log.Warn($"不检查语音包: {audioname} 因为 Audio_{audioname}_pkg_version文件不存在。", nameof(PkgVersionCheck));
                }
            }

            return UpdateCheck(datadir, checkAfter);
        }
        #endregion

        #region Param Getting
        //For standarlizing, we use a DirectoryInfo object. 
        //The same goes for the following methods. 
        static DirectoryInfo GetDataPath()
        {
            Log.Info("请输入完整游戏路径：（原神/崩铁） \n" +
                "通常，它的文件夹叫做：Genshin Impact Game（或者/Star Rail/Game）。", nameof(GetDataPath));
            string? dataPath = RemoveDoubleQuotes(Console.ReadLine());
            if (dataPath == null || dataPath == string.Empty)
            {
                Log.Warn("错误的游戏文件夹。", nameof(GetDataPath));
                return GetDataPath();
            }

            DirectoryInfo datadir = new(dataPath);
            if (!Helper.AnyCertainGameExists(datadir))
            {
                Log.Warn("错误的游戏文件夹。", nameof(GetDataPath));
                return GetDataPath();
            }
            else return datadir;
        }

        static FileInfo GetUpdatePakPath(string gamePath)
        {
            Log.Info("请你粘贴更新包路径。" +
                "它必须是一个zip压缩文件。", nameof(GetUpdatePakPath));
            Log.Info("如果文件在游戏路径中，那么不需要输入完整的路径。", nameof(GetUpdatePakPath));
            string? pakPath = RemoveDoubleQuotes(Console.ReadLine());
            if (pakPath == null || pakPath == string.Empty)
            {
                Log.Warn("错误的更新包位置。", nameof(GetUpdatePakPath));
                return GetUpdatePakPath(gamePath);
            }

            FileInfo zipfile = new(pakPath);

            // Fuck why I have tested this
            if (pakPath.Length >= 3)
                if (pakPath.Substring(1, 2) != ":\\")
                {
                    //Support relative path
                    pakPath = $"{gamePath}\\{pakPath}";
                    zipfile = new(pakPath);
                }

            //To protect fools who really just paste its name
            if (zipfile.Extension != ".zip"
                || zipfile.Extension != ".rar"
                || zipfile.Extension != ".7z"
                || zipfile.Extension != ".001")
            {
                if (File.Exists($"{pakPath}.zip")) pakPath += ".zip";
                else if (File.Exists($"{pakPath}.rar")) pakPath += ".rar";
                else if (File.Exists($"{pakPath}.7z")) pakPath += ".7z";
                else if (File.Exists($"{pakPath}.001")) pakPath += ".001";
                zipfile = new(pakPath);
            }

            if (!zipfile.Exists)
            {
                Log.Warn("错误的更新包位置。", nameof(GetUpdatePakPath));
                return GetUpdatePakPath(gamePath);
            }

            return zipfile;
        }

        static int GetZipCount()
        {
            int rtn = 0;
            Log.Info("你有多少个更新包？", nameof(GetUpdatePakPath));
            if (!int.TryParse(Console.ReadLine(), out rtn))
            {
                Log.Warn("错误的数字！", nameof(GetUpdatePakPath));
                return GetZipCount();
            }
            else return rtn;
        }

        // 0 -> none, 1 -> basic check (file size), 2 -> full check (size + md5)
        static int AskForCheck()
        {
            Log.Info("你想要在更新后进行文件校验吗？", nameof(AskForCheck));
            Log.Info("不想，请输入：0;", nameof(AskForCheck));
            Log.Info("[推荐]快速验证（只验证文件大小），请输入：1;", nameof(AskForCheck));
            Log.Info("完整检查，请输入：2。", nameof(AskForCheck));
            int rtn = 0;
            if (!int.TryParse(Console.ReadLine(), out rtn))
            {
                Log.Warn("错误的选择！", nameof(AskForCheck));
                return AskForCheck();
            }
            else if (rtn < 0 || rtn > 2)
            {
                Log.Warn("错误的选择！", nameof(AskForCheck));
                return AskForCheck();
            }
            else return rtn;
        }
        #endregion

        #region Delete Update Zip File
        /// <param name="delete">true=delete; false=reserve; null=not given, ask the user</param>
        static void DeleteZipFilesReq(List<FileInfo> zips, bool? delete = null)
        {
            if (delete == null)
            {
                Log.Info("更新包一般而言不再需要。", nameof(DeleteZipFilesReq));
                Log.Info("你需要删掉吗？输入‘y’删掉，‘n’不删掉。", nameof(DeleteZipFilesReq));
                string? s = Console.ReadLine();
                if (s == null)
                {
                    Log.Warn("错误的输入！", nameof(DeleteZipFilesReq));
                    DeleteZipFilesReq(zips);
                    return;
                }
                else if (s.ToLower() == "y")
                {
                    foreach (var zip in zips)
                    {
                        try
                        {
                            zip.Delete();
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(ex.ToString(), nameof(DeleteZipFilesReq));
                            Log.Warn($"删除文件: {zip} 失败。" +
                                $"请自己尝试删除！",
                                nameof(DeleteZipFilesReq));
                        }
                    }
                }
                else if (s.ToLower() == "n")
                {
                    return;
                }
                else
                {
                    Log.Warn("错误的输入！", nameof(DeleteZipFilesReq));
                    DeleteZipFilesReq(zips);
                    return;
                }
            }
            else if ((bool)delete)
            {
                foreach (var zip in zips)
                {
                    try
                    {
                        zip.Delete();
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(ex.ToString(), nameof(DeleteZipFilesReq));
                        Log.Warn($"删除文件: {zip} 失败。 " +
                            $"请自己尝试删除。",
                            nameof(DeleteZipFilesReq));
                    }
                }
            }
        }
        #endregion

        public static string? RemoveDoubleQuotes(string? str)
        {
            if (str == null) return null;
            if (str.StartsWith('"') && str.EndsWith('"'))
                return str.Substring(1, str.Length - 2);
            else return str;
        }
    }
}