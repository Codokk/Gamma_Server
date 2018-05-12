using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;


namespace GammaServer
{
    class Server
    {
        public static List<Server> ActiveServers = new List<Server>();
        public ushort Port;
        public int Players;
        public const int MaxPlayers = 10;
        public ushort GamePort = (ushort) (Program.Gameport + 1);
        public Server()
        {
            int i = 0;
            while (i > ActiveServers.Count)
            {
                if (ActiveServers[i] == null)
                {
                    ActiveServers[i] = this;
                    Port = GamePort;
                    return;
                }
                i++;
            }
            System.Diagnostics.Process.Start("Gamma_Instance.exe", Port.ToString());
        }
    }
}