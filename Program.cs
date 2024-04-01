using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Opendownload
{
    public class Opendownload_main
    {
        private string downloadUrl;
        private string savePath;
        private long startTime;
        private long totalDownloaded;

        private int updateCount;

        public Opendownload_main(string downloadUrl, string savePath)
        {
            this.downloadUrl = downloadUrl;
            this.savePath = savePath;
        }

        private bool CheckDiskSpace()
        {
            DirectoryInfo saveFolder = new DirectoryInfo(savePath);
            long requiredSpace = 0;
            try
            {
                Uri url = new Uri(downloadUrl);
                HttpWebRequest connection = (HttpWebRequest)WebRequest.Create(url);
                connection.Method = "HEAD";
                using (HttpWebResponse response = (HttpWebResponse)connection.GetResponse())
                {
                    requiredSpace = response.ContentLength;
                }
                DriveInfo drive = new DriveInfo(saveFolder.Root.FullName);
                long usableSpace = drive.AvailableFreeSpace;
                if (requiredSpace > usableSpace)
                {
                    ShowDiskSpaceNotification(drive, requiredSpace);
                    return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
            return true;
        }

        private void ShowDiskSpaceNotification(DriveInfo drive, long requiredSpace)
        {
            string title = drive.Name + " 上的空间不足以下载此文件";
        }

        public async Task Run()
        {
            try
            {
                if (!CheckDiskSpace())
                {
                    Main(new string[] { });
                    return;
                }
                Uri url = new Uri(downloadUrl);
                HttpWebRequest connection = (HttpWebRequest)WebRequest.Create(url);
                connection.Method = "GET";
                connection.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";
                using (HttpWebResponse response = (HttpWebResponse)await connection.GetResponseAsync())
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            break;
                        case HttpStatusCode.Unauthorized:
                            ShowErrorMessage("未授权的资源访问。");
                            return;
                        case HttpStatusCode.Forbidden:
                            ShowErrorMessage("禁止访问。");
                            return;
                        case HttpStatusCode.NotFound:
                            ShowErrorMessage("文件未找到。");
                            return;
                        case HttpStatusCode.InternalServerError:
                            ShowErrorMessage("服务器内部错误。");
                            return;
                        default:
                            if ((int)response.StatusCode >= 400)
                            {
                                ShowErrorMessage("未知错误。HTTP状态码：" + (int)response.StatusCode);
                                return;
                            }
                            break;
                    }
                    long fileLength = response.ContentLength;
                    string fileName = Path.GetFileName(url.LocalPath);

                    ShowNotification(fileName, fileLength, 0, "Calculating...");

                    DirectoryInfo saveFolder = new DirectoryInfo(savePath);
                    if (!saveFolder.Exists)
                    {
                        saveFolder.Create();
                        Console.WriteLine("文件夹已创建：" + savePath);
                    }

                    using (Stream input = response.GetResponseStream())
                    using (Stream output = File.Create(Path.Combine(savePath, fileName)))
                    {
                        byte[] data = new byte[1024];
                        long total = 0;
                        int count;
                        startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        while ((count = await input.ReadAsync(data, 0, data.Length)) > 0)
                        {
                            total += count;
                            await output.WriteAsync(data, 0, count);
                            int progress = (int)(total * 100 / fileLength);

                            UpdateProgress(fileName, fileLength, total, progress, CalculateETA(startTime, total, fileLength));
                        }
                    }

                    ShowNotification(fileName, fileLength, 100, "Download complete");
                    Console.WriteLine("下载完成：" + Path.Combine(savePath, fileName));
                }
            }
            catch (Exception e)
            {
                string errorMessage = "下载时出现错误：" + e.Message;
                string stackTrace = e.StackTrace;
                Console.WriteLine(errorMessage);
                Console.WriteLine("堆栈日志输出的信息：");
                Console.WriteLine(stackTrace);
                Console.WriteLine("请不要将此控制台截图发给别人，这没有任何作用。");
                Console.WriteLine("你应该全选提交控制台输出的所有信息，并前往 https://github.com/Lavaver/Opendownload 提交一个新的 Issue。");
                Console.WriteLine("而且，对于部分检查请求头极为离谱的网站仍存在 403 错误，请见谅！");
                ShowErrorMessage(errorMessage);
            }
        }

        private void ShowNotification(string fileName, long fileSize, int progress, string message)
        {
            string title = fileName + " 的下载已" + (progress == 0 ? "开始" : "完成");
            string content = (progress == 0 ? "文件大小：" + FormatSize(fileSize) + "\n返回到控制台页面以查看详情。" : "文件已下载到 " + savePath + "\n你可能需要按下 Ctrl+C 来关闭此程序");
        }

        private void UpdateProgress(string fileName, long fileSize, long downloaded, int progress, string eta)
        {
            string progressInfo = "已接收：" + FormatSize(downloaded) + " / " + FormatSize(fileSize) + " 数据" + " | 进度：" + progress + "% | 剩余时间（ETA）：" + eta;

            long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long elapsedTime = currentTime - startTime;
            double speed = (double)downloaded / elapsedTime;
            string speedInfo = "下载速度：" + FormatSize((long)speed) + "/s";

            totalDownloaded += downloaded;
            updateCount++;
            double averageSpeed = (double)totalDownloaded / (elapsedTime / 1000);
            string averageSpeedInfo = "平均速度：" + FormatSize((long)averageSpeed) + "/s";

            progressInfo += " | " + speedInfo + " | " + averageSpeedInfo;
            Console.WriteLine(progressInfo);
        }

        private string CalculateETA(long startTime, long downloaded, long fileSize)
        {
            long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long elapsedTime = currentTime - startTime;
            long remainingTime = (fileSize - downloaded) * elapsedTime / downloaded;
            return remainingTime / 1000 + " 秒";
        }

        private string FormatSize(long size)
        {
            if (size < 1024)
            {
                return size + " 字节";
            }
            else if (size < 1024 * 1024)
            {
                return string.Format("{0:F2} KiB", size / 1024.0);
            }
            else if (size < 1024 * 1024 * 1024)
            {
                return string.Format("{0:F2} MiB", size / (1024.0 * 1024));
            }
            else
            {
                return string.Format("{0:F2} GiB", size / (1024.0 * 1024 * 1024));
            }
        }


        private void ShowErrorMessage(string message)
        {
            Console.WriteLine(message, "下载出错");
        }

        public static void Main(string[] args)
        {
            if (args.Length == 2)
            {
                string downloadUrl = args[0];
                string savePath = args[1];
                Opendownload_main downloader = new Opendownload_main(downloadUrl, savePath);
                downloader.Run().Wait();
                return;
            }
            else if (args.Length == 3 && args[0].Equals("-download"))
            {
                string downloadUrl = args[1];
                string savePath = args[2];
                Opendownload_main downloader = new Opendownload_main(downloadUrl, savePath);
                downloader.Run().Wait();
                return;
            }
            else if (args.Length == 1 && args[0].Equals("-about"))
            {
                Console.WriteLine("Shiwulu OpenDownload");
                Console.WriteLine("请支持自由软件事业的开发，谢谢！");
                Console.WriteLine("如果你是通过购买而来的此发行版本体，那么你应该要求退款，并做法律程序。");
                Console.WriteLine("由 Lavaver 开发、发行的实用下载本体。2.1.2.70d Build 7 LTS 发行版");
            }
            else if (args.Length == 1 && args[0].Equals("-help"))
            {
                Console.WriteLine("帮助");
                Console.WriteLine("----------------");
                Console.WriteLine("下载文件请使用 -download [下载地址] [保存路径] 开始一个新下载。");
                Console.WriteLine("使用 -create 以在引导模式下下载");
                Console.WriteLine("使用 -about 获取发行版本体相关信息，使用 -help 呼出此页。");
                Console.WriteLine("使用 -updatelog 呼出更新日志");
            }
            else if (args.Length == 1 && args[0].Equals("-updatelog"))
            {
                Console.WriteLine("更新日志（LTS 70c3-d 版本）");
                Console.WriteLine("----------------");
                Console.WriteLine("- 本次更新包括了部分增量改进。");
                Console.WriteLine("- 本次更新针对于 .NET Framework 包括了部分增量改进，去除了多余元素适应开发环境");
                Console.WriteLine("有关详细信息，请参阅 https://github.com/Lavaver/Opendownload/releases/tag/2.1.2.70c3");
            }
            else if (args.Length == 1 && args[0].Equals("-create"))
            {
                Createnewdownload();
            }
            else
            {
                Createnewdownload();
            }
        }

        private static void Createnewdownload()
        {
            string downloadUrl = GetInput("请输入下载地址：\n请避免使用百度网盘或 GitHub 链接，因为会出错（");
            if (string.IsNullOrEmpty(downloadUrl))
            {
                return;
            }
            string savePath = GetInput("请输入保存路径：");
            if (string.IsNullOrEmpty(savePath))
            {
                return;
            }
            Opendownload_main downloader = new Opendownload_main(downloadUrl, savePath);
            downloader.Run().Wait();
        }

        private static string GetInput(string message)
        {
            Console.WriteLine(message);
            return Console.ReadLine();
        }
    }
}


