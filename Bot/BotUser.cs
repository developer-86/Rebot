using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;

namespace Bot
{
    /// <summary>
    /// User.
    /// </summary>
    public class BotUser
    {
        internal BotUser(Bot bot, TLUser tUser)
        {
            if (bot == null)
                throw new ArgumentNullException("bot");

            if (tUser == null)
                throw new ArgumentNullException("tUser");

            this.Bot = bot;
            this.TUser = tUser;
        }

        public Bot Bot { get; private set; }

        public TLUser TUser { get; private set; }

        private bool __init_UserName;
        private string _UserName;
        public string UserName
        {
            get
            {
                if (!__init_UserName)
                {
                    _UserName = this.TUser.Username;
                    __init_UserName = true;
                }
                return _UserName;
            }
        }

        private bool __init_Id;
        private int _Id;
        /// <summary>
        /// User Id.
        /// </summary>
        public int Id
        {
            get
            {
                if (!__init_Id)
                {
                    _Id = this.TUser.Id;
                    __init_Id = true;
                }
                return _Id;
            }
        }

        private bool __init_FirstName;
        private string _FirstName;
        /// <summary>
        /// First name.
        /// </summary>
        public string FirstName
        {
            get
            {
                if (!__init_FirstName)
                {
                    _FirstName = this.TUser.FirstName;
                    __init_FirstName = true;
                }
                return _FirstName;
            }
        }

        private bool __init_Phone;
        private string _Phone;
        /// <summary>
        /// User phone.
        /// </summary>
        public string Phone
        {
            get
            {
                if (!__init_Phone)
                {
                    _Phone = this.TUser.Phone;
                    __init_Phone = true;
                }
                return _Phone;
            }
        }
    }
}