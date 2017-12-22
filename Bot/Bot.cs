using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bot.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;
using TLSharp.Core.Utils;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;
using YVideo = YoutubeExplode.Models.Video;

namespace Bot
{
    public class Bot
    {
        private const int _messageLength = 3000;
        private static object _locker = new object();

        public Bot(IConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException("configuration");

            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; private set; }

        public TLUser CurrentAuthUser { get; private set; }

        private bool __init_TBot;
        private TelegramBotClient _TBot;
        internal TelegramBotClient TBot
        {
            get
            {
                if (!__init_TBot)
                {
                    string botID = this.Configuration.GetParameterStringValue("BotID");
                    string botHash = this.Configuration.GetParameterStringValue("BotHash");
                    string botKey = string.Format("{0}:{1}", botID, botHash);
                    _TBot = new TelegramBotClient(botKey);

                    __init_TBot = true;
                }
                return _TBot;
            }
        }

        private bool __init_Phone;
        private string _Phone;
        private string Phone
        {
            get
            {
                if (!__init_Phone)
                {
                    _Phone = this.Configuration.GetParameterStringValue("Phone");
                    __init_Phone = true;
                }
                return _Phone;
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
                    _SessionName = string.Format("session_{0}", this.Phone);
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
                    int appApiID = this.Configuration.GetParameterIntegerValue("AppApiID");
                    string appApiHash = this.Configuration.GetParameterStringValue("AppApiHash");

                    _TClient = new TelegramClient(appApiID, appApiHash, this.SessionStore, this.SessionName);
                    __init_TClient = true;
                }
                return _TClient;
            }
        }

        private bool __init_YoutubeClient;
        private YoutubeClient _YoutubeClient;
        private YoutubeClient YoutubeClient
        {
            get
            {
                if (!__init_YoutubeClient)
                {
                    _YoutubeClient = new YoutubeClient();
                    __init_YoutubeClient = true;
                }
                return _YoutubeClient;
            }
        }

        private Chat Chat { get; set; }

        public async Task StartAsync()
        {
            await this.TBot.SetWebhookAsync("");

            bool connected = await this.TClient.ConnectAsync();
            if (!connected)
                throw new Exception(string.Format("Не удалось подключиться"));

            bool isPhoneRegistered = await this.TClient.IsPhoneRegisteredAsync(this.Phone);

            if (!this.TClient.IsUserAuthorized())
            {
                string hash = await this.TClient.SendCodeRequestAsync(this.Phone);
                Console.WriteLine("Введите смс код авторизации...");
                string smsCode = Console.ReadLine();
                this.CurrentAuthUser = await this.TClient.MakeAuthAsync(this.Phone, hash, smsCode);
            }
            else
            {
                Session session = this.SessionStore.Load(this.SessionName);
                this.CurrentAuthUser = session.TLUser;
            }
        }

        public async Task<BotUser> GetUserAsync(string nikName)
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

            if (message.Length > _messageLength)
                message = message.Substring(0, _messageLength - 6) + "...EOF";

