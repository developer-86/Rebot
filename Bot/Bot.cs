using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;
using TLSharp.Core.Utils;
using VideoLibrary;

namespace Bot
{
    public class Bot
    {
        private static object _locker = new object();

        public TLUser CurrentAuthUser { get; private set; }

        private bool __init_TBot;
        private TelegramBotClient _TBot;
        internal TelegramBotClient TBot
        {
            get
            {
                if (!__init_TBot)
                {
                    string botKey = string.Format("{0}:{1}", BotConsts.BotID, BotConsts.BotHash);
                    _TBot = new TelegramBotClient(botKey);

                    __init_TBot = true;
                }
                return _TBot;
            }
        }

        private bool __init_SessionName;
        private string _SessionName;
        private string SessionName
        {
            get
            {
                if (!__init_SessionName)
                {
                    _SessionName = string.Format("session_{0}", BotConsts.Phone);
                    __init_SessionName = true;
                }
                return _SessionName;
            }
        }

        private bool __init_SessionStore;
        private ISessionStore _SessionStore;
        private ISessionStore SessionStore
        {
            get
            {
                if (!__init_SessionStore)
                {
                    _SessionStore = new FileSessionStore();
                    __init_SessionStore = true;
                }
                return _SessionStore;
            }
        }

        private bool __init_TClient;
        private TelegramClient _TClient;
        internal TelegramClient TClient
        {
            get
            {
                if (!__init_TClient)
                {
                    _TClient = new TelegramClient(BotConsts.AppApiID, BotConsts.AppApiHash, this.SessionStore, this.SessionName);
                    __init_TClient = true;
                }
                return _TClient;
            }
        }

        private Chat Chat { get; set; }

        private long Offset;

        public async Task Start()
        {
            await this.TBot.SetWebhookAsync("");

            bool connected = await this.TClient.ConnectAsync();
            if (!connected)
                throw new Exception(string.Format("Не удалось подключиться"));

            bool isPhoneRegistered = await this.TClient.IsPhoneRegisteredAsync(BotConsts.Phone);

            if (!this.TClient.IsUserAuthorized())
            {
                string hash = await this.TClient.SendCodeRequestAsync(BotConsts.Phone);
                string smsCode = null;
                var t = 0;
                this.CurrentAuthUser = await this.TClient.MakeAuthAsync(BotConsts.Phone, hash, smsCode);
            }
            else
            {
                Session session = this.SessionStore.Load(this.SessionName);
                this.CurrentAuthUser = session.TLUser;
            }
        }

        public async Task<BotUser> GetUser(string nikName)
        {
            if (string.IsNullOrEmpty(nikName))
                throw new ArgumentNullException("nikName");

            BotUser user = null;

            string condition = string.Format("@{0}!", nikName);
            var users = await this.TClient.SearchUserAsync(condition);

            if (users != null && users.Users != null && users.Users.Count > 0)
            {
                foreach (TLUser u in users.Users)
                {
                    if (nikName.ToLower() == u.Username.ToLower())
                    {
                        user = new BotUser(this, u);
                    }
                }
            }

            return user;
        }

        public void SendMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullException("message");

            var sendTask = this.SendMessageAsync(message);
            sendTask.Wait();
        }

        public async Task SendMessageAsync(string message)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullException("message");

            if (this.Chat == null)
                throw new Exception(string.Format("Сначала нужно активизировать чат любым сообщением"));

