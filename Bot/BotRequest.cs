using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using VideoLibrary;

namespace Bot
{
    internal class BotRequest
    {
        public BotRequest(Update update)
        {
            if (update == null)
                throw new ArgumentNullException("update");

            this.Update = update;
        }

        public Update Update { get; private set; }

        private bool __init_CmdQuery;
        private string _CmdQuery;
        private string CmdQuery
        {
            get
            {
                if (!__init_CmdQuery)
                {
                    if (this.Update.Message != null)
                    {
                        _CmdQuery = this.Update.Message.Text;
                    }
                    else if (this.Update.CallbackQuery != null)
                    {
                        _CmdQuery = this.Update.CallbackQuery.Data;
                    }
                    else
                        throw new Exception(string.Format("Unknown cmd query"));

                    __init_CmdQuery = true;
                }
                return _CmdQuery;
            }
        }

        private bool __init_CmdParams;
        private string[] _CmdParams;
        public string[] CmdParams
        {
            get
            {
                if (!__init_CmdParams)
                {
                    if (this.IsLink)
                    {
                        _CmdParams = new string[] { "/v", this.CmdQuery};
                    }
                    else
                    {
                        _CmdParams = this.CmdQuery.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        if (_CmdParams.Length == 0)
                            throw new Exception(string.Format("Не удалось прочитать параметры команды"));
                    }

                    __init_CmdParams = true;
                }
                return _CmdParams;
            }
        }

        private bool __init_Type;
        private BotRequestType _Type;
        public BotRequestType Type
        {
            get
            {
                if (!__init_Type)
                {
                    _Type = BotRequestType.Unknown;
                    string cmdType = this.CmdParams[0];
                    if (cmdType.ToLower() == "/v" || this.IsYoutubeLink)
                        _Type = BotRequestType.Download;

                    __init_Type = true;
                }
                return _Type;
            }
        }

        private bool __init_IsLink;
        private bool _IsLink;
        private bool IsLink
        {
            get
            {
                if (!__init_IsLink)
                {
                    _IsLink = this.CmdQuery.ToLower().StartsWith("http://") || this.CmdQuery.ToLower().StartsWith("https://");
                    __init_IsLink = true;
                }
                return _IsLink;
            }
        }

        private bool __init_IsYoutubeLink;
        private bool _IsYoutubeLink;
        private bool IsYoutubeLink
        {
            get
            {
                if (!__init_IsYoutubeLink)
                {
                    if (this.IsLink)
                    {
                        string url = this.CmdParams[0];
                        var allVideos = YouTube.Default.GetAllVideos(url);
                        _IsYoutubeLink = allVideos.Count() > 0;
                    }

                    __init_IsYoutubeLink = true;
                }
                return _IsYoutubeLink;
            }
        }
    }

    internal enum BotRequestType
    {
        Unknown = 0,
        Download = 1,
    }
}