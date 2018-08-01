using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Vlc.DotNet.Forms;

namespace MovieDesktop
{
  class Player : Form
  {
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr GetShellWindow();

    private VlcControl video;

    [STAThread]
    static void Main(string[] args)
    {
      if (args.Length < 1) throw new ArgumentException("No input file or url given");

      string videoSrc = args[0];

      int screenIdx = 0;
      if (args.Length == 2) Int32.TryParse(args[1], out screenIdx);


      //Application.EnableVisualStyles();
      Application.Run(new Player(videoSrc, screenIdx));
    }


    public Player(string videoSrc, int screenIdx = 0)
    {
      var desktop = GetShellWindow();

      if(desktop == IntPtr.Zero)
      {
        Console.Error.WriteLine("Desktop process not found.");
        throw new Exception("Desktop process not found.");
      }

      var vlcPath = new DirectoryInfo("C:\\Program Files\\VideoLAN\\VLC");
      if (!vlcPath.Exists || !Environment.Is64BitProcess)
      {
        vlcPath = new DirectoryInfo("C:\\Program Files (x86)\\VideoLAN\\VLC");

        if (!vlcPath.Exists)
        {
          throw new EntryPointNotFoundException("VLC not found on your system.");
        }
      }


      // If non-url given, check if local file exists
      if (!videoSrc.StartsWith("http"))
      {
        FileInfo file = new FileInfo(videoSrc);
        if (!file.Exists)
        {
          Console.Error.WriteLine("Doesn't exist dude");
          throw new FileNotFoundException("Video file doesn't exist!");
        }

        // Convert to file:// format
        videoSrc = new Uri(file.FullName).AbsoluteUri;
      }


      /// Forms part
      Text = "Fullscreen desktop movie";
      FormBorderStyle = FormBorderStyle.None;

      /// Video part
      video = new VlcControl
      {
        VlcLibDirectory = vlcPath
      };
      video.EndInit();

      // Loop infinitely
      video.SetMedia(videoSrc, new string[]{"input-repeat=65535"});

      // No audio
      video.Audio.IsMute = true;

      // Error handling
      video.EncounteredError += (sender, e) =>
      {
        Console.Error.Write("An error occurred - " + e);
        throw new Exception(e.ToString());
      };

      // Add to main form
      Controls.Add(video);


      /// Screen part
      // Try to get preferred screen, otherwise just get first
      var screen = screenIdx < Screen.AllScreens.Count() ? Screen.AllScreens[screenIdx] : Screen.AllScreens.First();

      video.Width = Width = screen.WorkingArea.Width;
      video.Height = Height = screen.WorkingArea.Height;

      // Desktop Window has [0,0] as top left screen.. get top and left for selected screen
      int top = 0;
      int left = 0;
      foreach (Screen s in Screen.AllScreens.Where(s => s != screen))
      {
        top += Math.Max(0, -s.WorkingArea.Top);
        left += Math.Max(0, -s.WorkingArea.Left);
      }

      Load += new EventHandler((s, e) =>
      {
        Top = top;
        Left = left;
        SetParent(Handle, desktop);

        // Play video
        video.Play();
      });

    }

  }
}
