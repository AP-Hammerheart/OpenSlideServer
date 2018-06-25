// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace OpenSlideServer
{
    public class Settings
    {
        public string location;
        public string cache;
        public string[] prefixes;
    }

    public class ImageServer
    {
        public ImageServer()
        {          
            var listener = new HttpListener();
            var settings = LoadSettings();

            foreach (var prefix in settings.prefixes)
            {
                listener.Prefixes.Add(prefix);
            }

            listener.Start();

            Console.WriteLine("Server running ...");

            while (true)
            {
                try
                {
                    var context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(o => HandleRequest(context, settings.location, settings.cache));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public Settings LoadSettings()
        {
            using (StreamReader sr = new StreamReader("config.json"))
            {
                var json = sr.ReadToEnd();
                return JsonConvert.DeserializeObject<Settings>(json);
            }
        }

        private void HandleRequest(object state, string location, string cache)
        { 
            var context = (HttpListenerContext)state;
            try
            {
                var command = context.Request.QueryString.Get("command");
                if (command == "image")
                {
                    HandleImageRequest(context, location, cache);
                }
                else if (command == "list")
                {
                    HandleListRequest(context, location);
                }
                else if (command == "cases")
                {
                    HandleCasesRequest(context, location);
                }
                else if (command == "details")
                {
                    HandleDetailsRequest(context, location);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }

        private void HandleCasesRequest(HttpListenerContext context, string location)
        {
            try
            {
                Console.WriteLine(context.Request.RawUrl);

                var dirs = Directory.GetDirectories(location);
                var json = "{\r\n\t\"Cases\":[\r\n";

                for (var i = 0; i < dirs.Length; i++)
                {
                    json += "\t\t\"" + Path.GetFileName(dirs[i]) + "\"" + ((i < dirs.Length - 1) ? ", \r\n" : "");
                }
                json += "\r\n\t]\r\n}";

                var bytes = Encoding.ASCII.GetBytes(json);

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";

                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Close();
            }
        }

        private void HandleListRequest(HttpListenerContext context, string location)
        {
            try
            {
                Console.WriteLine(context.Request.RawUrl);

                var caseID = context.Request.QueryString.Get("caseID");
                var files = Directory.GetFiles(location + "\\" + caseID + "\\");
                var json = "{\r\n\t\"Images\":[\r\n";

                for (var i = 0; i < files.Length; i++)
                {
                    json += "\t\t\"" + Path.GetFileName(files[i]) + "\"" + ((i < files.Length - 1) ? ", \r\n" : "");
                }
                json += "\r\n\t]\r\n}";

                var bytes = Encoding.ASCII.GetBytes(json);

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";

                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Close();
            }
        }

        private void HandleDetailsRequest(HttpListenerContext context, string location)
        {
            try
            {
                Console.WriteLine(context.Request.RawUrl);

                var name = context.Request.QueryString.Get("name");
                var caseID = context.Request.QueryString.Get("caseID");
                var bytes = Utilities.ImageDetails(location + "\\" + caseID + "\\" + name);

                if (bytes == null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";

                    context.Response.ContentLength64 = bytes.Length;
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Close();
            }
        }

        private void HandleImageRequest(HttpListenerContext context, string location, string cache)
        {
            try
            {
                Console.WriteLine(context.Request.RawUrl);

                var name = context.Request.QueryString.Get("name");
                var caseID = context.Request.QueryString.Get("caseID");
                var level = Int32.Parse(context.Request.QueryString.Get("level"));
                var format = context.Request.QueryString.Get("format");

                var x = Int64.Parse(context.Request.QueryString.Get("x"));
                var y = Int64.Parse(context.Request.QueryString.Get("y"));
                var w = Int32.Parse(context.Request.QueryString.Get("w"));
                var h = Int32.Parse(context.Request.QueryString.Get("h"));

                Console.WriteLine("Image:" + name
                        + ", level:" + level.ToString()
                        + ", X:" + x.ToString() + ", Y:" + y.ToString()
                        + ", W:" + w.ToString() + ", H:" + h.ToString());

                if (format == null)
                {
                    format = "PNG";
                }

                Console.WriteLine("Format: " + format);

                if (w < 0 || h < 0 || w > 10000 || h > 10000 || level < 0)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.Close();
                }
                else
                {
                    var start = DateTime.Now;
                    Console.WriteLine("Start:" + start.ToString(" h:m:s.ff"));

                    var fileName = name
                            + "&x=" + x.ToString()
                            + "&y=" + y.ToString()
                            + "&w=" + w.ToString()
                            + "&h=" + h.ToString()
                            + "&level=" + level.ToString();

                    var f = (cache + fileName + "." + format).ToLower();

                    var bytes = Utilities.LoadFile(f);

                    if (bytes == null)
                    {
                        bytes = Utilities.CreateRegion(location + "\\" + caseID + "\\" + name, level, x, y, w, h, format);
                        if (bytes != null)
                        {
                            Console.WriteLine("File: " + f);
                            Utilities.SaveFile(f, bytes);
                        }
                    }
                           
                    var end = DateTime.Now;

                    Console.WriteLine("Time:" + (end - start).TotalMilliseconds.ToString() + " ms");

                    if (bytes == null)
                    {
                        Console.WriteLine("bytes == null");

                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.Close();
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.OK;

                        if (format == null || format.Equals("PNG"))
                        {
                            context.Response.ContentType = "image/png";
                        }
                        else if (format.Equals("RAW"))
                        {
                            context.Response.ContentType = "application/octet-stream";
                        }
                        else if (format.Equals("JPG"))
                        {
                            context.Response.ContentType = "image/jpeg";
                        }
                        else if (format.Equals("BMP"))
                        {
                            context.Response.ContentType = "image/bmp";
                        }

                        context.Response.ContentLength64 = bytes.Length;
                        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Close();
            }
        }
    }
}