            await this.TBot.SendTextMessageAsync(this.Chat, message);
        }

        public async Task<bool> ActivateChatAsync()
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




        private TLAbsInputFile UploadLocalFileToTelegram(string path, string name)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");

            TLAbsInputFile fileResult;

            lock (_locker)
            {
                this.SendMessage("Начало отправки в Telegram");

                using (FileStream fs = System.IO.File.OpenRead(path))
                {
                    Task<TLAbsInputFile> uploadTask = this.TClient.UploadFile(name, new StreamReader(fs));
                    uploadTask.Wait();
                    fileResult = uploadTask.Result;
                }
            }

            return fileResult;
        }

        private long Offset;

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

        private void ProcessMessagesWorker(object state)
        {
            while (true)
            {
                try
                {
                    if (!this.TClient.IsConnected)
                    {
                        Console.WriteLine("reconnect...");
                        Task reconnectTask = this.StartAsync();
                        reconnectTask.Wait();

                        if (this.TClient.IsConnected)
                            Console.WriteLine("reconnect completed !");
                        else
                            Console.WriteLine("reconnect failed !!!!");
                    }

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

                    try
                    {
                        this.SendMessage(ex.ToString());
                    }
                    catch (Exception internalEx)
                    {
                        Console.WriteLine("INTERNAL_EXCEPTION: {0}", internalEx);
                    }
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
                    case BotRequestType.Download:
                        string url = null;
                        if (request.CmdParams.Length > 1)
                            url = request.CmdParams[1];

                        if (string.IsNullOrEmpty(url))
                            throw new Exception(string.Format("Не задана ссылка на видео"));

                        this.TransferVideo(url);
                        break;
                    default:
                        throw new Exception(string.Format("Неизвестная команда"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                this.SendMessage(ex.ToString());
            }
        }

        private void TransferVideo(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException("url");

            var downloadTask = this.TransferVideoAsync(url);
            downloadTask.Wait();
        }

        private async Task TransferVideoAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException("url");

            DirectoryInfo dir = null;
            string dirPath = this.Configuration.GetParameterStringValue("DownloadFolder");
            if (!Directory.Exists(dirPath))
                dir = Directory.CreateDirectory(dirPath);
            else
                dir = new DirectoryInfo(dirPath);

            string tmpFile = string.Format("{0}\\{1}.mp4",
                dir.FullName,
                Guid.NewGuid());

            var videoID = YoutubeClient.ParseVideoId(url);
            var streamInfos = await this.YoutubeClient.GetVideoMediaStreamInfosAsync(videoID);
            VideoStreamInfo videoStream = streamInfos.Video[0];
            YVideo video = await this.YoutubeClient.GetVideoAsync(videoID);

            await this.SendMessageAsync("Начало локальной загрузки файла");
            await this.YoutubeClient.DownloadMediaStreamAsync(videoStream, tmpFile);
            await this.SendLocalVideoAsync(video, videoStream, tmpFile);

            await this.SendMessageAsync("Окончание передачи файла");
        }

        public async Task SendLocalVideoAsync(YVideo video, VideoStreamInfo videoStream, string path)
        {
            if (video == null)
                throw new ArgumentNullException("video");

            if (videoStream == null)
                throw new ArgumentNullException("videoStream");

            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            FileInfo file = new FileInfo(path);
            if (!file.Exists)
                throw new Exception(string.Format("Файл по адресу {0} не найден",
                    path));

            TLAbsInputFile fileResult = this.UploadLocalFileToTelegram(path, video.Title);
            string mimeType = "video/mp4";
            TLDocumentAttributeVideo attr1 = new TLDocumentAttributeVideo()
            {
                Duration = (int)video.Duration.TotalSeconds + 1,
                H = videoStream.Resolution.Height,
                W = videoStream.Resolution.Width,
            };

            TLVector<TLAbsDocumentAttribute> attrs = new TLVector<TLAbsDocumentAttribute>();
            attrs.Add(attr1);

            TLInputPeerUser peer = new TLInputPeerUser() { UserId = this.CurrentAuthUser.Id };
            var sendTask = this.TClient.SendUploadedDocument(
                peer, fileResult, video.Title, mimeType, attrs);

            sendTask.Wait();
        }

        /* TESTS */
        /*public async Task SendTwoFilesAsync()
        {
            WaitCallback callback = new WaitCallback((state) =>
            {
                string path = state.ToString();

                TLAbsInputFile fileResult;
                FileStream fs = System.IO.File.OpenRead(path);
                var sr = new StreamReader(fs);
                var uploadTask = this.TClient.UploadFile("test.mp4", sr);
                uploadTask.Wait();
                fileResult = uploadTask.Result;
            });

            string p1 = @"C:\Rebot\d5fb9643-ad4d-47f3-8fd2-19aa2bac1034.mp4";
            string p2 = @"C:\Rebot\ca266fb8-515c-4d5f-9fa7-c65bab6a26db.mp4";
            ThreadPool.QueueUserWorkItem(callback, p1);
            ThreadPool.QueueUserWorkItem(callback, p2);
        }*/
    }
}