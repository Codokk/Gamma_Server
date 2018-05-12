using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using static GammaServer.Properties.Properties;

using MySql.Data.MySqlClient;



namespace GammaServer
{
	internal static class Utils
	{
		internal struct LoginPair
		{
			public string Username { get; }
			public string Password { get; }
			public byte[] SendData { get; }

			public LoginPair(IReadOnlyList<byte> data)
			{
				byte[] dataArr = data as byte[] ?? throw new InvalidCastException();

				byte usernameLength = data[1];

				Username = Encoding.ASCII.GetString(dataArr, 2, usernameLength);
				Password = Encoding.ASCII.GetString(dataArr, 2 + usernameLength, data.Count - usernameLength - 2);

				SendData = new byte[20];
			}
			public LoginPair(byte[] data)
			{
				byte usernameLength = data[1];

				Username = Encoding.ASCII.GetString(data, 2, usernameLength);
				Password = Encoding.ASCII.GetString(data, 2 + usernameLength, data.Length - usernameLength - 2);

				SendData = new byte[20];
			}
		}
	}


	internal static class Program
	{
		public static byte Login = 0;
		public static bool Listen = true;
		public static bool GlobalWait = true;
		public const ushort GamePort = 26565;

		//Inter-Server Communications Array
		//public static Array isc;
		public static readonly IPAddress Localhost = new IPAddress(0x7f000001); //Hex for 127.0.0.1, don't cast/parse if you can avoid it.
		public struct Packet
		{
			public IPEndPoint EndPoint;
			public byte[] Data;
		}

		private static readonly MySqlConnection Database = new MySqlConnection(MainDatabase.ToString());
		private static MySqlCommand sql;
		internal static Thread packetSendThread;
		internal static Socket packetSendSocket;
		public static Queue<Packet> sendQueue = new Queue<Packet>();
		static Queue<Packet> recvQueue = new Queue<Packet>();
		static EndPoint recvEndpoint = new IPEndPoint(IPAddress.Any, 0);

		//Initialize Server, Listen
		static void Main(string[] args)
		{
			Console.WriteLine("Initializing Server");

			IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, GamePort);

			packetSendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			packetSendSocket.Bind(endpoint);

			packetSendThread = new Thread(ThreadLoop)
			{
				IsBackground = true
			};

			packetSendThread.Start();

			try
			{
				Database.Open();

			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
				Console.ReadLine();
			}
			Console.WriteLine("Server Ready, Awaiting Packets");
			while (Listen)
			{

				if (TryRecvData(out IPEndPoint sender, out byte[] data))
				{
					//Sender -> IPAddress:Port of User
					//Data -> 1kb Byte Array of data send by User
					Console.WriteLine("Packet Received");
					DecryptPacket(data, sender);
				}
				Thread.Sleep(10);
			}
		}

		//Locks thread, Checks for new packet, Closes
		private static bool TryRecvData(out IPEndPoint from, out byte[] data)
		{
			lock (recvQueue)
			{
				if (recvQueue.Count > 0)
				{
					Packet packet = recvQueue.Dequeue();
					from = packet.EndPoint;
					data = packet.Data;
					return true;
				}
			}
			from = null;
			data = null;
			return false;
		}

		//Packet-Sending Process
		private static void ThreadLoop()
		{
			byte[] buffer = new byte[1200];
			while (true)
			{
				lock (sendQueue)
				{
					while (sendQueue.Count > 0)
					{
						Packet packet = sendQueue.Dequeue();
						packetSendSocket.SendTo(packet.Data, packet.EndPoint);
					}
				}
				if (packetSendSocket.Poll(1000, SelectMode.SelectRead))
				{
					int bytes = packetSendSocket.ReceiveFrom(buffer, ref recvEndpoint);
					byte[] recvBytes = new byte[bytes];
					Array.Copy(buffer, recvBytes, bytes);
					lock (recvQueue)
					{
						recvQueue.Enqueue(new Packet
						{
							Data = recvBytes,
							EndPoint = (IPEndPoint)recvEndpoint
						});
					}
				}
			}
		}

		//Decrypts Packet for further Usage
		private static void DecryptPacket(byte[] data, IPEndPoint sender)
		{
			if (data.Length > 2)
			{
				switch (data[0])
				{
					case 0:
						//Login
						Utils.LoginPair dataLogin = new Utils.LoginPair(data);
						dataLogin.SendData[0] = 0;

						//Replace me with Mysql Check 
						sql = Database.CreateCommand();
						sql.CommandText = "SELECT * FROM Users WHERE username = '" + dataLogin.Username + "' and password = '" + dataLogin.Password + "'";
						MySqlDataReader reader = sql.ExecuteReader();
						byte i = 0;
						while (reader.Read())
						{
							Console.WriteLine(reader["id"]);
							i++;
						}
						reader.Close();
						if (i == 1)
						{
							Console.WriteLine("Login Successful, Finding Server");
							dataLogin.SendData[1] = 1;
							while (ServerAssignment()) { };
							//Server Connected, Fresh Server Or Active Server

						}
						else
						{
							Console.WriteLine("Failed Login");
							dataLogin.SendData[1] = 0;
						}
						sendQueue.Enqueue(new Packet { Data = dataLogin.SendData, EndPoint = sender });
						break;
					case 1:
						//Register
						Utils.LoginPair dataRegister = new Utils.LoginPair(data);
						dataRegister.SendData[0] = 1;
						//Update Database
						sql = Database.CreateCommand();
						sql.CommandText = "INSERT INTO Users (username, password) VALUES ('" + dataRegister.Username + "', '" + dataRegister.Password + "')";
						bool reg = true;
						try
						{
							MySqlDataReader a = sql.ExecuteReader();
							a.Close();
						}
						catch
						{
							Console.WriteLine(sql.CommandText);
							reg = false;
						}

						if (reg)
						{
							Console.WriteLine("Registration Successful");
							dataRegister.SendData[1] = 1;
							//Add the Stats of the Character

						}
						else
						{
							Console.WriteLine("Registration Error");
							dataRegister.SendData[1] = 0;
						}
						sendQueue.Enqueue(new Packet { Data = dataRegister.SendData, EndPoint = sender });
						break;
					case 255:
						//RECIEVED DATA FROM INSTANCE SERVER

						break;
				}

			}
			else
			{
				Console.WriteLine("Bad Packet Recieved");
				Console.WriteLine("/n " + sender);
				//Store sender with Mysql into the Packets Log
			}
			return;
		}

		//Check For Open Servers & ask if slot is available
		private static bool ServerAssignment()
		{
			foreach (Server server in Server.ActiveServers)
			{
				if(server != null && server.Players < Server.MaxPlayers)
				{
					//Server Found
					return false;
				}
				else
				{
					//No Servers Found
					Console.WriteLine("No Servers Found, Creating New Server");
					Server.ActiveServers.Add(new Server()); //I presume someone forgot to add the server to the list ?
					return false;
				}
			}
			return true;
		}
	}
}