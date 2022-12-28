using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web;
using System.Windows.Forms;

namespace ClashUpgrade
{
    public partial class MainForm : Form
    {
        private const string DefaultFilename = "clashN.zip_temp";
        private string fileName;

        public MainForm(string[] args)
        {
            InitializeComponent();
            if (args.Length <= 0) return;
            fileName = string.Join(" ", args);
            fileName = HttpUtility.UrlDecode(fileName);
        }
        private static void ShowWarn(string message)
        {
            MessageBox.Show(message, "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            try
            {
                var existing = Process.GetProcessesByName("clashN");
                foreach (var p in existing)
                {
                    var path = p.MainModule!.FileName;
                    if (path != GetPath("clashN.exe")) continue;
                    p.Kill();
                    p.WaitForExit(100);
                }
            }
            catch (Exception ex)
            {
                // Access may be denied without admin right. The user may not be an administrator.
                ShowWarn("Failed to close clashN(关闭clashN失败).\n" +
                         "Close it manually, or the upgrade may fail.(请手动关闭正在运行的clashN，否则可能升级失败。\n\n" + ex.StackTrace);
            }

            var sb = new StringBuilder();
            try
            {
                if (!File.Exists(fileName))
                {
                    if (File.Exists(DefaultFilename))
                    {
                        fileName = DefaultFilename;
                    }
                    else
                    {
                        ShowWarn("Upgrade Failed, File Not Exist(升级失败,文件不存在).");
                        return;
                    }
                }

                var thisAppOldFile = Application.ExecutablePath + ".tmp";
                File.Delete(thisAppOldFile);
                var startKey = "clashN/";


                using (var archive = ZipFile.OpenRead(fileName))
                {
                    foreach (var entry in archive.Entries)
                    {
                        try
                        {
                            if (entry.Length == 0)
                            {
                                continue;
                            }
                            var fullName = entry.FullName;
                            if (fullName.StartsWith(startKey))
                            {
                                fullName = fullName.Substring(startKey.Length, fullName.Length - startKey.Length);
                            }
                            if (Application.ExecutablePath.ToLower() == GetPath(fullName).ToLower())
                            {
                                File.Move(Application.ExecutablePath, thisAppOldFile);
                            }

                            var entryOuputPath = GetPath(fullName);

                            var fileInfo = new FileInfo(entryOuputPath);
                            fileInfo.Directory.Create();
                            entry.ExtractToFile(entryOuputPath, true);
                        }
                        catch (Exception ex)
                        {
                            sb.Append(ex.StackTrace);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowWarn("Upgrade Failed(升级失败)." + ex.StackTrace);
                return;
            }
            if (sb.Length > 0)
            {
                ShowWarn("Upgrade Failed,Hold ctrl + c to copy to clipboard.\n" +
                         "(升级失败,按住ctrl+c可以复制到剪贴板)." + sb.ToString());
                return;
            }

            Process.Start("clashN.exe");
            MessageBox.Show("Upgrade successed(升级成功)", "", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Close();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        public static string GetExePath()
        {
            return Application.ExecutablePath;
        }

        public static string StartupPath()
        {
            return Application.StartupPath;
        }
        public static string GetPath(string fileName)
        {
            var startupPath = StartupPath();
            if (string.IsNullOrEmpty(fileName))
            {
                return startupPath;
            }
            return Path.Combine(startupPath, fileName);
        }
    }
}
