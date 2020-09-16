using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommon.MongoDB
{
    public interface IMongoConfig
    {
        string Host { get; }
        int Port { get; }
        string DBName { get; }
        string AuthDBName { get; }
        string AuthUser { get; }
        string AuthPassword { get; }
        bool Auth { get; }
    }
}
