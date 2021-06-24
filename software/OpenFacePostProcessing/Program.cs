// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace PSIPostProcessing
{
    using System;
    using System.IO;
    using System.Reflection;
    using Microsoft.Psi;
    using Microsoft.Psi.Imaging;

    class Program
    {
        static void Main(string[] args)
        {
            Gtk.Application.Init();
            InitializeCssStyles();
            var window = new VideoWindow();
            window.Destroyed += (sender, e) => Gtk.Application.Quit();
            window.ShowAll();
            Gtk.Application.Run();
        }

        private static void InitializeCssStyles()
        {
            var styleProvider = new Gtk.CssProvider();
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PSIPostProcessing.Styles.css");
            using StreamReader reader = new StreamReader(stream);

            styleProvider.LoadFromData(reader.ReadToEnd());
            Gtk.StyleContext.AddProviderForScreen(Gdk.Display.Default.DefaultScreen, styleProvider, Gtk.StyleProviderPriority.Application);
        }
    }
}
