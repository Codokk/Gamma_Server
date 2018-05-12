using System.Net;
using System.Text;
using GammaServer;

namespace GammaServer.Properties
{
	public static class Properties
	{
		public struct IPPortPair
		{
			public IPAddress IPAddress { get; set; }
			public ushort Port { get; set; }

			public IPPortPair(IPAddress ip, ushort port)
			{
				IPAddress = ip;
				Port = port;
			}
			public IPPortPair(string AddressString)
			{
				string[] tmp = new string[2];

				tmp = AddressString.Split(':');

				IPAddress = IPAddress.Parse(tmp[0]);
				Port = ushort.Parse(tmp[1]);
			}
		}

		public struct SQLDatabase
		{
			public IPPortPair DatabaseAddress { get; set; }
			public string Database { get; set; }
			public string Username { get; set; }
			public string Password { get; set; }

			public SQLDatabase(IPAddress ip, ushort port, string database, string username, string password)
			{
				DatabaseAddress = new IPPortPair(ip, port);

				Database = database;
				Username = username;
				Password = password;
			}
			public SQLDatabase(IPPortPair address, string database, string username, string password)
			{
				DatabaseAddress = address;
				Database = database;
				Username = username;
				Password = password;
			}

			public override string ToString()
			{
				return $"Server={DatabaseAddress.IPAddress};Port={DatabaseAddress.Port};Database={Database};uid={Username};pwd={Password}";
			}
		}

		public static readonly IPAddress Localhost = new IPAddress(0x7f000001); //Hex for 127.0.0.1, don't cast/parse if you can avoid it.

		public static readonly SQLDatabase MainDatabase = new SQLDatabase(Localhost, 3456, "GammaServer", "ServerAdmin", "iV1zKRay8gVMcsl2");
	}
}