using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot;
using Bot.Configuration.AppConfig;
using TBot = Bot.Bot;

namespace Rebot
{
    public class Program
    {
        public static void Main()
        {
            AppConfiguration configuration = new AppConfiguration();
            TBot bot = new TBot(configuration);

            try
            {
                Task task = bot.StartAsync();
                task.Wait();
                Console.WriteLine("ok");

                /*
                var taskUser = bot.GetUser("DMAAPrivateBot");
                taskUser.Wait();
                BotUser user = taskUser.Result;

                var taskUser2 = bot.GetUser("its_alright");
                taskUser2.Wait();
                BotUser user2 = taskUser2.Result;
                */

                Console.WriteLine("Активируйте чат бота.");
                Console.ReadLine();

                Task<bool> activateTask = bot.ActivateChatAsync();
                activateTask.Wait();
                bool activated = activateTask.Result;

                if (activated)
                {
                    Console.WriteLine("Бот запущен");
                }
                else
                {
                    Console.WriteLine("Не удалось активировать чат бота.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                Console.ReadLine();
            }
        }
    }
}