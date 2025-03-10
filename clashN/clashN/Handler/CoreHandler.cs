﻿using ClashN.Resx;
using System.Diagnostics;
using System.IO;
using System.Text;
using ClashN.Mode;
using ClashN.Tool;

namespace ClashN.Handler
{
    /// <summary>
    /// core进程处理类
    /// </summary>
    internal class CoreHandler
    {
        private const string CoreConfigRes = Global.coreConfigFileName;
        private CoreInfo coreInfo = null!;
        private Process _process = null!;
        private readonly Action<bool, string> _updateFunc;

        public CoreHandler(Action<bool, string> update)
        {
            _updateFunc = update;
        }

        /// <summary>
        /// 载入Core
        /// </summary>
        public void LoadCore(Config config)
        {
            if (Global.reloadCore)
            {
                var item = ConfigHandler.GetDefaultProfile(ref config);
                if (item == null)
                {
                    CoreStop();
                    ShowMsg(false, ResUI.CheckProfileSettings);
                    return;
                }

                if (item.enableTun && !Utils.IsAdministrator())
                {
                    ShowMsg(false, ResUI.EnableTunModeFailed);
                    return;
                }


                SetCore(config, item, out var blChanged);
                var fileName = Utils.GetConfigPath(CoreConfigRes);
                if (CoreConfigHandler.GenerateClientConfig(item, fileName, false, out var msg) != 0)
                {
                    CoreStop();
                    ShowMsg(false, msg);
                }
                else
                {
                    ShowMsg(true, msg);

                    if (_process != null && !_process.HasExited && !blChanged)
                    {
                        MainFormHandler.Instance.ClashConfigReload(fileName);
                    }
                    else
                    {
                        CoreRestart(item);
                    }
                }
            }
        }


        /// <summary>
        /// Core重启
        /// </summary>
        private void CoreRestart(ProfileItem item)
        {
            CoreStop();
            CoreStart(item);
        }

        /// <summary>
        /// Core停止
        /// </summary>
        public void CoreStop()
        {
            try
            {
                if (_process != null)
                {
                    KillProcess(_process);
                    _process.Dispose();
                    _process = null;
                }
                else
                {
                    if (coreInfo == null || coreInfo.coreExes == null)
                    {
                        return;
                    }

                    foreach (var vName in coreInfo.coreExes)
                    {
                        Process[] existing = Process.GetProcessesByName(vName);
                        foreach (var p in existing)
                        {
                            var path = p.MainModule.FileName;
                            if (path == $"{Utils.GetBinPath(vName, coreInfo.coreType)}.exe")
                            {
                                KillProcess(p);
                            }
                        }
                    }
                }

                //bool blExist = true;
                //if (processId > 0)
                //{
                //    Process p1 = Process.GetProcessById(processId);
                //    if (p1 != null)
                //    {
                //        p1.Kill();
                //        blExist = false;
                //    }
                //}
                //if (blExist)
                //{
                //    foreach (string vName in lstCore)
                //    {
                //        Process[] killPro = Process.GetProcessesByName(vName);
                //        foreach (Process p in killPro)
                //        {
                //            p.Kill();
                //        }
                //    }
                //}
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }
        /// <summary>
        /// Core停止
        /// </summary>
        public static void CoreStopPid(int pid)
        {
            try
            {
                var _p = Process.GetProcessById(pid);
                KillProcess(_p);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private string FindCoreExe()
        {
            var fileName = string.Empty;
            foreach (var name in coreInfo.coreExes)
            {
                var vName = string.Format("{0}.exe", name);
                vName = Utils.GetBinPath(vName, coreInfo.coreType);
                if (File.Exists(vName))
                {
                    fileName = vName;
                    break;
                }
            }
            if (Utils.IsNullOrEmpty(fileName))
            {
                var msg = string.Format(ResUI.NotFoundCore, coreInfo.coreUrl);
                ShowMsg(false, msg);
            }
            return fileName;
        }

        /// <summary>
        /// Core启动
        /// </summary>
        private void CoreStart(ProfileItem item)
        {
            ShowMsg(false, string.Format(ResUI.StartService, DateTime.Now.ToString()));
            ShowMsg(false, $"{ResUI.TbCoreType} {coreInfo.coreType}");

            try
            {
                var fileName = FindCoreExe();
                if (fileName == "") return;

                //Portable Mode
                var arguments = coreInfo.arguments;
                var data = Utils.GetPath("data");
                if (Directory.Exists(data))
                {
                    arguments += $" -d \"{data}\"";
                }

                var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        WorkingDirectory = Utils.GetConfigPath(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };
                if (item.enableTun)
                {
                    p.StartInfo.Verb = "runas";
                }
                p.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        var msg = e.Data + Environment.NewLine;
                        ShowMsg(false, msg);
                    }
                });
                p.Start();
                //p.PriorityClass = ProcessPriorityClass.High;
                p.BeginOutputReadLine();
                //processId = p.Id;
                _process = p;

                if (p.WaitForExit(1000))
                {
                    throw new Exception(p.StandardError.ReadToEnd());
                }

                Global.processJob.AddProcess(p.Handle);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                var msg = ex.Message;
                ShowMsg(true, msg);
            }
        }

        private void ShowMsg(bool updateToTrayTooltip, string msg)
        {
            _updateFunc(updateToTrayTooltip, msg);
        }

        private static void KillProcess(Process p)
        {
            try
            {
                p.CloseMainWindow();
                p.WaitForExit(100);
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(100);
                }
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }

        private int SetCore(Config config, ProfileItem item, out bool blChanged)
        {
            blChanged = true;
            if (item == null)
            {
                return -1;
            }
            var coreType = LazyConfig.Instance.GetCoreType(item);
            var tempInfo = LazyConfig.Instance.GetCoreInfo(coreType);
            if (tempInfo != null && coreInfo != null && tempInfo.coreType == coreInfo.coreType)
            {
                blChanged = false;
            }

            coreInfo = tempInfo;
            if (coreInfo == null)
            {
                return -1;
            }
            return 0;
        }
    }
}
