import java.io.DataOutputStream;
import java.io.FileReader;
import java.net.Socket;

import org.json.simple.JSONObject;
import org.json.simple.parser.JSONParser;
import org.opencv.core.Core;
import org.opencv.core.Mat;
import org.opencv.core.MatOfByte;
import org.opencv.imgcodecs.Imgcodecs;
import org.opencv.videoio.VideoCapture;

public class App {
    static {
        System.loadLibrary(Core.NATIVE_LIBRARY_NAME);
    }
    public static void main(String[] args) throws Exception {
        JSONObject config = (JSONObject) new JSONParser().parse(new FileReader("config.json"));
        String ip = (String) config.get("ip");
        VideoCapture video = new VideoCapture(0);
        while (true) {
            Mat frame = new Mat();
            video.read(frame);

            // convert frame to bytes
            MatOfByte mob = new MatOfByte();
            Imgcodecs.imencode(".jpg", frame, mob);
            byte[] frameBytes = mob.toArray();

            // send frame to computer
            try (Socket socket = new Socket(ip, 8080)) {
                DataOutputStream dos = new DataOutputStream(socket.getOutputStream());

                dos.writeInt(frameBytes.length);
                dos.write(frameBytes);
                dos.flush();

                dos.close();
                socket.close();
            } catch (Exception e) {
                // giving code a break between failed attmepts
                e.printStackTrace();
                Thread.sleep(5000);
            }
        }
    }
}
