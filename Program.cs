using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable CA1416
#pragma warning restore CA1416


namespace epikthumb09
{

    class Program
    {
        static void Banner()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.WriteLine("=========================================");
            Console.WriteLine("         Epik 09 thumbgen server         ");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("             made by leno :P             ");
            Console.WriteLine("=========================================");

            Console.ResetColor();
        }

        static Process process;

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        const uint WM_COMMAND = 0x0111;

        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        const int SW_RESTORE = 9;
        const int SW_MAXIMIZE = 3;

        static string robloxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RobloxApp.exe");

        const int SWP_NOSIZE = 0x0001;
        const int SWP_NOMOVE = 0x0002;
        const int TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y); // not for the render pos thing btw

        static Bitmap capture(bool autocrop)
        {
            RECT rect = new RECT();
            int width = 0, height = 0;

            for (int i = 0; i < 50; i++)
            {
                GetWindowRect(process.MainWindowHandle, out rect);
                width = rect.Right - rect.Left;
                height = rect.Bottom - rect.Top;

                if (width > 0 && height > 0)
                    break;

                Thread.Sleep(100);
            }

            if (width <= 0 || height <= 0)
            {
                Console.WriteLine("[X] ????");
                rect.Left = 0;
                rect.Top = 0;
                rect.Right = Screen.PrimaryScreen.Bounds.Width;
                rect.Bottom = Screen.PrimaryScreen.Bounds.Height;
                width = rect.Right - rect.Left;
                height = rect.Bottom - rect.Top;
            }

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            }

            if (autocrop)
            {
                const int targetR = 0;
                const int targetG = 255;
                const int targetB = 1;
                const int tolerance = 40;

                int minX = bmp.Width;
                int minY = bmp.Height;
                int maxX = 0;
                int maxY = 0;

                for (int y = 0; y < bmp.Height; y++)
                {
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        Color pixel = bmp.GetPixel(x, y);
                        int diffR = Math.Abs(pixel.R - targetR);
                        int diffG = Math.Abs(pixel.G - targetG);
                        int diffB = Math.Abs(pixel.B - targetB);

                        if (diffR < tolerance && diffG < tolerance && diffB < tolerance)
                        {
                            if (x < minX) minX = x;
                            if (y < minY) minY = y;
                            if (x > maxX) maxX = x;
                            if (y > maxY) maxY = y;
                        }
                    }
                }

                if (maxX >= minX && maxY >= minY)
                {
                    int cropWidth = maxX - minX + 1;
                    int cropHeight = maxY - minY + 1;

                    if (cropWidth > 0 && cropHeight > 0)
                    {
                        Bitmap cropped = new Bitmap(cropWidth, cropHeight);
                        using (Graphics g = Graphics.FromImage(cropped))
                        {
                            g.DrawImage(bmp, 0, 0, new Rectangle(minX, minY, cropWidth, cropHeight), GraphicsUnit.Pixel);
                        }

                        bmp.Dispose();
                        bmp = cropped;
                    }
                }
            }

            return bmp;
        }

        static Bitmap crop(Bitmap source, int cropLeft, int cropTop, int cropRight, int cropBottom)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            cropLeft = Math.Max(0, cropLeft);
            cropTop = Math.Max(0, cropTop);
            cropRight = Math.Max(0, cropRight);
            cropBottom = Math.Max(0, cropBottom);

            int newWidth = Math.Max(1, source.Width - cropLeft - cropRight);
            int newHeight = Math.Max(1, source.Height - cropTop - cropBottom);

            if (cropLeft + newWidth > source.Width)
                newWidth = source.Width - cropLeft;
            if (cropTop + newHeight > source.Height)
                newHeight = source.Height - cropTop;

            Rectangle cropArea = new Rectangle(cropLeft, cropTop, newWidth, newHeight);
            Bitmap cropped = source.Clone(cropArea, source.PixelFormat);

            return cropped;
        }



        static Bitmap removegreenscreen(Bitmap render)
        {
            for (int y = 0; y < render.Height; y++)
            {
                for (int x = 0; x < render.Width; x++)
                {
                    Color pixel = render.GetPixel(x, y);

                    if (pixel.G > 200 && pixel.R < 100 && pixel.B < 100)
                    {
                        render.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0)); 
                    }
                }
            }
            return render;
        }

        static async Task<Bitmap> render(string thang)
        {

            process = new Process();
                process.StartInfo.FileName = robloxPath;
                process.StartInfo.WorkingDirectory = Path.GetDirectoryName(robloxPath);
                process.StartInfo.UseShellExecute = false; // fuckm
                process.StartInfo.Arguments = "-script \"wait(); dofile('rbxasset://script.lua')\"";
                process.Start();
                Console.WriteLine($"[>] Started roblocko doing stuff");
                while (process.MainWindowHandle == IntPtr.Zero)
                {
                    await Task.Delay(100);
                    process.Refresh();
                }
            SetCursorPos(0, 0);
            SetForegroundWindow(process.MainWindowHandle);
            ShowWindow(process.MainWindowHandle, SW_MAXIMIZE);
            SetWindowPos(process.MainWindowHandle, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);

            Console.WriteLine("[>] thinking");
            for (int i = 0; i < 7; i++) 
                SendMessage(process.MainWindowHandle, WM_COMMAND, (IntPtr)32983, IntPtr.Zero);


            await Task.Delay(1000);

            Bitmap render = null;




            switch (thang) // yes ik this is clean
            {
                case "character":
                    render = capture(true);
                    render = crop(render, 580, 0, 500, 50);
                    render = removegreenscreen(render);
                    break;

                case "model":
                    render = capture(true);
                    render = crop(render, 580, 0, 500, 50);
                    render = removegreenscreen(render);
                    break;

                case "place":
                    render = capture(false);
                    render = crop(render, 36, 105, 1925, 787);
                    break;

                default:
                    break;
            }

            process.Kill();
            Console.WriteLine("[>] Rendering");


            return render;
        }

        static async Task Main(string[] args)
        {
            Console.Title = "epik09thumb";

            Banner();

            if (!HttpListener.IsSupported)
            {
                Console.WriteLine("[X] HttpListener not supported");
                return;
            }

            HttpListener listener = new HttpListener();

            listener.Prefixes.Add("http://localhost:8000/");
            listener.Prefixes.Add("http://127.0.0.1:8000/");
            listener.Prefixes.Add("http://+:8000/");

            listener.Start();

            Console.WriteLine("[>] Server started!");

            while (true)
            {
                var context = listener.GetContext();
                var request = context.Request;
                var response = context.Response;

                if (request.HttpMethod == "POST") 
                {
                    using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        var data = JsonSerializer.Deserialize<JsonElement>(json);
                        string type = data.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "character";
                        string script = data.TryGetProperty("script", out var scriptProp) ? scriptProp.GetString() : "print'wut'";
                        string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "content", "script.lua");

                        File.Delete(scriptPath);

                        if(type != "place")
                        {
                            script = "game:GetObjects('rbxasset://greensky.rbxm')[1].Parent=game.Lighting " + script;
                        }

                        File.AppendAllText(scriptPath, script);

                        Bitmap thumb = await render(type);
                        Console.WriteLine("[>] Thumbnail Done");
                        using (Bitmap bmp = new Bitmap(thumb))
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

                                byte[] imgbytes = ms.ToArray();
                                string base64 = Convert.ToBase64String(imgbytes); // base64 is fine right
                                //Console.WriteLine(base64String);
                                WriteResponse(response, base64);
                            }
                        }
                    }
                }
                else
                {
                    WriteResponse(response, "thinking");
                }
            }
        }

        static void WriteResponse(HttpListenerResponse response, string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);

            response.StatusCode = 200;
            response.ContentType = "text/plain";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;

            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}
