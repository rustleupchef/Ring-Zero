using System.Net;
using System.Net.Sockets;
using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Newtonsoft.Json;

namespace Ring_Zero
{
    public class Program
    {
        private static string task;
        private static string key;
        private static bool isRunning = false;
        private static readonly HttpClient client = new HttpClient();
        
        internal static void Main(string[] args)
        {
            string data = File.ReadAllText("source.json");
            dynamic json = JsonConvert.DeserializeObject(data);
            int source = (int) json["source"];
            
            task = (string) json["task"]; 
            key = (string) json["key"];

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

        private static void process(Mat frame)
        {
            VectorOfByte buffer = new();
            CvInvoke.Imencode(".jpg", frame, buffer);
            byte[] jpeg = buffer.ToArray();
        }
        
    }
}