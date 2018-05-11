using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace ConsoleApp1
{
    class Program
    {
        public static byte login = 0;
        public static bool Listen = true;
        public static bool GlobalWait = true;
        public static ushort Gameport = 26565;
        //Inter-Server Communications Array
        //public static Array isc;
        public static IPAddress localhost = IPAddress.Parse("127.0.0.1");
        public struct Packet
        {
            public IPEndPoint EndPoint;
            public byte[] Data;
        }

        private static MySqlConnection DB = new MySqlConnection("Server=localhost;Port=3456;Database=GammaServer;uid=ServerAdmin;pwd=iV1zKRay8gVMcsl2");
        private static MySqlCommand sql;
        private static MySqlConnection connection = new MySqlConnection();
        static Thread t;
        static Socket s;
        public static Queue<Packet> sendQueue = new Queue<Packet>();
        static Queue<Packet> recvQueue = new Queue<Packet>();
        static EndPoint recvEndpoint = (EndPoint)new IPEndPoint(IPAddress.Any, 0);

        //Initialize Server, Listen
        static void Main(string[] args)
        {
            Console.WriteLine("Initializing Server");
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, Gameport);
            s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Bind(endpoint);
            t = new Thread(ThreadLoop);
            t.IsBackground = true;
            t.Start();
            try
            {
                DB.Open();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
            }
            Console.WriteLine("Server Ready, Awaiting Packets");
            while (Listen)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, Gameport);
                EndPoint tmpRemote = sender;
                byte[] data;

                if (TryRecvData(out sender, out data))
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
        static bool TryRecvData(out IPEndPoint from, out byte[] data)
        {
            lock (recvQueue)
            {
                if (recvQueue.Count > 0)
                {
                    var packet = recvQueue.Dequeue();
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
        static void ThreadLoop()
        {
            var buffer = new byte[1200];
            while (true)
            {
                lock (sendQueue)
                {
                    while (sendQueue.Count > 0)
                    {
                        var packet = sendQueue.Dequeue();
                        s.SendTo(packet.Data, packet.EndPoint);
                    }
                }
                if (s.Poll(1000, SelectMode.SelectRead))
                {
                    int bytes = s.ReceiveFrom(buffer, ref recvEndpoint);
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
        static void DecryptPacket(byte[] data, IPEndPoint sender)
        {
            if (data.Length > 2)
            {
                switch (data[0])
                {
                    case 0:
                        //Login
                        byte usernamelength = data[1];
                        string username = System.Text.ASCIIEncoding.ASCII.GetString(data, 2, usernamelength);
                        string password = System.Text.ASCIIEncoding.ASCII.GetString(data, 2 + usernamelength, data.Length - usernamelength - 2);
                        byte[] sendata = new byte[20];
                        sendata[0] = 0;
                        //Replace me with Mysql Check 
                        sql = DB.CreateCommand();
                        sql.CommandText = "SELECT * FROM Users WHERE username = '" + username + "' and password = '" + password + "'";
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
                            sendata[1] = 1;
                            while (ServerAssignment()) { };
                            //Server Connected, Fresh Server Or Active Server

                        }
                        else
                        {
                            Console.WriteLine("Failed Login");
                            sendata[1] = 0;
                        }
                        sendQueue.Enqueue(new Packet { Data = sendata, EndPoint = sender });
                        break;
                    case 1:
                        //Register
                        byte rusernamelength = data[1];
                        string rusername = System.Text.ASCIIEncoding.ASCII.GetString(data, 2, rusernamelength);
                        string rpassword = System.Text.ASCIIEncoding.ASCII.GetString(data, 2 + rusernamelength, data.Length - rusernamelength - 2);
                        byte[] rsendata = new byte[20];
                        rsendata[0] = 1;
                        //Update Database
                        sql = DB.CreateCommand();
                        sql.CommandText = "INSERT INTO Users (username, password) VALUES ('" + rusername + "', '" + rpassword + "')";
                        bool Reg = true;
                        try
                        {
                            MySqlDataReader a = sql.ExecuteReader();
                            a.Close();
                        }
                        catch
                        {
                            Console.WriteLine(sql.CommandText);
                            Reg = false;
                        }

                        if (Reg)
                        {
                            Console.WriteLine("Registration Successful");
                            rsendata[1] = 1;
                            //Add the Stats of the Character

                        }
                        else
                        {
                            Console.WriteLine("Registration Error");
                            rsendata[1] = 0;
                        }
                        sendQueue.Enqueue(new Packet { Data = rsendata, EndPoint = sender });
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
        static bool ServerAssignment()
        {
            int i = 0;
            while (i<Server.ActiveServers.Length)
            {
                if(Server.ActiveServers[i] != null && Server.ActiveServers[i].Players < Server.MaxPlayers)
                {
                    //Server Found
                    return false;
                }
                else
                {
                    //No Servers Found
                    Console.WriteLine("No Servers Found, Creating New Server");
                    new Server();
                    return false;
                }
            }
            return true;
        }
    }
}