            await this.TBot.SendTextMessageAsync(this.Chat, message);
        }

        private long GetOffset()
        {
            long result = 0;
            lock (_locker)
            {
                result = this.Offset;
            }

            return result;
        }

        private void SetOffset(long offset)
        {
            lock (_locker)
            {
                this.Offset = offset;
                Console.WriteLine("offset: {0}", offset);
            }
        }

        public async Task<bool> ActivateChat()
        {
            bool activated = false;
            int offset = (int)this.GetOffset();
            var updates = await this.TBot.GetUpdatesAsync(offset); // получаем массив обновлений

            long lastOffset = 0;
            foreach (var update in updates)
            {
                if (update != null)
                {
                    if (update.Message != null && update.Message.Chat != null)
                    {
                        this.Chat = update.Message.Chat;
                        activated = true;
                    }

                    lastOffset = update.Id + 1;
                }
            }

            this.SetOffset(lastOffset);

            if (activated)
            {
                Thread observer = new Thread(new ParameterizedThreadStart(this.ProcessMessagesWorker));
                observer.Start();
            }

            return activated;
        }

        private void ProcessMessagesWorker(object state)
        {
            while (true)
            {
                try
                {
                    int offset = (int)this.GetOffset();
                    var updTask = this.TBot.GetUpdatesAsync(offset); // получаем массив обновлений
                    updTask.Wait();

                    if (updTask.Result == null || updTask.Result.Length == 0)
                        continue;

                    long lastOffset = 0;
                    foreach (Update update in updTask.Result) // Перебираем все обновления
                    {
                        WaitCallback callback = new WaitCallback(this.ProcessMessage);
                        ThreadPool.QueueUserWorkItem(callback, update);

                        lastOffset = update.Id + 1;
                    }

                    this.SetOffset(lastOffset);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);

                    this.SendMessage(ex.ToString());
                }
                finally
                {
                    Thread.Sleep(200);
                }
            }
        }

        private void ProcessMessage(object state)
        {
            try
            {
                if (state == null)
                    throw new ArgumentNullException("state");

                Update update = (Update)state;

                BotRequest request = new BotRequest(update);
                switch (request.Type)
                {
                    case BotRequestType.Unknown:
                        throw new Exception(string.Format("Неизвестная команда"));
                        break;
                    case BotRequestType.Download:
                        string url = null;
                        if (request.CmdParams.Length > 1)
                            url = request.CmdParams[1];

                        if (string.IsNullOrEmpty(url))
                            throw new Exception(string.Format("Не задана ссылка на видео"));

                        this.DownloadVideo(url);
                        break;
                    default:
                        throw new Exception(string.Format("Неизвестная команда"));
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                this.SendMessage(ex.ToString());
            }
        }

        private void DownloadVideo(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException("url");

            Console.WriteLine("download: {0}", url);

            var allVideos = YouTube.Default.GetAllVideos(url);
            var allMp4 = allVideos.Where(i => i.FileExtension == ".mp4");

            Dictionary<int, List<YouTubeVideo>> videosByResolutions = new Dictionary<int, List<YouTubeVideo>>();
            foreach (YouTubeVideo v in allMp4)
            {
                List<YouTubeVideo> resolutionVideos = null;
                if (videosByResolutions.ContainsKey(v.Resolution))
                    resolutionVideos = videosByResolutions[v.Resolution];
                else
                {
                    resolutionVideos = new List<YouTubeVideo>();
                    videosByResolutions.Add(v.Resolution, resolutionVideos);
                }

                resolutionVideos.Add(v);
            }

            Func<int, YouTubeVideo> getBestVideo = new Func<int, YouTubeVideo>((int maxRes) =>
            {
                YouTubeVideo bestVideo = null;
                var keys = videosByResolutions.OrderByDescending(v => v.Key);
                foreach (KeyValuePair<int, List<YouTubeVideo>> keyValue in keys)
                {
                    int resolution = keyValue.Key;

                    if (resolution > maxRes)
                        continue;

                    List<YouTubeVideo> videos = videosByResolutions[resolution];
                    foreach (YouTubeVideo v in videos)
                    {
                        if (bestVideo == null)
                            bestVideo = v;
                        else if (v.AudioBitrate > bestVideo.AudioBitrate)
                            bestVideo = v;
                    }

                    if (bestVideo != null && bestVideo.AudioBitrate > 0)
                        break;
                }

                return bestVideo;
            });


            YouTubeVideo best = getBestVideo(int.MaxValue);
            if (best == null)
                throw new Exception(string.Format("Не удалось найти видео для загрузки"));

            bool upload = false;

            upload = this.Upload(best, true);
            if (upload)
                upload = this.Upload(best, false);
            else
            {
                YouTubeVideo normalVideo = getBestVideo(best.Resolution - 1);
                if (normalVideo != null)
                {
                    upload = this.Upload(normalVideo, true);
                    if (upload)
                        upload = this.Upload(normalVideo, false);
                }
            }
        }

        private bool Upload(YouTubeVideo video, bool testAttempt)
        {
            bool result = false;

            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    var getStreamTask = httpClient.GetStreamAsync(video.Uri);
                    getStreamTask.Wait();
                    Stream webStream = getStreamTask.Result;

                    using (webStream)
                    {
                        if (testAttempt)
                        {
                            int limit = 1024 * 1024;
                            Tuple<string, long> writeResult = this.WriteLocal(webStream, limit);
                            if (writeResult.Item2 > limit)
                                result = true;
                        }
                        else
                        {
                            Tuple<string, long> writeResult = this.WriteLocal(webStream, 0);

                            TLAbsInputFile fileResult;
                            using (FileStream fs = System.IO.File.OpenRead(writeResult.Item1))
                            {
                                var downloadTask = this.TClient.UploadFile(video.Title, new StreamReader(fs));
                                downloadTask.Wait();
                                fileResult = downloadTask.Result;
                            }

                            TLInputPeerUser peer = new TLInputPeerUser() { UserId = this.CurrentAuthUser.Id };
                            var sendTask = this.TClient.SendUploadedDocument(
                                peer,
                                fileResult,
                                video.Title,
                                "video/mp4",
                                new TLVector<TLAbsDocumentAttribute>());

                            sendTask.Wait();

                            result = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }

            return result;
        }

        private Tuple<string, long> WriteLocal(Stream webStream, int limit = 0)
        {
            if (webStream == null)
                throw new ArgumentNullException("webStream");

            DirectoryInfo dir = null;
            string dirPath = @"C:\Rebot";
            if (!Directory.Exists(dirPath))
                dir = Directory.CreateDirectory(dirPath);
            else
                dir = new DirectoryInfo(dirPath);

            string tmpFile = string.Format("{0}\\{1}.mp4",
                dir.FullName,
                Guid.NewGuid());

            int totalRead = 0;
            using (FileStream fs = System.IO.File.OpenWrite(tmpFile))
            {
                byte[] buffer = new byte[1024 * 1024];
                int read = 0;

                while ((read = webStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fs.Write(buffer, 0, read);
                    totalRead += read;

                    if (limit > 0 && totalRead > limit)
                        break;
                }
            }

            Tuple<string, long> result = new Tuple<string, long>(tmpFile, totalRead);

            return result;
        }
    }
}