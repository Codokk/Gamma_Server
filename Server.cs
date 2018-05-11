using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;


namespace ConsoleApp1
{
    class Server
    {
        public static Server[] ActiveServers = new Server[500];
        public ushort Port;
        public int Players;
        public const int MaxPlayers = 10;
        public ushort GamePort = Program.Gameport + 1;
        public Server()
        {
            int i = 0;
            while (i > ActiveServers.Length)
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