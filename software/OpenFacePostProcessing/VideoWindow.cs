// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace PSIPostProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using Gdk;
    using Microsoft.Psi;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Interop.Format;
    using Microsoft.Psi.Interop.Transport;
    using Microsoft.Psi.Media;

    public class VideoWindow : Gtk.Window
    {
        private Gtk.Image displayImage;

        private const string PSI_STORES_PATH = "/some/path/todo";
        private const int i = 0;
        private const string INPUT_PSI_STORE_NAME = "VideoRawData";
        private const string OUTPUT_PSI_STORE_NAME = "GazeBlinkData";

        private const int IMAGE_WIDTH = 960;
        private const int IMAGE_HEIGHT = 540;
        private byte[] imageData = new byte[IMAGE_WIDTH * IMAGE_HEIGHT * 3];

        private List<string> csvLines = new List<string>();

        private PSIConnectorSocket socket;
        private Pipeline pipeline;

        public VideoWindow()
            : base("Webcam")
        {
            // create the window widgets from the VideoWindow.xml resource using the builder
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PSIPostProcessing.VideoWindow.xml");
            using System.IO.StreamReader reader = new System.IO.StreamReader(stream);
            var builder = new Gtk.Builder();
            builder.AddFromString(reader.ReadToEnd());
            this.Add((Gtk.Widget)builder.GetObject("root"));

            // get the widgets which we will modify
            this.displayImage = (Gtk.Image)builder.GetObject("image");

            // window event handlers
            this.Shown += this.MainWindow_Shown;
            this.DeleteEvent += this.MainWindow_DeleteEvent;
        }

        private void MainWindow_Shown(object sender, EventArgs e)
        {
            // TODO generate first image, to allow OpenFacePSIConnector to calibrate

            // Open PSIConnector to receive connection from OpenFacePSIConnector
            this.socket = new PSIConnectorSocket(12345);
            Console.Write("Waiting for OpenFacePSIConnector process...");
            this.socket.WaitForConnection();
            Console.WriteLine(" Done.");

            var dataset = new Dataset("GazeBlinkDataset");
            var session = dataset.CreateSession("Session");
            var rawPartition = session.AddPsiStorePartition(INPUT_PSI_STORE_NAME, PSI_STORES_PATH + "/VideoRawData." + i.ToString("D4"), "RawVideoData");
            csvLines.Add("datetime,gazeX,gazeY,blink");
            dataset.CreateDerivedPartitionAsync(
                (pipeline, importer, exporter) =>
                {
                    var rawVideo = importer.OpenStream<Shared<Image>>("Webcam");
                    var processedVideo = rawVideo.Select((frame, e) =>
                    {
                        Console.WriteLine($"\nOriginating time: {e.OriginatingTime.TimeOfDay}");
                        return ComputeOpenFace(frame);
                    }, DeliveryPolicy.Throttle);
                    processedVideo.Select(t => t.gazeX).Write("GazeAngleX", exporter);
                    processedVideo.Select(t => t.gazeY).Write("GazeAngleY", exporter);
                    processedVideo.Select(t => t.blink).Write("Blink", exporter);
                    processedVideo
                        .Select((t, e) => e.OriginatingTime.ToString("yyyyMMddTHHmmss.fffffff") + "," + t.gazeX + "," + t.gazeY + "," + t.blink)
                        .Do(s => csvLines.Add(s));
                },
                OUTPUT_PSI_STORE_NAME,
                false,
                OUTPUT_PSI_STORE_NAME,
                PSI_STORES_PATH + "/GazeBlinkData." + i.ToString("D4"),
                ReplayDescriptor.ReplayAllRealTime
            );
        }

        private (float gazeX, float gazeY, int blink) ComputeOpenFace(Shared<Image> frame)
        {
            // Encode image to JPEG format
            // Windows webcam video: Width: 960, Height: 540, PixelFormat: BGR_24Bpp
            Image jpegFormat = frame.Resource.Convert(PixelFormat.BGRA_32bpp);
            EncodedImage encImg = new EncodedImage(jpegFormat.Width, jpegFormat.Height, PixelFormat.BGRA_32bpp);
            encImg.EncodeFrom(jpegFormat, new ImageToJpegStreamEncoder());
            jpegFormat.Dispose();

            // Write image to temporary file
            var stream = encImg.ToStream();
            var tempImageFile = System.IO.File.Create("temp.jpg");
            stream.Seek(0, SeekOrigin.Begin);
            stream.CopyTo(tempImageFile);
            tempImageFile.Dispose();
            stream.Close();
            stream.Dispose();
            encImg.Dispose();

            // Send command to compute gaze/blink data to OpenFacePSIConnector
            Console.WriteLine("OpenFacePSIConnector is computing...");
            this.socket.SendComputeCommand();
            this.socket.WaitForReady();

            // Extract gaze coordinates from generated csv file
            var lines = File.ReadLines("processed/temp.csv");
            var columnNames = lines.First().Split(',');
            int gazeAngleXIndex = Array.FindIndex(columnNames, cn => cn == "gaze_angle_x");
            int gazeAngleYIndex = gazeAngleXIndex + 1;
            int blinkIndicationIndex = Array.FindIndex(columnNames, cn => cn == "AU45_c");
            var csvData = lines.Reverse().Skip(1).First().Split(',');
            if (csvData[0] == "frame") // Don't process first frame, because we look second to last frame
                return (0.0f, 0.0f, 0);
            float gazeAngleX = float.Parse(csvData[gazeAngleXIndex], CultureInfo.InvariantCulture);
            float gazeAngleY = float.Parse(csvData[gazeAngleYIndex], CultureInfo.InvariantCulture);
            int blinkIndication = (int) float.Parse(csvData[blinkIndicationIndex], CultureInfo.InvariantCulture);
            Console.WriteLine("Gaze angle x:" + gazeAngleX + ". Gaze angle y: " + gazeAngleY);
            Console.WriteLine("Blinking: " + blinkIndication);

            // Draw frame to sync up visualization
            this.DrawFrame(frame);

            return (gazeAngleX, gazeAngleY, blinkIndication);
        }

        private void DrawFrame(Shared<Image> frame)
        {
            // copy the frame image to the pixel buffer
            var pixbuf = this.ImageToPixbuf(frame);

            // redraw on the UI thread
            Gtk.Application.Invoke(
                (sender, e) =>
                {
                    this.displayImage.Pixbuf = pixbuf;
                });
        }

        private Pixbuf ImageToPixbuf(Shared<Image> frame)
        {
            Image image = frame.Resource.Convert(PixelFormat.RGB_24bpp);
            var length = image.Stride * image.Height;
            if (this.imageData.Length != length)
            {
                this.imageData = new byte[length];
            }

            image.CopyTo(this.imageData);
            image.Dispose();
            return new Pixbuf(this.imageData, false, 8, image.Width, image.Height, image.Stride);
        }

        private void MainWindow_DeleteEvent(object o, Gtk.DeleteEventArgs args)
        {
            File.WriteAllLines(PSI_STORES_PATH + "/GazeBlinkData." + i.ToString("D4") + "/GazeBlinkData.csv", this.csvLines);
        }
    }
}
