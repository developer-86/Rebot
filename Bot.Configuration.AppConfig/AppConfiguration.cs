using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Configuration.AppConfig
{
    public class AppConfiguration : IConfiguration
    {
        public string GetParameterStringValue(string parameterName, bool throwIfEmpty = true)
        {
            if (string.IsNullOrEmpty(parameterName))
                throw new ArgumentNullException("parameterName");

            string value = ConfigurationManager.AppSettings[parameterName];
            if (throwIfEmpty && string.IsNullOrEmpty(value))
                throw new Exception(string.Format("Параметр конфигурации {0} не задан",
                    parameterName));

            return value;
        }

        public int GetParameterIntegerValue(string parameterName, bool throwIfEmpty = true)
        {
            if (string.IsNullOrEmpty(parameterName))
                throw new ArgumentNullException("parameterName");

            string stringValue = this.GetParameterStringValue(parameterName, throwIfEmpty);
            int value;
            bool valid = int.TryParse(stringValue, out value);
            if (!valid && throwIfEmpty)
                throw new Exception(string.Format("Параметр конфигурации {0} не задан",
                    parameterName));

            return value;
        }
    }
}