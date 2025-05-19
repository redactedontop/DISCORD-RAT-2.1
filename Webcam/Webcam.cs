using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace Webcam
{
    public class Webcam
    {
        public static Dictionary<string, bool> ready = new Dictionary<string, bool>();
        public static Dictionary<string, Bitmap> holder = new Dictionary<string, Bitmap>();
        public static Dictionary<int, string> cameras = new Dictionary<int, string>();
        public static int selected = 1;

        public static async Task<string> GetWebcamsAsync()
        {
            StringBuilder deviceInfo = new StringBuilder();
            // Enumerate video capture devices
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            cameras.Clear();
            int index = 1;
            foreach (var device in devices)
            {
                deviceInfo.AppendLine($"{index}: {device.Name}");
                cameras[index] = device.Id;
                index++;
            }
            return deviceInfo.ToString();
        }

        public static async Task<byte[]> GetImageAsync()
        {
            if (!cameras.ContainsKey(selected))
            {
                throw new InvalidOperationException("Selected camera does not exist.");
            }

            var settings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = cameras[selected],
                StreamingCaptureMode = StreamingCaptureMode.Video
            };

            var capture = new MediaCapture();
            await capture.InitializeAsync(settings);

            var imageProperties = ImageEncodingProperties.CreateJpeg();
            using (var stream = new InMemoryRandomAccessStream())
            {
                await capture.CapturePhotoToStreamAsync(imageProperties, stream);
                byte[] buffer = new byte[stream.Size];
                stream.Seek(0);
                await stream.AsStreamForRead().ReadAsync(buffer, 0, buffer.Length);
                return buffer;
            }
        }

        public static bool Select(int num)
        {
            if (!cameras.ContainsKey(num))
                return false;
            selected = num;
            return true;
        }

        public static async Task InitAsync()
        {
            await GetWebcamsAsync();
        }
    }
}
