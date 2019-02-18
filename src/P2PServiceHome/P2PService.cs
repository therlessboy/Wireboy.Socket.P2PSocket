﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace P2PServiceHome
{
    public class P2PService
    {
        string service_IpAddress = "39.105.115.162";
        int service_Port = 3388;
        string ServerName = "MyPC";
        List<Task> taskList = new List<Task>();

        TcpClient outClient = null;
        TcpClient inClient = null;
        DateTime lastReceiveTime = DateTime.Now;

        TcpClient heartClient = null;
        public P2PService()
        {
        }
        public void Start()
        {
            lastReceiveTime = DateTime.Now;
            do
            {
                Console.WriteLine("请输入当前服务名称");
                ServerName = Console.ReadLine();
            } while (string.IsNullOrEmpty(ServerName));
            Console.WriteLine(string.Format("当前服务名称：{0}", ServerName));
            SendHeart();
        }



        public void SendHeart()
        {
            do
            {
                try
                {
                    if (heartClient == null)
                    {
                        heartClient = new TcpClient(service_IpAddress, service_Port);
                    }
                    else
                    {
                        //发送心跳包
                        heartClient.Client.Send(new byte[] { 0 });
                    }
                }catch(Exception ex)
                {
                    heartClient = null;
                    outClient = null;
                    Console.WriteLine("服务器连接失败，稍后将重连... {0}", ex);
                }
                try
                {
                    if (outClient == null || !outClient.Connected)
                    {
                        Console.WriteLine("正在连接服务器... {0}:{1}", service_IpAddress, service_Port);
                        outClient = new TcpClient(service_IpAddress, service_Port);
                        Console.WriteLine("服务器成功连接");

                        NetworkStream ssOut = outClient.GetStream();
                        List<byte> sMsg = Encoding.ASCII.GetBytes(ServerName).ToList();
                        sMsg.Insert(0, 55);
                        ssOut.Write(sMsg.ToArray(), 0, sMsg.ToArray().Length);
                        new TaskFactory().StartNew(() => { clientReceive(outClient); });
                    }
                }
                catch (Exception ex)
                {
                    outClient = null;
                    Console.WriteLine("服务器连接失败，稍后将重连... {0}", ex);
                }
                try
                {
                    if (inClient == null || !inClient.Connected)
                    {
                        Console.WriteLine("正在连接本地远程桌面服务... 127.0.0.1:3389");
                        inClient = new TcpClient();
                        inClient.Connect(IPAddress.Parse("127.0.0.1"), 3389);
                        Console.WriteLine("本地远程桌面服务连接成功");
                        new TaskFactory().StartNew(() => { clientReceive(inClient); });
                    }
                }
                catch (Exception ex)
                {
                    inClient = null;
                    Console.WriteLine("本地远程桌面服务连接失败，稍后将重连... {0}", ex.Message);
                }
                Thread.Sleep(2000);
            } while (true);

        }
        Task checkDeskConnectedTask = null;
        private void clientReceive(TcpClient client)
        {
            bool isLocalClient = client == inClient;

            while (true && client != null)
            {
                byte[] recBytes = new byte[10240];
                int count = 0;
                try
                {
                    count = client.Client.Receive(recBytes);
                }
                catch (Exception ex)
                {
                    if(isLocalClient)
                    {
                        TcpClient tempClient = inClient;
                        inClient = null;
                        tempClient.Close();
                    }
                    else
                    {
                        TcpClient tempClient = outClient;
                        outClient = null;
                        tempClient.Close();
                    }

                    Console.WriteLine("接收数据异常：{0}", ex);
                    break;
                }
                if (count > 0)
                {
                    if (!isLocalClient)
                    {
                        if (checkDeskConnectedTask == null)
                            checkDeskConnectedTask = new TaskFactory().StartNew(() => { CheckDeskConnected(); });
                    }
                    else
                    {
                        //远程桌面发送了数据
                        lastReceiveTime = DateTime.Now;
                    }
                    //Console.WriteLine("从{0}接收到数据,长度：{1}", client.Client.RemoteEndPoint,count);
                    TcpClient toClient = isLocalClient ? outClient : inClient;
                    if (toClient != null)
                    {
                        //转发数据
                        try
                        {
                            NetworkStream ss = toClient.GetStream();// Client.Send(recBytes);
                            ss.WriteAsync(recBytes, 0, count);
                        }
                        catch (Exception ex)
                        {
                            if (isLocalClient)
                            {
                                Console.WriteLine("向服务器发送数据失败！{0}",ex);
                                TcpClient tempClient = outClient;
                                outClient = null;
                                tempClient.Close();
                            }
                            else
                            {
                                Console.WriteLine("向本地远程桌面服务发送数据失败！{0}", ex);
                                TcpClient tempClient = inClient;
                                inClient = null;
                                tempClient.Close();
                            }
                        }
                    }
                }

                client = isLocalClient ? inClient : outClient;
            }
        }

        private void CheckDeskConnected()
        {
            Console.WriteLine("启动本地远程桌面服务通讯守护线程！");
            lastReceiveTime = DateTime.Now;
            while (true && inClient != null)
            {
                if ((DateTime.Now - lastReceiveTime).Milliseconds > 3000)
                {
                    Console.WriteLine("本地远程桌面服务连接超时！");
                    lastReceiveTime = DateTime.Now;
                    TcpClient tempClient = inClient;
                    inClient = null;
                    tempClient.Close();
                    break;
                }
                Thread.Sleep(1000);
            }
            Console.WriteLine("本地远程桌面服务守护线程退出！");
            checkDeskConnectedTask = null;
        }
    }
}
