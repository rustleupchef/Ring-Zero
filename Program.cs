using System.Drawing;
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
        private static bool isGemini = false;
        private static bool isRunning;
        
        internal static void Main()
        {
            string data = File.ReadAllText("source.json");
            dynamic json = JsonConvert.DeserializeObject(data);
            int source = (int) json["source"];
            
            // grabbing json data
            task = (string) json["task"]; 
            key = (string) json["key"];
            keyword = (string) json["keyword"];
            if (keyword == "")
            {
                keyword = "yes";
            }
            rate = (int) json["rate"];
            isGemini = (bool) json["gemini"];

            // use camera if source isn't -1
            if (source >= 0)
            {
                VideoCapture capture = new(source);
                capture.Start();
                while (true)
                {
                    Mat frame = new();
                    frame = capture.QueryFrame();
                    process(frame);
                    if (!frame.IsEmpty) CvInvoke.Imshow("frame", frame);

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
                    process(frame);

                    if (!frame.IsEmpty) CvInvoke.Imshow("frame", frame);

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

            // dividing image size by greater number if ollama to improve performance
            int denomitator = isGemini ? 1 : 5;
            
            // converting image to base64
            Mat small = new Mat();
            // resizing image for performance
            CvInvoke.Resize(frame, small, new Size(frame.Width/denomitator, frame.Height/denomitator));
            VectorOfByte buffer = new();
            CvInvoke.Imencode(".jpg", small, buffer);
            byte[] jpeg = buffer.ToArray();
            string base64 = Convert.ToBase64String(jpeg);

            // grabbing different url based if gemini or not
            string url = (isGemini) 
                ? $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={key}" 
                : "http://localhost:11434/";

            // making ollama request
            if (!isGemini)
            {
                using HttpClient client = new();
                client.BaseAddress = new Uri(url);
                
                var ollamaPayload = new
                {
                    model = "llava",
                    system = "You are a image captioner that makes extremely quick responses",
                    prompt = task,
                    images = new string[] {base64},
                    options = new
                    {
                        num_predict = 4,
                        temperature = 0
                    },
                    stream = false
                };
                
                StringContent content = new(JsonConvert.SerializeObject(ollamaPayload), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("/api/generate", content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine(response.StatusCode);
                    return;
                }
                
                string text = await response.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject(text);
                text = (string) json["response"];
                Console.WriteLine(text);
                if (text.ToLower().Contains(keyword.ToLower())) Console.Beep();
                
                isRunning = false;
                return;
            }
            
            // making gemini request
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
                },
                generationConfig = new
                {
                    temperature = 0,
                    maxOutputTokens = 10
                },
                systemInstruction = new
                {
                    role = "system",
                    parts = new object[]
                    {
                        new
                        {
                            text = "You are the best captioner in the world. You get straight to the point"
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
                Console.WriteLine(text);
                if (text.ToLower().Contains(keyword.ToLower())) Console.Beep();
            }
            Thread.Sleep(rate);
            isRunning = false;
        }
    }
}