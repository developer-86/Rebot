using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Configuration
{
    public interface IConfiguration
    {
        string GetParameterStringValue(string parameterName, bool throwIfEmpty = true);

        int GetParameterIntegerValue(string parameterName, bool throwIfEmpty = true);
    }
}