using System.Net;
using System.Net.Sockets;
using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Newtonsoft.Json;
using System.Text.Json;

namespace Ring_Zero
{
    public class Program
    {
        private static string task;
        private static string key;
        private static string keyword;
        private static int rate;
        private static bool isRunning;
        
        internal static void Main()
        {
            string data = File.ReadAllText("source.json");
            dynamic json = JsonConvert.DeserializeObject(data);
            int source = (int) json["source"];
            
            task = (string) json["task"]; 
            key = (string) json["key"];
            keyword = (string) json["keyword"];
            if (keyword == "")
            {
                keyword = "yes";
            }
            rate = (int) json["rate"];

            // use camera if source isn't -1
            if (source >= 0)
            {
                VideoCapture capture = new(source);
                capture.Start();
                while (true)
                {
                    Mat frame = new Mat();
                    frame = capture.QueryFrame();
                    process(frame);
                    
                    CvInvoke.Imshow("frame", frame);

                    if (CvInvoke.WaitKey(1) == 27)
                    {
                        CvInvoke.DestroyAllWindows();
                        capture.Stop();
                        capture.Dispose();
                        return;
                    }
                }
            }
            
            using (TcpListener socket = new(IPAddress.Any, 8080))
            {
                socket.Start();

                while (true)
                {
                    TcpClient client = socket.AcceptTcpClient();
                    NetworkStream stream = client.GetStream();
                    BinaryReader br = new(stream);
                    
                    // grabbing length of buffer
                    byte[] lengthBuffer = new byte[4];
                    br.Read(lengthBuffer, 0, 4);
                    if (BitConverter.IsLittleEndian) lengthBuffer = lengthBuffer.Reverse().ToArray();
                    int length = BitConverter.ToInt32(lengthBuffer, 0);
                    
                    // reading buffer fully
                    byte[] buffer = new byte[length];
                    for (int bytesRead = 0;
                         bytesRead < length; 
                         bytesRead += stream.Read(buffer, bytesRead, length - bytesRead));
                    
                    // converting buffer to frame
                    Mat frame = new();
                    CvInvoke.Imdecode(buffer, ImreadModes.Color, frame);
                    CvInvoke.Imshow("frame", frame);

                    // end code
                    if (CvInvoke.WaitKey(1) == 27)
                    {
                        CvInvoke.DestroyAllWindows();
                        stream.Close();
                        client.Close();
                        socket.Stop();
                        break;
                    }
                    
                    stream.Close();
                    client.Close();
                }
            }
        }

        private static async Task process(Mat frame)
        {
            if (isRunning) return;
            
            isRunning = true;
            VectorOfByte buffer = new();
            CvInvoke.Imencode(".jpg", frame, buffer);
            byte[] jpeg = buffer.ToArray();
            string base64 = Convert.ToBase64String(jpeg);

            string url =
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={key}";
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                text = task
                            },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = "image/jpeg",
                                    data = base64
                                }
                            }
                        }
                    }
                }
            };
            
            string data = JsonConvert.SerializeObject(payload);

            using (HttpClient client = new())
            {
                StringContent content = new(data, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, content);
                    
                string result = await response.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject(result);
                string text = (string)json["candidates"][0]["content"]["parts"][0]["text"];
                if (text.ToLower().Contains(keyword.ToLower())) Console.Beep();
            }
            Thread.Sleep(rate);
            isRunning = false;
        }
    }
}