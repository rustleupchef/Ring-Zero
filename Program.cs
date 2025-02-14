using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV.Structure;
using Newtonsoft.Json;

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
        private static Mat previous = new Mat();
        
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
                    
                    // only process if frames are severly different
                    double similarity = compare(previous, frame);
                    if (similarity > 1.2)
                        process(frame);
                    
                    // display frame
                    if (frame != null) 
                        CvInvoke.Imshow("frame", frame);
                    
                    // change previous frame
                    previous = frame;

                    // exit code
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
                    
                    // only process if frames are severly different
                    double similarity = compare(previous, frame);
                    if (similarity > 1.2)
                        process(frame);

                    if (!frame.IsEmpty) 
                        CvInvoke.Imshow("frame", frame);

                    // exit code
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

        // determing if image has desired task
        private static async Task process(Mat frame)
        {
            if (isRunning) return;
            
            isRunning = true;

            // dividing image size by greater number if ollama to improve performance
            int denomitator = isGemini ? 1 : 5;
            
            // resizing image for performance
            Mat small = new Mat();
            CvInvoke.Resize(frame, small, new Size(frame.Width/denomitator, frame.Height/denomitator));
            VectorOfByte buffer = new();
            
            // converting image to base64
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
                    previous = frame;
                    return;
                }
                
                string text = await response.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject(text);
                text = (string) json["response"];
                Console.WriteLine(text);
                if (text.ToLower().Contains(keyword.ToLower())) Console.Beep();
                
                isRunning = false;
                previous = frame;
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
            previous = frame;
        }

        private static double compare(Mat previous, Mat current)
        {
            if (previous == null || current == null || previous.IsEmpty || current.IsEmpty)
            {
                return 0.0;
            }

            Mat grayPrevious = new();
            Mat grayCurrent = new();
            
            CvInvoke.CvtColor(previous, grayPrevious, ColorConversion.Bgr2Gray);
            CvInvoke.CvtColor(current, grayCurrent, ColorConversion.Bgr2Gray);

            MCvScalar mssim = new();
            Mat difference = new();
            
            CvInvoke.AbsDiff(grayPrevious, grayCurrent, difference);
            mssim = CvInvoke.Mean(difference);
            return (mssim.V0 + mssim.V1 + mssim.V2 + mssim.V3) / 4.0;
        }
    }
}