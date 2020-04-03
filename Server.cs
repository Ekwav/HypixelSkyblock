using System;
using System.IO;
using System.Text;
using System.Threading;
using Coflnet;
using RestSharp;
using WebSocketSharp.Server;
using WebSocketSharp;
using RestSharp.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace hypixel
{
    public class Server 
    {
        public Server()
        {
        }

        /// <summary>
        /// Starts the backend server
        /// </summary>
        public void Start(short port = 8008,string urlPath = "/skyblock")
        {
            var server = new HttpServer(port);
            server.AddWebSocketService<SkyblockBackEnd> (urlPath);
            server.OnGet += (sender, e) => {
                var req = e.Request;
                var res = e.Response;

                var path = req.RawUrl.Split('?')[0];

                if(path == "/" || path.IsNullOrEmpty())
                {
                    path = "index.html";
                }

                byte[] contents;
                var relativePath = $"files/{path}";

                if(path.StartsWith("/static/skin"))
                {
                    if(!FileController.Exists(relativePath))
                    {
                        // try to get it from mojang
                        var client = new RestClient("https://textures.minecraft.net/");
                        var request = new RestRequest("/texture/{id}");
                        request.AddUrlSegment("id",Path.GetFileName(relativePath));
                        Console.WriteLine(Path.GetFileName(relativePath));
                        var fullPath = FileController.GetAbsolutePath(relativePath);
                        FileController.CreatePath(fullPath);
                        var inStream = new MemoryStream(client.DownloadData(request));
                        
                        client.DownloadData(request).SaveAs(fullPath+ "f.png" );

                        // parse it to only show face
                       // using (var inStream = new FileStream(File.Open("fullPath",FileMode.Rea)))
                        using (var outputImage = new Image<Rgba32>(16, 16))
                        {
                            var baseImage = SixLabors.ImageSharp.Image.Load(inStream);
                            
                            var lowerImage = baseImage.Clone(
                                            i => i.Resize(256, 256)
                                                .Crop(new Rectangle(32, 32, 32, 32)));
    
                            lowerImage.Save(fullPath+ ".png");        
                             
                        }
                        FileController.Move(relativePath + ".png",relativePath);
                    }

                }


                if (!FileController.Exists (relativePath)) {
                    res.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                    return;
                }

                contents = FileController.ReadAllBytes(relativePath);

                if (path.EndsWith (".html")) {
                    res.ContentType = "text/html";
                    res.ContentEncoding = Encoding.UTF8;
                }
                else if (path.EndsWith (".png") || path.StartsWith("/static/skin")) {
                    res.ContentType = "image/png";
                    res.ContentEncoding = Encoding.UTF8;
                }
                else if (path.EndsWith (".css")) {
                    res.ContentType = "text/css";
                    res.ContentEncoding = Encoding.UTF8;
                }
                else if (path.EndsWith (".js")) {
                    res.ContentType = "text/javascript";
                    res.ContentEncoding = Encoding.UTF8;
                }

                res.WriteContent (contents);
            };



            server.Start ();
            //Console.ReadKey (true);
            Thread.Sleep(Timeout.Infinite);
            server.Stop ();
        }
    }
}