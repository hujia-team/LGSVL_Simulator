/** 
 *
 * caidu@minieye.cc 20200925
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using SimpleJSON;
using UnityEngine;

namespace Simulator.Bridge
{
    public class LoggingBridgeAdasInstance : IBridgeInstance
    {
        //Stream File;
        Stream FileImage;
        StringBuilder SbImuData = new StringBuilder();
        int FileIndex = 0;
        int FileCount = 1;
        DateTime dt;
        string DateStart;

        const int DefaultPort = 9090;
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1.0);
        WebSocket Socket;

        List<string> Setup = new List<string>();

        public Status Status { get; private set; } = Status.Disconnected;

        public LoggingBridgeAdasInstance()
        {
            Status = Status.Disconnected;

            dt = DateTime.Now;
            DateStart = string.Format("{0:yyyyMMddHHmm}", dt);
            Directory.CreateDirectory(@"F:\LGSVL_Parse\Data\Image" + DateStart);
        }

        public void Connect(string connection)
        {
            if (FileImage != null)
            {
                Disconnect();
            }

            try
            {
                string FileName = DateStart + "_" + FileCount.ToString();
                var path = Path.Combine(@"F:\LGSVL_Parse\Data\Image" + DateStart + "\\" + FileName + ".txt.gz");
                FileImage = new GZipStream(new FileStream(path, FileMode.Create), CompressionMode.Compress, false);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return;
            }

            var split = connection.Split(new[] { ':' }, 2);

            var address = split[0];
            var port = split.Length == 1 ? DefaultPort : int.Parse(split[1]);

            try
            {
                Socket = new WebSocket($"ws://{address}:{port}");
                Socket.WaitTime = Timeout;

                // 绑定WebSocket事件
                Socket.OnMessage += OnMessage;
                Socket.OnOpen += OnOpen;
                Socket.OnError += OnError;
                Socket.OnClose += OnClose;

                // 林克死大头
                Status = Status.Connecting;
                Socket.ConnectAsync();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public void Disconnect()
        {
            if (FileImage != null)
            {
                lock (this)
                {
                    FileImage.Close();
                    FileImage = null;
                }
            }
            if (Socket != null)
            {
                if (Socket.ReadyState == WebSocketState.Open)
                {
                    Socket.SendAsync("kgsb", null);
                    Status = Status.Disconnecting;
                    Socket.CloseAsync();
                }
            }
        }

        // 内存泄漏（ManagedHeap.UsedSize过大），原因在CameraSensorBase.cs
        public void Write(string type, string topic, string data, Action completed)
        {

            if (type.Contains("CanBusData"))
            {
                if (Socket != null)
                {
                    Socket.SendAsync(data + "\n", null);
                }
            }
            else if (type.Contains("CorrectedImuData"))
            {
                SbImuData.Append(data);
                SbImuData.Append("\n");
                if (SbImuData.Length > 30)
                {
                    Socket.SendAsync(SbImuData.ToString(), null);
                    SbImuData.Clear();
                }
            }
            else if (type.Contains("ImageData"))
            {
                //return;
                if (FileImage != null)
                {
                    Task.Run(() =>
                    {
                        lock (this)
                        {
                            //JSONNode jNode = JSON.Parse(data);
                            //jNode["Bytes"].Value = "test";

                            //var bytes = Encoding.UTF8.GetBytes($"{type} {topic}\n");
                            //FileImage.Write(bytes, 0, bytes.Length);

                            //var bytes = Encoding.UTF8.GetBytes(jNode.ToString());
                            //jNode = null;
                            var bytes = Encoding.UTF8.GetBytes(data);
                            FileImage.Write(bytes, 0, bytes.Length);
                            FileImage.WriteByte((byte)'\n');

                            bytes = null;
                            FileIndex++;

                            ////分几个文件保存。没用。
                            //if (FileIndex > 10)
                            //    if (true)
                            //    {
                            //        FileImage.Close();
                            //        FileIndex = 0;
                            //        FileCount++;
                            //        string FileName = DateStart + "_" + FileCount.ToString();
                            //        var path = Path.Combine(@"F:\LGSVL_Parse\Data\Image" + DateStart + "\\" + FileName + ".txt.gz");
                            //        FileImage = new GZipStream(new FileStream(path, FileMode.Create), CompressionMode.Compress, false);
                            //    }
                        }
                        completed();
                    });
                }
            }
        }

        void OnClose(object sender, CloseEventArgs args)
        {
            //Socket.SendAsync("End", null);
            Status = Status.Disconnected;
            Socket = null;
        }

        void OnError(object sender, WebSocketSharp.ErrorEventArgs args)
        {
            Debug.LogError(args.Message);

            if (args.Exception != null)
            {
                Debug.LogException(args.Exception);
            }
        }

        void OnOpen(object sender, EventArgs args)
        {
            lock (Setup)
            {
                Setup.ForEach(s => Socket.SendAsync(s, null));
                Status = Status.Connected;
            }

            Debug.Log("WebSocket Connected!");
        }

        void OnMessage(object sender, MessageEventArgs args)
        {
            // TODO
        }
    }
}
